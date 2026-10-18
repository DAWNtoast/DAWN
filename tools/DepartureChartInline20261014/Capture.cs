using System;
using System.Collections;
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

namespace ShatteredSail.Editor
{
    // Copied only into this task's independent project. No saved scenes or PlayerPrefs writes.
    public static class DepartureChartInlineCapture20261014
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string scope = "Four 1920x1080 production dock states with the real CanvasScaler, transient preview scenes and memory-only chart records. No saved scene, player archive or image API is used.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string state, file, selectedSeed, background;
            public int width = 1920, height = 1080, recordCount;
            public bool passed, verifiedScaler;
            public float scaleFactor, expectedScaleFactor;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect, recordsBounds, previewBounds;
            public List<string> interactions = new List<string>();
            public List<string> errors = new List<string>();
            public List<Control> controls = new List<Control>();
        }
        [Serializable] private sealed class Control
        {
            public string name, label;
            public bool interactable;
            public Rect bounds;
        }

        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
            if (!project.Equals("D:/Game/tmp/departure-chart-inline-20261014", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This capture requires its task-owned isolated project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Stopped Editor with graphics is required.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-departureChartInlineOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Pass -departureChartInlineOutput NEW_ABSOLUTE_DIR.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Replace('\\','/').StartsWith("D:/Game/Artifacts/DepartureChartInline20261014/", StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
                throw new InvalidOperationException("Use a fresh task artifact directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (string state in new[] { "ordinary", "chart-empty", "chart-selected", "list-scroll" })
                    Capture(report, output, state);
                report.passed = report.frames.Count == 4 && report.frames.All(frame => frame.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
            if (!report.passed) throw new InvalidOperationException("Departure chart capture failed; inspect report.json.");
            Debug.Log("DEPARTURE_CHART_INLINE_CAPTURE_OK: four verified 1920x1080 frames at " + output);
        }

        private static void Capture(Report report, string output, string state)
        {
            var frame = new Frame { state=state, file=state+"_1920x1080.png" };
            report.frames.Add(frame);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var host = new GameObject("Departure Chart Inline Capture Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var gameplay = host.AddComponent<CoreGameplayDemoController>();
                gameplay.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                gameplay.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience=CoreVoyageSession.PirateExperienceForLevel(3) });
                InstallMemoryRecords(gameplay, state == "chart-selected" ? 2 : state == "list-scroll" ? 12 : 0);
                gameplay.Session.Cargo.Water = gameplay.Session.Cargo.Food = 80;
                Call(gameplay, "Render");
                Texture background = Find<RawImage>(host,"Voyage Background").texture;
                frame.background = background == null ? "missing" : background.name;
                RectTransform vessel = Find<RectTransform>(host,"Inspect Departure Vessel");
                Vector2 vesselSize = vessel.sizeDelta, vesselPosition = vessel.anchoredPosition;
                if (state != "ordinary")
                {
                    Find<Toggle>(host,"Use Chart Seed").isOn = true;
                    frame.interactions.Add("Enabled the production Use Chart Seed toggle.");
                    Button[] rows = RecordButtons(host);
                    frame.recordCount = rows.Length;
                    if (state == "chart-empty")
                    {
                        if (rows.Length != 0) frame.errors.Add("Empty archive displayed records.");
                        if (Find<Button>(host,"Start Voyage").IsInteractable()) frame.errors.Add("Empty selection allowed departure.");
                    }
                    else
                    {
                        if (rows.Length != (state == "list-scroll" ? 12 : 2)) throw new InvalidOperationException("Available chart record count is wrong: "+rows.Length);
                        if (state == "list-scroll")
                        {
                            ScrollRect scroll = Find<ScrollRect>(host,"Departure Dock Chart Records");
                            Canvas.ForceUpdateCanvases();
                            if (scroll.content.rect.height <= scroll.viewport.rect.height) frame.errors.Add("Long list cannot scroll.");
                            scroll.verticalNormalizedPosition = 0f;
                            frame.interactions.Add("Scrolled the fixed record viewport to the last entry.");
                        }
                        rows.Last().onClick.Invoke();
                        frame.selectedSeed = gameplay.Session.SelectedSeed;
                        if (!gameplay.Session.IsChartSeedSelected) frame.errors.Add("Click did not select a chart seed.");
                        if (Find<InputField>(host,"Seed Input").text != frame.selectedSeed) frame.errors.Add("Editable seed did not follow the record click.");
                        if (!Find<Button>(host,"Start Voyage").IsInteractable()) frame.errors.Add("Valid selected chart disabled departure.");
                        frame.interactions.Add("Clicked the final visible record and verified its seed and departure state.");
                    }
                }
                if (Find<RawImage>(host,"Voyage Background").texture != background) frame.errors.Add("Chart mode replaced the existing dock background.");
                foreach (string name in new[] { "Route Selection", "Voyage Setup" })
                {
                    RectTransform root = Find<RectTransform>(host,name);
                    if (root.sizeDelta != PhaseSurfaceLayout.ContentSize || root.anchoredPosition != Vector2.zero || root.GetComponent<Image>().color.a != 0f)
                        frame.errors.Add("Dock scene root lost its transparent full-content layout: "+name);
                }
                vessel = Find<RectTransform>(host,"Inspect Departure Vessel");
                if (vessel.sizeDelta != vesselSize || vessel.anchoredPosition != vesselPosition) frame.errors.Add("Chart mode moved the vessel.");

                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production scaler must remain enabled.");
                var cameraHost = new GameObject("Departure Chart Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0,0,-1000);
                camera.cullingMask = 1 << 31;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(frame.width,frame.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0,0,1,1);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100;
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int pass=0; pass<4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler,null);
                    foreach (Text label in canvas.GetComponentsInChildren<Text>(true)) label.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (state == "list-scroll") Find<ScrollRect>(host,"Departure Dock Chart Records").verticalNormalizedPosition=0f;
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=target });
                    else camera.Render();
                }
                frame.renderingDisplaySize = canvas.renderingDisplaySize;
                frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
                frame.canvasPixelRect = canvas.pixelRect;
                frame.cameraPixelRect = camera.pixelRect;
                frame.scaleFactor = canvas.scaleFactor;
                float wr=frame.width/scaler.referenceResolution.x, hr=frame.height/scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr,hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr,hr) : Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled && Mathf.Abs(frame.renderingDisplaySize.x-frame.width)<.5f && Mathf.Abs(frame.renderingDisplaySize.y-frame.height)<.5f &&
                    Mathf.Abs(frame.canvasPixelRect.width-frame.width)<.5f && Mathf.Abs(frame.canvasPixelRect.height-frame.height)<.5f &&
                    Mathf.Abs(frame.cameraPixelRect.width-frame.width)<.5f && Mathf.Abs(frame.cameraPixelRect.height-frame.height)<.5f &&
                    Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-frame.width/frame.scaleFactor)<1 && Mathf.Abs(frame.logicalCanvasSize.y-frame.height/frame.scaleFactor)<1;
                if (!frame.verifiedScaler) frame.errors.Add("Render target did not drive the production scaler.");
                if (state != "ordinary") frame.recordsBounds=Bounds(Find<RectTransform>(host,"Departure Dock Chart Records"),camera);
                if (state == "chart-selected" || state == "list-scroll") frame.previewBounds=Bounds(Find<RectTransform>(host,"Departure Chart Preview"),camera);
                foreach (Selectable control in canvas.GetComponentsInChildren<Selectable>(false))
                    frame.controls.Add(new Control { name=control.name, label=control.GetComponentInChildren<Text>()?.text, bounds=Bounds(control.GetComponent<RectTransform>(),camera), interactable=control.IsInteractable() });
                RenderTexture.active = target;
                pixels = new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false,false);
                pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);
                pixels.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());
                frame.passed = frame.errors.Count == 0;
            }
            catch (Exception exception) { frame.errors.Add(exception.ToString()); frame.passed=false; }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void InstallMemoryRecords(CoreGameplayDemoController game,int count)
        {
            Type recordType = typeof(CoreGameplayDemoController).GetNestedType("DepartureChartRecord",BindingFlags.NonPublic);
            IList records=(IList)typeof(CoreGameplayDemoController).GetField("recordedDepartureCharts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
            records.Clear();
            for (int i=0;i<count;i++)
            {
                int route=count == 2 && i == 1 ? 1 : 0;
                string destination=route == 1 ? "铜钟港" : "白贝港";
                string seed=CoreVoyageSession.CreateChartSeed(route,"断锚港",destination);
                CoreVoyageSession.TryParseChartSeed(seed,out CoreSeaChartDefinition definition);
                object record=Activator.CreateInstance(recordType,true);
                foreach (KeyValuePair<string,object> value in new Dictionary<string,object>
                {
                    {"seed",seed},{"routeIndex",route},{"mapGeometryVersion",definition.MapGeometryVersion},
                    {"routeName",CoreVoyageSession.Routes[route].Name},{"departurePort","断锚港"},{"destinationPort",destination},
                    {"tutorialWaters",false},{"coverage",i*7f},
                }) recordType.GetField(value.Key).SetValue(record,value.Value);
                records.Add(record);
            }
        }
        private static Button[] RecordButtons(GameObject host) => host.GetComponentsInChildren<Button>(false)
            .Where(button => button.name.StartsWith("Departure Dock Chart Record "))
            .OrderBy(button => int.Parse(button.name.Substring("Departure Dock Chart Record ".Length))).ToArray();
        private static void Call(object instance,string method) => instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(instance,null);
        private static T Find<T>(GameObject host,string name) where T:Component => host.GetComponentsInChildren<T>(true).Single(item => item.name == name && item.gameObject.activeInHierarchy);
        private static Rect Bounds(RectTransform rect,Camera camera)
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue), max=new Vector2(float.MinValue,float.MinValue);
            foreach (Vector3 corner in corners) { Vector2 point=camera.WorldToScreenPoint(corner); min=Vector2.Min(min,point); max=Vector2.Max(max,point); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
