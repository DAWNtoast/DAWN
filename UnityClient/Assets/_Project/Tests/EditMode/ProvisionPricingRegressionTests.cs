using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class ProvisionPricingRegressionTests
    {
        private static readonly string[] Ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };

        [TestCase(PortMarketItem.Water, 4)]
        [TestCase(PortMarketItem.Food, 12)]
        public void WaterAndFoodUseReducedSharedBasePrices(PortMarketItem item, int hundredUnitPrice)
        {
            decimal expected = hundredUnitPrice / 100m;
            Assert.That(CorePortEconomy.BuyBasePrice(PortMarketCatalog.Key(item)), Is.EqualTo(expected));
            Assert.That(PortMarketCatalog.BaseBuyPrice(item), Is.EqualTo(expected));
            Assert.That(PortMarketCatalog.BaseSellPrice(item), Is.EqualTo(expected * .8m));
        }

        [TestCase(PortMarketItem.Water)]
        [TestCase(PortMarketItem.Food)]
        [TestCase(PortMarketItem.Beer)]
        [TestCase(PortMarketItem.Medicine)]
        [TestCase(PortMarketItem.Wood)]
        [TestCase(PortMarketItem.Iron)]
        [TestCase(PortMarketItem.Cloth)]
        [TestCase(PortMarketItem.Powder)]
        [TestCase(PortMarketItem.Cannonballs)]
        public void SamePort_CheapestPurchaseAndBestLaterResaleNeverMintGold(PortMarketItem item)
        {
            foreach (string port in Ports)
            {
                var session = new CoreVoyageSession();
                session.RestoreProfile(500, port);
                int batchSize = PortMarketCatalog.SaleStep(item);
                bool hasMaintenanceSupply = Enum.TryParse(item.ToString(), out VoyageSupplyType supply);
                foreach (int quantity in new[] { 1, batchSize, batchSize * 10 })
                {
                    decimal lowestBuy = decimal.MaxValue, highestSale = 0m, highestBatchSale = 0m;
                    for (int days = 0; days < 365; days++)
                    {
                        DateTime date = new DateTime(2026, 1, 1).AddDays(days);
                        lowestBuy = Math.Min(lowestBuy, PortMarketCatalog.CalculateBuyCost(port, item, date, quantity));
                        highestSale = Math.Max(highestSale, PortMarketCatalog.CalculateSellRevenue(port, item, date, quantity));
                        decimal batchSale = quantity / batchSize * PortMarketCatalog.CalculateSellRevenue(port, item, date, batchSize) +
                            PortMarketCatalog.CalculateSellRevenue(port, item, date, quantity % batchSize);
                        highestBatchSale = Math.Max(highestBatchSale, batchSale);
                        PortMarketDayState offer = PortMarketCatalog.DayState(port, item, date);
                        Assert.That(offer.BuyMultiplier, Is.InRange(.95m, 1.05m));
                        Assert.That(offer.SellMultiplier, Is.InRange(.95m, 1.05m));
                    }
                    string context = port + " / " + item + " x" + quantity;
                    Assert.That(highestSale, Is.LessThanOrEqualTo(lowestBuy), context + " / cross-day market quotes");
                    Assert.That(highestBatchSale, Is.LessThanOrEqualTo(lowestBuy), context + " / cross-day batch resale");
                    if (!hasMaintenanceSupply) continue;
                    decimal maintenanceCost = session.CalculateSupplyPurchaseCost(supply, quantity);
                    Assert.That(highestSale, Is.LessThanOrEqualTo(maintenanceCost), context + " / maintenance");
                    Assert.That(highestBatchSale, Is.LessThanOrEqualTo(maintenanceCost), context + " / maintenance batch resale");
                }
            }
        }

        [TestCase(PortMarketItem.Powder, 5)]
        [TestCase(PortMarketItem.Cannonballs, 8)]
        public void DefensiveAmmunitionHasAnAffordableSharedBasePrice(PortMarketItem item, int price)
        {
            Assert.That(CorePortEconomy.BuyBasePrice(PortMarketCatalog.Key(item)), Is.EqualTo((decimal)price));
            Assert.That(PortMarketCatalog.BaseBuyPrice(item), Is.EqualTo((decimal)price));
        }

        [TestCase("断锚港", "白贝港", PortMarketItem.Wood, 40)]
        [TestCase("暮潮港", "断锚港", PortMarketItem.Medicine, 30)]
        [TestCase("灰鸥港", "盐冠港", PortMarketItem.Cloth, 40)]
        [TestCase("铜钟港", "灰鸥港", PortMarketItem.Iron, 30)]
        public void TradeRoute_WorstQuotesPayForEightDaysRationsAndDefensiveAmmunition(
            string origin, string destination, PortMarketItem freight, int quantity)
        {
            var session = new CoreVoyageSession();
            // This freight benchmark uses the early merchant's real crew and hold,
            // rather than assuming every starting sloop can carry the same trade lot.
            Assert.That(session.SelectShip(1), Is.True);
            Assert.That(session.SelectedShip.Id, Is.EqualTo("merchant"));
            session.RestoreProfile(500, origin);
            session.ConfigureDepartureCrew(session.CrewCountValue - 1, 1);
            session.ConfigureCrewCapabilities(false, false, false, false, true, false);
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(session.CrewCountValue));
            int rations = (int)Math.Ceiling(session.EstimateDailyRationConsumption(session.EffectiveCrewCount, session.HasCook) * 8d);
            int expectedRations = (int)Math.Ceiling(session.SelectedShip.CrewCount * GameplayBalance.CookRationMultiplier * 8d);
            Assert.That(rations, Is.EqualTo(expectedRations), "An eight-day voyage must budget every crew berth, including the cook, at the real cook saving.");
            foreach (var property in typeof(VoyageCargo).GetProperties().Where(property => property.PropertyType == typeof(int)))
                property.SetValue(session.Cargo, 0);

            decimal initialGold = session.ProfileGold;
            decimal freightCost = WorstBuyCost(origin, freight, quantity);
            decimal provisionCost = WorstBuyCost(origin, PortMarketItem.Water, rations) + WorstBuyCost(origin, PortMarketItem.Food, rations);
            decimal cannonballCost = WorstBuyCost(origin, PortMarketItem.Cannonballs, 6);
            decimal powderCost = WorstBuyCost(origin, PortMarketItem.Powder, 3);
            int saleBatch = PortMarketCatalog.SaleStep(freight);
            decimal minimumRevenue = quantity / saleBatch * WorstSaleRevenue(destination, freight, saleBatch);

            Assert.That(quantity, Is.LessThanOrEqualTo(PortMarketCatalog.MaxPurchaseQuota(freight, 0)), "The trade must work without port investments.");
            Assert.That(minimumRevenue, Is.GreaterThan(freightCost), "A regional producer-to-consumer route needs a positive gross margin.");
            Assert.That(session.TryPurchaseMarketProvisions(rations, rations, provisionCost), Is.True);
            Assert.That(session.TryPurchaseMarketCargo("cannonballs", 6, cannonballCost, PortMarketCatalog.CargoVolume(PortMarketItem.Cannonballs)), Is.True);
            Assert.That(session.TryPurchaseMarketCargo("powder", 3, powderCost, PortMarketCatalog.CargoVolume(PortMarketItem.Powder)), Is.True);
            Assert.That(session.TryPurchaseMarketCargo(PortMarketCatalog.Key(freight), quantity, freightCost, PortMarketCatalog.CargoVolume(freight)), Is.True);
            Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue), "Trade goods and the complete voyage load must fit together.");
            Assert.That(session.ProfileGold, Is.GreaterThanOrEqualTo(0), "The 500-gold starting account must fund the entire load.");
            decimal expenditure = initialGold - session.ProfileGold;
            Assert.That(minimumRevenue - expenditure, Is.GreaterThanOrEqualTo(10),
                origin + " → " + destination + ": freight " + freightCost + ", provisions " + provisionCost +
                ", ammunition " + (cannonballCost + powderCost) + ", sales " + minimumRevenue +
                ". This excludes battle rewards and spends the full eight-day ration and ammunition budget.");
            // Voyage navigation is covered by PlayMode. Here use the destination
            // account and real batch-sale transaction to verify the budget can bank.
            session.RestoreProfile(session.ProfileGold, destination);
            for (int sold = 0; sold < quantity; sold += saleBatch)
                Assert.That(session.TrySellMarketCargo(PortMarketCatalog.Key(freight), saleBatch,
                    WorstSaleRevenue(destination, freight, saleBatch)), Is.True);
            Assert.That(session.GetMarketCargoAmount(PortMarketCatalog.Key(freight)), Is.Zero);
            Assert.That(session.ProfileGold, Is.EqualTo(initialGold + minimumRevenue - expenditure));
        }

        [TestCase(1, "gold_convoy", 45, 12)]
        [TestCase(0, "event_reef_fisher_cache", 6, 2)]
        [TestCase(0, "common_floating_market", 24, 7)]
        public void EmergencyFoodOffersChargeTheAuthoredModeratePremium(int route, string encounterId, int food, int price)
        {
            var session = new CoreVoyageSession();
            session.SelectRoute(route);
            session.StartVoyage();
            CoreEncounter encounter = session.Map.Nodes.Single(node => node.Encounter.Id == encounterId).Encounter;
            CoreEncounterChoice purchase = encounter.Choices.Single(choice => choice.Effect.Food > 0 && choice.Effect.Gold < 0);
            Assert.That(purchase.Effect.Food, Is.EqualTo(food));
            Assert.That(purchase.Effect.Gold, Is.EqualTo(-price));
            Assert.That(purchase.Detail, Does.Contain("金币 -" + price).And.Contain("食物 +" + food));
            Assert.That(price / (food * CorePortEconomy.BuyBasePrice("food")), Is.InRange(2.2m, 2.8m),
                "At-sea emergency supplies may cost more, but should remain within the intended premium.");
        }

        // Model the least favorable authored daily quotes at the shared unit precision
        // and the real sale-button batch size, without depending on a lucky calendar date.
        private static decimal WorstBuyCost(string port, PortMarketItem item, int quantity) =>
            decimal.Round(CorePortEconomy.BuyBasePrice(PortMarketCatalog.Key(item)) *
                CorePortEconomy.LocalPriceMultiplier(port, PortMarketCatalog.Key(item)) * 1.05m, 4) * quantity;

        private static decimal WorstSaleRevenue(string port, PortMarketItem item, int quantity) =>
            decimal.Round(CorePortEconomy.BuyBasePrice(PortMarketCatalog.Key(item)) *
                CorePortEconomy.LocalPriceMultiplier(port, PortMarketCatalog.Key(item)) * .8m * .95m, 4) * quantity;

        [TestCase(PortMarketItem.Water)]
        [TestCase(PortMarketItem.Food)]
        public void SaleRevenue_PreservesTheActualFractionalTotalWithoutMinimumGold(PortMarketItem item)
        {
            var date = new DateTime(2026, 10, 1);
            foreach (string port in Ports)
            foreach (int quantity in new[] { 1, 2, 5, 10, 99, 100, 999, int.MaxValue })
            {
                decimal total = PortMarketCatalog.SellUnitPrice(port, item, date) * quantity;
                decimal expected = total;
                Assert.That(PortMarketCatalog.CalculateSellRevenue(port, item, date, quantity), Is.EqualTo(expected),
                    port + " / " + item + " x" + quantity);
            }
        }

        [TestCase(PortMarketItem.Water)]
        [TestCase(PortMarketItem.Food)]
        public void SplittingOneHundredSupplyUnitsIntoSingleSalesCannotIncreaseRevenue(PortMarketItem item)
        {
            var date = new DateTime(2026, 10, 1);
            foreach (string port in Ports)
            {
                decimal singleSale = PortMarketCatalog.CalculateSellRevenue(port, item, date, 1);
                decimal bulkSale = PortMarketCatalog.CalculateSellRevenue(port, item, date, 100);
                Assert.That(singleSale * 100, Is.EqualTo(bulkSale), port + " / " + item);
                Assert.That(singleSale * 100,
                    Is.LessThanOrEqualTo(PortMarketCatalog.CalculateBuyCost(port, item, date, 100)));
            }
        }

        [TestCase(PortMarketItem.Water)]
        [TestCase(PortMarketItem.Food)]
        public void RegionalPriceDifferencesStillAllowProfitableTransport(PortMarketItem item)
        {
            var date = new DateTime(2026, 10, 1);
            bool profitableRoute = false;
            foreach (string origin in Ports)
            foreach (string destination in Ports)
            {
                if (origin == destination) continue;
                decimal buy = PortMarketCatalog.CalculateBuyCost(origin, item, date, 1000);
                decimal sell = 10 * PortMarketCatalog.CalculateSellRevenue(destination, item, date, 100);
                profitableRoute |= sell > buy;
            }
            Assert.That(profitableRoute, Is.True, "Same-port safeguards must preserve useful cross-port supply trade.");
        }

        [TestCase(PortMarketItem.Wood, "wood", 2)]
        [TestCase(PortMarketItem.Iron, "iron", 6)]
        [TestCase(PortMarketItem.Cloth, "cloth", 4)]
        public void LegacyMaterialBuybackCannotProfitFromEvenTheLowestLocalDailyPurchase(
            PortMarketItem item, string key, int legacySellPrice)
        {
            foreach (string port in Ports)
            {
                decimal lowestMarketBuy = CorePortEconomy.BuyBasePrice(key) *
                    CorePortEconomy.LocalPriceMultiplier(port, key) * .95m;
                Assert.That(lowestMarketBuy, Is.GreaterThan(legacySellPrice), port + " / " + item);
            }
        }

        [TestCase(PortMarketItem.Water)]
        [TestCase(PortMarketItem.Food)]
        public void PurchaseQuotes_PreserveFractionalTotalsAndDoNotOverflow(PortMarketItem item)
        {
            var date = new DateTime(2026, 10, 1);
            foreach (string port in Ports)
            foreach (int quantity in new[] { 1, 10, 100, 500, 1000, int.MaxValue })
            {
                decimal total = PortMarketCatalog.BuyUnitPrice(port, item, date) * quantity;
                decimal expected = total;
                Assert.That(PortMarketCatalog.CalculateBuyCost(port, item, date, quantity), Is.EqualTo(expected),
                    port + " / " + item + " x" + quantity);
            }
        }

        [TestCase(PortMarketItem.Water)]
        [TestCase(PortMarketItem.Food)]
        public void NonPositiveQuantitiesHaveNoPurchaseCostOrSaleRevenue(PortMarketItem item)
        {
            var date = new DateTime(2026, 10, 1);
            foreach (int quantity in new[] { int.MinValue, -1, 0 })
            {
                Assert.That(PortMarketCatalog.CalculateBuyCost("白贝港", item, date, quantity), Is.Zero);
                Assert.That(PortMarketCatalog.CalculateSellRevenue("白贝港", item, date, quantity), Is.Zero);
            }
        }

        [TestCase("water")]
        [TestCase("food")]
        public void ZeroValueSaleCannotSilentlyDiscardSupplies(string material)
        {
            var session = new CoreVoyageSession();
            int before = session.GetMarketCargoAmount(material);
            decimal gold = session.ProfileGold;
            Assert.That(session.TrySellMarketCargo(material, 1, 0), Is.False);
            Assert.That(session.GetMarketCargoAmount(material), Is.EqualTo(before));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }
    }
}
