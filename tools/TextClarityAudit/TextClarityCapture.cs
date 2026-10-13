// Disposable Editor capture; copy into the isolated project's Editor directory.
// Uses production preview constructors and actual CanvasScaler targets; no saved scenes/assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public static class TextClarityCapture
    {
        private const int Layer = 30;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] DefaultStates = { "Home", "Settings", "PortHub", "PortSettings", "Navigation", "Settlement" };

        [Serializable] private sealed class Report
        {
            public string variant, startedUtc, completedUtc, unityVersion, projectPath, status;
            public string scope = "Actual production stopped-Editor preview UI at 1920x1080/3840x2160, ScreenSpaceCamera and enabled production CanvasScaler. No resolution simulation or post-capture resampling. All active nonempty Text components are measured, including labels behind modals; visibility is not an occlusion-aware test. Fractional glyph coordinates are normal for proportional, curved and rotated text; alignment residuals are diagnostic, not failure criteria. No gameplay, save, preference-writing or settings-apply callbacks are invoked; settings previews may read existing preferences.";
            public int frameCount, textCount, sampledTextCount;
            public List<Frame> frames = new List<Frame>();
            public List<string> errors = new List<string>();
        }

        [Serializable] private sealed class Frame
        {
            public string state, file, error;
            public int width, height, hostScreenWidth, hostScreenHeight;
            public float canvasScale, expectedCanvasScale;
            public bool scalerVerified, canvasPixelPerfect, imageHasContent;
            public Vector2 referenceResolution, renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect;
            public List<TextRow> texts = new List<TextRow>();
        }

        [Serializable] private sealed class TextRow
        {
            public string path, content, fontName, fontAsset, fontStyle, alignment, horizontalOverflow, verticalOverflow, cropFile;
            public string atlasFilterMode, rasterDensitySource;
            public string[] meshEffects;
            public int fontSize, bestFitSize, atlasWidth, atlasHeight, meshVertexCount;
            public bool bestFitEnabled, dynamicFont, richText, rendererCulled, meshAvailable, rectOutsideTarget, meshOutsideTarget, insideButton;
            public float textPixelsPerUnit, renderedPixelsPerUnit, rootCanvasScale, lineSpacing;
            public Vector3 localScale, lossyScale;
            public Vector2 scaleRelativeToRootCanvas, projectedPixelsPerLocalUnit, screenOrigin, originDistanceToNearestPixel;
            public float maximumRectCornerPixelResidual, maximumMeshVertexPixelResidual;
            public float maximumMeshVertexXResidual, maximumMeshVertexYResidual, meanMeshVertexYResidual;
            public Vector2 meshBoundsMinPixelResidual, meshBoundsMaxPixelResidual;
            public Rect rectPixels, meshPixels, cropPixels;
            public Color color;
        }

        // -executeMethod ShatteredSail.Editor.TextClarityCapture.CaptureFromCommandLine
        // -textClarityOutput D:/Game/Artifacts/TextClarity20261013/Baseline
        // -textClarityVariant Baseline
        // Optional: -textClarityStates Home,Settings,PortHub,PortSettings,Navigation,Settlement
        public static void CaptureFromCommandLine()
        {
            Require(!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode, "A stopped Editor is required.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
            Require(project.StartsWith(@"D:\Game\tmp\", StringComparison.OrdinalIgnoreCase), "Run only in an isolated D:/Game/tmp project.");
            string requested = Argument("-textClarityOutput");
            Require(!string.IsNullOrWhiteSpace(requested) && Path.IsPathRooted(requested), "Absolute -textClarityOutput is required.");
            string output = Path.GetFullPath(requested);
            Require(output.StartsWith(@"D:\Game\Artifacts\", StringComparison.OrdinalIgnoreCase), "Output must stay under D:/Game/Artifacts.");
            Require(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(), "Use a new/empty output directory.");
            string[] states = (Argument("-textClarityStates") ?? string.Join(",", DefaultStates)).Split(',').Select(s => s.Trim()).ToArray();
            Require(states.Length > 0 && states.All(s => DefaultStates.Contains(s)), "Unsupported text-clarity state.");
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output, "crops"));
            var report = new Report { variant = Argument("-textClarityVariant") ?? "unspecified", startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion, projectPath = project, status = "running" };
            try
            {
                EnsurePipeline();
                foreach (string state in states)
                    foreach (int width in new[] { 1920, 3840 })
                    {
                        var frame = new Frame { state = state, width = width, height = width * 9 / 16,
                            file = state + "-" + width + ".png", hostScreenWidth = Screen.width, hostScreenHeight = Screen.height };
                        report.frames.Add(frame);
                        try { Capture(output, frame); }
                        catch (Exception error) { frame.error = error.ToString(); report.errors.Add(state + "/" + width + ": " + error); }
                        report.frameCount = report.frames.Count;
                        report.textCount = report.frames.Sum(f => f.texts.Count);
                        report.sampledTextCount = report.frames.Sum(f => f.texts.Count(t => !string.IsNullOrEmpty(t.cropFile)));
                        Save(output, report);
                    }
                report.status = report.errors.Count == 0 ? "passed" : "failed";
            }
            catch (Exception error) { report.status = "failed"; report.errors.Add(error.ToString()); }
            finally
            {
                report.completedUtc = DateTime.UtcNow.ToString("O");
                Save(output, report); WriteIndex(output, report);
            }
            Require(report.status == "passed", "Text clarity capture failed; see " + Path.Combine(output, "report.json"));
            Debug.Log("TEXT_CLARITY_CAPTURE_OK: " + report.variant + ", frames=" + report.frameCount + ", Text=" + report.textCount +
                ", native crops=" + report.sampledTextCount + ", " + output);
        }

        private static void Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previousActive = RenderTexture.active;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Text Clarity " + frame.state) { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false); SceneManager.MoveGameObjectToScene(host, scene);
                bool mainMenu = frame.state == "Home" || frame.state == "Settings";
                CoreGameplayDemoController controller = null;
                if (mainMenu) host.AddComponent<MainMenuController>();
                else
                {
                    controller = host.AddComponent<CoreGameplayDemoController>();
                    var screen = (CoreGameplayPreviewScreen)Enum.Parse(typeof(CoreGameplayPreviewScreen), frame.state == "PortSettings" ? "PortHub" : frame.state);
                    controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                }
                host.SetActive(true);
                if (frame.state == "PortSettings")
                {
                    MethodInfo open = typeof(CoreGameplayDemoController).GetMethod("OpenPortSettings", PrivateInstance);
                    Require(open != null, "Production port-settings constructor is missing.");
                    open.Invoke(controller, null);
                    Require(host.GetComponentsInChildren<RectTransform>(true).Any(t => t.name == "Port Settings Sheet" && t.gameObject.activeInHierarchy), "Port settings did not open.");
                }
                string canvasName = mainMenu ? MainMenuController.EditorPreviewCanvasName : CoreGameplayDemoController.EditorPreviewCanvasName;
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == canvasName);
                if (mainMenu)
                    foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "Settings Panel") child.gameObject.SetActive(frame.state == "Settings");
                        if (child.name == "Achievements Panel" || child.name == "New Game Confirmation") child.gameObject.SetActive(false);
                    }
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                var cameraHost = new GameObject("Text Clarity Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5f; camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer;
                camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Require(target.Create(), "Cannot create render target.");
                camera.targetTexture = target; camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                Require(scaler != null && scaler.enabled, "Production CanvasScaler must remain enabled.");
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                Require(handle != null, "CanvasScaler.Handle is unavailable.");
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(false)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.canvasScale = canvas.scaleFactor; frame.referenceResolution = scaler.referenceResolution;
                frame.renderingDisplaySize = canvas.renderingDisplaySize; frame.canvasPixelRect = canvas.pixelRect;
                frame.logicalCanvasSize = ((RectTransform)canvas.transform).rect.size; frame.canvasPixelPerfect = canvas.pixelPerfect;
                float wr = frame.width / scaler.referenceResolution.x, hr = frame.height / scaler.referenceResolution.y;
                frame.expectedCanvasScale = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr) :
                    Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
                frame.scalerVerified = Mathf.Abs(frame.canvasScale - frame.expectedCanvasScale) < .001f &&
                    Mathf.Abs(canvas.renderingDisplaySize.x - frame.width) < .5f && Mathf.Abs(canvas.renderingDisplaySize.y - frame.height) < .5f &&
                    Mathf.Abs(canvas.pixelRect.width - frame.width) < .5f && Mathf.Abs(canvas.pixelRect.height - frame.height) < .5f;
                Require(frame.scalerVerified, "Target did not drive the actual CanvasScaler.");
                foreach (Text text in canvas.GetComponentsInChildren<Text>(false))
                    if (text.enabled && !string.IsNullOrWhiteSpace(text.text)) frame.texts.Add(Measure(text, canvas, camera, frame));
                Require(frame.texts.Count > 0, "No active production text was found.");
                RenderTexture.active = target;
                frame.imageHasContent = SavePixels(output, frame.file, new Rect(0, 0, frame.width, frame.height), frame.width, frame.height);
                Require(frame.imageHasContent, "The rendered frame is blank or nearly uniform.");
                int index = 0;
                foreach (TextRow text in SelectSamples(frame.texts))
                {
                    text.cropFile = "crops/" + frame.state + "-" + frame.width + "-" + (index++).ToString("D2") + ".png";
                    text.cropPixels = Grow(text.meshPixels, 6);
                    SavePixels(output, text.cropFile, text.cropPixels, frame.width, frame.height);
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static TextRow Measure(Text text, Canvas canvas, Camera camera, Frame frame)
        {
            var row = new TextRow { path = PathOf(text.transform, canvas.transform), content = text.text,
                fontName = text.font != null ? text.font.name : "", fontAsset = text.font != null ? AssetDatabase.GetAssetPath(text.font) : "",
                fontStyle = text.fontStyle.ToString(), fontSize = text.fontSize, dynamicFont = text.font != null && text.font.dynamic,
                textPixelsPerUnit = text.pixelsPerUnit, renderedPixelsPerUnit = text.pixelsPerUnit, rasterDensitySource = "Text.pixelsPerUnit",
                rootCanvasScale = canvas.rootCanvas.scaleFactor, localScale = text.transform.localScale, lossyScale = text.transform.lossyScale,
                lineSpacing = text.lineSpacing, alignment = text.alignment.ToString(), color = text.color, richText = text.supportRichText,
                horizontalOverflow = text.horizontalOverflow.ToString(), verticalOverflow = text.verticalOverflow.ToString(),
                bestFitEnabled = text.resizeTextForBestFit, bestFitSize = text.cachedTextGenerator.fontSizeUsedForBestFit,
                rendererCulled = text.canvasRenderer.cull, insideButton = text.GetComponentInParent<Button>() != null };
            Transform root = canvas.rootCanvas.transform;
            row.scaleRelativeToRootCanvas = new Vector2(root.InverseTransformVector(text.transform.TransformVector(Vector3.right)).magnitude,
                root.InverseTransformVector(text.transform.TransformVector(Vector3.up)).magnitude);
            Vector2 origin = camera.WorldToScreenPoint(text.transform.position);
            row.screenOrigin = origin;
            row.originDistanceToNearestPixel = new Vector2(Mathf.Abs(origin.x - Mathf.Round(origin.x)), Mathf.Abs(origin.y - Mathf.Round(origin.y)));
            row.projectedPixelsPerLocalUnit = new Vector2(Vector2.Distance(origin, camera.WorldToScreenPoint(text.transform.TransformPoint(Vector3.right))),
                Vector2.Distance(origin, camera.WorldToScreenPoint(text.transform.TransformPoint(Vector3.up))));
            var corners = new Vector3[4]; text.rectTransform.GetWorldCorners(corners);
            Vector2[] rectPixels = corners.Select(p => (Vector2)camera.WorldToScreenPoint(p)).ToArray();
            row.rectPixels = Bounds(rectPixels); row.maximumRectCornerPixelResidual = PixelResidual(rectPixels);
            row.rectOutsideTarget = Outside(row.rectPixels, frame.width, frame.height);
            Mesh mesh = text.canvasRenderer.GetMesh(); // owned by CanvasRenderer
            row.meshAvailable = mesh != null && mesh.vertexCount > 0;
            row.meshVertexCount = mesh != null ? mesh.vertexCount : 0;
            if (row.meshAvailable)
            {
                Vector2[] points = mesh.vertices.Select(p => (Vector2)camera.WorldToScreenPoint(text.transform.TransformPoint(p))).ToArray();
                row.meshPixels = Bounds(points); row.maximumMeshVertexPixelResidual = PixelResidual(points);
                row.maximumMeshVertexXResidual = points.Max(p => Mathf.Abs(p.x - Mathf.Round(p.x)));
                row.maximumMeshVertexYResidual = points.Max(p => Mathf.Abs(p.y - Mathf.Round(p.y)));
                row.meanMeshVertexYResidual = points.Average(p => Mathf.Abs(p.y - Mathf.Round(p.y)));
                row.meshBoundsMinPixelResidual = new Vector2(row.meshPixels.xMin - Mathf.Round(row.meshPixels.xMin), row.meshPixels.yMin - Mathf.Round(row.meshPixels.yMin));
                row.meshBoundsMaxPixelResidual = new Vector2(row.meshPixels.xMax - Mathf.Round(row.meshPixels.xMax), row.meshPixels.yMax - Mathf.Round(row.meshPixels.yMax));
                row.meshOutsideTarget = Outside(row.meshPixels, frame.width, frame.height);
            }
            BaseMeshEffect[] effects = text.GetComponents<BaseMeshEffect>().Where(e => e.enabled).ToArray();
            row.meshEffects = effects.Select(e => e.GetType().Name).ToArray();
            foreach (BaseMeshEffect effect in effects)
            {
                PropertyInfo density = effect.GetType().GetProperty("RenderedPixelsPerUnit", BindingFlags.Public | BindingFlags.Instance);
                if (density != null)
                {
                    row.renderedPixelsPerUnit = Convert.ToSingle(density.GetValue(effect));
                    row.rasterDensitySource = effect.GetType().Name + ".RenderedPixelsPerUnit";
                }
            }
            Texture atlas = text.font != null && text.font.material != null ? text.font.material.mainTexture : null;
            if (atlas != null) { row.atlasWidth = atlas.width; row.atlasHeight = atlas.height; row.atlasFilterMode = atlas.filterMode.ToString(); }
            return row;
        }

        private static IEnumerable<TextRow> SelectSamples(List<TextRow> texts)
        {
            var available = texts.Where(t => t.meshAvailable && !t.rendererCulled && !t.meshOutsideTarget && t.meshPixels.width > 2 && t.meshPixels.height > 2)
                .OrderBy(t => t.path, StringComparer.Ordinal).ToList();
            var result = new List<TextRow>();
            void Add(TextRow row) { if (row != null && !result.Contains(row)) result.Add(row); }
            Add(available.Where(t => t.path.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 || t.path.IndexOf("Heading", StringComparison.OrdinalIgnoreCase) >= 0).OrderByDescending(t => t.fontSize).FirstOrDefault());
            Add(available.Where(t => t.fontSize <= 18 && t.content.Length >= 8).OrderByDescending(t => t.content.Length).FirstOrDefault());
            Add(available.Where(t => t.content.Contains("\n")).OrderByDescending(t => t.content.Length).FirstOrDefault());
            Add(available.FirstOrDefault(t => t.insideButton));
            Add(available.FirstOrDefault(t => t.fontStyle.Contains("Bold")));
            Add(available.OrderByDescending(t => t.fontSize).FirstOrDefault());
            foreach (TextRow row in available) { if (result.Count >= 6) break; Add(row); }
            return result.Take(6);
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

        private static void EnsurePipeline()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null || RenderPipelineManager.currentPipeline != null) return;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            MethodInfo method = typeof(RenderPipelineManager).GetMethod("TryPrepareRenderPipeline", flags, null, new[] { typeof(RenderPipelineAsset) }, null) ??
                typeof(RenderPipelineManager).GetMethod("PrepareRenderPipeline", flags, null, new[] { typeof(RenderPipelineAsset) }, null);
            Require(method != null, "SRP preparation entry is unavailable.");
            object result = method.Invoke(null, new object[] { asset });
            Require(!(result is bool success) || success, "SRP preparation returned false.");
            Require(RenderPipelineManager.currentPipeline != null, "SRP instance was not created.");
        }

        private static Rect Bounds(IEnumerable<Vector2> points)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            foreach (Vector2 point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static float PixelResidual(IEnumerable<Vector2> points) => points.Select(p => Mathf.Max(Mathf.Abs(p.x - Mathf.Round(p.x)), Mathf.Abs(p.y - Mathf.Round(p.y)))).DefaultIfEmpty(0f).Max();
        private static bool Outside(Rect rect, int width, int height) => rect.xMin < -.5f || rect.yMin < -.5f || rect.xMax > width + .5f || rect.yMax > height + .5f;
        private static Rect Grow(Rect rect, float pixels) => Rect.MinMaxRect(rect.xMin - pixels, rect.yMin - pixels, rect.xMax + pixels, rect.yMax + pixels);
        private static string PathOf(Transform current, Transform root)
        {
            var parts = new List<string>();
            while (current != null && current != root) { parts.Add(current.name + "[" + current.GetSiblingIndex() + "]"); current = current.parent; }
            parts.Reverse(); return string.Join("/", parts);
        }
        private static string Argument(string key) { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
        private static void Save(string output, Report report) => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        private static void WriteIndex(string output, Report report)
        {
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>UI文字清晰度</title><style>body{background:#151922;color:#eee;font:16px system-ui;margin:24px}a{color:#acd}.scroll{overflow:auto;max-height:680px;border:1px solid #526071}img{display:block;max-width:none;width:auto;height:auto}.crops{display:flex;gap:20px;overflow:auto}figure{flex:none;margin:12px 0}figcaption{max-width:650px;overflow-wrap:anywhere}</style>");
            html.Append("<h1>").Append(System.Net.WebUtility.HtmlEncode(report.variant)).Append(" · UI文字清晰度</h1><p>真实Unity渲染。下列全帧与文字裁图均按原始像素显示，可滚动；没有截图后缩放。<a href='report.json'>详细字体/缩放/像素对齐记录</a></p>");
            foreach (Frame frame in report.frames)
            {
                html.Append("<h2>").Append(frame.state).Append(" · ").Append(frame.width).Append("×").Append(frame.height).Append("</h2>");
                if (!string.IsNullOrEmpty(frame.error)) { html.Append("<pre>").Append(System.Net.WebUtility.HtmlEncode(frame.error)).Append("</pre>"); continue; }
                html.Append("<p>Canvas ").Append(frame.canvasScale).Append("×；活动文字 ").Append(frame.texts.Count).Append("。</p><details><summary>展开原像素全帧</summary><div class='scroll'><img src='").Append(frame.file).Append("'></div></details><div class='crops'>");
                foreach (TextRow row in frame.texts.Where(t => !string.IsNullOrEmpty(t.cropFile)))
                    html.Append("<figure><img src='").Append(row.cropFile).Append("'><figcaption>").Append(System.Net.WebUtility.HtmlEncode(row.path)).Append("<br>")
                        .Append(row.fontName).Append(" · ").Append(row.fontStyle).Append(" · ").Append(row.fontSize).Append("px · PPU ").Append(row.textPixelsPerUnit).Append(" · rendered ").Append(row.renderedPixelsPerUnit).Append("</figcaption></figure>");
                html.Append("</div>");
            }
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
        }
    }
}
