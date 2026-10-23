using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageEventChoiceEffectTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly PropertyInfo[] Resources = typeof(VoyageCargo).GetProperties()
            .Where(p => p.PropertyType == typeof(int)).ToArray();

        [TestCase("event_moon_v2_borrowed_shadow", 0, "Lantern", 1)]
        [TestCase("event_moon_v2_false_ancestor", 1, "Medicine", 1)]
        [TestCase("event_moon_v2_moon_taxcollector", 0, "Lantern", 1)]
        [TestCase("event_moon_v2_ossuary_buoy", 1, "Beer", 1)]
        [TestCase("event_moon_v2_exorcist_kiln", 0, "Wood", 4)]
        [TestCase("event_moon_v2_votive_loom", 0, "Relic", 1)]
        [TestCase("event_moon_v2_unlit_court", 2, "Lantern", 1)]
        public void MoonCleansingRemovesOneHeldCurseAndPaysItsFullMaterialCost(string id, int index, string material, int cost)
        {
            CoreEncounter encounter = MoonEncounter(id);
            var session = Ready(encounter, index);
            session.Cargo.Curse = 1;
            PropertyInfo resource = typeof(VoyageCargo).GetProperty(material);
            resource.SetValue(session.Cargo, cost + 2);
            double hours = session.TotalHours;
            int water = session.Cargo.Water, food = session.Cargo.Food;
            Assert.That(session.IsChoiceAvailable(index), Is.True, id + " / " + session.ChoiceUnavailableReason(index));
            Assert.That(session.ResolveChoice(index), Is.True, id);
            Assert.That(session.Cargo.Curse, Is.Zero, id);
            Assert.That(resource.GetValue(session.Cargo), Is.EqualTo(2), id + " / full material payment");
            Assert.That(session.TotalHours, Is.EqualTo(hours + encounter.Choices[index].Hours), id);
            Assert.That(session.Cargo.Water, Is.LessThan(water), id + " / water rations");
            Assert.That(session.Cargo.Food, Is.LessThan(food), id + " / food rations");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing), id);
            if (id == "event_moon_v2_votive_loom") Assert.That(session.Cargo.Cloth, Is.EqualTo(6));
            if (id == "event_moon_v2_ossuary_buoy") Assert.That(session.Cargo.Fame, Is.EqualTo(28));
            if (id == "event_moon_v2_unlit_court") Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(12d));
            string paid = Snapshot(session);
            Assert.That(session.ResolveChoice(index), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(paid));
        }

        [TestCase("event_moon_v2_borrowed_shadow", 0, "Lantern", 1)]
        [TestCase("event_moon_v2_false_ancestor", 1, "Medicine", 1)]
        [TestCase("event_moon_v2_moon_taxcollector", 0, "Lantern", 1)]
        [TestCase("event_moon_v2_ossuary_buoy", 1, "Beer", 1)]
        [TestCase("event_moon_v2_exorcist_kiln", 0, "Wood", 4)]
        [TestCase("event_moon_v2_votive_loom", 0, "Relic", 1)]
        [TestCase("event_moon_v2_unlit_court", 2, "Lantern", 1)]
        public void MoonCleansingRejectsZeroCurseAndMissingMaterialWithoutChangingState(string id, int index, string material, int cost)
        {
            CoreEncounter encounter = MoonEncounter(id);
            foreach (bool missingMaterial in new[] { false, true })
            {
                var session = Ready(encounter, index);
                session.Cargo.Curse = missingMaterial ? 1 : 0;
                typeof(VoyageCargo).GetProperty(material).SetValue(session.Cargo, missingMaterial ? cost - 1 : cost);
                string before = Snapshot(session);
                Assert.That(session.IsChoiceAvailable(index), Is.False, id + " missingMaterial=" + missingMaterial);
                Assert.That(session.ResolveChoice(index), Is.False, id);
                Assert.That(Snapshot(session), Is.EqualTo(before), id + " / rejected action mutated state");
            }
        }

        [Test]
        public void RepairDockPaysTwoWoodAndTwoRationsForFourHoursAndTenHull()
        {
            CoreEncounter encounter = Fixed(0, "event_reef_hidden_repair_dock");
            var session = Ready(encounter, 0, 0);
            session.Cargo.Wood = 5;
            session.Cargo.Water = session.Cargo.Food = 20;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 20);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Hull, Is.EqualTo(30));
            Assert.That(session.Cargo.Wood, Is.EqualTo(3));
            Assert.That(session.Cargo.Water, Is.EqualTo(18));
            Assert.That(session.Cargo.Food, Is.EqualTo(18));
            Assert.That(session.TotalHours, Is.EqualTo(4d));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        [Test]
        public void SpiceAuctionPaysEightyGoldForOneGemAndOneHourOfRations()
        {
            CoreEncounter encounter = Fixed(1, "event_trade_spice_auction");
            var session = Ready(encounter, 0, 1);
            session.Cargo.Gold = 100;
            session.Cargo.Water = session.Cargo.Food = 20;
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Cargo.Gold, Is.EqualTo(20));
            Assert.That(session.Cargo.Gem, Is.EqualTo(1));
            Assert.That(session.Cargo.Water, Is.EqualTo(19));
            Assert.That(session.Cargo.Food, Is.EqualTo(19));
            Assert.That(session.TotalHours, Is.EqualTo(1d));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        [Test]
        public void MerchantRescuePaysCashFameAndDiscoveryOnlyAfterVictory()
        {
            CoreEncounter encounter = CoreVoyageEventCatalog.CreateRandomEncounter("event_trade_merchant_distress", 70);
            var session = Ready(encounter, 1, 1);
            CoreMapNode target = session.Map.Nodes.Single(node => node.Encounter.Id == "gold_wreck");
            target.Discovered = target.IntelRevealed = false;
            int gold = session.Cargo.Gold, fame = session.Cargo.Fame;
            Assert.That(session.ResolveChoice(1), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Fame, Is.EqualTo(fame));
            Assert.That(session.HasDiscovery("trade_rescue_seal"), Is.False);
            Assert.That(target.IsChartVisible, Is.False);
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Cargo.Gold, Is.GreaterThan(gold));
            Assert.That(session.Cargo.Fame, Is.GreaterThan(fame));
            Assert.That(session.HasDiscovery("trade_rescue_seal"), Is.True);
            Assert.That(target.IsChartVisible, Is.True);
        }

        [TestCase("gold")] [TestCase("rations")] [TestCase("capacity")]
        [TestCase("negative-index")] [TestCase("past-last-index")]
        public void RejectedAuctionChoiceDoesNotMutateVoyage(string rejection)
        {
            CoreEncounter encounter = Fixed(1, "event_trade_spice_auction");
            var session = Ready(encounter, 0, 1);
            session.Cargo.Gold = 100;
            int index = 0;
            if (rejection == "gold") session.Cargo.Gold = 79;
            if (rejection == "rations") session.Cargo.Water = 0;
            if (rejection == "capacity") session.Cargo.Iron = (int)session.CargoCapacityValue;
            if (rejection == "negative-index") index = -1;
            if (rejection == "past-last-index") index = encounter.Choices.Count;
            string before = Snapshot(session);
            Assert.That(session.IsChoiceAvailable(index), Is.False, rejection);
            Assert.That(session.ResolveChoice(index), Is.False, rejection);
            Assert.That(Snapshot(session), Is.EqualTo(before), rejection);
        }

        private static CoreEncounter Fixed(int route, string id) =>
            CoreVoyageEventCatalog.CreateExpandedFixedEncounters(CoreVoyageSession.Routes[route].Id).Single(encounter => encounter.Id == id);

        private static CoreEncounter MoonEncounter(string id)
        {
            string route = CoreVoyageSession.Routes[2].Id;
            CoreEncounter fixedEncounter = CoreVoyageEventCatalog.CreateExpandedFixedEncounters(route).FirstOrDefault(e => e.Id == id);
            return fixedEncounter ?? CoreVoyageEventCatalog.CreateRandomEncounter(id, 95);
        }

        private static CoreVoyageSession Ready(CoreEncounter encounter, int index, int route = 2)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            session.SelectRoute(route);
            session.ConfigureCrewCapabilities(false, false, encounter.Choices[index].RequiredCrew == "doctor", false, false, false);
            session.ConfigureDepartureCrew(9, 1);
            session.SetSeed("MOON-CLEANSE-CHOICE-AUDIT");
            session.StartVoyage();
            foreach (PropertyInfo property in Resources) property.SetValue(session.Cargo, 0);
            session.Cargo.Water = session.Cargo.Food = 40;
            session.Cargo.Fame = 20;
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
            typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(session, null);
            return session;
        }

        private static string Snapshot(CoreVoyageSession session) => string.Join("|", new[] {
            string.Join(",", Resources.Select(p => p.Name + "=" + p.GetValue(session.Cargo))),
            session.TotalHours.ToString("R"), session.GameDateTime.Ticks.ToString(), session.Hull.ToString(), session.Morale.ToString(),
            session.VoyageTreasureFragments.ToString(), session.TreasureClues.ToString(), session.SailingBoostHoursRemaining.ToString("R"),
            session.SurveyBoostHoursRemaining.ToString("R"), session.Phase.ToString(), session.ActiveEncounter?.Id ?? "none",
            session.ActiveEncounterClaimCount.ToString(), session.HasDiscovery("moon_star_chart").ToString(),
            string.Join(",", session.Map.Nodes.Select(n => n.Id + ":" + n.Visited + ":" + n.IntelRevealed + ":" + n.Discovered)) });
    }
}
