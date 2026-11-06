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
            double ramChance = 0d, float ramRangeMeters = 100f, decimal? healthMultiplier = null,
            decimal? boardingHealthMultiplier = null, int? boardingGuard = null)
        {
            BossKind = bossKind;
            Archetype = archetype;
            HealthMultiplier = Math.Max(0m, healthMultiplier ?? CoreBattleBalance.EnemyHealthMultiplier);
            BoardingHealthMultiplier = Math.Max(0m, boardingHealthMultiplier ?? HealthMultiplier);
            EnemyHull = Math.Max(1, CoreBattleBalance.ScaleBattleValue(enemyHull, HealthMultiplier));
            CannonDamage = Math.Max(0, cannonDamage);
            MeleeDamage = Math.Max(0, meleeDamage);
            Guard = Math.Max(0, guard);
            BoardingGuard = Math.Max(0, boardingGuard ?? guard);
            InitialGuard = Math.Max(0, initialGuard);
            OrdinaryCrewCount = Math.Max(0, ordinaryCrewCount);
            BoardingHealth = Math.Max(1, CoreBattleBalance.ScaleBattleValue(
                CoreBattleBalance.BoardingHealthForOrdinaryCrew(OrdinaryCrewCount), BoardingHealthMultiplier));
            BowQuality = Math.Max(1f, bowQuality);
            HullQuality = Math.Max(1f, hullQuality);
            RamChance = double.IsNaN(ramChance) ? 0d : Math.Max(0d, Math.Min(1d, ramChance));
            RamRangeMeters = float.IsNaN(ramRangeMeters) ? 100f : Math.Max(60f, Math.Min(150f, ramRangeMeters));
        }

        internal CoreBattleProfile(CoreTrainingTarget target)
            : this(CoreBossKind.None, CoreBattleArchetype.Balanced, target.Hull,
                target.CannonDamage, target.MeleeDamage, target.Guard, target.Guard,
                Math.Max(0, (target.BoardingHealth - CoreBattleBalance.CaptainBoardingHealth) /
                    CoreBattleBalance.BoardingHealthPerSailor), 2f, 2f, healthMultiplier: 1m,
                boardingHealthMultiplier: 1m)
        {
            TrainingTarget = target;
            BoardingHealth = target.BoardingHealth;
        }

        public CoreBossKind BossKind { get; }
        public CoreBattleArchetype Archetype { get; }
        public int EnemyHull { get; }
        public decimal HealthMultiplier { get; }
        public decimal BoardingHealthMultiplier { get; }
        public int CannonDamage { get; }
        public int MeleeDamage { get; }
        public int Guard { get; }
        public int BoardingGuard { get; }
        public int InitialGuard { get; }
        public int BoardingHealth { get; }
        public int OrdinaryCrewCount { get; }
        public float BowQuality { get; }
        public float HullQuality { get; }
        public double RamChance { get; }
        public float RamRangeMeters { get; }
        public bool IsBoss => BossKind != CoreBossKind.None;
        public CoreTrainingTarget TrainingTarget { get; }
    }

    public static class CoreBattleBalance
    {
        public const string ThirdRouteId = "route_moon_tide_ruins";
        public const string TreasureBossId = "treasure_boss_moon_vault";
        public const int BossVictoryGold = 120;
        public const int BossVictoryFame = 18;
        public const double BossTreasureFragmentDropChance = 0.5d;
        public const int CaptainBoardingHealth = 40;
        public const int BoardingHealthPerSailor = 8;
        public const decimal EnemyHealthMultiplier = 1.5m;
        public const decimal LateBossBoardingHealthMultiplier = 2m;
        // Ramming is an equipment action, independent of authored card values.
        public const int PlayerRamDamageMultiplier = 2;
        internal static int ScaleBattleValue(int value, decimal multiplier) =>
            (int)Math.Min(int.MaxValue, decimal.Round(Math.Max(0, value) * multiplier, 0, MidpointRounding.AwayFromZero));
        public static int BoardingHealthForOrdinaryCrew(int ordinaryCrew) =>
            (int)Math.Min(int.MaxValue, CaptainBoardingHealth + (long)Math.Max(0, ordinaryCrew) * BoardingHealthPerSailor);
        public static string BossVictoryBonusDescription => "夺船胜利金币、声望为海战胜利的1.25倍；物资战利品计入海战整百金币报价；" +
            "另有 50% 概率掉落藏宝图碎片 ×1";

        // Tables contain authored stats; profiles apply health scaling once so
        // encounter previews, simulation and live battle share the same values.
        // Naval hull, cannon fire and guard leave more room for error than boarding.
        // Route guardians test an unlocked build and a few voyages of development;
        // legendary fleets remain the final challenge. Authored hull inputs below
        // still receive the common 1.5 multiplier exactly once.
        // Early-chart boarding durability is tuned separately from naval hull:
        // Boarding-only budgets account for shared inherited fields and upgraded crew cards.
        public static readonly CoreBattleProfile FirstRouteBoss = new CoreBattleProfile(
            CoreBossKind.Route, CoreBattleArchetype.Balanced, 96, 6, 20, 4, 0, 9, 1.8f, 1.6f, .12d,
            boardingHealthMultiplier: 300m / 112m, boardingGuard: 10);
        public static readonly CoreBattleProfile SecondRouteBoss = new CoreBattleProfile(
            CoreBossKind.Route, CoreBattleArchetype.Artillery, 240, 23, 30, 8, 0, 16, 2.4f, 2.5f, .25d,
            boardingHealthMultiplier: 480m / 168m, boardingGuard: 15);
        // Finite-energy combat: naval durability/firepower and boarding crew/melee
        // pressure are calibrated independently. Ramming adds a telegraphed threat.
        public static readonly CoreBattleProfile ThirdRouteBoss = new CoreBattleProfile(
            CoreBossKind.Route, CoreBattleArchetype.Balanced, 400, 33, 40, 13, 0, 25, 3.0f, 3.0f, .35d, 120f,
            boardingHealthMultiplier: 600m / 240m, boardingGuard: 20);
        public static readonly CoreBattleProfile ThirdTreasureBoss = new CoreBattleProfile(
            CoreBossKind.Treasure, CoreBattleArchetype.Ironclad, 1000, 80, 44, 36, 20, 31, 3.7f, 3.9f, .40d, 120f,
            boardingHealthMultiplier: 1510m / 288m, boardingGuard: 30);

        public static CoreBossKind Classify(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId)) return CoreBossKind.None;
            if (encounterId.StartsWith("legendary_pirate_", StringComparison.Ordinal)) return CoreBossKind.Legendary;
            if (encounterId == TreasureBossId) return CoreBossKind.Treasure;
            if (encounterId.StartsWith("boss_route_", StringComparison.Ordinal)) return CoreBossKind.Route;
            return CoreBossKind.None;
        }

        public static CoreBattleProfile Resolve(CoreEncounter encounter, int routeIndex, bool isBoss = false, bool guidedTutorial = false)
        {
            string id = encounter == null ? string.Empty : encounter.Id;
            if (id == CoreGuidedTutorialContent.BossEventId)
            {
                CoreBattleProfile lesson = CoreGuidedTutorialContent.BossProfile;
                return guidedTutorial ? lesson : new CoreBattleProfile(lesson.BossKind, lesson.Archetype,
                    lesson.EnemyHull, lesson.CannonDamage, lesson.MeleeDamage, lesson.Guard, lesson.InitialGuard,
                    lesson.OrdinaryCrewCount, lesson.BowQuality, lesson.HullQuality, lesson.RamChance, lesson.RamRangeMeters);
            }
            CoreLegendaryWarship legendary = CoreLegendaryWarships.Find(id);
            if (legendary != null) return legendary.Profile;
            CoreRouteBoss routeBoss = CoreRouteBossCatalog.Find(id);
            if (routeBoss != null) return routeBoss.Profile;
            if (id == TreasureBossId) return ThirdTreasureBoss;
            int tier = Math.Max(0, Math.Min(2, routeIndex));
            if (Classify(id) == CoreBossKind.Route || isBoss)
                return tier == 0 ? FirstRouteBoss : tier == 1 ? SecondRouteBoss : ThirdRouteBoss;
            int power = Math.Max(0, encounter == null ? 70 : encounter.Power);
            return new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                Math.Max(38, (int)Math.Round(power * 0.46)), 9 + tier * 3, 12 + tier * 6,
                6 + tier * 2, 0, 5 + power / 28 + tier * 2,
                1f + tier * 0.6f, 1f + tier * 0.7f, tier >= 2 ? .15d : 0d, boardingHealthMultiplier: 1.75m + tier * .375m, boardingGuard: 10 + tier * 5);
        }
    }
}
