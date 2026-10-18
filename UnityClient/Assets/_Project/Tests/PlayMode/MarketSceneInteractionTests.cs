using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator MarketScene_VisibleShopsSwitchOppositeDrawersAndOccludedShopsCannotReceiveClicks()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return MarketSceneClick("Port Hotspot Market");
            Assert.That(GameObject.Find("Market Item Detail"), Is.Null, "Enter the market with the complete scene visible.");
            NavalBattleVisualCapture.Capture(controller,"MarketScene_01_Main.png","市场实景，真实PlayMode交互状态","Market Panel");
            decimal gold = controller.Session.ProfileGold;
            yield return MarketSceneClick("Market Shop Supplies");
            var drawer = GameObject.Find("Market Item Detail").GetComponent<RectTransform>();
            Assert.That(drawer.anchoredPosition.x, Is.GreaterThan(0));
            Assert.That(GameObject.Find("Market Quantity Input water"), Is.Not.Null);
            NavalBattleVisualCapture.Capture(controller,"MarketScene_02_Supplies_Right.png","左侧补给铺展开右侧交易面板","Market Panel");
            Assert.That(MarketSceneTopControlAt("Market Shop Ordnance"), Is.Not.EqualTo("Market Shop Ordnance"),
                "The opaque drawer must intercept the obscured shop's pointer.");
            yield return MarketSceneClick("Market Shop Supplies");
            Assert.That(GameObject.Find("Market Item Detail"), Is.Not.Null, "Clicking the current shop must not close it.");
            yield return MarketSceneClick("Market Shop Medicine");
            Assert.That(GameObject.Find("Market Quantity Input medicine"), Is.Not.Null);
            Assert.That(GameObject.Find("Market Item Detail").GetComponent<RectTransform>().anchoredPosition.x, Is.GreaterThan(0));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            yield return MarketSceneClick("Market Scene Empty Space");
            Assert.That(GameObject.Find("Market Item Detail"), Is.Null);
            yield return MarketSceneClick("Market Shop Ordnance");
            Assert.That(GameObject.Find("Market Item Detail").GetComponent<RectTransform>().anchoredPosition.x, Is.LessThan(0));
            NavalBattleVisualCapture.Capture(controller,"MarketScene_03_Ordnance_Left.png","右侧军需铺展开左侧交易面板","Market Panel");
            Assert.That(MarketSceneTopControlAt("Market Shop Supplies"), Is.Not.EqualTo("Market Shop Supplies"));
            yield return MarketSceneClick("Close Market Shop");
            yield return MarketSceneClick("Market Shop Shipwright");
            Assert.That(GameObject.Find("Market Quantity Input wood"), Is.Not.Null);
            Assert.That(GameObject.Find("Market Item Detail").GetComponent<RectTransform>().anchoredPosition.x, Is.LessThan(0));
            yield return MarketSceneClick("Return To Port Hub");
            Assert.That(ActiveButton("Port Hotspot Market"), Is.Not.Null, "Header back returns directly to the port.");
            Assert.That(GameObject.Find("Market Panel"), Is.Null);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketScene_SwitchingInvalidatesQuotesAndEscapeCancelsChoiceBeforeClosingShop()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            InputSettings originalSettings = InputSystem.settings;
            HideFlags originalFlags = originalSettings.hideFlags;
            if (originalFlags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.DontSave;
            InputSettings temporarySettings = Object.Instantiate(originalSettings);
            temporarySettings.hideFlags = HideFlags.DontSave;
            temporarySettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            temporarySettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            temporarySettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = temporarySettings;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                yield return MarketSceneClick("Port Hotspot Market");
                yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                yield return MarketSceneClick("Market Shop Supplies");
                yield return EnterMarketQuantity("water", "7");
                Button.ButtonClickedEvent staleConfirm = ActiveButton("Confirm Market Buy water").onClick;
                decimal gold = controller.Session.ProfileGold;
                yield return MarketSceneClick("Market Shop Medicine");
                staleConfirm.Invoke(); yield return null;
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null, "A stale quote cannot open a purchase after switching shop.");
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.Zero);
                yield return MarketSceneClick("Market Shop Supplies");
                Assert.That(MarketQuantityField("water").value, Is.EqualTo(PortMarketCatalog.PurchaseStep(PortMarketItem.Water)),
                    "Switching shop discards an unconfirmed quantity.");
                Assert.That(controller.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Ask));
                yield return EnterMarketQuantity("water", "7");
                yield return MarketSceneClick("Confirm Market Buy water");
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Not.Null);
                var staleDestination = ActiveButton("Market Buy To Warehouse").onClick;
                Assert.That(MarketSceneTopControlAt("Market Shop Medicine"), Is.Not.EqualTo("Market Shop Medicine"));
                Assert.That(MarketSceneTopControlAt("Return To Port Hub"), Is.Not.EqualTo("Return To Port Hub"));
                yield return MarketScenePressEscape(keyboard);
                Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null, "Escape cancels only the top-level choice first.");
                Assert.That(GameObject.Find("Market Item Detail"), Is.Not.Null);
                Assert.That(MarketSceneTopControlAt("Return To Port Hub"), Is.EqualTo("Return To Port Hub"), "Cancellation restores header input.");
                NavalBattleVisualCapture.Capture(controller,"MarketScene_04_Cancelled_Choice.png","Esc取消收货选择，保留当前店铺与可见反馈","Market Panel");
                staleDestination.Invoke(); yield return null;
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.PortWarehouse.GetAmount(controller.Session.CurrentPort, "water"), Is.Zero);
                yield return MarketScenePressEscape(keyboard);
                Assert.That(GameObject.Find("Market Item Detail"), Is.Null);
                Assert.That(GameObject.Find("Market Panel"), Is.Not.Null, "Escape closes the shop without leaving the market.");
                Assert.That(controller.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Ask));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalSettings;
                originalSettings.hideFlags = originalFlags;
                Object.Destroy(temporarySettings);
                RestoreProvisionLedger(ledger);
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MarketScene_TypedQuantityCommitsOnClickAndInvalidTextCannotReusePreviousQuote()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                SetProvisionClock(controller, () => now);
                yield return MarketSceneClick("Port Hotspot Market");
                yield return MarketSceneClick("Market Shop Supplies");
                decimal gold = controller.Session.ProfileGold;
                decimal cost = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort,PortMarketItem.Water,now,7);
                Assert.That(cost, Is.LessThan(1m), "Seven water units retain a fractional gold quote.");
                yield return MarketSceneClick("Market Quantity Input water");
                InputField field = GameObject.Find("Market Quantity Input water").GetComponent<InputField>();
                Assert.That(field.isFocused, Is.True);
                field.text = "7";
                yield return MarketSceneClick("Confirm Market Buy water");
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(7), "Pointer-down must commit the input before confirmation reads the quote.");
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold-cost));
                Assert.That(MarketQuantityField("water").value, Is.EqualTo(7));
                NavalBattleVisualCapture.Capture(controller,"MarketScene_05_Purchase_Feedback.png","真实输入7份淡水并成交，金币精确扣除且反馈可见","Market Panel");
                gold = controller.Session.ProfileGold;

                yield return MarketSceneClick("Market Quantity Input water");
                field.text = "";
                yield return MarketSceneClick("Market Inventory Toggle");
                Assert.That(MarketQuantityField("water").value, Is.Zero);
                Assert.That(ActiveButton("Confirm Market Buy water").interactable, Is.False);
                ActiveButton("Confirm Market Buy water").onClick.Invoke();
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(7));

                // Normal typing is capped to ten characters. The end-edit boundary
                // must also reject malformed external/accessibility submissions.
                field.onEndEdit.Invoke("999999999999999999999999999999");
                yield return null;
                Assert.That(MarketQuantityField("water").value, Is.Zero);
                Assert.That(ActiveButton("Confirm Market Buy water").interactable, Is.False);
                yield return MarketSceneClick("Market Quantity Input water");
                field.text = "7";
                yield return MarketSceneClick("Market Shop Medicine");
                Assert.That(GameObject.Find("Market Quantity Input medicine"), Is.Not.Null, "Committing text cannot swallow a shop switch.");
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(7));
                LogAssert.NoUnexpectedReceived();
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        private static IEnumerator MarketScenePressEscape(Keyboard keyboard)
        {
            keyboard.MakeCurrent();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }

        private static PointerEventData MarketScenePointerAt(string name, out RaycastResult hit)
        {
            Canvas.ForceUpdateCanvases();
            GameObject target = GameObject.Find(name);
            Assert.That(target, Is.Not.Null, name);
            Canvas canvas = target.GetComponentInParent<Canvas>();
            var rect = target.GetComponent<RectTransform>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = point, pressPosition = point };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, name + " must hit a rendered graphic");
            hit = hits[0];
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hit;
            return pointer;
        }

        private static string MarketSceneTopControlAt(string name)
        {
            MarketScenePointerAt(name, out RaycastResult hit);
            GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            return receiver == null ? hit.gameObject.name : receiver.name;
        }

        private static IEnumerator MarketSceneClick(string name)
        {
            PointerEventData pointer = MarketScenePointerAt(name, out RaycastResult hit);
            GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            Assert.That(receiver, Is.Not.Null, name + " has no click receiver at its visible center");
            Assert.That(receiver.name, Is.EqualTo(name), name + " is covered by " + hit.gameObject.name);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
