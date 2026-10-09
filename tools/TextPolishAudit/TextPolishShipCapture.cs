using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class TextPolishShipCapture
    {
        public static void CaptureFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1])) throw new ArgumentException("Absolute -uiAuditPath required.");
            string output = Path.GetFullPath(args[index + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use a fresh output directory.");
            Directory.CreateDirectory(output);
            var report = new UiAuditReadOnlyCapture.MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O"), scope = "Final ship performance copy at two verified production scaler targets; existing read-only capture implementation." };
            MethodInfo capture = typeof(UiAuditReadOnlyCapture).GetMethod("CaptureMenu", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (Vector2Int size in new[] {new Vector2Int(1920,1080),new Vector2Int(1280,720)})
            {
                var frame = new UiAuditReadOnlyCapture.MenuFrame { state="ShipyardDetail", width=size.x,height=size.y,file="ShipyardDetail_"+size.x+"x"+size.y+".png" };
                report.frames.Add(frame);
                try { capture.Invoke(null,new object[] {output,frame}); }
                catch (TargetInvocationException error) { frame.error=error.InnerException.ToString(); }
            }
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            if (report.frames.Any(frame => !frame.verifiedScaler || !string.IsNullOrEmpty(frame.error))) throw new InvalidOperationException("Ship capture failed.");
        }
    }
}
