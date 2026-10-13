using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ChartExplorationDetailTests
    {
        [Test]
        public void ChartCellsHalveBothDistancesAndKeepLegacyKnowledgeUntilInitialized()
        {
            var map = new CoreVoyageMap();
            Assert.That(CoreVoyageMap.GridColumns, Is.EqualTo(48));
            Assert.That(CoreVoyageMap.GridRows, Is.EqualTo(32));
            Assert.That(CoreVoyageMap.ChartColumns, Is.EqualTo(96));
            Assert.That(CoreVoyageMap.ChartRows, Is.EqualTo(64));
            Assert.That(map.ChartCellCenter(1, 0).X - map.ChartCellCenter(0, 0).X,
                Is.EqualTo(map.CellWidth * .5f).Within(.0001f));
            Assert.That(map.ChartCellCenter(0, 1).Y - map.ChartCellCenter(0, 0).Y,
                Is.EqualTo(map.CellHeight * .5f).Within(.0001f));

            map.ExploredCells[4 * CoreVoyageMap.GridColumns + 3] = true;
            for (int y = 8; y <= 9; y++)
                for (int x = 6; x <= 7; x++) Assert.That(map.IsChartCellExplored(x, y), Is.True);
            Assert.That(map.IsChartCellExplored(-1, 8), Is.False);
            Assert.That(map.IsChartCellExplored(CoreVoyageMap.ChartColumns, 8), Is.False);
            map.InitializeChartExploration();
            map.ChartExploredCells[8 * CoreVoyageMap.ChartColumns + 6] = false;
            Assert.That(map.IsChartCellExplored(6, 8), Is.False);
            Assert.That(map.IsChartCellExplored(7, 8), Is.True);
            Assert.That(map.ExploredCells[4 * CoreVoyageMap.GridColumns + 3], Is.True);
        }

        [Test]
        public void ObservationRevealsIndividualFineCellsAndRetainsTheirHistory()
        {
            CoreVoyageSession session = CreateVoyage("CHART-DETAIL-OBSERVE");
            CoreVoyageMap map = session.Map;
            Array.Clear(map.ExploredCells, 0, map.ExploredCells.Length);
            map.RestoreChartExploration(new bool[CoreVoyageMap.ChartColumns * CoreVoyageMap.ChartRows]);
            for (int i = 0; i < map.TerrainCells.Length; i++) map.TerrainCells[i] = SeaTerrainType.OpenWater;
            RevealAt(session, new VoyagePoint(50f, 32f));

            int partialGroups = 0;
            for (int row = 0; row < CoreVoyageMap.GridRows; row++)
                for (int column = 0; column < CoreVoyageMap.GridColumns; column++)
                {
                    int visible = 0;
                    for (int y = 0; y < 2; y++)
                        for (int x = 0; x < 2; x++)
                            if (map.IsChartCellExplored(column * 2 + x, row * 2 + y)) visible++;
                    if (visible > 0 && visible < 4) partialGroups++;
                }
            Assert.That(partialGroups, Is.GreaterThan(0), "A finer chart must reveal subcells, not repeat each coarse cell four times.");
            bool[] previous = map.CaptureChartExploration();
            RevealAt(session, new VoyagePoint(54f, 32f));
            Assert.That(map.ChartExploredCells.Count(value => value), Is.GreaterThan(previous.Count(value => value)));
            for (int i = 0; i < previous.Length; i++)
                if (previous[i]) Assert.That(map.ChartExploredCells[i], Is.True);
        }

        [TestCase(false, 0f, 0)]
        [TestCase(true, 40f, 0)]
        [TestCase(false, -200f, 0)]
        [TestCase(false, 5000f, 0)]
        [TestCase(false, float.NaN, 0)]
        [TestCase(false, float.PositiveInfinity, 0)]
        [TestCase(false, 0f, 1)]
        [TestCase(true, 40f, 1)]
        [TestCase(false, -200f, 1)]
        [TestCase(false, 5000f, 1)]
        [TestCase(false, float.NaN, 1)]
        [TestCase(false, float.PositiveInfinity, 1)]
        public void BoundedObservationMatchesFullGridVisibilityAtEdgesAndRetainsHistory(bool navigator, float visionBonus, int geometryVersion)
        {
            var session = new CoreVoyageSession { MapGeometryVersion = geometryVersion };
            session.SetSeed("CHART-DETAIL-BOUNDED-OBSERVATION");
            session.ConfigureCrewCapabilities(navigator, false, false, false, false, false);
            session.ConfigureComponentBonuses(0, 0, 0, 0f, visionBonus);
            session.StartVoyage();
            CoreVoyageMap map = session.Map;
            var terrain = new[] { SeaTerrainType.OpenWater, SeaTerrainType.FogBank, SeaTerrainType.StormFront,
                SeaTerrainType.Island, SeaTerrainType.Reef, SeaTerrainType.Beach, SeaTerrainType.Strait };
            for (int i = 0; i < map.TerrainCells.Length; i++) map.TerrainCells[i] = terrain[i % terrain.Length];
            Array.Clear(map.ExploredCells, 0, map.ExploredCells.Length);
            map.ExploredCells[map.ExploredCells.Length - 1] = true;
            var recordedDetail = new bool[map.FineColumns * map.FineRows];
            recordedDetail[recordedDetail.Length - 1] = true;
            Assert.That(map.RestoreChartExploration(recordedDetail), Is.True);
            var canSeeCell = (Func<VoyagePoint, int, int, float, bool>)Delegate.CreateDelegate(
                typeof(Func<VoyagePoint, int, int, float, bool>), session,
                typeof(CoreVoyageSession).GetMethod("CanSeeCell", BindingFlags.Instance | BindingFlags.NonPublic));
            var canSeePoint = (Func<VoyagePoint, VoyagePoint, float, float, bool>)Delegate.CreateDelegate(
                typeof(Func<VoyagePoint, VoyagePoint, float, float, bool>), session,
                typeof(CoreVoyageSession).GetMethod("CanSeePoint", BindingFlags.Instance | BindingFlags.NonPublic));
            var edgeVariation = (Func<uint, int, int, float>)Delegate.CreateDelegate(
                typeof(Func<uint, int, int, float>),
                typeof(CoreVoyageSession).GetMethod("VisibilityEdgeVariation", BindingFlags.Static | BindingFlags.NonPublic));
            float radius = (120f + (navigator ? 60f : 0f) + session.VisionBonusNm) / map.Route.NauticalMilesPerMapUnit;
            var observers = new[] { new VoyagePoint(0f, 0f), new VoyagePoint(map.WorldWidth, map.WorldHeight),
                new VoyagePoint(0f, map.WorldHeight), new VoyagePoint(map.WorldWidth, 0f),
                new VoyagePoint(map.WorldWidth * .5f, map.WorldHeight * .5f), new VoyagePoint(0f, 64f),
                map.ChartCellCenter(39, 26), new VoyagePoint(51.3f, 33.7f) };
            foreach (VoyagePoint observer in observers)
            {
                // This oracle retains the original full-map scan, independently
                // of the optimized candidate bounds, on both resolutions.
                bool[] coarse = (bool[])map.ExploredCells.Clone();
                bool[] detail = map.CaptureChartExploration();
                for (int row = 0; row < map.Rows; row++)
                    for (int column = 0; column < map.Columns; column++)
                        coarse[row * map.Columns + column] |= canSeeCell(observer, column, row, radius);
                for (int row = 0; row < map.FineRows; row++)
                    for (int column = 0; column < map.FineColumns; column++)
                        detail[row * map.FineColumns + column] |= canSeePoint(observer,
                            map.ChartCellCenter(column, row), radius, edgeVariation(map.Hash, column, row) * 0.5f);
                RevealAt(session, observer);
                Assert.That(map.ExploredCells, Is.EqualTo(coarse), "Coarse visibility at " + observer.X + ", " + observer.Y);
                Assert.That(map.ChartExploredCells, Is.EqualTo(detail), "Chart visibility at " + observer.X + ", " + observer.Y);
            }
        }

        [Test]
        public void DetailedCheckpointRoundTripPreservesExactCellsWithoutAliasing()
        {
            CoreVoyageSession source = CreateVoyage("CHART-DETAIL-CHECKPOINT");
            var detail = new bool[CoreVoyageMap.ChartColumns * CoreVoyageMap.ChartRows];
            detail[17] = true;
            detail[CoreVoyageMap.ChartColumns + 18] = true;
            source.Map.RestoreChartExploration(detail);
            CoreVoyageCheckpoint dto = source.CaptureVoyageCheckpoint();
            string json = JsonUtility.ToJson(dto);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(json), out string reason), Is.True, reason);
            Assert.That(restored.Map.ChartExploredCells, Is.EqualTo(detail));
            Assert.That(restored.Map.TerrainCells, Is.EqualTo(source.Map.TerrainCells));
            Assert.That(restored.Map.ExploredCells, Is.EqualTo(source.Map.ExploredCells));
            Assert.That(JsonUtility.ToJson(restored.CaptureVoyageCheckpoint()), Is.EqualTo(json));
            dto.chartExploredCells[17] = false;
            source.Map.ChartExploredCells[CoreVoyageMap.ChartColumns + 18] = false;
            Assert.That(restored.Map.ChartExploredCells, Is.EqualTo(detail));
            Assert.That(source.Map.ChartExploredCells[17], Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyCheckpointExpandsOnlyItsRecordedCoarseKnowledge(bool emptyArray)
        {
            CoreVoyageSession source = CreateVoyage("CHART-DETAIL-LEGACY");
            CoreVoyageCheckpoint dto = source.CaptureVoyageCheckpoint();
            dto.chartExploredCells = emptyArray ? Array.Empty<bool>() : null;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(dto, out string reason), Is.True, reason);
            Assert.That(restored.Map.ChartExploredCells, Is.EqualTo(CoreVoyageMap.ExpandExplorationForChart(dto.exploredCells)));
            Assert.That(restored.Map.TerrainCells, Is.EqualTo(source.Map.TerrainCells));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.X), Is.EqualTo(source.Map.Nodes.Select(node => node.Position.X)));
            Assert.That(restored.Map.Nodes.Select(node => node.Position.Y), Is.EqualTo(source.Map.Nodes.Select(node => node.Position.Y)));
        }

        [Test]
        public void MalformedDetailedCheckpointCannotReplaceTheCurrentVoyage()
        {
            CoreVoyageSession source = CreateVoyage("CHART-DETAIL-INVALID");
            CoreVoyageCheckpoint dto = source.CaptureVoyageCheckpoint();
            dto.chartExploredCells = new bool[3];
            CoreVoyageSession target = CreateVoyage("CHART-DETAIL-PROTECTED");
            string before = JsonUtility.ToJson(target.CaptureVoyageCheckpoint());
            Assert.That(target.TryRestoreVoyageCheckpoint(dto, out _), Is.False);
            Assert.That(JsonUtility.ToJson(target.CaptureVoyageCheckpoint()), Is.EqualTo(before));
        }

        private static CoreVoyageSession CreateVoyage(string seed)
        {
            // These fixtures exercise legacy 48 x 32 exploration and save data.
            var session = new CoreVoyageSession { MapGeometryVersion = 0 };
            session.SetSeed(seed);
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            return session;
        }

        private static void RevealAt(CoreVoyageSession session, VoyagePoint point) =>
            typeof(CoreVoyageSession).GetMethod("RevealAt", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { point });
    }
}
