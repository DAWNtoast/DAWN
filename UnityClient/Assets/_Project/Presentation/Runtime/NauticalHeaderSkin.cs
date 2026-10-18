using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared port, navigation and battle chrome. Never replaces a live control or listener.</summary>
    public static class NauticalHeaderSkin
    {
        private static readonly Color Ivory = new Color(.97f, .92f, .80f, 1f);
        private static readonly Color Muted = new Color(.70f, .69f, .61f, 1f);

        public static void ApplyPort(RectTransform root, Color accent, bool hub)
        {
            if (root == null) return;
            Surface(root, accent);
            string prefix = hub ? "Port Hub " : "Port Facility ";
            if (hub)
            {
                Place(root, prefix + "Time", -624, 0, 350, 64);
                Place(root, prefix + "Port", -130, -8, 450, 44);
                Place(root, prefix + "Gold", 420, 0, 290, 64);
                Place(root, prefix + "Settings", 816, 0, 152, 56);
                Plaque(root, "Date Inset", -624, 366, accent);
                Plaque(root, "Gold Inset", 420, 314, accent);
                Eyebrow(root, "Port Header Section", "港 口 总 览", -130, 28, 360, accent);
                Style(root.Find(prefix + "Port"), 32, Ivory, FontStyle.Bold);
            }
            else
            {
                Place(root, "Return To Port Hub", -820, 0, 176, 56);
                Place(root, "Previous Port Facility", -613, 0, 188, 56);
                Place(root, "Port Facility Title", -380, -8, 246, 44);
                Place(root, "Next Port Facility", -145, 0, 188, 56);
                Place(root, prefix + "Time", 155, 0, 350, 64);
                Place(root, prefix + "Port", 450, 0, 180, 56);
                Place(root, prefix + "Gold", 660, 0, 216, 64);
                Place(root, prefix + "Settings", 848, 0, 132, 56);
                Plaque(root, "Date Inset", 155, 366, accent);
                Plaque(root, "Gold Inset", 660, 232, accent);
                Eyebrow(root, "Port Header Section", "港 口 设 施", -380, 28, 246, accent);
                Style(root.Find(prefix + "Title"), 28, Ivory, FontStyle.Bold);
                Style(root.Find(prefix + "Port"), 24, accent, FontStyle.Normal);
            }
            Style(root.Find(prefix + "Time"), 20, Ivory, FontStyle.Normal);
            Style(root.Find(prefix + "Gold"), 26, accent, FontStyle.Normal);
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) StyleButton(button, accent);
        }

        public static void ApplyVoyage(RectTransform root, Color accent, bool battle)
        {
            if (root == null) return;
            Surface(root, accent);
            if (battle)
            {
                // These coordinates also reserve room for the separately rendered battle readouts.
                Plaque(root, "Range Inset", -250, 300, accent);
                Plaque(root, "Alignment Inset", 250, 330, accent);
                Plaque(root, "Ship Status Inset", 600, 340, accent);
                Place(root, "Voyage Header Status", 600, 0, 316, 60);
                Place(root, "Battle Deck", 818, 0, 64, 64);
                Place(root, "Battle Settings", 898, 0, 64, 64);
                Style(root.Find("Voyage Header Title"), 32, accent, FontStyle.Bold);
                Style(root.Find("Voyage Header Status"), 18, Ivory, FontStyle.Normal);
            }
            else
            {
                Plaque(root, "Clock Inset", 165, 760, accent);
                Style(root.Find("Voyage Header Title"), 29, Ivory, FontStyle.Bold, TextAnchor.MiddleLeft);
                Style(root.Find("Voyage Header Status"), 18, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
                foreach (Button button in root.GetComponentsInChildren<Button>(true)) StyleButton(button, accent, true);
            }
        }

        private static void Surface(RectTransform root, Color accent)
        {
            var graphic = Graphic(root, "Header Brass Rules", accent, false);
            graphic.rectTransform.anchorMin = Vector2.zero;
            graphic.rectTransform.anchorMax = Vector2.one;
            graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
            // Keep the actual regional frame as the bottom layer for editable prefab bindings.
            graphic.transform.SetSiblingIndex(1);
            Image backing = root.GetComponent<Image>();
            if (backing != null) backing.raycastTarget = false;
        }

        private static void Plaque(RectTransform root, string name, float x, float width, Color accent)
        {
            var graphic = Graphic(root, name, accent, true);
            Box(graphic.rectTransform, x, 0, width, 72);
            graphic.transform.SetSiblingIndex(2);
        }

        private static NauticalHeaderGraphic Graphic(RectTransform parent, string name, Color accent, bool inset)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(NauticalHeaderGraphic));
                node.layer = parent.gameObject.layer;
                node.transform.SetParent(parent, false);
                child = node.transform;
            }
            var graphic = child.GetComponent<NauticalHeaderGraphic>();
            graphic.Configure(accent, inset);
            return graphic;
        }

        private static void Eyebrow(RectTransform root, string name, string caption, float x, float y, float width, Color accent)
        {
            Transform child = root.Find(name);
            if (child == null)
            {
                var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                node.layer = root.gameObject.layer;
                node.transform.SetParent(root, false);
                child = node.transform;
                node.GetComponent<Text>().font = root.GetComponentInChildren<Text>(true).font;
            }
            Text label = child.GetComponent<Text>();
            label.text = caption;
            Box(label.rectTransform, x, y, width, 18);
            Style(child, 12, accent, FontStyle.Normal);
        }

        private static void StyleButton(Button button, Color accent, bool voyage = false)
        {
            // Retain the existing sliced walnut face and its hover/press/disabled states.
            WoodButtonSkin.Apply(button, Color.white);
            Image face = button.GetComponent<Image>();
            if (face != null) face.pixelsPerUnitMultiplier = 28f;
            foreach (Outline edge in button.GetComponents<Outline>())
            {
                edge.effectColor = new Color(accent.r, accent.g, accent.b, .23f);
                edge.effectDistance = new Vector2(1, -1);
            }
            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null) return;
            RectTransform control = (RectTransform)button.transform;
            Box(label.rectTransform, 0, 0, control.sizeDelta.x - 20, control.sizeDelta.y - 10);
            Style(label.transform, voyage ? 22 : 20, Ivory, voyage ? FontStyle.Bold : FontStyle.Normal);
        }

        private static void Style(Transform node, int size, Color color, FontStyle fontStyle,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = node == null ? null : node.GetComponent<Text>();
            if (text == null) return;
            text.color = color;
            text.fontSize = text.resizeTextMaxSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, size - 4);
            GameTypography.Apply(text, size, fontStyle);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static void Place(Transform root, string name, float x, float y, float width, float height)
        {
            if (root.Find(name) is RectTransform rect) Box(rect, x, y, width, height);
        }

        private static void Box(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
        }
    }
}
