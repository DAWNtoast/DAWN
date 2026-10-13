using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleShipTurnCutoutTests
    {
        [Test]
        public void KnownRoster_ProtectsCellSeparatorsWithoutChangingMirroredUvsOrBlackKey()
        {
            GameObject host = new GameObject("Mirrored Ship Cutout", typeof(RectTransform), typeof(RawImage));
            try
            {
                RawImage image = host.GetComponent<RawImage>();
                image.texture = new PortArtCatalog().ShipSheet;
                Rect uv = new Rect(1f, 0.5f, -0.5f, 0.5f);
                image.uvRect = uv;
                BattleShipCutout cutout = BattleShipCutout.Attach(image);
                Assert.That(image.uvRect, Is.EqualTo(uv));
                Assert.That(cutout.OwnedMaterial.GetVector("_AtlasGrid"), Is.EqualTo(new Vector4(2f, 2f, 10f, 10f)));
                Assert.That(cutout.OwnedMaterial.GetFloat("_SmoothCutout"), Is.EqualTo(1f));
                Assert.That(cutout.OwnedMaterial.GetFloat("_UseColorKey"), Is.Zero,
                    "The repainted atlas uses authored alpha and must preserve navy ship detail.");
                Assert.That(BattleShipCutout.CoverageForGammaColor(Color.black), Is.EqualTo(1f),
                    "Do not erase black sails to hide atlas separators.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void LateTextureAssignmentAndFilterChanges_UpdateTheExistingMaterialOnly()
        {
            GameObject host = new GameObject("Late Ship Texture", typeof(RectTransform), typeof(RawImage));
            Texture2D texture = new Texture2D(8, 8, TextureFormat.RGB24, false)
                { name = "Other Cutout", filterMode = FilterMode.Bilinear };
            try
            {
                RawImage image = host.GetComponent<RawImage>();
                BattleShipCutout cutout = BattleShipCutout.Attach(image);
                Material material = cutout.OwnedMaterial;
                Assert.That(material.GetFloat("_UseColorKey"), Is.EqualTo(1f));
                image.texture = new PortArtCatalog().ShipSheet;
                cutout.SendMessage("LateUpdate");
                Assert.That(cutout.OwnedMaterial, Is.SameAs(material));
                Assert.That(material.GetFloat("_UseColorKey"), Is.Zero);
                image.texture = texture;
                cutout.SendMessage("LateUpdate");
                Assert.That(cutout.OwnedMaterial, Is.SameAs(material));
                Assert.That(material.GetVector("_AtlasGrid"), Is.EqualTo(Vector4.zero), "Other art must not be cropped as a 2x2 ship atlas.");
                Assert.That(material.GetFloat("_SmoothCutout"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_UseColorKey"), Is.EqualTo(1f),
                    "Switching from an alpha atlas to generic opaque art must restore the legacy key.");
                texture.filterMode = FilterMode.Point;
                cutout.SendMessage("LateUpdate");
                Assert.That(cutout.OwnedMaterial, Is.SameAs(material));
                Assert.That(material.GetFloat("_SmoothCutout"), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SwitchingBetweenActualShipAtlases_ChangesMatteAndGridWithoutReplacingMaterialOrUvs()
        {
            GameObject host = new GameObject("Atlas Switching Cutout", typeof(RectTransform), typeof(RawImage));
            try
            {
                var catalog = new PortArtCatalog();
                RawImage image = host.GetComponent<RawImage>();
                image.texture = catalog.ShipSheet;
                image.uvRect = new Rect(0.5f, 1f, -0.5f, -0.5f);
                Rect uv = image.uvRect;
                BattleShipCutout cutout = BattleShipCutout.Attach(image);
                Material material = cutout.OwnedMaterial;
                Vector4 rosterKey = material.GetVector("_KeyGamma");
                image.texture = Resources.Load<Texture2D>("LegacyDemo/ship_sprite_sheet");
                Assert.That(image.texture, Is.Not.Null);
                cutout.SendMessage("LateUpdate");
                Assert.That(cutout.OwnedMaterial, Is.SameAs(material));
                Assert.That(image.uvRect, Is.EqualTo(uv));
                Assert.That(material.GetVector("_KeyGamma"), Is.Not.EqualTo(rosterKey));
                Assert.That(material.GetVector("_AtlasGrid"), Is.EqualTo(new Vector4(4f, 2f, 10f, 10f)),
                    "Legacy cells also need isolation from the adjacent ghost ship's flag.");
                Assert.That(material.GetFloat("_UseColorKey"), Is.Zero);
                image.texture = catalog.ShipSheet;
                cutout.SendMessage("LateUpdate");
                Assert.That(cutout.OwnedMaterial, Is.SameAs(material));
                Assert.That(material.GetVector("_KeyGamma"), Is.EqualTo(rosterKey));
                Assert.That(material.GetVector("_AtlasGrid"), Is.EqualTo(new Vector4(2f, 2f, 10f, 10f)));
                Assert.That(material.GetFloat("_UseColorKey"), Is.Zero);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
