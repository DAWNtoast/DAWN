using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSeaChartEventPacingTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string OldChart = "SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6";

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void RegionalBagsContainTheWholeSeaPoolAndNoOtherSea(int route)
        {
            CoreVoyageSession session = Ready(route);
            string[] expected = CoreVoyageEventCatalog.RandomEventIds(CoreVoyageSession.Routes[route].Id)
                .Concat(new[] { "random_tailwind", "random_floating_supplies" }).ToArray();
            Assert.That(expected.Length, Is.EqualTo(24));
            CollectionAssert.AreEquivalent(expected, session.CurrentVoyageRandomEventIds);
            var seen = new List<string>();
            for (int index = 0; index < 54; index++)
            {
                string next = Draw(session);
                Assert.That(seen.Skip(Math.Max(0, seen.Count - 16)), Does.Not.Contain(next), "Recently seen events must stay behind less-recent alternatives.");
                seen.Add(next);
            }
            Assert.That(seen.Take(24).Distinct().Count(), Is.EqualTo(24));
            Assert.That(seen.Skip(24).Take(24).Distinct().Count(), Is.EqualTo(24));
        }

        [Test]
        public void HistoryFollowsTheSeaAcrossNewChartsAndSaveReloads()
        {
            CoreVoyageSession first = Ready(0);
            var previous = Enumerable.Range(0, 16).Select(_ => Draw(first)).ToArray();
            CoreSeaChartProgress progress = first.CaptureSeaChartProgress();
            var loaded = new CoreVoyageSession();
            Assert.That(loaded.RestoreSeaChartProgress(progress), Is.True);
            Assert.That(loaded.TrySelectChartSeed(CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港"), out string reason), Is.True, reason);
            loaded.StartVoyage();
            foreach (string id in loaded.CurrentVoyageRandomEventIds.Take(8)) Assert.That(previous, Does.Not.Contain(id));
            // DTOs do not expose the session's mutable history.
            progress.randomHistory[0].recentEvents[0] = "invalid";
            Assert.That(loaded.CaptureSeaChartProgress().randomHistory[0].recentEvents, Does.Not.Contain("invalid"));
        }

        [Test]
        public void InvalidHistoryIsRejectedWithoutDestroyingExistingProgress()
        {
            CoreVoyageSession source = Ready(0); Draw(source);
            var loaded = new CoreVoyageSession();
            Assert.That(loaded.RestoreSeaChartProgress(source.CaptureSeaChartProgress()), Is.True);
            string prior = loaded.CaptureSeaChartProgress().randomHistory[0].recentEvents[0];
            CoreSeaChartProgress invalid = source.CaptureSeaChartProgress();
            invalid.randomHistory[0].recentEvents = new[] { "event_moon_moon_sleepwalker" };
            Assert.That(loaded.RestoreSeaChartProgress(invalid), Is.False);
            Assert.That(loaded.CaptureSeaChartProgress().randomHistory[0].recentEvents[0], Is.EqualTo(prior));
            invalid.randomHistory[0].recentEvents = new[] { prior, prior };
            Assert.That(loaded.RestoreSeaChartProgress(invalid), Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void PacingContinuesBeyondFourAndFixedActivityAddsBreathingRoom(int route)
        {
            CoreVoyageSession session = Ready(route);
            Assert.That(Field<float>(session, "nextChartRandomDistance"), Is.InRange(160f, 240f));
            for (int i = 0; i < 16; i++)
            {
                Draw(session);
                float gap = Field<float>(session, "nextChartRandomDistance") - session.TotalDistanceNm;
                Assert.That(gap, Is.InRange(route == 1 ? 180f : 220f, route == 1 ? 280f : 340f));
            }
            Assert.That(session.TotalDistanceNm, Is.GreaterThan(2800f));
            SetField(session, "nextChartRandomDistance", session.TotalDistanceNm + 1f);
            Invoke(session, "SpaceRandomEventAfterFixedEncounter");
            Assert.That(Field<float>(session, "nextChartRandomDistance"), Is.EqualTo(session.TotalDistanceNm + 100f));
        }

        [TestCase(3)] [TestCase(24)]
        public void CheckpointPreservesActiveIncidentAndFutureBags(int count)
        {
            CoreVoyageSession source = Ready(0);
            for (int i = 0; i < count - 1; i++) Draw(source);
            Draw(source, complete: false);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.ActiveEncounter.Id, Is.EqualTo(source.ActiveEncounter.Id));
            Invoke(source, "CompleteEncounter", false); Invoke(restored, "CompleteEncounter", false);
            for (int i = 0; i < 15; i++)
            {
                Assert.That(Draw(restored), Is.EqualTo(Draw(source)));
                Assert.That(Field<float>(restored, "nextChartRandomDistance"), Is.EqualTo(Field<float>(source, "nextChartRandomDistance")));
            }
        }

        [Test]
        public void LegacyFourEventCheckpointKeepsItsScriptUntilTheNextDeparture()
        {
            CoreVoyageSession source = Ready(0);
            CoreVoyageCheckpoint old = source.CaptureVoyageCheckpoint();
            old.chartRandomVersion = 0;
            old.chartRandomOrder = new[] { "random_tailwind", "random_floating_supplies", "random_food_spoilage", "random_ambush" };
            old.chartRandomEncounterIds = (string[])old.chartRandomOrder.Clone();
            old.chartRandomIndex = 4;
            old.nextChartRandomDistance = 180f;
            old.seaChartProgress.randomHistory = null;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(old, out string reason), Is.True, reason);
            Assert.That(Field<int>(restored, "chartRandomVersion"), Is.Zero);
            var path = new[] { new VoyagePoint(0, 0), new VoyagePoint(90, 0) };
            object[] arguments = { path, new VoyagePoint(), 0f };
            Assert.That(typeof(CoreVoyageSession).GetMethod("FindSeaChartTransientEncounter", Hidden).Invoke(restored, arguments), Is.Null);
            restored.SafeReturn(); restored.ReturnToPortAfterSettlement(); restored.StartVoyage();
            Assert.That(restored.CurrentVoyageRandomEventIds.Count, Is.EqualTo(24));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void LegacyTwelveEventCheckpointKeepsItsBagAndRefillsUntilNextDeparture(int route)
        {
            CoreVoyageSession source = Ready(route);
            CoreVoyageCheckpoint old = source.CaptureVoyageCheckpoint();
            old.chartRandomVersion = 1;
            old.chartRandomOrder = CoreVoyageEventCatalog.RandomEventIds(CoreVoyageSession.Routes[route].Id).Where(id => !id.Contains("_v2_"))
                .Concat(new[] { "random_tailwind", "random_floating_supplies" }).ToArray();
            old.chartRandomEncounterIds = (string[])old.chartRandomOrder.Clone();
            old.chartRandomIndex = 5;
            old.nextChartRandomDistance = 180f;
            old.seaChartProgress.randomHistory = null;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(old, out string reason), Is.True, reason);
            Assert.That(restored.CaptureVoyageCheckpoint().chartRandomVersion, Is.EqualTo(1));
            for (int i = 5; i < 12; i++) Assert.That(Draw(restored), Is.EqualTo(old.chartRandomOrder[i]));
            var nextBag = Enumerable.Range(0, 12).Select(_ => Draw(restored)).ToArray();
            CollectionAssert.AreEquivalent(old.chartRandomOrder, nextBag);
            Assert.That(restored.CaptureSeaChartProgress().randomHistory.Single(h => h.routeIndex == route).recentEvents.Length,
                Is.EqualTo(8), "Version one keeps its historical eight-event refill ordering.");
            CoreVoyageCheckpoint resumed = restored.CaptureVoyageCheckpoint();
            var second = new CoreVoyageSession();
            Assert.That(second.TryRestoreVoyageCheckpoint(resumed, out reason), Is.True, reason);
            for (int i = 0; i < 15; i++) Assert.That(Draw(second), Is.EqualTo(Draw(restored)), "Legacy RNG continuation changed.");
            restored.SafeReturn(); restored.ReturnToPortAfterSettlement(); restored.StartVoyage();
            Assert.That(restored.CurrentVoyageRandomEventIds.Count, Is.EqualTo(24));
            Assert.That(restored.CaptureVoyageCheckpoint().chartRandomVersion, Is.EqualTo(2));
        }

        [Test]
        public void CurrentCheckpointRejectsIncompleteOrCrossVersionRandomBags()
        {
            CoreVoyageSession source = Ready(0);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            Assert.That(checkpoint.chartRandomVersion, Is.EqualTo(2));
            checkpoint.chartRandomVersion = 1;
            Assert.That(new CoreVoyageSession().TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            checkpoint.chartRandomVersion = 2;
            checkpoint.chartRandomOrder = checkpoint.chartRandomOrder.Take(12).ToArray();
            checkpoint.chartRandomEncounterIds = checkpoint.chartRandomEncounterIds.Take(12).ToArray();
            Assert.That(new CoreVoyageSession().TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
        }

        private static CoreVoyageSession Ready(int route)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
            string seed = route == 0 ? OldChart : CoreVoyageSession.CreateChartSeed(route, "断锚港", "白贝港");
            Assert.That(session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            session.StartVoyage();
            return session;
        }

        // Drive incident state transitions in isolation. The independent acceptance
        // probe covers actual Sail/path collision; these tests focus on persistence/RNG.
        private static string Draw(CoreVoyageSession session, bool complete = true)
        {
            int index = Field<int>(session, "chartRandomIndex");
            if (index >= session.CurrentVoyageRandomEventIds.Count)
            { Invoke(session, "RefillSeaChartRandomBag", new object[] { null }); index = 0; }
            string id = session.CurrentVoyageRandomEventIds[index];
            typeof(CoreVoyageSession).GetProperty("TotalDistanceNm").SetValue(session, Field<float>(session, "nextChartRandomDistance"));
            Invoke(session, "BeginSeaChartTransientEncounter", Field<CoreEncounter[]>(session, "chartRandomEncounters")[index]);
            if (complete) Invoke(session, "CompleteEncounter", false);
            return id;
        }
        private static T Field<T>(object target, string field) => (T)typeof(CoreVoyageSession).GetField(field, Hidden).GetValue(target);
        private static void SetField(object target, string field, object value) => typeof(CoreVoyageSession).GetField(field, Hidden).SetValue(target, value);
        private static object Invoke(object target, string method, params object[] args) => typeof(CoreVoyageSession).GetMethod(method, Hidden).Invoke(target, args);
    }
}
