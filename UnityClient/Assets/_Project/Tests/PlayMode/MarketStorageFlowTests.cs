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
                decimal price = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, now, 1);
                decimal gold = session.ProfileGold;
                yield return EnterMarketQuantity("wood", "1");
                Assert.That(session.Cargo.Wood, Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold), "The direction button and slider never purchase goods.");
                ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(1));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold - price));
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null);
                CaptureMarketStorage(controller, "01_Default_Ship", "默认装船，确认后才按报价扣款及额度，不进入仓储");

                // A full hold and a small account are transaction boundary fixtures.
                session.Cargo.Wood = Mathf.FloorToInt(session.CargoCapacityValue);
                int fullHoldWood = session.Cargo.Wood;
                RenderResponsiveBattle(controller); yield return null;
                yield return OpenMarketItem("wood");
                Assert.That(ActiveButton("Confirm Market Buy wood").IsInteractable(), Is.False,
                    "A full hold cannot confirm a ship purchase even though the buy mode remains selectable.");
                Assert.That(MarketQuantityField("wood").maxValue, Is.Zero);
                Button detailMode = ActiveButton("Market Storage Mode");
                Assert.That(detailMode, Is.Not.Null, "The same purchase-mode control remains reachable in an item's detail.");
                for (int attempt = 0; controller.MarketStorageMode != MarketPurchaseStorageMode.Warehouse && attempt < 3; attempt++)
                { ClickAcceptanceButton("Market Storage Mode"); yield return null; }
                Assert.That(controller.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Warehouse));
                Assert.That(ActiveButton("Market Storage Mode"), Is.SameAs(detailMode));
                Assert.That(MarketInfoLabel("Market Quota wood"), Is.Not.Null, "Changing modes keeps the selected item open.");
                Assert.That(ActiveButton("Market Buy wood").IsInteractable(), Is.True,
                    "Changing to warehouse immediately enables a purchase despite the full hold.");
                gold = session.ProfileGold;
                yield return EnterMarketQuantity("wood", "1");
                Assert.That(MarketQuantityField("wood").maxValue, Is.GreaterThan(0), "Warehouse purchases are not limited by ship cargo space.");
                ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(fullHoldWood));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
                Assert.That(session.PortWarehouse.GetAmount("白贝港", "wood"), Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold - price));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.EqualTo(2));
                CaptureMarketStorage(controller, "02_Full_Hold_To_Warehouse", "满船仍可确认采购入本港仓，另一港库存不变");

                decimal afterWarehouse = session.ProfileGold;
                session.RestoreProfile(price * .5m, session.CurrentPort);
                RenderResponsiveBattle(controller); yield return null;
                yield return OpenMarketItem("wood");
                Assert.That(ActiveButton("Confirm Market Buy wood").interactable, Is.False, "Insufficient funds disable purchase confirmation.");
                decimal threeUnits = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, now, 3);
                session.RestoreProfile(threeUnits, session.CurrentPort);
                RenderResponsiveBattle(controller); yield return null;
                yield return EnterMarketQuantity("wood", "1000");
                Assert.That(MarketQuantityField("wood").maxValue, Is.EqualTo(3), "The quote upper bound includes affordability.");
                Assert.That(MarketQuantityField("wood").value, Is.EqualTo(3));
                ClickAcceptanceButton("Cancel Market Trade"); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(threeUnits));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
                session.RestoreProfile(afterWarehouse, session.CurrentPort);
                RenderResponsiveBattle(controller); yield return null;

                decimal sale = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Wood, now, 1);
                gold = session.ProfileGold;
                yield return EnterMarketQuantity("wood", "1", purchase: false);
                ClickAcceptanceButton("Confirm Market Sell wood"); yield return null;
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
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                decimal gold = session.ProfileGold;
                decimal price = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, now, 1);
                yield return EnterMarketQuantity("wood", "1");
                AssertMarketStorageUnpurchased(controller, now, gold);
                ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Not.Null);
                AssertMarketPurchaseChoiceReadingSurface(session.CurrentPort, now, PortMarketItem.Wood, 1, price);
                AssertMarketStorageUnpurchased(controller, now, gold);
                Button.ButtonClickedEvent cancelledWarehouse = ActiveButton("Market Buy To Warehouse").onClick;
                Button.ButtonClickedEvent cancelledShip = ActiveButton("Market Buy To Ship").onClick;
                CaptureMarketStorage(controller, "04_Ask_Quoted_Not_Paid", "询问弹层仅报价，尚未扣钱、货物或额度");
                ClickAcceptanceButton("Cancel Market Purchase"); yield return null;
                cancelledWarehouse.Invoke(); cancelledShip.Invoke(); yield return null;
                AssertMarketStorageUnpurchased(controller, now, gold);

                yield return EnterMarketQuantity("wood", "1");
                ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
                Button.ButtonClickedEvent alternative = ActiveButton("Market Buy To Ship").onClick;
                Button.ButtonClickedEvent repeated = ActiveButton("Market Buy To Warehouse").onClick;
                ClickAcceptanceButton("Market Buy To Warehouse"); yield return null;
                repeated.Invoke(); alternative.Invoke(); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(gold - price));
                Assert.That(session.Cargo.Wood, Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(1));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.EqualTo(1));

                yield return EnterMarketQuantity("wood", "1");
                ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
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
                    yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                    yield return EnterMarketQuantity("wood", "1");
                    ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
                    Button.ButtonClickedEvent oldQuote = ActiveButton("Market Buy To Warehouse").onClick;
                    decimal gold = controller.Session.ProfileGold;
                    if (change == "port") controller.Session.RestoreProfile(gold, "白贝港");
                    else if (change == "quantity")
                    {
                        // The destination overlay blocks the slider. Cancel it
                        // before changing quantity through the visible control.
                        ClickAcceptanceButton("Cancel Market Purchase"); yield return null;
                        yield return EnterMarketQuantity("wood", "2");
                    }
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
                    yield return SelectMarketStorageMode(controller, mode);
                    ClickAcceptanceButton("Return To Port Hub"); yield return null;
                    ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                    decimal gold = controller.Session.ProfileGold;
                    int amount = ExpectedProvisionQuantity(controller, 26);
                    decimal cost = ProvisionCost(controller, now, amount, amount);
                    yield return PurchaseRecommendedDepartureWaterFood();
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

        private static void AssertMarketPurchaseChoiceReadingSurface(string port, DateTime now,
            PortMarketItem item, int quantity, decimal price)
        {
            Canvas.ForceUpdateCanvases();
            GameObject dialog = GameObject.Find("Market Purchase Choice");
            Image paper = dialog.GetComponent<Image>();
            Assert.That(paper, Is.Not.Null);
            Assert.That(paper.sprite, Is.SameAs(NauticalSurfaceSkin.ReadingPaper),
                "Choosing a destination uses the reading parchment rather than the timber surface.");
            Assert.That(paper.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(paper.color, Is.EqualTo(Color.white), "The paper remains opaque enough for dark ink.");
            Assert.That(paper.sprite.border.x, Is.GreaterThan(0f));

            Text quote = GameObject.Find("Market Purchase Choice Quote").GetComponent<Text>();
            Assert.That(quote.text.Split('\n'), Has.Length.EqualTo(2),
                "The quote keeps only the item total and payment/cancellation explanation.");
            Assert.That(quote.text, Does.Contain(PortMarketCatalog.Name(item)).And.Contain("×" + quantity)
                .And.Contain(PortMarketCatalog.FormatPrice(price)).And.Contain("取消不扣款"));
            Assert.That(quote.text, Does.Not.Contain(port));
            Assert.That(quote.text, Does.Not.Contain(now.ToString("yy-MM-dd")));
            Assert.That(quote.text, Does.Not.Match(@"\b\d{2,4}-\d{2}-\d{2}\b"),
                "The removed location/date row must not return in a different date format.");
            Assert.That(quote.preferredHeight, Is.LessThanOrEqualTo(quote.rectTransform.rect.height + .5f));
            using (var visible = new TextGenerator())
            using (var complete = new TextGenerator())
            {
                TextGenerationSettings visibleSettings = quote.GetGenerationSettings(quote.rectTransform.rect.size);
                TextGenerationSettings completeSettings = visibleSettings;
                completeSettings.verticalOverflow = VerticalWrapMode.Overflow;
                Assert.That(visible.Populate(quote.text, visibleSettings), Is.True);
                Assert.That(complete.Populate(quote.text, completeSettings), Is.True);
                Assert.That(visible.vertexCount, Is.GreaterThan(4), "The quote renders visible glyphs.");
                Assert.That(visible.vertexCount, Is.EqualTo(complete.vertexCount), "Neither of the quote's lines is clipped.");
            }

            foreach (string name in new[] { "Market Purchase Choice Title", "Market Purchase Choice Quote",
                "Market Warehouse Purchase Status", "Market Ship Purchase Status" })
            {
                Text label = GameObject.Find(name).GetComponent<Text>();
                float luminance = .2126f * label.color.r + .7152f * label.color.g + .0722f * label.color.b;
                Assert.That(luminance, Is.LessThan(.3f), name + " uses readable dark ink on parchment.");
                Assert.That(label.color.a, Is.EqualTo(1f), name);
                Assert.That(label.raycastTarget, Is.False, name + " cannot block a destination action.");
            }
        }

        private static IEnumerator SelectMarketStorageMode(CoreGameplayDemoController controller, MarketPurchaseStorageMode desired)
        {
            yield return ReturnMarketCatalog();
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
                GameObject.Find("Market Purchase Choice") != null ? "Market Purchase Choice" : "Market Board");
    }
}
