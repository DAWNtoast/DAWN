using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageAchievementTests
    {
        [TestCase(VoyageOutcome.SafeReturn, 2, false)]
        [TestCase(VoyageOutcome.SafeReturn, 3, true)]
        [TestCase(VoyageOutcome.SafeReturn, 4, true)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 2, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 3, true)]
        [TestCase(VoyageOutcome.Failure, 3, false)]
        [TestCase(VoyageOutcome.TreasureRecovered, 3, false)]
        [TestCase(VoyageOutcome.SafeReturn, -1, false)]
        public void VoyageThreeEvents_RequiresThreeCompletedEventsAndSuccessfulOrdinaryReturn(
            VoyageOutcome outcome, int completedEvents, bool expected)
        {
            var settlement = new VoyageSettlement { Outcome = outcome, CompletedEvents = completedEvents };
            Assert.That(CoreAchievementCatalog.MeetsVoyageThreeEventsRequirement(settlement, false, false), Is.EqualTo(expected));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void VoyageThreeEvents_ExcludesTutorialAndTreasureVoyages(bool tutorial, bool treasure)
        {
            var settlement = new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn, CompletedEvents = 10 };
            Assert.That(CoreAchievementCatalog.MeetsVoyageThreeEventsRequirement(settlement, tutorial, treasure), Is.False);
        }

        [Test]
        public void VoyageThreeEvents_MissingSettlementCannotUnlock()
        {
            Assert.That(CoreAchievementCatalog.MeetsVoyageThreeEventsRequirement(null, false, false), Is.False);
        }

        [TestCase(VoyageOutcome.SafeReturn, 49.99f, false)]
        [TestCase(VoyageOutcome.SafeReturn, 50f, true)]
        [TestCase(VoyageOutcome.SafeReturn, 100f, true)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 49.99f, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 50f, true)]
        [TestCase(VoyageOutcome.Failure, 100f, false)]
        [TestCase(VoyageOutcome.TreasureRecovered, 100f, false)]
        [TestCase(VoyageOutcome.SafeReturn, -1f, false)]
        [TestCase(VoyageOutcome.SafeReturn, float.NaN, false)]
        [TestCase(VoyageOutcome.SafeReturn, float.PositiveInfinity, false)]
        public void VoyageHalfChart_RequiresFiniteHalfChartCoverageAndSuccessfulReturn(
            VoyageOutcome outcome, float coverage, bool expected)
        {
            var settlement = new VoyageSettlement { Outcome = outcome, VoyageCoverage = coverage };
            Assert.That(CoreAchievementCatalog.MeetsVoyageHalfChartRequirement(settlement, false, false), Is.EqualTo(expected));
        }

        [TestCase(VoyageOutcome.ArrivedAtPort, 3, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 5, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 6, true)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 7, true)]
        [TestCase(VoyageOutcome.SafeReturn, 6, false)]
        [TestCase(VoyageOutcome.Failure, 6, false)]
        [TestCase(VoyageOutcome.TreasureRecovered, 6, false)]
        public void VoyageEventArrival_RequiresSixCompletedEventsAndArrival(
            VoyageOutcome outcome, int completedEvents, bool expected)
        {
            var settlement = new VoyageSettlement { Outcome = outcome, CompletedEvents = completedEvents };
            Assert.That(CoreAchievementCatalog.MeetsVoyageEventArrivalRequirement(settlement, false, false), Is.EqualTo(expected));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void AdditionalVoyageAchievements_ExcludeTutorialAndTreasureVoyages(bool tutorial, bool treasure)
        {
            var settlement = new VoyageSettlement { Outcome = VoyageOutcome.ArrivedAtPort, CompletedEvents = 6, VoyageCoverage = 100f };
            Assert.That(CoreAchievementCatalog.MeetsVoyageHalfChartRequirement(settlement, tutorial, treasure), Is.False);
            Assert.That(CoreAchievementCatalog.MeetsVoyageEventArrivalRequirement(settlement, tutorial, treasure), Is.False);
        }

        [Test]
        public void AdditionalVoyageAchievements_MissingSettlementCannotUnlock()
        {
            Assert.That(CoreAchievementCatalog.MeetsVoyageHalfChartRequirement(null, false, false), Is.False);
            Assert.That(CoreAchievementCatalog.MeetsVoyageEventArrivalRequirement(null, false, false), Is.False);
        }

        [TestCase(VoyageOutcome.SafeReturn, 1, 25, 100, true)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 1, 25, 100, true)]
        [TestCase(VoyageOutcome.SafeReturn, 2, 1, 100, true)]
        [TestCase(VoyageOutcome.SafeReturn, 1, 26, 100, false)]
        [TestCase(VoyageOutcome.SafeReturn, 1, 13, 55, true)]
        [TestCase(VoyageOutcome.SafeReturn, 1, 14, 55, false)]
        [TestCase(VoyageOutcome.SafeReturn, 0, 25, 100, false)]
        [TestCase(VoyageOutcome.SafeReturn, -1, 25, 100, false)]
        [TestCase(VoyageOutcome.SafeReturn, 1, 0, 100, false)]
        [TestCase(VoyageOutcome.SafeReturn, 1, -1, 100, false)]
        [TestCase(VoyageOutcome.SafeReturn, 1, 1, 0, false)]
        [TestCase(VoyageOutcome.SafeReturn, 1, 1, -1, false)]
        [TestCase(VoyageOutcome.SafeReturn, 1, int.MaxValue, int.MaxValue, false)]
        [TestCase(VoyageOutcome.Failure, 1, 25, 100, false)]
        [TestCase(VoyageOutcome.TreasureRecovered, 1, 25, 100, false)]
        public void DamagedReturn_RequiresVictoryAndSurvivingQuarterHull(
            VoyageOutcome outcome, int victories, int hull, int maxHull, bool expected)
        {
            var settlement = new VoyageSettlement { Outcome = outcome };
            Assert.That(CoreAchievementCatalog.MeetsDamagedReturnRequirement(
                settlement, false, false, victories, hull, maxHull), Is.EqualTo(expected));
        }

        [TestCase(VoyageOutcome.ArrivedAtPort, 240d, true, false, true)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 300d, true, false, true)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 239.99d, true, false, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 0d, true, false, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, -1d, true, false, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, double.NaN, true, false, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, double.PositiveInfinity, true, false, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 240d, false, false, false)]
        [TestCase(VoyageOutcome.ArrivedAtPort, 240d, true, true, false)]
        [TestCase(VoyageOutcome.SafeReturn, 240d, true, false, false)]
        [TestCase(VoyageOutcome.Failure, 240d, true, false, false)]
        [TestCase(VoyageOutcome.TreasureRecovered, 240d, true, false, false)]
        public void SuppliedVoyage_RequiresTenDaysArrivalAndUnbrokenSupplyEvidence(
            VoyageOutcome outcome, double hours, bool hasEvidence, bool ranOut, bool expected)
        {
            var settlement = new VoyageSettlement { Outcome = outcome, Hours = hours };
            Assert.That(CoreAchievementCatalog.MeetsSupplyVoyageRequirement(
                settlement, false, false, hasEvidence, ranOut), Is.EqualTo(expected));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void VoyageChallenges_ExcludeTutorialAndTreasureVoyages(bool tutorial, bool treasure)
        {
            var settlement = new VoyageSettlement { Outcome = VoyageOutcome.ArrivedAtPort, Hours = 240d };
            Assert.That(CoreAchievementCatalog.MeetsDamagedReturnRequirement(
                settlement, tutorial, treasure, 1, 25, 100), Is.False);
            Assert.That(CoreAchievementCatalog.MeetsSupplyVoyageRequirement(
                settlement, tutorial, treasure, true, false), Is.False);
        }

        [Test]
        public void VoyageChallenges_MissingSettlementCannotUnlock()
        {
            Assert.That(CoreAchievementCatalog.MeetsDamagedReturnRequirement(null, false, false, 1, 25, 100), Is.False);
            Assert.That(CoreAchievementCatalog.MeetsSupplyVoyageRequirement(null, false, false, true, false), Is.False);
        }

        [Test]
        public void VoyageHalfChart_PreviousVoyageCoverageCannotQualifyANewShortVoyage()
        {
            var session = new CoreVoyageSession();
            session.RestoreLastVoyageExplorationCoverage(100f);
            session.StartVoyage();
            for (int index = 0; index < session.Map.ExploredCells.Length; index++) session.Map.ExploredCells[index] = true;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Coverage, Is.EqualTo(100f));
            Assert.That(session.Settlement.VoyageCoverage, Is.LessThan(50f));
            Assert.That(CoreAchievementCatalog.MeetsVoyageHalfChartRequirement(session.Settlement, false, false), Is.False);
        }

        [Test]
        public void VoyageHalfChart_ReobservingHistoricalChartCellsCountsOnlyTheVisibleArea()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            bool[] departureObservations = (bool[])session.Map.VoyageExploredCells.Clone();
            Assert.That(departureObservations.Any(value => value), Is.True);
            for (int index = 0; index < session.Map.ExploredCells.Length; index++)
            {
                session.Map.ExploredCells[index] = true;
                session.Map.VoyageExploredCells[index] = false;
            }
            typeof(CoreVoyageSession).GetMethod("RevealAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { session.Position });
            CollectionAssert.AreEqual(departureObservations, session.Map.VoyageExploredCells,
                "Known cells must still count when observed again, without inheriting unseen history.");
            Assert.That(session.ExplorationCoverage(), Is.EqualTo(100f));
            Assert.That(session.VoyageExplorationCoverage(), Is.GreaterThan(0f).And.LessThan(50f));
        }

        [Test]
        public void VoyageHalfChart_CheckpointRoundTripPreservesIndependentVoyageEvidence()
        {
            var source = new CoreVoyageSession();
            source.StartVoyage();
            for (int index = 0; index < source.Map.ExploredCells.Length; index++)
            {
                source.Map.ExploredCells[index] = true;
                source.Map.VoyageExploredCells[index] = index < source.Map.ExploredCells.Length / 2;
            }
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            source.Map.VoyageExploredCells[0] = false;
            Assert.That(checkpoint.voyageExploredCells[0], Is.True, "Checkpoint evidence must be a detached copy.");
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(
                JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(checkpoint)), out string reason), Is.True, reason);
            Assert.That(restored.ExplorationCoverage(), Is.EqualTo(100f));
            Assert.That(restored.VoyageExplorationCoverage(), Is.EqualTo(50f));
            Assert.That(restored.SafeReturn(), Is.True);
            Assert.That(CoreAchievementCatalog.MeetsVoyageHalfChartRequirement(restored.Settlement, false, false), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VoyageHalfChart_LegacyCheckpointDoesNotInventVoyageEvidence(bool emptyArray)
        {
            var source = new CoreVoyageSession();
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            for (int index = 0; index < checkpoint.exploredCells.Length; index++) checkpoint.exploredCells[index] = true;
            checkpoint.voyageExploredCells = emptyArray ? new bool[0] : null;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.ExplorationCoverage(), Is.EqualTo(100f));
            Assert.That(restored.VoyageExplorationCoverage(), Is.Zero);
            Assert.That(restored.SafeReturn(), Is.True);
            Assert.That(CoreAchievementCatalog.MeetsVoyageHalfChartRequirement(restored.Settlement, false, false), Is.False);
        }

        [Test]
        public void VoyageHalfChart_CorruptCheckpointEvidenceIsRejectedWithoutChangingTheSession()
        {
            var source = new CoreVoyageSession();
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.voyageExploredCells = new bool[1];
            var restored = new CoreVoyageSession();
            decimal originalGold = restored.ProfileGold;
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(restored.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(restored.Map, Is.Null);
            Assert.That(restored.ProfileGold, Is.EqualTo(originalGold));
        }

        [Test]
        public void VoyageThreeEvents_SkippedAndUnvisitedNodesDoNotCountInTheSettlement()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Map.Nodes.Count, Is.GreaterThanOrEqualTo(4));
            foreach (CoreMapNode node in session.Map.Nodes) { node.Visited = false; node.Skipped = false; }
            CoreMapNode[] nodes = session.Map.Nodes.Take(4).ToArray();
            nodes[0].Visited = nodes[1].Visited = nodes[2].Visited = true;
            nodes[2].Skipped = true;
            Assert.That(session.CompletedEventCount(), Is.EqualTo(2));
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.CompletedEvents, Is.EqualTo(2));
            Assert.That(CoreAchievementCatalog.MeetsVoyageThreeEventsRequirement(session.Settlement, false, false), Is.False,
                "A skipped event and a merely available event cannot fill the third completion.");
        }
    }
}
