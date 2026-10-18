using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalDifficultySeparationTests
    {
        // Frozen before naval tuning in BattleBalanceBaseline20261016_PreNaval.
        // These protect boarding difficulty while naval hull, cannon and guard change.
        [TestCase("boss_route_shattered_sail_reef", 119, 10, 4, 9)]
        [TestCase("boss_route_shattered_sail_reef_tide", 105, 10, 6, 9)]
        [TestCase("boss_route_golden_tide_trade", 133, 15, 6, 16)]
        [TestCase("boss_route_golden_tide_trade_thunder", 147, 15, 6, 16)]
        [TestCase("boss_route_moon_tide_ruins", 240, 20, 8, 25)]
        [TestCase("boss_route_moon_tide_ruins_arcane", 240, 19, 8, 25)]
        [TestCase("treasure_boss_moon_vault", 288, 22, 12, 31)]
        [TestCase("legendary_pirate_iron_oath", 324, 21, 14, 55)]
        [TestCase("legendary_pirate_thunder_crown", 314, 22, 8, 52)]
        [TestCase("legendary_pirate_red_widow", 327, 24, 8, 66)]
        [TestCase("legendary_pirate_ember_ark", 325, 23, 10, 60)]
        [TestCase("legendary_pirate_tide_requiem", 342, 20, 16, 62)]
        [TestCase("legendary_pirate_last_horizon", 328, 22, 12, 58)]
        public void NavalTuningPreservesFullHullBoardingBaseline(string id, int health, int melee, int guard, int ordinary)
        {
            CoreEncounter encounter = Encounter(id);
            CoreBattleProfile profile = CoreBattleBalance.Resolve(encounter, 2, true);
            Assert.That(new[] { profile.BoardingHealth, profile.MeleeDamage, profile.BoardingGuard, profile.OrdinaryCrewCount },
                Is.EqualTo(new[] { health, melee, guard, ordinary }));
            // Encounter previews and live battle must use the same single hull scaling.
            Assert.That(encounter.Power, Is.EqualTo(profile.EnemyHull));
            CoreVoyageSession session = Start(encounter);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(profile.EnemyHull));
            Assert.That(session.Battle.EnemyMaxHull, Is.EqualTo(profile.EnemyHull));
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull;
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            Assert.That(session.Battle.EnemyMaxBoardingHealth, Is.EqualTo(health));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(health), "Full hull must not inherit damage from naval tuning.");
        }

        [TestCase(0, 120, 80, 10, 8, 2)]
        [TestCase(1, 220, 140, 16, 12, 5)]
        [TestCase(2, 360, 240, 24, 20, 9)]
        [TestCase(3, 600, 400, 36, 30, 14)]
        [TestCase(4, 1000, 680, 50, 44, 20)]
        public void TrainingProfilesKeepAuthoredUnscaledValues(int index, int hull, int health, int cannon, int melee, int guard)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[index]), Is.True);
            CoreBattleProfile profile = session.Battle.Profile;
            Assert.That(new[] { profile.EnemyHull, profile.BoardingHealth, profile.CannonDamage, profile.MeleeDamage, profile.Guard },
                Is.EqualTo(new[] { hull, health, cannon, melee, guard }));
            Assert.That(profile.HealthMultiplier, Is.EqualTo(1m));
            Assert.That(profile.BoardingHealthMultiplier, Is.EqualTo(1m));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hull));
        }

        [Test]
        public void GuidedLessonRetainsItsOwnProfileAndScript()
        {
            CoreVoyageSession session = CoreGuidedBossScriptTests.StartBoss();
            CoreBattleProfile profile = session.Battle.Profile;
            Assert.That(profile, Is.SameAs(CoreGuidedTutorialContent.BossProfile));
            Assert.That(new[] { profile.EnemyHull, profile.CannonDamage, profile.MeleeDamage, profile.Guard,
                profile.InitialGuard, profile.BoardingHealth, profile.OrdinaryCrewCount },
                Is.EqualTo(new[] { 52, 7, 5, 3, 0, 28, 2 }));
            Assert.That(profile.HealthMultiplier, Is.EqualTo(1m));
            CoreGuidedBossScriptTests.PlayToCompletion(session);
        }

        private static CoreEncounter Encounter(string id) => id == CoreBattleBalance.TreasureBossId
            ? CoreLegendaryWarships.CreateTreasureEncounter()
            : CoreRouteBossCatalog.Find(id)?.CreateEncounter() ?? CoreLegendaryWarships.Find(id).CreateEncounter();

        private static CoreVoyageSession Start(CoreEncounter encounter)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.SetSeed("NAVAL-BOARDING-SEPARATION");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, true });
            return session;
        }
    }
}
