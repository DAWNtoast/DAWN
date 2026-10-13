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
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string CoreScenePath = "Assets/_Project/Scenes/CoreGameplayDemo.unity";

        [MenuItem("Shattered Sail/Scenes/Create or Refresh Main Menu")]
        public static void CreateOrRefreshFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CreateOrRefreshScene(true);
            }
        }

        [MenuItem("Shattered Sail/Scenes/Open Main Menu")]
        public static void OpenMainMenu()
        {
            if (!File.Exists(GetAbsoluteScenePath()))
            {
                CreateOrRefreshFromMenu();
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        public static void CreateFromCommandLine()
        {
            CreateOrRefreshScene(true);
        }

        private static void CreateOrRefreshScene(bool keepOpen)
        {
            string directory = Path.GetDirectoryName(GetAbsoluteScenePath());
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainMenu";

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.07f, 0.09f, 1f);
            camera.orthographic = true;
            // The title screen is overlay UI. HDR would retain full-screen
            // camera color/depth attachments without affecting its artwork.
            camera.allowHDR = false;
            cameraObject.tag = "MainCamera";

            GameObject controllerObject = new GameObject("Main Menu");
            controllerObject.AddComponent<MainMenuController>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save main menu scene at " + ScenePath);
            }

            EnsurePlayerBuildOrder();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (keepOpen)
            {
                Selection.activeGameObject = controllerObject;
            }
            Debug.Log("Main menu scene created: " + ScenePath);
        }

        public static void EnsurePlayerBuildOrder()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(CoreScenePath, true),
            };
            ordered.AddRange(existing.Where(item =>
                !string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(item.path, CoreScenePath, StringComparison.OrdinalIgnoreCase)));
            EditorBuildSettings.scenes = ordered.ToArray();
        }

        private static string GetAbsoluteScenePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
