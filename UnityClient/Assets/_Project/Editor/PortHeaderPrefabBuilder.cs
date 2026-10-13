using System;
using System.Collections.Generic;
using System.IO;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>One-time migration. Existing, artist-edited prefabs are never overwritten.</summary>
    public static class PortHeaderPrefabBuilder
    {
        private const string Folder = "Assets/_Project/Resources/UI";

        [MenuItem("Shattered Sail/UI/Create Missing Port Header Prefabs")]
        public static void CreateMissingPrefabs()
        {
            Directory.CreateDirectory(Folder + "/PortHeaderArt");
            AssetDatabase.Refresh();
            CreateHeader(false);
            CreateHeader(true);
            AssetDatabase.SaveAssets();
        }

        private static void CreateHeader(bool hub)
        {
            string path = Folder + (hub ? "/PortHubHeader.prefab" : "/PortFacilityHeader.prefab");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = null, copy = null;
            try
            {
                host = new GameObject("Port Header Export");
                SceneManager.MoveGameObjectToScene(host, scene);
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(hub ? CoreGameplayPreviewScreen.PortHub : CoreGameplayPreviewScreen.Warehouse,
                    CoreGameplayPreviewPort.BrokenAnchor, 0);
                Canvas previewCanvas = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName)?.GetComponent<Canvas>();
                Transform source = FixedGameViewport.Bounds(previewCanvas)?.Find(
                    "Phase Content/" + (hub ? "Port Hub" : "Port Facility Header"));
                if (source == null) throw new InvalidOperationException("Missing generated port header.");
                copy = Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(copy, scene);
                copy.name = source.name;
                foreach (Transform node in copy.GetComponentsInChildren<Transform>(true))
                {
                    node.gameObject.hideFlags = HideFlags.None;
                    foreach (Component component in node.GetComponents<Component>())
                        component.hideFlags = HideFlags.None;
                }
                foreach (Button button in copy.GetComponentsInChildren<Button>(true))
                    button.onClick = new Button.ButtonClickedEvent();
                // Runtime skins use Sprite.Create and HideAndDontSave. Export real
                // sprite assets so the prefab also looks correct after reloading.
                var sprites = new Dictionary<Sprite, Sprite>();
                int index = 0;
                foreach (Image image in copy.GetComponentsInChildren<Image>(true))
                {
                    // Regional frame art is always assigned by the controller
                    // before activation. Serializing a default frame would pin
                    // that port's large texture through the loaded prefab.
                    if (image.name == (hub ? "Port Hub Frame" : "Port Facility Frame"))
                    {
                        image.sprite = null;
                        continue;
                    }
                    Sprite sprite = image.sprite;
                    if (sprite == null || EditorUtility.IsPersistent(sprite)) continue;
                    if (!sprites.TryGetValue(sprite, out Sprite persistent))
                    {
                        persistent = Object.Instantiate(sprite);
                        persistent.hideFlags = HideFlags.None;
                        persistent.name = sprite.name;
                        string spritePath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/PortHeaderArt/" +
                            (hub ? "Hub" : "Facility") + "Sprite" + index++ + ".asset");
                        AssetDatabase.CreateAsset(persistent, spritePath);
                        sprites.Add(sprite, persistent);
                    }
                    image.sprite = persistent;
                }
                PortHeaderView bindings = copy.AddComponent<PortHeaderView>();
                bindings.CaptureBindings(hub);
                if (!bindings.IsComplete(hub)) throw new InvalidOperationException("Incomplete header bindings.");
                if (PrefabUtility.SaveAsPrefabAsset(copy, path) == null)
                    throw new InvalidOperationException("Failed to save " + path);
                Debug.Log("Created editable UI prefab: " + path);
            }
            finally
            {
                if (copy != null) Object.DestroyImmediate(copy);
                if (host != null) Object.DestroyImmediate(host);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
