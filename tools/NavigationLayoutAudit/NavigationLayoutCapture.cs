// Temporary Editor-only capture entry. Copy this file into the isolated validation
// project's Assets/_Project/Editor; do not install it in the main game's Assets.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShatteredSail.Editor
{
    public static class NavigationLayoutCapture
    {
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped Editor required.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Graphics required: do not use -nographics.");
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
            MethodInfo capture = typeof(UiAuditReadOnlyCapture).GetMethod("CaptureMenu", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string), typeof(UiAuditReadOnlyCapture.MenuFrame) }, null);
            if (capture == null) throw new MissingMethodException("UiAuditReadOnlyCapture.CaptureMenu");
            var report = new UiAuditReadOnlyCapture.MenuReport
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Current production Navigation and Encounter editor-preview UI, five requested render-target sizes each. " +
                    "Disposable preview scenes; actual enabled production CanvasScaler with ScreenSpaceCamera and SRP render request. " +
                    "Each frame validates renderingDisplaySize, pixelRect, logical canvas size and scaleFactor. " +
                    "No player save writes, saved-scene regeneration, gameplay callbacks or performance measurement. " +
                    "TextGeometry includes off-viewport journal content subject to clipping; inspect masks before treating it as visible overflow."
            };
            foreach (string state in new[] { "Navigation", "Encounter" })
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
                    new Vector2Int(1280, 1024), new Vector2Int(3440, 1440), new Vector2Int(3840, 1080) })
                {
                    var frame = new UiAuditReadOnlyCapture.MenuFrame { state = state, width = size.x, height = size.y,
                        file = state + "_" + size.x + "x" + size.y + ".png" };
                    report.frames.Add(frame);
                    try
                    {
                        capture.Invoke(null, new object[] { output, frame });
                        if (!frame.verifiedScaler || !File.Exists(Path.Combine(output, frame.file)))
                            throw new InvalidOperationException("Capture did not produce a verified frame.");
                    }
                    catch (Exception exception)
                    {
                        Exception actual = exception is TargetInvocationException && exception.InnerException != null
                            ? exception.InnerException : exception;
                        frame.error = actual.ToString();
                        Debug.LogWarning(frame.file + ": " + actual.Message);
                    }
                    File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                }
            if (report.frames.Count != 10 || report.frames.Any(frame => !string.IsNullOrEmpty(frame.error) || !frame.verifiedScaler))
                throw new InvalidOperationException("Navigation capture failed; inspect report.json.");
            Debug.Log("NAVIGATION_LAYOUT_CAPTURE_OK: 10 actual UI captures with verified CanvasScaler geometry at " + output);
        }
    }
}
