using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleIconArtCatalogTests
    {
        [Test]
        public void EveryAuthoredBase_IsUsedByACommandStatusOrHeader()
        {
            var referenced = new HashSet<string>();
            foreach (BattleCommandIconKind kind in Enum.GetValues(typeof(BattleCommandIconKind)))
                referenced.Add(BattleIconArtCatalog.CommandAssetId(kind));
            foreach (CoreBattleStatusKind kind in Enum.GetValues(typeof(CoreBattleStatusKind)))
                referenced.Add(BattleIconArtCatalog.StatusAssetId(kind));
            referenced.Add(BattleIconArtCatalog.HeaderAssetId(false));
            referenced.Add(BattleIconArtCatalog.HeaderAssetId(true));
            Assert.That(referenced, Does.Not.Contain(null));
            Assert.That(referenced.Count, Is.EqualTo(41));
            CollectionAssert.AreEquivalent(BattleIconArtCatalog.AssetIds, referenced);
            var cells = new HashSet<Rect>();
            foreach (string id in referenced)
            {
                Assert.That(BattleIconArtCatalog.TryGetUv(id, out Rect uv), Is.True, id);
                Assert.That(cells.Add(uv), Is.True, id);
                Assert.That(uv.xMin, Is.InRange(0f, 1f));
                Assert.That(uv.yMin, Is.InRange(0f, 1f));
                Assert.That(uv.xMax, Is.InRange(0f, 1f));
                Assert.That(uv.yMax, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void ImportedAtlas_PreservesTransparentIconsAndOpaqueSourceMarkSwatch()
        {
            Texture2D texture = BattleIconArtCatalog.Texture;
            Assert.That(texture, Is.Not.Null, "The HUD must load its authored art from Resources in a player build.");
            Assert.That(texture.width, Is.EqualTo(BattleIconArtCatalog.AtlasWidth));
            Assert.That(texture.height, Is.EqualTo(BattleIconArtCatalog.AtlasHeight));
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(readable.LoadImage(File.ReadAllBytes(path)), Is.True);
                foreach (string id in BattleIconArtCatalog.AssetIds)
                {
                    BattleIconArtCatalog.TryGetUv(id, out Rect uv);
                    int x = Mathf.RoundToInt(uv.xMin * readable.width);
                    int y = Mathf.RoundToInt(uv.yMin * readable.height);
                    Color[] pixels = readable.GetPixels(x, y, 256, 256);
                    int visible = 0;
                    for (int pixel = 0; pixel < pixels.Length; pixel++)
                    {
                        if (pixels[pixel].a > .5f) visible++;
                        int column = pixel % 256, row = pixel / 256;
                        if (column == 0 || column == 255 || row == 0 || row == 255)
                            Assert.That(pixels[pixel].a, Is.Zero, id + " must not have a square background or cropped edge.");
                    }
                    Assert.That(visible, Is.GreaterThan(5000), id + " must contain painted pixels.");
                }
                Color solid = readable.GetPixelBilinear(BattleIconArtCatalog.SolidUv.x, BattleIconArtCatalog.SolidUv.y);
                Assert.That(solid, Is.EqualTo(Color.white));
            }
            finally { Object.DestroyImmediate(readable); }
        }

        [Test]
        public void SourceMarks_UseOpaqueSwatchWhileBaseUsesAuthoredCell()
        {
            var host = new GameObject("Status art test", typeof(RectTransform), typeof(BattleStatusIconGraphic));
            var mesh = new Mesh();
            try
            {
                var graphic = host.GetComponent<BattleStatusIconGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(36, 36);
                graphic.Configure(CoreBattleStatusKind.Fire, "boarding_burn", true);
                Populate(graphic, mesh);
                Assert.That(graphic.mainTexture, Is.SameAs(BattleIconArtCatalog.Texture));
                Assert.That(mesh.vertexCount, Is.GreaterThan(4), "The boarding source badge must remain over the painting.");
                BattleIconArtCatalog.TryGetUv("fire", out Rect uv);
                Assert.That(mesh.uv[0], Is.EqualTo(uv.min));
                for (int i = 4; i < mesh.vertexCount; i++)
                    Assert.That(mesh.uv[i], Is.EqualTo(BattleIconArtCatalog.SolidUv));
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(host); }
        }

        [TestCase(false, "deck")]
        [TestCase(true, "settings")]
        public void Header_UsesTheAuthoredTextureWithoutTintingIt(bool settings, string id)
        {
            var host = new GameObject("Header art test", typeof(RectTransform), typeof(BattleHeaderIconGraphic));
            var mesh = new Mesh();
            try
            {
                var graphic = host.GetComponent<BattleHeaderIconGraphic>();
                graphic.rectTransform.sizeDelta = new Vector2(36, 36);
                graphic.Configure(settings, Color.red);
                Populate(graphic, mesh);
                Assert.That(graphic.mainTexture, Is.SameAs(BattleIconArtCatalog.Texture));
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                BattleIconArtCatalog.TryGetUv(id, out Rect uv);
                Assert.That(mesh.uv[0], Is.EqualTo(uv.min));
                foreach (Color32 tint in mesh.colors32) Assert.That(tint, Is.EqualTo((Color32)Color.white));
                Assert.That(graphic.raycastTarget, Is.False);
            }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(host); }
        }

        private static void Populate(Graphic graphic, Mesh mesh)
        {
            using (var helper = new VertexHelper())
            {
                graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
        }
    }
}
