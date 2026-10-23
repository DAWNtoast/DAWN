using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Places the existing departure controls over each regional painted dock scene.</summary>
    public static class DepartureDockSceneLayout
    {
        // Every regional painting and control uses this normalized art space,
        // independently of the texture resolution and screen size.
        public const float SourceWidth = 2048f;
        public const float SourceHeight = 1152f;
        private const float ArtScale = 1920f / SourceWidth;
        public const float ShipCenterX = 1168f * ArtScale - 960f;
        public const float ShipWaterlineY = 540f - 548f * ArtScale;
        public static readonly Vector2 ShipHitSize = new Vector2(928f, 478f) * ArtScale;
        public static readonly Vector2 ShipHitPosition = ArtPosition(1168f, 309f);
        private static readonly Color SceneInk = new Color(.96f, .92f, .82f, 1f);
        private static readonly Color SceneGold = new Color(1f, .87f, .61f, 1f);

        // Call after the shared layout and skin. The controller owns port selection,
        // interaction state, and the hull's waterline fit; this class only places UI.
        public static void Apply(RectTransform route, RectTransform setup, bool expandedChart,
            bool treasure, bool crew, int selectedRoute)
        {
            if (route != null)
            {
                bool compact = !crew;
                // The selected chart is its own full-surface, non-raycasting
                // graphic. Its mesh projects into the blank parchment slot.
                SetActive(route, "Departure Dock Selected Sea", compact && !expandedChart && !treasure);
                SetActive(route, "Departure Dock Sea Detail", compact && !expandedChart && !treasure);
                SetActive(route, "Departure Dock Treasure Selection", compact && treasure);
                SetActive(route, "Departure Dock Route Heading", compact);
                SetActive(route, "Departure Chart Selection/Departure Dock Chart Records", compact && expandedChart);
                SetActive(route, "Departure Chart Selection/Departure Dock Chart Records Heading", compact && expandedChart);
                SetActive(route, "Departure Chart Selection/Departure Dock Chart Records Empty", compact && expandedChart);
                SetActive(route, "Departure Chart Selection/Seed Input/Seed Input Dropdown", !compact);
                if (compact)
                {
                    ApplyRouteTable(route, selectedRoute, expandedChart, treasure);
                    if (expandedChart) ApplyInlineChart(route);
                    if (treasure) FindNode(route, "Departure Dock Treasure Selection")?.SetAsLastSibling();
                }
                else
                {
                    Image paper = route.GetComponent<Image>();
                    if (paper != null) paper.raycastTarget = true;
                    RectTransform toggle = FindNode(route, "Use Chart Seed") as RectTransform;
                    if (toggle != null)
                    {
                        Place(toggle, "Background", 32, 32, -234, 0);
                        Place(toggle, "Background/Checkmark", 32, 32, 0, 0);
                        Text caption = FitText(toggle, "Seed Label", 450, 34, 25, 0, 20, 18, TextAnchor.MiddleLeft);
                        if (caption != null)
                        {
                            caption.enabled = true;
                            Toggle control = toggle.GetComponent<Toggle>();
                            caption.text = control != null && control.interactable ?
                                "使用普通海图 · 选择已绘海图" : "普通海图 · 航海士随船可复用";
                        }
                        PaperLabel(caption);
                    }
                }
            }

            SetActive(setup, "Departure Dock Vessel Controls", false);
            SetActive(setup, "Departure Dock Ship Hint", false);
            SetActive(setup, "Departure Dock Crew Readout Panel", !crew);
            // The crew editor retains the shared paper layout and its controls.
            if (setup == null || crew)
            {
                Image paper = setup == null ? null : setup.GetComponent<Image>();
                if (paper != null) paper.raycastTarget = true;
                return;
            }
            SetRect(setup, new Vector2(1920f, 1080f), Vector2.zero);
            ClearPanel(setup);

            // Keep only the two independent arrows beside the vessel. The berth
            // retains its own inspection target without a caption bar below it.
            ArtPlace(setup, "Inspect Departure Vessel", 704, 70, 928, 478);
            StandaloneButton(setup, "Departure Previous Vessel", 640, 344, 72, 72, 36, "←");
            StandaloneButton(setup, "Departure Next Vessel", 1624, 344, 72, 72, 36, "→");
            HideText(setup, "Setup Title");

            CrewReadoutDesk(setup, "Departure Dock Crew Readout Panel", 1040, 912, 512, 240);
            HideText(setup, "DepartureOrdinaryCrewCount");
            HideText(setup, "DepartureCrewSpeedPreview");
            StandaloneButton(setup, "DepartureOrdinaryCrewDecreaseButton", 1375, 997, 40, 34, 24, "−");
            StandaloneButton(setup, "DepartureOrdinaryCrewIncreaseButton", 1423, 997, 40, 34, 24, "+");
            SceneLabel(ArtText(setup, "Departure Dock Crew Heading", 1194, 936, 210, 35, 21, 18, TextAnchor.MiddleCenter), SceneGold);
            SceneLabel(ArtText(setup, "Departure Dock Ordinary Readout", 1114, 995, 214, 28, 18, 16, TextAnchor.MiddleLeft), SceneInk);
            SceneLabel(ArtText(setup, "Departure Dock Special Readout", 1114, 1023, 250, 23, 17, 15, TextAnchor.MiddleLeft), SceneInk);
            SceneLabel(ArtText(setup, "Departure Dock Speed Readout", 1114, 1047, 334, 24, 19, 17, TextAnchor.MiddleLeft), SceneGold);

            Hotspot(setup, "Departure Buy Provisions", 1008, 737, 220, 51);
            PaintedCaption(setup, "Departure Buy Provisions", 21, 18, 43, 17);
            Hotspot(setup, "Configure Voyage Battle Decks", 1326, 740, 239, 52);
            PaintedCaption(setup, "Configure Voyage Battle Decks", 21, 18, 42, 16);
            Hotspot(setup, "Configure Voyage Crew", 1753, 742, 181, 50);
            PaintedCaption(setup, "Configure Voyage Crew", 23, 19, 41, 15);
            Hotspot(setup, "Adjust Voyage Supplies", 1854, 535, 178, 48);
            PaintedCaption(setup, "Adjust Voyage Supplies", 22, 18, 43, 15);
            Hotspot(setup, "Start Voyage", 1573, 998, 473, 110);
            PaintedCaption(setup, "Start Voyage", 40, 31, 139, 34, 17, 17);
        }

        private static void ApplyRouteTable(RectTransform route, int selectedRoute, bool chartMode, bool treasure)
        {
            SetRect(route, PhaseSurfaceLayout.ContentSize, Vector2.zero);
            ClearPanel(route);
            EnsureRouteHeading(route);
            SceneLabel(ArtText(route, "Departure Dock Route Heading", 321, 522, 228, 36, 27, 23, TextAnchor.MiddleCenter), SceneGold);
            RectTransform toggle = ArtPlace(route, "Use Chart Seed", 210, 567, 185, 51);
            if (toggle != null)
            {
                DeparturePaintedHotspot.Apply(toggle.GetComponent<Toggle>(), chartMode);
                Text seed = FitText(toggle, "Seed Label", 128 * ArtScale, 35 * ArtScale,
                    16.5f * ArtScale, 0, 20, 17, TextAnchor.MiddleCenter);
                if (seed != null)
                {
                    seed.text = "使用已绘海图";
                    seed.enabled = true;
                    SceneLabel(seed, SceneInk);
                }
            }
            Hotspot(route, "Departure Dock Ordinary Voyage", 35, 567, 170, 51, !chartMode && !treasure);
            PaintedCaption(route, "Departure Dock Ordinary Voyage", 22, 19, 52, 12, value: "普通航程");
            Hotspot(route, "Departure Treasure Seed Picker", 408, 567, 215, 51, treasure);
            PaintedCaption(route, "Departure Treasure Seed Picker", 20, 18, 57, 59, value: "藏宝海图");
            SceneLabel(ArtText(route, "Departure Dock Treasure Count", 565, 576, 44, 32, 18, 15, TextAnchor.MiddleCenter), SceneGold);
            PaperLabel(ArtText(route, "Departure Route Summary", 45, 623, 765, 31, 22, 19, TextAnchor.MiddleLeft));
            for (int index = 0; index < 6; index++)
            {
                string path = "Departure Destination " + index;
                Button button = FindNode(route, path)?.GetComponent<Button>();
                WoodButtonRoleState role = button == null ? null : button.GetComponent<WoodButtonRoleState>();
                Hotspot(route, path, 35 + index * 134, 660, 131, 40, role != null && role.IsSelected, showOutline: false);
                PaintedCaption(route, path, 22, 19, 8, 8, 4, 4);
            }
            PaperLabel(ArtText(route, "Departure Dock Sea Detail", 640, 719, 226, 160, 18, 16, TextAnchor.UpperLeft));
            float[] cardX = { 15, 305, 584 };
            for (int index = 0; index < 3; index++)
            {
                string path = "Route " + index;
                Hotspot(route, path, cardX[index], 898, 278, 118, index == selectedRoute, showOutline: false);
                PaintedCaption(route, path, 23, 19, 11, 11, 79, 5);
                SceneLabel(ArtText(route, "Departure Dock Route Lock " + index, cardX[index] + 5, 1019, 268, 25,
                    17, 15, TextAnchor.MiddleCenter), SceneGold);
            }
        }

        private static void ApplyInlineChart(RectTransform route)
        {
            RectTransform selection = ArtPlace(route, "Departure Chart Selection", 35, 706, 816, 184);
            if (selection == null) return;
            // Match the treasure chart table: thumbnail on the left, seed controls on the right.
            const float previewX = -226;
            const float controlsX = 176;
            const float controlsWidth = 400;
            Place(selection, "Seed Input", controlsWidth, 28, controlsX, 42);
            PaperLabel(FitText(selection, "Departure Dock Chart Records Heading", controlsWidth, 22, controlsX, 69, 16, 14, TextAnchor.MiddleLeft));
            Place(selection, "Departure Dock Chart Records", controlsWidth, 82, controlsX, -18);
            PaperLabel(FitText(selection, "Departure Dock Chart Records Empty", controlsWidth - 10, 74, controlsX, -18, 17, 15, TextAnchor.MiddleCenter));
            PaperLabel(FitText(selection, "Chart Seed Validation", controlsWidth, 22, controlsX, -72, 13, 12, TextAnchor.MiddleLeft));
            RectTransform input = FindNode(selection, "Seed Input") as RectTransform;
            if (input != null)
            {
                FitText(input, "Text", controlsWidth - 20, 24, 0, 0, 14, 12, TextAnchor.MiddleLeft);
                FitText(input, "Placeholder", controlsWidth - 20, 24, 0, 0, 14, 12, TextAnchor.MiddleLeft);
            }
            RectTransform preview = FindNode(selection, "Departure Chart Preview") as RectTransform;
            Vector2 previewSize = new Vector2(246, 138.375f);
            if (preview != null)
            {
                Vector2 oldSize = preview.sizeDelta;
                foreach (string endpoint in new[] { "Departure Chart Preview Origin", "Departure Chart Preview Destination" })
                {
                    RectTransform marker = FindNode(preview, endpoint) as RectTransform;
                    if (marker == null) continue;
                    if (oldSize.x > 0 && oldSize.y > 0)
                        marker.anchoredPosition = Vector2.Scale(marker.anchoredPosition,
                            new Vector2(previewSize.x / oldSize.x, previewSize.y / oldSize.y));
                    Place(marker, "Halo", 15, 15, 0, 0);
                    Place(marker, "Marker", 10, 10, 0, 0);
                    FitText(marker, "Label", 40, 18, 0, -15, 11, 10, TextAnchor.MiddleCenter);
                }
                SetRect(preview, previewSize, new Vector2(previewX, 0));
                Place(preview, "Departure Chart Preview Water", previewSize.x, previewSize.y, 0, 0);
                Place(preview, "Departure Chart Preview Surface", previewSize.x, previewSize.y, 0, 0);
            }
            PaperLabel(FitText(selection, "Departure Chart Route", 306, 16, previewX, 78, 16, 14, TextAnchor.MiddleCenter));
            PaperLabel(FitText(selection, "Departure Chart Ports", 306, 16, previewX, -78, 13, 11, TextAnchor.MiddleCenter));
            PaperLabel(FitText(selection, "Departure Chart Preview Empty", 294, 140, previewX, 0, 19, 17, TextAnchor.MiddleCenter));
            selection.SetAsLastSibling();
        }

        public static Vector2 ArtPosition(float x, float y) => new Vector2(x * ArtScale - 960f, 540f - y * ArtScale);

        private static RectTransform ArtPlace(Transform parent, string path, float x, float y, float width, float height)
        {
            Vector2 position = ArtPosition(x + width * .5f, y + height * .5f);
            return Place(parent, path, width * ArtScale, height * ArtScale, position.x, position.y);
        }

        private static Text ArtText(Transform parent, string path, float x, float y, float width, float height,
            int maximum, int minimum, TextAnchor anchor)
        {
            Vector2 position = ArtPosition(x + width * .5f, y + height * .5f);
            Text label = FitText(parent, path, width * ArtScale, height * ArtScale,
                position.x, position.y, maximum, minimum, anchor);
            if (label != null) label.enabled = true;
            return label;
        }

        private static void Hotspot(Transform parent, string path, float x, float y, float width, float height,
            bool selected = false, bool round = false, bool showOutline = true)
        {
            RectTransform rect = ArtPlace(parent, path, x, y, width, height);
            if (rect != null) DeparturePaintedHotspot.Apply(rect.GetComponent<Selectable>(), selected, round, showOutline);
        }

        private static void CrewReadoutDesk(Transform parent, string name, float x, float y, float width, float height)
        {
            RectTransform rect = FindNode(parent, name) as RectTransform;
            if (rect == null)
            {
                var holder = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                holder.transform.SetParent(parent, false);
                rect = holder.GetComponent<RectTransform>();
            }
            ArtPlace(parent, name, x, y, width, height);
            Image face = rect.GetComponent<Image>();
            // The desk is painted into each regional background. Retain only
            // its layout bounds so live readouts and retained views keep their identity.
            face.sprite = null;
            face.overrideSprite = null;
            face.type = Image.Type.Simple;
            face.preserveAspect = false;
            face.color = Color.clear;
            face.raycastTarget = false;
            rect.gameObject.SetActive(true);
            rect.SetAsLastSibling();
        }

        private static void StandaloneButton(Transform parent, string path, float x, float y,
            float width, float height, int fontSize, string caption)
        {
            RectTransform rect = ArtPlace(parent, path, x, y, width, height);
            Button button = rect == null ? null : rect.GetComponent<Button>();
            if (button == null) return;
            // These controls no longer borrow a painted arrow or +/- from the
            // background. Restore and reuse the existing Button and callback.
            DeparturePaintedHotspot.RestoreAll(rect);
            Image image = button.GetComponent<Image>();
            if (image != null) { image.enabled = true; image.raycastTarget = true; }
            WoodButtonSkin.Apply(button, Color.white);
            PaintedCaption(parent, path, fontSize, fontSize - 3, 5, 5, 3, 3, caption);
            rect.SetAsLastSibling();
        }

        private static void HideText(Transform parent, string path)
        {
            Text label = FindNode(parent, path)?.GetComponent<Text>();
            if (label != null) label.enabled = false;
        }

        private static void ClearPanel(RectTransform panel)
        {
            Image image = panel.GetComponent<Image>();
            if (image == null) return;
            image.sprite = null;
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        private static void SceneLabel(Text label, Color color)
        {
            if (label == null) return;
            label.color = color;
            label.raycastTarget = false;
            UiLabelBacking.Apply(label, true);
            Outline outline = label.GetComponent<Outline>();
            if (outline != null) outline.effectDistance = new Vector2(.6f, -.6f);
        }

        private static void EnsureRouteHeading(RectTransform route)
        {
            if (FindNode(route, "Departure Dock Route Heading") != null) return;
            Text template = FindNode(route, "Departure Route Summary")?.GetComponent<Text>();
            if (template == null) return;
            var holder = new GameObject("Departure Dock Route Heading", typeof(RectTransform), typeof(CanvasRenderer));
            holder.transform.SetParent(route, false);
            Text heading = holder.AddComponent<Text>();
            heading.font = template.font;
            heading.fontStyle = FontStyle.Normal;
            heading.text = "航线与海图";
            heading.raycastTarget = false;
        }

        // Only the painted face is replaced. Live labels stay in their original
        // Buttons, so localization and changing gameplay values remain editable.
        private static void PaintedCaption(Transform parent, string path, int maximum, int minimum,
            float leftInset = 16, float rightInset = 16, float topInset = 8, float bottomInset = 8,
            string value = null)
        {
            RectTransform button = FindNode(parent, path) as RectTransform;
            if (button == null) return;
            Text label = FitText(button, "Label", button.sizeDelta.x - (leftInset + rightInset) * ArtScale,
                button.sizeDelta.y - (topInset + bottomInset) * ArtScale,
                (leftInset - rightInset) * ArtScale * .5f, (bottomInset - topInset) * ArtScale * .5f,
                maximum, minimum, TextAnchor.MiddleCenter);
            if (label == null) return;
            if (value != null) label.text = value;
            label.enabled = true;
            label.fontStyle = FontStyle.Normal;
            label.canvasRenderer.SetColor(Color.white);
            Selectable control = button.GetComponent<Selectable>();
            SceneLabel(label, control != null && !control.IsInteractable() ? new Color(.68f, .67f, .60f, 1f) : SceneInk);
            label.transform.SetAsLastSibling();
        }

        private static void PaperLabel(Text label)
        {
            if (label == null) return;
            label.color = DepartureSkin.Ink;
            label.raycastTarget = false;
            foreach (Shadow shadow in label.GetComponents<Shadow>()) shadow.enabled = false;
        }

        private static void SetActive(Transform parent, string path, bool active)
        {
            Transform child = FindNode(parent, path);
            if (child != null) child.gameObject.SetActive(active);
        }

        private static RectTransform Place(Transform parent, string path, float width, float height, float x, float y)
        {
            RectTransform rect = FindNode(parent, path) as RectTransform;
            if (rect == null) return null;
            SetRect(rect, new Vector2(width, height), new Vector2(x, y));
            return rect;
        }

        private static Transform FindNode(Transform parent, string path)
        {
            if (parent == null) return null;
            foreach (string name in path.Split('/'))
            {
                Transform match = null;
                for (int index = 0; index < parent.childCount; index++)
                {
                    Transform child = parent.GetChild(index);
                    if (child.name != name) continue;
                    if (match == null || child.gameObject.activeSelf) match = child;
                    if (child.gameObject.activeSelf) break;
                }
                if (match == null) return null;
                parent = match;
            }
            return parent;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static Text FitText(Transform parent, string path, float width, float height, float x, float y,
            int maximum, int minimum, TextAnchor alignment)
        {
            RectTransform rect = Place(parent, path, width, height, x, y);
            if (rect == null || !rect.TryGetComponent(out Text label)) return null;
            label.fontSize = label.resizeTextMaxSize = maximum;
            label.resizeTextMinSize = minimum;
            label.resizeTextForBestFit = true;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 1f;
            return label;
        }

        private static void FitButtonCaption(Transform parent, string path, int maximum, int minimum)
        {
            RectTransform button = FindNode(parent, path) as RectTransform;
            if (button == null) return;
            FitText(button, "Label", button.sizeDelta.x - 16, button.sizeDelta.y - 8, 0, 0,
                maximum, minimum, TextAnchor.MiddleCenter);
        }
    }
}
