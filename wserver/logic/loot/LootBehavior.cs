﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using wServer.realm;
using wServer.realm.entities;
using wServer.realm.entities.player;

#endregion

namespace wServer.logic.loot
{
    internal class LootBehavior : ConditionalBehavior
    {
        private static readonly Random rand = new Random();

        public LootBehavior(
            LootDef pub,
            params Tuple<int, LootDef>[] soul)
        {
            PublicBag = pub ?? LootDef.Empty;
            SoulBag = soul;
        }

        public LootDef PublicBag { get; private set; }
        public Tuple<int, LootDef>[] SoulBag { get; private set; }

        public override BehaviorCondition Condition
        {
            get { return BehaviorCondition.OnDeath; }
        }

        private void ShowBags(Random rand, IEnumerable<Item> loots, Player owner)
        {
            var bagType = 0;
            var items = new Item[8];
            var idx = 0;

            short bag;
            Container container;
            foreach (var i in loots)
            {
                if (i.BagType > bagType) bagType = i.BagType;
                items[idx] = i;
                idx++;

                if (idx == 8)
                {
                    bag = 0x0500;
                    switch (bagType)
                    {
                        case 0:
                            bag = 0x0500;
                            break;
                        case 1:
                            bag = 0x0503;
                            break;
                        case 2:
                            bag = 0x0507;
                            break;
                        case 3:
                            bag = 0x0508;
                            break;
                        case 4:
                            bag = 0x0509;
                            break;
                        case 5:
                            bag = 0xffd;
                            break;
                        case 6:
                            bag = 0xffe;
                            break;
                        case 7:
                            bag = 0xfff;
                            break;
                    }
                    container = new Container(bag, 1000*60, true);
                    for (var j = 0; j < 8; j++)
                        container.Inventory[j] = items[j];
                    container.BagOwner = owner == null ? (int?) null : owner.AccountId;
                    container.Move(
                        Host.Self.X + (float) ((rand.NextDouble()*2 - 1)*0.5),
                        Host.Self.Y + (float) ((rand.NextDouble()*2 - 1)*0.5));
                    container.Size = 80;
                    Host.Self.Owner.EnterWorld(container);

                    bagType = 0;
                    items = new Item[8];
                    idx = 0;
                }
            }

            if (idx > 0)
            {
                bag = 0x0500;
                switch (bagType)
                {
                    case 0:
                        bag = 0x0500;
                        break;
                    case 1:
                        bag = 0x0503;
                        break;
                    case 2:
                        bag = 0x0507;
                        break;
                    case 3:
                        bag = 0x0508;
                        break;
                    case 4:
                        bag = 0x0509;
                        break;
                    case 5:
                        bag = 0xffd;
                        break;
                    case 6:
                        bag = 0xffe;
                        break;
                    case 7:
                        bag = 0xfff;
                        break;
                }
                container = new Container(bag, 1000*60, true);
                for (var j = 0; j < idx; j++)
                    container.Inventory[j] = items[j];
                container.BagOwner = owner == null ? (int?) null : owner.AccountId;
                container.Move(
                    Host.Self.X + (float) ((rand.NextDouble()*2 - 1)*0.5),
                    Host.Self.Y + (float) ((rand.NextDouble()*2 - 1)*0.5));
                container.Size = 80;
                if (Host.Self.Owner != null) //was null sometimes
                {
                    Host.Self.Owner.EnterWorld(container);
                }
            }
        }

        private void ProcessPublicBags(Random rand, Tuple<Player, int>[] dat)
        {
            var lootCount = PublicBag.BaseLootCount + PublicBag.PersonMultiplier*dat.Length;
            if (lootCount < PublicBag.MinLootCount) lootCount = PublicBag.MinLootCount;
            if (lootCount > PublicBag.MaxLootCount) lootCount = PublicBag.MaxLootCount;

            var loots = new HashSet<Item>();
            var pots = new List<Item>();
            for (var i = 0;
                i < lootCount ||
                (loots.Count < PublicBag.MinLootCount &&
                 pots.Count < PublicBag.MinLootCount);
                i++)
            {
                var loot = PublicBag.GetRandomLoot(rand);
                if (loot != null)
                {
                    if (loot.Potion)
                        pots.Add(loot);
                    else
                        loots.Add(loot);
                }
            }
            ShowBags(rand, loots.Concat(pots), null);
        }

        private void ProcessSoulBags(Random rand, Tuple<Player, int>[] dat)
        {
            var items = new Dictionary<Player, HashSet<Item>>();
            foreach (var i in dat)
                items.Add(i.Item1, new HashSet<Item>());

            foreach (var i in SoulBag)
            {
                var eligiblePlayers = dat
                    .Where(_ => _.Item2 > i.Item1)
                    .OrderByDescending(_ => _.Item2)
                    .ToArray();

                var lootCount = i.Item2.BaseLootCount + i.Item2.PersonMultiplier*eligiblePlayers.Length;

                var loots = new List<Item>();
                for (var j = 0;
                    (j < lootCount || loots.Count < i.Item2.MinLootCount) &&
                    loots.Count < i.Item2.MaxLootCount;
                    j++)
                {
                    var loot = i.Item2.GetRandomLoot(rand);
                    if (loot != null) loots.Add(loot);
                }
                int idx = 0, q = -1;
                for (var j = 0; j < loots.Count;) //Give loots rounds
                {
                    if (items[eligiblePlayers[idx].Item1].Add(loots[j]) ||
                        idx == q)
                    {
                        j++;
                        q = -1;
                    }
                    else if (q == -1)
                        q = idx;

                    idx++;
                    if (idx == eligiblePlayers.Length) idx = 0;
                }
            }
            foreach (var i in items)
                if (i.Value.Count > 0)
                    ShowBags(rand, i.Value, i.Key);
        }

        protected override void BehaveCore(BehaviorCondition cond, RealmTime? time, object state)
        {
            if (cond == BehaviorCondition.OnDeath)
            {
                if (!Host.Self.BagDropped)
                {
                    var counter = (Host as Enemy).DamageCounter;
                    var dat = counter.GetPlayerData();
                    var items = new Dictionary<Player, List<Item>>();
                    ProcessPublicBags(rand, dat);
                    if (Host.Self.Owner.Name == "Battle Arena")
                    {
                        if (rand.Next(1, 5) == 1)
                        {
                            ProcessSoulBags(rand, dat);
                        }
                    }
                    else if (Host.Self.Owner.Name == "Free Battle Arena")
                    {
                        if (rand.Next(1, 15) == 1)
                        {
                            ProcessSoulBags(rand, dat);
                        }
                    }
                    else
                        ProcessSoulBags(rand, dat);
                    Host.Self.BagDropped = true;
                }
            }
        }
    }
}