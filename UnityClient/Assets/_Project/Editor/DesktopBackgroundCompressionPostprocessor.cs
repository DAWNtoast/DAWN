using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public sealed class DesktopBackgroundCompressionPostprocessor : AssetPostprocessor
    {
        // Rebuild artifacts with the resolution-preserving NPOT mip fallback.
        public override uint GetVersion() => 2;

        private bool Eligible(TextureImporter importer) => DesktopBackgroundCompressionPolicy.IsEligible(
            assetPath, context.selectedBuildTarget, importer.isReadable, importer.textureType, importer.textureShape) &&
            TextureImporter.IsPlatformTextureFormatValid(importer.textureType, context.selectedBuildTarget, TextureImporterFormat.BC7);

        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (!Eligible(importer)) return;
            var settings = importer.GetPlatformTextureSettings("Standalone");
            // A disabled override can have stale max-size/resize settings. Copy
            // the effective defaults before enabling this platform override.
            if (!settings.overridden) settings = importer.GetDefaultPlatformTextureSettings();
            DesktopBackgroundCompressionPolicy.ConfigureImportedTextureSettings(importer, settings);
            importer.SetPlatformTextureSettings(settings);
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            var importer = (TextureImporter)assetImporter;
            if (!Eligible(importer)) return;
            DesktopBackgroundCompressionPolicy.CompressImportedTexture(texture, importer.mipmapEnabled);
        }
    }
}
