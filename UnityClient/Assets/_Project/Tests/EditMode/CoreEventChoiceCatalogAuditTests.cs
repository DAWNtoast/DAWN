using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreEventChoiceCatalogAuditTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly PropertyInfo[] CargoFields = typeof(VoyageCargo).GetProperties()
            .Where(p => p.PropertyType == typeof(int) && p.CanWrite).ToArray();
        private sealed class Entry
        {
            public int Route;
            public string Group;
            public string Context;
            public CoreEncounter Event;
        }

        [Test]
        public void CompleteFactoryInventoryMatchesThePublishedCurrentCatalogAndExportsAllChoices()
        {
            var entries = Current();
            Assert.That(entries.Count, Is.EqualTo(210));
            Assert.That(entries.Select(e => e.Event.Id).Distinct().Count(), Is.EqualTo(195));
            var csv = new StringBuilder();
            csv.AppendLine("group,context,eventId,eventName,repeatPolicy,choiceIndex,label,detail,hours,battle,requirements,effect,navalVictoryQuote");
            foreach (Entry entry in entries)
            {
                Assert.That(entry.Event.Choices, Is.Not.Empty, entry.Event.Id);
                foreach (CoreEncounterChoice choice in entry.Event.Choices)
                {
                    string key = entry.Context + "/" + entry.Event.Id + "/" + choice.Label;
                    Assert.That(choice.Label, Is.Not.Null.And.Not.Empty, key);
                    Assert.That(choice.Detail, Is.Not.Null.And.Not.Empty, key);
                    Assert.That(double.IsNaN(choice.Hours) || double.IsInfinity(choice.Hours), Is.False, key);
                    Assert.That(choice.Hours, Is.GreaterThanOrEqualTo(0), key);
                    Assert.That(choice.Effect.SailingBoostHours, Is.InRange(0d, 24d), key);
                    Assert.That(choice.Effect.SurveyBoostHours, Is.InRange(0d, 24d), key);
                    Assert.That(choice.MinimumCurse, Is.LessThanOrEqualTo(choice.MaximumCurse), key);
                    if (!string.IsNullOrEmpty(choice.RequiredDiscovery))
                        Assert.That(CoreVoyageSession.DiscoveryLabel(choice.RequiredDiscovery), Is.Not.EqualTo("未知线索"), key);
                    string requirements = string.Join(";", typeof(CoreEncounterChoice).GetProperties()
                        .Where(p => p.Name.StartsWith("Required", StringComparison.Ordinal) || p.Name.StartsWith("Minimum", StringComparison.Ordinal) || p.Name.StartsWith("Maximum", StringComparison.Ordinal) || p.Name == "Renewable")
                        .Select(p => p.Name + "=" + p.GetValue(choice)));
                    string effects = string.Join(";", typeof(VoyageEffect).GetProperties().Where(p => Meaningful(p.GetValue(choice.Effect)))
                        .Select(p => p.Name + "=" + Convert.ToString(p.GetValue(choice.Effect), CultureInfo.InvariantCulture)));
                    string quote = choice.StartsBattle ? CoreBattleRewardDescriptions.Victory(entry.Event.BossKind != CoreBossKind.None ||
                        entry.Event.Id == CoreGuidedTutorialContent.BossEventId, choice.Effect) : string.Empty;
                    csv.AppendLine(string.Join(",", new[] { entry.Group, entry.Context, entry.Event.Id, entry.Event.Name,
                        entry.Event.RepeatPolicy.ToString(), entry.Event.Choices.ToList().IndexOf(choice).ToString(), choice.Label,
                        choice.Detail, choice.Hours.ToString(CultureInfo.InvariantCulture), choice.StartsBattle.ToString(), requirements, effects, quote }.Select(Csv)));
                }
            }
            string output = TestContext.CurrentContext.WorkDirectory;
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "EventChoiceCatalogAudit");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "choices-current.csv"), csv.ToString(), new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(output, "choices-coverage.txt"), entries.GroupBy(e => e.Group).Select(g =>
                g.Key + ": events=" + g.Count() + ", choices=" + g.Sum(e => e.Event.Choices.Count)).Concat(new[] {
                "TOTAL: events=" + entries.Count + ", ids=" + entries.Select(e => e.Event.Id).Distinct().Count() +
                ", choices=" + entries.Sum(e => e.Event.Choices.Count) }));
            Console.WriteLine("Full current inventory: " + entries.Count + " records / 195 IDs / " + entries.Sum(e => e.Event.Choices.Count) + " choices.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EveryRegionalChoicePaysItsRealCargoAndTimeOnceAndRejectsMissingMaterials(int route)
        {
            int executed = 0, denied = 0;
            foreach (Entry entry in Current().Where(e => e.Route == route && (e.Group == "base" || e.Group == "fixed" || e.Group == "random" || e.Group == "common")))
            for (int index = 0; index < entry.Event.Choices.Count; index++)
            {
                CoreEncounterChoice choice = entry.Event.Choices[index];
                CoreVoyageSession session = Ready(entry, choice);
                string key = entry.Event.Id + "/" + choice.Label;
                Assert.That(session.IsChoiceAvailable(index), Is.True, key + ": " + session.ChoiceUnavailableReason(index));
                var before = CargoFields.ToDictionary(p => p.Name, p => (int)p.GetValue(session.Cargo));
                int rations = (int)Math.Ceiling(session.EstimateDailyRationConsumption(session.EffectiveCrewCount, session.HasCook) * choice.Hours / 24d - 1e-9d);
                int hull = session.Hull, morale = session.Morale, fragments = session.VoyageTreasureFragments;
                Assert.That(session.ResolveChoice(index), Is.True, key);
                Assert.That(session.TotalHours, Is.EqualTo(choice.Hours).Within(1e-9), key);
                foreach (PropertyInfo property in CargoFields)
                {
                    int delta = (int)typeof(VoyageEffect).GetProperty(property.Name).GetValue(choice.Effect);
                    if (choice.StartsBattle) delta = Math.Min(0, delta);
                    if (property.Name == "Medicine" && session.HasDoctor && delta < 0) delta = -(int)Math.Ceiling(-delta * .75d);
                    if ((property.Name == "Wood" || property.Name == "Iron") && session.HasDiver && delta > 0) delta += Math.Max(1, delta / 4);
                    int expected = before[property.Name] + delta - (property.Name == "Water" || property.Name == "Food" ? rations : 0);
                    if (property.Name == "Curse") expected = Math.Max(0, Math.Min(10, expected));
                    Assert.That(property.GetValue(session.Cargo), Is.EqualTo(expected), key + ": " + property.Name);
                }
                int hullDelta = choice.StartsBattle ? Math.Min(0, choice.Effect.Hull) : choice.Effect.Hull;
                Assert.That(session.Hull, Is.EqualTo(Math.Min(session.MaxHull, hull + hullDelta + (hullDelta > 0 && session.HasDoctor ? 2 : 0))), key);
                Assert.That(session.Morale, Is.EqualTo(Math.Max(0, Math.Min(CoreVoyageSession.MaxMorale,
                    morale + (choice.StartsBattle ? Math.Min(0, choice.Effect.Morale) : choice.Effect.Morale)))), key);
                Assert.That(session.VoyageTreasureFragments, Is.EqualTo(fragments + (choice.StartsBattle ? 0 : choice.Effect.MapFragments)), key);
                Assert.That(session.Phase, Is.EqualTo(choice.StartsBattle ? CoreDemoPhase.Battle : CoreDemoPhase.Sailing), key);
                if (!choice.StartsBattle)
                {
                    Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(choice.Effect.SailingBoostHours), key);
                    Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(choice.Effect.SurveyBoostHours), key);
                    if (!string.IsNullOrEmpty(choice.Effect.GrantDiscovery)) Assert.That(session.HasDiscovery(choice.Effect.GrantDiscovery), Is.True, key);
                }
                string paid = State(session);
                Assert.That(session.ResolveChoice(index), Is.False, key + ": double click");
                Assert.That(State(session), Is.EqualTo(paid), key + ": double click mutated state");
                executed++;

                foreach (PropertyInfo property in CargoFields.Where(p => p.Name != "Curse" &&
                    (int)typeof(VoyageEffect).GetProperty(p.Name).GetValue(choice.Effect) < 0))
                {
                    CoreVoyageSession poor = Ready(entry, choice);
                    int cost = -(int)typeof(VoyageEffect).GetProperty(property.Name).GetValue(choice.Effect);
                    if (property.Name == "Medicine" && poor.HasDoctor) cost = (int)Math.Ceiling(cost * .75d);
                    property.SetValue(poor.Cargo, Math.Max(0, cost - 1));
                    string unpaid = State(poor);
                    Assert.That(poor.IsChoiceAvailable(index), Is.False, key + ": insufficient " + property.Name);
                    Assert.That(poor.ResolveChoice(index), Is.False, key + ": insufficient " + property.Name);
                    Assert.That(State(poor), Is.EqualTo(unpaid), key + ": rejected payment mutated state");
                    denied++;
                }
            }
            Console.WriteLine("Route " + route + ": " + executed + " choices executed, " + denied + " insufficient-material cases rejected atomically.");
        }

        [TestCase("event_moon_open_water_ambush", 1)]
        [TestCase("event_moon_v2_moon_taxcollector", 2)]
        [TestCase("event_trade_merchant_distress", 1)]
        public void RandomBattleOptionsQuoteCashConversionAndPayOnlyAfterVictory(string id, int index)
        {
            Entry entry = Current().Single(e => e.Event.Id == id);
            CoreEncounterChoice choice = entry.Event.Choices[index];
            CoreVoyageSession session = Ready(entry, choice);
            Assert.That(choice.Detail, Does.Contain("击沉：金币 +100").And.Contain("夺船×2：金币 +200"));
            Assert.That(choice.Detail, Does.Not.Contain("宝石 +1").And.Not.Contain("古代材料 +1").And.Not.Contain("另计基础"));
            if (id == "event_trade_merchant_distress")
                Assert.That(choice.Detail, Does.Contain("商队互助凭证").And.Contain("香料沉船"));
            int beforeGold = session.Cargo.Gold, beforeGem = session.Cargo.Gem, beforeMaterial = session.Cargo.AncientMaterial;
            Assert.That(session.ResolveChoice(index), Is.True);
            Assert.That(session.Cargo.Gold, Is.EqualTo(beforeGold));
            Assert.That(session.HasDiscovery("trade_rescue_seal"), Is.False);
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Cargo.Gold, Is.EqualTo(beforeGold + 100));
            Assert.That(session.Cargo.Gem, Is.EqualTo(beforeGem));
            Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(beforeMaterial));
            if (id == "event_trade_merchant_distress") Assert.That(session.HasDiscovery("trade_rescue_seal"), Is.True);
        }

        private static CoreVoyageSession Ready(Entry entry, CoreEncounterChoice choice)
        {
            var session = new CoreVoyageSession { MapGeometryVersion = 1 };
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            if (!string.IsNullOrEmpty(choice.RequiredShip))
            {
                string ship = choice.RequiredShip.Split('|')[0];
                Assert.That(session.SelectShip(Enumerable.Range(0, CoreVoyageSession.Ships.Count).Single(i => CoreVoyageSession.Ships[i].Id == ship)), Is.True);
            }
            session.SelectRoute(entry.Route); session.SetSeed("ALL-CHOICE-AUDIT-" + entry.Route); session.StartVoyage();
            session.ConfigureCrewCapabilities(choice.RequiredCrew == "navigator", choice.RequiredCrew == "carpenter", choice.RequiredCrew == "doctor",
                choice.RequiredCrew == "gunner", choice.RequiredCrew == "cook", choice.RequiredCrew == "diver");
            foreach (PropertyInfo property in CargoFields)
                property.SetValue(session.Cargo, Math.Max(0, -(int)typeof(VoyageEffect).GetProperty(property.Name).GetValue(choice.Effect)));
            session.Cargo.Water += 40; session.Cargo.Food += 40; session.Cargo.Fame += 20;
            session.Cargo.Curse = Math.Max(choice.MinimumCurse, -choice.Effect.Curse);
            Set(session, "Hull", session.MaxHull / 2); Set(session, "Morale", 10);
            double hour = choice.RequiredTime == CoreVoyageTimeBand.Night ? 20 : choice.RequiredTime == CoreVoyageTimeBand.Dusk ? 14 :
                choice.RequiredTime == CoreVoyageTimeBand.Dawn ? 2 : 8;
            typeof(CoreVoyageSession).GetMethod("AdvanceGameClock", Hidden).Invoke(session,
                new object[] { (hour - session.GameDateTime.TimeOfDay.TotalHours + 24) % 24 });
            if (!string.IsNullOrEmpty(choice.RequiredDiscovery))
                ((HashSet<string>)typeof(CoreVoyageSession).GetField("explorationDiscoveries", Hidden).GetValue(session)).Add(choice.RequiredDiscovery);
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            for (int i = 0; i < session.Map.CellCount; i++) session.Map.CurrentStrengths[i] = choice.RequiredWeather == CoreVoyageWeather.Rain ? 2 : .5f;
            session.Map.GlobalWindStrength = choice.RequiredWeather == CoreVoyageWeather.Wind ? 10 : 5;
            EventTerrainConstraint terrain = choice.RequiredWeather == CoreVoyageWeather.Storm ? EventTerrainConstraint.Storm :
                choice.RequiredWeather == CoreVoyageWeather.Fog ? EventTerrainConstraint.Fog : choice.RequiredTerrain;
            typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(session, terrain == EventTerrainConstraint.Any ? null :
                new CoreMapNode { Encounter = entry.Event, TerrainConstraint = terrain, Position = session.Position });
            Set(session, "ActiveEncounter", entry.Event); Set(session, "Phase", CoreDemoPhase.Encounter);
            return session;
        }

        private static List<Entry> Current()
        {
            var entries = new List<Entry>();
            var session = new CoreVoyageSession(); session.StartVoyage();
            void Add(string group, string context, CoreEncounter encounter, int route = 0) =>
                entries.Add(new Entry { Group = group, Context = context, Event = encounter, Route = route });
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route++)
            {
                CoreRouteDefinition definition = CoreVoyageSession.Routes[route];
                var pool = (IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession).GetMethod("CreateEncounterPool", Hidden).Invoke(session, new object[] { definition });
                foreach (CoreEncounter e in pool) Add("base", definition.Id, CoreVoyageEventCatalog.EnrichExploration(e, definition.Id), route);
                foreach (CoreEncounter e in CoreVoyageEventCatalog.CreateExpandedFixedEncounters(definition.Id))
                    Add("fixed", definition.Id, CoreVoyageEventCatalog.EnrichExploration(e, definition.Id), route);
                foreach (string id in CoreVoyageEventCatalog.RandomEventIds(definition.Id))
                    Add("random", definition.Id, CoreVoyageEventCatalog.CreateRandomEncounter(id, definition.RecommendedPower), route);
            }
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.All) Add("routeBoss", boss.RouteId, boss.CreateEncounter());
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All) Add("legendary", ship.SchoolId, ship.CreateEncounter());
            Add("treasureBoss", "moon", CoreLegendaryWarships.CreateTreasureEncounter());
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
                foreach (CoreEncounter e in CoreTreasureExpeditionCatalog.CreateEncounters(rank)) Add("treasure", rank.Id, e);
            foreach (string method in new[] { "FirstEvent", "OptionalEvent", "CrewRequirementEvent", "NavigatorEvent", "AmbushEvent", "BossEvent" })
                Add("tutorial", method, (CoreEncounter)typeof(CoreGuidedTutorialContent).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
            foreach (bool lucky in new[] { false, true })
                Add("tutorial", "RandomEvent:" + lucky, (CoreEncounter)typeof(CoreGuidedTutorialContent).GetMethod("RandomEvent", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { lucky }));
            foreach (string id in new[] { "random_tailwind", "random_floating_supplies" })
                Add("common", "all routes", (CoreEncounter)typeof(CoreVoyageSession).GetMethod("CreateSeaChartRandomEncounter", Hidden).Invoke(session, new object[] { id }));
            foreach (bool detected in new[] { false, true })
                Add("common", "treasure/unmapped voyage", (CoreEncounter)typeof(CoreVoyageSession).GetMethod("CreateRandomAmbush", Hidden).Invoke(session, new object[] { detected }));
            return entries;
        }

        private static void Set(CoreVoyageSession session, string name, object value) => typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);
        private static bool Meaningful(object value) => value != null && !string.IsNullOrEmpty(value.ToString()) && value.ToString() != "0" && value.ToString() != "False";
        private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        private static string State(CoreVoyageSession session) => string.Join("|", CargoFields.Select(p => p.Name + "=" + p.GetValue(session.Cargo))) +
            "|" + session.Hull + "|" + session.Morale + "|" + session.TotalHours + "|" + session.VoyageTreasureFragments + "|" +
            session.SailingBoostHoursRemaining + "|" + session.SurveyBoostHoursRemaining + "|" + session.Phase;
    }
}
