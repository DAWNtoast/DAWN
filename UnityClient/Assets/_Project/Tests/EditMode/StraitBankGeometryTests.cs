using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class StraitBankGeometryTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void SeededBanks_AreThickConnectedAsymmetricAndWithinOriginalEnvelope(bool horizontal)
        {
            HashSet<string> firstProfiles = new HashSet<string>();
            HashSet<string> secondProfiles = new HashSet<string>();
            int asymmetricPairs = 0;
            for (uint seed = 0; seed < 64; seed += 1)
            {
                HashSet<VoyageGridCoordinate> first = BuildBank(horizontal, -1, seed ^ 0x91A74D3Bu);
                HashSet<VoyageGridCoordinate> second = BuildBank(horizontal, 1, seed ^ 0xE35A7BD1u);
                AssertBank(first, horizontal, -1);
                AssertBank(second, horizontal, 1);
                string firstProfile = Profile(first, horizontal, -1);
                string secondProfile = Profile(second, horizontal, 1);
                firstProfiles.Add(firstProfile);
                secondProfiles.Add(secondProfile);
                if (firstProfile != secondProfile) asymmetricPairs += 1;

                CollectionAssert.AreEquivalent(first, BuildBank(horizontal, -1, seed ^ 0x91A74D3Bu),
                    "A seeded bank must stay identical across rebuilds.");
                CollectionAssert.AreEquivalent(second, BuildBank(horizontal, 1, seed ^ 0xE35A7BD1u));
            }
            Assert.That(firstProfiles.Count, Is.GreaterThan(16), "Coasts should vary by more than a mirrored one-cell spine.");
            Assert.That(secondProfiles.Count, Is.GreaterThan(16));
            Assert.That(asymmetricPairs, Is.GreaterThan(48), "Opposite banks should not be cloned silhouettes.");
        }

        [Test]
        public void VerticalBank_IsExactTransposeOfHorizontalBankForSameSeed()
        {
            for (uint seed = 0; seed < 16; seed += 1)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    HashSet<VoyageGridCoordinate> horizontal = BuildBank(true, side, seed);
                    HashSet<VoyageGridCoordinate> vertical = BuildBank(false, side, seed);
                    CollectionAssert.AreEquivalent(
                        horizontal.Select(cell => new VoyageGridCoordinate(cell.Row, cell.Column)), vertical);
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void CompleteStraitFootprint_ProtectsStraightChannelAndOriginalReservationBounds(int orientation)
        {
            for (uint seed = 0; seed < 32; seed += 1)
            {
                object plan = BuildStraitPlan(orientation, seed * 977u + 17u);
                bool horizontal = orientation == 0;
                Dictionary<VoyageGridCoordinate, SeaTerrainType> terrain = ReadStamps(plan);
                HashSet<VoyageGridCoordinate> firstBank = new HashSet<VoyageGridCoordinate>();
                HashSet<VoyageGridCoordinate> secondBank = new HashSet<VoyageGridCoordinate>();
                foreach (KeyValuePair<VoyageGridCoordinate, SeaTerrainType> pair in terrain)
                {
                    int along = horizontal ? pair.Key.Column : pair.Key.Row;
                    int across = horizontal ? pair.Key.Row : pair.Key.Column;
                    Assert.That(along, Is.InRange(-5, 5));
                    Assert.That(across, Is.InRange(-5, 5));
                    if (pair.Value != SeaTerrainType.Island) continue;
                    Assert.That(Math.Abs(across), Is.InRange(2, 4), "Solid banks may not enter the three-cell central water lane.");
                    Assert.That(along, Is.InRange(-4, 4));
                    (across < 0 ? firstBank : secondBank).Add(pair.Key);
                }
                AssertBank(firstBank, horizontal, -1);
                AssertBank(secondBank, horizontal, 1);
                for (int along = -5; along <= 5; along += 1)
                {
                    VoyageGridCoordinate centre = horizontal
                        ? new VoyageGridCoordinate(along, 0)
                        : new VoyageGridCoordinate(0, along);
                    Assert.That(terrain.TryGetValue(centre, out SeaTerrainType type), Is.True);
                    Assert.That(type, Is.EqualTo(SeaTerrainType.Strait), "End-to-end centre line must remain directly navigable.");
                }

                Array reserved = (Array)plan.GetType().GetField("ReservedOffsets").GetValue(plan);
                foreach (VoyageGridCoordinate cell in reserved)
                {
                    Assert.That(cell.Column, Is.InRange(-6, 6), "Keep the existing footprint plus one-cell safety envelope.");
                    Assert.That(cell.Row, Is.InRange(-6, 6));
                }
            }
        }

        private static HashSet<VoyageGridCoordinate> BuildBank(bool horizontal, int side, uint seed)
        {
            MethodInfo method = typeof(VoyageMapGenerator).GetMethod("BuildStraitBankCells", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (HashSet<VoyageGridCoordinate>)method.Invoke(null, new object[] { horizontal, side, seed });
        }

        private static object BuildStraitPlan(int orientation, uint seed)
        {
            MethodInfo method = typeof(VoyageMapGenerator).GetMethod("BuildPlan", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new object[] { EventTerrainConstraint.Strait, orientation, seed });
        }

        private static Dictionary<VoyageGridCoordinate, SeaTerrainType> ReadStamps(object plan)
        {
            Array stamps = (Array)plan.GetType().GetField("Stamps").GetValue(plan);
            Dictionary<VoyageGridCoordinate, SeaTerrainType> terrain = new Dictionary<VoyageGridCoordinate, SeaTerrainType>();
            foreach (object stamp in stamps)
            {
                Type type = stamp.GetType();
                int column = (int)type.GetField("ColumnOffset").GetValue(stamp);
                int row = (int)type.GetField("RowOffset").GetValue(stamp);
                SeaTerrainType value = (SeaTerrainType)type.GetField("Terrain").GetValue(stamp);
                terrain.Add(new VoyageGridCoordinate(column, row), value);
            }
            return terrain;
        }

        private static void AssertBank(HashSet<VoyageGridCoordinate> bank, bool horizontal, int side)
        {
            Assert.That(bank.Count, Is.GreaterThanOrEqualTo(13), "Each bank needs a broad body, not a thin line with a single crosspiece.");
            HashSet<VoyageGridCoordinate> visited = new HashSet<VoyageGridCoordinate>();
            Queue<VoyageGridCoordinate> queue = new Queue<VoyageGridCoordinate>();
            VoyageGridCoordinate first = bank.First();
            visited.Add(first);
            queue.Enqueue(first);
            Dictionary<int, int> thickness = new Dictionary<int, int>();
            foreach (VoyageGridCoordinate cell in bank)
            {
                int along = horizontal ? cell.Column : cell.Row;
                int across = horizontal ? cell.Row : cell.Column;
                Assert.That(along, Is.InRange(-4, 4));
                Assert.That(across * side, Is.InRange(2, 4));
                thickness[along] = thickness.TryGetValue(along, out int count) ? count + 1 : 1;
            }
            Assert.That(thickness.Count, Is.InRange(7, 9));
            Assert.That(thickness.Count(pair => pair.Value >= 2), Is.GreaterThanOrEqualTo(5));
            Assert.That(thickness.Any(pair => pair.Value == 3), Is.True);
            Assert.That(thickness[thickness.Keys.Min()], Is.EqualTo(1), "Coast ends should taper.");
            Assert.That(thickness[thickness.Keys.Max()], Is.EqualTo(1));
            while (queue.Count > 0)
            {
                VoyageGridCoordinate current = queue.Dequeue();
                VoyageGridCoordinate[] neighbors =
                {
                    new VoyageGridCoordinate(current.Column - 1, current.Row),
                    new VoyageGridCoordinate(current.Column + 1, current.Row),
                    new VoyageGridCoordinate(current.Column, current.Row - 1),
                    new VoyageGridCoordinate(current.Column, current.Row + 1),
                };
                foreach (VoyageGridCoordinate neighbor in neighbors)
                {
                    if (bank.Contains(neighbor) && visited.Add(neighbor)) queue.Enqueue(neighbor);
                }
            }
            Assert.That(visited.Count, Is.EqualTo(bank.Count), "A bank must be four-neighbour connected, including its narrow tips.");
        }

        private static string Profile(IEnumerable<VoyageGridCoordinate> cells, bool horizontal, int side)
        {
            return string.Join(";", cells.Select(cell =>
            {
                int along = horizontal ? cell.Column : cell.Row;
                int across = (horizontal ? cell.Row : cell.Column) - side * 3;
                return along + "," + across;
            }).OrderBy(value => value, StringComparer.Ordinal));
        }
    }
}
