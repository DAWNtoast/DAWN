// Read-only UI capture used for audits and for the UI refactor's before/after baselines.
// Never saves scenes/assets and never enters PlayMode; battle states are arranged on the
// disposable preview session only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
    public static class UiAuditReadOnlyCapture
    {
        private const string Source = "Assets/_Project/Scenes/UIPreviewsForMe";
        private const int Layer = 31;
        [Serializable] public sealed class MenuReport
        {
            public string scope = "Current MainMenuController, disposable Editor PreviewScenes, actual enabled CanvasScaler and ScreenSpaceCamera target. No manual canvas scale/size assignment. Accepted only if renderingDisplaySize, pixelRect and scaleFactor match requested target. Text geometry includes all active labels, including Home labels behind an open modal; it is not an occlusion-aware accessibility test.";
            public string capturedAtUtc;
            public List<MenuFrame> frames = new List<MenuFrame>();
        }
        [Serializable] public sealed class MenuFrame
        {
            public string state, file, error;
            public int width, height;
            public bool verifiedScaler;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect;
            public float scaleFactor, expectedScaleFactor;
            public string screenMatchMode;
            public List<TextGeometry> text = new List<TextGeometry>();
        }
        [Serializable] public sealed class TextGeometry
        {
            public string path, content;
            public bool meshAvailable, meshOutsideTarget, rectOutsideTarget;
            public Rect rectPixels, meshPixels;
            public int meshVertexCount, fontSize;
        }

        public static void CaptureAuthoredFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] top = Directory.GetFiles(Path.Combine(project, Source), "UI_*.unity", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            string[] references = top.Concat(new[] {"UIBattle/UIBattle_BoardingHook.unity", "UIBattle/UIBattle_BoardingLadder.unity", "UIBattle/UIBattle_BoardingRam.unity"}).ToArray();
            MethodInfo method = typeof(UIPreviewSceneAcceptance).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string), typeof(string), typeof(string[]) }, null);
            if (method == null) throw new MissingMethodException("UIPreviewSceneAcceptance.Capture(string,string,string[])");
            try { method.Invoke(null, new object[] { output, Source, references }); }
            catch (TargetInvocationException e) { throw e.InnerException ?? e; }
            Debug.Log("UI_AUDIT_AUTHORED_OK: " + references.Length + " frames at " + output);
        }

        public static void CaptureMenusFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Output must be new/empty: " + output);
            Directory.CreateDirectory(output);
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            int[,] resolutions = { {1920,1080}, {1280,720}, {1280,1024}, {3440,1440}, {3840,1080} };
            for (int i = 0; i < resolutions.GetLength(0); i++)
                foreach (string state in new[] {"Home", "Settings", "Achievements"})
                {
                    var frame = new MenuFrame { state = state, width = resolutions[i,0], height = resolutions[i,1] };
                    frame.file = state + "_" + frame.width + "x" + frame.height + ".png";
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
                }
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error)))
                throw new InvalidOperationException("Some menu targets were not verified. Inspect report.json; do not classify them as passed.");
            Debug.Log("UI_AUDIT_MENUS_OK: " + report.frames.Count + " frames at " + output);
        }

        public static void CaptureFreshShopFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be empty.");
            Directory.CreateDirectory(output);
            var frame = new MenuFrame { state = "ComponentShop", width = 1920, height = 1080, file = "ComponentShop_Fresh.png" };
            CaptureMenu(output, frame);
            File.WriteAllText(Path.Combine(output,"report.json"), JsonUtility.ToJson(frame,true));
        }

        public static void CaptureFreshScreensFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be empty.");
            Directory.CreateDirectory(output);
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            foreach (CoreGameplayPreviewScreen screen in Enum.GetValues(typeof(CoreGameplayPreviewScreen)))
                foreach (Vector2Int size in new[] {new Vector2Int(1920,1080),new Vector2Int(1280,720)})
                {
                    var frame = new MenuFrame { state = screen.ToString(), width = size.x, height = size.y,
                        file = screen + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                    File.WriteAllText(Path.Combine(output,"report.json"), JsonUtility.ToJson(report,true));
                }
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error))) throw new InvalidOperationException("Fresh UI capture failed; inspect report.");
        }

        /// <summary>
        /// UI refactor baseline: main menu states, every gameplay preview screen and the
        /// battle/forecast states at 1080p, 720p and 3440x1440, plus an index.html contact sheet.
        /// </summary>
        public static void CaptureRefactorBaselineFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be new/empty: " + output);
            Directory.CreateDirectory(output);
            var states = new List<string> { "Home", "Settings", "Achievements" };
            states.AddRange(Enum.GetNames(typeof(CoreGameplayPreviewScreen)));
            states.AddRange(new[] { "ForecastOpen", "NavalFar", "NavalClose", "Boarding" });
            Vector2Int[] sizes = { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(3440,1440) };
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            foreach (string state in states)
                foreach (Vector2Int size in sizes)
                {
                    var frame = new MenuFrame { state = state, width = size.x, height = size.y, file = state + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(output, "index.html"), ContactSheet(report, states, sizes), new UTF8Encoding(false));
            int failed = report.frames.Count(f => !string.IsNullOrEmpty(f.error));
            if (failed > 0) throw new InvalidOperationException(failed + " baseline frames failed; inspect report.json.");
            Debug.Log("UI_REFACTOR_BASELINE_OK: " + report.frames.Count + " frames at " + output);
        }

        // Body text below 18 reference units is the readability threshold from GameReadability.
        private const int SmallTextThreshold = 18;

        private static string ContactSheet(MenuReport report, List<string> states, Vector2Int[] sizes)
        {
            var html = new StringBuilder();
            html.Append("<!doctype html><meta charset=utf-8><title>UI Baseline</title><style>")
                .Append("body{background:#16120e;color:#eadcc3;font:14px system-ui,sans-serif;margin:24px}")
                .Append("section{margin:0 0 40px}h2{font-size:18px;margin:0 0 8px}.row{display:flex;gap:12px;flex-wrap:wrap}")
                .Append("figure{margin:0;width:31%;min-width:320px}img{width:100%;border:1px solid #4a3b2a}")
                .Append("figcaption{font-size:12px;color:#b9a888;margin-top:4px}.err{color:#ff8a7a;white-space:pre-wrap}</style>")
                .Append("<h1>UI refactor baseline</h1><p>Captured ").Append(report.capturedAtUtc)
                .Append(" · text sizes in 1080p reference units · small = below ").Append(SmallTextThreshold).Append("</p>");
            foreach (string state in states)
            {
                html.Append("<section id=\"").Append(state).Append("\"><h2>").Append(state).Append("</h2><div class=row>");
                foreach (Vector2Int size in sizes)
                {
                    MenuFrame frame = report.frames.First(f => f.state == state && f.width == size.x && f.height == size.y);
                    html.Append("<figure>");
                    if (string.IsNullOrEmpty(frame.error))
                    {
                        List<TextGeometry> visible = frame.text.Where(t => t.meshAvailable).ToList();
                        int smallest = visible.Count == 0 ? 0 : visible.Min(t => t.fontSize);
                        html.Append("<a href=\"").Append(frame.file).Append("\"><img loading=lazy src=\"").Append(frame.file).Append("\"></a>")
                            .Append("<figcaption>").Append(size.x).Append('×').Append(size.y)
                            .Append(" · labels ").Append(visible.Count)
                            .Append(" · smallest ").Append(smallest)
                            .Append(" · small ").Append(visible.Count(t => t.fontSize < SmallTextThreshold))
                            .Append(" · offscreen ").Append(visible.Count(t => t.meshOutsideTarget)).Append("</figcaption>");
                    }
                    else html.Append("<p class=err>").Append(size.x).Append('×').Append(size.y).Append(" failed\n").Append(WebUtility.HtmlEncode(frame.error)).Append("</p>");
                    html.Append("</figure>");
                }
                html.Append("</div></section>");
            }
            return html.ToString();
        }

        private static void CaptureMenu(string output, MenuFrame frame)
        {
            Scene scene = default;
            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable UI Audit Main Menu");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                bool battleState = frame.state == "NavalFar" || frame.state == "NavalClose" || frame.state == "Boarding";
                string mapped = frame.state == "ForecastOpen" ? "Navigation" : battleState ? "Battle" : frame.state;
                bool shop = Enum.TryParse(mapped, out CoreGameplayPreviewScreen screen);
                if (shop) host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                else host.AddComponent<MainMenuController>();
                host.SetActive(true);
                if (battleState)
                {
                    var controller = host.GetComponent<CoreGameplayDemoController>();
                    var battle = controller.Session.Battle;
                    battle.Stage = NavalBattleStage.Naval;
                    battle.DistanceMeters = frame.state == "NavalFar" ? 600 : 0;
                    battle.Alignment = NavalBattleAlignment.Parallel;
                    if (frame.state == "Boarding" && !controller.Session.TryBoard()) throw new InvalidOperationException("Boarding preview failed");
                    battle.PlayerBlock = 8; battle.EnemyBlock = 6;
                    typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                }
                if (frame.state == "ForecastOpen")
                    host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Forecast Details").onClick.Invoke();
                if (!shop && frame.state == "Achievements")
                    host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements").onClick.Invoke();
                string canvasName = shop ? CoreGameplayDemoController.EditorPreviewCanvasName : MainMenuController.EditorPreviewCanvasName;
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == canvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unexpectedly disabled.");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Settings Panel") child.gameObject.SetActive(frame.state == "Settings");
                    if (child.name == "Achievements Panel") child.gameObject.SetActive(frame.state == "Achievements");
                    if (child.name == "New Game Confirmation") child.gameObject.SetActive(false);
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
        private static string PathOf(Transform item, Transform root)
        {
            var names = new List<string>(); for (Transform t=item;t!=null && t!=root;t=t.parent) names.Insert(0,t.name); return string.Join("/",names);
        }
        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2[] p=points.ToArray(); return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
        }
        private static bool Outside(Rect r,int width,int height) { return r.xMin < -0.5f || r.yMin < -0.5f || r.xMax > width+0.5f || r.yMax > height+0.5f; }
        private static void RequireEditor()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Graphics required; omit -nographics.");
        }
        private static string Output()
        {
            string[] args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-uiAuditPath");
            if(index<0 || index+1>=args.Length) throw new ArgumentException("Pass -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR");
            if(!Path.IsPathRooted(args[index+1]))throw new ArgumentException("Absolute output required.");
            return Path.GetFullPath(args[index+1]);
        }
    }
}
