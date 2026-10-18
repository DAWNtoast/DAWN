using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTreasureContentJsonTests
    {
        [TestCase("weathered", "weathered_winch", 0, 0, 0)]
        [TestCase("gilded", "gilded_manifest", 4, 2, 0)]
        [TestCase("moonlit", "moonlit_dawn", 0, 0, 1)]
        public void NativeJsonPreservesCurrentContentAndExpeditionState(string rank, string mark, int credit, int heat, int resonance)
        {
            CoreVoyageSession source = Start(rank);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureExpeditionMarks = new[] { mark };
            checkpoint.treasureExpeditionCredit = credit; checkpoint.treasureExpeditionHeat = heat;
            checkpoint.treasureExpeditionResonance = resonance;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(checkpoint)),
                out string reason), Is.True, reason);
            CoreVoyageCheckpoint saved = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(restored.CaptureVoyageCheckpoint()));
            Assert.That(saved.activeTreasureMap.contentVersion, Is.EqualTo(1));
            Assert.That(saved.treasureProgress.usedMaps.Single().contentVersion, Is.EqualTo(1));
            Assert.That(saved.treasureExpeditionMarks, Is.EqualTo(checkpoint.treasureExpeditionMarks));
            Assert.That(saved.treasureExpeditionCredit, Is.EqualTo(credit));
            Assert.That(saved.treasureExpeditionHeat, Is.EqualTo(heat));
            Assert.That(saved.treasureExpeditionResonance, Is.EqualTo(resonance));
            Assert.That(saved.nodes.Select(n => n.encounterId), Is.EqualTo(checkpoint.nodes.Select(n => n.encounterId)));
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void NativeJsonWithoutNewFieldsResumesLegacyVoyageWithoutReplacingEvents(string rank)
        {
            CoreVoyageSession source = Start(rank);
            CoreTreasureMapRecord active = source.ActiveTreasureMap;
            var legacy = (CoreTreasureMapRecord)typeof(CoreTreasureMapRecord)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                .Invoke(new object[] { active.Id, active.Seed, active.RankId, active.MapGeometryVersion, 0 });
            var map = (CoreVoyageMap)typeof(CoreVoyageSession).GetMethod("GenerateTreasureMap", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(source, new object[] { legacy, source.CurrentPort });
            typeof(CoreVoyageSession).GetProperty("Map").SetValue(source, map);
            typeof(CoreVoyageSession).GetProperty("ActiveTreasureMap").SetValue(source, legacy);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.treasureProgress.usedMaps[0].contentVersion = 0;
            string json = JsonUtility.ToJson(checkpoint);
            json = Regex.Replace(json, ",\"(?:contentVersion|treasureExpeditionCredit|treasureExpeditionHeat|treasureExpeditionResonance)\":0(?=[,}])", string.Empty);
            json = Regex.Replace(json, ",\"treasureExpeditionMarks\":(?:\\[\\]|null)(?=[,}])", string.Empty);
            Assert.That(json, Does.Not.Contain("contentVersion"));
            Assert.That(json, Does.Not.Contain("treasureExpedition"));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(json), out string reason), Is.True, reason);
            Assert.That(restored.ActiveTreasureMap.ContentVersion, Is.Zero);
            Assert.That(restored.UsedTreasureMaps.Single().ContentVersion, Is.Zero);
            Assert.That(restored.Map.Nodes.Select(n => n.Encounter.Id), Is.EqualTo(map.Nodes.Select(n => n.Encounter.Id)));
            Assert.That(restored.Map.Nodes.Select(n => n.Position), Is.EqualTo(map.Nodes.Select(n => n.Position)));
            Assert.That(restored.TreasureExpeditionCredit, Is.Zero);
            Assert.That(restored.TreasureExpeditionHeat, Is.Zero);
            Assert.That(restored.TreasureExpeditionResonance, Is.Zero);
            Assert.That(restored.CaptureVoyageCheckpoint().treasureExpeditionMarks, Is.Empty);
        }

        [Test]
        public void NativeJsonWithoutContentVersionKeepsHeldAndArchivedChartsLegacyUntilNewDeparture()
        {
            const string json = "{\"pirateExperience\":1400,\"selectedMapId\":\"held\",\"maps\":[{\"id\":\"held\",\"seed\":\"HELD-OLD\",\"rankId\":\"weathered\"}],\"usedMaps\":[{\"id\":\"used\",\"seed\":\"USED-OLD\",\"rankId\":\"moonlit\",\"departurePort\":\"断锚港\"}]}";
            var restored = new CoreVoyageSession();
            restored.RestoreTreasureProgress(JsonUtility.FromJson<CoreTreasureProgress>(json));
            Assert.That(restored.SelectedTreasureMap.ContentVersion, Is.Zero);
            Assert.That(restored.UsedTreasureMaps.Single().ContentVersion, Is.Zero);
            Assert.That(restored.PreviewUsedTreasureMap(restored.UsedTreasureMaps.Single()).Nodes
                .All(n => !n.Encounter.Id.StartsWith("weathered_") && !n.Encounter.Id.StartsWith("gilded_") && !n.Encounter.Id.StartsWith("moonlit_")), Is.True);
            Assert.That(restored.TryStartTreasureVoyage(out string reason), Is.True, reason);
            Assert.That(restored.ActiveTreasureMap.ContentVersion, Is.EqualTo(1));
            Assert.That(restored.UsedTreasureMaps.Single(m => m.Seed == "USED-OLD").ContentVersion, Is.Zero);
        }

        private static CoreVoyageSession Start(string rank)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = "json-map",
                maps = new[] { new CoreTreasureMapSave { id = "json-map", seed = "CONTENT-JSON", rankId = rank, mapGeometryVersion = 1 } } });
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            return session;
        }
    }
}
