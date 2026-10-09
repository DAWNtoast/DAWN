using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        public static void SyncAndCaptureCurseRiskFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Risk preview synchronization requires stopped edit mode.");
            string folder = AbsolutePath(EditableSceneFolder);
            string[] references = Directory.GetFiles(folder, "*.unity", SearchOption.AllDirectories)
                .Where(path =>
                {
                    string scene = File.ReadAllText(path).Replace("\r\n", "\n");
                    return scene.Contains("m_Name: Warehouse Panel\n") ||
                        scene.Contains("m_Name: Treasure Workbench\n") || scene.Contains("m_Name: Battle Card\n");
                }).Select(path => path.Substring(folder.Length + 1).Replace('\\', '/'))
                .Concat(MonthlyDispatchReferences).Distinct().ToArray();
            foreach (string reference in references) SyncCurseRiskScene(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CURSE_RISK_PREVIEW_SYNC: " + references.Length + " authored scenes updated.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-navalVisualCapturePath");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Provide a fresh capture path.");
            MethodInfo capture = typeof(UIPreviewSceneAcceptance).GetMethod("Capture",
                BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string), typeof(string[]) }, null);
            string[] samples = { "UI_Warehouse.unity", "UIWarehouse/UIWarehouse_RareStorage.unity",
                "UI_TreasureMap.unity", "UI_Battle.unity", "UIBattle/UIBattle_BoardingRam.unity",
                "UI_DispatchDeployment.unity", "UI_DispatchDeployment_Escort.unity", "UI_DispatchDeployment_Survey.unity" };
            Debug.Log("CURSE_RISK_CAPTURE: " + capture.Invoke(null, new object[] { args[index + 1], EditableSceneFolder, samples }));
        }

        private static void SyncCurseRiskScene(string reference)
        {
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            string backup = AbsolutePath("Artifacts/CurseRisk20261003/preview-backup/" + path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController target = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__CurseRiskPreviewStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(target, source);
                source.RefreshEditorPreview();
                if (!source.HasCompleteEditorPreview) throw new InvalidOperationException("Incomplete sample: " + path);
                SyncMonthlyHeader(source, target);
                PreviewSceneSpec spec = Array.Find(Specs, item => item.Screen == target.EditorPreviewScreen);
                // Only copy the new service or changed workbench controls. Preserve
                // authored furniture, item samples, battle poses and painted art.
                void Copy(string control, string parent)
                {
                    Transform from = MonthlyFind(source, control);
                    Transform to = MonthlyFind(target, parent);
                    if (from == null || to == null) throw new InvalidOperationException("Missing " + control + " in " + path);
                    PersistGeneratedSceneVisuals(from, spec);
                    CopyTreasureLayer(from, to, control);
                }
                if (MonthlyFind(target, "Warehouse Panel") != null)
                    Copy("Treasure Curse Panel", "Warehouse Panel");
                if (MonthlyFind(target, "Treasure Workbench") != null)
                    foreach (string name in new[] { "Treasure Craft Map", "Treasure Open Folio", "Treasure Purify Curse" })
                        Copy(name, "Treasure Workbench");
                if (MonthlyFind(target, "Battle Card") != null)
                    Copy("Battle Curse Panel", "Battle Card");
                if (MonthlyDispatchReferences.Contains(reference)) SyncMonthlyDispatchText(source, target);
            }
            finally
            {
                if (staging != null) UnityEngine.Object.DestroyImmediate(staging);
                if (stagingScene.IsValid() && stagingScene.isLoaded) EditorSceneManager.CloseScene(stagingScene, true);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Save failed: " + path);
            RepairSavedNavigationCanvasScale(absolute);
            AssertSavedNavigationCanvasScale(absolute);
        }
    }
}
