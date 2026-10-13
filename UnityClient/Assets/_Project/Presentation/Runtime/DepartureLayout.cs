using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared departure geometry for runtime views and authored previews.</summary>
    public static class DepartureLayout
    {
        public static readonly Vector2 RoutePanelSize = new Vector2(1150, 880);
        public static readonly Vector2 SetupPanelSize = new Vector2(660, 880);
        public static readonly Vector2 RoutePanelPosition = new Vector2(-350, -45);
        public static readonly Vector2 SetupPanelPosition = new Vector2(590, -45);
        public static readonly Vector2 ShipPreviewSize = new Vector2(440, 264);
        public static readonly Vector2 ShipPreviewPosition = new Vector2(0, 192);

        public static void Apply(RectTransform routePanel, RectTransform setupPanel)
        {
            if (routePanel != null)
            {
                SetRect(routePanel, RoutePanelSize, RoutePanelPosition);
                FitText(routePanel, "Departure Route Summary", 1060, 42, 0, 326, 24, 20, TextAnchor.MiddleLeft);
                // Both chart modes belong to the paper's header, independent of the backdrop.
                Place(routePanel, "Use Chart Seed", 500, 36, -280, 382);
                Place(routePanel, "Seed Locked Hint", 510, 36, 275, 382);
                Place(routePanel, "Seed Label", 500, 36, -280, 382);
                Place(routePanel, "Departure Treasure Seed Picker", 510, 36, 275, 382);
                FitButtonCaption(routePanel, "Departure Treasure Seed Picker", 19, 16);
                Place(routePanel, "Departure Chart Selection", 1060, 690, 0, -44);
                ApplyChart(routePanel);
                for (int index = 0; index < 6; index++)
                    Place(routePanel, "Departure Destination " + index, 160, 52, -440 + index * 176, 266);
                for (int index = 0; index < 3; index++)
                {
                    string path = "Route " + index;
                    Place(routePanel, path, 1060, 202, 0, 129 - index * 216);
                    FitText(routePanel, path + "/Label", 660, 174, 176, 0, 22, 19, TextAnchor.MiddleLeft);
                    RectTransform art = Place(routePanel, path + "/Route Preview " + index, 400, 180, -350, 0);
                    if (art != null && art.TryGetComponent(out RawImage image)) UiImageAspect.Fit(image, new Vector2(400, 180));
                }
                FitText(routePanel, "Treasure Departure Title", 1050, 48, 0, 200, 29, 24, TextAnchor.MiddleCenter);
                Place(routePanel, "Treasure Departure Art", 580, 326, -235, -8);
                FitText(routePanel, "Treasure Departure Detail", 430, 290, 315, -8, 21, 17, TextAnchor.MiddleLeft);
                FitText(routePanel, "Treasure Departure Consumption", 1030, 100, 0, -240, 20, 18, TextAnchor.MiddleCenter);
                Place(routePanel, "Cancel Treasure Voyage", 500, 58, -265, -350);
                Place(routePanel, "Return To Treasure Table", 500, 58, 265, -350);
                FitButtonCaption(routePanel, "Cancel Treasure Voyage", 20, 18);
                FitButtonCaption(routePanel, "Return To Treasure Table", 20, 18);
            }
            ApplySetup(setupPanel);
        }

        private static void ApplyChart(RectTransform panel)
        {
            const string chart = "Departure Chart Selection/";
            Place(panel, chart + "Seed Input", 1000, 48, 0, 236);
            Place(panel, chart + "Departure Chart Route", 1000, 32, 0, 184);
            RectTransform preview = FindNode(panel, chart + "Departure Chart Preview") as RectTransform;
            if (preview != null)
            {
                // Older authored previews used a larger chart. Preserve each
                // endpoint's normalized map position when matching runtime size.
                Vector2 previousSize = preview.sizeDelta;
                if (previousSize.x > 0 && previousSize.y > 0)
                    foreach (string endpoint in new[] { "Departure Chart Preview Origin", "Departure Chart Preview Destination" })
                    {
                        RectTransform marker = FindNode(preview, endpoint) as RectTransform;
                        if (marker != null) marker.anchoredPosition = Vector2.Scale(marker.anchoredPosition,
                            new Vector2(720 / previousSize.x, 405 / previousSize.y));
                    }
                Place(panel, chart + "Departure Chart Preview", 720, 405, 0, -58);
                Place(preview, "Departure Chart Preview Water", 720, 405, 0, 0);
                Place(preview, "Departure Chart Preview Surface", 720, 405, 0, 0);
            }
            Place(panel, chart + "Departure Chart Ports", 1000, 42, 0, -292);
            Place(panel, chart + "Departure Chart Preview Empty", 920, 160, 0, -58);
            Place(panel, chart + "Chart Seed Validation", 1000, 24, 0, -330);
        }

        public static void ApplySetup(RectTransform panel)
        {
            if (panel == null) return;
            SetRect(panel, SetupPanelSize, SetupPanelPosition);
            FitText(panel, "Setup Title", 552, 40, 0, 370, 28, 24, TextAnchor.MiddleCenter);
            if (panel.Find("Departure Crew Configuration Title") != null)
            {
                ApplyCrew(panel);
                return;
            }

            Place(panel, "Departure Previous Vessel", 48, 54, -258, ShipPreviewPosition.y);
            Place(panel, "Inspect Departure Vessel", ShipPreviewSize.x, ShipPreviewSize.y, ShipPreviewPosition.x, ShipPreviewPosition.y);
            ResizeShipPreview(panel);
            Place(panel, "Departure Next Vessel", 48, 54, 258, ShipPreviewPosition.y);
            Place(panel, "DepartureOrdinaryCrewDecreaseButton", 48, 40, -252, 18);
            Place(panel, "DepartureOrdinaryCrewIncreaseButton", 48, 40, 252, 18);
            FitText(panel, "DepartureOrdinaryCrewCount", 436, 56, 0, 18, 20, 18, TextAnchor.MiddleCenter);
            FitText(panel, "DepartureCrewSpeedPreview", 552, 32, 0, -35, 19, 17, TextAnchor.MiddleCenter);

            // The drawn bottom rule lies beyond the sprite's slice border.
            // Reserve space for that rule as well as the torn outer edge.
            Place(panel, "Departure Buy Provisions", 270, 62, -141, -132);
            Place(panel, "Adjust Voyage Supplies", 270, 62, 141, -132);
            Place(panel, "Configure Voyage Battle Decks", 270, 62, -141, -214);
            Place(panel, "Configure Voyage Crew", 270, 62, 141, -214);
            Place(panel, "Start Voyage", 552, 72, 0, -344);
            FitButtonCaption(panel, "Departure Buy Provisions", 18, 15);
            FitButtonCaption(panel, "Adjust Voyage Supplies", 18, 16);
            FitButtonCaption(panel, "Configure Voyage Battle Decks", 18, 16);
            FitButtonCaption(panel, "Configure Voyage Crew", 18, 16);
            FitButtonCaption(panel, "Start Voyage", 24, 20);
            FitButtonCaption(panel, "DepartureOrdinaryCrewDecreaseButton", 21, 18);
            FitButtonCaption(panel, "DepartureOrdinaryCrewIncreaseButton", 21, 18);
        }

        private static void ResizeShipPreview(RectTransform panel)
        {
            RectTransform ship = FindNode(panel, "Inspect Departure Vessel/Starter Modular Ship") as RectTransform;
            if (ship == null || ship.sizeDelta == ShipPreviewSize) return;
            Vector2 oldSize = ship.sizeDelta;
            if (oldSize.x <= 0 || oldSize.y <= 0) return;
            float scale = Mathf.Min(ShipPreviewSize.x / oldSize.x, ShipPreviewSize.y / oldSize.y);
            // Use the visible hull's fitted size, not the old frame's empty margins.
            // This matches BuildActiveShipPreview for both base and owned vessels.
            RawImage hull = ship.GetComponentInChildren<RawImage>(true);
            if (hull != null && hull.rectTransform.sizeDelta.x > 0 && hull.rectTransform.sizeDelta.y > 0)
            {
                Vector2 fitted = UiImageAspect.FitSize(hull.texture, hull.uvRect, ShipPreviewSize);
                scale = Mathf.Min(fitted.x / hull.rectTransform.sizeDelta.x, fitted.y / hull.rectTransform.sizeDelta.y);
            }
            foreach (RectTransform layer in ship.GetComponentsInChildren<RectTransform>(true))
            {
                if (layer == ship) continue;
                layer.sizeDelta *= scale;
                layer.anchoredPosition *= scale;
            }
            ship.sizeDelta = ShipPreviewSize;
        }

        private static void ApplyCrew(RectTransform panel)
        {
            FitText(panel, "Departure Crew Configuration Title", 552, 40, 0, 295, 23, 21, TextAnchor.MiddleCenter);
            FitText(panel, "Departure Ordinary Crew Label", 552, 34, 0, 238, 17, 15, TextAnchor.MiddleLeft);
            Place(panel, "Departure Ordinary Crew Slider", 528, 32, 0, 190);
            FitText(panel, "Departure Ordinary Crew Hint", 552, 50, 0, 140, 14, 12, TextAnchor.MiddleLeft);
            FitText(panel, "Departure Special Crew Heading", 552, 30, 0, 74, 16, 14, TextAnchor.MiddleLeft);
            for (int index = 0; index < 6; index++)
            {
                string path = "Departure Special Crew Toggle " + index;
                Place(panel, path, 174, 82, -186 + index % 3 * 186, -4 - index / 3 * 104);
                RectTransform art = Place(panel, path + "/Departure Special Crew Portrait " + index, 44, 56, -61, 0);
                if (art != null && art.TryGetComponent(out RawImage image)) UiImageAspect.Fit(image, new Vector2(44, 56));
                FitText(panel, path + "/Departure Special Crew Name " + index, 108, 32, 29, 16, 12, 11, TextAnchor.MiddleLeft);
                FitText(panel, path + "/Departure Special Crew State " + index, 108, 24, 29, -17, 12, 11, TextAnchor.MiddleLeft);
            }
            FitText(panel, "Departure Crew Total", 552, 44, 0, -211, 16, 14, TextAnchor.MiddleCenter);
            Place(panel, "Back To Voyage Setup", 270, 54, -141, -338);
            Place(panel, "Confirm Departure Crew", 270, 54, 141, -338);
            FitButtonCaption(panel, "Back To Voyage Setup", 17, 15);
            FitButtonCaption(panel, "Confirm Departure Crew", 17, 15);
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
            // Requotes disable their old controls before deferred Destroy runs.
            // Prefer the live replacement, but still lay out hidden chart sections.
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
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void FitText(Transform parent, string path, float width, float height, float x, float y,
            int maximum, int minimum, TextAnchor alignment)
        {
            RectTransform rect = Place(parent, path, width, height, x, y);
            if (rect == null || !rect.TryGetComponent(out Text label)) return;
            label.fontSize = label.resizeTextMaxSize = maximum;
            label.resizeTextMinSize = minimum;
            label.resizeTextForBestFit = true;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.lineSpacing = 1f;
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
