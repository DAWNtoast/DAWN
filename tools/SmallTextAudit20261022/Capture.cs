// Install only in an isolated Unity project's Editor folder.
// Builds disposable current-code Editor previews; never saves scenes or assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class SmallTextCapture
    {
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture requires stopped Editor.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-smallTextOutput");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass -smallTextOutput NEW_ABSOLUTE_OUTPUT_DIR");
            string output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            if (Directory.EnumerateFiles(output, "*.png").Any() || File.Exists(Path.Combine(output, "report.json")))
                throw new IOException("Preserve earlier captures: use a new output directory.");

            var report = new UiAuditReadOnlyCapture.MenuReport
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Current production code rendered in disposable Editor PreviewScenes at 1920x1080. In-memory fixture state only; no scene, asset or player save writes. Only visible text meshes are evidence; inspect screenshots for occlusion. Individual failures remain recorded."
            };
            var cases = new List<string[]>();
            foreach (string screen in Enum.GetNames(typeof(CoreGameplayPreviewScreen)))
                cases.Add(new[] { screen, "" });
            cases.AddRange(new[]
            {
                new[] { "VoyageDeparture", "BasicSupplies" },
                new[] { "VoyageDeparture", "AuditDepartureCharts" },
                new[] { "VoyageDeparture", "AuditDepartureTreasure" },
                new[] { "VoyageDeparture", "Crew" },
                new[] { "Warehouse", "Storage" },
                new[] { "Warehouse", "Rare" },
                new[] { "Warehouse", "Adjustment" },
                new[] { "Warehouse", "Sale" },
                new[] { "TreasureMap", "Folio" },
                new[] { "TreasureMap", "History" },
                new[] { "TreasureMap", "AuditFolioEmptyUnused" },
                new[] { "TreasureMap", "AuditFolioEmptyUsed" },
                new[] { "Navigation", "Cargo" },
                new[] { "Shipyard", "AuditShipyardEquipment" },
                new[] { "Battle", "AuditEnemyOverview" },
                new[] { "Battle", "AuditResourceOverview" },
                new[] { "Battle", "AuditNavalMovement" },
                new[] { "Encounter", "AuditEncounterOverflow" }
            });
            int onlyIndex = Array.IndexOf(args, "-smallTextOnly");
            if (onlyIndex >= 0 && onlyIndex + 1 < args.Length)
            {
                var requested = new HashSet<string>(args[onlyIndex + 1].Split(','), StringComparer.Ordinal);
                cases = cases.Where(item => requested.Contains(item[0]) || requested.Contains(item[1])).ToList();
                if (cases.Count == 0) throw new ArgumentException("No requested captures match -smallTextOnly.");
            }
            MethodInfo capture = typeof(UiAuditReadOnlyCapture).GetMethod("CaptureMenu", BindingFlags.NonPublic | BindingFlags.Static);
            if (capture == null) throw new MissingMethodException("UiAuditReadOnlyCapture.CaptureMenu");
            foreach (string[] item in cases)
            {
                var frame = new UiAuditReadOnlyCapture.MenuFrame
                {
                    state = item[0], variant = item[1], width = 1920, height = 1080,
                    file = item[0] + (item[1].Length == 0 ? "" : "_" + item[1]) + "_1920x1080.png"
                };
                report.frames.Add(frame);
                try
                {
                    capture.Invoke(null, new object[] { output, frame });
                    Debug.Log("SMALL_TEXT_CAPTURE_OK: " + frame.file);
                }
                catch (Exception error)
                {
                    Exception source = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                    frame.error = source.ToString();
                    Debug.LogWarning("SMALL_TEXT_CAPTURE_FAILED: " + frame.file + " " + source.Message);
                }
                File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            }
            int failed = report.frames.Count(f => !string.IsNullOrEmpty(f.error));
            Debug.Log("SMALL_TEXT_AUDIT_COMPLETE: " + (report.frames.Count - failed) + " captured, " + failed + " failed; " + output);
        }
    }
}
