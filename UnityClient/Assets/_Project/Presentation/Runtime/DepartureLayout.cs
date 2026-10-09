using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Departure actions remain visible while enlarged details can scroll.</summary>
    public static class DepartureLayout
    {
        public static readonly Vector2 RouteSize = new Vector2(900f, 880f);
        public static readonly Vector2 SetupSize = new Vector2(950f, 880f);
        public static readonly Vector2 RoutePosition = new Vector2(-486f, -60f);
        public static readonly Vector2 SetupPosition = new Vector2(463f, -60f);
        private const string DetailViewport = "Departure Details Viewport";
        private const string DetailContent = "Departure Details Content";

        public static void Apply(RectTransform routePanel, RectTransform setupPanel)
        {
            ApplyRoute(routePanel);
            ApplySetup(setupPanel);
        }

        public static Transform Find(Transform panel, string name)
        {
            if (panel == null) return null;
            Transform direct = FindNode(panel, name);
            if (direct != null && direct.gameObject.activeSelf) return direct;
            Transform detail = FindNode(panel, DetailViewport + "/" + DetailContent + "/" + name);
            return detail != null && detail.gameObject.activeSelf ? detail : null;
        }

        public static void ApplyRoute(RectTransform panel)
        {
            if (panel == null) return;
            FitDeparturePanel(panel, RouteSize, RoutePosition);
            float topShift = panel.rect.height * .5f - 440;
            for (int index = 0; index < 6; index++)
            {
                string path = "Departure Destination " + index;
                Place(panel, path, 134, 64, -360 + index * 144, 306 + topShift);
                Caption(panel, path, 22);
            }
            Place(panel, "Use Chart Seed", 414, 80, -216, 390 + topShift);
            Place(panel, "Use Chart Seed/Background", 40, 40, -187, 0);
            Label(panel, "Use Chart Seed/Background/Checkmark", 40, 40, 0, 0, 22, TextAnchor.MiddleCenter);
            Label(panel, "Use Chart Seed/Seed Label", 350, 76, 27, 0, 20, TextAnchor.MiddleLeft);
            Label(panel, "Seed Locked Hint", 416, 80, 219, 390 + topShift, 18, TextAnchor.MiddleRight);
            Label(panel, "Seed Label", 330, 76, -258, 390 + topShift, 20, TextAnchor.MiddleLeft);
            Label(panel, "Treasure Seed", 510, 76, 173, 390 + topShift, 18, TextAnchor.MiddleRight);
            float rowHeight = Mathf.Min(212, (panel.rect.height - 218) / 3f);
            float rowGap = Mathf.Lerp(10, 18, Mathf.InverseLerp(816, 880, panel.rect.height));
            for (int index = 0; index < 3; index++)
            {
                string path = "Route " + index;
                Place(panel, path, 856, rowHeight, 0, 258 + topShift - rowHeight * .5f - index * (rowHeight + rowGap));
                Label(panel, path + "/Label", 622, rowHeight - 16, 109, 0, 22, TextAnchor.MiddleLeft);
                RectTransform art = Place(panel, path + "/Route Preview " + index, 210, 142, -315, 0);
                if (art != null && art.TryGetComponent(out RawImage image)) UiImageAspect.Fit(image, new Vector2(210, 142));
            }
            ApplyChart(panel);
            ApplyTreasure(panel);
        }

        private static void FitDeparturePanel(RectTransform panel, Vector2 standardSize, Vector2 standardPosition)
        {
            RectTransform header = FindNode(panel.parent, "Port Facility Header") as RectTransform;
            float headerHeight = header == null ? PhaseSurfaceLayout.HeaderHeight : header.rect.height;
            float top = Mathf.Min(380, 540 - headerHeight - 12);
            panel.sizeDelta = new Vector2(standardSize.x, top + 500);
            panel.anchoredPosition = new Vector2(standardPosition.x, (top - 500) * .5f);
        }

        private static void ApplyChart(RectTransform panel)
        {
            RectTransform selection = panel.Find("Departure Chart Selection") as RectTransform;
            if (selection == null) return;
            float topShift = panel.rect.height * .5f - 440;
            Place(panel, "Departure Chart Selection", 856, 676, 0, 0);
            Place(selection, "Seed Input", 856, 64, 0, 230 + topShift);
            Label(selection, "Seed Input/Text", 608, 56, -116, 0, 22, TextAnchor.MiddleLeft);
            Label(selection, "Seed Input/Placeholder", 608, 56, -116, 0, 18, TextAnchor.MiddleLeft);
            Place(selection, "Seed Input/Seed Input Dropdown", 224, 64, 316, 0);
            Label(selection, "Seed Input/Seed Input Dropdown/Label", 208, 56, 0, 0, 22, TextAnchor.MiddleCenter);
            RectTransform dropdown = selection.Find("Seed Input/Seed Input Dropdown") as RectTransform;
            if (dropdown != null)
            {
                RectTransform template = dropdown.Find("Template") as RectTransform;
                if (template != null)
                {
                    template.sizeDelta = new Vector2(856, 330);
                    RectTransform viewport = template.Find("Viewport") as RectTransform;
                    if (viewport != null) viewport.sizeDelta = new Vector2(848, 322);
                    RectTransform content = viewport == null ? null : viewport.Find("Content") as RectTransform;
                    RectTransform item = content == null ? null : content.Find("Item") as RectTransform;
                    if (item != null)
                    {
                        float itemHeight = 108 * GameReadability.Scale;
                        item.sizeDelta = new Vector2(0, itemHeight);
                        Label(item, "Item Label", 820, itemHeight - 6, 0, 0, 22, TextAnchor.MiddleLeft);
                    }
                }
            }
            float routeHeight = Measured(selection, "Departure Chart Route", 856, 32, 46);
            Place(selection, "Departure Chart Route", 856, routeHeight, 0, 184 + topShift - routeHeight * .5f);
            float validationHeight = Measured(selection, "Chart Seed Validation", 856, 18, 32);
            float bottom = -panel.rect.height * .5f + 18;
            float validationTop = bottom + validationHeight;
            Place(selection, "Chart Seed Validation", 856, validationHeight, 0, bottom + validationHeight * .5f);
            float portsHeight = Measured(selection, "Departure Chart Ports", 856, 22, 40);
            float portsBottom = validationTop + 12;
            Place(selection, "Departure Chart Ports", 856, portsHeight, 0, portsBottom + portsHeight * .5f);
            float mapTop = 184 + topShift - routeHeight - 14;
            float mapBottom = portsBottom + portsHeight + 14;
            float mapHeight = Mathf.Min(450, 856 * 9f / 16f, mapTop - mapBottom);
            float mapY = (mapTop + mapBottom) * .5f;
            float mapWidth = mapHeight * 16f / 9f;
            RectTransform preview = Place(selection, "Departure Chart Preview", mapWidth, mapHeight, 0, mapY);
            if (preview != null)
            {
                Place(preview, "Departure Chart Preview Water", mapWidth, mapHeight, 0, 0);
                Place(preview, "Departure Chart Preview Surface", mapWidth, mapHeight, 0, 0);
                foreach (string marker in new[] { "Departure Chart Preview Origin", "Departure Chart Preview Destination" })
                    Label(preview, marker + "/Label", 100, 42, 0, -32, 18, TextAnchor.MiddleCenter);
            }
            Label(selection, "Departure Chart Preview Empty", 816, 180, 0, mapY, 22, TextAnchor.MiddleCenter);
        }

        private static void ApplyTreasure(RectTransform panel)
        {
            if (panel.Find("Treasure Departure Title") == null) return;
            float topShift = panel.rect.height * .5f - 440;
            float bottom = -panel.rect.height * .5f + 16;
            Label(panel, "Treasure Departure Title", 856, 70, 0, 226 + topShift, 32, TextAnchor.MiddleCenter);
            float footerHeight = Mathf.Max(64, Mathf.Ceil(GameReadability.FontSize(22) * 2.6f));
            Place(panel, "Cancel Treasure Voyage", 421, footerHeight, -217.5f, bottom + footerHeight * .5f);
            Place(panel, "Return To Treasure Table", 421, footerHeight, 217.5f, bottom + footerHeight * .5f);
            Caption(panel, "Cancel Treasure Voyage", 22); Caption(panel, "Return To Treasure Table", 22);
            float consumptionHeight = Measured(panel, "Treasure Departure Consumption", 856, 22, 112);
            float consumptionBottom = bottom + footerHeight + 14;
            Place(panel, "Treasure Departure Consumption", 856, consumptionHeight, 0, consumptionBottom + consumptionHeight * .5f);
            float bodyBottom = consumptionBottom + consumptionHeight + 14;
            float bodyTop = 177 + topShift, bodyY = (bodyTop + bodyBottom) * .5f, bodyHeight = bodyTop - bodyBottom;
            RectTransform art = Place(panel, "Treasure Departure Art", 236, Mathf.Min(133, bodyHeight), -310, bodyY);
            if (art != null && art.TryGetComponent(out RawImage image)) UiImageAspect.Fit(image, new Vector2(236, Mathf.Min(133, bodyHeight)));
            Label(panel, "Treasure Departure Detail", 606, bodyHeight, 125, bodyY, 22, TextAnchor.MiddleLeft);
        }

        public static void ApplySetup(RectTransform panel)
        {
            if (panel == null) return;
            FitDeparturePanel(panel, SetupSize, SetupPosition);
            float half = panel.rect.height * .5f;
            float titleHeight = Mathf.Max(48, GameReadability.FontSize(32) * 1.4f);
            Label(panel, "Setup Title", 650, titleHeight, -126, half - 24 - titleHeight * .5f, 32, TextAnchor.MiddleLeft);
            Place(panel, "Replay Tutorial", 218, titleHeight, 342, half - 24 - titleHeight * .5f);
            Caption(panel, "Replay Tutorial", 22);
            if (Find(panel, "Departure Crew Configuration Title") != null)
            {
                ApplyCrew(panel, half - 36 - titleHeight);
                return;
            }
            float bottom = -half + 16;
            float startHeight = Mathf.Max(58, GameReadability.FontSize(22) * 1.75f);
            Place(panel, "Start Voyage", 902, startHeight, 0, bottom + startHeight * .5f);
            Caption(panel, "Start Voyage", 22);
            bottom += startHeight + 12;
            float rowHeight = Mathf.Max(64, GameReadability.FontSize(22) * 2.9f);
            Place(panel, "Configure Voyage Battle Decks", 445, rowHeight, -228.5f, bottom + rowHeight * .5f);
            Place(panel, "Configure Voyage Crew", 445, rowHeight, 228.5f, bottom + rowHeight * .5f);
            Caption(panel, "Configure Voyage Battle Decks", 22); Caption(panel, "Configure Voyage Crew", 22);
            bottom += rowHeight + 12;
            Place(panel, "Departure Buy Provisions", 568, rowHeight, -167, bottom + rowHeight * .5f);
            Place(panel, "Adjust Voyage Supplies", 322, rowHeight, 290, bottom + rowHeight * .5f);
            Caption(panel, "Departure Buy Provisions", 22); Caption(panel, "Adjust Voyage Supplies", 22);
            bottom += rowHeight + 16;
            RectTransform content = ScrollContent(panel, half - 36 - titleHeight, bottom, out ScrollRect scroll, out float oldScroll);
            string[] details = { "Departure Previous Vessel", "Inspect Departure Vessel", "Departure Next Vessel", "Ship Stats",
                "Departure Ship Schematic Caption", "Departure Vessel Hint", "Destination Summary", "Cargo", "Departure Provision Quote" };
            foreach (string name in details) MoveToContent(panel, content, name);
            for (int index = 0; index < 5; index++)
            {
                MoveToContent(panel, content, "Upgrade Part " + index);
                MoveToContent(panel, content, "Upgrade Name " + index);
            }
            float top = -6;
            Place(content, "Departure Previous Vessel", 42, 64, -414, top - 64);
            Place(content, "Inspect Departure Vessel", 236, 148, -263, top - 74);
            Place(content, "Departure Next Vessel", 42, 64, -112, top - 64);
            Label(content, "Departure Previous Vessel/Label", 40, 54, 0, 0, 22, TextAnchor.MiddleCenter);
            Label(content, "Departure Next Vessel/Label", 40, 54, 0, 0, 22, TextAnchor.MiddleCenter);
            float captionHeight = Measured(content, "Departure Ship Schematic Caption", 334, 18, 28);
            Place(content, "Departure Ship Schematic Caption", 334, captionHeight, -263, top - 158 - captionHeight * .5f);
            float hintHeight = Measured(content, "Departure Vessel Hint", 334, 18, 28);
            Place(content, "Departure Vessel Hint", 334, hintHeight, -263, top - 168 - captionHeight - hintHeight * .5f);
            float statsHeight = Measured(content, "Ship Stats", 508, 22, 148);
            Place(content, "Ship Stats", 508, statsHeight, 182, top - statsHeight * .5f);
            top -= Mathf.Max(168 + captionHeight + hintHeight, statsHeight) + 16;
            top = FlowText(content, "Destination Summary", 872, 22, top, TextAnchor.MiddleLeft);
            float componentLabelHeight = 32;
            for (int index = 0; index < 5; index++)
                componentLabelHeight = Mathf.Max(componentLabelHeight, Measured(content, "Upgrade Name " + index, 164, 18, 32));
            for (int index = 0; index < 5; index++)
            {
                float x = -348 + index * 174;
                Place(content, "Upgrade Part " + index, 54, 54, x, top - 27);
                Place(content, "Upgrade Name " + index, 164, componentLabelHeight, x, top - 62 - componentLabelHeight * .5f);
            }
            top -= 62 + componentLabelHeight + 16;
            top = FlowText(content, "Cargo", 872, 22, top, TextAnchor.UpperLeft);
            float quoteHeight = Measured(content, "Departure Provision Quote", 834, 22, 90);
            RectTransform quote = Place(content, "Departure Provision Quote", 834, quoteHeight, 19, top - quoteHeight * .5f);
            if (quote != null)
            {
                RectTransform waterIcon = Place(quote, "Departure Provision Water Icon", 32, 32, -439, quoteHeight * .5f - 18);
                RectTransform foodIcon = Place(quote, "Departure Provision Food Icon", 32, 32, -439, quoteHeight * .5f - 58);
                UiImageAspect.Fit(waterIcon == null ? null : waterIcon.GetComponent<RawImage>(), new Vector2(32, 32));
                UiImageAspect.Fit(foodIcon == null ? null : foodIcon.GetComponent<RawImage>(), new Vector2(32, 32));
            }
            FinishScroll(content, scroll, oldScroll, -top + quoteHeight + 8);
        }

        private static void ApplyCrew(RectTransform panel, float topEdge)
        {
            float footerHeight = Mathf.Max(58, GameReadability.FontSize(22) * 1.9f);
            float bottom = -panel.rect.height * .5f + 16;
            Place(panel, "Back To Voyage Setup", 445, footerHeight, -228.5f, bottom + footerHeight * .5f);
            Place(panel, "Confirm Departure Crew", 445, footerHeight, 228.5f, bottom + footerHeight * .5f);
            Caption(panel, "Back To Voyage Setup", 22); Caption(panel, "Confirm Departure Crew", 22);
            RectTransform content = ScrollContent(panel, topEdge, bottom + 16 + footerHeight, out ScrollRect scroll, out float oldScroll);
            foreach (string name in new[] { "Departure Crew Configuration Title", "Departure Ordinary Crew Label", "Departure Ordinary Crew Slider",
                "Departure Ordinary Crew Hint", "Departure Special Crew Heading", "Departure Crew Total" }) MoveToContent(panel, content, name);
            for (int index = 0; index < 6; index++) MoveToContent(panel, content, "Departure Special Crew Toggle " + index);
            float top = -6;
            top = FlowText(content, "Departure Crew Configuration Title", 872, 32, top, TextAnchor.MiddleLeft);
            top = FlowText(content, "Departure Ordinary Crew Label", 872, 22, top, TextAnchor.MiddleLeft);
            Place(content, "Departure Ordinary Crew Slider", 848, 40, 0, top - 20);
            top -= 58;
            top = FlowText(content, "Departure Ordinary Crew Hint", 872, 20, top, TextAnchor.MiddleLeft);
            top = FlowText(content, "Departure Special Crew Heading", 872, 32, top, TextAnchor.MiddleLeft);
            float crewHeight = Mathf.Max(110, GameReadability.FontSize(20) * 4.5f);
            for (int index = 0; index < 6; index++)
            {
                string path = "Departure Special Crew Toggle " + index;
                Place(content, path, 428, crewHeight, index % 2 == 0 ? -222 : 222, top - crewHeight * .5f - index / 2 * (crewHeight + 14));
                RectTransform art = Place(content, path + "/Departure Special Crew Portrait " + index, 60, 82, -172, 0);
                if (art != null && art.TryGetComponent(out RawImage image)) UiImageAspect.Fit(image, new Vector2(60, 82));
                Label(content, path + "/Departure Special Crew Name " + index, 326, crewHeight - 52, 36, 23, 22, TextAnchor.MiddleLeft);
                Label(content, path + "/Departure Special Crew State " + index, 326, 42, 36, -(crewHeight - 46) * .5f, 18, TextAnchor.MiddleLeft);
            }
            top -= 3 * (crewHeight + 14);
            top = FlowText(content, "Departure Crew Total", 872, 22, top, TextAnchor.MiddleLeft);
            FinishScroll(content, scroll, oldScroll, -top);
        }

        private static RectTransform ScrollContent(RectTransform panel, float top, float bottom, out ScrollRect scroll, out float oldScroll)
        {
            RectTransform viewport = panel.Find(DetailViewport) as RectTransform;
            bool fresh = viewport == null;
            if (fresh)
            {
                viewport = new GameObject(DetailViewport, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
                viewport.SetParent(panel, false);
                viewport.GetComponent<Image>().color = Color.clear;
                RectTransform created = new GameObject(DetailContent, typeof(RectTransform)).GetComponent<RectTransform>();
                created.SetParent(viewport, false);
                created.anchorMin = created.anchorMax = new Vector2(.5f, 1f);
                created.pivot = new Vector2(.5f, 1f);
                created.anchoredPosition = new Vector2(-10, 0);
                RectTransform bar = new GameObject("Departure Details Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<RectTransform>();
                bar.SetParent(viewport, false);
                bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one;
                bar.pivot = new Vector2(1, .5f); bar.sizeDelta = new Vector2(14, -8); bar.anchoredPosition = new Vector2(-2, 0);
                bar.GetComponent<Image>().color = new Color(.08f, .055f, .035f, .75f);
                RectTransform handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                handle.SetParent(bar, false); handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
                handle.offsetMin = handle.offsetMax = Vector2.zero;
                Image handleImage = handle.GetComponent<Image>(); handleImage.color = new Color(.87f, .69f, .42f, 1f);
                Scrollbar scrollbar = bar.GetComponent<Scrollbar>(); scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scroll = viewport.GetComponent<ScrollRect>();
                scroll.viewport = viewport; scroll.content = created; scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 48;
                scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }
            scroll = viewport.GetComponent<ScrollRect>();
            // A content rect that previously fitted has no reading offset to
            // preserve. Its normalized getter can report zero at equal bounds.
            oldScroll = fresh || scroll.content.rect.height <= scroll.viewport.rect.height + .5f
                ? 1f : scroll.verticalNormalizedPosition;
            Place(panel, DetailViewport, 902, top - bottom, 0, (top + bottom) * .5f);
            RectTransform content = scroll.content;
            content.sizeDelta = new Vector2(872, content.sizeDelta.y);
            return content;
        }

        private static void FinishScroll(RectTransform content, ScrollRect scroll, float oldScroll, float height)
        {
            // FlowText uses a top-pivot content surface, so the last item's
            // measured position is the source of truth for the scroll extent.
            // A fixed estimate can leave the final quote/crew row below the
            // content rect after the reading size changes.
            float required = RequiredContentHeight(content);
            content.sizeDelta = new Vector2(872, Mathf.Max(height, required, scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = Mathf.Clamp01(oldScroll);
            if (oldScroll >= .999f) content.anchoredPosition = new Vector2(-10, 0);
            scroll.StopMovement();
        }

        private static float RequiredContentHeight(RectTransform content)
        {
            if (content == null) return 0f;
            float minimumY = 0f;
            Vector3[] corners = new Vector3[4];
            for (int index = 0; index < content.childCount; index++)
            {
                RectTransform child = content.GetChild(index) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                child.GetWorldCorners(corners);
                for (int corner = 0; corner < corners.Length; corner++)
                {
                    float y = content.InverseTransformPoint(corners[corner]).y;
                    minimumY = Mathf.Min(minimumY, y);
                }
            }
            return -minimumY + 12f;
        }

        private static void MoveToContent(RectTransform panel, RectTransform content, string name)
        {
            Transform item = Find(panel, name);
            if (item != null && item.parent != content) item.SetParent(content, false);
        }

        private static float FlowText(Transform panel, string name, float width, int standard, float top, TextAnchor alignment)
        {
            float height = Measured(panel, name, width, standard, GameReadability.FontSize(standard) * 1.3f);
            RectTransform rect = Place(panel, name, width, height, 0, top - height * .5f);
            if (rect != null && rect.TryGetComponent(out Text label)) label.alignment = alignment;
            return top - height - 14;
        }

        private static float Measured(Transform parent, string path, float width, int standard, float minimum)
        {
            RectTransform rect = FindNode(parent, path) as RectTransform;
            if (rect == null || !rect.TryGetComponent(out Text label)) return minimum;
            Style(label, standard);
            rect.sizeDelta = new Vector2(width, Mathf.Max(32, rect.sizeDelta.y));
            return Mathf.Max(minimum, Mathf.Ceil(label.preferredHeight) + 8);
        }

        private static RectTransform Place(Transform parent, string path, float width, float height, float x, float y)
        {
            RectTransform rect = FindNode(parent, path) as RectTransform;
            if (rect == null) return null;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            // Flow coordinates are measured down from the content's top edge.
            // Centre anchors would move every row again when its height changes.
            if (rect.parent != null && rect.parent.name == DetailContent)
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        private static Transform FindNode(Transform parent, string path)
        {
            if (parent == null) return null;
            // A requote disables its old controls before deferred Destroy runs.
            // Always bind the live replacement rather than its same-named predecessor.
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

        private static void Style(Text label, int standard)
        {
            GameReadability.Body(label, standard);
            if (label.name.EndsWith("Title") || label.name == "Departure Chart Route" || label.name == "Departure Special Crew Heading")
                label.font = GameTypography.InterfaceFont;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 1f;
        }

        private static void Label(Transform parent, string path, float width, float height, float x, float y, int standard, TextAnchor alignment)
        {
            RectTransform rect = Place(parent, path, width, height, x, y);
            if (rect == null || !rect.TryGetComponent(out Text label)) return;
            Style(label, standard); label.alignment = alignment;
        }

        private static void Caption(Transform parent, string path, int standard)
        {
            RectTransform button = FindNode(parent, path) as RectTransform;
            if (button == null) return;
            Label(button, "Label", button.rect.width - 20, button.rect.height - 10, 0, 0, standard, TextAnchor.MiddleCenter);
        }
    }
}
