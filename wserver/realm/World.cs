﻿#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using db.data;
using log4net;
using terrain;
using wServer.realm.entities;
using wServer.realm.entities.player;

#endregion

namespace wServer.realm
{
    public abstract class World
    {
        public const int TUT_ID = -1;
        public const int NEXUS_ID = -2;
        public const int RAND_REALM = -3;
        public const int NEXUS_LIMBO = -4;
        public const int VAULT_ID = -5;
        public const int TEST_ID = -6;
        public const int GAUNTLET = -7;
        public const int WC = -8;
        public const int ARENA = -9;
        public const int SHOP = -10;
        public const int GHALL = -11;
        public const int MARKET = -12;
        public const int ARENA_FREE = -13;
        public const int ARENA_PAID = -14;
//        public const int LAIROFDRACONIS = -25;
        public const int ADM_ID = -60;
        public const int ABYSS = -40;
        public const int DRACONIS = -39;
        public const int OCEAN = -16;
        public const int TOMBANCIENTS = -17;
        public const int SPRITEWORLD = -18;
        public const int ZOMBIEARENA = -19;
        public const int UNDEADLAIR = -20;
        public const int KITCHEN = -21;
        public const int ISLAND = -22;
        public const int PIRATECAVE = -23;
        public const int VOIDWORLD = -24;
        public const int BEACHZONE = -38;
        public const int SNAKEPIT = -26;
        public const int MANOR = -27;
        public const int MADLAB = -28;
        public const int SECRET = -29;
        public const int SPIDERDEN = -30;
        public const int DAVYJ = -31;
        public const int DRAGONLORDS = -32;
        public const int DOOMCRYSTAL = -33;
        public const int DOOMCELLAR = -34;
        public const int FJUNGLE = -35;
        public const int ELDERPCAVE = -37;
        public const int OCASTLE = -41;
        public const int OCHAMBER = -42;
        public const int CHOCHAMBER = -43;
        public const int ELDERSDEN = -44;
        public const int ELDERSPIT = -45;
        public const int ELDERSPRITE = -46;
        public const int ELDERUNDEAD = -47;
        public const int ELDERABYSS = -48;
        public const int ELDERDRACONIS = -49;
        public const int ELDERTOMB = -50;
        public const int NEXUSDEF = -51;
   //     public const int SHATTERS = -52;
        public const int EC = -53;
        private static readonly ILog log = LogManager.GetLogger(typeof (World));
        public string ExtraVar = "Default";
        private int entityInc;
        private RealmManager manager;

        protected World()
        {
            Players = new ConcurrentDictionary<int, Player>();
            Enemies = new ConcurrentDictionary<int, Enemy>();
            Quests = new ConcurrentDictionary<int, Enemy>();
            Pets = new ConcurrentDictionary<int, Entity>();
            Projectiles = new ConcurrentDictionary<Tuple<int, byte>, Projectile>();
            StaticObjects = new ConcurrentDictionary<int, StaticObject>();
            Timers = new List<WorldTimer>();
            ClientXML = ExtraXML = Empty<string>.Array;
            Map = new Wmap();
            AllowTeleport = true;
            ShowDisplays = true;

            ExtraXML = XmlDatas.ServerXmls.ToArray();
        }

        public bool IsLimbo { get; protected set; }

        public RealmManager Manager
        {
            get { return manager; }
            internal set
            {
                manager = value;
                if (manager != null)
                    Init();
            }
        }

        public int Id { get; internal set; }
        public string Name { get; protected set; }

        public ConcurrentDictionary<int, Player> Players { get; private set; }
        public ConcurrentDictionary<int, Enemy> Enemies { get; private set; }
        public ConcurrentDictionary<int, Entity> Pets { get; private set; }
        public ConcurrentDictionary<Tuple<int, byte>, Projectile> Projectiles { get; private set; }
        public ConcurrentDictionary<int, StaticObject> StaticObjects { get; private set; }
        public List<WorldTimer> Timers { get; private set; }
        public int Background { get; protected set; }

