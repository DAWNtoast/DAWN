using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSeaChartEventSerializationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string OldChart = "SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6";

        [Test]
        public void JsonKeepsActiveRegionalIncidentItsQueueAndRecentHistory()
        {
            CoreVoyageSession source = Ready();
            CoreEncounter[] events = (CoreEncounter[])typeof(CoreVoyageSession).GetField("chartRandomEncounters", Hidden).GetValue(source);
            typeof(CoreVoyageSession).GetMethod("BeginSeaChartTransientEncounter", Hidden).Invoke(source, new object[] { events[0] });
            CoreVoyageCheckpoint saved = source.CaptureVoyageCheckpoint();
            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(saved));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(decoded, out string reason), Is.True, reason);
            Assert.That(restored.ActiveEncounter.Id, Is.EqualTo(source.ActiveEncounter.Id));
            CollectionAssert.AreEqual(source.ActiveEncounter.Choices.Select(c => c.Label), restored.ActiveEncounter.Choices.Select(c => c.Label));
            CollectionAssert.AreEqual(saved.chartRandomOrder, restored.CaptureVoyageCheckpoint().chartRandomOrder);
            CollectionAssert.AreEqual(saved.seaChartProgress.randomHistory[0].recentEvents,
                restored.CaptureSeaChartProgress().randomHistory[0].recentEvents);
            Assert.That(restored.CaptureVoyageCheckpoint().encounterRandomState, Is.EqualTo(saved.encounterRandomState));
        }

        [Test]
        public void JsonWithoutNewFieldsRestoresTheLegacyFourEventVoyage()
        {
            CoreVoyageCheckpoint saved = Ready().CaptureVoyageCheckpoint();
            saved.chartRandomVersion = 0;
            saved.chartRandomOrder = new[] { "random_tailwind", "random_floating_supplies", "random_food_spoilage", "random_ambush" };
            saved.chartRandomEncounterIds = (string[])saved.chartRandomOrder.Clone();
            saved.seaChartProgress.randomHistory = Array.Empty<CoreSeaChartRandomHistory>();
            string json = JsonUtility.ToJson(saved);
            Assert.That(json, Does.Contain("\"chartRandomVersion\":0,"));
            Assert.That(json, Does.Contain(",\"randomHistory\":[]"));
            json = json.Replace("\"chartRandomVersion\":0,", string.Empty).Replace(",\"randomHistory\":[]", string.Empty);
            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(json);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(decoded, out string reason), Is.True, reason);
            Assert.That(restored.CaptureVoyageCheckpoint().chartRandomVersion, Is.Zero);
            Assert.That(restored.CurrentVoyageRandomEventIds.Count, Is.EqualTo(4));
            Assert.That(restored.Map.GeometryVersion, Is.EqualTo(1));
        }

        [Test]
        public void JsonHistorySurvivesPortProgressSavingAndRemainsDetached()
        {
            CoreVoyageSession source = Ready();
            CoreEncounter[] events = (CoreEncounter[])typeof(CoreVoyageSession).GetField("chartRandomEncounters", Hidden).GetValue(source);
            typeof(CoreVoyageSession).GetMethod("BeginSeaChartTransientEncounter", Hidden).Invoke(source, new object[] { events[0] });
            CoreSeaChartProgress decoded = JsonUtility.FromJson<CoreSeaChartProgress>(JsonUtility.ToJson(source.CaptureSeaChartProgress()));
            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreSeaChartProgress(decoded), Is.True);
            string expected = decoded.randomHistory[0].recentEvents[0];
            decoded.randomHistory[0].recentEvents[0] = "tampered";
            Assert.That(restored.CaptureSeaChartProgress().randomHistory[0].recentEvents[0], Is.EqualTo(expected));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ExpandedIncidentAndSixteenEventHistorySurviveJson(int route)
        {
            var source = new CoreVoyageSession();
            source.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
            Assert.That(source.TrySelectChartSeed(CoreVoyageSession.CreateChartSeed(route, "断锚港", "白贝港"), out string reason), Is.True, reason);
            source.StartVoyage();
            var incident = typeof(CoreVoyageSession).GetMethod("BeginSeaChartTransientEncounter", Hidden);
            var complete = typeof(CoreVoyageSession).GetMethod("CompleteEncounter", Hidden);
            CoreEncounter[] events = (CoreEncounter[])typeof(CoreVoyageSession).GetField("chartRandomEncounters", Hidden).GetValue(source);
            for (int i = 0; i < 16; i++)
            {
                incident.Invoke(source, new object[] { events[i] });
                complete.Invoke(source, new object[] { false });
            }
            CoreVoyageCheckpoint saved = source.CaptureVoyageCheckpoint();
            Assert.That(saved.chartRandomVersion, Is.EqualTo(2));
            Assert.That(saved.chartRandomOrder.Length, Is.EqualTo(24));
            Assert.That(saved.seaChartProgress.randomHistory.Single().recentEvents.Length, Is.EqualTo(16));
            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(saved));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(decoded, out reason), Is.True, reason);
            Assert.That(restored.Map.GeometryVersion, Is.EqualTo(4));
            CollectionAssert.AreEqual(saved.chartRandomOrder, restored.CurrentVoyageRandomEventIds);
            CollectionAssert.AreEqual(saved.seaChartProgress.randomHistory.Single().recentEvents,
                restored.CaptureSeaChartProgress().randomHistory.Single().recentEvents);

            // Exercise an authored new incident, preserving the actual bag and index.
            int newIndex = Array.FindIndex(events, e => e.Id.Contains("_v2_"));
            typeof(CoreVoyageSession).GetField("chartRandomIndex", Hidden).SetValue(source, newIndex);
            incident.Invoke(source, new object[] { events[newIndex] });
            decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(decoded, out reason), Is.True, reason);
            Assert.That(restored.ActiveEncounter.Id, Is.EqualTo(events[newIndex].Id));
            CollectionAssert.AreEqual(events[newIndex].Choices.Select(c => c.Detail), restored.ActiveEncounter.Choices.Select(c => c.Detail));
        }

        [Test]
        public void TwelveEventVersionOneJsonKeepsItsOriginalBag()
        {
            var source = Ready();
            typeof(CoreVoyageSession).GetField("chartRandomVersion", Hidden).SetValue(source, 1);
            typeof(CoreVoyageSession).GetMethod("RefillSeaChartRandomBag", Hidden).Invoke(source, new object[] { null });
            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(decoded, out string reason), Is.True, reason);
            Assert.That(restored.CaptureVoyageCheckpoint().chartRandomVersion, Is.EqualTo(1));
            Assert.That(restored.CurrentVoyageRandomEventIds.Count, Is.EqualTo(12));
            CollectionAssert.AreEqual(source.CurrentVoyageRandomEventIds, restored.CurrentVoyageRandomEventIds);
        }

        private static CoreVoyageSession Ready()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TrySelectChartSeed(OldChart, out string reason), Is.True, reason);
            session.StartVoyage();
            return session;
        }
    }
}
