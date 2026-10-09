using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Debug = UnityEngine.Debug;

namespace ShatteredSail.Editor
{
    /// <summary>
    /// Real Unity-rendered chart review across all three routes and six ports.
    /// Uses disposable editor previews, never Play Mode, user scenes or saves.
    /// Detail views are camera-framed renders, not resized or retouched images.
    /// </summary>
    public static class VoyageChartAcceptance
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const int Layer = 31;
        private const float ChartWidth = 1160f;
        private const float ChartHeight = 630f;

        [Serializable]
        private sealed class Report
        {
            public string capturedAtUtc;
            public int width = Width;
            public int height = Height;
            public string safety = "Disposable edit-mode previews. No Play Mode, scene saves, settings application, or PlayerPrefs writes. Close-ups are new camera renders, not image post-processing.";
            public int expectedChartCount = 18;
            public int successfulChartCount;
            public int successfulImageCount;
            public double totalElapsedMilliseconds;
            public List<ChartRecord> charts = new List<ChartRecord>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        private sealed class ChartRecord
        {
            public string port;
            public int routeIndex;
            public string route;
            public string departure;
            public string destination;
            public string seed;
            public string mapHash;
            public bool screenshotReferenceCase;
            public bool complete;
            public double buildMilliseconds;
            public int uiNodeCount;
            public int graphicCount;
            public int fixedEventCount;
            public int exploredCellCount;
            public int missingTextureCount;
            public int materialErrorCount;
            public List<string> materialShaders = new List<string>();
            public List<string> diagnostics = new List<string>();
            public List<FrameRecord> frames = new List<FrameRecord>();
            public string error;
        }

        [Serializable]
        private sealed class FrameRecord
        {
            public string label;
            public string file;
            public string framing;
            public float cameraWorldWidth;
            public float cameraWorldHeight;
            public double renderMilliseconds;
            public bool complete;
        }

        [MenuItem("Shattered Sail/Art Review/Capture Voyage Charts")]
        public static void CaptureFromMenu()
        {
            string directory = Capture(ResolveOutputDirectory());
            Debug.Log("Voyage chart gallery: " + directory);
            EditorUtility.RevealInFinder(Path.Combine(directory, "index.html"));
        }

        /// <summary>
        /// -executeMethod ShatteredSail.Editor.VoyageChartAcceptance.CaptureFromCommandLine
        /// -voyageChartCapturePath D:/Game/UnityClient/Artifacts/VoyageCharts/review-01
        /// Requires graphics: do not pass -nographics. Use a new, empty directory.
        /// </summary>
        public static void CaptureFromCommandLine()
        {
            Debug.Log("VOYAGE_CHART_ACCEPTANCE: " + Capture(ResolveOutputDirectory()));
        }

