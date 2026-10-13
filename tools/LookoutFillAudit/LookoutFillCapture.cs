// Editor-only audit entry. Install only in the isolated validation project's
// Assets/_Project/Editor folder, never in the production project's Assets.
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
    public static class LookoutFillCapture
    {
        private static readonly string[] PanelPaths =
        {
            "Phase Content/Navigation Chart",
            "Phase Content/Forward View Panel",
            "Phase Content/Navigation Controls"
        };

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
                scope = "Current production Navigation and Encounter editor-preview UI, five render-target sizes each. " +
                    "Disposable preview scenes; actual enabled production CanvasScaler with ScreenSpaceCamera and SRP render request. " +
                    "Each frame validates renderingDisplaySize, pixelRect, logical canvas size, scaleFactor and PNG dimensions. " +
                    "Requires the three main navigation paper panels and absence of Navigation Journal from reported text and paper paths. " +
                    "No player save writes, saved-scene regeneration, gameplay callbacks or performance measurement. " +
                    "Panel paperRect is panel-local geometry; screen coverage is checked separately by the layout tests and screenshot review. " +
                    "TextGeometry includes off-viewport event content subject to clipping; inspect masks before treating it as visible overflow."
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
                        if (!frame.verifiedScaler)
                            throw new InvalidOperationException("Capture did not produce verified CanvasScaler geometry.");
                        ValidatePng(Path.Combine(output, frame.file), frame.width, frame.height);
                        if (frame.text.Any(label => IsJournalPath(label.path)) || frame.papers.Any(panel => IsJournalPath(panel.path)))
                            throw new InvalidOperationException("Navigation Journal remains in captured UI.");
                        string[] panels = frame.papers.Where(panel => panel.path.StartsWith("Phase Content/", StringComparison.Ordinal)
                            && panel.path.Count(character => character == '/') == 1).Select(panel => panel.path).ToArray();
                        if (panels.Length != PanelPaths.Length || PanelPaths.Any(path => !panels.Contains(path)))
                            throw new InvalidOperationException("Capture must contain exactly the three main navigation paper panels.");
                        if (frame.text.Any(label => label.path.EndsWith("Encounter Journal Heading", StringComparison.Ordinal) ||
                            label.path.EndsWith("Encounter Terrain", StringComparison.Ordinal) ||
                            (label.content != null && label.content.Contains("航行已暂停，等待决定"))))
                            throw new InvalidOperationException("Previously removed encounter hint remains in captured UI.");
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
                throw new InvalidOperationException("Lookout fill capture failed; inspect report.json.");
            Debug.Log("LOOKOUT_FILL_CAPTURE_OK: 10 actual UI captures with verified CanvasScaler geometry at " + output);
        }

        private static bool IsJournalPath(string path)
        {
            return path != null && path.Split('/').Any(part => part.StartsWith("Navigation Journal", StringComparison.Ordinal));
        }

        private static void ValidatePng(string path, int width, int height)
        {
            if (!File.Exists(path)) throw new IOException("Capture image is missing: " + path);
            byte[] header = new byte[24];
            using (var stream = File.OpenRead(path))
                if (stream.Length <= header.Length || stream.Read(header, 0, header.Length) != header.Length)
                    throw new IOException("Capture image is truncated: " + path);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 };
            if (!header.Take(signature.Length).SequenceEqual(signature) || ReadBigEndianInt(header, 16) != width ||
                ReadBigEndianInt(header, 20) != height)
                throw new IOException("Capture PNG header or dimensions do not match requested target: " + path);
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }
    }
}
