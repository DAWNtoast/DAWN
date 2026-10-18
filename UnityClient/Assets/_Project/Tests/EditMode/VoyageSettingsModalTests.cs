using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class VoyageSettingsModalTests
    {
        [TestCase(CoreGameplayPreviewScreen.Navigation, true)]
        [TestCase(CoreGameplayPreviewScreen.Encounter, true)]
        [TestCase(CoreGameplayPreviewScreen.Battle, true)]
        [TestCase(CoreGameplayPreviewScreen.Settlement, false)]
        public void VoyageSettings_OnlyAppearsOnSupportedScreensAndLeavesHeaderSpace(CoreGameplayPreviewScreen screen, bool expected)
        {
            GameObject host = new GameObject("Voyage Settings Header Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                bool battle = screen == CoreGameplayPreviewScreen.Battle;
                Button button = host.GetComponentsInChildren<Button>().SingleOrDefault(item => item.name == (battle ? "Battle Settings" : "Voyage Settings"));
                Assert.That(button != null, Is.EqualTo(expected));
                Button cargo = host.GetComponentsInChildren<Button>().SingleOrDefault(item => item.name == "Voyage Cargo");
                Assert.That(cargo != null, Is.EqualTo(expected && !battle));
                if (!expected) return;
                Button adjacent = battle ? ButtonNamed(host, "Battle Deck") : cargo;
                RectTransform header = (RectTransform)button.transform.parent;
                Canvas.ForceUpdateCanvases();
                Rect buttonBounds = Bounds(header, (RectTransform)button.transform);
                string[] readouts = battle
                    ? new[] { "Voyage Header Title", "Voyage Header Status", "Alignment Inset", "Ship Status Inset", "Battle Deck" }
                    : new[] { "Voyage Header Title", "Voyage Header Status", "Lookout Time Ribbon", "Voyage Cargo" };
                foreach (string name in readouts)
                    Assert.That(buttonBounds.Overlaps(Bounds(header, (RectTransform)header.Find(name))), Is.False, name);
                Assert.That(adjacent.transform.parent, Is.SameAs(header));
                Rect adjacentBounds = Bounds(header, (RectTransform)adjacent.transform);
                Assert.That(buttonBounds.xMin - adjacentBounds.xMax, Is.GreaterThanOrEqualTo(11.99f), "The adjacent control leaves a clear gap before Settings.");
                if (battle)
                {
                    Rect statusBounds = Bounds(header, (RectTransform)header.Find("Ship Status Inset"));
                    Rect alignmentBounds = Bounds(header, (RectTransform)header.Find("Alignment Inset"));
                    Assert.That(adjacentBounds.xMin - statusBounds.xMax, Is.GreaterThanOrEqualTo(15.99f));
                    Assert.That(statusBounds.xMin - alignmentBounds.xMax, Is.GreaterThanOrEqualTo(14.99f));
                    Assert.That(button.GetComponentInChildren<BattleHeaderIconGraphic>().raycastTarget, Is.False);
                    Assert.That(adjacent.GetComponentInChildren<BattleHeaderIconGraphic>().raycastTarget, Is.False);
                }
                Assert.That(header.rect.xMax - buttonBounds.xMax, Is.EqualTo(30f).Within(.01f), "Settings stays near the right edge.");
                Assert.That(buttonBounds.yMin, Is.GreaterThan(-37f), "Leave the tutorial instruction band clear.");
                Assert.That(buttonBounds.yMax, Is.LessThan(39f), "Leave the tutorial objective band clear.");
                button.onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "portSettingsOverlay"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void BattleInspection_TutorialKeepsBothHeaderControlsAvailableWithoutAdvancingBattle()
        {
            GameObject host = new GameObject("Tutorial Battle Inspection Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                Set(controller, "tutorialActive", true);
                typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                var battle = controller.Session.Battle;
                int turn = battle.Turn;
                int[] hand = battle.Hand.ToArray();
                Assert.That(ButtonNamed(host, "Battle Deck").IsInteractable(), Is.True);
                Assert.That(ButtonNamed(host, "Battle Settings").IsInteractable(), Is.True);
                ButtonNamed(host, "Battle Deck").onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "battleDeckOverlay"), Is.Not.Null);
                ButtonNamed(host, "Close Battle Deck").onClick.Invoke();
                ButtonNamed(host, "Battle Settings").onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "portSettingsOverlay"), Is.Not.Null);
                Assert.That(controller.Session.Battle, Is.SameAs(battle));
                Assert.That(battle.Turn, Is.EqualTo(turn));
                CollectionAssert.AreEqual(hand, battle.Hand);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void BattleSettings_RejectsAnimationAndOtherBattleModals()
        {
            GameObject host = new GameObject("Battle Header Settings Guard Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                Button settings = ButtonNamed(host, "Battle Settings");
                Set(controller, "battleAnimationPlaying", true);
                settings.onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "portSettingsOverlay"), Is.Null);
                Set(controller, "battleAnimationPlaying", false);
                var blocker = new GameObject("Battle Modal Guard", typeof(RectTransform));
                blocker.transform.SetParent(host.transform, false);
                foreach (string field in new[] { "boardingCardChoiceOverlay", "battleDeckOverlay" })
                {
                    Set(controller, field, blocker.GetComponent<RectTransform>());
                    settings.onClick.Invoke();
                    Assert.That(Read<RectTransform>(controller, "portSettingsOverlay"), Is.Null, field);
                    Set(controller, field, null);
                }
                settings.onClick.Invoke();
                RectTransform first = Read<RectTransform>(controller, "portSettingsOverlay");
                Assert.That(first, Is.Not.Null);
                settings.onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "portSettingsOverlay"), Is.SameAs(first));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ReturnConfirmation_CancelPreservesVoyageDraftAndRestoresFocus()
        {
            GameObject host = new GameObject("Voyage Return Confirmation Test");
            EventSystem previousEvents = EventSystem.current;
            GameObject previousSelection = previousEvents == null ? null : previousEvents.currentSelectedGameObject;
            GameObject eventsObject = new GameObject("Voyage Return Test EventSystem", typeof(EventSystem));
            EventSystem events = eventsObject.GetComponent<EventSystem>();
            bool registeredForTest = false;
            try
            {
                // EventSystem is not ExecuteAlways. EditMode construction does
                // not invoke OnEnable, and its current setter only reorders
                // registered systems. Pair the real lifecycle calls explicitly.
                var registered = (List<EventSystem>)typeof(EventSystem)
                    .GetField("m_EventSystems", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                if (!registered.Contains(events))
                {
                    typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);
                    registeredForTest = true;
                }
                EventSystem.current = events;
                Assert.That(EventSystem.current, Is.SameAs(events), "The focus fixture must be the registered current EventSystem.");
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull, 0);
                var session = controller.Session;
                var position = session.Position;
                int water = session.Cargo.Water;
                RectTransform page = Read<RectTransform>(controller, "contentRoot");
                int[] childIds = page.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray();
                Button settings = ButtonNamed(host, "Voyage Settings");
                events.SetSelectedGameObject(settings.gameObject);
                settings.onClick.Invoke();
                Slider master = Read<Slider>(controller, "portSettingsMasterSlider");
                master.value = .17f;
                Button returnButton = ButtonNamed(host, "Return To Main Menu");
                returnButton.onClick.Invoke();
                RectTransform confirmation = Read<RectTransform>(controller, "returnMenuConfirmation");
                Assert.That(confirmation, Is.Not.Null);
                Assert.That(ButtonNamed(host, "Apply Port Settings").IsInteractable(), Is.False);
                Assert.That(master.IsInteractable(), Is.False);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(ButtonNamed(host, "Cancel Return To Main Menu").gameObject));
                Assert.That(Read<Text>(controller, "returnMenuMessage").text, Does.Contain("下次可继续航行"));

                // Old settings callbacks may still be queued when the child opens.
                ButtonNamed(host, "Apply Port Settings").onClick.Invoke();
                ButtonNamed(host, "Close Port Settings").onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "returnMenuConfirmation"), Is.SameAs(confirmation));
                Assert.That(Read<RectTransform>(controller, "portSettingsOverlay"), Is.Not.Null);

                // Clicking empty paper must restore focus inside the confirmation.
                events.SetSelectedGameObject(null);
                ExecuteEvents.ExecuteHierarchy(confirmation.gameObject, new PointerEventData(events), ExecuteEvents.pointerDownHandler);
                Assert.That(events.currentSelectedGameObject.transform.IsChildOf(confirmation), Is.True);
                ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.cancelHandler);
                Assert.That(Read<RectTransform>(controller, "returnMenuConfirmation"), Is.Null);
                Assert.That(events.currentSelectedGameObject, Is.SameAs(returnButton.gameObject));
                Assert.That(master.IsInteractable(), Is.True);
                Assert.That(master.value, Is.EqualTo(.17f).Within(.001f));
                Assert.That(controller.Session, Is.SameAs(session));
                Assert.That(session.Position.X, Is.EqualTo(position.X));
                Assert.That(session.Position.Y, Is.EqualTo(position.Y));
                Assert.That(session.Cargo.Water, Is.EqualTo(water));
                Assert.That(page.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray(), Is.EqualTo(childIds));
                ButtonNamed(host, "Close Port Settings").onClick.Invoke();
                Assert.That(events.currentSelectedGameObject, Is.SameAs(settings.gameObject));
            }
            finally
            {
                events.SetSelectedGameObject(null);
                if (registeredForTest)
                    typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);
                Object.DestroyImmediate(eventsObject);
                if (previousEvents != null)
                {
                    EventSystem.current = previousEvents;
                    if (previousSelection != null) previousEvents.SetSelectedGameObject(previousSelection);
                }
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ReturnConfirmation_EditorPreviewNeverSavesOrLoadsAndRejectsStaleConfirmation()
        {
            Assert.That(Application.isPlaying, Is.False);
            GameObject host = new GameObject("Voyage Preview Return Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull, 0);
                var scene = SceneManager.GetActiveScene().handle;
                string gold = PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey, "missing");
                string treasure = PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey, "missing");
                ButtonNamed(host, "Voyage Settings").onClick.Invoke();
                ButtonNamed(host, "Return To Main Menu").onClick.Invoke();
                Button.ButtonClickedEvent stale = ButtonNamed(host, "Confirm Return To Main Menu").onClick;
                stale.Invoke();
                Assert.That(Read<Text>(controller, "returnMenuMessage").text, Does.Contain("不保存进度或切换场景"));
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene));
                Assert.That(PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey, "missing"), Is.EqualTo(gold));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey, "missing"), Is.EqualTo(treasure));
                ButtonNamed(host, "Cancel Return To Main Menu").onClick.Invoke();
                ButtonNamed(host, "Return To Main Menu").onClick.Invoke();
                Text message = Read<Text>(controller, "returnMenuMessage");
                string prompt = message.text;
                stale.Invoke();
                Assert.That(message.text, Is.EqualTo(prompt), "A retired confirmation must not invoke the newly opened dialog.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void VoyageSettings_MotionCannotOpenSettingsOrCommitReturn()
        {
            GameObject host = new GameObject("Voyage Motion Settings Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull, 0);
                Set(controller, "navigationMotionPlaying", true);
                ButtonNamed(host, "Voyage Settings").onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "portSettingsOverlay"), Is.Null);
                Set(controller, "navigationMotionPlaying", false);
                ButtonNamed(host, "Voyage Settings").onClick.Invoke();
                Set(controller, "navigationMotionPlaying", true);
                ButtonNamed(host, "Return To Main Menu").onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "returnMenuConfirmation"), Is.Null);
                Assert.That(Read<Text>(controller, "portSettingsHint").text, Does.Contain("航行动画"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static Rect Bounds(RectTransform parent, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }
        private static Button ButtonNamed(GameObject host, string name) => host.GetComponentsInChildren<Button>().Single(item => item.name == name);
        private static T Read<T>(CoreGameplayDemoController controller, string name) =>
            (T)typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        private static void Set(CoreGameplayDemoController controller, string name, object value) =>
            typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
    }
}