        public static string Capture(string outputDirectory)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before capturing voyage charts.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Chart capture requires graphics. Remove -nographics.");
            string directory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("Output directory is not empty. Use a fresh -voyageChartCapturePath: " + directory);
            Directory.CreateDirectory(directory);
            Report report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            Stopwatch totalWatch = Stopwatch.StartNew();
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture previousTarget = RenderTexture.active;
            EventSystem previousEvents = EventSystem.current;
            GameObject previousSelection = previousEvents == null ? null : previousEvents.currentSelectedGameObject;
            GameObject cameraHost = null;
            GameObject host = null;
            RenderTexture target = null;
            ChartRecord activeRecord = null;
            Application.LogCallback collectErrors = (message, stackTrace, type) =>
            {
                if (activeRecord != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    activeRecord.diagnostics.Add(type + ": " + message);
            };
            Application.logMessageReceived += collectErrors;
            try
            {
                cameraHost = new GameObject("Voyage Chart Review Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.06f, 0.075f, 1f);
                camera.orthographic = true;
                camera.aspect = Width / (float)Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.cullingMask = 1 << Layer;
                camera.scene = scene;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { name = "Voyage Chart Review Target" };
                target.Create();
                camera.targetTexture = target;

                // Capture the user's WhiteShell / EDITOR-PREVIEW-0 reference first.
                CoreGameplayPreviewPort[] ports = (CoreGameplayPreviewPort[])Enum.GetValues(typeof(CoreGameplayPreviewPort));
                ports = ports.OrderBy(port => port == CoreGameplayPreviewPort.WhiteShell ? 0 : 1).ThenBy(port => (int)port).ToArray();
                int caseIndex = 0;
                for (int routeIndex = 0; routeIndex < 3; routeIndex += 1)
                {
                    foreach (CoreGameplayPreviewPort port in ports)
                    {
                        caseIndex += 1;
                        ChartRecord record = new ChartRecord
                        {
                            port = port.ToString(), routeIndex = routeIndex,
                            screenshotReferenceCase = port == CoreGameplayPreviewPort.WhiteShell && routeIndex == 0
                        };
                        report.charts.Add(record);
                        activeRecord = record;
                        string prefix = caseIndex.ToString("D2") + "_Route" + routeIndex + "_" + port;
                        try
                        {
                            Stopwatch buildWatch = Stopwatch.StartNew();
                            host = new GameObject("Voyage Chart Review " + prefix) { hideFlags = HideFlags.HideAndDontSave };
                            host.SetActive(false);
                            SceneManager.MoveGameObjectToScene(host, scene);
                            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, port, routeIndex);
                            host.SetActive(true);
                            if (!controller.HasCompleteEditorPreview || controller.Session.Map == null)
                                throw new InvalidOperationException("Navigation preview did not build.");
                            Canvas canvas = host.GetComponentInChildren<Canvas>();
                            if (canvas == null) throw new InvalidOperationException("Preview Canvas is missing.");
                            canvas.renderMode = RenderMode.WorldSpace;
                            canvas.worldCamera = camera;
                            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                            canvasRect.sizeDelta = new Vector2(Width, Height);
                            canvasRect.position = Vector3.zero;
                            canvasRect.localScale = Vector3.one;
                            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                            Canvas.ForceUpdateCanvases();
                            buildWatch.Stop();
                            record.buildMilliseconds = buildWatch.Elapsed.TotalMilliseconds;
                            CoreVoyageMap map = controller.Session.Map;
                            record.route = map.Route.Name;
                            record.departure = map.DepartureName;
                            record.destination = map.DestinationName;
                            record.seed = map.Seed;
                            record.mapHash = map.Hash.ToString("X8");
                            record.fixedEventCount = map.EventFootprints.Count;
                            record.exploredCellCount = map.ExploredCells.Count(explored => explored);
                            InspectUi(canvas, record);
                            RectTransform mapLayer = canvas.GetComponentsInChildren<RectTransform>().FirstOrDefault(item => item.name == "Map Layer");
                            if (mapLayer == null) throw new InvalidOperationException("Map Layer is missing.");
                            Rect fullView = new Rect(-Width * 0.5f, -Height * 0.5f, Width, Height);
                            CaptureFrame(camera, target, fullView, directory, record, prefix + "_Navigation.png", "航海界面全图", "Full 1920×1080 UI");
                            Rect chartView = WorldRect(mapLayer, new Rect(-ChartWidth * 0.5f - 12f, -ChartHeight * 0.5f - 12f, ChartWidth + 24f, ChartHeight + 24f));
                            CaptureFrame(camera, target, chartView, directory, record, prefix + "_Chart.png", "海图精细全景", "Camera fitted to the full chart; no image scaling or retouching");
                            if (record.screenshotReferenceCase)
                            {
                                int detailIndex = 0;
                                foreach (CoreEventFootprint footprint in map.EventFootprints.Where(item => item.TerrainConstraint == EventTerrainConstraint.Beach).Take(4))
                                {
                                    detailIndex += 1;
                                    float cellWidth = ChartWidth / CoreVoyageMap.GridColumns;
                                    float cellHeight = ChartHeight / CoreVoyageMap.GridRows;
                                    const float padding = 2.5f;
                                    Rect local = new Rect(
                                        -ChartWidth * 0.5f + (footprint.MinColumn - padding) * cellWidth,
                                        -ChartHeight * 0.5f + (footprint.MinRow - padding) * cellHeight,
                                        (footprint.MaxColumn - footprint.MinColumn + 1f + padding * 2f) * cellWidth,
                                        (footprint.MaxRow - footprint.MinRow + 1f + padding * 2f) * cellHeight);
                                    CoreMapNode node = map.Nodes.FirstOrDefault(item => item.Id == footprint.NodeId);
                                    string eventName = node == null || node.Encounter == null ? "海滩事件" : node.Encounter.Name;
                                    CaptureFrame(camera, target, WorldRect(mapLayer, local), directory, record,
                                        prefix + "_BeachDetail" + detailIndex + ".png", "海岸接缝近景 · " + eventName,
                                        "Camera close-up of event " + footprint.NodeId + "; cells (" + footprint.MinColumn + "," + footprint.MinRow + ")–(" + footprint.MaxColumn + "," + footprint.MaxRow + "), orientation " + footprint.Orientation);
                                }
                                CoreEventFootprint[] straits = map.EventFootprints
                                    .Where(item => item.TerrainConstraint == EventTerrainConstraint.Strait).ToArray();
                                // Prefer one horizontal and one vertical channel when both exist.
                                // Keep two cells beyond each event's actual coast to expose hard
                                // outer edges as well as accidentally filled central waterways.
                                CoreEventFootprint[] straitDetails = straits.GroupBy(item => item.Orientation % 2)
                                    .Select(group => group.First()).Concat(straits).Distinct().Take(2).ToArray();
                                int straitDetailIndex = 0;
                                foreach (CoreEventFootprint footprint in straitDetails)
                                {
                                    straitDetailIndex += 1;
                                    float cellWidth = ChartWidth / CoreVoyageMap.GridColumns;
                                    float cellHeight = ChartHeight / CoreVoyageMap.GridRows;
                                    const float padding = 2f;
                                    Rect local = new Rect(
                                        -ChartWidth * 0.5f + (footprint.MinColumn - padding) * cellWidth,
                                        -ChartHeight * 0.5f + (footprint.MinRow - padding) * cellHeight,
                                        (footprint.MaxColumn - footprint.MinColumn + 1f + padding * 2f) * cellWidth,
                                        (footprint.MaxRow - footprint.MinRow + 1f + padding * 2f) * cellHeight);
                                    CoreMapNode node = map.Nodes.FirstOrDefault(item => item.Id == footprint.NodeId);
                                    string eventName = node == null || node.Encounter == null ? "峡谷事件" : node.Encounter.Name;
                                    string axis = footprint.Orientation % 2 == 0 ? "横向" : "竖向";
                                    CaptureFrame(camera, target, WorldRect(mapLayer, local), directory, record,
                                        prefix + "_StraitDetail" + straitDetailIndex + ".png", axis + "峡谷双岸与航道近景 · " + eventName,
                                        "Camera close-up of strait event " + footprint.NodeId + "; cells (" + footprint.MinColumn + "," + footprint.MinRow + ")–(" + footprint.MaxColumn + "," + footprint.MaxRow + "), orientation " + footprint.Orientation + ", 2-cell coast margin");
                                }
                            }
                            record.complete = true;
                            report.successfulChartCount += 1;
                            report.successfulImageCount += record.frames.Count(frame => frame.complete);
                        }
                        catch (Exception exception)
                        {
                            record.error = exception.Message;
                            report.errors.Add(prefix + ": " + exception.Message);
                            Debug.LogException(exception);
                        }
                        finally
                        {
                            if (host != null) { UnityEngine.Object.DestroyImmediate(host); host = null; }
                            activeRecord = null;
                        }
                    }
                }
            }
            finally
            {
                activeRecord = null;
                Application.logMessageReceived -= collectErrors;
                RenderTexture.active = previousTarget;
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (cameraHost != null) UnityEngine.Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
                if (previousEvents != null)
                    previousEvents.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
                totalWatch.Stop();
                report.totalElapsedMilliseconds = totalWatch.Elapsed.TotalMilliseconds;
                File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                WriteGallery(directory, report);
            }
            if (report.errors.Count > 0 || report.successfulChartCount != report.expectedChartCount)
                throw new InvalidOperationException("Voyage chart review incomplete. See " + Path.Combine(directory, "report.json"));
            return directory;
        }

