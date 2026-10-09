using System;
using System.Collections.Generic;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// Engine-independent integer coordinate used by the deterministic sea chart.
    /// </summary>
    public struct VoyageGridCoordinate : IEquatable<VoyageGridCoordinate>
    {
        public VoyageGridCoordinate(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public int Column { get; }
        public int Row { get; }

        public bool Equals(VoyageGridCoordinate other)
        {
            return Column == other.Column && Row == other.Row;
        }

        public override bool Equals(object obj)
        {
            return obj is VoyageGridCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Column * 397) ^ Row;
            }
        }

        public override string ToString()
        {
            return "(" + Column + ", " + Row + ")";
        }
    }

    /// <summary>
    /// Immutable snapshot returned to renderers and map tools. Array storage stays
    /// row-major for backwards compatibility, while this view keeps callers from
    /// having to coordinate seven parallel arrays themselves.
    /// </summary>
    public struct CoreVoyageMapCell
    {
        internal CoreVoyageMapCell(
            int column,
            int row,
            VoyagePoint center,
            SeaTerrainType terrain,
            bool explored,
            bool passable,
            VoyagePoint currentVector,
            float currentStrength,
            int terrainFeatureId,
            int anchorNodeId,
            bool isEventAnchor,
            bool isEventReserved,
            byte terrainVariant)
        {
            Column = column;
            Row = row;
            Center = center;
            Terrain = terrain;
            Explored = explored;
            Passable = passable;
            CurrentVector = currentVector;
            CurrentStrength = currentStrength;
            TerrainFeatureId = terrainFeatureId;
            AnchorNodeId = anchorNodeId;
            IsEventAnchor = isEventAnchor;
            IsEventReserved = isEventReserved;
            TerrainVariant = terrainVariant;
        }

        public int Column { get; }
        public int Row { get; }
        public VoyagePoint Center { get; }
        public SeaTerrainType Terrain { get; }
        public bool Explored { get; }
        public bool Passable { get; }
        public VoyagePoint CurrentVector { get; }
        public float CurrentStrength { get; }
        public int TerrainFeatureId { get; }
        public int AnchorNodeId { get; }
        public bool IsEventAnchor { get; }
        public bool IsEventReserved { get; }
        public byte TerrainVariant { get; }
        public bool BelongsToEvent => AnchorNodeId >= 0;
    }

    /// <summary>
    /// Exact cells owned by a fixed event plus the one-cell placement safety band.
    /// ReservedCellIndices are disjoint between all events on a generated chart.
    /// </summary>
    public sealed class CoreEventFootprint
    {
        private readonly int[] cellIndices;
        private readonly int[] reservedCellIndices;

        internal CoreEventFootprint(
            int nodeId,
            EventTerrainConstraint terrainConstraint,
            VoyageGridCoordinate anchor,
            int orientation,
            int[] cells,
            int[] reservedCells,
            int minimumColumn,
            int maximumColumn,
            int minimumRow,
            int maximumRow)
        {
            NodeId = nodeId;
            TerrainConstraint = terrainConstraint;
            Anchor = anchor;
            Orientation = orientation;
            cellIndices = cells ?? Array.Empty<int>();
            reservedCellIndices = reservedCells ?? Array.Empty<int>();
            MinColumn = minimumColumn;
            MaxColumn = maximumColumn;
            MinRow = minimumRow;
            MaxRow = maximumRow;
        }

        public int NodeId { get; }
        public EventTerrainConstraint TerrainConstraint { get; }
        public VoyageGridCoordinate Anchor { get; }
        public int Orientation { get; }
        public IReadOnlyList<int> CellIndices => cellIndices;
        public IReadOnlyList<int> ReservedCellIndices => reservedCellIndices;
        public int MinColumn { get; }
        public int MaxColumn { get; }
        public int MinRow { get; }
        public int MaxRow { get; }

        public bool ContainsCell(int column, int row)
        {
            if (column < MinColumn || column > MaxColumn || row < MinRow || row > MaxRow)
            {
                return false;
            }

            int index = row * CoreVoyageMap.GridColumns + column;
            return Array.BinarySearch(cellIndices, index) >= 0;
        }

        public bool ReservesCell(int column, int row)
        {
            if (column < 0 || column >= CoreVoyageMap.GridColumns || row < 0 || row >= CoreVoyageMap.GridRows)
            {
                return false;
            }

            int index = row * CoreVoyageMap.GridColumns + column;
            return Array.BinarySearch(reservedCellIndices, index) >= 0;
        }
    }

    /// <summary>
    /// Generates the logical chart in four deterministic stages:
    /// 1. reserve non-overlapping fixed-event footprints;
    /// 2. stamp each event's required geography;
    /// 3. grow connected straits and islands in free cells;
    /// 4. fill every still-unclaimed cell with open blue water, then assign
    ///    stable feature ids and per-cell visual variants.
    ///
    /// No Unity APIs or process-dependent hashes are used. Identical inputs produce
    /// byte-for-byte identical terrain and ownership arrays on every platform.
    /// </summary>
    public static class VoyageMapGenerator
    {
        private const int EventSafetyBandCells = 1;
        private const int PlacementAttempts = 768;
        private const int IslandPlacementAttempts = 320;
        private const int PortReserveRadiusCells = 3;
        private const uint PlacementStream = 0x43A8F12Du;
        private const uint ShapeStream = 0xB7E15162u;
        private const uint StraitStream = 0x8AED2A6Bu;
        private const uint IslandStream = 0xC13FA9A9u;
        private const uint OceanStream = 0x91E10DA5u;
        private const uint VariantStream = 0xD1B54A35u;

        private struct TerrainStamp
        {
            public TerrainStamp(int columnOffset, int rowOffset, SeaTerrainType terrain)
            {
                ColumnOffset = columnOffset;
                RowOffset = rowOffset;
                Terrain = terrain;
            }

            public int ColumnOffset;
            public int RowOffset;
            public SeaTerrainType Terrain;
        }

        private sealed class FootprintPlan
        {
            public TerrainStamp[] Stamps;
            public VoyageGridCoordinate[] ReservedOffsets;
            public VoyageGridCoordinate[] IslandCenters;
            public int Orientation;
        }

        private sealed class PlacedEvent
        {
            public CoreMapNode Node;
            public VoyageGridCoordinate Anchor;
            public FootprintPlan Plan;
        }

        private sealed class ShapeBuilder
        {
            private readonly Dictionary<VoyageGridCoordinate, SeaTerrainType> terrain =
                new Dictionary<VoyageGridCoordinate, SeaTerrainType>();
            private readonly List<VoyageGridCoordinate> islandCenters = new List<VoyageGridCoordinate>();

            public void Set(int column, int row, SeaTerrainType value)
            {
                terrain[new VoyageGridCoordinate(column, row)] = value;
            }

            public void AddIslandCenter(int column, int row)
            {
                islandCenters.Add(new VoyageGridCoordinate(column, row));
            }

            public FootprintPlan Build(int orientation)
            {
                List<TerrainStamp> stamps = new List<TerrainStamp>(terrain.Count);
                HashSet<VoyageGridCoordinate> reserved = new HashSet<VoyageGridCoordinate>();
                foreach (KeyValuePair<VoyageGridCoordinate, SeaTerrainType> pair in terrain)
                {
                    stamps.Add(new TerrainStamp(pair.Key.Column, pair.Key.Row, pair.Value));
                    for (int y = -EventSafetyBandCells; y <= EventSafetyBandCells; y += 1)
                    {
                        for (int x = -EventSafetyBandCells; x <= EventSafetyBandCells; x += 1)
                        {
                            reserved.Add(new VoyageGridCoordinate(pair.Key.Column + x, pair.Key.Row + y));
                        }
                    }
                }

                stamps.Sort((left, right) =>
                {
                    int row = left.RowOffset.CompareTo(right.RowOffset);
                    return row != 0 ? row : left.ColumnOffset.CompareTo(right.ColumnOffset);
                });
                List<VoyageGridCoordinate> reservedList = new List<VoyageGridCoordinate>(reserved);
                reservedList.Sort(CompareCoordinates);
                islandCenters.Sort(CompareCoordinates);
                return new FootprintPlan
                {
                    Stamps = stamps.ToArray(),
                    ReservedOffsets = reservedList.ToArray(),
                    IslandCenters = islandCenters.ToArray(),
                    Orientation = orientation,
                };
            }
        }

        public static void Generate(
            CoreVoyageMap map,
            float minimumEventSpacing,
            float bossBorderMargin,
            float bossDepartureClearance,
            float bossDestinationClearance,
            float bossDestinationApproachRadius)
        {
            GenerateInternal(map, minimumEventSpacing, bossBorderMargin,
                bossDepartureClearance, bossDestinationClearance, bossDestinationApproachRadius, false);
        }

        /// <summary>Authors event anchors while retaining the normal terrain, coast and current data pipeline.</summary>
        public static void GenerateFixedLayout(CoreVoyageMap map)
        {
            GenerateInternal(map, 0f, 0f, 0f, 0f, 0f, true);
        }

        private static void GenerateInternal(CoreVoyageMap map, float minimumEventSpacing,
            float bossBorderMargin, float bossDepartureClearance, float bossDestinationClearance,
            float bossDestinationApproachRadius, bool useAuthoredPositions)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            if (map.Route == null)
            {
                throw new ArgumentException("A route is required before generating a voyage map.", nameof(map));
            }
            if (map.Nodes.Count == 0)
            {
                throw new ArgumentException("At least one fixed event is required before generating a voyage map.", nameof(map));
            }

            HashSet<int> nodeIds = new HashSet<int>();
            for (int index = 0; index < map.Nodes.Count; index += 1)
            {
                CoreMapNode node = map.Nodes[index];
                if (node == null || node.Id < 0 || !nodeIds.Add(node.Id))
                {
                    throw new ArgumentException("Fixed event node ids must be unique non-negative integers.", nameof(map));
                }
            }

            ResetMapStorage(map);
            bool[] portReserved = BuildPortReserve(map);
            int[] reservationOwners = new int[map.CellCount];
            for (int index = 0; index < reservationOwners.Length; index += 1)
            {
                reservationOwners[index] = -1;
            }

            PlacedEvent[] placements = useAuthoredPositions ? PlaceAuthoredEvents(map, portReserved, reservationOwners) : PlaceFixedEvents(
                map,
                minimumEventSpacing,
                bossBorderMargin,
                bossDepartureClearance,
                bossDestinationClearance,
                bossDestinationApproachRadius,
                portReserved,
                reservationOwners);

            map.MutableEventFootprints.Sort((left, right) => left.NodeId.CompareTo(right.NodeId));
            StampEventGeography(map, placements);
            bool[] guaranteedRoute = BuildGuaranteedNavigableRoute(map);
            if (useAuthoredPositions) ProtectAuthoredCourse(map, guaranteedRoute);
            int nextFeatureId = NextFeatureId(map);
            nextFeatureId = GenerateNaturalStraits(map, guaranteedRoute, nextFeatureId);
            nextFeatureId = GenerateOrganicIslands(map, portReserved, guaranteedRoute, nextFeatureId);
            FillRemainingWithOpenWater(map);
            ClearPortApproaches(map, portReserved);
            AssignUnownedFeatureIds(map, nextFeatureId);
            AssignTerrainVariants(map);
        }

        private static PlacedEvent[] PlaceAuthoredEvents(CoreVoyageMap map, bool[] portReserved, int[] reservationOwners)
        {
            var placements = new PlacedEvent[map.Nodes.Count];
            var completed = new List<PlacedEvent>();
            for (int index = 0; index < map.Nodes.Count; index++)
            {
                CoreMapNode node = map.Nodes[index];
                VoyageGridCoordinate anchor = map.PointToCell(node.Position);
                uint shapeSeed = Mix(map.Hash ^ ShapeStream, (uint)node.Id, (uint)node.TerrainConstraint);
                FootprintPlan plan = BuildPlan(node.TerrainConstraint, 0, shapeSeed);
                if (!IsValidPlacement(map, node, anchor, plan, completed, 0f, 0f, 0f, 0f, 0f,
                    portReserved, reservationOwners, out _))
                    throw new ArgumentException("Authored event anchors must preserve port and event safety bands.", nameof(map));
                var placement = new PlacedEvent { Node = node, Anchor = anchor, Plan = plan };
                ReservePlacement(map, placement, reservationOwners);
                placements[index] = placement;
                completed.Add(placement);
            }
            return placements;
        }

        private static void ProtectAuthoredCourse(CoreVoyageMap map, bool[] protectedRoute)
        {
            VoyagePoint from = map.Departure;
            for (int waypoint = 0; waypoint <= map.Nodes.Count; waypoint++)
            {
                VoyagePoint to = waypoint < map.Nodes.Count ? map.Nodes[waypoint].Position : map.Destination;
                int steps = Math.Max(1, (int)Math.Ceiling(Distance(from, to) / Math.Min(map.CellWidth, map.CellHeight) * 2f));
                for (int step = 0; step <= steps; step++)
                {
                    float t = step / (float)steps;
                    VoyageGridCoordinate cell = map.PointToCell(new VoyagePoint(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t));
                    for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                        if (map.IsInBounds(cell.Column + x, cell.Row + y))
                            protectedRoute[(cell.Row + y) * CoreVoyageMap.GridColumns + cell.Column + x] = true;
                }
                from = to;
            }
        }

        private static void ResetMapStorage(CoreVoyageMap map)
        {
            map.Islands.Clear();
            map.MutableEventFootprints.Clear();
            for (int index = 0; index < map.CellCount; index += 1)
            {
                map.TerrainCells[index] = SeaTerrainType.OpenWater;
                map.TerrainFeatureIds[index] = -1;
                map.AnchorNodeIds[index] = -1;
                map.EventAnchorCells[index] = false;
                map.EventReservedCells[index] = false;
                map.TerrainVariants[index] = 0;
                map.CurrentVectors[index] = new VoyagePoint(0f, 0f);
                map.CurrentStrengths[index] = 0f;
            }

            for (int index = 0; index < map.Nodes.Count; index += 1)
            {
                map.Nodes[index].Footprint = null;
            }
        }

        private static PlacedEvent[] PlaceFixedEvents(
            CoreVoyageMap map,
            float minimumEventSpacing,
            float bossBorderMargin,
            float bossDepartureClearance,
            float bossDestinationClearance,
            float bossDestinationApproachRadius,
            bool[] portReserved,
            int[] reservationOwners)
        {
            // A valid early placement can strand the last footprint even though
            // the chart has enough room. Retry the complete deterministic packing,
            // preserving every safety band and the original layout on first-pass success.
            for (int restart = 0; restart < 32; restart++)
            {
                if (restart > 0)
                {
                    map.MutableEventFootprints.Clear();
                    Array.Clear(map.EventReservedCells, 0, map.EventReservedCells.Length);
                    for (int cell = 0; cell < reservationOwners.Length; cell++) reservationOwners[cell] = -1;
                    foreach (CoreMapNode node in map.Nodes) node.Footprint = null;
                }
                uint placementSeed = unchecked((map.Hash ^ PlacementStream) + (uint)restart * 0x9E3779B9u);
                PlacedEvent[] result = TryPlaceFixedEvents(map, minimumEventSpacing, bossBorderMargin,
                    bossDepartureClearance, bossDestinationClearance, bossDestinationApproachRadius,
                    portReserved, reservationOwners, placementSeed);
                if (result != null) return result;
            }
            throw new InvalidOperationException("The fixed event footprints do not fit this chart while preserving their safety bands.");
        }

        private static PlacedEvent[] TryPlaceFixedEvents(CoreVoyageMap map, float minimumEventSpacing,
            float bossBorderMargin, float bossDepartureClearance, float bossDestinationClearance,
            float bossDestinationApproachRadius, bool[] portReserved, int[] reservationOwners, uint placementSeed)
        {
            Mulberry32Random random = new Mulberry32Random(placementSeed);
            PlacedEvent[] placements = new PlacedEvent[map.Nodes.Count];
            List<int> order = BuildPlacementOrder(map);
            List<PlacedEvent> completed = new List<PlacedEvent>();

            for (int orderIndex = 0; orderIndex < order.Count; orderIndex += 1)
            {
                int nodeIndex = order[orderIndex];
                CoreMapNode node = map.Nodes[nodeIndex];
                uint nodeShapeSeed = Mix(map.Hash ^ ShapeStream, (uint)node.Id, (uint)node.TerrainConstraint);
                int orientationCount = OrientationCount(node.TerrainConstraint);
                // A shape depends only on this node's seed and orientation, not
                // its candidate anchor. Reuse it throughout placement retries.
                var orientationPlans = new FootprintPlan[orientationCount];
                int preferredOrientation = (int)(nodeShapeSeed % (uint)orientationCount);
                PlacedEvent best = null;
                float bestScore = float.MinValue;

                for (int attempt = 0; attempt < PlacementAttempts; attempt += 1)
                {
                    int orientation = (preferredOrientation + attempt) % orientationCount;
                    FootprintPlan plan = orientationPlans[orientation] ??
                        (orientationPlans[orientation] = BuildPlan(node.TerrainConstraint, orientation, nodeShapeSeed));
                    VoyageGridCoordinate anchor = new VoyageGridCoordinate(
                        1 + NextInt(random, CoreVoyageMap.GridColumns - 2),
                        1 + NextInt(random, CoreVoyageMap.GridRows - 2));
                    if (!IsValidPlacement(
                        map,
                        node,
                        anchor,
                        plan,
                        completed,
                        minimumEventSpacing,
                        bossBorderMargin,
                        bossDepartureClearance,
                        bossDestinationClearance,
                        bossDestinationApproachRadius,
                        portReserved,
                        reservationOwners,
                        out float score))
                    {
                        continue;
                    }

                    best = new PlacedEvent { Node = node, Anchor = anchor, Plan = plan };
                    bestScore = score;
                    break;
                }

                if (best == null)
                {
                    // Exhaustive deterministic fallback protects generation from a
                    // rare unlucky random sequence without weakening any spacing rule.
                    for (int row = 1; row < CoreVoyageMap.GridRows - 1; row += 1)
                    {
                        for (int column = 1; column < CoreVoyageMap.GridColumns - 1; column += 1)
                        {
                            for (int orientationOffset = 0; orientationOffset < orientationCount; orientationOffset += 1)
                            {
                                int orientation = (preferredOrientation + orientationOffset) % orientationCount;
                                FootprintPlan plan = orientationPlans[orientation] ??
                                    (orientationPlans[orientation] = BuildPlan(node.TerrainConstraint, orientation, nodeShapeSeed));
                                VoyageGridCoordinate anchor = new VoyageGridCoordinate(column, row);
                                if (!IsValidPlacement(
                                    map,
                                    node,
                                    anchor,
                                    plan,
                                    completed,
                                    minimumEventSpacing,
                                    bossBorderMargin,
                                    bossDepartureClearance,
                                    bossDestinationClearance,
                                    bossDestinationApproachRadius,
                                    portReserved,
                                    reservationOwners,
                                    out float score) || score <= bestScore)
                                {
                                    continue;
                                }

                                best = new PlacedEvent { Node = node, Anchor = anchor, Plan = plan };
                                bestScore = score;
                            }
                        }
                    }
                }

                if (best == null)
                {
                    return null;
                }

                ReservePlacement(map, best, reservationOwners);
                best.Node.Position = map.CellCenter(best.Anchor.Column, best.Anchor.Row);
                placements[nodeIndex] = best;
                completed.Add(best);
            }

            return placements;
        }

        private static List<int> BuildPlacementOrder(CoreVoyageMap map)
        {
            List<int> order = new List<int>(map.Nodes.Count);
            for (int index = 0; index < map.Nodes.Count; index += 1)
            {
                order.Add(index);
            }

            order.Sort((left, right) =>
            {
                CoreMapNode leftNode = map.Nodes[left];
                CoreMapNode rightNode = map.Nodes[right];
                if (leftNode.IsBoss != rightNode.IsBoss)
                {
                    return leftNode.IsBoss ? -1 : 1;
                }

                int size = PlacementWeight(rightNode.TerrainConstraint).CompareTo(PlacementWeight(leftNode.TerrainConstraint));
                return size != 0 ? size : leftNode.Id.CompareTo(rightNode.Id);
            });
            return order;
        }

        private static int PlacementWeight(EventTerrainConstraint constraint)
        {
            switch (constraint)
            {
                case EventTerrainConstraint.Strait: return 6;
                case EventTerrainConstraint.Beach: return 5;
                case EventTerrainConstraint.Storm: return 4;
                case EventTerrainConstraint.Fog: return 3;
                case EventTerrainConstraint.Reef: return 2;
                default: return 1;
            }
        }

        private static bool IsValidPlacement(
            CoreVoyageMap map,
            CoreMapNode node,
            VoyageGridCoordinate anchor,
            FootprintPlan plan,
            List<PlacedEvent> completed,
            float minimumEventSpacing,
            float bossBorderMargin,
            float bossDepartureClearance,
            float bossDestinationClearance,
            float bossDestinationApproachRadius,
            bool[] portReserved,
            int[] reservationOwners,
            out float score)
        {
            score = float.MaxValue;
            for (int index = 0; index < plan.ReservedOffsets.Length; index += 1)
            {
                int column = anchor.Column + plan.ReservedOffsets[index].Column;
                int row = anchor.Row + plan.ReservedOffsets[index].Row;
                if (!map.IsInBounds(column, row))
                {
                    return false;
                }

                int cellIndex = row * CoreVoyageMap.GridColumns + column;
                if (reservationOwners[cellIndex] >= 0 || portReserved[cellIndex])
                {
                    return false;
                }
            }

            VoyagePoint point = map.CellCenter(anchor.Column, anchor.Row);
            float departureDistance = Distance(point, map.Departure);
            float destinationDistance = Distance(point, map.Destination);
            if (node.IsBoss)
            {
                if (point.X < bossBorderMargin || point.X > CoreVoyageMap.Width - bossBorderMargin ||
                    point.Y < bossBorderMargin || point.Y > CoreVoyageMap.Height - bossBorderMargin ||
                    departureDistance < bossDepartureClearance || destinationDistance < bossDestinationClearance || (bossDestinationApproachRadius > bossDestinationClearance && destinationDistance > bossDestinationApproachRadius))
                {
                    return false;
                }
            }
            else if (departureDistance < minimumEventSpacing || destinationDistance < minimumEventSpacing)
            {
                return false;
            }

            score = Math.Min(departureDistance, destinationDistance);
            for (int index = 0; index < completed.Count; index += 1)
            {
                float distance = Distance(point, map.CellCenter(completed[index].Anchor.Column, completed[index].Anchor.Row));
                if (distance < minimumEventSpacing)
                {
                    return false;
                }

                score = Math.Min(score, distance);
            }

            return true;
        }

        private static void ReservePlacement(CoreVoyageMap map, PlacedEvent placement, int[] reservationOwners)
        {
            List<int> footprintIndices = new List<int>(placement.Plan.Stamps.Length);
            List<int> reservedIndices = new List<int>(placement.Plan.ReservedOffsets.Length);
            int minColumn = CoreVoyageMap.GridColumns;
            int maxColumn = 0;
            int minRow = CoreVoyageMap.GridRows;
            int maxRow = 0;

            for (int index = 0; index < placement.Plan.ReservedOffsets.Length; index += 1)
            {
                int column = placement.Anchor.Column + placement.Plan.ReservedOffsets[index].Column;
                int row = placement.Anchor.Row + placement.Plan.ReservedOffsets[index].Row;
                int cellIndex = row * CoreVoyageMap.GridColumns + column;
                reservationOwners[cellIndex] = placement.Node.Id;
                map.EventReservedCells[cellIndex] = true;
                reservedIndices.Add(cellIndex);
            }

            for (int index = 0; index < placement.Plan.Stamps.Length; index += 1)
            {
                int column = placement.Anchor.Column + placement.Plan.Stamps[index].ColumnOffset;
                int row = placement.Anchor.Row + placement.Plan.Stamps[index].RowOffset;
                int cellIndex = row * CoreVoyageMap.GridColumns + column;
                footprintIndices.Add(cellIndex);
                minColumn = Math.Min(minColumn, column);
                maxColumn = Math.Max(maxColumn, column);
                minRow = Math.Min(minRow, row);
                maxRow = Math.Max(maxRow, row);
            }

            footprintIndices.Sort();
            reservedIndices.Sort();
            CoreEventFootprint footprint = new CoreEventFootprint(
                placement.Node.Id,
                placement.Node.TerrainConstraint,
                placement.Anchor,
                placement.Plan.Orientation,
                footprintIndices.ToArray(),
                reservedIndices.ToArray(),
                minColumn,
                maxColumn,
                minRow,
                maxRow);
            placement.Node.Footprint = footprint;
            map.MutableEventFootprints.Add(footprint);
        }

        private static void StampEventGeography(CoreVoyageMap map, PlacedEvent[] placements)
        {
            for (int nodeIndex = 0; nodeIndex < placements.Length; nodeIndex += 1)
            {
                PlacedEvent placement = placements[nodeIndex];
                int featureId = placement.Node.Id;
                for (int stampIndex = 0; stampIndex < placement.Plan.Stamps.Length; stampIndex += 1)
                {
                    TerrainStamp stamp = placement.Plan.Stamps[stampIndex];
                    int column = placement.Anchor.Column + stamp.ColumnOffset;
                    int row = placement.Anchor.Row + stamp.RowOffset;
                    int cellIndex = row * CoreVoyageMap.GridColumns + column;
                    map.TerrainCells[cellIndex] = stamp.Terrain;
                    map.TerrainFeatureIds[cellIndex] = featureId;
                    map.AnchorNodeIds[cellIndex] = placement.Node.Id;
                }

                int anchorIndex = placement.Anchor.Row * CoreVoyageMap.GridColumns + placement.Anchor.Column;
                map.EventAnchorCells[anchorIndex] = true;
                map.AnchorNodeIds[anchorIndex] = placement.Node.Id;
                for (int islandIndex = 0; islandIndex < placement.Plan.IslandCenters.Length; islandIndex += 1)
                {
                    VoyageGridCoordinate offset = placement.Plan.IslandCenters[islandIndex];
                    map.Islands.Add(map.CellCenter(
                        placement.Anchor.Column + offset.Column,
                        placement.Anchor.Row + offset.Row));
                }
            }
        }

        private static FootprintPlan BuildPlan(EventTerrainConstraint constraint, int orientation, uint shapeSeed)
        {
            ShapeBuilder builder = new ShapeBuilder();
            switch (constraint)
            {
                case EventTerrainConstraint.Beach:
                    BuildBeach(builder, orientation, shapeSeed);
                    break;
                case EventTerrainConstraint.Strait:
                    BuildStrait(builder, orientation, shapeSeed);
                    break;
                case EventTerrainConstraint.Reef:
                    AddIrregularEllipse(builder, 0, 0, orientation % 2 == 0 ? 3 : 2, orientation % 2 == 0 ? 2 : 3, SeaTerrainType.Reef, shapeSeed);
                    builder.Set(0, 0, SeaTerrainType.OpenWater);
                    break;
                case EventTerrainConstraint.Fog:
                    AddIrregularEllipse(builder, 0, 0, orientation % 2 == 0 ? 3 : 2, orientation % 2 == 0 ? 2 : 3, SeaTerrainType.FogBank, shapeSeed);
                    builder.Set(0, 0, SeaTerrainType.FogBank);
                    break;
                case EventTerrainConstraint.Storm:
                    AddIrregularEllipse(builder, 0, 0, 3, 3, SeaTerrainType.StormFront, shapeSeed);
                    builder.Set(0, 0, SeaTerrainType.StormFront);
                    break;
                default:
                    builder.Set(0, 0, SeaTerrainType.OpenWater);
                    builder.Set(-1, 0, SeaTerrainType.OpenWater);
                    builder.Set(1, 0, SeaTerrainType.OpenWater);
                    builder.Set(0, -1, SeaTerrainType.OpenWater);
                    builder.Set(0, 1, SeaTerrainType.OpenWater);
                    break;
            }

            return builder.Build(orientation);
        }

        private static void BuildBeach(ShapeBuilder builder, int orientation, uint shapeSeed)
        {
            DirectionForOrientation(orientation, out int dx, out int dy);
            int centerColumn = dx * 3;
            int centerRow = dy * 3;
            HashSet<VoyageGridCoordinate> land = BuildIrregularEllipseCells(
                centerColumn,
                centerRow,
                2,
                2,
                shapeSeed);
            AddShoalRing(builder, land);
            foreach (VoyageGridCoordinate cell in land)
            {
                builder.Set(cell.Column, cell.Row, SeaTerrainType.Island);
            }

            builder.Set(0, 0, SeaTerrainType.Beach);
            builder.Set(dx, dy, SeaTerrainType.Beach);
            builder.AddIslandCenter(centerColumn, centerRow);
        }

        private static void BuildStrait(ShapeBuilder builder, int orientation, uint shapeSeed)
        {
            bool horizontal = orientation % 2 == 0;
            HashSet<VoyageGridCoordinate> firstBank;
            HashSet<VoyageGridCoordinate> secondBank;
            if (horizontal)
            {
                firstBank = BuildStraitBankCells(true, -1, shapeSeed ^ 0x91A74D3Bu);
                secondBank = BuildStraitBankCells(true, 1, shapeSeed ^ 0xE35A7BD1u);
                builder.AddIslandCenter(0, -3);
                builder.AddIslandCenter(0, 3);
            }
            else
            {
                firstBank = BuildStraitBankCells(false, -1, shapeSeed ^ 0x91A74D3Bu);
                secondBank = BuildStraitBankCells(false, 1, shapeSeed ^ 0xE35A7BD1u);
                builder.AddIslandCenter(-3, 0);
                builder.AddIslandCenter(3, 0);
            }

            AddShoalRing(builder, firstBank);
            AddShoalRing(builder, secondBank);
            foreach (VoyageGridCoordinate cell in firstBank) builder.Set(cell.Column, cell.Row, SeaTerrainType.Island);
            foreach (VoyageGridCoordinate cell in secondBank) builder.Set(cell.Column, cell.Row, SeaTerrainType.Island);

            for (int offset = -5; offset <= 5; offset += 1)
            {
                if (horizontal)
                {
                    builder.Set(offset, 0, SeaTerrainType.Strait);
                    if ((HashCell(shapeSeed, offset, 0) & 3u) == 0u) builder.Set(offset, 1, SeaTerrainType.Strait);
                    if ((HashCell(shapeSeed, offset, 1) & 3u) == 0u) builder.Set(offset, -1, SeaTerrainType.Strait);
                }
                else
                {
                    builder.Set(0, offset, SeaTerrainType.Strait);
                    if ((HashCell(shapeSeed, 0, offset) & 3u) == 0u) builder.Set(1, offset, SeaTerrainType.Strait);
                    if ((HashCell(shapeSeed, 1, offset) & 3u) == 0u) builder.Set(-1, offset, SeaTerrainType.Strait);
                }
            }
            builder.Set(0, 0, SeaTerrainType.Strait);
        }

        private static HashSet<VoyageGridCoordinate> BuildStraitBankCells(bool horizontal, int bankSide, uint seed)
        {
            // A radius-(4,1) ellipse rasterizes to a one-cell line with a tiny
            // central cross. Give each bank a real two-cell body instead. Both
            // sides keep the same old maximum envelope, so event/port safety
            // bands and the straight central navigation lane remain intact.
            uint profile = HashCell(seed, 0, 0);
            int first = -4 + (int)(profile & 1u);
            int last = 4 - (int)((profile >> 1) & 1u);
            int bend = (int)((profile >> 2) % 3u) - 1;
            int firstShoulder = (profile & 0x20u) == 0u ? -1 : 1;
            int bulge = (int)((profile >> 6) % 3u) - 1;
            HashSet<VoyageGridCoordinate> cells = new HashSet<VoyageGridCoordinate>();

            for (int along = first; along <= last; along += 1)
            {
                AddStraitBankCell(cells, horizontal, bankSide, along, 0);
                if (along == first || along == last) continue;

                // A connected centre spine guarantees four-neighbour contact
                // even where the thick shoulder bends to the opposite side.
                int shoulder = along <= bend ? firstShoulder : -firstShoulder;
                AddStraitBankCell(cells, horizontal, bankSide, along, shoulder);
                bool secondBulgeCell = Math.Abs(along - bulge) == 1 &&
                    (HashCell(seed ^ 0x27D4EB2Du, along, bankSide) & 3u) == 0u;
                if (along == bulge || secondBulgeCell)
                {
                    AddStraitBankCell(cells, horizontal, bankSide, along, -shoulder);
                }
            }
            return cells;
        }

        private static void AddStraitBankCell(
            HashSet<VoyageGridCoordinate> cells,
            bool horizontal,
            int bankSide,
            int along,
            int crossOffset)
        {
            int across = bankSide * 3 + crossOffset;
            cells.Add(horizontal
                ? new VoyageGridCoordinate(along, across)
                : new VoyageGridCoordinate(across, along));
        }

        private static void AddIrregularEllipse(
            ShapeBuilder builder,
            int centerColumn,
            int centerRow,
            int radiusX,
            int radiusY,
            SeaTerrainType terrain,
            uint seed)
        {
            HashSet<VoyageGridCoordinate> cells = BuildIrregularEllipseCells(centerColumn, centerRow, radiusX, radiusY, seed);
            foreach (VoyageGridCoordinate cell in cells)
            {
                builder.Set(cell.Column, cell.Row, terrain);
            }
        }

        private static HashSet<VoyageGridCoordinate> BuildIrregularEllipseCells(
            int centerColumn,
            int centerRow,
            int radiusX,
            int radiusY,
            uint seed)
        {
            HashSet<VoyageGridCoordinate> cells = new HashSet<VoyageGridCoordinate>();
            for (int row = centerRow - radiusY; row <= centerRow + radiusY; row += 1)
            {
                for (int column = centerColumn - radiusX; column <= centerColumn + radiusX; column += 1)
                {
                    float x = (column - centerColumn) / (float)Math.Max(1, radiusX);
                    float y = (row - centerRow) / (float)Math.Max(1, radiusY);
                    float edgeNoise = ((HashCell(seed, column, row) & 255u) / 255f - 0.5f) * 0.22f;
                    if (x * x + y * y <= 1.02f + edgeNoise)
                    {
                        cells.Add(new VoyageGridCoordinate(column, row));
                    }
                }
            }
            cells.Add(new VoyageGridCoordinate(centerColumn, centerRow));
            return cells;
        }

        private static void AddShoalRing(ShapeBuilder builder, HashSet<VoyageGridCoordinate> land)
        {
            foreach (VoyageGridCoordinate cell in land)
            {
                for (int y = -1; y <= 1; y += 1)
                {
                    for (int x = -1; x <= 1; x += 1)
                    {
                        VoyageGridCoordinate neighbor = new VoyageGridCoordinate(cell.Column + x, cell.Row + y);
                        if (!land.Contains(neighbor))
                        {
                            builder.Set(neighbor.Column, neighbor.Row, SeaTerrainType.Shoal);
                        }
                    }
                }
            }
        }

        private static bool[] BuildPortReserve(CoreVoyageMap map)
        {
            bool[] reserved = new bool[map.CellCount];
            MarkDisc(map, reserved, map.PointToCell(map.Departure), PortReserveRadiusCells);
            MarkDisc(map, reserved, map.PointToCell(map.Destination), PortReserveRadiusCells);
            return reserved;
        }

        private static void MarkDisc(CoreVoyageMap map, bool[] cells, VoyageGridCoordinate center, int radius)
        {
            for (int row = center.Row - radius; row <= center.Row + radius; row += 1)
            {
                for (int column = center.Column - radius; column <= center.Column + radius; column += 1)
                {
                    if (!map.IsInBounds(column, row)) continue;
                    int x = column - center.Column;
                    int y = row - center.Row;
                    if (x * x + y * y <= radius * radius + 1)
                    {
                        cells[row * CoreVoyageMap.GridColumns + column] = true;
                    }
                }
            }
        }

        private static bool[] BuildGuaranteedNavigableRoute(CoreVoyageMap map)
        {
            VoyageGridCoordinate start = map.PointToCell(map.Departure);
            VoyageGridCoordinate goal = map.PointToCell(map.Destination);
            List<int> path = FindWeightedPath(
                map,
                start.Row * CoreVoyageMap.GridColumns + start.Column,
                goal.Row * CoreVoyageMap.GridColumns + goal.Column,
                map.Hash ^ 0x4CF5AD43u,
                false);
            if (path.Count == 0)
            {
                throw new InvalidOperationException("Fixed event geography blocked every route between chart ports.");
            }

            bool[] protectedRoute = new bool[map.CellCount];
            for (int index = 0; index < path.Count; index += 1)
            {
                int column = path[index] % CoreVoyageMap.GridColumns;
                int row = path[index] / CoreVoyageMap.GridColumns;
                for (int y = -1; y <= 1; y += 1)
                {
                    for (int x = -1; x <= 1; x += 1)
                    {
                        int neighborColumn = column + x;
                        int neighborRow = row + y;
                        if (!map.IsInBounds(neighborColumn, neighborRow)) continue;
                        int neighbor = neighborRow * CoreVoyageMap.GridColumns + neighborColumn;
                        if (map.TerrainCells[neighbor] != SeaTerrainType.Island)
                        {
                            protectedRoute[neighbor] = true;
                        }
                    }
                }
            }
            return protectedRoute;
        }

        private static int GenerateNaturalStraits(CoreVoyageMap map, bool[] guaranteedRoute, int nextFeatureId)
        {
            Mulberry32Random random = new Mulberry32Random(map.Hash ^ StraitStream);
            int count = IsGoldenRoute(map) ? 3 : IsMoonRoute(map) ? 4 : 5;
            for (int channel = 0; channel < count; channel += 1)
            {
                bool horizontal = (channel & 1) == 0;
                int start;
                int goal;
                if (horizontal)
                {
                    int startRow = 2 + NextInt(random, CoreVoyageMap.GridRows - 4);
                    int goalRow = 2 + NextInt(random, CoreVoyageMap.GridRows - 4);
                    start = startRow * CoreVoyageMap.GridColumns;
                    goal = goalRow * CoreVoyageMap.GridColumns + CoreVoyageMap.GridColumns - 1;
                }
                else
                {
                    int startColumn = 2 + NextInt(random, CoreVoyageMap.GridColumns - 4);
                    int goalColumn = 2 + NextInt(random, CoreVoyageMap.GridColumns - 4);
                    start = startColumn;
                    goal = (CoreVoyageMap.GridRows - 1) * CoreVoyageMap.GridColumns + goalColumn;
                }

                List<int> path = FindWeightedPath(map, start, goal, map.Hash ^ StraitStream ^ (uint)(channel * 977), true);
                if (path.Count == 0) continue;
                for (int index = 0; index < path.Count; index += 1)
                {
                    int cell = path[index];
                    if (map.AnchorNodeIds[cell] >= 0 || map.EventReservedCells[cell] || map.TerrainCells[cell] == SeaTerrainType.Island)
                    {
                        continue;
                    }

                    map.TerrainCells[cell] = SeaTerrainType.Strait;
                    guaranteedRoute[cell] = true;
                    if ((HashCell(map.Hash ^ StraitStream, cell % CoreVoyageMap.GridColumns, cell / CoreVoyageMap.GridColumns) & 7u) == 0u)
                    {
                        PaintPassableNeighbor(map, cell, guaranteedRoute);
                    }
                }
            }

            return nextFeatureId;
        }

        private static void PaintPassableNeighbor(CoreVoyageMap map, int cell, bool[] guaranteedRoute)
        {
            int column = cell % CoreVoyageMap.GridColumns;
            int row = cell / CoreVoyageMap.GridColumns;
            int selector = (int)(HashCell(map.Hash ^ 0xC2B2AE35u, column, row) & 3u);
            int neighborColumn = column + (selector == 0 ? -1 : selector == 1 ? 1 : 0);
            int neighborRow = row + (selector == 2 ? -1 : selector == 3 ? 1 : 0);
            if (!map.IsInBounds(neighborColumn, neighborRow)) return;
            int neighbor = neighborRow * CoreVoyageMap.GridColumns + neighborColumn;
            if (map.AnchorNodeIds[neighbor] >= 0 || map.EventReservedCells[neighbor] || map.TerrainCells[neighbor] == SeaTerrainType.Island) return;
            map.TerrainCells[neighbor] = SeaTerrainType.Strait;
            guaranteedRoute[neighbor] = true;
        }

        private static int GenerateOrganicIslands(
            CoreVoyageMap map,
            bool[] portReserved,
            bool[] guaranteedRoute,
            int nextFeatureId)
        {
            Mulberry32Random random = new Mulberry32Random(map.Hash ^ IslandStream);
            int requestedCount = IsGoldenRoute(map) ? 7 : IsMoonRoute(map) ? 9 : 11;
            for (int island = 0; island < requestedCount; island += 1)
            {
                int seedCell = -1;
                for (int attempt = 0; attempt < IslandPlacementAttempts; attempt += 1)
                {
                    int column = 2 + NextInt(random, CoreVoyageMap.GridColumns - 4);
                    int row = 2 + NextInt(random, CoreVoyageMap.GridRows - 4);
                    int candidate = row * CoreVoyageMap.GridColumns + column;
                    if (CanGrowIslandAt(map, candidate, portReserved, guaranteedRoute))
                    {
                        seedCell = candidate;
                        break;
                    }
                }

                if (seedCell < 0)
                {
                    seedCell = FindIslandFallback(map, portReserved, guaranteedRoute, map.Hash ^ (uint)island);
                }
                if (seedCell < 0) continue;

                int targetSize = 9 + NextInt(random, IsGoldenRoute(map) ? 10 : 17);
                List<int> cells = GrowIsland(map, seedCell, targetSize, portReserved, guaranteedRoute, random);
                if (cells.Count < 4) continue;
                int featureId = nextFeatureId++;
                float totalX = 0f;
                float totalY = 0f;
                for (int index = 0; index < cells.Count; index += 1)
                {
                    int cell = cells[index];
                    map.TerrainCells[cell] = SeaTerrainType.Island;
                    map.TerrainFeatureIds[cell] = featureId;
                    VoyagePoint center = map.CellCenter(cell % CoreVoyageMap.GridColumns, cell / CoreVoyageMap.GridColumns);
                    totalX += center.X;
                    totalY += center.Y;
                }

                PaintIslandShoals(map, cells, featureId, portReserved);
                map.Islands.Add(new VoyagePoint(totalX / cells.Count, totalY / cells.Count));
            }

            return nextFeatureId;
        }

        private static List<int> GrowIsland(
            CoreVoyageMap map,
            int seedCell,
            int targetSize,
            bool[] portReserved,
            bool[] guaranteedRoute,
            Mulberry32Random random)
        {
            List<int> cells = new List<int> { seedCell };
            HashSet<int> included = new HashSet<int> { seedCell };
            int guard = targetSize * 24;
            while (cells.Count < targetSize && guard-- > 0)
            {
                int parent = cells[NextInt(random, cells.Count)];
                int parentColumn = parent % CoreVoyageMap.GridColumns;
                int parentRow = parent / CoreVoyageMap.GridColumns;
                int direction = NextInt(random, 4);
                int x = direction == 0 ? -1 : direction == 1 ? 1 : 0;
                int y = direction == 2 ? -1 : direction == 3 ? 1 : 0;
                int column = parentColumn + x;
                int row = parentRow + y;
                if (!map.IsInBounds(column, row)) continue;
                int candidate = row * CoreVoyageMap.GridColumns + column;
                if (included.Contains(candidate) || !CanGrowIslandAt(map, candidate, portReserved, guaranteedRoute)) continue;
                if (DistanceSquaredCells(seedCell, candidate) > targetSize * 0.78f) continue;
                included.Add(candidate);
                cells.Add(candidate);
            }

            cells.Sort();
            return cells;
        }

        private static bool CanGrowIslandAt(CoreVoyageMap map, int cell, bool[] portReserved, bool[] guaranteedRoute)
        {
            if (cell < 0 || cell >= map.CellCount || portReserved[cell] || guaranteedRoute[cell] ||
                map.EventReservedCells[cell] || map.AnchorNodeIds[cell] >= 0 ||
                map.TerrainCells[cell] != SeaTerrainType.OpenWater || map.TerrainFeatureIds[cell] >= 0)
            {
                return false;
            }

            int column = cell % CoreVoyageMap.GridColumns;
            int row = cell / CoreVoyageMap.GridColumns;
            if (column < 2 || column >= CoreVoyageMap.GridColumns - 2 || row < 2 || row >= CoreVoyageMap.GridRows - 2)
            {
                return false;
            }

            // Separate independent islands with at least one cell of water.
            for (int y = -2; y <= 2; y += 1)
            {
                for (int x = -2; x <= 2; x += 1)
                {
                    int neighborColumn = column + x;
                    int neighborRow = row + y;
                    if (!map.IsInBounds(neighborColumn, neighborRow)) continue;
                    int neighbor = neighborRow * CoreVoyageMap.GridColumns + neighborColumn;
                    if (map.TerrainCells[neighbor] == SeaTerrainType.Island && map.AnchorNodeIds[neighbor] < 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static int FindIslandFallback(CoreVoyageMap map, bool[] portReserved, bool[] guaranteedRoute, uint seed)
        {
            int best = -1;
            uint bestHash = uint.MaxValue;
            for (int row = 2; row < CoreVoyageMap.GridRows - 2; row += 1)
            {
                for (int column = 2; column < CoreVoyageMap.GridColumns - 2; column += 1)
                {
                    int cell = row * CoreVoyageMap.GridColumns + column;
                    if (!CanGrowIslandAt(map, cell, portReserved, guaranteedRoute)) continue;
                    uint hash = HashCell(seed, column, row);
                    if (hash < bestHash)
                    {
                        bestHash = hash;
                        best = cell;
                    }
                }
            }
            return best;
        }

        private static void PaintIslandShoals(CoreVoyageMap map, List<int> islands, int featureId, bool[] portReserved)
        {
            HashSet<int> land = new HashSet<int>(islands);
            HashSet<int> shoals = new HashSet<int>();
            for (int index = 0; index < islands.Count; index += 1)
            {
                int column = islands[index] % CoreVoyageMap.GridColumns;
                int row = islands[index] / CoreVoyageMap.GridColumns;
                for (int y = -1; y <= 1; y += 1)
                {
                    for (int x = -1; x <= 1; x += 1)
                    {
                        int neighborColumn = column + x;
                        int neighborRow = row + y;
                        if (!map.IsInBounds(neighborColumn, neighborRow)) continue;
                        int neighbor = neighborRow * CoreVoyageMap.GridColumns + neighborColumn;
                        if (!land.Contains(neighbor)) shoals.Add(neighbor);
                    }
                }
            }

            foreach (int cell in shoals)
            {
                if (portReserved[cell] || map.EventReservedCells[cell] || map.AnchorNodeIds[cell] >= 0 ||
                    map.TerrainCells[cell] != SeaTerrainType.OpenWater || map.TerrainFeatureIds[cell] >= 0)
                {
                    continue;
                }
                map.TerrainCells[cell] = SeaTerrainType.Shoal;
                map.TerrainFeatureIds[cell] = featureId;
            }
        }

        private static void FillRemainingWithOpenWater(CoreVoyageMap map)
        {
            // Fixed events and their terrain footprints are deliberately resolved
            // before this pass. Organic islands and navigable straits may claim
            // additional cells, but every cell left over is true open water. The
            // presentation layer uses the seed-bound TerrainVariants to break the
            // ocean into broad visual regions without changing navigation rules.
            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
                {
                    int index = row * CoreVoyageMap.GridColumns + column;
                    if (map.TerrainFeatureIds[index] >= 0 || map.EventReservedCells[index]) continue;
                    map.TerrainCells[index] = SeaTerrainType.OpenWater;
                }
            }
        }

        private static void ClearPortApproaches(CoreVoyageMap map, bool[] portReserved)
        {
            for (int index = 0; index < portReserved.Length; index += 1)
            {
                if (!portReserved[index]) continue;
                map.TerrainCells[index] = SeaTerrainType.OpenWater;
                map.TerrainFeatureIds[index] = -1;
            }
        }

        private static void AssignUnownedFeatureIds(CoreVoyageMap map, int nextFeatureId)
        {
            Queue<int> queue = new Queue<int>();
            for (int start = 0; start < map.CellCount; start += 1)
            {
                if (map.TerrainFeatureIds[start] >= 0) continue;
                SeaTerrainType terrain = map.TerrainCells[start];
                int featureId = nextFeatureId++;
                map.TerrainFeatureIds[start] = featureId;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue();
                    int column = cell % CoreVoyageMap.GridColumns;
                    int row = cell / CoreVoyageMap.GridColumns;
                    TryAssignFeatureNeighbor(map, column - 1, row, terrain, featureId, queue);
                    TryAssignFeatureNeighbor(map, column + 1, row, terrain, featureId, queue);
                    TryAssignFeatureNeighbor(map, column, row - 1, terrain, featureId, queue);
                    TryAssignFeatureNeighbor(map, column, row + 1, terrain, featureId, queue);
                }
            }
        }

        private static void TryAssignFeatureNeighbor(
            CoreVoyageMap map,
            int column,
            int row,
            SeaTerrainType terrain,
            int featureId,
            Queue<int> queue)
        {
            if (!map.IsInBounds(column, row)) return;
            int index = row * CoreVoyageMap.GridColumns + column;
            if (map.TerrainFeatureIds[index] >= 0 || map.TerrainCells[index] != terrain) return;
            map.TerrainFeatureIds[index] = featureId;
            queue.Enqueue(index);
        }

        private static void AssignTerrainVariants(CoreVoyageMap map)
        {
            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
                {
                    int index = row * CoreVoyageMap.GridColumns + column;
                    uint value = HashCell(
                        map.Hash ^ VariantStream ^ (uint)((int)map.TerrainCells[index] * 0x45D9F3B),
                        column,
                        row);
                    map.TerrainVariants[index] = (byte)(value & 15u);
                }
            }
        }

        private static List<int> FindWeightedPath(
            CoreVoyageMap map,
            int start,
            int goal,
            uint seed,
            bool avoidEventSafetyBands)
        {
            int count = map.CellCount;
            float[] costs = new float[count];
            int[] previous = new int[count];
            bool[] closed = new bool[count];
            for (int index = 0; index < count; index += 1)
            {
                costs[index] = float.MaxValue;
                previous[index] = -1;
            }
            costs[start] = 0f;

            for (int iteration = 0; iteration < count; iteration += 1)
            {
                int current = -1;
                float best = float.MaxValue;
                for (int index = 0; index < count; index += 1)
                {
                    if (!closed[index] && costs[index] < best)
                    {
                        best = costs[index];
                        current = index;
                    }
                }
                if (current < 0) break;
                if (current == goal) break;
                closed[current] = true;
                int column = current % CoreVoyageMap.GridColumns;
                int row = current / CoreVoyageMap.GridColumns;
                RelaxPathNeighbor(map, current, column - 1, row, goal, seed, avoidEventSafetyBands, costs, previous, closed);
                RelaxPathNeighbor(map, current, column + 1, row, goal, seed, avoidEventSafetyBands, costs, previous, closed);
                RelaxPathNeighbor(map, current, column, row - 1, goal, seed, avoidEventSafetyBands, costs, previous, closed);
                RelaxPathNeighbor(map, current, column, row + 1, goal, seed, avoidEventSafetyBands, costs, previous, closed);
            }

            List<int> path = new List<int>();
            if (start != goal && previous[goal] < 0) return path;
            int cursor = goal;
            path.Add(cursor);
            while (cursor != start)
            {
                cursor = previous[cursor];
                if (cursor < 0)
                {
                    path.Clear();
                    return path;
                }
                path.Add(cursor);
            }
            path.Reverse();
            return path;
        }

        private static void RelaxPathNeighbor(
            CoreVoyageMap map,
            int current,
            int column,
            int row,
            int goal,
            uint seed,
            bool avoidEventSafetyBands,
            float[] costs,
            int[] previous,
            bool[] closed)
        {
            if (!map.IsInBounds(column, row)) return;
            int neighbor = row * CoreVoyageMap.GridColumns + column;
            if (closed[neighbor] || map.TerrainCells[neighbor] == SeaTerrainType.Island) return;
            if (avoidEventSafetyBands && neighbor != goal && map.EventReservedCells[neighbor]) return;
            float noise = (HashCell(seed, column, row) & 1023u) / 1023f;
            float nextCost = costs[current] + 0.82f + noise * 0.5f;
            if (nextCost >= costs[neighbor]) return;
            costs[neighbor] = nextCost;
            previous[neighbor] = current;
        }

        private static int NextFeatureId(CoreVoyageMap map)
        {
            int maximum = -1;
            for (int index = 0; index < map.Nodes.Count; index += 1)
            {
                maximum = Math.Max(maximum, map.Nodes[index].Id);
            }
            return maximum + 1;
        }

        private static int OrientationCount(EventTerrainConstraint constraint)
        {
            return constraint == EventTerrainConstraint.Beach ? 4 :
                constraint == EventTerrainConstraint.Strait || constraint == EventTerrainConstraint.Reef || constraint == EventTerrainConstraint.Fog ? 2 : 1;
        }

        private static void DirectionForOrientation(int orientation, out int x, out int y)
        {
            switch (orientation & 3)
            {
                case 0: x = 1; y = 0; break;
                case 1: x = 0; y = 1; break;
                case 2: x = -1; y = 0; break;
                default: x = 0; y = -1; break;
            }
        }

        private static int CompareCoordinates(VoyageGridCoordinate left, VoyageGridCoordinate right)
        {
            int row = left.Row.CompareTo(right.Row);
            return row != 0 ? row : left.Column.CompareTo(right.Column);
        }

        private static int NextInt(Mulberry32Random random, int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 1) return 0;
            return (int)(random.NextDouble() * exclusiveMaximum) % exclusiveMaximum;
        }

        private static float Distance(VoyagePoint left, VoyagePoint right)
        {
            float x = left.X - right.X;
            float y = left.Y - right.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private static float DistanceSquaredCells(int left, int right)
        {
            int leftColumn = left % CoreVoyageMap.GridColumns;
            int leftRow = left / CoreVoyageMap.GridColumns;
            int rightColumn = right % CoreVoyageMap.GridColumns;
            int rightRow = right / CoreVoyageMap.GridColumns;
            int x = leftColumn - rightColumn;
            int y = leftRow - rightRow;
            return x * x + y * y;
        }

        private static bool IsGoldenRoute(CoreVoyageMap map)
        {
            return map.Route.Id.IndexOf("gold", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsMoonRoute(CoreVoyageMap map)
        {
            return map.Route.Id.IndexOf("moon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsReefRoute(CoreVoyageMap map)
        {
            return !IsGoldenRoute(map) && !IsMoonRoute(map);
        }

        private static uint Mix(uint value, uint first, uint second)
        {
            unchecked
            {
                uint mixed = value ^ (first * 0x9E3779B9u) ^ (second * 0x85EBCA6Bu);
                mixed ^= mixed >> 16;
                mixed *= 0x7FEB352Du;
                mixed ^= mixed >> 15;
                mixed *= 0x846CA68Bu;
                return mixed ^ (mixed >> 16);
            }
        }

        private static uint HashCell(uint seed, int column, int row)
        {
            unchecked
            {
                uint value = seed;
                value ^= (uint)column * 0x8DA6B343u;
                value ^= (uint)row * 0xD8163841u;
                value ^= value >> 13;
                value *= 0x85EBCA6Bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static double FractalNoise(uint seed, double x, double y)
        {
            double total = 0d;
            double amplitude = 0.58d;
            double normalization = 0d;
            for (int octave = 0; octave < 3; octave += 1)
            {
                total += ValueNoise(seed + (uint)(octave * 0x9E3779B9u), x, y) * amplitude;
                normalization += amplitude;
                amplitude *= 0.5d;
                x *= 2.03d;
                y *= 2.03d;
            }
            return total / normalization;
        }

        private static double ValueNoise(uint seed, double x, double y)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            double tx = Smooth(x - x0);
            double ty = Smooth(y - y0);
            double top = Lerp(HashUnit(seed, x0, y0), HashUnit(seed, x1, y0), tx);
            double bottom = Lerp(HashUnit(seed, x0, y1), HashUnit(seed, x1, y1), tx);
            return Lerp(top, bottom, ty);
        }

        private static double HashUnit(uint seed, int column, int row)
        {
            return (HashCell(seed, column, row) & 0x00FFFFFFu) / 16777215d;
        }

        private static double Smooth(double value)
        {
            return value * value * (3d - 2d * value);
        }

        private static double Lerp(double start, double end, double amount)
        {
            return start + (end - start) * amount;
        }
    }
}
