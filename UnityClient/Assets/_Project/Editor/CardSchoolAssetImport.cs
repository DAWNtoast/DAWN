using System;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    /// <summary>Consistent UI imports, including illustrations added after the code lands.</summary>
    public sealed class CardSchoolAssetImport : AssetPostprocessor
    {
        private const string Root = "Assets/_Project/Resources/CardSchools/";
        private const string WesternRoot = "Assets/_Project/Resources/CardArt/Western/";
        private const int IllustrationMaxTextureSize = 1024;

        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal) &&
                !assetPath.StartsWith(WesternRoot, StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            bool frame = assetPath.StartsWith(Root + "Frames/", StringComparison.Ordinal);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = frame;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            // Preserve fine illustration detail for native 4K card faces and previews.
            // Frame dimensions and the existing BC7 compression remain unchanged.
            importer.maxTextureSize = frame ? 1024 : IllustrationMaxTextureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Standalone");
            settings.name = "Standalone";
            settings.overridden = true;
            settings.maxTextureSize = importer.maxTextureSize;
            settings.format = TextureImporterFormat.BC7;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
