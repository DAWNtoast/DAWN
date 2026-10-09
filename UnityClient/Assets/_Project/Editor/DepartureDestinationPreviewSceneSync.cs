using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
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
        public static void SyncDepartureDestinationStatesFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Departure destination previews require stopped edit mode.");
            foreach (string reference in ChartSeedReferencePaths)
                SyncDepartureDestinationScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DEPARTURE_DESTINATION_PREVIEW_SYNC: updated " + ChartSeedReferencePaths.Length +
                " authored departure scenes; route artwork, tutorial gates and unrelated layout retained.");
        }

        public static void SyncAndCaptureDepartureDestinationsFromCommandLine()
        {
            SyncDepartureDestinationStatesFromCommandLine();
            UIPreviewSceneAcceptance.CaptureDepartureDestinationsFromCommandLine();
        }

        private static void SyncDepartureDestinationScene(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored departure preview", path);
            string backup = AbsolutePath("Artifacts/DepartureDestinations20261007/preview-backup/" + reference);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.VoyageDeparture)
                throw new InvalidOperationException("Expected a departure preview in " + path);
            Transform targetContent = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
            Transform targetRoutes = targetContent.Find("Route Selection");
            if (targetRoutes == null) throw new InvalidOperationException("Missing authored route panel in " + path);

            Button oldPicker = targetRoutes.Find("Destination Port 0")?.GetComponent<Button>();
            bool tutorial = reference.StartsWith("TutorialVoyage/", StringComparison.Ordinal);
            bool tutorialDestinationEnabled = oldPicker != null ? oldPicker.interactable :
                targetRoutes.GetComponentsInChildren<Button>(true).Any(button =>
                    button.name.StartsWith("Departure Destination ", StringComparison.Ordinal) && button.interactable);
            string oldDestinationLabel = oldPicker?.GetComponentInChildren<Text>(true)?.text;
            string authoredDestination = CoreVoyageSession.DestinationPorts.FirstOrDefault(port =>
                !string.IsNullOrEmpty(oldDestinationLabel) && oldDestinationLabel.Contains(port));
            if (authoredDestination == null)
            {
                string summary = targetContent.Find("Voyage Setup/Destination Summary")?.GetComponent<Text>()?.text;
                authoredDestination = CoreVoyageSession.DestinationPorts.FirstOrDefault(port =>
                    summary != null && summary.StartsWith("到达港口：" + port, StringComparison.Ordinal));
            }

            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__DepartureDestinationStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                int chartState = reference == "UIVoyageDeparture/UIVoyageDeparture_RecordedChart.unity" ? 1 :
                    reference == "UIVoyageDeparture/UIVoyageDeparture_InvalidChart.unity" ? 2 : 0;
                if (chartState > 0) source.ConfigureEditorPreviewDepartureChart(chartState);
                else if (authoredDestination != null)
                {
                    if (source.Session.SelectDestinationPort(authoredDestination))
                    {
                        MethodInfo refresh = typeof(CoreGameplayDemoController).GetMethod("RefreshDepartureDestinationSelection",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        if (refresh == null) throw new MissingMethodException("RefreshDepartureDestinationSelection");
                        refresh.Invoke(source, null);
                    }
                }
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete destination staging preview: " + reference);
                Transform sourceRoutes = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Route Selection");
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.VoyageDeparture);
                for (int portIndex = 0; portIndex < CoreVoyageSession.DestinationPorts.Count; portIndex += 1)
                {
                    string name = "Departure Destination " + portIndex;
                    Transform sourceButton = sourceRoutes.Find(name);
                    if (sourceButton == null) throw new InvalidOperationException("Missing runtime destination button " + name);
                    PersistGeneratedSceneVisuals(sourceButton, artSpec);
                    CopyTreasureLayer(sourceButton, targetRoutes, name);
                    Button copied = targetRoutes.Find(name).GetComponent<Button>();
                    if (tutorial && !tutorialDestinationEnabled)
                    {
                        copied.interactable = false;
                        CanvasGroup group = copied.GetComponent<CanvasGroup>();
                        if (group == null) group = copied.gameObject.AddComponent<CanvasGroup>();
                        group.alpha = Mathf.Min(group.alpha, 0.42f);
                    }
                }

                for (int routeIndex = 0; routeIndex < CoreVoyageSession.Routes.Count; routeIndex += 1)
                {
                    CopyTreasureLayer(null, targetRoutes, "Destination Port " + routeIndex);
                    RectTransform generated = sourceRoutes.Find("Route " + routeIndex + "/Label") as RectTransform;
                    RectTransform authored = targetRoutes.Find("Route " + routeIndex + "/Label") as RectTransform;
                    if (generated != null && authored != null)
                    {
                        authored.sizeDelta = generated.sizeDelta;
                        authored.anchoredPosition = generated.anchoredPosition;
                    }
                }
                RectTransform generatedTitle = sourceRoutes.Find("Treasure Departure Title") as RectTransform;
                RectTransform authoredTitle = targetRoutes.Find("Treasure Departure Title") as RectTransform;
                if (generatedTitle != null && authoredTitle != null)
                    authoredTitle.anchoredPosition = generatedTitle.anchoredPosition;
                SyncPreviewCanvasScaling(source, destination);

                if (targetRoutes.Cast<Transform>().Count(item => item.name.StartsWith("Departure Destination ", StringComparison.Ordinal)) != 6 ||
                    targetRoutes.Cast<Transform>().Any(item => item.name.StartsWith("Destination Port ", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Departure port controls were not completely migrated: " + reference);
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save destination preview " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }
    }
}
