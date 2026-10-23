using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.EventConsistencyReview
{
    public static class Capture
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] sealed class Frame
        {
            public string file, kind, variant, encounterId, encounterName, expectedTexture, texture, error;
            public Rect visibleUv, canvasPixels;
            public bool verified;
        }
        [Serializable] sealed class Report { public Frame[] frames; }

        public static void Run()
        {
            if (Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A stopped Editor with graphics is required.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-eventReviewOutput");
            if (index < 0) throw new ArgumentException("Missing -eventReviewOutput.");
            string output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            var report = new Report { frames = new[] {
                new Frame { file = "core_weathered_event.png", kind = "core", variant = "weathered", expectedTexture = "core_weathered" },
                new Frame { file = "core_gilded_event.png", kind = "core", variant = "gilded", expectedTexture = "core_gilded" },
                new Frame { file = "core_moonlit_event.png", kind = "core", variant = "moonlit", expectedTexture = "core_moonlit" },
                new Frame { file = "tutorial_message_ship_event.png", kind = "random", variant = "ship", expectedTexture = "tutorial_message_ship" },
                new Frame { file = "tutorial_message_tube_event.png", kind = "random", variant = "tube", expectedTexture = "tutorial_message_tube" },
                new Frame { file = "core_weathered_map.png", kind = "mapCore", variant = "weathered", expectedTexture = "X marker (no card art by design)" },
                new Frame { file = "reef_broken_mast_map.png", kind = "mapEvent", expectedTexture = "reef_broken_mast" }
            } };
            foreach (Frame frame in report.frames)
            {
                try { Render(output, frame); }
                catch (Exception ex) { frame.error = ex.ToString(); }
            }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            if (report.frames.Any(frame => !frame.verified || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Capture failed; inspect report.json.");
            Debug.Log("EVENT_CONSISTENCY_PREVIEW_OK: five production event dialogs and two actual map presentations.");
        }

        static void Render(string output, Frame frame)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture rt = null;
            Texture2D pixels = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var host = new GameObject("Disposable Event Consistency Preview");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Encounter, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                Field(controller, "editorPreviewBuilding", true);
                bool map = frame.kind.StartsWith("map", StringComparison.Ordinal);
                bool treasure = frame.kind == "core" || frame.kind == "mapCore";
                CoreVoyageSession session = treasure ? TreasureSession(frame.variant) : controller.Session;
                Field(controller, "session", session);
                CoreMapNode node = null;
                CoreEncounter encounter;
                if (treasure)
                {
                    node = session.Map.Nodes.Single(n => n.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
                    encounter = node.Encounter;
                }
                else if (frame.kind == "random")
                {
                    encounter = (CoreEncounter)typeof(CoreGuidedTutorialContent).GetMethod("RandomEvent", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { frame.variant == "ship" });
                }
                else
                {
                    var pool = (System.Collections.Generic.IReadOnlyList<CoreEncounter>)typeof(CoreVoyageSession)
                        .GetMethod("CreateEncounterPool", Private).Invoke(session, new object[] { CoreVoyageSession.Routes[0] });
                    encounter = pool.Single(e => e.Id == "event_reef_broken_mast");
                    node = session.Map.Nodes.First(n => !n.IsBoss);
                    node.Encounter = encounter;
                }
                frame.encounterId = encounter.Id;
                frame.encounterName = encounter.Name;
                foreach (CoreMapNode other in session.Map.Nodes) other.Discovered = false;
                if (node != null)
                {
                    node.Discovered = true;
                    Property(session, "Position", map ? new VoyagePoint(Math.Max(2, node.Position.X - 8), node.Position.Y) : node.Position);
                }
                for (int i = 0; i < session.Map.ExploredCells.Length; i++) session.Map.ExploredCells[i] = true;
                session.Map.RestoreChartExploration(null);
                Property(session, "ActiveEncounter", map ? null : encounter);
                Property(session, "Phase", map ? CoreDemoPhase.Sailing : CoreDemoPhase.Encounter);
                Field(session, "activeNode", map ? null : node);
                Invoke(controller, "Render");
                Invoke(controller, "CloseTreasureStoryOverlay");
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
                string artName = map ? "Event Icon " + node.Id : "Demo Event Art";
                RawImage art = host.GetComponentsInChildren<RawImage>(false).SingleOrDefault(a => a.name == artName);
                if (frame.kind == "mapCore")
                {
                    Button marker = host.GetComponentsInChildren<Button>(false).Single(b => b.name == "Event Node " + node.Id);
                    frame.texture = marker.GetComponentInChildren<Text>().text;
                    frame.verified = frame.texture == "X" && art == null;
                }
                else
                {
                    frame.texture = art?.texture == null ? "MISSING" : art.texture.name;
                    if (art != null) frame.visibleUv = art.uvRect;
                    frame.verified = frame.texture == frame.expectedTexture;
                }
                frame.canvasPixels = canvas.pixelRect;
                if (!frame.verified || Mathf.Abs(canvas.pixelRect.width - 1920) > 1)
                    throw new InvalidOperationException("Production texture/marker/canvas verification failed.");
                RenderTexture.active = rt;
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            }
        }

        static CoreVoyageSession TreasureSession(string rank)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = rank,
                maps = new[] { new CoreTreasureMapSave { id = rank, seed = "ART-REVIEW-" + rank, rankId = rank, contentVersion = 1, mapGeometryVersion = 1 } } });
            session.Cargo.Water = session.Cargo.Food = 100;
            if (!session.TryStartTreasureVoyage(out string reason)) throw new InvalidOperationException(reason);
            return session;
        }
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static void Field(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    }
}
