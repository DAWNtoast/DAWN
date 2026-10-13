using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator MarketQuantity_DefaultHundredUsesLastStockAndTypedSevenPersistsExactGold()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                SetProvisionClock(controller, () => now);
                int foodAllowance = Math.Min(PortMarketCatalog.DayState(session.CurrentPort, PortMarketItem.Food, now).Stock,
                    PortMarketCatalog.MaxPurchaseQuota(PortMarketItem.Food, 0));
                PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Food), foodAllowance - 37);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                AssertMarketIconCatalog(controller);
                decimal gold = session.ProfileGold;
                yield return OpenMarketTrade("water");
                Assert.That(MarketQuantityField("water").value, Is.EqualTo(100));
                Assert.That(session.ProfileGold, Is.EqualTo(gold), "Choosing an item and direction only opens a quote.");
                yield return OpenMarketTrade("food");
                Assert.That(MarketQuantityField("food").value, Is.EqualTo(37));
                decimal foodCost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Food, now, 37);
                AssertMarketTradeTotal(foodCost);
                ClickAcceptanceButton("Confirm Market Buy food"); yield return null;
                Assert.That(session.Cargo.Food, Is.EqualTo(37));
                Assert.That(session.ProfileGold, Is.EqualTo(gold - foodCost));
                yield return OpenMarketItem("food");
                Assert.That(MarketInfoLabel("Market Quota food").text, Does.Contain("可购 0"));
                yield return OpenMarketTrade("food", false);
                Assert.That(MarketQuantityField("food").maxValue, Is.EqualTo(37),
                    "Sold-out purchase stock must not prevent a sale of carried food.");
                NavalBattleVisualCapture.Capture(controller, "MarketQuantity_01_Last_37_Food.png",
                    "余量37默认报价37；确认前不成交，买空后仍可售出船货", "Market Board");

                yield return OpenMarketTrade("water");
                Button.ButtonClickedEvent oldHundred = MarketConfirm("water").onClick;
                Slider waterSlider = MarketQuantityField("water");
                float originalQuantity = waterSlider.value;
                yield return DragMarketQuantitySlider(waterSlider,
                    originalQuantity / waterSlider.maxValue > .4f ? .2f : .8f);
                int draggedQuantity = Mathf.RoundToInt(waterSlider.value);
                Assert.That(draggedQuantity, Is.GreaterThan(0));
                Assert.That(draggedQuantity, Is.Not.EqualTo(originalQuantity), "Dragging the visible handle must change the quoted quantity.");
                Assert.That(GameObject.Find("Market Quantity water").GetComponent<Text>().text,
                    Is.EqualTo(draggedQuantity.ToString(CultureInfo.InvariantCulture)));
                AssertMarketTradeTotal(PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Water, now, draggedQuantity));
                oldHundred.Invoke();
                Assert.That(session.Cargo.Water, Is.Zero, "Dragging a slider invalidates the old confirmation without buying anything.");
                Assert.That(session.ProfileGold, Is.EqualTo(gold - foodCost));
                yield return EnterMarketQuantity("water", "7", false);
                oldHundred.Invoke();
                Assert.That(session.Cargo.Water, Is.Zero, "A quote captured before changing the slider cannot spend money.");
                decimal waterCost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Water, now, 7);
                Assert.That(waterCost, Is.GreaterThan(0m).And.LessThan(1m));
                AssertMarketTradeTotal(waterCost);
                Button.ButtonClickedEvent sevenQuote = MarketConfirm("water").onClick;
                ClickAcceptanceButton("Confirm Market Buy water"); yield return null;
                sevenQuote.Invoke(); yield return null;
                decimal expected = gold - foodCost - waterCost;
                Assert.That(session.Cargo.Water, Is.EqualTo(7));
                Assert.That(session.ProfileGold, Is.EqualTo(expected));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(expected));
                AssertMarketQuantityHeader(expected);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Water)), Is.EqualTo(7));
                NavalBattleVisualCapture.Capture(controller, "MarketQuantity_02_Seven_Water_Exact_Gold.png",
                    "滑条选择7水后确认购买，精确扣款；重复旧确认不扣款", "Market Board");
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(expected));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(7));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(37));
                AssertMarketQuantityHeader(expected);
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketQuantity_AskTypedSevenEachDestinationCancelAndInvalidInputAreAtomic()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                SetProvisionClock(controller, () => now);
                ClickAcceptanceButton("Port Hotspot Market"); yield return null;
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                decimal gold = session.ProfileGold;
                foreach (string invalid in new[] { "0", "-7" })
                {
                    yield return EnterMarketQuantity("water", invalid, false);
                    Assert.That(MarketQuantityField("water").value, Is.Zero, invalid);
                    Assert.That(MarketConfirm("water").interactable, Is.False, invalid);
                    MarketConfirm("water").onClick.Invoke();
                    Assert.That(session.ProfileGold, Is.EqualTo(gold));
                    Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null);
                }
                Slider slider = MarketQuantityField("water");
                slider.value = slider.maxValue + 1000f; yield return null;
                Assert.That(slider.value, Is.EqualTo(slider.maxValue), "The slider must clamp to a feasible purchase limit.");
                Assert.That(session.ProfileGold, Is.EqualTo(gold), "Changing a quantity never confirms a purchase.");
                yield return EnterMarketQuantity("water", "7");
                decimal waterCost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Water, now, 7);
                AssertMarketTradeTotal(waterCost);
                ClickAcceptanceButton("Confirm Market Buy water"); yield return null;
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(GameObject.Find("Market Purchase Choice Quote").GetComponent<Text>().text,
                    Does.Contain("×7").And.Contain(PortMarketCatalog.FormatPrice(waterCost)));
                Button.ButtonClickedEvent cancelled = ActiveButton("Market Buy To Warehouse").onClick;
                ClickAcceptanceButton("Cancel Market Purchase"); yield return null;
                cancelled.Invoke();
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.Zero);

                yield return EnterMarketQuantity("water", "7");
                ClickAcceptanceButton("Confirm Market Buy water"); yield return null;
                Button.ButtonClickedEvent otherDestination = ActiveButton("Market Buy To Ship").onClick;
                ClickAcceptanceButton("Market Buy To Warehouse"); yield return null;
                otherDestination.Invoke();
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "water"), Is.EqualTo(7));
                Assert.That(session.Cargo.Water, Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold - waterCost));
                yield return EnterMarketQuantity("food", "7");
                decimal foodCost = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Food, now, 7);
                ClickAcceptanceButton("Confirm Market Buy food"); yield return null;
                NavalBattleVisualCapture.Capture(controller, "MarketQuantity_03_Ask_Seven_Food.png",
                    "7份食物确认后询问去向；选择前仍不扣款", "Market Purchase Choice");
                ClickAcceptanceButton("Market Buy To Ship"); yield return null;
                decimal expected = gold - waterCost - foodCost;
                Assert.That(session.Cargo.Food, Is.EqualTo(7));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "food"), Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(expected));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(expected));
                AssertMarketQuantityHeader(expected);
                foreach (PortMarketItem item in new[] { PortMarketItem.Water, PortMarketItem.Food })
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, item)), Is.EqualTo(7));
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private static Slider MarketQuantityField(string key)
        {
            GameObject field = GameObject.Find("Market Quantity Slider " + key);
            Assert.That(field, Is.Not.Null, "Open the item's direction before choosing a quantity: " + key);
            Slider slider = field.GetComponent<Slider>();
            Assert.That(slider, Is.Not.Null);
            Assert.That(slider.minValue, Is.Zero);
            Assert.That(slider.wholeNumbers, Is.True);
            return slider;
        }

        private static Button MarketConfirm(string key, bool purchase = true) =>
            RequireWarehouseButton("Confirm Market " + (purchase ? "Buy " : "Sell ") + key);

        private static IEnumerator ReturnMarketCatalog()
        {
            if (ActiveButton("Cancel Market Purchase") != null)
            { ClickAcceptanceButton("Cancel Market Purchase"); yield return null; }
            if (ActiveButton("Close Market Shop") != null)
            { ClickAcceptanceButton("Close Market Shop"); yield return null; }
        }

        private static IEnumerator OpenMarketItem(string key)
        {
            if (ActiveButton("Cancel Market Purchase") != null)
            { ClickAcceptanceButton("Cancel Market Purchase"); yield return null; }
            if (GameObject.Find("Market Detail Icon " + key) != null) yield break;
            if (ActiveButton("Market Item Icon " + key) == null)
            {
                yield return ReturnMarketCatalog();
                PortMarketItem item = PortMarketCatalog.AllItems.Single(candidate => PortMarketCatalog.Key(candidate) == key);
                ClickAcceptanceButton("Market Shop " + MarketSceneLayout.ShopForItem(item)); yield return null;
            }
            if (GameObject.Find("Market Detail Icon " + key) == null)
            { ClickAcceptanceButton("Market Item Icon " + key); yield return null; }
            Assert.That(GameObject.Find("Market Detail Icon " + key), Is.Not.Null);
            Assert.That(MarketInfoLabel("Market Quota " + key), Is.Not.Null);
            Assert.That(MarketInfoLabel("Market Holdings " + key), Is.Not.Null);
        }

        private static Text MarketInfoLabel(string name)
        {
            GameObject panel = GameObject.Find("Market Panel");
            Assert.That(panel, Is.Not.Null, name + " / active market");
            Text label = panel.GetComponentsInChildren<Text>(true).SingleOrDefault(item => item.name == name);
            Assert.That(label, Is.Not.Null, name + " / retained inventory information");
            return label;
        }

        private static IEnumerator SetMarketInventoryExpanded(bool expanded)
        {
            Button toggle = ActiveButton("Market Inventory Toggle");
            Assert.That(toggle, Is.Not.Null);
            bool current = toggle.GetComponentInChildren<Text>().text.Contains("▲");
            if (current != expanded)
            {
                ClickAcceptanceButton("Market Inventory Toggle");
                yield return null;
            }
            Assert.That(toggle.GetComponentInChildren<Text>().text.Contains("▲"), Is.EqualTo(expanded));
        }

        private static IEnumerator OpenMarketTrade(string key, bool purchase = true)
        {
            string confirm = "Confirm Market " + (purchase ? "Buy " : "Sell ") + key;
            if (GameObject.Find("Market Quantity Slider " + key) != null && ActiveButton(confirm) != null &&
                ActiveButton("Cancel Market Purchase") == null) yield break;
            yield return OpenMarketItem(key);
            ClickAcceptanceButton("Market " + (purchase ? "Buy " : "Sell ") + key); yield return null;
            Assert.That(GameObject.Find("Market Quantity Slider " + key), Is.Not.Null);
            Assert.That(ActiveButton(confirm), Is.Not.Null);
        }

        // Retain the old string/bool call shape for the acceptance flows sharing
        // this helper. Slider.value uses the real UI change event; endEdit no
        // longer applies, and sales explicitly request purchase:false.
        private static IEnumerator EnterMarketQuantity(string key, string quantity, bool endEdit = true, bool purchase = true)
        {
            yield return OpenMarketTrade(key, purchase);
            Slider slider = MarketQuantityField(key);
            float requested;
            Assert.That(float.TryParse(quantity, NumberStyles.Float, CultureInfo.InvariantCulture, out requested), Is.True);
            slider.value = requested; yield return null;
            Assert.That(slider.value, Is.EqualTo(Mathf.Round(Mathf.Clamp(requested, slider.minValue, slider.maxValue))));
            Assert.That(GameObject.Find("Market Quantity " + key).GetComponent<Text>().text,
                Is.EqualTo(Mathf.RoundToInt(slider.value).ToString(CultureInfo.InvariantCulture)));
        }

        private static void AssertMarketTradeTotal(decimal expected)
        {
            Assert.That(GameObject.Find("Market Trade Total"), Is.Not.Null);
            Assert.That(GameObject.Find("Market Trade Total").GetComponent<Text>().text, Does.Contain(PortMarketCatalog.FormatPrice(expected)));
        }

        private static IEnumerator DragMarketQuantitySlider(Slider slider, float fraction)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(slider.IsInteractable(), Is.True);
            Assert.That(slider.handleRect, Is.Not.Null);
            Assert.That(slider.direction, Is.EqualTo(Slider.Direction.LeftToRight));
            RectTransform track = slider.handleRect.parent as RectTransform;
            Assert.That(track, Is.Not.Null);
            Canvas canvas = slider.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(eventCamera, slider.handleRect.position);
            Vector2 end = RectTransformUtility.WorldToScreenPoint(eventCamera,
                track.TransformPoint(new Vector3(Mathf.Lerp(track.rect.xMin, track.rect.xMax, fraction), track.rect.center.y, 0f)));
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = start,
                pressPosition = start,
                pointerPressRaycast = new RaycastResult
                {
                    gameObject = slider.gameObject,
                    module = canvas.GetComponent<GraphicRaycaster>(),
                    screenPosition = start,
                },
            };
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = end;
            pointer.delta = end - start;
            pointer.dragging = true;
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.endDragHandler);
            yield return null;
        }

        private static void AssertMarketIconCatalog(CoreGameplayDemoController controller)
        {
            Transform board = GameObject.Find("Market Board").transform;
            Transform[] nodes = board.GetComponentsInChildren<Transform>(true);
            Assert.That(GameObject.Find("Market Item Detail"), Is.Null, "The closed market shows its shops before any product drawer.");
            MarketShopKind[] shops = Enum.GetValues(typeof(MarketShopKind)).Cast<MarketShopKind>().ToArray();
            Assert.That(shops.Length, Is.EqualTo(4));
            foreach (MarketShopKind shop in shops)
            {
                Button hotspot = RequireWarehouseButton("Market Shop " + shop);
                Assert.That(hotspot.gameObject.activeInHierarchy, Is.True, shop.ToString());
                Assert.That(hotspot.IsInteractable(), Is.True, shop.ToString());
            }
            Assert.That(shops.SelectMany(MarketSceneLayout.ItemsForShop).Distinct().Count(), Is.EqualTo(9),
                "The four shops preserve access to every existing market item.");
            DateTime displayedDate = (DateTime)typeof(CoreGameplayDemoController).GetProperty("MarketNow",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                Transform row = nodes.Single(node => node.name == "Market Row " + key);
                Button button = nodes.Single(node => node.name == "Market Item Icon " + key).GetComponent<Button>();
                Assert.That(row.gameObject.activeInHierarchy, Is.False, "Product tabs stay inside the closed drawer: " + key);
                Assert.That(row.GetComponent<Button>(), Is.Not.Null, "The whole product tab remains selectable: " + key);
                RawImage icon = button.GetComponent<RawImage>();
                ItemIconArt art = ItemIconCatalog.ForCargo(key);
                Assert.That(icon, Is.Not.Null, "Each retained product tab keeps its item icon: " + key);
                Assert.That(icon.texture, Is.SameAs(art.Texture));
                Assert.That(icon.uvRect, Is.EqualTo(art.Uv));
                Assert.That(icon.raycastTarget, Is.True);
                Assert.That(button.targetGraphic, Is.SameAs(icon));
                Assert.That(nodes.Single(node => node.name == "Market Item Name " + key).GetComponent<Text>().text,
                    Is.EqualTo(PortMarketCatalog.Name(item)));
                int buyLot = Math.Max(PortMarketCatalog.PurchaseStep(item), PortMarketCatalog.SaleStep(item));
                decimal buy = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, item, displayedDate,
                    buyLot, controller.Session.Cargo.Fame);
                Text price = nodes.Single(node => node.name == "Market Buy Price " + key).GetComponent<Text>();
                Text unit = nodes.Single(node => node.name == "Market Buy Unit " + key).GetComponent<Text>();
                Assert.That(price.text, Is.EqualTo(PortMarketCatalog.FormatPrice(buy) + " 金"));
                Assert.That(unit.text, Is.EqualTo("每" + buyLot.ToString(CultureInfo.InvariantCulture) + "单位"));
                Assert.That(price.gameObject.activeInHierarchy, Is.False, "Prices appear only in the selected product ledger.");
                Assert.That(unit.gameObject.activeInHierarchy, Is.False);
                Assert.That(GameObject.Find("Market Quota " + key), Is.Null, "Inventory remains inside the closed drawer.");
                Assert.That(GameObject.Find("Market Holdings " + key), Is.Null);
                Assert.That(ActiveButton("Market Buy " + key), Is.Null);
                Assert.That(ActiveButton("Market Sell " + key), Is.Null);
            }
        }
        private static void AssertMarketQuantityHeader(decimal expected)
        {
            GameObject readout = GameObject.Find("Port Facility Gold") ?? GameObject.Find("Port Hub Gold");
            Assert.That(readout, Is.Not.Null);
            Text gold = readout.GetComponent<Text>();
            Assert.That(gold.text, Does.EndWith(PortMarketCatalog.FormatPrice(expected)));
        }
    }
}