        private static void InspectUi(Canvas canvas, ChartRecord record)
        {
            record.uiNodeCount = canvas.GetComponentsInChildren<Transform>(true).Length;
            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>();
            record.graphicCount = graphics.Length;
            HashSet<string> shaders = new HashSet<string>();
            foreach (Graphic graphic in graphics)
            {
                if (graphic is RawImage raw && raw.texture == null)
                {
                    record.missingTextureCount += 1;
                    record.diagnostics.Add("Missing RawImage texture: " + graphic.name);
                }
                Material material = graphic.materialForRendering;
                if (material == null || material.shader == null || !material.shader.isSupported ||
                    material.shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    record.materialErrorCount += 1;
                    record.diagnostics.Add("Missing/unsupported UI material: " + graphic.name);
                }
                else shaders.Add(material.shader.name);
                if (graphic is Text label && label.font != null && label.font.dynamic && !string.IsNullOrEmpty(label.text))
                    label.font.RequestCharactersInTexture(label.text, label.fontSize, label.fontStyle);
            }
            record.materialShaders = shaders.OrderBy(item => item).ToList();
            Canvas.ForceUpdateCanvases();
        }

        private static Rect WorldRect(RectTransform owner, Rect local)
        {
            Vector3 minimum = owner.TransformPoint(new Vector3(local.xMin, local.yMin, 0f));
            Vector3 maximum = owner.TransformPoint(new Vector3(local.xMax, local.yMax, 0f));
            return Rect.MinMaxRect(Mathf.Min(minimum.x, maximum.x), Mathf.Min(minimum.y, maximum.y),
                Mathf.Max(minimum.x, maximum.x), Mathf.Max(minimum.y, maximum.y));
        }

