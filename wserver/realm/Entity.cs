﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using db;
using db.data;
using wServer.logic;
using wServer.realm.entities;
using wServer.realm.entities.player;
using wServer.svrPackets;

#endregion

namespace wServer.realm
{
    public class Entity : IBehaviorHost, IProjectileOwner, ICollidable<Entity>
    {
        private const int EFFECT_COUNT = 28;
        private readonly ObjectDesc desc;
        private readonly int[] effects;
        private readonly bool interactive;
        private readonly Position[] posHistory;
        private readonly Projectile[] projectiles;
        public bool BagDropped;
        public Player PlayerOwner;
        public string State = "idle";
        public string SubState = "1";
        public TagList Tags;
        public bool isPet;
        public bool isSummon;
        private Dictionary<int, object> oncestates;
        private byte posIdx;
        protected byte projectileId;
        private Dictionary<int, object> states;
        private bool tickingEffects;

        public Entity(short objType)
            : this(objType, true)
        {
        }

        protected Entity(short objType, bool interactive)
        {
            this.interactive = interactive;
            ObjectType = objType;
            Name = "";
            Size = 100;
            Usable = false;
            BagDropped = false;
            isPet = false;
            isSummon = false;
            XmlDatas.ObjectDescs.TryGetValue(objType, out desc);
            BehaviorDb.ResolveBehavior(this);

            if (interactive)
            {
                posHistory = new Position[256];
                projectiles = new Projectile[256];
                effects = new int[EFFECT_COUNT];
            }
            if (objType == 0x072f)
                Usable = true;

            if (ObjectDesc != null)
            {
                Tags = desc.Tags;
            }
        }

        public RealmManager Manager { get; private set; }


        public ObjectDesc ObjectDesc
        {
            get { return desc; }
        }

        public World Owner { get; internal set; }

        public int UpdateCount { get; set; }

        public short ObjectType { get; private set; }
        public int Id { get; internal set; }

        public bool Usable { get; set; }


        //Stats
        public string Name { get; set; }
        public string nName { get; set; }
        public int Size { get; set; }
        public ConditionEffects ConditionEffects { get; set; }
        public Behavior AttackBehavior { get; set; }
        public Behavior MovementBehavior { get; set; }
        public Behavior ReproduceBehavior { get; set; }

        public Behavior[] Behaves
        {
            get { return new[] {AttackBehavior, MovementBehavior, ReproduceBehavior}; }
        }

        public ConditionalBehavior[] CondBehaviors { get; set; }

        IDictionary<int, object> IBehaviorHost.StateStorage
        {
            get
            {
                if (states == null) states = new Dictionary<int, object>();
                return states;
            }
        }

        IDictionary<int, object> IBehaviorHost.StateTable
        {
            get
            {
                if (oncestates == null) oncestates = new Dictionary<int, object>();
                return oncestates;
            }
        }

        Entity IBehaviorHost.Self
        {
            get { return this; }
        }

        public float X { get; private set; }
        public float Y { get; private set; }
        public CollisionNode<Entity> CollisionNode { get; set; }
        public CollisionMap<Entity> Parent { get; set; }

        Entity IProjectileOwner.Self
        {
            get { return this; }
        }

        Projectile[] IProjectileOwner.Projectiles
        {
            get { return projectiles; }
        }

        public Entity Move(float x, float y)
        {
            if (Owner != null && !(this is Projectile) &&
                (!(this is StaticObject) || (this as StaticObject).Hittestable))
                (this is Enemy ? Owner.EnemiesCollision : Owner.PlayersCollision)
                    .Move(this, x, y);
            X = x;
            Y = y;
            return this;
        }


        protected virtual void ImportStats(StatsType stats, object val)
        {
            if (stats == StatsType.Name) Name = (string) val;
            else if (stats == StatsType.Size) Size = (int) val;
            else if (stats == StatsType.Effects) ConditionEffects = (ConditionEffects) (int) val;
        }

        protected virtual void ExportStats(IDictionary<StatsType, object> stats)
        {
            stats[StatsType.Name] = Name;
            stats[StatsType.Size] = Size;
            stats[StatsType.Effects] = (int) ConditionEffects;
        }

        public void FromDefinition(ObjectDef def)
        {
            ObjectType = def.ObjectType;
            ImportStats(def.Stats);
        }

        public void ImportStats(ObjectStats stat)
        {
            Id = stat.Id;
            (this is Enemy ? Owner.EnemiesCollision : Owner.PlayersCollision)
                .Move(this, stat.Position.X, stat.Position.Y);
            X = stat.Position.X;
            Y = stat.Position.Y;
            foreach (var i in stat.Stats)
                ImportStats(i.Key, i.Value);
            UpdateCount++;
        }

        public ObjectStats ExportStats()
        {
            var stats = new Dictionary<StatsType, object>();
            ExportStats(stats);
            return new ObjectStats
            {
                Id = Id,
                Position = new Position {X = X, Y = Y},
                Stats = stats.ToArray()
            };
        }

        public ObjectDef ToDefinition()
        {
            return new ObjectDef
            {
                ObjectType = ObjectType,
                Stats = ExportStats()
            };
        }

        public virtual void Init(World owner)
        {
            Owner = owner;
        }

