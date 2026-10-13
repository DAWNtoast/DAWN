using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        // This scoped synchronizer changes only market backgrounds and market controls.
        // The destination's port fixture, header geometry, controller and unrelated roots survive.
        public static void SyncMarketSceneFromCommandLine()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\','/');
            if (!project.StartsWith("D:/Game/tmp/", StringComparison.OrdinalIgnoreCase) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run market synchronization in a stopped isolated validation project.");
            string backup = Environment.GetEnvironmentVariable("MARKET_SCENE_SYNC_BACKUP");
            if (string.IsNullOrWhiteSpace(backup) || Directory.Exists(backup)) throw new InvalidOperationException("Use a new backup directory.");
            Directory.CreateDirectory(backup);
            string[] paths = new[] { SceneFolder + "/UI_Market.unity" };
            foreach (string path in paths)
            {
                string original = AbsolutePath(path);
                string copy = Path.Combine(backup, path);
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(original, copy);
                Scene destinationScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                CoreGameplayDemoController destination = destinationScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                Transform targetContent = destination.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                GameObject staging = null;
                try
                {
                    staging = new GameObject("__MarketSceneStaging");
                    SceneManager.MoveGameObjectToScene(staging, stagingScene);
                    var source = staging.AddComponent<CoreGameplayDemoController>();
                    CopyProvisionPreviewSettings(destination, source);
                    source.RefreshEditorPreview();
                    if (!source.HasCompleteEditorPreview) throw new InvalidOperationException("Incomplete market source: " + path);
                    if (path.Contains("UIMarket_"))
                    {
                        MethodInfo open = typeof(CoreGameplayDemoController).GetMethod("OpenMarketShop", BindingFlags.Instance | BindingFlags.NonPublic);
                        open.Invoke(source, new object[] {path.Contains("Sell") ? MarketShopKind.Shipwright : MarketShopKind.Supplies});
                        if (path.Contains("Sell"))
                        {
                            source.Session.Cargo.Wood = 40; // Preview-only sample inventory, never persisted.
                            typeof(CoreGameplayDemoController).GetMethod("StartMarketTrade", BindingFlags.Instance | BindingFlags.NonPublic)
                                .Invoke(source, new object[] {false});
                        }
                        if (path.Contains("TransactionComplete"))
                        {
                            typeof(CoreGameplayDemoController).GetField("portNotice",BindingFlags.Instance | BindingFlags.NonPublic)
                                .SetValue(source,"已购入淡水 · 编辑样本，成交后仍可继续选购。");
                            typeof(CoreGameplayDemoController).GetMethod("RefreshMarketFacilityContents",BindingFlags.Instance | BindingFlags.NonPublic)
                                .Invoke(source,null);
                        }
                    }
                    PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == CoreGameplayPreviewScreen.Market);
                    PersistGeneratedSceneVisuals(staging.transform, artSpec);
                    Transform sourceContent = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Phase Content");
                    string[] marketLayers = sourceContent.Cast<Transform>()
                        .Where(item => item.name == "Market Panel" || item.name == "Facility Backdrop Tint" || item.name.EndsWith(" Background", StringComparison.Ordinal))
                        .Select(item => item.name).ToArray();
                    if (!marketLayers.Contains("Market Panel")) throw new InvalidOperationException("Market layer missing.");
                    foreach (string layer in marketLayers)
                    {
                        int sibling = targetContent.Find(layer)?.GetSiblingIndex() ?? sourceContent.Find(layer).GetSiblingIndex();
                        CopyTreasureLayer(sourceContent.Find(layer), targetContent, layer);
                        targetContent.Find(layer).SetSiblingIndex(sibling);
                    }
                    SyncPreviewCanvasScaling(source, destination);
                    UpdateMatchedPreviewText(source.transform, destination.transform);
                    var serialized = new SerializedObject(destination);
                    serialized.FindProperty("editorPreviewPersistGeneratedUi").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    // The generated (non-editable) market preview starts with
                    // DontSave flags on every canvas object. Make this scoped
                    // market canvas persistent before serializing its new scene.
                    Transform targetCanvas = destination.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                    foreach (Transform item in targetCanvas.GetComponentsInChildren<Transform>(true))
                        item.gameObject.hideFlags = HideFlags.None;
                    PersistGeneratedSceneVisuals(destination.transform,artSpec);
                }
                finally
                {
                    if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                    if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene,true);
                    SceneManager.SetActiveScene(destinationScene);
                }
                EditorSceneManager.MarkSceneDirty(destinationScene);
                if (!EditorSceneManager.SaveScene(destinationScene,path)) throw new InvalidOperationException("Failed to save: " + path);
                RepairSavedNavigationCanvasScale(original);
                AssertSavedNavigationCanvasScale(original);
            }
            AssetDatabase.SaveAssets();
            foreach (string path in paths)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                Scene saved = EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                Transform[] nodes = saved.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                foreach (MarketShopKind shop in Enum.GetValues(typeof(MarketShopKind)))
                    if (nodes.Count(item => item.name == "Market Shop " + shop) != 1)
                        throw new InvalidOperationException("Saved shop failed reload: " + path + " / " + shop);
                bool expanded = nodes.Single(item => item.name == "Market Item Detail").gameObject.activeInHierarchy;
                if (expanded != path.Contains("UIMarket_")) throw new InvalidOperationException("Saved drawer state failed reload: " + path);
                var image = nodes.Single(item => item.name == "Market Background").GetComponent<UnityEngine.UI.RawImage>();
                if (image.texture == null || image.texture.name != "market_scene_background_v2")
                    throw new InvalidOperationException("Saved background reference failed reload: " + path);
            }
            Debug.Log("MARKET_SCENE_PREVIEW_SYNC_OK: five market scenes updated; backup=" + backup);
        }
    }
}
