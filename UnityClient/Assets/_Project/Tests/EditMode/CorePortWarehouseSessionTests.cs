using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CorePortWarehouseSessionTests
    {
        [TestCase("water", 28)]
        [TestCase("food", 22)]
        [TestCase("beer", 25)]
        [TestCase("medicine", 50)]
        [TestCase("wood", 2)]
        [TestCase("iron", 1)]
        [TestCase("cloth", 2)]
        [TestCase("powder", 4)]
        [TestCase("cannonballs", 2)]
        public void WithdrawUsesRealItemVolumeAndRejectsOneExtraUnitWithoutLosingStock(string item, int fits)
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = (int)session.CargoCapacityValue - 2;
            Assert.That(session.CargoCapacityValue - session.Cargo.UsedVolume, Is.EqualTo(2f),
                "Each volume boundary is tested with exactly two free cargo units, independent of hull size.");
            Assert.That(session.PortWarehouse.Restore("v1;断锚港," + item + ",100"), Is.True);
            AssertRejected(session, item, fits + 1, false);
            int aboard = session.GetMarketCargoAmount(item);
            Assert.That(session.TryWithdrawPortCargo(" " + item.ToUpperInvariant() + " ", fits), Is.True);
            Assert.That(session.GetMarketCargoAmount(item), Is.EqualTo(aboard + fits));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", item), Is.EqualTo(100 - fits));
            Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue + .001f));
        }

        [Test]
        public void DepositsRemainInTheirPortAndDoNotOccupyShipCapacity()
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = 40;
            Assert.That(session.TryDepositPortCargo(" WOOD ", 30), Is.True);
            Assert.That(session.Cargo.Wood, Is.EqualTo(10));
            Assert.That(session.Cargo.UsedVolume, Is.EqualTo(10));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "wood"), Is.EqualTo(30));
            session.RestoreProfile(session.ProfileGold, "白贝港");
            AssertRejected(session, "wood", 1, false);
            Assert.That(session.TryDepositPortCargo("wood", 10), Is.True);
            Assert.That(session.Cargo.UsedVolume, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount("白贝港", "wood"), Is.EqualTo(10));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "wood"), Is.EqualTo(30));
            session.RestoreProfile(session.ProfileGold, "断锚港");
            Assert.That(session.TryWithdrawPortCargo("wood", 30), Is.True);
            Assert.That(session.PortWarehouse.GetAmount("白贝港", "wood"), Is.EqualTo(10));
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
        public void EveryBasicCargoCanBeDepositedAndWithdrawnWithoutTouchingOtherAssets(string item)
        {
            CoreVoyageSession session = EmptyShip();
            session.PortWarehouse.Restore("v1;断锚港," + item + ",3");
            decimal gold = session.ProfileGold;
            Assert.That(session.TryWithdrawPortCargo(item, 3), Is.True);
            Assert.That(session.TryDepositPortCargo(item, 2), Is.True);
            Assert.That(session.GetMarketCargoAmount(item), Is.EqualTo(1));
            Assert.That(session.PortWarehouse.GetAmount("断锚港", item), Is.EqualTo(2));
            foreach (var property in typeof(VoyageCargo).GetProperties().Where(property => property.PropertyType == typeof(int)))
                if (!property.Name.Equals(item, System.StringComparison.OrdinalIgnoreCase))
                    Assert.That(property.GetValue(session.Cargo), Is.EqualTo(0), property.Name);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        [TestCase("wood", 0)]
        [TestCase("wood", -1)]
        [TestCase("wood", int.MinValue)]
        [TestCase("wood", int.MaxValue)]
        [TestCase("wood", 11)]
        [TestCase("gold", 1)]
        [TestCase("gem", 1)]
        [TestCase("unknown", 1)]
        [TestCase(null, 1)]
        public void InvalidOrUnavailableTransfersCannotMutateShipOrWarehouse(string item, int quantity)
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = 10;
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            AssertRejected(session, item, quantity, true);
            AssertRejected(session, item, quantity, false);
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void WarehouseCannotBeUsedOutsidePort(CoreDemoPhase phase)
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = 10;
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            AssertRejected(session, "wood", 1, true);
            AssertRejected(session, "wood", 1, false);
        }

        [Test]
        public void UnknownCurrentPortAndNegativeCargoCannotBypassTransferValidation()
        {
            CoreVoyageSession session = EmptyShip();
            session.Cargo.Wood = 10;
            session.PortWarehouse.Restore("v1;断锚港,wood,10");
            session.RestoreProfile(session.ProfileGold, "未知港");
            AssertRejected(session, "wood", 1, true);
            AssertRejected(session, "wood", 1, false);
            session.RestoreProfile(session.ProfileGold, "断锚港");
            session.Cargo.Gem = -1;
            AssertRejected(session, "wood", 1, true);
            AssertRejected(session, "wood", 1, false);
        }

        [Test]
        public void IntegerLimitsNeverWrapOrRemoveItemsFromTheWrongSide()
        {
            CoreVoyageSession session = EmptyShip();
            session.PortWarehouse.Restore("v1;断锚港,water,2147483646");
            session.Cargo.Water = 2;
            Assert.That(session.TryDepositPortCargo("water", 1), Is.True);
            Assert.That(session.PortWarehouse.GetAmount("断锚港", "water"), Is.EqualTo(int.MaxValue));
            AssertRejected(session, "water", 1, true);
            session.Cargo.Water = int.MaxValue;
            Assert.That(session.GetPortInvestmentMaterialAmount("water"), Is.EqualTo(2L * int.MaxValue));
            AssertRejected(session, "water", 1, false);
        }

        [Test]
        public void VoyageAndFailureDoNotClearOrMovePortStorage()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositPortCargo("wood", 4), Is.True);
            string stored = session.PortWarehouse.Serialize();
            session.StartVoyage();
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(stored));
            session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(stored));
            var restored = new CoreVoyageSession();
            Assert.That(restored.PortWarehouse.Restore(stored), Is.True);
            Assert.That(restored.PortWarehouse.GetAmount("断锚港", "wood"), Is.EqualTo(4));
        }

        private static CoreVoyageSession EmptyShip()
        {
            var session = new CoreVoyageSession();
            foreach (var property in typeof(VoyageCargo).GetProperties().Where(property => property.PropertyType == typeof(int)))
                property.SetValue(session.Cargo, 0);
            return session;
        }

        private static void AssertRejected(CoreVoyageSession session, string item, int quantity, bool deposit)
        {
            string stored = session.PortWarehouse.Serialize();
            var cargo = typeof(VoyageCargo).GetProperties().Where(property => property.PropertyType == typeof(int))
                .ToDictionary(property => property, property => (int)property.GetValue(session.Cargo));
            decimal gold = session.ProfileGold;
            string[] log = session.LogEntries.ToArray();
            Assert.That(deposit ? session.TryDepositPortCargo(item, quantity) : session.TryWithdrawPortCargo(item, quantity), Is.False);
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(stored));
            foreach (var pair in cargo) Assert.That(pair.Key.GetValue(session.Cargo), Is.EqualTo(pair.Value), pair.Key.Name);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(log, session.LogEntries);
        }
    }
}
