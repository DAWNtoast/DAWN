using System;
using System.IO;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShatteredSail.Editor
{
    /// <summary>Bakes the original runtime algorithm without altering source artwork.</summary>
    public sealed class RegionalShopFurnitureCoverageBaker : IPreprocessBuildWithReport
    {
        private const string ResourceRoot = "Assets/_Project/Resources/";
        public int callbackOrder => 0;

        [MenuItem("Shattered Sail/Art/Bake Regional Furniture Coverage")]
        public static void BakeAll()
        {
            RegionalShopFurniture.ReleaseGeneratedResources();
            foreach (string port in RegionalShopFurniture.Ports)
            {
                string sourcePath = ResourceRoot + RegionalShopFurniture.ResourcePath(port) + ".png";
                string coveragePath = ResourceRoot + RegionalShopFurniture.CoverageResourcePath(port) + ".png";
                var source = new Texture2D(2, 2, TextureFormat.RGB24, false);
                Texture2D coverage = null, output = null;
                try
                {
                    if (!source.LoadImage(File.ReadAllBytes(sourcePath), false))
                        throw new InvalidOperationException("Cannot decode " + sourcePath);
                    coverage = RegionalShopFurniture.BuildCoverage(source);
                    // PNG encoding is explicitly RGB24; the importer retains only
                    // the red channel, exactly matching the original linear R8.
                    output = new Texture2D(coverage.width, coverage.height, TextureFormat.RGB24, false, true);
                    output.SetPixels32(coverage.GetPixels32());
                    output.Apply(false, false);
                    File.WriteAllBytes(coveragePath, output.EncodeToPNG());
                    AssetDatabase.ImportAsset(coveragePath, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(coveragePath);
                    importer.textureType = TextureImporterType.Default;
                    importer.isReadable = false;
                    importer.sRGBTexture = false;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = 2048;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.alphaIsTransparency = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    var defaults = importer.GetDefaultPlatformTextureSettings();
                    defaults.format = TextureImporterFormat.R8;
                    defaults.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SetPlatformTextureSettings(defaults);
                    var standalone = importer.GetPlatformTextureSettings("Standalone");
                    standalone.name = "Standalone";
                    standalone.overridden = true;
                    standalone.maxTextureSize = 2048;
                    standalone.format = TextureImporterFormat.R8;
                    standalone.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SetPlatformTextureSettings(standalone);
                    importer.SaveAndReimport();

                    var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
                    sourceImporter.isReadable = false;
                    sourceImporter.SaveAndReimport();
                    Debug.Log("Baked native-resolution linear R8 furniture coverage: " + coveragePath);
                }
                finally
                {
                    if (output != null) Object.DestroyImmediate(output);
                    if (coverage != null) Object.DestroyImmediate(coverage);
                    Object.DestroyImmediate(source);
                }
            }
            AssetDatabase.SaveAssets();
            ValidateAll();
        }

        [MenuItem("Shattered Sail/Art/Validate Regional Furniture Coverage")]
        public static void ValidateAll()
        {
            foreach (string port in RegionalShopFurniture.Ports)
            {
                string sourcePath = ResourceRoot + RegionalShopFurniture.ResourcePath(port) + ".png";
                string coveragePath = ResourceRoot + RegionalShopFurniture.CoverageResourcePath(port) + ".png";
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                var coverage = AssetDatabase.LoadAssetAtPath<Texture2D>(coveragePath);
                var importer = AssetImporter.GetAtPath(coveragePath) as TextureImporter;
                if (source == null || coverage == null || importer == null || source.isReadable || coverage.isReadable ||
                    source.width != coverage.width || source.height != coverage.height || coverage.format != TextureFormat.R8 ||
                    importer.sRGBTexture || coverage.mipmapCount != 1 || coverage.filterMode != FilterMode.Bilinear ||
                    coverage.wrapMode != TextureWrapMode.Clamp)
                    throw new BuildFailedException("Invalid baked furniture coverage for " + port + ". Run Bake Regional Furniture Coverage.");
            }
        }

        public void OnPreprocessBuild(BuildReport report) => ValidateAll();
    }
}
