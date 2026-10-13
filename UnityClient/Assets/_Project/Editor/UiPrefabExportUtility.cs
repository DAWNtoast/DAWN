using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>Exports shared generated skins once; no artwork is regenerated or copied.</summary>
    public static class UiPrefabExportUtility
    {
        private const string Folder = "Assets/_Project/Resources/UI/SharedPrefabSkins";

        public static void PrepareForSave(GameObject root)
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                node.gameObject.hideFlags = HideFlags.None;
                foreach (Component component in node.GetComponents<Component>())
                    if (component != null) component.hideFlags = HideFlags.None;
            }
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) button.onClick = new Button.ButtonClickedEvent();
            foreach (Slider slider in root.GetComponentsInChildren<Slider>(true)) slider.onValueChanged = new Slider.SliderEvent();
            foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true)) toggle.onValueChanged = new Toggle.ToggleEvent();
            foreach (Dropdown dropdown in root.GetComponentsInChildren<Dropdown>(true)) dropdown.onValueChanged = new Dropdown.DropdownEvent();
            var bindings = root.GetComponent<PrefabRuntimeSkinBindings>() ?? root.AddComponent<PrefabRuntimeSkinBindings>();
            bindings.CaptureBindings();
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && !EditorUtility.IsPersistent(image.sprite)) image.sprite = PersistSprite(image.sprite);
        }

        private static Sprite PersistSprite(Sprite source)
        {
            Texture2D texture = source.texture;
            string texturePath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(texturePath))
            {
                // Only tiny generated UI geometry is accepted here. Painting assets
                // must already have an imported texture and keep that same reference.
                if (texture.width > 256 || texture.height > 256)
                    throw new InvalidOperationException("Unexpected transient art texture: " + texture.name);
                byte[] png = texture.EncodeToPNG();
                string geometryIdentity = texture.graphicsFormat + "|" + texture.filterMode + "|" + texture.wrapModeU + "|" +
                    texture.wrapModeV + "|" + texture.wrapModeW + "|" + texture.anisoLevel + "|" + texture.mipmapCount + "|" + Convert.ToBase64String(png);
                texturePath = Folder + "/Geometry_" + Hash(Encoding.UTF8.GetBytes(geometryIdentity)) + ".png";
                if (!File.Exists(texturePath))
                {
                    File.WriteAllBytes(texturePath, png);
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = texture.mipmapCount > 1;
                    importer.isReadable = false;
                    importer.sRGBTexture = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);
                    importer.filterMode = texture.filterMode;
                    importer.wrapModeU = texture.wrapModeU;
                    importer.wrapModeV = texture.wrapModeV;
                    importer.wrapModeW = texture.wrapModeW;
                    importer.anisoLevel = texture.anisoLevel;
                    importer.alphaIsTransparency = false;
                    importer.SaveAndReimport();
                }
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }
            string identity = AssetDatabase.AssetPathToGUID(texturePath) + "|" + source.rect + "|" +
                source.pivot + "|" + source.pixelsPerUnit + "|" + source.border;
            string path = Folder + "/Skin_" + Hash(Encoding.UTF8.GetBytes(identity)) + ".asset";
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            Sprite copy = Sprite.Create(texture, source.rect,
                new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
            copy.name = source.name;
            copy.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 20).ToLowerInvariant();
        }
    }
}
