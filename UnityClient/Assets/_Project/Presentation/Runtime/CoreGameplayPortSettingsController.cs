using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform portSettingsOverlay;
        private Slider portSettingsMasterSlider;
        private Slider portSettingsMusicSlider;
        private Slider portSettingsEffectsSlider;
        private Toggle portSettingsFullScreenToggle;
        private ReadabilitySelector portReadabilitySelector;
        private Text portSettingsHint;
        private CanvasGroup portSettingsPageGroup;
        private bool portSettingsCreatedPageGroup;
        private bool portSettingsPreviousInteractable;
        private bool portSettingsPreviousRaycasts;
        private GameObject portSettingsPreviousSelection;

        /// <summary>
        /// Layers a settings draft over the existing page. Never calls Render: supply,
        /// deployment, and deck controls beneath the modal keep their current objects/state.
        /// </summary>
        private void OpenPortSettings()
        {
            if (navigationMotionPlaying || voyageCargoOverlay != null) return;
            if (portSettingsOverlay != null)
            {
                portSettingsOverlay.SetAsLastSibling();
                FocusPortSettingsControl();
                return;
            }
            if (contentRoot == null || contentRoot.parent == null) return;

            portSettingsPreviousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            portSettingsPageGroup = contentRoot.GetComponent<CanvasGroup>();
            portSettingsCreatedPageGroup = portSettingsPageGroup == null;
            if (portSettingsCreatedPageGroup) portSettingsPageGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
            portSettingsPreviousInteractable = portSettingsPageGroup.interactable;
            portSettingsPreviousRaycasts = portSettingsPageGroup.blocksRaycasts;
            portSettingsPageGroup.interactable = false;
            portSettingsPageGroup.blocksRaycasts = false;

            portSettingsOverlay = CreateStretchPanel("Port Settings Overlay", contentRoot.parent, new Color(0.075f, 0.04f, 0.015f, 0.76f));
            portSettingsOverlay.SetAsLastSibling();
            portSettingsOverlay.gameObject.AddComponent<PortSettingsBackgroundFocusHandler>().OnBackgroundPressed = FocusPortSettingsControl;
            RectTransform sheet = CreateFixedPanel("Port Settings Sheet", portSettingsOverlay, new Vector2(1040f, 960f), Vector2.zero, Html("#DFC591"));
            portSettingsSheetGroup = sheet.gameObject.AddComponent<CanvasGroup>();
            Texture2D parchment = cardWorkshopArt == null ? null : cardWorkshopArt.CollectionParchment;
            if (parchment == null) parchment = Resources.Load<Texture2D>("CardWorkshopV2/deck_builder_parchment_v2");
            if (parchment != null)
            {
                RawImage paper = CreateRawImage("Port Settings Parchment", sheet, sheet.sizeDelta, Vector2.zero, parchment, new Rect(0f, 0f, 1f, 1f));
                paper.raycastTarget = false;
            }

            Color ink = Html("#322215");
            Color mutedInk = Html("#665037");
            Text title = CreateText("Port Settings Title", sheet, new Vector2(920f, 80f), new Vector2(0f, 390f), 36, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            GameReadability.Heading(title, 36);
            title.text = "船长的航海偏好";
            Text subtitle = CreateText("Port Settings Subtitle", sheet, new Vector2(920f, 44f), new Vector2(0f, 325f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, mutedInk);
            GameReadability.Body(subtitle, 18);
            subtitle.text = "应用后生效；关闭放弃调整";
            Image divider = CreateImage("Port Settings Heading Rule", sheet, new Vector2(920f, 2f), new Vector2(0f, 290f), Html("#967348"));
            divider.raycastTarget = false;

            portSettingsMasterSlider = CreatePortSettingsSlider(sheet, "Master", "主音量", 224f, PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, 0.8f));
            portSettingsMusicSlider = CreatePortSettingsSlider(sheet, "Music", "背景音乐", 110f, PlayerPrefs.GetFloat(MainMenuController.MusicVolumeKey, 0.7f));
            portSettingsEffectsSlider = CreatePortSettingsSlider(sheet, "Effects",
                "战斗音效", -4f,
                PlayerPrefs.GetFloat(MainMenuController.EffectsVolumeKey, 0.8f));
            portSettingsMasterSlider.interactable = true;
            portSettingsMusicSlider.interactable = true;
            portSettingsEffectsSlider.interactable = SoundEffectsEnabled;
            portSettingsFullScreenToggle = CreatePortSettingsToggle(sheet,
                PlayerPrefs.GetInt(MainMenuController.FullScreenKey, Screen.fullScreen ? 1 : 0) == 1);

            portReadabilitySelector = GameReadability.CreateSelector(sheet, "Port Reading Size", new Vector2(0, -210f), GameReadability.Level);
            portSettingsHint = CreateText("Port Settings Hint", sheet, new Vector2(920f, 42f), new Vector2(0f, -278f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, mutedInk);
            GameReadability.Body(portSettingsHint, 18);
            portSettingsHint.text = Application.isPlaying
                ? ""
                : "编辑预览：不保存设置，不改变音量或全屏。";
            Button close = CreatePortSettingsButton("Close Port Settings", sheet, new Vector2(-225f, -342f), "关闭 · 不保存", Html("#705136"));
            close.onClick.AddListener(ClosePortSettings);
            Button apply = CreatePortSettingsButton("Apply Port Settings", sheet, new Vector2(225f, -342f), "应用设置", Html("#98682D"));
            apply.onClick.AddListener(ApplyPortSettings);
            Button returnToMenu = BuildReturnToMenuSettingsButton(sheet);

            Selectable[] controls = Array.FindAll(new Selectable[]
                { portSettingsMasterSlider, portSettingsMusicSlider, portSettingsEffectsSlider, portSettingsFullScreenToggle,
                    portReadabilitySelector.Buttons[0], portReadabilitySelector.Buttons[1], portReadabilitySelector.Buttons[2], apply, close, returnToMenu },
                control => control.interactable);
            for (int index = 0; index < controls.Length; index += 1)
            {
                Navigation navigation = controls[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = controls[(index + controls.Length - 1) % controls.Length];
                navigation.selectOnDown = controls[(index + 1) % controls.Length];
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                controls[index].navigation = navigation;
                controls[index].gameObject.AddComponent<PortSettingsCancelHandler>().OnCancelRequested = ClosePortSettings;
            }
            if (!Application.isPlaying) SetEditorPreviewFlags(portSettingsOverlay);
            SelectPortSettingsDefaultControl();
        }

        private void FocusPortSettingsControl()
        {
            if (portSettingsOverlay == null || EventSystem.current == null) return;
            if (returnMenuConfirmation != null)
            {
                FocusReturnToMenuConfirmation();
                return;
            }
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(portSettingsOverlay)) return;
            SelectPortSettingsDefaultControl();
        }

        private void SelectPortSettingsDefaultControl()
        {
            if (EventSystem.current == null) return;
            Selectable target = portSettingsMasterSlider != null && portSettingsMasterSlider.interactable
                ? portSettingsMasterSlider
                : portSettingsFullScreenToggle != null && portSettingsFullScreenToggle.interactable
                    ? portSettingsFullScreenToggle
                    : null;
            if (target != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private Slider CreatePortSettingsSlider(RectTransform parent, string key, string label, float y, float value)
        {
            Text title = CreateText("Port Settings " + key + " Label", parent, new Vector2(620f, 48f), new Vector2(-45f, y + 22f), 24, FontStyle.Normal, TextAnchor.MiddleLeft, Html("#322215"));
            GameReadability.Body(title, 24);
            title.text = label;
            Text percent = CreateText("Port Settings " + key + " Value", parent, new Vector2(120f, 48f), new Vector2(324f, y + 22f), 22, FontStyle.Normal, TextAnchor.MiddleRight, Html("#6C421B"));
            GameReadability.Body(percent, 22);
            RectTransform rect = CreateRect("Port Settings " + key + " Slider", parent, new Vector2(720f, 38f), new Vector2(0f, y - 28f));
            Image hitArea = rect.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            Slider slider = rect.gameObject.AddComponent<Slider>();
            Image track = CreateImage("Track", rect, new Vector2(680f, 10f), Vector2.zero, Html("#705237"));
            track.raycastTarget = false;
            RectTransform fillArea = CreateRect("Fill Area", rect, new Vector2(680f, 10f), Vector2.zero);
            RectTransform fill = CreateStretchPanel("Fill", fillArea, Html("#B38532"));
            fill.GetComponent<Image>().raycastTarget = false;
            RectTransform handleArea = CreateRect("Handle Area", rect, new Vector2(680f, 38f), Vector2.zero);
            Image handle = CreateImage("Handle", handleArea, new Vector2(30f, 0f), Vector2.zero, Html("#E0B95D"), circleSprite);
            slider.fillRect = fill;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            percent.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            // Changes stay exclusively in these controls until ApplyPortSettings is invoked.
            slider.onValueChanged.AddListener(next => percent.text = Mathf.RoundToInt(next * 100f) + "%");
            return slider;
        }

        private Toggle CreatePortSettingsToggle(RectTransform parent, bool fullScreen)
        {
            RectTransform rect = CreateRect("Port Settings Full Screen Toggle", parent, new Vector2(720f, 64f), new Vector2(0f, -118f));
            Image hitArea = rect.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            Toggle toggle = rect.gameObject.AddComponent<Toggle>();
            Image box = CreateImage("Toggle Box", rect, new Vector2(38f, 38f), new Vector2(-336f, 0f), Html("#735638"));
            Image check = CreateImage("Toggle Check", rect, new Vector2(22f, 22f), new Vector2(-336f, 0f), Html("#E3BC62"), circleSprite);
            check.raycastTarget = false;
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.SetIsOnWithoutNotify(fullScreen);
            Text label = CreateText("Port Settings Full Screen Label", rect, new Vector2(410f, 54f), new Vector2(-93f, 0f), 24, FontStyle.Normal, TextAnchor.MiddleLeft, Html("#322215"));
            GameReadability.Body(label, 24);
            label.text = "全屏显示";
            Text detail = CreateText("Port Settings Full Screen Note", rect, new Vector2(270f, 54f), new Vector2(210f, 0f), 18, FontStyle.Normal, TextAnchor.MiddleRight, Html("#665037"));
            GameReadability.Body(detail, 18);
            detail.text = "";
            return toggle;
        }

        private Button CreatePortSettingsButton(string name, RectTransform parent, Vector2 position, string label, Color face)
        {
            Button button = CreateButton(name, parent, new Vector2(410f, 48f * GameReadability.Scale), position, face, Html("#FFEDBE"), 24);
            GameReadability.Body(button.GetComponentInChildren<Text>(), 24);
            button.GetComponentInChildren<Text>().text = label;
            return button;
        }

        private void ApplyPortSettings()
        {
            if (portSettingsOverlay == null || returnMenuConfirmation != null || returnMenuSaving) return;
            // Preview controls can be exercised in edit-mode QA without touching user prefs
            // or process-wide audio/display settings. Do not move writes before this guard.
            if (!Application.isPlaying || editorPreviewBuilding)
            {
                portSettingsHint.text = "编辑预览：未应用或保存设置。";
                return;
            }

            float master = Mathf.Clamp01(portSettingsMasterSlider.value);
            float music = Mathf.Clamp01(portSettingsMusicSlider.value);
            float effects = Mathf.Clamp01(portSettingsEffectsSlider.value);
            bool fullScreen = portSettingsFullScreenToggle.isOn;
            int readingLevel = portReadabilitySelector.Value;
            bool readingChanged = readingLevel != GameReadability.Level;
            PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, master);
            PlayerPrefs.SetFloat(MainMenuController.MusicVolumeKey, music);
            if (SoundEffectsEnabled) PlayerPrefs.SetFloat(MainMenuController.EffectsVolumeKey, effects);
            PlayerPrefs.SetInt(MainMenuController.FullScreenKey, fullScreen ? 1 : 0);
            PlayerPrefs.SetInt(GameReadability.PreferenceKey, readingLevel);
            PlayerPrefs.Save();
            RefreshMusicVolume();
            if (Screen.fullScreen != fullScreen) Screen.fullScreen = fullScreen;
            ClosePortSettings();
            if (readingChanged) Render();
        }

        private void ClosePortSettings()
        {
            if (returnMenuConfirmation != null || returnMenuSaving) return;
            if (portSettingsPageGroup != null)
            {
                portSettingsPageGroup.interactable = portSettingsPreviousInteractable;
                portSettingsPageGroup.blocksRaycasts = portSettingsPreviousRaycasts;
                if (portSettingsCreatedPageGroup)
                {
                    // Runtime Destroy is delayed until frame end. Keep the restored group
                    // for reuse so a close/reopen in this frame cannot grab a doomed group
                    // and lose its input barrier on the following frame.
                    if (!Application.isPlaying) DestroyImmediate(portSettingsPageGroup);
                }
            }
            portSettingsPageGroup = null;
            portSettingsCreatedPageGroup = false;
            if (portSettingsOverlay != null)
            {
                GameObject overlay = portSettingsOverlay.gameObject;
                overlay.SetActive(false);
                portSettingsOverlay = null;
                if (Application.isPlaying) Destroy(overlay);
                else DestroyImmediate(overlay);
            }
            portSettingsMasterSlider = null;
            portSettingsMusicSlider = null;
            portSettingsEffectsSlider = null;
            portSettingsFullScreenToggle = null;
            portReadabilitySelector = null;
            portSettingsHint = null;
            portSettingsSheetGroup = null;
            returnMenuButton = null;
            if (EventSystem.current != null)
            {
                GameObject selection = portSettingsPreviousSelection != null && portSettingsPreviousSelection.activeInHierarchy
                    ? portSettingsPreviousSelection : null;
                EventSystem.current.SetSelectedGameObject(selection);
            }
            portSettingsPreviousSelection = null;
        }
    }

    internal sealed class PortSettingsCancelHandler : MonoBehaviour, ICancelHandler
    {
        public Action OnCancelRequested;

        public void OnCancel(BaseEventData eventData)
        {
            eventData.Use();
            OnCancelRequested?.Invoke();
        }
    }

    internal sealed class PortSettingsBackgroundFocusHandler : MonoBehaviour, IPointerDownHandler
    {
        public Action OnBackgroundPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            // The input module deselects first when the paper/backdrop has no Selectable.
            // Receiving the bubbled pointer-down restores a modal control, so Escape and
            // controller cancel still work after a click on empty parchment.
            OnBackgroundPressed?.Invoke();
            eventData.Use();
        }
    }
}
