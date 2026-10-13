using System;
using System.Collections.Generic;
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
        private const string StorageRoutingArtifactDirectory = "Artifacts/StorageRouting20261002";
        private const string StorageRoutingPreviewBackup = StorageRoutingArtifactDirectory + "/preview-backup";

        private static readonly string[] StorageRoutingPortReferences =
        {
            "UI_Market.unity",
            "UIMarket/UIMarket_Buy.unity",
            "UIMarket/UIMarket_Sell.unity",
            "UIMarket/UIMarket_TransactionComplete.unity",
            "UI_VoyageDeparture.unity",
            "UIVoyageDeparture/UIVoyageDeparture_RouteSelect.unity",
            "UIVoyageDeparture/UIVoyageDeparture_ShipSelect.unity",
            "UIVoyageDeparture/UIVoyageDeparture_ReadyToDepart.unity",
            "UIVoyageDeparture/UIVoyageDeparture_TreasureReady.unity",
        };

        private static readonly string[] StorageRoutingSettlementReferences =
        {
            "UI_Settlement.unity",
            "UISettlement/UISettlement_Victory.unity",
            "UISettlement/UISettlement_TreasureRecovered.unity",
            "UISettlement/UISettlement_Retreat.unity",
            "UISettlement/UISettlement_Failure.unity",
            "TutorialVoyage/07_FirstSettlement.unity",
            "TutorialVoyage/18_SecondSettlement.unity",
            "TutorialVoyage/27_ThirdSettlement.unity",
            "TutorialVoyage/28_DrawChart.unity",
        };

        public static string[] StorageRoutingReferencePaths =>
            StorageRoutingPortReferences.Concat(StorageRoutingSettlementReferences).ToArray();

        public static void SyncAndCaptureStorageRoutingFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            SyncStorageRoutingFromCommandLine();
            UIPreviewSceneAcceptance.CaptureStorageRoutingFromCommandLine();
        }

        public static void SyncStorageRoutingFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Storage routing preview synchronization requires edit mode.");
            var report = new List<string>
            {
                "Storage routing preview synchronization: " + DateTime.UtcNow.ToString("O"),
                "Scope: 4 market, 5 departure and 9 settlement pages.",
                "Backup: " + StorageRoutingPreviewBackup,
                "Settlement rewards, backgrounds, navigation, chart actions and tutorial overlays are retained.",
            };
            try
            {
                foreach (string reference in StorageRoutingPortReferences)
                {
                    SyncDepartureProvisionScene(reference, StorageRoutingPreviewBackup);
                    report.Add("Updated: " + reference);
                }
                foreach (string reference in StorageRoutingSettlementReferences)
                {
                    SyncStorageRoutingSettlementScene(reference);
                    report.Add("Updated: " + reference);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                report.Add("Complete: " + StorageRoutingReferencePaths.Length + " scenes.");
                Debug.Log("STORAGE_ROUTING_PREVIEW_SYNC: updated " + StorageRoutingReferencePaths.Length +
                    " existing pages. Backup and report: " + StorageRoutingArtifactDirectory);
            }
            catch (Exception exception)
            {
                report.Add("Failed: " + exception);
                throw;
            }
            finally
            {
                string reportPath = AbsolutePath(StorageRoutingArtifactDirectory + "/preview-sync.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllLines(reportPath, report);
            }
        }

        private static void SyncStorageRoutingSettlementScene(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored settlement preview", path);
            string backup = AbsolutePath(StorageRoutingPreviewBackup + "/" + path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.Settlement)
                throw new InvalidOperationException("Expected a settlement preview in " + path);
            Transform targetCard = destination.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "Settlement Card");

            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__StorageRoutingSettlementStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete settlement source for " + path);
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.Settlement);
                PersistGeneratedSceneVisuals(staging.transform, artSpec);
                Transform sourceCard = source.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Settlement Card");

                // Preserve the authored reward values and special drawing action.
                // Only the storage choice, its note and metric geometry changed.
                foreach (string name in new[] { "Settlement Store All Cargo", "Settlement Note" })
                {
                    Transform generated = sourceCard.Find(name);
                    if (generated == null) throw new InvalidOperationException("Missing runtime " + name + " in " + path);
                    CopyTreasureLayer(generated, targetCard, name);
                }
                foreach (string name in new[] { "Settlement Metrics", "Settlement Metrics/Settlement Distance",
                    "Settlement Metrics/Settlement Exploration", "Settlement Metrics/Settlement Events" })
                {
                    RectTransform from = sourceCard.Find(name) as RectTransform;
                    RectTransform to = targetCard.Find(name) as RectTransform;
                    if (from == null || to == null)
                        throw new InvalidOperationException("Missing settlement metric " + name + " in " + path);
                    CopyStorageRoutingRectGeometry(from, to);
                }
                if (reference.StartsWith("TutorialVoyage/", StringComparison.Ordinal))
                {
                    // Tutorial fixtures do not serialize the live tutorial state.
                    // Keep the real tutorial rule rather than suggesting supplies
                    // can be unloaded before the next guided voyage.
                    targetCard.Find("Settlement Note").GetComponent<Text>().text =
                        "教学物资保留随船；完成教学后可选择全货入库。\n确认一次即可完成结算。";
                }
                targetCard.Find("Settlement Store All Cargo").GetComponent<Button>().interactable = false;
                SyncPreviewCanvasScaling(source, destination);
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

        private static void CopyStorageRoutingRectGeometry(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.sizeDelta = source.sizeDelta;
            destination.anchoredPosition3D = source.anchoredPosition3D;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }
    }
}
