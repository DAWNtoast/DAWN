using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public enum LookoutSceneryKind { Island, Reef, Storm, Fog, PirateShip, Vessel }

    public sealed class LookoutObservation
    {
        public string Key, Label;
        public LookoutSceneryKind Kind;
        public VoyagePoint Position;
        public float DistanceNm, Horizontal, Proximity, Depth, Visibility, Scale;
        public int Variant;
        public bool IsEvent;
    }

    /// <summary>Read-only, deterministic ship-eye projection. Does not discover nodes,
    /// consume RNG or change the chart. Distant silhouettes are not event intelligence.</summary>
    public static class VoyageLookoutProjector
    {
        public const float HalfFieldOfViewDegrees = 55f;

        public static IReadOnlyList<LookoutObservation> Project(CoreVoyageMap map, VoyagePoint origin,
            SailingDirection heading, float eventVisionNm = 120f)
        {
            var result = new List<LookoutObservation>();
            if (map == null || map.Route == null) return result;
            float unit = Math.Max(1f, map.Route.NauticalMilesPerMapUnit);
            float eventRange = Math.Max(1f, eventVisionNm / unit);
            float terrainRange = eventRange * 3f;
            var seen = new bool[map.CellCount];
            var queue = new Queue<int>();
            // Connected terrain footprints produce one silhouette, not one sticker per grid square.
            for (int start = 0; start < map.CellCount; start++)
            {
                int group = TerrainGroup(map.TerrainCells[start]);
                if (seen[start] || group < 0) continue;
                seen[start] = true;
                queue.Enqueue(start);
                LookoutObservation nearest = null;
                int count = 0;
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue();
                    int x = cell % CoreVoyageMap.GridColumns, y = cell / CoreVoyageMap.GridColumns;
                    count++;
                    VoyagePoint point = map.CellCenter(x, y);
                    LookoutObservation sample = Observe(map, origin, heading, point, terrainRange, (LookoutSceneryKind)group);
                    if (sample != null && (nearest == null || sample.DistanceNm < nearest.DistanceNm)) nearest = sample;
                    foreach (int neighbor in Neighbors(x, y))
                    {
                        if (seen[neighbor] || TerrainGroup(map.TerrainCells[neighbor]) != group) continue;
                        seen[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }
                if (nearest == null) continue;
                nearest.Key = "terrain-" + start;
                nearest.Label = TerrainLabel(nearest.Kind);
                nearest.Variant = unchecked((int)map.Hash ^ start * 397) & 3;
                nearest.Scale *= Math.Min(1.45f, 0.8f + (float)Math.Sqrt(count) * 0.09f);
                result.Add(nearest);
            }
            foreach (CoreMapNode node in map.Nodes)
            {
                // Intel-only locations can appear on a chart, but are not physical sightings.
                if (!node.IsChartVisible || !node.Discovered || !node.Encounter.CanDiscoverBySight || node.Visited || node.Skipped) continue;
                LookoutSceneryKind kind = EventKind(node);
                LookoutObservation item = Observe(map, origin, heading, node.Position, eventRange, kind);
                if (item == null) continue;
                item.Key = "event-" + node.Id;
                item.Label = node.Encounter.Name;
                item.IsEvent = true;
                item.Variant = node.Id & 3;
                // A known coastal/weather encounter annotates its actual feature, not a duplicate island.
                LookoutObservation terrain = result.Find(x => !x.IsEvent && x.Kind == kind && Distance(x.Position, node.Position) < map.CellWidth * 3f);
                if (terrain != null)
                {
                    terrain.Label = item.Label;
                    terrain.IsEvent = true;
                    terrain.Key = item.Key;
                }
                else result.Add(item);
            }
            // Every kind shares one depth axis, even though event recognition and
            // terrain silhouette ranges differ. A nearer boat must sit below a farther coast.
            foreach (LookoutObservation item in result)
                item.Depth = Math.Max(0f, Math.Min(1f, 1f - item.DistanceNm / (terrainRange * unit)));
            // Stable painter's order: far-to-near, then key (no random reshuffle on UI rebuild).
            result.Sort((a, b) => { int distance = b.DistanceNm.CompareTo(a.DistanceNm); return distance != 0 ? distance : string.CompareOrdinal(a.Key, b.Key); });
            return result;
        }

        private static LookoutObservation Observe(CoreVoyageMap map, VoyagePoint origin, SailingDirection heading,
            VoyagePoint point, float range, LookoutSceneryKind kind)
        {
            float dx = point.X - origin.X, dy = point.Y - origin.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            bool localWeather = (kind == LookoutSceneryKind.Fog || kind == LookoutSceneryKind.Storm) &&
                TerrainGroup(map.TerrainAt(origin)) == (int)kind && distance <= map.CellWidth * 0.75f;
            if (localWeather) return new LookoutObservation { Position = origin, Kind = kind, DistanceNm = 0,
                Horizontal = 0, Proximity = 1, Scale = 1.4f, Visibility = 1 };
            if (distance > range || distance < 0.15f) return null;
            double radians = ((int)heading * 45d) * Math.PI / 180d;
            // Enum begins at north and proceeds clockwise. Positive side = starboard.
            double fx = Math.Sin(radians), fy = Math.Cos(radians);
            double forward = dx * fx + dy * fy;
            double side = dx * fy - dy * fx;
            double angle = Math.Atan2(side, forward);
            double half = HalfFieldOfViewDegrees * Math.PI / 180d;
            if (Math.Abs(angle) > half) return null;
            float visibility = LineVisibility(map, origin, point, kind, range);
            if (visibility <= 0.06f) return null;
            float proximity = 1f - distance / range;
            return new LookoutObservation
            {
                Position = point, Kind = kind, DistanceNm = distance * map.Route.NauticalMilesPerMapUnit,
                Horizontal = (float)(Math.Tan(angle) / Math.Tan(half)), Proximity = proximity,
                Scale = 0.22f + 0.78f * proximity * proximity, Visibility = visibility
            };
        }

        public static float LineVisibility(CoreVoyageMap map, VoyagePoint from, VoyagePoint to, LookoutSceneryKind kind, float range)
        {
            float length = Distance(from, to), fog = 0f;
            int steps = Math.Max(1, (int)Math.Ceiling(length / 0.7f));
            float stepLength = length / steps;
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                SeaTerrainType type = map.TerrainAt(new VoyagePoint(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t));
                // Do not reject the visible front surface of the target island itself.
                if (type == SeaTerrainType.Island && (kind != LookoutSceneryKind.Island || length * (1f - t) > map.CellWidth * 1.1f)) return 0f;
                if (type == SeaTerrainType.FogBank) fog += stepLength * 1.2f;
                if (type == SeaTerrainType.StormFront) fog += stepLength * 1.7f;
            }
            return Math.Max(0f, 1f - fog / Math.Max(2f, range * 0.6f));
        }

        private static int TerrainGroup(SeaTerrainType type)
        {
            switch (type)
            {
                case SeaTerrainType.Island: case SeaTerrainType.Beach: return 0;
                case SeaTerrainType.Reef: case SeaTerrainType.KelpField: return 1;
                case SeaTerrainType.StormFront: return 2;
                case SeaTerrainType.FogBank: return 3;
                default: return -1;
            }
        }

        private static LookoutSceneryKind EventKind(CoreMapNode node)
        {
            string id = node.Encounter.Id ?? string.Empty;
            // The anchor terrain does not turn an actual ambushing ship into a rock.
            if (node.IsBoss || id.Contains("pirate") || id.Contains("ambush") || id.Contains("ghost")) return LookoutSceneryKind.PirateShip;
            if (id.Contains("convoy") || id.Contains("customs") || id.Contains("market") ||
                id.Contains("broken_mast") || id.Contains("sunken_boat") || id.Contains("divers")) return LookoutSceneryKind.Vessel;
            switch (node.TerrainConstraint)
            {
                case EventTerrainConstraint.Beach: return LookoutSceneryKind.Island;
                case EventTerrainConstraint.Strait: return LookoutSceneryKind.Island;
                case EventTerrainConstraint.Reef: return LookoutSceneryKind.Reef;
                case EventTerrainConstraint.Storm: return LookoutSceneryKind.Storm;
                case EventTerrainConstraint.Fog: return LookoutSceneryKind.Fog;
            }
            return LookoutSceneryKind.Vessel;
        }

        private static IEnumerable<int> Neighbors(int x, int y)
        {
            if (x > 0) yield return y * CoreVoyageMap.GridColumns + x - 1;
            if (x + 1 < CoreVoyageMap.GridColumns) yield return y * CoreVoyageMap.GridColumns + x + 1;
            if (y > 0) yield return (y - 1) * CoreVoyageMap.GridColumns + x;
            if (y + 1 < CoreVoyageMap.GridRows) yield return (y + 1) * CoreVoyageMap.GridColumns + x;
        }

        private static float Distance(VoyagePoint a, VoyagePoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        private static string TerrainLabel(LookoutSceneryKind kind)
        {
            switch (kind) { case LookoutSceneryKind.Island: return "海岛 / 岸线"; case LookoutSceneryKind.Reef: return "露出水面的礁群";
                case LookoutSceneryKind.Storm: return "暴风前沿"; default: return "海雾"; }
        }
    }
}
