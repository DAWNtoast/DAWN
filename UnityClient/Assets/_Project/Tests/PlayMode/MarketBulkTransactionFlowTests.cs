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
                GameObject panel = GameObject.Find("Market Panel");
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Warehouse);
                yield return EnterMarketQuantity("water", "7");
                Slider water = MarketQuantityField("water");
                var oldWaterSlider = water.onValueChanged;
                Button.ButtonClickedEvent oldWaterConfirm = MarketConfirm("water").onClick;
                decimal gold = session.ProfileGold;
                ClickAcceptanceButton("Cancel Market Trade"); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.Cargo.Water, Is.Zero, "Cancelling a quantity quote does not purchase goods.");
                yield return OpenMarketTrade("wood", false);
                Slider wood = MarketQuantityField("wood");
                wood.value = 1;
                oldWaterSlider.Invoke(50); oldWaterConfirm.Invoke();
                Assert.That(wood.value, Is.EqualTo(1), "An old item slider must not change the newly selected item's quote.");
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Button.ButtonClickedEvent staleSale = MarketConfirm("wood", false).onClick;
                int childCount = panel.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 1; i < 40; i++) wood.value = i + 1;
                Assert.That(GameObject.Find("Market Panel"), Is.SameAs(panel), "The slider must not rebuild the market or port UI.");
                Assert.That(MarketQuantityField("wood"), Is.SameAs(wood));
                Assert.That(panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
                Assert.That(wood.value, Is.EqualTo(40));
                staleSale.Invoke();
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.Cargo.Wood, Is.EqualTo(80));
                decimal revenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Wood, now, 40);
                AssertMarketTradeTotal(revenue);
                Button.ButtonClickedEvent sale = MarketConfirm("wood", false).onClick;
                sale.Invoke(); sale.Invoke(); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(40), "A selected batch must sell atomically, without the former 10-unit cap.");
                Assert.That(session.ProfileGold, Is.EqualTo(gold + revenue));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(gold + revenue));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Wood")), Is.EqualTo(40));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.Zero);
                yield return OpenMarketTrade("water");
                Assert.That(MarketQuantityField("water"), Is.SameAs(water));
                Assert.That(water.value, Is.EqualTo(PortMarketCatalog.PurchaseStep(PortMarketItem.Water)), "Returning to a shop starts a fresh quantity while retaining the slider instance.");
                Assert.That(session.Cargo.Water, Is.Zero);
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
                yield return OpenMarketItem("water");
                Assert.That(MarketConfirm("water").interactable, Is.False, "Sold-out stock disables purchase confirmation without blocking sale navigation.");
                Assert.That(ActiveButton("Market Sell water").interactable, Is.True);
                yield return OpenMarketTrade("water", false);
                Slider slider = MarketQuantityField("water");
                Assert.That(slider.maxValue, Is.EqualTo(350));
                decimal gold = session.ProfileGold;
                foreach (string invalid in new[] { "0", "-1" })
                {
                    yield return EnterMarketQuantity("water", invalid, purchase: false);
                    Assert.That(slider.value, Is.Zero);
                    Assert.That(MarketConfirm("water", false).interactable, Is.False);
                    MarketConfirm("water", false).onClick.Invoke();
                    Assert.That(session.Cargo.Water, Is.EqualTo(350));
                    Assert.That(session.ProfileGold, Is.EqualTo(gold));
                }
                slider.value = 1000; yield return null;
                Assert.That(slider.value, Is.EqualTo(350), "Sale quantity cannot exceed carried cargo.");
                slider.value = 7.4f; yield return null;
                Assert.That(slider.value, Is.EqualTo(7), "Only integer quantities can be quoted.");
                yield return EnterMarketQuantity("water", "250", purchase: false);
                Assert.That(slider.value, Is.EqualTo(250));
                Assert.That(MarketConfirm("water", false).interactable, Is.True);
                decimal revenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Water, now, 250);
                AssertMarketTradeTotal(revenue);
                Button.ButtonClickedEvent sell = MarketConfirm("water", false).onClick;
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
                yield return EnterMarketQuantity("wood", "40", purchase: false);
                Button.ButtonClickedEvent stale = MarketConfirm("wood", false).onClick;
                decimal gold = session.ProfileGold;
                session.Cargo.Wood = 25;
                stale.Invoke(); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.Cargo.Wood, Is.EqualTo(25), "A stale 40-unit quote must not silently sell only 25 units.");
                session.RestoreProfile(CoreEconomyBalance.MaximumProfileGold, session.CurrentPort);
                MarketConfirm("wood", false).onClick.Invoke(); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(CoreEconomyBalance.MaximumProfileGold));
                Assert.That(session.Cargo.Wood, Is.EqualTo(25), "A rejected payout must not remove any cargo.");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }
    }
}
