// Copy to an isolated project's Editor folder only. Captures production UI in
// temporary PreviewScenes, with the real CanvasScaler and no scene/profile saves.
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
    public static class ShipDetailTavernCapture
    {
        private const int Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc, projectPath;
            public string scope = "23 actual Unity UI captures: 12 ships' performance pages; Gray Gull, Salt Mountain and Night Lantern background/equipment; shared tavern frame; four 1280x1024 narrow-screen checks. Disposable PreviewScenes, enabled production CanvasScaler; no scene/profile writes.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] public sealed class Frame
        {
            public string name, file, error, screenMatchMode;
            public int ship, page, width = 1920, height = 1080;
            public bool tavern, scalerVerified;
            public float scaleFactor;
            public Vector2 displaySize, canvasSize;
            public List<Element> elements = new List<Element>();
        }
        [Serializable] public sealed class Element
        {
            public string name, kind, text, texture;
            public int fontSize;
            public Rect screenRect, localRect;
            public bool active, raycastTarget;
        }

        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "-shipDetailCapturePath");
            if (key < 0 || key + 1 >= args.Length || !Path.IsPathRooted(args[key + 1]))
                throw new ArgumentException("Pass -shipDetailCapturePath NEW_ABSOLUTE_DIRECTORY");
            string output = Path.GetFullPath(args[key + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Capture directory must be empty.");
            Directory.CreateDirectory(output);
            var report = new Report { capturedAtUtc = DateTime.UtcNow.ToString("O"),
                projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")) };
            bool singleFrame = args.Contains("-shipDetailSingleFrame");
            if (singleFrame)
            {
                report.scope = "One actual Unity ship detail frame verifying the upper-left shared header return and removal of the scroll return button.";
                Add(report, 0, 0, false);
            }
            else
            {
            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++) Add(report, ship, 0, false);
            foreach (int ship in new[] { 0, 8, 10 })
                foreach (int page in new[] { 1, 2 }) Add(report, ship, page, false);
            Add(report, 0, 0, true);
            Add(report, 0, 0, false, true);
            Add(report, 8, 1, false, true);
            Add(report, 10, 2, false, true);
            Add(report, 0, 0, true, true);
            }
            foreach (Frame frame in report.frames)
            {
                try { Capture(output, frame); }
                catch (Exception exception) { frame.error = exception.ToString(); Debug.LogWarning(frame.file + ": " + exception.Message); }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("SHIP_DETAIL_FRAME: " + frame.file + " " + (string.IsNullOrEmpty(frame.error) ? "OK" : "FAILED"));
            }
            WriteGallery(output, report);
            int expectedFrames = singleFrame ? 1 : 23;
            if (report.frames.Count != expectedFrames || report.frames.Any(frame => !string.IsNullOrEmpty(frame.error) || !frame.scalerVerified))
                throw new InvalidOperationException("Ship detail capture incomplete; inspect report.json.");
            Debug.Log("SHIP_DETAIL_CAPTURE_OK: " + expectedFrames + " frames in " + output);
        }

        private static void Add(Report report, int ship, int page, bool tavern, bool narrow = false)
        {
            string pageName = new[] { "Performance", "Background", "Equipment" }[page];
            report.frames.Add(new Frame { ship = ship, page = page, tavern = tavern,
                name = tavern ? "酒馆同框对照" : CoreVoyageSession.Ships[ship].Name + " / " + pageName,
                file = (narrow ? "Narrow_" : "") + (tavern ? "Tavern_Reference" : "Ship_" + ship.ToString("D2") + "_" + pageName) + ".png",
                width = narrow ? 1280 : 1920, height = narrow ? 1024 : 1080 });
        }

        private static void Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Ship Detail Review");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                CoreGameplayPreviewPort port = frame.tavern ? CoreGameplayPreviewPort.BrokenAnchor : (CoreGameplayPreviewPort)CoreShipMarketCatalog.HomePortIndex(frame.ship);
                controller.ConfigureEditorPreview(frame.tavern ? CoreGameplayPreviewScreen.TavernCrewDetail : CoreGameplayPreviewScreen.Shipyard, port, 0);
                host.SetActive(true);
                if (frame.tavern) { controller.ConfigureEditorCrewDossier(2, 0); controller.SetTavernDossierAtlasComparison(true); }
                else controller.ConfigureEditorShipyard(frame.ship, true, frame.page);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler disabled.");
                foreach (Transform transform in canvas.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = Layer;
                var cameraHost = new GameObject("Disposable Ship Detail Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.displaySize = canvas.renderingDisplaySize;
                frame.canvasSize = canvas.GetComponent<RectTransform>().rect.size; frame.scaleFactor = canvas.scaleFactor;
                float expectedScale = Mathf.Min(frame.width / scaler.referenceResolution.x, frame.height / scaler.referenceResolution.y);
                frame.screenMatchMode = scaler.screenMatchMode.ToString();
                frame.scalerVerified = scaler.enabled && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand &&
                    Mathf.Abs(frame.displaySize.x - frame.width) < .5f && Mathf.Abs(frame.displaySize.y - frame.height) < .5f &&
                    Mathf.Abs(frame.scaleFactor - expectedScale) < .001f && Mathf.Abs(frame.canvasSize.x - frame.width / expectedScale) < 1f &&
                    Mathf.Abs(frame.canvasSize.y - frame.height / expectedScale) < 1f;
                if (!frame.scalerVerified) throw new InvalidOperationException("Actual render target/scaler geometry mismatch.");
                RectTransform detail = canvas.GetComponentsInChildren<RectTransform>().Single(item => item.name == (frame.tavern ? "Tavern Crew Detail" : "Shipyard Detail"));
                foreach (Graphic graphic in detail.GetComponentsInChildren<Graphic>(false))
                {
                    var corners = new Vector3[4]; graphic.rectTransform.GetWorldCorners(corners);
                    Vector2[] screen = corners.Select(v => (Vector2)camera.WorldToScreenPoint(v)).ToArray();
                    Vector3[] local = corners.Select(detail.InverseTransformPoint).ToArray();
                    Text text = graphic as Text; RawImage raw = graphic as RawImage;
                    frame.elements.Add(new Element { name = graphic.name, kind = graphic.GetType().Name,
                        text = text != null ? text.text : null, fontSize = text != null ? text.fontSize : 0,
                        texture = raw != null ? AssetDatabase.GetAssetPath(raw.texture) : null,
                        raycastTarget = graphic.raycastTarget, active = graphic.gameObject.activeInHierarchy,
                        screenRect = Rect.MinMaxRect(screen.Min(v => v.x), screen.Min(v => v.y), screen.Max(v => v.x), screen.Max(v => v.y)),
                        localRect = Rect.MinMaxRect(local.Min(v => v.x), local.Min(v => v.y), local.Max(v => v.x), local.Max(v => v.y)) });
                }
                RenderTexture.active = target; pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void WriteGallery(string output, Report report)
        {
            var html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>船只详情 · 酒馆同框布局</title><style>body{background:#17120f;color:#ead8b7;font:16px system-ui;margin:28px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0}img{width:100%;height:auto}a{color:#efd19d}figcaption{padding:10px 0}p{color:#bba98f}</style><h1>船只详情 · 酒馆同框布局</h1><p>实际 Unity 渲染，12 艘性能页、三艘背景与装配、酒馆对照，以及 1280×1024 窄屏检查。点击图片查看原图。</p><main>");
            foreach (Frame frame in report.frames)
                html.Append("<figure><a href=\"").Append(frame.file).Append("\"><img loading=\"lazy\" src=\"").Append(frame.file).Append("\"></a><figcaption>").Append(frame.name).Append(" · ").Append(frame.width).Append('×').Append(frame.height).Append("</figcaption></figure>");
            html.Append("</main></html>");
            File.WriteAllText(Path.Combine(output, "index.html"), html.ToString(), new UTF8Encoding(false));
        }
    }
}
