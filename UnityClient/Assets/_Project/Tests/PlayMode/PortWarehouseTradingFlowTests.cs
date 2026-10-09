using System;
using System.Collections;
using System.Collections.Generic;
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
        // Explicit inventory/port/clock fixtures exercise UI transactions. These
        // tests do not claim the arranged goods were earned on a played voyage.
        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouseTrading_NineRowsAndAdjustmentPreviewCancelEndpointsAndReload()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            string port = session.CurrentPort;
            var serialized = new List<string> { "v1" };
            int quantity = 0;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                quantity++;
                SetInvestmentCargo(session.Cargo, item, quantity);
                serialized.Add(port + "," + PortMarketCatalog.Key(item) + "," + (quantity + 10));
            }
            Assert.That(session.PortWarehouse.Restore(string.Join(";", serialized)), Is.True);
            yield return OpenPortStorage();
            Assert.That(GameObject.Find("Warehouse Storage Page"), Is.Null);
            Assert.That(PortMarketCatalog.AllItems.Count, Is.EqualTo(9));
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
            {
                string key = PortMarketCatalog.Key(item);
                Assert.That(ActiveButton("Warehouse Storage Row " + key), Is.Not.Null);
                Assert.That(GameObject.Find("Warehouse Storage Name " + key).GetComponent<Text>().text, Does.Contain(PortMarketCatalog.Name(item)));
                Assert.That(GameObject.Find("Warehouse Storage Stored " + key).GetComponent<Text>().text,
                    Does.Contain(session.PortWarehouse.GetAmount(port, key).ToString(CultureInfo.InvariantCulture)));
                Assert.That(GameObject.Find("Warehouse Storage Carried " + key).GetComponent<Text>().text,
                    Does.Contain(session.GetMarketCargoAmount(key).ToString(CultureInfo.InvariantCulture)));
                Assert.That(GameObject.Find("Warehouse Storage Volume " + key).GetComponent<Text>().text,
                    Does.Contain((session.GetMarketCargoAmount(key) * PortMarketCatalog.CargoVolume(item)).ToString("0.00", CultureInfo.InvariantCulture)));
            }
            CaptureWarehouseTrading(controller, "01_Nine_Inventory_Rows", "九种物资同时显示本港数量、随船数量和随船占用空间");
            ClickAcceptanceButton("Warehouse Storage Row wood"); yield return null;
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Not.Null);
            CaptureWarehouseTrading(controller, "02_Item_Actions", "点击物品后选择调整船舱或出售");
            ClickAcceptanceButton("Warehouse Adjust Cargo"); yield return null;
            Slider slider = GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>();
            int carried = session.Cargo.Wood, stored = session.PortWarehouse.GetAmount(port, "wood");
            decimal gold = session.ProfileGold;
            Assert.That(slider.minValue, Is.EqualTo(-carried));
            Assert.That(slider.maxValue, Is.EqualTo(Math.Min(stored,
                Math.Floor((session.CargoCapacityValue - session.Cargo.UsedVolume + .001f) / PortMarketCatalog.CargoVolume(PortMarketItem.Wood)))));
            Assert.That(slider.wholeNumbers, Is.True);
            Assert.That(slider.value, Is.Zero);
            Assert.That(ActiveButton("Confirm Warehouse Cargo Adjustment").IsInteractable(), Is.False);
            yield return DragWarehouseSlider(slider, slider.minValue);
            Assert.That(session.Cargo.Wood, Is.EqualTo(carried));
            Assert.That(session.PortWarehouse.GetAmount(port, "wood"), Is.EqualTo(stored));
            Button.ButtonClickedEvent cancelled = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            CaptureWarehouseTrading(controller, "03_Adjustment_Preview", "拖至负极仅预览全部存入，确认前两处库存不变");
            ClickAcceptanceButton("Cancel Warehouse Cargo Adjustment"); yield return null;
            cancelled.Invoke(); yield return null;
            Assert.That(session.Cargo.Wood, Is.EqualTo(carried));
            Assert.That(session.PortWarehouse.GetAmount(port, "wood"), Is.EqualTo(stored));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));

            yield return StoreAllVisibleCargo(controller, "wood");
            yield return OpenWarehouseCargoAdjustment("wood");
            slider = GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>();
            Assert.That(slider.minValue, Is.Zero);
            Assert.That(slider.value, Is.Zero);
            int maximum = (int)slider.maxValue;
            Assert.That(maximum, Is.EqualTo(session.GetMaxPortWarehouseWithdrawal("wood")));
            yield return DragWarehouseSlider(slider, slider.maxValue);
            Assert.That(session.Cargo.Wood, Is.Zero, "Positive dragging also only previews.");
            Button.ButtonClickedEvent repeated = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            repeated.Invoke(); yield return null;
            Assert.That(session.Cargo.Wood, Is.EqualTo(maximum));
            Assert.That(session.PortWarehouse.GetAmount(port, "wood"), Is.EqualTo(stored + carried - maximum));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            CaptureWarehouseTrading(controller, "04_Adjusted_Inventory", "正极最多装入可用货舱，确认仅转移一次");
            string warehouseAfter = session.PortWarehouse.Serialize();
            InvestmentResourceSnapshot cargoAfter = SnapshotInvestmentResources(session);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            AssertInvestmentResourcesEqual(controller.Session, cargoAfter);
            Assert.That(controller.Session.PortWarehouse.Serialize(), Is.EqualTo(warehouseAfter));
            yield return OpenPortStorage();
            CaptureWarehouseTrading(controller, "05_Adjustment_Reload", "重载保留九行库存、装船量与本港仓数量");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouseTrading_MixedSalePreservesFractionalPriceAndPersistsOnePayment()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            SetProvisionClock(controller, () => now);
            PortMarketItem selected = PortMarketItem.Water;
            int part = 1;
            Assert.That(PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, selected, now, part),
                Is.GreaterThan(0m).And.LessThan(1m), "A sub-gold sale must retain its value.");
            string key = PortMarketCatalog.Key(selected), port = session.CurrentPort;
            SetInvestmentCargo(session.Cargo, selected, part + 4);
            Assert.That(session.PortWarehouse.Restore("v1;" + port + "," + key + "," + (part + 3) + ";白贝港," + key + ",9"), Is.True);
            yield return OpenPortStorage();
            yield return OpenWarehouseCargoSale(key);
            Slider storedSlider = GameObject.Find("Warehouse Stored Sale Slider").GetComponent<Slider>();
            Slider shipSlider = GameObject.Find("Warehouse Ship Sale Slider").GetComponent<Slider>();
            Assert.That(storedSlider.minValue, Is.Zero); Assert.That(storedSlider.maxValue, Is.EqualTo(part + 3));
            Assert.That(shipSlider.minValue, Is.Zero); Assert.That(shipSlider.maxValue, Is.EqualTo(part + 4));
            Assert.That(storedSlider.value, Is.Zero); Assert.That(shipSlider.value, Is.Zero);
            decimal gold = session.ProfileGold;
            yield return DragWarehouseSlider(storedSlider, part);
            yield return DragWarehouseSlider(shipSlider, part);
            decimal expected = PortMarketCatalog.SellUnitPrice(port, selected, now) * (part * 2);
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.GetMarketCargoAmount(key), Is.EqualTo(part + 4));
            Assert.That(session.PortWarehouse.GetAmount(port, key), Is.EqualTo(part + 3));
            CaptureWarehouseTrading(controller, "06_Mixed_Sale_Quote", "两个滑条选择本港与随船数量，合计按当月单价保留小数金额");
            Button.ButtonClickedEvent repeat = ActiveButton("Confirm Warehouse Cargo Sale").onClick;
            ClickAcceptanceButton("Confirm Warehouse Cargo Sale"); yield return null;
            repeat.Invoke(); repeat.Invoke(); yield return null;
            Assert.That(session.ProfileGold, Is.EqualTo(gold + expected));
            Assert.That(session.GetMarketCargoAmount(key), Is.EqualTo(4));
            Assert.That(session.PortWarehouse.GetAmount(port, key), Is.EqualTo(3));
            Assert.That(session.PortWarehouse.GetAmount("白贝港", key), Is.EqualTo(9));
            CaptureWarehouseTrading(controller, "07_Mixed_Sale_Complete", "确认原子扣除两处货物并一次入金，重复旧确认无效");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold + expected));
            Assert.That(controller.Session.GetMarketCargoAmount(key), Is.EqualTo(4));
            Assert.That(controller.Session.PortWarehouse.GetAmount(port, key), Is.EqualTo(3));
            Assert.That(controller.Session.PortWarehouse.GetAmount("白贝港", key), Is.EqualTo(9));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouseTrading_SaleCancellationAndStalePortMonthOrQuantityCannotCharge()
        {
            foreach (string change in new[] { "cancel", "port", "month", "quantity" })
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                session.RestoreProfile(2000, "断锚港");
                session.Cargo.Wood = 10;
                Assert.That(session.PortWarehouse.Restore("v1;断锚港,wood,10;白贝港,wood,10"), Is.True);
                DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddMonths(1).AddMinutes(-1);
                SetProvisionClock(controller, () => now);
                yield return OpenPortStorage();
                yield return OpenWarehouseCargoSale("wood");
                yield return DragWarehouseSlider(GameObject.Find("Warehouse Stored Sale Slider").GetComponent<Slider>(), 2);
                yield return DragWarehouseSlider(GameObject.Find("Warehouse Ship Sale Slider").GetComponent<Slider>(), 3);
                Button.ButtonClickedEvent stale = ActiveButton("Confirm Warehouse Cargo Sale").onClick;
                if (change == "cancel") { ClickAcceptanceButton("Cancel Warehouse Cargo Sale"); yield return null; }
                else if (change == "port") session.RestoreProfile(session.ProfileGold, "白贝港");
                else if (change == "month") now = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddSeconds(1);
                else session.Cargo.Wood -= 1; // still enough to fulfil it, but no longer the quoted inventory
                InvestmentResourceSnapshot before = SnapshotInvestmentResources(session);
                string warehouseBefore = session.PortWarehouse.Serialize();
                stale.Invoke(); yield return null;
                AssertInvestmentResourcesEqual(session, before);
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore), change);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortWarehouseTrading_ZeroQuantityAndAccountOverflowRejectButFractionalSalePays()
        {
            foreach (bool overflow in new[] { false, true })
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                PortMarketItem item = overflow ? PortMarketItem.Wood : PortMarketItem.Water;
                string key = PortMarketCatalog.Key(item);
                DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
                SetProvisionClock(controller, () => now);
                session.RestoreProfile(overflow ? CoreEconomyBalance.MaximumProfileGold - 1 : 2000, "断锚港");
                SetInvestmentCargo(session.Cargo, item, 2);
                Assert.That(session.PortWarehouse.Restore("v1;断锚港," + key + ",2;白贝港," + key + ",9"), Is.True);
                yield return OpenPortStorage();
                yield return OpenWarehouseCargoSale(key);

                InvestmentResourceSnapshot before = SnapshotInvestmentResources(session);
                string warehouseBefore = session.PortWarehouse.Serialize();
                Assert.That(GameObject.Find("Warehouse Stored Sale Slider").GetComponent<Slider>().value, Is.Zero);
                Assert.That(GameObject.Find("Warehouse Ship Sale Slider").GetComponent<Slider>().value, Is.Zero);
                Assert.That(ActiveButton("Confirm Warehouse Cargo Sale").IsInteractable(), Is.False);
                ActiveButton("Confirm Warehouse Cargo Sale").onClick.Invoke(); yield return null;
                AssertInvestmentResourcesEqual(session, before);
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore), "A zero-quantity callback cannot remove either inventory.");

                yield return DragWarehouseSlider(GameObject.Find("Warehouse Stored Sale Slider").GetComponent<Slider>(), 1);
                if (overflow) yield return DragWarehouseSlider(GameObject.Find("Warehouse Ship Sale Slider").GetComponent<Slider>(), 1);
                decimal revenue = PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, item, now, overflow ? 2 : 1);
                AssertInvestmentResourcesEqual(session, before);
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore), "Selecting quantities only previews the sale.");
                if (overflow)
                {
                    Assert.That(revenue, Is.GreaterThan(1m));
                    Assert.That(ActiveButton("Confirm Warehouse Cargo Sale").IsInteractable(), Is.False);
                    ActiveButton("Confirm Warehouse Cargo Sale").onClick.Invoke(); yield return null;
                    AssertInvestmentResourcesEqual(session, before);
                    Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouseBefore));
                    CaptureWarehouseTrading(controller, "09_Account_Limit", "账户溢出拒绝整单，不扣两处库存");
                }
                else
                {
                    Assert.That(revenue, Is.GreaterThan(0m).And.LessThan(1m));
                    Assert.That(ActiveButton("Confirm Warehouse Cargo Sale").IsInteractable(), Is.True);
                    Button.ButtonClickedEvent repeated = ActiveButton("Confirm Warehouse Cargo Sale").onClick;
                    ClickAcceptanceButton("Confirm Warehouse Cargo Sale"); yield return null;
                    repeated.Invoke(); yield return null;
                    before.gold += revenue;
                    AssertInvestmentResourcesEqual(session, before);
                    Assert.That(session.PortWarehouse.Serialize(),
                        Is.EqualTo(warehouseBefore.Replace("断锚港,water,2", "断锚港,water,1")));
                    Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(before.gold));
                    CaptureWarehouseTrading(controller, "08_Fractional_Sale", "本港出售一份水精确入账小数金币，随船与异港货物不变，重复确认不再扣货");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator OpenWarehouseCargoAdjustment(string key)
        {
            yield return ShowPortStorageItem(key);
            ClickAcceptanceButton("Warehouse Storage Row " + key); yield return null;
            ClickAcceptanceButton("Warehouse Adjust Cargo"); yield return null;
            Assert.That(GameObject.Find("Warehouse Cargo Adjustment"), Is.Not.Null);
        }

        private static IEnumerator OpenWarehouseCargoSale(string key)
        {
            yield return ShowPortStorageItem(key);
            ClickAcceptanceButton("Warehouse Storage Row " + key); yield return null;
            ClickAcceptanceButton("Warehouse Sell Cargo"); yield return null;
            Assert.That(GameObject.Find("Warehouse Cargo Sale"), Is.Not.Null);
        }

        private static IEnumerator DragWarehouseSlider(Slider slider, float target)
        {
            Assert.That(slider, Is.Not.Null);
            Assert.That(slider.IsInteractable(), Is.True);
            Assert.That(target, Is.InRange(slider.minValue, slider.maxValue));
            Assert.That(slider.direction, Is.EqualTo(Slider.Direction.LeftToRight));
            yield return null;
            Canvas.ForceUpdateCanvases();
            RectTransform handle = slider.handleRect;
            Assert.That(handle, Is.Not.Null);
            RectTransform track = (RectTransform)handle.parent;
            Canvas canvas = slider.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 start = RectTransformUtility.WorldToScreenPoint(camera, handle.TransformPoint(handle.rect.center));
            float normalized = Mathf.InverseLerp(slider.minValue, slider.maxValue, target);
            Vector2 end = RectTransformUtility.WorldToScreenPoint(camera,
                track.TransformPoint(new Vector2(Mathf.Lerp(track.rect.xMin, track.rect.xMax, normalized), track.rect.center.y)));
            var pointer = new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left, position = start, pressPosition = start,
                pointerDrag = slider.gameObject, useDragThreshold = false
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "The visible slider handle must accept a real pointer hit.");
            Assert.That(hits[0].gameObject.GetComponentInParent<Slider>(), Is.SameAs(slider),
                "Another UI graphic covers the slider: " + hits[0].gameObject.name);
            pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            pointer.dragging = true;
            for (int step = 1; step <= 3; step++)
            {
                Vector2 next = Vector2.Lerp(start, end, step / 3f);
                pointer.delta = next - pointer.position; pointer.position = next;
                ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
                yield return null;
            }
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(slider.value, Is.EqualTo(target).Within(.001f), "The pointer drag must select the intended integer, not mutate slider.value directly.");
        }

        private static void CaptureWarehouseTrading(CoreGameplayDemoController controller, string stage, string detail)
        {
            string root = GameObject.Find("Warehouse Cargo Sale") != null ? "Warehouse Cargo Sale" :
                GameObject.Find("Warehouse Cargo Adjustment") != null ? "Warehouse Cargo Adjustment" :
                GameObject.Find("Warehouse Item Actions") != null ? "Warehouse Item Actions" : "Warehouse Panel";
            NavalBattleVisualCapture.Capture(controller, "WarehouseTrading_" + stage + ".png", "仓储交易 UI 夹具 · " + detail, root);
        }
    }
}
