using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class PortSettingsModalTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ReadingSizes_SettingsAndReturnConfirmationNeverShrinkText(int level)
        {
            int? previous = GameReadability.OverrideLevel;
            GameReadability.OverrideLevel = level;
            GameObject host = new GameObject("Settings Reading Size Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.GrayGull, 0);
                Invoke(controller, "OpenPortSettings");
                Canvas.ForceUpdateCanvases();
                RectTransform sheet = host.GetComponentsInChildren<RectTransform>(true).Single(t => t.name == "Port Settings Sheet");
                foreach (Text text in sheet.GetComponentsInChildren<Text>())
                {
                    if (string.IsNullOrEmpty(text.text)) continue;
                    Assert.That(text.resizeTextForBestFit, Is.False, text.name);
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(18)), text.name);
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f), text.name);
                }
                Invoke(controller, "OpenReturnToMenuConfirmation");
                Canvas.ForceUpdateCanvases();
                Text message = host.GetComponentsInChildren<Text>(true).Single(t => t.name == "Return To Main Menu Message");
                Assert.That(message.fontSize, Is.EqualTo(GameReadability.FontSize(22)));
                Assert.That(message.preferredHeight, Is.LessThanOrEqualTo(message.rectTransform.rect.height + 1f));
                Assert.That(message.resizeTextForBestFit, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
                GameReadability.OverrideLevel = previous;
            }
        }

        [TestCase(CoreGameplayPreviewScreen.Maintenance)]
        [TestCase(CoreGameplayPreviewScreen.DeckBuilder)]
        [TestCase(CoreGameplayPreviewScreen.DispatchDeployment)]
        [TestCase(CoreGameplayPreviewScreen.PortHub)]
        public void CloseSettings_PreservesCurrentPageDraftsAndPreferences(CoreGameplayPreviewScreen screen)
        {
            GameObject host = new GameObject("Port Settings Preservation Test");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                var session = controller.Session;
                decimal originalGold = session.ProfileGold;
                string originalPort = session.CurrentPort;
                RectTransform page = ReadField<RectTransform>(controller, "contentRoot");
                int[] originalChildren = page.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray();
                CanvasGroup existingGroup = page.gameObject.AddComponent<CanvasGroup>();
                existingGroup.interactable = true;
                existingGroup.blocksRaycasts = true;

                VoyageSupplyOrder order = ReadField<VoyageSupplyOrder>(controller, "pendingSupplyOrder");
                order.Water = 70;
                order.Cannonballs = 3;
                List<int> deck = ReadField<List<int>>(controller, "configuredBattleDeck");
                int[] originalDeck = deck.ToArray();
                List<int> crew = ReadField<List<int>>(controller, "selectedDispatchCrew");
                crew.Clear();
                crew.Add(1);
                crew.Add(5);
                WriteField(controller, "selectedDispatchDurationHours", 7);
                SettingsSnapshot before = SettingsSnapshot.Read();

                if (screen == CoreGameplayPreviewScreen.PortHub)
                    host.GetComponentsInChildren<Button>().Single(item => item.name == "Port Hub Settings").onClick.Invoke();
                else
                    Invoke(controller, "OpenPortSettings");
                RectTransform modal = ReadField<RectTransform>(controller, "portSettingsOverlay");
                Assert.That(modal, Is.Not.Null);
                Assert.That(modal.parent, Is.SameAs(page.parent));
                Assert.That(modal.GetSiblingIndex(), Is.EqualTo(modal.parent.childCount - 1));
                Assert.That(modal.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(existingGroup.interactable, Is.False);
                Assert.That(existingGroup.blocksRaycasts, Is.False);
                Assert.That(modal.GetComponentsInChildren<RawImage>().Any(item => item.name == "Port Settings Parchment" && item.texture != null), Is.True);

                // Reopening must not create another modal or reset an in-progress draft.
                Slider master = ReadField<Slider>(controller, "portSettingsMasterSlider");
                master.value = 0.13f;
                ReadField<Slider>(controller, "portSettingsMusicSlider").value = 0.27f;
                ReadField<Slider>(controller, "portSettingsEffectsSlider").value = 0.41f;
                Toggle toggle = ReadField<Toggle>(controller, "portSettingsFullScreenToggle");
                toggle.isOn = !toggle.isOn;
                Invoke(controller, "OpenPortSettings");
                Assert.That(ReadField<RectTransform>(controller, "portSettingsOverlay"), Is.SameAs(modal));
                Assert.That(master.value, Is.EqualTo(0.13f).Within(0.001f));
                before.AssertUnchanged();

                modal.GetComponentsInChildren<Button>().Single(item => item.name == "Close Port Settings").onClick.Invoke();
                Assert.That(ReadField<RectTransform>(controller, "portSettingsOverlay"), Is.Null);
                Assert.That(page.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray(), Is.EqualTo(originalChildren));
                Assert.That(page.GetComponent<CanvasGroup>(), Is.SameAs(existingGroup));
                Assert.That(existingGroup.interactable, Is.True);
                Assert.That(existingGroup.blocksRaycasts, Is.True);
                Assert.That(ReadField<VoyageSupplyOrder>(controller, "pendingSupplyOrder"), Is.SameAs(order));
                Assert.That(order.Water, Is.EqualTo(70));
                Assert.That(order.Cannonballs, Is.EqualTo(3));
                Assert.That(deck, Is.EqualTo(originalDeck));
                Assert.That(crew, Is.EqualTo(new[] { 1, 5 }));
                Assert.That(ReadField<int>(controller, "selectedDispatchDurationHours"), Is.EqualTo(7));
                Assert.That(controller.Session, Is.SameAs(session));
                Assert.That(session.ProfileGold, Is.EqualTo(originalGold));
                Assert.That(session.CurrentPort, Is.EqualTo(originalPort));
                before.AssertUnchanged();
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ApplySettings_InEditorPreview_DoesNotWritePrefsOrChangeAudioDisplay()
        {
            Assert.That(Application.isPlaying, Is.False);
            GameObject host = new GameObject("Port Settings Preview Apply Test");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Tavern, CoreGameplayPreviewPort.WhiteShell, 0);
                SettingsSnapshot before = SettingsSnapshot.Read();
                Invoke(controller, "OpenPortSettings");
                ReadField<Slider>(controller, "portSettingsMasterSlider").value = 0.17f;
                ReadField<Slider>(controller, "portSettingsMusicSlider").value = 0.28f;
                ReadField<Slider>(controller, "portSettingsEffectsSlider").value = 0.39f;
                Toggle toggle = ReadField<Toggle>(controller, "portSettingsFullScreenToggle");
                toggle.isOn = !toggle.isOn;

                Invoke(controller, "ApplyPortSettings");
                before.AssertUnchanged();
                Assert.That(ReadField<RectTransform>(controller, "portSettingsOverlay"), Is.Not.Null);
                Assert.That(ReadField<Text>(controller, "portSettingsHint").text, Does.Contain("未应用或保存"));
                Invoke(controller, "ClosePortSettings");
                Assert.That(ReadField<RectTransform>(controller, "contentRoot").GetComponent<CanvasGroup>(), Is.Null,
                    "Closing must remove the temporary input blocker when the page did not already have one.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static T ReadField<T>(CoreGameplayDemoController controller, string name)
        {
            FieldInfo field = typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(controller);
        }

        private static void WriteField(CoreGameplayDemoController controller, string name, object value)
        {
            typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
        }

        private static void Invoke(CoreGameplayDemoController controller, string name)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(controller, null);
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
                Assert.That(after.existed, Is.EqualTo(existed), "The modal must not create preference keys before an actual runtime apply.");
                Assert.That(after.volumes, Is.EqualTo(volumes));
                Assert.That(after.fullScreenPreference, Is.EqualTo(fullScreenPreference));
                Assert.That(after.listenerVolume, Is.EqualTo(listenerVolume));
                Assert.That(after.actualFullScreen, Is.EqualTo(actualFullScreen));
            }
        }
    }
}
