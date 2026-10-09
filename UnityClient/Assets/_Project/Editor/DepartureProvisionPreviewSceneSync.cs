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
        // Scope stays explicit: these are the existing market/departure art pages,
        // not the unrelated workshop, port, tutorial or treasure event scenes.
        public static string[] DepartureProvisionReferencePaths => new[]
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
            "UI_Maintenance.unity",
            "UIMaintenance/UIMaintenance_RepairAvailable.unity",
            "UIMaintenance/UIMaintenance_RepairComplete.unity",
            "UIMaintenance/UIMaintenance_InsufficientMaterials.unity",
        };

        private static readonly string[] DepartureProvisionControls =
        {
            "Cargo", "Adjust Voyage Supplies", "Configure Voyage Battle Decks",
            "Configure Voyage Crew", "Start Voyage", "Departure Provision Quote",
            "Departure Buy Provisions",
            "Upgrade Part 0", "Upgrade Part 1", "Upgrade Part 2", "Upgrade Part 3", "Upgrade Part 4",
            "Upgrade Name 0", "Upgrade Name 1", "Upgrade Name 2", "Upgrade Name 3", "Upgrade Name 4",
        };

        public static void SyncDepartureProvisionStatesFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Departure provisioning previews require edit mode.");
            foreach (string reference in DepartureProvisionReferencePaths)
                SyncDepartureProvisionScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DEPARTURE_PROVISION_PREVIEW_SYNC: updated " + DepartureProvisionReferencePaths.Length +
                " existing market/departure scenes; authored backgrounds and selection samples retained.");
        }

        public static void SyncAndCaptureDepartureProvisionsFromCommandLine()
        {
            SyncDepartureProvisionStatesFromCommandLine();
            UIPreviewSceneAcceptance.CaptureDepartureProvisionsFromCommandLine();
        }

        public static void SyncAndCaptureTradeEconomyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Trade economy previews require edit mode.");
            foreach (string reference in DepartureProvisionReferencePaths)
                SyncDepartureProvisionScene(reference, "Artifacts/TradeEconomy20261001/preview-backup");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("TRADE_ECONOMY_PREVIEW_SYNC: updated " + DepartureProvisionReferencePaths.Length +
                " existing market/departure/investment scenes; authored samples retained.");
            UIPreviewSceneAcceptance.CaptureTradeEconomyFromCommandLine();
        }

        public static string[] RareInvestmentReferencePaths => DepartureProvisionReferencePaths
            .Where(path => path.Contains("Maintenance")).ToArray();

        public static void SyncAndCaptureRareInvestmentFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Investment previews require stopped edit mode.");
            foreach (string reference in RareInvestmentReferencePaths)
                SyncDepartureProvisionScene(reference, "Artifacts/RareConsumption20261003/preview-backup");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UIPreviewSceneAcceptance.CaptureRareInvestmentFromCommandLine();
        }

        private static void SyncDepartureProvisionScene(string reference,
            string backupDirectory = "Artifacts/DepartureProvisions20260930/preview-backup")
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored provisioning preview", path);
            string backup = AbsolutePath(backupDirectory + "/" + path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            bool market = destination.EditorPreviewScreen == CoreGameplayPreviewScreen.Market;
            bool investment = destination.EditorPreviewScreen == CoreGameplayPreviewScreen.Maintenance;
            if (!market && !investment && destination.EditorPreviewScreen != CoreGameplayPreviewScreen.VoyageDeparture)
                throw new InvalidOperationException("Unexpected preview screen in " + path);
            Transform targetContent = destination.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "Phase Content");
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__DepartureProvisionStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                // Copy serialized editor fixture choices, including TreasureDeparture.
                // Do not reset every scene to the default ship, port or ordinary voyage.
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete provisioning source for " + path);
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == source.EditorPreviewScreen);
                PersistGeneratedSceneVisuals(staging.transform, artSpec);
                Transform sourceContent = source.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Phase Content");
                SyncPreviewCanvasScaling(source, destination);
                // Date-sensitive quotes must not sit under a clock frozen on an
                // older capture day. Preserve the authored header's layout/art.
                Text targetClock = targetContent.Find("Port Facility Header/Port Facility Time")?.GetComponent<Text>();
                Text sourceClock = sourceContent.Find("Port Facility Header/Port Facility Time")?.GetComponent<Text>();
                if (targetClock != null && sourceClock != null) targetClock.text = sourceClock.text;
                if (market || investment)
                {
                    string panelName = market ? "Market Panel" : "Port Investment Panel";
                    CopyTreasureLayer(sourceContent.Find(panelName), targetContent, panelName);
                }
                else
                {
                    Transform targetSetup = targetContent.Find("Voyage Setup");
                    Transform sourceSetup = sourceContent.Find("Voyage Setup");
                    if (targetSetup == null || sourceSetup == null)
                        throw new InvalidOperationException("Missing Voyage Setup in " + path);
                    // Keep authored ship/crew selection controls, art and route cards.
                    // A saved crew editor remains a crew editor; its return button leads
                    // back to the setup, where provisioning is available in the game.
                    if (targetSetup.Find("Departure Crew Configuration Title") == null)
                    {
                        if (sourceSetup.Find("Departure Buy Provisions") == null ||
                            sourceSetup.Find("Departure Provision Quote") == null)
                            throw new InvalidOperationException("Runtime provisioning controls are missing in " + path);
                        targetSetup.GetComponent<RectTransform>().sizeDelta = sourceSetup.GetComponent<RectTransform>().sizeDelta;
                        foreach (string control in DepartureProvisionControls)
                            CopyTreasureLayer(sourceSetup.Find(control), targetSetup, control);
                        // Keep the modern editable field and its narrow history
                        // selector when this older provisioning command is rerun.
                        CopyDepartureChartControls(sourceSetup, targetSetup);
                    }
                }
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

        private static void CopyProvisionPreviewSettings(CoreGameplayDemoController destination,
            CoreGameplayDemoController source)
        {
            var original = new SerializedObject(destination);
            var generated = new SerializedObject(source);
            SerializedProperty property = original.GetIterator();
            while (property.NextVisible(true))
            {
                if (!property.name.StartsWith("editorPreview", StringComparison.Ordinal)) continue;
                generated.CopyFromSerializedProperty(property);
            }
            generated.FindProperty("editorPreviewPersistGeneratedUi").boolValue = true;
            generated.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
