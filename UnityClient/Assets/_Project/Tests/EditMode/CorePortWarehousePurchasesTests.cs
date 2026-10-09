using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CorePortWarehousePurchasesTests
    {
        private static readonly string[] Materials =
            { "water", "food", "beer", "medicine", "wood", "iron", "cloth", "powder", "cannonballs" };
        private static readonly PropertyInfo[] CargoProperties = typeof(VoyageCargo).GetProperties()
            .Where(property => property.CanWrite && property.PropertyType == typeof(int)).ToArray();
        private static IEnumerable<string> CargoPropertyNames => CargoProperties.Select(property => property.Name);

        [TestCase("water")]
        [TestCase("food")]
        [TestCase("beer")]
        [TestCase("medicine")]
        [TestCase("wood")]
        [TestCase("iron")]
        [TestCase("cloth")]
        [TestCase("powder")]
        [TestCase("cannonballs")]
        public void FullShipCanPurchaseAnyBasicCargoDirectlyIntoWarehouse(string item)
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = (int)session.CargoCapacityValue;
            session.Cargo.Gold = 31;
            session.Cargo.Fame = 7;
            Dictionary<string, int> aboard = CaptureCargo(session);
            decimal gold = session.ProfileGold;
            Assert.That(session.TryPurchasePortWarehouseCargo(" " + item.ToUpperInvariant() + " ", 100, 17), Is.True);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, item), Is.EqualTo(100));
            Assert.That(session.ProfileGold, Is.EqualTo(gold - 17));
            AssertCargo(session, aboard);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue));
        }

        [TestCase("断锚港")]
        [TestCase("白贝港")]
        [TestCase("铜钟港")]
        [TestCase("盐冠港")]
        [TestCase("暮潮港")]
        [TestCase("灰鸥港")]
        public void PurchaseOnlyChangesTheCurrentPortsStock(string currentPort)
        {
            CoreVoyageSession session = EmptyShip();
            session.RestoreProfile(100, currentPort);
            session.PortWarehouse.Restore("v1;" + string.Join(";", CoreVoyageSession.DestinationPorts
                .Select(port => port + ",medicine,10")));
            Assert.That(session.TryPurchasePortWarehouseCargo("medicine", 3, 100), Is.True);
            Assert.That(session.ProfileGold, Is.Zero);
            foreach (string port in CoreVoyageSession.DestinationPorts)
                Assert.That(session.PortWarehouse.GetAmount(port, "medicine"), Is.EqualTo(port == currentPort ? 13 : 10), port);
            Assert.That(session.GetMarketCargoAmount("medicine"), Is.Zero);
            AssertUnchanged(session, () => session.TryPurchasePortWarehouseCargo("medicine", 1, 1));
        }

        [TestCase("wood", 0, 1)]
        [TestCase("wood", -1, 1)]
        [TestCase("wood", int.MinValue, 1)]
        [TestCase("wood", 1, 0)]
        [TestCase("wood", 1, -1)]
        [TestCase("wood", 1, int.MinValue)]
        [TestCase("wood", 1, int.MaxValue)]
        [TestCase(null, 1, 1)]
        [TestCase("", 1, 1)]
        [TestCase("  ", 1, 1)]
        [TestCase("unknown", 1, 1)]
        [TestCase("gold", 1, 1)]
        [TestCase("fame", 1, 1)]
        [TestCase("gem", 1, 1)]
        public void InvalidPurchaseCannotChangeMoneyCargoOrStorage(string item, int quantity, int cost)
        {
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,wood,10;白贝港,food,20");
            AssertUnchanged(session, () => session.TryPurchasePortWarehouseCargo(item, quantity, cost));
        }

        [Test]
        public void DirectPurchaseSupportsExactIntegerLimitsAndRejectsWarehouseOverflow()
        {
            CoreVoyageSession session = EmptyShip();
            session.RestoreProfile(int.MaxValue, session.CurrentPort);
            Assert.That(session.TryPurchasePortWarehouseCargo("wood", int.MaxValue, int.MaxValue), Is.True);
            Assert.That(session.ProfileGold, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(int.MaxValue));
            Assert.That(session.Cargo.Wood, Is.Zero);
            session.RestoreProfile(100, session.CurrentPort);
            AssertUnchanged(session, () => session.TryPurchasePortWarehouseCargo("wood", 1, 1));
            session.PortWarehouse.Restore("v1;断锚港,wood,2147483646");
            AssertUnchanged(session, () => session.TryPurchasePortWarehouseCargo("wood", 2, 1));
            Assert.That(session.TryPurchasePortWarehouseCargo("wood", 1, 1), Is.True);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void BulkDepositConservesAllFifteenGoodsAndPreservesOtherAssetsAndPorts()
        {
            var session = new CoreVoyageSession();
            int amount = 1;
            foreach (PropertyInfo property in CargoProperties) property.SetValue(session.Cargo, amount++);
            session.RestoreProfile(700, "白贝港");
            session.PortWarehouse.Restore("v1;" + string.Join(";", CoreVoyageSession.DestinationPorts
                .SelectMany(port => Materials.Select(item => port + "," + item + ",20"))));
            Dictionary<string, int> aboard = CaptureCargo(session);
            Assert.That(session.CanDepositAllPortCargo(), Is.True);
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            foreach (string port in CoreVoyageSession.DestinationPorts)
            foreach (CoreWarehouseItemDefinition definition in CoreWarehouseItemCatalog.All)
            {
                string item = definition.Key;
                int transferred = aboard.Single(pair => pair.Key.Equals(item, StringComparison.OrdinalIgnoreCase)).Value;
                int previousStock = definition.IsRare ? 0 : 20;
                Assert.That(session.PortWarehouse.GetAmount(port, item), Is.EqualTo(previousStock + (port == "白贝港" ? transferred : 0)), port + "/" + item);
            }
            foreach (PropertyInfo property in CargoProperties)
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(CoreWarehouseItemCatalog.Find(property.Name) != null ? 0 : aboard[property.Name]), property.Name);
            Assert.That(session.ProfileGold, Is.EqualTo(700));
            string saved = session.PortWarehouse.Serialize();
            var reloaded = new CoreVoyageSession();
            Assert.That(reloaded.PortWarehouse.Restore(saved), Is.True);
            Assert.That(reloaded.PortWarehouse.Serialize(), Is.EqualTo(saved));
            AssertUnchanged(session, () => session.TryDepositAllPortCargo(), true);
        }

        [TestCase("water")]
        [TestCase("food")]
        [TestCase("beer")]
        [TestCase("medicine")]
        [TestCase("wood")]
        [TestCase("iron")]
        [TestCase("cloth")]
        [TestCase("powder")]
        [TestCase("cannonballs")]
        public void OneOverflowingWarehouseBalanceRejectsTheEntireBatch(string item)
        {
            CoreVoyageSession session = EmptyShip();
            foreach (PropertyInfo property in CargoProperties.Where(property => Materials.Contains(property.Name.ToLowerInvariant())))
                property.SetValue(session.Cargo, 2);
            session.PortWarehouse.Restore("v1;断锚港," + item + ",2147483646;白贝港,food,10");
            AssertUnchanged(session, () => session.CanDepositAllPortCargo());
            AssertUnchanged(session, () => session.TryDepositAllPortCargo());
        }

        [Test]
        public void EmptyShipIsASuccessfulNoOpEvenWhenAllLocalStockIsAtItsLimit()
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Gold = 99;
            session.Cargo.Fame = 11;
            session.PortWarehouse.Restore("v2;" + string.Join(";", CoreWarehouseItemCatalog.All.Select(item => "断锚港," + item.Key + ",2147483647")));
            AssertUnchanged(session, () => session.CanDepositAllPortCargo(), true);
            AssertUnchanged(session, () => session.TryDepositAllPortCargo(), true);
        }

        [Test]
        public void BulkDepositAcceptsExactLimitsAndDoesNotCountAnotherPortsFullWarehouse()
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = int.MaxValue;
            session.Cargo.Food = 1;
            session.PortWarehouse.Restore("v1;白贝港,wood,2147483647;断锚港,food,2147483646");
            Assert.That(session.CanDepositAllPortCargo(), Is.True);
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(session.Cargo.Food, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "wood"), Is.EqualTo(int.MaxValue));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "food"), Is.EqualTo(int.MaxValue));
            Assert.That(session.PortWarehouse.GetAmount("白贝港", "wood"), Is.EqualTo(int.MaxValue));
        }

        [TestCaseSource(nameof(CargoPropertyNames))]
        public void AnyNegativeCargoRejectsTheWholeBulkDeposit(string resource)
        {
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,water,10;断锚港,wood,20");
            CargoProperties.Single(property => property.Name == resource).SetValue(session.Cargo, -1);
            AssertUnchanged(session, () => session.CanDepositAllPortCargo());
            AssertUnchanged(session, () => session.TryDepositAllPortCargo());
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void PurchasesAndBulkDepositsRejectEveryNonPortPhase(CoreDemoPhase phase)
        {
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            AssertUnchanged(session, () => session.TryPurchasePortWarehouseCargo("wood", 1, 1));
            AssertUnchanged(session, () => session.CanDepositAllPortCargo());
            AssertUnchanged(session, () => session.TryDepositAllPortCargo());
        }

        [Test]
        public void UnknownCurrentPortCannotReceivePurchasesOrBulkDeposits()
        {
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            session.RestoreProfile(session.ProfileGold, "未知港");
            AssertUnchanged(session, () => session.TryPurchasePortWarehouseCargo("wood", 1, 1));
            AssertUnchanged(session, () => session.CanDepositAllPortCargo());
            AssertUnchanged(session, () => session.TryDepositAllPortCargo());
        }

        private static CoreVoyageSession EmptyShip()
        {
            var session = new CoreVoyageSession();
            foreach (PropertyInfo property in CargoProperties) property.SetValue(session.Cargo, 0);
            return session;
        }

        private static Dictionary<string, int> CaptureCargo(CoreVoyageSession session) =>
            CargoProperties.ToDictionary(property => property.Name, property => (int)property.GetValue(session.Cargo));

        private static void AssertCargo(CoreVoyageSession session, Dictionary<string, int> expected)
        {
            foreach (PropertyInfo property in CargoProperties)
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(expected[property.Name]), property.Name);
        }

        private static void AssertUnchanged(CoreVoyageSession session, Func<bool> action, bool expectedResult = false)
        {
            decimal gold = session.ProfileGold;
            string warehouse = session.PortWarehouse.Serialize();
            Dictionary<string, int> cargo = CaptureCargo(session);
            string[] log = session.LogEntries.ToArray();
            Assert.That(action(), Is.EqualTo(expectedResult));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            AssertCargo(session, cargo);
            CollectionAssert.AreEqual(log, session.LogEntries);
        }
    }
}
