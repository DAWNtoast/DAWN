// Editor-only audit entry. Install only in the isolated validation project's
// Assets/_Project/Editor folder; never add this tool to production Assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class FixedAspectCapture
    {
        private const int BlackTolerance = 2;
        private const float EdgeClearancePixels = 3f;

        [Serializable] private sealed class CaptureReport
        {
            public string capturedAtUtc;
            public string scope = "Disposable current-code previews at three render-target sizes. " +
                "The original enabled CanvasScaler and CaptureMenu render path are retained. " +
                "A read-only willRenderCanvases observer measures the actual clipped 1920x1080 Game Viewport. " +
                "PNG verification checks every exterior pixel at least three pixels from the viewport, " +
                "requiring RGB channels no higher than 2/255. A 16:9 target has no exterior to sample. " +
                "PortHub is the production enum name for the port overview. No gameplay or save writes.";
            public List<CaptureFrame> frames = new List<CaptureFrame>();
        }

        [Serializable] private sealed class CaptureFrame
        {
            public string state, file, error;
            public int width, height;
            public UiAuditReadOnlyCapture.MenuFrame production;
            public Rect expectedContentPixels, actualContentPixels, nonBlackPixelsBounds;
            public Vector2 viewportLocalSize;
            public Vector3 viewportLocalScale;
            public bool observedViewport, enabledViewportMask, verifiedViewport, verifiedBlackBars;
            public int sampledExteriorPixels, nonBlackExteriorPixels, nonBlackInteriorPixels, maximumExteriorChannel;
            public int firstBadPixelX = -1, firstBadPixelY = -1;
            public string observationError;
        }

        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required; do not use -nographics.");
            string output = OutputDirectory();
            MethodInfo capture = typeof(UiAuditReadOnlyCapture).GetMethod("CaptureMenu",
                BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(string), typeof(UiAuditReadOnlyCapture.MenuFrame) }, null);
            if (capture == null) throw new MissingMethodException("UiAuditReadOnlyCapture.CaptureMenu");
            var report = new CaptureReport { capturedAtUtc = DateTime.UtcNow.ToString("O") };
            string[] states = { "Navigation", "Encounter", "Home", "Settings", "PortHub", "Battle" };
            Vector2Int[] sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 1024), new Vector2Int(3840, 1080) };
            foreach (string state in states)
                foreach (Vector2Int size in sizes)
                {
                    var frame = new CaptureFrame { state = state, width = size.x, height = size.y,
                        file = state + "_" + size.x + "x" + size.y + ".png" };
                    frame.production = new UiAuditReadOnlyCapture.MenuFrame {
                        state = state, width = size.x, height = size.y, file = frame.file,
                    };
                    float scale = Mathf.Min(size.x / 1920f, size.y / 1080f);
                    frame.expectedContentPixels = new Rect((size.x - 1920f * scale) * .5f,
                        (size.y - 1080f * scale) * .5f, 1920f * scale, 1080f * scale);
                    report.frames.Add(frame);
                    Canvas.WillRenderCanvases observer = () => ObserveViewport(frame);
                    try
                    {
                        Canvas.willRenderCanvases += observer;
                        capture.Invoke(null, new object[] { output, frame.production });
                        ValidateGeometry(frame);
                        ValidatePngPixels(Path.Combine(output, frame.file), frame);
                    }
                    catch (Exception exception)
                    {
                        Exception actual = exception is TargetInvocationException && exception.InnerException != null
                            ? exception.InnerException : exception;
                        frame.error = actual.ToString();
                        frame.production.error = frame.error;
                        Debug.LogWarning(frame.file + ": " + actual.Message);
                    }
                    finally
                    {
                        Canvas.willRenderCanvases -= observer;
                        File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                    }
                }
            int failed = report.frames.Count(frame => !string.IsNullOrEmpty(frame.error) ||
                !frame.verifiedViewport || !frame.verifiedBlackBars || !frame.production.verifiedScaler);
            if (report.frames.Count != states.Length * sizes.Length || failed != 0)
                throw new InvalidOperationException("Fixed-aspect capture failed for " + failed + " frame(s); inspect report.json.");
            Debug.Log("FIXED_ASPECT_CAPTURE_OK: 18 UI captures verified with actual scaler, centered 16:9 viewport and PNG black-bar pixels at " + output);
        }

        private static string OutputDirectory()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass -uiAuditPath NEW_ABSOLUTE_OUTPUT_DIR.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Captures must be stored beneath an Artifacts directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a new or empty output directory; previous captures are preserved.");
            Directory.CreateDirectory(output);
            return output;
        }

        private static void ObserveViewport(CaptureFrame frame)
        {
            try
            {
                Canvas canvas = Resources.FindObjectsOfTypeAll<Canvas>().FirstOrDefault(item =>
                    item != null && item.transform.parent != null &&
                    item.transform.parent.name == "Disposable UI Audit Main Menu" &&
                    item.gameObject.activeInHierarchy && item.renderMode == RenderMode.ScreenSpaceCamera && item.worldCamera != null);
                if (canvas == null) return;
                RectTransform viewport = canvas.transform.Find(FixedGameViewport.Name) as RectTransform;
                if (viewport == null) return;
                var corners = new Vector3[4];
                viewport.GetWorldCorners(corners);
                Vector2[] pixels = corners.Select(corner => (Vector2)canvas.worldCamera.WorldToScreenPoint(corner)).ToArray();
                frame.actualContentPixels = Rect.MinMaxRect(pixels.Min(point => point.x), pixels.Min(point => point.y),
                    pixels.Max(point => point.x), pixels.Max(point => point.y));
                frame.viewportLocalSize = viewport.rect.size;
                frame.viewportLocalScale = viewport.localScale;
                RectMask2D mask = viewport.GetComponent<RectMask2D>();
                frame.enabledViewportMask = mask != null && mask.isActiveAndEnabled;
                frame.observedViewport = true;
                frame.observationError = null;
            }
            catch (Exception exception) { frame.observationError = exception.ToString(); }
        }

        private static void ValidateGeometry(CaptureFrame frame)
        {
            Rect actual = frame.actualContentPixels, expected = frame.expectedContentPixels;
            frame.verifiedViewport = frame.production.verifiedScaler && frame.production.screenMatchMode == "Expand" &&
                frame.observedViewport && frame.enabledViewportMask && string.IsNullOrEmpty(frame.observationError) &&
                Vector2.Distance(frame.viewportLocalSize, new Vector2(1920f, 1080f)) < .01f &&
                Vector3.Distance(frame.viewportLocalScale, Vector3.one) < .001f &&
                Mathf.Abs(actual.xMin - expected.xMin) < 1f && Mathf.Abs(actual.xMax - expected.xMax) < 1f &&
                Mathf.Abs(actual.yMin - expected.yMin) < 1f && Mathf.Abs(actual.yMax - expected.yMax) < 1f;
            if (!frame.verifiedViewport)
                throw new InvalidOperationException("Actual viewport is not the centered, uniformly scaled, clipped 1920x1080 content region; inspect geometry in report.json.");
        }

        private static void ValidatePngPixels(string path, CaptureFrame frame)
        {
            if (!File.Exists(path)) throw new IOException("Capture image missing: " + path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false) ||
                    texture.width != frame.width || texture.height != frame.height)
                    throw new IOException("PNG could not be decoded at the requested dimensions: " + path);
                Color32[] pixels = texture.GetPixels32();
                Rect content = frame.expectedContentPixels;
                int minimumX = frame.width, minimumY = frame.height, maximumX = -1, maximumY = -1;
                for (int y = 0; y < frame.height; y++)
                    for (int x = 0; x < frame.width; x++)
                    {
                        Color32 pixel = pixels[y * frame.width + x];
                        int maximum = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
                        bool nonBlack = maximum > BlackTolerance;
                        if (nonBlack)
                        {
                            minimumX = Math.Min(minimumX, x); maximumX = Math.Max(maximumX, x);
                            minimumY = Math.Min(minimumY, y); maximumY = Math.Max(maximumY, y);
                        }
                        float centerX = x + .5f, centerY = y + .5f;
                        bool exterior = centerX <= content.xMin - EdgeClearancePixels || centerX >= content.xMax + EdgeClearancePixels ||
                            centerY <= content.yMin - EdgeClearancePixels || centerY >= content.yMax + EdgeClearancePixels;
                        if (!exterior)
                        {
                            if (nonBlack && content.Contains(new Vector2(centerX, centerY))) frame.nonBlackInteriorPixels++;
                            continue;
                        }
                        frame.sampledExteriorPixels++;
                        frame.maximumExteriorChannel = Math.Max(frame.maximumExteriorChannel, maximum);
                        if (!nonBlack) continue;
                        frame.nonBlackExteriorPixels++;
                        if (frame.firstBadPixelX < 0) { frame.firstBadPixelX = x; frame.firstBadPixelY = y; }
                    }
                frame.nonBlackPixelsBounds = maximumX < 0 ? new Rect() :
                    Rect.MinMaxRect(minimumX, minimumY, maximumX + 1, maximumY + 1);
                bool requiresBars = content.width < frame.width - 6f || content.height < frame.height - 6f;
                frame.verifiedBlackBars = frame.nonBlackExteriorPixels == 0 && frame.nonBlackInteriorPixels > 0 &&
                    (!requiresBars || frame.sampledExteriorPixels > 0);
                if (!frame.verifiedBlackBars)
                    throw new InvalidOperationException("PNG black-bar verification failed: " + frame.nonBlackExteriorPixels +
                        " non-black exterior pixels of " + frame.sampledExteriorPixels + ", maximum channel " +
                        frame.maximumExteriorChannel + ", first bad bottom-left-origin pixel (" + frame.firstBadPixelX + "," + frame.firstBadPixelY + ").");
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
