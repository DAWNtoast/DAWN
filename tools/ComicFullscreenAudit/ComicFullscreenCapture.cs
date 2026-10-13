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
            public string scope = "Real ram, treasure and opening comic builders in disposable Editor PreviewScenes. Enabled production CanvasScaler driven by the camera render target. Verifies full viewport coverage, undistorted ram panels, complete lettered treasure/opening pages, and artistic text only inside authored dialogue or narration boxes. No saved scenes, gameplay actions or manual canvas scaling.";
            public string capturedAtUtc;
            public List<CaptureFrame> frames = new List<CaptureFrame>();
        }

        [Serializable] public sealed class CaptureFrame
        {
            public string family, resource, state, file, error;
            public int panel, width, height, comicTextCount, advanceCount;
            public bool verifiedScaler, verifiedCoverage, verifiedInputTarget, verifiedSourceUv, verifiedDialogue;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect, overlayPixels, pagePixels, advancePixels, sourceUv, visibleUv;
            public Rect narrativeBounds, narrationPixels;
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
            foreach (TreasureStoryChapter chapter in CoreTreasureStoryCatalog.Chapters)
                for (int page = 0; page < chapter.ImageResources.Count; page++)
                    foreach (Vector2Int size in sizes)
                        AddFrame(report, output, "treasure", chapter.ImageResources[page], chapter.RankId + "_page" + (page + 1), page, size);
            for (int page = 0; page < OpeningComicCatalog.Pages.Count; page++)
                foreach (Vector2Int size in sizes)
                    AddFrame(report, output, "opening", OpeningComicCatalog.Pages[page].ImageResource, "opening_page" + (page + 1), page, size);
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            if (report.frames.Count != 52 || report.frames.Any(frame => !string.IsNullOrEmpty(frame.error) ||
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
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
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
                else if (frame.family == "treasure")
                {
                    TreasureStoryChapter chapter = CoreTreasureStoryCatalog.Chapters.Single(item => item.ImageResources.Contains(frame.resource));
                    Field("treasureStoryPage").SetValue(controller, frame.panel);
                    Method("BuildTreasureStoryOverlay").Invoke(controller, new object[] { chapter, null, "audit:" + frame.state + ":" + frame.width });
                    overlayName = "Treasure Story Overlay";
                    pageName = "Treasure Story Comic Page";
                    advanceName = "Treasure Story Advance";
                    frame.sourceUv = new Rect(0f, 0f, 1f, 1f);
                }
                else if (frame.family == "opening")
                {
                    Method("BuildOpeningComicOverlay").Invoke(controller, null);
                    Method("SetOpeningComicPage").Invoke(controller, new object[] { frame.panel });
                    overlayName = "Opening Comic Overlay";
                    pageName = "Opening Comic Page";
                    advanceName = "Opening Comic Advance";
                    frame.sourceUv = new Rect(0f, 0f, 1f, 1f);
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
            frame.verifiedCoverage = Covers(frame.overlayPixels, frame) && Covers(frame.pagePixels, frame) && Covers(frame.advancePixels, frame);
            Text[] lettering = overlay.GetComponentsInChildren<Text>(true);
            frame.comicTextCount = lettering.Length;
            frame.verifiedDialogue = lettering.Length == 0;
            if (frame.family == "treasure")
            {
                TreasureStoryChapter chapter = CoreTreasureStoryCatalog.Chapters.Single(item => item.ImageResources.Contains(frame.resource));
                var expected = TreasureComicDialogueCatalog.GetPage(chapter.RankId, frame.panel);
                frame.verifiedDialogue = lettering.Length == expected.Count && lettering.Select((label, index) =>
                    label.name == "Treasure Comic Dialogue " + (index + 1) && label.text == expected[index].Text &&
                    label.font == GameTypography.InterfaceFont && !label.raycastTarget && label.transform.IsChildOf(page.transform)).All(value => value);
            }
            else if (frame.family == "opening")
            {
                OpeningComicPage expected = OpeningComicCatalog.Pages[frame.panel];
                frame.narrativeBounds = expected.NarrativeBounds;
                frame.verifiedDialogue = lettering.Length == 1;
                if (frame.verifiedDialogue)
                {
                    Text narration = lettering[0];
                    RectTransform rect = narration.rectTransform;
                    frame.narrationPixels = Pixels(rect, camera);
                    Rect safePixels = new Rect(
                        frame.pagePixels.xMin + expected.NarrativeBounds.xMin * frame.pagePixels.width,
                        frame.pagePixels.yMin + expected.NarrativeBounds.yMin * frame.pagePixels.height,
                        expected.NarrativeBounds.width * frame.pagePixels.width,
                        expected.NarrativeBounds.height * frame.pagePixels.height);
                    // The only text belongs to the painted inset. A title/footer,
                    // page counter or visible skip control would fail these checks.
                    frame.verifiedDialogue = narration.name == "Opening Comic Narration" &&
                        narration.text == expected.Narration && narration.transform.parent == page.transform &&
                        narration.font == GameTypography.InterfaceFont && narration.fontStyle == FontStyle.Normal &&
                        narration.color == (Color)new Color32(49, 30, 19, 255) && !narration.raycastTarget &&
                        narration.alignment == TextAnchor.MiddleCenter && narration.resizeTextForBestFit &&
                        narration.resizeTextMinSize == 18 && narration.resizeTextMaxSize == 36 &&
                        narration.cachedTextGenerator.lineCount == 2 &&
                        Vector2.Distance(rect.anchorMin, expected.NarrativeBounds.min) < .0001f &&
                        Vector2.Distance(rect.anchorMax, expected.NarrativeBounds.max) < .0001f &&
                        frame.narrationPixels.width > 0f && frame.narrationPixels.height > 0f &&
                        frame.narrationPixels.xMin >= safePixels.xMin - .5f && frame.narrationPixels.xMax <= safePixels.xMax + .5f &&
                        frame.narrationPixels.yMin >= safePixels.yMin - .5f && frame.narrationPixels.yMax <= safePixels.yMax + .5f;
                }
            }
            frame.advanceCount = overlay.GetComponentsInChildren<Button>(true).Length;
            Graphic surface = advance.targetGraphic;
            frame.verifiedInputTarget = frame.advanceCount == 1 && advance.IsInteractable() &&
                advance.navigation.mode == Navigation.Mode.None && advance.transition == Selectable.Transition.None &&
                surface != null && surface.raycastTarget && surface.color.a == 0f && !page.raycastTarget &&
                overlay.GetComponent<Image>().raycastTarget && overlay.GetComponent<GraphicRaycaster>() != null;
            frame.visibleUv = page.uvRect;
            frame.visibleAspect = art.width * page.uvRect.width / (art.height * page.uvRect.height);
            frame.targetAspect = frame.width / (float)frame.height;
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
