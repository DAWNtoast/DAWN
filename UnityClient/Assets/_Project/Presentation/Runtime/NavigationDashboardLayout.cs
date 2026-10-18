using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Tiles the voyage surface, filling the chart while preserving logical coordinates and marker proportions.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class NavigationDashboardLayout : MonoBehaviour
    {
        private const string LayoutName = "Navigation Dashboard Layout";
        private bool applying;
        private float appliedCanvasScale = -1f;

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

        private void LateUpdate()
        {
            // The fixed 1920x1080 game surface keeps the same logical size when
            // the window changes. CanvasScaler can still change glyph metrics
            // and wrapping without sending a RectTransform size notification.
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && !Mathf.Approximately(appliedCanvasScale, canvas.rootCanvas.scaleFactor)) Apply();
        }

        public void Apply()
        {
            if (applying || !isActiveAndEnabled) return;
            var content = transform.parent as RectTransform;
            if (content == null || content.rect.width <= 0f || content.rect.height <= PhaseSurfaceLayout.HeaderHeight) return;
            // Old saved previews also use the fixed game surface when opened.
            Canvas canvas = content.GetComponentInParent<Canvas>();
            if (canvas != null) FixedGameViewport.Create(canvas.rootCanvas);
            RectTransform chart = Child(content, "Navigation Chart"), controls = Child(content, "Navigation Controls"),
                lookout = Child(content, "Forward View Panel");
            if (chart == null || controls == null || lookout == null) return;
            RectTransform header = Child(content, "Voyage Header");
            applying = true;
            try
            {
                if (header != null) NavigationHeaderSkin.ApplyLayout(header);
                float headerHeight = header == null ? PhaseSurfaceLayout.HeaderHeight : header.rect.height;
                if (content.rect.height <= headerHeight) return;
                Rect bounds = content.rect;
                float height = bounds.height - headerHeight;
                bool encounter = Child(Child(lookout, "Forward Viewport"), "Encounter Card") != null;
                // Events preserve the established chart bounds; only the right
                // column changes from sailing controls/lookout into a journal.
                float leftWidth = bounds.width * .6625f;
                float splitX = bounds.xMin + leftWidth;
                float rightBottom = height * .48f;
                Fit(chart, new Rect(bounds.xMin, bounds.yMin, leftWidth, height), new Vector2(1100, 800));
                Fit(controls, new Rect(splitX, bounds.yMin, bounds.width - leftWidth, rightBottom), new Vector2(660, 470));
                Fit(lookout, new Rect(splitX, encounter ? bounds.yMin : bounds.yMin + rightBottom,
                    bounds.width - leftWidth, encounter ? height : height - rightBottom),
                    encounter ? new Vector2(660, 960) : new Vector2(980, 650));
                CanvasGroup controlsVisibility = controls.GetComponent<CanvasGroup>();
                if (controlsVisibility == null) controlsVisibility = controls.gameObject.AddComponent<CanvasGroup>();
                // Keep the current controls discoverable for future resize and
                // restore passes. Disabling their object would hide it from Child.
                controlsVisibility.alpha = encounter ? 0f : 1f;
                controlsVisibility.interactable = !encounter;
                controlsVisibility.blocksRaycasts = !encounter;
                NavigationInterfaceSkin.ApplyDashboard(content);
                LayoutChart(chart);
                LayoutControls(controls);
                LayoutLookout(lookout);
                appliedCanvasScale = canvas == null ? 1f : canvas.rootCanvas.scaleFactor;
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
            // Extend under the painted gold rim so no dark gutter remains.
            const float padding = 4f, stripHeight = 58f;
            float headingY = size.y * .5f - padding - stripHeight * .5f;
            Box(Child(panel, "Navigation Map Title Strip"), new Vector2(size.x - padding * 2f, stripHeight), new Vector2(0, headingY));
            Box(Child(panel, "Navigation Map Title Compass"), new Vector2(34f, 34f), new Vector2(-size.x * .5f + 36f, headingY));
            float titleWidth = Mathf.Clamp((size.x - 96f) * .42f, 330f, 560f);
            ChartTextBox(Child(panel, "Map Title"), new Vector2(titleWidth, 34f),
                new Vector2(-size.x * .5f + 64f + titleWidth * .5f, headingY), 23, panel.localScale.x);
            float enduranceWidth = size.x - 104f - titleWidth;
            RectTransform endurance = Child(panel, "Voyage Supply Endurance");
            Box(endurance, new Vector2(enduranceWidth, 34f), new Vector2(size.x * .5f - 24f - enduranceWidth * .5f, headingY));
            ChartTextBox(Child(endurance, "Water Food Endurance"), new Vector2(enduranceWidth, 30f),
                Vector2.zero, 17, panel.localScale.x);
            float explorationHeight = LayoutExplorationStatus(panel, headingY - stripHeight * .5f);
            RectTransform hint = Child(panel, "Map Course Hint");
            ChartTextBox(hint, new Vector2(size.x - 48f, 32f),
                new Vector2(0, -size.y * .5f + 28f), 14, panel.localScale.x);
            RectTransform bossGuidance = Child(panel, "Destination Boss Guidance");
            if (bossGuidance != null)
            {
                float guidanceWidth = size.x - 24f;
                Box(bossGuidance, new Vector2(guidanceWidth, 64f), new Vector2(0f, -size.y * .5f + 38f));
                ChartTextBox(Child(bossGuidance, "Destination Boss Guidance Text"), new Vector2(guidanceWidth - 336f, 52f),
                    new Vector2(-152f, 0f), 18, panel.localScale.x);
                ControlBox(Child(bossGuidance, "Locate Blocking Boss"), new Vector2(136f, 44f),
                    new Vector2(guidanceWidth * .5f - 226f, 0f));
                ControlBox(Child(bossGuidance, "Plot Blocking Boss Course"), new Vector2(136f, 44f),
                    new Vector2(guidanceWidth * .5f - 76f, 0f));
            }
            RectTransform frame = Child(panel, "Procedural Sea Chart Frame"), map = Child(frame, "Map Layer");
            if (map == null) return;
            // Match the old procedural backing to the surrounding fog surface,
            // so its inset does not draw a second dark rectangle around the map.
            Image mapBacking = Child(map, "Procedural Sea Chart")?.GetComponent<Image>();
            Image panelBacking = panel.GetComponent<Image>();
            if (mapBacking != null && panelBacking != null)
            {
                mapBacking.sprite = mapBacking.overrideSprite = null;
                mapBacking.type = Image.Type.Simple;
                mapBacking.color = panelBacking.color;
                mapBacking.raycastTarget = false;
            }
            // Fit the plotted sea, not the larger logical root's empty margins.
            // Keep the established coordinates for routes, markers and input.
            float mapTop = headingY - stripHeight * .5f - explorationHeight;
            float mapBottom = -size.y * .5f + padding + (bossGuidance != null ? 72f : hint == null ? 0f : 40f);
            Vector2 mapSize = new Vector2(size.x - padding * 2f, mapTop - mapBottom);
            RectTransform sea = Child(map, "Ocean Tile Layer");
            if (sea == null) return;
            map.sizeDelta = sea.rect.size + new Vector2(40f, 45f);
            Box(frame, mapSize, new Vector2(0, (mapTop + mapBottom) * .5f));
            // The logical input/backing and edge sprites may extend beyond the
            // sea. Clip them at the frame, including their raycast area.
            if (frame.GetComponent<RectMask2D>() == null) frame.gameObject.AddComponent<RectMask2D>();
            NavigationMapViewport viewport = map.GetComponent<NavigationMapViewport>() ?? map.gameObject.AddComponent<NavigationMapViewport>();
            if (viewport.Columns == 0)
            {
                // Persisted previews predating the viewport retain their full
                // fine fog data, including transparent explored cells.
                VoyageMapTileLayerGraphic fog = Child(Child(map, "Visibility Mask Layer"), "Chart Fog Tiles")?.GetComponent<VoyageMapTileLayerGraphic>();
                viewport.Initialize(48, fog == null ? 32 : fog.TileCount / (48 * 4));
            }
            viewport.ApplyLayout(frame, sea);
            ApplyMapMarkerScale(map);
        }

        private static float LayoutExplorationStatus(RectTransform chart, float top)
        {
            RectTransform panel = Child(chart, "Voyage Exploration Status");
            RectTransform message = Child(panel, "Voyage Exploration Status Text");
            if (panel == null || message == null) return 0f;
            float width = chart.sizeDelta.x - 8f;
            Text text = message.GetComponent<Text>();
            ChartTextBox(message, new Vector2(width - 28f, 42f), Vector2.zero, 18, chart.localScale.x);
            // Reserve the complete wrapped summary above the chart. Clues and
            // remaining buff durations stay visible after closing an event.
            float height = Mathf.Max(42f, Mathf.Ceil(text.preferredHeight * message.localScale.y) + 16f);
            Box(panel, new Vector2(width, height), new Vector2(0f, top - height * .5f));
            ChartTextBox(message, new Vector2(width - 28f, height - 16f), Vector2.zero, 18, chart.localScale.x);
            return height;
        }

        public static void ApplyMapMarkerScale(RectTransform map)
        {
            if (map == null || map.localScale.x <= 0f || map.localScale.y <= 0f) return;
            float uniformScale = Mathf.Min(map.localScale.x, map.localScale.y);
            Vector3 correction = new Vector3(uniformScale / map.localScale.x, uniformScale / map.localScale.y, 1f);
            // Marker positions follow the chart projection; artwork and hit areas
            // keep their authored proportions. Lines and terrain follow the map.
            foreach (Transform child in map)
            {
                if (!child.gameObject.activeSelf) continue;
                if (child.name == "Player Ship" || child.name.StartsWith("Event Node ") ||
                    child.name == "Departure Port" || child.name == "Departure Port Halo" ||
                    child.name == "Destination Port" || child.name == "Destination Port Halo")
                    SetMarkerScale(child as RectTransform, correction);
            }
            RectTransform course = Child(map, "Course Preview Layer");
            SetMarkerScale(Child(course, "Course Target"), correction);
            SetMarkerScale(Child(course, "Direct Course Target Halo"), correction);
        }

        private static void SetMarkerScale(RectTransform marker, Vector3 scale)
        {
            if (marker == null) return;
            marker.localScale = scale;
            // Animation must use the same corrected baseline after a resize.
            marker.GetComponent<NauticalButtonMotion>()?.SetBaseScale(scale);
            marker.GetComponent<VoyageEventMarkerPulse>()?.SetBaseScale(scale);
        }

        private static void LayoutControls(RectTransform panel)
        {
            Vector2 size = panel.sizeDelta;
            const float compassWidth = 280f, gap = 22f, padding = 28f;
            float usableWidth = size.x - padding * 2f;
            float compassCenter = -size.x * .5f + padding + compassWidth * .5f;
            float forecastWidth = usableWidth - compassWidth - gap;
            float forecastCenter = size.x * .5f - padding - forecastWidth * .5f;
            float top = size.y * .5f - 82f;
            float footerY = -size.y * .5f + 49f;
            float bodyBottom = footerY + 47f;
            float compassDiameter = Mathf.Min(compassWidth, top - bodyBottom);
            Box(Child(panel, "Navigation Title"), new Vector2(usableWidth-140f,32f),
                new Vector2(-70f,size.y*.5f-39f));
            NavigationCompassControl.Attach(panel);
            NavigationCompassControl.Layout(panel, new Vector2(compassCenter, top - compassDiameter * .5f), compassDiameter);
            Box(Child(panel, "Distance Label"), new Vector2(forecastWidth,48), new Vector2(forecastCenter,top-38f));
            ControlBox(Child(panel, "Forecast Details"), new Vector2(128,38), new Vector2(size.x*.5f-padding-64f,size.y*.5f-39f));
            RectTransform slider = Child(panel, "Distance Slider");
            Box(slider, new Vector2(forecastWidth,34), new Vector2(forecastCenter,top-91f));
            Box(Child(slider, "Background"), new Vector2(forecastWidth,12), Vector2.zero);
            Box(Child(slider, "Fill Area"), new Vector2(forecastWidth-18,10), new Vector2(-9,0));
            Box(Child(slider, "Handle Slide Area"), new Vector2(forecastWidth-24,34), Vector2.zero);
            Box(Child(panel, "Distance Limit"), new Vector2(forecastWidth,18), new Vector2(forecastCenter,top-119f));
            float forecastTop = top - 130f;
            float forecastHeight = Mathf.Max(132f, forecastTop - bodyBottom);
            Box(Child(panel, "Forecast"), new Vector2(forecastWidth,forecastHeight), new Vector2(forecastCenter,forecastTop-forecastHeight*.5f));
            float actionWidth = (usableWidth-gap)*.5f;
            ControlBox(Child(panel, "Execute Sail"), new Vector2(actionWidth,66), new Vector2((actionWidth+gap)*.5f,footerY));
            ControlBox(Child(panel, "Safe Return"), new Vector2(actionWidth,66), new Vector2(-(actionWidth+gap)*.5f,footerY));
            // Expanded details float above the controls into the right-hand lookout.
            Transform disclosure = panel.Find("Forecast Details Panel");
            Box(disclosure as RectTransform, new Vector2(usableWidth,150), new Vector2(0,size.y*.5f+75));
            if (disclosure != null)
                Box(disclosure.Find("Forecast Detail Text") as RectTransform, new Vector2(usableWidth-32,124), Vector2.zero);
        }

        private static void LayoutLookout(RectTransform panel)
        {
            RectTransform viewport = Child(panel, "Forward Viewport");
            if (viewport == null) return;
            RectTransform card = Child(viewport, "Encounter Card");
            // The panorama covers the entire region; only event paper needs an inset.
            Vector2 size = card == null ? panel.sizeDelta : panel.sizeDelta - new Vector2(24,24);
            Box(viewport, size, Vector2.zero);
            Box(Child(viewport, "Lookout Ocean"), size, Vector2.zero);
            Box(Child(viewport, "Lookout Ocean Transition"), size, Vector2.zero);
            FitPanorama(Child(viewport, "Lookout Ocean"), size);
            FitPanorama(Child(viewport, "Lookout Ocean Transition"), size);
            Box(Child(viewport, "Lookout Heading Plaque"), new Vector2(size.x,60), new Vector2(0,size.y*.5f-30));
            Box(Child(viewport, "Lookout Heading"), new Vector2(size.x-48,40), new Vector2(0,size.y*.5f-30));
            Box(Child(viewport, "Lookout Report Plaque"), new Vector2(size.x,92), new Vector2(0,-size.y*.5f+46));
            Box(Child(viewport, "Lookout Report"), new Vector2(size.x-48,80), new Vector2(0,-size.y*.5f+46));
            if (card == null) return;
            Box(card, size, card.anchoredPosition);
            LayoutEmbeddedEncounter(card);
        }

        public static void LayoutEmbeddedEncounter(RectTransform card)
        {
            EncounterJournalLayout.Apply(card);
        }

        private static void FitPanorama(RectTransform rect, Vector2 size)
        {
            RawImage image = rect == null ? null : rect.GetComponent<RawImage>();
            if (image != null) image.uvRect = NavigationLookoutArtCatalog.PanoramaUv(size, image.texture);
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

        private static void ChartTextBox(RectTransform rect, Vector2 size, Vector2 position,
            int authoredFontSize, float chartScale)
        {
            if (rect == null) return;
            Text label = rect.GetComponent<Text>();
            if (label == null) { Box(rect, size, position); return; }
            // The map panel scales as one surface, but legacy Text rasterizes
            // only for the Canvas scale. Request enough native glyph pixels and
            // counter-scale this label so its displayed font size and box stay
            // unchanged. Always derive from authored values, never the last pass.
            int rasterFontSize = Mathf.CeilToInt(authoredFontSize * Mathf.Max(1f, chartScale));
            float labelScale = authoredFontSize / (float)rasterFontSize;
            label.fontSize = rasterFontSize;
            Vector3 scale = new Vector3(labelScale, labelScale, 1f);
            if (rect.localScale != scale) rect.localScale = scale;
            Box(rect, size / labelScale, position);
        }
        private static void ControlBox(RectTransform rect, Vector2 size, Vector2 position)
        {
            Box(rect, size, position);
            Box(Child(rect, "Label"), size - new Vector2(24,8), Vector2.zero);
        }
    }
}
