// Scratch-only PlayMode audit; never install in the production project's Assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    internal static class NavigationInterfaceRefreshVisualCapture
    {
        private const int Width = 1920, Height = 1080, CaptureLayer = 31;
        private static string directory;
        private static readonly Report report = new Report();

        [Serializable]
        private sealed class Report
        {
            public string scope = "Live Unity PlayMode navigation after real EventSystem map/compass pointer clicks. " +
                "Every production regression assertion completes before capture changes the render target. " +
                "Actual enabled CanvasScaler and ScreenSpaceCamera; no manual capture canvas size or scale. " +
                "Isolated test fixture; capture code does not access persistence. This is not a saved preview or mockup.";
            public List<Frame> frames = new List<Frame>();
        }

        [Serializable]
        private sealed class Frame
        {
            public string file, caption, capturedAtUtc, phase, heading, screenMatchMode;
            public int width = Width, height = Height;
            public bool directCourse, verifiedScaler;
            public float courseX, courseY, scaleFactor, expectedScaleFactor;
            public Vector2 renderingDisplaySize, logicalCanvasSize;
            public Rect canvasPixelRect, cameraPixelRect;
        }

        public static void Capture(CoreGameplayDemoController controller, string fileName, string caption)
        {
            ResolveDirectory();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Navigation capture requires graphics; do not use -nographics.");
            if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expected a simple PNG filename.");
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path)) throw new IOException("Refusing to replace capture: " + path);
            GameObject chart = GameObject.Find("Navigation Chart");
            if (chart == null || controller == null) throw new InvalidOperationException("Live navigation UI missing.");
            Canvas canvas = chart.GetComponentInParent<Canvas>().rootCanvas;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null || !scaler.enabled) throw new InvalidOperationException("Production CanvasScaler must be enabled.");
            MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
            if (handle == null) throw new MissingMethodException("CanvasScaler.Handle");
            RenderMode originalMode = canvas.renderMode;
            Camera originalCamera = canvas.worldCamera;
            float originalPlaneDistance = canvas.planeDistance;
            RenderTexture previousActive = RenderTexture.active;
            Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
            int[] layers = children.Select(child => child.gameObject.layer).ToArray();
            GameObject cameraHost = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                cameraHost = new GameObject("Navigation Refresh Capture Camera", typeof(Camera));
                Camera camera = cameraHost.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2000f;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.cullingMask = 1 << CaptureLayer;
                camera.allowHDR = camera.allowMSAA = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = Width / (float)Height;
                foreach (Transform child in children) child.gameObject.layer = CaptureLayer;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;
                // Let the enabled, production scaler derive geometry from the
                // camera's target. Do not assign canvas dimensions or scale.
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

                var frame = new Frame
                {
                    file = fileName, caption = caption, capturedAtUtc = DateTime.UtcNow.ToString("O"),
                    phase = controller.Session.Phase.ToString(), heading = controller.Session.Heading.ToString(),
                    directCourse = controller.Session.HasDirectCourseTarget,
                    courseX = controller.Session.DirectCourseTarget.X, courseY = controller.Session.DirectCourseTarget.Y,
                    renderingDisplaySize = canvas.renderingDisplaySize, canvasPixelRect = canvas.pixelRect,
                    cameraPixelRect = camera.pixelRect, logicalCanvasSize = canvas.GetComponent<RectTransform>().rect.size,
                    scaleFactor = canvas.scaleFactor, screenMatchMode = scaler.screenMatchMode.ToString()
                };
                float wr = Width / scaler.referenceResolution.x, hr = Height / scaler.referenceResolution.y;
                frame.expectedScaleFactor = scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand ? Mathf.Min(wr, hr) :
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Shrink ? Mathf.Max(wr, hr) :
                    Mathf.Pow(2, Mathf.Lerp(Mathf.Log(wr, 2), Mathf.Log(hr, 2), scaler.matchWidthOrHeight));
                frame.verifiedScaler = scaler.enabled &&
                    Mathf.Abs(frame.renderingDisplaySize.x - Width) < .5f && Mathf.Abs(frame.renderingDisplaySize.y - Height) < .5f &&
                    Mathf.Abs(frame.canvasPixelRect.width - Width) < .5f && Mathf.Abs(frame.canvasPixelRect.height - Height) < .5f &&
                    Mathf.Abs(frame.cameraPixelRect.width - Width) < .5f && Mathf.Abs(frame.cameraPixelRect.height - Height) < .5f &&
                    Mathf.Abs(frame.scaleFactor - frame.expectedScaleFactor) < .001f &&
                    Mathf.Abs(frame.logicalCanvasSize.x - Width / frame.scaleFactor) < 1f &&
                    Mathf.Abs(frame.logicalCanvasSize.y - Height / frame.scaleFactor) < 1f;
                report.frames.Add(frame);
                WriteReport();
                if (!frame.verifiedScaler)
                    throw new InvalidOperationException("Actual CanvasScaler geometry did not match the requested target; see report.json.");

                RenderTexture.active = target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply(false, false);
                Color32[] samples = pixels.GetPixels32();
                int low = 255, high = 0;
                for (int i = 0; i < samples.Length; i += 997)
                {
                    int brightness = (samples[i].r + samples[i].g + samples[i].b) / 3;
                    low = Math.Min(low, brightness); high = Math.Max(high, brightness);
                }
                if (high - low < 20) throw new InvalidOperationException("Blank navigation capture: " + fileName);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] png = pixels.EncodeToPNG();
                    stream.Write(png, 0, png.Length);
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                for (int i = 0; i < children.Length; i++)
                    if (children[i] != null) children[i].gameObject.layer = layers[i];
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalPlaneDistance;
                Canvas.ForceUpdateCanvases();
                handle.Invoke(scaler, null);
                Canvas.ForceUpdateCanvases();
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void ResolveDirectory()
        {
            if (directory != null) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-navigationRefreshCapturePath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass -navigationRefreshCapturePath NEW_ABSOLUTE_ARTIFACTS_SUBDIRECTORY.");
            string destination = Path.GetFullPath(args[index + 1]);
            if (!destination.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => string.Equals(part, "Artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Capture output must be beneath Artifacts.");
            if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
                throw new IOException("Use a new or empty capture directory: " + destination);
            Directory.CreateDirectory(destination);
            directory = destination;
        }

        private static void WriteReport() =>
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
    }
}
