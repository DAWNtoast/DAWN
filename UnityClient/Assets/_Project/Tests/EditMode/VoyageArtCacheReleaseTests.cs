using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class VoyageArtCacheReleaseTests
    {
        [Test]
        public void VoyageConstructionAndCounts_DoNotLoadAnyTexture()
        {
            var requests = new List<string>();
            var catalog = Create<VoyageMapArtCatalog>(path => { requests.Add(path); return null; });
            Assert.That(catalog.LoadedTextureCount, Is.Zero);
            Assert.That(catalog.WaterVariantCount, Is.EqualTo(3));
            Assert.That(catalog.TerrainFor(SeaTerrainType.OpenWater), Is.Null);
            Assert.That(catalog.TerrainFor((SeaTerrainType)int.MaxValue), Is.Null);
            catalog.ReleaseLoadedTextures();
            Assert.That(requests, Is.Empty);
        }

        [Test]
        public void VoyageGetters_LoadOnlyRequestedWaterAndTerrainAndPreserveClampingAndAliases()
        {
            var requests = new List<string>();
            var water = new Texture2D(2, 2);
            var land = new Texture2D(2, 2);
            try
            {
                var catalog = Create<VoyageMapArtCatalog>(path =>
                {
                    requests.Add(path);
                    return path.Contains("terrain_") ? land : water;
                });
                Assert.That(catalog.WaterVariant(int.MinValue), Is.SameAs(water));
                Assert.That(catalog.WaterVariant(0), Is.SameAs(water));
                Assert.That(water.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(catalog.WaterVariant(int.MaxValue), Is.SameAs(water));
                Assert.That(catalog.WaterVariant(2), Is.SameAs(water));
                Assert.That(catalog.TerrainFor(SeaTerrainType.Beach), Is.SameAs(land));
                Assert.That(catalog.TerrainFor(SeaTerrainType.Shoal), Is.SameAs(land));
                Assert.That(land.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(catalog.BeachLandOverlay, Is.SameAs(land));
                Assert.That(catalog.TerrainFor(SeaTerrainType.Island), Is.SameAs(land));
                Assert.That(catalog.LoadedTextureCount, Is.EqualTo(4));
                CollectionAssert.AreEqual(new[]
                {
                    "VoyageMapV2/water_open_a_v2", "VoyageMapV2/water_open_c_v2",
                    "VoyageMapV2/terrain_shoal_v2", "VoyageMapV2/terrain_island_v2",
                }, requests);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(water);
                UnityEngine.Object.DestroyImmediate(land);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VoyageAvailability_ExplicitlyChecksAllTwelveResourcesAndReloadsAfterRelease(bool missingGround)
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<VoyageMapArtCatalog>(path =>
                {
                    requests.Add(path);
                    return missingGround && path == "VoyageMapV2/terrain_ground_detail_v3" ? null : texture;
                });
                Assert.That(catalog.IsAvailable, Is.EqualTo(!missingGround));
                Assert.That(catalog.IsAvailable, Is.EqualTo(!missingGround));
                Assert.That(catalog.LoadedTextureCount, Is.EqualTo(missingGround ? 11 : 12));
                Assert.That(requests.Count, Is.EqualTo(12));
                Assert.That(new HashSet<string>(requests).Count, Is.EqualTo(12));
                catalog.ReleaseLoadedTextures();
                Assert.That(catalog.LoadedTextureCount, Is.Zero);
                Assert.That(texture == null, Is.False);
                Assert.That(catalog.IsAvailable, Is.EqualTo(!missingGround));
                Assert.That(requests.Count, Is.EqualTo(24));
                Assert.That(catalog.LoadedTextureCount, Is.EqualTo(missingGround ? 11 : 12));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void MissingPreferredWater_FallsBackAndCachesMissingAttemptsUntilRelease()
        {
            const uint hash = 77u;
            const int column = 3, row = 2;
            string[] paths = { "VoyageMapV2/water_open_a_v2", "VoyageMapV2/water_open_b_v2", "VoyageMapV2/water_open_c_v2" };
            int preferred = (int)(VoyageMapArtCatalog.VisualHash(hash, column, row, 0xA341316Cu) % 3u);
            string fallbackPath = paths[(preferred + 1) % paths.Length];
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<VoyageMapArtCatalog>(path => { requests.Add(path); return path == fallbackPath ? texture : null; });
                Assert.That(catalog.WaterAt(hash, column, row), Is.SameAs(texture));
                Assert.That(requests[0], Is.EqualTo(paths[preferred]));
                Assert.That(requests, Does.Contain(fallbackPath));
                int initialRequests = requests.Count;
                Assert.That(catalog.WaterAt(hash, column, row), Is.SameAs(texture));
                Assert.That(requests.Count, Is.EqualTo(initialRequests));
                Assert.That(catalog.LoadedTextureCount, Is.EqualTo(1));
                catalog.ReleaseLoadedTextures();
                Assert.That(catalog.LoadedTextureCount, Is.Zero);
                Assert.That(catalog.WaterAt(hash, column, row), Is.SameAs(texture));
                Assert.That(requests.Count, Is.EqualTo(initialRequests * 2));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void VoyageTextureRelease_PreservesManagedCoastGeometryAndBorrowedTexture()
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<VoyageMapArtCatalog>(path => { requests.Add(path); return texture; });
                var map = new CoreVoyageMap { Hash = 53u, Seed = "RELEASE-COAST-CACHE-53" };
                map.TerrainCells[12 * CoreVoyageMap.GridColumns + 18] = SeaTerrainType.Island;
                VoyageChartCoastField coast = catalog.GetCoastField(map);
                Assert.That(requests, Is.Empty);
                Assert.That(catalog.ChartBase, Is.SameAs(texture));
                catalog.ReleaseLoadedTextures();
                catalog.ReleaseLoadedTextures();
                Assert.That(catalog.GetCoastField(map), Is.SameAs(coast));
                Assert.That(catalog.GetCoastField(map).Pixels, Is.SameAs(coast.Pixels));
                Assert.That(catalog.LoadedTextureCount, Is.Zero);
                Assert.That(texture == null, Is.False);
                Assert.That(catalog.ChartBase, Is.SameAs(texture));
                Assert.That(requests.Count, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void FacilityRelease_ResetsGenericAndRegionalSuccessMissingFlagsAndLoadedCount()
        {
            const string whiteShell = "PortFacilities/Regional/Tavern/tavern_white_shell_v1";
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            bool whiteShellMissing = true;
            try
            {
                var catalog = Create<PortFacilityArtCatalog>(path =>
                {
                    requests.Add(path);
                    return whiteShellMissing && path == whiteShell ? null : texture;
                });
                Assert.That(catalog.Background(2, "断锚港"), Is.SameAs(texture));
                Assert.That(catalog.Background(2, "白贝港"), Is.SameAs(texture));
                Assert.That(catalog.HasRegionalBackground(2, "白贝港"), Is.False);
                Assert.That(catalog.Background(0), Is.SameAs(texture));
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.EqualTo(1));
                Assert.That(requests.Count, Is.EqualTo(4));
                catalog.ReleaseLoadedTextures();
                catalog.ReleaseLoadedTextures();
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.Zero);
                Assert.That(texture == null, Is.False);
                Assert.That(requests.Count, Is.EqualTo(4));
                whiteShellMissing = false;
                Assert.That(catalog.Background(2, "白贝港"), Is.SameAs(texture));
                Assert.That(catalog.HasRegionalBackground(2, "白贝港"), Is.True);
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.EqualTo(1));
                Assert.That(catalog.Background(0), Is.SameAs(texture));
                Assert.That(requests.Count, Is.EqualTo(6));
                Assert.That(requests[4], Is.EqualTo(whiteShell));
                Assert.That(requests[5], Is.EqualTo("PortFacilities/warehouse_background"));
                Assert.That(PortFacilityArtCatalog.SupportedRegionalPorts.Count, Is.EqualTo(6));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void BoardingRelease_ClearsEveryCachedReferenceAndFlagThenRebindsRealResources()
        {
            BoardingBattleArtCatalog.ReleaseLoadedTextures();
            var texture = new Texture2D(2, 2);
            Type type = typeof(BoardingBattleArtCatalog);
            Texture2D[] backgrounds = StaticField<Texture2D[]>(type, "Backgrounds");
            bool[] backgroundLoaded = StaticField<bool[]>(type, "BackgroundLoaded");
            Texture2D[] parties = StaticField<Texture2D[]>(type, "Parties");
            bool[] partyLoaded = StaticField<bool[]>(type, "PartyLoaded");
            var cast = StaticField<Dictionary<string, Texture2D>>(type, "Cast");
            const string enemy = "BoardingBattle/Characters/boarding_enemy_party_v1";
            try
            {
                Array.Fill(backgrounds, texture);
                Array.Fill(backgroundLoaded, true);
                Array.Fill(parties, texture);
                Array.Fill(partyLoaded, true);
                cast.Add(enemy, texture);
                cast.Add("previously_missing_cast", null);
                Assert.That(BoardingBattleArtCatalog.ForEnvironment(SeaTerrainType.OpenWater, NavigationTimeOfDay.Day), Is.SameAs(texture));
                Assert.That(BoardingBattleArtCatalog.ForParty(false), Is.SameAs(texture));
                Assert.That(BoardingBattleArtCatalog.ForEnemy(null, null, 0), Is.SameAs(texture));
                BoardingBattleArtCatalog.ReleaseLoadedTextures();
                Assert.That(backgrounds, Is.All.Null);
                Assert.That(backgroundLoaded, Is.All.False);
                Assert.That(parties, Is.All.Null);
                Assert.That(partyLoaded, Is.All.False);
                Assert.That(cast, Is.Empty);
                Assert.That(texture == null, Is.False);
                Texture2D realDeck = Resources.Load<Texture2D>(BoardingBattleArtCatalog.EnvironmentResourcePath(SeaTerrainType.OpenWater, NavigationTimeOfDay.Day));
                Assert.That(realDeck, Is.Not.Null);
                Assert.That(BoardingBattleArtCatalog.ForEnvironment(SeaTerrainType.OpenWater, NavigationTimeOfDay.Day), Is.SameAs(realDeck));
                Assert.That(backgroundLoaded[0], Is.True);
                Texture2D realParty = Resources.Load<Texture2D>("BoardingBattle/Characters/boarding_player_party_v1");
                Assert.That(realParty, Is.Not.Null);
                Assert.That(BoardingBattleArtCatalog.ForParty(false), Is.SameAs(realParty));
                Texture2D realEnemy = Resources.Load<Texture2D>(enemy);
                Assert.That(realEnemy, Is.Not.Null);
                Assert.That(BoardingBattleArtCatalog.ForEnemy(null, null, 0), Is.SameAs(realEnemy));
            }
            finally
            {
                BoardingBattleArtCatalog.ReleaseLoadedTextures();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void LookoutRelease_ClearsAllMissingFlagsAndPreservesLiveBorrowedPanorama()
        {
            NavigationLookoutArtCatalog.ReleaseLoadedTextures();
            var texture = new Texture2D(2, 2);
            Type type = typeof(NavigationLookoutArtCatalog);
            Texture2D[] textures = StaticField<Texture2D[]>(type, "textures");
            bool[] attempted = StaticField<bool[]>(type, "attempted");
            try
            {
                // Simulate a cached missing fog panorama falling back to a still-live clear view.
                textures[0] = texture;
                Array.Fill(attempted, true);
                Assert.That(NavigationLookoutArtCatalog.ForEnvironment(SeaTerrainType.FogBank, NavigationTimeOfDay.Night), Is.SameAs(texture));
                NavigationLookoutArtCatalog.ReleaseLoadedTextures();
                Assert.That(textures, Is.All.Null);
                Assert.That(attempted, Is.All.False);
                Assert.That(texture == null, Is.False);
                Texture2D real = Resources.Load<Texture2D>(NavigationLookoutArtCatalog.EnvironmentResourcePath(SeaTerrainType.FogBank, NavigationTimeOfDay.Night));
                Assert.That(real, Is.Not.Null);
                Assert.That(NavigationLookoutArtCatalog.ForEnvironment(SeaTerrainType.FogBank, NavigationTimeOfDay.Night), Is.SameAs(real));
                Assert.That(attempted[5], Is.True);
            }
            finally
            {
                NavigationLookoutArtCatalog.ReleaseLoadedTextures();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static T StaticField<T>(Type type, string name) =>
            (T)type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static T Create<T>(Func<string, Texture2D> loader)
        {
            ConstructorInfo constructor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(Func<string, Texture2D>) }, null);
            Assert.That(constructor, Is.Not.Null);
            return (T)constructor.Invoke(new object[] { loader });
        }
    }
}
