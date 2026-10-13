using System;
using System.IO;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>One-time migration preserving the established hierarchy and pixel geometry.</summary>
    public static class MainMenuPrefabBuilder
    {
        private const string Folder = "Assets/_Project/Resources/UI";

        [MenuItem("Shattered Sail/UI/Create Missing Main Menu Prefabs")]
        public static void CreateMissingPrefabs()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            bool previous = MainMenuController.ExportingPrefab;
            EventSystem previousEventSystem = EventSystem.current;
            GameObject previousSelection = previousEventSystem != null ? previousEventSystem.currentSelectedGameObject : null;
            MainMenuController.ExportingPrefab = true;
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null;
            try
            {
                host = new GameObject("Main Menu Prefab Export");
                SceneManager.MoveGameObjectToScene(host, scene);
                MainMenuController controller = host.AddComponent<MainMenuController>();
                foreach (MainMenuPrefabKind kind in Enum.GetValues(typeof(MainMenuPrefabKind)))
                {
                    string path = Folder + "/MainMenu" + kind + ".prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                    Transform source = controller.GetPrefabExportSource(kind);
                    if (source == null) throw new InvalidOperationException("Missing menu surface: " + kind);
                    GameObject copy = Object.Instantiate(source.gameObject);
                    try
                    {
                        SceneManager.MoveGameObjectToScene(copy, scene);
                        copy.name = kind == MainMenuPrefabKind.Surface ? "Main Menu Canvas" : source.name;
                        if (kind == MainMenuPrefabKind.Surface)
                        {
                            Transform viewport = copy.transform.Find(FixedGameViewport.Name);
                            foreach (string optional in new[] { "Settings Panel", "New Game Confirmation", "Achievements Panel", "Voyage Save Notice" })
                            {
                                Transform panel = viewport.Find(optional);
                                if (panel != null) Object.DestroyImmediate(panel.gameObject);
                            }
                        }
                        var view = copy.AddComponent<MainMenuPrefabView>();
                        view.CaptureBindings(kind);
                        UiPrefabExportUtility.PrepareForSave(copy);
                        // Settings are per-profile drafts; no editor machine state
                        // or achievement progress is serialized into the template.
                        foreach (Slider slider in copy.GetComponentsInChildren<Slider>(true)) slider.SetValueWithoutNotify(0f);
                        foreach (Toggle toggle in copy.GetComponentsInChildren<Toggle>(true)) toggle.SetIsOnWithoutNotify(false);
                        if (kind == MainMenuPrefabKind.Settings)
                            view.MasterValue.text = view.MusicValue.text = view.EffectsValue.text = "0%";
                        copy.SetActive(kind == MainMenuPrefabKind.Surface);
                        if (PrefabUtility.SaveAsPrefabAsset(copy, path) == null)
                            throw new InvalidOperationException("Failed to save " + path);
                        Debug.Log("Created editable menu prefab: " + path);
                    }
                    finally { Object.DestroyImmediate(copy); }
                }
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
                    MainMenuController.ExportingPrefab = previous;
                    if (previousEventSystem != null)
                    {
                        EventSystem.current = previousEventSystem;
                        previousEventSystem.SetSelectedGameObject(previousSelection != null ? previousSelection : null);
                    }
                }
            }
        }
    }
}
