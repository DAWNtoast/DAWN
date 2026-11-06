using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator RuntimeMemory_BattleArtKeepsLiveOwnersThenCollectsAndReopens()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return AwaitTransientMemoryCollection(controller);
            GameObject host = CreateBattleArtMemoryOwners();
            int[] ids = BattleArtMemoryTextureIds(host);
            string[] content = ArtMemoryContentSignatures(host);
            Assert.That(ids.Length, Is.EqualTo(8), "Six fields plus separate naval and boarding effect atlases.");
            try
            {
                yield return CollectGameplayBattleArtForTest(controller);
                CollectionAssert.AreEqual(ids, BattleArtMemoryTextureIds(host), "Visible art survives the production cache release.");
                AssertBattleArtMemoryMeshes(host);
                Object.Destroy(host);
                host = null;
                yield return null;
                yield return CollectGameplayBattleArtForTest(controller);
                AssertBattleArtMemoryTexturesGone(ids);

                host = CreateBattleArtMemoryOwners();
                int[] reopened = BattleArtMemoryTextureIds(host);
                Assert.That(reopened.Length, Is.EqualTo(ids.Length));
                // Unity may reuse an imported asset's instance ID after unloading.
                // The absent inventory above proves collection; reopening must
                // restore the same artwork and UVs, regardless of its new ID.
                CollectionAssert.AreEqual(content, ArtMemoryContentSignatures(host));
                AssertBattleArtMemoryMeshes(host);
                Object.Destroy(host);
                host = null;
                yield return null;
                yield return CollectGameplayBattleArtForTest(controller);
                AssertBattleArtMemoryTexturesGone(reopened);
                LogAssert.NoUnexpectedReceived();
            }
            finally { if (host != null) Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_RarityBoardsPreserveLiveImagesAndReleaseGeneratedSpritePins()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return CollectMenuArtForTest();
            GameObject host = CreateRarityMemoryOwners();
            int[] sprites = RarityMemorySpriteIds(host);
            int[] textures = RarityMemoryTextureIds(host);
            string[] content = ArtMemoryContentSignatures(host);
            Assert.That(sprites.Length, Is.EqualTo(5));
            Assert.That(textures.Length, Is.EqualTo(5));
            try
            {
                yield return CollectMenuArtForTest();
                CollectionAssert.AreEqual(sprites, RarityMemorySpriteIds(host));
                CollectionAssert.AreEqual(textures, RarityMemoryTextureIds(host));
                AssertBattleArtMemoryMeshes(host);
                foreach (Image image in host.GetComponentsInChildren<Image>())
                {
                    Assert.That(image.sprite.border.x, Is.GreaterThan(0));
                    Assert.That((image.sprite.hideFlags & HideFlags.DontUnloadUnusedAsset) == 0, Is.True);
                }
                Object.Destroy(host);
                host = null;
                yield return null;
                yield return CollectMenuArtForTest();
                Assert.That(sprites.Any(LoadedSpriteExists), Is.False, "Retired generated rarity sprites must no longer pin their source art.");
                AssertBattleArtMemoryTexturesGone(textures);

                host = CreateRarityMemoryOwners();
                int[] reopenedSprites = RarityMemorySpriteIds(host);
                int[] reopenedTextures = RarityMemoryTextureIds(host);
                Assert.That(reopenedSprites.Length, Is.EqualTo(sprites.Length));
                Assert.That(reopenedTextures.Length, Is.EqualTo(textures.Length));
                CollectionAssert.AreEqual(content, ArtMemoryContentSignatures(host));
                AssertBattleArtMemoryMeshes(host);
                yield return CollectMenuArtForTest();
                CollectionAssert.AreEqual(reopenedSprites, RarityMemorySpriteIds(host));
                CollectionAssert.AreEqual(reopenedTextures, RarityMemoryTextureIds(host));
                foreach (Image image in host.GetComponentsInChildren<Image>())
                    Assert.That((image.sprite.hideFlags & HideFlags.DontUnloadUnusedAsset) == 0, Is.True);
                Object.Destroy(host);
                host = null;
                yield return null;
                yield return CollectMenuArtForTest();
                Assert.That(reopenedSprites.Any(LoadedSpriteExists), Is.False);
                AssertBattleArtMemoryTexturesGone(reopenedTextures);
                LogAssert.NoUnexpectedReceived();
            }
            finally { if (host != null) Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_MenuReleasesBattleIconsWithoutBreakingSharedSupplyImages()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return CollectMenuArtForTest();
            GameObject host = CreateSupplyMemoryOwners();
            int[] ids = host.GetComponentsInChildren<RawImage>().Select(image => image.texture.GetInstanceID()).Distinct().ToArray();
            string[] content = ArtMemoryContentSignatures(host);
            Assert.That(ids.Length, Is.EqualTo(1), "Supply items keep their shared battle icon atlas.");
            try
            {
                yield return CollectMenuArtForTest();
                Assert.That(host.GetComponentsInChildren<RawImage>().All(image => image.texture != null && image.texture.GetInstanceID() == ids[0]), Is.True);
                AssertBattleArtMemoryMeshes(host);
                Object.Destroy(host);
                host = null;
                yield return null;
                yield return CollectMenuArtForTest();
                AssertBattleArtMemoryTexturesGone(ids);
                host = CreateSupplyMemoryOwners();
                int[] reopened = host.GetComponentsInChildren<RawImage>().Select(image => image.texture.GetInstanceID()).Distinct().ToArray();
                Assert.That(reopened.Length, Is.EqualTo(1));
                CollectionAssert.AreEqual(content, ArtMemoryContentSignatures(host));
                AssertBattleArtMemoryMeshes(host);
                Object.Destroy(host);
                host = null;
                yield return null;
                yield return CollectMenuArtForTest();
                AssertBattleArtMemoryTexturesGone(reopened);
                LogAssert.NoUnexpectedReceived();
            }
            finally { if (host != null) Object.Destroy(host); }
        }

        private static GameObject CreateBattleArtMemoryOwners()
        {
            var host = new GameObject("Battle Art Memory Owners", typeof(RectTransform), typeof(Canvas));
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            foreach (CoreNavalFieldKind kind in Enum.GetValues(typeof(CoreNavalFieldKind)))
            {
                var child = new GameObject("Memory Field " + kind, typeof(RectTransform), typeof(RawImage));
                child.transform.SetParent(host.transform, false);
                child.GetComponent<RawImage>().texture = NavalFieldArtCatalog.ForField(kind);
                Assert.That(child.GetComponent<RawImage>().texture, Is.Not.Null, kind.ToString());
                Assert.That(child.GetComponent<RawImage>().texture.name,
                    Is.EqualTo("field_" + kind.ToString().ToLowerInvariant() + "_v2"));
            }
            var effect = new GameObject("Memory Naval Effect", typeof(RectTransform), typeof(NavalCombatFxGraphic));
            effect.transform.SetParent(host.transform, false);
            effect.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 600);
            var naval = effect.GetComponent<NavalCombatFxGraphic>();
            naval.Configure(NavalVfxRole.Cannon, BattleSchoolAttackKind.Fire, true, BattleSchoolAttackOutcome.Hit,
                new Vector2(-200, 0), new Vector2(200, 0));
            naval.SetProgress(.4f);
            Assert.That(naval.mainTexture.name, Is.EqualTo("naval_fx_remaster_v2"));
            foreach (BattleSchoolAttackKind kind in new[] { BattleSchoolAttackKind.Fire, BattleSchoolAttackKind.Tide })
            {
                var child = new GameObject("Memory Boarding " + kind, typeof(RectTransform), typeof(BattleSchoolAttackGraphic));
                child.transform.SetParent(host.transform, false);
                child.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 600);
                var graphic = child.GetComponent<BattleSchoolAttackGraphic>();
                graphic.Configure(kind, BattleSchoolAttackOutcome.Hit, new Vector2(-200, 0), new Vector2(200, 0), true);
                graphic.SetProgress(.55f);
                RawImage[] paintings = graphic.GetComponentsInChildren<RawImage>();
                Assert.That(paintings, Is.Not.Empty);
                Assert.That(paintings.All(image => image.texture != null && image.texture.name == "boarding_fx_v3"), Is.True);
            }
            Canvas.ForceUpdateCanvases();
            return host;
        }

        private static int[] BattleArtMemoryTextureIds(GameObject host)
        {
            Assert.That(host.GetComponentsInChildren<RawImage>().All(image => image.texture != null), Is.True);
            return host.GetComponentsInChildren<RawImage>().Select(image => image.texture.GetInstanceID())
                .Concat(host.GetComponentsInChildren<NavalCombatFxGraphic>().Select(graphic => graphic.mainTexture.GetInstanceID()))
                .Distinct().OrderBy(id => id).ToArray();
        }

        private static void AssertBattleArtMemoryMeshes(GameObject host)
        {
            foreach (Graphic graphic in host.GetComponentsInChildren<Graphic>().Where(graphic =>
                graphic is NavalCombatFxGraphic || graphic is BattleSchoolAttackGraphic || graphic is RawImage || graphic is Image))
            {
                using (var mesh = new VertexHelper())
                {
                    graphic.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { mesh });
                    Assert.That(mesh.currentVertCount, Is.GreaterThan(0), graphic.name + " must retain visible effect geometry.");
                }
            }
        }

        private static GameObject CreateRarityMemoryOwners()
        {
            var host = new GameObject("Rarity Memory Owners", typeof(RectTransform));
            foreach (ComponentRarity rarity in Enum.GetValues(typeof(ComponentRarity)))
            {
                var child = new GameObject("Memory Rarity " + rarity, typeof(RectTransform), typeof(Image));
                child.transform.SetParent(host.transform, false);
                child.GetComponent<Image>().sprite = ComponentRarityWoodSkin.FaceFor(rarity);
                Assert.That(child.GetComponent<Image>().sprite, Is.Not.Null, rarity.ToString());
                Assert.That(child.GetComponent<Image>().sprite.texture.name,
                    Is.EqualTo("wood_" + rarity.ToString().ToLowerInvariant()));
            }
            return host;
        }

        private static int[] RarityMemorySpriteIds(GameObject host) => host.GetComponentsInChildren<Image>()
            .Select(image => image.sprite.GetInstanceID()).OrderBy(id => id).ToArray();

        private static int[] RarityMemoryTextureIds(GameObject host) => host.GetComponentsInChildren<Image>()
            .Select(image => image.sprite.texture.GetInstanceID()).Distinct().OrderBy(id => id).ToArray();

        private static GameObject CreateSupplyMemoryOwners()
        {
            var host = new GameObject("Supply Memory Owners", typeof(RectTransform));
            foreach (string key in new[] { "powder", "cannonballs", "wood", "medicine" })
            {
                ItemIconArt art = ItemIconCatalog.ForCargo(key);
                Assert.That(art.IsAvailable, Is.True, key);
                string icon = key == "wood" ? "planks" : key == "cannonballs" ? "cannonball" : key;
                Assert.That(BattleIconArtCatalog.TryGetUv(icon, out Rect expectedUv), Is.True);
                Assert.That(art.Uv, Is.EqualTo(expectedUv), key + " must retain the shared supply source cell.");
                Assert.That(art.Texture.name, Is.EqualTo("battle_icons_v1"));
                Assert.That(art.Texture.width, Is.EqualTo(BattleIconArtCatalog.AtlasWidth));
                Assert.That(art.Texture.height, Is.EqualTo(BattleIconArtCatalog.AtlasHeight));
                var child = new GameObject("Memory Supply " + key, typeof(RectTransform), typeof(RawImage));
                child.transform.SetParent(host.transform, false);
                child.GetComponent<RawImage>().texture = art.Texture;
                child.GetComponent<RawImage>().uvRect = art.Uv;
            }
            return host;
        }

        // Store only scalar/string content identities across yields. Keeping a
        // Texture/Sprite snapshot would itself prevent the collection under test.
        private static string[] ArtMemoryContentSignatures(GameObject host) => host.GetComponentsInChildren<Graphic>()
            .Where(graphic => graphic is RawImage || graphic is Image || graphic is NavalCombatFxGraphic)
            .Select(graphic =>
            {
                Texture texture = graphic is Image image ? image.sprite.texture : graphic.mainTexture;
                Assert.That(texture != null && texture.width > 0 && texture.height > 0, Is.True, graphic.name);
                string source = string.Empty;
                if (graphic is RawImage raw)
                    source = ArtMemoryGeometry(raw.uvRect.x, raw.uvRect.y, raw.uvRect.width, raw.uvRect.height);
                else if (graphic is Image face)
                {
                    Sprite sprite = face.sprite;
                    source = sprite.name + ":" + ArtMemoryGeometry(sprite.rect.x, sprite.rect.y, sprite.rect.width, sprite.rect.height,
                        sprite.border.x, sprite.border.y, sprite.border.z, sprite.border.w, sprite.pivot.x, sprite.pivot.y);
                }
                return graphic.name + ":" + texture.name + ":" + texture.width + "x" + texture.height + ":" + source;
            }).OrderBy(signature => signature, StringComparer.Ordinal).ToArray();

        private static string ArtMemoryGeometry(params float[] values) =>
            string.Join(",", values.Select(value => value.ToString("R", CultureInfo.InvariantCulture)));

        private static void AssertBattleArtMemoryTexturesGone(int[] ids)
        {
            int[] loaded = Resources.FindObjectsOfTypeAll<Texture2D>().Select(texture => texture.GetInstanceID()).ToArray();
            Assert.That(ids.Intersect(loaded), Is.Empty, "Textures with no remaining live owner must leave the Resources object inventory.");
        }

        private static IEnumerator CollectGameplayBattleArtForTest(CoreGameplayDemoController controller)
        {
            typeof(CoreGameplayDemoController).GetMethod("QueueUnusedArtCollection", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { true });
            yield return AwaitTransientMemoryCollection(controller);
        }
    }
}
