using System;
using System.IO;
using System.Linq;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Editor
{
    public static class CoreGameplaySceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/CoreGameplayDemo.unity";

        [MenuItem("Shattered Sail/Scenes/Create or Refresh Core Gameplay Demo")]
        public static void CreateOrRefreshFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CreateOrRefreshScene(true);
            }
        }

        [MenuItem("Shattered Sail/Scenes/Open Core Gameplay Demo")]
        public static void OpenCoreGameplayDemo()
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
            scene.name = "CoreGameplayDemo";

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.10f, 0.14f, 1f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            GameObject controllerObject = new GameObject("Core Gameplay Demo");
            controllerObject.AddComponent<CoreGameplayDemoController>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save core gameplay scene at " + ScenePath);
            }

            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (keepOpen)
            {
                Selection.activeGameObject = controllerObject;
            }
            Debug.Log("Core gameplay scene created: " + ScenePath);
        }

        private static string GetAbsoluteScenePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void AddSceneToBuildSettings()
        {
            const string mainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            if (existing.Any(item => string.Equals(item.path, mainMenuPath, StringComparison.OrdinalIgnoreCase)))
            {
                EditorBuildSettings.scenes = new[]
                    {
                        new EditorBuildSettingsScene(mainMenuPath, true),
                        new EditorBuildSettingsScene(ScenePath, true),
                    }
                    .Concat(existing.Where(item =>
                        !string.Equals(item.path, mainMenuPath, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(existing.Where(item => !string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }
    }
}
