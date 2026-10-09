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
        private sealed class FlowSceneSpec
        {
            public FlowSceneSpec(string path, string template, string source, GameplayFlowPreviewState state, params string[] layers)
            { Path = path; Template = template; Source = source; State = state; Layers = layers; }
            public readonly string Path, Template, Source;
            public readonly GameplayFlowPreviewState State;
            public readonly string[] Layers;
        }

        private static readonly FlowSceneSpec[] FlowScenes =
        {
            new FlowSceneSpec("UI_Maintenance.unity", "UI_Maintenance.unity", "UI_Maintenance",
                GameplayFlowPreviewState.None, "Port Investment Panel"),
            new FlowSceneSpec("UI_Market.unity", "UI_Market.unity", "UI_Market",
                GameplayFlowPreviewState.None, "Market Panel"),
            new FlowSceneSpec("UIMarket/UIMarket_Buy.unity", "UI_Market.unity", "UI_Market",
                GameplayFlowPreviewState.None, "Market Panel"),
            new FlowSceneSpec("UIMarket/UIMarket_Sell.unity", "UI_Market.unity", "UI_Market",
                GameplayFlowPreviewState.None, "Market Panel"),
            new FlowSceneSpec("UIMarket/UIMarket_TransactionComplete.unity", "UI_Market.unity", "UI_Market",
                GameplayFlowPreviewState.None, "Market Panel"),
            new FlowSceneSpec("UITavern/UITavern_RecruitmentOffer.unity", "UI_Tavern.unity", "UI_Tavern",
                GameplayFlowPreviewState.RecruitmentOffer, "Tavern Crew Stage", "Tavern Crew Detail"),
            new FlowSceneSpec("UITavern/UITavern_ContractSigned.unity", "UI_Tavern.unity", "UI_Tavern",
                GameplayFlowPreviewState.ContractSigned, "Tavern Crew Stage", "Tavern Crew Detail"),
            new FlowSceneSpec("UICardWorkshop/UICardWorkshop_UpgradePreview.unity", "UI_CardWorkshop.unity", "UI_CardWorkshop",
                GameplayFlowPreviewState.UpgradePreview, "Card Workshop Chest Stage", "Battle Card Upgrade Panel"),
            new FlowSceneSpec("UICardWorkshop/UICardWorkshop_UpgradeComplete.unity", "UI_CardWorkshop.unity", "UI_CardWorkshop",
                GameplayFlowPreviewState.UpgradeComplete, "Card Workshop Chest Stage", "Battle Card Upgrade Panel"),
            new FlowSceneSpec("UICardUpgrade/UICardUpgrade_UpgradePreview.unity", "UI_CardWorkshop.unity", "UI_CardWorkshop",
                GameplayFlowPreviewState.UpgradePreview, "Card Workshop Chest Stage", "Battle Card Upgrade Panel"),
            new FlowSceneSpec("UIVoyageDeparture/UIVoyageDeparture_TreasureReady.unity", "UI_VoyageDeparture.unity", "UI_VoyageDeparture",
                GameplayFlowPreviewState.TreasureDeparture, "Port Briefing", "Route Selection", "Voyage Setup"),
            new FlowSceneSpec("UINavigation/UINavigation_TreasureExploration.unity", "UI_Navigation.unity", "UI_Navigation",
                GameplayFlowPreviewState.TreasureNavigation, "Navigation Chart", "Navigation Controls", "Forward View Panel", "Navigation Journal"),
            new FlowSceneSpec("UIEncounter/UIEncounter_TreasureClue.unity", "UI_Encounter.unity", "UI_Encounter",
                GameplayFlowPreviewState.TreasureClue, "Navigation Chart", "Navigation Controls", "Forward View Panel", "Navigation Journal"),
            new FlowSceneSpec("UIEncounter/UIEncounter_TreasureCoreLocked.unity", "UI_Encounter.unity", "UI_Encounter",
                GameplayFlowPreviewState.TreasureCoreLocked, "Navigation Chart", "Navigation Controls", "Forward View Panel", "Navigation Journal"),
            new FlowSceneSpec("UIEncounter/UIEncounter_TreasureCoreReady.unity", "UI_Encounter.unity", "UI_Encounter",
                GameplayFlowPreviewState.TreasureCoreReady, "Navigation Chart", "Navigation Controls", "Forward View Panel", "Navigation Journal"),
            new FlowSceneSpec("UISettlement/UISettlement_TreasureRecovered.unity", "UI_Settlement.unity", "UI_Settlement",
                GameplayFlowPreviewState.TreasureSettlement, "Settlement Card"),
        };

        public static string[] TreasureAdventureReferencePaths => new[]
        {
            "UI_TreasureMap.unity", "UITreasureMap/UITreasureMap_MapUnopened.unity",
            "UITreasureMap/UITreasureMap_FragmentsReady.unity", "UITreasureMap/UITreasureMap_MapDecoded.unity",
            "UITreasureMap/UITreasureMap_RewardClaim.unity",
        }.Concat(FlowScenes.Select(item => item.Path)).ToArray();

        public static void SyncTreasureAdventureStatesFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Flow preview synchronization requires edit mode.");
            SyncTreasureStatesFromCommandLine();
            foreach (FlowSceneSpec flow in FlowScenes) SyncFlowScene(flow);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("TREASURE_ADVENTURE_PREVIEW_SYNC: updated " + TreasureAdventureReferencePaths.Length +
                " authored state scenes. Unrelated scene objects and pages were retained.");
        }

        public static void SyncAndCaptureTreasureAdventureFromCommandLine()
        {
            SyncTreasureAdventureStatesFromCommandLine();
            UIPreviewSceneAcceptance.CaptureTreasureAdventureFromCommandLine();
        }

        private static void SyncFlowScene(FlowSceneSpec flow)
        {
            string path = EditableSceneFolder + "/" + flow.Path;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute))
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absolute));
                File.Copy(AbsolutePath(EditableSceneFolder + "/" + flow.Template), absolute);
                AssetDatabase.ImportAsset(path);
            }
            string backup = AbsolutePath("Artifacts/TreasureAdventure20260930/preview-backup/" + path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            Transform targetContent = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__FlowStateStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.FileName == flow.Source);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                source.ConfigureEditorPreview(artSpec.Screen, artSpec.Port, artSpec.RouteIndex, true);
                source.ConfigureEditorFlowState(flow.State);
                if (!source.HasCompleteEditorPreview) throw new InvalidOperationException("Incomplete flow preview: " + flow.State);
                PersistGeneratedSceneVisuals(staging.transform, artSpec);
                Transform sourceContent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                if (sourceContent.Find("Port Facility Description") == null)
                    CopyTreasureLayer(null, targetContent, "Port Facility Description");
                SyncPreviewCanvasScaling(source, destination);
                foreach (string layer in flow.Layers)
                    CopyTreasureLayer(sourceContent.Find(layer), targetContent, layer);
                CopyTreasureLayer(sourceContent.Find("Flow Preview Sample Badge"), targetContent, "Flow Preview Sample Badge");
                UpdateMatchedPreviewText(source.transform, destination.transform);
                var serialized = new SerializedObject(destination);
                serialized.FindProperty("editorPreviewScreen").enumValueIndex = (int)source.EditorPreviewScreen;
                serialized.FindProperty("editorPreviewFlowState").enumValueIndex = (int)flow.State;
                serialized.FindProperty("editorPreviewPersistGeneratedUi").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save " + path);
        }

        private static void SyncPreviewCanvasScaling(CoreGameplayDemoController source, CoreGameplayDemoController destination)
        {
            CanvasScaler sourceScaler = source.GetComponentInChildren<CanvasScaler>(true);
            CanvasScaler destinationScaler = destination.GetComponentInChildren<CanvasScaler>(true);
            destinationScaler.uiScaleMode = sourceScaler.uiScaleMode;
            destinationScaler.referenceResolution = sourceScaler.referenceResolution;
            destinationScaler.screenMatchMode = sourceScaler.screenMatchMode;
            destinationScaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
        }

        private static void UpdateMatchedPreviewText(Transform sourceRoot, Transform destinationRoot)
        {
            foreach (Text source in sourceRoot.GetComponentsInChildren<Text>(true))
            {
                var names = new Stack<string>();
                for (Transform item = source.transform; item != sourceRoot; item = item.parent) names.Push(item.name);
                Transform destination = destinationRoot.Find(string.Join("/", names));
                Text label = destination == null ? null : destination.GetComponent<Text>();
                if (label != null) label.text = source.text;
            }
        }
    }
}
