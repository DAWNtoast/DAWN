using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public IEnumerator MarketRetainedView_TradesModesAndChoiceKeepRowsAndRejectOldCallbacks()
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
                var retained = CaptureRetainedMarketObjects();
                AssertMarketIconCatalog(controller);
                Assert.That(GameObject.Find("Market Summary").GetComponent<Text>().text, Does.Contain("货舱余量"));
                yield return OpenMarketItem("water");
                Image readingSurface = AssertLivePaperSurface("Market Item Detail", NauticalSurfaceSkin.ReadingPaper, true);
                Sprite readingPaper = readingSurface.sprite;
                Button inventoryToggle = ActiveButton("Market Inventory Toggle");
                var oldFold = inventoryToggle.onClick;
                var oldDetailBuy = ActiveButton("Market Buy water").onClick;
                decimal beforeInformation = session.ProfileGold;
                int beforeInformationWater = session.Cargo.Water;
                int beforeInformationQuota = PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(
                    session.CurrentPort, now, PortMarketItem.Water), 0);
                Assert.That(MarketInfoLabel("Market Quota water").gameObject.activeInHierarchy, Is.False);
                Assert.That(MarketInfoLabel("Market Holdings water").gameObject.activeInHierarchy, Is.False);
                yield return SetMarketInventoryExpanded(true);
                oldFold.Invoke(); oldDetailBuy.Invoke();
                Assert.That(MarketInfoLabel("Market Quota water").gameObject.activeInHierarchy, Is.True);
                Assert.That(MarketInfoLabel("Market Holdings water").gameObject.activeInHierarchy, Is.True);
                Assert.That(ActiveButton("Confirm Market Buy water"), Is.Not.Null, "Expanding information retains the current buy direction while old callbacks expire.");
                Assert.That(session.ProfileGold, Is.EqualTo(beforeInformation));
                Assert.That(session.Cargo.Water, Is.EqualTo(beforeInformationWater));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(
                    session.CurrentPort, now, PortMarketItem.Water), 0), Is.EqualTo(beforeInformationQuota));
                var expandedDetailToggle = inventoryToggle.onClick;
                ClickAcceptanceButton("Market Buy water"); yield return null;
                expandedDetailToggle.Invoke();
                Assert.That(MarketInfoLabel("Market Quota water").gameObject.activeInHierarchy, Is.True);
                Assert.That(MarketInfoLabel("Market Holdings water").gameObject.activeInHierarchy, Is.True,
                    "Expanded purchase information includes carried and stored holdings.");
                ClickAcceptanceButton("Cancel Market Trade"); yield return null;
                Assert.That(GameObject.Find("Market Item Detail"), Is.Null, "Cancelling closes the shop without committing its quote.");
                Assert.That(MarketInfoLabel("Market Quota water").gameObject.activeInHierarchy, Is.False);
                Assert.That(MarketInfoLabel("Market Holdings water").gameObject.activeInHierarchy, Is.False);
                yield return OpenMarketItem("wood");
                Assert.That(MarketInfoLabel("Market Quota wood").gameObject.activeInHierarchy, Is.False);
                Assert.That(MarketInfoLabel("Market Holdings wood").gameObject.activeInHierarchy, Is.False,
                    "Each newly selected item starts with optional information folded.");
                Assert.That(ActiveButton("Market Inventory Toggle"), Is.SameAs(inventoryToggle));
                yield return EnterMarketQuantity("water", "7");
                Assert.That(MarketInfoLabel("Market Quota water").gameObject.activeInHierarchy, Is.False);
                Assert.That(MarketInfoLabel("Market Holdings water").gameObject.activeInHierarchy, Is.False);
                Slider water = MarketQuantityField("water");
                var oldInput = water.onValueChanged;
                Button.ButtonClickedEvent oldBuy = MarketConfirm("water").onClick;
                decimal initialGold = session.ProfileGold;
                decimal cost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Water, now, 7);
                NavalBattleVisualCapture.Capture(controller, "AgedPaper_01_MarketReadingSurface.png",
                    "市场图标目录进入饮水购入数量页；七份报价尚未成交", "Market Board");
                yield return ClickShipyardControl("Confirm Market Buy water");
                oldBuy.Invoke(); oldInput.Invoke(100);
                Assert.That(session.Cargo.Water, Is.EqualTo(7));
                Assert.That(session.ProfileGold, Is.EqualTo(initialGold - cost));
                yield return OpenMarketTrade("water");
                Assert.That(MarketQuantityField("water"), Is.SameAs(water));
                Assert.That(water.value, Is.EqualTo(7));
                AssertMarketQuantityHeader(session.ProfileGold);
                AssertRetainedMarketObjects(retained);
                Assert.That(AssertLivePaperSurface("Market Item Detail", readingPaper, true), Is.SameAs(readingSurface));
                NavalBattleVisualCapture.Capture(controller, "AgedPaper_02_MarketRetainedPurchase.png",
                    "真实指针确认购买成功；数据刷新保留纸面、目录与滑条", "Market Board");

                var previousDirectionSlider = water.onValueChanged;
                Button.ButtonClickedEvent previousDirectionConfirm = MarketConfirm("water").onClick;
                yield return EnterMarketQuantity("water", "7", purchase: false);
                yield return SetMarketInventoryExpanded(true);
                Assert.That(MarketInfoLabel("Market Quota water").gameObject.activeInHierarchy, Is.True);
                Assert.That(MarketInfoLabel("Market Holdings water").gameObject.activeInHierarchy, Is.True,
                    "Expanded sale information retains both quota and holdings.");
                previousDirectionSlider.Invoke(100); previousDirectionConfirm.Invoke();
                Assert.That(MarketQuantityField("water").value, Is.EqualTo(7), "An old buy slider must not alter the sell quote.");
                Assert.That(session.Cargo.Water, Is.EqualTo(7));
                Assert.That(session.ProfileGold, Is.EqualTo(initialGold - cost));
                Button.ButtonClickedEvent oldSell = MarketConfirm("water", false).onClick;
                decimal sale = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Water, now, 7);
                ClickAcceptanceButton("Confirm Market Sell water"); yield return null;
                oldSell.Invoke();
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(initialGold - cost + sale));
                AssertRetainedMarketObjects(retained);

                yield return EnterMarketQuantity("water", "7");
                Button footer = ActiveButton("Close Market Shop");
                Button investment = ActiveButton("Market Open Investment");
                Slider modeQuantity = MarketQuantityField("water");
                Button.ButtonClickedEvent previousQuote = MarketConfirm("water").onClick;
                Button.ButtonClickedEvent previousMode = ActiveButton("Market Storage Mode").onClick;
                ClickAcceptanceButton("Market Storage Mode"); yield return null;
                MarketPurchaseStorageMode mode = controller.MarketStorageMode;
                previousMode.Invoke();
                Assert.That(controller.MarketStorageMode, Is.EqualTo(mode), "An old queued mode callback cannot cycle a new display.");
                previousQuote.Invoke();
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null, "A quote captured before changing destination cannot open a purchase.");
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(initialGold - cost + sale));
                Assert.That(MarketQuantityField("water"), Is.SameAs(modeQuantity), "Destination changes retain the selected item and quantity page.");
                Assert.That(modeQuantity.value, Is.EqualTo(7));
                Assert.That(MarketInfoLabel("Market Quota water").gameObject.activeInHierarchy, Is.True,
                    "Changing the purchase destination retains the expanded inventory preference.");
                Assert.That(MarketInfoLabel("Market Holdings water").gameObject.activeInHierarchy, Is.True);
                AssertRetainedMarketObjects(retained);
                ClickAcceptanceButton("Close Market Shop"); yield return null;
                Assert.That(retained["Close Market Shop"].GetComponent<Button>(), Is.SameAs(footer), "Closing the drawer retains its close control.");
                Assert.That(ActiveButton("Market Open Investment"), Is.SameAs(investment), "The investment shortcut remains available outside the drawer.");
                AssertMarketIconCatalog(controller);
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                yield return EnterMarketQuantity("water", "7");
                ClickAcceptanceButton("Confirm Market Buy water"); yield return null;
                var cancelledChoice = ActiveButton("Market Buy To Ship").onClick;
                ClickAcceptanceButton("Cancel Market Purchase"); yield return null;
                cancelledChoice.Invoke();
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null);
                Assert.That(GameObject.Find("Market Board").GetComponent<CanvasGroup>().interactable, Is.True);
                AssertRetainedMarketObjects(retained);

                decimal beforeWarehouse = session.ProfileGold;
                yield return EnterMarketQuantity("water", "7");
                ClickAcceptanceButton("Confirm Market Buy water"); yield return null;
                var alternateDestination = ActiveButton("Market Buy To Ship").onClick;
                ClickAcceptanceButton("Market Buy To Warehouse"); yield return null;
                alternateDestination.Invoke();
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.EqualTo(7));
                Assert.That(session.ProfileGold, Is.EqualTo(beforeWarehouse - cost));
                yield return OpenMarketItem("water");
                Assert.That(MarketInfoLabel("Market Holdings water").text, Does.Contain("本港仓储 7"));
                AssertMarketQuantityHeader(session.ProfileGold);
                AssertRetainedMarketObjects(retained);
                yield return ReturnMarketCatalog();
                AssertMarketIconCatalog(controller);
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketRetainedView_NewMonthRefreshesInPlaceAndPortChangeRejectsOldView()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                var retained = CaptureRetainedMarketObjects();
                yield return EnterMarketQuantity("wood", "1");
                yield return SetMarketInventoryExpanded(true);
                var oldBuy = MarketConfirm("wood").onClick;
                Slider quantity = MarketQuantityField("wood");
                var oldInput = quantity.onValueChanged;
                decimal gold = controller.Session.ProfileGold;
                now = now.AddMonths(1);
                yield return null; yield return null;
                AssertRetainedMarketObjects(retained);
                Assert.That(MarketInfoLabel("Market Quota wood").gameObject.activeInHierarchy, Is.True,
                    "The monthly refresh retains the expanded purchase information.");
                Assert.That(MarketInfoLabel("Market Holdings wood").gameObject.activeInHierarchy, Is.True);
                Assert.That(typeof(CoreGameplayDemoController).GetField("marketDisplayedDate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller), Is.EqualTo(new DateTime(now.Year, now.Month, 1)),
                    "The retained market must advance its quote month even when the summary only shows cargo space.");
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    int buyLot = Math.Max(PortMarketCatalog.PurchaseStep(item), PortMarketCatalog.SaleStep(item));
                    decimal buy = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, item,
                        now, buyLot, controller.Session.Cargo.Fame);
                    Assert.That(retained["Market Buy Price " + key].GetComponent<Text>().text,
                        Is.EqualTo(PortMarketCatalog.FormatPrice(buy) + " 金"), key + " / refreshed buy quote");
                    Assert.That(retained["Market Buy Unit " + key].GetComponent<Text>().text,
                        Is.EqualTo("每" + buyLot + "单位"), key + " / refreshed purchase lot");
                }
                oldBuy.Invoke(); oldInput.Invoke(40);
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                yield return OpenMarketTrade("wood");
                Assert.That(MarketQuantityField("wood"), Is.SameAs(quantity));
                Assert.That(quantity.value, Is.EqualTo(1));

                var sourceBuy = MarketConfirm("wood").onClick;
                var sourcePanel = GameObject.Find("Market Panel");
                controller.Session.RestoreProfile(gold, "白贝港");
                MethodInfo refresh = typeof(CoreGameplayDemoController).GetMethod("TryRefreshMarketFacilityView", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(refresh.Invoke(controller, null), Is.EqualTo(false), "A different port must build its own view.");
                sourceBuy.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                yield return null;
                Assert.That(GameObject.Find("Market Panel"), Is.Not.SameAs(sourcePanel));
                Assert.That(GameObject.Find("Market Title").GetComponent<Text>().text, Is.EqualTo(CorePortEconomy.TradeSummary("白贝港")));
                sourceBuy.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                yield return EnterMarketQuantity("wood", "1");
                ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
                Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private static Dictionary<string, GameObject> CaptureRetainedMarketObjects()
        {
            var objects = new Dictionary<string, GameObject>();
            GameObject panel = GameObject.Find("Market Panel");
            Assert.That(panel, Is.Not.Null);
            Transform[] nodes = panel.GetComponentsInChildren<Transform>(true);
            foreach (string name in new[] { "Market Panel", "Market Board", "Market Summary", "Market Storage Mode",
                "Market Catalog Grid", "Market Item Detail", "Market Trade Selection", "Market Inventory Toggle",
                "Market Shop Title", "Close Market Shop", "Market Detail Buy Price", "Market Detail Sell Price" })
                objects[name] = name == "Market Panel" ? panel : nodes.Single(node => node.name == name).gameObject;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                foreach (string prefix in new[] { "Market Row ", "Market Item Icon ", "Market Item Name ",
                    "Market Buy Price ", "Market Buy Unit " })
                {
                    string name = prefix + PortMarketCatalog.Key(item);
                    objects[name] = nodes.Single(node => node.name == name).gameObject;
                }
            foreach (var pair in objects) Assert.That(pair.Value, Is.Not.Null, pair.Key);
            return objects;
        }

        private static Image AssertLivePaperSurface(string name, Sprite paper, bool raycastTarget)
        {
            GameObject panel = GameObject.Find(name);
            Assert.That(panel, Is.Not.Null, name);
            Image image = panel.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, name);
            Assert.That(paper, Is.Not.Null, name);
            Assert.That(image.sprite, Is.SameAs(paper), name);
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), name);
            Assert.That(image.color, Is.EqualTo(Color.white), name);
            Assert.That(image.raycastTarget, Is.EqualTo(raycastTarget), name);
            Assert.That(image.transform.Find("Material Timber Rim"), Is.Null, name + " already has an authored paper edge.");
            return image;
        }

        private static void AssertRetainedMarketObjects(Dictionary<string, GameObject> objects)
        {
            GameObject panel = GameObject.Find("Market Panel");
            Assert.That(panel, Is.SameAs(objects["Market Panel"]));
            Transform[] nodes = panel.GetComponentsInChildren<Transform>(true);
            foreach (var pair in objects)
            {
                GameObject actual = pair.Key == "Market Panel" ? panel : nodes.Single(node => node.name == pair.Key).gameObject;
                Assert.That(actual, Is.SameAs(pair.Value), pair.Key + " was rebuilt during a data refresh.");
            }
        }
    }
}
