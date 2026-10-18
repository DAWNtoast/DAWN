using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleEquipmentRestoreTests
    {
        [TestCase(3f, 3f, 3f, 3f, 38)]
        [TestCase(5f, 4f, 5f, 4f, 64)]
        [TestCase(0f, 0f, 1f, 1f, 12)]
        public void RestoreSynchronizationUpdatesActivePlayerEquipmentAndRamWithoutChangingEnemy(
            float bow, float plating, float expectedBow, float expectedPlating, int expectedDamage)
        {
            var session = new CoreVoyageSession();
            session.ConfigureBattleEquipment(2.7f, 3.3f);
            session.StartVoyage();
            StartBattle(session);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            session.Battle.EnemyBowQuality = 3f;
            session.Battle.EnemyHullQuality = 2.6f;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 200;
            int playerHull = session.Hull;

            session.SynchronizeRestoredBattleEquipment(bow, plating);

            Assert.That(session.Battle.PlayerBowQuality, Is.EqualTo(expectedBow));
            Assert.That(session.Battle.PlayerHullQuality, Is.EqualTo(expectedPlating));
            Assert.That(session.Battle.EnemyBowQuality, Is.EqualTo(3f));
            Assert.That(session.Battle.EnemyHullQuality, Is.EqualTo(2.6f));
            Assert.That(session.Hull, Is.EqualTo(playerHull), "Restoring equipment must not repair or damage the vessel.");
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            session.Battle.Alignment = NavalBattleAlignment.HeadOn;
            session.Battle.PlayerMovingTowardEnemy = true;
            session.Battle.Energy = 3;
            Assert.That(session.TryRam(), Is.True);
            Assert.That(200 - session.Battle.EnemyHull, Is.EqualTo(expectedDamage),
                "The next real ram must use synchronized rarity strength, including the empty-slot floor.");
        }

        [Test]
        public void OrdinaryEquipmentConfigurationRemainsPortOnlyAfterRestoreSynchronization()
        {
            var session = new CoreVoyageSession();
            session.ConfigureBattleEquipment(2f, 4f);
            session.StartVoyage();
            session.ConfigureBattleEquipment(5f, 5f);
            CoreVoyageCheckpoint sailing = session.CaptureVoyageCheckpoint();
            Assert.That(sailing.battleBowQuality, Is.EqualTo(2f));
            Assert.That(sailing.battleHullQuality, Is.EqualTo(4f));

            session.SynchronizeRestoredBattleEquipment(3f, 1f);
            sailing = session.CaptureVoyageCheckpoint();
            Assert.That(sailing.battleBowQuality, Is.EqualTo(3f));
            Assert.That(sailing.battleHullQuality, Is.EqualTo(1f));
            StartBattle(session);
            session.ConfigureBattleEquipment(5f, 5f);
            Assert.That(session.Battle.PlayerBowQuality, Is.EqualTo(3f));
            Assert.That(session.Battle.PlayerHullQuality, Is.EqualTo(1f));
        }

        private static void StartBattle(CoreVoyageSession session)
        {
            CoreEncounter encounter = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_coral_ambush").Encounter;
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle, Is.Not.Null);
        }
    }
}
