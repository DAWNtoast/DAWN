using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class PortFacilityArtCatalogLazyTests
    {
        [Test]
        public void ConstructionAndUnsupportedRegionalQueries_DoNotLoadAnyResource()
        {
            var requests = new List<string>();
            PortFacilityArtCatalog catalog = Create(path => { requests.Add(path); return null; });
            Assert.That(catalog.LoadedRegionalBackgroundCount, Is.Zero);
            Assert.That(catalog.RegionalBackground(2, null), Is.Null);
            Assert.That(catalog.RegionalBackground(2, "未知港口"), Is.Null);
            Assert.That(catalog.RegionalBackground(1, "断锚港"), Is.Null);
            Assert.That(requests, Is.Empty);
        }

        [Test]
        public void GenericBackground_ClampsAndCachesSuccessAndMissingResults()
        {
            var requests = new List<string>();
            var warehouse = new Texture2D(2, 2);
            try
            {
                PortFacilityArtCatalog catalog = Create(path =>
                {
                    requests.Add(path);
                    return path == "PortFacilities/warehouse_background" ? warehouse : null;
                });
                Assert.That(catalog.Background(-100), Is.SameAs(warehouse));
                Assert.That(catalog.Background(0), Is.SameAs(warehouse));
                Assert.That(catalog.Background(int.MaxValue), Is.Null);
                Assert.That(catalog.Background(8), Is.Null);
                CollectionAssert.AreEqual(new[] { "PortFacilities/warehouse_background", "PortFacilities/Market/market_scene_background_v2" }, requests);
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(warehouse); }
        }

        [TestCase(PortFacilityArtCatalog.WarehouseFacilityIndex, "Warehouse/warehouse_", "warehouse_background")]
        [TestCase(PortFacilityArtCatalog.TavernFacilityIndex, "Tavern/tavern_", "tavern_background_seated_v2")]
        public void RegionalBackground_LoadsOnePortAndFallsBackWithoutBorrowingOrRetryingMissingArt(
            int facilityIndex, string regionalPrefix, string genericName)
        {
            string brokenAnchor = "PortFacilities/Regional/" + regionalPrefix + "broken_anchor_v1";
            string whiteShell = "PortFacilities/Regional/" + regionalPrefix + "white_shell_v1";
            string fallback = "PortFacilities/" + genericName;
            var requests = new List<string>();
            var regional = new Texture2D(2, 2);
            var generic = new Texture2D(2, 2);
            try
            {
                PortFacilityArtCatalog catalog = Create(path =>
                {
                    requests.Add(path);
                    return path == brokenAnchor ? regional : path == fallback ? generic : null;
                });
                Assert.That(catalog.Background(facilityIndex, "断锚港"), Is.SameAs(regional));
                Assert.That(catalog.HasRegionalBackground(facilityIndex, "断锚港"), Is.True);
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.EqualTo(1));
                CollectionAssert.AreEqual(new[] { brokenAnchor }, requests);
                Assert.That(catalog.Background(facilityIndex, "白贝港"), Is.SameAs(generic));
                Assert.That(catalog.Background(facilityIndex, "白贝港"), Is.SameAs(generic));
                Assert.That(catalog.HasRegionalBackground(facilityIndex, "白贝港"), Is.False);
                Assert.That(catalog.Background(facilityIndex, "未知港口"), Is.SameAs(generic));
                CollectionAssert.AreEqual(new[] { brokenAnchor, whiteShell, fallback }, requests);
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.EqualTo(1));

                catalog.ReleaseLoadedTextures();
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.Zero);
                Assert.That(regional == null, Is.False, "Live UI may still borrow a cached room texture.");
                Assert.That(catalog.Background(facilityIndex, "断锚港"), Is.SameAs(regional));
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.EqualTo(1));
                CollectionAssert.AreEqual(new[] { brokenAnchor, whiteShell, fallback, brokenAnchor }, requests);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(regional);
                UnityEngine.Object.DestroyImmediate(generic);
            }
        }

        [Test]
        public void ExplicitAvailabilityValidation_StillChecksAllThirtyNinePathsAndCachesThem()
        {
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                PortFacilityArtCatalog catalog = Create(path => { requests.Add(path); return texture; });
                Assert.That(requests, Is.Empty);
                Assert.That(catalog.AreAllRegionalBackgroundsAvailable, Is.True);
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.EqualTo(30));
                Assert.That(requests.Count, Is.EqualTo(30));
                Assert.That(catalog.MissingRegionalBackgroundPaths(), Is.Empty);
                Assert.That(catalog.IsAvailable, Is.True);
                Assert.That(requests.Count, Is.EqualTo(39));
                Assert.That(new HashSet<string>(requests, StringComparer.Ordinal).Count, Is.EqualTo(39));
                Assert.That(catalog.AreAllRegionalBackgroundsAvailable && catalog.IsAvailable, Is.True);
                Assert.That(requests.Count, Is.EqualTo(39));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public void ExplicitRegionalValidation_ReportsTheExactMissingPathWithoutRetryingIt()
        {
            const string missing = "PortFacilities/Regional/Maintenance/maintenance_gray_gull_side_v2";
            var requests = new List<string>();
            var texture = new Texture2D(2, 2);
            try
            {
                PortFacilityArtCatalog catalog = Create(path => { requests.Add(path); return path == missing ? null : texture; });
                Assert.That(catalog.AreAllRegionalBackgroundsAvailable, Is.False);
                Assert.That(catalog.LoadedRegionalBackgroundCount, Is.EqualTo(29));
                CollectionAssert.AreEqual(new[] { missing }, catalog.MissingRegionalBackgroundPaths());
                Assert.That(catalog.AreAllRegionalBackgroundsAvailable, Is.False);
                Assert.That(requests.Count, Is.EqualTo(30));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static PortFacilityArtCatalog Create(Func<string, Texture2D> loader)
        {
            ConstructorInfo constructor = typeof(PortFacilityArtCatalog).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Func<string, Texture2D>) }, null);
            Assert.That(constructor, Is.Not.Null);
            return (PortFacilityArtCatalog)constructor.Invoke(new object[] { loader });
        }
    }
}
