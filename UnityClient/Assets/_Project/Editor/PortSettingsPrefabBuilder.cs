using System;
using System.Reflection;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class PortSettingsPrefabBuilder
    {
        private const string SettingsPath = "Assets/_Project/Resources/UI/PortSettings.prefab";
        private const string ConfirmationPath = "Assets/_Project/Resources/UI/ReturnToMenuConfirmation.prefab";

        [MenuItem("Shattered Sail/UI/Create Missing Gameplay Settings Prefabs")]
        public static void CreateMissingPrefabs()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPath) != null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmationPath) != null) return;
            EventSystem previousEventSystem = EventSystem.current;
            GameObject previousSelection = previousEventSystem != null ? previousEventSystem.currentSelectedGameObject : null;
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null;
            try
            {
                host = new GameObject("Gameplay Settings Prefab Export");
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.BrokenAnchor, 0);
                Invoke(controller, "OpenPortSettings");
                var settings = Read<RectTransform>(controller, "portSettingsOverlay");
                Export(settings.gameObject, SettingsPath, scene, true);
                Invoke(controller, "OpenReturnToMenuConfirmation");
                var confirmation = Read<RectTransform>(controller, "returnMenuConfirmation");
                Export(confirmation.gameObject, ConfirmationPath, scene, false);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                try
                {
                    if (host != null) Object.DestroyImmediate(host);
                    EditorSceneManager.ClosePreviewScene(scene);
                }
                finally
                {
                    if (previousEventSystem != null)
                    {
                        EventSystem.current = previousEventSystem;
                        previousEventSystem.SetSelectedGameObject(previousSelection != null ? previousSelection : null);
                    }
                }
            }
        }

        private static void Export(GameObject source, string path, Scene scene, bool settings)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject copy = Object.Instantiate(source);
            try
            {
                copy.name = source.name;
                SceneManager.MoveGameObjectToScene(copy, scene);
                // These transient input handlers carry delegates and are rebound at open.
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null && (behaviour.GetType().Name == "PortSettingsCancelHandler" ||
                        behaviour.GetType().Name == "PortSettingsBackgroundFocusHandler")) Object.DestroyImmediate(behaviour);
                if (settings)
                {
                    PortSettingsView view = copy.GetComponent<PortSettingsView>() ?? copy.AddComponent<PortSettingsView>();
                    view.CaptureBindings();
                    if (!view.IsComplete) throw new InvalidOperationException("Incomplete settings bindings.");
                    // A retained template must not keep the 4K paper loaded while closed.
                    view.Parchment.texture = null;
                    view.Hint.text = "";
                }
                else
                {
                    var view = copy.GetComponent<ReturnToMenuConfirmationView>() ?? copy.AddComponent<ReturnToMenuConfirmationView>();
                    view.CaptureBindings();
                    if (!view.IsComplete) throw new InvalidOperationException("Incomplete confirmation bindings.");
                }
                UiPrefabExportUtility.PrepareForSave(copy);
                copy.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(copy, path) == null) throw new InvalidOperationException("Failed to save " + path);
                Debug.Log("Created editable UI prefab: " + path);
            }
            finally { Object.DestroyImmediate(copy); }
        }

        private static void Invoke(CoreGameplayDemoController controller, string name) =>
            typeof(CoreGameplayDemoController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
        private static T Read<T>(CoreGameplayDemoController controller, string name) =>
            (T)typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
    }
}
