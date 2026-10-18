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

namespace ShatteredSail.Editor
{
    // Installed only into the isolated validation project. All scenes are transient.
    public static class SecondaryPanelsCapture20261014
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string scope = "Four production secondary panels at 1080p, 720p and 5:4. Real enabled CanvasScaler, read-only open/tab/selection/draft/close callbacks. No scene generation, profile save, fullscreen change or cargo discard.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string state, file;
            public int width, height;
            public bool passed, verifiedScaler;
            public float scaleFactor, expectedScaleFactor;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect;
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
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("An isolated validation project is required.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Stopped Editor with graphics is required.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-secondaryPanelsOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Pass -secondaryPanelsOutput NEW_ABSOLUTE_DIR.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Replace('\\','/').StartsWith("D:/Game/Artifacts/SecondaryPanels20261014/", StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
                throw new InvalidOperationException("Use a fresh task artifact directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1280,1024) })
                    foreach (string state in new[] { "MainSettings", "Achievements", "VoyageCargo", "PortSettings" })
                        Capture(report, output, state, size.x, size.y);
                report.passed = report.frames.Count == 12 && report.frames.All(frame => frame.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
            if (!report.passed) throw new InvalidOperationException("Secondary panels capture failed; inspect report.json.");
            Debug.Log("SECONDARY_PANELS_CAPTURE_OK: 12 verified frames at " + output);
        }

        private static void Capture(Report report, string output, string state, int width, int height)
        {
            var frame = new Frame { state=state, width=width, height=height, file=state+"_"+width+"x"+height+".png" };
            report.frames.Add(frame);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                var host = new GameObject("Secondary Panels Capture Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                bool menu = state == "MainSettings" || state == "Achievements";
                CoreGameplayDemoController gameplay = null;
                if (menu) host.AddComponent<MainMenuController>();
                else
                {
                    gameplay = host.AddComponent<CoreGameplayDemoController>();
                    gameplay.ConfigureEditorPreview(state == "VoyageCargo" ? CoreGameplayPreviewScreen.Navigation : CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.GrayGull, 0);
                }
                host.SetActive(true);
                if (state == "MainSettings") OpenMenuSettings(host, frame);
                else if (state == "Achievements") OpenAchievements(host, frame);
                else if (state == "PortSettings") OpenPortSettings(host, frame);
                else OpenCargo(host, gameplay, frame);

                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == (menu ? MainMenuController.EditorPreviewCanvasName : CoreGameplayDemoController.EditorPreviewCanvasName));
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production scaler must remain enabled.");
                var cameraHost = new GameObject("Secondary Panels Capture Camera", typeof(Camera));
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
                target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0,0,1,1);
                camera.aspect = width / (float)height;
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
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=target });
                    else camera.Render();
                }
                frame.renderingDisplaySize = canvas.renderingDisplaySize;
                frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
                frame.canvasPixelRect = canvas.pixelRect;
                frame.cameraPixelRect = camera.pixelRect;
                frame.scaleFactor = canvas.scaleFactor;
                float wr=width/scaler.referenceResolution.x, hr=height/scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr,hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr,hr) : Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled && Mathf.Abs(frame.renderingDisplaySize.x-width)<.5f && Mathf.Abs(frame.renderingDisplaySize.y-height)<.5f &&
                    Mathf.Abs(frame.canvasPixelRect.width-width)<.5f && Mathf.Abs(frame.canvasPixelRect.height-height)<.5f &&
                    Mathf.Abs(frame.cameraPixelRect.width-width)<.5f && Mathf.Abs(frame.cameraPixelRect.height-height)<.5f &&
                    Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-width/frame.scaleFactor)<1 && Mathf.Abs(frame.logicalCanvasSize.y-height/frame.scaleFactor)<1;
                if (!frame.verifiedScaler) throw new InvalidOperationException("Render target did not drive the real production scaler.");
                string panelName = state == "MainSettings" ? "Settings Panel" : state == "Achievements" ? "Achievements Panel" : state == "PortSettings" ? "Port Settings Overlay" : "Voyage Cargo Overlay";
                Transform panel = host.GetComponentsInChildren<Transform>(false).SingleOrDefault(item => item.name == panelName);
                if (panel == null && state == "PortSettings") panel = Read<RectTransform>(gameplay,"portSettingsOverlay");
                if (panel == null) throw new InvalidOperationException("Expected panel is not visible: " + panelName);
                foreach (Selectable control in panel.GetComponentsInChildren<Selectable>(false))
                {
                    Rect bounds = Bounds(control.GetComponent<RectTransform>(),camera);
                    frame.controls.Add(new Control { name=control.name, label=control.GetComponentInChildren<Text>()?.text, bounds=bounds, interactable=control.IsInteractable() });
                    if (bounds.xMin < -1 || bounds.yMin < -1 || bounds.xMax > width+1 || bounds.yMax > height+1)
                        frame.errors.Add("Control outside target: " + control.name);
                }
                foreach (Text text in panel.GetComponentsInChildren<Text>(false))
                {
                    if (text.text.Contains("应用后保存在本机") || text.text.Contains("应用后生效；关闭放弃调整"))
                        frame.errors.Add("Removed explanatory text is still visible: " + text.text);
                }
                RenderTexture.active = target;
                pixels = new Texture2D(width,height,TextureFormat.RGB24,false,false);
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);
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

        private static void OpenMenuSettings(GameObject host, Frame frame)
        {
            Click(host,"Settings");
            Slider slider = Find<Slider>(host,"Master Volume");
            float original = slider.value;
            slider.value = .42f;
            if (!host.GetComponentsInChildren<Text>(false).Any(text => text.text == "42%")) throw new InvalidOperationException("Menu volume label did not update.");
            Toggle toggle = Find<Toggle>(host,"Full Screen");
            bool enabled = toggle.isOn;
            toggle.isOn = !enabled;
            Click(host,"Back From Settings");
            Click(host,"Settings");
            if (Mathf.Abs(Find<Slider>(host,"Master Volume").value-.42f)>.001f || Find<Toggle>(host,"Full Screen").isOn == enabled)
                throw new InvalidOperationException("Menu settings lost retained drafts.");
            slider.value = original;
            toggle.isOn = enabled;
            frame.interactions.Add("Menu open, live percentage, fullscreen draft, close/reopen retention.");
        }

        private static void OpenAchievements(GameObject host, Frame frame)
        {
            Click(host,"Achievements");
            string first = Find<Text>(host,"Achievements Page Indicator").text;
            Click(host,"Achievements Next Page");
            if (Find<Text>(host,"Achievements Page Indicator").text == first) throw new InvalidOperationException("Achievement paging did not advance.");
            Click(host,"Achievements Previous Page");
            if (Find<Text>(host,"Achievements Page Indicator").text != first) throw new InvalidOperationException("Achievement paging did not return.");
            frame.interactions.Add("Achievements open, next/previous page callbacks.");
        }

        private static void OpenPortSettings(GameObject host, Frame frame)
        {
            Click(host,"Port Hub Settings");
            var game = host.GetComponent<CoreGameplayDemoController>();
            Slider slider = Read<Slider>(game,"portSettingsMasterSlider");
            float original = slider.value;
            slider.value=.37f;
            if (!host.GetComponentsInChildren<Text>(false).Any(text => text.text == "37%")) throw new InvalidOperationException("Port volume label did not update.");
            Click(host,"Close Port Settings");
            Click(host,"Port Hub Settings");
            if (Mathf.Abs(Read<Slider>(game,"portSettingsMasterSlider").value-original)>.001f) throw new InvalidOperationException("Closing port settings should discard its draft.");
            frame.interactions.Add("Port settings open, live percentage, close discards draft, reopen.");
        }

        private static void OpenCargo(GameObject host, CoreGameplayDemoController game, Frame frame)
        {
            CoreVoyageSession session=game.Session;
            session.Cargo.Water=255; session.Cargo.Food=255; session.Cargo.Beer=4; session.Cargo.Medicine=2;
            session.Cargo.Wood=8; session.Cargo.Iron=0; session.Cargo.Cloth=0; session.Cargo.Powder=18; session.Cargo.Cannonballs=15;
            session.Cargo.Gem=1; session.Cargo.Gold=60;
            string before = CargoSnapshot(session);
            Click(host,"Voyage Cargo");
            Click(host,"Voyage Cargo Special Tab");
            Click(host,"Voyage Cargo Item gem");
            if (Find<Button>(host,"Confirm Voyage Cargo Discard").IsInteractable()) throw new InvalidOperationException("Editor preview must not enable discard.");
            Click(host,"Voyage Cargo Basic Tab");
            Click(host,"Voyage Cargo Item iron");
            if (Find<Button>(host,"Confirm Voyage Cargo Discard").IsInteractable()) throw new InvalidOperationException("Zero cargo must not enable discard.");
            Click(host,"Close Voyage Cargo");
            Click(host,"Voyage Cargo");
            if (CargoSnapshot(session) != before) throw new InvalidOperationException("Read-only cargo inspection changed inventory.");
            frame.interactions.Add("Cargo tabs, special item, zero-stock item, disabled discard, close/reopen; inventory preserved.");
        }

        private static string CargoSnapshot(CoreVoyageSession session) => string.Join(",", CoreWarehouseItemCatalog.All.Select(item => item.Key+"="+session.GetWarehouseCargoAmount(item.Key)))+";gold="+session.Cargo.Gold;
        private static T Read<T>(object instance,string field) => (T)instance.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);
        private static T Find<T>(GameObject host,string name) where T:Component => host.GetComponentsInChildren<T>(true).Single(item => item.name == name && item.gameObject.activeInHierarchy);
        private static void Click(GameObject host,string name) => Find<Button>(host,name).onClick.Invoke();
        private static Rect Bounds(RectTransform rect,Camera camera)
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue), max=new Vector2(float.MinValue,float.MinValue);
            foreach (Vector3 corner in corners) { Vector2 point=camera.WorldToScreenPoint(corner); min=Vector2.Min(min,point); max=Vector2.Max(max,point); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
