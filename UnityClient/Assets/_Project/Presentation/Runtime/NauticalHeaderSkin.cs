using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared port, navigation and battle chrome. Never replaces a live control or listener.</summary>
    public static class NauticalHeaderSkin
    {
        // Shared design-space size for every header icon, including future additions.
        // CanvasScaler handles resolution changes; do not size individual icons separately.
        public const float IconSize = 96f;
        public const float IconGap = 16f;
        public const float SettingsRightOffset = 62f;
        public const float SettingsTopOffset = 54f;
        public const float SettingsRightMargin = SettingsRightOffset - IconSize * .5f;
        public static readonly Color Ivory = new Color(.96f, .94f, .86f, 1f);
        private static readonly Color Muted = new Color(.65f, .76f, .77f, 1f);

        public static void ApplyPort(RectTransform root, Color accent, bool hub, string portName = null)
        {
            if (root == null) return;
            Surface(root, accent, portName);
            // Cached/older views may still carry the retired section caption.
            root.Find("Port Header Section")?.gameObject.SetActive(false);
            string prefix = hub ? "Port Hub " : "Port Facility ";
            if (hub)
            {
                Place(root, prefix + "Time", -624, 0, 380, 72);
                Plaque(root, "Date Inset", -624, 366, accent);
                Plaque(root, "Gold Inset", 420, 314, accent);
                Style(root.Find(prefix + "Port"), 38, Ivory, FontStyle.Bold);
                LayoutPortHubTitle(root);
            }
            else
            {
                Place(root, "Previous Port Facility", -613, 0, IconSize, IconSize);
                Place(root, "Port Facility Title", -333, 0, 196, 60);
                Place(root, "Next Port Facility", -145, 0, IconSize, IconSize);
                Place(root, prefix + "Time", 155, 0, 350, 64);
                Place(root, prefix + "Port", 450, 0, 180, 56);
                Plaque(root, "Date Inset", 155, 366, accent);
                Plaque(root, "Gold Inset", 660, 232, accent);
                Style(root.Find(prefix + "Title"), 28, Ivory, FontStyle.Bold, TextAnchor.MiddleLeft);
                Style(root.Find(prefix + "Port"), 24, Muted, FontStyle.Normal);
            }
            Style(root.Find(prefix + "Time"), hub ? 30 : 20, Ivory, FontStyle.Normal);
            Style(root.Find(prefix + "Gold"), hub ? 34 : 26, accent, FontStyle.Normal);
            ApplyGoldReadout(root, prefix);
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) StyleButton(button, accent);
        }

        public static void ApplyPortHubTitleIcon(RectTransform root, Texture2D texture)
        {
            if (root == null) return;
            Transform child = root.Find("Port Hub Port Icon");
            if (child == null)
            {
                var node = new GameObject("Port Hub Port Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                node.layer = root.gameObject.layer;
                node.transform.SetParent(root, false);
                child = node.transform;
            }
            RawImage icon = child.GetComponent<RawImage>();
            icon.texture = texture;
            icon.uvRect = new Rect(0f, 0f, 1f, 1f);
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(texture != null);
            LayoutPortHubTitle(root);
        }

        private static void LayoutPortHubTitle(RectTransform root)
        {
            Text title = root.Find("Port Hub Port")?.GetComponent<Text>();
            if (title == null) return;
            RawImage icon = root.Find("Port Hub Port Icon")?.GetComponent<RawImage>();
            bool hasIcon = icon != null && icon.texture != null;
            // Measure the current name so the complete icon/name group stays centered
            // after cached headers bind another port, without a fixed-width text gutter.
            float titleWidth = Mathf.Max(title.fontSize, Mathf.Ceil(title.preferredWidth));
            float iconWidth = hasIcon ? IconSize + IconGap : 0f;
            Box(title.rectTransform, iconWidth * .5f, 0, titleWidth, 72);
            if (hasIcon) Box(icon.rectTransform, -(titleWidth + IconGap) * .5f, 0, IconSize, IconSize);
        }

        public static void ApplyVoyage(RectTransform root, Color accent, bool battle, bool boarding = false, string portName = null)
        {
            if (root == null) return;
            Surface(root, accent, portName);
            if (battle)
            {
                // Only boarding has readouts here; the naval grid shows them in its own HUD.
                // Set visibility at construction too, so editor previews match live gameplay.
                Plaque(root, "Range Inset", -250, 300, accent, boarding);
                Plaque(root, "Alignment Inset", 250, 330, accent, boarding);
                // The boarding range readout ends at -125; use the remaining
                // header span for a readable, single-line ship summary.
                const float statusLeft = -100f;
                float statusRight = PhaseSurfaceLayout.ContentSize.x * .5f - SettingsRightOffset
                    - IconSize * 1.5f - IconGap * 2f;
                float statusWidth = statusRight - statusLeft;
                float statusCenter = (statusLeft + statusRight) * .5f;
                Plaque(root, "Ship Status Inset", statusCenter, statusWidth, accent);
                Place(root, "Voyage Header Status", statusCenter, 0, statusWidth, 60);
                RectTransform deck = root.Find("Battle Deck") as RectTransform;
                PositionSettingsButton(deck);
                if (deck != null) deck.anchoredPosition += Vector2.left * (IconSize + IconGap);
                ApplyIconButton(root.Find("Battle Deck")?.GetComponent<Button>(), false);
                ApplySettingsButton(root.Find("Battle Settings")?.GetComponent<Button>());
                Style(root.Find("Voyage Header Title"), 32, accent, FontStyle.Bold);
                Style(root.Find("Voyage Header Status"), 28, Ivory, FontStyle.Normal, TextAnchor.MiddleRight);
                Text status = root.Find("Voyage Header Status")?.GetComponent<Text>();
                if (status != null)
                {
                    status.resizeTextForBestFit = false;
                    status.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
            }
            else
            {
                Plaque(root, "Clock Inset", 165, 760, accent);
                Style(root.Find("Voyage Header Title"), 29, Ivory, FontStyle.Bold, TextAnchor.MiddleLeft);
                Style(root.Find("Voyage Header Status"), 18, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
                foreach (Button button in root.GetComponentsInChildren<Button>(true)) StyleButton(button, accent, true);
            }
        }

        private static void Surface(RectTransform root, Color accent, string portName)
        {
            var graphic = Graphic(root, "Header Brass Rules", accent, false, portName);
            graphic.rectTransform.anchorMin = Vector2.zero;
            graphic.rectTransform.anchorMax = Vector2.one;
            graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
            // Keep the actual regional frame as the bottom layer for editable prefab bindings.
            graphic.transform.SetSiblingIndex(1);
            Image backing = root.GetComponent<Image>();
            if (backing != null) backing.raycastTarget = false;
        }

        private static void Plaque(RectTransform root, string name, float x, float width, Color accent, bool visible = true)
        {
            var graphic = Graphic(root, name, accent, true);
            Box(graphic.rectTransform, x, 0, width, 70);
            graphic.transform.SetSiblingIndex(2);
            graphic.gameObject.SetActive(visible);
        }

        private static NauticalHeaderGraphic Graphic(RectTransform parent, string name, Color accent, bool inset, string portName = null)
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
            graphic.Configure(accent, inset, portName);
            return graphic;
        }

        private static void StyleButton(Button button, Color accent, bool voyage = false)
        {
            if (button.name.EndsWith("Settings", System.StringComparison.Ordinal))
            {
                ApplySettingsButton(button);
                return;
            }
            if (voyage && button.name == "Voyage Cargo")
            {
                ApplyCabinButton(button);
                return;
            }
            // Keep the original selectable and walnut asset, with a slimmer brass edge.
            WoodButtonSkin.Apply(button, Color.white);
            Image face = button.GetComponent<Image>();
            if (face != null) face.pixelsPerUnitMultiplier = 40f;
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

        /// <summary>Identical game-viewport coordinates in ordinary play and guided lessons.</summary>
        public static void PositionSettingsButton(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f;
            rect.localScale = Vector3.one;
            rect.sizeDelta = Vector2.one * IconSize;
            rect.anchoredPosition = new Vector2(-SettingsRightOffset, -SettingsTopOffset);
        }

        public static void ApplySettingsButton(Button button)
        {
            if (button == null) return;
            PositionSettingsButton((RectTransform)button.transform);
            ApplyIconButton(button, true);
        }

        public static void ApplyCabinButton(Button button)
        {
            if (button == null) return;
            RectTransform rect = (RectTransform)button.transform;
            PositionSettingsButton(rect);
            rect.anchoredPosition += Vector2.left * (IconSize + IconGap);
            ApplyFacilityIcon(button, PortHeaderIconKind.Cabin);
        }

        public static void ApplyIconButton(Button button, bool settings)
        {
            if (button == null) return;
            Transform child = button.transform.Find(button.name + " Icon");
            if (child == null)
            {
                var node = new GameObject(button.name + " Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHeaderIconGraphic));
                node.layer = button.gameObject.layer;
                node.transform.SetParent(button.transform, false);
                child = node.transform;
            }
            BattleHeaderIconGraphic icon = child.GetComponent<BattleHeaderIconGraphic>();
            icon.Configure(settings, new Color(.85f, .69f, .40f, 1f));
            StyleIconButton(button, icon);
        }

        public static void ApplyFacilityNavigation(RectTransform root, int facility, int facilityCount)
        {
            if (root == null) return;
            Transform heading = root.Find("Port Facility Icon");
            if (heading == null)
            {
                var node = new GameObject("Port Facility Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(PortHeaderIconGraphic));
                node.layer = root.gameObject.layer;
                node.transform.SetParent(root, false);
                heading = node.transform;
            }
            PortHeaderIconGraphic currentIcon = heading.GetComponent<PortHeaderIconGraphic>();
            currentIcon.Configure((PortHeaderIconKind)facility, false);
            currentIcon.enabled = true;
            Box(currentIcon.rectTransform, -431 - IconGap - IconSize * .5f, 0, IconSize, IconSize);
            Button back = root.Find("Return To Port Hub")?.GetComponent<Button>();
            ApplyFacilityIcon(back, PortHeaderIconKind.Back);
            if (back != null)
            {
                RectTransform rect = (RectTransform)back.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = Vector2.one * .5f;
                rect.localScale = Vector3.one;
                rect.anchoredPosition = new Vector2(SettingsRightOffset, -SettingsTopOffset);
            }
            int previous = (facility + facilityCount - 1) % facilityCount;
            int next = (facility + 1) % facilityCount;
            Button previousButton = root.Find("Previous Port Facility")?.GetComponent<Button>();
            Button nextButton = root.Find("Next Port Facility")?.GetComponent<Button>();
            ApplyFacilityIcon(previousButton, (PortHeaderIconKind)previous);
            ApplyFacilityIcon(nextButton, (PortHeaderIconKind)next);
            ApplyNavigationArrow(previousButton, false);
            ApplyNavigationArrow(nextButton, true);
        }

        private static void ApplyNavigationArrow(Button button, bool right)
        {
            if (button == null) return;
            Transform child = button.transform.Find("Navigation Arrow");
            if (child == null)
            {
                var node = new GameObject("Navigation Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(HeaderNavigationArrowGraphic));
                node.layer = button.gameObject.layer;
                node.transform.SetParent(button.transform, false);
                child = node.transform;
            }
            var arrow = child.GetComponent<HeaderNavigationArrowGraphic>();
            arrow.Configure(right, button);
            arrow.enabled = true;
            float offset = IconSize * .5f + 24f;
            Box(arrow.rectTransform, right ? offset : -offset, 0, 24, 40);
        }

        private static void ApplyFacilityIcon(Button button, PortHeaderIconKind kind)
        {
            if (button == null) return;
            Transform child = button.transform.Find(button.name + " Icon");
            if (child == null)
            {
                var node = new GameObject(button.name + " Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(PortHeaderIconGraphic));
                node.layer = button.gameObject.layer;
                node.transform.SetParent(button.transform, false);
                child = node.transform;
            }
            PortHeaderIconGraphic icon = child.GetComponent<PortHeaderIconGraphic>();
            icon.Configure(kind);
            StyleIconButton(button, icon);
        }

        private static void StyleIconButton(Button button, Graphic icon)
        {
            ((RectTransform)button.transform).sizeDelta = Vector2.one * IconSize;
            Image hitArea = button.GetComponent<Image>();
            if (hitArea != null)
            {
                hitArea.sprite = hitArea.overrideSprite = null;
                hitArea.type = Image.Type.Simple;
                hitArea.color = Color.clear;
                hitArea.raycastTarget = true;
            }
            foreach (Shadow edge in button.GetComponents<Shadow>()) edge.enabled = false;
            Box(icon.rectTransform, 0, 0, IconSize, IconSize);
            icon.enabled = true;
            icon.raycastTarget = true;
            foreach (Graphic decoration in button.GetComponentsInChildren<Graphic>(true))
            {
                if (decoration == hitArea || decoration == icon) continue;
                decoration.enabled = false;
                decoration.raycastTarget = false;
                if (decoration is Text label) Box(label.rectTransform, 0, 0, IconSize, IconSize);
            }
            button.targetGraphic = icon;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.15f, 1f);
            colors.selectedColor = new Color(1.1f, 1.1f, 1.05f, 1f);
            colors.pressedColor = new Color(.7f, .7f, .7f, 1f);
            colors.disabledColor = new Color(.6f, .6f, .6f, .35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
            button.colors = colors;
        }

        public static void ApplyGoldReadout(RectTransform root, string prefix)
        {
            Text gold = root.Find(prefix + "Gold")?.GetComponent<Text>();
            if (gold == null) return;
            const float iconX = 590f, amountWidth = 160f;
            Box(gold.rectTransform, iconX + IconSize * .5f + IconGap + amountWidth * .5f, 0, amountWidth, 64);
            gold.alignment = TextAnchor.MiddleLeft;
            Transform child = root.Find(prefix + "Gold Icon");
            if (child == null)
            {
                var node = new GameObject(prefix + "Gold Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleCommandIconGraphic));
                node.layer = root.gameObject.layer;
                node.transform.SetParent(root, false);
                child = node.transform;
            }
            var icon = child.GetComponent<BattleCommandIconGraphic>();
            icon.Configure(BattleCommandIconKind.Gold);
            Box(icon.rectTransform, iconX, 0, IconSize, IconSize);
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
