using System.Collections;
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
        public IEnumerator RuntimeMemory_CollectsOnlyOnViewChangesAndPreservesLiveMarketTextures()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return new WaitForSecondsRealtime(.8f);
            ActiveButton("Port Hotspot Market").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            int count = MemoryCollectionCount(controller);
            Assert.That(count, Is.GreaterThanOrEqualTo(2));
            yield return OpenMarketItem("water");
            Texture background = GameObject.Find("Market Background").GetComponent<RawImage>().texture;
            Texture icon = GameObject.Find("Market Item Icon water").GetComponent<RawImage>().texture;
            Assert.That(background, Is.Not.Null);
            Assert.That(icon, Is.Not.Null);
            yield return OpenMarketTrade("water");
            MarketQuantityField("water").value = 7;
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(MemoryCollectionCount(controller), Is.EqualTo(count), "Changing quantities in a retained view must not run asset scans.");
            Assert.That(background, Is.Not.Null, "Visible art is owned by live Graphics during unloading.");
            Assert.That(icon, Is.Not.Null);
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(MemoryCollectionCount(controller), Is.GreaterThan(count));
            ActiveButton("Port Hotspot Market").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            yield return OpenMarketItem("water");
            Assert.That(GameObject.Find("Market Item Icon water").GetComponent<RawImage>().texture, Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_ReleasesFurnitureAfterLeavingShopAndReturningToMenu()
        {
            yield return LoadProvisioningFixture();
            ActiveButton("Port Hotspot ComponentShop").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            var furniture = RegionalShopFurniture.For("断锚港");
            Texture coverage = furniture.Coverage;
            Material material = furniture.Material;
            Assert.That(material, Is.Not.Null);
            Assert.That(coverage, Is.Not.Null);
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(material == null, Is.True, "Generated material is destroyed once its shop is gone.");
            Assert.That(coverage == null, Is.True);
            ActiveButton("Port Hotspot ComponentShop").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            var reloaded = RegionalShopFurniture.For("断锚港");
            Assert.That(reloaded.Material, Is.Not.Null);
            Assert.That(reloaded.Coverage, Is.Not.Null);
            Material reopenedMaterial = reloaded.Material;
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(reopenedMaterial == null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_ReturnToMenuClearsIconCachesAndPreservesAnAlreadyOpenAchievementPage()
        {
            yield return LoadProvisioningFixture();
            Assert.That(ItemIconCatalog.ForCargo("water").IsAvailable, Is.True);
            Assert.That(ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[0]).IsAvailable, Is.True);
            Assert.That(ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[20]).IsAvailable, Is.True);
            Assert.That(AchievementIconCatalog.ForId("tutorial_departure").IsAvailable, Is.True);
            Assert.That(AchievementIconCatalog.ForId("invest_all_ports").IsAvailable, Is.True);
            Assert.That(AchievementIconCatalog.ForId("warehouse_display_six").IsAvailable, Is.True);
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            ActiveButton("Achievements").onClick.Invoke();
            var controller = Object.FindFirstObjectByType<MainMenuController>();
            RawImage[] badges = controller.GetComponentsInChildren<RawImage>()
                .Where(image => image.name.StartsWith("Achievement Icon ")).ToArray();
            Assert.That(badges.Length, Is.EqualTo(8));
            Texture[] visibleTextures = badges.Select(image => image.texture).ToArray();
            Assert.That(visibleTextures.All(texture => texture != null), Is.True);
            // Await the complete menu cleanup rather than guessing how long the
            // asynchronous engine scan takes on the test machine. The page is
            // already open before this instance reaches the cache release.
            yield return CollectMenuArtForTest();
            AssertIconCachesReleased(typeof(ItemIconCatalog), "atlas", "treasureAtlasA", "treasureAtlasB");
            AssertIconCachesReleased(typeof(AchievementIconCatalog), "atlasA", "atlasB", "atlasC");
            for (int index = 0; index < badges.Length; index++)
            {
                Assert.That(visibleTextures[index] != null, Is.True, badges[index].name);
                Assert.That(badges[index].texture, Is.SameAs(visibleTextures[index]), badges[index].name);
            }
            // The fifth page crosses into the second atlas after cleanup.
            for (int page = 0; page < 4; page++) ActiveButton("Achievements Next Page").onClick.Invoke();
            Assert.That(GameObject.Find("Achievements Page Indicator").GetComponent<Text>().text, Is.EqualTo("5 / " + Mathf.CeilToInt(AchievementCatalog.All.Count / 8f)));
            Assert.That(controller.GetComponentsInChildren<RawImage>()
                .Where(image => image.name.StartsWith("Achievement Icon ")).All(image => image.texture != null), Is.True);
            Assert.That(AchievementIconCatalog.ForId("invest_all_ports").IsAvailable, Is.True);
            Assert.That(ItemIconCatalog.ForCargo("water").IsAvailable, Is.True);
            Assert.That(ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[20]).IsAvailable, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_MenuCollectionPreservesLiveFacilityPaperAndReleasesItAfterTheViewCloses()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Sprite menuPaper = NauticalSurfaceSkin.ReadingPaper;
            var host = new GameObject("Retained Facility Paper Test", typeof(RectTransform), typeof(Image));
            Image image = host.GetComponent<Image>();
            image.sprite = NauticalSurfaceSkin.InvestmentPaper;
            int investmentId = image.sprite.GetInstanceID();
            int collectionId = NauticalSurfaceSkin.CollectionPaper.GetInstanceID();
            // Do not keep unused Sprite references across yields: coroutine
            // locals become fields on the test runner's retained IEnumerator.
            // Only the live Image should own investment art during this scan.
            try
            {
                yield return CollectMenuArtForTest();
                Assert.That(image.sprite != null, Is.True, "A live Image protects its paper during the engine scan.");
                Assert.That(image.sprite.GetInstanceID(), Is.EqualTo(investmentId));
                Assert.That(NauticalSurfaceSkin.IsPaperSurface(image.sprite), Is.True);
                Assert.That(NauticalSurfaceSkin.ReadableTextColor(image.transform, Color.white).grayscale, Is.LessThan(.25f));
                Assert.That(LoadedSpriteExists(collectionId), Is.False, "The unused generated collection sprite must no longer be pinned.");
                Assert.That(NauticalSurfaceSkin.ReadingPaper, Is.SameAs(menuPaper));
                Object.Destroy(host);
                image = null;
                host = null;
                yield return null;
                yield return CollectMenuArtForTest();
                Assert.That(LoadedSpriteExists(investmentId), Is.False, "The released sprite is collected when its last UI owner is gone.");
                Sprite reloaded = NauticalSurfaceSkin.InvestmentPaper;
                Assert.That(reloaded != null && reloaded.texture != null, Is.True);
                Assert.That(reloaded.border.x, Is.GreaterThan(0f));
                LogAssert.NoUnexpectedReceived();
            }
            finally { if (host != null) Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_MenuCollectionPreservesLiveNavigationArtThenReleasesItAndReopens()
        {
            yield return LoadNavigationInterfaceRefreshFixture();
            int[] original = NavigationArtSpriteIds();
            // A fast scene transition can overlap a live view with menu cleanup.
            // Only Images, not coroutine-held Sprite references, own this art.
            yield return CollectMenuArtForTest();
            CollectionAssert.AreEqual(original, NavigationArtSpriteIds(), "Live navigation art must survive cache release.");
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return CollectMenuArtForTest();
            foreach (int id in original)
                Assert.That(LoadedSpriteExists(id), Is.False, "Closed navigation sprites must no longer be pinned.");

            yield return LoadNavigationInterfaceRefreshFixture();
            int[] reopened = NavigationArtSpriteIds();
            Assert.That(reopened.Intersect(original), Is.Empty, "Reopening navigation reloads its collected sprites.");
            AssertNavigationInterfaceRefreshSkin("after menu cleanup and reopening");
            LogAssert.NoUnexpectedReceived();
        }

        private static int[] NavigationArtSpriteIds()
        {
            string[] names = { "Voyage Header Frame", "Voyage Header Compass", "Navigation Chart Timber Frame" };
            string[] textures = { "navigation_header_v3", "navigation_compass_v3", "navigation_panel_frame_v3" };
            var ids = new int[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                Image image = GameObject.Find(names[index]).GetComponent<Image>();
                Assert.That(image.sprite != null && image.sprite.texture != null, Is.True, names[index]);
                Assert.That(image.sprite.texture.name, Is.EqualTo(textures[index]));
                ids[index] = image.sprite.GetInstanceID();
            }
            return ids;
        }

        private static bool LoadedSpriteExists(int instanceId) => Resources.FindObjectsOfTypeAll<Sprite>()
            .Any(sprite => sprite.GetInstanceID() == instanceId);

        private static IEnumerator CollectMenuArtForTest() => (IEnumerator)typeof(CoreGameplayDemoController).Assembly
            .GetType("ShatteredSail.Presentation.RuntimeArtMemory")
            .GetMethod("CollectAfterReturningToMenu", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        private static void AssertIconCachesReleased(System.Type catalog, params string[] textures)
        {
            foreach (string field in textures)
            {
                Assert.That(catalog.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null, field);
                Assert.That(catalog.GetField(field + "Loaded", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.False, field);
            }
        }

        private static int MemoryCollectionCount(CoreGameplayDemoController controller) =>
            (int)typeof(CoreGameplayDemoController).GetField("runtimeArtCollectionCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);

        [UnityTest]
        public IEnumerator RuntimeMemory_ResumesCollectionAfterControllerDisableDuringPendingCleanup()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ActiveButton("Port Hotspot Market").onClick.Invoke();
            controller.gameObject.SetActive(false);
            yield return null;
            controller.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(.8f);
            int count = MemoryCollectionCount(controller);
            Assert.That(count, Is.GreaterThanOrEqualTo(1));
            yield return OpenMarketItem("water");
            Assert.That(GameObject.Find("Market Item Icon water").GetComponent<RawImage>().texture, Is.Not.Null);
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return null;
            ActiveButton("Port Hotspot Warehouse").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(MemoryCollectionCount(controller), Is.GreaterThan(count));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
