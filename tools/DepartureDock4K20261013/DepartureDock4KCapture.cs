using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Disposable-project verification only. Copy to Assets/_Project/Editor in
    // IsolatedProject. Does not enter Play, invoke a purchase/start callback,
    // save a scene/asset, or deliberately write PlayerPrefs.
    public static class DepartureDock4KCapture
    {
        private const string IsolatedProject = "D:/Game/tmp/departure-dock-scene-20261012";
        private const int CaptureLayer = 31;
        private static readonly string[] PortNames = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
        private static readonly string[] PortSlugs = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };
        private static readonly string[] HeaderButtons = { "Return To Port Hub", "Previous Port Facility", "Next Port Facility", "Port Facility Settings" };
        private static readonly string[] SetupButtons = {
            "Departure Previous Vessel", "Departure Next Vessel", "Inspect Departure Vessel",
            "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton", "Departure Buy Provisions",
            "Adjust Voyage Supplies", "Configure Voyage Battle Decks", "Configure Voyage Crew", "Start Voyage"
        };
        private static readonly string[] PaintedRouteButtons = { "Departure Dock Ordinary Voyage" };
        private static readonly Dictionary<string, Dictionary<Rect, Rect>> SourceAlphaBounds =
            new Dictionary<string, Dictionary<Rect, Rect>>(StringComparer.OrdinalIgnoreCase);
        private enum State { Ordinary, Chart, InvalidChart, Treasure, Crew, BasicSupplies }

        [Serializable] private sealed class Report
        {
            public string utc, project;
            public string scope = "Production non-Play previews in private PreviewScenes. Five in-memory vessel fixtures, ordinary/valid and invalid chart/treasure/crew/basic supplies, regional fallback, 720p/1440p, and all three separate sea charts at 3840x2160. Uses the production CanvasScaler Expand ratio. This captures real generated UI but does not prove live switch or sail callbacks; run PlayMode tests for those. No ownership persistence, start/purchase callbacks or scene/asset saves.";
            public bool passed;
            public List<Page> pages = new List<Page>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Page
        {
            public string state, port, actualPort, file, sessionPhase, backgroundResource, backgroundAsset, shipName;
            public int width, height, treasureMapsBefore, treasureMapsAfter, shipIndex, actualShipIndex, routeIndex;
            public string routeName, chartAsset;
            public bool commonHeaderVisible, independentChartVisible;
            public bool passed, completePreview, screenshotSaved, chartSelected, startInteractable;
            public string authoredRenderMode, scaleMode, screenMatchMode;
            public Vector2 referenceResolution, logicalCanvasSize;
            public float outputScale, cameraOrthographicSize;
            public Rect routeColumn, setupColumn;
            public bool expectsDockScene, expectsIndependentBerth, independentShipLayer;
            public string shipHullPath, shipHullTexture, waterContactPath;
            public Rect shipScreenBounds, shipVisibleScreenBounds, shipHullUv;
            public bool shipFrameInsideScreen;
            public Vector2 hullContactInWaterSpace, waterAnchorScreenPosition;
            public float expectedHullWaterContactY;
            public bool setupRootDoesNotBlockRaycasts;
            public ForegroundDiagnostic foreground = new ForegroundDiagnostic();
            public List<Control> controls = new List<Control>();
            public List<Label> labels = new List<Label>();
            public List<RaycastProbe> raycastProbes = new List<RaycastProbe>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Control
        {
            public string path, type, label;
            public bool active, enabled, interactable, insideScreen, insideColumn;
            public Rect screenBounds;
        }
        [Serializable] private sealed class Label
        {
            public string path, text;
            public int nominalFontSize, minimumFontSize;
            public float minimumPixelFontSize, minimumRequiredHeight, availableHeight;
            public bool activeInHierarchy, graphicEnabled, fitsAtMinimumSize, checkedForClipping;
            public Rect screenBounds;
        }
        [Serializable] private sealed class RaycastProbe
        {
            public string target, firstHit, hitControl;
            public Vector2 screenPoint;
            public bool reachesTarget;
            public List<string> graphicAncestors = new List<string>();
        }
        [Serializable] private sealed class ForegroundDiagnostic
        {
            public bool found, expectedActive, activeSelf, activeInHierarchy, graphicEnabled, raycastTarget, hasCanvasRenderer, rendererCulled;
            public string path, type, texture, material, parent;
            public float graphicColorAlpha, rendererColorAlpha, rendererAlpha, inheritedAlpha;
            public int siblingIndex = -1, inspectSiblingIndex = -1, graphicDepth = -1, canvasAbsoluteDepth = -1;
            public int meshVertexCount, meshIndexCount;
            public Rect screenBounds;
            public Bounds meshBounds;
            public float minimumVertexAlpha = 1f, maximumVertexAlpha;
            public List<string> ancestorGraphics = new List<string>();
        }

        public static void CaptureFromCommandLine()
        {
            string project = RequireIsolatedProject();
            string output = RequiredEmptyOutput("-departureDock4KCapturePath");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use stopped non-Play editor with graphics enabled; omit -nographics.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), project = project };
            SourceAlphaBounds.Clear();
            try
            {
                foreach (int vessel in new[] { 0, 5, 7, 8, 11 })
                    Capture(report, output, CoreGameplayPreviewPort.BrokenAnchor, State.Ordinary, 1920, 1080, vessel);
                Capture(report, output, CoreGameplayPreviewPort.WhiteShell, State.Ordinary, 1920, 1080, 0);
                Capture(report, output, CoreGameplayPreviewPort.BrokenAnchor, State.Ordinary, 1280, 720, 0);
                Capture(report, output, CoreGameplayPreviewPort.BrokenAnchor, State.Ordinary, 2560, 1440, 0);
                foreach (State state in new[] { State.Chart, State.InvalidChart, State.Treasure, State.Crew, State.BasicSupplies })
                    Capture(report, output, CoreGameplayPreviewPort.BrokenAnchor, state, 1920, 1080, 0);
                for (int routeIndex = 0; routeIndex < 3; routeIndex++)
                    Capture(report, output, CoreGameplayPreviewPort.BrokenAnchor, State.Ordinary, 3840, 2160, 0, routeIndex);
                if (report.pages.Count != 16) report.errors.Add("Expected 16 captures.");
                InspectVesselComparisons(report);
                report.passed = report.errors.Count == 0 && report.pages.All(page => page.passed);
            }
            finally
            {
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            if (!report.passed) throw new InvalidOperationException("Departure dock scene review failed; inspect report.json. Errors=" + report.errors.Count);
            Debug.Log("DEPARTURE_DOCK_4K_REVIEW: 16 production UI captures passed: five vessels, three 4K sea charts, common header, one regional fallback, 720p/1080p/1440p/2160p, chart, invalid chart, treasure, crew and supplies. " + output);
        }

        private static void Capture(Report report, string output, CoreGameplayPreviewPort port, State state, int width, int height, int shipIndex, int routeIndex = 0)
        {
            var page = new Page { state = state.ToString(), port = PortNames[(int)port], width = width, height = height,
                shipIndex = shipIndex, routeIndex = routeIndex, expectsDockScene = port == CoreGameplayPreviewPort.BrokenAnchor,
                expectsIndependentBerth = port == CoreGameplayPreviewPort.BrokenAnchor && (state == State.Ordinary || state == State.BasicSupplies),
                file = PortSlugs[(int)port] + "_ship" + shipIndex + "_route" + routeIndex + "_" + state.ToString().ToLowerInvariant() + "_" + width + "x" + height + ".png" };
            report.pages.Add(page);
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, previousTarget = RenderTexture.active;
            try
            {
                host = new GameObject("Departure Dock Scene Review Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, port, 0);
                host.SetActive(true);
                if (routeIndex > 0)
                {
                    controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
                        { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
                    controller.Session.SelectRoute(routeIndex);
                    Invoke(controller, "Render");
                }
                ConfigureState(controller, state);
                ConfigureVessel(controller, shipIndex, state);
                page.actualPort = controller.Session.CurrentPort;
                page.routeName = CoreVoyageSession.Routes[controller.Session.SelectedRouteIndex].Name;
                if (state == State.Ordinary && controller.Session.SelectedRouteIndex != routeIndex)
                    page.errors.Add("Unexpected selected sea chart: " + controller.Session.SelectedRouteIndex);
                page.actualShipIndex = controller.Session.SelectedShipIndex;
                page.shipName = CoreVoyageSession.Ships[page.actualShipIndex].Name;
                page.completePreview = controller.HasCompleteEditorPreview;
                page.treasureMapsBefore = controller.Session.TreasureMaps.Count;
                if (!page.completePreview) page.errors.Add("Incomplete production departure preview.");
                if (page.actualPort != page.port) page.errors.Add("Unexpected origin port: " + page.actualPort);
                if (page.actualShipIndex != page.shipIndex) page.errors.Add("Unexpected active vessel: " + page.actualShipIndex);

                cameraHost = new GameObject("Departure Dock Scene Review Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .06f, .075f, 1f);
                camera.orthographic = true;
                camera.aspect = width / (float)height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << CaptureLayer;
                camera.scene = scene;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;

                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                ConfigureRenderGeometry(page, canvas, camera);
                foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = CaptureLayer;
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                InspectPage(page, host, controller, state, camera);
                SaveFrame(target, Path.Combine(output, page.file));
                page.screenshotSaved = File.Exists(Path.Combine(output, page.file));
                page.sessionPhase = controller.Session.Phase.ToString();
                page.treasureMapsAfter = controller.Session.TreasureMaps.Count;
                if (controller.Session.Phase != CoreDemoPhase.Port) page.errors.Add("Capture must not start a voyage.");
                if (page.treasureMapsBefore != page.treasureMapsAfter) page.errors.Add("Capture consumed a treasure map.");
            }
            catch (Exception error) { page.errors.Add(error.ToString()); Debug.LogException(error); }
            finally
            {
                RenderTexture.active = previousTarget;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
            page.passed = page.errors.Count == 0 && page.completePreview && page.screenshotSaved;
            foreach (string error in page.errors) report.errors.Add(page.file + ": " + error);
        }

        private static void ConfigureState(CoreGameplayDemoController controller, State state)
        {
            // These helpers mutate only the newly constructed in-memory fixture.
            switch (state)
            {
                case State.Chart: controller.ConfigureEditorPreviewDepartureChart(1); break;
                case State.InvalidChart: controller.ConfigureEditorPreviewDepartureChart(2); break;
                case State.Treasure: controller.ConfigureEditorFlowState(GameplayFlowPreviewState.TreasureDeparture); break;
                case State.Crew:
                    typeof(CoreGameplayDemoController).GetField("departureCrewConfigurationOpen", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(controller, true);
                    Invoke(controller, "Render");
                    break;
                case State.BasicSupplies:
                    Invoke(controller, "OpenDepartureBasicSupplies", Find<RectTransform>(controller.gameObject, "Voyage Setup"));
                    break;
            }
        }

        private static void ConfigureVessel(CoreGameplayDemoController controller, int shipIndex, State state)
        {
            // SelectShip changes only this private fixture. Calling the shipyard
            // preview API here would navigate away and rebuild its session.
            // Keep ship 0 state fixtures intact: chart warnings/overlays are
            // attached after Render and should not be dismissed for capture.
            if (controller.Session.SelectedShipIndex == shipIndex) return;
            if (!controller.Session.SelectShip(shipIndex))
                throw new InvalidOperationException("Fixture cargo does not fit vessel " + shipIndex);
            controller.EnsureShipComponentLoadout(shipIndex);
            Invoke(controller, "RefreshComponentBonuses", false);
            Invoke(controller, "Render");
            if (state == State.BasicSupplies)
                Invoke(controller, "OpenDepartureBasicSupplies", Find<RectTransform>(controller.gameObject, "Voyage Setup"));
        }

        private static void InspectVesselComparisons(Report report)
        {
            Page[] vessels = report.pages.Where(page => page.expectsDockScene && page.state == State.Ordinary.ToString() && page.width == 1920).ToArray();
            if (vessels.Length != 5) { report.errors.Add("Expected five same-resolution dock vessel fixtures."); return; }
            if (vessels.Select(page => page.backgroundAsset).Distinct().Count() != 1)
                report.errors.Add("Selecting another vessel changed the fixed dock background.");
            if (vessels.All(page => page.independentShipLayer))
            {
                if (vessels.Select(page => page.shipHullTexture + "|" + page.shipHullUv).Distinct().Count() != 5)
                    report.errors.Add("Five vessel fixtures did not resolve to distinct hull art/UV selections.");
                foreach (Page page in vessels.Skip(1))
                {
                    if (Vector2.Distance(page.waterAnchorScreenPosition, vessels[0].waterAnchorScreenPosition) > .5f)
                        report.errors.Add("Vessel switch moved the fixed berth waterline anchor: " + page.file);
                    foreach (string name in new[] { "Departure Previous Vessel", "Departure Next Vessel", "Start Voyage", "Configure Voyage Crew" })
                    {
                        Control first = vessels[0].controls.FirstOrDefault(item => item.path.EndsWith("/" + name, StringComparison.Ordinal));
                        Control next = page.controls.FirstOrDefault(item => item.path.EndsWith("/" + name, StringComparison.Ordinal));
                        if (first != null && next != null && (Vector2.Distance(first.screenBounds.position, next.screenBounds.position) > .5f ||
                            Vector2.Distance(first.screenBounds.size, next.screenBounds.size) > .5f))
                            report.errors.Add("Vessel switch moved a fixed operation: " + name + " in " + page.file);
                    }
                }
            }
        }

        private static void ConfigureRenderGeometry(Page page, Canvas canvas, Camera camera)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                throw new InvalidOperationException("Production CanvasScaler is not the expected ScaleWithScreenSize/Expand configuration.");
            page.authoredRenderMode = canvas.renderMode.ToString();
            page.scaleMode = scaler.uiScaleMode.ToString();
            page.screenMatchMode = scaler.screenMatchMode.ToString();
            page.referenceResolution = scaler.referenceResolution;
            // CanvasScaler.Expand uses min(width/refWidth, height/refHeight).
            // Keep the logical canvas and all authored elements unchanged for a
            // 16:9 resize. Only the camera's render target changes pixel density.
            page.outputScale = Mathf.Min(page.width / scaler.referenceResolution.x, page.height / scaler.referenceResolution.y);
            page.logicalCanvasSize = new Vector2(page.width / page.outputScale, page.height / page.outputScale);
            page.cameraOrthographicSize = page.logicalCanvasSize.y * .5f;
            scaler.enabled = false; // Avoid sampling the unrelated editor GameView.
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.scaleFactor = 1f;
            RectTransform root = canvas.GetComponent<RectTransform>();
            root.sizeDelta = page.logicalCanvasSize;
            root.position = Vector3.zero;
            root.localScale = Vector3.one;
            camera.orthographicSize = page.cameraOrthographicSize;
        }

        private static void InspectPage(Page page, GameObject host, CoreGameplayDemoController controller, State state, Camera camera)
        {
            RectTransform routes = Require<RectTransform>(page, host, "Route Selection");
            RectTransform setup = Require<RectTransform>(page, host, "Voyage Setup");
            if (routes == null || setup == null) return;
            page.routeColumn = ScreenBounds(routes, camera, page.height);
            page.setupColumn = ScreenBounds(setup, camera, page.height);
            if (!InsideScreen(page, page.routeColumn) || !InsideScreen(page, page.setupColumn)) page.errors.Add("A departure column extends outside the screen.");
            foreach (string name in HeaderButtons) Require<Button>(page, host, name);
            Require<Button>(page, host, "Departure Treasure Seed Picker");
            Require<Text>(page, host, "Departure Route Summary");
            if (page.expectsIndependentBerth)
            {
                if (Vector2.Distance(routes.sizeDelta, PhaseSurfaceLayout.ContentSize) > .2f || routes.anchoredPosition.sqrMagnitude > .04f)
                    page.errors.Add("Painted route controls must use the complete 1920x1080 scene coordinate surface.");
                foreach (string name in PaintedRouteButtons) Require<Button>(page, host, name);
                foreach (string name in new[] { "Departure Dock Ordinary Readout", "Departure Dock Special Readout", "Departure Dock Speed Readout" })
                {
                    Text readout = Require<Text>(page, host, name);
                    if (readout != null && !readout.enabled) page.errors.Add(name + ": dynamic painted-panel readout is disabled.");
                }
            }
            if (state == State.Crew)
            {
                Require<Text>(page, host, "Departure Crew Configuration Title");
                Require<Slider>(page, host, "Departure Ordinary Crew Slider");
                Require<Button>(page, host, "Back To Voyage Setup");
                Require<Button>(page, host, "Confirm Departure Crew");
                for (int index = 0; index < 6; index++) Require<Button>(page, host, "Departure Special Crew Toggle " + index);
            }
            else foreach (string name in SetupButtons) Require<Button>(page, host, name);

            bool chart = state == State.Chart || state == State.InvalidChart;
            if (state == State.Treasure)
            {
                Require<Text>(page, host, "Treasure Departure Title");
                Require<RawImage>(page, host, "Treasure Departure Art");
                Require<Text>(page, host, "Treasure Departure Detail");
                Require<Text>(page, host, "Treasure Departure Consumption");
                Require<Button>(page, host, "Cancel Treasure Voyage");
                Require<Button>(page, host, "Return To Treasure Table");
                if (controller.Session.SelectedTreasureMap == null) page.errors.Add("Treasure fixture has no selected treasure map.");
            }
            else
            {
                Require<Toggle>(page, host, "Use Chart Seed");
                for (int index = 0; index < 6; index++) Require<Button>(page, host, "Departure Destination " + index, !chart);
                for (int index = 0; index < 3; index++)
                {
                    Require<Button>(page, host, "Route " + index, !chart);
                    Require<RawImage>(page, host, "Route Preview " + index, !chart);
                }
            }
            page.chartSelected = controller.Session.IsChartSeedSelected;
            Button start = Find<Button>(host, "Start Voyage");
            page.startInteractable = start != null && start.IsInteractable();
            if (chart)
            {
                Require<InputField>(page, host, "Seed Input");
                Require<Dropdown>(page, host, "Seed Input Dropdown");
                Require<Text>(page, host, "Chart Seed Validation");
                if (state == State.Chart)
                {
                    Require<RectTransform>(page, host, "Departure Chart Preview");
                    Require<RectTransform>(page, host, "Departure Chart Preview Origin");
                    Require<RectTransform>(page, host, "Departure Chart Preview Destination");
                    Require<Text>(page, host, "Departure Chart Ports");
                    if (!page.chartSelected) page.errors.Add("Valid chart fixture was not selected.");
                }
                else
                {
                    Require<RectTransform>(page, host, "Chart Seed Warning");
                    Require<Text>(page, host, "Chart Seed Warning Reason");
                    Require<Button>(page, host, "Close Chart Seed Warning");
                    if (page.chartSelected || page.startInteractable) page.errors.Add("Invalid chart must block departure and clear chart selection.");
                }
            }
            if (state == State.BasicSupplies)
            {
                Require<RectTransform>(page, host, "Departure Basic Supplies Overlay");
                foreach (string name in new[] { "Departure Basic Supplies Recommend", "Departure Basic Supplies Close", "Departure Basic Supplies Purchase" })
                    Require<Button>(page, host, name);
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    Require<Button>(page, host, "Departure Basic Supply Decrease " + key);
                    Require<Button>(page, host, "Departure Basic Supply Increase " + key);
                    Require<Text>(page, host, "Departure Basic Supply Quantity " + key);
                }
            }
            RawImage background = Require<RawImage>(page, host, "Voyage Background");
            page.backgroundResource = PortFacilityArtCatalog.RegionalBackgroundResourcePath(PortFacilityArtCatalog.VoyageFacilityIndex, page.port);
            if (page.expectsDockScene && page.backgroundResource != "PortFacilities/Regional/Voyage/voyage_broken_anchor_scene_v4")
                page.errors.Add("Broken Anchor did not use its dedicated textless 4K dock scene resource.");
            if (background != null)
            {
                page.backgroundAsset = AssetDatabase.GetAssetPath(background.texture);
                if (background.texture != Resources.Load<Texture2D>(page.backgroundResource) || background.color != Color.white)
                    page.errors.Add("Regional departure background texture/tint differs from the production catalog.");
            }
            InspectIndependentChartAndSharedHeader(page, host, controller, background);
            InspectIndependentShip(page, host, background, state, camera);
            InspectForeground(page, host, camera);
            InspectControlsAndLabels(page, host, routes, setup, camera);
            InspectPointerReachability(page, host, routes, setup, state, camera);
        }

        private static void InspectIndependentChartAndSharedHeader(Page page, GameObject host,
            CoreGameplayDemoController controller, RawImage background)
        {
            Image header = Require<Image>(page, host, "Port Facility Header");
            Image frame = Require<Image>(page, host, "Port Facility Frame");
            Text title = Require<Text>(page, host, "Port Facility Title");
            Text port = Require<Text>(page, host, "Port Facility Port");
            Text time = Require<Text>(page, host, "Port Facility Time");
            Text gold = Require<Text>(page, host, "Port Facility Gold");
            page.commonHeaderVisible = new Graphic[] { header, frame, title, port, time, gold }
                .All(graphic => graphic != null && graphic.isActiveAndEnabled);
            if (!page.commonHeaderVisible) page.errors.Add("The shared port facility header has hidden graphics.");
            foreach (string name in HeaderButtons)
            {
                Button button = Find<Button>(host, name);
                if (button == null) continue;
                Image face = button.GetComponent<Image>();
                Text caption = button.GetComponentInChildren<Text>(true);
                if (face == null || !face.enabled || face.color.a <= 0 || caption == null || !caption.isActiveAndEnabled)
                    page.errors.Add("Common header button lost its original face/caption: " + name);
            }
            if (!page.expectsIndependentBerth) return;
            if (background == null || background.texture == null || background.texture.width != 3840 || background.texture.height != 2160)
                page.errors.Add("The imported dock base must preserve its 3840x2160 source dimensions.");
            RawImage sea = Require<RawImage>(page, host, "Departure Dock Selected Sea");
            Text detail = Require<Text>(page, host, "Departure Dock Sea Detail");
            Text hint = Require<Text>(page, host, "Departure Dock Ship Hint");
            string[] paths = { "chart-shattered-reef", "chart-golden-trade", "chart-moon-ruins" };
            Texture2D expected = Resources.Load<Texture2D>("PortFacilities/DepartureCharts4K/" + paths[controller.Session.SelectedRouteIndex]);
            page.independentChartVisible = sea != null && sea.isActiveAndEnabled && sea.texture == expected && expected != null;
            if (!page.independentChartVisible) page.errors.Add("The selected sea chart is missing or not independently visible.");
            if (sea != null)
            {
                page.chartAsset = AssetDatabase.GetAssetPath(sea.texture);
                if (sea.texture == null || sea.texture.width != 3840 || sea.texture.height != 2160)
                    page.errors.Add("The separate sea chart must preserve its 3840x2160 source dimensions.");
                if (sea.raycastTarget) page.errors.Add("Decorative sea chart blocks route input.");
                Mesh mesh = sea.canvasRenderer.GetMesh();
                if (mesh == null || mesh.vertexCount < 4) page.errors.Add("Independent sea chart has no rendered mesh.");
            }
            if (detail == null || !detail.isActiveAndEnabled || !detail.text.Contains(page.routeName) ||
                !detail.text.Contains(CoreVoyageSession.Routes[controller.Session.SelectedRouteIndex].BossName))
                page.errors.Add("Selected sea description is not visible/live.");
            if (hint == null || !hint.isActiveAndEnabled) page.errors.Add("Independent ship inspect hint is hidden.");
            foreach (string name in new[] { "Departure Buy Provisions", "Adjust Voyage Supplies", "Configure Voyage Battle Decks",
                "Configure Voyage Crew", "Start Voyage", "Departure Dock Ordinary Voyage", "Departure Treasure Seed Picker" }
                .Concat(Enumerable.Range(0, 6).Select(index => "Departure Destination " + index))
                .Concat(Enumerable.Range(0, 3).Select(index => "Route " + index)))
            {
                Button button = Find<Button>(host, name);
                Text caption = button == null ? null : button.transform.Find("Label")?.GetComponent<Text>();
                if (caption == null || !caption.isActiveAndEnabled || string.IsNullOrWhiteSpace(caption.text))
                    page.errors.Add("Textless scene needs a visible independent caption: " + name);
            }
        }
        private static void InspectIndependentShip(Page page, GameObject host, RawImage background, State state, Camera camera)
        {
            // Chart, treasure and crew editing retain the shared paper layout.
            // Only ordinary preparation and its supplies overlay use the berth.
            if (!page.expectsIndependentBerth) return;
            RectTransform layer = Require<RectTransform>(page, host, "Starter Modular Ship");
            RectTransform hit = Require<RectTransform>(page, host, "Inspect Departure Vessel");
            RectTransform water = Require<RectTransform>(page, host, "Departure Dock Water Contact");
            if (layer == null || hit == null || water == null || background == null) return;
            RectTransform setup = Find<RectTransform>(host, "Voyage Setup");
            if (water.parent != setup.parent) page.errors.Add("Water contact must be a direct child of the scene content root.");
            if (Vector2.Distance(water.anchoredPosition, new Vector2(DepartureDockSceneLayout.ShipCenterX, DepartureDockSceneLayout.ShipWaterlineY)) > .2f)
                page.errors.Add("Water contact marker left the authored fixed berth anchor.");
            if (Vector2.Distance(hit.sizeDelta, DepartureDockSceneLayout.ShipHitSize) > .2f ||
                Vector2.Distance(hit.anchoredPosition, DepartureDockSceneLayout.ShipHitPosition) > .2f)
                page.errors.Add("Vessel inspection hit area differs from the fixed dock scene contract.");
            RawImage[] hullCandidates = layer.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.texture != null && (image.name.EndsWith(" Base", StringComparison.Ordinal) ||
                    image.name.Contains("Ship Art") || image.name.EndsWith(" Hull", StringComparison.Ordinal))).ToArray();
            if (hullCandidates.Length != 1)
            {
                page.errors.Add("Expected one independently selectable dock hull, found " + hullCandidates.Length);
                return;
            }
            RawImage hull = hullCandidates[0];
            page.independentShipLayer = hull.texture != background.texture && !layer.IsChildOf(background.transform) &&
                !background.transform.IsChildOf(layer) && !hull.transform.IsChildOf(background.transform);
            if (!page.independentShipLayer) page.errors.Add("The displayed ship is coupled to the fixed dock background.");
            page.shipHullPath = HierarchyPath(hull.transform, host.transform);
            page.shipHullTexture = AssetDatabase.GetAssetPath(hull.texture);
            page.shipHullUv = hull.uvRect;
            page.shipScreenBounds = ScreenBounds(hull.rectTransform, camera, page.height);
            page.shipFrameInsideScreen = InsideScreen(page, page.shipScreenBounds);
            page.shipVisibleScreenBounds = VisibleHullScreenBounds(hull, camera, page.height);
            page.waterContactPath = HierarchyPath(water, host.transform);
            page.hullContactInWaterSpace = water.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
            Vector3 waterScreen = camera.WorldToScreenPoint(water.position);
            page.waterAnchorScreenPosition = new Vector2(waterScreen.x, page.height - waterScreen.y);
            page.expectedHullWaterContactY = 0f;
            if (Mathf.Abs(page.hullContactInWaterSpace.y - page.expectedHullWaterContactY) > .2f)
                page.errors.Add("Hull does not contact its authored waterline. Actual y=" + page.hullContactInWaterSpace.y + "; expected=" + page.expectedHullWaterContactY);
            // Transparent source padding can leave the screen when a low hull
            // is enlarged. Validate source alpha, not its invisible sample frame.
            if (!InsideScreen(page, page.shipVisibleScreenBounds)) page.errors.Add("The selected visible hull extends beyond the screen.");
            RectTransform header = Find<RectTransform>(host, "Port Facility Header");
            if (header != null && page.shipVisibleScreenBounds.yMin < ScreenBounds(header, camera, page.height).yMax - 1f)
                page.errors.Add("The selected vessel extends under the common header.");
            foreach (Graphic graphic in water.GetComponentsInChildren<Graphic>(true))
                if (graphic.raycastTarget) page.errors.Add("Decorative water contact blocks pointer input: " + graphic.name);
        }

        private static void InspectForeground(Page page, GameObject host, Camera camera)
        {
            ForegroundDiagnostic diagnostic = page.foreground;
            diagnostic.expectedActive = page.expectsIndependentBerth;
            Graphic[] candidates = host.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic.name == "Departure Dock Foreground").ToArray();
            diagnostic.found = candidates.Length > 0;
            if (candidates.Length == 0)
            {
                if (diagnostic.expectedActive) page.errors.Add("Independent berth has no Departure Dock Foreground graphic.");
                return;
            }
            if (candidates.Length != 1) page.errors.Add("Expected at most one Departure Dock Foreground, found " + candidates.Length);
            Graphic foreground = candidates.FirstOrDefault(graphic => graphic.gameObject.activeInHierarchy) ?? candidates[0];
            CanvasRenderer renderer = foreground.GetComponent<CanvasRenderer>();
            diagnostic.hasCanvasRenderer = renderer != null;
            if (renderer == null)
            {
                page.errors.Add("Departure Dock Foreground has no CanvasRenderer.");
                return;
            }
            diagnostic.path = HierarchyPath(foreground.transform, host.transform);
            diagnostic.type = foreground.GetType().Name;
            diagnostic.parent = foreground.transform.parent == null ? string.Empty : HierarchyPath(foreground.transform.parent, host.transform);
            diagnostic.activeSelf = foreground.gameObject.activeSelf;
            diagnostic.activeInHierarchy = foreground.gameObject.activeInHierarchy;
            diagnostic.graphicEnabled = foreground.enabled;
            diagnostic.raycastTarget = foreground.raycastTarget;
            diagnostic.graphicColorAlpha = foreground.color.a;
            diagnostic.rendererColorAlpha = renderer.GetColor().a;
            diagnostic.rendererAlpha = renderer.GetAlpha();
            diagnostic.inheritedAlpha = renderer.GetInheritedAlpha();
            diagnostic.rendererCulled = renderer.cull;
            diagnostic.siblingIndex = foreground.transform.GetSiblingIndex();
            Transform inspect = Find<RectTransform>(host, "Inspect Departure Vessel");
            if (inspect != null && inspect.parent == foreground.transform.parent) diagnostic.inspectSiblingIndex = inspect.GetSiblingIndex();
            diagnostic.graphicDepth = foreground.depth;
            diagnostic.canvasAbsoluteDepth = renderer.absoluteDepth;
            diagnostic.texture = foreground.mainTexture == null ? string.Empty : AssetDatabase.GetAssetPath(foreground.mainTexture);
            diagnostic.material = foreground.material == null ? string.Empty : foreground.material.name;
            diagnostic.screenBounds = ScreenBounds(foreground.rectTransform, camera, page.height);
            // Unity 6000.3 returns the renderer-owned mesh. Inspect it without
            // mutating or destroying it; the CanvasRenderer keeps ownership.
            Mesh mesh = renderer.GetMesh();
            if (mesh != null)
            {
                diagnostic.meshVertexCount = mesh.vertexCount;
                diagnostic.meshIndexCount = mesh.triangles.Length;
                diagnostic.meshBounds = mesh.bounds;
                Color32[] colors = mesh.colors32;
                if (colors.Length == 0) diagnostic.minimumVertexAlpha = diagnostic.maximumVertexAlpha = 0f;
                else foreach (Color32 color in colors)
                {
                    float alpha = color.a / 255f;
                    diagnostic.minimumVertexAlpha = Mathf.Min(diagnostic.minimumVertexAlpha, alpha);
                    diagnostic.maximumVertexAlpha = Mathf.Max(diagnostic.maximumVertexAlpha, alpha);
                }
            }
            else diagnostic.minimumVertexAlpha = diagnostic.maximumVertexAlpha = 0f;
            for (Transform ancestor = foreground.transform.parent; ancestor != null && ancestor != host.transform; ancestor = ancestor.parent)
            {
                Graphic graphic = ancestor.GetComponent<Graphic>();
                if (graphic != null)
                    diagnostic.ancestorGraphics.Add(HierarchyPath(ancestor, host.transform) + " [active=" + graphic.gameObject.activeInHierarchy +
                        ", alpha=" + graphic.color.a + ", depth=" + graphic.depth + ", culled=" + graphic.canvasRenderer.cull + "]");
                CanvasGroup group = ancestor.GetComponent<CanvasGroup>();
                if (group != null)
                    diagnostic.ancestorGraphics.Add(HierarchyPath(ancestor, host.transform) + " [groupAlpha=" + group.alpha + "]");
            }
            if (diagnostic.activeInHierarchy != diagnostic.expectedActive)
                page.errors.Add("Departure Dock Foreground has unexpected active state for this departure mode.");
            if (diagnostic.expectedActive && diagnostic.raycastTarget)
                page.errors.Add("Decorative dock foreground blocks pointer input.");
            if (diagnostic.expectedActive && (!diagnostic.graphicEnabled || diagnostic.meshVertexCount <= 0 || diagnostic.meshIndexCount <= 0))
                page.errors.Add("Active painted dock foreground has no enabled, populated rendering mesh.");
        }

        private static void InspectControlsAndLabels(Page page, GameObject host, RectTransform routes, RectTransform setup, Camera camera)
        {
            Selectable[] controls = host.GetComponentsInChildren<Selectable>(false).Where(control => control.gameObject.activeInHierarchy).ToArray();
            foreach (Selectable control in controls)
            {
                Rect bounds = ScreenBounds(control.GetComponent<RectTransform>(), camera, page.height);
                Text label = control.GetComponentInChildren<Text>();
                Rect column = control.transform.IsChildOf(routes) ? page.routeColumn : control.transform.IsChildOf(setup) ? page.setupColumn : default;
                bool insideColumn = page.expectsIndependentBerth || column.width <= 0 || bounds.xMin >= column.xMin - 1f && bounds.xMax <= column.xMax + 1f;
                var item = new Control { path = HierarchyPath(control.transform, host.transform), type = control.GetType().Name,
                    label = label != null ? label.text : string.Empty, active = control.gameObject.activeInHierarchy, enabled = control.enabled,
                    interactable = control.IsInteractable(), screenBounds = bounds, insideScreen = InsideScreen(page, bounds), insideColumn = insideColumn };
                page.controls.Add(item);
                if (!item.insideScreen || !insideColumn || bounds.width <= 0 || bounds.height <= 0)
                    page.errors.Add(item.path + ": empty control or bounds outside screen/own column.");
                if (bounds.height / page.outputScale < 28f) page.errors.Add(item.path + ": control height is below 28 logical pixels.");
            }
            // Compare only siblings: parent controls intentionally contain labels,
            // ship images and dropdown sub-controls; modals intentionally cover UI.
            foreach (IGrouping<Transform, Selectable> group in controls.Where(control => control is Button).GroupBy(control => control.transform.parent))
            {
                Selectable[] siblings = group.ToArray();
                for (int a = 0; a < siblings.Length; a++) for (int b = a + 1; b < siblings.Length; b++)
                {
                    // Painted arrows and crew +/- sit over the vessel by design.
                    // Their actual pointer ordering is still checked below.
                    if (page.expectsIndependentBerth && IsLayeredShipControlPair(siblings[a], siblings[b])) continue;
                    Rect first = ScreenBounds(siblings[a].GetComponent<RectTransform>(), camera, page.height);
                    Rect second = ScreenBounds(siblings[b].GetComponent<RectTransform>(), camera, page.height);
                    if (Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin) > 1f &&
                        Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin) > 1f)
                        page.errors.Add("Sibling buttons overlap: " + siblings[a].name + " / " + siblings[b].name);
                }
            }
            foreach (DeparturePaintedHotspot feedback in host.GetComponentsInChildren<DeparturePaintedHotspot>(true))
                if (feedback.raycastTarget) page.errors.Add(HierarchyPath(feedback.transform, host.transform) + ": decorative hotspot feedback blocks pointer input.");
            foreach (Text text in host.GetComponentsInChildren<Text>(false).Where(text => text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text)))
            {
                bool inDeparture = text.transform.IsChildOf(routes) || text.transform.IsChildOf(setup);
                bool buttonCaption = text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null;
                bool keyLabel = text.name.StartsWith("Departure", StringComparison.Ordinal) || text.name.StartsWith("Treasure Departure", StringComparison.Ordinal) ||
                    text.name == "Setup Title" || text.name == "Chart Seed Validation" || text.name == "Chart Seed Warning Reason";
                // Captions render as independent Text graphics over the unlettered scene signs.
                bool visible = text.isActiveAndEnabled;
                bool check = visible && (buttonCaption || keyLabel);
                if (text.GetComponentInParent<InputField>() != null) check = false; // Seed inputs scroll long strings by design.
                int minimumSize = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
                TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(text.rectTransform.rect.width, 0f));
                settings.resizeTextForBestFit = false;
                settings.fontSize = minimumSize;
                float required = 0f;
                if (visible)
                    using (var generator = new TextGenerator()) required = generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                var label = new Label { path = HierarchyPath(text.transform, host.transform), text = text.text,
                    activeInHierarchy = text.gameObject.activeInHierarchy, graphicEnabled = text.enabled,
                    nominalFontSize = text.fontSize, minimumFontSize = minimumSize, minimumPixelFontSize = minimumSize * page.outputScale,
                    minimumRequiredHeight = required, availableHeight = text.rectTransform.rect.height,
                    fitsAtMinimumSize = required <= text.rectTransform.rect.height + 1f, checkedForClipping = check,
                    screenBounds = ScreenBounds(text.rectTransform, camera, page.height) };
                page.labels.Add(label);
                if (check && !label.fitsAtMinimumSize) page.errors.Add(label.path + ": text cannot fit even at minimum font size.");
                if (check && !InsideScreen(page, label.screenBounds)) page.errors.Add(label.path + ": text bounds extend outside screen.");
                if (check && inDeparture && !page.expectsIndependentBerth)
                {
                    Rect column = text.transform.IsChildOf(routes) ? page.routeColumn : page.setupColumn;
                    if (label.screenBounds.xMin < column.xMin - 1f || label.screenBounds.xMax > column.xMax + 1f)
                        page.errors.Add(label.path + ": text extends across its column boundary.");
                }
            }
        }

        private static bool IsLayeredShipControlPair(Selectable first, Selectable second)
        {
            Selectable above = first.name == "Inspect Departure Vessel" ? second :
                second.name == "Inspect Departure Vessel" ? first : null;
            return above != null && (above.name == "Departure Previous Vessel" || above.name == "Departure Next Vessel" ||
                above.name == "DepartureOrdinaryCrewDecreaseButton" || above.name == "DepartureOrdinaryCrewIncreaseButton");
        }

        private static void InspectPointerReachability(Page page, GameObject host, RectTransform routes, RectTransform setup, State state, Camera camera)
        {
            Image setupImage = setup.GetComponent<Image>();
            bool spansScene = setup.rect.width >= page.referenceResolution.x * .9f;
            page.setupRootDoesNotBlockRaycasts = !spansScene || setupImage == null || !setupImage.raycastTarget;
            if (page.expectsDockScene && !page.setupRootDoesNotBlockRaycasts)
                page.errors.Add("Full-scene Voyage Setup Image intercepts the original map-table controls.");
            Image routeImage = routes.GetComponent<Image>();
            if (page.expectsIndependentBerth && routeImage != null && routeImage.raycastTarget)
                page.errors.Add("Full-scene Route Selection Image intercepts the painted vessel and preparation controls.");

            var targetNames = new List<string>();
            if (state == State.InvalidChart) targetNames.Add("Close Chart Seed Warning");
            else if (state == State.BasicSupplies)
            {
                targetNames.AddRange(new[] { "Departure Basic Supplies Recommend", "Departure Basic Supplies Close", "Departure Basic Supplies Purchase" });
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    targetNames.Add("Departure Basic Supply Decrease " + key);
                    targetNames.Add("Departure Basic Supply Increase " + key);
                }
            }
            else if (state == State.Crew)
            {
                targetNames.AddRange(new[] { "Departure Ordinary Crew Slider", "Back To Voyage Setup", "Confirm Departure Crew" });
                for (int index = 0; index < 6; index++) targetNames.Add("Departure Special Crew Toggle " + index);
            }
            else
            {
                targetNames.AddRange(HeaderButtons);
                targetNames.AddRange(SetupButtons);
                targetNames.Add("Departure Treasure Seed Picker");
                if (state == State.Treasure) targetNames.AddRange(new[] { "Cancel Treasure Voyage", "Return To Treasure Table" });
                else
                {
                    targetNames.Add("Use Chart Seed");
                    if (state == State.Chart) targetNames.AddRange(new[] { "Seed Input", "Seed Input Dropdown" });
                    else
                    {
                        for (int index = 0; index < 6; index++) targetNames.Add("Departure Destination " + index);
                        for (int index = 0; index < 3; index++) targetNames.Add("Route " + index);
                        if (page.expectsIndependentBerth) targetNames.AddRange(PaintedRouteButtons);
                    }
                }
            }

            GraphicRaycaster[] raycasters = host.GetComponentsInChildren<GraphicRaycaster>(true)
                .Where(raycaster => raycaster.isActiveAndEnabled).ToArray();
            if (raycasters.Length == 0) { page.errors.Add("Production canvas has no active GraphicRaycaster."); return; }
            foreach (string name in targetNames.Distinct())
            {
                Selectable target = Find<Selectable>(host, name);
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                RectTransform targetRect = target.GetComponent<RectTransform>();
                Vector3 screen = camera.WorldToScreenPoint(targetRect.TransformPoint(targetRect.rect.center));
                var probe = new RaycastProbe { target = HierarchyPath(target.transform, host.transform), screenPoint = screen };
                var pointer = new PointerEventData(null) { position = screen };
                var hits = new List<RaycastResult>();
                foreach (GraphicRaycaster raycaster in raycasters) raycaster.Raycast(pointer, hits);
                RaycastResult first = hits.OrderByDescending(hit => hit.sortingLayer)
                    .ThenByDescending(hit => hit.sortingOrder).ThenByDescending(hit => hit.depth).ThenBy(hit => hit.distance).FirstOrDefault();
                probe.firstHit = first.gameObject == null ? string.Empty : HierarchyPath(first.gameObject.transform, host.transform);
                Selectable hitControl = first.gameObject == null ? null : first.gameObject.GetComponentInParent<Selectable>();
                probe.hitControl = hitControl == null ? string.Empty : HierarchyPath(hitControl.transform, host.transform);
                probe.reachesTarget = hitControl == target;
                for (Transform ancestor = target.transform.parent; ancestor != null && ancestor != host.transform; ancestor = ancestor.parent)
                {
                    Graphic graphic = ancestor.GetComponent<Graphic>();
                    if (graphic != null)
                        probe.graphicAncestors.Add(HierarchyPath(ancestor, host.transform) + " [raycast=" + graphic.raycastTarget + ", depth=" + graphic.depth + "]");
                    CanvasGroup group = ancestor.GetComponent<CanvasGroup>();
                    if (group != null)
                        probe.graphicAncestors.Add(HierarchyPath(ancestor, host.transform) + " [blocksRaycasts=" + group.blocksRaycasts + ", interactable=" + group.interactable + "]");
                }
                page.raycastProbes.Add(probe);
                if (!probe.reachesTarget)
                    page.errors.Add("Pointer center does not reach " + name + "; top graphic=" + probe.firstHit + "; selected control=" + probe.hitControl);
            }
        }

        private static T Require<T>(Page page, GameObject host, string name, bool expectedActive = true) where T : Component
        {
            T[] matches = host.GetComponentsInChildren<T>(true).Where(item => item.name == name).ToArray();
            if (matches.Length != 1) { page.errors.Add(name + ": expected one " + typeof(T).Name + ", found " + matches.Length); return null; }
            if (matches[0].gameObject.activeInHierarchy != expectedActive) page.errors.Add(name + ": unexpected active state; expected " + expectedActive);
            return matches[0];
        }

        private static T Find<T>(GameObject host, string name) where T : Component =>
            host.GetComponentsInChildren<T>(true).SingleOrDefault(item => item.name == name);

        private static void Invoke(CoreGameplayDemoController controller, string method, params object[] args)
        {
            MethodInfo target = typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (target == null) throw new MissingMethodException(method);
            target.Invoke(controller, args);
        }

        private static string HierarchyPath(Transform item, Transform root)
        {
            var names = new List<string>();
            while (item != null && item != root) { names.Add(item.name); item = item.parent; }
            names.Reverse();
            return string.Join("/", names);
        }

        private static Rect ScreenBounds(RectTransform rect, Camera camera, int height)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return ScreenBounds(corners, camera, height);
        }

        private static Rect ScreenBounds(Vector3[] corners, Camera camera, int height)
        {
            Vector3 min = camera.WorldToScreenPoint(corners[0]), max = min;
            foreach (Vector3 corner in corners) { Vector3 point = camera.WorldToScreenPoint(corner); min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
            return new Rect(min.x, height - max.y, max.x - min.x, max.y - min.y);
        }

        private static Rect VisibleHullScreenBounds(RawImage hull, Camera camera, int height)
        {
            Rect uv = hull.uvRect;
            if (Mathf.Abs(uv.width) < .000001f || Mathf.Abs(uv.height) < .000001f)
                throw new InvalidDataException("Vessel preview has an empty texture sample window.");
            Rect alpha = ReadSourceAlphaBounds(hull.texture, uv);
            Rect local = hull.rectTransform.rect;
            float firstX = local.xMin + local.width * (alpha.xMin - uv.x) / uv.width;
            float secondX = local.xMin + local.width * (alpha.xMax - uv.x) / uv.width;
            float firstY = local.yMin + local.height * (alpha.yMin - uv.y) / uv.height;
            float secondY = local.yMin + local.height * (alpha.yMax - uv.y) / uv.height;
            return ScreenBounds(new[] {
                hull.rectTransform.TransformPoint(new Vector3(firstX, firstY)),
                hull.rectTransform.TransformPoint(new Vector3(firstX, secondY)),
                hull.rectTransform.TransformPoint(new Vector3(secondX, secondY)),
                hull.rectTransform.TransformPoint(new Vector3(secondX, firstY))
            }, camera, height);
        }

        private static Rect ReadSourceAlphaBounds(Texture texture, Rect uv)
        {
            // Editor-only audit of the original PNG at alpha >= 16. Never make
            // imported production textures readable or read their runtime pixels.
            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Expected a source PNG for visible hull bounds: " + assetPath);
            if (!SourceAlphaBounds.TryGetValue(assetPath, out Dictionary<Rect, Rect> windows))
            {
                windows = new Dictionary<Rect, Rect>();
                SourceAlphaBounds.Add(assetPath, windows);
            }
            if (windows.TryGetValue(uv, out Rect cached)) return cached;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(path), false))
                    throw new InvalidDataException("Could not decode source hull PNG: " + assetPath);
                Color32[] pixels = source.GetPixels32();
                int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(uv.x, uv.x + uv.width) * source.width), 0, source.width);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(uv.x, uv.x + uv.width) * source.width), 0, source.width);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(uv.y, uv.y + uv.height) * source.height), 0, source.height);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(uv.y, uv.y + uv.height) * source.height), 0, source.height);
                int left = x1, right = x0, bottom = y1, top = y0;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                {
                    if (pixels[y * source.width + x].a < 16) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x + 1);
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y + 1);
                }
                if (right <= left || top <= bottom) throw new InvalidDataException("No visible alpha in hull sample: " + assetPath + " " + uv);
                Rect bounds = Rect.MinMaxRect(left / (float)source.width, bottom / (float)source.height,
                    right / (float)source.width, top / (float)source.height);
                windows.Add(uv, bounds);
                return bounds;
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static bool InsideScreen(Page page, Rect bounds) => bounds.xMin >= -1f && bounds.yMin >= -1f &&
            bounds.xMax <= page.width + 1f && bounds.yMax <= page.height + 1f;

        private static string RequireIsolatedProject()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(project, IsolatedProject, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This helper may run only in the disposable project: " + IsolatedProject);
            return project;
        }

        private static string RequiredEmptyOutput(string option)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, option);
            if (at < 0 || at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1])) throw new ArgumentException("An absolute " + option + " is required.");
            string output = Path.GetFullPath(args[at + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Capture output must be new or empty.");
            Directory.CreateDirectory(output);
            return output;
        }

        private static void SaveFrame(RenderTexture target, string path)
        {
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(false, false);
                Color32[] pixels = image.GetPixels32(); int low = 255, high = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    int value = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                    low = Math.Min(low, value); high = Math.Max(high, value);
                }
                if (high - low < 20) throw new InvalidOperationException("Screenshot is nearly uniform.");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
