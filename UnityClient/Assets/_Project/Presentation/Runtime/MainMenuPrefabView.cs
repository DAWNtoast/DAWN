using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum MainMenuPrefabKind { Surface, Settings, Achievements, NewGameConfirmation, SaveNotice }

    [Serializable]
    public sealed class MainMenuAchievementRowView
    {
        public RectTransform Root;
        public Image Face;
        public RawImage Icon;
        public Text Title, Description, State, Progress;
    }

    /// <summary>Authored menu hierarchies. Controller binds data and callbacks through serialized references.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuPrefabView : MonoBehaviour
    {
        [SerializeField] private MainMenuPrefabKind kind;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RawImage background, titleArtwork;
        [SerializeField] private Button primary, secondary, settings, achievements, exit;
        [SerializeField] private Slider master, music, effects;
        [SerializeField] private Text masterValue, musicValue, effectsValue, summary, empty, page, body;
        [SerializeField] private Toggle fullScreen;
        [SerializeField] private Dropdown category;
        [SerializeField] private Button previous, next;
        [SerializeField] private Button[] filters = Array.Empty<Button>();
        [SerializeField] private MainMenuAchievementRowView[] rows = Array.Empty<MainMenuAchievementRowView>();

        public MainMenuPrefabKind Kind => kind;
        public RectTransform Viewport => viewport;
        public RawImage Background => background;
        public RawImage TitleArtwork => titleArtwork;
        public Button Primary => primary;
        public Button Secondary => secondary;
        public Button Settings => settings;
        public Button Achievements => achievements;
        public Button Exit => exit;
        public Slider Master => master;
        public Slider Music => music;
        public Slider Effects => effects;
        public Text MasterValue => masterValue;
        public Text MusicValue => musicValue;
        public Text EffectsValue => effectsValue;
        public Toggle FullScreen => fullScreen;
        public Text Summary => summary;
        public Text Empty => empty;
        public Text Page => page;
        public Text Body => body;
        public Dropdown Category => category;
        public Button Previous => previous;
        public Button Next => next;
        public IReadOnlyList<Button> Filters => filters;
        public IReadOnlyList<MainMenuAchievementRowView> Rows => rows;

        public bool IsComplete => primary != null && (kind switch
        {
            MainMenuPrefabKind.Surface => viewport != null && background != null && titleArtwork != null && secondary != null && settings != null && achievements != null && exit != null,
            MainMenuPrefabKind.Settings => secondary != null && master != null && music != null && effects != null &&
                masterValue != null && musicValue != null && effectsValue != null && fullScreen != null,
            MainMenuPrefabKind.Achievements => summary != null && empty != null && page != null && category != null &&
                previous != null && next != null && filters.Length == 3 && rows.Length == 8 && ValidRows(),
            MainMenuPrefabKind.NewGameConfirmation => secondary != null,
            MainMenuPrefabKind.SaveNotice => body != null,
            _ => false
        });

        private bool ValidRows()
        {
            foreach (MainMenuAchievementRowView row in rows)
                if (row == null || row.Root == null || row.Face == null || row.Icon == null || row.Title == null ||
                    row.Description == null || row.State == null || row.Progress == null) return false;
            foreach (Button filter in filters) if (filter == null) return false;
            return true;
        }

#if UNITY_EDITOR
        public void CaptureBindings(MainMenuPrefabKind value)
        {
            kind = value;
            switch (kind)
            {
                case MainMenuPrefabKind.Surface:
                    viewport = Find<RectTransform>(FixedGameViewport.Name);
                    background = Find<RawImage>("Main Menu Background"); titleArtwork = Find<RawImage>("Game Title Artwork");
                    primary = Find<Button>("New Game"); secondary = Find<Button>("Continue Game");
                    settings = Find<Button>("Settings"); achievements = Find<Button>("Achievements"); exit = Find<Button>("Exit Game");
                    break;
                case MainMenuPrefabKind.Settings:
                    primary = Find<Button>("Apply Settings"); secondary = Find<Button>("Back From Settings");
                    master = Find<Slider>("Master Volume"); music = Find<Slider>("Music Volume"); effects = Find<Slider>("Effects Volume");
                    masterValue = Find<Text>("Master Volume Value"); musicValue = Find<Text>("Music Volume Value"); effectsValue = Find<Text>("Effects Volume Value");
                    fullScreen = Find<Toggle>("Full Screen");
                    break;
                case MainMenuPrefabKind.Achievements:
                    primary = Find<Button>("Back From Achievements");
                    summary = Find<Text>("Modal Subtitle"); empty = Find<Text>("Achievements Empty"); page = Find<Text>("Achievements Page Indicator");
                    category = Find<Dropdown>("Achievements Category"); previous = Find<Button>("Achievements Previous Page"); next = Find<Button>("Achievements Next Page");
                    filters = new[] { Find<Button>("Achievements Filter All"), Find<Button>("Achievements Filter Locked"), Find<Button>("Achievements Filter Unlocked") };
                    var captured = new List<MainMenuAchievementRowView>();
                    foreach (RectTransform root in GetComponentsInChildren<RectTransform>(true))
                    {
                        if (root.name != "Achievement Row") continue;
                        captured.Add(new MainMenuAchievementRowView
                        {
                            Root = root, Face = root.GetComponent<Image>(), Icon = root.Find("Achievement Icon").GetComponent<RawImage>(),
                            Title = root.Find("Title").GetComponent<Text>(), Description = root.Find("Description").GetComponent<Text>(),
                            State = root.Find("State").GetComponent<Text>(), Progress = root.Find("Progress").GetComponent<Text>()
                        });
                    }
                    rows = captured.ToArray();
                    break;
                case MainMenuPrefabKind.NewGameConfirmation:
                    primary = Find<Button>("Confirm New Game"); secondary = Find<Button>("Cancel New Game");
                    break;
                case MainMenuPrefabKind.SaveNotice:
                    primary = Find<Button>("Close Voyage Save Notice"); body = Find<Text>("Voyage Save Notice Body");
                    break;
            }
            if (!IsComplete) throw new InvalidOperationException("Incomplete menu prefab: " + kind);
        }

        private T Find<T>(string name) where T : Component
        {
            foreach (T candidate in GetComponentsInChildren<T>(true)) if (candidate.name == name) return candidate;
            throw new InvalidOperationException("Missing " + typeof(T).Name + " binding: " + name);
        }
#endif
    }
}
