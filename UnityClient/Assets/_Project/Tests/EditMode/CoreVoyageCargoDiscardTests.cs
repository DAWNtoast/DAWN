using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageCargoDiscardTests
    {
        private static IEnumerable Items => CoreWarehouseItemCatalog.All.Select(item => item.Key);

        [TestCaseSource(nameof(Items))]
        public void EveryPhysicalItem_OnlySelectedCargoAndItsDeparturePrincipalChange(string key)
        {
            CoreVoyageSession session = Voyage(key, 10);
            Write(session.Cargo, key, 14); // Four units were earned during this voyage.
            Assert.That(session.PortWarehouse.Restore("v2;断锚港," + key + ",7;白贝港," + key + ",13"), Is.True);
            string warehouse = session.PortWarehouse.Serialize();
            CoreVoyageCheckpoint before = session.CaptureVoyageCheckpoint();
            float volume = session.Cargo.UsedVolume;

            Assert.That(session.TryDiscardVoyageCargo(key, 6), Is.True);
            CoreVoyageCheckpoint after = session.CaptureVoyageCheckpoint();
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(8));
            Assert.That(Read(Departure(session), key), Is.EqualTo(8), "Four gains and two departure units were discarded.");
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            Assert.That(session.Cargo.UsedVolume,
                Is.EqualTo(volume - 6 * CoreWarehouseItemCatalog.Find(key).UnitVolume).Within(.0001));
            Assert.That(after.logs.Length, Is.EqualTo(before.logs.Length + 1));
            Assert.That(after.logs.Last(), Does.Contain(CoreWarehouseItemCatalog.Find(key).Name).And.Contain("×6"));
            SetCheckpointQuantity(before.cargo, key, 8);
            SetCheckpointQuantity(before.departureCargo, key, 8);
            SetTreasureProgressQuantity(before.treasureProgress, key, 8);
            after.logs = before.logs;
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(JsonUtility.ToJson(before)),
                "No other cargo, money, map, time, event, progression or random state may change.");
        }

        [TestCase(null, 1)] [TestCase("", 1)] [TestCase("unknown", 1)]
        [TestCase("gold", 1)] [TestCase("fame", 1)] [TestCase("curse", 1)]
        [TestCase("treasurefragment", 1)] [TestCase("treasuremap", 1)]
        [TestCase("wood", 0)] [TestCase("wood", -1)] [TestCase("wood", int.MinValue)]
        [TestCase("wood", 11)] [TestCase("wood", int.MaxValue)]
        public void InvalidRequests_AreAtomicAndDoNotWriteLogs(string key, int quantity)
        {
            CoreVoyageSession session = Voyage("wood", 10);
            string before = JsonUtility.ToJson(session.CaptureVoyageCheckpoint());
            string warehouse = session.PortWarehouse.Serialize();
            Assert.That(session.TryDiscardVoyageCargo(key, quantity), Is.False);
            Assert.That(JsonUtility.ToJson(session.CaptureVoyageCheckpoint()), Is.EqualTo(before));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
        }

        [TestCase(CoreDemoPhase.Port)] [TestCase(CoreDemoPhase.Battle)] [TestCase(CoreDemoPhase.Settlement)]
        public void PortBattleAndSettlementRejectDiscard(CoreDemoPhase phase)
        {
            CoreVoyageSession session = Voyage("wood", 10);
            SetProperty(session, "Phase", phase);
            string[] logs = session.LogEntries.ToArray();
            Assert.That(session.TryDiscardVoyageCargo("wood", 1), Is.False);
            Assert.That(session.Cargo.Wood, Is.EqualTo(10));
            Assert.That(Departure(session).Wood, Is.EqualTo(10));
            CollectionAssert.AreEqual(logs, session.LogEntries);
        }

        [Test]
        public void InvalidCargoOrMissingDepartureBasisRejectWithoutPartialMutation()
        {
            CoreVoyageSession session = Voyage("wood", 10);
            session.Cargo.Iron = -1;
            int logs = session.LogEntries.Count;
            Assert.That(session.TryDiscardVoyageCargo("wood", 1), Is.False);
            session.Cargo.Iron = 0;
            Departure(session).Iron = -1;
            Assert.That(session.TryDiscardVoyageCargo("wood", 1), Is.False);
            Assert.That(session.Cargo.Wood, Is.EqualTo(10));
            Assert.That(Departure(session).Wood, Is.EqualTo(10));
            Assert.That(session.LogEntries.Count, Is.EqualTo(logs));
            var unstarted = new CoreVoyageSession();
            SetProperty(unstarted, "Phase", CoreDemoPhase.Sailing);
            Assert.That(unstarted.TryDiscardVoyageCargo("wood", 1), Is.False);
        }

        [Test]
        public void ConsecutiveDiscards_NormalizeKeysAndSpendVoyageGainsBeforePrincipal()
        {
            CoreVoyageSession session = Voyage("wood", 10);
            session.Cargo.Wood = 16;
            Assert.That(session.TryDiscardVoyageCargo(" WOOD ", 4), Is.True);
            Assert.That(session.Cargo.Wood, Is.EqualTo(12));
            Assert.That(Departure(session).Wood, Is.EqualTo(10));
            Assert.That(session.TryDiscardVoyageCargo("wood", 5), Is.True);
            Assert.That(session.Cargo.Wood, Is.EqualTo(7));
            Assert.That(Departure(session).Wood, Is.EqualTo(7));
            Assert.That(session.TryDiscardVoyageCargo("wood", 7), Is.True);
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(Departure(session).Wood, Is.Zero);
            Assert.That(session.TryDiscardVoyageCargo("wood", 1), Is.False);
        }

        [Test]
        public void PriorOrdinaryConsumptionKeepsItsExistingProtectionGap()
        {
            CoreVoyageSession session = Voyage("wood", 10);
            session.Cargo.Wood = 5; // An earlier ordinary repair consumed five.
            Assert.That(session.TryDiscardVoyageCargo("wood", 1), Is.True);
            Assert.That(session.Cargo.Wood, Is.EqualTo(4));
            Assert.That(Departure(session).Wood, Is.EqualTo(9),
                "Discard must remove only its one principal unit, not the prior repair's protection gap.");
            session.Cargo.Wood += 5;
            Finish(session, VoyageOutcome.Failure);
            Assert.That(session.Cargo.Wood, Is.EqualTo(9));
            Assert.That(session.Settlement.BankedWood, Is.Zero);
        }

        [TestCaseSource(nameof(Items))]
        public void DiscardedPrincipalCannotProtectReplacementLootOnFailure(string key)
        {
            CoreVoyageSession session = Voyage(key, 10);
            Assert.That(session.TryDiscardVoyageCargo(key, 10), Is.True);
            Write(session.Cargo, key, 10); // Ten newly earned units, not the discarded supplies.
            Finish(session, VoyageOutcome.Failure);
            int expected = CoreWarehouseItemCatalog.Find(key).IsRare && key != "ancientmaterial" ? 4 : 3;
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(expected));
            session.ReturnToPortAfterSettlement();
            session.ReturnToPortAfterSettlement();
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(expected), "Repeated settlement must not revive discarded principal.");
        }

        [TestCase(VoyageOutcome.SafeReturn)] [TestCase(VoyageOutcome.ArrivedAtPort)]
        [TestCase(VoyageOutcome.Failure)]
        public void AllDiscardedCargoStaysAbsentThroughEveryOrdinarySettlement(VoyageOutcome outcome)
        {
            CoreVoyageSession session = AllCargoVoyage();
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                Assert.That(session.TryDiscardVoyageCargo(item.Key, session.GetWarehouseCargoAmount(item.Key)), Is.True, item.Key);
            Finish(session, outcome);
            session.ReturnToPortAfterSettlement();
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                Assert.That(session.GetWarehouseCargoAmount(item.Key), Is.Zero, item.Key);
        }

        [Test]
        public void RealSafeReturnAndBattleDefeatDoNotReviveDiscardedGoods()
        {
            CoreVoyageSession safe = Voyage("wood", 10);
            Assert.That(safe.TryDiscardVoyageCargo("wood", 6), Is.True);
            Assert.That(safe.SafeReturn(), Is.True);
            safe.ReturnToPortAfterSettlement();
            Assert.That(safe.Cargo.Wood, Is.EqualTo(4));

            CoreVoyageSession lost = Voyage("wood", 10);
            Assert.That(lost.TryDiscardVoyageCargo("wood", 10), Is.True);
            Invoke(lost, "StartBattle", lost.Map.Nodes[0].Encounter, false);
            Assert.That(lost.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            SetProperty(lost, "Hull", 0);
            Assert.That(lost.EndBattleTurn(), Is.True);
            Assert.That(lost.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            lost.ReturnToPortAfterSettlement();
            Assert.That(lost.Cargo.Wood, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void CheckpointJsonRetainsAllDiscardedQuantitiesAndAdjustedBaselines(bool encounter)
        {
            CoreVoyageSession source = AllCargoVoyage();
            if (encounter) EnterNode(source, source.Map.Nodes[0]);
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                Assert.That(source.TryDiscardVoyageCargo(item.Key, 3), Is.True, item.Key);
            string json = JsonUtility.ToJson(source.CaptureVoyageCheckpoint());
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(json), out string reason), Is.True, reason);
            Assert.That(JsonUtility.ToJson(restored.CaptureVoyageCheckpoint()), Is.EqualTo(json));
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
            {
                Assert.That(restored.GetWarehouseCargoAmount(item.Key), Is.EqualTo(source.GetWarehouseCargoAmount(item.Key)), item.Key);
                Assert.That(restored.TryDiscardVoyageCargo(item.Key, 1), Is.True, item.Key);
                Assert.That(source.TryDiscardVoyageCargo(item.Key, 1), Is.True, item.Key);
            }
            Finish(source, VoyageOutcome.Failure);
            Finish(restored, VoyageOutcome.Failure);
            source.ReturnToPortAfterSettlement();
            restored.ReturnToPortAfterSettlement();
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                Assert.That(restored.GetWarehouseCargoAmount(item.Key), Is.EqualTo(source.GetWarehouseCargoAmount(item.Key)), item.Key);
            Assert.That(restored.ProfileGold, Is.EqualTo(source.ProfileGold));
        }

        [Test]
        public void MaximumIntegerCargoCanBeDiscardedWithoutOverflow()
        {
            CoreVoyageSession session = Voyage("wood", 10);
            session.Cargo.Wood = int.MaxValue;
            Assert.That(session.TryDiscardVoyageCargo("wood", int.MaxValue), Is.True);
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(Departure(session).Wood, Is.Zero);
        }

        [Test]
        public void LanternDetectionAndSpecialItemChoiceRequirementsRefreshImmediately()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 100,
                maps = new[] { new CoreTreasureMapSave { id = "discard-light", seed = "DISCARD-LIGHT", rankId = "weathered" } },
                selectedMapId = "discard-light",
            });
            session.Cargo.Lantern = 4;
            session.Cargo.AncientCompass = 1;
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            double detected = session.AmbushDetectionChance;
            bool[] explored = (bool[])session.Map.ExploredCells.Clone();
            Assert.That(session.TryDiscardVoyageCargo("lantern", 3), Is.True);
            Assert.That(session.AmbushDetectionChance, Is.EqualTo(detected - .09d).Within(.000001d));
            CollectionAssert.AreEqual(explored, session.Map.ExploredCells, "Already revealed chart cells do not become unexplored.");
            var encounters = CoreTreasureHuntCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank("weathered"));
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
            SetProperty(session, "ActiveEncounter", encounters.Single(item => item.Id == "treasure_clue_fogbell"));
            Assert.That(session.IsChoiceAvailable(3), Is.True);
            Assert.That(session.TryDiscardVoyageCargo("lantern", 1), Is.True);
            Assert.That(session.IsChoiceAvailable(3), Is.False);
            SetProperty(session, "ActiveEncounter", encounters.Single(item => item.Id == "treasure_coral_maze"));
            Assert.That(session.IsChoiceAvailable(3), Is.True);
            Assert.That(session.TryDiscardVoyageCargo("ancientcompass", 1), Is.True);
            Assert.That(session.IsChoiceAvailable(3), Is.False);
        }

        private static CoreVoyageSession Voyage(string key, int quantity)
        {
            var session = new CoreVoyageSession();
            session.SetSeed("VOYAGE-CARGO-DISCARD-REGRESSION");
            Write(session.Cargo, key, quantity);
            Assert.That(session.SelectShip(8), Is.True, "Use enough capacity for every isolated cargo fixture.");
            session.StartVoyage();
            return session;
        }

        private static CoreVoyageSession AllCargoVoyage()
        {
            var session = new CoreVoyageSession();
            session.SetSeed("VOYAGE-CARGO-DISCARD-REGRESSION");
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All) Write(session.Cargo, item.Key, 10);
            session.Cargo.Water = session.Cargo.Food = 100;
            Assert.That(session.SelectShip(8), Is.True);
            session.StartVoyage();
            return session;
        }

        private static VoyageCargo Departure(CoreVoyageSession session) =>
            (VoyageCargo)typeof(CoreVoyageSession).GetField("departureCargo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        private static PropertyInfo CargoProperty(string key) => typeof(VoyageCargo).GetProperty(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        private static void Write(VoyageCargo cargo, string key, int value) => CargoProperty(key).SetValue(cargo, value);
        private static int Read(VoyageCargo cargo, string key) => (int)CargoProperty(key).GetValue(cargo);
        private static void SetCheckpointQuantity(CoreVoyageCargoCheckpoint cargo, string key, int value) =>
            typeof(CoreVoyageCargoCheckpoint).GetField(key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase).SetValue(cargo, value);
        private static void SetTreasureProgressQuantity(CoreTreasureProgress progress, string key, int value)
        {
            // The checkpoint also serializes the same six rare cargo quantities in treasure progress.
            switch (key)
            {
                case "gem": progress.gems = value; break;
                case "ancientmaterial": progress.ancientMaterials = value; break;
                case "relic": progress.relics = value; break;
                case "blackpearl": progress.blackPearls = value; break;
                case "ancientcompass": progress.ancientCompasses = value; break;
                case "lantern": progress.lanterns = value; break;
            }
        }
        private static void SetProperty(CoreVoyageSession session, string name, object value) => typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);
        private static void Invoke(CoreVoyageSession session, string name, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, args);
        private static void Finish(CoreVoyageSession session, VoyageOutcome outcome) => Invoke(session, "FinishVoyage", outcome);
        private static void EnterNode(CoreVoyageSession session, CoreMapNode node)
        {
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            SetProperty(session, "ActiveEncounter", node.Encounter);
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
        }
    }
}
