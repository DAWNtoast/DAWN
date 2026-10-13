using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class MainMenuController
    {
        private MainMenuPrefabView saveNoticeView;
#if UNITY_EDITOR
        // Only the one-time editor exporter uses the legacy authoring path.
        public static bool ExportingPrefab { get; set; }
#endif

        private MainMenuPrefabView InstantiateMenuPrefab(MainMenuPrefabKind kind, Transform parent)
        {
#if UNITY_EDITOR
            if (ExportingPrefab) return null;
#endif
            MainMenuPrefabView prefab = Resources.Load<MainMenuPrefabView>("UI/MainMenu" + kind);
            if (prefab == null) return null;
            if (prefab.Kind != kind || !prefab.IsComplete)
                throw new InvalidOperationException("Incomplete main menu prefab: " + kind);
            MainMenuPrefabView view = Instantiate(prefab, parent, false);
            // Unity gives the asset root its filename when saving a prefab.
            // Preserve the established live hierarchy names used by navigation,
            // editor preview visibility, accessibility and acceptance captures.
            view.name = kind switch
            {
                MainMenuPrefabKind.Surface => "Main Menu Canvas",
                MainMenuPrefabKind.Settings => "Settings Panel",
                MainMenuPrefabKind.Achievements => "Achievements Panel",
                MainMenuPrefabKind.NewGameConfirmation => "New Game Confirmation",
                MainMenuPrefabKind.SaveNotice => "Voyage Save Notice",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            view.GetComponent<PrefabRuntimeSkinBindings>()?.Apply();
            return view;
        }

        private bool TryBuildPrefabCanvas(string canvasName, bool editorPreview, MenuSaveView saveView)
        {
            MainMenuPrefabView view = InstantiateMenuPrefab(MainMenuPrefabKind.Surface, transform);
            if (view == null) return false;
            view.name = canvasName;
            menuCanvas = view.Viewport;
            menuCanvasIsEditorPreview = editorPreview;
            ClearAchievementView();
            settingsPanel = confirmationPanel = saveNoticePanel = null;
            saveNoticeView = null;
            if (view.Background.texture != null)
            {
                view.Background.texture.filterMode = FilterMode.Bilinear;
                view.Background.texture.anisoLevel = 2;
                view.Background.texture.wrapMode = TextureWrapMode.Clamp;
            }
            if (view.TitleArtwork.texture != null)
            {
                view.TitleArtwork.texture.filterMode = FilterMode.Bilinear;
                view.TitleArtwork.texture.wrapMode = TextureWrapMode.Clamp;
            }
            SetMenuNormalColor(view.Primary, saveView.HasSave ? Ink : Gold);
            view.Primary.interactable = !ReleasePlayerSmokeCheck.IsRequested;
            view.Primary.onClick.AddListener(RequestNewGame);
            continueButton = view.Secondary;
            SetMenuNormalColor(continueButton, saveView.HasSave ? Gold : Ink);
            continueButton.interactable = (Application.isPlaying && profileRecoveryBlocked) || saveView.HasSave;
            continueButton.onClick.AddListener(ContinueGame);
            view.Settings.onClick.AddListener(OpenSettings);
            view.Achievements.onClick.AddListener(OpenAchievements);
            view.Exit.onClick.AddListener(ExitGame);
            if (editorPreview)
            {
                BuildSettingsPanel(menuCanvas);
                BuildNewGameConfirmation(menuCanvas);
                SetEditorPreviewFlags(view.transform);
            }
            return true;
        }

        private static void SetMenuNormalColor(Button button, Color color)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            button.colors = colors;
        }

        private bool TryBuildPrefabSettings(RectTransform parent)
        {
            MainMenuPrefabView view = InstantiateMenuPrefab(MainMenuPrefabKind.Settings, parent);
            if (view == null) return false;
            settingsPanel = (RectTransform)view.transform;
            masterSlider = view.Master; musicSlider = view.Music; effectsSlider = view.Effects;
            masterValue = view.MasterValue; musicValue = view.MusicValue; effectsValue = view.EffectsValue;
            BindMenuVolume(masterSlider, masterValue, PlayerPrefs.GetFloat(MasterVolumeKey, .8f));
            BindMenuVolume(musicSlider, musicValue, PlayerPrefs.GetFloat(MusicVolumeKey, .7f));
            BindMenuVolume(effectsSlider, effectsValue, PlayerPrefs.GetFloat(EffectsVolumeKey, .8f));
            masterSlider.interactable = musicSlider.interactable = true;
            effectsSlider.interactable = CoreGameplayDemoController.SoundEffectsEnabled;
            fullScreenToggle = view.FullScreen;
            fullScreenToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1);
            view.Primary.onClick.AddListener(ApplySettings);
            view.Primary.interactable = !ReleasePlayerSmokeCheck.IsRequested;
            view.Secondary.onClick.AddListener(() => settingsPanel.gameObject.SetActive(false));
            settingsPanel.gameObject.SetActive(false);
            return true;
        }

        private static void BindMenuVolume(Slider slider, Text label, float value)
        {
            slider.SetValueWithoutNotify(value);
            label.text = Mathf.RoundToInt(slider.value * 100f) + "%";
            slider.onValueChanged.AddListener(next => label.text = Mathf.RoundToInt(next * 100f) + "%");
        }

        private bool TryBuildPrefabAchievements(RectTransform parent)
        {
            MainMenuPrefabView view = InstantiateMenuPrefab(MainMenuPrefabKind.Achievements, parent);
            if (view == null) return false;
            achievementsPanel = (RectTransform)view.transform;
            achievementSummary = view.Summary;
            achievementCategory = view.Category;
            achievementCategories.Clear();
            achievementCategories.Add("全部分类");
            foreach (CoreAchievementDefinition definition in AchievementCatalog.All)
                if (!achievementCategories.Contains(definition.CategoryLabel)) achievementCategories.Add(definition.CategoryLabel);
            achievementCategory.ClearOptions();
            achievementCategory.AddOptions(achievementCategories);
            achievementCategory.SetValueWithoutNotify(0);
            achievementCategory.onValueChanged.AddListener(_ => FilterAchievements(true));
            achievementStateButtons.Clear();
            for (int index = 0; index < view.Filters.Count; index++)
            {
                int filter = index;
                Button button = view.Filters[index];
                button.onClick.AddListener(() => { achievementStateFilter = filter; FilterAchievements(true); });
                achievementStateButtons.Add(button);
            }
            achievementRows.Clear();
            foreach (MainMenuAchievementRowView row in view.Rows) achievementRows.Add(row);
            achievementPage = 0;
            achievementEmpty = view.Empty;
            achievementPageLabel = view.Page;
            achievementPreviousPage = view.Previous;
            achievementNextPage = view.Next;
            achievementPreviousPage.onClick.AddListener(() => SetAchievementPage(achievementPage - 1));
            achievementNextPage.onClick.AddListener(() => SetAchievementPage(achievementPage + 1));
            view.Primary.onClick.AddListener(() => achievementsPanel.gameObject.SetActive(false));
            achievementsPanel.gameObject.SetActive(false);
            return true;
        }

        private bool TryBuildPrefabConfirmation(RectTransform parent)
        {
            MainMenuPrefabView view = InstantiateMenuPrefab(MainMenuPrefabKind.NewGameConfirmation, parent);
            if (view == null) return false;
            confirmationPanel = (RectTransform)view.transform;
            view.Primary.onClick.AddListener(StartNewGame);
            view.Secondary.onClick.AddListener(() => confirmationPanel.gameObject.SetActive(false));
            confirmationPanel.gameObject.SetActive(false);
            return true;
        }

        private bool TryShowPrefabSaveNotice(string notice)
        {
            if (menuCanvas == null) return false;
            if (saveNoticeView == null)
            {
                if (saveNoticePanel != null) return false;
                saveNoticeView = InstantiateMenuPrefab(MainMenuPrefabKind.SaveNotice, menuCanvas);
                if (saveNoticeView == null) return false;
                saveNoticePanel = (RectTransform)saveNoticeView.transform;
                saveNoticeView.Primary.onClick.AddListener(CloseSaveNotice);
                if (menuCanvasIsEditorPreview) SetEditorPreviewFlags(saveNoticePanel);
            }
            saveNoticeView.Body.text = notice;
            saveNoticePanel.SetAsLastSibling();
            saveNoticePanel.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(saveNoticeView.Primary.gameObject);
            return true;
        }

#if UNITY_EDITOR
        public Transform GetPrefabExportSource(MainMenuPrefabKind kind)
        {
            if (!ExportingPrefab || Application.isPlaying)
                throw new InvalidOperationException("Menu export requires the editor-only migration scope.");
            switch (kind)
            {
                case MainMenuPrefabKind.Surface: return transform.Find(EditorPreviewCanvasName);
                case MainMenuPrefabKind.Settings: return settingsPanel;
                case MainMenuPrefabKind.NewGameConfirmation: return confirmationPanel;
                case MainMenuPrefabKind.Achievements:
                    if (achievementsPanel == null) BuildAchievementsPanel(menuCanvas);
                    return achievementsPanel;
                case MainMenuPrefabKind.SaveNotice:
                    if (saveNoticePanel == null) ShowSaveNotice(string.Empty);
                    return saveNoticePanel;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
#endif
    }
}
