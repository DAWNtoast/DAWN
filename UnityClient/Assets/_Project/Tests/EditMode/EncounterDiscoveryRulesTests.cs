using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;

namespace ShatteredSail.Core.Tests
{
    public sealed class EncounterDiscoveryRulesTests
    {
        [Test]
        public void EveryAuthoredRouteAndRandomEncounter_HasAnExplicitDiscoveryRule()
        {
            var authored = new Dictionary<string, CoreEncounter>();
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route++)
            {
                CoreVoyageSession voyage = CreateVoyage(route);
                foreach (CoreMapNode node in voyage.Map.Nodes) authored[node.Encounter.Id] = node.Encounter;

                MethodInfo createAmbush = typeof(CoreVoyageSession).GetMethod("CreateRandomAmbush", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (bool detected in new[] { false, true })
                {
                    var encounter = (CoreEncounter)createAmbush.Invoke(voyage, new object[] { detected });
                    authored[encounter.Id] = encounter;
                    Assert.That(encounter.DiscoveryKind, Is.EqualTo(EncounterDiscoveryKind.TransientEncounter));
                }
            }

            foreach (CoreEncounter encounter in authored.Values)
            {
                Assert.That(CoreEncounterDiscoveryRules.TryClassify(encounter.Id, out EncounterDiscoveryKind kind), Is.True,
                    encounter.Id + " needs an explicit disclosure decision when it is added to the game.");
                Assert.That(encounter.DiscoveryKind, Is.EqualTo(kind));
                if (encounter.HasChartLocation)
                    Assert.That(encounter.CanRevealByIntel, Is.EqualTo(encounter.Hidden), encounter.Id);
            }
            Assert.That(authored.Values.Where(x => x.DiscoveryKind == EncounterDiscoveryKind.OnboardIncident).Select(x => x.Id),
                Is.EquivalentTo(new[] { "event_reef_saltwater_leak" }));
        }

        [Test]
        public void BothTutorialLegs_HaveExplicitRules_AndStillReachTheirScriptedEncounters()
        {
            var tutorial = new TutorialVoyageSession();
            tutorial.BeginFirstLeg();
            AssertTutorialRules(tutorial.CurrentLegEncounters);
            Assert.That(tutorial.SailToNext().Id, Is.EqualTo("tutorial_first_event"));
            Assert.That(tutorial.ResolveChoice(0), Is.True);
            tutorial.SailToNext();
            Assert.That(tutorial.SkipCurrentEncounter(), Is.True);
            Assert.That(tutorial.SailToNext().Id, Is.EqualTo("tutorial_crew_requirement_event"));
            Assert.That(CoreEncounterDiscoveryRules.Classify(tutorial.CurrentEncounter.Id, tutorial.CurrentEncounter.Hidden),
                Is.EqualTo(EncounterDiscoveryKind.OnboardIncident));
            Assert.That(tutorial.ResolveChoice(1), Is.True);
            tutorial.SailToNext();
            Assert.That(CoreEncounterDiscoveryRules.Classify(tutorial.CurrentEncounter.Id, tutorial.CurrentEncounter.Hidden),
                Is.EqualTo(EncounterDiscoveryKind.TransientEncounter));
            Assert.That(tutorial.ResolveChoice(0), Is.True);
            tutorial.BeginAdvancedLeg();
            AssertTutorialRules(tutorial.CurrentLegEncounters);
            Assert.That(tutorial.SailToNext().Id, Is.EqualTo("tutorial_navigator_event"));
            Assert.That(tutorial.ResolveChoice(0), Is.True);
            Assert.That(tutorial.HiddenLocationRevealed, Is.True);
            Assert.That(tutorial.SailToNext().Id, Is.EqualTo("tutorial_hidden_event"));
            Assert.That(CoreEncounterDiscoveryRules.Classify(tutorial.CurrentEncounter.Id, tutorial.CurrentEncounter.Hidden),
                Is.EqualTo(EncounterDiscoveryKind.HiddenLocation));
        }

        [Test]
        public void Intelligence_SkipsNearerOnboardIncident_WithoutSpendingItsRevealAllowance()
        {
            CoreVoyageSession voyage = CreateVoyage();
            CoreEncounter leak = Encounter(voyage, "event_reef_saltwater_leak");
            CoreEncounter whale = Encounter(voyage, "common_whale_song");
            CoreEncounter shortcut = Encounter(voyage, "event_reef_shortcut");
            CoreEncounter beacon = Encounter(voyage, "common_strait_beacon");
            ClearSea(voyage);
            VoyagePoint origin = voyage.Position;
            CoreMapNode destination = Add(voyage, beacon, origin.X + 2, origin.Y);
            CoreMapNode incident = Add(voyage, leak, origin.X + 2, origin.Y + 2);
            CoreMapNode nearestPlace = Add(voyage, whale, origin.X + 2, origin.Y + 3);
            CoreMapNode furtherPlace = Add(voyage, shortcut, origin.X + 2, origin.Y + 4);

            SailInto(voyage, destination);
            Assert.That(nearestPlace.Discovered, Is.False, "Seeing explored sea does not disclose a concealed external event.");
            Assert.That(voyage.ResolveChoice(0), Is.True);
            Assert.That(incident.IntelRevealed || incident.Discovered || incident.IsChartVisible, Is.False);
            Assert.That(nearestPlace.IntelRevealed && nearestPlace.IsChartVisible, Is.True);
            Assert.That(furtherPlace.IntelRevealed, Is.False, "The beacon grants exactly one real location.");
        }

