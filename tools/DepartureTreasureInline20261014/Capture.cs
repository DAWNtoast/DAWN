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
    // Copied only into this task's independent project; transient preview scenes and memory-only inventory.
    public static class DepartureTreasureInlineCapture20261014
    {
        private const string Prefix = "Departure Dock Treasure ";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string scope = "Six 1920x1080 production dock states, real CanvasScaler, memory-only treasure inventory and transient preview scenes. No saved scene, player archive, image API or retired preview directory is used.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string state, file, selectedSeed, background;
            public int width = 1920, height = 1080, mapCount, dropdownCount;
            public bool passed, verifiedScaler;
            public float scaleFactor, expectedScaleFactor;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect, selectionBounds, previewBounds, dropdownBounds;
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
            if (!project.Equals("D:/Game/tmp/departure-treasure-inline-20261014", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This capture requires its task-owned isolated project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Stopped Editor with graphics is required.");
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-departureTreasureInlineOutput");
            if (argument < 0 || argument + 1 >= args.Length) throw new ArgumentException("Pass -departureTreasureInlineOutput NEW_ABSOLUTE_DIR.");
            string output = Path.GetFullPath(args[argument + 1]);
            if (!output.Replace('\\','/').StartsWith("D:/Game/Artifacts/DepartureTreasureInline20261014/", StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
                throw new InvalidOperationException("Use a fresh task artifact directory.");
            Directory.CreateDirectory(output);
            var report = new Report();
            try
            {
                foreach (string state in new[] { "ordinary", "treasure-unselected", "treasure-weathered", "treasure-gilded", "treasure-empty", "dropdown-expanded" })
                    Capture(report, output, state);
                report.passed = report.frames.Count == 6 && report.frames.All(frame => frame.passed);
            }
            finally { File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true)); }
            if (!report.passed) throw new InvalidOperationException("Departure treasure capture failed; inspect report.json.");
            Debug.Log("DEPARTURE_TREASURE_INLINE_CAPTURE_OK: six verified 1920x1080 frames at " + output);
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
                var host = new GameObject("Departure Treasure Inline Capture Host");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var gameplay = host.AddComponent<CoreGameplayDemoController>();
                gameplay.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                int count = state == "treasure-empty" ? 0 : 6;
                gameplay.Session.RestoreTreasureProgress(new CoreTreasureProgress
                {
                    pirateExperience = CoreVoyageSession.PirateExperienceForLevel(3),
                    maps = Enumerable.Range(0, count).Select(index => new CoreTreasureMapSave
                    {
                        id = "inline-capture-" + index,
                        rankId = index % 2 == 0 ? "weathered" : "gilded",
                        seed = "DOCK-TREASURE-" + (index + 1).ToString("00") + "-20261014",
                    }).ToArray(),
                });
                gameplay.Session.Cargo.Water = gameplay.Session.Cargo.Food = 80;
                Call(gameplay, "Render");
                Texture background = Find<RawImage>(host,"Voyage Background").texture;
                frame.background = background == null ? "missing" : background.name;
                RectTransform vessel = Find<RectTransform>(host,"Inspect Departure Vessel");
                Vector2 vesselSize = vessel.sizeDelta, vesselPosition = vessel.anchoredPosition;
                string vesselParent = vessel.parent.name;

                if (state != "ordinary")
                {
                    Find<Button>(host,"Departure Treasure Seed Picker").onClick.Invoke();
                    frame.interactions.Add("Clicked the production Treasure Charts tab; no seed equipped automatically.");
                    Assert(gameplay.Session.SelectedTreasureMap == null, frame, "Opening the tab equipped a map without selection.");
                    Assert(Find<Button>(host,"Start Voyage").IsInteractable() == false, frame, "Empty treasure selection allowed departure.");
                    Dropdown dropdown = Find<Dropdown>(host,Prefix+"Dropdown");
                    frame.dropdownCount = dropdown.options.Count;
                    Assert(frame.dropdownCount == count + 1, frame, "Dropdown must contain a placeholder followed by every held seed.");
                    if (state == "treasure-weathered" || state == "treasure-gilded")
                    {
                        int chosen = state == "treasure-weathered" ? 1 : 2;
                        dropdown.value = chosen;
                        Assert(gameplay.Session.SelectedTreasureMap?.Seed == "DOCK-TREASURE-" + chosen.ToString("00") + "-20261014", frame, "Dropdown selection did not equip the expected seed.");
                        Assert(gameplay.Session.TreasureMaps.Count == count, frame, "Selecting a seed consumed inventory.");
                        Assert(!gameplay.Session.IsChartSeedSelected, frame, "Treasure seed did not clear ordinary chart selection.");
                        Assert(Find<RawImage>(host,Prefix+"Preview").texture != null, frame, "Selected treasure preview has no texture.");
                        Assert(Find<Button>(host,"Start Voyage").IsInteractable(), frame, "Valid selected treasure disabled departure.");
                        frame.selectedSeed = gameplay.Session.SelectedTreasureMap?.Seed;
                        frame.interactions.Add("Selected dropdown option " + chosen + ", checked inventory and preview, and retained the dock scene.");
                    }
                    if (state == "treasure-empty")
                    {
                        Assert(!dropdown.IsInteractable(), frame, "Empty inventory dropdown is unexpectedly interactable.");
                        Assert(Find<Text>(host,Prefix+"Empty").text.Contains("藏宝"), frame, "Empty inventory guidance is missing.");
                    }
                }
                frame.mapCount = gameplay.Session.TreasureMaps.Count;
                Assert(!host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Departure Treasure Seed Selection" && item.gameObject.activeInHierarchy), frame, "The old treasure modal is still visible.");
                Assert(Find<RawImage>(host,"Voyage Background").texture == background, frame, "Treasure mode replaced the dock background.");
                foreach (string name in new[] { "Route Selection", "Voyage Setup" })
                {
                    RectTransform root = Find<RectTransform>(host,name);
                    Assert(root.sizeDelta == PhaseSurfaceLayout.ContentSize && root.anchoredPosition == Vector2.zero && root.GetComponent<Image>().color.a == 0f, frame, "Dock root lost transparent full-content layout: "+name);
                }
                vessel = Find<RectTransform>(host,"Inspect Departure Vessel");
                Assert(vessel.sizeDelta == vesselSize && vessel.anchoredPosition == vesselPosition && vessel.parent.name == vesselParent, frame, "Treasure mode moved or reparented the vessel.");

                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production scaler must remain enabled.");
                var cameraHost = new GameObject("Departure Treasure Capture Camera", typeof(Camera));
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
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                Canvas.ForceUpdateCanvases();
                handle.Invoke(scaler,null);
                Canvas.ForceUpdateCanvases();
                if (state == "dropdown-expanded")
                {
                    Dropdown dropdown = Find<Dropdown>(host,Prefix+"Dropdown");
                    // uGUI initializes its tween runner in Start, which stopped Editor previews do not dispatch.
                    typeof(Dropdown).GetMethod("Start", Private).Invoke(dropdown, null);
                    dropdown.alphaFadeSpeed = 0;
                    dropdown.Show();
                    Canvas.ForceUpdateCanvases();
                    Transform list = dropdown.transform.Find("Dropdown List");
                    if (list == null) throw new InvalidOperationException("Dropdown.Show did not create its production list.");
                    // Stopped Editor does not animate the uGUI fade; showing its generated list at full opacity is visual staging only.
                    CanvasGroup fade = list.GetComponent<CanvasGroup>();
                    if (fade != null) fade.alpha = 1;
                    ScrollRect scroll = list.GetComponentInChildren<ScrollRect>();
                    if (scroll == null) throw new InvalidOperationException("Treasure dropdown has no ScrollRect.");
                    Assert(scroll.content.rect.height > scroll.viewport.rect.height, frame, "Six seed choices cannot scroll within the dropdown viewport.");
                    scroll.verticalNormalizedPosition = 0f;
                    frame.interactions.Add("Opened the production dropdown and scrolled to its last seed; all six held seeds are available.");
                }
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
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
                float wr=frame.width/scaler.referenceResolution.x, hr=frame.height/scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr,hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr,hr) : Mathf.Pow(2,Mathf.Lerp(Mathf.Log(wr,2),Mathf.Log(hr,2),scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled && Mathf.Abs(frame.renderingDisplaySize.x-frame.width)<.5f && Mathf.Abs(frame.renderingDisplaySize.y-frame.height)<.5f &&
                    Mathf.Abs(frame.canvasPixelRect.width-frame.width)<.5f && Mathf.Abs(frame.canvasPixelRect.height-frame.height)<.5f &&
                    Mathf.Abs(frame.cameraPixelRect.width-frame.width)<.5f && Mathf.Abs(frame.cameraPixelRect.height-frame.height)<.5f &&
                    Mathf.Abs(frame.scaleFactor-frame.expectedScaleFactor)<.001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x-frame.width/frame.scaleFactor)<1 && Mathf.Abs(frame.logicalCanvasSize.y-frame.height/frame.scaleFactor)<1;
                Assert(frame.verifiedScaler, frame, "Render target did not drive the production scaler.");
                if (state != "ordinary")
                {
                    frame.selectionBounds=Bounds(Find<RectTransform>(host,Prefix+"Selection"),camera);
                    frame.dropdownBounds=Bounds(Find<RectTransform>(host,Prefix+"Dropdown"),camera);
                    RawImage preview = Optional<RawImage>(host,Prefix+"Preview");
                    if (preview != null) frame.previewBounds=Bounds(preview.rectTransform,camera);
                }
                foreach (Selectable control in canvas.GetComponentsInChildren<Selectable>(false))
                    frame.controls.Add(new Control { name=control.name, label=control.GetComponentInChildren<Text>()?.text, bounds=Bounds(control.GetComponent<RectTransform>(),camera), interactable=control.IsInteractable() });
                RenderTexture.active = target;
                pixels = new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false,false);
                pixels.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);
                pixels.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());

                if (state == "dropdown-expanded")
                {
                    // Dropdown's normal OnDisable uses delayed Destroy, unsuitable for a stopped preview scene.
                    Dropdown dropdown = Find<Dropdown>(host,Prefix+"Dropdown");
                    foreach (string fieldName in new[] { "m_Dropdown", "m_Blocker" })
                    {
                        FieldInfo field = typeof(Dropdown).GetField(fieldName, Private);
                        GameObject transient = field.GetValue(dropdown) as GameObject;
                        field.SetValue(dropdown, null);
                        if (transient != null) Object.DestroyImmediate(transient);
                    }
                }

                if (state == "treasure-gilded")
                {
                    Find<Dropdown>(host,Prefix+"Dropdown").value = 1;
                    Assert(gameplay.Session.SelectedTreasureMap?.RankId == "weathered", frame, "Changing dropdown did not switch the selected treasure map.");
                    Find<Button>(host,"Departure Dock Ordinary Voyage").onClick.Invoke();
                    Assert(gameplay.Session.SelectedTreasureMap == null, frame, "Ordinary voyage tab did not clear treasure selection.");
                    Assert(Optional<RectTransform>(host,Prefix+"Selection") == null, frame, "Ordinary tab left the treasure selection visible.");
                    Assert(Find<Button>(host,"Start Voyage").IsInteractable(), frame, "Ordinary tab did not restore departure.");
                    frame.interactions.Add("After capture, switched the seed, then selected Ordinary Voyage and verified the treasure choice and panel cleared.");
                }
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
        private static void Assert(bool condition, Frame frame, string error) { if (!condition) frame.errors.Add(error); }
        private static void Call(object instance,string method) => instance.GetType().GetMethod(method,Private).Invoke(instance,null);
        private static T Find<T>(GameObject host,string name) where T:Component => host.GetComponentsInChildren<T>(true).Single(item => item.name == name && item.gameObject.activeInHierarchy);
        private static T Optional<T>(GameObject host,string name) where T:Component => host.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name && item.gameObject.activeInHierarchy);
        private static Rect Bounds(RectTransform rect,Camera camera)
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue), max=new Vector2(float.MinValue,float.MinValue);
            foreach (Vector3 corner in corners) { Vector2 point=camera.WorldToScreenPoint(corner); min=Vector2.Min(min,point); max=Vector2.Max(max,point); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
