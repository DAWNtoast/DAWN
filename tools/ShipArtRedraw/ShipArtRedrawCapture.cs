// Temporary read-only audit. Install only in a disposable project's Editor folder.
// Uses actual production builders in fresh Editor PreviewScenes; never saves assets/scenes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class ShipArtRedrawCapture
    {
        private const int Layer = 31, Width = 1920, Height = 1080;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Report
        {
            public string capturedAtUtc;
            public string shipShaderSha256;
            public string scope = "All 12 player shipyard galleries and performance dossiers, three actual route boss naval battles, and retained legacy tutorial route/battle builders. Fresh in-memory editor sessions; actual enabled CanvasScaler, ScreenSpaceCamera and rendered UI; no PlayMode, profile save, scene save or asset writes.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] public sealed class Frame
        {
            public string state, name, file, error, scenePath, screenMatchMode;
            public int index;
            public bool scalerVerified;
            public float scaleFactor;
            public Vector2 displaySize, canvasSize;
            public List<Art> shipArt = new List<Art>();
        }
        [Serializable] public sealed class Art
        {
            public string name, texture, assetPath, sourceSha256, filter, shader, graphicsFormat;
            public int width, height;
            public bool formatHasAlpha;
            public Rect uv, screenRect;
            public float waterContactY, useColorKey, smoothCutout;
            public Vector4 atlasGrid;
        }
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string[] args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "-shipArtCapturePath");
            if (key < 0 || key + 1 >= args.Length || !Path.IsPathRooted(args[key+1]))
                throw new ArgumentException("Pass -shipArtCapturePath NEW_ABSOLUTE_DIRECTORY");
            string output = Path.GetFullPath(args[key+1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Capture directory must be empty.");
            Directory.CreateDirectory(output);
            Report report = NewReport();
            for (int i = 0; i < CoreVoyageSession.Ships.Count; i++)
                foreach (string state in new[] { "Shipyard", "ShipyardDetail" })
                    report.frames.Add(new Frame { state = state, index = i, name = CoreVoyageSession.Ships[i].Name,
                        file = state + "_Ship" + i.ToString("D2") + ".png" });
            for (int i = 0; i < CoreVoyageSession.Routes.Count; i++)
                report.frames.Add(new Frame { state = "RouteBoss", index = i, file = "RouteBoss_" + i + ".png" });
            foreach (string state in new[] { "LegacyTutorialRoute", "LegacyTutorialBattle" })
                report.frames.Add(new Frame { state = state, file = state + ".png" });
            foreach (Frame frame in report.frames)
            {
                try { Capture(output, frame); }
                catch (Exception e) { frame.error = e.ToString(); Debug.LogWarning(frame.file + ": " + e.Message); }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            }
            WriteGallery(output, report);
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error) || !f.scalerVerified))
                throw new InvalidOperationException("Ship art capture incomplete; inspect report.");
            Debug.Log("SHIP_ART_REDRAW_CAPTURE_OK: " + report.frames.Count + " frames in " + output);
        }
        public static void CaptureSavedPreviewsFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            string[] args = Environment.GetCommandLineArgs();
            int key=Array.IndexOf(args,"-shipArtCapturePath"), manifest=Array.IndexOf(args,"-shipArtSavedManifest");
            if(key<0 || manifest<0) throw new ArgumentException("Pass capture directory and synced-scene manifest.");
            string output=Path.GetFullPath(args[key+1]);
            if(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use an empty capture directory.");
            Directory.CreateDirectory(output);
            Report report=NewReport();
            report.scope="Reopened saved editable previews rendered without rebuilding their UI or saving scenes; persisted atlas UV/aspect/material/waterline evidence.";
            foreach(string path in File.ReadAllLines(args[manifest+1]))
            {
                var frame=new Frame {state="SavedPreview",name=Path.GetFileNameWithoutExtension(path),scenePath=path,
                    file=path.Substring("Assets/_Project/Scenes/UIPreviewsForMe/".Length).Replace('/','_').Replace(".unity",".png")};
                report.frames.Add(frame);
                try { Capture(output,frame); } catch(Exception e) { frame.error=e.ToString(); Debug.LogWarning(frame.file+": "+e.Message); }
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            }
            WriteGallery(output,report);
            if(report.frames.Any(f=>!string.IsNullOrEmpty(f.error) || !f.scalerVerified)) throw new InvalidOperationException("Saved preview capture incomplete.");
            Debug.Log("SHIP_ART_SAVED_CAPTURE_OK: "+report.frames.Count+" frames in "+output);

#endif
        }
        public static void CompleteBossCapturesFromCommandLine()
        {
            string[] args=Environment.GetCommandLineArgs();
            int key=Array.IndexOf(args,"-shipArtCapturePath");
            if(key<0) throw new ArgumentException("Pass existing incomplete final capture directory.");
            string output=Path.GetFullPath(args[key+1]);
            Report report=JsonUtility.FromJson<Report>(File.ReadAllText(Path.Combine(output,"report.json")));
            if(report.shipShaderSha256!=NewReport().shipShaderSha256) throw new InvalidOperationException("Capture shader changed.");
            foreach(Frame frame in report.frames.Where(f=>f.state=="RouteBoss"))
            {
                if(File.Exists(Path.Combine(output,frame.file))) throw new IOException("Boss supplement must fill missing images only.");
                frame.error=null; frame.shipArt.Clear(); Capture(output,frame);
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true),new UTF8Encoding(false));
            }
            WriteGallery(output,report);
            if(report.frames.Count!=29 || report.frames.Any(f=>!string.IsNullOrEmpty(f.error)||!f.scalerVerified||!File.Exists(Path.Combine(output,f.file))))
                throw new InvalidOperationException("Final capture remains incomplete.");
            if(report.frames.Where(f=>f.state=="RouteBoss").Select(f=>f.shipArt.Single(a=>a.name=="Enemy Ship Art").uv).Distinct().Count()!=3)
                throw new InvalidOperationException("Boss captures must show three distinct selected route atlas cells.");
            Debug.Log("SHIP_ART_REDRAW_CAPTURE_OK: 29 final frames with three distinct actual boss UVs in "+output);
        }
        private static Report NewReport() => new Report {capturedAtUtc=DateTime.UtcNow.ToString("O"),
            shipShaderSha256=SourceHash("Assets/_Project/Resources/Shaders/BattleShipCutout.shader")};
        private static string SourceHash(string path)
        {
            if(string.IsNullOrEmpty(path)) return null;
            using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"..",path))))).Replace("-","");
        }
        private static void Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                bool saved=!string.IsNullOrEmpty(frame.scenePath);
                GameObject host;
                CoreGameplayDemoController controller;
                bool shipyard = frame.state.StartsWith("Shipyard", StringComparison.Ordinal);
                if(saved)
                {
                    scene=EditorSceneManager.OpenScene(frame.scenePath,OpenSceneMode.Additive);
                    controller=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                    host=controller.gameObject;
                }
                else
                {
                    scene = EditorSceneManager.NewPreviewScene();
                    host = new GameObject("Disposable Ship Art Review");
                    host.SetActive(false);
                    SceneManager.MoveGameObjectToScene(host, scene);
                    controller = host.AddComponent<CoreGameplayDemoController>();
                    CoreGameplayPreviewPort port = shipyard ? (CoreGameplayPreviewPort)CoreShipMarketCatalog.HomePortIndex(frame.index) : CoreGameplayPreviewPort.GrayGull;
                    controller.ConfigureEditorPreview(shipyard ? CoreGameplayPreviewScreen.Shipyard : CoreGameplayPreviewScreen.Battle,
                        port, frame.state == "RouteBoss" ? frame.index : 0);
                    host.SetActive(true);
                    if (shipyard) controller.ConfigureEditorShipyard(frame.index, frame.state == "ShipyardDetail");
                    else if (frame.state == "RouteBoss") ConfigureBoss(controller, frame);
                    else ConfigureLegacyTutorial(controller, frame.state == "LegacyTutorialBattle");
                }
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (!scaler.enabled) throw new InvalidOperationException("Production CanvasScaler disabled.");
                foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                GameObject cameraHost = new GameObject("Disposable Ship Art Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.aspect = Width / (float)Height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                frame.displaySize = canvas.renderingDisplaySize;
                frame.canvasSize = canvas.GetComponent<RectTransform>().rect.size;
                frame.scaleFactor = canvas.scaleFactor;
                float expected = Mathf.Min(Width / scaler.referenceResolution.x, Height / scaler.referenceResolution.y);
                frame.screenMatchMode=scaler.screenMatchMode.ToString();
                if(saved && scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Shrink)
                    expected=Mathf.Max(Width/scaler.referenceResolution.x,Height/scaler.referenceResolution.y);
                if(saved && scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
                    expected=Mathf.Pow(2f,Mathf.Lerp(Mathf.Log(Width/scaler.referenceResolution.x,2f),Mathf.Log(Height/scaler.referenceResolution.y,2f),scaler.matchWidthOrHeight));
                frame.scalerVerified = scaler.enabled && (saved || scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand) &&
                    Mathf.Abs(frame.displaySize.x-Width) < .5f && Mathf.Abs(frame.displaySize.y-Height) < .5f &&
                    Mathf.Abs(frame.scaleFactor-expected) < .001f && Mathf.Abs(frame.canvasSize.x-Width/expected) < 1f &&
                    Mathf.Abs(frame.canvasSize.y-Height/expected) < 1f;
                if (!frame.scalerVerified) throw new InvalidOperationException("Actual render target/scaler geometry mismatch.");
                foreach (RawImage image in canvas.GetComponentsInChildren<RawImage>(false))
                {
                    if (image.texture == null || !(image.name.Contains("Ship") &&
                        (image.texture.name.Contains("ship") || image.name.Contains("Base")))) continue;
                    Vector3[] corners = new Vector3[4];
                    image.rectTransform.GetWorldCorners(corners);
                    Vector2[] screen = corners.Select(v => (Vector2)camera.WorldToScreenPoint(v)).ToArray();
                    frame.shipArt.Add(new Art { name = image.name, texture = image.texture.name,
                        assetPath = AssetDatabase.GetAssetPath(image.texture), width = image.texture.width, height = image.texture.height,
                        sourceSha256=SourceHash(AssetDatabase.GetAssetPath(image.texture)),
                        filter = image.texture.filterMode.ToString(), shader = image.material.shader.name, uv = image.uvRect,
                        graphicsFormat = image.texture.graphicsFormat.ToString(), formatHasAlpha = GraphicsFormatUtility.HasAlphaChannel(image.texture.graphicsFormat),
                        useColorKey = image.material.HasProperty("_UseColorKey") ? image.material.GetFloat("_UseColorKey") : -1f,
                        smoothCutout = image.material.HasProperty("_SmoothCutout") ? image.material.GetFloat("_SmoothCutout") : -1f,
                        atlasGrid = image.material.HasProperty("_AtlasGrid") ? image.material.GetVector("_AtlasGrid") : Vector4.zero,
                        screenRect = Rect.MinMaxRect(screen.Min(v=>v.x),screen.Min(v=>v.y),screen.Max(v=>v.x),screen.Max(v=>v.y)),
                        waterContactY = camera.WorldToScreenPoint(image.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(image))).y });
                }
                if (frame.shipArt.Count == 0) throw new InvalidOperationException("No actual visible ship artwork.");
                if (shipyard && frame.index > 0 && !frame.shipArt.Any(a => a.texture == "ship_roster_sheet" || a.texture == "ship_sprite_sheet"))
                    throw new InvalidOperationException("Selected vessel did not use its ship atlas.");
                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);
                pixels.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,frame.file),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid())
                {
                    if(string.IsNullOrEmpty(frame.scenePath)) EditorSceneManager.ClosePreviewScene(scene);
                    else EditorSceneManager.CloseScene(scene,true);
                }
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }
        private static void ConfigureBoss(CoreGameplayDemoController controller, Frame frame)
        {
            // The initial editor Battle fixture is already at sea. Match the
            // passing production boss test with fresh, unlocked port state.
            CoreVoyageSession session = new CoreVoyageSession();
            typeof(CoreGameplayDemoController).GetField("session",Private).SetValue(controller,session);
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.SelectRoute(frame.index);
            if (session.SelectedRouteIndex != frame.index)
                throw new InvalidOperationException("Boss capture route was not selected.");
            session.StartVoyage();
            CoreEncounter encounter = (CoreEncounter)Invoke(session, "CreateBossEncounter", CoreVoyageSession.Routes[frame.index]);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
            Invoke(session, "StartBattle", encounter, true);
            Invoke(session, "EnterBattleStage", NavalBattleStage.Naval);
            session.Battle.DistanceMeters = 120f;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            frame.name = encounter.Name;
            Invoke(controller, "Render");
        }
        private static void ConfigureLegacyTutorial(CoreGameplayDemoController controller, bool battle)
        {
            var tutorial = new TutorialVoyageSession();
            if (battle)
            {
                tutorial.BeginFirstLeg();
                while (tutorial.Phase != TutorialVoyagePhase.FirstLegComplete)
                {
                    tutorial.SailToNext();
                    int choice = Enumerable.Range(0,tutorial.CurrentEncounter.Choices.Count).First(tutorial.IsChoiceAvailable);
                    if (!tutorial.ResolveChoice(choice)) throw new InvalidOperationException("Tutorial first leg fixture rejected.");
                }
                tutorial.BeginAdvancedLeg();
                while (tutorial.Phase != TutorialVoyagePhase.Battle)
                {
                    tutorial.SailToNext();
                    int choice = Enumerable.Range(0,tutorial.CurrentEncounter.Choices.Count).First(tutorial.IsChoiceAvailable);
                    if (!tutorial.ResolveChoice(choice)) throw new InvalidOperationException("Tutorial battle fixture rejected.");
                }
            }
            typeof(CoreGameplayDemoController).GetField("tutorial",Private).SetValue(controller,tutorial);
            // The retained legacy builder owns an empty production canvas; do
            // not leave a newer battle/regional header outside its content root.
            Invoke(controller,"RemoveEditorPreview");
            Invoke(controller,"BuildCanvas",CoreGameplayDemoController.EditorPreviewCanvasName);
            Invoke(controller,"BuildTutorial");
        }
        private static object Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, Private);
            if (info == null) throw new MissingMethodException(target.GetType().FullName,method);
            return info.Invoke(target,args);
        }
        private static void WriteGallery(string output, Report report)
        {
            var html = new StringBuilder("<!doctype html><html lang='zh-CN'><meta charset='utf-8'><title>Ship Art Redraw UI Review</title><style>body{background:#10232c;color:#f1dec1;font:18px sans-serif;margin:28px}section{margin-bottom:32px}img{width:100%;max-width:1440px;height:auto;border:1px solid #776646}p{color:#c1c7c9}</style><h1>船只重画 · 实际 UI 截图</h1><p>12 艘船的船坞和档案、3 艘区域首领敌舰、旧教学兼容画面。1920 × 1080，临时 Editor PreviewScene，未出包。</p>");
            foreach (Frame frame in report.frames)
                html.Append("<section><h2>").Append(frame.state).Append(" · ").Append(frame.index).Append(" · ").Append(System.Net.WebUtility.HtmlEncode(frame.name)).Append("</h2><img loading='lazy' src='").Append(frame.file).Append("'><p>").Append(System.Net.WebUtility.HtmlEncode(frame.error)).Append("</p></section>");
            File.WriteAllText(Path.Combine(output,"index.html"),html.Append("</html>").ToString(),new UTF8Encoding(false));
        }
    }
}
