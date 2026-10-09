using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Tiles the voyage surface below its header without stretching map coordinates or text.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class NavigationDashboardLayout : MonoBehaviour
    {
        private const string LayoutName = "Navigation Dashboard Layout";
        private bool applying;

        public static void Attach(RectTransform content)
        {
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            Transform existing = Child(content, LayoutName);
            NavigationDashboardLayout layout;
            if (existing == null)
            {
                var host = new GameObject(LayoutName, typeof(RectTransform));
                var rect = host.GetComponent<RectTransform>();
                rect.SetParent(content, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                layout = host.AddComponent<NavigationDashboardLayout>();
            }
            else layout = existing.GetComponent<NavigationDashboardLayout>();
            layout.Apply();
        }

        private void OnEnable() => Apply();
        private void OnRectTransformDimensionsChange() => Apply();

        public void Apply()
        {
            if (applying || !isActiveAndEnabled) return;
            var content = transform.parent as RectTransform;
            if (content == null || content.rect.width <= 0f || content.rect.height <= PhaseSurfaceLayout.HeaderHeight) return;
            RectTransform chart = Child(content, "Navigation Chart"), controls = Child(content, "Navigation Controls"),
                lookout = Child(content, "Forward View Panel"), journal = Child(content, "Navigation Journal");
            if (chart == null || controls == null || lookout == null || journal == null) return;
            RectTransform header = Child(content, "Voyage Header");
            float headerHeight = header == null ? PhaseSurfaceLayout.HeaderHeight : header.rect.height;
            if (content.rect.height <= headerHeight) return;
            applying = true;
            try
            {
                Rect bounds = content.rect;
                float height = bounds.height - headerHeight;
                float leftWidth = bounds.width * .51f;
                float splitX = bounds.xMin + leftWidth;
                float controlsHeight = 380f + (GameReadability.Scale - 1f) * 180f;
                // Reserve space for the fixed readouts AND several complete log
                // lines. A ScrollRect cannot compensate for a collapsed viewport.
                float journalHeight = 340f + (GameReadability.Scale - 1f) * 240f;
                float leftBottom = height * (controlsHeight / 972f), rightBottom = height * (journalHeight / 972f);
                Fit(chart, new Rect(bounds.xMin, bounds.yMin + leftBottom, leftWidth, height - leftBottom), new Vector2(860, 560));
                Fit(controls, new Rect(bounds.xMin, bounds.yMin, leftWidth, leftBottom), new Vector2(860, controlsHeight));
                Fit(lookout, new Rect(splitX, bounds.yMin + rightBottom, bounds.width - leftWidth, height - rightBottom), new Vector2(980, 650));
                Fit(journal, new Rect(splitX, bounds.yMin, bounds.width - leftWidth, rightBottom), new Vector2(980, journalHeight));
                LayoutChart(chart);
                LayoutControls(controls);
                LayoutLookout(lookout);
                LayoutJournal(journal);
                if (header != null)
                {
                    // Give the chosen font its real line height. The shared
                    // header grows with reading size and reserves lesson bands.
                    RectTransform title = Child(header, "Voyage Header Title");
                    RectTransform status = Child(header, "Voyage Header Status");
                    float titleHeight = Mathf.Ceil(GameReadability.FontSize(32) * 1.16f) + 4;
                    float statusHeight = Mathf.Ceil(GameReadability.FontSize(18) * 1.16f) + 4;
                    Box(title, new Vector2(650,titleHeight), Vector2.zero);
                    HeaderAnchor(title, 0f, 373f, (statusHeight + 4) * .5f);
                    Box(status, new Vector2(650,statusHeight), Vector2.zero);
                    HeaderAnchor(status, 0f, 373f, -(titleHeight + 4) * .5f);
                    Text statusLabel = status == null ? null : status.GetComponent<Text>();
                    if (statusLabel != null) statusLabel.alignment = TextAnchor.MiddleLeft;
                    RectTransform ribbon = Child(header, "Lookout Time Ribbon");
                    // The stretched header can still have its previous width
                    // during a canvas resize callback; content bounds are current.
                    float clockWidth = Mathf.Min(900, bounds.width - 1040);
                    float dateHeight = GameReadability.FontSize(20) + 8;
                    float legendHeight = GameReadability.FontSize(18) + 7;
                    float trackHeight = 12 * GameReadability.Scale;
                    // Two 2-unit gaps separate the track from the labels. This
                    // also leaves a real gap above the standard lesson footer.
                    float clockHeight = dateHeight + legendHeight + trackHeight + 4;
                    Box(ribbon, new Vector2(clockWidth,clockHeight), Vector2.zero);
                    HeaderAnchor(ribbon, .5f, 190f);
                    ReadingBox(ribbon, "Lookout Time Readout", 20, new Vector2(clockWidth - 20,dateHeight), new Vector2(0,(clockHeight-dateHeight)*.5f));
                    ReadingBox(ribbon, "Lookout Time Legend", 18, new Vector2(clockWidth - 20,legendHeight), new Vector2(0,-(clockHeight-legendHeight)*.5f));
                    RectTransform track = Child(ribbon, "Lookout Time Track");
                    if (track != null)
                    {
                        float trackScale = trackHeight / 116f;
                        track.localScale = Vector3.one * trackScale;
                        Box(track, new Vector2((clockWidth - 32) / trackScale,116), new Vector2(0,(legendHeight-dateHeight)*.5f));
                    }
                    Box(Child(header, "Voyage Cargo"), new Vector2(144,64), Vector2.zero);
                    HeaderAnchor(Child(header, "Voyage Cargo"), 1f, -246f);
                    Box(Child(header, "Voyage Settings"), new Vector2(128,64), Vector2.zero);
                    HeaderAnchor(Child(header, "Voyage Settings"), 1f, -94f);
                }
            }
            finally { applying = false; }
        }

        private static void Fit(RectTransform panel, Rect region, Vector2 design)
        {
            float scale = Mathf.Min(region.width / design.x, region.height / design.y);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
            panel.localScale = new Vector3(scale, scale, 1f);
            panel.sizeDelta = region.size / scale;
            panel.anchoredPosition = region.center;
            Box(Child(panel, panel.name + " Timber Frame"), panel.sizeDelta, Vector2.zero);
        }

        private static void LayoutChart(RectTransform panel)
        {
            Vector2 size = panel.sizeDelta;
            float titleHeight = 46 * GameReadability.Scale, seedHeight = 28 * GameReadability.Scale;
            float hintHeight = 42 * GameReadability.Scale;
            float top = size.y * .5f - 12;
            ReadingBox(panel, "Map Title", 32, new Vector2(size.x - 50f, titleHeight), new Vector2(0, top - titleHeight * .5f));
            ReadingBox(panel, "Map Seed", 18, new Vector2(size.x - 50f, seedHeight), new Vector2(0, top - titleHeight - seedHeight * .5f));
            ReadingBox(panel, "Map Course Hint", 18, new Vector2(size.x - 50f, hintHeight), new Vector2(0, -size.y * .5f + 10 + hintHeight * .5f));
            RectTransform frame = Child(panel, "Procedural Sea Chart Frame"), map = Child(frame, "Map Layer");
            if (map == null) return;
            float mapTop = top - titleHeight - seedHeight - 8;
            float mapBottom = -size.y * .5f + 18 + hintHeight;
            float scale = Mathf.Min((size.x - 32f) / 1200f, (mapTop - mapBottom) / 675f);
            map.localScale = new Vector3(scale, scale, 1f);
            Box(frame, new Vector2(1200, 675) * scale, new Vector2(0, (mapTop + mapBottom) * .5f));
        }

        private static void LayoutControls(RectTransform panel)
        {
            float scale = GameReadability.Scale, half = panel.sizeDelta.y * .5f;
            float dx = (panel.sizeDelta.x - 860f) * .5f;
            float titleHeight = 46 * scale;
            ReadingBox(panel, "Navigation Title", 32, new Vector2(310, titleHeight), new Vector2(-247-dx, half-16-titleHeight*.5f));
            string[] directions = { "NorthWest", "North", "NorthEast", "West", "East", "SouthWest", "South", "SouthEast" };
            float cell = 48 * scale, row = half - 28 - titleHeight - cell * .5f;
            Vector2[] positions = { new Vector2(-350,row), new Vector2(-247,row), new Vector2(-144,row), new Vector2(-350,row-cell-8),
                new Vector2(-144,row-cell-8), new Vector2(-350,row-2*(cell+8)), new Vector2(-247,row-2*(cell+8)), new Vector2(-144,row-2*(cell+8)) };
            for (int i = 0; i < directions.Length; i++)
                ReadingButton(panel, "Heading " + directions[i], 22, new Vector2(96,cell), positions[i]+new Vector2(-dx,0));
            ReadingBox(panel, "Compass", 20, new Vector2(94,cell), new Vector2(-247-dx,row-cell-8));
            float width = panel.sizeDelta.x - 422f;
            ReadingBox(panel, "Distance Label", 22, new Vector2(width-176,titleHeight), new Vector2(81,half-16-titleHeight*.5f));
            ReadingButton(panel, "Forecast Details", 22, new Vector2(164,titleHeight), new Vector2(169+width*.5f-82,half-16-titleHeight*.5f));
            RectTransform slider = Child(panel, "Distance Slider");
            Box(slider, new Vector2(width,38), new Vector2(169,half-titleHeight-43));
            Box(Child(slider, "Background"), new Vector2(width,8), Vector2.zero);
            Box(Child(slider, "Fill Area"), new Vector2(width-18,8), new Vector2(-9,0));
            Box(Child(slider, "Handle Slide Area"), new Vector2(width-24,34), Vector2.zero);
            float actionHeight = 56 * scale, actionY = -half + 16 + actionHeight * .5f;
            float forecastTop = half - titleHeight - 72, forecastBottom = actionY + actionHeight * .5f + 12;
            ReadingBox(panel, "Forecast", 22, new Vector2(width,forecastTop-forecastBottom), new Vector2(169,(forecastTop+forecastBottom)*.5f));
            ReadingButton(panel, "Execute Sail", 24, new Vector2(width,actionHeight), new Vector2(169,actionY));
            ReadingButton(panel, "Safe Return", 22, new Vector2(302,actionHeight), new Vector2(-247-dx,actionY));
            // Disclosure floats into the chart only while open; the chart's input geometry is unchanged.
            Transform disclosure = panel.Find("Forecast Details Panel");
            float disclosureHeight = 140 * scale;
            Box(disclosure as RectTransform, new Vector2(width,disclosureHeight), new Vector2(169,half+disclosureHeight*.5f));
            if (disclosure != null)
                ReadingBox(disclosure, "Forecast Detail Text", 22, new Vector2(width-32,disclosureHeight-24), Vector2.zero);
        }

        private static void LayoutLookout(RectTransform panel)
        {
            RectTransform viewport = Child(panel, "Forward Viewport");
            if (viewport == null) return;
            Vector2 size = panel.sizeDelta - new Vector2(24,24);
            Box(viewport, size, Vector2.zero);
            Box(Child(viewport, "Lookout Ocean"), size, Vector2.zero);
            Box(Child(viewport, "Lookout Ocean Transition"), size, Vector2.zero);
            FitPanorama(Child(viewport, "Lookout Ocean"), size);
            FitPanorama(Child(viewport, "Lookout Ocean Transition"), size);
            float headingHeight = 56 * GameReadability.Scale, reportHeight = 100 * GameReadability.Scale;
            Box(Child(viewport, "Lookout Heading Plaque"), new Vector2(size.x,headingHeight), new Vector2(0,size.y*.5f-headingHeight*.5f));
            ReadingBox(viewport, "Lookout Heading", 32, new Vector2(size.x-48,headingHeight-10), new Vector2(0,size.y*.5f-headingHeight*.5f));
            Box(Child(viewport, "Lookout Report Plaque"), new Vector2(size.x,reportHeight), new Vector2(0,-size.y*.5f+reportHeight*.5f));
            ReadingBox(viewport, "Lookout Report", 22, new Vector2(size.x-48,reportHeight-10), new Vector2(0,-size.y*.5f+reportHeight*.5f));
            RectTransform card = Child(viewport, "Encounter Card");
            if (card == null) return;
            Box(card, size, card.anchoredPosition);
            Box(Child(card, "Encounter Parchment"), size - new Vector2(16,12), Vector2.zero);
            Box(Child(card, "Encounter Themed Frame"), size, Vector2.zero);
            CoreGameplayDemoController.ReflowEmbeddedEncounter(card);
        }

        private static void FitPanorama(RectTransform rect, Vector2 size)
        {
            RawImage image = rect == null ? null : rect.GetComponent<RawImage>();
            if (image != null) image.uvRect = NavigationLookoutArtCatalog.PanoramaUv(size, image.texture);
        }

        private static void LayoutJournal(RectTransform panel)
        {
            Vector2 size = panel.sizeDelta;
            float scale = GameReadability.Scale, titleHeight = 46 * scale, cargoHeight = 32 * scale;
            float top = size.y * .5f - 16;
            ReadingBox(panel, "Log Title", 32, new Vector2(285,titleHeight), new Vector2(-size.x*.5f+175,top-titleHeight*.5f));
            ReadingBox(panel, "Voyage Cargo Summary", 18, new Vector2(size.x-355,titleHeight), new Vector2(145,top-titleHeight*.5f));
            float cargoTop = top-titleHeight-10;
            ReadingBox(panel, "Cargo Status", 18, new Vector2(size.x-66,cargoHeight), new Vector2(0,cargoTop-cargoHeight*.5f));
            Text cargo = Child(panel, "Cargo Status")?.GetComponent<Text>();
            if(cargo != null) cargo.alignment = TextAnchor.MiddleLeft;
            float logTop = cargoTop-cargoHeight-10, enduranceHeight = 64*scale;
            float logBottom = -size.y*.5f+14+enduranceHeight+10;
            RectTransform scroll = Child(panel, "Voyage Journal Scroll");
            Box(scroll, new Vector2(size.x-66,logTop-logBottom), new Vector2(0,(logTop+logBottom)*.5f));
            RectTransform log = Child(scroll, "Voyage Log");
            if (log != null)
            {
                log.sizeDelta = new Vector2(size.x-96,log.sizeDelta.y);
                Text label = log.GetComponent<Text>();
                GameReadability.Body(label, 22);
                log.sizeDelta = new Vector2(log.sizeDelta.x, Mathf.Max(scroll.rect.height-2,label.preferredHeight+18));
            }
            RectTransform endurance = Child(panel, "Voyage Supply Endurance");
            Box(endurance, new Vector2(size.x-66,enduranceHeight), new Vector2(0,-size.y*.5f+14+enduranceHeight*.5f));
            ReadingBox(endurance, "Water Food Endurance", 20, new Vector2(size.x-100,32*scale), new Vector2(0,16*scale));
            ReadingBox(endurance, "Supply Endurance Summary", 18, new Vector2(size.x-100,28*scale), new Vector2(0,-15*scale));
        }

        private static void ReadingBox(Transform parent, string name, int standard, Vector2 size, Vector2 position)
        {
            RectTransform rect = Child(parent, name);
            Box(rect,size,position);
            if(rect != null)
            {
                Text label = rect.GetComponent<Text>();
                GameReadability.Body(label,standard);
                if(label != null && (name.EndsWith("Title") || name == "Lookout Heading")) label.font = GameTypography.InterfaceFont;
            }
        }

        private static void ReadingButton(Transform parent, string name, int standard, Vector2 size, Vector2 position)
        {
            RectTransform rect = Child(parent,name);
            Box(rect,size,position);
            Text label = rect == null ? null : rect.GetComponentInChildren<Text>();
            if(label == null) return;
            label.rectTransform.sizeDelta = size-new Vector2(14,8);
            GameReadability.Body(label,standard);
        }

        private static RectTransform Child(Transform parent, string name)
        {
            if (parent == null) return null;
            // Render retires old children immediately but destroys them at the
            // end of the frame. Never bind those inactive, same-named objects.
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name && child.gameObject.activeSelf) return child as RectTransform;
            }
            return null;
        }
        private static void Box(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        }
        private static void HeaderAnchor(RectTransform rect, float x, float offset, float y = 0f)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(x,.5f);
            rect.anchoredPosition = new Vector2(offset,y);
        }
    }
}
