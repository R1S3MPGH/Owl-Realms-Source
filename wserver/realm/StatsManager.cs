﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using wServer.realm.entities;
using wServer.realm.entities.player;

#endregion

namespace wServer.realm
{
    internal class StatsManager
    {
        private readonly Player player;

        public StatsManager(Player player)
        {
            this.player = player;
        }

        //from wiki

        private int GetStats(int id)
        {
            return player.Stats[id] + player.Boost[id];
        }

        public float GetAttackDamage(int min, int max)
        {
            var att = GetStats(2);
            if (player.HasConditionEffect(ConditionEffects.Paralyzed))
                att = 0;

            var ret = player.Random.Next(min, max)*(0.5f + att/50f);

            if (player.HasConditionEffect(ConditionEffects.Damaging))
                ret *= 1.5f;

            return ret;
        }

        public static float GetDefenseDamage(Entity host, int dmg, int def)
        {
            if (host.HasConditionEffect(ConditionEffects.Armored))
                def *= 2;
            if (host.HasConditionEffect(ConditionEffects.ArmorBroken))
                def = 0;

            var limit = dmg*0.15f;

            float ret;
            if (dmg - def < limit) ret = limit;
            else ret = dmg - def;

            if (host.HasConditionEffect(ConditionEffects.Invulnerable) ||
                host.HasConditionEffect(ConditionEffects.Invincible))
                ret = 0;
            return ret;
        }

        public float GetDefenseDamage(int dmg, bool noDef)
        {
            var def = GetStats(3);
            if (player.HasConditionEffect(ConditionEffects.Armored))
                def *= 2;
            if (player.HasConditionEffect(ConditionEffects.ArmorBroken) ||
                noDef)
                def = 0;

            var limit = dmg*0.15f;

            float ret;
            if (dmg - def < limit) ret = limit;
            else ret = dmg - def;

            if (player.HasConditionEffect(ConditionEffects.Invulnerable) ||
                player.HasConditionEffect(ConditionEffects.Invincible))
                ret = 0;
            return ret;
        }

        public static float GetSpeed(Entity entity, float stat)
        {
            var ret = 4 + 5.6f*(stat/75f);
            if (entity.HasConditionEffect(ConditionEffects.Speedy))
                ret *= 1.5f;
            if (entity.HasConditionEffect(ConditionEffects.Slowed))
                ret = 4;
            if (entity.HasConditionEffect(ConditionEffects.Paralyzed))
                ret = 0;
            return ret;
        }

        public float GetSpeed()
        {
            return GetSpeed(player, GetStats(4));
        }

        public float GetHPRegen()
        {
            var vit = GetStats(5);
            if (player.HasConditionEffect(ConditionEffects.Sick))
                vit = 0;
            return 1 + vit/8f;
        }

        public float GetMPRegen()
        {
            var wis = GetStats(6);
            if (player.HasConditionEffect(ConditionEffects.Quiet))
                return 0;
            return 0.8f + wis/16.7f;
        }

        public float GetDex()
        {
            var dex = GetStats(7);
            if (player.HasConditionEffect(ConditionEffects.Dazed))
                dex = 0;

            var ret = 1.5f + 6.5f*(dex/75f);
            if (player.HasConditionEffect(ConditionEffects.Berserk))
                ret *= 1.5f;
            if (player.HasConditionEffect(ConditionEffects.Stunned))
                ret = 0;
            return ret;
        }

        public static int StatsNameToIndex(string name)
        {
            switch (name)
            {
                case "MaxHitPoints":
                    return 0;
                case "MaxMagicPoints":
                    return 1;
                case "Attack":
                    return 2;
                case "Defense":
                    return 3;
                case "Speed":
                    return 4;
                case "HpRegen":
                    return 5;
                case "MpRegen":
                    return 6;
                case "Dexterity":
                    return 7;
            }
            return -1;
        }

        public static string StatsIndexToName(int index)
        {
            switch (index)
            {
                case 0:
                    return "MaxHitPoints";
                case 1:
                    return "MaxMagicPoints";
                case 2:
                    return "Attack";
                case 3:
                    return "Defense";
                case 4:
                    return "Speed";
                case 5:
                    return "HpRegen";
                case 6:
                    return "MpRegen";
                case 7:
                    return "Dexterity";
            }
            return null;
        }

        public static string StatsIndexToPotName(int index)
        {
            switch (index)
            {
                case 0:
                    return "Life";
                case 1:
                    return "Mana";
                case 2:
                    return "Attack";
                case 3:
                    return "Defense";
                case 4:
                    return "Speed";
                case 5:
                    return "Vitality";
                case 6:
                    return "Wisdom";
                case 7:
                    return "Dexterity";
                case 8:
                    return "Swagger";
            }
            return null;
        }
    }
}