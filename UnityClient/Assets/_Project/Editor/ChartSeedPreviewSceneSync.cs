using System;
using System.IO;
using System.Linq;
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
        public static string[] ChartSeedReferencePaths => new[]
        {
            "UI_VoyageDeparture.unity",
            "UIVoyageDeparture/UIVoyageDeparture_RouteSelect.unity",
            "UIVoyageDeparture/UIVoyageDeparture_ShipSelect.unity",
            "UIVoyageDeparture/UIVoyageDeparture_ReadyToDepart.unity",
            "UIVoyageDeparture/UIVoyageDeparture_TreasureReady.unity",
            "UIVoyageDeparture/UIVoyageDeparture_RecordedChart.unity",
            "UIVoyageDeparture/UIVoyageDeparture_InvalidChart.unity",
            "TutorialVoyage/01_FirstDeparture.unity",
            "TutorialVoyage/10_SecondPier.unity",
            "TutorialVoyage/11_SecondRoute.unity",
            "TutorialVoyage/12_SecondDeparture.unity",
            "TutorialVoyage/21_ThirdPier.unity",
            "TutorialVoyage/22_ThirdRoute.unity",
            "TutorialVoyage/23_ThirdDeparture.unity",
        };

        private static readonly string[] DepartureChartControlNames =
        {
            "Use Chart Seed", "Seed Locked Hint", "Departure Chart Selection", "Seed Label", "Treasure Seed",
        };

        private static readonly string[] ObsoleteSetupChartControlNames =
        {
            "Seed Label", "Use Chart Seed", "Seed Input", "Chart Seed Validation", "Seed Locked Hint", "Treasure Seed",
        };

        public static void SyncChartSeedStatesFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Chart seed previews require stopped edit mode.");
            string[] references = ChartSeedReferencePaths;
            foreach (string reference in references)
            {
                int state = reference == "UIVoyageDeparture/UIVoyageDeparture_RecordedChart.unity" ? 1 :
                    reference == "UIVoyageDeparture/UIVoyageDeparture_InvalidChart.unity" ? 2 : 0;
                SyncChartSeedPreview(reference, state);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CHART_SEED_PREVIEW_SYNC: synchronized left-column chart selection in 14 departure pages; authored route art, destination buttons and tutorial gates retained.");
        }

        public static void SyncAndCaptureChartSeedsFromCommandLine()
        {
            SyncChartSeedStatesFromCommandLine();
            UIPreviewSceneAcceptance.CaptureChartSeedsFromCommandLine();
        }

        public static void SyncAndCaptureDepartureChartSelectionFromCommandLine()
        {
            SyncChartSeedStatesFromCommandLine();
            UIPreviewSceneAcceptance.CaptureDepartureChartSelectionFromCommandLine();
        }

        private static void CopyDepartureChartControls(Transform sourceSetup, Transform targetSetup)
        {
            Transform sourceRoutes = sourceSetup.parent.Find("Route Selection");
            Transform targetRoutes = targetSetup.parent.Find("Route Selection");
            if (sourceRoutes == null || targetRoutes == null)
                throw new InvalidOperationException("Chart selection requires the existing left Route Selection panel.");

            // Keep this shared helper compatible with the provisioning synchronizer:
            // chart selection belongs to the route column, never the ship setup.
            foreach (string name in ObsoleteSetupChartControlNames)
                CopyTreasureLayer(null, targetSetup, name);
            foreach (string name in DepartureChartControlNames)
                CopyTreasureLayer(sourceRoutes.Find(name), targetRoutes, name);
            InputField input = targetRoutes.Find("Departure Chart Selection/Seed Input")?.GetComponent<InputField>();
            if (input != null && (!input.enabled || input.textComponent == null || !input.textComponent.gameObject.activeSelf ||
                targetRoutes.Find("Use Chart Seed") == null))
                throw new InvalidOperationException("The synchronized chart seed field must keep its visible editable text and mode toggle.");
        }

        private static void SyncChartSeedPreview(string reference, int state)
        {
            bool tutorial = reference.StartsWith("TutorialVoyage/", StringComparison.Ordinal);
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            bool existed = File.Exists(absolute);
            if (existed)
            {
                string backup = AbsolutePath("Artifacts/DepartureChartSelection20261007/preview-backup/" + reference);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(absolute, backup);
            }
            else if (state == 0) throw new FileNotFoundException("Missing authored voyage departure preview", path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            string template = existed ? path : EditableSceneFolder + "/UI_VoyageDeparture.unity";
            Scene scene = EditorSceneManager.OpenScene(template, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.VoyageDeparture)
                throw new InvalidOperationException("Expected editorPreviewScreen 10 in " + template);
            Transform targetContent = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
            Transform targetSetup = targetContent.Find("Voyage Setup");
            Transform targetRoutes = targetContent.Find("Route Selection");
            if (targetSetup == null || targetRoutes == null)
                throw new InvalidOperationException("Missing authored departure columns in " + template);
            string[] retainedLayers = targetContent.Cast<Transform>()
                .Where(item => item.name != "Chart Seed Warning").Select(item => item.name).ToArray();
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__ChartSeedPreviewStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (state > 0) source.ConfigureEditorPreviewDepartureChart(state);
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete chart seed staging preview: " + reference);
                Transform sourceContent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                Transform sourceSetup = sourceContent.Find("Voyage Setup");
                Transform sourceRoutes = sourceContent.Find("Route Selection");
                if (sourceSetup == null || sourceRoutes == null)
                    throw new InvalidOperationException("Missing chart seed staging columns.");
                PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.VoyageDeparture);
                foreach (string name in DepartureChartControlNames)
                {
                    Transform control = sourceRoutes.Find(name);
                    if (control != null) PersistGeneratedSceneVisuals(control, spec);
                }
                Transform warning = sourceContent.Find("Chart Seed Warning");
                if (warning != null) PersistGeneratedSceneVisuals(warning, spec);
                CopyDepartureChartControls(sourceSetup, targetSetup);
                if (tutorial) LockTutorialChartPreviewControls(targetRoutes);
                if (source.Session.SelectedTreasureMap == null)
                {
                    Toggle mode = targetRoutes.Find("Use Chart Seed")?.GetComponent<Toggle>();
                    Transform selection = targetRoutes.Find("Departure Chart Selection");
                    if (mode == null || selection == null || mode.isOn != (state > 0) ||
                        selection.gameObject.activeSelf != (state > 0))
                        throw new InvalidOperationException("The left-column chart mode was not synchronized: " + reference);
                }
                else if (targetRoutes.Find("Treasure Seed") == null)
                    throw new InvalidOperationException("The treasure seed must remain visible above the left panel: " + reference);
                if (targetSetup.Find("Departure Crew Configuration Title") == null)
                {
                    Button targetStart = targetSetup.Find("Start Voyage")?.GetComponent<Button>();
                    Button sourceStart = sourceSetup.Find("Start Voyage")?.GetComponent<Button>();
                    if (!tutorial && targetStart != null && sourceStart != null) targetStart.interactable = sourceStart.interactable;
                    if (state > 0)
                    {
                        // New samples inherit the authored page, then update only
                        // the captions whose content depends on their chosen chart.
                        foreach (string name in new[] { "Destination Summary", "Cargo", "Departure Provision Quote" })
                        {
                            Text targetText = targetSetup.Find(name)?.GetComponent<Text>();
                            Text sourceText = sourceSetup.Find(name)?.GetComponent<Text>();
                            if (targetText != null && sourceText != null) targetText.text = sourceText.text;
                        }
                    }
                }
                // Teaching pages have authored route/destination gates and focus
                // highlights. Updating the seed row must never reopen those gates.
                SynchronizeChartRouteVisibility(sourceRoutes, targetRoutes, tutorial);
                if (!tutorial) SynchronizeChartRouteControls(sourceRoutes, targetRoutes, state > 0);
                if (state == 2 && warning == null) throw new InvalidOperationException("Invalid chart sample needs its warning modal.");
                CopyTreasureLayer(warning, targetContent, "Chart Seed Warning");
                if (state > 0)
                {
                    Toggle selected = targetRoutes.Find("Use Chart Seed")?.GetComponent<Toggle>();
                    if (selected == null || !selected.isOn) throw new InvalidOperationException("The chart sample must have seed mode enabled.");
                    if (targetRoutes.Find("Departure Chart Selection")?.gameObject.activeSelf != true)
                        throw new InvalidOperationException("The selected chart must replace the route list in the left column.");
                    if (targetRoutes.Find("Departure Chart Selection/Seed Input")?.GetComponent<InputField>()?.interactable != true)
                        throw new InvalidOperationException("The recorded chart sample must retain an editable seed.");
                }
                if (targetSetup.GetComponentsInChildren<Toggle>(true).Any(toggle => toggle.name == "Use Chart Seed") ||
                    ObsoleteSetupChartControlNames.Any(name => targetSetup.Find(name) != null))
                    throw new InvalidOperationException("Legacy right-column chart controls remain in " + reference);
                if (!retainedLayers.SequenceEqual(targetContent.Cast<Transform>()
                    .Where(item => item.name != "Chart Seed Warning").Select(item => item.name)))
                    throw new InvalidOperationException("Authored departure layers changed unexpectedly in " + reference);
                Text targetClock = targetContent.Find("Port Facility Header/Port Facility Time")?.GetComponent<Text>();
                Text sourceClock = sourceContent.Find("Port Facility Header/Port Facility Time")?.GetComponent<Text>();
                if (targetClock != null && sourceClock != null) targetClock.text = sourceClock.text;
                SyncPreviewCanvasScaling(source, destination);
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save chart seed preview " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }

        private static void SynchronizeChartRouteVisibility(Transform source, Transform destination, bool tutorial)
        {
            foreach (Transform target in destination)
            {
                if (!IsDepartureChartRouteChoice(target.name)) continue;
                Transform generated = source.Find(target.name);
                if (generated != null) target.gameObject.SetActive(tutorial || generated.gameObject.activeSelf);
            }
        }

        private static bool IsDepartureChartRouteChoice(string name)
        {
            return name.StartsWith("Route ", StringComparison.Ordinal) ||
                name.StartsWith("Departure Destination ", StringComparison.Ordinal);
        }

        private static void SynchronizeChartRouteControls(Transform source, Transform destination, bool updateLabels)
        {
            if (source == null || destination == null) return;
            foreach (Button sourceButton in source.GetComponentsInChildren<Button>(true))
            {
                if (!IsDepartureChartRouteChoice(sourceButton.name)) continue;
                Button targetButton = destination.Find(sourceButton.name)?.GetComponent<Button>();
                if (targetButton == null) continue;
                targetButton.interactable = sourceButton.interactable;
                CanvasGroup sourceGroup = sourceButton.GetComponent<CanvasGroup>();
                if (sourceGroup != null)
                {
                    CanvasGroup targetGroup = targetButton.GetComponent<CanvasGroup>();
                    if (targetGroup == null) targetGroup = targetButton.gameObject.AddComponent<CanvasGroup>();
                    targetGroup.alpha = sourceGroup.alpha;
                }
                if (updateLabels)
                {
                    Text sourceText = sourceButton.GetComponentInChildren<Text>(true);
                    Text targetText = targetButton.GetComponentInChildren<Text>(true);
                    if (sourceText != null && targetText != null) targetText.text = sourceText.text;
                }
            }
        }

        private static void LockTutorialChartPreviewControls(Transform routes)
        {
            Toggle mode = routes.Find("Use Chart Seed")?.GetComponent<Toggle>();
            if (mode != null)
            {
                mode.SetIsOnWithoutNotify(false);
                mode.interactable = false;
            }
            Transform selection = routes.Find("Departure Chart Selection");
            if (selection != null) selection.gameObject.SetActive(false);
            InputField input = selection?.Find("Seed Input")?.GetComponent<InputField>();
            if (input != null)
            {
                input.SetTextWithoutNotify(string.Empty);
                input.interactable = false;
                if (input.placeholder is Text placeholder)
                    placeholder.text = "教学指定海图，完成教学后可自行选择";
                Dropdown history = input.transform.Find("Seed Input Dropdown")?.GetComponent<Dropdown>();
                if (history != null)
                {
                    history.interactable = false;
                    history.ClearOptions();
                    history.AddOptions(new System.Collections.Generic.List<string> { "教学指定航程" });
                    history.SetValueWithoutNotify(0);
                }
            }
            Text label = routes.Find("Use Chart Seed/Seed Label")?.GetComponent<Text>();
            if (label != null) label.text = "海图种子 · 教学指定航程";
            Text validation = selection?.Find("Chart Seed Validation")?.GetComponent<Text>();
            if (validation != null) validation.text = "教学使用固定海图；完成引导后开放随机与已绘海图。";
            Text locked = routes.Find("Seed Locked Hint")?.GetComponent<Text>();
            if (locked != null) locked.text = "教学指定航程 · 完成引导后可选择已绘海图";
        }
    }
}
