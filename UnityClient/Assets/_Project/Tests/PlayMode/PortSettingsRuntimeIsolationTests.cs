using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Exercises runtime UI lifetime without activating the gameplay controller's Awake,
    /// applying settings, starting audio, or writing any PlayerPrefs/System display state.
    /// The inactive controller builds an independent, active canvas only for these tests.
    /// </summary>
    public sealed class PortSettingsRuntimeIsolationTests
    {
        private GameObject host;
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private CoreGameplayDemoController controller;
        private RectTransform page;
        private Button underlyingButton;
        private EventSystem events;
        private EventSystem previousEvents;
        private GameObject previousSelection;
        private Font testFont;
        private SettingsSnapshot settingsBefore;

        [SetUp]
        public void CreateIsolatedUi()
        {
            Assert.That(Application.isPlaying, Is.True);
            settingsBefore = SettingsSnapshot.Read();
            previousEvents = EventSystem.current;
            previousSelection = previousEvents == null ? null : previousEvents.currentSelectedGameObject;

            host = new GameObject("Inactive Port Settings Test Controller");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null, "Gameplay Awake must never run in this isolated test.");
            testFont = (Font)Invoke("CreateInterfaceFont");
            WriteField("interfaceFont", testFont);
            Invoke("BuildCanvas", "Isolated Port Settings Test Canvas");
            page = ReadField<RectTransform>("contentRoot");
            canvasObject = page.parent.gameObject;
            canvasObject.transform.SetParent(null, false);

            GameObject underlay = new GameObject("Underlying Draft Control", typeof(RectTransform), typeof(Image), typeof(Button));
            underlay.transform.SetParent(page, false);
            underlyingButton = underlay.GetComponent<Button>();
            underlyingButton.targetGraphic = underlay.GetComponent<Image>();
            eventSystemObject = new GameObject("Isolated Port Settings EventSystem", typeof(EventSystem));
            events = eventSystemObject.GetComponent<EventSystem>();
            EventSystem.current = events;
            events.SetSelectedGameObject(underlay);
        }

        [UnityTearDown]
        public IEnumerator DisposeIsolatedUi()
        {
            if (controller != null) Invoke("ClosePortSettings");
            if (events != null) events.SetSelectedGameObject(null);
            if (previousEvents != null)
            {
                EventSystem.current = previousEvents;
                if (previousSelection != null && previousSelection.activeInHierarchy)
                    previousEvents.SetSelectedGameObject(previousSelection);
            }
            if (canvasObject != null) Object.Destroy(canvasObject);
            if (eventSystemObject != null) Object.Destroy(eventSystemObject);
            if (host != null) Object.Destroy(host);
            // CreateInterfaceFont now borrows the embedded Resources font.
            // Destroying it would invalidate every other preview and test.
            testFont = null;
            yield return null;
            settingsBefore.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator SameFrameCloseAndReopen_KeepsInputBarrierAndDraftsAfterFrameEnd()
        {
            VoyageSupplyOrder order = ReadField<VoyageSupplyOrder>("pendingSupplyOrder");
            order.Water = 70;
            List<int> deck = ReadField<List<int>>("configuredBattleDeck");
            int[] deckBefore = deck.ToArray();
            List<int> dispatchCrew = ReadField<List<int>>("selectedDispatchCrew");
            dispatchCrew.Add(1);
            dispatchCrew.Add(5);
            WriteField("selectedDispatchDurationHours", 7);
            int originalPageChildCount = page.childCount;

            Invoke("OpenPortSettings");
            CanvasGroup firstBarrier = page.GetComponent<CanvasGroup>();
            Invoke("ClosePortSettings");
            Invoke("OpenPortSettings");
            CanvasGroup reopenedBarrier = page.GetComponent<CanvasGroup>();
            Assert.That(reopenedBarrier, Is.SameAs(firstBarrier));
            Assert.That(underlyingButton.IsInteractable(), Is.False);
            yield return null; // Flush the first overlay's deferred destruction.

            Assert.That(page.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
            Assert.That(page.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(page.GetComponent<CanvasGroup>().interactable, Is.False);
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(underlyingButton.IsInteractable(), Is.False,
                "A same-frame reopen must not restore the page's keyboard/controller actions.");
            Assert.That(ReadField<RectTransform>("portSettingsOverlay"), Is.Not.Null);
            Assert.That(canvasObject.GetComponentsInChildren<Transform>().Count(item => item.name == "Port Settings Overlay"), Is.EqualTo(1));

            Invoke("ClosePortSettings");
            Assert.That(underlyingButton.IsInteractable(), Is.True);
            Assert.That(page.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(underlyingButton.gameObject));
            Assert.That(page.childCount, Is.EqualTo(originalPageChildCount));
            Assert.That(order.Water, Is.EqualTo(70));
            Assert.That(deck, Is.EqualTo(deckBefore));
            Assert.That(dispatchCrew, Is.EqualTo(new[] { 1, 5 }));
            Assert.That(ReadField<int>("selectedDispatchDurationHours"), Is.EqualTo(7));
            Assert.That(controller.Session, Is.Null);
            settingsBefore.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator BlankParchmentClick_RestoresModalFocusAndAllowsCancel()
        {
            Invoke("OpenPortSettings");
            RectTransform modal = ReadField<RectTransform>("portSettingsOverlay");
            Transform sheet = modal.GetComponentsInChildren<Transform>().Single(item => item.name == "Port Settings Sheet");
            ReadField<Slider>("portSettingsMusicSlider").value = 0.17f;

            // InputSystemUIInputModule deselects empty backgrounds before pointerDown.
            events.SetSelectedGameObject(null);
            ExecuteEvents.ExecuteHierarchy(sheet.gameObject, new PointerEventData(events)
            {
                button = PointerEventData.InputButton.Left,
            }, ExecuteEvents.pointerDownHandler);
            GameObject selected = events.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.transform.IsChildOf(modal), Is.True);
            ExecuteEvents.Execute(selected, new BaseEventData(events), ExecuteEvents.cancelHandler);

            Assert.That(ReadField<RectTransform>("portSettingsOverlay"), Is.Null);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(underlyingButton.gameObject));
            Assert.That(underlyingButton.IsInteractable(), Is.True);
            yield return null;
            Assert.That(canvasObject.GetComponentsInChildren<Transform>().Any(item => item.name == "Port Settings Overlay"), Is.False);
            Assert.That(controller.Session, Is.Null);
            settingsBefore.AssertUnchanged();
        }

        private object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(controller, arguments);
        }

        private T ReadField<T>(string name)
        {
            return (T)typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        }

        private void WriteField(string name, object value)
        {
            typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
        }

        private sealed class SettingsSnapshot
        {
            private static readonly string[] Keys =
            {
                MainMenuController.MasterVolumeKey, MainMenuController.MusicVolumeKey,
                MainMenuController.EffectsVolumeKey, MainMenuController.FullScreenKey,
            };
            private bool[] existed;
            private float[] volumes;
            private int fullScreenPreference;
            private float listenerVolume;
            private bool actualFullScreen;

            public static SettingsSnapshot Read()
            {
                return new SettingsSnapshot
                {
                    existed = Keys.Select(PlayerPrefs.HasKey).ToArray(),
                    volumes = new[] { PlayerPrefs.GetFloat(Keys[0], -1f), PlayerPrefs.GetFloat(Keys[1], -1f), PlayerPrefs.GetFloat(Keys[2], -1f) },
                    fullScreenPreference = PlayerPrefs.GetInt(Keys[3], -1),
                    listenerVolume = AudioListener.volume,
                    actualFullScreen = Screen.fullScreen,
                };
            }

            public void AssertUnchanged()
            {
                SettingsSnapshot after = Read();
                Assert.That(after.existed, Is.EqualTo(existed));
                Assert.That(after.volumes, Is.EqualTo(volumes));
                Assert.That(after.fullScreenPreference, Is.EqualTo(fullScreenPreference));
                Assert.That(after.listenerVolume, Is.EqualTo(listenerVolume));
                Assert.That(after.actualFullScreen, Is.EqualTo(actualFullScreen));
            }
        }
    }
}
