using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CorePortWarehouseTradingTests
    {
        private static readonly PropertyInfo[] CargoProperties = typeof(VoyageCargo).GetProperties()
            .Where(property => property.CanWrite && property.PropertyType == typeof(int)).ToArray();
        private static IEnumerable<string> CargoPropertyNames => CargoProperties.Select(property => property.Name);

        [TestCase("water", 28)]
        [TestCase("food", 22)]
        [TestCase("beer", 25)]
        [TestCase("medicine", 50)]
        [TestCase("wood", 2)]
        [TestCase("iron", 1)]
        [TestCase("cloth", 2)]
        [TestCase("powder", 4)]
        [TestCase("cannonballs", 2)]
        public void MaximumUsesTrustedFractionalVolumesAndCanBeLoadedExactly(string item, int expected)
        {
            CoreVoyageSession session = EmptyShip();
            int ballast = (int)session.CargoCapacityValue - 2;
            session.Cargo.Wood = ballast;
            Assert.That(session.CargoCapacityValue - session.Cargo.UsedVolume, Is.EqualTo(2f));
            session.PortWarehouse.Restore("v1;断锚港," + item + ",100;白贝港," + item + ",1000");
            string normalized = " " + item.ToUpperInvariant() + " ";
            Assert.That(session.GetMaxPortWarehouseWithdrawal(normalized), Is.EqualTo(expected));
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo(item, expected + 1));
            Assert.That(session.TryAdjustPortWarehouseCargo(normalized, expected), Is.True);
            Assert.That(session.GetMaxPortWarehouseWithdrawal(item), Is.Zero);
            Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue + .001f));
            Assert.That(session.TryAdjustPortWarehouseCargo(normalized, -expected), Is.True);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(ballast));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", item), Is.EqualTo(100));
            Assert.That(session.PortWarehouse.GetAmount("白贝港", item), Is.EqualTo(1000));
        }

        [Test]
        public void MaximumIsLimitedToLocalStockAndCannotBorrowFromAnotherPort()
        {
            CoreVoyageSession session = EmptyShip();
            session.PortWarehouse.Restore("v1;断锚港,medicine,3;白贝港,medicine,2147483647");
            Assert.That(session.GetMaxPortWarehouseWithdrawal("medicine"), Is.EqualTo(3));
            Assert.That(session.TryAdjustPortWarehouseCargo("medicine", 3), Is.True);
            Assert.That(session.GetMaxPortWarehouseWithdrawal("medicine"), Is.Zero);
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo("medicine", 1));
            session.RestoreProfile(session.ProfileGold, "白贝港");
            Assert.That(session.GetMaxPortWarehouseWithdrawal("medicine"), Is.GreaterThan(3));
            Assert.That(session.PortWarehouse.GetAmount("白贝港", "medicine"), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void FullOrOverloadedShipCanUnloadEvenWhenItCannotLoadAnything()
        {
            CoreVoyageSession session = EmptyShip();
            int capacity = (int)session.CargoCapacityValue;
            session.Cargo.Wood = capacity;
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue));
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            Assert.That(session.GetMaxPortWarehouseWithdrawal("wood"), Is.Zero);
            Assert.That(session.TryAdjustPortWarehouseCargo("wood", -1), Is.True);
            Assert.That(session.GetMaxPortWarehouseWithdrawal("wood"), Is.EqualTo(1));
            Assert.That(session.TryAdjustPortWarehouseCargo("wood", 1), Is.True);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue));
            session.Cargo.Wood = capacity + 1;
            Assert.That(session.GetMaxPortWarehouseWithdrawal("wood"), Is.Zero);
            Assert.That(session.TryAdjustPortWarehouseCargo("wood", -1), Is.True);
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(session.CargoCapacityValue));
        }

        [Test]
        public void SignedAdjustmentHandlesIntegerLimitsWithoutNegationOverflow()
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = int.MaxValue;
            Assert.That(session.GetMaxPortWarehouseWithdrawal("wood"), Is.Zero);
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo("wood", 0));
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo("wood", int.MinValue));
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo("wood", int.MaxValue));
            Assert.That(session.TryAdjustPortWarehouseCargo("wood", -int.MaxValue), Is.True);
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "wood"), Is.EqualTo(int.MaxValue));
            Assert.That(session.GetMaxPortWarehouseWithdrawal("wood"), Is.EqualTo((int)session.CargoCapacityValue));
            session.Cargo.Wood = 1;
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo("wood", -1));
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
        public void SalesSupportEitherSourceOrBothAndCreditRevenueOnce(string item)
        {
            foreach (int stored in new[] { 0, 3, 5 })
            {
                int aboard = 5 - stored;
                CoreVoyageSession session = EmptyShip();
                foreach (PropertyInfo property in CargoProperties) property.SetValue(session.Cargo, 10);
                session.PortWarehouse.Restore("v1;断锚港," + item + ",10;白贝港," + item + ",20");
                Dictionary<string, int> before = CaptureCargo(session);
                decimal gold = session.ProfileGold;
                int logCount = session.LogEntries.Count;
                Assert.That(session.TrySellPortWarehouseCargo(" " + item.ToUpperInvariant() + " ", stored, aboard, 7), Is.True);
                Assert.That(session.ProfileGold, Is.EqualTo(gold + 7));
                Assert.That(session.PortWarehouse.GetAmount("断锚港", item), Is.EqualTo(10 - stored));
                Assert.That(session.PortWarehouse.GetAmount("白贝港", item), Is.EqualTo(20));
                foreach (PropertyInfo property in CargoProperties)
                    Assert.That(property.GetValue(session.Cargo), Is.EqualTo(before[property.Name] -
                        (property.Name.Equals(item, StringComparison.OrdinalIgnoreCase) ? aboard : 0)), property.Name);
                Assert.That(session.LogEntries.Count, Is.EqualTo(logCount + 1));
            }
        }

        [TestCase(4, 1)]
        [TestCase(1, 3)]
        public void InsufficientStockOnEitherSideDoesNotSpendTheOtherSide(int stored, int aboard)
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = 2;
            session.PortWarehouse.Restore("v1;断锚港,wood,3;白贝港,wood,1000");
            AssertUnchanged(session, () => session.TrySellPortWarehouseCargo("wood", stored, aboard, 20));
        }

        [TestCase(-1, 1, 1)]
        [TestCase(1, -1, 1)]
        [TestCase(int.MinValue, 1, 1)]
        [TestCase(1, int.MinValue, 1)]
        [TestCase(0, 0, 1)]
        [TestCase(1, 1, 0)]
        [TestCase(1, 1, -1)]
        [TestCase(1, 1, int.MinValue)]
        [TestCase(1, 1, int.MaxValue)]
        [TestCase(int.MaxValue, 1, 1)]
        [TestCase(1, int.MaxValue, 1)]
        public void InvalidSaleAmountsCannotMutateAnyAssets(int stored, int aboard, int revenue)
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = 10;
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            AssertUnchanged(session, () => session.TrySellPortWarehouseCargo("wood", stored, aboard, revenue));
        }

        [Test]
        public void SalePreservesExistingAccountLimitAndRejectsOverflowWithoutLosingEitherStock()
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = 10;
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 5, session.CurrentPort);
            AssertUnchanged(session, () => session.TrySellPortWarehouseCargo("wood", 1, 1, 6));
            Assert.That(session.TrySellPortWarehouseCargo("wood", 1, 1, 5), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            AssertUnchanged(session, () => session.TrySellPortWarehouseCargo("wood", 1, 1, 1));
            session.RestoreProfile(int.MaxValue, session.CurrentPort);
            AssertUnchanged(session, () => session.TrySellPortWarehouseCargo("wood", 1, 1, int.MaxValue));
        }

        [Test]
        public void TwoIntegerLimitStocksCanBeSoldWithoutSummingQuantitiesInAnInt()
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = int.MaxValue;
            session.PortWarehouse.Restore("v1;断锚港,wood,2147483647;白贝港,wood,2147483647");
            decimal gold = session.ProfileGold;
            Assert.That(session.TrySellPortWarehouseCargo("wood", int.MaxValue, int.MaxValue, 1), Is.True);
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "wood"), Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount("白贝港", "wood"), Is.EqualTo(int.MaxValue));
            Assert.That(session.ProfileGold, Is.EqualTo(gold + 1));
            AssertUnchanged(session, () => session.TrySellPortWarehouseCargo("wood", 1, 1, 1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("gold")]
        [TestCase("unknown")]
        public void InvalidItemsCannotLoadUnloadOrSell(string item)
        {
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            Assert.That(session.GetMaxPortWarehouseWithdrawal(item), Is.Zero);
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo(item, 1));
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo(item, -1));
            AssertUnchanged(session, () => session.TrySellPortWarehouseCargo(item, 1, 1, 1));
        }

        [TestCaseSource(nameof(CargoPropertyNames))]
        public void AnyNegativeCargoBlocksAllWarehouseTrading(string resource)
        {
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            CargoProperties.Single(property => property.Name == resource).SetValue(session.Cargo, -1);
            AssertTradingRejected(session);
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void WarehouseTradingRequiresPortPhase(CoreDemoPhase phase)
        {
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            AssertTradingRejected(session);
        }

        [Test]
        public void UnknownCurrentPortCannotTradeKnownPortsStockOrShipCargo()
        {
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            session.RestoreProfile(session.ProfileGold, "未知港");
            AssertTradingRejected(session);
        }

        private static void AssertTradingRejected(CoreVoyageSession session)
        {
            Assert.That(session.GetMaxPortWarehouseWithdrawal("wood"), Is.Zero);
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo("wood", 1));
            AssertUnchanged(session, () => session.TryAdjustPortWarehouseCargo("wood", -1));
            AssertUnchanged(session, () => session.TrySellPortWarehouseCargo("wood", 1, 1, 1));
        }

        private static CoreVoyageSession EmptyShip()
        {
            var session = new CoreVoyageSession();
            foreach (PropertyInfo property in CargoProperties) property.SetValue(session.Cargo, 0);
            return session;
        }

        private static Dictionary<string, int> CaptureCargo(CoreVoyageSession session) =>
            CargoProperties.ToDictionary(property => property.Name, property => (int)property.GetValue(session.Cargo));

        private static void AssertUnchanged(CoreVoyageSession session, Func<bool> action)
        {
            decimal gold = session.ProfileGold;
            string warehouse = session.PortWarehouse.Serialize();
            Dictionary<string, int> cargo = CaptureCargo(session);
            string[] log = session.LogEntries.ToArray();
            Assert.That(action(), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            foreach (PropertyInfo property in CargoProperties)
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(cargo[property.Name]), property.Name);
            CollectionAssert.AreEqual(log, session.LogEntries);
        }
    }
}
