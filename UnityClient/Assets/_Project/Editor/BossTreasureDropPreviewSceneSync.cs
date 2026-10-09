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
        public static string[] BossTreasureDropReferencePaths => new[]
        {
            "UI_TreasureMap.unity", "UITreasureMap/UITreasureMap_MapUnopened.unity",
            "UITreasureMap/UITreasureMap_FragmentsReady.unity", "UITreasureMap/UITreasureMap_MapDecoded.unity",
            "UITreasureMap/UITreasureMap_RewardClaim.unity",
            "TutorialVoyage/26_ThirdReturn.unity", "TutorialVoyage/27_ThirdSettlement.unity",
            "TutorialVoyage/28_DrawChart.unity",
        };

        public static void SyncAndCaptureBossTreasureDropsFromCommandLine()
        {
            SyncBossTreasureDropsFromCommandLine();
            UIPreviewSceneAcceptance.CaptureBossTreasureDropsFromCommandLine();
        }

        public static void SyncBossTreasureDropsFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Boss reward previews require edit mode.");
            foreach (string reference in BossTreasureDropReferencePaths) SyncBossTreasureDropScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BOSS_TREASURE_DROP_PREVIEW_SYNC: updated " + BossTreasureDropReferencePaths.Length +
                " authored treasure/tutorial pages; unrelated art, rewards and controls retained.");
        }

        private static void SyncBossTreasureDropScene(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            string backup = AbsolutePath("Artifacts/BossTreasureDrops20260930/preview-backup/" + path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (reference.EndsWith("26_ThirdReturn.unity", StringComparison.Ordinal))
            {
                Text log = destination.GetComponentsInChildren<Text>(true).Single(item => item.name == "Voyage Log");
                const string drop = "新手首领必定掉落：藏宝图碎片 +1（返航后入账）";
                if (!log.text.Contains(drop)) log.text = drop + "\n" + log.text;
                log.rectTransform.sizeDelta = new Vector2(log.rectTransform.sizeDelta.x,
                    Mathf.Max(106f, log.preferredHeight + 18f));
            }
            else
            {
                Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                GameObject staging = null;
                try
                {
                    staging = new GameObject("__BossTreasureDropStaging");
                    SceneManager.MoveGameObjectToScene(staging, stagingScene);
                    CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                    CopyProvisionPreviewSettings(destination, source);
                    source.RefreshEditorPreview();
                    if (!source.HasCompleteEditorPreview)
                        throw new InvalidOperationException("Incomplete boss reward preview source: " + path);
                    if (destination.EditorPreviewScreen == CoreGameplayPreviewScreen.TreasureMap)
                    {
                        // Preserve every saved route selection, map and reward receipt.
                        // Only changed instructional labels need to follow runtime.
                        foreach (string name in new[] { "Treasure Workbench Notice", "Treasure Map Details" })
                        {
                            Text from = source.GetComponentsInChildren<Text>(true).SingleOrDefault(item => item.name == name);
                            Text to = destination.GetComponentsInChildren<Text>(true).SingleOrDefault(item => item.name == name);
                            if (from != null && to != null) to.text = from.text;
                        }
                    }
                    else if (destination.EditorPreviewScreen == CoreGameplayPreviewScreen.Settlement)
                    {
                        // This is an explicitly marked art sample. Construct only
                        // its one-fragment manifest with the real settlement UI;
                        // never grant rewards to a session or player's save.
                        UnityEngine.Object.DestroyImmediate(source.GetComponentsInChildren<Transform>(true)
                            .Single(item => item.name == "Settlement Card").gameObject);
                        var sample = new VoyageSettlement { BankedFragments = 1, Outcome = VoyageOutcome.SafeReturn };
                        typeof(CoreGameplayDemoController).GetMethod("BuildSettlementPresentation",
                            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { sample, true });
                        PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.Settlement);
                        PersistGeneratedSceneVisuals(staging.transform, artSpec);
                        Transform manifest = source.GetComponentsInChildren<Transform>(true)
                            .Single(item => item.name == "Settlement Treasure Manifest");
                        Transform hero = destination.GetComponentsInChildren<Transform>(true)
                            .Single(item => item.name == "Settlement Hero");
                        CopyTreasureLayer(manifest, hero, "Settlement Treasure Manifest");
                        destination.GetComponentsInChildren<Text>(true).Single(item => item.name == "Settlement Badge").text =
                            "新手引导 · 首领战利品 / 演示数据，不写入存档";
                    }
                    else throw new InvalidOperationException("Unexpected boss reward preview: " + path);
                }
                finally
                {
                    if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                    if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                    if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
                }
            }
            if (reference.StartsWith("TutorialVoyage/", StringComparison.Ordinal))
                SyncBossTutorialRewardOverlay(destination, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Failed to save " + path);
            // Overlay canvas scale is driven to zero by batch edit-mode saves.
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }

        private static void SyncBossTutorialRewardOverlay(CoreGameplayDemoController destination, Scene scene)
        {
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__BossTutorialRewardOverlayStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                bool sailing = destination.EditorPreviewScreen == CoreGameplayPreviewScreen.Navigation;
                string parentName = sailing ? "Forward View Panel" : "Settlement Hero";
                Transform parent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == parentName);
                typeof(CoreGameplayDemoController).GetMethod("BuildGuidedTutorialRewardPlaque",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source,
                        new object[] { parent, sailing ? new Vector2(0, -164) : new Vector2(0, -55) });
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == destination.EditorPreviewScreen);
                PersistGeneratedSceneVisuals(staging.transform, artSpec);
                // Include the actual runtime teaching overlay in these three
                // saved examples, so subsequent visual reviews cannot miss it.
                foreach (Transform previous in destination.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name == "Guided Tutorial Component Reward").ToArray())
                    UnityEngine.Object.DestroyImmediate(previous.gameObject);
                Transform target = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == parentName);
                CopyTreasureLayer(parent.Find("Guided Tutorial Component Reward"), target, "Guided Tutorial Component Reward");
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
        }
    }
}
