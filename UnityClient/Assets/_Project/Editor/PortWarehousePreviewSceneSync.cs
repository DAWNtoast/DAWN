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
        private const string PortWarehousePreviewBackup = "Artifacts/PortInvestmentWarehouse20261002/preview-backup";
        private const string WarehouseTabsPreviewBackup = "Artifacts/WarehouseTabs20261002/preview-backup";

        private static readonly string[] PortWarehouseCollectionReferences =
        {
            "UI_Warehouse.unity",
            "UIWarehouse/UIWarehouse_EmptySlots.unity",
            "UIWarehouse/UIWarehouse_CollectionDetail.unity",
            "UIWarehouse/UIWarehouse_MultipleCollectibles.unity",
            "UIWarehouse/UIWarehouse_Storage.unity",
            "UIWarehouse/UIWarehouse_AdjustCargo.unity",
            "UIWarehouse/UIWarehouse_SellCargo.unity",
            "TutorialVoyage/29_Warehouse.unity",
            "TutorialVoyage/30_SelectTreasure.unity",
            "TutorialVoyage/31_DisplayTreasure.unity",
        };

        public static string[] PortWarehouseReferencePaths =>
            DepartureProvisionReferencePaths.Concat(PortWarehouseCollectionReferences).ToArray();

        public static string[] WarehouseTabsReferencePaths => PortWarehouseCollectionReferences.ToArray();

        public static void SyncWarehouseTabsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Warehouse tab previews require edit mode.");
            foreach (string reference in WarehouseTabsReferencePaths)
                SyncPortWarehouseCollectionScene(reference, WarehouseTabsPreviewBackup, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("WAREHOUSE_TABS_PREVIEW_SYNC: updated " + WarehouseTabsReferencePaths.Length +
                " authored collection pages to exhibits/basic cargo/rare cargo; inventory rows, dialogs, exhibits and tutorial overlays retained.");
        }

        public static void SyncAndCaptureWarehouseTabsFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            SyncWarehouseTabsFromCommandLine();
            UIPreviewSceneAcceptance.CaptureWarehouseTabsFromCommandLine();
        }

        /// <summary>
        /// Synchronize only the existing market, departure, investment and
        /// collection pages affected by port-local warehouses. Capture the saved
        /// results into the directory supplied by -navalVisualCapturePath.
        /// </summary>
        public static void SyncAndCapturePortWarehouseFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Port warehouse preview synchronization requires edit mode.");
            foreach (string reference in DepartureProvisionReferencePaths)
                SyncDepartureProvisionScene(reference, PortWarehousePreviewBackup);
            foreach (string reference in PortWarehouseCollectionReferences)
                SyncPortWarehouseCollectionScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("PORT_WAREHOUSE_PREVIEW_SYNC: updated " + PortWarehouseReferencePaths.Length +
                " existing pages; collection samples, cabinet art and authored backgrounds retained. Backup: " + PortWarehousePreviewBackup);
            UIPreviewSceneAcceptance.CapturePortWarehouseFromCommandLine();
        }

        private static void SyncPortWarehouseCollectionScene(string reference,
            string backupDirectory = PortWarehousePreviewBackup, bool tabsOnly = false)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored collection preview", path);
            string backup = AbsolutePath(backupDirectory + "/" + path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.Warehouse)
                throw new InvalidOperationException("Expected the authored collection screen in " + path);
            Transform targetContent = destination.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "Phase Content");
            Transform targetPanel = targetContent.Find("Warehouse Panel");
            Transform targetLedger = targetPanel != null ? targetPanel.Find("Warehouse Ledger") : null;
            if (targetLedger == null) throw new InvalidOperationException("Missing authored collection ledger in " + path);

            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__PortWarehouseStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete warehouse source for " + path);
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.Warehouse);
                Transform sourceContent = source.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Phase Content");
                Transform sourceLedger = sourceContent.Find("Warehouse Panel/Warehouse Ledger");
                if (sourceLedger == null) throw new InvalidOperationException("Missing runtime collection ledger in " + path);

                // These old authored scenes do not serialize collection samples,
                // selected keepsakes or their display slots. Replacing the whole
                // panel would silently replace every tutorial/art sample with the
                // default catalog. Only the navigation tabs are transplanted.
                bool storageSelected = targetLedger.Find("Warehouse Storage Summary") != null;
                bool rareSelected = targetLedger.Find("Warehouse Rare Summary") != null;
                // Apply selection before sprites become serialized copies: the
                // runtime skin checks its original face sprite by identity.
                WoodButtonSkin.SetSelected(sourceLedger.Find("Warehouse Tab Valuables").GetComponent<Button>(), !storageSelected && !rareSelected);
                WoodButtonSkin.SetSelected(sourceLedger.Find("Warehouse Tab Storage").GetComponent<Button>(), storageSelected);
                WoodButtonSkin.SetSelected(sourceLedger.Find("Warehouse Tab Rare").GetComponent<Button>(), rareSelected);
                PersistGeneratedSceneVisuals(staging.transform, artSpec);
                bool ordinaryTabsEnabled = targetLedger.Find("Warehouse Tab Storage")?.GetComponent<Button>()?.interactable ??
                    targetLedger.Find("Warehouse Tab Valuables")?.GetComponent<Button>()?.interactable ?? true;
                RemoveObsoleteWarehouseBasicControls(targetLedger);
                foreach (string tabName in new[] { "Warehouse Tab Valuables", "Warehouse Tab Storage", "Warehouse Tab Rare" })
                {
                    Transform generatedTab = sourceLedger.Find(tabName);
                    if (generatedTab == null) throw new InvalidOperationException("Missing runtime tab " + tabName);
                    Button oldTab = targetLedger.Find(tabName)?.GetComponent<Button>();
                    bool interactable = oldTab != null ? oldTab.interactable : ordinaryTabsEnabled;
                    if (tabName == "Warehouse Tab Rare" && reference.StartsWith("TutorialVoyage/", StringComparison.Ordinal))
                        interactable = false;
                    CopyTreasureLayer(generatedTab, targetLedger, tabName);
                    Button replacement = targetLedger.Find(tabName).GetComponent<Button>();
                    replacement.interactable = interactable;
                    RectTransform rect = replacement.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(196f, 52f);
                    rect.anchoredPosition = new Vector2(tabName == "Warehouse Tab Valuables" ? -208f : tabName == "Warehouse Tab Storage" ? 0f : 208f, 320f);
                    WoodButtonSkin.SetSelected(replacement, tabName == "Warehouse Tab Storage" ? storageSelected :
                        tabName == "Warehouse Tab Rare" ? rareSelected : !storageSelected && !rareSelected);
                }

                // Change ownership wording while retaining authored item counts,
                // empty slots, selected details, fonts/art outside this one label.
                Text count = targetLedger.Find("Warehouse Collection Count")?.GetComponent<Text>();
                Text countStyle = sourceLedger.Find("Warehouse Collection Count")?.GetComponent<Text>();
                if (!tabsOnly && count != null)
                {
                    int marker = count.text.IndexOf("珍藏", StringComparison.Ordinal);
                    if (marker < 0) throw new InvalidOperationException("Cannot preserve unknown authored collection count in " + path);
                    count.text = source.Session.CurrentPort + count.text.Substring(marker);
                    if (countStyle != null)
                    {
                        count.fontSize = countStyle.fontSize;
                        count.resizeTextForBestFit = countStyle.resizeTextForBestFit;
                        count.rectTransform.sizeDelta = countStyle.rectTransform.sizeDelta;
                    }
                }
                Text empty = targetLedger.Find("Warehouse Empty Collection")?.GetComponent<Text>();
                if (!tabsOnly && empty != null)
                    empty.text = source.Session.CurrentPort + "的珍藏室尚无藏品\n\n各港分别收藏与陈列，其他港的藏品保留在原港。";
                Text targetClock = targetContent.Find("Port Facility Header/Port Facility Time")?.GetComponent<Text>();
                Text sourceClock = sourceContent.Find("Port Facility Header/Port Facility Time")?.GetComponent<Text>();
                if (!tabsOnly && targetClock != null && sourceClock != null) targetClock.text = sourceClock.text;
                SyncPreviewCanvasScaling(source, destination);

                // In particular, do not copy Warehouse Background, Cabinet Art,
                // Display Cabinet, treasure tiles, descriptions or tutorial guide.
                foreach (string tabName in new[] { "Warehouse Tab Valuables", "Warehouse Tab Storage", "Warehouse Tab Rare" })
                    if (targetLedger.Find(tabName) == null)
                        throw new InvalidOperationException("Warehouse tab synchronization failed: " + path);
                if (targetLedger.Cast<Transform>().Any(item => IsObsoleteWarehouseBasicControl(item.name)))
                    throw new InvalidOperationException("Obsolete basic-items controls remain in " + path);
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }

        private static void RemoveObsoleteWarehouseBasicControls(Transform ledger)
        {
            for (int index = ledger.childCount - 1; index >= 0; index--)
            {
                Transform child = ledger.GetChild(index);
                if (IsObsoleteWarehouseBasicControl(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static bool IsObsoleteWarehouseBasicControl(string name)
        {
            return name == "Warehouse Tab Basics" || name == "Warehouse Summary" ||
                name == "Warehouse Sale Hint" || name == "Warehouse Deposit Travel Cash" ||
                name == "Warehouse Restock" || name == "Warehouse Basic Safety" ||
                name.StartsWith("Warehouse Sell ", StringComparison.Ordinal) ||
                name.StartsWith("Warehouse Item ", StringComparison.Ordinal);
        }
    }
}
