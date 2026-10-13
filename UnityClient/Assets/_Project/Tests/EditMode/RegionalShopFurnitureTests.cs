using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class RegionalShopFurnitureTests
    {
        [TearDown]
        public void ReleaseFurnitureMaterials() => RegionalShopFurniture.ReleaseGeneratedResources();

        [Test]
        public void SixPortsLoadDistinctCompleteNativeResolutionAtlases()
        {
            var sources = new HashSet<Texture2D>();
            foreach (string port in RegionalShopFurniture.Ports)
            {
                var set = RegionalShopFurniture.For(port);
                Assert.That(set, Is.Not.Null, port);
                Assert.That(sources.Add(set.Atlas), Is.True, "No shared image with a different port tint.");
                Assert.That(set.Atlas.width, Is.EqualTo(1536));
                Assert.That(set.Atlas.height, Is.EqualTo(1024));
                Assert.That(set.Pieces.Length, Is.EqualTo(6));
                Assert.That(set.Material.shader.isSupported, Is.True);
                Assert.That(set.Atlas.isReadable, Is.False, "The painted atlas needs no CPU pixel copy.");
                Assert.That(set.Coverage.isReadable, Is.False, "Baked coverage needs no CPU pixel copy.");
                Assert.That(set.Coverage.format, Is.EqualTo(TextureFormat.R8));
                Assert.That(set.Coverage.width, Is.EqualTo(set.Atlas.width));
                Assert.That(set.Coverage.height, Is.EqualTo(set.Atlas.height));
                Assert.That(set.Material.GetTexture("_Coverage"), Is.SameAs(set.Coverage));
                foreach (var piece in set.Pieces)
                {
                    Assert.That(piece.Uv.xMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(piece.Uv.yMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(piece.Uv.xMax, Is.LessThanOrEqualTo(1));
                    Assert.That(piece.Uv.yMax, Is.LessThanOrEqualTo(1));
                    Assert.That(piece.Size.x / piece.Size.y, Is.EqualTo(piece.Pixels.width / piece.Pixels.height).Within(0.001));
                }
            }
        }

        [TestCase("灰鸥港")]
        [TestCase("白贝港")]
        [TestCase("铜钟港")]
        [TestCase("盐冠港")]
        [TestCase("暮潮港")]
        [TestCase("断锚港")]
        public void BakedCoverageMatchesOriginalAlgorithmAtEverySourcePixel(string port)
        {
            string sourcePath = "Assets/_Project/Resources/" + RegionalShopFurniture.ResourcePath(port) + ".png";
            string coveragePath = "Assets/_Project/Resources/" + RegionalShopFurniture.CoverageResourcePath(port) + ".png";
            var source = new Texture2D(2, 2, TextureFormat.RGB24, false);
            var baked = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
            Texture2D expected = null;
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(sourcePath)), Is.True);
                Assert.That(baked.LoadImage(File.ReadAllBytes(coveragePath)), Is.True);
                expected = RegionalShopFurniture.BuildCoverage(source);
                Assert.That(baked.width, Is.EqualTo(expected.width));
                Assert.That(baked.height, Is.EqualTo(expected.height));
                Color32[] expectedPixels = expected.GetPixels32(), bakedPixels = baked.GetPixels32();
                int differences = 0;
                for (int index = 0; index < expectedPixels.Length; index++)
                    if (expectedPixels[index].r != bakedPixels[index].r) differences++;
                Assert.That(differences, Is.Zero, "All 1,572,864 coverage samples must match the original flood-fill/edge contraction.");
                Assert.That(bakedPixels[0].r, Is.Zero);
                var importer = (TextureImporter)AssetImporter.GetAtPath(coveragePath);
                Assert.That(importer.sRGBTexture, Is.False, "Coverage is linear, including the original 180/255 boundary samples.");
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(importer.GetPlatformTextureSettings("Standalone").format, Is.EqualTo(TextureImporterFormat.R8));
            }
            finally
            {
                if (expected != null) Object.DestroyImmediate(expected);
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(source);
            }
        }

        [TestCase("灰鸥港")]
        [TestCase("白贝港")]
        [TestCase("铜钟港")]
        [TestCase("盐冠港")]
        [TestCase("暮潮港")]
        [TestCase("断锚港")]
        public void All125ComponentsStayClearOfTheirRegionalStandingSigns(string port)
        {
            foreach (var entry in new ComponentArtCatalog().Entries)
            {
                var geometry = ComponentShopDisplayGeometry.For(entry, port);
                Assert.That(geometry.RegionalFurniture, Is.Not.Null);
                Assert.That(geometry.ModelBounds.Overlaps(geometry.PlaqueBounds), Is.False, entry.Id);
                Assert.That(geometry.ModelBounds.yMin - geometry.PlaqueBounds.yMax,
                    Is.EqualTo(ComponentShopDisplayGeometry.MinimumPlaqueClearance).Within(0.01), entry.Id);
                Assert.That(geometry.PlaqueUv, Is.EqualTo(geometry.RegionalFurniture.Sign.Uv));
                Assert.That(geometry.WritingArea.width, Is.GreaterThan(115));
                Assert.That(geometry.WritingArea.height, Is.GreaterThan(44));
                float lowest = float.MaxValue;
                foreach (var foot in geometry.RegionalFurniture.Sign.Feet)
                    lowest = Mathf.Min(lowest, geometry.PlaquePosition.y + geometry.RegionalFurniture.Sign.Local(foot).y);
                Assert.That(lowest, Is.EqualTo(ComponentShopDisplayGeometry.PlaqueFloorY).Within(0.01));
            }
        }

        [Test]
        public void PhysicalPropsUseMinificationLevelsAndComponentArtSharesPaintedSampling()
        {
            var cards = new CardWorkshopArtCatalog();
            for (int i = 0; i < cards.ChestCount; i++)
            {
                Assert.That(cards.Chest(i).mipmapCount, Is.GreaterThan(1));
                Assert.That(cards.Chest(i).filterMode, Is.EqualTo(FilterMode.Trilinear));
            }
            var legacy = new LegacyDemoArtCatalog();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(legacy.RouteMap(i).mipmapCount, Is.GreaterThan(1));
                Assert.That(legacy.RouteMap(i).filterMode, Is.EqualTo(FilterMode.Trilinear));
            }
            foreach (var entry in new ComponentArtCatalog().Entries)
            {
                Assert.That(entry.ShipLayer.mipmapCount, Is.GreaterThan(1), entry.Id);
                Assert.That(entry.ShipLayer.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(entry.Texture.filterMode, Is.EqualTo(FilterMode.Bilinear));
            }
        }

        [Test]
        public void CoverageRemovesExteriorButKeepsSmallIvoryHighlights()
        {
            var texture = new Texture2D(32, 32);
            var colors = new Color[1024];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++) colors[y * 32 + x] = x > 3 && x < 28 && y > 3 && y < 28
                    ? new Color(0.48f, 0.36f, 0.22f) : Color.white;
            colors[16 * 32 + 16] = new Color(0.94f, 0.94f, 0.93f);
            texture.SetPixels(colors); texture.Apply();
            var mask = RegionalShopFurniture.BuildCoverage(texture);
            try
            {
                Assert.That(mask.GetPixel(0, 0).r, Is.Zero);
                Assert.That(mask.GetPixel(16, 16).r, Is.EqualTo(1));
                Assert.That(mask.GetPixel(12, 12).r, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(mask); Object.DestroyImmediate(texture); }
        }
    }
}
