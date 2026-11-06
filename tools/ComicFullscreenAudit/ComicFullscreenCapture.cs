// Temporary Editor audit only. Copy into an isolated project's Editor folder.
// Uses the real builders and enabled CanvasScaler; no saved scene changes or gameplay callbacks.
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
    public static class ComicFullscreenCapture
    {
        private const int Layer = 31;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Serializable] public sealed class CaptureReport
        {
            public string scope = "Real ram and opening comic builders in disposable Editor PreviewScenes. Enabled production CanvasScaler driven by the camera render target. Verifies game-viewport input coverage, undistorted ram panels, and complete proportional wordless opening panels. Opening reading state is isolated in memory. No saved scenes, player-profile writes or manual canvas scaling.";
            public string capturedAtUtc;
            public List<CaptureFrame> frames = new List<CaptureFrame>();
        }

        [Serializable] public sealed class CaptureFrame
        {
            public string family, resource, state, file, error;
            public int panel, width, height, comicTextCount, advanceCount;
            public bool verifiedScaler, verifiedCoverage, verifiedInputTarget, verifiedSourceUv, verifiedDialogue;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect, viewportPixels, overlayPixels, pagePixels, advancePixels, sourceUv, visibleUv;
            public float scaleFactor, expectedScaleFactor, visibleAspect, targetAspect;
            public string screenMatchMode;
        }

        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; omit -nographics.");
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new or empty output directory.");
            Directory.CreateDirectory(output);
            var report = new CaptureReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            Vector2Int[] sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 960) };
            for (int sequence = 0; sequence < RamComicCatalog.SequenceCount; sequence++)
                for (int panel = 0; panel < RamComicCatalog.PagesPerSequence; panel++)
                    foreach (Vector2Int size in sizes)
                    {
                        string resource = RamComicCatalog.ImageResource(sequence, panel);
                        AddFrame(report, output, "ram", resource, Path.GetFileName(resource), panel, size);
                    }
            for (int page = 0; page < OpeningComicCatalog.Pages.Count; page++)
                for (int panel = 0; panel < ComicPanelCatalog.QuadPanelCount; panel++)
                    foreach (Vector2Int size in sizes)
                        AddFrame(report, output, "opening", OpeningComicCatalog.Pages[page].ImageResource,
                            "opening_page" + (page + 1) + "_panel" + (panel + 1), page * ComicPanelCatalog.QuadPanelCount + panel, size);
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            int expectedFrames = (RamComicCatalog.SequenceCount * RamComicCatalog.PagesPerSequence +
                OpeningComicCatalog.Pages.Count * ComicPanelCatalog.QuadPanelCount) * sizes.Length;
            if (report.frames.Count != expectedFrames || report.frames.Any(frame => !string.IsNullOrEmpty(frame.error) ||
                !frame.verifiedScaler || !frame.verifiedCoverage || !frame.verifiedInputTarget || !frame.verifiedSourceUv || !frame.verifiedDialogue))
                throw new InvalidOperationException("Fullscreen comic capture failed. See report.json.");
            Debug.Log("COMIC_FULLSCREEN_CAPTURE_OK frames=" + report.frames.Count + " output=" + output);
        }

        private static void AddFrame(CaptureReport report, string output, string family, string resource, string state, int panel, Vector2Int size)
        {
            var frame = new CaptureFrame {
                family = family, resource = resource, state = state, panel = panel,
                width = size.x, height = size.y, file = state + "_" + size.x + "x" + size.y + ".png"
            };
            report.frames.Add(frame);
            try { Capture(output, frame); }
            catch (Exception error) { frame.error = error.ToString(); }
        }

        private static void Capture(string output, CaptureFrame frame)
        {
            Scene scene = default;
            RenderTexture target = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var host = new GameObject("Disposable Fullscreen Comic Audit");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(frame.family == "opening" ? CoreGameplayPreviewScreen.PortHub :
                    CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                Texture2D art = Resources.Load<Texture2D>(frame.resource);
                if (art == null) throw new InvalidOperationException("Missing comic resource: " + frame.resource);
                string overlayName, pageName, advanceName;
                if (frame.family == "ram")
                {
                    Method("BuildRamComicOverlay").Invoke(controller, new object[] {
                        art, frame.resource.Contains("_boss_"), frame.resource.Contains("_head_on_"), string.Empty
                    });
                    Method("SetRamComicPanel").Invoke(controller, new object[] { frame.panel });
                    overlayName = "Battle Ram Comic Overlay";
                    pageName = "Battle Ram Comic Page";
                    advanceName = "Battle Ram Comic Advance";
                    frame.sourceUv = new Rect(0f, 0f, 1f, 1f);
                }
                else if (frame.family == "opening")
                {
                    string readState = string.Empty;
                    var progress = new OpeningPortComicProgress("isolated-fullscreen-audit", () => readState, json => readState = json);
                    progress.TryQueue(OpeningPortComicProgress.OpeningKey);
                    for (int panelIndex = 0; panelIndex < frame.panel; panelIndex++)
                        progress.AdvancePanel(OpeningPortComicProgress.OpeningKey, progress.PendingPage, progress.PendingPanel);
                    Field("openingPortComicProgress").SetValue(controller, progress);
                    Method("BuildOpeningComicOverlay").Invoke(controller, null);
                    overlayName = "Opening Comic Overlay";
                    pageName = "Opening Comic Page";
                    advanceName = "Opening Comic Advance";
                    frame.sourceUv = ComicPanelCatalog.QuadPanel(frame.panel % ComicPanelCatalog.QuadPanelCount);
                }
                else throw new InvalidOperationException("Unknown comic family: " + frame.family);
                Canvas canvas = host.GetComponentsInChildren<Canvas>(true).Single(item => item.name == CoreGameplayDemoController.EditorPreviewCanvasName);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                RectTransform overlay = canvas.GetComponentsInChildren<RectTransform>(false).Single(item => item.name == overlayName);
                RawImage page = overlay.GetComponentsInChildren<RawImage>(false).Single(item => item.name == pageName);
                Button advance = overlay.GetComponentsInChildren<Button>(false).Single(item => item.name == advanceName);
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Layer;

                var cameraHost = new GameObject("Disposable Fullscreen Comic Camera", typeof(Camera));
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
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << Layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                target = new RenderTexture(frame.width, frame.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.aspect = frame.width / (float)frame.height;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", PrivateInstance);
                if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
                // The actual scaler and camera drive the canvas; no scaleFactor, size or scale assignments.
                for (int pass = 0; pass < 4; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    handle.Invoke(scaler, null);
                    page.SetAllDirty();
                    Canvas.ForceUpdateCanvases();
                    if (GraphicsSettings.currentRenderPipeline != null)
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    else camera.Render();
                }
                Verify(frame, canvas, scaler, camera, overlay, page, advance, art);
                RenderTexture.active = target;
                pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0f, 0f, frame.width, frame.height), 0, 0);
                pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32();
                int low = 255, high = 0;
                for (int index = 0; index < samples.Length; index += 997)
                {
                    int brightness = (samples[index].r + samples[index].g + samples[index].b) / 3;
                    low = Math.Min(low, brightness); high = Math.Max(high, brightness);
                }
                if (high - low < 20) throw new InvalidOperationException("Nearly blank frame; render rejected.");
                File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void Verify(CaptureFrame frame, Canvas canvas, CanvasScaler scaler, Camera camera,
            RectTransform overlay, RawImage page, Button advance, Texture2D art)
        {
            frame.renderingDisplaySize = canvas.renderingDisplaySize;
            frame.canvasPixelRect = canvas.pixelRect;
            frame.cameraPixelRect = camera.pixelRect;
            frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
            frame.scaleFactor = canvas.scaleFactor;
            frame.screenMatchMode = scaler.screenMatchMode.ToString();
            float widthRatio = frame.width / scaler.referenceResolution.x, heightRatio = frame.height / scaler.referenceResolution.y;
            frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(widthRatio, heightRatio) :
                scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(widthRatio, heightRatio) :
                Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(widthRatio, 2f), Mathf.Log(heightRatio, 2f), scaler.matchWidthOrHeight));
            frame.verifiedScaler = scaler.enabled &&
                Near(frame.renderingDisplaySize.x, frame.width, .5f) && Near(frame.renderingDisplaySize.y, frame.height, .5f) &&
                Covers(frame.canvasPixelRect, frame) && Covers(frame.cameraPixelRect, frame) &&
                Near(frame.scaleFactor, frame.expectedScaleFactor, .001f) &&
                Near(frame.logicalCanvasSize.x, frame.width / frame.scaleFactor, 1f) && Near(frame.logicalCanvasSize.y, frame.height / frame.scaleFactor, 1f);
            frame.overlayPixels = Pixels(overlay, camera);
            frame.pagePixels = Pixels(page.rectTransform, camera);
            frame.advancePixels = Pixels((RectTransform)advance.transform, camera);
            frame.viewportPixels = Pixels(FixedGameViewport.Bounds(canvas), camera);
            bool pageFits = frame.family == "opening"
                ? frame.pagePixels.width > 0f && frame.pagePixels.height > 0f &&
                    frame.pagePixels.xMin >= frame.viewportPixels.xMin - 1f && frame.pagePixels.xMax <= frame.viewportPixels.xMax + 1f &&
                    frame.pagePixels.yMin >= frame.viewportPixels.yMin - 1f && frame.pagePixels.yMax <= frame.viewportPixels.yMax + 1f &&
                    Near(frame.pagePixels.width / frame.pagePixels.height, art.width / (float)art.height, .002f) &&
                    (Near(frame.pagePixels.width, frame.viewportPixels.width, 1f) || Near(frame.pagePixels.height, frame.viewportPixels.height, 1f)) &&
                    page.GetComponent<AspectRatioFitter>()?.aspectMode == AspectRatioFitter.AspectMode.FitInParent
                : Matches(frame.pagePixels, frame.viewportPixels);
            frame.verifiedCoverage = Matches(frame.overlayPixels, frame.viewportPixels) && pageFits && Matches(frame.advancePixels, frame.viewportPixels);
            Text[] lettering = overlay.GetComponentsInChildren<Text>(true);
            frame.comicTextCount = lettering.Length;
            frame.verifiedDialogue = lettering.Length == 0;
            frame.advanceCount = overlay.GetComponentsInChildren<Button>(true).Length;
            Graphic surface = advance.targetGraphic;
            frame.verifiedInputTarget = frame.advanceCount == 1 && advance.IsInteractable() &&
                advance.navigation.mode == Navigation.Mode.None && advance.transition == Selectable.Transition.None &&
                surface != null && surface.raycastTarget && surface.color.a == 0f && !page.raycastTarget &&
                overlay.GetComponent<Image>().raycastTarget && overlay.GetComponent<GraphicRaycaster>() != null;
            frame.visibleUv = page.uvRect;
            frame.visibleAspect = art.width * page.uvRect.width / (art.height * page.uvRect.height);
            frame.targetAspect = frame.overlayPixels.width / frame.overlayPixels.height;
            frame.verifiedSourceUv = page.texture == art && page.uvRect.xMin >= frame.sourceUv.xMin - .0001f &&
                page.uvRect.xMax <= frame.sourceUv.xMax + .0001f && page.uvRect.yMin >= frame.sourceUv.yMin - .0001f &&
                page.uvRect.yMax <= frame.sourceUv.yMax + .0001f &&
                (frame.family != "ram" ? page.uvRect == frame.sourceUv : Near(frame.visibleAspect, frame.targetAspect, .002f)) &&
                Near(page.uvRect.center.x, frame.sourceUv.center.x, .0001f) && Near(page.uvRect.center.y, frame.sourceUv.center.y, .0001f);
            if (!frame.verifiedScaler || !frame.verifiedCoverage || !frame.verifiedInputTarget || !frame.verifiedSourceUv || !frame.verifiedDialogue)
                throw new InvalidOperationException("Fullscreen/scaler/input/UV/text validation failed for " + frame.state + ".");
        }

        private static MethodInfo Method(string name) => typeof(CoreGameplayDemoController).GetMethod(name, PrivateInstance) ?? throw new MissingMethodException(name);
        private static FieldInfo Field(string name) => typeof(CoreGameplayDemoController).GetField(name, PrivateInstance) ?? throw new MissingFieldException(name);
        private static bool Near(float first, float second, float tolerance) => Mathf.Abs(first - second) < tolerance;
        private static bool Covers(Rect rect, CaptureFrame frame) => Near(rect.xMin, 0f, 1f) && Near(rect.yMin, 0f, 1f) &&
            Near(rect.xMax, frame.width, 1f) && Near(rect.yMax, frame.height, 1f);
        private static bool Matches(Rect rect, Rect expected) => Near(rect.xMin, expected.xMin, 1f) && Near(rect.yMin, expected.yMin, 1f) &&
            Near(rect.xMax, expected.xMax, 1f) && Near(rect.yMax, expected.yMax, 1f);
        private static Rect Pixels(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2[] screen = corners.Select(corner => (Vector2)camera.WorldToScreenPoint(corner)).ToArray();
            return Rect.MinMaxRect(screen.Min(point => point.x), screen.Min(point => point.y), screen.Max(point => point.x), screen.Max(point => point.y));
        }
        private static string Output()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR.");
            return Path.GetFullPath(args[index + 1]);
        }
    }
}
