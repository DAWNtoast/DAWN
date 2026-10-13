using System;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public sealed class DeckListAssetImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Resources/CardWorkshopV2/DeckList/",StringComparison.Ordinal)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Standalone");
            settings.name="Standalone"; settings.overridden=true; settings.maxTextureSize=2048;
            settings.format=TextureImporterFormat.BC7;
            settings.textureCompression=TextureImporterCompression.CompressedHQ; settings.compressionQuality=100;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
