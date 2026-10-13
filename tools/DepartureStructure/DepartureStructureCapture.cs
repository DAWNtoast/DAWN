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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Disposable-project verification only. Copy to Assets/_Project/Editor in
    // IsolatedProject. Does not enter Play, invoke a purchase/start callback,
    // save a scene/asset, or deliberately write PlayerPrefs.
    public static class DepartureStructureCapture
    {
        private const string IsolatedProject = "D:/Game/tmp/departure-structure-20261012";
        private const int CaptureLayer = 31;
        private static readonly string[] PortNames = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
        private static readonly string[] PortSlugs = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };
        private static readonly string[] HeaderButtons = { "Return To Port Hub", "Previous Port Facility", "Next Port Facility", "Port Facility Settings" };
        private static readonly string[] SetupButtons = {
            "Departure Previous Vessel", "Departure Next Vessel", "Inspect Departure Vessel",
            "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton", "Departure Buy Provisions",
            "Adjust Voyage Supplies", "Configure Voyage Battle Decks", "Configure Voyage Crew", "Start Voyage"
        };
        private enum State { Ordinary, Chart, InvalidChart, Treasure, Crew, BasicSupplies }

        [Serializable] private sealed class Report
        {
            public string utc, project;
            public string scope = "Production non-Play previews in private PreviewScenes. Resolution captures reproduce the production CanvasScaler ScaleWithScreenSize/Expand uniform scale; no arbitrary UI stretch. No start/purchase callbacks or scene/asset saves.";
            public bool passed;
            public List<Page> pages = new List<Page>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Page
        {
            public string state, port, actualPort, file, sessionPhase, backgroundResource, backgroundAsset;
            public int width, height, treasureMapsBefore, treasureMapsAfter;
            public bool passed, completePreview, screenshotSaved, chartSelected, startInteractable;
            public string authoredRenderMode, scaleMode, screenMatchMode;
            public Vector2 referenceResolution, logicalCanvasSize;
            public float outputScale, cameraOrthographicSize;
            public Rect routeColumn, setupColumn;
            public List<Control> controls = new List<Control>();
            public List<Label> labels = new List<Label>();
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
            public bool fitsAtMinimumSize, checkedForClipping;
            public Rect screenBounds;
        }

        public static void CaptureFromCommandLine()
        {
            string project = RequireIsolatedProject();
            string output = RequiredEmptyOutput("-departureStructureCapturePath");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use stopped non-Play editor with graphics enabled; omit -nographics.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), project = project };
            try
            {
                foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                    Capture(report, output, port, State.Ordinary, 1920, 1080);
                foreach (CoreGameplayPreviewPort port in new[] { CoreGameplayPreviewPort.BrokenAnchor, CoreGameplayPreviewPort.WhiteShell })
                {
                    Capture(report, output, port, State.Ordinary, 1280, 720);
                    Capture(report, output, port, State.Ordinary, 2560, 1440);
                }
                foreach (State state in new[] { State.Chart, State.InvalidChart, State.Treasure, State.Crew, State.BasicSupplies })
                    Capture(report, output, CoreGameplayPreviewPort.BrokenAnchor, state, 1920, 1080);
                if (report.pages.Count != 15) report.errors.Add("Expected 15 captures.");
                report.passed = report.errors.Count == 0 && report.pages.All(page => page.passed);
            }
            finally
            {
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            if (!report.passed) throw new InvalidOperationException("Departure structure review failed; inspect report.json. Errors=" + report.errors.Count);
            Debug.Log("DEPARTURE_STRUCTURE_REVIEW: 15 production UI captures passed: six ports; 720p, 1080p, 1440p; chart, invalid chart, treasure, crew and supplies. " + output);
        }

        private static void Capture(Report report, string output, CoreGameplayPreviewPort port, State state, int width, int height)
        {
            var page = new Page { state = state.ToString(), port = PortNames[(int)port], width = width, height = height,
                file = PortSlugs[(int)port] + "_" + state.ToString().ToLowerInvariant() + "_" + width + "x" + height + ".png" };
            report.pages.Add(page);
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, previousTarget = RenderTexture.active;
            try
            {
                host = new GameObject("Departure Structure Review Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, port, 0);
                host.SetActive(true);
                ConfigureState(controller, state);
                page.actualPort = controller.Session.CurrentPort;
                page.completePreview = controller.HasCompleteEditorPreview;
                page.treasureMapsBefore = controller.Session.TreasureMaps.Count;
                if (!page.completePreview) page.errors.Add("Incomplete production departure preview.");
                if (page.actualPort != page.port) page.errors.Add("Unexpected origin port: " + page.actualPort);

                cameraHost = new GameObject("Departure Structure Review Camera", typeof(Camera));
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
                InspectPage(page, host, controller, state, camera);
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
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
            if (page.routeColumn.xMax > page.setupColumn.xMin + .5f) page.errors.Add("Route and setup columns overlap.");
            if (!InsideScreen(page, page.routeColumn) || !InsideScreen(page, page.setupColumn)) page.errors.Add("A departure column extends outside the screen.");
            foreach (string name in HeaderButtons) Require<Button>(page, host, name);
            Require<Button>(page, host, "Departure Treasure Seed Picker");
            Require<Text>(page, host, "Departure Route Summary");
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
            if (background != null)
            {
                page.backgroundAsset = AssetDatabase.GetAssetPath(background.texture);
                if (background.texture != Resources.Load<Texture2D>(page.backgroundResource) || background.color != Color.white)
                    page.errors.Add("Regional departure background texture/tint differs from the production catalog.");
            }
            InspectControlsAndLabels(page, host, routes, setup, camera);
        }

        private static void InspectControlsAndLabels(Page page, GameObject host, RectTransform routes, RectTransform setup, Camera camera)
        {
            Selectable[] controls = host.GetComponentsInChildren<Selectable>(false).Where(control => control.gameObject.activeInHierarchy).ToArray();
            foreach (Selectable control in controls)
            {
                Rect bounds = ScreenBounds(control.GetComponent<RectTransform>(), camera, page.height);
                Text label = control.GetComponentInChildren<Text>();
                Rect column = control.transform.IsChildOf(routes) ? page.routeColumn : control.transform.IsChildOf(setup) ? page.setupColumn : default;
                bool insideColumn = column.width <= 0 || bounds.xMin >= column.xMin - 1f && bounds.xMax <= column.xMax + 1f;
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
                    Rect first = ScreenBounds(siblings[a].GetComponent<RectTransform>(), camera, page.height);
                    Rect second = ScreenBounds(siblings[b].GetComponent<RectTransform>(), camera, page.height);
                    if (Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin) > 1f &&
                        Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin) > 1f)
                        page.errors.Add("Sibling buttons overlap: " + siblings[a].name + " / " + siblings[b].name);
                }
            }
            foreach (Text text in host.GetComponentsInChildren<Text>(false).Where(text => text.isActiveAndEnabled && !string.IsNullOrEmpty(text.text)))
            {
                bool inDeparture = text.transform.IsChildOf(routes) || text.transform.IsChildOf(setup);
                bool buttonCaption = text.transform.parent != null && text.transform.parent.GetComponent<Button>() != null;
                bool keyLabel = text.name.StartsWith("Departure", StringComparison.Ordinal) || text.name.StartsWith("Treasure Departure", StringComparison.Ordinal) ||
                    text.name == "Setup Title" || text.name == "Chart Seed Validation" || text.name == "Chart Seed Warning Reason";
                bool check = buttonCaption || keyLabel;
                if (text.GetComponentInParent<InputField>() != null) check = false; // Seed inputs scroll long strings by design.
                int minimumSize = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
                TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(text.rectTransform.rect.width, 0f));
                settings.resizeTextForBestFit = false;
                settings.fontSize = minimumSize;
                float required;
                using (var generator = new TextGenerator()) required = generator.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                var label = new Label { path = HierarchyPath(text.transform, host.transform), text = text.text,
                    nominalFontSize = text.fontSize, minimumFontSize = minimumSize, minimumPixelFontSize = minimumSize * page.outputScale,
                    minimumRequiredHeight = required, availableHeight = text.rectTransform.rect.height,
                    fitsAtMinimumSize = required <= text.rectTransform.rect.height + 1f, checkedForClipping = check,
                    screenBounds = ScreenBounds(text.rectTransform, camera, page.height) };
                page.labels.Add(label);
                if (check && !label.fitsAtMinimumSize) page.errors.Add(label.path + ": text cannot fit even at minimum font size.");
                if (check && !InsideScreen(page, label.screenBounds)) page.errors.Add(label.path + ": text bounds extend outside screen.");
                if (check && inDeparture)
                {
                    Rect column = text.transform.IsChildOf(routes) ? page.routeColumn : page.setupColumn;
                    if (label.screenBounds.xMin < column.xMin - 1f || label.screenBounds.xMax > column.xMax + 1f)
                        page.errors.Add(label.path + ": text extends across its column boundary.");
                }
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
            Vector3 min = camera.WorldToScreenPoint(corners[0]), max = min;
            foreach (Vector3 corner in corners) { Vector3 point = camera.WorldToScreenPoint(corner); min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
            return new Rect(min.x, height - max.y, max.x - min.x, max.y - min.y);
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
