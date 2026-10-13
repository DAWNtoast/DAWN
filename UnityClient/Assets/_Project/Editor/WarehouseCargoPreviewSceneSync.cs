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
        public static readonly string[] WarehouseCargoReferencePaths =
        {
            "UIWarehouse/UIWarehouse_Storage.unity",
            "UIWarehouse/UIWarehouse_AdjustCargo.unity",
            "UIWarehouse/UIWarehouse_SellCargo.unity",
        };

        public static void SyncAndCaptureWarehouseCargoFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Warehouse cargo previews require edit mode.");
            for (int state = 0; state < WarehouseCargoReferencePaths.Length; state++)
                SaveWarehouseCargoPreview(state);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("WAREHOUSE_CARGO_PREVIEW_SYNC: saved 3 warehouse cargo pages; original collection scenes retained.");
            UIPreviewSceneAcceptance.CaptureWarehouseCargoFromCommandLine();
        }

        private static void SaveWarehouseCargoPreview(int state)
        {
            string path = EditableSceneFolder + "/" + WarehouseCargoReferencePaths[state];
            string absolute = AbsolutePath(path);
            if (File.Exists(absolute))
            {
                string backup = AbsolutePath("Artifacts/StorageRouting20261002/warehouse-preview-backup/" + WarehouseCargoReferencePaths[state]);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(absolute, backup);
            }
            Scene scene = EditorSceneManager.OpenScene(EditableSceneFolder + "/UI_Warehouse.unity", OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            Transform targetContent = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
            Transform targetPanel = targetContent.Find("Warehouse Panel");
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__WarehouseCargoStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                source.ConfigureEditorPreviewWarehouseStorage(state);
                PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.Warehouse);
                PersistGeneratedSceneVisuals(staging.transform, spec);
                Transform sourceContent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                Transform ledger = sourceContent.Find("Warehouse Panel/Warehouse Ledger");
                if (ledger == null || ledger.Find("Warehouse Storage Row wood") == null)
                    throw new InvalidOperationException("Warehouse cargo preview has no inventory rows.");
                CopyTreasureLayer(ledger, targetPanel, "Warehouse Ledger");
                Transform modal = sourceContent.Find("Warehouse Storage Modal");
                if (state > 0 && modal == null) throw new InvalidOperationException("Missing warehouse cargo dialog.");
                CopyTreasureLayer(modal, targetContent, "Warehouse Storage Modal");
                SyncPreviewCanvasScaling(source, destination);
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }
    }
}
