// Editor-only review entry. Uses disposable in-memory scenes and never saves scene or asset files.
// Generic GrayGull navigation/encounter/battle frames call the production audit by reflection.
// Other ports and boarding reuse that audit's renderer with the same enabled CanvasScaler checks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using MenuFrame = ShatteredSail.Editor.UiAuditReadOnlyCapture.MenuFrame;
using TextGeometry = ShatteredSail.Editor.UiAuditReadOnlyCapture.TextGeometry;

namespace ShatteredSail.Editor
{
    public static class HeaderRedesignCapture20261014
    {
        private const int Layer = 31;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static ReviewCase activeCase;

        [Serializable] private sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string scope = "Current production headers in disposable in-memory Editor scenes. Six ports, representative facilities, voyage, encounter, battle and boarding at 1920x1080, 1280x720 and 3440x1440. No persisted scenes, PlayerPrefs or gameplay actions are saved.";
            public int expectedCount, successfulCount;
            public List<ReviewCase> cases = new List<ReviewCase>();
        }

        [Serializable] private sealed class ReviewCase
        {
            public string port, label;
            public bool boarding, tutorial, usesProductionCapture;
            public Rect headerPixels;
            public List<string> geometryWarnings = new List<string>();
            public MenuFrame frame;
            [NonSerialized] public CoreGameplayPreviewPort previewPort;
            [NonSerialized] public CoreGameplayPreviewScreen screen;
        }

        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-headerRedesignCapturePath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass -headerRedesignCapturePath NEW_ABSOLUTE_ARTIFACTS_DIRECTORY.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Capture directory must be below Artifacts.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Preserve prior captures: choose a new or empty output directory.");
            Directory.CreateDirectory(output);
            MethodInfo productionCapture = typeof(UiAuditReadOnlyCapture).GetMethod("CaptureMenu", BindingFlags.Static | BindingFlags.NonPublic);
            if (productionCapture == null) throw new MissingMethodException("UiAuditReadOnlyCapture.CaptureMenu");
            var report = new Report();
            report.cases.AddRange(Cases());
            report.expectedCount = report.cases.Count;
            foreach (ReviewCase item in report.cases)
            {
                activeCase = item;
                try
                {
                    if (item.usesProductionCapture)
                        productionCapture.Invoke(null, new object[] { output, item.frame });
                    else
                        CaptureCustom(output, item.frame, item);
                    if (!item.frame.verifiedScaler) throw new InvalidOperationException("Production scaler was not verified.");
                    InspectRecordedHeaderText(item);
                    report.successfulCount++;
                }
                catch (Exception exception)
                {
                    item.frame.error = (exception is TargetInvocationException invocation && invocation.InnerException != null
                        ? invocation.InnerException : exception).ToString();
                    Debug.LogError(item.frame.file + ": " + item.frame.error);
                }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
            activeCase = null;
            WriteGallery(output, report);
            if (report.successfulCount != report.expectedCount)
                throw new InvalidOperationException("Header capture incomplete: " + report.successfulCount + "/" + report.expectedCount + ". Inspect report.json.");
            Debug.Log("HEADER_REDESIGN_CAPTURE_OK: " + report.successfulCount + " production frames at " + output);
        }

