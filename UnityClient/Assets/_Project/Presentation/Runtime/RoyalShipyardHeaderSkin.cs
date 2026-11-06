using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shipyard typography over the common header surface; navigation events remain intact.</summary>
    public static class RoyalShipyardHeaderSkin
    {
        private static readonly Color Ivory = new Color(.96f, .92f, .81f, 1f);
        private static Sprite frameSprite, compassSprite;

        static RoyalShipyardHeaderSkin()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        public static void Apply(RectTransform contentRoot)
        {
            if (contentRoot == null) return;
            RectTransform header = null;
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform candidate = contentRoot.GetChild(i);
                if (candidate.name == "Port Facility Header" && candidate.gameObject.activeSelf)
                { header = candidate as RectTransform; break; }
            }
            if (header == null) return;
            Text title = header.Find("Port Facility Title")?.GetComponent<Text>();
            if (title == null) return;
            var state = header.GetComponent<RoyalShipyardHeaderState>() ?? header.gameObject.AddComponent<RoyalShipyardHeaderState>();
            state.Begin(title);

            Image regional = header.Find("Port Facility Frame")?.GetComponent<Image>();
            if (regional != null) state.Enabled(regional, false);
            Graphic surface = header.Find("Header Brass Rules")?.GetComponent<NauticalHeaderGraphic>();
            if (surface != null) surface.enabled = true;
            foreach (string name in new[] { "Date Inset", "Gold Inset", "Port Header Section" })
            {
                Graphic old = header.Find(name)?.GetComponent<Graphic>();
                if (old != null) state.Enabled(old, false);
            }

            RectTransform art = state.Decorations(header);
            // Only noninteractive labels remain above the shared opaque surface.
            art.SetAsLastSibling();
            // The common heading now supplies the shipyard's own facility icon.
            foreach (string name in new[] { "Royal Shipyard Brass Frame", "Royal Shipyard Title Upper Rule",
                "Royal Shipyard Title Lower Rule", "Royal Shipyard Navigation Divider", "Royal Shipyard Treasury Divider",
                "Royal Shipyard Header Compass", "Royal Shipyard Naval Cloth" })
            {
                Transform old = art.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }
            // Navigation keeps the shared icon size and its original callbacks.
            NauticalHeaderSkin.ApplySettingsButton(header.Find("Port Facility Settings")?.GetComponent<Button>());
            state.Color(title, NauticalHeaderSkin.Ivory);
            state.Type(title, 31, FontStyle.Bold);
            StyleText(state, header.Find("Port Facility Time")?.GetComponent<Text>(), 169f, 0f, 316f, 64f, 20, Ivory, false);
            StyleText(state, header.Find("Port Facility Port")?.GetComponent<Text>(), 444f, -5f, 186f, 44f, 24, Ivory, true);
            NauticalHeaderSkin.ApplyGoldReadout(header, "Port Facility ");
            Text harbour = Text(art, "Royal Shipyard Harbour Legend", title.font);
            Box(harbour.rectTransform, 444f, 26f, 186f, 18f);
            harbour.text = "停 泊 港";
            harbour.fontSize = 11;
            harbour.color = new Color(.70f, .72f, .66f, 1f);
            state.GuardNavigation(header.Find("Return To Port Hub")?.GetComponent<Button>());
            state.GuardNavigation(header.Find("Previous Port Facility")?.GetComponent<Button>());
            state.GuardNavigation(header.Find("Next Port Facility")?.GetComponent<Button>());
        }

        private static void StyleText(RoyalShipyardHeaderState state, Text label, float x, float y,
            float width, float height, int size, Color color, bool bold)
        {
            if (label == null) return;
            state.Box(label.rectTransform, x, y, width, height);
            state.Color(label, color);
            state.Type(label, size, bold ? FontStyle.Bold : FontStyle.Normal);
        }

        private static Sprite Frame
        {
            get
            {
                if (frameSprite != null) return frameSprite;
                Texture2D source = Resources.Load<Texture2D>(NavigationInterfaceSkin.PanelFrameResourcePath);
                if (source == null) return WoodButtonSkin.Face;
                frameSprite = Sprite.Create(source, new Rect(32f, 32f, source.width - 64f, source.height - 64f),
                    Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * 64f);
                frameSprite.name = "Royal Shipyard Walnut Brass Frame";
                frameSprite.hideFlags = HideFlags.HideAndDontSave;
                return frameSprite;
            }
        }

        private static Sprite Compass
        {
            get
            {
                if (compassSprite != null) return compassSprite;
                Texture2D source = Resources.Load<Texture2D>(NavigationCompassControl.ResourcePath);
                if (source == null) return null;
                compassSprite = Sprite.Create(source, new Rect(0, 0, source.width, source.height), Vector2.one * .5f, 100f);
                compassSprite.name = "Royal Shipyard Brass Compass";
                compassSprite.hideFlags = HideFlags.HideAndDontSave;
                return compassSprite;
            }
        }

        private static Image Image(Transform parent, string name, Sprite sprite)
        {
            Transform found = parent.Find(name);
            if (found == null)
            {
                var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                node.layer = parent.gameObject.layer;
                node.transform.SetParent(parent, false);
                found = node.transform;
            }
            Image graphic = found.GetComponent<Image>();
            graphic.sprite = sprite;
            graphic.raycastTarget = false;
            return graphic;
        }

        private static Text Text(Transform parent, string name, Font font)
        {
            Transform found = parent.Find(name);
            if (found == null)
            {
                var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                node.layer = parent.gameObject.layer;
                node.transform.SetParent(parent, false);
                found = node.transform;
            }
            Text label = found.GetComponent<Text>();
            label.font = font;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        private static void Rule(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            Image rule = Image(parent, name, null);
            Box(rule.rectTransform, x, y, width, height);
            rule.color = color;
        }

        internal static void Box(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        private static void Release()
        {
            foreach (Sprite sprite in new[] { frameSprite, compassSprite })
            {
                if (sprite == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            frameSprite = compassSprite = null;
        }
    }

    // Port headers are cached across facilities. Restore only properties still owned by this skin,
    // so a subsequent facility's binding always wins, including programmatic navigation.
    // The same skin is used by live editor previews, whose header can be parked or rebuilt
    // outside play mode. Their teardown must run the same restoration as the game view.
    [ExecuteAlways]
    internal sealed class RoyalShipyardHeaderState : MonoBehaviour
    {
        private readonly List<Action> restore = new List<Action>();
        private Text title;
        private string titleCaption;
        private RectTransform decoration;

        public void Begin(Text label)
        {
            Restore();
            title = label;
            titleCaption = label.text;
        }

        public RectTransform Decorations(RectTransform header)
        {
            if (decoration == null)
            {
                var node = new GameObject("Royal Shipyard Header Decorations", typeof(RectTransform));
                node.layer = header.gameObject.layer;
                decoration = node.GetComponent<RectTransform>();
                decoration.SetParent(header, false);
                decoration.anchorMin = Vector2.zero;
                decoration.anchorMax = Vector2.one;
                decoration.offsetMin = decoration.offsetMax = Vector2.zero;
            }
            decoration.gameObject.SetActive(true);
            decoration.SetAsFirstSibling();
            return decoration;
        }

        public void Box(RectTransform rect, float x, float y, float width, float height)
        {
            Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot;
            Vector2 size = rect.sizeDelta, position = rect.anchoredPosition;
            Vector2 styledSize = new Vector2(width, height), styledPosition = new Vector2(x, y);
            restore.Add(() =>
            {
                if (rect == null || rect.sizeDelta != styledSize || rect.anchoredPosition != styledPosition) return;
                rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
                rect.sizeDelta = size; rect.anchoredPosition = position;
            });
            RoyalShipyardHeaderSkin.Box(rect, x, y, width, height);
        }

        public void Color(Graphic graphic, Color value)
        {
            Color prior = graphic.color;
            restore.Add(() => { if (graphic != null && graphic.color == value) graphic.color = prior; });
            graphic.color = value;
        }

        public void Enabled(Graphic graphic, bool value)
        {
            bool prior = graphic.enabled;
            restore.Add(() => { if (graphic != null && graphic.enabled == value) graphic.enabled = prior; });
            graphic.enabled = value;
        }

        public void Pixels(Image image, float value)
        {
            float prior = image.pixelsPerUnitMultiplier;
            restore.Add(() => { if (image != null && Mathf.Approximately(image.pixelsPerUnitMultiplier, value)) image.pixelsPerUnitMultiplier = prior; });
            image.pixelsPerUnitMultiplier = value;
        }

        public void Type(Text label, int size, FontStyle style)
        {
            int priorSize = label.fontSize, priorMax = label.resizeTextMaxSize;
            FontStyle priorStyle = label.fontStyle;
            Font priorFont = label.font;
            float priorSpacing = label.lineSpacing;
            GameTypography.Apply(label, size, style);
            Font styledFont = label.font;
            FontStyle styledStyle = label.fontStyle;
            float styledSpacing = label.lineSpacing;
            restore.Add(() =>
            {
                if (label == null) return;
                if (label.fontSize == size) label.fontSize = priorSize;
                if (label.resizeTextMaxSize == size) label.resizeTextMaxSize = priorMax;
                if (label.fontStyle == styledStyle) label.fontStyle = priorStyle;
                if (label.font == styledFont) label.font = priorFont;
                if (Mathf.Approximately(label.lineSpacing, styledSpacing)) label.lineSpacing = priorSpacing;
            });
            label.resizeTextMaxSize = size;
        }

        public void GuardNavigation(Button button)
        {
            if (button == null) return;
            Button.ButtonClickedEvent original = button.onClick;
            var wrapped = new Button.ButtonClickedEvent();
            wrapped.AddListener(() => { Restore(); original.Invoke(); });
            restore.Add(() => { if (button != null && ReferenceEquals(button.onClick, wrapped)) button.onClick = original; });
            button.onClick = wrapped;
        }

        private void LateUpdate()
        {
            if (title != null && title.text != titleCaption) Restore();
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
            restore.Clear();
            if (decoration != null) decoration.gameObject.SetActive(false);
            title = null;
        }
    }
}
