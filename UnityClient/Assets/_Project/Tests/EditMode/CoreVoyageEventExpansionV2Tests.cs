using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageEventExpansionV2Tests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly string[][] Anchors = {
            new[] { "event_reef_broken_mast", "event_reef_hidden_cache" },
            new[] { "common_beach_castaway", "gold_wreck" },
            new[] { "common_strait_beacon", "moon_obelisk" } };

        // Captured by executing the already-built 2026-10-15 pre-expansion assembly,
        // not by recomputing a baseline from the implementation under test.
        [TestCase(0, 0, "17566860EC84AC0B8CCD051F1DACA8FB23CC1F88B5EDB0C6708EAF185124F7D6")]
        [TestCase(0, 1, "2F3A4FEDEEE068E6715E759BC7A726E41A4703BE634A207FB4065831AC28628A")]
        [TestCase(0, 2, "0E4881332E9A71810642A3BB0CF34AEB874F07E15E0D0B69888C1C438570EA94")]
        [TestCase(1, 0, "39F996EF8F4124F8FCE17540C8491996E684DE298227540A870581B9FE44EBE6")]
        [TestCase(1, 1, "B8BDE28413E5FA78D668D66AAB1FF6C9C808D6A1E0B280FCD107FBB973730439")]
        [TestCase(1, 2, "024002163D311C4A8CAEBF61CE0A7CC45A8B42654ACA07EC257C4F4E53318E27")]
        [TestCase(2, 0, "BBB50780E0C51C79FF187082DD292AC59AD13F0822A34E6B36750FDCD4382150")]
        [TestCase(2, 1, "7ECFD806C6EC555B4F3F8812B9C8BDE4A09E990CD612155C14DCD4B29B2EF124")]
        [TestCase(2, 2, "00838356CE23463558E0CB442AB443D0FCE054CCB95C834D6FFC83F6BA4743AD")]
        public void Ss4NodeAndTerrainSnapshotsRemainUnchanged(int route, int sample, string expected) =>
            Assert.That(Signature(Generate(2, route, sample, out _)), Is.EqualTo(expected));

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void Ss5RotatesOrdinaryStopsWithoutRemovingTheExplorationChain(int route)
        {
            var combinations = new HashSet<string>();
            var covered = new HashSet<string>();
            var expanded = CoreVoyageEventCatalog.CreateExpandedFixedEncounters(CoreVoyageSession.Routes[route].Id)
                .Select(e => e.Id).ToArray();
            for (int sample = 0; sample < 16; sample++)
            {
                CoreVoyageMap map = Generate(3, route, sample, out CoreVoyageSession session);
                var baseline = ((IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession).GetMethod("CreateEncounterPool", Hidden)
                    .Invoke(session, new object[] { map.Route })).Select(e => e.Id).ToArray();
                var ordinary = map.Nodes.Where(n => baseline.Contains(n.Encounter.Id) || expanded.Contains(n.Encounter.Id)).ToArray();
                Assert.That(ordinary.Length, Is.EqualTo(13));
                Assert.That(ordinary.Count(n => expanded.Contains(n.Encounter.Id)), Is.EqualTo(8));
                Assert.That(ordinary.Count(n => baseline.Contains(n.Encounter.Id)), Is.EqualTo(5));
                Assert.That(ordinary.Count(n => n.Encounter.CanDiscoverBySight), Is.GreaterThanOrEqualTo(3));
                foreach (string anchor in Anchors[route]) Assert.That(ordinary.Any(n => n.Encounter.Id == anchor), Is.True, anchor);
                Assert.That(map.Nodes.Select(n => n.Encounter.Id).Distinct().Count(), Is.EqualTo(map.Nodes.Count));
                Assert.That(session.PlanNavigablePath(map.Departure, map.Destination).Count, Is.EqualTo(2));
                Assert.That(session.PlanNavigablePath(map.Destination, map.Departure).Count, Is.EqualTo(2));
                foreach (var node in ordinary)
                {
                    Assert.That(session.PlanNavigablePath(map.Departure, node.Position).Count, Is.GreaterThanOrEqualTo(2), node.Encounter.Id);
                    if (expanded.Contains(node.Encounter.Id)) covered.Add(node.Encounter.Id);
                }
                var reservations = new HashSet<int>();
                foreach (var footprint in map.EventFootprints) foreach (int cell in footprint.ReservedCellIndices)
                    Assert.That(reservations.Add(cell), Is.True, "Overlapping fixed-event safety bands.");
                combinations.Add(string.Join("|", ordinary.Select(n => n.Encounter.Id).OrderBy(id => id)));
                Assert.That(Signature(Generate(3, route, sample, out _)), Is.EqualTo(Signature(map)), "Regeneration changed the chart.");
            }
            Assert.That(combinations.Count, Is.GreaterThanOrEqualTo(12));
            CollectionAssert.AreEquivalent(expanded, covered, "The sampling corpus should exercise all sixteen optional locations.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EveryNewChoiceExecutesWithItsActualCrewAndDiscoveryRequirements(int route)
        {
            foreach (var encounter in NewEvents(route)) foreach (int index in Enumerable.Range(0, encounter.Choices.Count))
            {
                CoreVoyageSession session = Ready(route);
                CoreEncounterChoice choice = encounter.Choices[index];
                PrepareChoice(session, choice);
                Enter(session, encounter);
                Assert.That(session.IsChoiceAvailable(index), Is.True, encounter.Id + " / " + choice.Label + " / " + session.EncounterChoiceConditionReason(choice));
                Assert.That(session.ResolveChoice(index), Is.True, encounter.Id + " / " + choice.Label);
                if (choice.StartsBattle)
                {
                    Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
                    session.Battle.EnemyHull = 0;
                    Assert.That(session.EndBattleTurn(), Is.True);
                }
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing), encounter.Id + " did not release sailing.");
                Assert.That(session.Hull, Is.GreaterThan(0));
                foreach (var property in typeof(VoyageCargo).GetProperties().Where(p => p.PropertyType == typeof(int)))
                    Assert.That((int)property.GetValue(session.Cargo), Is.GreaterThanOrEqualTo(0), encounter.Id + " / " + property.Name);
                if (!string.IsNullOrEmpty(choice.Effect.GrantDiscovery)) Assert.That(session.HasDiscovery(choice.Effect.GrantDiscovery), Is.True);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void NewEventsCannotTrapAOneHullShipWithoutSuppliesOrCrew(int route)
        {
            foreach (var encounter in NewEvents(route))
            {
                CoreVoyageSession session = Ready(route);
                ClearCargo(session);
                typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1);
                session.ConfigureCrewCapabilities(false, false, false, false, false, false);
                Enter(session, encounter);
                if (encounter.Mandatory)
                {
                    int fallback = Enumerable.Range(0, encounter.Choices.Count).FirstOrDefault(i =>
                        encounter.Choices[i].Hours == 0 && !encounter.Choices[i].StartsBattle && session.IsChoiceAvailable(i));
                    Assert.That(session.IsChoiceAvailable(fallback), Is.True, encounter.Id);
                    Assert.That(session.ResolveChoice(fallback), Is.True, encounter.Id);
                }
                else Assert.That(session.SkipEncounter(), Is.True, encounter.Id);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing), encounter.Id);
                Assert.That(session.Hull, Is.EqualTo(1), encounter.Id);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void NewFixedLocationRewardsAndVisitClaimsSurviveCheckpointRestore(int route)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            Assert.That(session.TrySelectChartSeed(CoreVoyageSession.CreateChartSeed(route, "断锚港", "白贝港"), out string reason), Is.True, reason);
            session.StartVoyage();
            var candidates = session.Map.Nodes.Where(n => n.Encounter.Id.Contains("_v2_")).Select(n => n.Encounter.Id).ToArray();
            Assert.That(candidates, Is.Not.Empty);
            foreach (string id in candidates)
            {
                CoreMapNode node = session.Map.Nodes.Single(n => n.Encounter.Id == id);
                typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(session, node);
                typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, node.Position);
                typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
                typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
                node.Discovered = true;
                int index = -1;
                for (int candidate = 0; candidate < node.Encounter.Choices.Count; candidate++)
                {
                    if (node.Encounter.Choices[candidate].StartsBattle) continue;
                    PrepareChoice(session, node.Encounter.Choices[candidate]);
                    if (!session.IsChoiceAvailable(candidate)) continue;
                    index = candidate; break;
                }
                Assert.That(index, Is.GreaterThanOrEqualTo(0), id + " has no applicable choice after prior clue rewards.");
                CoreVoyageCheckpoint active = session.CaptureVoyageCheckpoint();
                var loaded = new CoreVoyageSession();
                Assert.That(loaded.TryRestoreVoyageCheckpoint(active, out reason), Is.True, id + " active: " + reason);
                Assert.That(loaded.ActiveEncounter.Id, Is.EqualTo(id));
                Assert.That(loaded.ResolveChoice(index), Is.True, id + " / " + loaded.ChoiceUnavailableReason(index));
                CoreVoyageCheckpoint paid = loaded.CaptureVoyageCheckpoint();
                var again = new CoreVoyageSession();
                Assert.That(again.TryRestoreVoyageCheckpoint(paid, out reason), Is.True, id + " paid: " + reason);
                Assert.That(again.Map.Nodes.Single(n => n.Encounter.Id == id).Visited, Is.True, id);
                foreach (var property in typeof(VoyageCargo).GetProperties().Where(p => p.PropertyType == typeof(int)))
                    Assert.That(property.GetValue(again.Cargo), Is.EqualTo(property.GetValue(loaded.Cargo)), id + " / " + property.Name);
                Assert.That(again.SailingBoostHoursRemaining, Is.EqualTo(loaded.SailingBoostHoursRemaining));
                Assert.That(again.SurveyBoostHoursRemaining, Is.EqualTo(loaded.SurveyBoostHoursRemaining));
                session = again;
            }
        }

        private static IEnumerable<CoreEncounter> NewEvents(int route)
        {
            string routeId = CoreVoyageSession.Routes[route].Id;
            return CoreVoyageEventCatalog.RandomEventIds(routeId).Select(id => CoreVoyageEventCatalog.CreateRandomEncounter(id, 95))
                .Concat(CoreVoyageEventCatalog.CreateExpandedFixedEncounters(routeId)).Where(e => e.Id.Contains("_v2_"));
        }
        private static CoreVoyageSession Ready(int route)
        {
            var session = new CoreVoyageSession { MapGeometryVersion = 1 };
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            session.SelectRoute(route); session.SetSeed("EVENT-CHOICE-20261015-" + route); session.StartVoyage();
            return session;
        }
        private static void ClearCargo(CoreVoyageSession session)
        {
            foreach (var property in typeof(VoyageCargo).GetProperties().Where(p => p.PropertyType == typeof(int))) property.SetValue(session.Cargo, 0);
        }
        private static void PrepareChoice(CoreVoyageSession session, CoreEncounterChoice choice)
        {
            ClearCargo(session);
            foreach (var property in typeof(VoyageCargo).GetProperties().Where(p => p.PropertyType == typeof(int)))
            {
                PropertyInfo effect = typeof(VoyageEffect).GetProperty(property.Name);
                if (effect != null) property.SetValue(session.Cargo, Math.Max(0, -(int)effect.GetValue(choice.Effect)));
            }
            session.Cargo.Water += 80; session.Cargo.Food += 80;
            session.Cargo.Curse = Math.Max(choice.MinimumCurse, -choice.Effect.Curse);
            double wantedHour = choice.RequiredTime == CoreVoyageTimeBand.Night ? 20d :
                choice.RequiredTime == CoreVoyageTimeBand.Dusk ? 14d : choice.RequiredTime == CoreVoyageTimeBand.Dawn ? 2d : 8d;
            double offset = (wantedHour - session.GameDateTime.TimeOfDay.TotalHours + 24d) % 24d;
            typeof(CoreVoyageSession).GetMethod("AdvanceGameClock", Hidden).Invoke(session, new object[] { offset });
            session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull / 2);
            if (!string.IsNullOrEmpty(choice.RequiredDiscovery))
                ((HashSet<string>)typeof(CoreVoyageSession).GetField("explorationDiscoveries", Hidden).GetValue(session)).Add(choice.RequiredDiscovery);
        }
        private static void Enter(CoreVoyageSession session, CoreEncounter encounter)
        {
            typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(session, null);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
        }
        private static CoreVoyageMap Generate(int version, int route, int sample, out CoreVoyageSession session)
        {
            object[] args = { version, route, sample, null };
            var map = (CoreVoyageMap)typeof(VoyageEventDistributionTests).GetMethod("Generate", StaticHidden).Invoke(null, args);
            session = (CoreVoyageSession)args[3]; return map;
        }
        private static string Signature(CoreVoyageMap map) => (string)typeof(VoyageEventDistributionTests)
            .GetMethod("Signature", StaticHidden).Invoke(null, new object[] { map });
    }
}
