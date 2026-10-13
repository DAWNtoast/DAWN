using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    // Install only in the isolated audit project's Editor directory.
    public static partial class GameplayPreviewSceneBuilder
    {
        public static void SyncEquipmentPreviewsFromCommandLine()
        {
            throw new System.NotSupportedException("UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.");
#if false // Historical implementation; this target is retired.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Preview synchronization requires stopped edit mode.");
            string[] equipment = {
                "UIShipyardDetail/UIShipyardDetail_ComponentLoadout.unity",
                "TutorialVoyage/37_EquipRewardComponent.unity",
            };
            var changed = new List<string>();
            foreach (string reference in equipment)
            {
                string path = EditableSceneFolder + "/" + reference;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var controller = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
                var settings = new SerializedObject(controller);
                settings.FindProperty("editorPreviewShipIndex").intValue = 0;
                settings.FindProperty("editorPreviewShipPage").intValue = (int)ShipyardDossierTab.Equipment;
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save " + path);
                SyncRegionalShipyard(reference, -1, -1);
                changed.Add(path);
            }

            // Dynamic UIPreviews scenes rebuild from runtime code and have no authored
            // Back control to migrate. Restrict serialized edits to editable previews.
            string[] dossiers = AssetDatabase.FindAssets("t:Scene", new[] { EditableSceneFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.Contains("ShipyardDetail") && !changed.Contains(path)).ToArray();
            foreach (string path in dossiers)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                RectTransform back = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .FirstOrDefault(rect => rect.name == "Back To Shipyard Gallery" || rect.name == "Back To Voyage Departure");
                if (back == null) continue;
                back.sizeDelta = new Vector2(236f, 42f);
                back.anchoredPosition = new Vector2(-594f, 338f);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save " + path);
                RepairSavedNavigationCanvasScale(AbsolutePath(path));
                AssertSavedNavigationCanvasScale(AbsolutePath(path));
                changed.Add(path);
            }
            AssetDatabase.SaveAssets();
            string output = "D:/Game/Artifacts/ShipyardEquipment20261010";
            Directory.CreateDirectory(output);
            File.WriteAllLines(output + "/synced-scenes.txt", changed);
            var dependencies = AssetDatabase.GetDependencies(changed.ToArray(), true)
                .Where(path => path.StartsWith("Assets/_Project/Presentation/EditablePreviewAssets/", StringComparison.Ordinal));
            File.WriteAllLines(output + "/preview-dependencies.txt", dependencies);
            Debug.Log("SHIPYARD_EQUIPMENT_PREVIEWS_SYNCED: " + changed.Count);

#endif
        }
    }
}
