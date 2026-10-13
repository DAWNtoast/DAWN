using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ItemIcons_WarehouseRowsAndDialogsKeepRealTransfersReachable()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems) SetInvestmentCargo(session.Cargo, item, 2);
            SetRareWarehouseFixture(session, 1, 2, 1, 1, 1, 1);
            string port = session.CurrentPort;
            yield return OpenPortStorage();
            yield return WaitForItemIconView(controller);
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                RawImage icon = AssertItemIcon("Warehouse Storage Icon " + key, ItemIconCatalog.ForCargo(key), 42f);
                AssertIconDoesNotCoverRowText(icon);
                AssertIconPointerReachesButton(icon, "Warehouse Storage Row " + key, false);
            }
            NavalBattleVisualCapture.Capture(controller, "ItemIcons_01_Basic_Warehouse.png",
                "基础九种物资小图标；库存由隔离测试夹具准备，非游玩所得", "Warehouse Panel");

            int wood = session.Cargo.Wood;
            int storedWood = session.PortWarehouse.GetAmount(port, "wood");
            AssertIconPointerReachesButton(GameObject.Find("Warehouse Storage Icon wood").GetComponent<RawImage>(),
                "Warehouse Storage Row wood", true);
            yield return null;
            RawImage actionIcon = AssertItemIcon("Warehouse Item Actions Icon", ItemIconCatalog.ForCargo("wood"), 40f);
            AssertIconDoesNotCoverRowText(actionIcon);
            ClickAcceptanceButton("Warehouse Adjust Cargo"); yield return null;
            RawImage adjustmentIcon = AssertItemIcon("Warehouse Cargo Adjustment Icon", ItemIconCatalog.ForCargo("wood"), 40f);
            AssertIconDoesNotCoverRowText(adjustmentIcon);
            NavalBattleVisualCapture.Capture(controller, "ItemIcons_02_Warehouse_Adjustment.png",
                "仓库弹窗40像素物品图标保留原滑条和确认按钮", "Warehouse Storage Modal");
            Slider slider = GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>();
            yield return DragWarehouseSlider(slider, slider.minValue);
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "wood"), Is.EqualTo(storedWood + wood));

            ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;
            yield return WaitForItemIconView(controller);
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.RareItems)
            {
                RawImage icon = AssertItemIcon("Warehouse Rare Icon " + item.Key, ItemIconCatalog.ForCargo(item.Key), 56f);
                AssertIconDoesNotCoverRowText(icon);
                AssertIconPointerReachesButton(icon, "Warehouse Rare Row " + item.Key, false);
            }
            NavalBattleVisualCapture.Capture(controller, "ItemIcons_03_Rare_Warehouse.png",
                "六类珍稀物品使用统一小图标，名称和库存保持独立列", "Warehouse Panel");
            int storedGem = session.PortWarehouse.GetAmount(port, "gem");
            AssertIconPointerReachesButton(GameObject.Find("Warehouse Rare Icon gem").GetComponent<RawImage>(),
                "Warehouse Rare Row gem", true);
            yield return null;
            ClickAcceptanceButton("Warehouse Adjust Cargo"); yield return null;
            slider = GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>();
            yield return DragWarehouseSlider(slider, slider.minValue);
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.Cargo.Gem, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(port, "gem"), Is.EqualTo(storedGem + 1));
            // Collection ownership is a visual fixture. Display and ledger must
            // resolve the same authored item, regardless of its source atlas.
            for (int i = 0; i < CoreWarehouseCollection.DisplaySlotCount; i++)
            {
                string id = CoreWarehouseCollection.Catalog[i].Id;
                controller.WarehouseCollection.Grant(id);
                Assert.That(controller.WarehouseCollection.TryDisplay(id, i), Is.True);
            }
            ClickAcceptanceButton("Warehouse Tab Valuables"); yield return null;
            yield return WaitForItemIconView(controller);
            for (int i = 0; i < CoreWarehouseCollection.DisplaySlotCount; i++)
            {
                CoreWarehouseTreasureDefinition definition = CoreWarehouseCollection.Catalog[i];
                RawImage displayed = AssertItemIcon("Warehouse Displayed Art " + i, ItemIconCatalog.ForTreasure(definition), 186f);
                Assert.That(displayed.rectTransform.anchoredPosition.y - displayed.rectTransform.rect.height * .5f,
                    Is.EqualTo(-93f).Within(.01f), "Treasures stay grounded on the regional shelf.");
                if (i < 3) AssertItemIcon("Warehouse Inventory Art " + definition.Id, ItemIconCatalog.ForTreasure(definition), 130f);
            }
            NavalBattleVisualCapture.Capture(controller, "ItemIcons_06_Treasure_Collection.png",
                "六件藏品为隔离美术夹具，右侧列表与实际陈列共用每件物品的独立图标", "Warehouse Panel");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ItemIcons_MarketKeepsAllGoodsReadableAndBuySellAmountsExact()
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
                yield return WaitForItemIconView(controller);
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    RawImage icon = AssertItemIcon("Market Item Icon " + key, ItemIconCatalog.ForCargo(key), 80f);
                    AssertIconDoesNotCoverRowText(icon);
                    Assert.That(GameObject.Find("Market Item Name " + key).GetComponent<Text>().text,
                        Does.Contain(PortMarketCatalog.Name(item)));
                    foreach (Selectable control in icon.transform.parent.GetComponentsInChildren<Selectable>())
                    {
                        if (control.gameObject == icon.gameObject) continue;
                        Assert.That(ItemIconBounds(icon.rectTransform, icon.transform.parent)
                            .Overlaps(ItemIconBounds((RectTransform)control.transform, icon.transform.parent)),
                            Is.False, key + " icon must leave the quantity and transaction controls clear.");
                    }
                }
                NavalBattleVisualCapture.Capture(controller, "ItemIcons_04_Market.png",
                    "九种商品80像素可点击图标，三列各三件，价格直接显示在羊皮纸上", "Market Panel");
                decimal gold = session.ProfileGold;
                decimal buy = PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, now, 1);
                decimal sell = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Wood, now, 1);
                yield return OpenMarketTrade("wood");
                ClickAcceptanceButton("Confirm Market Buy wood"); yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(1));
                Assert.That(session.ProfileGold, Is.EqualTo(gold - buy));
                yield return OpenMarketTrade("wood", false);
                ClickAcceptanceButton("Confirm Market Sell wood"); yield return null;
                Assert.That(session.Cargo.Wood, Is.Zero);
                Assert.That(session.ProfileGold, Is.EqualTo(gold - buy + sell));
                Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.Zero);

                ClickAcceptanceButton("Return To Port Hub"); yield return null;
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                yield return WaitForItemIconView(controller);
                Assert.That(GameObject.Find("Departure Provision Water Icon"), Is.Null);
                Assert.That(GameObject.Find("Departure Provision Food Icon"), Is.Null);
                Assert.That(GameObject.Find("Departure Provision Quote"), Is.Null);
                Assert.That(ActiveButton("Departure Buy Provisions"), Is.Not.Null);
                ClickAcceptanceButton("Route 1"); yield return null;
                yield return WaitForItemIconView(controller);
                RawImage[] visibleImages = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None);
                Assert.That(Array.FindAll(visibleImages, icon => icon.name == "Departure Provision Water Icon").Length, Is.Zero);
                Assert.That(Array.FindAll(visibleImages, icon => icon.name == "Departure Provision Food Icon").Length, Is.Zero);
                Assert.That(GameObject.Find("Departure Provision Quote"), Is.Null);
                NavalBattleVisualCapture.Capture(controller, "ItemIcons_05_Departure.png",
                    "出航界面保留基础物资入口，切换航线不会重建已删除的报价图标", "Voyage Setup");
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private static IEnumerator WaitForItemIconView(CoreGameplayDemoController controller)
        {
            var field = typeof(CoreGameplayDemoController).GetField("transitionCanvasGroup", BindingFlags.Instance | BindingFlags.NonPublic);
            var group = (CanvasGroup)field.GetValue(controller);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (group != null && group.alpha < .999f && Time.realtimeSinceStartup < deadline) yield return null;
            if (group != null) Assert.That(group.alpha, Is.EqualTo(1f).Within(.001f), "Wait for the actual screen reveal before icon capture.");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static RawImage AssertItemIcon(string name, ItemIconArt expected, float size)
        {
            GameObject item = GameObject.Find(name);
            Assert.That(item, Is.Not.Null, name);
            RawImage icon = item.GetComponent<RawImage>();
            Assert.That(expected.IsAvailable, Is.True, name + " source art");
            Assert.That(icon.enabled, Is.True, name);
            Assert.That(icon.texture, Is.SameAs(expected.Texture), name);
            Assert.That(icon.uvRect, Is.EqualTo(expected.Uv), name);
            Assert.That(icon.raycastTarget, Is.EqualTo(icon.GetComponent<Button>() != null),
                name + " only interactive item icons consume pointer hits");
            Assert.That(icon.rectTransform.rect.width, Is.GreaterThan(0f).And.LessThanOrEqualTo(size + .01f), name);
            Assert.That(icon.rectTransform.rect.height, Is.GreaterThan(0f).And.LessThanOrEqualTo(size + .01f), name);
            float aspect = icon.texture.width * icon.uvRect.width / (icon.texture.height * icon.uvRect.height);
            Assert.That(icon.rectTransform.rect.width / icon.rectTransform.rect.height, Is.EqualTo(aspect).Within(.001f), name);
            return icon;
        }

        private static void AssertIconDoesNotCoverRowText(RawImage icon)
        {
            Transform row = icon.transform.parent;
            Rect bounds = ItemIconBounds(icon.rectTransform, row);
            foreach (Text label in row.GetComponentsInChildren<Text>())
                if (label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                    Assert.That(bounds.Overlaps(ItemIconBounds(label.rectTransform, row)), Is.False,
                        icon.name + " overlaps " + label.name);
        }

        private static Rect ItemIconBounds(RectTransform rect, Transform relativeTo)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3 minimum = relativeTo.InverseTransformPoint(corners[0]);
            Vector3 maximum = minimum;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 point = relativeTo.InverseTransformPoint(corners[i]);
                minimum = Vector3.Min(minimum, point); maximum = Vector3.Max(maximum, point);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static void AssertIconPointerReachesButton(RawImage icon, string buttonName, bool click)
        {
            Button button = ActiveButton(buttonName);
            Assert.That(button, Is.Not.Null, buttonName);
            Canvas.ForceUpdateCanvases();
            Canvas canvas = icon.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, icon.rectTransform.TransformPoint(icon.rectTransform.rect.center))
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, buttonName + " icon area has no hit");
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button),
                buttonName + " icon area intercepted by " + hits[0].gameObject.name);
            if (click)
            {
                Assert.That(button.IsInteractable(), Is.True, buttonName);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            }
        }
    }
}
