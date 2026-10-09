using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class PortInvestmentCatalogTests
    {
        private static IEnumerable<string> Ports => PortInvestmentCatalog.SupportedPorts;

        [Test]
        public void EveryReachablePortHasADistinctConstructionPlanAtEveryTier()
        {
            CollectionAssert.AreEquivalent(CoreVoyageSession.DestinationPorts, PortInvestmentCatalog.SupportedPorts);
            for (int level = 1; level <= PortInvestmentCatalog.MaxLevel; level++)
            {
                var plans = new HashSet<string>();
                foreach (string port in Ports)
                {
                    PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, level);
                    plans.Add(cost.RequiredItem + ":" + cost.RequiredQuantity);
                }
                Assert.That(plans.Count, Is.EqualTo(PortInvestmentCatalog.SupportedPorts.Count),
                    "Ports may need the same commodity, but their required quantity must distinguish their plans at tier " + level);
            }
        }

        [TestCaseSource(nameof(Ports))]
        public void EachUpgradeRequiresFourPositiveIncreasingCostsAndALocallyExpensiveDistinctCommodity(string port)
        {
            PortInvestmentCost previous = PortInvestmentCatalog.CostForLevel(port, 0);
            Assert.That(previous.IsEmpty, Is.True);
            PortMarketItem required = PortInvestmentCatalog.RequiredItemFor(port);
            Assert.That(required, Is.Not.EqualTo(PortMarketItem.Wood).And.Not.EqualTo(PortMarketItem.Iron));
            Assert.That(CorePortEconomy.LocalPriceMultiplier(port, PortMarketCatalog.Key(required)),
                Is.GreaterThanOrEqualTo(1.45m), port + " must demand a genuinely expensive local commodity.");
            for (int level = 1; level <= PortInvestmentCatalog.MaxLevel; level++)
            {
                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, level);
                string context = port + " / Lv." + level;
                Assert.That(cost.RequiredItem, Is.EqualTo(required), context);
                Assert.That(cost.Gold, Is.GreaterThan(previous.Gold), context + " gold");
                Assert.That(cost.Wood, Is.GreaterThan(previous.Wood), context + " wood");
                Assert.That(cost.Iron, Is.GreaterThan(previous.Iron), context + " iron");
                Assert.That(cost.RequiredQuantity, Is.GreaterThan(previous.RequiredQuantity), context + " local demand");
                previous = cost;
            }
        }

        [TestCaseSource(nameof(Ports))]
        public void SuggestedForeignSourceRemainsCheaperForEveryWholeBatchEvenOnAdverseMarketMonths(string port)
        {
            string source = PortInvestmentCatalog.SuggestedSourcePort(port);
            Assert.That(PortInvestmentCatalog.SupportedPorts, Does.Contain(source));
            Assert.That(source, Is.Not.EqualTo(port));
            PortMarketItem item = PortInvestmentCatalog.RequiredItemFor(port);
            string key = PortMarketCatalog.Key(item);
            decimal local = CorePortEconomy.LocalPriceMultiplier(port, key);
            decimal foreign = CorePortEconomy.LocalPriceMultiplier(source, key);
            Assert.That(foreign, Is.LessThan(local));
            foreach (string candidate in Ports)
                Assert.That(foreign, Is.LessThanOrEqualTo(CorePortEconomy.LocalPriceMultiplier(candidate, key)),
                    source + " should be a cheapest source for " + item);

            // Boundary quotes independently model the documented +/-5% monthly
            // market, including four-decimal unit prices and exact batch totals.
            decimal cheapestLocalUnit = decimal.Round(CorePortEconomy.BuyBasePrice(key) * local * .95m, 4);
            decimal dearestForeignUnit = decimal.Round(CorePortEconomy.BuyBasePrice(key) * foreign * 1.05m, 4);
            for (int level = 1; level <= PortInvestmentCatalog.MaxLevel; level++)
            {
                int quantity = PortInvestmentCatalog.CostForLevel(port, level).RequiredQuantity;
                decimal localBill = cheapestLocalUnit * quantity;
                decimal foreignBill = dearestForeignUnit * quantity;
                Assert.That(foreignBill, Is.LessThan(localBill), port + " / Lv." + level +
                    ": importing the demand material must save gold before voyage costs.");
                Assert.That(quantity, Is.LessThanOrEqualTo(PortMarketCatalog.MaxPurchaseQuota(item, 0)),
                    "The fourth material for one tier must be purchasable within an uninvested source port's monthly quota.");
            }
        }

        [TestCaseSource(nameof(Ports))]
        public void HighTiersExceedOneMerchantHoldButCanBeDeliveredInBatchesAndBuiltFromLocalStorage(string port)
        {
            CoreShipDefinition merchant = CoreVoyageSession.Ships.Single(ship => ship.Id == "merchant");
            for (int level = 4; level <= PortInvestmentCatalog.MaxLevel; level++)
            {
                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, level);
                double volume = cost.Wood * (double)PortMarketCatalog.CargoVolume(PortMarketItem.Wood) +
                    cost.Iron * (double)PortMarketCatalog.CargoVolume(PortMarketItem.Iron) +
                    cost.RequiredQuantity * (double)PortMarketCatalog.CargoVolume(cost.RequiredItem);
                Assert.That(volume, Is.GreaterThan(merchant.CargoCapacity),
                    port + " / Lv." + level + " should encourage repeated deliveries to local storage.");

                var session = new CoreVoyageSession();
                session.RestoreProfile(cost.Gold, port);
                // This is an arrival-boundary fixture, not a claim that the
                // source route was sailed. Every deposit still uses the Core API.
                foreach (PortMarketItem item in PortMarketCatalog.AllItems) SetCargo(session, item, 0);
                DeliverInBatches(session, PortMarketItem.Wood, cost.Wood);
                DeliverInBatches(session, PortMarketItem.Iron, cost.Iron);
                DeliverInBatches(session, cost.RequiredItem, cost.RequiredQuantity);
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                {
                    typeof(VoyageCargo).GetProperties().Single(property =>
                        property.Name.Equals(rare.Key, StringComparison.OrdinalIgnoreCase)).SetValue(session.Cargo, rare.Quantity);
                    Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue));
                    Assert.That(session.TryDepositPortCargo(rare.Key, rare.Quantity), Is.True);
                }
                Assert.That(session.Cargo.UsedVolume, Is.Zero);
                Assert.That(session.TrySpendPortInvestment(cost.Gold, cost.Wood, cost.Iron,
                    PortMarketCatalog.Key(cost.RequiredItem), cost.RequiredQuantity, cost.RareMaterials), Is.True);
                Assert.That(session.ProfileGold, Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(port, "wood"), Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(port, "iron"), Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(port, PortMarketCatalog.Key(cost.RequiredItem)), Is.Zero);
                foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                    Assert.That(session.PortWarehouse.GetAmount(port, rare.Key), Is.Zero);
            }
        }

        [TestCase("断锚港", "ancientmaterial", 4, 8, "relic")]
        [TestCase("白贝港", "gem", 1, 2, "ancientcompass")]
        [TestCase("铜钟港", "ancientmaterial", 4, 8, "blackpearl")]
        [TestCase("盐冠港", "gem", 1, 2, "relic")]
        [TestCase("暮潮港", "lantern", 1, 2, "blackpearl")]
        [TestCase("灰鸥港", "ancientcompass", 1, 1, "lantern")]
        public void RareDonationsBeginOnlyAtTierFourAndFollowEachPortsApprovedSpecialty(
            string port, string first, int tierFourQuantity, int tierFiveQuantity, string second)
        {
            for (int level = 0; level <= 3; level++)
                Assert.That(PortInvestmentCatalog.CostForLevel(port, level).RareMaterials, Is.Empty,
                    "Existing early progression must not acquire a treasure prerequisite.");
            PortInvestmentCost four = PortInvestmentCatalog.CostForLevel(port, 4);
            PortInvestmentCost five = PortInvestmentCatalog.CostForLevel(port, 5);
            Assert.That(four.RareMaterials.Select(item => item.Key), Is.EqualTo(new[] { first }));
            Assert.That(four.RareMaterials[0].Quantity, Is.EqualTo(tierFourQuantity));
            Assert.That(five.RareMaterials.Select(item => item.Key), Is.EqualTo(new[] { first, second }));
            Assert.That(five.RareMaterials[0].Quantity, Is.EqualTo(tierFiveQuantity));
            Assert.That(five.RareMaterials[1].Quantity, Is.EqualTo(1));
            foreach (CorePortInvestmentRequirement rare in five.RareMaterials)
                Assert.That(CoreWarehouseItemCatalog.Find(rare.Key)?.IsRare, Is.True);
            Assert.That(five.IsEmpty, Is.False);
        }

        private static void DeliverInBatches(CoreVoyageSession session, PortMarketItem item, int total)
        {
            string key = PortMarketCatalog.Key(item);
            for (int delivered = 0; delivered < total;)
            {
                int batch = Math.Min(10, total - delivered);
                SetCargo(session, item, batch);
                Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue),
                    "Each delivery must fit a real hold even when the whole building cannot.");
                Assert.That(session.TryDepositPortCargo(key, batch), Is.True);
                delivered += batch;
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, key), Is.EqualTo(delivered));
                Assert.That(session.GetMarketCargoAmount(key), Is.Zero);
            }
        }

        private static void SetCargo(CoreVoyageSession session, PortMarketItem item, int quantity) =>
            typeof(VoyageCargo).GetProperty(item.ToString()).SetValue(session.Cargo, quantity);
    }
}
