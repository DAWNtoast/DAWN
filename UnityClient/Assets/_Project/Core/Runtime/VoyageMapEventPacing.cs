using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public static partial class VoyageMapGenerator
    {
        // SS4 keeps three ordinary, sighted places near the real sailing course.
        // All other locations retain the full chart as exploration space. Legacy
        // maps never enter these rules, including their old random draw order.
        private static bool UsesPacedEventLayout(CoreVoyageMap map) => map.GeometryVersion >= 2 &&
            (map.Route.Id == "route_shattered_sail_reef" || map.Route.Id == "route_golden_tide_trade" ||
             map.Route.Id == "route_moon_tide_ruins");

        private static Dictionary<int, int> BuildPacedRouteBands(CoreVoyageMap map, List<int> placementOrder, int firstBandCandidate = 0)
        {
            var candidates = new List<int>();
            for (int index = 0; index < map.Nodes.Count; index++)
            {
                CoreMapNode node = map.Nodes[index];
                if (!node.IsBoss && node.Encounter.CanDiscoverBySight && node.Encounter.BossKind == CoreBossKind.None)
                    candidates.Add(index);
            }
            candidates.Sort((left, right) =>
            {
                // A ship or reef marker can sit close to the lane without an
                // island forcing the navigator to take an invisible detour.
                int weight = PlacementWeight(map.Nodes[left].TerrainConstraint).CompareTo(PlacementWeight(map.Nodes[right].TerrainConstraint));
                if (weight != 0) return weight;
                uint a = Mix(map.Hash ^ 0x1C79B4E5u, (uint)map.Nodes[left].Id, 0u);
                uint b = Mix(map.Hash ^ 0x1C79B4E5u, (uint)map.Nodes[right].Id, 0u);
                return a != b ? a.CompareTo(b) : left.CompareTo(right);
            });
            var bands = new Dictionary<int, int>();
            if (firstBandCandidate > 0 && firstBandCandidate < candidates.Count)
            {
                int first = candidates[0];
                candidates[0] = candidates[firstBandCandidate];
                candidates[firstBandCandidate] = first;
            }
            for (int band = 0; band < Math.Min(3, candidates.Count); band++)
                bands.Add(map.Nodes[candidates[band]].Id, band);
            // Reserve the boss first, then the three course opportunities before
            // ordinary footprints can use their narrow early/middle/late bands.
            placementOrder.Sort((left, right) =>
            {
                CoreMapNode a = map.Nodes[left], b = map.Nodes[right];
                if (a.IsBoss != b.IsBoss) return a.IsBoss ? -1 : 1;
                int aBand = bands.TryGetValue(a.Id, out int av) ? av : 3;
                int bBand = bands.TryGetValue(b.Id, out int bv) ? bv : 3;
                if (aBand != bBand) return aBand.CompareTo(bBand);
                int weight = PlacementWeight(b.TerrainConstraint).CompareTo(PlacementWeight(a.TerrainConstraint));
                return weight != 0 ? weight : a.Id.CompareTo(b.Id);
            });
            return bands;
        }

        private static bool FitsPacedSailingCourse(CoreVoyageMap map, VoyagePoint anchor, FootprintPlan plan, int band)
        {
            if (band >= 0)
            {
                float offset = CourseOffset(map, anchor, out float progress);
                float start = band == 0 ? .10f : band == 1 ? .40f : .80f;
                float end = band == 0 ? .17f : band == 1 ? .58f : .90f;
                // At 2.1 units even the smallest base sight radius (120/27)
                // sees a reef marker after conservative terrain/edge penalties.
                if (progress < start || progress > end || offset > 2.1f) return false;
            }
            // Reserve a navigable lane, not an illustrative line: no authored
            // island cell may intersect the port-to-port course. The half-cell
            // diagonal bound covers every point the navigation sampler can use.
            float laneRadius = (float)Math.Sqrt(map.CellWidth * map.CellWidth + map.CellHeight * map.CellHeight) * .55f;
            foreach (TerrainStamp stamp in plan.Stamps)
            {
                if (stamp.Terrain != SeaTerrainType.Island) continue;
                var point = new VoyagePoint(anchor.X + stamp.ColumnOffset * map.CellWidth,
                    anchor.Y + stamp.RowOffset * map.CellHeight);
                if (CourseOffset(map, point, out _) <= laneRadius) return false;
            }
            return true;
        }

        private static FootprintPlan BuildPlacedEventPlan(CoreVoyageMap map, CoreMapNode node, int orientation, uint seed)
        {
            if (!UsesPacedEventLayout(map) || node.IsBoss) return BuildPlan(node.TerrainConstraint, orientation, seed);
            var builder = new ShapeBuilder();
            switch (node.TerrainConstraint)
            {
                // More event locations need slightly smaller terrain features,
                // not overlapping safety bands or silently dropped locations.
                case EventTerrainConstraint.Reef:
                case EventTerrainConstraint.Fog:
                case EventTerrainConstraint.Storm:
                    SeaTerrainType terrain = node.TerrainConstraint == EventTerrainConstraint.Reef ? SeaTerrainType.Reef :
                        node.TerrainConstraint == EventTerrainConstraint.Fog ? SeaTerrainType.FogBank : SeaTerrainType.StormFront;
                    AddIrregularEllipse(builder, 0, 0, 2, 2, terrain, seed);
                    builder.Set(0, 0, node.TerrainConstraint == EventTerrainConstraint.Reef ? SeaTerrainType.OpenWater : terrain);
                    return builder.Build(orientation);
                case EventTerrainConstraint.Beach:
                    DirectionForOrientation(orientation, out int dx, out int dy);
                    HashSet<VoyageGridCoordinate> land = BuildIrregularEllipseCells(dx * 3, dy * 3, 1, 1, seed);
                    land.Add(new VoyageGridCoordinate(dx * 3 - 1, dy * 3));
                    land.Add(new VoyageGridCoordinate(dx * 3 + 1, dy * 3));
                    land.Add(new VoyageGridCoordinate(dx * 3, dy * 3 - 1));
                    land.Add(new VoyageGridCoordinate(dx * 3, dy * 3 + 1));
                    AddShoalRing(builder, land);
                    foreach (VoyageGridCoordinate cell in land) builder.Set(cell.Column, cell.Row, SeaTerrainType.Island);
                    builder.Set(0, 0, SeaTerrainType.Beach);
                    builder.Set(dx, dy, SeaTerrainType.Beach);
                    builder.AddIslandCenter(dx * 3, dy * 3);
                    return builder.Build(orientation);
                default: return BuildPlan(node.TerrainConstraint, orientation, seed);
            }
        }

        private static void ProtectPacedSailingCourse(CoreVoyageMap map, bool[] protectedRoute)
        {
            float laneRadius = (float)Math.Sqrt(map.CellWidth * map.CellWidth + map.CellHeight * map.CellHeight) * .55f;
            for (int row = 0; row < map.Rows; row++)
            for (int column = 0; column < map.Columns; column++)
                if (CourseOffset(map, map.CellCenter(column, row), out _) <= laneRadius)
                    protectedRoute[row * map.Columns + column] = true;
        }

        private static float CourseOffset(CoreVoyageMap map, VoyagePoint point, out float progress)
        {
            float dx = map.Destination.X - map.Departure.X, dy = map.Destination.Y - map.Departure.Y;
            float lengthSquared = dx * dx + dy * dy;
            progress = lengthSquared > 0f ? ((point.X - map.Departure.X) * dx + (point.Y - map.Departure.Y) * dy) / lengthSquared : 0f;
            float clamped = Math.Max(0f, Math.Min(1f, progress));
            return Distance(point, new VoyagePoint(map.Departure.X + dx * clamped, map.Departure.Y + dy * clamped));
        }
    }
}
