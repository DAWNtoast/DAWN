using System;
using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform lookoutViewport;
        private CoreVoyageMap navigationViewedMap;
        private Vector2? navigationMapCenter;

        private void ConfigureNavigationMapViewport(RectTransform map)
        {
            if (navigationViewedMap != session.Map)
            {
                navigationViewedMap = session.Map;
                navigationMapCenter = null;
            }
            NavigationMapViewport viewport = map.gameObject.AddComponent<NavigationMapViewport>();
            viewport.Initialize(session.Map.Columns, session.Map.Rows, navigationMapCenter,
                center => navigationMapCenter = center);
        }

        private void InitializeNavigationControlColors()
        {
            if (!Application.isPlaying || contentRoot == null) return;
            // These controls have just been built. Their role, skin and enabled
            // state can start multiple ColorTint tweens during construction;
            // finish those before the first frame, while keeping later pointer
            // transitions at their normal duration.
            GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            foreach (Selectable control in contentRoot.GetComponentsInChildren<Selectable>())
            {
                if (control.transition != Selectable.Transition.ColorTint || control.targetGraphic == null) continue;
                ColorBlock colors = control.colors;
                Color tint = !control.IsInteractable() ? colors.disabledColor :
                    selected == control.gameObject ? colors.selectedColor : colors.normalColor;
                control.targetGraphic.CrossFadeColor(tint * colors.colorMultiplier, 0f, true, true);
            }
        }

        private RectTransform NavigationPanel(string name, Vector2 size, Vector2 position)
        {
            RectTransform panel = CreateFixedPanel(name, contentRoot, size, position, Html("#242A29"));
            // The responsive layout joins the three outer rectangles after their
            // contents are built; it keeps each panel's content scale uniform.
            panel.GetComponent<Image>().raycastTarget = false;
            Image frame = CreateImage(name + " Timber Frame", panel, size, Vector2.zero, Color.white, WoodButtonSkin.Face);
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = 20f;
            frame.color = new Color(.82f, .77f, .67f, 1f);
            frame.raycastTarget = false;
            return panel;
        }

        private void BuildNavigationDashboard()
        {
            if (Application.isPlaying && !editorPreviewBuilding)
                RefreshComponentEnvironmentEffect();
            RectTransform mapPanel = NavigationPanel("Navigation Chart", new Vector2(860f, 560f), new Vector2(-500f, 195f));
            NavigationText("Map Title", mapPanel, new Vector2(810f, 30f), new Vector2(0f, 251f), 23, Gold).text =
                session.IsTreasureVoyage ? session.ActiveTreasureMap.Name + " · 隐藏海域" :
                session.Map.DepartureName + " → " + session.Map.DestinationName;
            BuildNavigationSupplyEndurance(mapPanel);
            RectTransform chart = CreateRect("Procedural Sea Chart Frame", mapPanel, new Vector2(828f, 466f), new Vector2(0f, -21f));
            // Keep the chart's established logical coordinates and click conversion intact.
            Vector2 mapSize = new Vector2(ChartGridWidth + 40f, ChartGridHeight + 45f);
            RectTransform mapRoot = CreateRect("Map Layer", chart, mapSize, Vector2.zero);
            mapRoot.localScale = Vector3.one * 0.69f;
            CreateImage("Procedural Sea Chart", mapRoot, mapSize, Vector2.zero,
                ProceduralWaterColor(session.SelectedRouteIndex), seaSprite).raycastTarget = false;
            BuildMap(mapRoot);
            if (session.IsTreasureVoyage)
                NavigationText("Map Course Hint", mapPanel, new Vector2(810f, 22f), new Vector2(0f, -264f), 14, Gold).text =
                    TreasureNavigationReadout();

            RectTransform controls = NavigationPanel("Navigation Controls", new Vector2(860f, 360f), new Vector2(-500f, -285f));
            BuildNavigationControls(controls);
            RectTransform view = NavigationPanel("Forward View Panel", new Vector2(980f, 650f), new Vector2(450f, 150f));
            lookoutViewport = CreateRect("Forward Viewport", view, view.sizeDelta, Vector2.zero);
            lookoutViewport.gameObject.AddComponent<RectMask2D>();
            BuildForwardLookout();
            NavigationDashboardLayout.Attach(contentRoot);
        }

        private void BuildDashboardControls(RectTransform panel)
        {
            NavigationText("Navigation Title", panel, new Vector2(290f, 32f), new Vector2(-245f, 148f), 24, Gold).text = "航行指令";
            SailingDirection[] directions = { SailingDirection.NorthWest, SailingDirection.North, SailingDirection.NorthEast,
                SailingDirection.West, SailingDirection.East, SailingDirection.SouthWest, SailingDirection.South, SailingDirection.SouthEast };
            Vector2[] points = { new Vector2(-350, 88), new Vector2(-247, 88), new Vector2(-144, 88), new Vector2(-350, 25),
                new Vector2(-144, 25), new Vector2(-350, -38), new Vector2(-247, -38), new Vector2(-144, -38) };
            for (int i = 0; i < directions.Length; i++)
            {
                SailingDirection heading = directions[i];
                Button button = CreateButton("Heading " + heading, panel, new Vector2(96f, 56f), points[i], PanelRaised, Ink, 20);
                button.GetComponentInChildren<Text>().text = session.DirectionLabel(heading);
                WoodButtonSkin.SetSelected(button, heading == session.Heading);
                button.interactable = session.Phase == CoreDemoPhase.Sailing && !navigationMotionPlaying;
                button.onClick.AddListener(() =>
                {
                    if (!CanApplyGuidedNavigationInput(button, GuidedTutorialNavigationInputStep.Heading)) return;
                    if (tutorialActive && heading != SailingDirection.East) return;
                    session.SetHeading(heading);
                    if (tutorialActive) guidedTutorialNavigationInputStep = GuidedTutorialNavigationInputStep.Sail;
                    Render();
                });
            }
            NavigationText("Compass", panel, new Vector2(94f, 48f), new Vector2(-247f, 25f), 19, Gold, TextAnchor.MiddleCenter).text = "航向";
            Text distanceLabel = NavigationText("Distance Label", panel, new Vector2(438f, 32f), new Vector2(169f, 144f), 21, Ink);
            Slider distance = CreateSlider("Distance Slider", panel, new Vector2(438f, 34f), new Vector2(169f, 97f),
                CoreVoyageSession.MinimumDistanceNm, CoreVoyageSession.MaximumDistanceNm, session.PlannedDistanceNm);
            distance.wholeNumbers = true;
            distance.interactable = !tutorialActive && session.Phase == CoreDemoPhase.Sailing && !navigationMotionPlaying;
            Text forecastText = NavigationText("Forecast", panel, new Vector2(438f, 132f), new Vector2(169f, -3f), 20, Ink, TextAnchor.UpperLeft);
            Button detailsButton = CreateButton("Forecast Details", panel, new Vector2(116,32), new Vector2(330,120), PanelRaised, Ink, 16);
            Text detailsCaption = detailsButton.GetComponentInChildren<Text>();
            detailsCaption.text = "风流详情";
            detailsButton.interactable = !tutorialActive && !navigationMotionPlaying;
            RectTransform detailsPanel = CreateFixedPanel("Forecast Details Panel", panel, new Vector2(438,124), new Vector2(169,242), Html("#DFC591"));
            Text detailsText = NavigationText("Forecast Detail Text", detailsPanel, new Vector2(406,98), Vector2.zero, 18, Ink, TextAnchor.MiddleLeft);
            detailsText.lineSpacing = 1.3f;
            detailsPanel.gameObject.SetActive(false);
            detailsButton.onClick.AddListener(() =>
            {
                if (!detailsButton.isActiveAndEnabled || !detailsButton.IsInteractable()) return;
                bool open = !detailsPanel.gameObject.activeSelf;
                detailsPanel.gameObject.SetActive(open);
                if (open) panel.SetAsLastSibling();
                detailsPanel.SetAsLastSibling();
                detailsCaption.text = open ? "收起详情" : "风流详情";
                WoodButtonSkin.SetSelected(detailsButton, open);
            });
            Action updateForecast = () =>
            {
                SailingForecast forecast = session.GetForecast();
                distanceLabel.text = session.HasDirectCourseTarget && forecast != null ? "精确航点　" + forecast.ActualDistanceNm.ToString("0") + " 海里" : "计划航程　" + session.PlannedDistanceNm + " 海里";
                forecastText.text = forecast == null ? "无法计算航程" :
                    "预计 " + forecast.Hours.ToString("0.0") + " 小时\n" +
                    "航速 " + forecast.EffectiveSpeedKnots.ToString("0.0") + " 节" + (forecast.Rerouted ? " · 已绕岛" : "") + "\n" +
                    "淡水 −" + forecast.WaterCost + "　食物 −" + forecast.FoodCost +
                    (forecast.Blocked ? "\n当前航点无可通行航线" : "") +
                    (forecast.Clipped ? "\n边界航迹可能擦碰暗礁" : "");
                detailsText.text = forecast == null ? "暂无风流资料" :
                    "洋流 " + session.DirectionLabel(forecast.CurrentDirection) + " " + forecast.CurrentStrength.ToString("0.0") + " 节　·　" +
                    forecast.CurrentRelation + " " + SignedSpeed(forecast.CurrentSpeedModifier) + "\n" +
                    "海风 " + session.DirectionLabel(forecast.WindDirection) + " " + forecast.WindStrength.ToString("0.0") + " 节　·　" +
                    forecast.WindRelation + " " + SignedSpeed(forecast.WindSpeedModifier);
            };
            distance.onValueChanged.AddListener(value =>
            {
                if (!CanApplyGuidedNavigationInput(distance, GuidedTutorialNavigationInputStep.Distance)) return;
                int requested = Mathf.RoundToInt(value / 10f) * 10;
                // Slider also sends driven-value notifications while Canvas rebuilds.
                // They are not user course changes and must never destroy UI mid-layout.
                if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics() ||
                    (requested == session.PlannedDistanceNm && !session.HasDirectCourseTarget)) return;
                session.SetDistance(requested);
                updateForecast();
                RefreshDashboardCourse();
            });
            updateForecast();
            Button sail = CreateButton("Execute Sail", panel, new Vector2(438f, 62f), new Vector2(169f, -129f), Gold, Ink, 24);
            sail.GetComponentInChildren<Text>().text = session.Phase == CoreDemoPhase.Encounter ? "处理右上方事件后继续" : "执行航行";
            sail.interactable = session.Phase == CoreDemoPhase.Sailing && !navigationMotionPlaying;
            sail.onClick.AddListener(() =>
            {
                if (!CanApplyGuidedNavigationInput(sail, GuidedTutorialNavigationInputStep.Sail)) return;
                BeginNavigationMotion(sail);
            });
            Button back = CreateButton("Safe Return", panel, new Vector2(302f, 62f), new Vector2(-247f, -129f), PanelRaised, Ink, 18);
            back.GetComponentInChildren<Text>().text = session.Cargo.Water < 2 || session.Cargo.Food < 1
                ? "缺粮拖带救援\n保护 35% 战利品" : "主动返航\n保护 85% 战利品";
            back.interactable = session.Phase == CoreDemoPhase.Sailing && !navigationMotionPlaying;
            back.onClick.AddListener(() =>
            {
                if (navigationMotionPlaying || back == null || !back.isActiveAndEnabled ||
                    !back.IsInteractable() || session == null || session.Phase != CoreDemoPhase.Sailing) return;
                session.SafeReturn();
                Render();
            });
        }

        private void RefreshDashboardCourse()
        {
            // Distance changes only the planned course. Preserve the map, its
            // input surface and the lookout; their world state has not changed.
            Transform map = contentRoot.Find("Navigation Chart/Procedural Sea Chart Frame/Map Layer");
            Transform course = map == null ? null : map.Find("Course Preview Layer");
            if (course != null)
            {
                ClearChildren(course);
                BuildMapCoursePreview((RectTransform)course);
                NavigationDashboardLayout.ApplyMapMarkerScale(map as RectTransform);
            }
        }

        private static void RetireNavigationChild(Transform child)
        {
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }

        private void BuildForwardLookout()
        {
            SeaTerrainType localTerrain = session.Map.TerrainAt(session.Position);
            NavigationLookoutBlend blend = NavigationLookoutArtCatalog.BlendForVoyageHours(session.ElapsedGameHours);
            Texture2D water = NavigationLookoutArtCatalog.ForEnvironment(localTerrain, blend.From);
            Texture2D nextWater = blend.From == blend.To ? water : NavigationLookoutArtCatalog.ForEnvironment(localTerrain, blend.To);
            // Crop the panorama to fill, keeping painted objects proportional and
            // its horizon aligned with the separately projected scenery.
            Vector2 panoramaSize = lookoutViewport.rect.size;
            RawImage ocean = CreateRawImage("Lookout Ocean", lookoutViewport, panoramaSize, Vector2.zero, water,
                NavigationLookoutArtCatalog.PanoramaUv(panoramaSize, water));
            ocean.color = Color.white;
            ocean.raycastTarget = false;
            RawImage transition = CreateRawImage("Lookout Ocean Transition", lookoutViewport, panoramaSize, Vector2.zero, nextWater,
                NavigationLookoutArtCatalog.PanoramaUv(panoramaSize, nextWater));
            transition.color = new Color(1f, 1f, 1f, blend.Amount);
            transition.raycastTarget = false;
            Color environmentTint = NavigationLookoutArtCatalog.EnvironmentTint(localTerrain, blend);
            var observations = VoyageLookoutProjector.Project(session.Map, session.Position, session.Heading,
                120f + (session.HasNavigator ? 60f : 0f) + session.VisionBonusNm);
            foreach (LookoutObservation item in observations)
            {
                // Local fog/rain is painted into the panorama; keep distant
                // projected weather and the report without drawing it twice.
                if (NavigationLookoutArtCatalog.ShouldSkipLocalWeather(item, localTerrain)) continue;
                string key = item.Kind == LookoutSceneryKind.Island ? "island" : item.Kind == LookoutSceneryKind.Reef ? "reef" :
                    item.Kind == LookoutSceneryKind.Storm ? "storm" : item.Kind == LookoutSceneryKind.Fog ? "fog" : "pirate";
                bool vessel = item.Kind == LookoutSceneryKind.Vessel;
                Texture2D texture = vessel ? portArt.ShipSheet : Resources.Load<Texture2D>("NavigationLookout/lookout_" + key + "_v1");
                Rect uv = vessel ? portArt.ShipUv(1) : new Rect(0, 0, 1, 1);
                if (!vessel && key != "pirate" && (item.Variant & 1) != 0) uv = new Rect(1, 0, -1, 1);
                bool weather = item.Kind == LookoutSceneryKind.Storm || item.Kind == LookoutSceneryKind.Fog;
                float width = (weather ? 920f : vessel || key == "pirate" ? 365f : 730f) * item.Scale;
                float aspect = texture == null ? 1.5f : texture.width * Mathf.Abs(uv.width) / (texture.height * uv.height);
                float height = width / aspect;
                float waterline = 64f - item.Depth * item.Depth * 192f;
                float contactUv = key == "pirate" || vessel ? 0.055f : key == "fog" ? 0.30f : 0.18f;
                Vector2 position = new Vector2(item.Horizontal * 440f, waterline + height * (0.5f - contactUv));
                RawImage scenery = CreateRawImage("Lookout " + item.Key, lookoutViewport, new Vector2(width, height), position, texture, uv);
                if (vessel)
                {
                    // Redrawn atlas cells have their own authored keel contact.
                    // Use it rather than the old standalone pirate's padding.
                    position.y = waterline - ShipWaterlineLayout.HullContactLocal(scenery).y;
                    scenery.rectTransform.anchoredPosition = position;
                    BattleShipCutout.Attach(scenery);
                }
                // Distant scenery loses contrast in the blue atmospheric haze, not a black matte.
                scenery.color = Color.Lerp(new Color(0.66f, 0.78f, 0.83f, 1f), Color.white, item.Proximity) * environmentTint;
                scenery.color = new Color(scenery.color.r, scenery.color.g, scenery.color.b, scenery.color.a * item.Visibility);
                scenery.raycastTarget = false;
            }
            Image top = CreateFixedPanel("Lookout Heading Plaque", lookoutViewport, new Vector2(956f, 68f), new Vector2(0f, 279f), new Color(0.035f, 0.075f, 0.085f, 0.94f)).GetComponent<Image>();
            top.raycastTarget = false;
            NavigationText("Lookout Heading", lookoutViewport, new Vector2(900f, 40f), new Vector2(0f, 281f), 28, Ink).text =
                "前方视野 · " + session.DirectionLabel(session.Heading) + "向";
            Image strip = CreateFixedPanel("Lookout Report Plaque", lookoutViewport, new Vector2(956f, 98f), new Vector2(0f, -264f), new Color(0.035f, 0.075f, 0.085f, 0.94f)).GetComponent<Image>();
            strip.raycastTarget = false;
            StringBuilder report = new StringBuilder();
            int shown = 0;
            for (int i = observations.Count - 1; i >= 0 && shown < 3; i--, shown++)
            {
                LookoutObservation item = observations[i];
                if (shown > 0) report.Append('\n');
                report.Append(item.Horizontal < -0.15f ? "左舷" : item.Horizontal > 0.15f ? "右舷" : "正前方");
                report.Append(" · ").Append(item.Label).Append("　").Append(item.DistanceNm.ToString("0")).Append(" 海里");
            }
            if (shown == 0) report.Append("海面开阔 · 暂无目标");
            NavigationText("Lookout Report", lookoutViewport, new Vector2(894f, 80f), new Vector2(0f, -264f), 22, Ink).text = report.ToString();
        }

        private void BuildVoyageHeaderTime(RectTransform header)
        {
            // VoyageTimeRibbonGraphic draws the dawn/day/dusk/night track and
            // moving marker directly on the header, without a backing panel. Do not create
            // an Image on the same object: Unity allows only one Graphic component
            // per GameObject, and the old CreateFixedPanel helper already adds one.
            // Keep established object names for preview migration and diagnostics.
            // Only the graphical track is scaled; its date and legend remain
            // readable, unscaled text inside the header's central tutorial-free band.
            RectTransform ribbon = CreateRect("Lookout Time Ribbon", header, new Vector2(720f, 68f),
                new Vector2(115f, 0f));
            RectTransform track = CreateRect("Lookout Time Track", ribbon, new Vector2(1800f, 116f), new Vector2(0f, -3f));
            track.localScale = Vector3.one * .4f;
            VoyageTimeRibbonGraphic timeline = track.gameObject.AddComponent<VoyageTimeRibbonGraphic>();
            timeline.SetVoyageHours(session.ElapsedGameHours);
            timeline.raycastTarget = false;
            NavigationText("Lookout Time Readout", ribbon, new Vector2(700f, 24f), new Vector2(0f, 22f), 20, Ink,
                TextAnchor.MiddleCenter).text = "游戏 " + CoreVoyageSession.FormatGameDate(session.GameDateTime, true) + "　·　" +
                    NavigationTimeOfDayUtility.Label(NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours));
            NavigationText("Lookout Time Legend", ribbon, new Vector2(700f, 16f), new Vector2(0f, -26f), 12, Ink,
                TextAnchor.MiddleCenter).text = "夜航　·　黎明　·　白昼　·　黄昏　·　夜航";
        }

        private void BuildNavigationSupplyEndurance(RectTransform chart)
        {
            RectTransform panel = CreateRect("Voyage Supply Endurance", chart,
                new Vector2(470f, 30f), new Vector2(170f, 251f));
            NavigationText("Water Food Endurance", panel, new Vector2(470f, 24f), Vector2.zero, 17, Ink).text =
                "淡水 " + FormatSupplyEnduranceDuration(session.WaterSupplyHours) +
                "　·　食物 " + FormatSupplyEnduranceDuration(session.FoodSupplyHours);
        }

        private static string FormatSupplyEnduranceDuration(double hours)
        {
            if (double.IsPositiveInfinity(hours)) return "无需消耗";
            if (hours <= 1e-9d) return "已耗尽";
            if (hours < 1d) return "不足 1 小时";
            double wholeHours = System.Math.Floor(hours);
            double days = System.Math.Floor(wholeHours / 24d);
            int remainder = (int)(wholeHours % 24d);
            return days > 0d ? "约 " + days.ToString("0") + " 天 " + remainder + " 小时" : "约 " + remainder + " 小时";
        }

        private Text NavigationText(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            // Chart and controls use parchment. Their former light-on-dark
            // colors disappear on the current paper skin; lookout text remains
            // light because it is drawn over the sea and wooden caption plates.
            for (Transform surface = parent; surface != null; surface = surface.parent)
            {
                if (surface.name != "Navigation Chart" && surface.name != "Navigation Controls") continue;
                color = color == Gold ? Html("#694014") : color == MutedInk ? Html("#4F4A3E") : Html("#2E281E");
                break;
            }
            Text text = CreateText(name, parent, size, position, fontSize,
                name.EndsWith("Title") || name == "Lookout Heading" ? FontStyle.Bold : FontStyle.Normal, alignment, color);
            if (name == "Lookout Heading" || name == "Lookout Report") text.color = Ink;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (name == "Forecast" || name == "Lookout Report") text.lineSpacing = name == "Lookout Report" ? 1.08f : 1.16f;
            text.raycastTarget = false;
            return text;
        }
    }
}
