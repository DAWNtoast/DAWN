using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTreasureContentVersionTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string BaselineSeed = "TREASURE-CONTENT-BASELINE";

        // Captured from the pre-expedition compiled Core on 2026-10-15. These
        // include node IDs, event IDs, exact positions and every terrain cell.
        [TestCase("weathered", 0, "78A82602810F44849BE7F2FDA13BDB27821EA8830313489B5DC042639C1DE30F")]
        [TestCase("weathered", 1, "7B9182C1B384EDD35E7405A0246B0D1DC537D3D374D4878035587F0766309701")]
        [TestCase("weathered", 2, "7B9182C1B384EDD35E7405A0246B0D1DC537D3D374D4878035587F0766309701")]
        [TestCase("gilded", 0, "1DB226660111C1922DA2262A16D6901203B646414014047CCE2750DF8AABA5D6")]
        [TestCase("gilded", 1, "9D1BE60D3D5D6D3693B551DC7C934B807DFF2C4719AD0B365837CF79906905C5")]
        [TestCase("gilded", 2, "9D1BE60D3D5D6D3693B551DC7C934B807DFF2C4719AD0B365837CF79906905C5")]
        [TestCase("moonlit", 0, "33719B27C05983A52DF8CDAC4ECC2F606C82613B3E471CA8C2F81776C7162764")]
        [TestCase("moonlit", 1, "0796E0BB83CBEE5402F1644E9E12569A6DAD5E9B62E7F82D471BC8D26E75F3CD")]
        [TestCase("moonlit", 2, "0796E0BB83CBEE5402F1644E9E12569A6DAD5E9B62E7F82D471BC8D26E75F3CD")]
        public void LegacyArchivePreservesExactOriginalEventsNodesAndTerrain(string rank, int geometry, string expected)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { usedMaps = new[] {
                new CoreUsedTreasureMapSave { id = "legacy", seed = BaselineSeed, rankId = rank,
                    departurePort = "断锚港", mapGeometryVersion = geometry } } });
            Assert.That(session.UsedTreasureMaps.Single().ContentVersion, Is.Zero);
            Assert.That(MapDigest(session.PreviewUsedTreasureMap(session.UsedTreasureMaps.Single())), Is.EqualTo(expected));
            Assert.That(session.CaptureTreasureProgress().usedMaps.Single().contentVersion, Is.Zero);
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void NewMapsAlwaysContainVisibleObjectivesAndKeepSeededSelectionStable(string rank)
        {
            string[] pool = CoreTreasureExpeditionCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank(rank)).Select(e => e.Id).ToArray();
            foreach (int seed in Enumerable.Range(0, 18))
            {
                CoreVoyageSession first = Ready(rank, "NEW-CONTENT-" + seed);
                CoreVoyageSession second = Ready(rank, "NEW-CONTENT-" + seed);
                Assert.That(first.TryStartTreasureVoyage(out string reason), Is.True, reason);
                Assert.That(second.TryStartTreasureVoyage(out reason), Is.True, reason);
                Assert.That(first.ActiveTreasureMap.ContentVersion, Is.EqualTo(1));
                Assert.That(MapDigest(first.Map), Is.EqualTo(MapDigest(second.Map)));
                Assert.That(first.Map.Nodes.Select(n => n.Encounter.Id).Distinct().Count(), Is.EqualTo(first.Map.Nodes.Count));
                foreach (string objective in CoreTreasureExpeditionCatalog.GuaranteedEventIds(rank))
                {
                    CoreMapNode node = first.Map.Nodes.Single(n => n.Encounter.Id == objective);
                    Assert.That(node.Discovered && node.IntelRevealed, Is.True, objective);
                }
                Assert.That(first.Map.Nodes.Count(n => pool.Contains(n.Encounter.Id)),
                    Is.EqualTo(first.ActiveTreasureMap.Rank.SidePointCount + 1));
                Assert.That(first.Map.Nodes.Count(n => n.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId), Is.EqualTo(1));
                Assert.That(MapDigest(first.PreviewUsedTreasureMap(first.UsedTreasureMaps.Single())), Is.EqualTo(MapDigest(first.Map)));
            }
        }

        [Test]
        public void NewCraftsUseCurrentContentAndUnusedLegacyChartsUpgradeOnlyAfterDepartureValidation()
        {
            var crafted = new CoreVoyageSession();
            crafted.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, identifiedWeatheredFragments = 4 });
            Assert.That(crafted.TryCraftTreasureMapForRank("weathered", "NEW-CRAFT", out string reason), Is.True, reason);
            Assert.That(crafted.TreasureMaps.Single().ContentVersion, Is.EqualTo(1));

            CoreVoyageSession held = Ready("weathered", "HELD-LEGACY");
            Assert.That(held.SelectedTreasureMap.ContentVersion, Is.Zero);
            held.Cargo.Food = 0;
            Assert.That(held.TryStartTreasureVoyage(out _), Is.False);
            Assert.That(held.SelectedTreasureMap.ContentVersion, Is.Zero);
            Assert.That(held.UsedTreasureMaps, Is.Empty);
            held.Cargo.Food = 100;
            Assert.That(held.TryStartTreasureVoyage(out reason), Is.True, reason);
            Assert.That(held.TreasureMaps, Is.Empty);
            Assert.That(held.ActiveTreasureMap.ContentVersion, Is.EqualTo(1));
            Assert.That(held.UsedTreasureMaps.Single().ContentVersion, Is.EqualTo(1));
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void ActiveLegacyCheckpointKeepsLegacyEventLayoutAndEmptyExpeditionState(string rank)
        {
            CoreVoyageCheckpoint checkpoint = LegacyCheckpoint(rank);
            string[] ids = checkpoint.nodes.Select(n => n.encounterId).ToArray();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.ActiveTreasureMap.ContentVersion, Is.Zero);
            Assert.That(restored.UsedTreasureMaps.Single().ContentVersion, Is.Zero);
            Assert.That(restored.Map.Nodes.Select(n => n.Encounter.Id), Is.EqualTo(ids));
            Assert.That(restored.CaptureVoyageCheckpoint().treasureExpeditionMarks, Is.Empty);
            Assert.That(restored.CaptureVoyageCheckpoint().treasureExpeditionCredit, Is.Zero);
            Assert.That(restored.TreasureMaps, Is.Empty);
        }

        [TestCase("weathered", "weathered_winch", 0, 0, 0)]
        [TestCase("gilded", "gilded_manifest", 3, 2, 0)]
        [TestCase("moonlit", "moonlit_dawn", 0, 0, 1)]
        public void ExpeditionCheckpointRestoresVersionMarksAndRankSpecificCounters(string rank, string mark, int credit, int heat, int resonance)
        {
            CoreVoyageSession source = Ready(rank, "EXPEDITION-STATE");
            source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureExpeditionMarks = new[] { mark };
            checkpoint.treasureExpeditionCredit = credit;
            checkpoint.treasureExpeditionHeat = heat;
            checkpoint.treasureExpeditionResonance = resonance;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            CoreVoyageCheckpoint saved = restored.CaptureVoyageCheckpoint();
            Assert.That(saved.activeTreasureMap.contentVersion, Is.EqualTo(1));
            Assert.That(saved.treasureProgress.usedMaps.Single().contentVersion, Is.EqualTo(1));
            Assert.That(saved.treasureExpeditionMarks, Is.EqualTo(checkpoint.treasureExpeditionMarks));
            Assert.That(saved.treasureExpeditionCredit, Is.EqualTo(credit));
            Assert.That(saved.treasureExpeditionHeat, Is.EqualTo(heat));
            Assert.That(saved.treasureExpeditionResonance, Is.EqualTo(resonance));
            Assert.That(MapDigest(restored.Map), Is.EqualTo(MapDigest(source.Map)));
        }

        [TestCase(false, -1)] [TestCase(false, 2)]
        [TestCase(true, -1)] [TestCase(true, 2)]
        public void UnknownInventoryOrArchiveContentVersionsRejectBeforeMutatingProfile(bool used, int version)
        {
            CoreVoyageSession target = Ready("weathered", "KEEP-THIS");
            target.Cargo.Gem = 7;
            CoreTreasureProgress invalid = used
                ? new CoreTreasureProgress { usedMaps = new[] { new CoreUsedTreasureMapSave { id = "future", seed = "FUTURE", rankId = "gilded", contentVersion = version } } }
                : new CoreTreasureProgress { maps = new[] { new CoreTreasureMapSave { id = "future", seed = "FUTURE", rankId = "gilded", contentVersion = version } } };
            Assert.Throws<ArgumentException>(() => target.RestoreTreasureProgress(invalid));
            Assert.That(target.TreasureMaps.Single().Seed, Is.EqualTo("KEEP-THIS"));
            Assert.That(target.UsedTreasureMaps, Is.Empty);
            Assert.That(target.Cargo.Gem, Is.EqualTo(7));
            Assert.That(target.PirateExperience, Is.EqualTo(1400));
        }

        [TestCase("unknown-version")]
        [TestCase("archive-version-mismatch")]
        [TestCase("unknown-mark")]
        [TestCase("duplicate-mark")]
        [TestCase("wrong-rank-mark")]
        [TestCase("credit-overflow")]
        [TestCase("heat-negative")]
        [TestCase("resonance-wrong-rank")]
        [TestCase("legacy-marks")]
        [TestCase("ordinary-marks")]
        public void InvalidCheckpointRejectsAtomically(string invalid)
        {
            CoreVoyageSession source = Ready("gilded", "INVALID-EXPEDITION"); source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            switch (invalid)
            {
                case "unknown-version": checkpoint.activeTreasureMap.contentVersion = 2; break;
                case "archive-version-mismatch": checkpoint.treasureProgress.usedMaps[0].contentVersion = 0; break;
                case "unknown-mark": checkpoint.treasureExpeditionMarks = new[] { "future-mark" }; break;
                case "duplicate-mark": checkpoint.treasureExpeditionMarks = new[] { "gilded_manifest", "gilded_manifest" }; break;
                case "wrong-rank-mark": checkpoint.treasureExpeditionMarks = new[] { "weathered_winch" }; break;
                case "credit-overflow": checkpoint.treasureExpeditionCredit = 7; break;
                case "heat-negative": checkpoint.treasureExpeditionHeat = -1; break;
                case "resonance-wrong-rank": checkpoint.treasureExpeditionResonance = 1; break;
                case "legacy-marks": checkpoint = LegacyCheckpoint("gilded"); checkpoint.treasureExpeditionMarks = new[] { "gilded_manifest" }; break;
                case "ordinary-marks":
                    source = new CoreVoyageSession(); source.StartVoyage(); checkpoint = source.CaptureVoyageCheckpoint();
                    checkpoint.treasureExpeditionMarks = new[] { "gilded_manifest" }; break;
            }
            CoreVoyageSession target = Ready("weathered", "PRESERVE-INVENTORY"); target.Cargo.Gem = 5;
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(target.Map, Is.Null);
            Assert.That(target.TreasureMaps.Single().Seed, Is.EqualTo("PRESERVE-INVENTORY"));
            Assert.That(target.Cargo.Gem, Is.EqualTo(5));
            Assert.That(target.UsedTreasureMaps, Is.Empty);
        }

        private static CoreVoyageCheckpoint LegacyCheckpoint(string rank)
        {
            CoreVoyageSession source = Ready(rank, "ACTIVE-LEGACY"); source.StartVoyage();
            var record = (CoreTreasureMapRecord)typeof(CoreTreasureMapRecord).GetConstructors(Hidden).Single()
                .Invoke(new object[] { "active-legacy", "ACTIVE-LEGACY", rank, source.Map.GeometryVersion, 0 });
            var map = (CoreVoyageMap)typeof(CoreVoyageSession).GetMethod("GenerateTreasureMap", Hidden)
                .Invoke(source, new object[] { record, source.Map.DepartureName });
            typeof(CoreVoyageSession).GetProperty("Map").SetValue(source, map);
            typeof(CoreVoyageSession).GetProperty("ActiveTreasureMap").SetValue(source, record);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureProgress.usedMaps = new[] { new CoreUsedTreasureMapSave {
                id = record.Id, seed = record.Seed, rankId = record.RankId,
                mapGeometryVersion = record.MapGeometryVersion, departurePort = source.CurrentPort } };
            checkpoint.treasureExpeditionMarks = null;
            return checkpoint;
        }

        private static CoreVoyageSession Ready(string rank, string seed)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = rank + ":" + seed,
                maps = new[] { new CoreTreasureMapSave { id = rank + ":" + seed, seed = seed, rankId = rank, mapGeometryVersion = 1 } } });
            session.Cargo.Water = session.Cargo.Food = 100;
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Powder = session.Cargo.Cannonballs = 0;
            session.Cargo.Beer = session.Cargo.Medicine = 0;
            return session;
        }

        private static string MapDigest(CoreVoyageMap map)
        {
            var text = new StringBuilder();
            foreach (CoreMapNode node in map.Nodes)
                text.Append(node.Id).Append('|').Append(node.Encounter.Id).Append('|')
                    .Append(node.Position.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                    .Append(node.Position.Y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            foreach (SeaTerrainType cell in map.TerrainCells) text.Append((int)cell).Append(',');
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", string.Empty);
        }
    }
}