        public CollisionMap<Entity> EnemiesCollision { get; private set; }
        public CollisionMap<Entity> PlayersCollision { get; private set; }
        public byte[,] Obstacles { get; private set; }

        public bool AllowTeleport { get; protected set; }
        public bool ShowDisplays { get; protected set; }
        public string[] ClientXML { get; protected set; }
        public string[] ExtraXML { get; protected set; }

        public Wmap Map { get; private set; }
        public ConcurrentDictionary<int, Enemy> Quests { get; private set; }

        public virtual World GetInstance(ClientProcessor psr)
        {
            return null;
        }

        public bool IsPassable(int x, int y)
        {
            var tile = Map[x, y];
            ObjectDesc desc;
            if (XmlDatas.TileDescs[tile.TileId].NoWalk)
                return false;
            if (XmlDatas.ObjectDescs.TryGetValue(tile.ObjType, out desc))
            {
                if (!desc.Static)
                    return false;
            }
            return true;
        }

        public int GetNextEntityId()
        {
            return Interlocked.Increment(ref entityInc);
        }

        public bool Delete()
        {
            lock (this)
            {
                if (Players.Count > 0) return false;
                Id = 0;
            }
            Map = null;
            Players = null;
            Enemies = null;
            Projectiles = null;
            StaticObjects = null;
            return true;
        }

        public virtual void BehaviorEvent(string type)
        {
        }

        protected virtual void Init()
        {
        }
        
        protected void FromWorldMap(Stream dat)
        {
            
            log.InfoFormat("Loading map for world {0}({1})...", Id, Name);
            
            var map = new Wmap();
            Map = map;
            entityInc = 0;
            entityInc += Map.Load(dat, 0);

            int w = Map.Width, h = Map.Height;
            Obstacles = new byte[w, h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var tile = Map[x, y];
                    ObjectDesc desc;
                    if (XmlDatas.TileDescs[tile.TileId].NoWalk)
                        Obstacles[x, y] = 3;
                    if (XmlDatas.ObjectDescs.TryGetValue(tile.ObjType, out desc))
                    {
                        if (desc.Class == "Wall" ||
                            desc.Class == "ConnectedWall" ||
                            desc.Class == "CaveWall")
                            Obstacles[x, y] = 2;
                        else if (desc.OccupySquare || desc.EnemyOccupySquare)
                            Obstacles[x, y] = 1;
                    }
                }
            EnemiesCollision = new CollisionMap<Entity>(0, w, h);
            PlayersCollision = new CollisionMap<Entity>(1, w, h);

            Projectiles.Clear();
            StaticObjects.Clear();
            Enemies.Clear();
            Players.Clear();
            foreach (var i in Map.InstantiateEntities(Manager))
            {
                if (i.ObjectDesc != null &&
                    (i.ObjectDesc.OccupySquare || i.ObjectDesc.EnemyOccupySquare))
                    Obstacles[(int) (i.X - 0.5), (int) (i.Y - 0.5)] = 2;
                EnterWorld(i);
            }
        }
        
        public void FromJsonMap(string file)
        {
            if (File.Exists(file))
            {
                var wmap = Json2Wmap.Convert(File.ReadAllText(file));

                FromWorldMap(new MemoryStream(wmap));
            }
            else
            {
                throw new FileNotFoundException("Json file not found!", file);
            }
        }

        public void FromJsonStream(Stream dat)
        {
            byte[] data = {};
            dat.Read(data, 0, (int) dat.Length);
            var json = Encoding.ASCII.GetString(data);
            var wmap = Json2Wmap.Convert(json);
            FromWorldMap(new MemoryStream(wmap));
        } //not working

