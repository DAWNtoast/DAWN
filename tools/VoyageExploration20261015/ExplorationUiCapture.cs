// Installed only in the isolated PlayMode test assembly. Captures the real live
// UI and preserves its enabled production CanvasScaler at both target sizes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    internal static class ExplorationUiCapture
    {
        private const int Layer = 31;
        private static readonly MethodInfo ScalerHandle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Report Evidence = new Report();
        private static string output;

        [Serializable] private sealed class Report
        {
            public string scope = "Real Unity PlayMode exploration-chain UI; actual enabled production CanvasScaler and camera target dimensions. No saved preview scenes. Each image records a real chain stage arranged by the PlayMode fixture. The helper changes only presentation geometry and scroll position, never voyage state.";
            public List<Frame> frames = new List<Frame>();
        }
        [Serializable] private sealed class Frame
        {
            public string file, state, phase, encounter, summary, capturedAtUtc, error;
            public int width, height, selectedChoice;
            public float scaleFactor;
            public float encounterOpacity;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect gamePixels;
            public Rect explorationStripInChart, seaFrameInChart;
            public bool verifiedScaler;
            public List<Label> labels = new List<Label>();
        }
        [Serializable] private sealed class Label
        {
            public string path, text;
            public int fontSize, visibleCharacters;
            public float preferredHeight, allocatedHeight;
            public Rect pixels;
        }

        public static void Capture(CoreGameplayDemoController controller, string state, int selectedChoice = -1)
        {
            ResolveOutput();
            if (output == null) return;
            Assert.That(Application.isPlaying, Is.True, "Exploration screenshots must come from PlayMode.");
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "Omit -nographics for UI validation.");
            Assert.That(state, Does.Match("^[A-Za-z0-9_-]+$"));
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 1024) })
                CaptureAt(controller, state, selectedChoice, size.x, size.y);
        }

        private static void ResolveOutput()
        {
            if (output != null) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-explorationUiCapturePath");
            if (index < 0) index = Array.IndexOf(args, "-navalVisualCapturePath");
            if (index < 0) return;
            Assert.That(index + 1, Is.LessThan(args.Length));
            Assert.That(Path.IsPathRooted(args[index + 1]), Is.True);
            output = Path.GetFullPath(args[index + 1]);
            Assert.That(output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("Artifacts", StringComparison.OrdinalIgnoreCase)), Is.True);
            Directory.CreateDirectory(output);
        }

        private static void CaptureAt(CoreGameplayDemoController controller, string state, int selectedChoice, int width, int height)
        {
            Canvas canvas = controller.GetComponentsInChildren<Canvas>(false).Single(c => c.isRootCanvas);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null); Assert.That(scaler.enabled, Is.True);
            Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float oldPlaneDistance = canvas.planeDistance;
            RenderTexture oldActive = RenderTexture.active;
            Transform[] descendants = canvas.GetComponentsInChildren<Transform>(true);
            int[] oldLayers = descendants.Select(t => t.gameObject.layer).ToArray();
            ScrollRect scroll = FindOptional<ScrollRect>(controller, "Encounter Decision Viewport");
            float oldScroll = scroll == null ? 1f : scroll.verticalNormalizedPosition;
            Vector2 oldVelocity = scroll == null ? Vector2.zero : scroll.velocity;
            string before = VoyageState(controller.Session);
            GameObject cameraHost = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            Frame frame = new Frame { file = state + "_" + width + "x" + height + ".png", state = state,
                width = width, height = height, selectedChoice = selectedChoice, phase = controller.Session.Phase.ToString(),
                encounter = controller.Session.ActiveEncounter?.Id ?? "", capturedAtUtc = DateTime.UtcNow.ToString("O") };
            try
            {
                cameraHost = new GameObject("Exploration UI Capture Camera", typeof(Camera));
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.orthographic = true; camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0f, 0f, -1000f);
                camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create(); camera.targetTexture = target;
                camera.aspect = width / (float)height; camera.rect = new Rect(0, 0, 1, 1);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 100f;
                foreach (Transform child in descendants) child.gameObject.layer = Layer;
                Refresh(canvas, scaler, camera, target);
                VerifyViewport(canvas, camera, width, height, frame);
                RectTransform explorationStrip = FindOptional<RectTransform>(controller, "Voyage Exploration Status");
                if (explorationStrip != null) ValidateNavigation(controller, frame, camera);
                else ValidateTreasurePort(controller, frame, camera);
                if (controller.Session.Phase == CoreDemoPhase.Encounter)
                {
                    ValidateEncounter(controller, scroll, frame, camera);
                    if (selectedChoice >= 0)
                    {
                        Button choice = Find<Button>(controller, "Encounter Choice " + selectedChoice);
                        RevealRow(scroll, choice.GetComponent<RectTransform>());
                    }
                    Refresh(canvas, scaler, camera, target);
                    if (selectedChoice >= 0)
                        Contains(scroll.viewport.rect, BoundsIn(scroll.viewport,
                            Find<Button>(controller, "Encounter Choice " + selectedChoice).GetComponent<RectTransform>()), "Selected chain branch is reachable");
                }
                foreach (Text label in canvas.GetComponentsInChildren<Text>(false))
                {
                    if (explorationStrip != null && label.transform.IsChildOf(explorationStrip) ||
                        explorationStrip == null && IsTreasurePortLabel(label) ||
                        label.GetComponentInParent<ScrollRect>() == scroll && scroll != null ||
                        label.name == "Encounter Title" || label.name == "Encounter Description" || label.name == "Encounter Badge")
                        frame.labels.Add(new Label { path = PathOf(label.transform, canvas.transform), text = label.text,
                            fontSize = label.fontSize, visibleCharacters = label.cachedTextGenerator.characterCountVisible,
                            preferredHeight = label.preferredHeight, allocatedHeight = label.rectTransform.rect.height,
                            pixels = ScreenBounds(label.rectTransform, camera) });
                }
                Assert.That(VoyageState(controller.Session), Is.EqualTo(before), "Capturing must not alter voyage/save state.");
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32(); int min = 255, max = 0;
                for (int i = 0; i < samples.Length; i += 997)
                { int value = (samples[i].r + samples[i].g + samples[i].b) / 3; min = Math.Min(min, value); max = Math.Max(max, value); }
                Assert.That(max - min, Is.GreaterThan(20), "A blank frame is not a valid screenshot.");
                using (FileStream stream = new FileStream(Path.Combine(output, frame.file), FileMode.CreateNew))
                { byte[] bytes = pixels.EncodeToPNG(); stream.Write(bytes, 0, bytes.Length); }
                Evidence.frames.Add(frame);
                File.WriteAllText(Path.Combine(output, "exploration-ui-report.json"), JsonUtility.ToJson(Evidence, true));
            }
            catch (Exception exception)
            {
                // Keep a clearly marked diagnostic image even when a geometry
                // assertion fails. It is never included as a passing capture.
                frame.error = exception.ToString();
                frame.file = "FAILED_" + frame.file;
                try
                {
                    if (target != null && target.IsCreated())
                    {
                        RenderTexture.active = target;
                        if (pixels != null) Object.DestroyImmediate(pixels);
                        pixels = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(false, false);
                        File.WriteAllBytes(Path.Combine(output, frame.file), pixels.EncodeToPNG());
                    }
                    Evidence.frames.Add(frame);
                    File.WriteAllText(Path.Combine(output, "exploration-ui-report.json"), JsonUtility.ToJson(Evidence, true));
                }
                catch (Exception diagnosticError) { Debug.LogWarning("Failed to preserve UI failure image: " + diagnosticError.Message); }
                throw;
            }
            finally
            {
                RenderTexture.active = oldActive;
                if (scroll != null) { scroll.verticalNormalizedPosition = oldScroll; scroll.velocity = oldVelocity; }
                for (int i = 0; i < descendants.Length; i++) if (descendants[i] != null) descendants[i].gameObject.layer = oldLayers[i];
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlaneDistance;
                ScalerHandle.Invoke(scaler, null); Canvas.ForceUpdateCanvases();
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void Refresh(Canvas canvas, CanvasScaler scaler, Camera camera, RenderTexture target)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                Canvas.ForceUpdateCanvases(); ScalerHandle.Invoke(scaler, null);
                // This synchronous offscreen render does not advance a frame.
                // Run the production scale-change lifecycle explicitly, just as
                // Unity does after CanvasScaler.Update in an ordinary frame.
                foreach (NavigationDashboardLayout layout in canvas.GetComponentsInChildren<NavigationDashboardLayout>(false))
                    layout.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                foreach (Text label in canvas.GetComponentsInChildren<Text>(false)) label.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
            }
        }
        private static void VerifyViewport(Canvas canvas, Camera camera, int width, int height, Frame frame)
        {
            float scale = Mathf.Min(width / 1920f, height / 1080f);
            RectTransform root = FixedGameViewport.Bounds(canvas);
            frame.scaleFactor = canvas.scaleFactor; frame.renderingDisplaySize = canvas.renderingDisplaySize;
            frame.logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size;
            frame.gamePixels = ScreenBounds(root, camera);
            Assert.That(canvas.renderingDisplaySize.x, Is.EqualTo(width).Within(.5f));
            Assert.That(canvas.renderingDisplaySize.y, Is.EqualTo(height).Within(.5f));
            Assert.That(canvas.pixelRect.width, Is.EqualTo(width).Within(.5f));
            Assert.That(canvas.pixelRect.height, Is.EqualTo(height).Within(.5f));
            Assert.That(canvas.scaleFactor, Is.EqualTo(scale).Within(.001f));
            Assert.That(frame.logicalCanvasSize.x, Is.EqualTo(width / scale).Within(.1f));
            Assert.That(frame.logicalCanvasSize.y, Is.EqualTo(height / scale).Within(.1f));
            Assert.That(root.rect.width, Is.EqualTo(1920).Within(.1f));
            Assert.That(root.rect.height, Is.EqualTo(1080).Within(.1f));
            Assert.That(frame.gamePixels.xMin, Is.EqualTo((width - 1920 * scale) / 2f).Within(.2f));
            Assert.That(frame.gamePixels.yMin, Is.EqualTo((height - 1080 * scale) / 2f).Within(.2f));
            Assert.That(frame.gamePixels.width, Is.EqualTo(1920 * scale).Within(.2f));
            Assert.That(frame.gamePixels.height, Is.EqualTo(1080 * scale).Within(.2f));
            frame.verifiedScaler = true;
        }
        private static void ValidateNavigation(CoreGameplayDemoController controller, Frame frame, Camera camera)
        {
            Text summary = Find<Text>(controller, "Voyage Exploration Status Text");
            frame.summary = summary.text; TextFits(summary);
            Assert.That(summary.resizeTextForBestFit, Is.False, "The exploration summary must stay at its authored readable size.");
            RectTransform chart = Find<RectTransform>(controller, "Navigation Chart");
            RectTransform strip = Find<RectTransform>(controller, "Voyage Exploration Status");
            RectTransform sea = Find<RectTransform>(controller, "Procedural Sea Chart Frame");
            Contains(strip.rect, BoundsIn(strip, summary.rectTransform), "Summary fits its strip");
            Contains(chart.rect, BoundsIn(chart, strip), "Exploration strip stays in chart");
            frame.explorationStripInChart = BoundsIn(chart, strip); frame.seaFrameInChart = BoundsIn(chart, sea);
            Assert.That(frame.seaFrameInChart.yMax, Is.LessThanOrEqualTo(frame.explorationStripInChart.yMin + .1f),
                "Exploration strip must not cover sea or target markers (shared boundary allows floating-point roundoff).");
            Contains(frame.gamePixels, ScreenBounds(strip, camera), "Exploration strip stays inside the fixed game surface");
        }
        private static void ValidateEncounter(CoreGameplayDemoController controller, ScrollRect scroll, Frame frame, Camera camera)
        {
            Assert.That(scroll, Is.Not.Null);
            CoreEncounter encounter = controller.Session.ActiveEncounter;
            CanvasGroup reveal = Find<RectTransform>(controller, "Encounter Card").GetComponent<CanvasGroup>();
            Assert.That(reveal, Is.Not.Null);
            frame.encounterOpacity = reveal.alpha;
            Assert.That(reveal.alpha, Is.GreaterThanOrEqualTo(.999f), "Wait for the real encounter entrance animation before capturing.");
            RectTransform paper = Find<RectTransform>(controller, "Encounter Paper Content");
            Rect[] regions = new[] { "Encounter Illustration Mount", "Encounter Title", "Encounter Description", "Encounter Badge" }
                .Select(name => BoundsIn(paper, Find<RectTransform>(controller, name))).Concat(new[] { BoundsIn(paper, scroll.viewport) }).ToArray();
            foreach (Rect region in regions) Contains(paper.rect, region, "Encounter content stays on parchment");
            for (int i = 0; i < regions.Length; i++) for (int j = i + 1; j < regions.Length; j++)
                Assert.That(regions[i].Overlaps(regions[j]), Is.False, "Encounter art, prose, category and decisions must not overlap.");
            foreach (string name in new[] { "Encounter Title", "Encounter Description", "Encounter Badge" }) TextFits(Find<Text>(controller, name));
            for (int i = 0; i < encounter.Choices.Count; i++)
            {
                Button button = Find<Button>(controller, "Encounter Choice " + i);
                Assert.That(button.interactable, Is.EqualTo(controller.Session.IsChoiceAvailable(i)), "Choice availability " + i);
                RectTransform row = button.GetComponent<RectTransform>();
                Assert.That(row.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height + .5f), "Every complete choice must be scroll-reachable.");
                Text[] fields = button.GetComponentsInChildren<Text>(false);
                foreach (Text label in fields)
                {
                    TextFits(label); Contains(row.rect, BoundsIn(row, label.rectTransform), "Choice text stays in its row: " + label.name);
                    if (label.name == "Label" || label.name == "Detail" || label.name == "Hours")
                        Assert.That(label.resizeTextForBestFit, Is.False, "Exploration choices should grow their rows instead of shrinking text.");
                }
                Rect[] boxes = fields.Select(text => BoundsIn(row, text.rectTransform)).ToArray();
                for (int a = 0; a < boxes.Length; a++) for (int b = a + 1; b < boxes.Length; b++)
                    Assert.That(boxes[a].Overlaps(boxes[b]), Is.False, "Choice label / reward / requirement must not overlap.");
            }
            Button[] buttons = scroll.content.GetComponentsInChildren<Button>(false);
            Assert.That(buttons.Any(b => b.name == "Skip Encounter"), Is.EqualTo(!encounter.Mandatory));
            scroll.verticalNormalizedPosition = 1f; Canvas.ForceUpdateCanvases();
            Contains(scroll.viewport.rect, BoundsIn(scroll.viewport, buttons.First().GetComponent<RectTransform>()), "First choice reachable");
            scroll.verticalNormalizedPosition = 0f; Canvas.ForceUpdateCanvases();
            Contains(scroll.viewport.rect, BoundsIn(scroll.viewport, buttons.Last().GetComponent<RectTransform>()), "Last action reachable");
        }
        private static string VoyageState(CoreVoyageSession session) => session.CanCaptureVoyageCheckpoint
            ? JsonUtility.ToJson(session.CaptureVoyageCheckpoint())
            : session.Phase + ":" + JsonUtility.ToJson(session.CaptureTreasureProgress());

        private static bool IsTreasurePortLabel(Text label) => label.name.StartsWith("Treasure Folio ", StringComparison.Ordinal) ||
            label.name.StartsWith("Treasure Map ", StringComparison.Ordinal) || label.name.StartsWith("Departure Dock Treasure ", StringComparison.Ordinal);

        private static void ValidateTreasurePort(CoreGameplayDemoController controller, Frame frame, Camera camera)
        {
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Text[] labels = controller.GetComponentsInChildren<Text>(false).Where(IsTreasurePortLabel).ToArray();
            Assert.That(labels.Length, Is.GreaterThan(0), "A port capture must show the live treasure folio or departure selection.");
            foreach (Text label in labels)
            {
                TextFits(label);
                Contains(frame.gamePixels, ScreenBounds(label.rectTransform, camera), "Treasure explanation stays on screen: " + label.name);
            }
            frame.summary = string.Join(" | ", labels.Where(label => label.name.Contains("Mechanic") || label.name.Contains("Detail")).Select(label => label.text));
        }
        private static void RevealRow(ScrollRect scroll, RectTransform row)
        {
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; Canvas.ForceUpdateCanvases();
            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            if (overflow <= 0) return;
            Rect bounds = BoundsIn(scroll.viewport, row);
            float distance = scroll.viewport.rect.yMax - bounds.yMax;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(distance / overflow);
            Canvas.ForceUpdateCanvases();
        }
        private static void TextFits(Text text)
        {
            if (string.IsNullOrEmpty(text.text)) return;
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f), text.name + ": " + text.text);
            if (text.horizontalOverflow == HorizontalWrapMode.Overflow)
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + .5f), text.name + ": complete line width");
            // Off-viewport rows can be culled before UGUI fills its mesh cache.
            // Generate the same text settings independently so masking is not
            // mistaken for truncated content in otherwise reachable rows.
            using (var generator = new TextGenerator())
            {
                generator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
                string plain = text.supportRichText ? System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]+>", "") : text.text;
                Assert.That(generator.characterCountVisible, Is.GreaterThanOrEqualTo(plain.Length), text.name + ": complete glyph geometry");
            }
        }
        private static void Contains(Rect parent, Rect child, string reason)
        {
            Assert.That(child.xMin, Is.GreaterThanOrEqualTo(parent.xMin - 1f), reason);
            Assert.That(child.xMax, Is.LessThanOrEqualTo(parent.xMax + 1f), reason);
            Assert.That(child.yMin, Is.GreaterThanOrEqualTo(parent.yMin - 1f), reason);
            Assert.That(child.yMax, Is.LessThanOrEqualTo(parent.yMax + 1f), reason);
        }
        private static T Find<T>(CoreGameplayDemoController controller, string name) where T : Component =>
            controller.GetComponentsInChildren<T>(false).Single(item => item.name == name);
        private static T FindOptional<T>(CoreGameplayDemoController controller, string name) where T : Component =>
            controller.GetComponentsInChildren<T>(false).SingleOrDefault(item => item.name == name);
        private static Rect BoundsIn(Transform parent, RectTransform child)
        { var corners = new Vector3[4]; child.GetWorldCorners(corners); return Bounds(corners.Select(point => (Vector2)parent.InverseTransformPoint(point))); }
        private static Rect ScreenBounds(RectTransform child, Camera camera)
        { var corners = new Vector3[4]; child.GetWorldCorners(corners); return Bounds(corners.Select(point => (Vector2)camera.WorldToScreenPoint(point))); }
        private static Rect Bounds(IEnumerable<Vector2> points)
        { Vector2[] p = points.ToArray(); return Rect.MinMaxRect(p.Min(v => v.x), p.Min(v => v.y), p.Max(v => v.x), p.Max(v => v.y)); }
        private static string PathOf(Transform item, Transform root)
        { var names = new List<string>(); for (Transform t = item; t != null && t != root; t = t.parent) names.Insert(0, t.name); return string.Join("/", names); }
    }
}
