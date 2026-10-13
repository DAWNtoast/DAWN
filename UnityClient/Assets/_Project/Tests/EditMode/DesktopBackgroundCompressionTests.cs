using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DesktopBackgroundCompressionTests
    {
        private const string Prefix = "Assets/_Project/Resources/";
        // This test assembly deliberately does not reference the Editor assembly.
        private static Type Policy => Type.GetType("ShatteredSail.Editor.DesktopBackgroundCompressionPolicy, ShatteredSail.Editor", true);
        private static object Call(string method, params object[] args) => Policy.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        [TestCase("MainMenu/main_menu_background.png", true)]
        [TestCase("PortArt/port_hub_gray_gull_v3.png", true)]
        [TestCase("PortFacilities/shipyard_water_gallery_v3.png", true)]
        [TestCase("PortFacilities/treasure_worktable_background_v3.png", true)]
        [TestCase("PortFacilities/Regional/Tavern/tavern_white_shell_v1.png", true)]
        [TestCase("PortFacilities/Regional/Warehouse/warehouse_broken_anchor_v1.png", true)]
        [TestCase("PortFacilities/Regional/Warehouse/warehouse_white_shell_v1.png", true)]
        [TestCase("PortFacilities/Regional/Warehouse/warehouse_bronze_bell_v1.png", true)]
        [TestCase("PortFacilities/Regional/Warehouse/warehouse_salt_crown_v1.png", true)]
        [TestCase("PortFacilities/Regional/Warehouse/warehouse_twilight_tide_v1.png", true)]
        [TestCase("PortFacilities/Regional/Warehouse/warehouse_gray_gull_v1.png", true)]
        [TestCase("BoardingBattle/Backgrounds/boarding_storm_night_v1.png", true)]
        [TestCase("BattleEnvironments/battle_open_ocean_v1.png", true)]
        [TestCase("NavigationLookout/Environments/lookout_storm_night_v2.png", true)]
        [TestCase("NavigationLookout/lookout_sea_v1.png", true)]
        [TestCase("PortFacilities/shipyard_background.png", false)]
        [TestCase("PortFacilities/facility_icon_sheet.png", false)]
        [TestCase("PortHeaders/broken_anchor_header_v1.png", true)]
        [TestCase("PortHeaders/white_shell_header_v1.png", true)]
        [TestCase("PortHeaders/bronze_bell_header_v1.png", true)]
        [TestCase("PortHeaders/salt_crown_header_v1.png", true)]
        [TestCase("PortHeaders/twilight_tide_header_v1.png", true)]
        [TestCase("PortHeaders/gray_gull_header_v1.png", true)]
        [TestCase("UIWood/wood_button_face_v1.png", false)]
        [TestCase("UISurfaces/journal_parchment_v1.png", true)]
        [TestCase("UISurfaces/nautical_canvas_v1.png", true)]
        [TestCase("UISurfaces/aged_reading_parchment_v1.png", false)]
        [TestCase("PortIcons/port_gray_gull_marker_v1.png", false)]
        [TestCase("ComponentShopRegional/furniture_gray_gull_v1.png", false)]
        [TestCase("PortArt/crew_gray_gull_portraits_v2.png", false)]
        [TestCase("BoardingBattle/Characters/boarding_crew_gray_gull_v2.png", false)]
        [TestCase("NavigationLookout/lookout_fog_v1.png", false)]
        [TestCase("ItemIcons/items_small_v1.png", false)]
        [TestCase("MainMenu/main_menu_background_copy.png", false)]
        public void WhitelistSeparatesFullBackgroundsFromPixelSensitiveArt(string relative, bool expected)
        {
            Assert.That(Call("IsListed", Prefix + relative), Is.EqualTo(expected));
        }

        [Test]
        public void OnlyListedDefaultOrSpriteSurfacesOnDesktopAreEligible()
        {
            string path = Prefix + "MainMenu/main_menu_background.png";
            foreach (BuildTarget target in new[] { BuildTarget.StandaloneWindows64, BuildTarget.StandaloneOSX })
            {
                Assert.That(Call("IsEligible", path, target, false, TextureImporterType.Default, TextureImporterShape.Texture2D), Is.True);
                Assert.That(Call("IsEligible", path, target, false, TextureImporterType.Sprite, TextureImporterShape.Texture2D), Is.True);
            }
            Assert.That(Call("IsEligible", path, BuildTarget.Android, false, TextureImporterType.Default, TextureImporterShape.Texture2D), Is.False);
            Assert.That(Call("IsEligible", path, BuildTarget.StandaloneWindows64, true, TextureImporterType.Default, TextureImporterShape.Texture2D), Is.False);
            Assert.That(Call("IsEligible", path, BuildTarget.StandaloneWindows64, true, TextureImporterType.Sprite, TextureImporterShape.Texture2D), Is.False);
            Assert.That(Call("IsEligible", path, BuildTarget.StandaloneWindows64, false, TextureImporterType.NormalMap, TextureImporterShape.Texture2D), Is.False);
            Assert.That(Call("IsEligible", path, BuildTarget.StandaloneWindows64, false, TextureImporterType.Default, TextureImporterShape.TextureCube), Is.False);
            foreach (string unlisted in new[] { "PortIcons/port_gray_gull_marker_v1.png", "PortArt/CrewFullBody/crew_cook_pepe_fullbody.png",
                "ModularShip/Layers/bow_oak_ram.png", "ComponentShopRegional/furniture_gray_gull_v1.png",
                "UIWood/wood_button_face_v1.png" })
                Assert.That(Call("IsEligible", Prefix + unlisted, BuildTarget.StandaloneWindows64, false,
                    TextureImporterType.Sprite, TextureImporterShape.Texture2D), Is.False, unlisted);
        }

        [Test]
        public void AlignmentKeepsNearestFourDimensionsWithoutLargeResolutionChanges()
        {
            Assert.That(Call("AlignedDimension", 941), Is.EqualTo(940));
            Assert.That(Call("AlignedDimension", 1254), Is.EqualTo(1256));
            Assert.That(Call("AlignedDimension", 1536), Is.EqualTo(1536));
            for (int dimension = 1; dimension <= 4096; dimension++)
            {
                int aligned = (int)Call("AlignedDimension", dimension);
                Assert.That(aligned % 4, Is.Zero);
                Assert.That(aligned, Is.GreaterThanOrEqualTo(4));
                Assert.That(Math.Abs(aligned - dimension), Is.LessThanOrEqualTo(dimension < 4 ? 3 : 2));
            }
        }

        [Test]
        public void PlatformCompressionRetainsResolutionResizeAndAlphaSettings()
        {
            var settings = new TextureImporterPlatformSettings { name = "Standalone", maxTextureSize = 512,
                resizeAlgorithm = TextureResizeAlgorithm.Bilinear, allowsAlphaSplitting = true, overridden = false };
            Call("ConfigureStandaloneSettings", settings);
            Assert.That(settings.overridden, Is.True);
            Assert.That(settings.format, Is.EqualTo(TextureImporterFormat.BC7));
            Assert.That(settings.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
            Assert.That(settings.compressionQuality, Is.EqualTo(100));
            Assert.That(settings.crunchedCompression, Is.False);
            Assert.That(settings.maxTextureSize, Is.EqualTo(512));
            Assert.That(settings.resizeAlgorithm, Is.EqualTo(TextureResizeAlgorithm.Bilinear));
            Assert.That(settings.allowsAlphaSplitting, Is.True);
        }

        [Test]
        public void BilinearSamplingPreservesFullImageEdgesAndBlendsInteriorWithoutPadding()
        {
            var source = new[] { new Color32(0, 0, 0, 255), new Color32(200, 0, 0, 255),
                new Color32(0, 200, 0, 255), new Color32(200, 200, 0, 255) };
            var result = (Color32[])Call("Resample", source, 2, 2, 4, 4);
            Assert.That(result[0], Is.EqualTo(source[0]));
            Assert.That(result[3], Is.EqualTo(source[1]));
            Assert.That(result[12], Is.EqualTo(source[2]));
            Assert.That(result[15], Is.EqualTo(source[3]));
            Assert.That(result[5].r, Is.InRange(49, 51));
            Assert.That(result[5].g, Is.InRange(49, 51));
            foreach (Color32 pixel in result) Assert.That(pixel.a, Is.EqualTo(255));
        }

        [Test]
        public void AlignedImageSamplingIsAnExactIndependentCopy()
        {
            var source = new[] { new Color32(13, 47, 109, 211), new Color32(255, 3, 68, 128) };
            var result = (Color32[])Call("Resample", source, 2, 1, 2, 1);
            Assert.That(result, Is.Not.SameAs(source));
            CollectionAssert.AreEqual(source, result);
            Assert.That(((Color32[])Call("Resample", new[] { source[0] }, 1, 1, 4, 4)), Is.All.EqualTo(source[0]));
        }

        [TestCase(7, 5, false, 8, 4)]
        [TestCase(8, 8, false, 8, 8)]
        [TestCase(7, 5, true, 8, 4)]
        public void ImportProcessingActuallyEncodesBC7AndPreservesMipPolicy(int width, int height, bool mips, int expectedWidth, int expectedHeight)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mips);
            try
            {
                var pixels = new Color32[width * height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)(i * 3), 80, 170, 255);
                texture.SetPixels32(pixels);
                texture.Apply(mips, false);
                Call("CompressImportedTexture", texture, mips);
                Assert.That(texture.format, Is.EqualTo(TextureFormat.BC7), "A selected importer enum alone is not proof of compression.");
                Assert.That(texture.width, Is.EqualTo(expectedWidth));
                Assert.That(texture.height, Is.EqualTo(expectedHeight));
                Assert.That(texture.mipmapCount > 1, Is.EqualTo(mips));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [TestCase(SpriteImportMode.Single, false)]
        [TestCase(SpriteImportMode.Multiple, false)]
        [TestCase(SpriteImportMode.Multiple, true)]
        public void ListedSpriteBackgroundImportKeepsSourceSettingsAndGeneratedRectsInsideAlignedTexture(SpriteImportMode mode, bool mips)
        {
            if (!(bool)Call("SupportsTarget", EditorUserBuildSettings.activeBuildTarget))
                Assert.Ignore("Real BC7 import regression requires a desktop active build target.");
            string folderName = "__DesktopBackgroundCompressionTest_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            string path = folder + "/background.png";
            var listed = (HashSet<string>)Policy.GetField("pathSet", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var source = new Texture2D(7, 5, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[35];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32((byte)(i * 5), 85, 165, 255);
                source.SetPixels32(pixels); source.Apply(false, false);
                byte[] png = source.EncodeToPNG();
                AssetDatabase.CreateFolder("Assets", folderName);
                File.WriteAllBytes(path, png);
                // Register only this isolated fixture, not an existing production asset.
                Assert.That(listed.Add(path), Is.True);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = mode;
                importer.spritePixelsPerUnit = 100f;
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);
                importer.isReadable = false;
                importer.mipmapEnabled = mips;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 512;
                var settings = importer.GetPlatformTextureSettings("Standalone");
                settings.maxTextureSize = 512;
                Call("ConfigureStandaloneSettings", settings);
                importer.SetPlatformTextureSettings(settings);
                if (mode == SpriteImportMode.Multiple)
                {
#pragma warning disable 0618
                    importer.spritesheet = new[] { new SpriteMetaData { name = "Full Painted Background",
                        rect = new Rect(0f, 0f, 7f, 5f), pivot = Vector2.one * .5f, alignment = (int)SpriteAlignment.Center } };
#pragma warning restore 0618
                }
                importer.SaveAndReimport();
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.format, Is.EqualTo(TextureFormat.BC7));
                Assert.That(texture.width, Is.EqualTo(8));
                Assert.That(texture.height, Is.EqualTo(4));
                Assert.That(texture.isReadable, Is.False);
                Assert.That(texture.mipmapCount > 1, Is.EqualTo(mips));
                CollectionAssert.AreEqual(png, File.ReadAllBytes(path), "The source PNG must remain byte-for-byte unchanged.");
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Assert.That(sprites, Has.Length.EqualTo(1), "A successful texture import must preserve its Sprite subasset.");
                foreach (Sprite sprite in sprites)
                {
                    Assert.That(sprite.texture, Is.SameAs(texture));
                    Assert.That(sprite.rect.xMin, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(sprite.rect.yMin, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(sprite.rect.xMax, Is.LessThanOrEqualTo(texture.width));
                    Assert.That(sprite.rect.yMax, Is.LessThanOrEqualTo(texture.height));
                }
                importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.spriteImportMode, Is.EqualTo(mode));
                Assert.That(importer.isReadable, Is.False);
                Assert.That(importer.mipmapEnabled, Is.EqualTo(mips));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(importer.GetPlatformTextureSettings("Standalone").maxTextureSize, Is.EqualTo(512));
            }
            finally
            {
                listed.Remove(path);
                AssetDatabase.DeleteAsset(folder);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