        [Test]
        public void OnboardLeak_StillInterruptsVoyageAndResolvesWithOriginalResourceEffects()
        {
            CoreVoyageSession voyage = CreateVoyage();
            CoreEncounter leak = Encounter(voyage, "event_reef_saltwater_leak");
            ClearSea(voyage);
            CoreMapNode incident = Add(voyage, leak, voyage.Position.X + 2, voyage.Position.Y);
            SailInto(voyage, incident);
            Assert.That(incident.Discovered || incident.IntelRevealed || incident.IsChartVisible, Is.False);
            Assert.That(voyage.SkipEncounter(), Is.False, "The mandatory failure still needs to be handled.");
            int hull = voyage.Hull, wood = voyage.Cargo.Wood;
            double hours = voyage.TotalHours;

            Assert.That(voyage.ResolveChoice(2), Is.True, "The resource-free emergency option remains usable.");
            Assert.That(voyage.Hull, Is.EqualTo(hull - 6));
            Assert.That(voyage.Cargo.Wood, Is.EqualTo(wood));
            Assert.That(voyage.TotalHours, Is.EqualTo(hours));
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(incident.Visited, Is.True);
            Assert.That(voyage.CompletedEventCount(), Is.EqualTo(1));
            Assert.That(incident.IsChartVisible, Is.False, "Resolving a shipboard problem does not create a chart destination.");
        }

        [Test]
        public void SightDiscovery_UsesEventNature_EvenIfAnOnboardEventHasTheWrongLegacyHiddenFlag()
        {
            CoreVoyageSession voyage = CreateVoyage();
            CoreEncounter beacon = Encounter(voyage, "common_strait_beacon");
            ClearSea(voyage);
            var leak = new CoreEncounter("event_reef_saltwater_leak", "舱底进水", "强制事件", "船内事故", true, false, 2, 0);
            CoreMapNode incident = Add(voyage, leak, voyage.Position.X + 2, voyage.Position.Y + 2);
            CoreMapNode destination = Add(voyage, beacon, voyage.Position.X + 2, voyage.Position.Y);
            SailInto(voyage, destination);
            Assert.That(voyage.Map.ExploredCells[voyage.Map.CellIndex(incident.Position)], Is.True);
            Assert.That(incident.Discovered || incident.IsChartVisible, Is.False);
            Assert.That(destination.IsChartVisible, Is.True);
        }

        [TestCase("event_reef_saltwater_leak")]
        [TestCase("tutorial_crew_requirement_event")]
        [TestCase("random_ambush")]
        [TestCase("random_ambush_warning")]
        [TestCase("tutorial_ambush_event")]
        public void NonLocationEvents_NeverAppearOnChartOrLookout_EvenWithStaleDiscoveryFlags(string id)
        {
            var map = new CoreVoyageMap { Route = CoreVoyageSession.Routes[0] };
            var node = new CoreMapNode { Id = 1, Position = new VoyagePoint(53, 32), Discovered = true, IntelRevealed = true,
                Encounter = new CoreEncounter(id, "不应泄漏的事件名称", "事件", "事件", true, false, 1, 0) };
            map.Nodes.Add(node);
            Assert.That(node.IsChartVisible, Is.False);
            Assert.That(VoyageLookoutProjector.Project(map, new VoyagePoint(50, 32), SailingDirection.East), Is.Empty);
            node.Visited = true;
            Assert.That(node.IsChartVisible, Is.False);
        }

        [Test]
        public void NavigatorEnablesChartDrawing_ButDepartureLogsNeverExposeTheSeed()
        {
            foreach (bool navigator in new[] { false, true })
            {
                var voyage = new CoreVoyageSession();
                voyage.ConfigureCrewCapabilities(navigator, false, false, false, false, false);
                voyage.SetSeed("PRIVATE-CHART-SEED");
                Assert.That(voyage.CanDrawSeaChart, Is.EqualTo(navigator));
                voyage.StartVoyage();
                Assert.That(voyage.CanDrawSeaChart, Is.EqualTo(navigator));
                Assert.That(voyage.LogEntries.Any(x => x.Contains("PRIVATE-CHART-SEED")), Is.False);
                Assert.That(voyage.Map.Seed, Is.EqualTo("PRIVATE-CHART-SEED"), "Disclosure rules do not alter generation input.");
            }
        }

        private static void AssertTutorialRules(IReadOnlyList<TutorialEncounter> encounters)
        {
            foreach (TutorialEncounter encounter in encounters)
                Assert.That(CoreEncounterDiscoveryRules.TryClassify(encounter.Id, out _), Is.True, encounter.Id);
        }

        private static CoreVoyageSession CreateVoyage(int route = 0)
        {
            var voyage = new CoreVoyageSession();
            voyage.SelectRoute(route);
            voyage.SetSeed("DISCOVERY-AUDIT");
            voyage.StartVoyage();
            return voyage;
        }

        private static CoreEncounter Encounter(CoreVoyageSession voyage, string id) => voyage.Map.Nodes.Single(x => x.Encounter.Id == id).Encounter;

        private static void ClearSea(CoreVoyageSession voyage)
        {
            voyage.Map.Nodes.Clear();
            Array.Clear(voyage.Map.TerrainCells, 0, voyage.Map.CellCount);
            Array.Clear(voyage.Map.ExploredCells, 0, voyage.Map.CellCount);
        }

        private static CoreMapNode Add(CoreVoyageSession voyage, CoreEncounter encounter, float x, float y)
        {
            var node = new CoreMapNode { Id = voyage.Map.Nodes.Count, Encounter = encounter, Position = new VoyagePoint(x, y) };
            voyage.Map.Nodes.Add(node);
            return node;
        }

        private static void SailInto(CoreVoyageSession voyage, CoreMapNode node)
        {
            voyage.SetCourseTo(node.Position);
            Assert.That(voyage.Sail(), Is.True);
            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(voyage.ActiveEncounter, Is.SameAs(node.Encounter));
        }
    }
}