        public virtual void Tick(RealmTime time)
        {
            if (this is Projectile) return;
            if (interactive && Owner != null)
            {
                if (!HasConditionEffect(ConditionEffects.Stasis))
                {
                    MovementBehavior.Tick(this, time);
                    AttackBehavior.Tick(this, time);
                    ReproduceBehavior.Tick(this, time);
                }
                foreach (var i in CondBehaviors)
                    if ((i.Condition & BehaviorCondition.Other) != 0 &&
                        i.ConditionMeet(this))
                        i.Behave(BehaviorCondition.Other, this, time, null);
                posHistory[posIdx++] = new Position {X = X, Y = Y};
                ProcessConditionEffects(time);
            }
        }

        public Position? TryGetHistory(long timeAgo)
        {
            if (posHistory == null) return null;
            var tickPast = timeAgo*LogicTicker.TPS/1000;
            if (tickPast > 255) return null;
            return posHistory[(byte) (posIdx - (byte) tickPast)];
        }


        /* Projectile
* Sign
* Wall
* ConnectedWall
* MoneyChanger
* CharacterChanger
* Stalagmite
* NameChanger
* GuildRegister
* GuildChronicle
* GuildBoard
* CaveWall
* Player
* Dye
* ClosedVaultChest
* ClosedVaultChestGold
* Merchant
* GuildHallPortal
* SpiderWeb
* GuildMerchant
* Portal
* Equipment
* Container
* GameObject
* Character
*/

        public static Entity Resolve(short id)
        {
            var tr = new Random();
            var node = XmlDatas.TypeToElement[id];
            var type = node.Element("Class").Value;
            switch (type)
            {
                case "Projectile":
                    throw new Exception("Projectile should not instantiated using Entity.Resolve");
                case "Sign":
                    return new Sign(id);
                case "Wall":
                    return new Wall(id, node);
                case "ConnectedWall":
                case "CaveWall":
                    return new ConnectedObject(id);
                case "GameObject":
                case "CharacterChanger":
                case "MoneyChanger":
                case "NameChanger":
                case "Alchemy":
                    return new StaticObject(id, StaticObject.GetHP(node), true, false, true);
                case "GuildRegister":
                case "GuildChronicle":
                case "GuildBoard":
                    return new StaticObject(id, null, false, false, false);
                case "Container":
                    return new Container(node);
                case "Player":
                    throw new Exception("Player should not instantiated using Entity.Resolve");
                case "Character": //Other characters means enemy
                    return new Enemy(id);
                case "Portal":
                    return new Portal(id, null);
                case "ClosedVaultChest":
                    // Has to be empty!
                case "ClosedVaultChestGold":
                    // Has to be empty!
                case "VaultChest":
                    // Has to be empty!
                case "SlotsMachine":
                    // Has to be empty!
                case "Merchant":
                    return new Merchants(id);
                case "GuildMerchant":
                    return new GuildMerchant(id);
                case "GuildHallPortal":
                    return new StaticObject(id, null, false, false, false);
                default:
                    Console.WriteLine("Not supported type: " + type);
                    return new Entity(id);
            }
        }

        public Projectile CreateProjectile(ProjectileDesc desc, short container, int dmg, long time, Position pos,
            float angle)
        {
            var ret = new Projectile(desc) //Assume only one
            {
                ProjectileOwner = this,
                ProjectileId = projectileId++,
                Container = container,
                Damage = dmg,
                BeginTime = time,
                BeginPos = pos,
                Angle = angle,
                X = pos.X,
                Y = pos.Y
            };
            if (projectiles[ret.ProjectileId] != null)
                projectiles[ret.ProjectileId].Destroy(true);
            projectiles[ret.ProjectileId] = ret;
            return ret;
        }

        public virtual bool HitByProjectile(Projectile projectile, RealmTime time)
        {
            //Console.WriteLine("HIT! " + Id);
            if (ObjectDesc == null)
                return true;
            return ObjectDesc.OccupySquare || ObjectDesc.EnemyOccupySquare || ObjectDesc.FullOccupy;
        }

        public virtual void ProjectileHit(Projectile projectile, Entity target)
        {
        }


        private void ProcessConditionEffects(RealmTime time)
        {
            if (effects == null || !tickingEffects) return;

            ConditionEffects newEffects = 0;
            tickingEffects = false;
            for (var i = 0; i < effects.Length; i++)
                if (effects[i] > 0)
                {
                    effects[i] -= time.thisTickTimes;
                    if (effects[i] > 0)
                        newEffects |= (ConditionEffects) (1 << i);
                    else
                        effects[i] = 0;
                    tickingEffects = true;
                }
                else if (effects[i] != 0)
                    newEffects |= (ConditionEffects) (1 << i);
            if (newEffects != ConditionEffects)
            {
                ConditionEffects = newEffects;
                UpdateCount++;
            }
        }

        public bool HasConditionEffect(ConditionEffects eff)
        {
            return (ConditionEffects & eff) != 0;
        }

        public void ApplyConditionEffect(params ConditionEffect[] effs)
        {
            foreach (var i in effs)
            {
                if (i.Effect == ConditionEffectIndex.Stunned &&
                    HasConditionEffect(ConditionEffects.StunImmume))
                    continue;
                if (i.Effect == ConditionEffectIndex.Stasis &&
                    HasConditionEffect(ConditionEffects.StasisImmune))
                    continue;
                effects[(int) i.Effect] = i.DurationMS;
                if (i.DurationMS != 0)
                    ConditionEffects |= (ConditionEffects) (1 << (int) i.Effect);
            }
            tickingEffects = true;
            UpdateCount++;
        }
    }
}