using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreRareResourceSinkTests
    {
        [TestCase("treasure_rival_sloop", "gem", 1)]
        [TestCase("treasure_reef_snare", "ancientmaterial", 3)]
        [TestCase("treasure_drowned_shrine", "relic", 1)]
        [TestCase("treasure_ghost_fleet", "blackpearl", 1)]
        [TestCase("treasure_coral_maze", "ancientcompass", 1)]
        [TestCase("treasure_clue_fogbell", "lantern", 1)]
        public void RareEvent_ConsumesCarriedGoodsOnceAndKeepsPortStock(string eventId, string key, int cost)
        {
            var session = Ready(key, cost + 1);
            int stored = session.PortWarehouse.GetAmount(session.CurrentPort, key);
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            CoreMapNode node = Enter(session, eventId);
            Set(session, "Hull", session.MaxHull - 20);
            int hull = session.Hull, clues = session.TreasureClues;
            float space = session.Cargo.UsedVolume;
            int water = session.Cargo.Water, food = session.Cargo.Food;
            double hours = session.TotalHours;
            CoreEncounterChoice choice = node.Encounter.Choices[3];
            Assert.That(session.IsChoiceAvailable(3), Is.True, session.ChoiceUnavailableReason(3));
            Assert.That(session.ResolveChoice(3), Is.True);
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(1));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, key), Is.EqualTo(stored));
            Assert.That(session.Hull, Is.EqualTo(hull + choice.Effect.Hull));
            Assert.That(session.TreasureClues, Is.EqualTo(clues + choice.Effect.TreasureClues));
            Assert.That(session.TotalHours, Is.EqualTo(hours + choice.Hours).Within(.0001));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(space - cost * CoreWarehouseItemCatalog.Find(key).UnitVolume -
                (water - session.Cargo.Water) * .07 - (food - session.Cargo.Food) * .09).Within(.002));
            Assert.That(node.Visited, Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing), "The rare option avoids combat.");
            Assert.That(session.ResolveChoice(3), Is.False, "A completed event cannot charge the same payment twice.");
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(1));
        }

        [TestCase("treasure_rival_sloop", "gem", 1)]
        [TestCase("treasure_reef_snare", "ancientmaterial", 3)]
        [TestCase("treasure_drowned_shrine", "relic", 1)]
        [TestCase("treasure_ghost_fleet", "blackpearl", 1)]
        [TestCase("treasure_coral_maze", "ancientcompass", 1)]
        [TestCase("treasure_clue_fogbell", "lantern", 1)]
        public void RareEvent_PortStockCannotPayAndRejectedChoicesLeaveEverythingUntouched(string eventId, string key, int cost)
        {
            var session = Ready(key, cost - 1);
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            Enter(session, eventId);
            int water = session.Cargo.Water, food = session.Cargo.Food, hull = session.Hull, gold = session.Cargo.Gold;
            double hours = session.TotalHours;
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, key), Is.EqualTo(20));
            Assert.That(session.IsChoiceAvailable(3), Is.False);
            Assert.That(session.ChoiceUnavailableReason(3), Does.Contain(CoreWarehouseItemCatalog.Find(key).Name));
            Assert.That(session.ResolveChoice(3), Is.False);
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(cost - 1));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.TotalHours, Is.EqualTo(hours));
            Assert.That(session.TreasureClues, Is.Zero);
        }

        [TestCase("treasure_rival_sloop", "gem", 1)]
        [TestCase("treasure_reef_snare", "ancientmaterial", 3)]
        [TestCase("treasure_drowned_shrine", "relic", 1)]
        [TestCase("treasure_ghost_fleet", "blackpearl", 1)]
        [TestCase("treasure_coral_maze", "ancientcompass", 1)]
        [TestCase("treasure_clue_fogbell", "lantern", 1)]
        public void RareEvent_ConsumedDepartureCargoStaysConsumedAfterReturnAndReload(string eventId, string key, int cost)
        {
            var session = Ready(key, cost);
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            Enter(session, eventId);
            Assert.That(session.ResolveChoice(3), Is.True);
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            session.ReturnToPortAfterSettlement();
            Assert.That(session.GetWarehouseCargoAmount(key), Is.Zero, "Settlement must not restore spent departure stock.");
            var restored = new CoreVoyageSession();
            restored.RestoreProfile(session.ProfileGold, session.CurrentPort);
            restored.RestoreTreasureProgress(session.CaptureTreasureProgress());
            Assert.That(restored.PortWarehouse.Restore(session.PortWarehouse.Serialize()), Is.True);
            Assert.That(restored.GetWarehouseCargoAmount(key), Is.Zero);
            Assert.That(restored.PortWarehouse.GetAmount(restored.CurrentPort, key), Is.EqualTo(20));
        }

        [Test]
        public void RareEvent_TimeRationsMustBeAffordableBeforeAnyRarePayment()
        {
            var session = Ready("gem", 1);
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            Enter(session, "treasure_rival_sloop");
            session.Cargo.Water = 0;
            Assert.That(session.ResolveChoice(3), Is.False);
            Assert.That(session.Cargo.Gem, Is.EqualTo(1));
            Assert.That(session.TreasureClues, Is.Zero);
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void RareEvent_AllSixTypesHaveOptionalUsesWithoutRemovingExistingRoutes(string rankId)
        {
            var events = CoreTreasureHuntCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank(rankId));
            var costs = events.SelectMany(e => e.Choices).Select(c => c.Effect).ToArray();
            Assert.That(costs.Any(e => e.Gem < 0) && costs.Any(e => e.AncientMaterial < 0) &&
                costs.Any(e => e.Relic < 0) && costs.Any(e => e.BlackPearl < 0) &&
                costs.Any(e => e.AncientCompass < 0) && costs.Any(e => e.Lantern < 0), Is.True);
            foreach (CoreEncounter encounter in events.Where(e => e.Id != CoreTreasureHuntCatalog.CoreEncounterId))
            {
                Assert.That(encounter.Choices.Count, Is.InRange(3, 4));
                foreach (CoreEncounterChoice oldChoice in encounter.Choices.Take(3))
                    Assert.That(oldChoice.Effect.Gem >= 0 && oldChoice.Effect.AncientMaterial >= 0 && oldChoice.Effect.Relic >= 0 &&
                        oldChoice.Effect.BlackPearl >= 0 && oldChoice.Effect.AncientCompass >= 0 && oldChoice.Effect.Lantern >= 0,
                        Is.True, "The old non-rare routes remain available: " + encounter.Id);
            }
        }

        private static CoreVoyageSession Ready(string key, int quantity)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            var progress = new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = "rare-sink-fixture",
                maps = new[] { new CoreTreasureMapSave { id = "rare-sink-fixture", seed = "RARE-SINK", rankId = "weathered" } } };
            if (key == "gem") progress.gems = quantity;
            if (key == "ancientmaterial") progress.ancientMaterials = quantity;
            if (key == "relic") progress.relics = quantity;
            if (key == "blackpearl") progress.blackPearls = quantity;
            if (key == "ancientcompass") progress.ancientCompasses = quantity;
            if (key == "lantern") progress.lanterns = quantity;
            session.RestoreTreasureProgress(progress);
            session.Cargo.Water = session.Cargo.Food = 150;
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Beer = 0;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Medicine = 0;
            Assert.That(session.PortWarehouse.Restore("v2;" + session.CurrentPort + "," + key + ",20"), Is.True);
            return session;
        }

        // Rule fixtures enter authored events directly; separate PlayMode tests
        // exercise the actual fourth-choice button and scroll container.
        private static CoreMapNode Enter(CoreVoyageSession session, string id)
        {
            var node = new CoreMapNode { Id = 999, Position = session.Position,
                TerrainConstraint = id == "treasure_reef_snare" ? EventTerrainConstraint.Reef : EventTerrainConstraint.Any,
                Encounter = CoreTreasureHuntCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank("weathered")).Single(e => e.Id == id) };
            session.Map.Nodes.Add(node);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            Set(session, "ActiveEncounter", node.Encounter);
            Set(session, "Phase", CoreDemoPhase.Encounter);
            return node;
        }
        private static void Set(CoreVoyageSession session, string property, object value) =>
            typeof(CoreVoyageSession).GetProperty(property).SetValue(session, value);
    }
}

