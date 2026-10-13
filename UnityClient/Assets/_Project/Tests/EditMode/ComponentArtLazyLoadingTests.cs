using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ComponentArtLazyLoadingTests
    {
        private Texture2D texture;
        private readonly List<string> requests = new List<string>();

        [SetUp]
        public void SetUp()
        {
            requests.Clear();
            texture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(texture);

        private ComponentArtCatalog CreateCatalog(bool missing = false)
        {
            Func<string, Texture2D> loader = path =>
            {
                requests.Add(path);
                return missing ? null : texture;
            };
            return (ComponentArtCatalog)Activator.CreateInstance(typeof(ComponentArtCatalog),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { loader }, null);
        }

        [Test]
        public void ConstructingAndReadingMetadataPreservesLegacyOrderWithoutLoadingImages()
        {
            ComponentArtCatalog catalog = CreateCatalog();
            string[] expectedIds = Resources.Load<TextAsset>(ComponentArtCatalog.CatalogResourcePath).text
                .Replace("\r", string.Empty).Split('\n').Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
                .Select(line => line.Split(',')[0].Trim()).ToArray();

            Assert.That(catalog.Entries.Select(entry => entry.Id), Is.EqualTo(expectedIds));
            Assert.That(catalog.Entries.Count, Is.EqualTo(125));
            Assert.That(catalog.ExpandedEntries.Count, Is.EqualTo(150));
            for (int index = 0; index < catalog.Entries.Count; index++)
            {
                ComponentArtEntry entry = catalog.Get(index);
                Assert.That(catalog.GetById(entry.Id), Is.SameAs(entry));
                Assert.That(catalog.IndexOf(entry), Is.EqualTo(index));
                Assert.That(catalog.ExpandedEntries[index], Is.SameAs(entry));
                Assert.That(entry.Name, Is.Not.Empty);
                Assert.That(entry.StoryBody, Is.Not.Empty);
                Assert.That(ComponentArtCatalog.EnvironmentSummary(entry), Is.Not.Empty);
            }
            Assert.That(requests, Is.Empty, "Save restoration and stat queries must not load component art.");
        }

        [Test]
        public void IndependentArtLoadsOncePerRequestedItemAcrossLegacyAndExpandedEntries()
        {
            ComponentArtCatalog catalog = CreateCatalog();
            ComponentArtEntry first = catalog.Entries[0];
            ComponentArtEntry sibling = catalog.Entries[1];
            ComponentArtEntry expanded = catalog.ExpandedEntries[125];

            Assert.That(first.Texture, Is.SameAs(texture));
            Assert.That(first.Texture, Is.SameAs(texture));
            Assert.That(sibling.Texture, Is.SameAs(texture));
            Assert.That(expanded.Texture, Is.SameAs(texture));
            Assert.That(requests, Is.EqualTo(new[]
            {
                "ComponentArt/" + first.Id, "ComponentArt/" + sibling.Id, "ComponentArt/" + expanded.Id,
            }));

            Assert.That(first.ShipLayer, Is.SameAs(texture));
            Assert.That(sibling.ShipLayer, Is.SameAs(texture));
            Assert.That(expanded.ShipLayer, Is.SameAs(texture));
            Assert.That(requests, Is.EqualTo(new[]
            {
                "ComponentArt/" + first.Id, "ComponentArt/" + sibling.Id, "ComponentArt/" + expanded.Id,
                "ModularShip/Layers/" + first.Id, "ModularShip/Layers/" + sibling.Id, "ModularShip/Layers/" + expanded.Id,
            }));
            Assert.That(catalog.BaseShipTexture, Is.SameAs(texture));
            Assert.That(catalog.BaseShipTexture, Is.SameAs(texture));
            Assert.That(requests.Last(), Is.EqualTo("ModularShip/light_sloop_base"));
            Assert.That(requests.Count, Is.EqualTo(7));
        }

        [Test]
        public void MissingArtIsCachedAndInternalSetterKeepsExplicitOverrides()
        {
            ComponentArtCatalog catalog = CreateCatalog(true);
            ComponentArtEntry first = catalog.Entries[0];
            ComponentArtEntry sibling = catalog.Entries[1];
            Assert.That(first.Texture, Is.Null);
            Assert.That(first.Texture, Is.Null);
            Assert.That(sibling.Texture, Is.Null);
            Assert.That(requests, Is.EqualTo(new[] { "ComponentArt/" + first.Id, "ComponentArt/" + sibling.Id }));
            Assert.That(catalog.BaseShipTexture, Is.Null);
            Assert.That(catalog.BaseShipTexture, Is.Null);
            Assert.That(requests.Count, Is.EqualTo(3));

            typeof(ComponentArtEntry).GetProperty(nameof(ComponentArtEntry.Texture))
                .GetSetMethod(true).Invoke(first, new object[] { texture });
            typeof(ComponentArtEntry).GetProperty(nameof(ComponentArtEntry.ShipLayer))
                .GetSetMethod(true).Invoke(first, new object[] { null });
            Assert.That(first.Texture, Is.SameAs(texture));
            Assert.That(first.ShipLayer, Is.Null);
            Assert.That(requests.Count, Is.EqualTo(3), "Explicit entry overrides must not fall through to Resources.");
        }

        [Test]
        public void AvailabilityStillValidatesEveryUniqueArtMappingAndReusesItsCache()
        {
            ComponentArtCatalog catalog = CreateCatalog();
            Assert.That(catalog.IsAvailable, Is.True);
            Assert.That(catalog.ExpandedIsAvailable, Is.True);
            string[] sources = catalog.ExpandedEntries.Select(entry => entry.ArtSourceId).Distinct().ToArray();
            string[] expectedPaths = sources.Select(id => "ComponentArt/" + id)
                .Concat(sources.Select(id => "ModularShip/Layers/" + id))
                .Concat(new[] { "ModularShip/light_sloop_base" }).ToArray();
            Assert.That(requests, Is.EquivalentTo(expectedPaths));
            Assert.That(requests.Count, Is.EqualTo(301));
            Assert.That(catalog.IsAvailable && catalog.ExpandedIsAvailable, Is.True);
            Assert.That(requests.Count, Is.EqualTo(301));
            Assert.That(CreateCatalog(true).IsAvailable, Is.False);
        }

        [Test]
        public void ReleasingBorrowedArtPreservesEntryIdentityAndReloadsEachPathOnce()
        {
            ComponentArtCatalog catalog = CreateCatalog();
            ComponentArtEntry entry = catalog.Entries[0];
            ComponentArtEntry sibling = catalog.Entries[1];
            Assert.That(entry.Texture, Is.SameAs(texture));
            Assert.That(entry.ShipLayer, Is.SameAs(texture));
            Assert.That(catalog.BaseShipTexture, Is.SameAs(texture));
            Assert.That(sibling.Texture, Is.SameAs(texture));
            Assert.That(requests.Count, Is.EqualTo(4));

            catalog.ReleaseLoadedTextures();
            catalog.ReleaseLoadedTextures();
            Assert.That(texture != null, Is.True, "Release must not destroy a borrowed image still used by the UI.");
            Assert.That(catalog.GetById(entry.Id), Is.SameAs(entry));
            Assert.That(catalog.ExpandedEntries[0], Is.SameAs(entry));
            Assert.That(requests.Count, Is.EqualTo(4), "Releasing references itself must make no image requests.");
            Assert.That(entry.Texture, Is.SameAs(texture));
            Assert.That(sibling.Texture, Is.SameAs(texture));
            Assert.That(entry.ShipLayer, Is.SameAs(texture));
            Assert.That(catalog.BaseShipTexture, Is.SameAs(texture));
            Assert.That(requests.Count, Is.EqualTo(8));
            Assert.That(requests.GroupBy(path => path).All(group => group.Count() == 2), Is.True);
        }

        [Test]
        public void ReleaseKeepsExplicitNullAndNonNullEntryOverrides()
        {
            ComponentArtCatalog catalog = CreateCatalog();
            ComponentArtEntry entry = catalog.Entries[0];
            typeof(ComponentArtEntry).GetProperty(nameof(ComponentArtEntry.Texture))
                .GetSetMethod(true).Invoke(entry, new object[] { null });
            typeof(ComponentArtEntry).GetProperty(nameof(ComponentArtEntry.ShipLayer))
                .GetSetMethod(true).Invoke(entry, new object[] { texture });
            catalog.ReleaseLoadedTextures();
            Assert.That(entry.Texture, Is.Null);
            Assert.That(entry.ShipLayer, Is.SameAs(texture));
            Assert.That(requests, Is.Empty);
        }
    }

    public sealed class FacilityArtLazyLoadingTests
    {
        private Texture2D texture;
        private readonly List<string> requests = new List<string>();

        [SetUp]
        public void SetUp()
        {
            requests.Clear();
            texture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(texture);

        private object CreateCatalog(Type type, bool missing = false)
        {
            Func<string, Texture2D> loader = path =>
            {
                requests.Add(path);
                return missing ? null : texture;
            };
            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { loader }, null);
        }

        private static Texture2D FirstImage(object catalog)
        {
            if (catalog is ComponentShopArtCatalog shop) return shop.Merchant;
            if (catalog is CardWorkshopArtCatalog workshop) return workshop.CollectionParchment;
            return ((MaintenanceArtCatalog)catalog).Background;
        }

        [TestCase(typeof(ComponentShopArtCatalog), 7)]
        [TestCase(typeof(CardWorkshopArtCatalog), 7)]
        [TestCase(typeof(MaintenanceArtCatalog), 4)]
        public void ConstructorsMakeNoImageRequestsAndAvailabilityExplicitlyChecksAllArt(Type type, int expectedCount)
        {
            object catalog = CreateCatalog(type);
            if (catalog is CardWorkshopArtCatalog workshop) Assert.That(workshop.ChestCount, Is.EqualTo(4));
            Assert.That(requests, Is.Empty);
            Assert.That(FirstImage(catalog), Is.SameAs(texture));
            Assert.That(FirstImage(catalog), Is.SameAs(texture));
            Assert.That(requests.Count, Is.EqualTo(1));
            PropertyInfo availability = type.GetProperty("IsAvailable");
            Assert.That(availability.GetValue(catalog), Is.True);
            Assert.That(requests.Count, Is.EqualTo(expectedCount));
            Assert.That(requests.Distinct().Count(), Is.EqualTo(expectedCount));
            Assert.That(availability.GetValue(catalog), Is.True);
            Assert.That(requests.Count, Is.EqualTo(expectedCount));
        }

        [TestCase(typeof(ComponentShopArtCatalog))]
        [TestCase(typeof(CardWorkshopArtCatalog))]
        [TestCase(typeof(MaintenanceArtCatalog))]
        public void MissingImageIsRequestedOnceAndAvailabilityReportsFalse(Type type)
        {
            object catalog = CreateCatalog(type, true);
            Assert.That(FirstImage(catalog), Is.Null);
            Assert.That(FirstImage(catalog), Is.Null);
            Assert.That(type.GetProperty("IsAvailable").GetValue(catalog), Is.False);
            Assert.That(requests.Count, Is.EqualTo(1));
        }

        [TestCase(typeof(ComponentShopArtCatalog))]
        [TestCase(typeof(CardWorkshopArtCatalog))]
        [TestCase(typeof(MaintenanceArtCatalog))]
        public void ReleasingImagesOnlyClearsReferencesAndNextAccessReloads(Type type)
        {
            object catalog = CreateCatalog(type);
            Assert.That(FirstImage(catalog), Is.SameAs(texture));
            Assert.That(requests.Count, Is.EqualTo(1));
            type.GetMethod("ReleaseLoadedTextures").Invoke(catalog, null);
            type.GetMethod("ReleaseLoadedTextures").Invoke(catalog, null);
            Assert.That(texture != null, Is.True);
            Assert.That(requests.Count, Is.EqualTo(1));
            Assert.That(FirstImage(catalog), Is.SameAs(texture));
            Assert.That(FirstImage(catalog), Is.SameAs(texture));
            Assert.That(requests.Count, Is.EqualTo(2));
            Assert.That(requests[1], Is.EqualTo(requests[0]));
        }

        [Test]
        public void IndexedArtPreservesClampingAndDoesNotLoadOtherSlots()
        {
            var shop = (ComponentShopArtCatalog)CreateCatalog(typeof(ComponentShopArtCatalog));
            Assert.That(shop.Mount((ComponentSlot)(-1)), Is.SameAs(texture));
            Assert.That(shop.Mount(ComponentSlot.Bow), Is.SameAs(texture));
            Assert.That(shop.Mount((ComponentSlot)int.MaxValue), Is.SameAs(texture));
            Assert.That(shop.Mount(ComponentSlot.Instrument), Is.SameAs(texture));
            Assert.That(requests, Is.EqualTo(new[] { "mount_bow_v1", "mount_instrument_v1" }));
            requests.Clear();
            var workshop = (CardWorkshopArtCatalog)CreateCatalog(typeof(CardWorkshopArtCatalog));
            Assert.That(workshop.Chest(-1), Is.SameAs(texture));
            Assert.That(workshop.Chest(0), Is.SameAs(texture));
            Assert.That(workshop.Chest(int.MaxValue), Is.SameAs(texture));
            Assert.That(workshop.Chest(3), Is.SameAs(texture));
            Assert.That(requests, Is.EqualTo(new[] { "deck_chest_standard_v1", "deck_chest_captain_v1" }));
        }
    }
}
