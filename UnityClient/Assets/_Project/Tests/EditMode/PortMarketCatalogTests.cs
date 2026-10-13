using System;
using NUnit.Framework;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class PortMarketCatalogTests
    {
        private static readonly DateTime TestDay = new DateTime(2026, 9, 23);

        [Test]
        public void Catalog_LastSupportedMonthWrapsRestockLabelToFirstYear()
        {
            Assert.That(PortMarketCatalog.NextRefreshLabel(DateTime.MaxValue), Is.EqualTo("1-01-01"));
            Assert.That(PortComponentShopCatalog.NextRefreshLabel(DateTime.MaxValue), Is.EqualTo("1-01-01"));
        }

        [TestCase(1, "1-09")]
        [TestCase(77, "77-09")]
        [TestCase(999, "999-09")]
        [TestCase(2026, "2026-09")]
        [TestCase(9999, "9999-09")]
        public void Catalog_MonthLabelShowsTheFullYearWithoutLeadingZeros(int year, string expected)
        {
            Assert.That(PortMarketCatalog.MonthLabel(new DateTime(year, 9, 23)), Is.EqualTo(expected));
        }

        [Test]
        public void Catalog_ContainsTradeableCargoAndDeterministicMonthlyState()
        {
            Assert.That(PortMarketCatalog.AllItems.Count, Is.EqualTo(9));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                PortMarketDayState first = PortMarketCatalog.DayState("白贝港", item, TestDay);
                PortMarketDayState second = PortMarketCatalog.DayState("白贝港", item, TestDay);
                Assert.That(second.BuyMultiplier, Is.EqualTo(first.BuyMultiplier));
                Assert.That(second.SellMultiplier, Is.EqualTo(first.SellMultiplier));
                Assert.That(second.Stock, Is.EqualTo(first.Stock));
                Assert.That(first.Stock, Is.GreaterThan(0));
                Assert.That(PortMarketCatalog.CalculateBuyCost("白贝港", item, TestDay,
                    PortMarketCatalog.PurchaseStep(item)), Is.GreaterThan(0));
                Assert.That(PortMarketCatalog.CalculateSellRevenue("白贝港", item, TestDay,
                    PortMarketCatalog.SaleStep(item)), Is.GreaterThan(0));
            }
        }

        [Test]
        public void Catalog_StockAndPricesStayFixedUntilNextGameMonth()
        {
            DateTime morning = new DateTime(2026, 9, 1);
            DateTime evening = morning.AddMonths(1).AddTicks(-1);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                PortMarketDayState first = PortMarketCatalog.DayState("白贝港", item, morning);
                PortMarketDayState later = PortMarketCatalog.DayState("白贝港", item, evening);
                Assert.That(later.Stock, Is.EqualTo(first.Stock), item.ToString());
                Assert.That(later.BuyMultiplier, Is.EqualTo(first.BuyMultiplier));
                Assert.That(later.SellMultiplier, Is.EqualTo(first.SellMultiplier));
                Assert.That(PortMarketCatalog.CalculateBuyCost("白贝港", item, evening, 100),
                    Is.EqualTo(PortMarketCatalog.CalculateBuyCost("白贝港", item, morning, 100)));
            }
        }

        [TestCase(2026, 9, 23, "2026-09-23", "2026-09-24")]
        [TestCase(2026, 9, 30, "2026-09-30", "2026-10-01")]
        [TestCase(2026, 12, 31, "2026-12-31", "2027-01-01")]
        [TestCase(2028, 2, 28, "2028-02-28", "2028-02-29")]
        public void Catalog_DayKeysChangeAtEveryCalendarBoundary(int year, int month, int day,
            string expectedToday, string expectedTomorrow)
        {
            DateTime beforeMidnight = new DateTime(year, month, day, 23, 59, 59);
            Assert.That(PortMarketCatalog.DayKey(beforeMidnight), Is.EqualTo(expectedToday));
            Assert.That(PortMarketCatalog.DayKey(beforeMidnight.AddSeconds(1)), Is.EqualTo(expectedTomorrow));
        }

        [Test]
        public void Catalog_NextMonthAndOtherPortHaveIndependentMonthlyOffers()
        {
            PortMarketDayState today = PortMarketCatalog.DayState("白贝港", PortMarketItem.Water, TestDay);
            PortMarketDayState tomorrow = PortMarketCatalog.DayState("白贝港", PortMarketItem.Water, TestDay.AddMonths(1));
            PortMarketDayState otherPort = PortMarketCatalog.DayState("铜钟港", PortMarketItem.Water, TestDay);
            Assert.That(tomorrow.BuyMultiplier, Is.Not.EqualTo(today.BuyMultiplier));
            Assert.That(otherPort.BuyMultiplier, Is.Not.EqualTo(today.BuyMultiplier));
            Assert.That(PortMarketCatalog.DayState(" 白贝港 ", PortMarketItem.Water, TestDay).Stock,
                Is.EqualTo(today.Stock));
        }

        [Test]
        public void Catalog_MonthlyPurchaseKeysKeepDailyPurchasesSpentAndSeparateMonthsPortsAndItems()
        {
            string today = CoreGameplayDemoController.MarketPurchaseKey(" 白贝港 ", TestDay, PortMarketItem.Water);
            Assert.That(today, Does.StartWith("ShatteredSail.PortMarketPurchase.v3."));
            Assert.That(today, Does.EndWith(".白贝港.2026-09.water"));
            Assert.That(CoreGameplayDemoController.MarketPurchaseKey("白贝港", TestDay.AddHours(23), PortMarketItem.Water),
                Is.EqualTo(today));
            Assert.That(CoreGameplayDemoController.MarketPurchaseKey("白贝港", TestDay.AddDays(1), PortMarketItem.Water),
                Is.EqualTo(today));
            Assert.That(CoreGameplayDemoController.MarketPurchaseKey("白贝港", TestDay.AddMonths(1), PortMarketItem.Water),
                Is.Not.EqualTo(today));
            Assert.That(CoreGameplayDemoController.MarketPurchaseKey("铜钟港", TestDay, PortMarketItem.Water),
                Is.Not.EqualTo(today));
            Assert.That(CoreGameplayDemoController.MarketPurchaseKey("白贝港", TestDay, PortMarketItem.Food),
                Is.Not.EqualTo(today));
        }

        [Test]
        public void Catalog_PortSpecialtiesCreateDistinctTradeRoutes()
        {
            Assert.That(PortMarketCatalog.PortPurchaseMultiplier("白贝港", PortMarketItem.Water), Is.LessThan(1f));
            Assert.That(PortMarketCatalog.PortPurchaseMultiplier("铜钟港", PortMarketItem.Wood), Is.LessThan(1f));
            Assert.That(PortMarketCatalog.PortPurchaseMultiplier("铜钟港", PortMarketItem.Iron), Is.LessThan(1f));
            Assert.That(PortMarketCatalog.PortSaleMultiplier("暮潮港", PortMarketItem.Iron), Is.GreaterThan(1f));
            Assert.That(PortMarketCatalog.PortSaleMultiplier("盐冠港", PortMarketItem.Cloth), Is.GreaterThan(1f));
            Assert.That(PortMarketCatalog.PortPurchaseMultiplier("灰鸥港", PortMarketItem.Medicine), Is.LessThan(1f));
            Assert.That(PortMarketCatalog.PortSaleMultiplier("灰鸥港", PortMarketItem.Medicine),
                Is.EqualTo(PortMarketCatalog.PortPurchaseMultiplier("灰鸥港", PortMarketItem.Medicine)),
                "The local abundance factor applies to both sides of the quote; profit requires another port.");
            Assert.That(PortMarketCatalog.PortPurchaseMultiplier("灰鸥港", PortMarketItem.Medicine), Is.Not.EqualTo(PortMarketCatalog.PortPurchaseMultiplier("白贝港", PortMarketItem.Medicine)));
            Assert.That(PortMarketCatalog.CanonicalPortName("灰鸥港"), Is.EqualTo("灰鸥港"));
        }

        [Test]
        public void Catalog_InvestmentRaisesEveryPurchaseQuota()
        {
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                int baseQuota = PortMarketCatalog.MaxPurchaseQuota(item, 0);
                int maxQuota = PortMarketCatalog.MaxPurchaseQuota(item, PortInvestmentCatalog.MaxLevel);
                Assert.That(maxQuota, Is.GreaterThan(baseQuota), item.ToString());
                Assert.That(PortMarketCatalog.PurchaseStep(item), Is.LessThanOrEqualTo(baseQuota));
            }
        }

        [TestCase(1, 1, 1, "0001-01", "1-02-01")]
        [TestCase(77, 9, 1, "0077-09", "77-10-01")]
        [TestCase(999, 12, 31, "0999-12", "1000-01-01")]
        [TestCase(2026, 9, 1, "2026-09", "2026-10-01")]
        [TestCase(2026, 9, 30, "2026-09", "2026-10-01")]
        [TestCase(2026, 12, 31, "2026-12", "2027-01-01")]
        [TestCase(2028, 2, 29, "2028-02", "2028-03-01")]
        [TestCase(9999, 11, 30, "9999-11", "9999-12-01")]
        [TestCase(9999, 12, 1, "9999-12", "1-01-01")]
        [TestCase(9999, 12, 31, "9999-12", "1-01-01")]
        public void Catalog_MonthKeysAndNextRefreshHandleCalendarBoundaries(int year, int month, int day,
            string period, string nextRefresh)
        {
            DateTime date = new DateTime(year, month, day);
            Assert.That(PortMarketCatalog.MonthKey(date), Is.EqualTo(period));
            Assert.That(PortMarketCatalog.NextRefreshLabel(date), Is.EqualTo(nextRefresh));
            Assert.That(PortComponentShopCatalog.NextRefreshLabel(date), Is.EqualTo(nextRefresh));
        }

        [Test]
        public void Catalog_InvestmentAddsRealStockWithoutRerollingOrRestoringSpentPurchases()
        {
            foreach (string port in PortInvestmentCatalog.SupportedPorts)
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                PortMarketDayState original = PortMarketCatalog.DayState(port, item, TestDay);
                int purchased = original.Stock;
                Assert.That(purchased, Is.InRange(PortMarketCatalog.MaxPurchaseQuota(item, 0) * 9 / 10,
                    PortMarketCatalog.MaxPurchaseQuota(item, 0)));
                for (int level = 1; level <= PortInvestmentCatalog.MaxLevel; level++)
                {
                    PortMarketDayState upgraded = PortMarketCatalog.DayState(port, item, TestDay.AddDays(1), level);
                    int expectedAdded = PortMarketCatalog.MaxPurchaseQuota(item, level) -
                        PortMarketCatalog.MaxPurchaseQuota(item, 0);
                    Assert.That(upgraded.Stock - purchased, Is.EqualTo(expectedAdded), port + "/" + item + "/" + level);
                    Assert.That(upgraded.Stock - original.Stock, Is.EqualTo(expectedAdded));
                    Assert.That(upgraded.BuyMultiplier, Is.EqualTo(original.BuyMultiplier));
                    Assert.That(upgraded.SellMultiplier, Is.EqualTo(original.SellMultiplier));
                    Assert.That(upgraded.Stock, Is.LessThanOrEqualTo(PortMarketCatalog.MaxPurchaseQuota(item, level)));
                }
                Assert.That(PortMarketCatalog.MaxPurchaseQuota(item, 5),
                    Is.EqualTo(PortMarketCatalog.MaxPurchaseQuota(item, 0) * 6));
            }
        }

        [TestCase(PortMarketItem.Water, 6000)]
        [TestCase(PortMarketItem.Food, 4000)]
        [TestCase(PortMarketItem.Beer, 240)]
        [TestCase(PortMarketItem.Medicine, 120)]
        [TestCase(PortMarketItem.Wood, 200)]
        public void Catalog_MonthlyContractsSupportSeveralTradeCircuits(PortMarketItem item, int expected)
        {
            Assert.That(PortMarketCatalog.MaxPurchaseQuota(item, 0), Is.EqualTo(expected));
            Assert.That(PortMarketCatalog.MaxPurchaseQuota(item, -10), Is.EqualTo(expected));
            Assert.That(PortMarketCatalog.MaxPurchaseQuota(item, 99), Is.EqualTo(expected * 6));
        }

        [Test]
        public void Catalog_UsesBatchStepsForBulkSupplies()
        {
            Assert.That(PortMarketCatalog.PurchaseStep(PortMarketItem.Water), Is.EqualTo(100));
            Assert.That(PortMarketCatalog.PurchaseStep(PortMarketItem.Food), Is.EqualTo(100));
            Assert.That(PortMarketCatalog.SaleStep(PortMarketItem.Water), Is.EqualTo(100));
            Assert.That(PortMarketCatalog.SaleStep(PortMarketItem.Wood), Is.EqualTo(10));
        }

        [TestCase(0, 0)]
        [TestCase(49, 0)]
        [TestCase(50, 1)]
        [TestCase(750, 15)]
        public void Catalog_FameDiscountUsesWholeFiftiesAndCapsAtFifteenPercent(int fame, int expectedDiscount)
        {
            const string port = "白贝港";
            const PortMarketItem item = PortMarketItem.Wood;
            PortMarketDayState day = PortMarketCatalog.DayState(port, item, TestDay);
            decimal undiscountedRaw = PortMarketCatalog.BaseBuyPrice(item) *
                ShatteredSail.Core.Gameplay.CorePortEconomy.LocalPriceMultiplier(port, PortMarketCatalog.Key(item)) *
                day.BuyMultiplier;
            decimal expected = decimal.Round(undiscountedRaw * (100 - expectedDiscount) / 100m, 4);

            Assert.That(PortMarketCatalog.FamePurchaseDiscountPercent(fame), Is.EqualTo(expectedDiscount));
            Assert.That(PortMarketCatalog.BuyUnitPrice(port, item, TestDay, fame), Is.EqualTo(expected));
            Assert.That(PortMarketCatalog.CalculateBuyCost(port, item, TestDay, 1, fame), Is.EqualTo(expected));
        }

        [Test]
        public void Catalog_FameDiscountDoesNotChangeSaleQuotes()
        {
            const string port = "白贝港";
            const PortMarketItem item = PortMarketItem.Wood;
            const int quantity = 10;
            decimal baseline = PortMarketCatalog.CalculateSellRevenue(port, item, TestDay, quantity);

            foreach (int fame in new[] { 0, 49, 50, 750 })
            {
                // Sale quotes intentionally have no fame input; verify that
                // changing the purchase discount leaves the sale quote fixed.
                Assert.That(PortMarketCatalog.CalculateBuyCost(port, item, TestDay, quantity, fame), Is.GreaterThan(0m));
                Assert.That(PortMarketCatalog.CalculateSellRevenue(port, item, TestDay, quantity), Is.EqualTo(baseline));
            }
        }
    }
}
