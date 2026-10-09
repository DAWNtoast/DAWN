using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreRarePortWarehouseTests
    {
        private static readonly string[] RareKeys = { "gem", "ancientmaterial", "relic", "blackpearl", "ancientcompass", "lantern" };
        private static readonly PropertyInfo[] CargoFields = typeof(VoyageCargo).GetProperties()
            .Where(p => p.PropertyType == typeof(int) && p.CanWrite).ToArray();

        [TestCase("gem", "宝石", 5d)]
        [TestCase("ancientmaterial", "古代材料", 1.5d)]
        [TestCase("relic", "稀有遗物", 5d)]
        [TestCase("blackpearl", "黑珍珠", 4d)]
        [TestCase("ancientcompass", "古代罗盘", 3d)]
        [TestCase("lantern", "航海灯", 1d)]
        public void RareCatalogUsesActualCargoWeightAndDoesNotBecomeMarketStock(string key, string name, double volume)
        {
            var session = EmptyShip();
            CoreWarehouseItemDefinition item = CoreWarehouseItemCatalog.Find(" " + key.ToUpperInvariant() + " ");
            Assert.That(item.Name, Is.EqualTo(name));
            Assert.That(item.UnitVolume, Is.EqualTo(volume));
            Assert.That(item.IsRare, Is.True);
            SetCargo(session, key, 2);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(2 * volume).Within(.00001));
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(2));
            Assert.That(session.GetPortWarehouseCargoAmount(key), Is.EqualTo(2));
            Assert.That(session.GetMarketCargoAmount(key), Is.Zero);
            Assert.That(session.TryDepositPortCargo(key, 1), Is.True);
            RejectUnchanged(session, () => session.TryPurchasePortWarehouseCargo(key, 1, 1m));
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo(key, 1, 1, 1m)); // Reject a forged recovery quote.
            session.Cargo.Wood = session.Cargo.Iron = 10;
            RejectUnchanged(session, () => session.TrySpendPortInvestment(1m, 1, 1, key, 1));
            Assert.That(session.GetPortInvestmentMaterialAmount(key), Is.EqualTo(2),
                "Rare donations may use the local warehouse plus carried goods, but do not replace the port specialty.");
        }

        [Test]
        public void CatalogSeparatesFifteenPhysicalGoodsAndExcludesCurrenciesAndCurses()
        {
            Assert.That(CoreWarehouseItemCatalog.All.Count, Is.EqualTo(15));
            Assert.That(CoreWarehouseItemCatalog.BasicItems.Count, Is.EqualTo(9));
            Assert.That(CoreWarehouseItemCatalog.RareItems.Select(i => i.Key), Is.EquivalentTo(RareKeys));
            Assert.That(CoreWarehouseItemCatalog.All.Select(i => i.Key).Distinct().Count(), Is.EqualTo(15));
            Assert.That(CoreWarehouseItemCatalog.BasicItems.All(i => !i.IsRare), Is.True);
            foreach (string invalid in new[] { "gold", "fame", "curse", "fragments", "treasuremap", "unknown", "", null })
            {
                Assert.That(CoreWarehouseItemCatalog.Find(invalid), Is.Null);
                var session = EmptyShip();
                RejectUnchanged(session, () => session.TryDepositPortCargo(invalid, 1));
                RejectUnchanged(session, () => session.TryWithdrawPortCargo(invalid, 1));
                Assert.That(session.GetMaxPortWarehouseWithdrawal(invalid), Is.Zero);
            }
        }

        [TestCase("gem")]
        [TestCase("ancientmaterial")]
        [TestCase("relic")]
        [TestCase("blackpearl")]
        [TestCase("ancientcompass")]
        [TestCase("lantern")]
        public void RareTransfersAreLocalConservedAndUseSignedAdjustment(string key)
        {
            var session = EmptyShip();
            SetCargo(session, key, 3);
            session.Cargo.Gold = 31; session.Cargo.Fame = 7; session.Cargo.Curse = 2;
            decimal money = session.ProfileGold;
            Assert.That(session.TryAdjustPortWarehouseCargo(" " + key.ToUpperInvariant() + " ", -2), Is.True);
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(1));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", key), Is.EqualTo(2));
            session.RestoreProfile(money, "白贝港");
            RejectUnchanged(session, () => session.TryWithdrawPortCargo(key, 1));
            Assert.That(session.GetMaxPortWarehouseWithdrawal(key), Is.Zero);
            Assert.That(session.TryDepositPortCargo(key, 1), Is.True);
            session.RestoreProfile(money, "断锚港");
            Assert.That(session.TryAdjustPortWarehouseCargo(key, 2), Is.True);
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(2));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", key), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount("白贝港", key), Is.EqualTo(1));
            Assert.That(session.Cargo.Gold, Is.EqualTo(31));
            Assert.That(session.Cargo.Fame, Is.EqualTo(7));
            Assert.That(session.Cargo.Curse, Is.EqualTo(2));
            Assert.That(session.ProfileGold, Is.EqualTo(money));
            foreach (CoreWarehouseItemDefinition other in CoreWarehouseItemCatalog.All.Where(i => i.Key != key))
                Assert.That(session.GetWarehouseCargoAmount(other.Key), Is.Zero);
        }

        [TestCase("gem")]
        [TestCase("ancientmaterial")]
        [TestCase("relic")]
        [TestCase("blackpearl")]
        [TestCase("ancientcompass")]
        [TestCase("lantern")]
        public void CapacityAndIntegerBoundsNeverLoseRareCargo(string key)
        {
            var session = EmptyShip();
            Assert.That(session.PortWarehouse.Restore("v2;断锚港," + key + ",2147483647"), Is.True);
            session.Cargo.Wood = (int)session.CargoCapacityValue;
            Assert.That(session.GetMaxPortWarehouseWithdrawal(key), Is.Zero);
            RejectUnchanged(session, () => session.TryWithdrawPortCargo(key, 1));
            SetCargo(session, key, 1);
            RejectUnchanged(session, () => session.TryDepositPortCargo(key, 1));
            RejectUnchanged(session, () => session.TryDepositAllPortCargo());
            RejectUnchanged(session, () => session.TryDepositAllRarePortCargo());
            Assert.That(session.CanDepositAllPortCargo(), Is.False);
            Assert.That(session.CanDepositAllRarePortCargo(), Is.False);
            Assert.That(session.PortWarehouse.Restore("v2;断锚港," + key + ",1"), Is.True);
            SetCargo(session, key, int.MaxValue);
            RejectUnchanged(session, () => session.TryWithdrawPortCargo(key, 1));
            Assert.That(session.GetMaxPortWarehouseWithdrawal(key), Is.Zero);
            // An overloaded ship can still unload safely into a warehouse.
            SetCargo(session, key, 2);
            Assert.That(session.TryDepositPortCargo(key, 2), Is.True);
            Assert.That(session.GetWarehouseCargoAmount(key), Is.Zero);
        }

        [Test]
        public void FractionalRareWeightUsesTheSameExactCapacityAsWithdrawal()
        {
            var session = EmptyShip();
            session.Cargo.Wood = 77;
            Assert.That(session.PortWarehouse.Restore("v2;断锚港,ancientmaterial,3"), Is.True);
            Assert.That(session.GetMaxPortWarehouseWithdrawal("ancientmaterial"), Is.EqualTo(2));
            RejectUnchanged(session, () => session.TryWithdrawPortCargo("ancientmaterial", 3));
            Assert.That(session.TryWithdrawPortCargo("ancientmaterial", 2), Is.True);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue));
            Assert.That(session.GetMaxPortWarehouseWithdrawal("ancientmaterial"), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "ancientmaterial"), Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BulkStorageIsAtomicAndLeavesNonPhysicalAssetsUntouched(bool rareOnly)
        {
            var session = EmptyShip();
            session.RestoreTreasureProgress(new CoreTreasureProgress { fragments = 7, pirateExperience = 120, curses = 4,
                maps = new[] { new CoreTreasureMapSave { id = "stored-map", seed = "WAREHOUSE-MAP", rankId = "weathered" } } });
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All) SetCargo(session, item.Key, 2);
            session.Cargo.Gold = 31; session.Cargo.Fame = 9;
            Assert.That(session.PortWarehouse.Restore("v2;白贝港,gem,11;断锚港,lantern,2147483646"), Is.True);
            RejectUnchanged(session, () => rareOnly ? session.TryDepositAllRarePortCargo() : session.TryDepositAllPortCargo());
            Assert.That(session.PortWarehouse.Restore("v2;白贝港,gem,11;断锚港,lantern,2147483645"), Is.True);
            Assert.That(rareOnly ? session.TryDepositAllRarePortCargo() : session.TryDepositAllPortCargo(), Is.True);
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
            {
                bool moved = !rareOnly || item.IsRare;
                Assert.That(session.GetWarehouseCargoAmount(item.Key), Is.EqualTo(moved ? 0 : 2), item.Key);
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, item.Key), Is.EqualTo(item.Key == "lantern" ? int.MaxValue : moved ? 2 : 0), item.Key);
            }
            Assert.That(session.PortWarehouse.GetAmount("白贝港", "gem"), Is.EqualTo(11));
            Assert.That(session.Cargo.Gold, Is.EqualTo(31)); Assert.That(session.Cargo.Fame, Is.EqualTo(9));
            Assert.That(session.Cargo.Curse, Is.EqualTo(4)); Assert.That(session.TreasureFragments, Is.EqualTo(7));
            Assert.That(session.PirateExperience, Is.EqualTo(120)); Assert.That(session.ProfileGold, Is.EqualTo(500m));
            Assert.That(session.TreasureMaps.Single().Id, Is.EqualTo("stored-map"));
            string before = Snapshot(session);
            Assert.That(rareOnly ? session.TryDepositAllRarePortCargo() : session.TryDepositAllPortCargo(), Is.True);
            Assert.That(Snapshot(session), Is.EqualTo(before), "An empty batch is an idempotent no-op.");
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void RareTransfersOnlyOperateAtALegalPort(CoreDemoPhase phase)
        {
            var session = EmptyShip(); session.Cargo.Gem = 2;
            session.PortWarehouse.Restore("v2;断锚港,gem,3");
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            Assert.That(session.CanDepositAllRarePortCargo(), Is.False);
            Assert.That(session.GetMaxPortWarehouseWithdrawal("gem"), Is.Zero);
            RejectUnchanged(session, () => session.TryDepositPortCargo("gem", 1));
            RejectUnchanged(session, () => session.TryWithdrawPortCargo("gem", 1));
            RejectUnchanged(session, () => session.TryDepositAllRarePortCargo());
            RejectUnchanged(session, () => session.TryDepositAllPortCargo());
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
        public void InvalidOrUnavailableQuantitiesPreserveEveryAsset(int quantity)
        {
            var session = EmptyShip(); session.Cargo.Gem = 2;
            session.PortWarehouse.Restore("v2;断锚港,gem,3");
            RejectUnchanged(session, () => session.TryDepositPortCargo("gem", quantity));
            RejectUnchanged(session, () => session.TryWithdrawPortCargo("gem", quantity));
            if (quantity == 0 || quantity == int.MinValue)
                RejectUnchanged(session, () => session.TryAdjustPortWarehouseCargo("gem", quantity));
            session.Cargo.Lantern = -1;
            RejectUnchanged(session, () => session.TryDepositPortCargo("gem", 1));
            RejectUnchanged(session, () => session.TryWithdrawPortCargo("gem", 1));
            Assert.That(session.CanDepositAllRarePortCargo(), Is.False);
            session.Cargo.Lantern = 0;
            session.RestoreProfile(500m, "未知港");
            RejectUnchanged(session, () => session.TryDepositAllRarePortCargo());
        }

        [Test]
        public void V1BasicRecordsMigrateAndV2RoundTripsAllSixPortsWithoutAccumulating()
        {
            var warehouse = new CorePortWarehouse();
            Assert.That(warehouse.Restore("v1;断锚港,water,12;灰鸥港,iron,7"), Is.True);
            Assert.That(warehouse.Serialize(), Does.StartWith("v2;"));
            Assert.That(warehouse.GetAmount("断锚港", "water"), Is.EqualTo(12));
            var records = new List<string> { "v2" }; int count = 0;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                records.Add(port + "," + item.Key + "," + ++count);
            Assert.That(warehouse.Restore(string.Join(";", records)), Is.True);
            string saved = warehouse.Serialize();
            Assert.That(warehouse.Restore(saved), Is.True);
            Assert.That(warehouse.Serialize(), Is.EqualTo(saved));
            count = 0;
            foreach (string port in CoreVoyageSession.DestinationPorts)
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
                Assert.That(warehouse.GetAmount(port, item.Key), Is.EqualTo(++count));
        }

        [TestCase("v3;断锚港,gem,1")]
        [TestCase("v1;断锚港,gem,1")]
        [TestCase("v2;未知港,gem,1")]
        [TestCase("v2;断锚港,curse,1")]
        [TestCase("v2;断锚港,gold,1")]
        [TestCase("v2;断锚港,gem,-1")]
        [TestCase("v2;断锚港,gem,2147483648")]
        [TestCase("v2;断锚港,gem,1.5")]
        [TestCase("v2;断锚港,gem,1; 断锚港 , GEM ,2")]
        [TestCase("v2;断锚港,gem,1;白贝港,lantern")]
        public void MalformedRareWarehouseSnapshotRejectsTheEntireReplacement(string serialized)
        {
            var warehouse = new CorePortWarehouse(); warehouse.Restore("v2;断锚港,gem,9;白贝港,wood,8");
            string before = warehouse.Serialize();
            Assert.That(warehouse.Restore(serialized), Is.False);
            Assert.That(warehouse.Serialize(), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void TreasureAndWarehouseSnapshotsReplaceSeparateBalancesWithoutDuplicating(bool treasureFirst)
        {
            var original = EmptyShip();
            foreach (string key in RareKeys) SetCargo(original, key, 5);
            original.Cargo.Curse = 3;
            foreach (string key in RareKeys) Assert.That(original.TryDepositPortCargo(key, 3), Is.True);
            string warehouse = original.PortWarehouse.Serialize();
            CoreTreasureProgress treasure = original.CaptureTreasureProgress();
            var restored = EmptyShip();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                if (treasureFirst) restored.RestoreTreasureProgress(treasure);
                Assert.That(restored.PortWarehouse.Restore(warehouse), Is.True);
                if (!treasureFirst) restored.RestoreTreasureProgress(treasure);
                foreach (string key in RareKeys)
                {
                    Assert.That(restored.GetWarehouseCargoAmount(key), Is.EqualTo(2));
                    Assert.That(restored.PortWarehouse.GetAmount("断锚港", key), Is.EqualTo(3));
                }
                Assert.That(restored.Cargo.Curse, Is.EqualTo(3));
            }
            Assert.That(restored.TryWithdrawPortCargo("gem", 1), Is.True);
            var reloaded = EmptyShip();
            Assert.That(reloaded.PortWarehouse.Restore(restored.PortWarehouse.Serialize()), Is.True);
            reloaded.RestoreTreasureProgress(restored.CaptureTreasureProgress());
            Assert.That(reloaded.Cargo.Gem, Is.EqualTo(3));
            Assert.That(reloaded.PortWarehouse.GetAmount("断锚港", "gem"), Is.EqualTo(2));
        }

        [Test]
        public void RareStockSurvivesAVoyageFailureWithoutReturningToTheShip()
        {
            var session = EmptyShip(); session.Cargo.Gem = 3; session.Cargo.AncientMaterial = 2;
            Assert.That(session.TryDepositAllRarePortCargo(), Is.True);
            string stored = session.PortWarehouse.Serialize();
            session.StartVoyage();
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(stored));
            Assert.That(session.Cargo.Gem, Is.Zero); Assert.That(session.Cargo.AncientMaterial, Is.Zero);
        }

        private static CoreVoyageSession EmptyShip()
        {
            var session = new CoreVoyageSession();
            foreach (PropertyInfo property in CargoFields) property.SetValue(session.Cargo, 0);
            return session;
        }
        private static void SetCargo(CoreVoyageSession session, string key, int quantity) =>
            CargoFields.Single(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)).SetValue(session.Cargo, quantity);
        private static string Snapshot(CoreVoyageSession session) => session.ProfileGold + "|" + session.PortWarehouse.Serialize() + "|" +
            string.Join(",", CargoFields.Select(p => p.Name + "=" + p.GetValue(session.Cargo))) + "|" + session.TreasureFragments + "|" + session.PirateExperience;
        private static void RejectUnchanged(CoreVoyageSession session, Func<bool> operation)
        {
            string before = Snapshot(session);
            Assert.That(operation(), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(before));
        }
    }
}
