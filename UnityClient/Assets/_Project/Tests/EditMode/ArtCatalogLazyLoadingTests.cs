using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ArtCatalogLazyLoadingTests
    {
        [TestCase(typeof(LegacyDemoArtCatalog))]
        [TestCase(typeof(PortArtCatalog))]
        [TestCase(typeof(PortMapIconCatalog))]
        [TestCase(typeof(BattleEnvironmentArtCatalog))]
        [TestCase(typeof(PortHeaderArtCatalog))]
        public void Construction_DoesNotRequestTextures(Type type)
        {
            var requests = new List<string>();
            object catalog = Create(type, path => { requests.Add(path); return null; });
            if (catalog is PortArtCatalog port)
            {
                Assert.That(port.PortBackgroundCount, Is.EqualTo(6));
                Assert.That(port.CrewFullBodyCount, Is.EqualTo(6));
                port.ShipUv(11);
                port.SpecialCrewUv(5);
            }
            if (catalog is PortMapIconCatalog icons) Assert.That(icons.Count, Is.EqualTo(6));
            if (catalog is BattleEnvironmentArtCatalog battle) Assert.That(battle.Count, Is.EqualTo(6));
            if (catalog is IDisposable disposable) disposable.Dispose();
            Assert.That(requests, Is.Empty);
        }

        [TestCase(typeof(LegacyDemoArtCatalog))]
        [TestCase(typeof(PortArtCatalog))]
        [TestCase(typeof(PortMapIconCatalog))]
        [TestCase(typeof(BattleEnvironmentArtCatalog))]
        public void ReleaseLoadedTextures_DropsSuccessAndMissingCachesWithoutDestroyingBorrowedArt(Type type)
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            bool missing = false;
            object catalog = Create(type, path => { requests.Add(path); return missing ? null : texture; });
            try
            {
                Assert.That(RequestOneTexture(catalog), Is.SameAs(texture));
                Assert.That(RequestOneTexture(catalog), Is.SameAs(texture));
                Assert.That(requests.Count, Is.EqualTo(1));
                ReleaseTextures(catalog);
                ReleaseTextures(catalog);
                Assert.That(texture == null, Is.False);
                Assert.That(requests.Count, Is.EqualTo(1), "Releasing references must not request new art.");
                missing = true;
                Assert.That(RequestOneTexture(catalog), Is.Null);
                Assert.That(RequestOneTexture(catalog), Is.Null);
                Assert.That(requests.Count, Is.EqualTo(2), "Missing resources are cached until the next release.");
                missing = false;
                ReleaseTextures(catalog);
                Assert.That(RequestOneTexture(catalog), Is.SameAs(texture));
                Assert.That(requests.Count, Is.EqualTo(3));
                Assert.That(new HashSet<string>(requests).Count, Is.EqualTo(1));
                if (catalog is PortArtCatalog port)
                {
                    Assert.That(port.PortBackgroundCount, Is.EqualTo(6));
                    Assert.That(port.CrewFullBodyCount, Is.EqualTo(6));
                }
                if (catalog is PortMapIconCatalog icons) Assert.That(icons.Count, Is.EqualTo(6));
                if (catalog is BattleEnvironmentArtCatalog battle) Assert.That(battle.Count, Is.EqualTo(6));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PortRelease_AlsoDropsRegionalPortraitAndAtlasReferences()
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<PortArtCatalog>(path => { requests.Add(path); return texture; });
                Assert.That(catalog.CrewFullBody("regional_test", 0), Is.SameAs(texture));
                Assert.That(catalog.RegionalCrewAtlas("白贝港"), Is.SameAs(texture));
                Assert.That(catalog.HasRegionalCrewFullBody("regional_test"), Is.True);
                catalog.ReleaseLoadedTextures();
                Assert.That(texture == null, Is.False);
                Assert.That(catalog.CrewFullBody("regional_test", 0), Is.SameAs(texture));
                Assert.That(catalog.RegionalCrewAtlas("白贝港"), Is.SameAs(texture));
                CollectionAssert.AreEqual(new[]
                {
                    "PortArt/CrewRegional/regional_test", "PortArt/CrewRegional/crew_white_shell_portraits_v2",
                    "PortArt/CrewRegional/regional_test", "PortArt/CrewRegional/crew_white_shell_portraits_v2",
                }, requests);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void LegacyRoutes_ClampAndReuseOnlyRequestedMapsAndSheets()
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<LegacyDemoArtCatalog>(path => { requests.Add(path); return texture; });
                Assert.That(catalog.RouteMap(int.MinValue), Is.SameAs(texture));
                Assert.That(catalog.RouteMap(0), Is.SameAs(texture));
                Assert.That(catalog.RouteMap(int.MaxValue), Is.SameAs(texture));
                Assert.That(catalog.RouteMap(2), Is.SameAs(texture));
                Assert.That(catalog.EventSheet(-1), Is.SameAs(texture));
                Assert.That(catalog.EventSheet(0), Is.SameAs(texture));
                CollectionAssert.AreEqual(new[]
                {
                    "LegacyDemo/shattered_sail_route_map_v2", "LegacyDemo/moon_tide_route_map_v2",
                    "LegacyDemo/shattered_sail_event_sheet_v2",
                }, requests);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PaintedEventSheets_UseSmoothClampedSamplingForDetailViews()
        {
            var texture = new Texture2D(10, 8) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            try
            {
                var catalog = Create<LegacyDemoArtCatalog>(path => texture);
                for (int route = 0; route < 3; route++)
                {
                    Assert.That(catalog.EventSheet(route).filterMode, Is.EqualTo(FilterMode.Bilinear));
                    Assert.That(catalog.EventSheet(route).wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void MissingLegacyCardSheet_KeepsCellFallbackWithoutRepeatedLoadAttempts()
        {
            var requests = new List<string>();
            var catalog = Create<LegacyDemoArtCatalog>(path => { requests.Add(path); return null; });
            Assert.That(catalog.CardUv("unknown"), Is.EqualTo(LegacyDemoArtCatalog.CellUv(1, 2, 4, 4)));
            Assert.That(catalog.CardUv("card_chain_shot"), Is.EqualTo(LegacyDemoArtCatalog.CellUv(1, 0, 4, 4)));
            Assert.That(catalog.CardSheet, Is.Null);
            CollectionAssert.AreEqual(new[] { "LegacyDemo/card_icon_sheet" }, requests);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyAvailability_ExplicitlyChecksAllTenResourcesOnce(bool missingLaterMap)
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<LegacyDemoArtCatalog>(path =>
                {
                    requests.Add(path);
                    return missingLaterMap && path == "LegacyDemo/moon_tide_route_map_v2" ? null : texture;
                });
                Assert.That(catalog.IsAvailable, Is.EqualTo(!missingLaterMap));
                Assert.That(catalog.IsAvailable, Is.EqualTo(!missingLaterMap));
                Assert.That(requests.Count, Is.EqualTo(10));
                Assert.That(new HashSet<string>(requests).Count, Is.EqualTo(10));
                Assert.That(requests, Does.Contain("LegacyDemo/moon_tide_event_sheet_v2"));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PortLookups_PreserveFallbackAndKeepOtherPortsShipsAndCrewUnloaded()
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<PortArtCatalog>(path => { requests.Add(path); return texture; });
                Assert.That(catalog.PortBackgroundFor(null), Is.SameAs(texture));
                Assert.That(catalog.PortBackgroundFor("未知港口"), Is.SameAs(texture));
                Assert.That(catalog.PortBackgroundFor("断锚港"), Is.SameAs(texture));
                Assert.That(catalog.ShipTexture(-1), Is.SameAs(texture));
                Assert.That(catalog.ShipSheet, Is.SameAs(texture));
                Assert.That(catalog.ShipTexture(4), Is.SameAs(texture));
                Assert.That(catalog.ShipTexture(11), Is.SameAs(texture));
                Assert.That(catalog.CrewFullBody(int.MinValue), Is.SameAs(texture));
                Assert.That(catalog.CrewFullBody(0), Is.SameAs(texture));
                CollectionAssert.AreEqual(new[]
                {
                    "PortArt/port_hub_broken_anchor_v3", "PortArt/ship_roster_sheet",
                    "LegacyDemo/ship_sprite_sheet", "PortArt/CrewFullBody/crew_navigator_evelyn_fullbody",
                }, requests);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void CrewDossiers_LoadOnlyTheRequestedIdentityAndReleaseBorrowedReferences()
        {
            var requests = new List<string>();
            var lorne = new Texture2D(2, 2);
            var saina = new Texture2D(2, 2);
            const string lorneId = "crew_broken_anchor_navigator_lorne";
            const string sainaId = "crew_white_shell_navigator_saina";
            string lornePath = "PortArt/CrewDossiers/" + lorneId + "_dossier_v1";
            string sainaPath = "PortArt/CrewDossiers/" + sainaId + "_dossier_v1";
            try
            {
                var catalog = Create<PortArtCatalog>(path =>
                {
                    requests.Add(path);
                    return path == lornePath ? lorne : path == sainaPath ? saina : null;
                });
                Assert.That(requests, Is.Empty);
                Assert.That(catalog.CrewDossier(lorneId), Is.SameAs(lorne));
                Assert.That(catalog.CrewDossier(lorneId), Is.SameAs(lorne));
                CollectionAssert.AreEqual(new[] { lornePath }, requests);
                Assert.That(catalog.CrewDossier(sainaId), Is.SameAs(saina));
                CollectionAssert.AreEqual(new[] { lornePath, sainaPath }, requests);
                Assert.That(lorne.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(lorne.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                catalog.ReleaseLoadedTextures();
                Assert.That(lorne != null && saina != null, Is.True, "The visible UI borrows these assets.");
                Assert.That(catalog.CrewDossier(lorneId), Is.SameAs(lorne));
                CollectionAssert.AreEqual(new[] { lornePath, sainaPath, lornePath }, requests);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lorne);
                UnityEngine.Object.DestroyImmediate(saina);
            }
        }

        [Test]
        public void MissingRegionalCrew_UsesItsLegacyFallbackAndCachesBothMissingPaths()
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            const string fallback = "PortArt/CrewFullBody/crew_diver_noya_fullbody";
            try
            {
                var catalog = Create<PortArtCatalog>(path => { requests.Add(path); return path == fallback ? texture : null; });
                Assert.That(catalog.CrewFullBody("missing_portrait", int.MaxValue), Is.SameAs(texture));
                Assert.That(catalog.HasRegionalCrewFullBody("missing_portrait"), Is.False);
                Assert.That(catalog.CrewFullBody("missing_portrait", 5), Is.SameAs(texture));
                CollectionAssert.AreEqual(new[]
                {
                    "PortArt/CrewRegional/missing_portrait", "PortArt/CrewFullBody/missing_portrait", fallback,
                }, requests);
                Assert.That(catalog.RegionalCrewAtlas(null), Is.Null);
                Assert.That(catalog.RegionalCrewAtlas("未知港口"), Is.Null);
                Assert.That(requests.Count, Is.EqualTo(4));
                Assert.That(requests[3], Is.EqualTo("PortArt/CrewRegional/crew_gray_gull_portraits_v2"));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PortAvailability_ExplicitlyChecksAllNineteenBaseResourcesOnce(bool missingCrew)
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<PortArtCatalog>(path =>
                {
                    requests.Add(path);
                    return missingCrew && path == "PortArt/CrewFullBody/crew_diver_noya_fullbody" ? null : texture;
                });
                Assert.That(catalog.IsAvailable, Is.EqualTo(!missingCrew));
                Assert.That(catalog.IsAvailable, Is.EqualTo(!missingCrew));
                Assert.That(requests.Count, Is.EqualTo(19));
                Assert.That(new HashSet<string>(requests).Count, Is.EqualTo(19));
                Assert.That(requests, Does.Contain("PortArt/port_hub_gray_gull_v3"));
                Assert.That(catalog.PortBackgroundCount, Is.EqualTo(6));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void MapIcons_UnknownPortUsesBrokenAnchorAndMissingKnownPortDoesNotBorrowArt()
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<PortMapIconCatalog>(path =>
                {
                    requests.Add(path);
                    return path == "PortIcons/port_broken_anchor_marker_v1" ? texture : null;
                });
                Assert.That(catalog.Get("未知港口"), Is.SameAs(texture));
                Assert.That(catalog.Get(null), Is.SameAs(texture));
                Assert.That(catalog.Get("断锚港"), Is.SameAs(texture));
                Assert.That(catalog.Get("白贝港"), Is.Null);
                Assert.That(catalog.Get("白贝港"), Is.Null);
                Assert.That(catalog.Count, Is.EqualTo(6));
                CollectionAssert.AreEqual(new[]
                    { "PortIcons/port_broken_anchor_marker_v1", "PortIcons/port_white_shell_marker_v1" }, requests);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void BattleEnvironment_TerrainAliasesShareOneLoadAndUnknownTerrainUsesOpenSea()
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                var catalog = Create<BattleEnvironmentArtCatalog>(path =>
                {
                    requests.Add(path);
                    return path == "BattleEnvironments/battle_coral_reef_v1" ? null : texture;
                });
                Assert.That(catalog.ForTerrain(SeaTerrainType.Island), Is.SameAs(texture));
                Assert.That(catalog.ForTerrain(SeaTerrainType.Shoal), Is.SameAs(texture));
                Assert.That(catalog.ForTerrain(SeaTerrainType.Beach), Is.SameAs(texture));
                Assert.That(catalog.ForTerrain((SeaTerrainType)int.MaxValue), Is.SameAs(texture));
                Assert.That(catalog.ForTerrain(SeaTerrainType.Reef), Is.Null);
                Assert.That(catalog.ForTerrain(SeaTerrainType.Reef), Is.Null);
                Assert.That(catalog.Count, Is.EqualTo(6));
                CollectionAssert.AreEqual(new[]
                {
                    "BattleEnvironments/battle_beach_cove_v1", "BattleEnvironments/battle_open_ocean_v1",
                    "BattleEnvironments/battle_coral_reef_v1",
                }, requests);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void PortHeaders_OnlyCreateRequestedFrameAndDisposeOwnedSpritesWithoutDestroyingBorrowedTexture()
        {
            var requests = new List<string>();
            var texture = new Texture2D(128, 128);
            var catalog = Create<PortHeaderArtCatalog>(path => { requests.Add(path); return texture; });
            try
            {
                PortHeaderTheme theme = catalog.ForPort("断锚港");
                Assert.That(catalog.ForPort("断锚港"), Is.SameAs(theme));
                Sprite frame = theme.Frame;
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.texture, Is.SameAs(texture));
                Assert.That(frame.rect.height, Is.EqualTo(128f * 378f / 1214f).Within(.001f));
                Assert.That(frame.border.x, Is.EqualTo(frame.rect.width * .13f).Within(.001f));
                Assert.That(frame.border.y, Is.EqualTo(frame.rect.height * .29f).Within(.001f));
                CollectionAssert.AreEqual(new[] { "PortHeaders/broken_anchor_header_v1" }, requests);
                catalog.Dispose();
                Assert.That(frame == null, Is.True, "Only the catalog-owned runtime sprite should be destroyed.");
                Assert.That(texture == null, Is.False, "The shared Resources texture is borrowed.");
                catalog.Dispose();
                Assert.That(requests.Count, Is.EqualTo(1));
            }
            finally
            {
                catalog.Dispose();
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MissingPortHeader_UnknownPortUsesGrayGullThemeWithoutRetrying()
        {
            var requests = new List<string>();
            var catalog = Create<PortHeaderArtCatalog>(path => { requests.Add(path); return null; });
            try
            {
                PortHeaderTheme fallback = catalog.ForPort(null);
                Assert.That(catalog.ForPort("未知港口"), Is.SameAs(fallback));
                Assert.That(catalog.ForPort("灰鸥港"), Is.SameAs(fallback));
                Assert.That(fallback.Frame, Is.Null);
                Assert.That(fallback.FrameHeight, Is.EqualTo(148f));
                ColorUtility.TryParseHtmlString("#EBD0A0", out Color accent);
                Assert.That(fallback.Accent, Is.EqualTo(accent));
                CollectionAssert.AreEqual(new[] { "PortHeaders/gray_gull_header_v1" }, requests);
            }
            finally { catalog.Dispose(); }
        }

        [Test]
        public void PortHeaders_SuppliedNavigationFrameKeepsRegionalAccentsWithoutLoadingRegionalTextures()
        {
            var requests = new List<string>();
            var texture = new Texture2D(128, 128);
            Sprite navigation = Sprite.Create(texture, new Rect(0, 0, 128, 128), Vector2.one * .5f);
            var catalog = Create<PortHeaderArtCatalog>(path => { requests.Add(path); return texture; });
            try
            {
                foreach (string port in RegionalShopFurniture.Ports)
                {
                    PortHeaderTheme supplied = catalog.ForPort(port, navigation);
                    Assert.That(supplied.Frame, Is.SameAs(navigation));
                    Assert.That(supplied.FrameHeight, Is.EqualTo(128));
                }
                Assert.That(requests, Is.Empty, "Navigation must not load regional textures it immediately replaces.");
                foreach (string port in RegionalShopFurniture.Ports)
                    Assert.That(catalog.ForPort(port, navigation).Accent, Is.EqualTo(catalog.ForPort(port).Accent));
                Assert.That(requests.Count, Is.EqualTo(6), "A missing supplied frame retains the original regional loading path.");
                catalog.Dispose();
                Assert.That(navigation != null, Is.True, "The supplied frame belongs to its own skin, not this catalog.");
            }
            finally
            {
                catalog.Dispose();
                UnityEngine.Object.DestroyImmediate(navigation);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void PortHeaders_ReleaseUnusedThemesUnpinsBorrowedFramesAndKeepsTheCurrentPort()
        {
            var texture = new Texture2D(128, 128);
            var catalog = Create<PortHeaderArtCatalog>(_ => texture);
            Sprite oldFrame = null;
            try
            {
                oldFrame = catalog.ForPort("断锚港").Frame;
                PortHeaderTheme current = catalog.ForPort("白贝港");
                catalog.ReleaseUnusedThemes("白贝港");
                Assert.That(catalog.ForPort("白贝港"), Is.SameAs(current));
                Assert.That(oldFrame != null, Is.True, "An Image may still own this frame; release must never destroy it.");
                Assert.That(oldFrame.hideFlags & HideFlags.DontUnloadUnusedAsset, Is.EqualTo(HideFlags.None));
                Assert.That(current.Frame.hideFlags & HideFlags.DontUnloadUnusedAsset, Is.EqualTo(HideFlags.DontUnloadUnusedAsset));
                Assert.That(catalog.ForPort("断锚港").Frame, Is.Not.SameAs(oldFrame));
            }
            finally
            {
                catalog.Dispose();
                if (oldFrame != null) UnityEngine.Object.DestroyImmediate(oldFrame);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase("UI/PortHubHeader", true)]
        [TestCase("UI/PortFacilityHeader", false)]
        public void PortHeaderPrefabs_DoNotPinAnyDefaultRegionalFrame(string path, bool hub)
        {
            PortHeaderView prefab = Resources.Load<PortHeaderView>(path);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.IsComplete(hub), Is.True);
            Assert.That(prefab.Frame.sprite, Is.Null, "The controller binds the current region before making the view visible.");
        }

        private static T Create<T>(Func<string, Texture2D> loader) => (T)Create(typeof(T), loader);

        private static Texture2D RequestOneTexture(object catalog)
        {
            if (catalog is LegacyDemoArtCatalog legacy) return legacy.ShipSheet;
            if (catalog is PortArtCatalog port) return port.PortBackground;
            if (catalog is PortMapIconCatalog icons) return icons.Get("断锚港");
            return ((BattleEnvironmentArtCatalog)catalog).ForTerrain(SeaTerrainType.Reef);
        }

        private static void ReleaseTextures(object catalog)
        {
            if (catalog is LegacyDemoArtCatalog legacy) legacy.ReleaseLoadedTextures();
            else if (catalog is PortArtCatalog port) port.ReleaseLoadedTextures();
            else if (catalog is PortMapIconCatalog icons) icons.ReleaseLoadedTextures();
            else ((BattleEnvironmentArtCatalog)catalog).ReleaseLoadedTextures();
        }

        private static object Create(Type type, Func<string, Texture2D> loader)
        {
            ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(Func<string, Texture2D>) }, null);
            Assert.That(constructor, Is.Not.Null, "Expected the isolated resource loader seam.");
            return constructor.Invoke(new object[] { loader });
        }
    }
}
