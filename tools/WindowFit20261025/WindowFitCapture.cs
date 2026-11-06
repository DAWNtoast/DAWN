// Install in an isolated validation project's Editor directory only.
// Uses production builders and CanvasScaler; saves no scenes, assets or PlayerPrefs.
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
    public static class WindowFitCapture
    {
        private const int Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string projectPath;
            public string scope = "Production workshop and ship equipment UI in disposable PreviewScenes, with enabled CanvasScaler. Each screen resizes the same existing hierarchy through four resolutions and back to 1920x1080. Geometry checks use actual screen bounds. No scene, asset, profile or PlayerPrefs writes.";
            public List<Frame> frames = new List<Frame>();
            public List<Restoration> restorations = new List<Restoration>();
        }
        [Serializable] public sealed class Frame
        {
            public string screen, file, error, screenMatchMode;
            public int width, height, hierarchyId, visibleGraphicCount, paperCount;
            public bool scalerVerified, headerVerified, surfaceVerified, contentVerified, hierarchyRetained;
            public float scaleFactor, expectedScaleFactor, surfaceCoverage, contentWidthCoverage, contentHeightCoverage, paperCoverage;
            public Vector2 canvasSize, displaySize;
            public Rect headerScreen, availableScreen, surfaceScreen, contentScreen;
            public List<Element> elements = new List<Element>();
            public List<string> violations = new List<string>();
        }
        [Serializable] public sealed class Restoration
        {
            public string from, destination, error;
            public bool passed, parentRestored, sizeRestored, letterboxRestored;
        }
        [Serializable] public sealed class Element
        {
            public string name, kind, text;
            public Rect screenRect;
        }

        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use an isolated project below D:/Game/tmp.");
            string[] arguments = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(arguments, "-windowFitCapturePath");
            if (option < 0 || option + 1 >= arguments.Length || !Path.IsPathRooted(arguments[option + 1]))
                throw new ArgumentException("Pass -windowFitCapturePath NEW_ABSOLUTE_DIRECTORY.");
            string output = Path.GetFullPath(arguments[option + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/Artifacts/WindowFit20261025/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Output must be below D:/Game/Artifacts/WindowFit20261025.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a fresh output directory.");
            Directory.CreateDirectory(output);
            var report = new Report { projectPath = project };
            foreach (bool equipment in new[] { false, true }) CaptureScreen(output, report, equipment);
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            WriteGallery(output, report);
            if (report.frames.Count != 10 || report.frames.Any(frame => !string.IsNullOrEmpty(frame.error)) || report.restorations.Count != 4 || report.restorations.Any(item => !item.passed))
                throw new InvalidOperationException("Window fit capture failed; inspect report.json.");
            Debug.Log("WINDOW_FIT_CAPTURE_OK: " + report.frames.Count + " frames in " + output);
        }

        private static void CaptureScreen(string output, Report report, bool equipment)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Window Fit Review");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(equipment ? CoreGameplayPreviewScreen.Shipyard : CoreGameplayPreviewScreen.CardWorkshop,
                    CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                if (equipment)
                {
                    controller.ConfigureEditorShipyard(0, true, 2);
                    ArrangeEquipment(controller);
                }
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production scaler is disabled.");
                var cameraHost = new GameObject("Disposable Window Fit Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer;
                camera.allowHDR = camera.allowMSAA = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                string rootName = equipment ? "Shipyard Detail" : "Card Workshop Preview Panel";
                int hierarchyId = Find(canvas, rootName).GetInstanceID();
                var sizes = new[] { new Vector2Int(1920,1080), new Vector2Int(1280,1024), new Vector2Int(2560,1080), new Vector2Int(3389,1908), new Vector2Int(1920,1080) };
                for (int index = 0; index < sizes.Length; index++)
                {
                    Vector2Int size = sizes[index];
                    var frame = new Frame { screen = equipment ? "Equipment" : "Workshop", width = size.x, height = size.y,
                        file = (equipment ? "Equipment" : "Workshop") + "_" + index.ToString("D2") + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try
                    {
                        if (target != null) { camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); }
                        target = new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                        target.Create(); camera.targetTexture = target; camera.aspect = size.x/(float)size.y;
                        MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                        if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                        for (int pass = 0; pass < 5; pass++)
                        {
                            Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                            // A synchronous executeMethod has no frame loop. Run the production
                            // layout LateUpdates exactly as an Editor frame would, without rebuilding UI.
                            foreach (MonoBehaviour behaviour in canvas.GetComponentsInChildren<MonoBehaviour>(false))
                                if (behaviour.GetType().Name == "ShipyardEquipmentLayout")
                                    behaviour.GetType().GetMethod("LateUpdate", Private)?.Invoke(behaviour, null);
                            foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                            Render(camera,target);
                        }
                        SavePixels(output,frame,target);
                        Inspect(canvas,camera,scaler,frame,equipment,hierarchyId);
                    }
                    catch (Exception exception) { frame.error = exception.ToString(); Debug.LogWarning(frame.file + ": " + exception.Message); }
                    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
                    Debug.Log("WINDOW_FIT_FRAME: " + frame.file + " " + (string.IsNullOrEmpty(frame.error) ? "OK" : "FAILED"));
                }
                VerifyRestoration(controller, canvas, equipment, report);
            }
            finally
            {
                RenderTexture.active = previous;
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void VerifyRestoration(CoreGameplayDemoController controller, Canvas canvas, bool equipment, Report report)
        {
            foreach (CoreGameplayPreviewScreen destination in new[] { CoreGameplayPreviewScreen.Shipyard, CoreGameplayPreviewScreen.PortHub })
            {
                var result = new Restoration { from = equipment ? "Equipment" : "Workshop", destination = destination.ToString() };
                report.restorations.Add(result);
                try
                {
                    Set(controller, "editorPreviewScreen", equipment ? CoreGameplayPreviewScreen.ShipyardDetail : CoreGameplayPreviewScreen.CardWorkshop);
                    Set(controller, "editorPreviewShipPage", equipment ? 2 : 0);
                    typeof(CoreGameplayDemoController).GetMethod("BuildSelectedEditorPreview", Private).Invoke(controller, null);
                    RectTransform content = (RectTransform)typeof(CoreGameplayDemoController).GetField("contentRoot", Private).GetValue(controller);
                    Transform expectedParent = (Transform)typeof(CoreGameplayDemoController).GetField("fullWindowFacilityViewportParent", Private).GetValue(controller);
                    if (content.parent != canvas.transform || expectedParent == null)
                        throw new InvalidOperationException("Fixture failed to enter a full-window facility.");
                    Set(controller, "editorPreviewScreen", destination);
                    Set(controller, "editorPreviewShipPage", 0);
                    typeof(CoreGameplayDemoController).GetMethod("BuildSelectedEditorPreview", Private).Invoke(controller, null);
                    Canvas.ForceUpdateCanvases();
                    result.parentRestored = content.parent == expectedParent && content.parent.name == FixedGameViewport.Name;
                    result.sizeRestored = Vector2.Distance(content.sizeDelta, PhaseSurfaceLayout.ContentSize) < .01f &&
                        Vector2.Distance(content.anchorMin,Vector2.one*.5f)<.01f && Vector2.Distance(content.anchorMax,Vector2.one*.5f)<.01f && content.anchoredPosition.sqrMagnitude < .01f;
                    Transform letterbox = canvas.transform.Find("Game Letterbox Overlay");
                    result.letterboxRestored = letterbox != null && letterbox.gameObject.activeSelf;
                    result.passed = result.parentRestored && result.sizeRestored && result.letterboxRestored;
                    if (!result.passed) throw new InvalidOperationException("Normal facility viewport or letterbox was not restored.");
                }
                catch (Exception exception) { result.error = exception.ToString(); Debug.LogWarning("WINDOW_FIT_RESTORE: " + result.from + " -> " + destination + ": " + exception.Message); }
            }
        }
        private static void ArrangeEquipment(CoreGameplayDemoController controller)
        {
            var catalog = new ComponentArtCatalog();
            int serial = 0;
            var inventory = new OwnedShipComponentInventory(catalog, () => "window-fit-" + serial++);
            inventory.EnsureLoadout(0);
            OwnedShipComponent selected = inventory.Grant(catalog.GetById("bow_freewake_lance"));
            foreach (ComponentSlot slot in new[] { ComponentSlot.Sail, ComponentSlot.Cannon, ComponentSlot.Plating })
                inventory.Grant(catalog.Entries.First(entry => entry.Slot == slot && entry.Rarity == ComponentRarity.Rare));
            Set(controller,"componentInventory",inventory);
            Set(controller,"selectedShipyardComponentInstanceId",selected.InstanceId);
            Set(controller,"selectedShipyardComponentSlot",ComponentSlot.Bow);
            Set(controller,"shipyardInventoryShowAll",true);
            typeof(CoreGameplayDemoController).GetMethod("BuildSelectedEditorPreview",Private).Invoke(controller,null);
        }

        private static void Inspect(Canvas canvas,Camera camera,CanvasScaler scaler,Frame frame,bool equipment,int expectedId)
        {
            Rect screen = new Rect(0,0,frame.width,frame.height);
            frame.scaleFactor = canvas.scaleFactor;
            frame.expectedScaleFactor = Mathf.Min(frame.width/scaler.referenceResolution.x,frame.height/scaler.referenceResolution.y);
            frame.canvasSize = canvas.GetComponent<RectTransform>().rect.size;
            frame.displaySize = canvas.renderingDisplaySize;
            frame.screenMatchMode = scaler.screenMatchMode.ToString();
            frame.scalerVerified = scaler.enabled && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand &&
                Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<.001f && SameRect(canvas.pixelRect,screen,1f) && SameRect(camera.pixelRect,screen,1f);
            RectTransform header = Find(canvas,"Port Facility Header");
            frame.headerScreen = Bounds(camera,header);
            frame.headerVerified = Contains(screen,frame.headerScreen,1f) &&
                Mathf.Abs(frame.headerScreen.xMin)<1f && Mathf.Abs(frame.headerScreen.xMax-frame.width)<1f && Mathf.Abs(frame.headerScreen.yMax-frame.height)<1f;
            frame.availableScreen = Rect.MinMaxRect(0,0,frame.width,frame.headerScreen.yMin);
            RectTransform surface = Find(canvas,equipment ? "Shipyard Equipment Paper" : "Card Workshop Preview Panel");
            RectTransform content = Find(canvas,equipment ? "Shipyard Detail" : "Card Workshop Preview Panel");
            frame.hierarchyId = content.GetInstanceID(); frame.hierarchyRetained = frame.hierarchyId == expectedId;
            frame.surfaceScreen = Bounds(camera,surface);
            frame.surfaceCoverage = frame.surfaceScreen.width*frame.surfaceScreen.height/(frame.availableScreen.width*frame.availableScreen.height);
            frame.surfaceVerified = SameRect(frame.surfaceScreen,frame.availableScreen,1.5f);
            Graphic[] graphics = content.GetComponentsInChildren<Graphic>(false).Where(Visible).ToArray();
            frame.visibleGraphicCount = graphics.Length;
            frame.contentScreen = Union(graphics.Select(graphic => Bounds(camera,graphic.rectTransform)).ToArray());
            frame.contentWidthCoverage = frame.contentScreen.width/frame.availableScreen.width;
            frame.contentHeightCoverage = frame.contentScreen.height/frame.availableScreen.height;
            foreach (Graphic graphic in graphics)
            {
                Rect bounds = Bounds(camera,graphic.rectTransform);
                frame.elements.Add(new Element { name=graphic.name,kind=graphic.GetType().Name,text=graphic is Text text ? text.text : null,screenRect=bounds });
                if (!Contains(frame.availableScreen,bounds,2f*frame.scaleFactor)) frame.violations.Add(graphic.name+" outside content area: "+bounds);
            }
            if (equipment)
            {
                Rect vessel = Bounds(camera,Find(canvas,"Shipyard Equipment Vessel Surface"));
                Rect inventory = Bounds(camera,Find(canvas,"Shipyard Equipment Inventory"));
                Rect current = Bounds(camera,Find(canvas,"Shipyard Equipment Current Comparison"));
                Rect selected = Bounds(camera,Find(canvas,"Shipyard Equipment Selected Comparison"));
                Rect action = Bounds(camera,Find(canvas,"Shipyard Equip Component"));
                if (!(vessel.xMax < inventory.xMin && inventory.xMax < current.xMin && current.xMax < selected.xMin))
                    frame.violations.Add("Equipment columns overlap or are out of order.");
                if (action.yMax > current.yMin+1f || action.yMax > selected.yMin+1f)
                    frame.violations.Add("Equipment action overlaps comparison cards.");
                if (Find(canvas,"Shipyard Equip Component").GetComponent<Button>().interactable)
                    frame.violations.Add("Editor fixture permits inventory changes.");
            }
            if (!equipment)
            {
                float paperArea = 0f;
                foreach (RectTransform paperSurface in content.GetComponentsInChildren<RectTransform>(false).Where(rect => rect.name.EndsWith(" Surface")))
                {
                    string paperName = paperSurface.name.Substring(0,paperSurface.name.Length-8);
                    Transform texture = paperSurface.Find(paperName+" Texture");
                    if (texture == null) continue;
                    frame.paperCount++;
                    Rect paperBounds = Bounds(camera,paperSurface);
                    paperArea += paperBounds.width*paperBounds.height;
                    if (!SameRect(paperBounds,Bounds(camera,(RectTransform)texture),1f)) frame.violations.Add(paperName+" texture does not fill its surface.");
                    foreach (Graphic graphic in paperSurface.GetComponentsInChildren<Graphic>(false).Where(Visible))
                        if (!Contains(paperBounds,Bounds(camera,graphic.rectTransform),2f*frame.scaleFactor)) frame.violations.Add(graphic.name+" escapes its paper surface.");
                    Transform paperContent = paperSurface.Find(paperName);
                    if (paperContent != null && Mathf.Abs(paperContent.localScale.x-paperContent.localScale.y)>.001f)
                        frame.violations.Add(paperName+" distorts its content aspect ratio.");
                }
                frame.paperCoverage = paperArea/(frame.availableScreen.width*frame.availableScreen.height);
                if (frame.paperCount != 5 || frame.paperCoverage < .97f) frame.violations.Add("Workshop paper coverage or paper count is incomplete.");
            }
            frame.contentVerified = graphics.Length>0 && frame.violations.Count==0 && frame.contentWidthCoverage>.94f && frame.contentHeightCoverage>.94f;
            if (!frame.scalerVerified || !frame.headerVerified || !frame.surfaceVerified || !frame.contentVerified || !frame.hierarchyRetained)
                throw new InvalidOperationException("Fit checks: scaler="+frame.scalerVerified+", header="+frame.headerVerified+", surface="+frame.surfaceVerified+
                    ", content="+frame.contentVerified+", retained="+frame.hierarchyRetained+", coverage="+frame.contentWidthCoverage+"x"+frame.contentHeightCoverage+"; "+string.Join("; ",frame.violations.Take(10)));
        }

        private static void Set(CoreGameplayDemoController controller,string name,object value)
        { typeof(CoreGameplayDemoController).GetField(name,Private).SetValue(controller,value); }
        private static RectTransform Find(Canvas canvas,string name)
        { return canvas.GetComponentsInChildren<RectTransform>(false).Single(rect=>rect.name==name); }
        private static bool Visible(Graphic graphic) => graphic.isActiveAndEnabled && graphic.color.a>.001f && (!(graphic is Text text) || !string.IsNullOrEmpty(text.text));
        private static Rect Bounds(Camera camera,RectTransform rect)
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3[] points=corners.Select(camera.WorldToScreenPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        private static Rect Union(Rect[] bounds) => bounds.Length==0 ? new Rect() : Rect.MinMaxRect(bounds.Min(r=>r.xMin),bounds.Min(r=>r.yMin),bounds.Max(r=>r.xMax),bounds.Max(r=>r.yMax));
        private static bool Contains(Rect outer,Rect inner,float tolerance) => inner.xMin>=outer.xMin-tolerance && inner.xMax<=outer.xMax+tolerance && inner.yMin>=outer.yMin-tolerance && inner.yMax<=outer.yMax+tolerance;
        private static bool SameRect(Rect a,Rect b,float tolerance) => Mathf.Abs(a.xMin-b.xMin)<=tolerance && Mathf.Abs(a.xMax-b.xMax)<=tolerance && Mathf.Abs(a.yMin-b.yMin)<=tolerance && Mathf.Abs(a.yMax-b.yMax)<=tolerance;
        private static void Render(Camera camera,RenderTexture target)
        {
            Canvas.ForceUpdateCanvases();
            if(GraphicsSettings.currentRenderPipeline!=null) RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
            else camera.Render();
        }
        private static void SavePixels(string output,Frame frame,RenderTexture target)
        {
            Texture2D pixels=null;
            try
            {
                RenderTexture.active=target; pixels=new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false,false);
                pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0); pixels.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());
            }
            finally { if(pixels!=null) Object.DestroyImmediate(pixels); }
        }
        private static void WriteGallery(string output,Report report)
        {
            var html=new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>窗口尺寸适配验收</title><style>body{background:#17211f;color:#f1e7cf;font:16px system-ui;margin:24px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0}img{width:100%}a{color:#e3c184}figcaption{padding:8px 0}</style><h1>窗口尺寸适配验收</h1><p>实际 Unity 生产 UI 与 CanvasScaler，同一界面连续切换四种窗口尺寸后回到初始尺寸。<a href=\"report.json\">几何检查报告</a></p><main>");
            foreach(Frame frame in report.frames) html.Append("<figure><a href=\"").Append(frame.file).Append("\"><img loading=\"lazy\" src=\"").Append(frame.file).Append("\"></a><figcaption>").Append(frame.screen=="Workshop"?"卡组工坊":"配件装配").Append(" · ").Append(frame.width).Append(" × ").Append(frame.height).Append(string.IsNullOrEmpty(frame.error)?" · 通过":" · 待检查").Append("</figcaption></figure>");
            html.Append("</main></html>"); File.WriteAllText(Path.Combine(output,"index.html"),html.ToString(),new UTF8Encoding(false));
        }
    }
}
