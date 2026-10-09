using System;
using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Builds the player-facing title screen at runtime and supplies a transient
    /// editor preview so the authored result stays visible outside Play Mode.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        public const string MainSceneName = "CoreGameplayDemo";
        public const string EditorPreviewCanvasName = "Main Menu Canvas [Editor Preview]";
        public const string MasterVolumeKey = "ShatteredSail.MasterVolume.v1";
        public const string MusicVolumeKey = "ShatteredSail.MusicVolume.v1";
        public const string EffectsVolumeKey = "ShatteredSail.EffectsVolume.v1";
        public const string FullScreenKey = "ShatteredSail.FullScreen.v1";

        private static readonly Color Ink = Html("#F1E7CC");
        private static readonly Color MutedInk = Html("#A9B8B5");
        private static readonly Color DeepSea = Html("#06151C");
        private static readonly Color Panel = Html("#0B222B");
        private static readonly Color PanelRaised = Html("#123640");
        private static readonly Color Gold = Html("#D9A83E");
        private static readonly Color Teal = Html("#2B8880");
        private static readonly Color Coral = Html("#D16B52");
        private static readonly Color PaperInk = Html("#2B1B11");
        private static readonly Color PaperMutedInk = Html("#46321F");
        private static readonly Color PaperAccent = Html("#203D2D");

        private Font interfaceFont;
        private Button continueButton;
        private RectTransform settingsPanel;
        private RectTransform achievementsPanel;
        private RectTransform confirmationPanel;
        private RectTransform saveNoticePanel;
        private Slider masterSlider;
        private Slider musicSlider;
        private Slider effectsSlider;
        private Toggle fullScreenToggle;
        private ReadabilitySelector readabilitySelector;
        private Text masterValue;
        private Text musicValue;
        private Text effectsValue;
        private sealed class AchievementRowView
        {
            public RectTransform Root;
            public Image Face;
            public RawImage Icon;
            public Text Title;
            public Text Description;
            public Text State;
            public Text Progress;
        }

        private RectTransform menuCanvas;
        private bool menuCanvasIsEditorPreview;
        private readonly List<AchievementRowView> achievementRows = new List<AchievementRowView>(8);
        private AchievementProgressView[] achievementProgress;
        private readonly int[] achievementFilteredIndices = new int[AchievementCatalog.TotalCount];
        private int achievementFilteredCount;
        private Dropdown achievementCategory;
        private readonly List<string> achievementCategories = new List<string>();
        private readonly List<Button> achievementStateButtons = new List<Button>(3);
        private Text achievementEmpty;
        private int achievementStateFilter;
        private Text achievementMenuDetail;
        private Text achievementSummary;
        private Text achievementPageLabel;
        private Button achievementPreviousPage;
        private Button achievementNextPage;
        private int achievementPage;
        private static int AchievementsPerPage => GameReadability.Level == 0 ? 8 : GameReadability.Level == 1 ? 6 : 4;
        private static bool profileRecoveryBlocked;

        public Button ContinueButton => continueButton;
        public bool HasSaveData => HasSave();

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            string profileNotice = null;
            bool readOnlySmoke = ReleasePlayerSmokeCheck.IsRequested;
            profileRecoveryBlocked = !readOnlySmoke && !ProfileSaveJournal.TryRecover(out profileNotice);
            RemoveEditorPreview();
            string voyageNotice = null;
            bool readableVoyage = !readOnlySmoke && !profileRecoveryBlocked && VoyageSuspensionPersistence.TryRead(out _, out voyageNotice);
            if (!readOnlySmoke && !profileRecoveryBlocked && HasSave() && readableVoyage)
            {
                AchievementCatalog.CaptureProgress();
                PlayerPrefs.Save();
            }
            Application.targetFrameRate = 60;
            interfaceFont = CreateInterfaceFont();
            EnsureEventSystem();
            BuildCanvas("Main Menu Canvas", false);
            string pendingNotice = VoyageSuspensionPersistence.PendingNotice;
            VoyageSuspensionPersistence.PendingNotice = null;
            string notice = profileNotice ?? pendingNotice ?? voyageNotice;
            if (!string.IsNullOrEmpty(notice)) ShowSaveNotice(notice);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                BuildEditorPreview();
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                RemoveEditorPreview();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (saveNoticePanel != null && saveNoticePanel.gameObject.activeSelf)
            {
                CloseSaveNotice();
            }
            else if (confirmationPanel != null && confirmationPanel.gameObject.activeSelf)
            {
                confirmationPanel.gameObject.SetActive(false);
            }
            else if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
            {
                settingsPanel.gameObject.SetActive(false);
            }
            else if (achievementsPanel != null && achievementsPanel.gameObject.activeSelf)
            {
                achievementsPanel.gameObject.SetActive(false);
            }
        }

        private void BuildEditorPreview()
        {
            if (transform.Find(EditorPreviewCanvasName) != null)
            {
                return;
            }

            interfaceFont = CreateInterfaceFont();
            BuildCanvas(EditorPreviewCanvasName, true);
        }

        private void RemoveEditorPreview()
        {
            Transform preview = transform.Find(EditorPreviewCanvasName);
            if (preview == null)
            {
                return;
            }

            preview.gameObject.SetActive(false);
            ClearAchievementView();
            menuCanvas = null;
            if (Application.isPlaying)
            {
                Destroy(preview.gameObject);
            }
            else
            {
                DestroyImmediate(preview.gameObject);
            }
        }

        private void BuildCanvas(string canvasName, bool editorPreview)
        {
            GameObject canvasObject = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Keep the entire authored menu and modal surface visible at narrow
            // and ultrawide window ratios, just like the core gameplay canvas.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            menuCanvas = canvasRect;
            menuCanvasIsEditorPreview = editorPreview;
            ClearAchievementView();
            BuildBackground(canvasRect);
            BuildMainMenu(canvasRect);
            BuildSettingsPanel(canvasRect);
            BuildNewGameConfirmation(canvasRect);

            if (editorPreview)
            {
                SetEditorPreviewFlags(canvasObject.transform);
            }
        }

        private static void SetEditorPreviewFlags(Transform root)
        {
            root.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            for (int index = 0; index < root.childCount; index += 1)
            {
                SetEditorPreviewFlags(root.GetChild(index));
            }
        }

        private void BuildBackground(RectTransform canvasRect)
        {
            Texture2D backgroundTexture = Resources.Load<Texture2D>("MainMenu/main_menu_background");
            if (backgroundTexture != null)
            {
                // Keep the painted menu backdrop visually consistent with the
                // facility backgrounds when the envelope fitter scales it.
                backgroundTexture.filterMode = FilterMode.Bilinear;
                backgroundTexture.anisoLevel = 2;
                backgroundTexture.wrapMode = TextureWrapMode.Clamp;
                GameObject backgroundObject = new GameObject("Main Menu Background", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                backgroundObject.transform.SetParent(canvasRect, false);
                RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
                backgroundRect.anchorMin = Vector2.zero;
                backgroundRect.anchorMax = Vector2.one;
                backgroundRect.offsetMin = Vector2.zero;
                backgroundRect.offsetMax = Vector2.zero;
                RawImage background = backgroundObject.GetComponent<RawImage>();
                background.texture = backgroundTexture;
                background.color = Color.white;
                background.raycastTarget = false;
                AspectRatioFitter fitter = backgroundObject.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = backgroundTexture.width / (float)backgroundTexture.height;
            }
            else
            {
                CreateStretchPanel("Main Menu Background Fallback", canvasRect, DeepSea);
                Debug.LogWarning("Main menu background was not found at Resources/MainMenu/main_menu_background.");
            }

            Image atmosphere = CreateStretchPanel("Atmosphere Tint", canvasRect, new Color(0.01f, 0.04f, 0.06f, 0.16f)).GetComponent<Image>();
            atmosphere.raycastTarget = false;
            RectTransform leftVeil = CreateFixedPanel("Left Menu Veil", canvasRect, new Vector2(760f, 1080f), new Vector2(-580f, 0f), new Color(0.015f, 0.055f, 0.075f, 0.88f));
            leftVeil.GetComponent<Image>().raycastTarget = false;
            Image rule = CreateImage("Gold Rule", canvasRect, new Vector2(3f, 920f), new Vector2(-198f, 0f), new Color(Gold.r, Gold.g, Gold.b, 0.62f));
            rule.raycastTarget = false;
        }

        private void BuildMainMenu(RectTransform canvasRect)
        {
            RectTransform menu = CreateRect("Main Menu Content", canvasRect, new Vector2(620f, 1000f), new Vector2(-565f, 0f));

            Text eyebrow = CreateText("Eyebrow", menu, new Vector2(600f, 32f), new Vector2(0f, 444f), 20, FontStyle.Bold, TextAnchor.MiddleLeft, Html("#91C9B6"));
            eyebrow.text = "远海航路局 · 船长终端";

            Text title = CreateText("Game Title", menu, new Vector2(620f, 88f), new Vector2(0f, 380f), 58, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            title.text = GameClientInfo.DisplayName;
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            titleOutline.effectDistance = new Vector2(2f, -2f);

            Text englishTitle = CreateText("English Title", menu, new Vector2(600f, 32f), new Vector2(0f, 322f), 20, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            englishTitle.text = "T H E   S E V E N T H   H A R B O R";
            Text subtitle = CreateText("Subtitle", menu, new Vector2(600f, 188f), new Vector2(0f, 204f), 22, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            subtitle.text = SeventhPortLoreCatalog.CurrentIntroduction;
            // Preserve the entire story at a readable fixed size. Shrinking
            // this paragraph to fit hid its meaning at 720p.
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitle.verticalOverflow = VerticalWrapMode.Truncate;
            subtitle.resizeTextForBestFit = false;
            subtitle.lineSpacing = 1.22f;

            CreateImage("Menu Divider", menu, new Vector2(600f, 2f), new Vector2(0f, 100f), new Color(Gold.r, Gold.g, Gold.b, 0.48f)).raycastTarget = false;

            Button newGame = CreateMenuButton("New Game", menu, new Vector2(600f, 72f), new Vector2(0f, 54f), "开始新游戏", "从断锚港重新启航", Gold, DeepSea);
            newGame.onClick.AddListener(RequestNewGame);
            newGame.interactable = !ReleasePlayerSmokeCheck.IsRequested;

            continueButton = CreateMenuButton("Continue Game", menu, new Vector2(600f, 72f), new Vector2(0f, -30f), "继续游戏", BuildContinueDetail(), Teal, Ink);
            continueButton.interactable = (Application.isPlaying && profileRecoveryBlocked) || HasSave();
            continueButton.onClick.AddListener(ContinueGame);
            WoodButtonSkin.SetRole(continueButton, continueButton.interactable ? WoodButtonRole.Primary : WoodButtonRole.Secondary);
            WoodButtonSkin.SetRole(newGame, continueButton.interactable ? WoodButtonRole.Secondary : WoodButtonRole.Primary);

            Button settings = CreateMenuButton("Settings", menu, new Vector2(600f, 72f), new Vector2(0f, -114f), "设置", "", PanelRaised, Ink);
            settings.onClick.AddListener(() =>
            {
                readabilitySelector.Value = GameReadability.Level;
                settingsPanel.gameObject.SetActive(true);
            });

            Button achievements = CreateMenuButton("Achievements", menu, new Vector2(600f, 72f), new Vector2(0f, -198f), "成就", BuildAchievementProgress(), PanelRaised, Ink);
            achievementMenuDetail = achievements.transform.Find("Detail").GetComponent<Text>();
            achievements.onClick.AddListener(OpenAchievements);

            Button exit = CreateMenuButton("Exit Game", menu, new Vector2(600f, 72f), new Vector2(0f, -282f), "退出游戏", "", new Color(0.24f, 0.20f, 0.19f, 0.96f), Ink);
            exit.onClick.AddListener(ExitGame);

            Text saveState = CreateText("Save State", menu, new Vector2(600f, 56f), new Vector2(0f, -366f), 18, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            saveState.text = ReleasePlayerSmokeCheck.IsRequested ? "只读发布验收 · 未载入玩家存档" :
                Application.isPlaying && profileRecoveryBlocked ? "角色存档等待恢复 · 修复后可点击继续游戏重试" :
                HasSave() ? BuildSaveSummary() : "尚无航行记录";

            Text version = CreateText("Version", menu, new Vector2(600f, 30f), new Vector2(0f, -466f), 18, FontStyle.Normal, TextAnchor.MiddleLeft, MutedInk);
            version.text = "版本 " + Application.version + " · 本地存档";
            ApplyHomeReadingLayout(menu, subtitle, saveState, version);
        }

        private void ApplyHomeReadingLayout(RectTransform menu, Text introduction, Text saveState, Text version)
        {
            float scale = GameReadability.Scale;
            foreach (string name in new[] { "Eyebrow", "English Title" })
            {
                Text label = menu.Find(name).GetComponent<Text>();
                GameReadability.Body(label, 20);
                label.rectTransform.sizeDelta = new Vector2(600f, 40f);
            }
            GameReadability.Body(introduction, 22);
            GameReadability.Body(saveState, 18);
            GameReadability.Body(version, 18);
            string[] actions = { "New Game", "Continue Game", "Settings", "Achievements", "Exit Game" };
            float height = 72f + 40f * (scale - 1f), step = height + 12f;
            for (int i = 0; i < actions.Length; i++)
            {
                RectTransform button = menu.Find(actions[i]) as RectTransform;
                button.sizeDelta = new Vector2(600f, height);
                button.anchoredPosition = new Vector2(0f, 54f - i * step);
                Text title = button.Find("Title").GetComponent<Text>();
                Text detail = button.Find("Detail").GetComponent<Text>();
                GameReadability.Body(title, 26); GameReadability.Body(detail, 18);
                title.rectTransform.sizeDelta = new Vector2(460f, 34f * scale);
                title.rectTransform.anchoredPosition = new Vector2(-45f, 12f * scale);
                detail.rectTransform.sizeDelta = new Vector2(510f, 24f * scale);
                detail.rectTransform.anchoredPosition = new Vector2(-20f, -19f * scale);
            }
            saveState.rectTransform.sizeDelta = new Vector2(600f, 56f * scale);
            saveState.rectTransform.anchoredPosition = new Vector2(0f, 54f - 4f * step - height * .5f - 10f - 28f * scale);
            version.rectTransform.sizeDelta = new Vector2(600f, 32f);
            version.rectTransform.anchoredPosition = new Vector2(0f, -522f);
            if (GameReadability.Level == 0) return;
            // Keep the main actions in view while giving the complete introduction
            // its own scroll area; never shrink the paragraph to reclaim space.
            GameReadability.ScrollBody(introduction, menu, new Vector2(600f, 188f), new Vector2(0f, 204f));
        }

        private void BuildSettingsPanel(RectTransform canvasRect)
        {
            settingsPanel = CreateModalBackdrop("Settings Panel", canvasRect);
            RectTransform card = CreatePaperCard("Settings Card", settingsPanel, new Vector2(1040f, 960f));
            BuildModalHeading(card, "游戏设置", "应用后保存在本机，下次启动自动沿用。", "SETTINGS");

            float master = PlayerPrefs.GetFloat(MasterVolumeKey, 0.8f);
            float music = PlayerPrefs.GetFloat(MusicVolumeKey, 0.7f);
            float effects = PlayerPrefs.GetFloat(EffectsVolumeKey, 0.8f);
            masterSlider = CreateSettingSlider(card, "Master Volume", "主音量", 216f, master, out masterValue);
            musicSlider = CreateSettingSlider(card, "Music Volume", "音乐", 102f, music, out musicValue);
            effectsSlider = CreateSettingSlider(card, "Effects Volume",
                "战斗音效", -12f, effects, out effectsValue);
            masterSlider.interactable = true;
            musicSlider.interactable = true;
            effectsSlider.interactable = CoreGameplayDemoController.SoundEffectsEnabled;

            masterSlider.onValueChanged.AddListener(value => masterValue.text = Mathf.RoundToInt(value * 100f) + "%");
            musicSlider.onValueChanged.AddListener(value => musicValue.text = Mathf.RoundToInt(value * 100f) + "%");
            effectsSlider.onValueChanged.AddListener(value => effectsValue.text = Mathf.RoundToInt(value * 100f) + "%");

            fullScreenToggle = CreateToggle("Full Screen", card, new Vector2(600f, 64f), new Vector2(-30f, -124f), "全屏模式");
            fullScreenToggle.isOn = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;

            readabilitySelector = GameReadability.CreateSelector(card, "Menu Reading Size", new Vector2(0f, -230f), GameReadability.Level);
            Text hint = CreateText("Settings Hint", card, new Vector2(880f, 40f), new Vector2(0f, -300f), 18, FontStyle.Normal, TextAnchor.UpperLeft, PaperMutedInk);
            hint.text = "";

            Button apply = CreateButton("Apply Settings", card, new Vector2(400f, 76f), new Vector2(225f, -388f), Gold, DeepSea, 24);
            apply.GetComponentInChildren<Text>().text = "应用设置";
            apply.onClick.AddListener(ApplySettings);
            apply.interactable = !ReleasePlayerSmokeCheck.IsRequested;
            Button back = CreateButton("Back From Settings", card, new Vector2(400f, 76f), new Vector2(-225f, -388f), PanelRaised, Ink, 24);
            back.GetComponentInChildren<Text>().text = "返回主菜单";
            back.onClick.AddListener(() => settingsPanel.gameObject.SetActive(false));
            foreach (Text label in card.GetComponentsInChildren<Text>(true))
            {
                if (label.name == "Modal Title")
                {
                    GameReadability.Heading(label, 36);
                    label.rectTransform.sizeDelta = new Vector2(880f, 80f);
                    label.rectTransform.anchoredPosition = new Vector2(0f, 365f);
                }
                else if (label.name == "Modal Eyebrow" || label.name == "Modal Subtitle" || label.name == "Settings Hint")
                {
                    GameReadability.Body(label, 18);
                    label.rectTransform.sizeDelta = new Vector2(880f, 40f);
                    if (label.name == "Modal Eyebrow") label.rectTransform.anchoredPosition = new Vector2(0f, 438f);
                    if (label.name == "Modal Subtitle") label.rectTransform.anchoredPosition = new Vector2(0f, 298f);
                }
            }
            GameReadability.Body(apply.GetComponentInChildren<Text>(), 24);
            GameReadability.Body(back.GetComponentInChildren<Text>(), 24);
            settingsPanel.gameObject.SetActive(false);
        }

        private void ClearAchievementView()
        {
            achievementsPanel = null;
            achievementRows.Clear();
            achievementProgress = null;
            achievementFilteredCount = 0;
            achievementCategory = null;
            achievementCategories.Clear();
            achievementStateButtons.Clear();
            achievementEmpty = null;
            achievementStateFilter = 0;
            achievementMenuDetail = null;
            achievementSummary = null;
            achievementPageLabel = null;
            achievementPreviousPage = null;
            achievementNextPage = null;
            achievementPage = 0;
        }

        private void OpenAchievements()
        {
            if (Application.isPlaying && profileRecoveryBlocked)
            {
                ShowSaveNotice("角色存档尚未恢复，已暂停读取成就。请修复后点击继续游戏重试。");
                return;
            }
            if (menuCanvas == null) return;
            if (achievementProgress == null || achievementProgress.Length != AchievementCatalog.All.Count)
                achievementProgress = new AchievementProgressView[AchievementCatalog.All.Count];
            // Read progress once when opening. Paging only rebinds this snapshot.
            int unlockedCount = AchievementCatalog.FillProgress(achievementProgress);
            if (achievementsPanel == null)
            {
                BuildAchievementsPanel(menuCanvas);
                if (menuCanvasIsEditorPreview) SetEditorPreviewFlags(achievementsPanel);
            }
            string progress = BuildAchievementProgress(unlockedCount);
            achievementSummary.text = progress;
            achievementMenuDetail.text = progress;
            FilterAchievements(false);
            achievementsPanel.gameObject.SetActive(true);
        }

        private void BuildAchievementsPanel(RectTransform canvasRect)
        {
            achievementsPanel = CreateModalBackdrop("Achievements Panel", canvasRect);
            RectTransform card = CreatePaperCard("Achievements Card", achievementsPanel, new Vector2(1180f, 960f));
            achievementSummary = BuildModalHeading(card, "船长成就", string.Empty, "ACHIEVEMENTS");
            foreach (Text heading in card.GetComponentsInChildren<Text>(true))
            {
                if (heading.name == "Modal Eyebrow")
                {
                    GameReadability.Body(heading, 18);
                    heading.rectTransform.sizeDelta = new Vector2(880f, 32f);
                    heading.rectTransform.anchoredPosition = new Vector2(0f, 451f);
                }
                else if (heading.name == "Modal Title")
                {
                    GameReadability.Heading(heading, 36);
                    heading.rectTransform.sizeDelta = new Vector2(880f, 72f);
                    heading.rectTransform.anchoredPosition = new Vector2(0f, 390f);
                }
                else if (heading.name == "Modal Subtitle")
                {
                    GameReadability.Body(heading, 20);
                    heading.alignment = TextAnchor.MiddleCenter;
                    heading.rectTransform.sizeDelta = new Vector2(880f, 40f);
                    heading.rectTransform.anchoredPosition = new Vector2(0f, 331f);
                }
            }
            ((RectTransform)card.Find("Modal Divider")).anchoredPosition = new Vector2(0f, 307f);
            BuildAchievementFilters(card);

            achievementRows.Clear();
            achievementPage = 0;
            for (int slot = 0; slot < AchievementsPerPage; slot += 1)
            {
                achievementRows.Add(CreateAchievementRow(card, new Vector2(0f, 217f - 48f * (GameReadability.Scale - 1f) - slot * 73f * GameReadability.Scale)));
            }
            achievementEmpty = CreateText("Achievements Empty", card, new Vector2(980f, 80f), Vector2.zero, 24, FontStyle.Normal, TextAnchor.MiddleCenter, PaperMutedInk);
            achievementEmpty.text = "暂无符合条件的成就。";
            GameReadability.Body(achievementEmpty, 24);
            achievementEmpty.gameObject.SetActive(false);

            achievementPreviousPage = CreateButton("Achievements Previous Page", card, new Vector2(190f, 50f), new Vector2(-290f, -372f), PanelRaised, Ink, 22);
            achievementPreviousPage.GetComponentInChildren<Text>().text = "上一页";
            achievementPreviousPage.onClick.AddListener(() => SetAchievementPage(achievementPage - 1));
            achievementPageLabel = CreateText("Achievements Page Indicator", card, new Vector2(220f, 32f), new Vector2(0f, -372f), 22, FontStyle.Bold, TextAnchor.MiddleCenter, PaperInk);
            achievementNextPage = CreateButton("Achievements Next Page", card, new Vector2(190f, 50f), new Vector2(290f, -372f), PanelRaised, Ink, 22);
            achievementNextPage.GetComponentInChildren<Text>().text = "下一页";
            achievementNextPage.onClick.AddListener(() => SetAchievementPage(achievementPage + 1));
            GameReadability.Body(achievementPageLabel, 22);
            achievementPageLabel.rectTransform.sizeDelta = new Vector2(220f, 48f);
            foreach (Button pager in new[] { achievementPreviousPage, achievementNextPage })
            {
                pager.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 64f);
                pager.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(170f, 54f);
                GameReadability.Body(pager.GetComponentInChildren<Text>(), 22);
            }

            Button back = CreateButton("Back From Achievements", card, new Vector2(320f, 64f), new Vector2(0f, -436f), Gold, DeepSea, 22);
            GameReadability.Body(back.GetComponentInChildren<Text>(), 22);
            back.GetComponentInChildren<Text>().text = "返回主菜单";
            back.onClick.AddListener(() => achievementsPanel.gameObject.SetActive(false));
            achievementsPanel.gameObject.SetActive(false);
        }

        private void SetAchievementPage(int page)
        {
            int pageCount = Mathf.Max(1, (achievementFilteredCount + AchievementsPerPage - 1) / AchievementsPerPage);
            achievementPage = Mathf.Clamp(page, 0, pageCount - 1);
            for (int index = 0; index < achievementRows.Count; index += 1)
            {
                int filteredIndex = achievementPage * AchievementsPerPage + index;
                bool visible = filteredIndex < achievementFilteredCount;
                AchievementRowView row = achievementRows[index];
                if (visible)
                {
                    int catalogIndex = achievementFilteredIndices[filteredIndex];
                    BindAchievementRow(row, AchievementCatalog.All[catalogIndex], achievementProgress[catalogIndex]);
                }
                row.Root.gameObject.SetActive(visible);
            }
            if (achievementPageLabel != null)
                achievementPageLabel.text = (achievementPage + 1) + " / " + pageCount;
            if (achievementPreviousPage != null)
                achievementPreviousPage.interactable = achievementPage > 0;
            if (achievementNextPage != null)
                achievementNextPage.interactable = achievementPage < pageCount - 1;
            if (achievementEmpty != null) achievementEmpty.gameObject.SetActive(achievementFilteredCount == 0);
        }

        private void FilterAchievements(bool resetPage)
        {
            if (achievementProgress == null) return;
            string category = achievementCategory != null && achievementCategory.value > 0 ? achievementCategories[achievementCategory.value] : null;
            achievementFilteredCount = 0;
            for (int index = 0; index < AchievementCatalog.All.Count; index++)
            {
                if (category != null && AchievementCatalog.All[index].CategoryLabel != category) continue;
                if (achievementStateFilter == 1 && achievementProgress[index].Unlocked ||
                    achievementStateFilter == 2 && !achievementProgress[index].Unlocked) continue;
                achievementFilteredIndices[achievementFilteredCount++] = index;
            }
            for (int index = 0; index < achievementStateButtons.Count; index++)
            {
                Text label = achievementStateButtons[index].GetComponentInChildren<Text>();
                label.text = (index == achievementStateFilter ? "● " : string.Empty) + new[] { "全部", "未解锁", "已解锁" }[index];
            }
            SetAchievementPage(resetPage ? 0 : achievementPage);
        }

        private void BuildAchievementFilters(RectTransform card)
        {
            achievementCategories.Add("全部分类");
            foreach (CoreAchievementDefinition definition in AchievementCatalog.All)
                if (!achievementCategories.Contains(definition.CategoryLabel)) achievementCategories.Add(definition.CategoryLabel);
            float filterHeight = 40f * GameReadability.Scale;
            RectTransform root = CreateRect("Achievements Category", card, new Vector2(380f, filterHeight), new Vector2(-330f, 274f));
            Image face = root.gameObject.AddComponent<Image>(); face.color = new Color(.88f, .81f, .67f, 1f);
            achievementCategory = root.gameObject.AddComponent<Dropdown>();
            achievementCategory.targetGraphic = face;
            Text caption = CreateText("Category Label", root, new Vector2(314f, 32f), new Vector2(-12f, 0f), 20, FontStyle.Bold, TextAnchor.MiddleLeft, PaperInk);
            Text arrow = CreateText("Category Arrow", root, new Vector2(28f, 32f), new Vector2(164f, 0f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, PaperInk);
            arrow.text = "▼";
            GameReadability.Body(caption, 22);
            caption.rectTransform.sizeDelta = new Vector2(314f, filterHeight);
            GameReadability.Body(arrow, 18);
            arrow.rectTransform.sizeDelta = new Vector2(28f, filterHeight);
            RectTransform template = CreateRect("Template", root, new Vector2(380f, 390f), Vector2.zero);
            template.anchorMin = new Vector2(0f, 0f); template.anchorMax = new Vector2(1f, 0f); template.pivot = new Vector2(.5f, 1f);
            template.sizeDelta = new Vector2(0f, 390f); template.anchoredPosition = Vector2.zero;
            Image templateFace = template.gameObject.AddComponent<Image>(); templateFace.color = new Color(.94f, .88f, .74f, 1f);
            ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 32f; scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = CreateRect("Viewport", template, Vector2.zero, Vector2.zero);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("Content", viewport, new Vector2(0f, 36f), Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1f);
            RectTransform item = CreateRect("Item", content, new Vector2(0f, 36f), Vector2.zero);
            item.anchorMin = new Vector2(0f, .5f); item.anchorMax = new Vector2(1f, .5f);
            Image itemFace = item.gameObject.AddComponent<Image>(); itemFace.color = new Color(.91f, .84f, .70f, 1f);
            Toggle toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = itemFace;
            Image check = CreateImage("Selected", item, new Vector2(8f, 22f), new Vector2(-177f, 0f), Gold); check.raycastTarget = false; toggle.graphic = check;
            Text itemText = CreateText("Item Label", item, new Vector2(328f, 32f), new Vector2(9f, 0f), 20, FontStyle.Normal, TextAnchor.MiddleLeft, PaperInk);
            GameReadability.Body(itemText, 22);
            item.sizeDelta = new Vector2(0f, 40f * GameReadability.Scale);
            content.sizeDelta = item.sizeDelta;
            itemText.rectTransform.sizeDelta = new Vector2(328f, 36f * GameReadability.Scale);
            scroll.viewport = viewport; scroll.content = content;
            achievementCategory.template = template; achievementCategory.captionText = caption; achievementCategory.itemText = itemText;
            achievementCategory.AddOptions(achievementCategories);
            template.gameObject.SetActive(false);
            achievementCategory.onValueChanged.AddListener(_ => FilterAchievements(true));
            string[] names = { "All", "Locked", "Unlocked" };
            for (int index = 0; index < 3; index++)
            {
                int filter = index;
                Button button = CreateButton("Achievements Filter " + names[index], card, new Vector2(200f, filterHeight), new Vector2(-5f + index * 210f, 274f), PanelRaised, Ink, 22);
                GameReadability.Body(button.GetComponentInChildren<Text>(), 22);
                button.onClick.AddListener(() => { achievementStateFilter = filter; FilterAchievements(true); });
                achievementStateButtons.Add(button);
            }
        }

        private void BuildNewGameConfirmation(RectTransform canvasRect)
        {
            confirmationPanel = CreateModalBackdrop("New Game Confirmation", canvasRect);
            RectTransform card = CreatePaperCard("Confirmation Card", confirmationPanel, new Vector2(1040f, 580f));
            Text eyebrow = CreateText("Confirmation Eyebrow", card, new Vector2(920f, 40f), new Vector2(0f, 230f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, PaperAccent);
            GameReadability.Body(eyebrow, 18);
            eyebrow.text = "NEW VOYAGE";
            Text title = CreateText("Confirmation Title", card, new Vector2(920f, 80f), new Vector2(0f, 158f), 34, FontStyle.Bold, TextAnchor.MiddleCenter, PaperInk);
            GameReadability.Heading(title, 34);
            title.text = "开始新的航行？";
            Text body = CreateText("Confirmation Body", card, new Vector2(920f, 186f), new Vector2(0f, 3f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, PaperMutedInk);
            GameReadability.Body(body, 22);
            body.text = "现有航程、教学进度、金币、贵重藏品及陈列记录将被重置。\n游戏设置与美术资源不会受到影响。";
            Button cancel = CreateButton("Cancel New Game", card, new Vector2(390f, 80f), new Vector2(-225f, -202f), PanelRaised, Ink, 22);
            GameReadability.Body(cancel.GetComponentInChildren<Text>(), 22);
            cancel.GetComponentInChildren<Text>().text = "保留存档";
            cancel.onClick.AddListener(() => confirmationPanel.gameObject.SetActive(false));
            Button confirm = CreateButton("Confirm New Game", card, new Vector2(390f, 80f), new Vector2(225f, -202f), Coral, Ink, 22);
            GameReadability.Body(confirm.GetComponentInChildren<Text>(), 22);
            confirm.GetComponentInChildren<Text>().text = "确认重新开始";
            confirm.onClick.AddListener(StartNewGame);
            confirmationPanel.gameObject.SetActive(false);
        }

        private void RequestNewGame()
        {
            if (ReleasePlayerSmokeCheck.IsRequested) return;
            if ((Application.isPlaying && profileRecoveryBlocked) || HasSave())
            {
                confirmationPanel.gameObject.SetActive(true);
                return;
            }

            StartNewGame();
        }

        private static void StartNewGame()
        {
            if (ReleasePlayerSmokeCheck.IsRequested) return;
            if (Application.isPlaying && !VoyageSuspensionPersistence.TryClear(out string clearError))
            {
                FindFirstObjectByType<MainMenuController>()?.ShowSaveNotice("无法安全清理旧航程，未重置角色存档。\n" + clearError);
                return;
            }
            if (Application.isPlaying && !ProfileSaveJournal.TryReset(out string journalError))
            {
                FindFirstObjectByType<MainMenuController>()?.ShowSaveNotice("新游戏尚未创建；旧的海上进度已清除，角色资料已保留。\n请修复存储问题后重试。\n" + journalError);
                return;
            }
            profileRecoveryBlocked = false;
            AchievementCatalog.ClearProgress();
            CoreGameplayDemoController.InitializeNewGameCalendar(DateTime.Now);
            DateTime startDate = CoreGameplayDemoController.SavedGameDateTime();
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchGameClockKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchRealClockKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentPurchaseAchievementKey);
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, Guid.NewGuid().ToString("N"));
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialCompletedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialRewardGrantedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialCheckpointKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialDeckEditedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialComponentRewardKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialRecoveryKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialNavigatorEventKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseOwnedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseDisplayKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehousePortMigrationKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.CollectionProgressKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseDisplayRecordKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.MarketPurchaseStorageModeKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.PortWarehouseCargoKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DrawnSeaChartKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.SeaChartProgressKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.RecordedSeaChartsKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TreasureProgressKey);
            ProfileGoldPersistence.Reset();
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GameMasterShopScopeKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.CurrentPortKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.SelectedShipKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleDeckKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleCardUpgradeKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.SpecialCardExperienceKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.AcquiredSpecialCardsKey);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.StageBattleDeckKey(stage));
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.LegacyWitchRecruitedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.CrewAtSeaKey);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id));
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchMissionKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchEndsAtKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchDurationKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchCrewKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchCrewIdentityKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DispatchBalanceVersionKey);
            for (int index = 0; index < 6; index += 1)
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.CrewRecruitmentKey(index));
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.ActiveCrewVariantKey(index));
            }
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id));
            }
            for (int index = 0; index < CoreVoyageSession.Ships.Count; index += 1)
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.ShipOwnedKey(index));
            }
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentLoadoutKey(slot));
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentConditionKey(slot));
            }
            ComponentArtCatalog componentArt = new ComponentArtCatalog();
            string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            for (int portIndex = 0; portIndex < ports.Length; portIndex += 1)
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.PortInvestmentKey(ports[portIndex]));
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseOwnedPortKey(ports[portIndex]));
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseDisplayPortKey(ports[portIndex]));
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.GameMasterShopRefreshKey(ports[portIndex]));
                for (int marketIndex = 0; marketIndex < PortMarketCatalog.AllItems.Count; marketIndex += 1)
                {
                    PlayerPrefs.DeleteKey(CoreGameplayDemoController.MarketPurchaseKey(
                        ports[portIndex], startDate, PortMarketCatalog.AllItems[marketIndex]));
                }
                foreach (PortComponentShopStockItem offer in PortComponentShopCatalog.BuildStock(componentArt, ports[portIndex], startDate))
                {
                    PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentShopPurchaseKey(offer.Id));
                }
            }
            string[] cargoResources = { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame" };
            for (int index = 0; index < cargoResources.Length; index += 1)
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.CargoKey(cargoResources[index]));
            }
            PlayerPrefs.Save();
            SceneManager.LoadScene(MainSceneName);
        }

        private static void ContinueGame()
        {
            if (ReleasePlayerSmokeCheck.IsRequested) return;
            if (Application.isPlaying && !ProfileSaveJournal.TryRecover(out string journalNotice))
            {
                profileRecoveryBlocked = true;
                FindFirstObjectByType<MainMenuController>()?.ShowSaveNotice(journalNotice);
                return;
            }
            profileRecoveryBlocked = false;
            if (!VoyageSuspensionPersistence.TryRead(out _, out string reason))
            {
                FindFirstObjectByType<MainMenuController>()?.ShowSaveNotice(reason);
                return;
            }
            SceneManager.LoadScene(MainSceneName);
        }

        private void ShowSaveNotice(string notice)
        {
            if (saveNoticePanel != null) Destroy(saveNoticePanel.gameObject);
            RectTransform canvas = transform.Find("Main Menu Canvas") as RectTransform;
            if (canvas == null) return;
            saveNoticePanel = CreateModalBackdrop("Voyage Save Notice", canvas);
            RectTransform card = CreatePaperCard("Voyage Save Notice Card", saveNoticePanel, new Vector2(1040f, 640f));
            Text title = CreateText("Voyage Save Notice Title", card, new Vector2(920f, 80f), new Vector2(0f, 246f), 32, FontStyle.Bold, TextAnchor.MiddleCenter, PaperInk);
            GameReadability.Heading(title);
            title.text = "航程存档提示";
            Text body = CreateText("Voyage Save Notice Body", card, new Vector2(780f, 205f), new Vector2(0f, 0f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, PaperMutedInk);
            body.text = notice;
            GameReadability.ScrollBody(body, card, new Vector2(920f, 340f), new Vector2(0f, 0f));
            Button close = CreateButton("Close Voyage Save Notice", card, new Vector2(360f, 80f), new Vector2(0f, -246f), Gold, DeepSea, 24);
            GameReadability.Body(close.GetComponentInChildren<Text>(), 24);
            close.GetComponentInChildren<Text>().text = "知道了";
            close.onClick.AddListener(CloseSaveNotice);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }

        private void CloseSaveNotice()
        {
            if (saveNoticePanel == null) return;
            saveNoticePanel.gameObject.SetActive(false);
            Destroy(saveNoticePanel.gameObject);
            saveNoticePanel = null;
            if (EventSystem.current != null && continueButton != null)
                EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        private void ApplySettings()
        {
            if (!Application.isPlaying || ReleasePlayerSmokeCheck.IsRequested) return;
            bool readingChanged = readabilitySelector.Value != GameReadability.Level;
            PlayerPrefs.SetFloat(MasterVolumeKey, masterSlider.value);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicSlider.value);
            if (CoreGameplayDemoController.SoundEffectsEnabled) PlayerPrefs.SetFloat(EffectsVolumeKey, effectsSlider.value);
            PlayerPrefs.SetInt(FullScreenKey, fullScreenToggle.isOn ? 1 : 0);
            PlayerPrefs.SetInt(GameReadability.PreferenceKey, readabilitySelector.Value);
            PlayerPrefs.Save();
            Screen.fullScreen = fullScreenToggle.isOn;
            settingsPanel.gameObject.SetActive(false);
            if (readingChanged && menuCanvas != null)
            {
                GameObject previous = menuCanvas.gameObject;
                string canvasName = previous.name;
                previous.SetActive(false);
                BuildCanvas(canvasName, menuCanvasIsEditorPreview);
                Destroy(previous);
            }
        }

        private static void ExitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Exit requested from the main menu. Application.Quit runs in a player build.");
#else
            Application.Quit();
#endif
        }

        private static bool HasSave()
        {
            // File-store reads acquire/create lock files. A smoke run must not
            // probe or recover the player's suspended voyage, even for labels.
            if (ReleasePlayerSmokeCheck.IsRequested) return false;
            if (Application.isPlaying && profileRecoveryBlocked) return false;
            return VoyageSuspensionPersistence.HasCheckpoint || PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialCompletedKey) ||
                PlayerPrefs.HasKey(CoreGameplayDemoController.ProfileGoldKey) || PlayerPrefs.HasKey(ProfileGoldPersistence.DecimalKey) ||
                PlayerPrefs.HasKey(CoreGameplayDemoController.CurrentPortKey);
        }

        private static string BuildContinueDetail()
        {
            if (ReleasePlayerSmokeCheck.IsRequested) return "只读发布验收 · 未载入玩家存档";
            if (Application.isPlaying && profileRecoveryBlocked) return "角色保存等待恢复 · 点击重试";
            return HasSave() ? BuildSaveSummary() : "暂无可继续的航行记录";
        }

        private static string BuildSaveSummary()
        {
            if (Application.isPlaying && profileRecoveryBlocked) return "角色存档等待恢复 · 原文件已保留";
            if (Application.isPlaying)
            {
                if (!VoyageSuspensionPersistence.TryRead(out VoyageSuspensionRecord voyage, out _)) return "航程存档需要检查 · 原文件已保留";
                if (voyage != null) return "继续已保存的航程 · " + voyage.voyage.departurePort + " → " + voyage.voyage.destinationPort;
            }
            string port = PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
            decimal gold = ProfileGoldPersistence.Load();
            return port + " · " + PortMarketCatalog.FormatPrice(gold) + " 金币";
        }

        private static string BuildAchievementProgress()
        {
            if (Application.isPlaying && profileRecoveryBlocked) return "角色存档等待恢复";
            return BuildAchievementProgress(AchievementCatalog.CountUnlocked());
        }

        private static string BuildAchievementProgress(int unlockedCount)
        {
            return "已解锁 " + unlockedCount + " / " + AchievementCatalog.TotalCount;
        }

        private static RectTransform CreatePaperCard(string name, Transform parent, Vector2 size)
        {
            RectTransform card = CreateFixedPanel(name, parent, size, Vector2.zero, new Color(Panel.r, Panel.g, Panel.b, 0.99f));
            // These dialogs use parchment; pair a light paper face with dark
            // reading ink instead of the former pale text for blue panels.
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.94f, 1f);
            return card;
        }

        private Text BuildModalHeading(RectTransform card, string titleValue, string subtitleValue, string eyebrowValue)
        {
            Text eyebrow = CreateText("Modal Eyebrow", card, new Vector2(880f, 28f), new Vector2(0f, card.rect.height * 0.5f - 58f), 18, FontStyle.Bold, TextAnchor.MiddleCenter, PaperAccent);
            eyebrow.text = eyebrowValue;
            Text title = CreateText("Modal Title", card, new Vector2(880f, 52f), new Vector2(0f, card.rect.height * 0.5f - 105f), 36, FontStyle.Bold, TextAnchor.MiddleCenter, PaperInk);
            title.text = titleValue;
            Text subtitle = CreateText("Modal Subtitle", card, new Vector2(880f, 36f), new Vector2(0f, card.rect.height * 0.5f - 150f), 20, FontStyle.Normal, TextAnchor.UpperCenter, PaperMutedInk);
            subtitle.text = subtitleValue;
            CreateImage("Modal Divider", card, new Vector2(card.rect.width - 120f, 2f), new Vector2(0f, card.rect.height * 0.5f - 185f), new Color(PaperMutedInk.r, PaperMutedInk.g, PaperMutedInk.b, 0.4f)).raycastTarget = false;
            return subtitle;
        }

        private Slider CreateSettingSlider(RectTransform parent, string objectName, string labelValue, float y, float value, out Text valueText)
        {
            Text label = CreateText(objectName + " Label", parent, new Vector2(200f, 54f), new Vector2(-280f, y), 24, FontStyle.Normal, TextAnchor.MiddleLeft, PaperInk);
            GameReadability.Body(label, 24);
            label.text = labelValue;
            Slider slider = CreateSlider(objectName, parent, new Vector2(410f, 42f), new Vector2(40f, y), value);
            valueText = CreateText(objectName + " Value", parent, new Vector2(140f, 54f), new Vector2(355f, y), 22, FontStyle.Normal, TextAnchor.MiddleRight, PaperInk);
            GameReadability.Body(valueText, 22);
            valueText.text = Mathf.RoundToInt(value * 100f) + "%";
            return slider;
        }

        private AchievementRowView CreateAchievementRow(RectTransform parent, Vector2 position)
        {
            float scale = GameReadability.Scale;
            RectTransform row = CreateRect("Achievement Row", parent, new Vector2(1040f, 64f * scale), position);
            row.gameObject.AddComponent<Image>();
            Image rowFace = row.GetComponent<Image>();
            RawImage icon = CreateRect("Achievement Icon", row,
                new Vector2(48f, 48f), new Vector2(-477f, 0f)).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            Text title = CreateText("Title", row, new Vector2(680f, 30f), new Vector2(-95f, 15f), 24, FontStyle.Bold, TextAnchor.MiddleLeft, PaperInk);
            Text detail = CreateText("Description", row, new Vector2(680f, 26f), new Vector2(-95f, -17f), 18, FontStyle.Normal, TextAnchor.MiddleLeft, PaperMutedInk);
            Text state = CreateText("State", row, new Vector2(220f, 26f), new Vector2(395f, 14f), 18, FontStyle.Bold, TextAnchor.MiddleRight, PaperMutedInk);
            Text progress = CreateText("Progress", row, new Vector2(220f, 26f), new Vector2(395f, -15f), 18, FontStyle.Normal, TextAnchor.MiddleRight, PaperMutedInk);
            foreach (Text label in new[] { title, detail, state, progress })
            {
                GameReadability.Body(label, label == title ? 24 : 18);
                label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, label.rectTransform.sizeDelta.y * scale);
                label.rectTransform.anchoredPosition = new Vector2(label.rectTransform.anchoredPosition.x, label.rectTransform.anchoredPosition.y * scale);
            }
            return new AchievementRowView { Root = row, Face = rowFace, Icon = icon, Title = title, Description = detail, State = state, Progress = progress };
        }

        private static void BindAchievementRow(AchievementRowView row, CoreAchievementDefinition achievement, AchievementProgressView progress)
        {
            bool unlocked = progress.Unlocked;
            row.Root.name = "Achievement Row " + achievement.Id;
            row.Face.color = new Color(PaperMutedInk.r, PaperMutedInk.g, PaperMutedInk.b, unlocked ? 0.12f : 0.05f);
            ItemIconArt art = AchievementIconCatalog.ForId(achievement.Id);
            row.Icon.name = "Achievement Icon " + achievement.Id;
            row.Icon.texture = art.Texture;
            row.Icon.uvRect = art.Uv;
            row.Icon.enabled = art.IsAvailable;
            row.Icon.color = unlocked ? Color.white : new Color(.72f, .76f, .80f, .94f);
            UiImageAspect.Fit(row.Icon, new Vector2(48f, 48f));
            row.Title.text = achievement.Title;
            row.Description.text = achievement.Description;
            row.State.color = unlocked ? PaperAccent : PaperMutedInk;
            row.State.text = unlocked ? "已解锁" : "未解锁";
            row.Progress.text = unlocked || !progress.IsQuantified ? string.Empty : progress.Detail ??
                progress.Current.ToString("0.####") + " / " + progress.Target.ToString("0.####");
        }

        private RectTransform CreateModalBackdrop(string name, RectTransform canvasRect)
        {
            RectTransform backdrop = CreateStretchPanel(name, canvasRect, new Color(0.005f, 0.018f, 0.024f, 0.92f));
            backdrop.GetComponent<Image>().raycastTarget = true;
            return backdrop;
        }

        private Button CreateMenuButton(string name, Transform parent, Vector2 size, Vector2 position, string title, string detail, Color normalColor, Color textColor)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = normalColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.16f, 0.21f, 0.22f, 0.82f);
            button.colors = colors;
            Text titleText = CreateText("Title", rect, new Vector2(size.x - 140f, 34f), new Vector2(-45f, 12f), 26, FontStyle.Bold, TextAnchor.MiddleLeft, textColor);
            titleText.text = title;
            Text detailText = CreateText("Detail", rect, new Vector2(size.x - 90f, 24f), new Vector2(-20f, -19f), 18, FontStyle.Normal, TextAnchor.MiddleLeft, textColor);
            detailText.color = new Color(detailText.color.r, detailText.color.g, detailText.color.b, 0.75f);
            detailText.text = detail;
            Text arrow = CreateText("Arrow", rect, new Vector2(44f, 54f), new Vector2(size.x * 0.5f - 35f, 0f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            arrow.text = "›";
            WoodButtonSkin.Apply(button, normalColor);
            return button;
        }

        private Slider CreateSlider(string name, Transform parent, Vector2 size, Vector2 position, float value)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            RectTransform background = CreateRect("Background", root, new Vector2(size.x, 8f), Vector2.zero);
            background.gameObject.AddComponent<Image>().color = DeepSea;
            NauticalSurfaceSkin.ApplyControlPart(background.GetComponent<Image>());
            RectTransform fillArea = CreateRect("Fill Area", root, new Vector2(size.x - 20f, 8f), new Vector2(-10f, 0f));
            RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = Gold;
            NauticalSurfaceSkin.ApplyControlPart(fill.GetComponent<Image>());
            RectTransform handleArea = CreateRect("Handle Slide Area", root, new Vector2(size.x - 24f, size.y), Vector2.zero);
            // Slider stretches the handle over its track area's height.
            Image handle = CreateImage("Handle", handleArea, new Vector2(26f, 0f), Vector2.zero, Gold);
            slider.fillRect = fill;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.value = value;
            return slider;
        }

        private Toggle CreateToggle(string name, Transform parent, Vector2 size, Vector2 position, string labelValue)
        {
            RectTransform root = CreateRect(name, parent, size, position);
            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            Image background = CreateImage("Background", root, new Vector2(34f, 34f), new Vector2(-280f, 0f), DeepSea);
            Image checkmark = CreateImage("Checkmark", background.rectTransform, new Vector2(22f, 22f), Vector2.zero, Gold);
            Text label = CreateText("Label", root, new Vector2(510f, 54f), new Vector2(28f, 0f), 24, FontStyle.Normal, TextAnchor.MiddleLeft, PaperInk);
            GameReadability.Body(label, 24);
            label.text = labelValue;
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            return toggle;
        }

        private Button CreateButton(string name, Transform parent, Vector2 size, Vector2 position, Color normalColor, Color textColor, int fontSize)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = normalColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.26f, 0.27f, 0.75f);
            button.colors = colors;
            Text label = CreateText("Label", rect, size - new Vector2(24f, 10f), Vector2.zero, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor);
            label.text = name;
            WoodButtonSkin.Apply(button, normalColor);
            return button;
        }

        private Text CreateText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, FontStyle style, TextAnchor alignment, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Text text = rect.gameObject.AddComponent<Text>();
            GameTypography.Apply(text, fontSize, style);
            text.alignment = alignment;
            text.color = NauticalSurfaceSkin.ReadableTextColor(parent, color);
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static RectTransform CreateStretchPanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            if (name == "Fill") NauticalSurfaceSkin.ApplyControlPart(panel.GetComponent<Image>());
            else NauticalSurfaceSkin.ApplyPanel(panel.GetComponent<Image>(), color);
            return rect;
        }

        private static RectTransform CreateFixedPanel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            NauticalSurfaceSkin.ApplyPanel(image, color);
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            NauticalSurfaceSkin.ApplyControlPart(image);
            return image;
        }

        private static Font CreateInterfaceFont()
        {
            return GameTypography.InterfaceFont;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject input = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.GetComponent<InputSystemUIInputModule>().actionsAsset = InputSystem.actions;
        }

        private static Color Html(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.magenta;
        }
    }
}
