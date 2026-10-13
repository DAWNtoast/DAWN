using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreUsedTreasureMapTests
    {
        [Test]
        public void DepartureArchivesOnlyAfterValidationAndKeepsConsumedMapInHistory()
        {
            CoreVoyageSession session = Ready("gilded", "USED-DEPARTURE");
            session.Cargo.Food = 0;
            Assert.That(session.TryStartTreasureVoyage(out _), Is.False);
            Assert.That(session.UsedTreasureMaps, Is.Empty);
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(1));
            session.Cargo.Food = 100;
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            Assert.That(session.TreasureMaps, Is.Empty);
            CoreUsedTreasureMapRecord record = session.UsedTreasureMaps.Single();
            Assert.That(record.Id, Is.EqualTo("gilded:USED-DEPARTURE"));
            Assert.That(record.Seed, Is.EqualTo("USED-DEPARTURE"));
            Assert.That(record.Rank.Name, Is.EqualTo("鎏金"));
            Assert.That(record.DeparturePort, Is.EqualTo(session.Map.DepartureName));
            Assert.That(record.HasExplorationCoverage, Is.True);
            Assert.That(session.CaptureTreasureProgress().usedMaps.Single().explorationCoverage,
                Is.EqualTo(session.ExplorationCoverage()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RetreatAndFailureBothKeepFinalExplorationWithoutReturningTheMap(bool failure)
        {
            CoreVoyageSession session = Ready("moonlit", "USED-OUTCOME-" + failure);
            session.StartVoyage();
            RevealHalf(session);
            if (failure) session.Cargo.Water = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(failure ? VoyageOutcome.Failure : VoyageOutcome.SafeReturn));
            Assert.That(session.UsedTreasureMaps.Single().ExplorationCoverage, Is.EqualTo(50f));
            Assert.That(session.CompletedTreasureSeeds, Is.Empty,
                "Consumed history is separate from the successful-recovery ledger.");
            session.ReturnToPortAfterSettlement();
            session.ReturnToPortAfterSettlement();
            Assert.That(session.TreasureMaps, Is.Empty);
            Assert.That(session.UsedTreasureMaps.Count, Is.EqualTo(1));
            Assert.That(session.UsedTreasureMaps.Single().ExplorationCoverage, Is.EqualTo(50f));
            session.RestoreTreasureProgress(session.CaptureTreasureProgress());
            Assert.That(session.TrySelectTreasureSeed("USED-OUTCOME-" + failure, out _), Is.False);
            Assert.That(session.UsedTreasureMaps.Single().ExplorationCoverage, Is.EqualTo(50f));
        }

        [Test]
        public void UsedPreviewRegeneratesActualFullGeographyWithoutMutatingTheLiveMapOrFog()
        {
            CoreVoyageSession session = Ready("weathered", "USED-GEOGRAPHY");
            session.StartVoyage();
            RevealHalf(session);
            CoreVoyageMap original = session.Map;
            bool[] fog = (bool[])original.ExploredCells.Clone();
            CoreVoyageMap preview = session.PreviewUsedTreasureMap(session.UsedTreasureMaps.Single());
            Assert.That(preview, Is.Not.SameAs(original));
            Assert.That(preview.Hash, Is.EqualTo(original.Hash));
            Assert.That(preview.Seed, Is.EqualTo(original.Seed));
            Assert.That(preview.TerrainCells, Is.EqualTo(original.TerrainCells));
            Assert.That(preview.Nodes.Select(node => node.Position), Is.EqualTo(original.Nodes.Select(node => node.Position)));
            Assert.That(session.Map, Is.SameAs(original));
            Assert.That(session.Map.ExploredCells, Is.EqualTo(fog));
            Assert.That(preview.ExploredCells.Any(cell => cell), Is.False);
        }

        [Test]
        public void CheckpointPreservesConsumedArchiveAndMeasuredExploration()
        {
            CoreVoyageSession source = Ready("gilded", "USED-CHECKPOINT");
            source.StartVoyage();
            RevealHalf(source);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            Assert.That(checkpoint.treasureProgress.usedMaps.Single().explorationCoverage, Is.EqualTo(50f));
            var resumed = new CoreVoyageSession();
            Assert.That(resumed.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(resumed.TreasureMaps, Is.Empty);
            Assert.That(resumed.UsedTreasureMaps.Single().RankId, Is.EqualTo("gilded"));
            Assert.That(resumed.UsedTreasureMaps.Single().ExplorationCoverage, Is.EqualTo(50f));
            Assert.That(resumed.PreviewUsedTreasureMap(resumed.UsedTreasureMaps.Single()).TerrainCells,
                Is.EqualTo(source.Map.TerrainCells));
            Assert.That(resumed.SafeReturn(), Is.True);
            resumed.ReturnToPortAfterSettlement();
            Assert.That(resumed.UsedTreasureMaps.Count, Is.EqualTo(1));
        }

        [Test]
        public void LegacyCheckpointRecoversActualActiveRankAndExplorationFromStoredVoyage()
        {
            CoreVoyageSession source = Ready("moonlit", "USED-LEGACY-ACTIVE");
            source.StartVoyage(); RevealHalf(source);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureProgress.usedMaps = null;
            var resumed = new CoreVoyageSession();
            Assert.That(resumed.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            CoreUsedTreasureMapRecord record = resumed.UsedTreasureMaps.Single();
            Assert.That(record.Rank.Name, Is.EqualTo("月蚀"));
            Assert.That(record.HasExplorationCoverage, Is.True);
            Assert.That(record.ExplorationCoverage, Is.EqualTo(50f));
            Assert.That(record.Id, Is.EqualTo(source.ActiveTreasureMap.Id));
        }

        [Test]
        public void LegacyIssuedHistoryExcludesHeldSeedsAndLeavesMissingRankAndExplorationUnknown()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                issuedSeeds = new[] { "HELD", "FAILED", "COMPLETED", "FAILED" }, completedSeeds = new[] { "COMPLETED" },
                maps = new[] { new CoreTreasureMapSave { id = "weathered:HELD", seed = "HELD", rankId = "weathered" } }
            });
            Assert.That(session.TreasureMaps.Single().Seed, Is.EqualTo("HELD"));
            Assert.That(session.UsedTreasureMaps.Select(map => map.Seed), Is.EquivalentTo(new[] { "FAILED", "COMPLETED" }));
            foreach (CoreUsedTreasureMapRecord record in session.UsedTreasureMaps)
            {
                Assert.That(record.Rank, Is.Null);
                Assert.That(record.HasExplorationCoverage, Is.False);
                Assert.That(session.PreviewUsedTreasureMap(record), Is.Null);
            }
        }

        [Test]
        public void LegacyCompletedMapWithRetainedRankUsesKnownCategoryButUnknownExploration()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                completedSeeds = new[] { "KNOWN" },
                maps = new[] { new CoreTreasureMapSave { id = "gilded:KNOWN", seed = "KNOWN", rankId = "gilded" } }
            });
            Assert.That(session.TreasureMaps, Is.Empty);
            CoreUsedTreasureMapRecord record = session.UsedTreasureMaps.Single();
            Assert.That(record.Rank.Name, Is.EqualTo("鎏金"));
            Assert.That(record.HasExplorationCoverage, Is.False);
            Assert.That(session.PreviewUsedTreasureMap(record), Is.Not.Null);
        }

        [Test]
        public void ArchiveJsonRoundTripPreservesUsedCategoryAndExploration()
        {
            CoreVoyageSession source = Ready("weathered", "USED-JSON");
            source.StartVoyage(); RevealHalf(source); source.SafeReturn(); source.ReturnToPortAfterSettlement();
            string json = JsonUtility.ToJson(source.CaptureTreasureProgress());
            var restored = new CoreVoyageSession();
            restored.RestoreTreasureProgress(JsonUtility.FromJson<CoreTreasureProgress>(json));
            CoreUsedTreasureMapRecord record = restored.UsedTreasureMaps.Single();
            Assert.That(record.Seed, Is.EqualTo("USED-JSON"));
            Assert.That(record.Rank.Name, Is.EqualTo("残旧"));
            Assert.That(record.HasExplorationCoverage, Is.True);
            Assert.That(record.ExplorationCoverage, Is.EqualTo(50f));
            Assert.That(restored.TreasureMaps, Is.Empty);
            Assert.That(restored.CaptureTreasureProgress().issuedSeeds, Does.Contain("USED-JSON"));
        }

        [Test]
        public void CheckpointWithConflictingUsedCategoryIsRejectedWithoutMutatingTheProfile()
        {
            CoreVoyageSession source = Ready("gilded", "USED-CONFLICT");
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureProgress.usedMaps[0].rankId = "moonlit";
            var target = new CoreVoyageSession();
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(target.UsedTreasureMaps, Is.Empty);
        }

        private static void RevealHalf(CoreVoyageSession session)
        {
            for (int i = 0; i < session.Map.ExploredCells.Length; i++)
                session.Map.ExploredCells[i] = i < session.Map.ExploredCells.Length / 2;
        }

        private static CoreVoyageSession Ready(string rank, string seed)
        {
            var session = new CoreVoyageSession();
            string id = rank + ":" + seed;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = id,
                maps = new[] { new CoreTreasureMapSave { id = id, seed = seed, rankId = rank } } });
            session.Cargo.Water = session.Cargo.Food = 100;
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Powder = session.Cargo.Cannonballs = 0;
            session.Cargo.Beer = session.Cargo.Medicine = 0;
            return session;
        }
    }
}
