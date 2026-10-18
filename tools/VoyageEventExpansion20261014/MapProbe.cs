using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ShatteredSail.Core.Gameplay;

static class MapProbe
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Generate = typeof(CoreVoyageSession).GetMethods(Private).Single(m => m.Name == "GenerateMap" && m.GetParameters().Length == 6);
    static CoreVoyageMap Make(int version, int route, int sample, out CoreVoyageSession session)
    {
        session = new CoreVoyageSession();
        var map = (CoreVoyageMap)Generate.Invoke(session, new object[] { CoreVoyageSession.Routes[route], "PACING-" + sample,
            CoreVoyageSession.DestinationPorts[0], CoreVoyageSession.DestinationPorts[1 + sample % 5], null, version });
        typeof(CoreVoyageSession).GetProperty("Map").SetValue(session, map);
        return map;
    }
    static string Signature(CoreVoyageMap map)
    {
        var text = new StringBuilder();
        foreach (var node in map.Nodes) text.Append(node.Id).Append(':').Append(node.Encounter.Id).Append(':').Append(node.Position.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(node.Position.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
        foreach (var cell in map.TerrainCells) text.Append((int)cell).Append(',');
        foreach (var owner in map.AnchorNodeIds) text.Append(owner).Append(',');
        foreach (var variant in map.TerrainVariants) text.Append(variant).Append(',');
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static float Offset(CoreVoyageMap map, VoyagePoint point, out float progress)
    {
        float dx = map.Destination.X - map.Departure.X, dy = map.Destination.Y - map.Departure.Y;
        progress = ((point.X - map.Departure.X) * dx + (point.Y - map.Departure.Y) * dy) / (dx * dx + dy * dy);
        float x = point.X - map.Departure.X - progress * dx, y = point.Y - map.Departure.Y - progress * dy;
        return (float)Math.Sqrt(x * x + y * y);
    }
    public static void Main(string[] args)
    {
        var baseline = new List<string>();
        for (int version = 0; version <= 1; version++)
        for (int route = 0; route < 3; route++)
        for (int sample = 0; sample < 10; sample++) baseline.Add(version + "," + route + "," + sample + "," + Signature(Make(version, route, sample, out _)));
        string baselinePath = Path.Combine(args[0], "legacy-map-baseline.csv");
        if (args.Contains("baseline")) { File.WriteAllLines(baselinePath, baseline); Console.WriteLine("Saved 60 geometry 0/1 map signatures before changes."); return; }
        Require(File.ReadAllLines(baselinePath).SequenceEqual(baseline), "Legacy map signatures changed");
        Console.WriteLine("PASS: all 60 legacy map IDs, positions, terrain, owners and variants match pre-change baseline.");
        var canSee = typeof(CoreVoyageSession).GetMethod("CanSeeCell", Private);
        var basePool = typeof(CoreVoyageSession).GetMethod("CreateEncounterPool", Private);
        int sampleCount = args.Length > 1 && int.TryParse(args[1], out int requestedSamples) ? requestedSamples : 40;
        var report = new List<string> { "route,sample,nodes,ordinary,side_branches,visible_bands,first_fixed_visible_nm,reverse_first_fixed_visible_nm,signature" };
        for (int route = 0; route < 3; route++)
        {
            var sets = new HashSet<string>();
            for (int sample = 0; sample < sampleCount; sample++)
            {
                CoreVoyageMap map;
                CoreVoyageSession session;
                try { map = Make(2, route, sample, out session); }
                catch (Exception error) { throw new Exception("Generation route=" + route + " sample=" + sample, error); }
                var original = ((IReadOnlyList<CoreEncounter>)basePool.Invoke(session, new object[] { map.Route })).Select(x => x.Id).ToArray();
                var extra = new HashSet<string>(CoreVoyageEventCatalog.CreateFixedEncounters(map.Route.Id).Select(x => x.Id));
                var ordinary = map.Nodes.Where(x => original.Contains(x.Encounter.Id) || extra.Contains(x.Encounter.Id)).ToArray();
                Require(ordinary.Length == original.Length + 4, "Four additions required");
                Require(original.All(id => ordinary.Any(x => x.Encounter.Id == id)), "Original ordinary pool lost");
                sets.Add(String.Join("|", ordinary.Where(x => extra.Contains(x.Encounter.Id)).Select(x => x.Encounter.Id).OrderBy(x => x)));
                Require(map.Nodes.Select(x => x.Encounter.Id).Distinct().Count() == map.Nodes.Count, "Repeated fixed event");
                var path = session.PlanNavigablePath(map.Departure, map.Destination);
                Require(path.Count == 2, "Main course was blocked or rerouted at route=" + route + " sample=" + sample);
                int visibleBands = 0;
                for (int band = 0; band < 3; band++)
                {
                    float lo = band == 0 ? .10f : band == 1 ? .40f : .80f, hi = band == 0 ? .17f : band == 1 ? .58f : .90f;
                    bool visible = ordinary.Any(node => {
                        if (!node.Encounter.CanDiscoverBySight || Offset(map, node.Position, out float progress) > 2.1001f || progress < lo || progress > hi) return false;
                        var observer = new VoyagePoint(map.Departure.X + (map.Destination.X - map.Departure.X) * progress, map.Departure.Y + (map.Destination.Y - map.Departure.Y) * progress);
                        var cell = map.PointToCell(node.Position);
                        return (bool)canSee.Invoke(session, new object[] { observer, cell.Column, cell.Row, 120f / map.Route.NauticalMilesPerMapUnit });
                    });
                    Require(visible, "Missing visible band " + band + " route=" + route + " sample=" + sample);
                    visibleBands++;
                }
                int side = ordinary.Count(node => Offset(map, node.Position, out _) > 8f);
                Require(side >= 4, "Exploration side branches lost");
                float firstVisible = float.MaxValue;
                for (int step = 0; step <= 200 && firstVisible == float.MaxValue; step++)
                {
                    float progress = step / 200f;
                    var observer = new VoyagePoint(map.Departure.X + (map.Destination.X - map.Departure.X) * progress, map.Departure.Y + (map.Destination.Y - map.Departure.Y) * progress);
                    foreach (var node in ordinary.Where(x => x.Encounter.CanDiscoverBySight))
                    {
                        var cell = map.PointToCell(node.Position);
                        if ((bool)canSee.Invoke(session, new object[] { observer, cell.Column, cell.Row, 120f / map.Route.NauticalMilesPerMapUnit }))
                        { float dx = observer.X - map.Departure.X, dy = observer.Y - map.Departure.Y; firstVisible = (float)Math.Sqrt(dx * dx + dy * dy) * map.Route.NauticalMilesPerMapUnit; break; }
                    }
                }
                Require(firstVisible <= 340f, "Early fixed discovery too late: " + firstVisible);
                foreach (var node in map.Nodes)
                {
                    Require(map.IsPassable(map.PointToCell(node.Position).Column, map.PointToCell(node.Position).Row), "Unreachable anchor terrain");
                    Require(session.PlanNavigablePath(map.Departure, node.Position).Count >= 2, "Isolated event anchor");
                }
                var reserved = new HashSet<int>();
                foreach (var footprint in map.EventFootprints) foreach (int cell in footprint.ReservedCellIndices) Require(reserved.Add(cell), "Overlapping reservations");
                float reverseFirst = float.MaxValue;
                for (int step = 0; step <= 200 && reverseFirst == float.MaxValue; step++)
                {
                    float progress = step / 200f;
                    var observer = new VoyagePoint(map.Destination.X + (map.Departure.X - map.Destination.X) * progress, map.Destination.Y + (map.Departure.Y - map.Destination.Y) * progress);
                    foreach (var node in ordinary.Where(x => x.Encounter.CanDiscoverBySight))
                    {
                        var cell = map.PointToCell(node.Position);
                        if ((bool)canSee.Invoke(session, new object[] { observer, cell.Column, cell.Row, 120f / map.Route.NauticalMilesPerMapUnit }))
                        { float dx = observer.X - map.Destination.X, dy = observer.Y - map.Destination.Y; reverseFirst = (float)Math.Sqrt(dx * dx + dy * dy) * map.Route.NauticalMilesPerMapUnit; break; }
                    }
                }
                Require(session.PlanNavigablePath(map.Destination, map.Departure).Count == 2, "Reverse main course is blocked");
                Require(reverseFirst <= 420f, "Reverse early fixed discovery too late: " + reverseFirst);
                report.Add(String.Join(",", route, sample, map.Nodes.Count, ordinary.Length, side, visibleBands, firstVisible.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), reverseFirst.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), Signature(map)));
            }
            Require(sets.Count >= 15, "Too few changing fixed content combinations: " + sets.Count);
            Console.WriteLine("PASS route " + route + ": " + sampleCount + " maps, 3 actually visible course bands, 4+ side branches, all anchors reachable, reverse course and early visibility, " + sets.Count + " fixed-content combinations.");
        }
        File.WriteAllLines(Path.Combine(args[0], "map-pacing-samples.csv"), report);
    }
}
