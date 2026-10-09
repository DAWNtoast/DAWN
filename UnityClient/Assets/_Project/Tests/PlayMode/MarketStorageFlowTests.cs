using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator MarketStorage_DefaultShipThenFullHoldWarehousePurchaseAndShipOnlyResalePersist()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                SetProvisionClock(controller, () => now);
                Assert.That(controller.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Ship));
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return EnsureMarketItemVisible("wood");
                decimal price = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, now, 1);
                decimal gold = session.ProfileGold;
                ClickAcceptanceButton("Market Buy wood"); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(1));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold - price));
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null);
                CaptureMarketStorage(controller, "01_Default_Ship", "默认装船，按报价扣款及额度，不进入仓储");

                // A full hold is a transaction fixture, not earned cargo.
                session.Cargo.Wood = Mathf.FloorToInt(session.CargoCapacityValue);
                int fullHoldWood = session.Cargo.Wood;
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Warehouse);
                gold = session.ProfileGold;
                ClickAcceptanceButton("Market Buy wood"); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(fullHoldWood));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
                Assert.That(session.PortWarehouse.GetAmount("白贝港", "wood"), Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold - price));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.EqualTo(2));
                CaptureMarketStorage(controller, "02_Full_Hold_To_Warehouse", "满船仍可直接采购入本港仓，另一港库存不变");

                decimal sale = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Wood, now, 1);
                gold = session.ProfileGold;
                ClickAcceptanceButton("Market Sell wood"); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(fullHoldWood - 1));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
                Assert.That(session.ProfileGold, Is.EqualTo(gold + sale));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.EqualTo(2));
                decimal finalGold = session.ProfileGold;
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Warehouse));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(finalGold));
                Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(fullHoldWood - 1));
                Assert.That(controller.Session.PortWarehouse.GetAmount(controller.Session.CurrentPort, "wood"), Is.EqualTo(1));
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return EnsureMarketItemVisible("wood");
                CaptureMarketStorage(controller, "03_Mode_And_Cargo_Reload", "重载保留模式、船货、本港仓货及即时交易金币");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketStorage_AskWaitsForChoiceCancelIsFreeAndEachDestinationPaysExactlyOnce()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return EnsureMarketItemVisible("wood");
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                decimal gold = session.ProfileGold;
                decimal price = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, now, 1);
                ClickAcceptanceButton("Market Buy wood"); yield return null;
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Not.Null);
                AssertMarketStorageUnpurchased(controller, now, gold);
                Button.ButtonClickedEvent cancelledWarehouse = ActiveButton("Market Buy To Warehouse").onClick;
                Button.ButtonClickedEvent cancelledShip = ActiveButton("Market Buy To Ship").onClick;
                CaptureMarketStorage(controller, "04_Ask_Quoted_Not_Paid", "询问弹层仅报价，尚未扣钱、货物或额度");
                ClickAcceptanceButton("Cancel Market Purchase"); yield return null;
                cancelledWarehouse.Invoke(); cancelledShip.Invoke(); yield return null;
                AssertMarketStorageUnpurchased(controller, now, gold);

                ClickAcceptanceButton("Market Buy wood"); yield return null;
                Button.ButtonClickedEvent alternative = ActiveButton("Market Buy To Ship").onClick;
                Button.ButtonClickedEvent repeated = ActiveButton("Market Buy To Warehouse").onClick;
                ClickAcceptanceButton("Market Buy To Warehouse"); yield return null;
                repeated.Invoke(); alternative.Invoke(); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(gold - price));
                Assert.That(session.Cargo.Wood, Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.EqualTo(1));

                ClickAcceptanceButton("Market Buy wood"); yield return null;
                ClickAcceptanceButton("Market Buy To Ship"); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(gold - price * 2));
                Assert.That(session.Cargo.Wood, Is.EqualTo(1));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.EqualTo(2));
                CaptureMarketStorage(controller, "05_Ask_Both_Destinations", "分别选择仓储与装船，两笔各扣一次；旧另一路回调无效");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketStorage_AskRejectsQuotesAfterPortQuantityOrMonthChanges()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddMonths(1).AddMinutes(-1), firstDay = now;
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                foreach (string change in new[] { "port", "quantity", "month" })
                {
                    yield return LoadProvisioningFixture();
                    var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                    controller.Session.RestoreProfile(2000, "断锚港");
                    now = firstDay;
                    SetProvisionClock(controller, () => now);
                    ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return EnsureMarketItemVisible("wood");
                    yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                    Button.ButtonClickedEvent changeQuantity = ActiveButton("Market Increase wood").onClick;
                    ClickAcceptanceButton("Market Buy wood"); yield return null;
                    Button.ButtonClickedEvent oldQuote = ActiveButton("Market Buy To Warehouse").onClick;
                    decimal gold = controller.Session.ProfileGold;
                    if (change == "port") controller.Session.RestoreProfile(gold, "白贝港");
                    else if (change == "quantity") changeQuantity.Invoke(); // previously queued UI input, not a private field write
                    else now = firstDay.Date.AddMonths(1).AddSeconds(1);
                    oldQuote.Invoke(); yield return null;
                    Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), change);
                    Assert.That(controller.Session.Cargo.Wood, Is.Zero, change);
                    foreach (string port in new[] { "断锚港", "白贝港" })
                    {
                        Assert.That(controller.Session.PortWarehouse.GetAmount(port, "wood"), Is.Zero, change);
                        for (int day = 0; day < 2; day++)
                            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(port, firstDay.AddMonths(day), PortMarketItem.Wood)), Is.Zero, change);
                    }
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketStorage_DepartureProvisionsAlwaysBoardTheShipForWarehouseAndAskPreferences()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                foreach (MarketPurchaseStorageMode mode in new[] { MarketPurchaseStorageMode.Warehouse, MarketPurchaseStorageMode.Ask })
                {
                    yield return LoadProvisioningFixture();
                    var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                    SetProvisionClock(controller, () => now);
                    ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return EnsureMarketItemVisible("wood");
                    yield return SelectMarketStorageMode(controller, mode);
                    ClickAcceptanceButton("Return To Port Hub"); yield return null;
                    ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                    decimal gold = controller.Session.ProfileGold;
                    int amount = ExpectedProvisionQuantity(controller, 26);
                    decimal cost = ProvisionCost(controller, now, amount, amount);
                    ClickAcceptanceButton("Departure Buy Provisions"); yield return null;
                    Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost));
                    Assert.That(controller.Session.Cargo.Water, Is.EqualTo(amount));
                    Assert.That(controller.Session.Cargo.Food, Is.EqualTo(amount));
                    Assert.That(controller.Session.PortWarehouse.GetAmount(controller.Session.CurrentPort, "water"), Is.Zero);
                    Assert.That(controller.Session.PortWarehouse.GetAmount(controller.Session.CurrentPort, "food"), Is.Zero);
                    Assert.That(controller.MarketStorageMode, Is.EqualTo(mode), "The provisioning exception does not overwrite the player's market preference.");
                    Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null);
                    NavalBattleVisualCapture.Capture(controller, "MarketStorage_06_Departure_" + mode + ".png",
                        "市场模式 UI 夹具 · 备航采购在 " + mode + " 偏好下仍直接补入船舱", "Voyage Setup");
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private static IEnumerator SelectMarketStorageMode(CoreGameplayDemoController controller, MarketPurchaseStorageMode desired)
        {
            for (int attempt = 0; controller.MarketStorageMode != desired && attempt < 3; attempt++)
            { ClickAcceptanceButton("Market Storage Mode"); yield return null; }
            Assert.That(controller.MarketStorageMode, Is.EqualTo(desired));
        }

        private static void AssertMarketStorageUnpurchased(CoreGameplayDemoController controller, DateTime day, decimal gold)
        {
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.Session.Cargo.Wood, Is.Zero);
            Assert.That(controller.Session.PortWarehouse.GetAmount(controller.Session.CurrentPort, "wood"), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(controller.Session.CurrentPort, day, PortMarketItem.Wood)), Is.Zero);
        }

        private static Dictionary<string, int?> PreserveMarketStorageLedgers(DateTime day)
        {
            var result = new Dictionary<string, int?>();
            foreach (string port in new[] { "断锚港", "白贝港" })
                for (int offset = 0; offset < 2; offset++)
                    foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                    {
                        string key = CoreGameplayDemoController.MarketPurchaseKey(port, day.AddMonths(offset), item);
                        result[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
                        PlayerPrefs.DeleteKey(key);
                    }
            return result;
        }

        private static void CaptureMarketStorage(CoreGameplayDemoController controller, string stage, string detail) =>
            NavalBattleVisualCapture.Capture(controller, "MarketStorage_" + stage + ".png",
                "市场入货模式 UI 事务夹具 · " + detail,
                GameObject.Find("Market Purchase Choice") != null ? "Market Purchase Choice" : "Market Panel");
    }
}
