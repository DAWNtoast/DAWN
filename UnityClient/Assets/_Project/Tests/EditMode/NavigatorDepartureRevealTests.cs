using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class NavigatorDepartureRevealTests
    {
        [TestCase(0, 0)]
        [TestCase(1, 2)]
        [TestCase(2, 4)]
        [TestCase(3, 6)]
        public void EachNavigatorMarksTwoPreviouslyUnknownFixedLocations(int navigators, int expected)
        {
            CoreVoyageSession session = Ready(navigators);
            session.StartVoyage();
            Assert.That(session.NavigatorCount, Is.EqualTo(navigators));
            Assert.That(session.Map.Nodes.Count(node => node.IntelRevealed), Is.EqualTo(expected));
            Assert.That(session.Map.Nodes.Where(node => node.IntelRevealed).All(node =>
                node.Encounter.HasChartLocation && node.IsChartVisible && !node.Discovered && !node.Visited), Is.True);
            Assert.That(session.Map.Nodes.Where(node => !node.Encounter.HasChartLocation).All(node => !node.IsChartVisible), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.ActiveEncounter, Is.Null);
            Assert.That(session.Map.Nodes.Any(node => node.Visited), Is.False);
            Assert.That(session.TotalHours, Is.Zero);
            Assert.That(session.TotalDistanceNm, Is.Zero);
            Assert.That(session.VoyageTreasureFragments, Is.Zero);
        }

        [Test]
        public void ExtraNavigatorsDoNotStackVisionOrChangeGeographyRandomSequenceAndSupplies()
        {
            CoreVoyageSession one = Ready(1), three = Ready(3);
            one.StartVoyage(); three.StartVoyage();
            Assert.That(three.Map.Hash, Is.EqualTo(one.Map.Hash));
            Assert.That(three.Map.Seed, Is.EqualTo(one.Map.Seed));
            Assert.That(three.Map.TerrainCells, Is.EqualTo(one.Map.TerrainCells));
            Assert.That(three.Map.Nodes.Select(node => node.Position), Is.EqualTo(one.Map.Nodes.Select(node => node.Position)));
            Assert.That(three.Map.ExploredCells, Is.EqualTo(one.Map.ExploredCells));
            Assert.That(three.Map.VoyageExploredCells, Is.EqualTo(one.Map.VoyageExploredCells));
            Assert.That(three.Map.ChartExploredCells, Is.EqualTo(one.Map.ChartExploredCells));
            Assert.That(three.CaptureVoyageCheckpoint().voyageRandomState, Is.EqualTo(one.CaptureVoyageCheckpoint().voyageRandomState));
            Assert.That(three.Cargo.Gold, Is.EqualTo(one.Cargo.Gold));
            Assert.That(three.Cargo.Water, Is.EqualTo(one.Cargo.Water));
            Assert.That(three.Cargo.Food, Is.EqualTo(one.Cargo.Food));
            Assert.That(three.EffectiveSpeedKnots, Is.EqualTo(one.EffectiveSpeedKnots));
            Assert.That(three.HasCarpenter && three.HasDoctor && three.HasGunner && three.HasCook && three.HasDiver, Is.True);
            Assert.That(three.Map.Nodes.Where(node => node.IntelRevealed).Select(node => node.Id),
                Is.SupersetOf(one.Map.Nodes.Where(node => node.IntelRevealed).Select(node => node.Id)));
        }

        [Test]
        public void LoadedChartUsesTheSameDepartureBonusWithoutClaimingItsEvents()
        {
            CoreVoyageSession session = Ready(2);
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            Assert.That(session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            session.StartVoyage();
            Assert.That(session.Map.Nodes.Count(node => node.IntelRevealed), Is.EqualTo(4));
            CoreSeaChartState state = session.CaptureSeaChartProgress().charts.Single();
            Assert.That(state.events, Is.Empty);
            Assert.That(state.defeatedBosses, Is.Empty);
            Assert.That(session.Map.Nodes.Any(node => node.Visited), Is.False);
        }

        [Test]
        public void TreasureBonusSkipsAlreadyKnownCoreAndDoesNotGrantClues()
        {
            CoreVoyageSession session = Ready(2);
            const string seed = "NAVIGATOR-TREASURE";
            const string id = "weathered:" + seed;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = id,
                maps = new[] { new CoreTreasureMapSave { id = id, seed = seed, rankId = "weathered" } } });
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            Assert.That(session.Map.Nodes.Count(node => node.IntelRevealed && !node.Discovered), Is.EqualTo(4));
            Assert.That(session.Map.Nodes.Single(node => node.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId).IsChartVisible, Is.True);
            Assert.That(session.TreasureClues, Is.Zero);
            Assert.That(session.TreasureAdventureCompleted, Is.False);
            Assert.That(session.Map.Nodes.Any(node => node.Visited), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ReusingActualChartKnowledgeRevealsAdditionalLocationsBeforeSaving(int navigators)
        {
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            CoreVoyageSession first = Ready(1);
            Assert.That(first.TrySelectChartSeed(seed, out _), Is.True);
            first.StartVoyage();
            int[] known = first.Map.Nodes.Where(node => node.IsChartVisible).Select(node => node.Id).ToArray();
            CoreSeaChartProgress progress = first.CaptureSeaChartProgress();
            CoreVoyageSession replay = Ready(navigators);
            Assert.That(replay.RestoreSeaChartProgress(progress), Is.True);
            Assert.That(replay.TrySelectChartSeed(seed, out _), Is.True);
            replay.StartVoyage(replay.SelectedDestinationPort, known);
            Assert.That(replay.Map.Nodes.Count(node => node.IsChartVisible && !known.Contains(node.Id)), Is.EqualTo(navigators * 2));
            Assert.That(replay.Map.Nodes.Where(node => known.Contains(node.Id)).All(node => node.IsChartVisible), Is.True);
            Assert.That(replay.Map.Nodes.Any(node => node.Visited), Is.False);
            Assert.That(replay.CaptureSeaChartProgress().charts.Single().events, Is.Empty);
            CoreVoyageCheckpoint saved = replay.CaptureVoyageCheckpoint();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(saved, out string reason), Is.True, reason);
            Assert.That(restored.Map.Nodes.Select(node => node.IsChartVisible), Is.EqualTo(replay.Map.Nodes.Select(node => node.IsChartVisible)));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TutorialWithTooFewUnknownLocationsOnlyRevealsExistingFixedPoints(int leg)
        {
            CoreVoyageSession session = Ready(3);
            Assert.That(session.ConfigureGuidedTutorialVoyage(leg), Is.True);
            session.StartVoyage();
            Assert.That(session.Map.Nodes.Where(node => node.Encounter.HasChartLocation).All(node => node.IsChartVisible), Is.True);
            Assert.That(session.Map.Nodes.Where(node => !node.Encounter.HasChartLocation).All(node => !node.IntelRevealed), Is.True);
            Assert.That(session.GuidedTutorialCompletedEncounters, Is.Zero);
            Assert.That(session.GuidedTutorialAmbushTriggered, Is.False);
            Assert.That(session.GuidedTutorialRandomEventTriggered, Is.False);
            Assert.That(session.GuidedTutorialBossDefeated, Is.False);
        }

        [Test]
        public void RestoringVoyagePreservesTheCountAndNeverAddsMoreReveals()
        {
            CoreVoyageSession source = Ready(2);
            source.StartVoyage();
            CoreVoyageCheckpoint saved = source.CaptureVoyageCheckpoint();
            // A saved voyage can contain previously known/visited points. Loading
            // must not replace them with another four new bonus targets.
            CoreVoyageNodeCheckpoint marked = saved.nodes.First(node => node.intelRevealed);
            marked.visited = true;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(saved, out string reason), Is.True, reason);
            Assert.That(restored.NavigatorCount, Is.EqualTo(2));
            Assert.That(restored.Map.Nodes.Select(node => node.IntelRevealed), Is.EqualTo(saved.nodes.Select(node => node.intelRevealed)));
            Assert.That(restored.Map.Nodes.Select(node => node.Visited), Is.EqualTo(saved.nodes.Select(node => node.visited)));
            restored.ConfigureCrewCapabilities(5, true, true, true, true, true);
            Assert.That(restored.NavigatorCount, Is.EqualTo(2), "Crew cannot change while sailing.");
            Assert.That(restored.CaptureVoyageCheckpoint().navigatorCount, Is.EqualTo(2));
        }

        [TestCase(false, 0)]
        [TestCase(true, 1)]
        public void LegacyBooleanConfigurationAndMissingCheckpointCountRemainCompatible(bool navigator, int expected)
        {
            var source = new CoreVoyageSession();
            source.ConfigureCrewCapabilities(navigator, false, false, false, false, false);
            Assert.That(source.NavigatorCount, Is.EqualTo(expected));
            source.StartVoyage();
            CoreVoyageCheckpoint saved = source.CaptureVoyageCheckpoint();
            saved.navigatorCount = 0; // default when this field is absent in old JSON
            foreach (CoreVoyageNodeCheckpoint node in saved.nodes) node.intelRevealed = false;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(saved, out string reason), Is.True, reason);
            Assert.That(restored.NavigatorCount, Is.EqualTo(expected));
            Assert.That(restored.Map.Nodes.Any(node => node.IntelRevealed), Is.False,
                "Old voyages receive no retroactive departure bonus while loading.");
        }

        [Test]
        public void NextDepartureAppliesTheBonusAgainAfterReturningToPort()
        {
            CoreVoyageSession session = Ready(2);
            session.StartVoyage();
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            session.Cargo.Water = session.Cargo.Food = 100;
            session.StartVoyage();
            Assert.That(session.Map.Nodes.Count(node => node.IntelRevealed), Is.EqualTo(4));
        }

        [TestCase(-1)]
        [TestCase(11)]
        public void CorruptNavigatorCountsAreRejectedWithoutMutatingThePort(int count)
        {
            CoreVoyageSession source = Ready(2);
            source.StartVoyage();
            CoreVoyageCheckpoint saved = source.CaptureVoyageCheckpoint();
            saved.navigatorCount = count;
            var target = new CoreVoyageSession();
            Assert.That(target.TryRestoreVoyageCheckpoint(saved, out _), Is.False);
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(target.Map, Is.Null);
        }

        private static CoreVoyageSession Ready(int navigators)
        {
            var session = new CoreVoyageSession();
            session.SetSeed("NAVIGATOR-DEPARTURE-REVEAL");
            session.ConfigureCrewCapabilities(navigators, true, true, true, true, true);
            session.Cargo.Water = session.Cargo.Food = 100;
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Powder = session.Cargo.Cannonballs = 0;
            session.Cargo.Beer = session.Cargo.Medicine = 0;
            return session;
        }
    }
}
