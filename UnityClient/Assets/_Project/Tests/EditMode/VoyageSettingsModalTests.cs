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
        [TestCase(CoreGameplayPreviewScreen.Battle, false)]
        [TestCase(CoreGameplayPreviewScreen.Settlement, false)]
        public void VoyageSettings_OnlyAppearsOnSupportedScreensAndLeavesHeaderSpace(CoreGameplayPreviewScreen screen, bool expected)
        {
            GameObject host = new GameObject("Voyage Settings Header Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                Button button = host.GetComponentsInChildren<Button>().SingleOrDefault(item => item.name == "Voyage Settings");
                Assert.That(button != null, Is.EqualTo(expected));
                Button cargo = host.GetComponentsInChildren<Button>().SingleOrDefault(item => item.name == "Voyage Cargo");
                Assert.That(cargo != null, Is.EqualTo(expected));
                if (!expected) return;
                RectTransform header = (RectTransform)button.transform.parent;
                Canvas.ForceUpdateCanvases();
                Rect buttonBounds = Bounds(header, (RectTransform)button.transform);
                foreach (string name in new[] { "Voyage Header Title", "Voyage Header Status", "Lookout Time Ribbon", "Voyage Cargo" })
                    Assert.That(buttonBounds.Overlaps(Bounds(header, (RectTransform)header.Find(name))), Is.False, name);
                Assert.That(cargo.transform.parent, Is.SameAs(header));
                Rect cargoBounds = Bounds(header, (RectTransform)cargo.transform);
                Assert.That(buttonBounds.xMin - cargoBounds.xMax, Is.GreaterThanOrEqualTo(11.99f), "Cargo sits next to Settings with a clear gap.");
                Assert.That(header.rect.xMax - buttonBounds.xMax, Is.EqualTo(30f).Within(.01f), "Settings stays near the right edge.");
                Assert.That(buttonBounds.yMin, Is.GreaterThan(-37f), "Leave the tutorial instruction band clear.");
                Assert.That(buttonBounds.yMax, Is.LessThan(39f), "Leave the tutorial objective band clear.");
                button.onClick.Invoke();
                Assert.That(Read<RectTransform>(controller, "portSettingsOverlay"), Is.Not.Null);
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
