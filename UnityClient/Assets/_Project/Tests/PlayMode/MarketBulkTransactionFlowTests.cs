using System;
using System.Collections;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator MarketBulk_QuantitySteppersKeepViewAndSellSelectedBatchExactlyOnce()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                SetProvisionClock(controller, () => now);
                session.Cargo.Wood = 80; // Carried cargo fixture, not a purchase.
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return EnsureMarketItemVisible("wood");
                GameObject panel = GameObject.Find("Market Panel");
                InputField otherQuantity = MarketQuantityField("iron");
                otherQuantity.text = "7";
                Button increase = ActiveButton("Market Increase wood");
                Button.ButtonClickedEvent staleSale = ActiveButton("Market Sell wood").onClick;
                decimal gold = session.ProfileGold;
                int childCount = panel.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 1; i < 40; i++) increase.onClick.Invoke();
                Assert.That(GameObject.Find("Market Panel"), Is.SameAs(panel),
                    "Quantity steppers must not rebuild the market or port UI.");
                Assert.That(MarketQuantityField("iron"), Is.SameAs(otherQuantity));
                Assert.That(otherQuantity.text, Is.EqualTo("7"));
                Assert.That(panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
                Assert.That(MarketQuantityField("wood").text, Is.EqualTo("40"));
                staleSale.Invoke();
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.Cargo.Wood, Is.EqualTo(80));
                decimal revenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Wood, now, 40);
                Assert.That(ActiveButton("Market Sell wood").GetComponentInChildren<Text>().text,
                    Is.EqualTo("<size=22>出售 40</size>\n<size=22>+" + PortMarketCatalog.FormatPrice(revenue) + " 金</size>"));
                Button.ButtonClickedEvent sale = ActiveButton("Market Sell wood").onClick;
                sale.Invoke(); sale.Invoke(); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(40), "The former 10-unit cap must not split a selected batch.");
                Assert.That(session.ProfileGold, Is.EqualTo(gold + revenue));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(gold + revenue));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Wood")), Is.EqualTo(40));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.Zero);
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketBulk_SoldOutStockStillAllowsTypedBulkSaleAndRejectsInvalidInput()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                SetProvisionClock(controller, () => now);
                session.Cargo.Water = 350;
                string key = CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Water);
                int stock = PortMarketCatalog.DayState(session.CurrentPort, PortMarketItem.Water, now).Stock;
                PlayerPrefs.SetInt(key, stock);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                Assert.That(MarketQuantityField("water").interactable, Is.True);
                decimal gold = session.ProfileGold;
                foreach (string invalid in new[] { "0", "-1", "9999999999" })
                {
                    MarketQuantityField("water").text = invalid;
                    Assert.That(ActiveButton("Market Buy water").interactable, Is.False);
                    Assert.That(ActiveButton("Market Sell water").interactable, Is.False);
                    ActiveButton("Market Sell water").onClick.Invoke();
                    Assert.That(session.Cargo.Water, Is.EqualTo(350));
                }
                yield return EnterMarketQuantity("water", "250");
                Assert.That(MarketQuantityField("water").text, Is.EqualTo("250"));
                Assert.That(ActiveButton("Market Buy water").interactable, Is.False);
                Assert.That(ActiveButton("Market Sell water").interactable, Is.True);
                decimal revenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Water, now, 250);
                Button.ButtonClickedEvent sell = ActiveButton("Market Sell water").onClick;
                sell.Invoke(); sell.Invoke(); yield return null;
                Assert.That(session.Cargo.Water, Is.EqualTo(100));
                Assert.That(session.ProfileGold, Is.EqualTo(gold + revenue));
                Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(stock), "Selling never replenishes the purchase allowance.");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketBulk_ChangedCargoOrAccountLimitRejectsEntireSale()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                SetProvisionClock(controller, () => now);
                session.Cargo.Wood = 80;
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return EnterMarketQuantity("wood", "40");
                Button.ButtonClickedEvent stale = ActiveButton("Market Sell wood").onClick;
                decimal gold = session.ProfileGold;
                session.Cargo.Wood = 25;
                stale.Invoke(); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.Cargo.Wood, Is.EqualTo(25), "A stale 40-unit quote must not silently sell only 25 units.");
                session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold, session.CurrentPort);
                ActiveButton("Market Sell wood").onClick.Invoke(); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
                Assert.That(session.Cargo.Wood, Is.EqualTo(25), "A rejected payout must not remove any cargo.");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }
    }
}
