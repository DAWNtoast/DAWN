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
    public static class ShipyardEquipmentCapture
    {
        private const string Source = "Assets/_Project/Scenes/UIPreviewsForMe";
        private const int Layer = 31;
        [Serializable] public sealed class MenuReport
        {
            public string scope = "Current production shipyard equipment UI, disposable Editor PreviewScenes, actual enabled CanvasScaler and ScreenSpaceCamera target. No manual canvas scale/size assignment. Accepted only if renderingDisplaySize, pixelRect and scaleFactor match requested target. Text geometry includes actual rendered comparison panel labels. Deterministic preview inventory; no player save access.";
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
            public float sixRowComparisonPreferredHeight;
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

        public static void CaptureFreshScreensFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be empty.");
            Directory.CreateDirectory(output);
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            foreach (CoreGameplayPreviewScreen screen in new[] { CoreGameplayPreviewScreen.ShipyardDetail })
                foreach (Vector2Int size in new[] {new Vector2Int(1920,1080),new Vector2Int(1280,720)})
                foreach (string variant in new[] { "Unselected", "Bow", "Cannon" })
                {
                    var frame = new MenuFrame { state = screen.ToString(), width = size.x, height = size.y,
                        variant = variant, file = "ShipyardEquipment_" + variant + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                    File.WriteAllText(Path.Combine(output,"report.json"), JsonUtility.ToJson(report,true));
                }
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error))) throw new InvalidOperationException("Fresh UI capture failed; inspect report.");
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
                if (shop) host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                else host.AddComponent<MainMenuController>();
                host.SetActive(true);
                if (shop && screen == CoreGameplayPreviewScreen.ShipyardDetail)
                {
                    CoreGameplayDemoController controller = host.GetComponent<CoreGameplayDemoController>();
                    controller.ConfigureEditorShipyard(0, true, 2);
                    if (frame.variant != "Unselected")
                    {
                        // These are read-only preview selection callbacks: deterministic
                        // preview inventory is rebuilt, and save paths are play-mode guarded.
                        host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Shipyard Equipment Slot " + frame.variant).onClick.Invoke();
                        host.GetComponentsInChildren<Button>(true).First(button => button.name.StartsWith("Shipyard Equipment Owned ")).onClick.Invoke();
                    }
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
                Text comparisonStats = canvas.GetComponentsInChildren<Text>(false).Single(text => text.name == "Shipyard Equipment Selected Comparison Stats");
                string comparisonContent = comparisonStats.text;
                try
                {
                    comparisonStats.text = "船体　+10 (+10)\n装甲　+10 (+10)\n火力　+10 (+10)\n航速　+10 (+10)\n视野　+10 (+10)\n品质　110% (+20%)";
                    frame.sixRowComparisonPreferredHeight = comparisonStats.preferredHeight;
                    if (comparisonStats.preferredHeight > comparisonStats.rectTransform.rect.height + 0.1f)
                        throw new InvalidOperationException("All five effects plus quality do not fit in the comparison panel.");
                }
                finally { comparisonStats.text = comparisonContent; Canvas.ForceUpdateCanvases(); }
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
