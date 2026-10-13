// Temporary audit helper only. Install in an isolated validation project's Editor folder.
// Never saves scenes/assets and never enters PlayMode or invokes gameplay callbacks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public static class ShipyardDetailCleanCapture
    {
        
        private const int Layer = 31;
        [Serializable] public sealed class MenuReport
        {
            public string scope = "Current production shipyard detail, merchant vessel index 1 at SaltCrown, Performance/Background/Equipment tabs in disposable Editor PreviewScenes. Actual enabled CanvasScaler and ScreenSpaceCamera target verified at each requested resolution. Text geometry uses rendered glyph meshes. Complete sliced parchment mapped safe rect checks all active dossier controls and glyphs. Deterministic preview inventory; no player save access, scene writes, asset writes or PlayMode.";
            public string capturedAtUtc;
            public List<MenuFrame> frames = new List<MenuFrame>();
        }
        [Serializable] public sealed class MenuFrame
        {
            public string state, file, error;
            public int width, height; public string variant;
            public bool verifiedScaler;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect;
            public float scaleFactor, expectedScaleFactor;
            public bool cleanNodesRemoved, rolePriceRemoved, heldGoldRemoved, completeParchment, rectangularBars, paperContentInside;
            public string paperTexture;
            public Rect paperRect, safeRect;
            public List<PaperContent> paperContent = new List<PaperContent>();
            public string screenMatchMode;
            public List<TextGeometry> text = new List<TextGeometry>();
        }
        [Serializable] public sealed class PaperContent
        {
            public string path, kind;
            public Rect visibleBounds;
            public bool touchesFrame;
        }
        [Serializable] public sealed class TextGeometry
        {
            public string path, content;
            public bool meshAvailable, meshOutsideTarget, rectOutsideTarget;
            public Rect rectPixels, meshPixels;
            public int meshVertexCount, fontSize;
        }

        public static void CaptureFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be empty.");
            Directory.CreateDirectory(output);
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            foreach (CoreGameplayPreviewScreen screen in new[] { CoreGameplayPreviewScreen.ShipyardDetail })
                foreach (Vector2Int size in new[] {new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1280,1024)})
                foreach (string variant in new[] { "Performance", "Background", "Equipment" })
                {
                    var frame = new MenuFrame { state = screen.ToString(), width = size.x, height = size.y,
                        variant = variant, file = "ShipyardDetailClean_" + variant + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                    File.WriteAllText(Path.Combine(output,"report.json"), JsonUtility.ToJson(report,true));
                }
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error) || !f.cleanNodesRemoved || !f.rolePriceRemoved || !f.heldGoldRemoved || !f.completeParchment || !f.rectangularBars || !f.paperContentInside))
                throw new InvalidOperationException("Shipyard detail capture failed; inspect report and available PNGs.");
            Debug.Log("SHIPYARD_DETAIL_CLEAN_CAPTURE_OK: " + report.frames.Count + " verified frames at " + output);
        }
        private static void VerifyDossier(Canvas canvas, MenuFrame frame)
        {
            RectTransform detail = canvas.GetComponentsInChildren<RectTransform>(false).Single(item => item.name == "Shipyard Detail");
            Transform[] children = detail.GetComponentsInChildren<Transform>(false);
            string[] removed = { "Shipyard Dossier Illustration Caption", "Shipyard Performance Source", "Shipyard Description Panel",
                "Shipyard Description Heading", "Shipyard Dossier Specialization", "Shipyard Description Footer" };
            frame.cleanNodesRemoved = !children.Any(item => removed.Contains(item.name));
            Text role = detail.GetComponentsInChildren<Text>(false).Single(item => item.name == "Shipyard Dossier Role");
            frame.rolePriceRemoved = !role.text.Contains("售价");
            frame.heldGoldRemoved = !detail.GetComponentsInChildren<Text>(false).Any(item => item.text.Contains("持有"));
            Image paper = detail.GetComponentsInChildren<Image>(false).Single(item => item.name == "Shipyard Dossier Parchment");
            frame.paperTexture = paper.sprite == null ? null : paper.sprite.texture.name;
            frame.completeParchment = paper.sprite == NauticalSurfaceSkin.ReadingPaper && paper.type == Image.Type.Sliced && paper.fillCenter;
            frame.rectangularBars = true;
            if (frame.variant == "Performance")
            {
                Image[] bars = detail.GetComponentsInChildren<Image>(false).Where(item => item.name.StartsWith("Shipyard Stat Bar ")).ToArray();
                frame.rectangularBars = bars.Length == 10 && bars.All(item => item.sprite == null && item.type == Image.Type.Simple);
            }
            RectTransform rect = paper.rectTransform;
            frame.paperRect = rect.rect;
            Vector4 source = new Vector4(60,67,60,62);
            Vector4 border = paper.sprite.border;
            float ppu = paper.pixelsPerUnit * paper.pixelsPerUnitMultiplier;
            float Map(float distance, float edge, float opposite, float textureSize, float actualSize) =>
                distance <= edge ? distance / ppu : edge / ppu + (distance - edge) *
                (actualSize - (edge + opposite) / ppu) / (textureSize - edge - opposite);
            frame.safeRect = Rect.MinMaxRect(rect.rect.xMin + Map(source.x,border.x,border.z,paper.sprite.rect.width,rect.rect.width)+6,
                rect.rect.yMin + Map(source.y,border.y,border.w,paper.sprite.rect.height,rect.rect.height)+6,
                rect.rect.xMax - Map(source.z,border.z,border.x,paper.sprite.rect.width,rect.rect.width)-6,
                rect.rect.yMax - Map(source.w,border.w,border.y,paper.sprite.rect.height,rect.rect.height)-6);
            Rect InPaper(RectTransform item)
            {
                Vector3[] corners = new Vector3[4]; item.GetWorldCorners(corners);
                return Bounds(corners.Select(v => (Vector2)rect.InverseTransformPoint(v)));
            }
            void Record(Transform item, string kind, Rect bounds)
            {
                if (bounds.width <= 0 || bounds.height <= 0) return;
                frame.paperContent.Add(new PaperContent { path = PathOf(item, detail), kind = kind, visibleBounds = bounds,
                    touchesFrame = bounds.xMin < frame.safeRect.xMin-.1f || bounds.yMin < frame.safeRect.yMin-.1f ||
                        bounds.xMax > frame.safeRect.xMax+.1f || bounds.yMax > frame.safeRect.yMax+.1f });
            }
            foreach (Selectable control in detail.GetComponentsInChildren<Selectable>(false))
                if (control.enabled) Record(control.transform,"control",InPaper((RectTransform)control.transform));
            foreach (Text label in detail.GetComponentsInChildren<Text>(false))
            {
                if (!label.enabled || string.IsNullOrEmpty(label.text)) continue;
                Mesh mesh = label.canvasRenderer.GetMesh();
                if (mesh == null || mesh.vertexCount == 0) continue;
                Rect bounds = Bounds(mesh.vertices.Select(v => (Vector2)rect.InverseTransformPoint(label.transform.TransformPoint(v))));
                for (Transform t = label.transform.parent; t != null && t != detail.parent; t = t.parent)
                    if (t.TryGetComponent(out RectMask2D mask) && mask.enabled)
                    {
                        Rect clip = InPaper((RectTransform)t);
                        bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin,clip.xMin),Mathf.Max(bounds.yMin,clip.yMin),
                            Mathf.Min(bounds.xMax,clip.xMax),Mathf.Min(bounds.yMax,clip.yMax));
                    }
                Record(label.transform,"glyph",bounds);
            }
            frame.paperContentInside = frame.paperContent.Count > 0 && !frame.paperContent.Any(item => item.touchesFrame);
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
                bool shop = Enum.TryParse(frame.state, out CoreGameplayPreviewScreen screen);
                if (shop) host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(screen, CoreGameplayPreviewPort.SaltCrown, 0);
                else host.AddComponent<MainMenuController>();
                host.SetActive(true);
                if (shop && screen == CoreGameplayPreviewScreen.ShipyardDetail)
                {
                    CoreGameplayDemoController controller = host.GetComponent<CoreGameplayDemoController>();
                    controller.ConfigureEditorShipyard(1, true, frame.variant == "Performance" ? 0 : frame.variant == "Background" ? 1 : 2);
                }
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
                VerifyDossier(canvas, frame);
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
