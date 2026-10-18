using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator WarehouseTreasureDrag_PlacesMovesReplacesAndReturnsWithoutLosingOwnership()
        {
            string first = CoreWarehouseCollection.TutorialTreasureId;
            string second = CoreWarehouseCollection.BoatswainBellTreasureId;
            yield return LoadWarehouseDragFixture(new[] { first, second });
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            decimal gold = controller.Session.ProfileGold;
            int hull = controller.Session.Hull;
            int[] cargo = WarehouseCargoSnapshot(controller);

            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Treasure " + first),
                WarehouseDragPoint("Warehouse Display Slot 5"), "65_Warehouse_Treasure_Drag_To_Slot.png");
            Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.EqualTo(first));
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.Empty,
                "The pointer destination wins over the previously selected display slot.");
            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Treasure " + second),
                WarehouseDragPoint("Warehouse Display Slot 2"));
            Assert.That(controller.WarehouseCollection.DisplayedAt(2), Is.EqualTo(second));

            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Display Slot 5"),
                WarehouseDragPoint("Warehouse Display Slot 2"));
            Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.Empty);
            Assert.That(controller.WarehouseCollection.DisplayedAt(2), Is.EqualTo(first));
            Assert.That(controller.WarehouseCollection.IsDisplayed(second), Is.False,
                "Replacing an exhibit returns the previous keepsake to storage.");
            CollectionAssert.AreEqual(new[] { first, second }, controller.WarehouseCollection.OwnedIds);
            AssertWarehouseDragSaved(controller);

            // Dropping onto the same occupied slot is also safe and preserves uniqueness.
            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Display Slot 2"),
                WarehouseDragPoint("Warehouse Display Slot 2"));
            Assert.That(Enumerable.Range(0, CoreWarehouseCollection.DisplaySlotCount)
                .Count(slot => controller.WarehouseCollection.DisplayedAt(slot) == first), Is.EqualTo(1));

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.WarehouseCollection.DisplayedAt(2), Is.EqualTo(first));
            CollectionAssert.AreEqual(new[] { first, second }, controller.WarehouseCollection.OwnedIds);
            ClickWarehouseButton("Port Hotspot Warehouse");
            yield return null;
            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Display Slot 2"),
                WarehouseDragPoint("Warehouse Treasure Details"));
            Assert.That(controller.WarehouseCollection.IsDisplayed(first), Is.False);
            Assert.That(controller.WarehouseCollection.IsDisplayed(second), Is.False);
            AssertWarehouseDragSaved(controller);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayKey), Is.EqualTo(",,,,,"));

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CollectionAssert.AreEqual(new[] { first, second }, controller.WarehouseCollection.OwnedIds);
            Assert.That(controller.WarehouseCollection.IsDisplayed(first), Is.False);
            Assert.That(controller.WarehouseCollection.IsDisplayed(second), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.Session.Hull, Is.EqualTo(hull));
            CollectionAssert.AreEqual(cargo, WarehouseCargoSnapshot(controller));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WarehouseTreasureDrag_InvalidTargetsAndCancellationPreserveTheExhibit()
        {
            string id = CoreWarehouseCollection.TutorialTreasureId;
            yield return LoadWarehouseDragFixture(new[] { id }, id + ",,,,,");
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string savedDisplay = controller.WarehouseCollection.ExportDisplay();

            // The inventory representation is not a removal handle, even when already displayed.
            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Treasure " + id),
                WarehouseDragPoint("Warehouse Treasure Details"));
            Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(savedDisplay));

            RectTransform cabinet = GameObject.Find("Warehouse Display Cabinet").GetComponent<RectTransform>();
            Vector2 shelfEdge = WarehouseDragScreenPoint(cabinet,
                new Vector2(cabinet.rect.center.x, cabinet.rect.yMin + 8f));
            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Display Slot 0"), shelfEdge);
            Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(savedDisplay),
                "An imprecise drop inside the cabinet must not return the exhibit to storage.");

            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Display Slot 0"),
                new Vector2(-20f, -20f));
            Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(savedDisplay),
                "Leaving the game viewport cancels instead of removing a keepsake.");

            for (int cancellation = 0; cancellation < 2; cancellation++)
            {
                Button source = RequireWarehouseButton("Warehouse Display Slot 0");
                PointerEventData pointer = BeginWarehouseTreasureDrag(source, WarehouseDragPoint("Warehouse Display Slot 5"));
                if (cancellation == 0)
                    ExecuteEvents.Execute(source.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                else source.gameObject.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
                Assert.That(GameObject.Find("Warehouse Treasure Drag Ghost"), Is.Null);
                EndWarehouseTreasureDrag(source, pointer);
                yield return null;
                Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(savedDisplay));
            }

            // Escape can cancel the pressed gesture before Unity crosses its drag threshold.
            Button pressed = RequireWarehouseButton("Warehouse Display Slot 0");
            PointerEventData cancelledPress = WarehouseDragPointer(pressed);
            cancelledPress.pointerDrag = pressed.gameObject;
            ExecuteEvents.Execute(pressed.gameObject, cancelledPress, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(pressed.gameObject, cancelledPress, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(pressed.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            cancelledPress.position = WarehouseDragPoint("Warehouse Display Slot 4");
            ExecuteEvents.Execute(pressed.gameObject, cancelledPress, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(pressed.gameObject, cancelledPress, ExecuteEvents.dragHandler);
            Assert.That(GameObject.Find("Warehouse Treasure Drag Ghost"), Is.Null);
            EndWarehouseTreasureDrag(pressed, cancelledPress);
            yield return null;
            Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(savedDisplay));
            AssertWarehouseDragSaved(controller);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WarehouseTreasureDrag_DisableRefreshAndPagingInvalidateTheOldGesture()
        {
            string[] ids = CoreWarehouseCollection.Catalog.Take(4).Select(item => item.Id).ToArray();
            yield return LoadWarehouseDragFixture(ids, ids[0] + ",,,,,");
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string savedDisplay = controller.WarehouseCollection.ExportDisplay();

            Button source = RequireWarehouseButton("Warehouse Display Slot 0");
            PointerEventData pointer = BeginWarehouseTreasureDrag(source, WarehouseDragPoint("Warehouse Display Slot 1"));
            source.gameObject.SetActive(false);
            Assert.That(GameObject.Find("Warehouse Treasure Drag Ghost"), Is.Null);
            source.gameObject.SetActive(true);
            EndWarehouseTreasureDrag(source, pointer);
            yield return null;
            Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(savedDisplay));

            source = RequireWarehouseButton("Warehouse Display Slot 0");
            pointer = BeginWarehouseTreasureDrag(source, WarehouseDragPoint("Warehouse Display Slot 1"));
            ClickWarehouseButton("Warehouse Display Slot 2");
            yield return null;
            Assert.That(RequireWarehouseButton("Warehouse Display Slot 0"), Is.SameAs(source),
                "The refresh rebinds the retained display slot instead of replacing it.");
            Assert.That(GameObject.Find("Warehouse Treasure Drag Ghost"), Is.Null);
            EndWarehouseTreasureDrag(source, pointer);
            yield return null;
            Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(savedDisplay));

            source = RequireWarehouseButton("Warehouse Treasure " + ids[0]);
            pointer = BeginWarehouseTreasureDrag(source, WarehouseDragPoint("Warehouse Display Slot 3"));
            ClickWarehouseButton("Warehouse Treasure Page Next");
            yield return null;
            Assert.That(source.gameObject.activeInHierarchy, Is.False);
            Assert.That(GameObject.Find("Warehouse Treasure Drag Ghost"), Is.Null);
            ClickWarehouseButton("Warehouse Treasure Page Previous");
            yield return null;
            Assert.That(RequireWarehouseButton("Warehouse Treasure " + ids[0]), Is.SameAs(source));
            EndWarehouseTreasureDrag(source, pointer, false);
            yield return null;
            Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(savedDisplay),
                "Revisiting a cached inventory card must not revive its previous gesture.");
            AssertWarehouseDragSaved(controller);

            // A new pointer press is still usable after the old gesture has been rejected.
            yield return DragWarehouseTreasure(source, WarehouseDragPoint("Warehouse Display Slot 3"));
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.Empty);
            Assert.That(controller.WarehouseCollection.DisplayedAt(3), Is.EqualTo(ids[0]));
            AssertWarehouseDragSaved(controller);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WarehouseTreasureDrag_EscapeCancelsWithoutLeavingTheCollectionRoom()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                string id = CoreWarehouseCollection.TutorialTreasureId;
                yield return LoadWarehouseDragFixture(new[] { id }, id + ",,,,,");
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                GameObject panel = GameObject.Find("Warehouse Panel");
                Button source = RequireWarehouseButton("Warehouse Display Slot 0");
                PointerEventData pointer = BeginWarehouseTreasureDrag(source, WarehouseDragPoint("Warehouse Display Slot 5"));
                yield return null;
                EventSystem.current.SetSelectedGameObject(source.gameObject);
                yield return input.Press(3);
                Assert.That(GameObject.Find("Warehouse Treasure Drag Ghost"), Is.Null);
                Assert.That(GameObject.Find("Warehouse Panel"), Is.SameAs(panel),
                    "Escape cancels only the held keepsake, even while the key stays down.");
                EndWarehouseTreasureDrag(source, pointer);
                yield return null;
                Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(id));
                Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.Empty);
                AssertWarehouseDragSaved(controller);
                yield return input.Press();
                Assert.That(GameObject.Find("Warehouse Panel"), Is.Null);
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator WarehouseTreasureDrag_TapsKeepSelectionActionsAndEmptySlotsCannotDrag()
        {
            string first = CoreWarehouseCollection.TutorialTreasureId;
            string second = CoreWarehouseCollection.BoatswainBellTreasureId;
            yield return LoadWarehouseDragFixture(new[] { first, second });
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Button empty = RequireWarehouseButton("Warehouse Display Slot 4");
            WarehouseTreasureDrag emptyDrag = empty.GetComponent<WarehouseTreasureDrag>();
            Assert.That(emptyDrag, Is.Not.Null);
            PointerEventData pointer = WarehouseDragPointer(empty);
            pointer.pointerDrag = empty.gameObject;
            ExecuteEvents.Execute(empty.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(empty.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(empty.gameObject, pointer, ExecuteEvents.beginDragHandler);
            Assert.That(GameObject.Find("Warehouse Treasure Drag Ghost"), Is.Null,
                "An empty recess is a destination, not a draggable item.");
            ExecuteEvents.Execute(empty.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(empty.gameObject, pointer, ExecuteEvents.endDragHandler);

            TapWarehouseTreasureButton(RequireWarehouseButton("Warehouse Treasure " + second));
            yield return null;
            Assert.That(GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text,
                Is.EqualTo(CoreWarehouseCollection.Catalog.Single(item => item.Id == second).Name));
            TapWarehouseTreasureButton(RequireWarehouseButton("Warehouse Display Slot 4"));
            yield return null;
            Assert.That(controller.WarehouseCollection.IsDisplayed(second), Is.False);
            ClickWarehouseButton("Warehouse Display Selected");
            yield return null;
            Assert.That(controller.WarehouseCollection.DisplayedAt(4), Is.EqualTo(second));

            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Treasure " + first),
                WarehouseDragPoint("Warehouse Treasure Details"));
            Assert.That(controller.WarehouseCollection.IsDisplayed(first), Is.False);
            Assert.That(controller.WarehouseCollection.DisplayedAt(4), Is.EqualTo(second));
            Assert.That(GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text,
                Is.EqualTo(CoreWarehouseCollection.Catalog.Single(item => item.Id == second).Name),
                "The release click from an invalid drag must not select the source item.");
            ExecuteEvents.Execute(RequireWarehouseButton("Warehouse Treasure " + first).gameObject,
                new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            Assert.That(GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text,
                Is.EqualTo(CoreWarehouseCollection.Catalog[0].Name),
                "Keyboard submit must work again on the frame after the drag release.");
            TapWarehouseTreasureButton(RequireWarehouseButton("Warehouse Treasure " + second));
            yield return null;
            TapWarehouseTreasureButton(RequireWarehouseButton("Warehouse Treasure " + first));
            yield return null;
            Assert.That(GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text,
                Is.EqualTo(CoreWarehouseCollection.Catalog[0].Name),
                "A fresh tap is not swallowed by the preceding invalid drag.");
            TapWarehouseTreasureButton(RequireWarehouseButton("Warehouse Display Slot 4"));
            yield return null;
            ClickWarehouseButton("Warehouse Remove Selected");
            yield return null;
            Assert.That(controller.WarehouseCollection.IsDisplayed(second), Is.False);
            CollectionAssert.AreEqual(new[] { first, second }, controller.WarehouseCollection.OwnedIds);
            AssertWarehouseDragSaved(controller);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WarehouseTreasureDrag_DirectTutorialDropCompletesTheDisplayLesson()
        {
            string id = CoreWarehouseCollection.TutorialTreasureId;
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 5);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, id);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayKey, ",,,,,");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Warehouse));
            ClickWarehouseButton("Port Hotspot Warehouse");
            yield return null;
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.SelectTreasure));
            yield return DragWarehouseTreasure(RequireWarehouseButton("Warehouse Treasure " + id),
                WarehouseDragPoint("Warehouse Display Slot 0"));
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(id));
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Shipyard));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey), Is.False);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey), Is.EqualTo(1));
            AssertWarehouseDragSaved(controller);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadWarehouseDragFixture(string[] owned, string displayed = ",,,,,")
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, string.Join(",", owned));
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayKey, displayed);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            ClickWarehouseButton("Port Hotspot Warehouse");
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertWarehouseDragSaved(CoreGameplayDemoController controller)
        {
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayKey),
                Is.EqualTo(controller.WarehouseCollection.ExportDisplay()));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayPortKey(controller.Session.CurrentPort)),
                Is.EqualTo(controller.WarehouseCollection.ExportDisplay()));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedPortKey(controller.Session.CurrentPort)),
                Is.EqualTo(controller.WarehouseCollection.ExportOwned()));
        }

        private static Vector2 WarehouseDragScreenPoint(RectTransform rect, Vector2? localPoint = null)
        {
            Canvas.ForceUpdateCanvases();
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(localPoint ?? rect.rect.center));
        }

        private static Vector2 WarehouseDragPoint(string name) =>
            WarehouseDragScreenPoint(GameObject.Find(name).GetComponent<RectTransform>());

        private static PointerEventData WarehouseDragPointer(Button button)
        {
            Assert.That(button, Is.Not.Null);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                position = WarehouseDragScreenPoint(button.GetComponent<RectTransform>()),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button),
                button.name + " must receive the visible pointer hit.");
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
            pointer.pressPosition = pointer.position;
            pointer.pointerPress = pointer.rawPointerPress = button.gameObject;
            return pointer;
        }

        private static PointerEventData BeginWarehouseTreasureDrag(Button source, Vector2 destination)
        {
            Assert.That(source.GetComponent<WarehouseTreasureDrag>(), Is.Not.Null, source.name);
            PointerEventData pointer = WarehouseDragPointer(source);
            pointer.pointerDrag = source.gameObject;
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.beginDragHandler);
            GameObject ghost = GameObject.Find("Warehouse Treasure Drag Ghost");
            Assert.That(ghost, Is.Not.Null, "The held keepsake has a visible drag preview.");
            Assert.That(ghost.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(ghost.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
            pointer.dragging = true;
            pointer.delta = destination - pointer.position;
            pointer.position = destination;
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.dragHandler);
            return pointer;
        }

        private static void EndWarehouseTreasureDrag(Button source, PointerEventData pointer, bool assertReleaseSuppression = true)
        {
            WarehouseTreasureDrag drag = source.GetComponent<WarehouseTreasureDrag>();
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            if (assertReleaseSuppression)
                Assert.That(drag.SuppressClick, Is.True, "PointerUp must not release drag-click suppression before EndDrag.");
            ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(pointer.eligibleForClick, Is.False);
            if (assertReleaseSuppression) Assert.That(drag.SuppressClick, Is.True);
            pointer.dragging = false;
            // Stress the same-frame release click. Inactive paged-away sources have
            // already invalidated eligibleForClick, so their delayed release has no click.
            if (assertReleaseSuppression)
                ExecuteEvents.Execute(source.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(GameObject.Find("Warehouse Treasure Drag Ghost"), Is.Null);
        }

        private static IEnumerator DragWarehouseTreasure(Button source, Vector2 destination, string captureName = null)
        {
            PointerEventData pointer = BeginWarehouseTreasureDrag(source, destination);
            yield return null;
            if (captureName != null)
                NavalBattleVisualCapture.Capture(Object.FindFirstObjectByType<CoreGameplayDemoController>(), captureName,
                    "拖动藏品到第六展位；指针随行预览与目标提示，释放后保存陈列位置", "Warehouse Panel");
            EndWarehouseTreasureDrag(source, pointer);
            yield return null;
        }

        private static void TapWarehouseTreasureButton(Button button)
        {
            PointerEventData pointer = WarehouseDragPointer(button);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(button.GetComponent<WarehouseTreasureDrag>().SuppressClick, Is.False);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