        private static void CaptureFrame(Camera camera, RenderTexture target, Rect worldView, string directory,
            ChartRecord record, string file, string label, string framing)
        {
            float worldHeight = Mathf.Max(worldView.height, worldView.width / camera.aspect);
            camera.orthographicSize = worldHeight * 0.5f;
            camera.transform.position = new Vector3(worldView.center.x, worldView.center.y, -1000f);
            FrameRecord frame = new FrameRecord { file = file, label = label, framing = framing,
                cameraWorldWidth = worldHeight * camera.aspect, cameraWorldHeight = worldHeight };
            record.frames.Add(frame);
            Stopwatch watch = Stopwatch.StartNew();
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();
            SaveFrame(target, Path.Combine(directory, file));
            watch.Stop();
            frame.renderMilliseconds = watch.Elapsed.TotalMilliseconds;
            frame.complete = true;
        }

        private static void SaveFrame(RenderTexture target, string file)
        {
            RenderTexture.active = target;
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply(false, false);
                Color32[] pixels = image.GetPixels32();
                int minimum = 255, maximum = 0;
                for (int index = 0; index < pixels.Length; index += 997)
                {
                    int brightness = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                    minimum = Math.Min(minimum, brightness);
                    maximum = Math.Max(maximum, brightness);
                }
                if (maximum - minimum < 20)
                    throw new InvalidOperationException("Nearly uniform frame; preview did not render.");
                File.WriteAllBytes(file, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void WriteGallery(string directory, Report report)
        {
            StringBuilder html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>种子海图 · 接缝与细节验收</title><style>body{margin:28px;background:#071f2a;color:#e7e1ce;font:16px system-ui}article{padding:22px;margin:24px 0;background:#103440;border:1px solid #5c7b80}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(550px,1fr));gap:18px}figure{margin:0}img{width:100%;display:block;margin:12px 0}small{display:block;color:#bdd0cd;line-height:1.6}a{color:#efc37a}p{line-height:1.7}code{color:#efc37a}summary{cursor:pointer}</style>");
            html.Append("<h1>种子海图 · 接缝与细节验收</h1><p>").Append(report.successfulChartCount).Append(" / ").Append(report.expectedChartCount)
                .Append(" 张种子海图，").Append(report.successfulImageCount).Append(" 张真实 Unity 截图。每个海域覆盖六个港口；每组包括完整航海界面与海图细节全景。白贝港 / EDITOR-PREVIEW-0 为用户截图重点，另含海滩接缝和横/竖峡谷双岸、航道近景。</p><p>所有图像均为 1920 × 1080 的独立相机渲染，无图片后期加工。地形在临时预览中全开图，运行时的迷雾、事件发现与存档不受影响。<a href=\"report.json\">完整种子、构建时间与节点/材质诊断</a></p>");
            foreach (ChartRecord chart in report.charts)
            {
                html.Append("<article><h2>").Append(Escape(chart.route)).Append(" · ").Append(Escape(chart.departure)).Append(" → ").Append(Escape(chart.destination));
                if (chart.screenshotReferenceCase) html.Append(" · 截图复现重点");
                html.Append("</h2><p>种子 <code>").Append(Escape(chart.seed)).Append(" / ").Append(Escape(chart.mapHash)).Append("</code> · 构建 ")
                    .Append(chart.buildMilliseconds.ToString("0")).Append(" ms · UI 节点 ").Append(chart.uiNodeCount).Append(" · 图形节点 ").Append(chart.graphicCount)
                    .Append(" · 固定事件 ").Append(chart.fixedEventCount).Append(" · 缺失纹理 ").Append(chart.missingTextureCount).Append(" · 材质错误 ").Append(chart.materialErrorCount).Append("</p>");
                if (!chart.complete) html.Append("<p>捕获失败：").Append(Escape(chart.error)).Append("</p>");
                if (chart.diagnostics.Count > 0)
                {
                    html.Append("<details><summary>诊断信息</summary><ul>");
                    foreach (string diagnostic in chart.diagnostics) html.Append("<li>").Append(Escape(diagnostic)).Append("</li>");
                    html.Append("</ul></details>");
                }
                html.Append("<section>");
                foreach (FrameRecord frame in chart.frames.Where(item => item.complete))
                    html.Append("<figure><figcaption>").Append(Escape(frame.label)).Append("<small>").Append(Escape(frame.framing)).Append("</small></figcaption><a href=\"")
                        .Append(Escape(frame.file)).Append("\"><img src=\"").Append(Escape(frame.file)).Append("\" loading=\"lazy\"></a></figure>");
                html.Append("</section></article>");
            }
            html.Append("</html>");
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), new UTF8Encoding(false));
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string ResolveOutputDirectory()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index += 1)
                if (arguments[index] == "-voyageChartCapturePath") return Path.GetFullPath(Path.Combine(project, arguments[index + 1]));
            return Path.Combine(project, "Artifacts", "VoyageCharts", "review-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        }
    }
}
