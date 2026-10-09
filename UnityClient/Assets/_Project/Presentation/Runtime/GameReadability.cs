using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Reading sizes are applied by each page's layout, never by multiplying an existing mesh.</summary>
    public static class GameReadability
    {
        public const string PreferenceKey = "ShatteredSail.Readability.v1";
        // Isolated previews/tests use an override and restore it in finally; never write player preferences.
        public static int? OverrideLevel { get; set; }
        public static int Level => Mathf.Clamp(OverrideLevel ??
            (Application.isPlaying ? PlayerPrefs.GetInt(PreferenceKey, 0) : 0), 0, 2);
        public static float Scale => Level == 2 ? 1.5f : Level == 1 ? 1.25f : 1f;
        public static int FontSize(int standard) => Mathf.RoundToInt(standard * Scale);

        public static void Heading(Text label, int standard = 32)
        {
            Body(label, standard);
            if (label != null) label.font = GameTypography.InterfaceFont;
        }

        public static void Body(Text label, int standard)
        {
            if (label == null) return;
            label.font = GameTypography.BodyFont;
            label.fontStyle = FontStyle.Normal;
            label.fontSize = FontSize(standard);
            label.resizeTextForBestFit = false;
            label.resizeTextMinSize = label.resizeTextMaxSize = label.fontSize;
        }

        public static ScrollRect ScrollBody(Text label, RectTransform parent, Vector2 size, Vector2 position, int standard = 22)
        {
            var viewport = new GameObject(label.name + " Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
            viewport.SetParent(parent, false); viewport.sizeDelta = size; viewport.anchoredPosition = position;
            viewport.GetComponent<Image>().color = Color.clear;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            label.rectTransform.SetParent(content, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, 1f);
            Body(label, standard);
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.sizeDelta = new Vector2(size.x - 30f, size.y);
            float height = Mathf.Ceil(label.preferredHeight);
            label.rectTransform.sizeDelta = new Vector2(size.x - 30f, height);
            label.rectTransform.anchoredPosition = new Vector2(-5f, -8f);
            content.sizeDelta = new Vector2(size.x, Mathf.Max(size.y, height + 16f));
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 44f; scroll.movementType = ScrollRect.MovementType.Clamped;
            var track = new GameObject(label.name + " Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<RectTransform>();
            track.SetParent(viewport, false); track.sizeDelta = new Vector2(8f, size.y);
            track.anchoredPosition = new Vector2(size.x * .5f - 4f, 0f);
            track.GetComponent<Image>().color = new Color(.2f, .13f, .08f, .15f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            handle.SetParent(track, false);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one; handle.sizeDelta = Vector2.zero;
            handle.GetComponent<Image>().color = new Color(.2f, .13f, .08f, .65f);
            Scrollbar bar = track.GetComponent<Scrollbar>(); bar.handleRect = handle;
            bar.targetGraphic = handle.GetComponent<Image>(); bar.direction = Scrollbar.Direction.BottomToTop;
            bar.SetValueWithoutNotify(1f); scroll.verticalScrollbar = bar;
            track.gameObject.SetActive(content.rect.height > size.y);
            return scroll;
        }

        public static ReadabilitySelector CreateSelector(Transform parent, string name, Vector2 position, int level)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(ReadabilitySelector));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            float height = 56f * Scale;
            rect.sizeDelta = new Vector2(920f, height);
            rect.anchoredPosition = position;
            Text heading = Label("Reading Size Label", rect, new Vector2(180, height), new Vector2(-350, 0), "阅读字号");
            heading.color = new Color(.20f, .13f, .08f, 1f);
            var selector = root.GetComponent<ReadabilitySelector>();
            selector.Buttons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int choice = i;
                var host = new GameObject("Reading Size " + i, typeof(RectTransform), typeof(Image), typeof(Button));
                var buttonRect = host.GetComponent<RectTransform>();
                buttonRect.SetParent(rect, false);
                buttonRect.sizeDelta = new Vector2(200, height);
                buttonRect.anchoredPosition = new Vector2(-120 + i * 220, 0);
                Button button = host.GetComponent<Button>();
                button.targetGraphic = host.GetComponent<Image>();
                Label("Label", buttonRect, new Vector2(180, height - 10), Vector2.zero, new[] { "标准", "大字", "特大字" }[i]);
                WoodButtonSkin.Apply(button, Color.white);
                Body(button.GetComponentInChildren<Text>(), 22);
                selector.Buttons[i] = button;
                button.onClick.AddListener(() => selector.Value = choice);
            }
            selector.Value = level;
            return selector;
        }

        private static Text Label(string name, Transform parent, Vector2 size, Vector2 position, string content)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
            Text label = host.GetComponent<Text>();
            Body(label, 22);
            label.text = content; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; label.color = WoodButtonSkin.Ink;
            return label;
        }
    }

    public sealed class ReadabilitySelector : MonoBehaviour
    {
        public Button[] Buttons { get; set; }
        private int value;
        public int Value
        {
            get => value;
            set
            {
                this.value = Mathf.Clamp(value, 0, 2);
                if (Buttons == null) return;
                for (int i = 0; i < Buttons.Length; i++) WoodButtonSkin.SetSelected(Buttons[i], i == this.value);
            }
        }
    }
}
