using System;
using System.Globalization;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreDecimalMoneyTests
    {
        [TestCase(PortMarketItem.Water, 1)]
        [TestCase(PortMarketItem.Water, 7)]
        [TestCase(PortMarketItem.Water, 99)]
        [TestCase(PortMarketItem.Food, 1)]
        [TestCase(PortMarketItem.Food, 7)]
        [TestCase(PortMarketItem.Food, 99)]
        public void DailyWaterAndFoodQuotesChargeTheActualQuantityWithoutWholeCoinRounding(PortMarketItem item, int quantity)
        {
            var date = new DateTime(2026, 10, 2);
            foreach (string port in CoreVoyageSession.DestinationPorts)
            {
                decimal buyUnit = PortMarketCatalog.BuyUnitPrice(port, item, date);
                decimal sellUnit = PortMarketCatalog.SellUnitPrice(port, item, date);
                Assert.That(PortMarketCatalog.CalculateBuyCost(port, item, date, quantity), Is.EqualTo(buyUnit * quantity));
                Assert.That(PortMarketCatalog.CalculateSellRevenue(port, item, date, quantity), Is.EqualTo(sellUnit * quantity));
                Assert.That(PortMarketCatalog.CalculateSellRevenue(port, item, date, 1), Is.GreaterThan(0m));
            }
            Assert.That(PortMarketCatalog.FormatPrice(0.0391m), Is.EqualTo("0.0391"));
            Assert.That(PortMarketCatalog.FormatPrice(12.3400m), Is.EqualTo("12.34"));
        }

        [Test]
        public void SplittingSmallPurchasesAndSalesDoesNotChangeTheAccountTotal()
        {
            var single = new CoreVoyageSession();
            var batch = new CoreVoyageSession();
            single.RestoreProfile(1m, single.CurrentPort);
            batch.RestoreProfile(1m, batch.CurrentPort);
            int water = single.Cargo.Water;
            for (int i = 0; i < 7; i++)
                Assert.That(single.TryPurchaseMarketCargo("water", 1, 0.0391m, 0.07f), Is.True);
            Assert.That(batch.TryPurchaseMarketCargo("water", 7, 0.2737m, 0.07f), Is.True);
            Assert.That(single.ProfileGold, Is.EqualTo(0.7263m));
            Assert.That(single.ProfileGold, Is.EqualTo(batch.ProfileGold));
            for (int i = 0; i < 7; i++)
                Assert.That(single.TrySellMarketCargo("water", 1, 0.0313m), Is.True);
            Assert.That(batch.TrySellMarketCargo("water", 7, 0.2191m), Is.True);
            Assert.That(single.Cargo.Water, Is.EqualTo(water));
            Assert.That(single.ProfileGold, Is.EqualTo(0.9454m));
            Assert.That(batch.ProfileGold, Is.EqualTo(single.ProfileGold));
        }

        [Test]
        public void SupplyQuotesAndCombinedOrdersKeepFractionalCostsAtomic()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(0.5999m, session.CurrentPort);
            int water = session.Cargo.Water;
            int food = session.Cargo.Food;
            var order = new VoyageSupplyOrder { Water = 3, Food = 4 };
            Assert.That(session.GetSupplyUnitPrice(VoyageSupplyType.Water), Is.EqualTo(0.04m));
            Assert.That(session.CalculateSupplyPurchaseCost(VoyageSupplyType.Water, 3), Is.EqualTo(0.12m));
            Assert.That(session.QuoteSupplyPurchase(order).Cost, Is.EqualTo(0.60m));
            Assert.That(session.TryPurchaseSupplies(order), Is.False);
            Assert.That(session.TryPurchaseMarketSupplyOrder(order, 0.60m), Is.False);
            Assert.That(session.TryPurchaseMarketProvisions(3, 4, 0.60m), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(0.5999m));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
            session.RestoreProfile(0.6001m, session.CurrentPort);
            Assert.That(session.TryPurchaseMarketSupplyOrder(order, 0.60m), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(0.0001m));
            Assert.That(session.Cargo.Water, Is.EqualTo(water + 3));
            Assert.That(session.Cargo.Food, Is.EqualTo(food + 4));
        }

        [TestCase("0.00001")]
        [TestCase("-0.0001")]
        [TestCase("2147483647.0001")]
        [TestCase("79228162514264337593543950335")]
        public void InvalidTransactionAmountsNeverDebitEitherCargoSource(string text)
        {
            decimal amount = decimal.Parse(text, CultureInfo.InvariantCulture);
            var session = new CoreVoyageSession();
            session.PortWarehouse.Restore("v1;断锚港,water,4");
            int water = session.Cargo.Water;
            int food = session.Cargo.Food;
            decimal gold = session.ProfileGold;
            Assert.That(session.TrySpendProfileGold(amount, "invalid"), Is.False);
            Assert.That(session.TryPurchaseMarketCargo("water", 1, amount, 0.07f), Is.False);
            Assert.That(session.TryPurchasePortWarehouseCargo("water", 1, amount), Is.False);
            Assert.That(session.TryPurchaseMarketProvisions(1, 1, amount), Is.False);
            Assert.That(session.TryPurchaseMarketSupplyOrder(new VoyageSupplyOrder { Water = 1 }, amount), Is.False);
            Assert.That(session.TrySellMarketCargo("water", 1, amount), Is.False);
            Assert.That(session.TrySellPortWarehouseCargo("water", 1, 1, amount), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.EqualTo(4));
        }

        [Test]
        public void MixedWarehouseSaleCreditsTheExactCombinedQuoteOnce()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = 1;
            session.PortWarehouse.Restore("v1;断锚港,water,2");
            session.RestoreProfile(0.0001m, session.CurrentPort);
            Assert.That(session.TrySellPortWarehouseCargo("water", 2, 1, 0.0939m), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(0.0940m));
            Assert.That(session.Cargo.Water, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.Zero);
            Assert.That(session.TrySellPortWarehouseCargo("water", 2, 1, 0.0939m), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(0.0940m));
        }

        [Test]
        public void AccountHeadroomChecksTheFractionalPartBeforeMovingAnyCargo()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 0.0001m, session.CurrentPort);
            int water = session.Cargo.Water;
            session.PortWarehouse.Restore("v1;断锚港,water,2");
            Assert.That(session.TrySellPortWarehouseCargo("water", 1, 1, 0.0002m), Is.False);
            Assert.That(session.TrySellMarketCargo("water", 1, 0.0002m), Is.False);
            Assert.That(session.TryReceivePortRewards(1, 0, 0, 0, "reward", out _), Is.False);
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.EqualTo(2));
            Assert.That(session.TrySellMarketCargo("water", 1, 0.0001m), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
        }

        [TestCase("0.9999", 0)]
        [TestCase("12.3456", 12)]
        [TestCase("500.4321", 60)]
        public void IntegerVoyageCashAndSettlementPreserveTheAccountFraction(string text, int cash)
        {
            decimal original = decimal.Parse(text, CultureInfo.InvariantCulture);
            var session = new CoreVoyageSession();
            session.RestoreProfile(original, session.CurrentPort);
            session.Cargo.Gold = 0;
            session.StartVoyage();
            Assert.That(session.Cargo.Gold, Is.EqualTo(cash));
            Assert.That(session.ProfileGold, Is.EqualTo(original - cash));
            session.Cargo.Gold += 20;
            Assert.That(session.SafeReturn(), Is.True);
            decimal expected = original + session.Settlement.BankedGold;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.ProfileGold, Is.EqualTo(expected));
            Assert.That(session.Cargo.Gold, Is.Zero);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.ProfileGold, Is.EqualTo(expected));
            var restored = new CoreVoyageSession();
            restored.RestoreProfile(session.ProfileGold, session.CurrentPort);
            Assert.That(restored.ProfileGold, Is.EqualTo(expected));
        }

        [Test]
        public void IntegerRewardsAndDepositsCannotRoundFractionalHeadroomUp()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold - 1.0001m, session.CurrentPort);
            session.Cargo.Gold = 0;
            session.GrantProfileGold(3, "reward");
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold - 0.0001m));
            Assert.That(session.Cargo.Gold, Is.EqualTo(2));
            Assert.That(session.TryDepositTravelCash(), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(2));
            Assert.That(session.TrySpendProfileGold(0.9999m, "purchase"), Is.True);
            Assert.That(session.TryDepositTravelCash(), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(1));
        }

        [Test]
        public void LegacyBalancesAreRetainedAndMalformedRestoreValuesAreBounded()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(int.MaxValue, session.CurrentPort);
            Assert.That(session.ProfileGold, Is.EqualTo((decimal)int.MaxValue));
            session.RestoreProfile(decimal.MaxValue, session.CurrentPort);
            Assert.That(session.ProfileGold, Is.EqualTo((decimal)int.MaxValue));
            session.RestoreProfile(1.23459m, session.CurrentPort);
            Assert.That(session.ProfileGold, Is.EqualTo(1.2345m));
            session.RestoreProfile(decimal.MinValue, session.CurrentPort);
            Assert.That(session.ProfileGold, Is.Zero);
        }

        [TestCase("account")]
        [TestCase("market")]
        [TestCase("warehouse")]
        [TestCase("provisions")]
        [TestCase("supplyOrder")]
        [TestCase("investment")]
        public void EveryPaymentCanSpendLegacyAssetsWithoutLosingTheFraction(string channel)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(int.MaxValue, session.CurrentPort);
            session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Medicine = 1;
            decimal payment = int.MaxValue - 0.0001m;
            bool success;
            switch (channel)
            {
                case "account": success = session.TrySpendProfileGold(payment, "legacy"); break;
                case "market": success = session.TryPurchaseMarketCargo("water", 1, payment, 0.07f); break;
                case "warehouse": success = session.TryPurchasePortWarehouseCargo("water", 1, payment); break;
                case "provisions": success = session.TryPurchaseMarketProvisions(1, 1, payment); break;
                case "supplyOrder": success = session.TryPurchaseMarketSupplyOrder(new VoyageSupplyOrder { Water = 1 }, payment); break;
                default: success = session.TrySpendPortInvestment(payment, 1, 1, "medicine", 1); break;
            }
            Assert.That(success, Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(0.0001m));
        }

        [Test]
        public void LegacyPaymentAllowanceDoesNotRaiseTheNewIncomeOrGmLimit()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(0m, session.CurrentPort);
            session.PortWarehouse.Restore("v1;断锚港,water,2");
            int water = session.Cargo.Water;
            decimal overNewLimit = CoreEconomyBalance.MaximumProfileGold + 0.0001m;
            Assert.That(CoreEconomyBalance.IsValidGoldPayment(overNewLimit), Is.True);
            Assert.That(session.TrySellMarketCargo("water", 1, overNewLimit), Is.False);
            Assert.That(session.TrySellPortWarehouseCargo("water", 1, 1, overNewLimit), Is.False);
            Assert.That(session.TryGameMasterSetProfileGold(overNewLimit), Is.False);
            Assert.That(session.ProfileGold, Is.Zero);
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.EqualTo(2));
            Assert.That(session.TrySellMarketCargo("water", 1, CoreEconomyBalance.MaximumProfileGold), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
        }

        [TestCase("0.0001")]
        [TestCase("12.3456")]
        [TestCase("0012.34000")]
        [TestCase("1000000000.0000")]
        public void ExplicitGmAmountsAcceptExactFourPlaceValues(string input)
        {
            Assert.That(CoreGameMasterTools.TryParseProfileGold(input, out decimal gold), Is.True);
            var session = new CoreVoyageSession();
            Assert.That(session.TryGameMasterSetProfileGold(gold), Is.True);
            Assert.That(session.ProfileGold, Is.EqualTo(decimal.Parse(input, CultureInfo.InvariantCulture)));
        }

        [TestCase("0.00001")]
        [TestCase("0.000000000000000000000000000001")]
        [TestCase("1.00001")]
        [TestCase("1.2.3")]
        [TestCase(".5")]
        [TestCase("1.")]
        [TestCase("NaN")]
        public void ExplicitGmAmountsCannotSilentlyRoundOrCoerceMalformedInput(string input)
        {
            Assert.That(CoreGameMasterTools.TryParseProfileGold(input, out decimal gold), Is.False);
            Assert.That(gold, Is.Zero);
        }
    }
}
