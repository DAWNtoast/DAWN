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
    public static class TutorialVoyageSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/TutorialVoyage.unity";

        [InitializeOnLoadMethod]
        private static void QueueInitialSceneCreation()
        {
            if (Application.isBatchMode || File.Exists(GetAbsoluteScenePath()))
            {
                return;
            }

            EditorApplication.delayCall += CreateInitialSceneInBackground;
        }

        [MenuItem("Shattered Sail/Scenes/Create or Refresh Tutorial Voyage")]
        public static void CreateOrRefreshFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateOrRefreshScene(true);
        }

        [MenuItem("Shattered Sail/Scenes/Open Tutorial Voyage")]
        public static void OpenTutorialVoyage()
        {
            if (!File.Exists(Path.GetFullPath(ScenePath)))
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

        private static void CreateInitialSceneInBackground()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(GetAbsoluteScenePath()))
            {
                return;
            }

            CreateOrRefreshScene(false);
        }

        private static void CreateOrRefreshScene(bool keepOpen)
        {
            string directory = Path.GetDirectoryName(GetAbsoluteScenePath());
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                keepOpen ? NewSceneMode.Single : NewSceneMode.Additive);
            scene.name = "TutorialVoyage";
            SceneManager.SetActiveScene(scene);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.10f, 0.14f, 1f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            GameObject controllerObject = new GameObject("Tutorial Voyage");
            controllerObject.AddComponent<TutorialVoyageController>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save tutorial voyage scene at " + ScenePath);
            }

            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (keepOpen)
            {
                Selection.activeGameObject = controllerObject;
            }
            else
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousScene.IsValid() && previousScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousScene);
                }
            }
            Debug.Log("Tutorial voyage scene created: " + ScenePath);
        }

        private static string GetAbsoluteScenePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            if (existing.Any(item => string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase)))
            {
                EditorBuildSettings.scenes = existing
                    .Select(item => string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase)
                        ? new EditorBuildSettingsScene(ScenePath, true)
                        : item)
                    .ToArray();
                return;
            }

            EditorBuildSettings.scenes = existing
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
                .ToArray();
        }
    }
}
