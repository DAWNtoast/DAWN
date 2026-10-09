using System;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    /// <summary>Managed geometry caching requires no GPU or texture assertions.</summary>
    public sealed class VoyageMapArtCatalogCoastCacheTests
    {
        [Test]
        public void SameMapAndGeometry_ReuseTheExistingManagedField()
        {
            VoyageMapArtCatalog art = new VoyageMapArtCatalog();
            CoreVoyageMap map = CreateMap(19u);
            VoyageChartCoastField first = art.GetCoastField(map);

            Assert.That(art.GetCoastField(map), Is.SameAs(first));
            Assert.That(art.GetCoastField(map).Pixels, Is.SameAs(first.Pixels));
            Assert.That(first.GeometryFingerprint, Is.EqualTo(VoyageChartCoastField.Fingerprint(map)));
        }

        [Test]
        public void ExplorationAndCurrentChanges_DoNotInvalidateCoastGeometry()
        {
            VoyageMapArtCatalog art = new VoyageMapArtCatalog();
            CoreVoyageMap map = CreateMap(23u);
            VoyageChartCoastField first = art.GetCoastField(map);

            Array.Fill(map.ExploredCells, true);
            map.CurrentVectors[0] = new VoyagePoint(0.4f, -0.8f);
            map.CurrentStrengths[0] = 3.2f;
            map.CurrentPhaseA = 12.5;
            map.CurrentPhaseB = 92.25;
            map.GlobalWindVector = new VoyagePoint(-0.2f, 0.9f);
            map.GlobalWindStrength = 7.4f;
            map.TerrainFeatureIds[0] = 900;
            map.TerrainVariants[0] = 4;

            Assert.That(art.GetCoastField(map), Is.SameAs(first),
                "Exploration, currents and decorative metadata do not change solid coast geometry.");
        }

        [Test]
        public void TerrainChangeOnTheSameMap_RebuildsThenCachesTheNewField()
        {
            VoyageMapArtCatalog art = new VoyageMapArtCatalog();
            CoreVoyageMap map = CreateMap(31u);
            VoyageChartCoastField first = art.GetCoastField(map);
            string originalFingerprint = first.GeometryFingerprint;
            map.TerrainCells[14 * CoreVoyageMap.GridColumns + 21] = SeaTerrainType.Island;
            VoyageChartCoastField changed = art.GetCoastField(map);

            Assert.That(changed, Is.Not.SameAs(first));
            Assert.That(changed.GeometryFingerprint, Is.Not.EqualTo(originalFingerprint));
            Assert.That(first.GeometryFingerprint, Is.EqualTo(originalFingerprint),
                "Previously borrowed fields must not be mutated when rebuilding the cache.");
            Assert.That(art.GetCoastField(map), Is.SameAs(changed));
        }

        [Test]
        public void SeedHashChange_InvalidatesTheCachedRoughness()
        {
            VoyageMapArtCatalog art = new VoyageMapArtCatalog();
            CoreVoyageMap map = CreateMap(41u);
            VoyageChartCoastField first = art.GetCoastField(map);
            map.Hash = 43u;

            VoyageChartCoastField changed = art.GetCoastField(map);
            Assert.That(changed, Is.Not.SameAs(first));
            Assert.That(changed.GeometryFingerprint, Is.Not.EqualTo(first.GeometryFingerprint));
            Assert.That(art.GetCoastField(map), Is.SameAs(changed));
        }

        [Test]
        public void SwitchingMaps_ReplacesTheSingleEntryEvenWhenTheirFingerprintsMatch()
        {
            VoyageMapArtCatalog art = new VoyageMapArtCatalog();
            CoreVoyageMap firstMap = CreateMap(47u);
            CoreVoyageMap secondMap = CreateMap(47u);
            VoyageChartCoastField firstField = art.GetCoastField(firstMap);
            VoyageChartCoastField secondField = art.GetCoastField(secondMap);

            Assert.That(secondField.GeometryFingerprint, Is.EqualTo(firstField.GeometryFingerprint));
            Assert.That(secondField, Is.Not.SameAs(firstField),
                "Reuse requires both object identity and a matching geometry fingerprint.");
            Assert.That(art.GetCoastField(secondMap), Is.SameAs(secondField));

            VoyageChartCoastField returningToFirst = art.GetCoastField(firstMap);
            Assert.That(returningToFirst, Is.Not.SameAs(firstField),
                "Do not retain an unbounded dictionary of every previously visited sea map.");
            Assert.That(returningToFirst, Is.Not.SameAs(secondField));
            Assert.That(returningToFirst.GeometryFingerprint, Is.EqualTo(firstField.GeometryFingerprint));
        }

        [Test]
        public void NullMap_IsRejectedWithoutDiscardingTheExistingEntry()
        {
            VoyageMapArtCatalog art = new VoyageMapArtCatalog();
            CoreVoyageMap map = CreateMap(53u);
            VoyageChartCoastField field = art.GetCoastField(map);

            Assert.Throws<ArgumentNullException>(() => art.GetCoastField(null));
            Assert.That(art.GetCoastField(map), Is.SameAs(field));
        }

        private static CoreVoyageMap CreateMap(uint hash)
        {
            CoreVoyageMap map = new CoreVoyageMap { Hash = hash, Seed = "COAST-CACHE-" + hash };
            map.TerrainCells[12 * CoreVoyageMap.GridColumns + 18] = SeaTerrainType.Island;
            return map;
        }
    }
}