        public virtual int EnterWorld(Entity entity)
        {
            if (entity is Player)
            {
                entity.Id = GetNextEntityId();
                entity.Init(this);
                Players.TryAdd(entity.Id, entity as Player);
                PlayersCollision.Insert(entity);
            }
            else if (entity is Enemy)
            {
                entity.Id = GetNextEntityId();
                entity.Init(this);
                Enemies.TryAdd(entity.Id, entity as Enemy);
                EnemiesCollision.Insert(entity);
                if (entity.ObjectDesc.Quest)
                    Quests.TryAdd(entity.Id, entity as Enemy);

                if (entity.isPet)
                {
                    Pets.TryAdd(entity.Id, entity);
                }
            }
            else if (entity is Projectile)
            {
                entity.Init(this);
                var prj = entity as Projectile;
                Projectiles[new Tuple<int, byte>(prj.ProjectileOwner.Self.Id, prj.ProjectileId)] = prj;
            }
            else if (entity is StaticObject)
            {
                entity.Id = GetNextEntityId();
                entity.Init(this);
                StaticObjects.TryAdd(entity.Id, entity as StaticObject);
                if (entity is Decoy)
                    PlayersCollision.Insert(entity);
                else
                    EnemiesCollision.Insert(entity);
            }
            return entity.Id;
        }

        public virtual void LeaveWorld(Entity entity)
        {
            if (entity is Player)
            {
                Player dummy;
                Players.TryRemove(entity.Id, out dummy);
                PlayersCollision.Remove(entity);
            }
            else if (entity is Enemy)
            {
                Enemy dummy;
                Enemies.TryRemove(entity.Id, out dummy);
                EnemiesCollision.Remove(entity);
                if (entity.ObjectDesc.Quest)
                    Quests.TryRemove(entity.Id, out dummy);
                if (entity.isPet)
                {
                    Entity dummy2;
                    Pets.TryRemove(entity.Id, out dummy2);
                }
            }
            else if (entity is Projectile)
            {
                var p = entity as Projectile;
                Projectiles.TryRemove(new Tuple<int, byte>(p.ProjectileOwner.Self.Id, p.ProjectileId), out p);
            }
            else if (entity is StaticObject)
            {
                StaticObject dummy;
                StaticObjects.TryRemove(entity.Id, out dummy);
                if (entity is Decoy)
                    PlayersCollision.Remove(entity);
                else
                    EnemiesCollision.Remove(entity);
            }
            entity.Owner = null;
        }

        public Entity GetEntity(int id)
        {
            Player ret1;
            if (Players.TryGetValue(id, out ret1)) return ret1;
            Enemy ret2;
            if (Enemies.TryGetValue(id, out ret2)) return ret2;
            StaticObject ret3;
            if (StaticObjects.TryGetValue(id, out ret3)) return ret3;
            return null;
        }

        public Player GetUniqueNamedPlayer(string name)
        {
            foreach (var i in Players)
            {
                if (i.Value.NameChosen && i.Value.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    return i.Value;
            }
            return null;
        }

        public Player GetUniqueNamedPlayerRough(string name)
        {
            foreach (var i in Players)
            {
                if (i.Value.CompareName(name))
                    return i.Value;
            }
            return null;
        }

        public void BroadcastPacket(Packet pkt, Player exclude)
        {
            foreach (var i in Players)
                if (i.Value != exclude)
                    i.Value.Client.SendPacket(pkt);
        }

        public void BroadcastPackets(IEnumerable<Packet> pkts, Player exclude)
        {
            foreach (var i in Players)
                if (i.Value != exclude)
                    i.Value.Client.SendPackets(pkts);
        }

        public virtual void Tick(RealmTime time)
        {
            if (IsLimbo) return;

            for (var i = 0; i < Timers.Count; i++)
                if (Timers[i].Tick(this, time))
                {
                    Timers.RemoveAt(i);
                    i--;
                }

            foreach (var i in Players)
                i.Value.Tick(time);

            if (EnemiesCollision != null)
            {
                foreach (var i in EnemiesCollision.GetActiveChunks(PlayersCollision))
                    i.Tick(time);
                foreach (var i in StaticObjects.Where(x => x.Value is Decoy))
                    i.Value.Tick(time);
            }
            else
            {
                foreach (var i in Enemies)
                    i.Value.Tick(time);
                foreach (var i in StaticObjects)
                    i.Value.Tick(time);
            }
            foreach (var i in Projectiles)
                i.Value.Tick(time);
        }
    }
}