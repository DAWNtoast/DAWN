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
        public static string[] ComponentResaleReferencePaths => new[]
        {
            "UI_ComponentShop.unity",
            "UIComponentShop/UIComponentShop_OfferBoard.unity",
            "UIComponentShop/UIComponentShop_OfferSoldOut.unity",
            "UIComponentShop/UIComponentShop_RefreshNotice.unity",
        };

        public static void SyncAndCaptureComponentResaleFromCommandLine()
        {
            GameplayPreviewSceneBuilder.RejectRetiredEditablePreviews();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Component resale previews require edit mode.");
            foreach (string reference in ComponentResaleReferencePaths)
                SyncComponentResaleEntrance(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("COMPONENT_RESALE_PREVIEW_SYNC: added the resale entrance to " +
                ComponentResaleReferencePaths.Length + " existing shop scenes; authored offers and showroom art retained.");
            UIPreviewSceneAcceptance.CaptureComponentResaleFromCommandLine();
        }

        private static void SyncComponentResaleEntrance(string reference)
        {
            const string entranceName = "Open Component Resale";
            string path = EditableSceneFolder + "/" + reference;
            string absolute = AbsolutePath(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing authored component shop preview", path);
            string backup = AbsolutePath("Artifacts/ComponentResale20261001/preview-backup/" + path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(absolute, backup);

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            CoreGameplayDemoController destination = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();
            if (destination.EditorPreviewScreen != CoreGameplayPreviewScreen.ComponentShop)
                throw new InvalidOperationException("Expected an authored component shop showroom in " + path);
            Transform targetPanel = destination.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "Component Shop Panel");
            string[] retainedChildren = targetPanel.Cast<Transform>().Where(item => item.name != entranceName)
                .Select(item => item.name).ToArray();
            Scene stagingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject staging = null;
            try
            {
                staging = new GameObject("__ComponentResaleEntranceStaging");
                SceneManager.MoveGameObjectToScene(staging, stagingScene);
                CoreGameplayDemoController source = staging.AddComponent<CoreGameplayDemoController>();
                CopyProvisionPreviewSettings(destination, source);
                source.RefreshEditorPreview();
                if (!source.HasCompleteEditorPreview)
                    throw new InvalidOperationException("Incomplete component resale source for " + path);
                Text targetClock = destination.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(item => item.name == "Port Facility Time");
                Text sourceClock = source.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(item => item.name == "Port Facility Time");
                if (targetClock != null && sourceClock != null) targetClock.text = sourceClock.text;
                Transform sourcePanel = source.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "Component Shop Panel");
                Transform entrance = sourcePanel.Find(entranceName);
                if (entrance == null || entrance.GetComponent<Button>() == null)
                    throw new InvalidOperationException("Runtime component resale entrance is missing in " + path);
                Text caption = entrance.GetComponentInChildren<Text>(true);
                if (caption == null || string.IsNullOrWhiteSpace(caption.text))
                    throw new InvalidOperationException("Runtime component resale entrance lacks its fixture caption in " + path);
                string fixtureCaption = caption.text;
                PreviewSceneSpec artSpec = Array.Find(Specs, item => item.Screen == source.EditorPreviewScreen);
                // Persist only the new control's generated visuals. The saved
                // merchandise, sold-out samples and showroom assets stay intact.
                PersistGeneratedSceneVisuals(entrance, artSpec);
                CopyTreasureLayer(entrance, targetPanel, entranceName);
                Transform copied = targetPanel.Find(entranceName);
                if (copied == null || copied.GetComponentInChildren<Text>(true)?.text != fixtureCaption)
                    throw new InvalidOperationException("The resale fixture count was not preserved in " + path);
                if (!retainedChildren.SequenceEqual(targetPanel.Cast<Transform>()
                    .Where(item => item.name != entranceName).Select(item => item.name)))
                    throw new InvalidOperationException("Authored component shop children changed while adding its resale entrance: " + path);
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
    }
}
