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
        private readonly List<AchievementRowView> achievementRows = new List<AchievementRowView>(AchievementsPerPage);
        private AchievementProgressView[] achievementProgress;
        private readonly int[] achievementFilteredIndices = new int[AchievementCatalog.TotalCount];
        private int achievementFilteredCount;
        private Dropdown achievementCategory;
        private readonly List<string> achievementCategories = new List<string>();
        private readonly List<Button> achievementStateButtons = new List<Button>(3);
        private Text achievementEmpty;
        private int achievementStateFilter;
        private Text achievementSummary;
        private Text achievementPageLabel;
        private Button achievementPreviousPage;
        private Button achievementNextPage;
        private int achievementPage;
        private const int AchievementsPerPage = 8;
        private static bool profileRecoveryBlocked;

        // Startup visibility and progress capture share one validated snapshot;
        // every player action still revalidates the current save.
        private readonly struct MenuSaveView
        {
            internal readonly bool HasSave;
            internal readonly bool ReadableVoyage;
            internal readonly string VoyageNotice;

            internal MenuSaveView(bool hasSave, bool readableVoyage, string voyageNotice)
            {
                HasSave = hasSave;
                ReadableVoyage = readableVoyage;
                VoyageNotice = voyageNotice;
            }
        }

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
            MenuSaveView saveView = ReadMenuSaveView();
            if (saveView.HasSave && saveView.ReadableVoyage)
            {
                AchievementCatalog.CaptureProgress();
                PlayerPrefs.Save();
            }
            Application.targetFrameRate = 60;
            interfaceFont = CreateInterfaceFont();
            EnsureEventSystem();
            BuildCanvas("Main Menu Canvas", false, saveView);
            string pendingNotice = VoyageSuspensionPersistence.PendingNotice;
            VoyageSuspensionPersistence.PendingNotice = null;
            string notice = profileNotice ?? pendingNotice ?? saveView.VoyageNotice;
            if (!string.IsNullOrEmpty(notice)) ShowSaveNotice(notice);
            StartCoroutine(RuntimeArtMemory.CollectAfterReturningToMenu());
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
            Transform existing = transform.Find(EditorPreviewCanvasName);
            if (existing != null)
            {
                Canvas previewCanvas = existing.GetComponent<Canvas>();
                if (previewCanvas != null)
                {
                    menuCanvas = FixedGameViewport.Create(previewCanvas);
                    menuCanvasIsEditorPreview = true;
                }
                return;
            }

            interfaceFont = CreateInterfaceFont();
            BuildCanvas(EditorPreviewCanvasName, true, ReadMenuSaveView());
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

        private void BuildCanvas(string canvasName, bool editorPreview, MenuSaveView saveView)
        {
            GameObject canvasObject = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Fit the fixed 16:9 menu and its dialogs uniformly inside any window.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform canvasRect = FixedGameViewport.Create(canvas);
            menuCanvas = canvasRect;
            menuCanvasIsEditorPreview = editorPreview;
            ClearAchievementView();
            settingsPanel = null;
            confirmationPanel = null;
            BuildBackground(canvasRect);
            BuildMainMenu(canvasRect, saveView);
            if (editorPreview)
            {
                // Keep complete inactive surfaces available to scene previews and
                // editor captures; players create optional dialogs on first use.
                BuildSettingsPanel(canvasRect);
                BuildNewGameConfirmation(canvasRect);
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

        }

        private void BuildMainMenu(RectTransform canvasRect, MenuSaveView saveView)
        {
            // The artwork keeps its quiet sky on the left; the sunset harbor
            // remains visible without an opaque navigation panel.
            RectTransform menu = CreateRect("Main Menu Content", canvasRect, new Vector2(1000f, 1000f), new Vector2(-280f, 0f));
            BuildGameTitle(menu);

            Button newGame = CreateMenuButton("New Game", menu, new Vector2(580f, 62f), new Vector2(0f, -35f),
                "开始新游戏", saveView.HasSave ? Ink : Gold);
            newGame.onClick.AddListener(RequestNewGame);
            newGame.interactable = !ReleasePlayerSmokeCheck.IsRequested;

            continueButton = CreateMenuButton("Continue Game", menu, new Vector2(580f, 62f), new Vector2(0f, -101f),
                "继续游戏", saveView.HasSave ? Gold : Ink);
            continueButton.interactable = (Application.isPlaying && profileRecoveryBlocked) || saveView.HasSave;
            continueButton.onClick.AddListener(ContinueGame);

            Button settings = CreateMenuButton("Settings", menu, new Vector2(580f, 62f), new Vector2(0f, -167f), "设置", Ink);
            settings.onClick.AddListener(OpenSettings);

            Button achievements = CreateMenuButton("Achievements", menu, new Vector2(580f, 62f), new Vector2(0f, -233f), "成就", Ink);
            achievements.onClick.AddListener(OpenAchievements);

            Button exit = CreateMenuButton("Exit Game", menu, new Vector2(580f, 62f), new Vector2(0f, -299f), "退出游戏", Ink);
            exit.onClick.AddListener(ExitGame);
        }

        private void BuildGameTitle(RectTransform menu)
        {
            Texture2D texture = Resources.Load<Texture2D>("MainMenu/main_menu_title_bilingual");
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                // Size follows the source aspect, preserving the transparent
                // bilingual lettering instead of stretching it into a banner.
                float width = 920f;
                RawImage title = CreateRect("Game Title Artwork", menu,
                    new Vector2(width, width * texture.height / texture.width), new Vector2(0f, 250f))
                    .gameObject.AddComponent<RawImage>();
                title.texture = texture;
                title.color = Color.white;
                title.raycastTarget = false;
                return;
            }

            Text fallback = CreateText("Game Title", menu, new Vector2(900f, 130f), new Vector2(0f, 300f),
                96, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            fallback.text = GameClientInfo.DisplayName;
            AddMenuTextOutline(fallback);
            Text english = CreateText("English Title", menu, new Vector2(900f, 48f), new Vector2(0f, 190f),
                28, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            english.text = "THE SEVENTH HARBOR";
            AddMenuTextOutline(english);
        }

        private void OpenSettings()
        {
            if (menuCanvas == null) return;
            if (settingsPanel == null)
            {
                BuildSettingsPanel(menuCanvas);
                if (menuCanvasIsEditorPreview) SetEditorPreviewFlags(settingsPanel);
            }
            settingsPanel.gameObject.SetActive(true);
        }

        private void BuildSettingsPanel(RectTransform canvasRect)
        {
            settingsPanel = CreateModalBackdrop("Settings Panel", canvasRect);
            RectTransform card = CreatePaperCard("Settings Card", settingsPanel, new Vector2(1040f, 800f));
            BuildModalHeading(card, "游戏设置", "应用后保存在本机，下次启动自动沿用。", "SETTINGS");

            float master = PlayerPrefs.GetFloat(MasterVolumeKey, 0.8f);
            float music = PlayerPrefs.GetFloat(MusicVolumeKey, 0.7f);
            float effects = PlayerPrefs.GetFloat(EffectsVolumeKey, 0.8f);
            masterSlider = CreateSettingSlider(card, "Master Volume", "主音量", 156f, master, out masterValue);
            musicSlider = CreateSettingSlider(card, "Music Volume", "音乐", 52f, music, out musicValue);
            effectsSlider = CreateSettingSlider(card, "Effects Volume",
                "战斗音效", -52f, effects, out effectsValue);
            masterSlider.interactable = true;
            musicSlider.interactable = true;
            effectsSlider.interactable = CoreGameplayDemoController.SoundEffectsEnabled;

            masterSlider.onValueChanged.AddListener(value => masterValue.text = Mathf.RoundToInt(value * 100f) + "%");
            musicSlider.onValueChanged.AddListener(value => musicValue.text = Mathf.RoundToInt(value * 100f) + "%");
            effectsSlider.onValueChanged.AddListener(value => effectsValue.text = Mathf.RoundToInt(value * 100f) + "%");

            fullScreenToggle = CreateToggle("Full Screen", card, new Vector2(600f, 54f), new Vector2(-30f, -160f), "全屏模式");
            fullScreenToggle.isOn = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;

            Text hint = CreateText("Settings Hint", card, new Vector2(880f, 54f), new Vector2(0f, -235f), 20, FontStyle.Normal, TextAnchor.UpperLeft, PaperMutedInk);
            hint.text = "";

            Button apply = CreateButton("Apply Settings", card, new Vector2(320f, 62f), new Vector2(190f, -317f), Gold, DeepSea, 24);
            apply.GetComponentInChildren<Text>().text = "应用设置";
            apply.onClick.AddListener(ApplySettings);
            apply.interactable = !ReleasePlayerSmokeCheck.IsRequested;
            Button back = CreateButton("Back From Settings", card, new Vector2(300f, 62f), new Vector2(-190f, -317f), PanelRaised, Ink, 24);
            back.GetComponentInChildren<Text>().text = "返回主菜单";
            back.onClick.AddListener(() => settingsPanel.gameObject.SetActive(false));
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
            FilterAchievements(false);
            achievementsPanel.gameObject.SetActive(true);
        }

        private void BuildAchievementsPanel(RectTransform canvasRect)
        {
            achievementsPanel = CreateModalBackdrop("Achievements Panel", canvasRect);
            RectTransform card = CreatePaperCard("Achievements Card", achievementsPanel, new Vector2(1180f, 960f));
            achievementSummary = BuildModalHeading(card, "船长成就", string.Empty, "ACHIEVEMENTS");
            BuildAchievementFilters(card);

            achievementRows.Clear();
            achievementPage = 0;
            for (int slot = 0; slot < AchievementsPerPage; slot += 1)
            {
                achievementRows.Add(CreateAchievementRow(card, new Vector2(0f, 217f - slot * 73f)));
            }
            achievementEmpty = CreateText("Achievements Empty", card, new Vector2(980f, 80f), Vector2.zero, 24, FontStyle.Normal, TextAnchor.MiddleCenter, PaperMutedInk);
            achievementEmpty.text = "暂无符合条件的成就。";
            achievementEmpty.gameObject.SetActive(false);

            // Keep a gap between the final row, paging controls, return action
            // and the parchment's lower inner frame.
            achievementPreviousPage = CreateButton("Achievements Previous Page", card, new Vector2(190f, 38f), new Vector2(-290f, -360f), PanelRaised, Ink, 22);
            achievementPreviousPage.GetComponentInChildren<Text>().text = "上一页";
            achievementPreviousPage.onClick.AddListener(() => SetAchievementPage(achievementPage - 1));
            achievementPageLabel = CreateText("Achievements Page Indicator", card, new Vector2(220f, 32f), new Vector2(0f, -360f), 22, FontStyle.Bold, TextAnchor.MiddleCenter, PaperInk);
            achievementNextPage = CreateButton("Achievements Next Page", card, new Vector2(190f, 38f), new Vector2(290f, -360f), PanelRaised, Ink, 22);
            achievementNextPage.GetComponentInChildren<Text>().text = "下一页";
            achievementNextPage.onClick.AddListener(() => SetAchievementPage(achievementPage + 1));

            Button back = CreateButton("Back From Achievements", card, new Vector2(320f, 38f), new Vector2(0f, -406f), Gold, DeepSea, 22);
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
            RectTransform root = CreateRect("Achievements Category", card, new Vector2(380f, 32f), new Vector2(-330f, 274f));
            Image face = root.gameObject.AddComponent<Image>(); face.color = new Color(.88f, .81f, .67f, 1f);
            achievementCategory = root.gameObject.AddComponent<Dropdown>();
            achievementCategory.targetGraphic = face;
            Text caption = CreateText("Category Label", root, new Vector2(314f, 32f), new Vector2(-12f, 0f), 20, FontStyle.Bold, TextAnchor.MiddleLeft, PaperInk);
            Text arrow = CreateText("Category Arrow", root, new Vector2(28f, 32f), new Vector2(164f, 0f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, PaperInk);
            arrow.text = "▼";
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
            scroll.viewport = viewport; scroll.content = content;
            achievementCategory.template = template; achievementCategory.captionText = caption; achievementCategory.itemText = itemText;
            achievementCategory.AddOptions(achievementCategories);
            template.gameObject.SetActive(false);
            achievementCategory.onValueChanged.AddListener(_ => FilterAchievements(true));
            string[] names = { "All", "Locked", "Unlocked" };
            for (int index = 0; index < 3; index++)
            {
                int filter = index;
                Button button = CreateButton("Achievements Filter " + names[index], card, new Vector2(200f, 32f), new Vector2(-5f + index * 210f, 274f), PanelRaised, Ink, 18);
                button.onClick.AddListener(() => { achievementStateFilter = filter; FilterAchievements(true); });
                achievementStateButtons.Add(button);
            }
        }

        private void BuildNewGameConfirmation(RectTransform canvasRect)
        {
            confirmationPanel = CreateModalBackdrop("New Game Confirmation", canvasRect);
            RectTransform card = CreatePaperCard("Confirmation Card", confirmationPanel, new Vector2(820f, 430f));
            Text eyebrow = CreateText("Confirmation Eyebrow", card, new Vector2(700f, 28f), new Vector2(0f, 150f), 18, FontStyle.Bold, TextAnchor.MiddleCenter, PaperAccent);
            eyebrow.text = "NEW VOYAGE";
            Text title = CreateText("Confirmation Title", card, new Vector2(700f, 55f), new Vector2(0f, 92f), 34, FontStyle.Bold, TextAnchor.MiddleCenter, PaperInk);
            title.text = "开始新的航行？";
            Text body = CreateText("Confirmation Body", card, new Vector2(710f, 90f), new Vector2(0f, 5f), 22, FontStyle.Normal, TextAnchor.UpperCenter, PaperMutedInk);
            body.text = "现有航程、教学进度、金币、贵重藏品及陈列记录将被重置。\n游戏设置与美术资源不会受到影响。";
            Button cancel = CreateButton("Cancel New Game", card, new Vector2(280f, 60f), new Vector2(-160f, -135f), PanelRaised, Ink, 22);
            cancel.GetComponentInChildren<Text>().text = "保留存档";
            cancel.onClick.AddListener(() => confirmationPanel.gameObject.SetActive(false));
            Button confirm = CreateButton("Confirm New Game", card, new Vector2(280f, 60f), new Vector2(160f, -135f), Coral, Ink, 22);
            confirm.GetComponentInChildren<Text>().text = "确认重新开始";
            confirm.onClick.AddListener(StartNewGame);
            confirmationPanel.gameObject.SetActive(false);
        }

        private void RequestNewGame()
        {
            if (ReleasePlayerSmokeCheck.IsRequested) return;
            if ((Application.isPlaying && profileRecoveryBlocked) || HasSave())
            {
                OpenNewGameConfirmation();
                return;
            }

            StartNewGame();
        }

        private void OpenNewGameConfirmation()
        {
            if (menuCanvas == null) return;
            if (confirmationPanel == null)
            {
                BuildNewGameConfirmation(menuCanvas);
                if (menuCanvasIsEditorPreview) SetEditorPreviewFlags(confirmationPanel);
            }
            confirmationPanel.gameObject.SetActive(true);
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
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.LastVoyageExplorationCoverageKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleDeckKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleDeckEditedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.BattleCardUpgradeKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.SpecialCardExperienceKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.AcquiredSpecialCardsKey);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.StageBattleDeckKey(stage));
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.LegacyWitchRecruitedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.CrewAtSeaKey);
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id));
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
            OpeningComicCatalog.RequestPlayback();
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
            OpeningComicCatalog.ClearPlaybackRequest();
            SceneManager.LoadScene(MainSceneName);
        }

        private void ShowSaveNotice(string notice)
        {
            if (saveNoticePanel != null) Destroy(saveNoticePanel.gameObject);
            RectTransform canvas = menuCanvas;
            if (canvas == null) return;
            saveNoticePanel = CreateModalBackdrop("Voyage Save Notice", canvas);
            RectTransform card = CreatePaperCard("Voyage Save Notice Card", saveNoticePanel, new Vector2(900f, 440f));
            Text title = CreateText("Voyage Save Notice Title", card, new Vector2(780f, 50f), new Vector2(0f, 145f), 32, FontStyle.Bold, TextAnchor.MiddleCenter, PaperInk);
            title.text = "航程存档提示";
            Text body = CreateText("Voyage Save Notice Body", card, new Vector2(780f, 205f), new Vector2(0f, 0f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, PaperMutedInk);
            body.text = notice;
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 15;
            body.resizeTextMaxSize = 22;
            Button close = CreateButton("Close Voyage Save Notice", card, new Vector2(300f, 56f), new Vector2(0f, -150f), Gold, DeepSea, 23);
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
            if (ReleasePlayerSmokeCheck.IsRequested) return;
            PlayerPrefs.SetFloat(MasterVolumeKey, masterSlider.value);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicSlider.value);
            if (CoreGameplayDemoController.SoundEffectsEnabled) PlayerPrefs.SetFloat(EffectsVolumeKey, effectsSlider.value);
            PlayerPrefs.SetInt(FullScreenKey, fullScreenToggle.isOn ? 1 : 0);
            PlayerPrefs.Save();
            Screen.fullScreen = fullScreenToggle.isOn;
            settingsPanel.gameObject.SetActive(false);
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
            return VoyageSuspensionPersistence.HasCheckpoint || HasProfileSave();
        }

        private static bool HasProfileSave()
        {
            return PlayerPrefs.HasKey(CoreGameplayDemoController.TutorialCompletedKey) ||
                PlayerPrefs.HasKey(CoreGameplayDemoController.ProfileGoldKey) || PlayerPrefs.HasKey(ProfileGoldPersistence.DecimalKey) ||
                PlayerPrefs.HasKey(CoreGameplayDemoController.CurrentPortKey);
        }

        private static MenuSaveView ReadMenuSaveView()
        {
            if (ReleasePlayerSmokeCheck.IsRequested)
                return new MenuSaveView(false, false, null);
            if (Application.isPlaying && profileRecoveryBlocked)
                return new MenuSaveView(false, false, null);

            VoyageSuspensionRecord voyage = null;
            string notice = null;
            bool readableVoyage = !Application.isPlaying || VoyageSuspensionPersistence.TryRead(out voyage, out notice);
            // An unreadable slot still needs Continue/retry and new-game
            // confirmation, even when the settled profile has no keys.
            bool hasSave = !readableVoyage || voyage != null || HasProfileSave();
            return new MenuSaveView(hasSave, readableVoyage, notice);
        }

        private static string BuildAchievementProgress(int unlockedCount)
        {
            return "已解锁 " + unlockedCount + " / " + AchievementCatalog.TotalCount;
        }

        private static RectTransform CreatePaperCard(string name, Transform parent, Vector2 size)
        {
            RectTransform card = CreateRect(name, parent, size, Vector2.zero);
            Image image = card.gameObject.AddComponent<Image>();
            image.color = new Color(Panel.r, Panel.g, Panel.b, 0.99f);
            // These dialogs use parchment; pair a light paper face with dark
            // reading ink without first loading a generic panel's paper skin.
            NauticalSurfaceSkin.ApplyReadingPanel(image);
            return card;
        }

        private Text BuildModalHeading(RectTransform card, string titleValue, string subtitleValue, string eyebrowValue)
        {
            Text eyebrow = CreateText("Modal Eyebrow", card, new Vector2(880f, 28f), new Vector2(0f, card.rect.height * 0.5f - 64f), 18, FontStyle.Bold, TextAnchor.MiddleCenter, PaperAccent);
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
            Text label = CreateText(objectName + " Label", parent, new Vector2(180f, 40f), new Vector2(-275f, y), 24, FontStyle.Bold, TextAnchor.MiddleLeft, PaperInk);
            label.text = labelValue;
            Slider slider = CreateSlider(objectName, parent, new Vector2(410f, 42f), new Vector2(40f, y), value);
            valueText = CreateText(objectName + " Value", parent, new Vector2(90f, 40f), new Vector2(330f, y), 22, FontStyle.Bold, TextAnchor.MiddleRight, PaperInk);
            valueText.text = Mathf.RoundToInt(value * 100f) + "%";
            return slider;
        }

        private AchievementRowView CreateAchievementRow(RectTransform parent, Vector2 position)
        {
            RectTransform row = CreateRect("Achievement Row", parent, new Vector2(1040f, 64f), position);
            row.gameObject.AddComponent<Image>();
            Image rowFace = row.GetComponent<Image>();
            RawImage icon = CreateRect("Achievement Icon", row,
                new Vector2(48f, 48f), new Vector2(-477f, 0f)).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            Text title = CreateText("Title", row, new Vector2(680f, 30f), new Vector2(-95f, 15f), 24, FontStyle.Bold, TextAnchor.MiddleLeft, PaperInk);
            Text detail = CreateText("Description", row, new Vector2(680f, 26f), new Vector2(-95f, -17f), 18, FontStyle.Normal, TextAnchor.MiddleLeft, PaperMutedInk);
            Text state = CreateText("State", row, new Vector2(220f, 26f), new Vector2(395f, 14f), 18, FontStyle.Bold, TextAnchor.MiddleRight, PaperMutedInk);
            Text progress = CreateText("Progress", row, new Vector2(220f, 26f), new Vector2(395f, -15f), 18, FontStyle.Normal, TextAnchor.MiddleRight, PaperMutedInk);
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

        private Button CreateMenuButton(string name, Transform parent, Vector2 size, Vector2 position,
            string title, Color textColor)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            // Invisible hit area keeps the entire row clickable while the title
            // itself carries pointer, keyboard selection and disabled feedback.
            Image hitArea = rect.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            Button button = rect.gameObject.AddComponent<Button>();
            Text titleText = CreateText("Title", rect, new Vector2(size.x, 40f), Vector2.zero,
                32, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            titleText.text = title;
            AddMenuTextOutline(titleText);
            button.targetGraphic = titleText;
            ColorBlock colors = button.colors;
            colors.normalColor = textColor;
            colors.highlightedColor = new Color(1f, 0.81f, 0.48f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(1f, 0.52f, 0.31f, 1f);
            colors.disabledColor = new Color(0.56f, 0.60f, 0.65f, 1f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            return button;
        }

        private static void AddMenuTextOutline(Text text)
        {
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.025f, 0.035f, 0.065f, 0.92f);
            outline.effectDistance = new Vector2(1f, -1f);
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
            Text label = CreateText("Label", root, new Vector2(510f, 42f), new Vector2(28f, 0f), 24, FontStyle.Bold, TextAnchor.MiddleLeft, PaperInk);
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
