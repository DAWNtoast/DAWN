// Disposable Editor capture. Copy only into an isolated project's Editor directory.
// Stages fresh in-memory preview data; never invokes gameplay or persistence callbacks.
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
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class NavigationEventMarkerCapture
    {
        private const int CaptureLayer = 30;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Serializable] private sealed class Report
        {
            public string status, unityVersion, projectPath, startedUtc, completedUtc;
            public string scope = "Actual production Navigation stopped-Editor preview at native 1920x1080 and 3840x2160 with its enabled CanvasScaler. Existing encounter definitions are staged in separated positions on a fresh in-memory session; nodes cover unvisited/visited art, a route boss, legendary pulse, and undiscovered intel glyph fallback. Only BuildMap and animation presentation callbacks are invoked. No gameplay choices, save methods or player data writes.";
            public List<Frame> frames = new List<Frame>();
            public List<string> errors = new List<string>();
        }

        [Serializable] private sealed class Frame
        {
            public string file;
            public int width, height;
            public float canvasScale;
            public bool nativeResolutionVerified, imageHasContent;
            public List<Marker> markers = new List<Marker>();
        }

        [Serializable] private sealed class Marker
        {
            public string state, encounterId, bossKind, glyph, cropFile;
            public int id, rootOutlineCount;
            public bool visited, discovered, intelRevealed, artVisible, glyphVisible, raycastTarget, interactable, pulsePresent;
            public float backgroundAlpha, afterPulseAlpha, afterDisableAlpha;
            public Vector2 hitArea;
            public Rect screenRect;
        }

        // -executeMethod ShatteredSail.Editor.NavigationEventMarkerCapture.CaptureFromCommandLine
        // -eventMarkerOutput D:/Game/Artifacts/NavigationEventMarker20261013/Screenshots
        public static void CaptureFromCommandLine()
        {
            Require(!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode, "Stopped Editor required.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            Require(project.StartsWith(@"D:\Game\tmp\", StringComparison.OrdinalIgnoreCase), "Use an isolated D:/Game/tmp project.");
            string requested = Argument("-eventMarkerOutput");
            Require(!string.IsNullOrWhiteSpace(requested) && Path.IsPathRooted(requested), "Absolute -eventMarkerOutput is required.");
            string output = Path.GetFullPath(requested);
            Require(output.StartsWith(@"D:\Game\Artifacts\", StringComparison.OrdinalIgnoreCase), "Output must be under D:/Game/Artifacts.");
            Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Use a new or empty output directory.");
            Directory.CreateDirectory(output);
            var report = new Report { status = "running", unityVersion = Application.unityVersion, projectPath = project,
                startedUtc = DateTime.UtcNow.ToString("O") };
            try
            {
                EnsurePipeline();
                foreach (int width in new[] { 1920, 3840 })
                {
                    var frame = new Frame { width = width, height = width * 9 / 16, file = "Navigation-" + width + ".png" };
                    report.frames.Add(frame);
                    try { Capture(output, frame); }
                    catch (Exception error) { report.errors.Add(width + ": " + error); }
                    Save(output, report);
                }
                report.status = report.errors.Count == 0 ? "passed" : "failed";
            }
            catch (Exception error) { report.status = "failed"; report.errors.Add(error.ToString()); }
            finally { report.completedUtc = DateTime.UtcNow.ToString("O"); Save(output, report); }
            Require(report.status == "passed", "Event marker capture failed; see report.json in " + output);
            Debug.Log("NAVIGATION_EVENT_MARKER_CAPTURE_OK: " + output);
        }

        private static void Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previousActive = RenderTexture.active;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Navigation Event Marker Audit") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                Require(controller.Session != null && controller.Session.Map != null, "Navigation preview session did not initialize.");
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                RectTransform mapRoot = (RectTransform)canvas.GetComponentsInChildren<Image>(true).Single(i => i.name == "Map Course Input").transform.parent;
                Dictionary<int, string> cases = StageNodes(controller.Session);
                for (int i = mapRoot.childCount - 1; i >= 0; i--)
                    if (mapRoot.GetChild(i).name != "Procedural Sea Chart") Object.DestroyImmediate(mapRoot.GetChild(i).gameObject);
                Invoke(controller, "BuildMap", mapRoot);

                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = CaptureLayer;
                var cameraHost = new GameObject("Navigation Event Marker Audit Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << CaptureLayer;
                camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Require(target.Create(), "Render target could not be created.");
                camera.targetTexture = target; camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                Require(scaler != null && scaler.enabled, "Production CanvasScaler must remain enabled.");
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                Require(handle != null, "CanvasScaler.Handle missing.");
                foreach (CoreMapNode node in controller.Session.Map.Nodes)
                {
                    Button marker = mapRoot.GetComponentsInChildren<Button>(true).Single(b => b.name == "Event Node " + node.Id);
                    var row = Inspect(marker, node, cases[node.Id]);
                    frame.markers.Add(row);
                }
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(false)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.canvasScale = canvas.scaleFactor;
                frame.nativeResolutionVerified = Mathf.Abs(canvas.renderingDisplaySize.x - frame.width) < .5f &&
                    Mathf.Abs(canvas.renderingDisplaySize.y - frame.height) < .5f &&
                    Mathf.Abs(canvas.pixelRect.width - frame.width) < .5f && Mathf.Abs(canvas.pixelRect.height - frame.height) < .5f;
                Require(frame.nativeResolutionVerified, "Render target did not drive native canvas dimensions.");
                RenderTexture.active = target;
                frame.imageHasContent = SavePixels(output, frame.file, new Rect(0, 0, frame.width, frame.height), frame.width, frame.height);
                Require(frame.imageHasContent, "Rendered image is blank.");
                foreach (Marker row in frame.markers)
                {
                    RectTransform rect = (RectTransform)mapRoot.Find("Event Node " + row.id);
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    Vector2[] points = corners.Select(p => (Vector2)camera.WorldToScreenPoint(p)).ToArray();
                    row.screenRect = Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
                    row.cropFile = row.state + "-" + frame.width + ".png";
                    float pad = 20 * canvas.scaleFactor;
                    SavePixels(output, row.cropFile, Rect.MinMaxRect(row.screenRect.xMin - pad, row.screenRect.yMin - pad,
                        row.screenRect.xMax + pad, row.screenRect.yMax + pad), frame.width, frame.height);
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static Dictionary<int, string> StageNodes(CoreVoyageSession session)
        {
            CoreEncounter ordinary = session.Map.Nodes.Select(n => n.Encounter).FirstOrDefault(e => e != null && e.HasChartLocation && e.BossKind == CoreBossKind.None && !e.Hidden);
            CoreEncounter hidden = session.Map.Nodes.Select(n => n.Encounter).FirstOrDefault(e => e != null && e.CanRevealByIntel);
            CoreEncounter routeBoss = session.Map.Nodes.Select(n => n.Encounter).FirstOrDefault(e => e != null && e.BossKind == CoreBossKind.Route && e.HasChartLocation);
            Require(ordinary != null, "No ordinary chart encounter available.");
            Require(hidden != null, "No hidden intel encounter available.");
            session.Map.Nodes.Clear();
            var cases = new Dictionary<int, string>();
            void Add(string state, CoreEncounter encounter, float x, float y, bool visited = false, bool discovered = true)
            {
                int id = 9101 + cases.Count;
                session.Map.Nodes.Add(new CoreMapNode { Id = id, Encounter = encounter, Position = new VoyagePoint(x, y),
                    Discovered = discovered, IntelRevealed = !discovered, Visited = visited, IsBoss = encounter.BossKind == CoreBossKind.Route });
                cases.Add(id, state);
            }
            Add("ordinary-unvisited", ordinary, 26, 24);
            Add("ordinary-visited", ordinary, 45, 24, visited: true);
            Add("legendary-pulse", CoreLegendaryWarships.All[0].CreateEncounter(), 64, 24);
            Add("undiscovered-intel-fallback", hidden, 45, 44, discovered: false);
            if (routeBoss != null) Add("route-boss", routeBoss, 78, 44);
            Require(session.Map.Nodes.All(n => n.IsChartVisible), "Staged encounter is not chart-visible.");
            return cases;
        }

        private static Marker Inspect(Button button, CoreMapNode node, string state)
        {
            Image frame = button.targetGraphic as Image;
            Require(frame != null, "Marker target Image missing.");
            RawImage art = button.GetComponentsInChildren<RawImage>(false).SingleOrDefault(i => i.name == "Event Icon " + node.Id);
            Text glyph = button.GetComponentInChildren<Text>();
            VoyageEventMarkerPulse pulse = button.GetComponent<VoyageEventMarkerPulse>();
            var row = new Marker { id = node.Id, state = state, encounterId = node.Encounter.Id, bossKind = node.Encounter.BossKind.ToString(),
                visited = node.Visited, discovered = node.Discovered, intelRevealed = node.IntelRevealed,
                backgroundAlpha = frame.color.a, rootOutlineCount = button.GetComponents<Outline>().Length,
                hitArea = ((RectTransform)button.transform).sizeDelta, raycastTarget = frame.raycastTarget,
                interactable = button.interactable, pulsePresent = pulse != null,
                artVisible = art != null && art.texture != null && art.color.a > 0,
                glyph = glyph != null ? glyph.text : "", glyphVisible = glyph != null && glyph.enabled && glyph.gameObject.activeInHierarchy && glyph.color.a > 0 };
            if (pulse != null)
            {
                Invoke(pulse, "Update"); row.afterPulseAlpha = frame.color.a;
                Invoke(pulse, "OnDisable"); row.afterDisableAlpha = frame.color.a;
                Invoke(pulse, "Update");
            }
            else row.afterPulseAlpha = row.afterDisableAlpha = frame.color.a;
            Require(row.rootOutlineCount == 0, state + ": marker frame Outline returned.");
            Require(row.raycastTarget && row.hitArea.x >= 36 && row.hitArea.y >= 36, state + ": hit area not retained.");
            Require(row.interactable == !node.Visited, state + ": interaction state changed.");
            Require(row.glyphVisible && !string.IsNullOrWhiteSpace(row.glyph), state + ": fallback/status glyph missing.");
            if (node.Visited) Require(row.glyph == "✓", "Visited checkmark missing.");
            if (node.Discovered)
            {
                Require(row.artVisible, state + ": artwork missing.");
                Require(row.backgroundAlpha == 0 && row.afterPulseAlpha == 0 && row.afterDisableAlpha == 0, state + ": art marker background not transparent.");
            }
            else Require(!row.artVisible && row.backgroundAlpha > 0, "Undiscovered intel fallback is not visible.");
            if (node.Encounter.BossKind == CoreBossKind.Legendary) Require(row.pulsePresent, "Legendary pulse missing.");
            return row;
        }

        private static void EnsurePipeline()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null || RenderPipelineManager.currentPipeline != null) return;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            MethodInfo method = typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline", flags, null, new[] { typeof(RenderPipelineAsset) }, null) ??
                typeof(RenderPipelineManager).GetMethod("PrepareRenderPipeline", flags, null, new[] { typeof(RenderPipelineAsset) }, null);
            Require(method != null, "SRP preparation entry missing.");
            object result = method.Invoke(null, new object[] { asset });
            Require(!(result is bool success) || success, "SRP preparation failed.");
            Require(RenderPipelineManager.currentPipeline != null, "SRP instance missing.");
        }

        private static bool SavePixels(string output, string file, Rect area, int targetWidth, int targetHeight)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(area.xMin), 0, targetWidth - 1), y = Mathf.Clamp(Mathf.FloorToInt(area.yMin), 0, targetHeight - 1);
            int width = Mathf.Clamp(Mathf.CeilToInt(area.xMax), x + 1, targetWidth) - x;
            int height = Mathf.Clamp(Mathf.CeilToInt(area.yMax), y + 1, targetHeight) - y;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(x, y, width, height), 0, 0, false); image.Apply(false, false);
                Color32[] pixels = image.GetPixels32(); int low = 255, high = 0;
                for (int i = 0; i < pixels.Length; i += Math.Max(1, pixels.Length / 10000))
                { low = Math.Min(low, Math.Min(pixels[i].r, Math.Min(pixels[i].g, pixels[i].b))); high = Math.Max(high, Math.Max(pixels[i].r, Math.Max(pixels[i].g, pixels[i].b))); }
                File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
                return high - low > 10;
            }
            finally { Object.DestroyImmediate(image); }
        }

        private static object Invoke(object instance, string method, params object[] args)
        {
            MethodInfo found = instance.GetType().GetMethod(method, PrivateInstance);
            Require(found != null, "Missing method " + method);
            return found.Invoke(instance, args);
        }
        private static string Argument(string key) { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Save(string output, Report report) => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
    }
}
