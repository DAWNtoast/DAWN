using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class TavernPortraitPaperBlendPlayModeTests
    {
        private const string ExpectedShader = "Shattered Sail/UI/Tavern Portrait Paper Blend";
        private readonly List<Object> owned = new List<Object>();

        [SetUp]
        public void RequireGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Portrait mask readback requires the graphics-enabled PlayMode suite.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index] != null) Object.Destroy(owned[index]);
            owned.Clear();
            // Components release their own materials/textures during OnDisable.
            yield return null;
            yield return null;
        }

        [TestCase("断锚港")]
        [TestCase("白贝港")]
        [TestCase("铜钟港")]
        [TestCase("盐冠港")]
        [TestCase("暮潮港")]
        [TestCase("灰鸥港")]
        public void SixRegionalPortraitsCreateIndependentCoverageWithoutChangingTheAtlas(string port)
        {
            PortArtCatalog art = new PortArtCatalog();
            Texture2D atlas = art.RegionalCrewAtlas(port);
            Assert.That(atlas, Is.Not.Null, port);
            Assert.That(atlas.name, Does.EndWith("_portraits_v2"));
            HashSet<Texture2D> masks = new HashSet<Texture2D>();
            HashSet<Material> materials = new HashSet<Material>();
            HashSet<Vector4> mappings = new HashSet<Vector4>();
            HashSet<Rect> cells = new HashSet<Rect>();

            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.ForPort(port))
            {
                Texture2D source = art.CrewPortrait(crew);
                Rect sourceUv = art.CrewPortraitUv(crew);
                Assert.That(source, Is.SameAs(atlas), crew.Id);
                int originalId = source.GetInstanceID();
                bool originalReadable = source.isReadable;
                TextureFormat originalFormat = source.format;
                int originalMipCount = source.mipmapCount;
                FilterMode originalFilter = source.filterMode;
                TextureWrapMode originalWrap = source.wrapMode;
                HideFlags originalFlags = source.hideFlags;
                RawImage image = CreateImage(crew.Id, source, sourceUv);

                TavernPortraitPaperBlend blend = TavernPortraitPaperBlend.Attach(image);

                AssertOwnedMask(blend, image, crew.Id);
                Assert.That(masks.Add(blend.MaskTexture), Is.True, crew.Id + " needs its own cell coverage.");
                Assert.That(materials.Add(blend.OwnedMaterial), Is.True, crew.Id + " needs its own UV mapping material.");
                Assert.That(cells.Add(sourceUv), Is.True, crew.Id);
                Vector4 mapping = blend.OwnedMaterial.GetVector("_MaskUv");
                Assert.That(mappings.Add(mapping), Is.True, crew.Id + " must not reuse another cell's atlas-to-mask mapping.");
                Assert.That(sourceUv.xMin * mapping.x + mapping.z, Is.EqualTo(0f).Within(0.00001f), crew.Id);
                Assert.That(sourceUv.yMin * mapping.y + mapping.w, Is.EqualTo(0f).Within(0.00001f), crew.Id);
                Assert.That(sourceUv.xMax * mapping.x + mapping.z, Is.EqualTo(1f).Within(0.00001f), crew.Id);
                Assert.That(sourceUv.yMax * mapping.y + mapping.w, Is.EqualTo(1f).Within(0.00001f), crew.Id);
                AssertCoverageRange(blend.MaskTexture, crew.Id);

                // Inspect the borrowed resource through runtime properties only;
                // this suite never edits or queries an Editor texture importer.
                Assert.That(image.texture, Is.SameAs(source), crew.Id);
                Assert.That(image.uvRect, Is.EqualTo(sourceUv), crew.Id);
                Assert.That(art.CrewPortrait(crew), Is.SameAs(source), crew.Id);
                Assert.That(art.CrewPortraitUv(crew), Is.EqualTo(sourceUv), crew.Id);
                Assert.That(source.GetInstanceID(), Is.EqualTo(originalId), crew.Id);
                Assert.That(source.isReadable, Is.EqualTo(originalReadable), crew.Id);
                Assert.That(source.format, Is.EqualTo(originalFormat), crew.Id);
                Assert.That(source.mipmapCount, Is.EqualTo(originalMipCount), crew.Id);
                Assert.That(source.filterMode, Is.EqualTo(originalFilter), crew.Id);
                Assert.That(source.wrapMode, Is.EqualTo(originalWrap), crew.Id);
                Assert.That(source.hideFlags, Is.EqualTo(originalFlags), crew.Id);
            }

            Assert.That(masks.Count, Is.EqualTo(6), port);
            Assert.That(materials.Count, Is.EqualTo(6), port);
            Assert.That(mappings.Count, Is.EqualTo(6), port);
            Assert.That(cells.Count, Is.EqualTo(6), port);
        }

        [UnityTest]
        public IEnumerator DisableReenableAndDestroyRestoreBorrowedMaterialAndReleaseEveryOwnedObject()
        {
            PortArtCatalog art = new PortArtCatalog();
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 0);
            Texture2D source = art.CrewPortrait(crew);
            Rect sourceUv = art.CrewPortraitUv(crew);
            RawImage image = CreateImage("Dossier Blend Lifetime", source, sourceUv);
            Shader defaultShader = Shader.Find("UI/Default");
            Assert.That(defaultShader, Is.Not.Null);
            Material priorMaterial = Track(new Material(defaultShader)
            {
                name = "Borrowed Dossier Test Material",
                hideFlags = HideFlags.HideAndDontSave,
            });
            image.material = priorMaterial;
            TavernPortraitPaperBlend blend = TavernPortraitPaperBlend.Attach(image);
            AssertOwnedMask(blend, image, "Initial attach");
            Material firstMaterial = blend.OwnedMaterial;
            Texture2D firstMask = blend.MaskTexture;
            int firstMaterialId = firstMaterial.GetInstanceID();
            int firstMaskId = firstMask.GetInstanceID();

            blend.enabled = false;

            Assert.That(image.material, Is.SameAs(priorMaterial), "Disable must immediately restore the prior material.");
            Assert.That(blend.OwnedMaterial, Is.Null);
            Assert.That(blend.MaskTexture, Is.Null);
            yield return null;
            Assert.That(firstMaterial == null, Is.True, "Disabled material must be destroyed after the frame.");
            Assert.That(firstMask == null, Is.True, "Disabled mask must be destroyed after the frame.");

            blend.enabled = true;

            AssertOwnedMask(blend, image, "Re-enable");
            Material secondMaterial = blend.OwnedMaterial;
            Texture2D secondMask = blend.MaskTexture;
            Assert.That(secondMaterial.GetInstanceID(), Is.Not.EqualTo(firstMaterialId));
            Assert.That(secondMask.GetInstanceID(), Is.Not.EqualTo(firstMaskId));
            Assert.That(image.texture, Is.SameAs(source));
            Assert.That(image.uvRect, Is.EqualTo(sourceUv));

            Object.Destroy(blend);
            yield return null;
            yield return null;

            Assert.That(blend == null, Is.True);
            Assert.That(secondMaterial == null, Is.True, "Component destruction must release the recreated material.");
            Assert.That(secondMask == null, Is.True, "Component destruction must release the recreated mask.");
            Assert.That(image.material, Is.SameAs(priorMaterial));
            Assert.That(image.texture, Is.SameAs(source));
            Assert.That(image.uvRect, Is.EqualTo(sourceUv));
            Assert.That(priorMaterial != null, Is.True, "The component must never destroy the borrowed material.");

            TavernPortraitPaperBlend replacement = TavernPortraitPaperBlend.Attach(image);
            AssertOwnedMask(replacement, image, "Attach after component removal");
            Material lastMaterial = replacement.OwnedMaterial;
            Texture2D lastMask = replacement.MaskTexture;
            Object.Destroy(image.gameObject);
            yield return null;
            yield return null;

            Assert.That(replacement == null, Is.True);
            Assert.That(lastMaterial == null, Is.True, "Destroying the portrait host must not leak its material.");
            Assert.That(lastMask == null, Is.True, "Destroying the portrait host must not leak its mask.");
            Assert.That(priorMaterial != null, Is.True);
            Assert.That(source != null, Is.True, "The authored Resources atlas remains borrowed throughout the lifetime.");
            Assert.That(art.CrewPortrait(crew), Is.SameAs(source));
        }

        [Test]
        public void GrayGullNavigatorCurlOpeningsClearWithoutRemovingHairFaceOrSleeve()
        {
            PortArtCatalog art = new PortArtCatalog();
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 0);
            RawImage image = CreateImage("Curl opening regression", art.CrewPortrait(crew), art.CrewPortraitUv(crew));
            Color32[] mask = TavernPortraitPaperBlend.Attach(image).MaskTexture.GetPixels32();
            foreach (Vector2Int probe in new[] { new Vector2Int(70, 250), new Vector2Int(35, 335),
                new Vector2Int(360, 160), new Vector2Int(354, 206),
                new Vector2Int(362, 253), new Vector2Int(359, 219) })
                Assert.That(mask[(511 - probe.y) * 512 + probe.x].r, Is.LessThan(20), "Empty curl opening " + probe);
            foreach (Vector2Int probe in new[] { new Vector2Int(355, 145), new Vector2Int(110, 245),
                new Vector2Int(250, 185), new Vector2Int(343, 438), new Vector2Int(182, 68) })
                Assert.That(mask[(511 - probe.y) * 512 + probe.x].r, Is.EqualTo(255), "Hair, face, sleeve, headscarf " + probe);
        }

        [TestCase("灰鸥港", 4)]
        [TestCase("暮潮港", 5)]
        public void PalePaperBetweenCurlsAndInstrumentsClearsButAdjacentLightHairAndHeadwearRemain(string port, int profession)
        {
            PortArtCatalog art = new PortArtCatalog();
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession(port, profession);
            RawImage image = CreateImage("Instrument opening regression", art.CrewPortrait(crew), art.CrewPortraitUv(crew));
            Color32[] mask = TavernPortraitPaperBlend.Attach(image).MaskTexture.GetPixels32();
            Vector2Int[] empty = profession == 4 ? new[] { new Vector2Int(137, 107), new Vector2Int(140, 176),
                new Vector2Int(141, 199), new Vector2Int(145, 214) } : new[] { new Vector2Int(176, 228), new Vector2Int(146, 258) };
            Vector2Int[] foreground = profession == 4 ? new[] { new Vector2Int(146, 199), new Vector2Int(259, 36) } :
                new[] { new Vector2Int(192, 219) };
            Color32[] raw = (Color32[])typeof(TavernPortraitPaperBlend).GetMethod("ReadCellSrgb", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { image.texture, image.uvRect, 512, 512 });
            object[] estimate = { raw, 512, 512, Vector3.zero };
            typeof(TavernPortraitPaperBlend).GetMethod("TryEstimatePaper", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, estimate);
            List<string> missed = new List<string>();
            foreach (Vector2Int probe in empty)
            {
                int index = (511 - probe.y) * 512 + probe.x;
                if (mask[index].r >= 20) missed.Add(probe + " source=" + raw[index] + " paper=" + estimate[3]);
            }
            Assert.That(missed, Is.Empty, port + " paper openings: " + string.Join("; ", missed));
            foreach (Vector2Int probe in foreground)
                Assert.That(mask[(511 - probe.y) * 512 + probe.x].r, Is.EqualTo(255), port + " preserved hair/scarf " + probe);
        }

        [Test]
        public void WhiteShellNavigatorCurlOpeningsClearAndShellOrnamentsRemain()
        {
            PortArtCatalog art = new PortArtCatalog();
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 0);
            RawImage image = CreateImage("White Shell curl regression", art.CrewPortrait(crew), art.CrewPortraitUv(crew));
            Color32[] mask = TavernPortraitPaperBlend.Attach(image).MaskTexture.GetPixels32();
            foreach (Vector2Int probe in new[] { new Vector2Int(268, 24), new Vector2Int(219, 35), new Vector2Int(306, 29),
                new Vector2Int(435, 217), new Vector2Int(421, 245), new Vector2Int(431, 289),
                new Vector2Int(54, 252), new Vector2Int(29, 346), new Vector2Int(59, 410) })
                Assert.That(mask[(511 - probe.y) * 512 + probe.x].r, Is.LessThan(20), "White Shell empty curl " + probe);
            foreach (Vector2Int probe in new[] { new Vector2Int(307, 316), new Vector2Int(160, 108) })
                Assert.That(mask[(511 - probe.y) * 512 + probe.x].r, Is.EqualTo(255), "Skin / shell ornament " + probe);
        }

        private static void AssertOwnedMask(TavernPortraitPaperBlend blend, RawImage image, string context)
        {
            Assert.That(blend, Is.Not.Null, context);
            Assert.That(blend.OwnedMaterial, Is.Not.Null, context);
            Assert.That(blend.MaskTexture, Is.Not.Null, context);
            Assert.That(image.material, Is.SameAs(blend.OwnedMaterial), context);
            Assert.That(blend.OwnedMaterial.shader.name, Is.EqualTo(ExpectedShader), context);
            Assert.That(blend.OwnedMaterial.shader.isSupported, Is.True, context);
            Assert.That(blend.OwnedMaterial.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave), context);
            Assert.That(blend.MaskTexture.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave), context);
            Assert.That(blend.OwnedMaterial.GetTexture("_PaperMask"), Is.SameAs(blend.MaskTexture), context);
            Assert.That(blend.MaskTexture.isReadable, Is.True, context);
            Assert.That(blend.MaskTexture.width, Is.EqualTo(512), context);
            Assert.That(blend.MaskTexture.height, Is.EqualTo(512), context);
            Assert.That(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(blend.MaskTexture.graphicsFormat), Is.False,
                context + " coverage must be linear data rather than an sRGB color texture.");
        }

        private static void AssertCoverageRange(Texture2D mask, string context)
        {
            Color32[] pixels = mask.GetPixels32();
            float transparent = pixels.Count(pixel => pixel.r <= 12) / (float)pixels.Length;
            float opaque = pixels.Count(pixel => pixel.r >= 243) / (float)pixels.Length;
            // The 36 captured v2 masks ranged from 0.21 to 0.62 transparent and
            // 0.38 to 0.79 opaque. Broad limits catch a missing/inverted matte
            // without treating a guessed face coordinate as ground truth.
            Assert.That(transparent, Is.InRange(0.1f, 0.9f), context + " transparent fraction");
            Assert.That(opaque, Is.GreaterThan(0.2f), context + " opaque fraction");
            TestContext.Progress.WriteLine(context + ": transparent=" + transparent.ToString("F3") + ", opaque=" + opaque.ToString("F3"));
        }

        private RawImage CreateImage(string name, Texture2D source, Rect uv)
        {
            GameObject host = Track(new GameObject(name, typeof(RectTransform), typeof(RawImage)));
            RawImage image = host.GetComponent<RawImage>();
            image.texture = source;
            image.uvRect = uv;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private T Track<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }
    }
}
