// Temporary Editor-only entry: install only in an isolated validation project.
// CaptureMenu's renderer/scaler verification is copied from UiAuditReadOnlyCapture.
// The sole extension arranges an in-memory cargo preview and opens read-only UI.
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
    public static class TextPolishCargoCapture
    {
        private const int Layer = 31;
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Captures must be below an Artifacts directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new or empty directory; existing captures are preserved.");
            Directory.CreateDirectory(output);
            var report = new UiAuditReadOnlyCapture.MenuReport {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Read-only cargo and warehouse item description previews at two production scaler targets. All 15 descriptions checked against their actual text rectangle. " +
                    "Cargo amounts are an isolated visual fixture; only open/tab/row callbacks run. Confirm is asserted disabled and never invoked. " +
                    "Actual enabled CanvasScaler/display/viewport geometry is verified; no player saves, scene regeneration, discard, or performance measurement. " +
                    "Text geometry includes underlying content occluded by the modal and masked journals; it is not an occlusion-aware accessibility result."
            };
            foreach (string state in new[] { "CargoWater", "CargoGem", "CargoCompass", "WarehouseWater", "WarehouseGem", "WarehouseRelic" })
                foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720) })
                {
                    var frame = new MenuFrame { state=state, width=size.x, height=size.y, file=state+"_"+size.x+"x"+size.y+".png" };
                    report.frames.Add(frame);
                    try { CaptureMenu(output, frame); }
                    catch (Exception exception) { frame.error=exception.ToString(); Debug.LogWarning(frame.file+": "+exception.Message); }
                    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                }
            if (report.frames.Count != 12 || report.frames.Any(frame => !frame.verifiedScaler || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Voyage header/cargo capture failed; inspect report.json.");
            Debug.Log("VOYAGE_HEADER_CARGO_CAPTURE_OK: 12 verified production cargo-description captures at "+output);
        }

        private static void PrepareCargoView(CoreGameplayDemoController controller, string state)
        {
            CoreVoyageSession session = controller.Session;
            session.Cargo.Water=100; session.Cargo.Food=100; session.Cargo.Wood=8; session.Cargo.Iron=3;
            session.Cargo.Cloth=3; session.Cargo.Beer=3; session.Cargo.Medicine=3; session.Cargo.Powder=3; session.Cargo.Cannonballs=3;
            session.Cargo.Gem=3; session.Cargo.AncientMaterial=2; session.Cargo.Relic=1;
            session.Cargo.BlackPearl=1; session.Cargo.AncientCompass=1; session.Cargo.Lantern=1;
            string cargo = CargoSnapshot(session);
            double hours = session.TotalHours;
            CoreDemoPhase phase = session.Phase;
            PreviewButton(controller,"Voyage Cargo").onClick.Invoke();
            if (!controller.GetComponentsInChildren<Transform>(true).Any(item => item.gameObject.activeInHierarchy && item.name == "Voyage Cargo Overlay"))
                throw new InvalidOperationException("Production cargo preview did not open.");
            if (state != "CargoWater") PreviewButton(controller,"Voyage Cargo Special Tab").onClick.Invoke();
            if (state.StartsWith("Cargo", StringComparison.Ordinal))
            {
                PreviewButton(controller,"Voyage Cargo Item " + (state == "CargoWater" ? "water" : state == "CargoCompass" ? "ancientcompass" : "gem")).onClick.Invoke();
                if (PreviewButton(controller,"Confirm Voyage Cargo Discard").IsInteractable())
                    throw new InvalidOperationException("Editor preview must never enable cargo discard.");
            }
            if (CargoSnapshot(session) != cargo || session.TotalHours != hours || session.Phase != phase)
                throw new InvalidOperationException("Read-only cargo preview changed voyage state.");
        }

        private static void PrepareWarehouseView(CoreGameplayDemoController controller, string state)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(CoreGameplayDemoController);
            type.GetProperty("warehouseStorageOpen", flags).SetValue(controller, true);
            type.GetField("warehouseRareStorageOpen", flags).SetValue(controller, state != "WarehouseWater");
            type.GetMethod("Render", flags).Invoke(controller, null);
            string key = state == "WarehouseWater" ? "water" : state == "WarehouseRelic" ? "relic" : "gem";
            string before = CargoSnapshot(controller.Session);
            object quote = type.GetMethod("CreateWarehouseCargoQuote", flags, null, new[] {typeof(string),typeof(bool)}, null)
                .Invoke(controller, new object[] {key, true});
            type.GetMethod("BuildWarehouseCargoActions", flags).Invoke(controller, new[] {quote});
            if (CargoSnapshot(controller.Session) != before) throw new InvalidOperationException("Warehouse preview changed cargo.");
        }

        private static void CheckCargoDescriptions(Canvas canvas)
        {
            Text label = canvas.GetComponentsInChildren<Text>(false).Single(t => t.name == "Voyage Cargo Item Description" || t.name == "Warehouse Item Description");
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            using (var generator = new TextGenerator())
                foreach (var item in CoreWarehouseItemCatalog.All)
                {
                    float height = generator.GetPreferredHeight(item.Description, settings) / label.pixelsPerUnit;
                    if (height > label.rectTransform.rect.height + 1f)
                        throw new InvalidOperationException("Description clipped: " + item.Key + " " + height + "/" + label.rectTransform.rect.height);
                }
        }

        private static Button PreviewButton(CoreGameplayDemoController controller, string name) =>
            controller.GetComponentsInChildren<Button>(true).Single(button => button.name == name && button.gameObject.activeInHierarchy);
        private static string CargoSnapshot(CoreVoyageSession session) =>
            string.Join(",",CoreWarehouseItemCatalog.All.Select(item => item.Key+"="+session.GetWarehouseCargoAmount(item.Key)))+";gold="+session.Cargo.Gold;

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
                CoreGameplayPreviewScreen screen = frame.state.StartsWith("Warehouse", StringComparison.Ordinal) ? CoreGameplayPreviewScreen.Warehouse : CoreGameplayPreviewScreen.Navigation;
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                if (frame.state.StartsWith("Cargo", StringComparison.Ordinal)) PrepareCargoView(controller, frame.state);
                else PrepareWarehouseView(controller, frame.state);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
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
                CheckCargoDescriptions(canvas);
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
    }
}
