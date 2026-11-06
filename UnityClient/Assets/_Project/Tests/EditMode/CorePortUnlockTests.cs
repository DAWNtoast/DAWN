using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CorePortUnlockTests
    {
        [Test]
        public void NewSaveHasThreeLevelEligiblePortsButMustVisitWhiteShellBeforeBronzeBell()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.PirateLevel, Is.EqualTo(1));
            Assert.That(CoreVoyageSession.DestinationPorts.Count(port =>
                CoreVoyageSession.PortUnlockLevel(port) <= session.PirateLevel), Is.EqualTo(3));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo("白贝港"));
            Assert.That(session.CanSelectDestinationPort("白贝港", out _), Is.True);
            Assert.That(session.CanSelectDestinationPort("铜钟港", out string reason), Is.False);
            Assert.That(reason, Does.Contain("先到达一次白贝港"));
            Assert.That(session.CanSelectDestinationPort("断锚港", out _), Is.False);
            Assert.That(session.CanSelectDestinationPort("未知港口", out _), Is.False);
            Assert.That(session.CanSelectDestinationPort(null, out _), Is.False);
        }

        [TestCase("铜钟港", "白贝港", 1)]
        [TestCase("盐冠港", "铜钟港", 3)]
        [TestCase("暮潮港", "盐冠港", 3)]
        [TestCase("灰鸥港", "暮潮港", 5)]
        public void EachNewPortRequiresBothItsLevelAndItsPreviousArrival(string port, string previous, int level)
        {
            var session = new CoreVoyageSession();
            Assert.That(CoreVoyageSession.PortUnlockLevel(port), Is.EqualTo(level));
            SetLevel(session, level);
            Assert.That(session.CanSelectDestinationPort(port, out string missingVisit), Is.False);
            Assert.That(missingVisit, Does.Contain(previous));
            session.RecordCollectionPortVisit(previous);
            if (level > 1)
            {
                SetLevel(session, level - 1);
                Assert.That(session.CanSelectDestinationPort(port, out string missingLevel), Is.False);
                Assert.That(missingLevel, Does.Contain(level + " 级"));
                SetLevel(session, level);
            }
            Assert.That(session.CanSelectDestinationPort(port, out string reason), Is.True, reason);
        }

        [Test]
        public void LevelFiveDoesNotSkipUnvisitedPortsOrUnlockTheSecondPortInABand()
        {
            var session = new CoreVoyageSession();
            SetLevel(session, 5);
            session.RecordCollectionPortVisit("白贝港");
            Assert.That(session.CanSelectDestinationPort("铜钟港", out _), Is.True);
            Assert.That(session.CanSelectDestinationPort("盐冠港", out _), Is.False);
            session.RecordCollectionPortVisit("铜钟港");
            Assert.That(session.CanSelectDestinationPort("盐冠港", out _), Is.True);
            Assert.That(session.CanSelectDestinationPort("暮潮港", out _), Is.False);
            session.RecordCollectionPortVisit("盐冠港");
            Assert.That(session.CanSelectDestinationPort("暮潮港", out _), Is.True);
            Assert.That(session.CanSelectDestinationPort("灰鸥港", out _), Is.False);
        }

        [Test]
        public void LockedDestinationCannotBeSelectedOrReachedThroughASeedOrExplicitDeparture()
        {
            var session = new CoreVoyageSession();
            string selected = session.SelectedDestinationPort, seed = session.SelectedSeed;
            decimal gold = session.ProfileGold;
            int cargoGold = session.Cargo.Gold;
            Assert.That(session.SelectDestinationPort("铜钟港"), Is.False);
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(selected));
            string lockedSeed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "铜钟港");
            Assert.That(session.TrySelectChartSeed(lockedSeed, out string reason), Is.False);
            Assert.That(reason, Does.Contain("白贝港"));
            Assert.That(session.SelectedSeed, Is.EqualTo(seed));
            Assert.Throws<InvalidOperationException>(() => session.StartVoyage("铜钟港"));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(selected));
            Assert.That(session.Map, Is.Null);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
            session.SetSeed(lockedSeed);
            Assert.Throws<InvalidOperationException>(() => session.StartVoyage());
            Assert.That(session.Map, Is.Null);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
        }

        [Test]
        public void ConfirmedRealArrivalUnlocksTheNextPortButSightingAndUnconfirmedSettlementDoNot()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            typeof(CoreVoyageSession).GetField("bossDefeated", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, true);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, session.Map.Destination);
            session.SetCourseTo(session.Map.Destination);
            Assert.That(session.PortUnlockReason("铜钟港"), Is.Not.Empty);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
            Assert.That(session.HasVisitedPort("白贝港"), Is.False);
            Assert.That(session.PortUnlockReason("铜钟港"), Is.Not.Empty);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.CurrentPort, Is.EqualTo("白贝港"));
            Assert.That(session.HasVisitedPort("白贝港"), Is.True);
            Assert.That(session.CanSelectDestinationPort("铜钟港", out string reason), Is.True, reason);
            Assert.That(session.SelectedDestinationPort, Is.EqualTo("铜钟港"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReturnWithoutArrivalDoesNotUnlockTheNextPort(bool failure)
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            if (failure) session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.HasVisitedPort("白贝港"), Is.False);
            Assert.That(session.CanSelectDestinationPort("铜钟港", out _), Is.False);
        }

        [Test]
        public void ArrivalHistorySurvivesRestoreAndLegacyVisitedPortsRemainAvailableWithoutInventingVisits()
        {
            var original = new CoreVoyageSession();
            original.RecordCollectionPortVisit("白贝港");
            original.RecordCollectionPortVisit("灰鸥港");
            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreCollectionProgress(original.CaptureCollectionProgress()), Is.True);
            restored.RefreshPortDestinationSelection();
            Assert.That(restored.CanSelectDestinationPort("铜钟港", out _), Is.True);
            Assert.That(restored.CanSelectDestinationPort("灰鸥港", out _), Is.True);
            Assert.That(restored.HasVisitedPort("暮潮港"), Is.False);
            Assert.That(restored.CanSelectDestinationPort("暮潮港", out _), Is.False);
        }

        [Test]
        public void ValidExplicitDestinationIsUsedAndDefaultSelectionIsRepairedAfterHistoryRestore()
        {
            var session = new CoreVoyageSession();
            session.RecordCollectionPortVisit("白贝港");
            session.StartVoyage("铜钟港");
            Assert.That(session.Map.DestinationName, Is.EqualTo("铜钟港"));
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.RestoreCollectionProgress(new CoreCollectionProgress()), Is.True);
            session.RefreshPortDestinationSelection();
            Assert.That(session.SelectedDestinationPort, Is.EqualTo("白贝港"));
        }

        private static void SetLevel(CoreVoyageSession session, int level) =>
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(level) });
    }
}
