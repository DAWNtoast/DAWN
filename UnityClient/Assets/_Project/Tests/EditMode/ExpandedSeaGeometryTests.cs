using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ExpandedSeaGeometryTests
    {
        [Test]
        public void ExpandedSea_SailsBeyondLegacyNorthEdgeAndRevealsRealCellsWithNormalCosts()
        {
            CoreVoyageSession session = CreateVoyage(1, "EXPANDED-SEA-SAIL");
            CoreVoyageMap map = session.Map;
            Assert.That(map.GeometryVersion, Is.EqualTo(1));
            Assert.That(map.Columns, Is.EqualTo(48));
            Assert.That(map.Rows, Is.EqualTo(36));
            Assert.That(map.WorldWidth, Is.EqualTo(100f));
            Assert.That(map.WorldHeight, Is.EqualTo(75f));
            Assert.That(map.CellWidth, Is.EqualTo(map.CellHeight).Within(.00001f), "The new world has square terrain cells.");
            Assert.That(map.ChartCellCenter(1, 0).X - map.ChartCellCenter(0, 0).X,
                Is.EqualTo(map.ChartCellCenter(0, 1).Y - map.ChartCellCenter(0, 0).Y).Within(.00001f));

            // Isolate the boundary/route behavior from incidental event encounters.
            map.Nodes.Clear();
            Array.Clear(map.TerrainCells, 0, map.TerrainCells.Length);
            Array.Clear(map.ExploredCells, 0, map.ExploredCells.Length);
            Array.Clear(map.VoyageExploredCells, 0, map.VoyageExploredCells.Length);
            Assert.That(map.RestoreChartExploration(new bool[map.FineColumns * map.FineRows]), Is.True);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(50f, 63f));
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, true);
            session.SetCourseTo(new VoyagePoint(50f, 71f));
            SailingForecast forecast = session.GetForecast();
            Assert.That(forecast.Blocked, Is.False);
            Assert.That(forecast.Clipped, Is.False, "The former north boundary is now ordinary navigable water.");
            Assert.That(forecast.Target.Y, Is.GreaterThan(64f));
            Assert.That(forecast.PathPoints.Any(point => point.Y > 64f), Is.True);
            Assert.That(forecast.WaterCost, Is.GreaterThan(0));
            int water = session.Cargo.Water, food = session.Cargo.Food;
            double hours = session.ElapsedGameHours;
            float distance = session.TotalDistanceNm;

            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Position.Y, Is.EqualTo(forecast.Target.Y).Within(.001f));
            Assert.That(session.TotalDistanceNm - distance, Is.EqualTo(forecast.ActualDistanceNm).Within(.001f));
            Assert.That(session.ElapsedGameHours - hours, Is.EqualTo(forecast.Hours).Within(.0001d));
            Assert.That(water - session.Cargo.Water, Is.EqualTo(forecast.WaterCost));
            Assert.That(food - session.Cargo.Food, Is.EqualTo(forecast.FoodCost));
            Assert.That(Enumerable.Range(0, map.CellCount).Any(index =>
                map.CellCenter(index % map.Columns, index / map.Columns).Y > 64f && map.VoyageExploredCells[index]), Is.True,
                "Sailing through the extension must record new exploration, not just paint extra grid lines.");
            Assert.That(Enumerable.Range(0, map.ChartExploredCells.Length).Any(index =>
                map.ChartCellCenter(index % map.FineColumns, index / map.FineColumns).Y > 64f && map.ChartExploredCells[index]), Is.True);
        }

        [Test]
        public void ExpandedSea_CheckpointRoundTripKeepsNewRowsAndWorldIdentity()
        {
            CoreVoyageSession source = CreateVoyage(1, "EXPANDED-SEA-CHECKPOINT");
            CoreVoyageMap map = source.Map;
            int cell = Enumerable.Range(0, map.CellCount).First(index =>
                map.CellCenter(index % map.Columns, index / map.Columns).Y > 66f &&
                map.IsPassable(index % map.Columns, index / map.Columns));
            VoyagePoint position = map.CellCenter(cell % map.Columns, cell / map.Columns);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(source, position);
            map.ExploredCells[cell] = true;
            map.VoyageExploredCells[cell] = true;
            Assert.That(map.RestoreChartExploration(new bool[map.FineColumns * map.FineRows]), Is.True);
            int fineCell = (cell / map.Columns * 2) * map.FineColumns + cell % map.Columns * 2;
            map.ChartExploredCells[fineCell] = true;
            string json = JsonUtility.ToJson(source.CaptureVoyageCheckpoint());
            CoreVoyageCheckpoint saved = JsonUtility.FromJson<CoreVoyageCheckpoint>(json);
            Assert.That(saved.mapGeometryVersion, Is.EqualTo(1));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(saved, out string reason), Is.True, reason);
            Assert.That(restored.Map.GeometryVersion, Is.EqualTo(1));
            Assert.That(restored.Map.Rows, Is.EqualTo(36));
            Assert.That(restored.Map.FineRows, Is.EqualTo(72));
            Assert.That(restored.Map.WorldHeight, Is.EqualTo(75f));
            Assert.That(restored.Map.Hash, Is.EqualTo(map.Hash));
            Assert.That(restored.Position.Y, Is.EqualTo(position.Y));
            Assert.That(restored.Map.TerrainCells, Is.EqualTo(map.TerrainCells));
            Assert.That(restored.Map.ExploredCells, Is.EqualTo(map.ExploredCells));
            Assert.That(restored.Map.VoyageExploredCells, Is.EqualTo(map.VoyageExploredCells));
            Assert.That(restored.Map.ChartExploredCells, Is.EqualTo(map.ChartExploredCells));
            Assert.That(JsonUtility.ToJson(restored.CaptureVoyageCheckpoint()), Is.EqualTo(json));
            saved.chartExploredCells[fineCell] = false;
            map.ChartExploredCells[fineCell] = false;
            Assert.That(restored.Map.ChartExploredCells[fineCell], Is.True, "Restored exploration must own its data.");
        }

        [Test]
        public void LegacyCheckpointWithoutGeometryVersionRetainsOriginalTerrainAndBounds()
        {
            const string legacySeed = "SS2.0.0.1.0.0.2EDC24E9.0000C0DE.4DD97F4E";
            var source = new CoreVoyageSession();
            Assert.That(source.TrySelectChartSeed(legacySeed, out string selectionReason), Is.True, selectionReason);
            source.StartVoyage();
            CoreVoyageMap original = source.Map;
            Assert.That(original.GeometryVersion, Is.Zero);
            string json = JsonUtility.ToJson(source.CaptureVoyageCheckpoint())
                .Replace("\"mapGeometryVersion\":0,", string.Empty)
                .Replace(",\"mapGeometryVersion\":0", string.Empty);
            Assert.That(json, Does.Not.Contain("mapGeometryVersion"));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(json), out string reason), Is.True, reason);
            Assert.That(restored.Map.GeometryVersion, Is.Zero);
            Assert.That(restored.Map.Columns, Is.EqualTo(48));
            Assert.That(restored.Map.Rows, Is.EqualTo(32));
            Assert.That(restored.Map.FineColumns, Is.EqualTo(96));
            Assert.That(restored.Map.FineRows, Is.EqualTo(64));
            Assert.That(restored.Map.WorldWidth, Is.EqualTo(100f));
            Assert.That(restored.Map.WorldHeight, Is.EqualTo(64f));
            Assert.That(restored.Map.Hash, Is.EqualTo(original.Hash));
            Assert.That(restored.Map.Seed, Is.EqualTo(original.Seed));
            Assert.That(restored.CaptureVoyageCheckpoint().activeChartSeed, Is.EqualTo(legacySeed));
            Assert.That(restored.Map.TerrainCells, Is.EqualTo(original.TerrainCells));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.X), Is.EqualTo(original.Nodes.Select(node => node.Position.X)));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.Y), Is.EqualTo(original.Nodes.Select(node => node.Position.Y)));
            Assert.That(restored.Map.ExploredCells, Is.EqualTo(original.ExploredCells));
            Assert.That(restored.Map.ChartExploredCells, Is.EqualTo(original.ChartExploredCells));
        }

        [TestCase(0, "SS2", 32, 64f)]
        [TestCase(1, "SS3", 36, 75f)]
        [TestCase(2, "SS4", 36, 75f)]
        [TestCase(3, "SS5", 36, 75f)]
        [TestCase(4, "SS6", 34, 100f * 34f / 48f)]
        public void EveryChartFormatKeepsItsOwnWorldAndExplorationOnResume(int version, string prefix, int rows, float height)
        {
            // Kind 1 is the supported imported-chart format, with a fixed geography hash.
            string payload = prefix + ".0.0.1.0.1.1234ABCD.00000000";
            string seed = payload + "." + SeedHash.Compute(payload).ToString("X8");
            var source = new CoreVoyageSession();
            Assert.That(source.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            source.Cargo.Water = source.Cargo.Food = 100;
            source.StartVoyage();
            Assert.That(source.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            CoreVoyageMap map = source.Map;
            int edge = Enumerable.Range(0, map.CellCount).Last(index => map.IsPassable(index % map.Columns, index / map.Columns));
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(source, map.CellCenter(edge % map.Columns, edge / map.Columns));
            map.ExploredCells[edge] = map.VoyageExploredCells[edge] = true;
            map.InitializeChartExploration();
            map.ChartExploredCells[map.ChartExploredCells.Length - 1] = true;

            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(decoded, out reason), Is.True, reason);
            Assert.That(restored.Map.GeometryVersion, Is.EqualTo(version));
            Assert.That(restored.Map.Rows, Is.EqualTo(rows));
            Assert.That(restored.Map.FineRows, Is.EqualTo(rows * 2));
            Assert.That(restored.Map.WorldHeight, Is.EqualTo(height).Within(.00001f));
            Assert.That(restored.CurrentChartSeed, Is.EqualTo(seed));
            Assert.That(restored.Position.Y, Is.EqualTo(source.Position.Y));
            Assert.That(restored.Map.TerrainCells, Is.EqualTo(map.TerrainCells));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.X), Is.EqualTo(map.Nodes.Select(node => node.Position.X)));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.Y), Is.EqualTo(map.Nodes.Select(node => node.Position.Y)));
            Assert.That(restored.Map.ExploredCells, Is.EqualTo(map.ExploredCells));
            Assert.That(restored.Map.VoyageExploredCells, Is.EqualTo(map.VoyageExploredCells));
            Assert.That(restored.Map.ChartExploredCells, Is.EqualTo(map.ChartExploredCells));
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void NewlyCraftedTreasureChartsUseCompactBoundsAndRestoreEveryLocation(string rank)
        {
            var source = new CoreVoyageSession();
            source.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5),
                identifiedWeatheredFragments = rank == "weathered" ? 4 : 0,
                identifiedGildedFragments = rank == "gilded" ? 4 : 0,
                identifiedMoonlitFragments = rank == "moonlit" ? 4 : 0
            });
            string seed = "COMPACT-TREASURE-" + rank;
            Assert.That(source.TryCraftTreasureMapForRank(rank, seed, out string reason), Is.True, reason);
            Assert.That(source.TrySelectTreasureSeed(seed, out reason), Is.True, reason);
            Assert.That(source.SelectedTreasureMap.MapGeometryVersion, Is.EqualTo(4));
            source.Cargo.Water = source.Cargo.Food = 100;
            Assert.That(source.TryStartTreasureVoyage(out reason), Is.True, reason);
            Assert.That(source.Map.Rows, Is.EqualTo(34));
            foreach (CoreMapNode node in source.Map.Nodes)
            {
                Assert.That(node.Position.X, Is.InRange(0f, source.Map.WorldWidth), node.Encounter.Id);
                Assert.That(node.Position.Y, Is.InRange(0f, source.Map.WorldHeight), node.Encounter.Id);
                Assert.That(source.PlanNavigablePath(source.Map.Departure, node.Position).Count,
                    Is.GreaterThanOrEqualTo(2), "Unreachable compact treasure location " + node.Encounter.Id);
            }
            var restored = new CoreVoyageSession();
            CoreVoyageCheckpoint checkpoint = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out reason), Is.True, reason);
            Assert.That(restored.Map.GeometryVersion, Is.EqualTo(4));
            Assert.That(restored.ActiveTreasureMap.MapGeometryVersion, Is.EqualTo(4));
            Assert.That(restored.Map.TerrainCells, Is.EqualTo(source.Map.TerrainCells));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.X), Is.EqualTo(source.Map.Nodes.Select(node => node.Position.X)));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.Y), Is.EqualTo(source.Map.Nodes.Select(node => node.Position.Y)));
            Assert.That(restored.Map.ChartExploredCells, Is.EqualTo(source.Map.ChartExploredCells));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ExistingTreasureMapsKeepTheirOriginalRowsAndWorldWhenStartedAndResumed(int version)
        {
            var source = new CoreVoyageSession();
            source.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5), selectedMapId = "legacy-bounds",
                maps = new[] { new CoreTreasureMapSave { id = "legacy-bounds", seed = "LEGACY-TREASURE-BOUNDS",
                    rankId = "weathered", mapGeometryVersion = version, contentVersion = 1 } }
            });
            source.Cargo.Water = source.Cargo.Food = 100;
            Assert.That(source.TryStartTreasureVoyage(out string reason), Is.True, reason);
            Assert.That(source.Map.GeometryVersion, Is.EqualTo(version));
            Assert.That(source.Map.Rows, Is.EqualTo(version == 0 ? 32 : 36));
            Assert.That(source.Map.WorldHeight, Is.EqualTo(version == 0 ? 64f : 75f));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(source.CaptureVoyageCheckpoint(), out reason), Is.True, reason);
            Assert.That(restored.ActiveTreasureMap.MapGeometryVersion, Is.EqualTo(version));
            Assert.That(restored.Map.TerrainCells, Is.EqualTo(source.Map.TerrainCells));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.Y), Is.EqualTo(source.Map.Nodes.Select(node => node.Position.Y)));
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void CheckpointGeometryMismatchOrUnknownVersionCannotReplacePortState(int invalidVersion)
        {
            CoreVoyageSession source = CreateVoyage(1, "EXPANDED-SEA-INVALID");
            CoreVoyageCheckpoint invalid = source.CaptureVoyageCheckpoint();
            var validControl = new CoreVoyageSession();
            Assert.That(validControl.TryRestoreVoyageCheckpoint(invalid, out string validReason), Is.True, validReason);
            invalid.mapGeometryVersion = invalidVersion;
            var target = new CoreVoyageSession();
            target.SetSeed("EXPANDED-SEA-PROTECTED");
            target.GrantProfileGold(123, "保护恢复前的港口状态");
            string beforeCharts = JsonUtility.ToJson(target.CaptureSeaChartProgress());
            string beforeTreasure = JsonUtility.ToJson(target.CaptureTreasureProgress());
            decimal beforeGold = target.ProfileGold;
            string beforeSeed = target.SelectedSeed, beforePort = target.CurrentPort;
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port), "Reach geometry validation rather than the active-voyage guard.");
            Assert.That(target.TryRestoreVoyageCheckpoint(invalid, out _), Is.False);
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(target.Map, Is.Null);
            Assert.That(target.ProfileGold, Is.EqualTo(beforeGold));
            Assert.That(target.SelectedSeed, Is.EqualTo(beforeSeed));
            Assert.That(target.CurrentPort, Is.EqualTo(beforePort));
            Assert.That(JsonUtility.ToJson(target.CaptureSeaChartProgress()), Is.EqualTo(beforeCharts));
            Assert.That(JsonUtility.ToJson(target.CaptureTreasureProgress()), Is.EqualTo(beforeTreasure),
                "Invalid geometry cannot truncate exploration or replace the protected port state.");
        }

        private static CoreVoyageSession CreateVoyage(int geometryVersion, string seed)
        {
            var session = new CoreVoyageSession { MapGeometryVersion = geometryVersion };
            session.SetSeed(seed);
            session.Cargo.Water = session.Cargo.Food = 200;
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            return session;
        }
    }
}
