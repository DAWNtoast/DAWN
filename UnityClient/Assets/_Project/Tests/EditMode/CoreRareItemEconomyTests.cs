using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreRareItemEconomyTests
    {
        private static readonly PropertyInfo[] CargoProperties = typeof(VoyageCargo).GetProperties()
            .Where(p => p.PropertyType == typeof(int) && p.CanWrite).ToArray();

        [TestCase("gem", 60)] [TestCase("ancientmaterial", 8)] [TestCase("relic", 100)]
        [TestCase("blackpearl", 120)] [TestCase("ancientcompass", 75)] [TestCase("lantern", 20)]
        public void FixedRarePriceSupportsBothSourcesAtEveryPortWithoutCreatingMarketStock(string key, int expected)
        {
            Assert.That(CoreRareItemEconomy.SaleUnitPrice(" " + key.ToUpperInvariant() + " "), Is.EqualTo((decimal)expected));
            foreach (string port in CoreVoyageSession.DestinationPorts)
            foreach (int stored in new[] { 0, 2, 5 })
            {
                var session = Fixture(key);
                session.RestoreProfile(500.1234m, port);
                session.PortWarehouse.Restore("v2;" + port + "," + key + ",5");
                decimal money = session.ProfileGold;
                int logCount = session.LogEntries.Count;
                Assert.That(session.TrySellPortWarehouseCargo(key, stored, 5 - stored, expected * 5m), Is.True);
                Assert.That(session.ProfileGold, Is.EqualTo(money + expected * 5m));
                Assert.That(session.PortWarehouse.GetAmount(port, key), Is.EqualTo(5 - stored));
                Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(stored));
                Assert.That(session.LogEntries.Count, Is.EqualTo(logCount + 1));
                RejectUnchanged(session, () => session.TryPurchasePortWarehouseCargo(key, 1, expected));
            }
        }

        [TestCase(null)] [TestCase("")] [TestCase("gold")] [TestCase("water")]
        [TestCase("curse")] [TestCase("fragments")] [TestCase("unknown")]
        public void OnlyTheSixRareGoodsHaveAFixedRecoveryPrice(string key) =>
            Assert.That(CoreRareItemEconomy.SaleUnitPrice(key), Is.Zero);

        [TestCase("gem")] [TestCase("ancientmaterial")] [TestCase("relic")]
        [TestCase("blackpearl")] [TestCase("ancientcompass")] [TestCase("lantern")]
        public void ForgedOrStaleQuantityQuoteCannotTakeEitherSourceOrChangeMoney(string key)
        {
            var session = Fixture(key);
            decimal price = CoreRareItemEconomy.SaleUnitPrice(key);
            foreach (decimal wrong in new[] { 0m, -1m, price * 3m - .0001m, price * 3m + .0001m, 1m })
                RejectUnchanged(session, () => session.TrySellPortWarehouseCargo(key, 1, 2, wrong));
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo(key, 6, 1, price * 7));
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo(key, 1, 6, price * 7));
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo(key, -1, 2, price));
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo(key, 0, 0, price));
            Assert.That(session.TrySellPortWarehouseCargo(key, 1, 2, price * 3), Is.True);
            Assert.That(session.PortWarehouse.GetAmount("断锚港", key), Is.EqualTo(4));
            Assert.That(session.PortWarehouse.GetAmount("白贝港", key), Is.EqualTo(11));
            Assert.That(session.GetWarehouseCargoAmount(key), Is.EqualTo(3));
        }

        [Test]
        public void AccountLimitAndWideQuantityOverflowRejectWithoutDiscardingCargo()
        {
            var session = Fixture("gem");
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 120m, session.CurrentPort);
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo("gem", 1, 2, 180m));
            Assert.That(session.TrySellPortWarehouseCargo("gem", 1, 1, 120m), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo("gem", 1, 0, 60m));
            session.RestoreProfile(500m, session.CurrentPort);
            session.Cargo.Gem = int.MaxValue;
            session.PortWarehouse.Restore("v2;断锚港,gem,2147483647");
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo("gem", int.MaxValue, int.MaxValue, 1m));
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo("gem", int.MaxValue, int.MaxValue,
                60m * ((long)int.MaxValue + int.MaxValue)));
        }

        [Test]
        public void SaleSnapshotsRestoreRemainingRareCargoAndWarehouseWithoutRecreatingSoldGoods()
        {
            var source = Fixture("ancientmaterial");
            Assert.That(source.TrySellPortWarehouseCargo("ancientmaterial", 2, 3, 40m), Is.True);
            string warehouse = source.PortWarehouse.Serialize();
            CoreTreasureProgress treasure = source.CaptureTreasureProgress();
            var restored = Fixture("ancientmaterial");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                restored.RestoreProfile(source.ProfileGold, source.CurrentPort);
                restored.PortWarehouse.Restore(warehouse);
                restored.RestoreTreasureProgress(treasure);
                Assert.That(restored.GetWarehouseCargoAmount("ancientmaterial"), Is.EqualTo(2));
                Assert.That(restored.PortWarehouse.GetAmount("断锚港", "ancientmaterial"), Is.EqualTo(3));
                Assert.That(restored.PortWarehouse.GetAmount("白贝港", "ancientmaterial"), Is.EqualTo(11));
                Assert.That(restored.ProfileGold, Is.EqualTo(540.1234m));
            }
        }

        [TestCase(CoreDemoPhase.Sailing)] [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)] [TestCase(CoreDemoPhase.Settlement)]
        public void AValidRecoveryQuoteCannotSellAtSea(CoreDemoPhase phase)
        {
            var session = Fixture("gem");
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            RejectUnchanged(session, () => session.TrySellPortWarehouseCargo("gem", 1, 1, 120m));
        }

        private static CoreVoyageSession Fixture(string key)
        {
            var session = new CoreVoyageSession();
            foreach (PropertyInfo property in CargoProperties) property.SetValue(session.Cargo, 0);
            CargoProperties.Single(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)).SetValue(session.Cargo, 5);
            session.RestoreProfile(500.1234m, "断锚港");
            session.PortWarehouse.Restore("v2;断锚港," + key + ",5;白贝港," + key + ",11");
            return session;
        }

        private static void RejectUnchanged(CoreVoyageSession session, Func<bool> action)
        {
            decimal money = session.ProfileGold;
            string warehouse = session.PortWarehouse.Serialize();
            int[] cargo = CargoProperties.Select(p => (int)p.GetValue(session.Cargo)).ToArray();
            string[] log = session.LogEntries.ToArray();
            Assert.That(action(), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(money));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
            CollectionAssert.AreEqual(cargo, CargoProperties.Select(p => (int)p.GetValue(session.Cargo)).ToArray());
            CollectionAssert.AreEqual(log, session.LogEntries);
        }
    }
}
