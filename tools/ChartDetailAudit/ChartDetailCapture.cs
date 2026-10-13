// Isolated Editor-only audit fixture. Never install into the production Assets tree.
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

namespace ShatteredSail.Editor
{
    public static class ChartDetailCapture
    {
        private const int Layer = 31;
        [Serializable] public sealed class ChartMetrics
        {
            public string file;
            public int chartColumns, chartRows, coarseCellsExplored, fineCellsExplored, mixedCoarseCells;
            public float nauticalMilesPerCellX, nauticalMilesPerCellY;
            public Rect mapPixels, lookoutPanelPixels, lookoutViewportPixels;
            public Vector2[] revealSamples;
            public bool fineGridVerified, lookoutFillsPanel, voyageUnchangedDuringRender;
            public string closeupFile;
        }
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc, scope;
            public List<MenuFrame> frames = new List<MenuFrame>();
            public List<ChartMetrics> charts = new List<ChartMetrics>();
        }
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
                throw new IOException("Use a new or empty directory; prior captures are preserved.");
            Directory.CreateDirectory(output);
            var report = new Report {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Production navigation UI rendered in disposable preview scenes. Departure and CurvedExploration are in-memory art-review fixtures: " +
                    "clear preview exploration, then call the original RevealAt at departure or along a sampled curved line snapped to passable points. " +
                    "The curved fixture illustrates visibility boundaries; it is not a sailed route, gameplay timing test or route-validity claim. " +
                    "FullChart retains the public navigation preview's complete art-review knowledge. No player saves, live voyage callbacks or saved-scene changes. " +
                    "Seven full UI frames verify the actual enabled production CanvasScaler, render-target dimensions and logical canvas. " +
                    "Each 1920-wide frame also supplies a direct orthographic camera render of the chart at 2400x1350; these closeups use a frozen world-space canvas and are not claimed as production UI scaling. " +
                    "Metrics verify independent 96x64 exploration, mixed fine cells at partial boundaries, and continued full-panel lookout coverage."
            };
            foreach (string state in new[] { "Departure", "CurvedExploration", "FullChart" })
                foreach (Vector2Int size in state == "FullChart"
                    ? new[] { new Vector2Int(1920, 1080) }
                    : new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(3440,1440) })
                {
                    var frame = new MenuFrame { state=state, width=size.x, height=size.y,
                        file=state+"_"+size.x+"x"+size.y+".png" };
                    var metrics = new ChartMetrics { file=frame.file };
                    report.frames.Add(frame); report.charts.Add(metrics);
                    try { CaptureMenu(output, frame, metrics); }
                    catch (Exception exception) {
                        Exception actual = exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception;
                        frame.error=actual.ToString(); Debug.LogWarning(frame.file+": "+actual.Message);
                    }
                    File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                }
            if (report.frames.Count != 7 || report.frames.Any(frame => !frame.verifiedScaler || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Chart detail capture failed; inspect report.json.");
            Debug.Log("CHART_DETAIL_CAPTURE_OK: 7 verified UI captures and 3 camera-rendered chart closeups at "+output);
        }

        private static Vector2[] PrepareChart(CoreGameplayDemoController controller, string state)
        {
            CoreVoyageSession session = controller.Session;
            CoreVoyageMap map = session.Map;
            var samples = new List<Vector2>();
            if (state == "FullChart") return samples.ToArray();
            SetField(controller, "editorPreviewBuilding", true);
            try
            {
                Array.Clear(map.ExploredCells, 0, map.ExploredCells.Length);
                map.RestoreChartExploration(new bool[CoreVoyageMap.ChartColumns * CoreVoyageMap.ChartRows]);
                foreach (CoreMapNode node in map.Nodes) node.Discovered = false;
                VoyagePoint start = map.Departure;
                VoyagePoint end = start;
                int count = state == "Departure" ? 0 : 42;
                for (int i = 0; i <= count; i++)
                {
                    float t = count == 0 ? 0 : i / (float)count;
                    float towardX = map.Destination.X >= start.X ? 1 : -1;
                    float towardY = start.Y > CoreVoyageMap.Height * .5f ? -1 : 1;
                    VoyagePoint requested = new VoyagePoint(
                        Mathf.Clamp(start.X + towardX * 35f * t, 2f, CoreVoyageMap.Width - 2f),
                        Mathf.Clamp(start.Y + towardY * (6f * Mathf.Sin(t * Mathf.PI * 1.4f) + 5f * t), 2f, CoreVoyageMap.Height - 2f));
                    end = (VoyagePoint)Invoke(session, "SnapToNearestPassable", requested);
                    if (!map.IsPassable(end)) throw new InvalidOperationException("Observation fixture is not passable.");
                    Invoke(session, "RevealAt", end);
                    samples.Add(new Vector2(end.X, end.Y));
                }
                SetProperty(session, "Position", end);
                SetProperty(session, "Phase", CoreDemoPhase.Sailing);
                session.SetHeading(SailingDirection.East);
                Invoke(controller, "Render");
            }
            finally { SetField(controller, "editorPreviewBuilding", false); }
            return samples.ToArray();
        }
        private static void ValidateChart(CoreGameplayDemoController controller, Camera camera, MenuFrame frame, ChartMetrics metrics)
        {
            CoreVoyageMap map = controller.Session.Map;
            metrics.chartColumns = CoreVoyageMap.ChartColumns; metrics.chartRows = CoreVoyageMap.ChartRows;
            metrics.nauticalMilesPerCellX = CoreVoyageMap.Width / CoreVoyageMap.ChartColumns * map.Route.NauticalMilesPerMapUnit;
            metrics.nauticalMilesPerCellY = CoreVoyageMap.Height / CoreVoyageMap.ChartRows * map.Route.NauticalMilesPerMapUnit;
            metrics.coarseCellsExplored = map.ExploredCells.Count(value => value);
            for (int row=0; row<CoreVoyageMap.ChartRows; row++)
                for (int col=0; col<CoreVoyageMap.ChartColumns; col++)
                    if (map.IsChartCellExplored(col,row)) metrics.fineCellsExplored++;
            for (int row=0; row<CoreVoyageMap.GridRows; row++)
                for (int col=0; col<CoreVoyageMap.GridColumns; col++) {
                    int count=0;
                    for (int y=0;y<2;y++) for(int x=0;x<2;x++) if(map.IsChartCellExplored(col*2+x,row*2+y)) count++;
                    if(count>0 && count<4) metrics.mixedCoarseCells++;
                }
            metrics.fineGridVerified = CoreVoyageMap.ChartColumns == CoreVoyageMap.GridColumns * 2 &&
                CoreVoyageMap.ChartRows == CoreVoyageMap.GridRows * 2 && metrics.fineCellsExplored > 0 &&
                (frame.state == "FullChart" ? metrics.fineCellsExplored == CoreVoyageMap.ChartColumns * CoreVoyageMap.ChartRows
                    : metrics.fineCellsExplored < CoreVoyageMap.ChartColumns * CoreVoyageMap.ChartRows && metrics.mixedCoarseCells > 0);
            if (!metrics.fineGridVerified) throw new InvalidOperationException("Fine chart exploration fixture failed.");
            metrics.mapPixels = ScreenBounds(Find<RectTransform>(controller,"Map Layer"),camera);
            metrics.lookoutPanelPixels = ScreenBounds(Find<RectTransform>(controller,"Forward View Panel"),camera);
            metrics.lookoutViewportPixels = ScreenBounds(Find<RectTransform>(controller,"Forward Viewport"),camera);
            Rect ocean = ScreenBounds(Find<RawImage>(controller,"Lookout Ocean").rectTransform,camera);
            Rect transition = ScreenBounds(Find<RawImage>(controller,"Lookout Ocean Transition").rectTransform,camera);
            metrics.lookoutFillsPanel = SameRect(metrics.lookoutPanelPixels,metrics.lookoutViewportPixels) &&
                SameRect(metrics.lookoutPanelPixels,ocean) && SameRect(ocean,transition);
            if (!metrics.lookoutFillsPanel) throw new InvalidOperationException("Lookout no longer fills its upper-right panel.");
        }

        private static void CaptureMenu(string output, MenuFrame frame, ChartMetrics metrics)
        {
            Scene scene = default; RenderTexture rt = null; RenderTexture previousActive = RenderTexture.active;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                GameObject host = new GameObject("Disposable Chart Detail Audit"); host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                metrics.revealSamples = PrepareChart(controller, frame.state);
                string before = VoyageSnapshot(controller.Session);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler unexpectedly disabled.");
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                GameObject cameraHost = new GameObject("Disposable Chart Audit Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0,0,-1000); camera.cullingMask = 1 << Layer;
                camera.allowHDR = false; camera.allowMSAA = false;
                rt = new RenderTexture(frame.width,frame.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                rt.Create(); camera.targetTexture = rt; camera.rect = new Rect(0,0,1,1); camera.aspect = frame.width/(float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
                if(handle==null) throw new MissingMethodException("CanvasScaler.Handle");
                for(int pass=0;pass<4;pass++) {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler,null);
                    foreach(Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases(); Render(camera,rt);
                }
                frame.renderingDisplaySize=canvas.renderingDisplaySize; frame.canvasPixelRect=canvas.pixelRect;
                frame.cameraPixelRect=camera.pixelRect; frame.logicalCanvasSize=canvas.GetComponent<RectTransform>().rect.size;
                frame.scaleFactor=canvas.scaleFactor; frame.screenMatchMode=scaler.screenMatchMode.ToString();
                float wr=frame.width/scaler.referenceResolution.x, hr=frame.height/scaler.referenceResolution.y;
                frame.expectedScaleFactor=scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand?Mathf.Min(wr,hr):
                    scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink?Mathf.Max(wr,hr):
                    Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
                frame.verifiedScaler=scaler.enabled && Mathf.Abs(frame.renderingDisplaySize.x-frame.width)<.5f &&
                    Mathf.Abs(frame.renderingDisplaySize.y-frame.height)<.5f && Mathf.Abs(frame.canvasPixelRect.width-frame.width)<.5f &&
                    Mathf.Abs(frame.canvasPixelRect.height-frame.height)<.5f && Mathf.Abs(frame.cameraPixelRect.width-frame.width)<.5f &&
                    Mathf.Abs(frame.cameraPixelRect.height-frame.height)<.5f && Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-frame.width/frame.scaleFactor)<1f && Mathf.Abs(frame.logicalCanvasSize.y-frame.height/frame.scaleFactor)<1f;
                if(!frame.verifiedScaler) throw new InvalidOperationException("Actual CanvasScaler/render-target geometry mismatch.");
                ValidateChart(controller,camera,frame,metrics);
                WritePng(rt,Path.Combine(output,frame.file));
                if(frame.width==1920) CaptureChartCloseup(output,controller,canvas,scaler,camera,frame,metrics);
                metrics.voyageUnchangedDuringRender=before==VoyageSnapshot(controller.Session);
                if(!metrics.voyageUnchangedDuringRender) throw new InvalidOperationException("Read-only rendering changed voyage state.");
            }
            finally {
                RenderTexture.active=previousActive;
                if(scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if(rt!=null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }
        private static void CaptureChartCloseup(string output,CoreGameplayDemoController controller,Canvas canvas,CanvasScaler scaler,
            Camera camera,MenuFrame frame,ChartMetrics metrics)
        {
            RectTransform map=Find<RectTransform>(controller,"Map Layer");
            var corners=new Vector3[4]; map.GetWorldCorners(corners);
            Vector3 center=(corners[0]+corners[2])*.5f;
            float height=Vector3.Distance(corners[0],corners[1]);
            scaler.enabled=false; canvas.renderMode=RenderMode.WorldSpace;
            // Freeze the already laid-out chart, then rerender through a closer camera.
            camera.transform.position=new Vector3(center.x,center.y,center.z-100f);
            camera.orthographicSize=height*.5f; camera.aspect=2400f/1350f;
            RenderTexture closeup=new RenderTexture(2400,1350,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            try {
                closeup.Create(); camera.targetTexture=closeup;
                Canvas.ForceUpdateCanvases(); Render(camera,closeup);
                metrics.closeupFile=frame.state+"_ChartCloseup_2400x1350.png";
                WritePng(closeup,Path.Combine(output,metrics.closeupFile));
            } finally { camera.targetTexture=null; closeup.Release(); Object.DestroyImmediate(closeup); }
        }
        private static void WritePng(RenderTexture rt,string path)
        {
            RenderTexture previous=RenderTexture.active; Texture2D pixels=null;
            try {
                RenderTexture.active=rt; pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false,false);
                pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); pixels.Apply(false,false);
                Color32[] samples=pixels.GetPixels32(); int low=255,high=0;
                for(int i=0;i<samples.Length;i+=997) { int b=(samples[i].r+samples[i].g+samples[i].b)/3; low=Math.Min(low,b); high=Math.Max(high,b); }
                if(high-low<20) throw new InvalidOperationException("Nearly blank render rejected.");
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            } finally { RenderTexture.active=previous; if(pixels!=null)Object.DestroyImmediate(pixels); }
        }
        private static void Render(Camera camera,RenderTexture rt) {
            if(GraphicsSettings.currentRenderPipeline!=null) RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=rt });
            else camera.Render();
        }
        private static string VoyageSnapshot(CoreVoyageSession s) => s.Phase+";"+s.Position.X+";"+s.Position.Y+";"+s.TotalHours+";"+
            s.TotalDistanceNm+";"+string.Join(",",s.Map.ExploredCells.Select(v=>v?"1":"0"))+";"+
            string.Join(",",s.Map.CaptureChartExploration().Select(v=>v?"1":"0"))+";"+s.Cargo.Gold+";"+s.Cargo.Water+";"+s.Cargo.Food;
        private static T Find<T>(CoreGameplayDemoController c,string name) where T:Component => c.GetComponentsInChildren<T>(false).Single(item=>item.name==name);
        private static object Invoke(object target,string name,params object[] args) => target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        private static void SetField(object target,string name,object value) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static void SetProperty(object target,string name,object value) => target.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public).GetSetMethod(true).Invoke(target,new[]{value});
        private static Rect ScreenBounds(RectTransform t,Camera camera) { var corners=new Vector3[4]; t.GetWorldCorners(corners); var p=corners.Select(v=>(Vector2)camera.WorldToScreenPoint(v)).ToArray(); return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y)); }
        private static bool SameRect(Rect a,Rect b) => Mathf.Abs(a.xMin-b.xMin)<.5f && Mathf.Abs(a.xMax-b.xMax)<.5f && Mathf.Abs(a.yMin-b.yMin)<.5f && Mathf.Abs(a.yMax-b.yMax)<.5f;
    }
}
