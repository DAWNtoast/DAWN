using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform portSettingsOverlay;
        private PortSettingsView cachedPortSettingsView;
        private int portSettingsBindingRevision;
        private Slider portSettingsMasterSlider;
        private Slider portSettingsMusicSlider;
        private Slider portSettingsEffectsSlider;
        private Toggle portSettingsFullScreenToggle;
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
            if (BlockIncompleteDeckBuilderNavigation()) return;
            if (navigationMotionPlaying || voyageCargoOverlay != null || pendingMarketPurchase != null) return;
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

            PortSettingsView view = AcquirePortSettingsView();
            BindPortSettingsView(view);
            Button apply = view.Apply, close = view.Close, returnToMenu = view.ReturnToMenu;

            Selectable[] controls = Array.FindAll(new Selectable[]
                { portSettingsMasterSlider, portSettingsMusicSlider, portSettingsEffectsSlider, portSettingsFullScreenToggle, apply, close, returnToMenu },
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
                PortSettingsCancelHandler cancel = controls[index].GetComponent<PortSettingsCancelHandler>();
                if (cancel == null) cancel = controls[index].gameObject.AddComponent<PortSettingsCancelHandler>();
                int revision = portSettingsBindingRevision;
                cancel.OnCancelRequested = () => { if (revision == portSettingsBindingRevision) ClosePortSettings(); };
            }
            if (!Application.isPlaying) SetEditorPreviewFlags(portSettingsOverlay);
            SelectPortSettingsDefaultControl();
        }

        private PortSettingsView AcquirePortSettingsView()
        {
            bool retain = Application.isPlaying && !editorPreviewBuilding;
            PortSettingsView view = retain ? cachedPortSettingsView : null;
            if (view == null)
            {
                PortSettingsView prefab = Resources.Load<PortSettingsView>("UI/PortSettings");
                if (prefab != null && prefab.IsComplete)
                {
                    view = Instantiate(prefab, contentRoot.parent, false);
                    view.name = "Port Settings Overlay";
                    view.GetComponent<PrefabRuntimeSkinBindings>()?.Apply();
                }
                else
                {
                    // One-time editor export and deliberately resource-stripped fixtures.
                    BuildPortSettingsLayout();
                    view = portSettingsOverlay.gameObject.AddComponent<PortSettingsView>();
                    view.CaptureBindings();
                }
                if (retain) cachedPortSettingsView = view;
            }
            return view;
        }

        private void BindPortSettingsView(PortSettingsView view)
        {
            portSettingsOverlay = (RectTransform)view.transform;
            // Fullscreen secondary windows can leave the fixed game viewport.
            // A cached settings view must follow the current page's modal host.
            portSettingsOverlay.SetParent(contentRoot.parent, false);
            portSettingsOverlay.SetAsLastSibling();
            portSettingsSheetGroup = view.Sheet;
            portSettingsSheetGroup.interactable = true;
            portSettingsSheetGroup.blocksRaycasts = true;
            portSettingsMasterSlider = view.Master;
            portSettingsMusicSlider = view.Music;
            portSettingsEffectsSlider = view.Effects;
            portSettingsFullScreenToggle = view.FullScreen;
            portSettingsHint = view.Hint;
            returnMenuButton = view.ReturnToMenu;
            int revision = ++portSettingsBindingRevision;
            BindPortSettingsSlider(view.Master, view.MasterValue, MainMenuController.MasterVolumeKey, .8f, revision);
            BindPortSettingsSlider(view.Music, view.MusicValue, MainMenuController.MusicVolumeKey, .7f, revision);
            BindPortSettingsSlider(view.Effects, view.EffectsValue, MainMenuController.EffectsVolumeKey, .8f, revision);
            view.Master.interactable = true;
            view.Music.interactable = true;
            view.Effects.interactable = SoundEffectsEnabled;
            view.FullScreen.SetIsOnWithoutNotify(PlayerPrefs.GetInt(MainMenuController.FullScreenKey, Screen.fullScreen ? 1 : 0) == 1);
            view.Hint.text = Application.isPlaying ? "" : "编辑预览：不保存设置，不改变音量或全屏。";
            ApplyPortSettingsMarketStyle(view);
            BindPortSettingsAction(view.Apply, ApplyPortSettings, revision);
            BindPortSettingsAction(view.Close, ClosePortSettings, revision);
            BindPortSettingsAction(view.ReturnToMenu, OpenReturnToMenuConfirmation, revision);
            PortSettingsBackgroundFocusHandler focus = view.GetComponent<PortSettingsBackgroundFocusHandler>();
            if (focus == null) focus = view.gameObject.AddComponent<PortSettingsBackgroundFocusHandler>();
            focus.OnBackgroundPressed = FocusPortSettingsControl;
            portSettingsOverlay.gameObject.SetActive(true);
        }

        private void BindPortSettingsAction(Button button, Action action, int revision)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (revision == portSettingsBindingRevision && portSettingsOverlay != null && portSettingsOverlay.gameObject.activeInHierarchy)
                    action();
            });
        }

        private void BindPortSettingsSlider(Slider slider, Text percent, string key, float fallback, int revision)
        {
            slider.onValueChanged = new Slider.SliderEvent();
            slider.SetValueWithoutNotify(Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback)));
            percent.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            slider.onValueChanged.AddListener(value =>
            {
                if (revision == portSettingsBindingRevision && portSettingsOverlay != null)
                    percent.text = Mathf.RoundToInt(value * 100f) + "%";
            });
        }

        private void ApplyPortSettingsMarketStyle(PortSettingsView view)
        {
            // Apply after prefab bindings so authored and resource-stripped layouts
            // share the live market finish without rebuilding retained controls.
            RectTransform sheet = (RectTransform)view.Sheet.transform;
            sheet.sizeDelta = new Vector2(980f, 780f);
            Image shade = view.GetComponent<Image>();
            SetPortSettingsFlatImage(shade, new Color(.025f, .018f, .012f, .68f));
            Image paper = sheet.GetComponent<Image>();
            MarketShopSkin.ApplyPanel(paper);
            paper.raycastTarget = true;
            // Keep the authored texture binding for resource lifetime bookkeeping;
            // the sliced Image preserves the narrow frame at every aspect ratio.
            view.Parchment.texture = paper.sprite != null ? paper.sprite.texture : null;
            view.Parchment.enabled = false;

            Transform oldSubtitle = sheet.Find("Port Settings Subtitle");
            if (oldSubtitle != null)
            {
                oldSubtitle.GetComponent<Text>().text = string.Empty;
                oldSubtitle.gameObject.SetActive(false);
            }
            Text eyebrow = sheet.Find("Port Settings Eyebrow")?.GetComponent<Text>();
            if (eyebrow == null)
                eyebrow = CreateText("Port Settings Eyebrow", sheet, new Vector2(780f, 24f),
                    new Vector2(0f, 320f), 16, FontStyle.Normal, TextAnchor.MiddleCenter, MarketShopSkin.MutedInk);
            eyebrow.text = "SETTINGS";
            StylePortSettingsText(eyebrow, new Vector2(780f, 24f), new Vector2(0f, 320f),
                16, FontStyle.Normal, TextAnchor.MiddleCenter, MarketShopSkin.MutedInk);
            StylePortSettingsText(sheet.Find("Port Settings Title").GetComponent<Text>(),
                new Vector2(780f, 50f), new Vector2(0f, 278f), 34, FontStyle.Bold,
                TextAnchor.MiddleCenter, MarketShopSkin.Ink);
            StylePortSettingsRule(sheet, "Port Settings Heading Rule", 240f);
            StylePortSettingsRule(sheet, "Port Settings Footer Rule", -229f);

            StylePortSettingsSlider(view.Master, view.MasterValue, sheet, "Master", 183f);
            StylePortSettingsSlider(view.Music, view.MusicValue, sheet, "Music", 83f);
            StylePortSettingsSlider(view.Effects, view.EffectsValue, sheet, "Effects", -17f);

            Toggle toggle = view.FullScreen;
            SetPortSettingsRect((RectTransform)toggle.transform, new Vector2(780f, 54f), new Vector2(0f, -153f));
            Image box = toggle.transform.Find("Toggle Box").GetComponent<Image>();
            SetPortSettingsRect(box.rectTransform, new Vector2(32f, 32f), new Vector2(-372f, 0f));
            SetPortSettingsFlatImage(box, new Color(.97f, .94f, .86f, 1f));
            Outline border = box.GetComponent<Outline>();
            if (border == null) border = box.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.57f, .43f, .27f, 1f);
            border.effectDistance = new Vector2(1f, -1f);
            border.useGraphicAlpha = true;
            Image check = toggle.transform.Find("Toggle Check").GetComponent<Image>();
            SetPortSettingsRect(check.rectTransform, new Vector2(22f, 22f), new Vector2(-372f, 0f));
            SetPortSettingsFlatImage(check, Color.white);
            check.sprite = view.Master.handleRect.GetComponent<Image>().sprite;
            check.raycastTarget = false;
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.transition = Selectable.Transition.ColorTint;
            ColorBlock toggleColors = ColorBlock.defaultColorBlock;
            toggleColors.highlightedColor = toggleColors.selectedColor = new Color(1f, .94f, .80f, 1f);
            toggleColors.pressedColor = new Color(.83f, .75f, .60f, 1f);
            toggleColors.disabledColor = new Color(.8f, .78f, .72f, .65f);
            toggleColors.fadeDuration = .1f;
            toggle.colors = toggleColors;
            StylePortSettingsText(toggle.transform.Find("Port Settings Full Screen Label").GetComponent<Text>(),
                new Vector2(520f, 42f), new Vector2(-74f, 0f), 22, FontStyle.Normal,
                TextAnchor.MiddleLeft, MarketShopSkin.Ink);
            StylePortSettingsText(view.Hint, new Vector2(780f, 36f), new Vector2(0f, -200f),
                16, FontStyle.Normal, TextAnchor.MiddleCenter, MarketShopSkin.MutedInk);

            StylePortSettingsAction(view.Close, new Vector2(-204f, -277f), new Vector2(372f, 54f), false);
            StylePortSettingsAction(view.Apply, new Vector2(204f, -277f), new Vector2(372f, 54f), true);
            StylePortSettingsAction(view.ReturnToMenu, new Vector2(0f, -342f), new Vector2(440f, 42f), false);
        }

        private void StylePortSettingsSlider(Slider slider, Text percent, RectTransform sheet, string key, float labelY)
        {
            StylePortSettingsText(sheet.Find("Port Settings " + key + " Label").GetComponent<Text>(),
                new Vector2(660f, 32f), new Vector2(-60f, labelY), 23, FontStyle.Normal,
                TextAnchor.MiddleLeft, MarketShopSkin.Ink);
            StylePortSettingsText(percent, new Vector2(100f, 32f), new Vector2(340f, labelY),
                22, FontStyle.Normal, TextAnchor.MiddleRight, MarketShopSkin.MutedInk);
            SetPortSettingsRect((RectTransform)slider.transform, new Vector2(800f, 44f), new Vector2(0f, labelY - 40f));
            Image track = slider.transform.Find("Track")?.GetComponent<Image>() ??
                slider.transform.Find("Background")?.GetComponent<Image>();
            if (track != null)
            {
                SetPortSettingsRect(track.rectTransform, new Vector2(756f, 6f), Vector2.zero);
                SetPortSettingsFlatImage(track, new Color(.76f, .64f, .43f, 1f));
                track.raycastTarget = false;
            }
            SetPortSettingsRect((RectTransform)slider.fillRect.parent, new Vector2(756f, 6f), Vector2.zero);
            slider.fillRect.sizeDelta = Vector2.zero;
            slider.fillRect.anchoredPosition = Vector2.zero;
            SetPortSettingsRect((RectTransform)slider.handleRect.parent, new Vector2(756f, 0f), Vector2.zero);
            slider.handleRect.sizeDelta = new Vector2(32f, 32f);
            slider.handleRect.anchoredPosition = Vector2.zero;
            MarketShopSkin.ApplySlider(slider);
        }

        private void StylePortSettingsAction(Button button, Vector2 position, Vector2 size, bool primary)
        {
            SetPortSettingsRect((RectTransform)button.transform, size, position);
            Text label = button.GetComponentInChildren<Text>(true);
            StylePortSettingsText(label, size - new Vector2(24f, 8f), Vector2.zero, 22,
                FontStyle.Normal, TextAnchor.MiddleCenter, MarketShopSkin.Ink);
            if (primary)
            {
                WoodButtonSkin.Apply(button, Color.white);
                WoodButtonSkin.SetRole(button, WoodButtonRole.Primary);
            }
            else MarketShopSkin.ApplyLightButton(button);
        }

        private void StylePortSettingsRule(RectTransform sheet, string name, float y)
        {
            Image rule = sheet.Find(name)?.GetComponent<Image>();
            if (rule == null) rule = CreateImage(name, sheet, new Vector2(780f, 1f), new Vector2(0f, y), Color.white);
            SetPortSettingsRect(rule.rectTransform, new Vector2(780f, 1f), new Vector2(0f, y));
            SetPortSettingsFlatImage(rule, new Color(.68f, .58f, .43f, .7f));
            rule.raycastTarget = false;
        }

        private static void StylePortSettingsText(Text text, Vector2 size, Vector2 position, int fontSize,
            FontStyle style, TextAnchor alignment, Color color)
        {
            SetPortSettingsRect(text.rectTransform, size, position);
            GameTypography.Apply(text, fontSize, style);
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
        }

        private static void SetPortSettingsRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetPortSettingsFlatImage(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = null;
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.pixelsPerUnitMultiplier = 1f;
        }

        private void BuildPortSettingsLayout()
        {
            portSettingsOverlay = CreateStretchPanel("Port Settings Overlay", contentRoot.parent, new Color(0.075f, 0.04f, 0.015f, 0.76f));
            portSettingsOverlay.SetAsLastSibling();
            portSettingsOverlay.gameObject.AddComponent<PortSettingsBackgroundFocusHandler>().OnBackgroundPressed = FocusPortSettingsControl;
            RectTransform sheet = CreateFixedPanel("Port Settings Sheet", portSettingsOverlay, new Vector2(1000f, 800f), Vector2.zero, Html("#DFC591"));
            portSettingsSheetGroup = sheet.gameObject.AddComponent<CanvasGroup>();
            Texture2D parchment = MarketShopSkin.Paper != null ? MarketShopSkin.Paper.texture : null;
            RawImage paper = CreateRawImage("Port Settings Parchment", sheet, new Vector2(1000f, 800f), Vector2.zero, parchment, new Rect(0f, 0f, 1f, 1f));
            paper.raycastTarget = false;
            paper.enabled = parchment != null;

            Color ink = Html("#322215");
            Color mutedInk = Html("#665037");
            Text title = CreateText("Port Settings Title", sheet, new Vector2(760f, 48f), new Vector2(0f, 274f), 30, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            title.text = "船长的航海偏好";
            Image divider = CreateImage("Port Settings Heading Rule", sheet, new Vector2(740f, 2f), new Vector2(0f, 206f), Html("#967348"));
            divider.raycastTarget = false;

            portSettingsMasterSlider = CreatePortSettingsSlider(sheet, "Master", "主音量", 160f, PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, 0.8f));
            portSettingsMusicSlider = CreatePortSettingsSlider(sheet, "Music", "背景音乐", 66f, PlayerPrefs.GetFloat(MainMenuController.MusicVolumeKey, 0.7f));
            portSettingsEffectsSlider = CreatePortSettingsSlider(sheet, "Effects",
                "战斗音效", -28f,
                PlayerPrefs.GetFloat(MainMenuController.EffectsVolumeKey, 0.8f));
            portSettingsMasterSlider.interactable = true;
            portSettingsMusicSlider.interactable = true;
            portSettingsEffectsSlider.interactable = SoundEffectsEnabled;
            portSettingsFullScreenToggle = CreatePortSettingsToggle(sheet,
                PlayerPrefs.GetInt(MainMenuController.FullScreenKey, Screen.fullScreen ? 1 : 0) == 1);

            portSettingsHint = CreateText("Port Settings Hint", sheet, new Vector2(750f, 48f), new Vector2(0f, -213f), 15, FontStyle.Normal, TextAnchor.MiddleCenter, mutedInk);
            portSettingsHint.text = Application.isPlaying
                ? ""
                : "编辑预览：不保存设置，不改变音量或全屏。";
            Button close = CreatePortSettingsButton("Close Port Settings", sheet, new Vector2(-205f, -283f), "关闭 · 不保存", Html("#705136"));
            close.onClick.AddListener(ClosePortSettings);
            Button apply = CreatePortSettingsButton("Apply Port Settings", sheet, new Vector2(205f, -283f), "应用设置", Html("#98682D"));
            apply.onClick.AddListener(ApplyPortSettings);
            Button returnToMenu = BuildReturnToMenuSettingsButton(sheet);

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
            Text title = CreateText("Port Settings " + key + " Label", parent, new Vector2(620f, 30f), new Vector2(-45f, y + 16f), 20, FontStyle.Bold, TextAnchor.MiddleLeft, Html("#322215"));
            title.text = label;
            Text percent = CreateText("Port Settings " + key + " Value", parent, new Vector2(90f, 30f), new Vector2(324f, y + 16f), 20, FontStyle.Bold, TextAnchor.MiddleRight, Html("#6C421B"));
            RectTransform rect = CreateRect("Port Settings " + key + " Slider", parent, new Vector2(720f, 38f), new Vector2(0f, y - 22f));
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
            RectTransform rect = CreateRect("Port Settings Full Screen Toggle", parent, new Vector2(720f, 54f), new Vector2(0f, -139f));
            Image hitArea = rect.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            Toggle toggle = rect.gameObject.AddComponent<Toggle>();
            Image box = CreateImage("Toggle Box", rect, new Vector2(38f, 38f), new Vector2(-336f, 0f), Html("#735638"));
            Image check = CreateImage("Toggle Check", rect, new Vector2(22f, 22f), new Vector2(-336f, 0f), Html("#E3BC62"), circleSprite);
            check.raycastTarget = false;
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.SetIsOnWithoutNotify(fullScreen);
            Text label = CreateText("Port Settings Full Screen Label", rect, new Vector2(410f, 42f), new Vector2(-93f, 0f), 20, FontStyle.Bold, TextAnchor.MiddleLeft, Html("#322215"));
            label.text = "全屏显示";
            Text detail = CreateText("Port Settings Full Screen Note", rect, new Vector2(270f, 42f), new Vector2(210f, 0f), 14, FontStyle.Normal, TextAnchor.MiddleRight, Html("#665037"));
            detail.text = "";
            return toggle;
        }

        private Button CreatePortSettingsButton(string name, RectTransform parent, Vector2 position, string label, Color face)
        {
            Button button = CreateButton(name, parent, new Vector2(330f, 52f), position, face, Html("#FFEDBE"), 19);
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
            PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, master);
            PlayerPrefs.SetFloat(MainMenuController.MusicVolumeKey, music);
            if (SoundEffectsEnabled) PlayerPrefs.SetFloat(MainMenuController.EffectsVolumeKey, effects);
            PlayerPrefs.SetInt(MainMenuController.FullScreenKey, fullScreen ? 1 : 0);
            PlayerPrefs.Save();
            RefreshMusicVolume();
            if (Screen.fullScreen != fullScreen) Screen.fullScreen = fullScreen;
            ClosePortSettings();
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
                // Keep only this small hierarchy; release the large borrowed paper.
                PortSettingsView view = overlay.GetComponent<PortSettingsView>();
                if (view != null)
                {
                    if (view.Parchment != null) view.Parchment.texture = null;
                    Image sheetPaper = view.Sheet != null ? view.Sheet.GetComponent<Image>() : null;
                    if (sheetPaper != null)
                    {
                        sheetPaper.sprite = null;
                        sheetPaper.overrideSprite = null;
                    }
                    MarketShopSkin.ReleaseGameplayPaperReference();
                }
                portSettingsOverlay = null;
                if (cachedPortSettingsView == null || cachedPortSettingsView.gameObject != overlay)
                {
                    if (Application.isPlaying) Destroy(overlay); else DestroyImmediate(overlay);
                }
                QueueUnusedArtCollection(true);
            }
            portSettingsBindingRevision++;
            portSettingsMasterSlider = null;
            portSettingsMusicSlider = null;
            portSettingsEffectsSlider = null;
            portSettingsFullScreenToggle = null;
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
            // Physical Escape is routed by the scene controller even without
            // focus. Do not also dismiss a newly revealed lower modal here.
            if (UiEscapeInput.IsPressed) return;
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

