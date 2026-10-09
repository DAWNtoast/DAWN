using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleShipCutoutTests
    {
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = temporary.Count - 1; index >= 0; index--)
                if (temporary[index] != null) Object.DestroyImmediate(temporary[index]);
            temporary.Clear();
        }

        [Test]
        public void KnownAtlasNavyVariations_AreFullyTransparent()
        {
            for (int red = 0; red <= 1; red++)
                for (int green = 12; green <= 13; green++)
                    for (int blue = 31; blue <= 33; blue++)
                    {
                        Color encoded = new Color32((byte)red, (byte)green, (byte)blue, 255);
                        Assert.That(BattleShipCutout.CoverageForGammaColor(encoded), Is.EqualTo(0f));
                    }
        }

        [Test]
        public void LegacyAtlasSourceAndBlockCompressionNavy_AreFullyTransparent()
        {
            for (int red = 0; red <= 1; red++)
                for (int green = 8; green <= 12; green++)
                    for (int blue = 24; blue <= 27; blue++)
                    {
                        Color encoded = new Color32((byte)red, (byte)green, (byte)blue, 255);
                        Assert.That(BattleShipCutout.CoverageForGammaColor(encoded, true), Is.Zero,
                            "The 4x2 atlas uses a different navy matte from the 2x2 roster.");
                    }
        }

        [TestCase(0, 0, 0)]
        [TestCase(1, 1, 1)]
        [TestCase(6, 6, 6)]
        [TestCase(14, 8, 7)]
        [TestCase(19, 9, 25)]
        [TestCase(0, 0, 25)]
        [TestCase(7, 28, 21)]
        public void DarkSailTimberAndGhostColors_AreNotRemovedByLuminance(int red, int green, int blue)
        {
            Color encoded = new Color32((byte)red, (byte)green, (byte)blue, 255);
            Assert.That(BattleShipCutout.CoverageForGammaColor(encoded), Is.EqualTo(1f));
            Assert.That(BattleShipCutout.CoverageForGammaColor(encoded, true), Is.EqualTo(1f));
        }

        [Test]
        public void MatteEdge_IsSoftOnlyInTheSmallColorKeyBand()
        {
            Color edge = new Color(2.75f / 255f, 12.5f / 255f, 32f / 255f, 1f);
            float coverage = BattleShipCutout.CoverageForGammaColor(edge);
            Assert.That(coverage, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(BattleShipCutout.CoverageForGammaColor(new Color(0.8f, 0.7f, 0.4f, 0f)), Is.Zero);
            Assert.That(BattleShipCutout.CoverageForGammaColor(new Color(0.8f, 0.7f, 0.4f, 0.5f)), Is.EqualTo(0.5f));
        }

        [Test]
        public void ShaderIsIncludedAsAResourceWithTheStandardUiMaskProperties()
        {
            Shader shader = Resources.Load<Shader>(BattleShipCutout.ShaderResourcePath);
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            foreach (string property in new[] { "_MainTex", "_Color", "_KeyGamma", "_KeyTolerance", "_StencilComp",
                "_Stencil", "_StencilOp", "_StencilWriteMask", "_StencilReadMask", "_ColorMask", "_UseUIAlphaClip" })
                Assert.That(material.HasProperty(property), Is.True, property);
        }

        [Test]
        public void AttachAndReconfigure_ReleaseOnlyInstanceMaterialAndKeepAtlasAndUvs()
        {
            RawImage image = CreateImage();
            Texture2D atlas = Track(new Texture2D(4, 4, TextureFormat.RGBA32, false));
            image.texture = atlas;
            image.uvRect = new Rect(0.505f, 0.505f, 0.485f, 0.485f);
            Rect originalUv = image.uvRect;
            Material borrowedMaterial = image.material;
            BattleShipCutout cutout = BattleShipCutout.Attach(image);
            Material first = cutout.OwnedMaterial;
            Assert.That(first, Is.Not.Null);
            Assert.That(image.material, Is.SameAs(first));
            BattleShipCutout second = BattleShipCutout.Attach(image);
            Assert.That(second, Is.SameAs(cutout));
            Assert.That(first == null, Is.True, "Reinitializing must release the previous owned material.");
            Assert.That(image.GetComponents<BattleShipCutout>().Length, Is.EqualTo(1));
            Assert.That(image.uvRect, Is.EqualTo(originalUv));
            Assert.That(image.texture, Is.SameAs(atlas));

            Material last = cutout.OwnedMaterial;
            Object.DestroyImmediate(cutout);
            Assert.That(last == null, Is.True);
            Assert.That(image.material, Is.SameAs(borrowedMaterial));
            Assert.That(atlas != null, Is.True);
            Assert.That(borrowedMaterial != null, Is.True);
        }

        [Test]
        public void DifferentShips_DoNotShareOrModifyEachOthersCutoutMaterials()
        {
            RawImage first = CreateImage();
            RawImage second = CreateImage();
            BattleShipCutout firstCutout = BattleShipCutout.Attach(first);
            BattleShipCutout secondCutout = BattleShipCutout.Attach(second);
            Assert.That(firstCutout.OwnedMaterial, Is.Not.SameAs(secondCutout.OwnedMaterial));
            Material secondMaterial = secondCutout.OwnedMaterial;
            Object.DestroyImmediate(first.gameObject);
            Assert.That(secondMaterial != null, Is.True);
            Assert.That(second.material, Is.SameAs(secondMaterial));
        }

        private RawImage CreateImage()
        {
            return Track(new GameObject("Battle Cutout Test", typeof(RectTransform), typeof(RawImage))).GetComponent<RawImage>();
        }

        private T Track<T>(T value) where T : Object
        {
            temporary.Add(value);
            return value;
        }
    }
}
