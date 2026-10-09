using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        public static void SyncNavigationEnduranceStatesFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Navigation endurance previews require edit mode.");
            foreach (string reference in NavigationEnduranceReferencePaths)
                SyncNavigationEnduranceScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NAVIGATION_ENDURANCE_PREVIEW_SYNC: updated " + NavigationEnduranceReferencePaths.Length +
                " authored navigation/event/tutorial scenes; existing maps, encounters, cargo and journal text retained.");
        }

        public static void SyncAndCaptureNavigationEnduranceFromCommandLine()
        {
            SyncNavigationEnduranceStatesFromCommandLine();
            UIPreviewSceneAcceptance.CaptureNavigationEnduranceFromCommandLine();
        }

        private static void SyncNavigationEnduranceScene(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored navigation preview", path);
            string backup = AbsolutePath("Artifacts/NavigationEndurance20260930/preview-backup/" + path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.Navigation &&
                destination.EditorPreviewScreen != CoreGameplayPreviewScreen.Encounter)
                throw new InvalidOperationException("Unexpected preview screen in " + path);
            Transform targetJournal = destination.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "Navigation Journal");
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__NavigationEnduranceStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete endurance source for " + path);
                if (!destination.GetComponentsInChildren<Text>(true)
                    .Any(label => label.text == source.Session.SelectedShip.Name))
                    throw new InvalidOperationException("The saved ship does not match the endurance sample in " + path);

                // Some authored tutorial/event pages contain hand-staged cargo
                // figures. Derive the new sample from those retained figures,
                // rather than silently displaying a different stock of rations.
                Text cargo = targetJournal.Find("Cargo Status").GetComponent<Text>();
                source.Session.Cargo.Water = ReadPreviewRations(cargo.text, "淡水", path);
                source.Session.Cargo.Food = ReadPreviewRations(cargo.text, "食物", path);
                MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingMethodException(typeof(CoreGameplayDemoController).FullName, "Render");
                render.Invoke(source, null);
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == source.EditorPreviewScreen);
                PersistGeneratedSceneVisuals(staging.transform, artSpec);
                Transform sourceJournal = source.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Navigation Journal");
                RectTransform sourceScroll = (RectTransform)sourceJournal.Find("Voyage Journal Scroll");
                RectTransform targetScroll = (RectTransform)targetJournal.Find("Voyage Journal Scroll");
                Transform sourceEndurance = sourceJournal.Find("Voyage Supply Endurance");
                if (sourceScroll == null || targetScroll == null || sourceEndurance == null)
                    throw new InvalidOperationException("Navigation endurance controls are missing in " + path);

                // Only reserve the new footer space. The saved journal, its
                // art, cargo numbers, event cards and map nodes stay authored.
                targetScroll.sizeDelta = sourceScroll.sizeDelta;
                targetScroll.anchoredPosition = sourceScroll.anchoredPosition;
                Text log = targetScroll.Find("Voyage Log").GetComponent<Text>();
                float minimumLogHeight = sourceScroll.sizeDelta.y - 2f;
                log.rectTransform.sizeDelta = new Vector2(log.rectTransform.sizeDelta.x,
                    Mathf.Max(minimumLogHeight, log.preferredHeight + 18f));
                CopyTreasureLayer(sourceEndurance, targetJournal, "Voyage Supply Endurance");
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            // In batch edit mode an overlay Canvas can receive zero scale from
            // its unavailable Game view. Saving that transient value hides the
            // authored page in Scene view; the screenshot path resets scale and
            // would otherwise conceal the regression. Keep the scaler itself.
            RectTransform canvasRect = destination.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName)
                .GetComponent<RectTransform>();
            Vector3 scale = canvasRect.localScale;
            if (!ValidNavigationCanvasScale(scale.x) || !ValidNavigationCanvasScale(scale.y) ||
                !ValidNavigationCanvasScale(scale.z))
                canvasRect.localScale = Vector3.one;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
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

        private static int ReadPreviewRations(string text, string label, string path)
        {
            Match match = Regex.Match(text, Regex.Escape(label) + @"\s+(\d+)");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int amount))
                throw new InvalidOperationException("Cannot read the saved " + label + " sample in " + path);
            return amount;
        }
    }
}
