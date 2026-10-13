using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        public static string[] NavigationEnduranceReferencePaths => new[]
        {
            "UI_Navigation.unity", "UI_Encounter.unity",
            "UINavigation/UINavigation_Exploration.unity",
            "UINavigation/UINavigation_ApproachingEncounter.unity",
            "UINavigation/UINavigation_Logbook.unity",
            "UINavigation/UINavigation_TreasureExploration.unity",
            "UIEncounter/UIEncounter_Choice.unity",
            "UIEncounter/UIEncounter_RiskPreview.unity",
            "UIEncounter/UIEncounter_Resolved.unity",
            "UIEncounter/UIEncounter_TreasureClue.unity",
            "UIEncounter/UIEncounter_TreasureCoreLocked.unity",
            "UIEncounter/UIEncounter_TreasureCoreReady.unity",
            "TutorialVoyage/02_FirstSail.unity",
            "TutorialVoyage/03_FirstMandatory.unity",
            "TutorialVoyage/04_FirstOptional.unity",
            "TutorialVoyage/05_FirstCrewEvent.unity",
            "TutorialVoyage/06_FirstReturn.unity",
            "TutorialVoyage/13_SecondSail.unity",
            "TutorialVoyage/14_SecondCrewEvent.unity",
            "TutorialVoyage/24_ThirdSail.unity",
            "TutorialVoyage/26_ThirdReturn.unity",
        };

        public static void SyncNavigationEnduranceStatesFromCommandLine() => SyncNavigationHudFromCommandLine();

        /// <summary>Keep the legacy HUD entry point aligned with the current three-panel dashboard.</summary>
        public static void SyncNavigationHudFromCommandLine()
        {
            string artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../Artifacts/NavigationThreePanel20261011"));
            string backupRoot = DashboardCommandLinePath("-navigationHudBackupPath",
                Path.Combine(artifactRoot, "scene-backup"));
            string reportPath = DashboardCommandLinePath("-navigationHudReportPath",
                Path.Combine(artifactRoot, "scene-sync.json"));
            NavigationThreePanelPreviewSceneSync.Sync(backupRoot, reportPath);
        }

        public static void SyncAndCaptureNavigationEnduranceFromCommandLine()
        {
            SyncNavigationEnduranceStatesFromCommandLine();
            UIPreviewSceneAcceptance.CaptureNavigationEnduranceFromCommandLine();
        }

        private static bool ValidNavigationCanvasScale(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static void AssertSavedNavigationCanvasScale(string path)
        {
            Match scale = FindSavedNavigationCanvasScale(File.ReadAllText(path), path);
            foreach (string axis in new[] { "x", "y", "z" })
                if (!float.TryParse(scale.Groups[axis].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) || !ValidNavigationCanvasScale(value))
                    throw new InvalidOperationException("Saved navigation Canvas scale is invalid in " + path);
        }

        private static void RepairSavedNavigationCanvasScale(string path)
        {
            // SaveScene recomputes the driven overlay transform, even after an
            // explicit assignment. Repair only that identified root scale line
            // in the written file, keeping every other byte and newline intact.
            byte[] bytes = File.ReadAllBytes(path);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
            int offset = bom ? 3 : 0;
            string text = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
            Match scale = FindSavedNavigationCanvasScale(text, path);
            bool valid = new[] { "x", "y", "z" }.All(axis =>
                float.TryParse(scale.Groups[axis].Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out float value) && ValidNavigationCanvasScale(value));
            if (valid) return;
            text = text.Remove(scale.Index, scale.Length).Insert(scale.Index, "m_LocalScale: {x: 1, y: 1, z: 1}");
            File.WriteAllText(path, text, new UTF8Encoding(bom));
        }

        private static Match FindSavedNavigationCanvasScale(string text, string path)
        {
            Match canvas = Regex.Match(text, @"(?ms)^--- !u!1 &(?<id>\d+)[^\n]*\nGameObject:\r?\n(?:(?!^---).)*?^  m_Name: ['""]?" +
                Regex.Escape(CoreGameplayDemoController.EditorPreviewCanvasName) + @"['""]?\r?$");
            if (!canvas.Success) throw new InvalidOperationException("Missing saved navigation Canvas in " + path);
            string owner = "m_GameObject: {fileID: " + canvas.Groups["id"].Value + "}";
            Match rect = Regex.Matches(text, @"(?ms)^--- !u!224 &\d+[^\n]*\n(?<body>.*?)(?=^---|\z)")
                .Cast<Match>().Single(item => item.Groups["body"].Value.Contains(owner));
            Match scale = new Regex(@"m_LocalScale: \{x: (?<x>[^,]+), y: (?<y>[^,]+), z: (?<z>[^}]+)\}")
                .Match(text, rect.Groups["body"].Index);
            if (!scale.Success || scale.Index + scale.Length > rect.Groups["body"].Index + rect.Groups["body"].Length)
                throw new InvalidOperationException("Missing saved navigation Canvas scale in " + path);
            return scale;
        }

    }
}
