using System;
using System.IO;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    public static class VoyageHeaderPrefabBuilder
    {
        private const string Path = "Assets/_Project/Resources/UI/VoyageHeader.prefab";

        [MenuItem("Shattered Sail/UI/Create Missing Voyage Header Prefab")]
        public static void CreateMissingPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null) return;
            Directory.CreateDirectory("Assets/_Project/Resources/UI");
            AssetDatabase.Refresh();
            bool previous = CoreGameplayDemoController.ExportingVoyageHeaderPrefab;
            CoreGameplayDemoController.ExportingVoyageHeaderPrefab = true;
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, copy = null;
            try
            {
                host = new GameObject("Voyage Header Prefab Export");
                SceneManager.MoveGameObjectToScene(host, scene);
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.BrokenAnchor, 0);
                Canvas canvas = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName)?.GetComponent<Canvas>();
                Transform source = FixedGameViewport.Bounds(canvas)?.Find("Phase Content/Voyage Header");
                if (source == null) throw new InvalidOperationException("Missing generated voyage header.");
                copy = Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(copy, scene);
                copy.name = "Voyage Header";
                VoyageHeaderView view = copy.AddComponent<VoyageHeaderView>();
                view.CaptureBindings();
                // Inactive navigation chrome must not keep its old artwork
                // alive through a prefab asset's dependency graph.
                view.Frame.sprite = null;
                view.Compass.sprite = null;
                view.Title.text = view.Status.text = view.Time.text = string.Empty;
                view.Timeline.SetVoyageHours(0d);
                UiPrefabExportUtility.PrepareForSave(copy);
                if (PrefabUtility.SaveAsPrefabAsset(copy, Path) == null)
                    throw new InvalidOperationException("Failed to save " + Path);
                AssetDatabase.SaveAssets();
                Debug.Log("Created editable voyage header prefab: " + Path);
            }
            finally
            {
                if (copy != null) Object.DestroyImmediate(copy);
                if (host != null) Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
                CoreGameplayDemoController.ExportingVoyageHeaderPrefab = previous;
            }
        }
    }
}
