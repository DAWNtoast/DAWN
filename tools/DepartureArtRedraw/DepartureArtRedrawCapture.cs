using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    // Copy only to the disposable project's Assets/_Project/Editor directory.
    // Builds private non-Play PreviewScenes; never saves scenes, assets or PlayerPrefs.
    public static class DepartureArtRedrawCapture
    {
        private const int Width = 1920, Height = 1080, Layer = 31;
        private const string IsolatedProject = "D:/Game/tmp/departure-art-redraw-20261012";
        private const string ResourceRoot = "Assets/_Project/Resources/";
        private static readonly string[] PortNames = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
        private static readonly string[] PortSlugs = { "broken_anchor", "white_shell", "bronze_bell", "salt_crown", "twilight_tide", "gray_gull" };
        private static readonly string[] RouteResources = {
            "LegacyDemo/shattered_sail_route_map_v2", "LegacyDemo/golden_tide_route_map_v2", "LegacyDemo/moon_tide_route_map_v2"
        };
        private static readonly string[] MainButtons = {
            "Return To Port Hub", "Previous Port Facility", "Next Port Facility", "Port Facility Settings",
            "Departure Treasure Seed Picker", "Departure Previous Vessel", "Departure Next Vessel", "Inspect Departure Vessel",
            "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton", "Departure Buy Provisions",
            "Adjust Voyage Supplies", "Configure Voyage Battle Decks", "Configure Voyage Crew", "Start Voyage"
        };

        [Serializable] private sealed class Report
        {
            public string utc, project, scope = "Six regional departure pages built by ConfigureEditorPreview in non-Play private PreviewScenes. No source scene/assets/PlayerPrefs writes.";
            public int width = Width, height = Height;
            public bool passed;
            public List<Page> pages = new List<Page>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Page
        {
            public string previewPort, expectedPort, actualPort, file, catalogResourcePath;
            public bool completePreview, screenshotSaved, passed;
            public Art background;
            public List<Art> routePreviews = new List<Art>();
            public List<Control> controls = new List<Control>();
            public List<string> errors = new List<string>();
        }
        [Serializable] private sealed class Art
        {
            public string node, expectedResourcePath, expectedAssetPath, actualAssetPath, sha256, format, graphicsFormat, filterMode, wrapMode;
            public int sourceWidth, sourceHeight, width, height, mipmapCount;
            public bool present, active, exactAssetMatch, colorWhite, readable, passed;
            public Color color;
            public Rect uv, screenBounds;
        }
        [Serializable] private sealed class Control
        {
            public string node, type, label;
            public bool present, active, enabled, interactable, passed;
            public Rect screenBounds;
        }

        public static void CaptureFromCommandLine()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(project, IsolatedProject, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This helper may run only in the disposable project: " + IsolatedProject);
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-departureArtCapturePath");
            if (at < 0 || at + 1 >= args.Length || !Path.IsPathRooted(args[at + 1]))
                throw new ArgumentException("An absolute -departureArtCapturePath is required.");
            string output = Path.GetFullPath(args[at + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Capture output must be new or empty.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Use stopped non-Play editor with graphics enabled; omit -nographics.");
            Directory.CreateDirectory(output);
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), project = project };
            try
            {
                foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                {
                    int index = (int)port;
                    var page = new Page { previewPort = port.ToString(), expectedPort = PortNames[index], file = "departure_" + PortSlugs[index] + ".png" };
                    report.pages.Add(page);
                    try { CapturePage(page, port, index, output); }
                    catch (Exception error) { page.errors.Add(error.ToString()); Debug.LogException(error); }
                    page.passed = page.errors.Count == 0 && page.completePreview && page.screenshotSaved &&
                        page.background != null && page.background.passed && page.routePreviews.Count == 3 &&
                        page.routePreviews.All(item => item.passed) && page.controls.Count == MainButtons.Length + 10 &&
                        page.controls.All(item => item.passed);
                    if (!page.passed && page.errors.Count == 0) page.errors.Add("Page did not satisfy all capture invariants.");
                    foreach (string error in page.errors) report.errors.Add(port + ": " + error);
                }
                if (report.pages.Count != 6) report.errors.Add("Expected exactly six regional pages.");
                if (report.pages.Select(page => page.actualPort).Distinct().Count() != 6)
                    report.errors.Add("The six previews did not resolve to six distinct ports.");
                report.passed = report.errors.Count == 0 && report.pages.All(page => page.passed);
            }
            finally
            {
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            if (!report.passed) throw new InvalidOperationException("Departure art review failed; inspect report.json. Errors=" + report.errors.Count);
            Debug.Log("DEPARTURE_ART_REDRAW_REVIEW: six 1920x1080 pages, six regional backgrounds, 18 route preview bindings and 150 controls passed. " + output);
        }

        private static void CapturePage(Page page, CoreGameplayPreviewPort port, int portIndex, string output)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, cameraHost = null;
            RenderTexture target = null, previousTarget = RenderTexture.active;
            try
            {
                host = new GameObject("Departure Art Review Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, port, 0);
                host.SetActive(true);
                page.actualPort = controller.Session.CurrentPort;
                page.completePreview = controller.HasCompleteEditorPreview;
                if (!page.completePreview) page.errors.Add("Incomplete production VoyageDeparture preview.");
                if (page.actualPort != page.expectedPort) page.errors.Add("Wrong current port: " + page.actualPort + "; expected " + page.expectedPort);

                cameraHost = new GameObject("Departure Art Review Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .06f, .075f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Height / 2f;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << Layer;
                camera.scene = scene;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;

                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var rect = canvas.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(Width, Height);
                rect.position = Vector3.zero;
                rect.localScale = Vector3.one;
                foreach (Transform item in canvas.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = Layer;
                Canvas.ForceUpdateCanvases();

                string expectedBackground = "PortFacilities/Regional/Voyage/voyage_" + PortSlugs[portIndex] + "_v1";
                page.catalogResourcePath = PortFacilityArtCatalog.RegionalBackgroundResourcePath(PortFacilityArtCatalog.VoyageFacilityIndex, page.actualPort);
                if (page.catalogResourcePath != expectedBackground) page.errors.Add("Regional catalog returned wrong resource path: " + page.catalogResourcePath);
                page.background = InspectArt(page, host, "Voyage Background", expectedBackground, camera);
                for (int index = 0; index < RouteResources.Length; index++)
                    page.routePreviews.Add(InspectArt(page, host, "Route Preview " + index, RouteResources[index], camera));
                foreach (string name in MainButtons) InspectControl<Button>(page, host, name, camera);
                for (int index = 0; index < 6; index++) InspectControl<Button>(page, host, "Departure Destination " + index, camera);
                for (int index = 0; index < 3; index++) InspectControl<Button>(page, host, "Route " + index, camera);
                InspectControl<Toggle>(page, host, "Use Chart Seed", camera);

                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                SaveFrame(target, Path.Combine(output, page.file));
                page.screenshotSaved = File.Exists(Path.Combine(output, page.file));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Art InspectArt(Page page, GameObject host, string name, string expectedResource, Camera camera)
        {
            var item = new Art { node = name, expectedResourcePath = expectedResource, expectedAssetPath = ResourceRoot + expectedResource + ".png" };
            RawImage[] matches = host.GetComponentsInChildren<RawImage>(true).Where(image => image.name == name).ToArray();
            if (matches.Length != 1) { page.errors.Add(name + ": expected exactly one RawImage, found " + matches.Length); return item; }
            RawImage image = matches[0];
            item.present = true; item.active = image.isActiveAndEnabled;
            item.color = image.color; item.colorWhite = image.color == Color.white;
            item.uv = image.uvRect; item.screenBounds = ScreenBounds(image.rectTransform, camera);
            Texture2D texture = image.texture as Texture2D;
            if (texture == null) { page.errors.Add(name + ": texture missing"); return item; }
            item.actualAssetPath = AssetDatabase.GetAssetPath(texture);
            item.exactAssetMatch = item.actualAssetPath == item.expectedAssetPath && texture == Resources.Load<Texture2D>(expectedResource);
            item.width = texture.width; item.height = texture.height;
            item.format = texture.format.ToString(); item.graphicsFormat = texture.graphicsFormat.ToString();
            item.filterMode = texture.filterMode.ToString(); item.wrapMode = texture.wrapMode.ToString();
            item.mipmapCount = texture.mipmapCount; item.readable = texture.isReadable;
            var importer = AssetImporter.GetAtPath(item.actualAssetPath) as TextureImporter;
            if (importer != null) importer.GetSourceTextureWidthAndHeight(out item.sourceWidth, out item.sourceHeight);
            if (!string.IsNullOrEmpty(item.actualAssetPath) && File.Exists(item.actualAssetPath))
                using (var stream = File.OpenRead(item.actualAssetPath))
                using (var sha = SHA256.Create()) item.sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            item.passed = item.active && item.exactAssetMatch && item.colorWhite && item.width > 0 && item.height > 0 && importer != null;
            if (!item.active) page.errors.Add(name + ": artwork is inactive");
            if (!item.exactAssetMatch) page.errors.Add(name + ": wrong texture " + item.actualAssetPath + "; expected " + item.expectedAssetPath);
            if (!item.colorWhite) page.errors.Add(name + ": artwork has a non-white tint " + item.color);
            if (importer == null) page.errors.Add(name + ": missing texture importer");
            return item;
        }

        private static void InspectControl<T>(Page page, GameObject host, string name, Camera camera) where T : Selectable
        {
            var item = new Control { node = name, type = typeof(T).Name };
            page.controls.Add(item);
            T[] matches = host.GetComponentsInChildren<T>(true).Where(control => control.name == name).ToArray();
            if (matches.Length != 1) { page.errors.Add(name + ": expected exactly one " + item.type + ", found " + matches.Length); return; }
            T control = matches[0];
            item.present = true; item.active = control.gameObject.activeInHierarchy; item.enabled = control.enabled;
            item.interactable = control.IsInteractable();
            Text label = control.GetComponentInChildren<Text>(true);
            item.label = label != null ? label.text : string.Empty;
            item.screenBounds = ScreenBounds(control.GetComponent<RectTransform>(), camera);
            // Locked routes/current-port destinations/one-owned-ship arrows may intentionally be disabled.
            // Record interactability without clicking or weakening production gates.
            item.passed = item.active && item.enabled && item.screenBounds.width > 0 && item.screenBounds.height > 0;
            if (!item.passed) page.errors.Add(name + ": required entry is inactive, disabled or has empty bounds");
        }

        private static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3 a = camera.WorldToScreenPoint(corners[0]), b = camera.WorldToScreenPoint(corners[2]);
            return new Rect(a.x, Height - b.y, b.x - a.x, b.y - a.y);
        }

        private static void SaveFrame(RenderTexture target, string path)
        {
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply(false, false);
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