        private static IEnumerable<ReviewCase> Cases()
        {
            Vector2Int[] sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(3440, 1440) };
            CoreGameplayPreviewScreen[] facility = { CoreGameplayPreviewScreen.Warehouse, CoreGameplayPreviewScreen.Market,
                CoreGameplayPreviewScreen.ComponentShop, CoreGameplayPreviewScreen.Maintenance,
                CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewScreen.Tavern };
            foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
            {
                foreach (Vector2Int size in sizes) yield return Make(port, CoreGameplayPreviewScreen.PortHub, size);
                yield return Make(port, facility[(int)port], sizes[0]);
            }
            foreach (CoreGameplayPreviewScreen screen in new[] { CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewScreen.ComponentShop })
                foreach (Vector2Int size in sizes) yield return Make(CoreGameplayPreviewPort.BrokenAnchor, screen, size);
            foreach (CoreGameplayPreviewScreen screen in new[] { CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewScreen.Encounter, CoreGameplayPreviewScreen.Battle })
                foreach (Vector2Int size in sizes)
                {
                    ReviewCase item = Make(CoreGameplayPreviewPort.GrayGull, screen, size);
                    item.usesProductionCapture = true;
                    yield return item;
                }
            foreach (Vector2Int size in sizes) yield return Make(CoreGameplayPreviewPort.GrayGull, CoreGameplayPreviewScreen.Battle, size, boarding: true);
            yield return Make(CoreGameplayPreviewPort.GrayGull, CoreGameplayPreviewScreen.Navigation, sizes[1], tutorial: true);
            yield return Make(CoreGameplayPreviewPort.GrayGull, CoreGameplayPreviewScreen.Battle, sizes[1], tutorial: true);
        }

        private static ReviewCase Make(CoreGameplayPreviewPort port, CoreGameplayPreviewScreen screen, Vector2Int size,
            bool boarding = false, bool tutorial = false)
        {
            string label = boarding ? "Boarding" : screen.ToString();
            if (tutorial) label += "_TutorialBand";
            return new ReviewCase { previewPort = port, port = port.ToString(), screen = screen, label = label,
                boarding = boarding, tutorial = tutorial, frame = new MenuFrame { state = screen.ToString(),
                    width = size.x, height = size.y, file = port + "_" + label + "_" + size.x + "x" + size.y + ".png" } };
        }

        private static void ArrangeFixture(CoreGameplayDemoController controller, ReviewCase item)
        {
            if (item.boarding)
            {
                CoreVoyageSession session = controller.Session;
                if (session == null || session.Battle == null) throw new InvalidOperationException("Production battle fixture is missing.");
                typeof(CoreVoyageSession).GetMethod("EnterBattleStage", PrivateInstance)
                    .Invoke(session, new object[] { NavalBattleStage.Boarding });
                CoreBattleState battle = session.Battle;
                battle.DistanceMeters = 0f;
                battle.Alignment = NavalBattleAlignment.Parallel;
                battle.BoardingApproach = NavalBoardingApproach.Alongside;
                battle.PlannedBoardingApproach = battle.BoardingApproach;
                battle.BoardingConnection = NavalBoardingConnection.Planned;
                battle.PlayerMovingTowardEnemy = battle.EnemyMovingTowardPlayer = false;
                battle.LastAction = "接舷界面样本";
            }
            if (item.tutorial) typeof(CoreGameplayDemoController).GetField("tutorialActive", PrivateInstance).SetValue(controller, true);
            if (item.boarding || item.tutorial)
                typeof(CoreGameplayDemoController).GetMethod("Render", PrivateInstance).Invoke(controller, null);
        }

        private static void CaptureCustom(string output, MenuFrame frame, ReviewCase item)
        {
            Scene scene = default;
            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable Header Redesign Review");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(item.screen, item.previewPort, 0);
                host.SetActive(true);
                ArrangeFixture(controller, item);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unexpectedly disabled.");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Settings Panel") child.gameObject.SetActive(frame.state == "Settings");
                    if (child.name == "Achievements Panel") child.gameObject.SetActive(frame.state == "Achievements");
                    if (child.name == "New Game Confirmation") child.gameObject.SetActive(frame.state == "NewGameConfirm");
                    child.gameObject.layer = Layer;
                }
                GameObject cameraHost = new GameObject("Disposable UI Audit Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0,0,-1000);
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                rt = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create();
                camera.targetTexture = rt;
                camera.rect = new Rect(0,0,1,1);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                // No assignment to canvas.scaleFactor, RectTransform size or localScale.
                // Calling the original scaler Handle preserves production calculations.
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                    else camera.Render();
                }
                frame.renderingDisplaySize = canvas.renderingDisplaySize;
                frame.canvasPixelRect = canvas.pixelRect;
                frame.cameraPixelRect = camera.pixelRect;
                frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
                frame.scaleFactor = canvas.scaleFactor;
                frame.screenMatchMode = scaler.screenMatchMode.ToString();
                float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr,hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr,hr) :
                    Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr,2), Mathf.Log(hr,2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled &&
                    Mathf.Abs(frame.renderingDisplaySize.x-frame.width)<0.5f && Mathf.Abs(frame.renderingDisplaySize.y-frame.height)<0.5f &&
                    Mathf.Abs(frame.canvasPixelRect.width-frame.width)<0.5f && Mathf.Abs(frame.canvasPixelRect.height-frame.height)<0.5f &&
                    Mathf.Abs(frame.cameraPixelRect.width-frame.width)<0.5f && Mathf.Abs(frame.cameraPixelRect.height-frame.height)<0.5f &&
                    Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<0.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-frame.width/frame.scaleFactor)<1f &&
                    Mathf.Abs(frame.logicalCanvasSize.y-frame.height/frame.scaleFactor)<1f;
                if (!frame.verifiedScaler)
                    throw new InvalidOperationException("Target texture did not drive actual scaler/display geometry. Refuse false multi-resolution capture. Read recorded display/scale fields and use fixed Game View instead.");
                foreach (Text label in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (!label.enabled || string.IsNullOrEmpty(label.text)) continue;
                    var geometry = new TextGeometry { path = PathOf(label.transform, canvas.transform), content = label.text, fontSize = label.fontSize };
                    var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                    geometry.rectPixels = Bounds(corners.Select(v => (Vector2)camera.WorldToScreenPoint(v)));
                    geometry.rectOutsideTarget = Outside(geometry.rectPixels, frame.width, frame.height);
                    Mesh mesh = label.canvasRenderer.GetMesh();
                    try
                    {
                        geometry.meshVertexCount = mesh == null ? 0 : mesh.vertexCount;
                        geometry.meshAvailable = mesh != null && mesh.vertexCount > 0;
                        if (geometry.meshAvailable)
                        {
                            geometry.meshPixels = Bounds(mesh.vertices.Select(v => (Vector2)camera.WorldToScreenPoint(label.transform.TransformPoint(v))));
                            geometry.meshOutsideTarget = Outside(geometry.meshPixels, frame.width, frame.height);
                        }
                    }
                    finally { /* CanvasRenderer owns the returned mesh. */ }
                    frame.text.Add(geometry);
                }
                InspectHeader(canvas, frame, item);
                RenderTexture.active = rt;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0,0,frame.width,frame.height), 0,0);
                pixels.Apply(false,false);
                Color32[] samples = pixels.GetPixels32(); int low=255,high=0;
                for (int i=0;i<samples.Length;i+=997) { int b=(samples[i].r+samples[i].g+samples[i].b)/3; low=Math.Min(low,b); high=Math.Max(high,b); }
                if (high-low<20) throw new InvalidOperationException("Nearly blank frame; render not accepted.");
                File.WriteAllBytes(Path.Combine(output,frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }

        private static void InspectHeader(Canvas canvas, MenuFrame frame, ReviewCase item)
        {
            RectTransform header = canvas.GetComponentsInChildren<RectTransform>(false).SingleOrDefault(rect =>
                rect.name == "Voyage Header" || rect.name == "Port Facility Header" || rect.name == "Port Hub");
            if (header == null) throw new InvalidOperationException("Missing active production header.");
            Vector3[] corners = new Vector3[4];
            header.GetWorldCorners(corners);
            item.headerPixels = Bounds(corners.Select(point => (Vector2)canvas.worldCamera.WorldToScreenPoint(point)));
            float expectedHeight = PhaseSurfaceLayout.GetHeaderHeight(item.tutorial);
            if (Mathf.Abs(header.rect.height - expectedHeight) > .1f)
                throw new InvalidOperationException("Unexpected header height: " + header.rect.height + " instead of " + expectedHeight);
            foreach (Image image in header.GetComponentsInChildren<Image>(false))
                if (image.enabled && image.name.EndsWith("Frame", StringComparison.Ordinal) && image.raycastTarget)
                    throw new InvalidOperationException("Decorative frame intercepts input: " + image.name);
        }

        private static void InspectRecordedHeaderText(ReviewCase item)
        {
            TextGeometry[] text = item.frame.text.Where(entry => IsHeader(entry.path)).ToArray();
            if (text.Length == 0) throw new InvalidOperationException("No visible header text was captured.");
            foreach (TextGeometry entry in text)
            {
                if (entry.rectOutsideTarget || entry.meshOutsideTarget)
                    item.geometryWarnings.Add("Outside render target: " + entry.path);
                if (!entry.meshAvailable) item.geometryWarnings.Add("No rendered glyphs: " + entry.path);
            }
        }

        private static bool IsHeader(string path) => path.Contains("/Voyage Header/") ||
            path.Contains("/Port Facility Header/") || path.Contains("/Port Hub/") ||
            path.Contains("/Battle Readouts Overlay/");

        private static string Escape(string text) => System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
        private static void WriteGallery(string output, Report report)
        {
            var html = new System.Text.StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>Header review</title><style>body{margin:28px;background:#111a21;color:#f4e6cb;font:16px system-ui}article{margin:24px 0;padding:16px;background:#1c2932}img{display:block;width:100%;height:auto}h1{font-size:28px}h2{font-size:18px}p{color:#c9bda8}a{color:#edc97d}</style><h1>港口、航行与战斗 Header</h1><p>生产代码内存预览 / ");
            html.Append(report.successfulCount).Append(" / ").Append(report.expectedCount).Append(" verified captures</p>");
            foreach (ReviewCase item in report.cases)
            {
                html.Append("<article><h2>").Append(Escape(item.port + " · " + item.label + " · " +
                    item.frame.width + "×" + item.frame.height)).Append("</h2>");
                if (string.IsNullOrEmpty(item.frame.error)) html.Append("<a href=\"").Append(Escape(item.frame.file)).Append("\"><img src=\"")
                    .Append(Escape(item.frame.file)).Append("\" loading=\"lazy\"></a>");
                else html.Append("<p>").Append(Escape(item.frame.error)).Append("</p>");
                foreach (string warning in item.geometryWarnings) html.Append("<p>").Append(Escape(warning)).Append("</p>");
                html.Append("</article>");
            }
            html.Append("</html>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new System.Text.UTF8Encoding(false));
        }

        private static string PathOf(Transform item, Transform root)
        {
            var names = new List<string>();
            for (Transform cursor = item; cursor != null && cursor != root; cursor = cursor.parent) names.Insert(0, cursor.name);
            return string.Join("/", names);
        }
        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2[] values = points.ToArray();
            return Rect.MinMaxRect(values.Min(v => v.x), values.Min(v => v.y), values.Max(v => v.x), values.Max(v => v.y));
        }
        private static bool Outside(Rect rect, int width, int height) =>
            rect.xMin < -.5f || rect.yMin < -.5f || rect.xMax > width + .5f || rect.yMax > height + .5f;
    }
}