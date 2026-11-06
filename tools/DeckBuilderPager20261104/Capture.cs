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
    // Temporary validation helper. Never installed in the main project or used to save scenes.
    public static class DeckBuilderPagerCapture20261104
    {
        private const int Layer = 31;
        [Serializable] private sealed class Frame
        {
            public string page, file;
            public int width, height;
            public float scale;
            public List<Rect> buttons = new List<Rect>();
        }
        [Serializable] private sealed class Report { public List<Frame> frames = new List<Frame>(); }

        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase) || Application.isPlaying)
                throw new InvalidOperationException("An isolated stopped editor project is required.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-deckPagerOutput");
            string output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : "";
            if (!output.Replace('\\', '/').StartsWith("D:/Game/outputs/deck-builder-pager-20261104/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Unexpected capture destination.");
            if (Directory.EnumerateFiles(output, "*.png").Any()) throw new IOException("Preserve previous captures.");
            var report = new Report();
            foreach (string page in new[] { "DeckBuilder", "DeckBuilderPage2", "Special", "Collection" })
                report.frames.Add(Capture(output, page, 1920, 1080));
            report.frames.Add(Capture(output, "DeckBuilder", 1280, 720));
            File.WriteAllText(Path.Combine(output, "capture.json"), JsonUtility.ToJson(report, true));
            Debug.Log("DECK_PAGER_CAPTURE_OK: " + report.frames.Count);
        }

        private static Frame Capture(string output, string page, int width, int height)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            var frame = new Frame { page = page, width = width, height = height, file = page + "_" + width + "x" + height + ".png" };
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Workshop Navigation Capture");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(page == "Collection" ? CoreGameplayPreviewScreen.CardWorkshop : CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.BrokenAnchor, 0);
                host.SetActive(true);
                                if (page == "Collection") host.GetComponentsInChildren<Button>().Single(b => b.name == "Workshop Entrance Collection").onClick.Invoke();
                if (page == "DeckBuilderPage2") host.GetComponentsInChildren<Button>().Single(b => b.name == "Next Battle Card Collection Page").onClick.Invoke();
                if (page == "Special") host.GetComponentsInChildren<Button>().Single(b => b.name == "Open Special Card Collection").onClick.Invoke();
                if (host.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Deck Builder Interaction Hint" || t.name == "Workshop Collection Page Count"))
                    throw new InvalidOperationException("Removed helper text reappeared.");
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(c => c.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                var cameraHost = new GameObject("Disposable Capture Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000); camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = width / (float)height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                if (!scaler.enabled || Mathf.Abs(canvas.renderingDisplaySize.x - width) > .5f || Mathf.Abs(canvas.renderingDisplaySize.y - height) > .5f)
                    throw new InvalidOperationException("Capture must use the production scaler.");
                frame.scale = canvas.scaleFactor;
                Transform nav = host.GetComponentsInChildren<Transform>().Single(t => t.name == "Workshop Page Navigation");
                if (nav.GetComponentsInChildren<Button>().Length != 4 || nav.GetComponentsInChildren<Transform>().Any(t => t.name == "Workshop Return Home" || t.name == "Workshop Current Page Title"))
                    throw new InvalidOperationException("Expected exactly four navigation buttons without the removed controls.");
                foreach (Button button in host.GetComponentsInChildren<Button>().Where(b => b.name == "Previous Battle Card Collection Page" || b.name == "Next Battle Card Collection Page" || b.name == "Previous Upgrade Cards" || b.name == "Next Upgrade Cards"))
                {
                    var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                    Vector2[] p = corners.Select(v => (Vector2)camera.WorldToScreenPoint(v)).ToArray();
                    frame.buttons.Add(Rect.MinMaxRect(p.Min(v => v.x), height - p.Max(v => v.y), p.Max(v => v.x), height - p.Min(v => v.y)));
                }
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
                Debug.Log("DECK_PAGER_CAPTURE: " + frame.file);
                return frame;
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }
    }
}
