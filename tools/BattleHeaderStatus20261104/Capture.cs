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
    public static class BattleHeaderStatusCapture20261104
    {
        private const int Width = 1920, Height = 1080, Layer = 31;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [Serializable] public sealed class Report
        {
            public string capturedAtUtc = DateTime.UtcNow.ToString("O");
            public string scope = "Current production Unity UI, rendered with the production CanvasScaler at 1920x1080 in disposable PreviewScenes. Long values are isolated in-memory layout samples, not a player save. No authored scenes or main-project files changed.";
            public List<Frame> frames = new List<Frame>();
        }

        [Serializable] public sealed class Frame
        {
            public string file, variant, text, error;
            public bool boarding, longValues, verifiedScaler, bestFit, statusDeckOverlap, statusRangeOverlap, statusTitleOverlap;
            public int fontSize, lineCount;
            public float preferredWidth, preferredHeight, deckGap, rangeGap;
            public Rect statusBounds, deckBounds, rangeBounds, titleBounds;
        }

        public static void Run()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Stopped graphics-enabled editor required.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Isolated project required.");
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-battleHeaderStatusOutput");
            if (option < 0 || option + 1 >= args.Length) throw new ArgumentException("Missing capture output.");
            string output = Path.GetFullPath(args[option + 1]);
            if (!output.Replace('\\', '/').StartsWith("D:/Game/outputs/battle-header-status-20261104/", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Unexpected output.");
            Directory.CreateDirectory(output);
            if (Directory.EnumerateFiles(output, "*.png").Any()) throw new IOException("Preserve prior screenshots.");
            var report = new Report();
            report.frames.Add(new Frame { file = "01_Naval.png", variant = "Naval" });
            report.frames.Add(new Frame { file = "02_BoardingLongValues.png", variant = "BoardingLongValues", boarding = true, longValues = true });
            foreach (Frame frame in report.frames)
            {
                try { Capture(output, frame); Debug.Log("BATTLE_HEADER_OK: " + frame.file); }
                catch (Exception exception)
                {
                    while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
                    frame.error = exception.ToString();
                    Debug.LogWarning("BATTLE_HEADER_FAILED: " + frame.variant + " " + exception);
                }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
            if (report.frames.Any(frame => !string.IsNullOrEmpty(frame.error))) throw new InvalidOperationException("Capture verification failed; inspect report.json.");
        }

        private static void Capture(string output, Frame frame)
        {
            Scene scene = default;
            RenderTexture target = null, previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Battle Header Screenshot");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                if (frame.boarding || frame.longValues)
                {
                    CoreVoyageSession session = controller.Session;
                    if (frame.boarding) session.Battle.Stage = NavalBattleStage.Boarding;
                    if (frame.longValues)
                    {
                        // Refit APIs intentionally reject battle-time changes; arrange only
                        // this disposable sample's backing data before production rendering.
                        Set(session, "componentHullBonus", 9999 - session.SelectedShip.MaxHull);
                        Set(session, "componentContextHullBonus", 0);
                        Set(session, "Hull", 9999);
                        Set(session, "Morale", 20);
                        Set(session, "TotalDistanceNm", 999999f);
                    }
                    Set(controller, "editorPreviewBuilding", true);
                    try
                    {
                        Call(controller, "ClearPhaseContentForRender");
                        Call(controller, "BuildBattleOverlay");
                        Call(controller, "RemoveLegacyBattleScaffolding");
                    }
                    finally { Set(controller, "editorPreviewBuilding", false); }
                }

                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                var cameraHost = new GameObject("Disposable Header Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraHost, scene);
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false; camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.orthographic = true; camera.orthographicSize = 5;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000; camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1); camera.aspect = Width / (float)Height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases(); handle.Invoke(scaler, null);
                    foreach (Text text in canvas.GetComponentsInChildren<Text>(true)) text.SetAllDirty();
                    foreach (Transform node in canvas.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                float wr = Width / scaler.referenceResolution.x, hr = Height / scaler.referenceResolution.y;
                float expected = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr)
                    : scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr)
                    : Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled && Mathf.Abs(canvas.renderingDisplaySize.x - Width) < .5f
                    && Mathf.Abs(canvas.renderingDisplaySize.y - Height) < .5f && Mathf.Abs(canvas.scaleFactor - expected) < .001f;
                if (!frame.verifiedScaler) throw new InvalidOperationException("Production CanvasScaler mismatch.");
                Verify(host, frame);
                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void Verify(GameObject host, Frame frame)
        {
            RectTransform header = host.GetComponentsInChildren<RectTransform>().Single(item => item.name == "Voyage Header");
            Text status = header.Find("Voyage Header Status").GetComponent<Text>();
            RectTransform deck = (RectTransform)header.Find("Battle Deck");
            Text range = host.GetComponentsInChildren<Text>().SingleOrDefault(item => item.name == "Battle Range");
            frame.text = status.text; frame.fontSize = status.fontSize; frame.bestFit = status.resizeTextForBestFit;
            frame.preferredWidth = status.preferredWidth; frame.preferredHeight = status.preferredHeight;
            status.cachedTextGenerator.Populate(status.text, status.GetGenerationSettings(status.rectTransform.rect.size));
            frame.lineCount = status.cachedTextGenerator.lineCount;
            frame.statusBounds = Bounds(header, status.rectTransform); frame.deckBounds = Bounds(header, deck);
            frame.titleBounds = Bounds(header, (RectTransform)header.Find("Voyage Header Title"));
            frame.statusDeckOverlap = frame.statusBounds.Overlaps(frame.deckBounds);
            frame.statusTitleOverlap = frame.statusBounds.Overlaps(frame.titleBounds);
            frame.deckGap = frame.deckBounds.xMin - frame.statusBounds.xMax;
            if (range != null)
            {
                frame.rangeBounds = Bounds(header, range.rectTransform);
                frame.rangeGap = frame.statusBounds.xMin - frame.rangeBounds.xMax;
                frame.statusRangeOverlap = frame.statusBounds.Overlaps(frame.rangeBounds);
            }
            if (frame.fontSize != 28 || frame.bestFit || frame.lineCount != 1 || frame.preferredWidth > status.rectTransform.rect.width + .1f)
                throw new InvalidOperationException("Header text must fit on one full-size 28-point line.");
            if (frame.statusDeckOverlap || frame.statusTitleOverlap || frame.statusRangeOverlap || frame.deckGap < 15.99f)
                throw new InvalidOperationException("Header status overlaps a visible control/readout or loses its icon gap.");
            if (frame.boarding && (range == null || !status.text.Contains("接舷暴击"))) throw new InvalidOperationException("Boarding sample missing its actual readout.");
        }

        private static Rect Bounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector3[] points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }
        private static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Private);
            if (field != null) { field.SetValue(target, value); return; }
            PropertyInfo property = target.GetType().GetProperty(name, Private);
            if (property == null) throw new MissingMemberException(target.GetType().Name, name);
            property.SetValue(target, value);
        }
        private static object Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    }
}
