using System;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class VoyageChartCoastFieldTests
    {
        [Test]
        public void SameMap_BuildsIdenticalHighResolutionFieldAndFingerprint()
        {
            CoreVoyageMap map = CreateScenario("ConcaveBay", 0x13579BDFu);
            VoyageChartCoastField first = VoyageChartCoastField.Build(map);
            VoyageChartCoastField second = VoyageChartCoastField.Build(map);

            Assert.That(VoyageChartCoastField.SamplesPerCell, Is.EqualTo(12));
            Assert.That(first.Width, Is.EqualTo(CoreVoyageMap.GridColumns * VoyageChartCoastField.SamplesPerCell + 1));
            Assert.That(first.Height, Is.EqualTo(CoreVoyageMap.GridRows * VoyageChartCoastField.SamplesPerCell + 1));
            Assert.That(first.Pixels.Length, Is.EqualTo(first.Width * first.Height));
            Assert.That(first.GeometryFingerprint, Is.Not.Null.And.Not.Empty);
            Assert.That(first.GeometryFingerprint, Is.EqualTo(second.GeometryFingerprint));
            CollectionAssert.AreEqual(first.Pixels, second.Pixels,
                "The same chart must not change its coastline when the UI is rebuilt.");
        }

        [Test]
        public void DifferentSeeds_VaryCoastalDetailWithoutChangingCellCentrePassability()
        {
            CoreVoyageMap firstMap = CreateScenario("ConcaveBay", 0x13579BDFu);
            CoreVoyageMap secondMap = CreateScenario("ConcaveBay", 0x2468ACE0u);
            VoyageChartCoastField first = VoyageChartCoastField.Build(firstMap);
            VoyageChartCoastField second = VoyageChartCoastField.Build(secondMap);

            Assert.That(first.GeometryFingerprint, Is.Not.EqualTo(second.GeometryFingerprint));
            bool coastChanged = false;
            for (int index = 0; index < first.Pixels.Length; index += 1)
            {
                if (first.Pixels[index].r != second.Pixels[index].r)
                {
                    coastChanged = true;
                    break;
                }
            }
            Assert.That(coastChanged, Is.True,
                "Different seeds should affect the shore contour rather than only texture colour.");
            AssertCellCentrePassability(firstMap, first);
            AssertCellCentrePassability(secondMap, second);
        }

        [TestCase("SingleCell")]
        [TestCase("ConcaveBay")]
        [TestCase("DisconnectedSameFeature")]
        [TestCase("HorizontalStrait")]
        [TestCase("VerticalStrait")]
        public void CoastGeometry_PreservesActualCellsInsteadOfTheirBoundingRectangle(string scenario)
        {
            CoreVoyageMap map = CreateScenario(scenario, 0xBADC0FFEu);
            VoyageChartCoastField field = VoyageChartCoastField.Build(map);
            AssertCellCentrePassability(map, field);

            switch (scenario)
            {
                case "ConcaveBay":
                    for (int row = 10; row <= 18; row += 1)
                    {
                        Assert.That(field.SampleSignedDistance(14.5f, row + 0.5f), Is.LessThan(0f),
                            "An open concave bay must not become the filled rectangle around its banks.");
                    }
                    break;
                case "DisconnectedSameFeature":
                    Assert.That(field.SampleSignedDistance(10.5f, 11.5f), Is.LessThan(0f),
                        "Two islands sharing a feature id must retain the water separating them.");
                    break;
                case "HorizontalStrait":
                    AssertOpenChannel(field, true);
                    break;
                case "VerticalStrait":
                    AssertOpenChannel(field, false);
                    break;
            }
        }

        [Test]
        public void OpenWaterOnlyChart_ContainsNoPhantomLand()
        {
            CoreVoyageMap map = new CoreVoyageMap { Hash = 7u, Seed = "ALL-WATER" };
            VoyageChartCoastField field = VoyageChartCoastField.Build(map);
            AssertCellCentrePassability(map, field);
            Assert.That(Array.TrueForAll(field.Pixels, pixel => pixel.r < 128), Is.True,
                "The ocean base must not acquire land from the corners of a decorative source image.");
        }

        [Test]
        public void IslandOnlyChart_ClosesItsCoastInsideEveryMapEdge()
        {
            CoreVoyageMap map = new CoreVoyageMap { Hash = 0xA17D5EEDu, Seed = "ALL-LAND" };
            FillRect(map, 0, 0, CoreVoyageMap.GridColumns - 1, CoreVoyageMap.GridRows - 1, SeaTerrainType.Island);
            VoyageChartCoastField field = VoyageChartCoastField.Build(map);
            AssertCellCentrePassability(map, field);

            // Only the sub-cell rim may close up; edge cell centres must remain
            // land so the visual repair cannot contradict the collision map.
            for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
            {
                float x = column + 0.5f;
                Assert.That(field.SampleSignedDistance(x, 0.05f), Is.LessThan(0f));
                Assert.That(field.SampleSignedDistance(x, CoreVoyageMap.GridRows - 0.05f), Is.LessThan(0f));
            }
            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                float y = row + 0.5f;
                Assert.That(field.SampleSignedDistance(0.05f, y), Is.LessThan(0f));
                Assert.That(field.SampleSignedDistance(CoreVoyageMap.GridColumns - 0.05f, y), Is.LessThan(0f));
            }
        }

        [Test]
        public void AllPassableTerrainKinds_RemainWaterAtTheirCellCentres()
        {
            CoreVoyageMap map = new CoreVoyageMap { Hash = 0xC0111510u, Seed = "TERRAIN-KINDS" };
            SeaTerrainType[] passable =
            {
                SeaTerrainType.OpenWater, SeaTerrainType.Shoal, SeaTerrainType.Reef,
                SeaTerrainType.KelpField, SeaTerrainType.FogBank, SeaTerrainType.StormFront,
                SeaTerrainType.Beach, SeaTerrainType.Strait,
            };
            FillRect(map, 5, 5, 39, 22, SeaTerrainType.Island);
            for (int index = 0; index < passable.Length; index += 1)
            {
                FillRect(map, 7 + index * 4, 8, 8 + index * 4, 19, passable[index]);
            }

            VoyageChartCoastField field = VoyageChartCoastField.Build(map);
            AssertCellCentrePassability(map, field);
        }

        [Test]
        public void Build_DoesNotMutateLogicalTerrainOwnershipExplorationOrCurrents()
        {
            CoreVoyageMap map = CreateScenario("HorizontalStrait", 0x10203040u);
            for (int index = 0; index < map.CellCount; index += 1)
            {
                map.TerrainFeatureIds[index] = index % 13;
                map.AnchorNodeIds[index] = index % 7 == 0 ? index % 13 : -1;
                map.EventAnchorCells[index] = index % 19 == 0;
                map.EventReservedCells[index] = index % 7 == 0;
                map.TerrainVariants[index] = (byte)(index % 16);
                map.ExploredCells[index] = index % 3 == 0;
                map.CurrentVectors[index] = new VoyagePoint((index % 5) * 0.1f, (index % 7) * -0.1f);
                map.CurrentStrengths[index] = (index % 11) * 0.25f;
            }
            SeaTerrainType[] terrain = (SeaTerrainType[])map.TerrainCells.Clone();
            int[] featureIds = (int[])map.TerrainFeatureIds.Clone();
            int[] anchorIds = (int[])map.AnchorNodeIds.Clone();
            bool[] anchors = (bool[])map.EventAnchorCells.Clone();
            bool[] reserved = (bool[])map.EventReservedCells.Clone();
            byte[] variants = (byte[])map.TerrainVariants.Clone();
            bool[] explored = (bool[])map.ExploredCells.Clone();
            VoyagePoint[] currents = (VoyagePoint[])map.CurrentVectors.Clone();
            float[] strengths = (float[])map.CurrentStrengths.Clone();
            string seed = map.Seed;
            uint hash = map.Hash;

            VoyageChartCoastField.Build(map);

            CollectionAssert.AreEqual(terrain, map.TerrainCells);
            CollectionAssert.AreEqual(featureIds, map.TerrainFeatureIds);
            CollectionAssert.AreEqual(anchorIds, map.AnchorNodeIds);
            CollectionAssert.AreEqual(anchors, map.EventAnchorCells);
            CollectionAssert.AreEqual(reserved, map.EventReservedCells);
            CollectionAssert.AreEqual(variants, map.TerrainVariants);
            CollectionAssert.AreEqual(explored, map.ExploredCells);
            CollectionAssert.AreEqual(currents, map.CurrentVectors);
            CollectionAssert.AreEqual(strengths, map.CurrentStrengths);
            Assert.That(map.Seed, Is.EqualTo(seed));
            Assert.That(map.Hash, Is.EqualTo(hash));
        }

        [Test]
        public void RedChannel_EncodesSignedDistanceAtSharedGridSamples()
        {
            CoreVoyageMap map = CreateScenario("SingleCell", 0x513DF1E1u);
            VoyageChartCoastField field = VoyageChartCoastField.Build(map);
            for (int y = 10 * VoyageChartCoastField.SamplesPerCell; y < 15 * VoyageChartCoastField.SamplesPerCell; y += 5)
            {
                for (int x = 10 * VoyageChartCoastField.SamplesPerCell; x < 15 * VoyageChartCoastField.SamplesPerCell; x += 5)
                {
                    float distance = field.SampleSignedDistance(
                        x / (float)VoyageChartCoastField.SamplesPerCell,
                        y / (float)VoyageChartCoastField.SamplesPerCell);
                    float expected = Mathf.Clamp01(distance / 4f + 0.5f);
                    float encoded = field.Pixels[y * field.Width + x].r / 255f;
                    Assert.That(encoded, Is.EqualTo(expected).Within(1.1f / 255f),
                        "Shader distance encoding and CPU sampling must agree around the shore.");
                }
            }
        }

        [Test]
        public void MaterialChannels_KeepShoalsAndReefsDistinctFromSolidLand()
        {
            CoreVoyageMap map = new CoreVoyageMap { Hash = 0x531813u, Seed = "COAST-MATERIALS" };
            FillRect(map, 5, 5, 9, 9, SeaTerrainType.Shoal);
            FillRect(map, 15, 5, 19, 9, SeaTerrainType.Reef);
            FillRect(map, 25, 5, 29, 9, SeaTerrainType.Beach);
            VoyageChartCoastField field = VoyageChartCoastField.Build(map);
            Color32 shoal = PixelAtCellCentre(field, 7, 7);
            Color32 reef = PixelAtCellCentre(field, 17, 7);
            Color32 beach = PixelAtCellCentre(field, 27, 7);
            Color32 ocean = PixelAtCellCentre(field, 40, 25);

            Assert.That(shoal.g, Is.GreaterThan(192));
            Assert.That(shoal.b, Is.LessThan(32));
            Assert.That(reef.b, Is.GreaterThan(192));
            Assert.That(reef.g, Is.LessThan(32));
            Assert.That(beach.g, Is.GreaterThan(192));
            Assert.That(ocean.g, Is.LessThan(32));
            Assert.That(ocean.b, Is.LessThan(32));
            AssertCellCentrePassability(map, field);
        }

        [Test]
        public void GeometryFingerprint_IgnoresVisibilityButInvalidatesTerrainEdits()
        {
            CoreVoyageMap map = CreateScenario("SingleCell", 0x19760521u);
            string initial = VoyageChartCoastField.Build(map).GeometryFingerprint;
            Array.Fill(map.ExploredCells, true);
            Array.Fill(map.TerrainFeatureIds, 91);
            Array.Fill(map.TerrainVariants, (byte)15);
            map.CurrentVectors[0] = new VoyagePoint(0.3f, 0.4f);
            map.CurrentStrengths[0] = 2f;
            string revealed = VoyageChartCoastField.Build(map).GeometryFingerprint;
            Assert.That(revealed, Is.EqualTo(initial),
                "Exploring the same island must not reroll its geometry or invalidate the coast cache.");

            map.TerrainCells[12 * CoreVoyageMap.GridColumns + 12] = SeaTerrainType.OpenWater;
            string changed = VoyageChartCoastField.Build(map).GeometryFingerprint;
            Assert.That(changed, Is.Not.EqualTo(initial),
                "A real terrain edit must invalidate cached visual geometry even when the seed is unchanged.");
        }

        private static Color32 PixelAtCellCentre(VoyageChartCoastField field, int column, int row)
        {
            int x = column * VoyageChartCoastField.SamplesPerCell + VoyageChartCoastField.SamplesPerCell / 2;
            int y = row * VoyageChartCoastField.SamplesPerCell + VoyageChartCoastField.SamplesPerCell / 2;
            return field.Pixels[y * field.Width + x];
        }

        private static CoreVoyageMap CreateScenario(string scenario, uint hash)
        {
            CoreVoyageMap map = new CoreVoyageMap { Hash = hash, Seed = "COAST-" + hash };
            switch (scenario)
            {
                case "SingleCell":
                    FillRect(map, 12, 12, 12, 12, SeaTerrainType.Island);
                    break;
                case "ConcaveBay":
                    FillRect(map, 10, 8, 18, 16, SeaTerrainType.Island);
                    FillRect(map, 12, 10, 16, 16, SeaTerrainType.OpenWater);
                    break;
                case "DisconnectedSameFeature":
                    FillRect(map, 6, 10, 8, 12, SeaTerrainType.Island);
                    FillRect(map, 12, 10, 14, 12, SeaTerrainType.Island);
                    break;
                case "HorizontalStrait":
                    FillRect(map, 9, 8, 21, 11, SeaTerrainType.Island);
                    FillRect(map, 9, 13, 21, 16, SeaTerrainType.Island);
                    FillRect(map, 7, 12, 23, 12, SeaTerrainType.Strait);
                    break;
                case "VerticalStrait":
                    FillRect(map, 8, 7, 11, 23, SeaTerrainType.Island);
                    FillRect(map, 13, 7, 16, 23, SeaTerrainType.Island);
                    FillRect(map, 12, 5, 12, 25, SeaTerrainType.Strait);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown geometry fixture.");
            }
            return map;
        }

        private static void FillRect(CoreVoyageMap map, int minX, int minY, int maxX, int maxY, SeaTerrainType terrain)
        {
            for (int row = minY; row <= maxY; row += 1)
            {
                for (int column = minX; column <= maxX; column += 1)
                {
                    int index = row * CoreVoyageMap.GridColumns + column;
                    map.TerrainCells[index] = terrain;
                    // Deliberately identical ownership: rendering must use cell
                    // geometry rather than assuming one feature is a filled box.
                    map.TerrainFeatureIds[index] = 42;
                }
            }
        }

        private static void AssertCellCentrePassability(CoreVoyageMap map, VoyageChartCoastField field)
        {
            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
                {
                    float distance = field.SampleSignedDistance(column + 0.5f, row + 0.5f);
                    bool isLand = map.TerrainAt(column, row) == SeaTerrainType.Island;
                    Assert.That(float.IsNaN(distance) || float.IsInfinity(distance), Is.False);
                    if (isLand)
                    {
                        Assert.That(distance, Is.GreaterThan(0f), "Blocked island centre must look like land: " + column + ", " + row);
                    }
                    else
                    {
                        Assert.That(distance, Is.LessThan(0f), "Passable cell centre must stay open: " + column + ", " + row);
                    }
                }
            }
        }

        private static void AssertOpenChannel(VoyageChartCoastField field, bool horizontal)
        {
            for (int step = 0; step <= 64; step += 1)
            {
                float axis = 7.5f + step * 0.25f;
                float x = horizontal ? axis : 12.5f;
                float y = horizontal ? 12.5f : axis;
                Assert.That(field.SampleSignedDistance(x, y), Is.LessThan(0f),
                    "The centre line of a navigable strait must remain clear between both closed banks.");
            }
        }
    }
}
