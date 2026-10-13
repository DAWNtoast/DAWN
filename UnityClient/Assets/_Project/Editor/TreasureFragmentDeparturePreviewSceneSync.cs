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
        public static void SyncTreasureFragmentDepartureStatesFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Treasure departure synchronization requires stopped edit mode.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save the modified open scenes before synchronizing treasure departure previews.");

            string[] references = ChartSeedReferencePaths;
            foreach (string reference in references)
            {
                string absolute = AbsolutePath(EditableSceneFolder + "/" + reference);
                if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored departure preview", absolute);
                string backup = AbsolutePath("Artifacts/TreasureFragments20261010/preview-backup/" + reference);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(absolute, backup);
            }
            foreach (string reference in references) SyncTreasureFragmentDepartureScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("TREASURE_FRAGMENT_DEPARTURE_PREVIEW_SYNC: updated " + references.Length +
                " authored departure previews; preserved route, destination, crew, ship and provision layouts and tutorial gates.");
        }

        private static void SyncTreasureFragmentDepartureScene(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            string before = File.ReadAllText(absolute);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.VoyageDeparture ||
                !destination.HasCompleteEditorPreview)
                throw new InvalidOperationException("Expected a complete authored departure preview in " + path);
            Transform targetContent = destination.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "Phase Content");
            Transform targetRoutes = targetContent.Find("Route Selection");
            Transform targetSetup = targetContent.Find("Voyage Setup");
            if (targetRoutes == null || targetSetup == null)
                throw new InvalidOperationException("Missing authored departure panels in " + path);
            bool tutorial = reference.StartsWith("TutorialVoyage/", StringComparison.Ordinal);

            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__TreasureFragmentDepartureStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete treasure departure staging preview in " + path);
                Transform sourceContent = source.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Phase Content");
                Transform sourceRoutes = sourceContent.Find("Route Selection");
                Transform sourceSetup = sourceContent.Find("Voyage Setup");
                const string pickerName = "Departure Treasure Seed Picker";
                RectTransform generatedPicker = sourceRoutes?.Find(pickerName) as RectTransform;
                if (generatedPicker == null || sourceSetup == null)
                    throw new InvalidOperationException("Missing runtime treasure seed selection in " + path);

                // Occupy the authored right-hand seed row. Repeat runs retain any
                // placement edits made to the new picker itself, without relaying
                // out the ordinary seed switch, route panel or ship controls.
                RectTransform previousPicker = (targetRoutes.Find(pickerName) ??
                    targetRoutes.Find("Seed Locked Hint") ?? targetRoutes.Find("Treasure Seed")) as RectTransform;
                int sibling = previousPicker != null ? previousPicker.GetSiblingIndex() : targetRoutes.childCount;
                if (previousPicker != null) CopyStorageRoutingRectGeometry(previousPicker, generatedPicker);
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.VoyageDeparture);
                PersistGeneratedSceneVisuals(generatedPicker, artSpec);
                CopyTreasureLayer(generatedPicker, targetRoutes, pickerName);
                Transform copiedPicker = targetRoutes.Find(pickerName);
                copiedPicker.SetSiblingIndex(Mathf.Min(sibling, targetRoutes.childCount - 1));
                if (tutorial)
                {
                    copiedPicker.GetComponent<Button>().interactable = false;
                    copiedPicker.GetComponentInChildren<Text>(true).text = "藏宝海图种子 · 教学结束后可选";
                }
                foreach (string obsolete in new[] { "Seed Locked Hint", "Treasure Seed" })
                {
                    CopyTreasureLayer(null, targetRoutes, obsolete);
                    CopyTreasureLayer(null, targetSetup, obsolete);
                }

                Text ordinaryLabel = targetRoutes.Find("Use Chart Seed/Seed Label")?.GetComponent<Text>();
                if (ordinaryLabel != null)
                {
                    Text generatedLabel = sourceRoutes.Find("Use Chart Seed/Seed Label")?.GetComponent<Text>();
                    if (generatedLabel == null) throw new InvalidOperationException("Missing ordinary chart label in " + path);
                    ordinaryLabel.text = tutorial ? "普通海图 · 教学指定航程" : generatedLabel.text;
                }
                CopyTreasureDeparturePreviewText(sourceRoutes, targetRoutes, "Seed Label");

                RawImage authoredArt = targetRoutes.Find("Treasure Departure Art")?.GetComponent<RawImage>();
                if (authoredArt != null)
                {
                    RawImage generatedArt = sourceRoutes.Find("Treasure Departure Art")?.GetComponent<RawImage>();
                    if (generatedArt == null || generatedArt.texture == null)
                        throw new InvalidOperationException("Missing treasure departure chart artwork in " + path);
                    PersistGeneratedSceneVisuals(generatedArt.transform, artSpec);
                    authoredArt.texture = generatedArt.texture;
                    foreach (string control in new[] {
                        "Treasure Departure Title", "Treasure Departure Detail", "Treasure Departure Consumption",
                        "Cancel Treasure Voyage/Label", "Return To Treasure Table/Label",
                    }) CopyTreasureDeparturePreviewText(sourceRoutes, targetRoutes, control);
                    CopyTreasureDeparturePreviewText(sourceSetup, targetSetup, "Start Voyage/Label");
                }
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            if (File.ReadAllText(absolute) != before)
                throw new IOException("Departure scene changed during treasure preview synchronization: " + path);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("Failed to save treasure departure preview " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }

        private static void CopyTreasureDeparturePreviewText(Transform source, Transform target, string path)
        {
            Text authored = target.Find(path)?.GetComponent<Text>();
            if (authored == null) return;
            Text generated = source.Find(path)?.GetComponent<Text>();
            if (generated == null) throw new InvalidOperationException("Missing runtime treasure departure text " + path);
            authored.text = generated.text;
        }
    }
}
