// Isolated-project-only visual audit. No scenes or player saves are written.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static class LightPaperCapture
    {
        public static void CaptureFromCommandLine()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stopped editor required.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-uiAuditPath");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass an absolute output path.");
            string output = Path.GetFullPath(args[index + 1]);
            if (!output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part == "Artifacts"))
                throw new ArgumentException("Use an Artifacts directory.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Preserve prior captures; use a fresh directory.");
            Directory.CreateDirectory(output);
            var report = new UiAuditReadOnlyCapture.MenuReport {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Current-code light paper UI at two real CanvasScaler resolutions. Includes unchanged tavern and component dossiers, navigation, departure, workshop, shipyard, maintenance and modal surfaces. Disposable preview scenes; no scene regeneration or player saves."
            };
            string[][] states = {
                new[]{"Navigation",""},new[]{"Encounter",""},new[]{"VoyageDeparture",""},
                new[]{"DeckBuilder",""},new[]{"CardWorkshop",""},new[]{"CardUpgrade",""},
                new[]{"ShipyardDetail",""},new[]{"Maintenance",""},new[]{"SpecialCardCollection",""},
                new[]{"Settings",""},new[]{"Achievements",""},new[]{"NewGameConfirm",""},new[]{"SaveNotice",""},
                new[]{"TavernCrewDetail",""},new[]{"ComponentShopDetail",""},
                new[]{"VoyageDeparture","Crew"},new[]{"VoyageDeparture","BasicSupplies"},
                new[]{"VoyageDeparture","ChartWarning"},new[]{"Navigation","Cargo"},
                new[]{"Warehouse","Actions"},new[]{"Warehouse","Adjustment"},new[]{"PortHub","ReturnMenu"}
            };
            MethodInfo capture = typeof(UiAuditReadOnlyCapture).GetMethod("CaptureMenu",BindingFlags.NonPublic|BindingFlags.Static);
            foreach (string[] state in states)
            foreach (Vector2Int size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
            {
                var frame = new UiAuditReadOnlyCapture.MenuFrame {
                    state=state[0],variant=state[1],width=size.x,height=size.y,
                    file=state[0]+(state[1].Length==0?"":"_"+state[1])+"_"+size.x+"x"+size.y+".png"
                };
                report.frames.Add(frame);
                try { capture.Invoke(null,new object[]{output,frame}); }
                catch(Exception error) { frame.error=(error.InnerException??error).ToString(); }
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            }
            if (report.frames.Count!=44 || report.frames.Any(frame=>!frame.verifiedScaler || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Light paper capture failed; inspect report.json.");
            Debug.Log("LIGHT_PAPER_CAPTURE_OK: "+report.frames.Count+" verified frames at "+output);
        }
    }
}