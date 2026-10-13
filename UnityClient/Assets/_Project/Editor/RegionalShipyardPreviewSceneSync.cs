using System;
using System.IO;
using System.Linq;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        private static readonly string[] ExistingRegionalShipyardReferences =
        {
            "UI_Shipyard.unity", "UI_ShipyardDetail.unity",
            "UIShipyard/UIShipyard_FleetBrowse.unity", "UIShipyard/UIShipyard_OwnedShip.unity",
            "UIShipyard/UIShipyard_ShipHover.unity",
            "UIShipyardDetail/UIShipyardDetail_Performance.unity", "UIShipyardDetail/UIShipyardDetail_History.unity",
            "UIShipyardDetail/UIShipyardDetail_ComponentLoadout.unity", "UIShipyardDetail/UIShipyardDetail_PurchaseConfirm.unity",
            "TutorialVoyage/33_Shipyard.unity", "TutorialVoyage/37_EquipRewardComponent.unity",
        };

        public static string[] RegionalShipyardReferencePaths => ExistingRegionalShipyardReferences.Concat(
            Enumerable.Range(0, 6).SelectMany(port => new[] {
                "UIShipyard/UIShipyard_Port" + port + ".unity",
                "UIShipyardDetail/UIShipyardDetail_Port" + port + ".unity" })).ToArray();

        public static void SyncAndCaptureRegionalShipyardsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Regional shipyard previews require stopped edit mode.");
            foreach (string reference in ExistingRegionalShipyardReferences) SyncRegionalShipyard(reference, -1, -1);
            int[] samples = { 4, 6, 7, 8, 10, 11 };
            for (int port = 0; port < 6; port++)
                foreach (bool detail in new[] { false, true })
                    SyncRegionalShipyard((detail ? "UIShipyardDetail/UIShipyardDetail_Port" : "UIShipyard/UIShipyard_Port") + port + ".unity", port, samples[port]);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UIPreviewSceneAcceptance.CaptureRegionalShipyardsFromCommandLine();
        }

        private static void SyncRegionalShipyard(string reference, int port, int ship)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            bool exists = File.Exists(absolute);
            if (exists)
            {
                string backup = AbsolutePath("Artifacts/RegionalShips20261002/preview-backup/" + reference);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(absolute, backup);
            }
            bool detail = reference.Contains("ShipyardDetail") || reference.Contains("37_Equip");
            bool tutorialEquipment = reference.Contains("37_EquipRewardComponent");
            string template = exists ? path : EditableSceneFolder + (detail ? "/UI_ShipyardDetail.unity" : "/UI_Shipyard.unity");
            Scene scene = EditorSceneManager.OpenScene(template, OpenSceneMode.Single);
            var destination = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            Transform targetContent = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__RegionalShipyardStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                var source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (tutorialEquipment) source.ConfigureEditorShipyard(0, true, 2);
                if (port >= 0)
                {
                    source.ConfigureEditorPreview(detail ? CoreGameplayPreviewScreen.ShipyardDetail : CoreGameplayPreviewScreen.Shipyard,
                        (CoreGameplayPreviewPort)port, 0, true);
                    source.ConfigureEditorShipyard(ship, detail);
                }
                if (!source.HasCompleteEditorPreview) throw new InvalidOperationException("Incomplete shipyard: " + reference);
                Transform sourceContent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                Transform panel = sourceContent.Find("Shipyard Panel");
                if (panel == null) throw new InvalidOperationException("No shipyard panel: " + reference);
                PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == source.EditorPreviewScreen);
                PersistGeneratedSceneVisuals(panel, spec);
                CopyTreasureLayer(panel, targetContent, "Shipyard Panel");
                if (tutorialEquipment)
                {
                    var tutorialSettings = new SerializedObject(destination);
                    tutorialSettings.FindProperty("editorPreviewShipIndex").intValue = 0;
                    tutorialSettings.FindProperty("editorPreviewShipPage").intValue = 2;
                    tutorialSettings.ApplyModifiedPropertiesWithoutUndo();
                }
                if (port >= 0)
                {
                    // New samples use the matching port header and retain the authored berth background.
                    CopyTreasureLayer(sourceContent.Find("Port Facility Header"), targetContent, "Port Facility Header");
                    var serialized = new SerializedObject(destination);
                    serialized.FindProperty("editorPreviewPort").enumValueIndex = port;
                    serialized.FindProperty("editorPreviewShipIndex").intValue = ship;
                    serialized.FindProperty("editorPreviewShipPage").intValue = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PersistGeneratedSceneVisuals(destination.transform, spec);
                }
                SyncPreviewCanvasScaling(source, destination);
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }
    }
}
