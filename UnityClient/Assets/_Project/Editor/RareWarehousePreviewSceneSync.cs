using System;
using System.IO;
using System.Linq;
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
        private const string RareWarehousePreviewBackup = "Artifacts/RareWarehouse20261003/preview-backup";
        private static readonly string[] NewRareWarehouseReferences =
        {
            "UIWarehouse/UIWarehouse_RareStorage.unity",
            "UIWarehouse/UIWarehouse_RareAdjustCargo.unity",
            "UIWarehouse/UIWarehouse_RareSellCargo.unity",
        };
        public static string[] RareWarehouseReferencePaths =>
            WarehouseTabsReferencePaths.Concat(NewRareWarehouseReferences).ToArray();

        public static void SyncAndCaptureRareWarehouseFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Rare warehouse previews require stopped edit mode.");
            // Retain authored cabinets, exhibit placements and backgrounds.
            // Cargo ledgers/dialogs use the current runtime quantity/price views;
            // collection pages only repair overlapping hints and stale thumbnails.
            foreach (string reference in WarehouseTabsReferencePaths)
            {
                SyncPortWarehouseCollectionScene(reference, RareWarehousePreviewBackup, true);
                RepairWarehouseReferenceContent(reference);
            }
            for (int state = 0; state < NewRareWarehouseReferences.Length; state++)
                SaveRareWarehousePreview(state);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UIPreviewSceneAcceptance.CaptureRareWarehouseFromCommandLine();
        }

        private static void RepairWarehouseReferenceContent(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var destination = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            Transform content = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
            Transform panel = content.Find("Warehouse Panel");
            Transform ledger = panel != null ? panel.Find("Warehouse Ledger") : null;
            if (ledger == null) throw new InvalidOperationException("Missing warehouse ledger in " + path);
            int cargoState = reference == "UIWarehouse/UIWarehouse_Storage.unity" ? 0 :
                reference == "UIWarehouse/UIWarehouse_AdjustCargo.unity" ? 1 :
                reference == "UIWarehouse/UIWarehouse_SellCargo.unity" ? 2 : -1;
            if (cargoState >= 0)
            {
                Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                GameObject staging = null;
                try
                {
                    staging = new GameObject("__WarehouseCargoReferenceRepair");
                    SceneManager.MoveGameObjectToScene(staging, stagingScene);
                    var source = staging.AddComponent<CoreGameplayDemoController>();
                    CopyProvisionPreviewSettings(destination, source);
                    source.RefreshEditorPreview();
                    // Use an existing production ship definition for this standalone
                    // layout sample, without reading/writing the player's ownership.
                    if (!source.Session.SelectShip(1)) throw new InvalidOperationException("Cargo preview requires its merchant fixture.");
                    source.ConfigureEditorPreviewWarehouseStorage(cargoState);
                    PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.Warehouse);
                    PersistGeneratedSceneVisuals(staging.transform, spec);
                    Transform sourceContent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                    CopyTreasureLayer(sourceContent.Find("Warehouse Panel/Warehouse Ledger"), panel, "Warehouse Ledger");
                    CopyTreasureLayer(sourceContent.Find("Warehouse Storage Modal"), content, "Warehouse Storage Modal");
                    Text clock = content.Find("Port Facility Header/Port Facility Time")?.GetComponent<Text>();
                    Text sourceClock = sourceContent.Find("Port Facility Header/Port Facility Time")?.GetComponent<Text>();
                    if (clock != null && sourceClock != null) clock.text = sourceClock.text;
                    SyncPreviewCanvasScaling(source, destination);
                }
                finally
                {
                    if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                    if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                    if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
                }
            }
            else
            {
                int tiles = 0;
                foreach (CoreWarehouseTreasureDefinition treasure in CoreWarehouseCollection.Catalog)
                {
                    Transform tile = ledger.Find("Warehouse Treasure " + treasure.Id);
                    if (tile == null) continue;
                    tiles++;
                    RawImage icon = tile.Find("Warehouse Inventory Art " + treasure.Id)?.GetComponent<RawImage>();
                    if (icon == null) throw new InvalidOperationException("Missing authored inventory art for " + treasure.Id);
                    ItemIconArt art = ItemIconCatalog.ForTreasure(treasure);
                    if (!art.IsAvailable) throw new InvalidOperationException("Missing keepsake icon " + treasure.Id);
                    icon.texture = art.Texture;
                    icon.uvRect = art.Uv;
                    UiImageAspect.Fit(icon, new Vector2(126f, 126f));
                }
                // The right-hand hint fits only beside a single inventory tile.
                // The selected title/source/story below remain untouched.
                Transform hint = ledger.Find("Warehouse Collection Hint");
                if (tiles != 1 && hint != null) UnityEngine.Object.DestroyImmediate(hint.gameObject);
            }
            foreach (RawImage displayed in content.GetComponentsInChildren<RawImage>(true))
            {
                if (!displayed.name.StartsWith("Warehouse Displayed Art ", StringComparison.Ordinal)) continue;
                RectTransform rect = displayed.rectTransform;
                float bottom = rect.anchoredPosition.y - rect.rect.height * rect.pivot.y;
                UiImageAspect.Fit(displayed, new Vector2(176f, 176f));
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom + rect.rect.height * rect.pivot.y);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not repair " + path);
            RepairSavedNavigationCanvasScale(AbsolutePath(path));
            AssertSavedNavigationCanvasScale(AbsolutePath(path));
        }

        private static void SaveRareWarehousePreview(int state)
        {
            string reference = NewRareWarehouseReferences[state];
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            bool exists = File.Exists(absolute);
            if (exists)
            {
                string backup = AbsolutePath(RareWarehousePreviewBackup + "/" + reference);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(absolute, backup);
            }
            Scene scene = EditorSceneManager.OpenScene(exists ? path : EditableSceneFolder + "/UI_Warehouse.unity", OpenSceneMode.Single);
            var destination = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            Transform content = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
            Transform panel = content.Find("Warehouse Panel");
            if (panel == null) throw new InvalidOperationException("Missing authored warehouse panel in " + path);
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__RareWarehousePreviewStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                var source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                source.ConfigureEditorPreviewWarehouseRare(state);
                PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.Warehouse);
                PersistGeneratedSceneVisuals(staging.transform, spec);
                Transform sourceContent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                Transform ledger = sourceContent.Find("Warehouse Panel/Warehouse Ledger");
                if (ledger == null || ledger.Find("Warehouse Rare Row gem") == null ||
                    ledger.Find("Warehouse Rare Row lantern") == null)
                    throw new InvalidOperationException("Rare warehouse sample is incomplete.");
                CopyTreasureLayer(ledger, panel, "Warehouse Ledger");
                Transform modal = sourceContent.Find("Warehouse Storage Modal");
                if (state > 0 && modal == null) throw new InvalidOperationException("Missing rare cargo dialog sample.");
                CopyTreasureLayer(modal, content, "Warehouse Storage Modal");
                SyncPreviewCanvasScaling(source, destination);
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }
    }
}
