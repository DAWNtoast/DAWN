using System;
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

namespace ShatteredSail.BossSceneReview
{
    public static class Capture
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] sealed class Frame
        {
            public string file, encounterId, texture, error;
            public Rect visibleUv, canvasPixels;
            public bool sourceIsScene, noSeparateShipLayer;
        }
        [Serializable] sealed class Report { public Frame[] frames; }

        public static void Run()
        {
            if (Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A stopped Editor with graphics is required.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-bossSceneOutput");
            if (index < 0) throw new ArgumentException("Missing -bossSceneOutput.");
            string output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            var report = new Report { frames = new[] {
                new Frame { file = "boss_arcane_event_1920x1080.png", encounterId = "boss_route_moon_tide_ruins_arcane" },
                new Frame { file = "legendary_curse_prelude_1920x1080.png", encounterId = "legendary_pirate_iron_oath" }
            } };
            for (int i = 0; i < report.frames.Length; i++)
            {
                try { Render(output, report.frames[i], i == 1); }
                catch (Exception ex) { report.frames[i].error = ex.ToString(); }
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            if (report.frames.Any(f => !string.IsNullOrEmpty(f.error))) throw new InvalidOperationException("Capture failed; inspect report.json.");
            Debug.Log("BOSS_SCENE_PREVIEW_OK: two disposable production UI captures.");
        }

        static void Render(string output, Frame frame, bool prelude)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            Texture2D pixels = null;
            RenderTexture old = RenderTexture.active;
            try
            {
                var host = new GameObject("Disposable Boss Scene Preview");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Encounter, CoreGameplayPreviewPort.GrayGull, 2);
                host.SetActive(true);
                Field(controller, "editorPreviewBuilding", true);
                if (!prelude)
                {
                    CoreVoyageSession session = controller.Session;
                    CoreEncounter encounter = CoreRouteBossCatalog.Find(frame.encounterId).CreateEncounter();
                    var node = new CoreMapNode { Id = 991, Position = session.Position,
                        Encounter = encounter, IsBoss = true, Discovered = true, TerrainConstraint = EventTerrainConstraint.Any };
                    session.Map.Nodes.Add(node);
                    Property(session, "ActiveEncounter", encounter);
                    Property(session, "Phase", CoreDemoPhase.Encounter);
                    Field(session, "activeNode", node);
                    Invoke(controller, "Render");
                    Invoke(controller, "CloseTreasureStoryOverlay");
                }
                else
                {
                    TreasureBattleStory story = CoreTreasureStoryCatalog.FindBattle(frame.encounterId);
                    Invoke(controller, "BuildTreasureStoryOverlay", CoreTreasureStoryCatalog.Chapters[0], story, "boss-scene-review");
                }
                Field(controller, "editorPreviewBuilding", false);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                var cameraHost = new GameObject("Disposable Preview Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000; camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << 30; camera.allowHDR = false; camera.allowMSAA = false;
                rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create(); camera.targetTexture = rt; camera.aspect = 1920f / 1080;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", Private);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (NavigationDashboardLayout layout in host.GetComponentsInChildren<NavigationDashboardLayout>(false))
                        layout.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    Canvas.ForceUpdateCanvases(); camera.Render();
                }
                RawImage art = host.GetComponentsInChildren<RawImage>(false).Single(a => a.name == (prelude ? "Treasure Story Comic Page" : "Demo Event Art"));
                frame.texture = art.texture == null ? "MISSING" : art.texture.name;
                frame.sourceIsScene = frame.texture == (prelude ? "scene_legendary_curse" : "scene_boss_arcane");
                frame.visibleUv = art.uvRect; frame.canvasPixels = canvas.pixelRect;
                frame.noSeparateShipLayer = !host.GetComponentsInChildren<Transform>(false).Any(t => t.name == "Treasure Legendary Ship");
                if (!frame.sourceIsScene || !frame.noSeparateShipLayer || Mathf.Abs(canvas.pixelRect.width - 1920) > 1)
                    throw new InvalidOperationException("Production texture/layer/canvas verification failed.");
                RenderTexture.active = rt;
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old;
                if (pixels != null) Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Field(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    }
}
