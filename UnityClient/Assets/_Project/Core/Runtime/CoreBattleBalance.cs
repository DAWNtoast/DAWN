using System;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreBossKind { None, Route, Treasure, Legendary }
    public enum CoreBattleArchetype { Balanced, Ironclad, Artillery, Raider, Ember, Tempest }

    /// <summary>Immutable, explicit encounter stats. Boss power labels never substitute for combat tuning.</summary>
    public sealed class CoreBattleProfile
    {
        public CoreBattleProfile(CoreBossKind bossKind, CoreBattleArchetype archetype,
            int enemyHull, int cannonDamage, int meleeDamage, int guard, int initialGuard,
            int ordinaryCrewCount, float bowQuality, float hullQuality,
            double ramChance = 0d, float ramRangeMeters = 100f)
        {
            BossKind = bossKind;
            Archetype = archetype;
            EnemyHull = Math.Max(1, enemyHull);
            CannonDamage = Math.Max(0, cannonDamage);
            MeleeDamage = Math.Max(0, meleeDamage);
            Guard = Math.Max(0, guard);
            InitialGuard = Math.Max(0, initialGuard);
            OrdinaryCrewCount = Math.Max(0, ordinaryCrewCount);
            BoardingHealth = CoreBattleBalance.BoardingHealthForOrdinaryCrew(OrdinaryCrewCount);
            BowQuality = Math.Max(1f, bowQuality);
            HullQuality = Math.Max(1f, hullQuality);
            RamChance = double.IsNaN(ramChance) ? 0d : Math.Max(0d, Math.Min(1d, ramChance));
            RamRangeMeters = float.IsNaN(ramRangeMeters) ? 100f : Math.Max(60f, Math.Min(150f, ramRangeMeters));
        }

        public CoreBossKind BossKind { get; }
        public CoreBattleArchetype Archetype { get; }
        public int EnemyHull { get; }
        public int CannonDamage { get; }
        public int MeleeDamage { get; }
        public int Guard { get; }
        public int InitialGuard { get; }
        public int BoardingHealth { get; }
        public int OrdinaryCrewCount { get; }
        public float BowQuality { get; }
        public float HullQuality { get; }
        public double RamChance { get; }
        public float RamRangeMeters { get; }
        public bool IsBoss => BossKind != CoreBossKind.None;
    }

    public static class CoreBattleBalance
    {
        public const string ThirdRouteId = "route_moon_tide_ruins";
        public const string TreasureBossId = "treasure_boss_moon_vault";
        public const int BossVictoryGold = 120;
        public const int BossVictoryFame = 18;
        public const double BossTreasureFragmentDropChance = 0.5d;
        public const int CaptainBoardingHealth = 20;
        public const int BoardingHealthPerSailor = 4;
        public static int BoardingHealthForOrdinaryCrew(int ordinaryCrew) =>
            (int)Math.Min(int.MaxValue, CaptainBoardingHealth + (long)Math.Max(0, ordinaryCrew) * BoardingHealthPerSailor);
        public static string BossVictoryBonusDescription => "另计基础海战奖励：金币 +" + BossVictoryGold + "、声望 +" + BossVictoryFame +
            "；另有 50% 概率掉落藏宝图碎片 ×1";

        // These are the only boss stat tables. Calibration uses complete stage decks,
        // not a damage multiplier hidden in the UI or bonus damage granted to test bots.
        public static readonly CoreBattleProfile FirstRouteBoss = new CoreBattleProfile(
            CoreBossKind.Route, CoreBattleArchetype.Balanced, 75, 12, 11, 6, 0, 9, 1.8f, 1.6f, .12d);
        public static readonly CoreBattleProfile SecondRouteBoss = new CoreBattleProfile(
            CoreBossKind.Route, CoreBattleArchetype.Artillery, 148, 18, 15, 10, 0, 16, 2.4f, 2.5f, .25d);
        // Finite-energy combat: naval durability/firepower and boarding crew/melee
        // pressure are calibrated independently. Ramming adds a telegraphed threat.
        public static readonly CoreBattleProfile ThirdRouteBoss = new CoreBattleProfile(
            CoreBossKind.Route, CoreBattleArchetype.Balanced, 220, 25, 21, 14, 0, 25, 3.0f, 3.0f, .35d, 120f);
        public static readonly CoreBattleProfile ThirdTreasureBoss = new CoreBattleProfile(
            CoreBossKind.Treasure, CoreBattleArchetype.Ironclad, 280, 29, 25, 18, 12, 31, 3.7f, 3.9f, .40d, 120f);

        public static CoreBossKind Classify(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId)) return CoreBossKind.None;
            if (encounterId.StartsWith("legendary_pirate_", StringComparison.Ordinal)) return CoreBossKind.Legendary;
            if (encounterId == TreasureBossId) return CoreBossKind.Treasure;
            if (encounterId.StartsWith("boss_route_", StringComparison.Ordinal)) return CoreBossKind.Route;
            return CoreBossKind.None;
        }

        public static CoreBattleProfile Resolve(CoreEncounter encounter, int routeIndex, bool isBoss = false)
        {
            string id = encounter == null ? string.Empty : encounter.Id;
            if (id == CoreGuidedTutorialContent.BossEventId) return CoreGuidedTutorialContent.BossProfile;
            CoreLegendaryWarship legendary = CoreLegendaryWarships.Find(id);
            if (legendary != null) return legendary.Profile;
            if (id == TreasureBossId) return ThirdTreasureBoss;
            int tier = Math.Max(0, Math.Min(2, routeIndex));
            if (Classify(id) == CoreBossKind.Route || isBoss)
                return tier == 0 ? FirstRouteBoss : tier == 1 ? SecondRouteBoss : ThirdRouteBoss;
            int power = Math.Max(0, encounter == null ? 70 : encounter.Power);
            return new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                Math.Max(38, (int)Math.Round(power * 0.46)), 9 + tier * 3, 8 + tier * 3,
                6 + tier * 2, 0, 5 + power / 28 + tier * 2,
                1f + tier * 0.6f, 1f + tier * 0.7f, tier >= 2 ? .15d : 0d);
        }
    }
}
