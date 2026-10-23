using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class VoyageEventDistributionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void NewChartUsesSs6WhileHistoricalSs3RetainsItsVersion()
        {
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            Assert.That(seed, Does.StartWith("SS6."));
            Assert.That(CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition chart), Is.True);
            Assert.That(chart.MapGeometryVersion, Is.EqualTo(4));
            Assert.That(CoreVoyageSession.TryParseChartSeed(seed.Replace("SS6.", "SS5."), out _), Is.False);
            var compact = new CoreVoyageMap(chart.MapGeometryVersion);
            Assert.That(compact.FineColumns, Is.EqualTo(96));
            Assert.That(compact.FineRows, Is.EqualTo(68));
            Assert.That(compact.CellWidth, Is.EqualTo(compact.CellHeight).Within(.00001f));
            const string legacy = "SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6";
            Assert.That(CoreVoyageSession.TryParseChartSeed(legacy, out chart), Is.True);
            Assert.That(chart.MapGeometryVersion, Is.EqualTo(1));
            var geometry = new CoreVoyageMap(2);
            Assert.That(geometry.WorldWidth, Is.EqualTo(100f));
            Assert.That(geometry.WorldHeight, Is.EqualTo(75f));
            Assert.That(geometry.FineColumns, Is.EqualTo(96));
            Assert.That(geometry.FineRows, Is.EqualTo(72));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CompactChartsKeepEveryEventInsideTheSeaAndReachable(int route)
        {
            for (int sample = 0; sample < 12; sample++)
            {
                CoreVoyageMap map = Generate(4, route, sample, out CoreVoyageSession session);
                Assert.That(map.Columns, Is.EqualTo(48));
                Assert.That(map.Rows, Is.EqualTo(34));
                Assert.That(map.WorldHeight, Is.EqualTo(100f * 34f / 48f).Within(.00001f));
                foreach (CoreMapNode node in map.Nodes)
                {
                    Assert.That(node.Position.X, Is.InRange(0f, map.WorldWidth), node.Encounter.Id);
                    Assert.That(node.Position.Y, Is.InRange(0f, map.WorldHeight), node.Encounter.Id);
                    Assert.That(session.PlanNavigablePath(map.Departure, node.Position).Count,
                        Is.GreaterThanOrEqualTo(2), "Unreachable compact-chart event " + node.Encounter.Id);
                }
                Assert.That(session.PlanNavigablePath(map.Departure, map.Destination).Count, Is.EqualTo(2));
                Assert.That(session.PlanNavigablePath(map.Destination, map.Departure).Count, Is.EqualTo(2));
                Assert.That(Signature(Generate(4, route, sample, out _)), Is.EqualTo(Signature(map)),
                    "Compact charts must regenerate the same events, terrain and coordinates.");
            }
        }

        // Captured from the unmodified geometry 0/1 generator before the event
        // expansion. Covers identities, coordinates, terrain, ownership and art variants.
        [TestCase(0, "36059D501FCC07FCB8FC9C24BD9A8199C2712F740DDA25ACB957BD821EA56778")]
        [TestCase(1, "3BA90F4ECCFFFF0BC5D727F4C769717021570E5AAF15FD063EFC0D842840917C")]
        public void HistoricalMapLayoutMatchesPreExpansionSnapshot(int version, string expected)
        {
            CoreVoyageMap map = Generate(version, 0, 0, out _);
            Assert.That(Signature(map), Is.EqualTo(expected));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void NewChartsVaryFourAdditionsAndExposeCourseStopsInBothDirections(int route)
        {
            var combinations = new HashSet<string>();
            MethodInfo basePool = typeof(CoreVoyageSession).GetMethod("CreateEncounterPool", Private);
            MethodInfo canSee = typeof(CoreVoyageSession).GetMethod("CanSeeCell", Private);
            for (int sample = 0; sample < 12; sample++)
            {
                CoreVoyageMap map = Generate(2, route, sample, out CoreVoyageSession session);
                string[] original = ((IReadOnlyList<CoreEncounter>)basePool.Invoke(session, new object[] { map.Route })).Select(x => x.Id).ToArray();
                var extraIds = new HashSet<string>(CoreVoyageEventCatalog.CreateFixedEncounters(map.Route.Id).Select(x => x.Id));
                CoreMapNode[] ordinary = map.Nodes.Where(x => original.Contains(x.Encounter.Id) || extraIds.Contains(x.Encounter.Id)).ToArray();
                Assert.That(ordinary.Length, Is.EqualTo(original.Length + 4));
                Assert.That(original.All(id => ordinary.Any(x => x.Encounter.Id == id)), Is.True);
                Assert.That(map.Nodes.Select(x => x.Encounter.Id).Distinct().Count(), Is.EqualTo(map.Nodes.Count));
                combinations.Add(string.Join("|", ordinary.Where(x => extraIds.Contains(x.Encounter.Id)).Select(x => x.Encounter.Id).OrderBy(x => x)));
                Assert.That(session.PlanNavigablePath(map.Departure, map.Destination).Count, Is.EqualTo(2), "Actual path must use the protected sailing course.");
                Assert.That(session.PlanNavigablePath(map.Destination, map.Departure).Count, Is.EqualTo(2));

                for (int band = 0; band < 3; band++)
                {
                    float low = band == 0 ? .10f : band == 1 ? .40f : .80f;
                    float high = band == 0 ? .17f : band == 1 ? .58f : .90f;
                    Assert.That(ordinary.Any(node =>
                    {
                        if (!node.Encounter.CanDiscoverBySight || Offset(map, node.Position, out float progress) > 2.1001f || progress < low || progress > high) return false;
                        var observer = new VoyagePoint(map.Departure.X + (map.Destination.X - map.Departure.X) * progress,
                            map.Departure.Y + (map.Destination.Y - map.Departure.Y) * progress);
                        VoyageGridCoordinate cell = map.PointToCell(node.Position);
                        return (bool)canSee.Invoke(session, new object[] { observer, cell.Column, cell.Row, 120f / map.Route.NauticalMilesPerMapUnit });
                    }), Is.True, "Missing actually visible ordinary stop in course band " + band + ", sample " + sample);
                }
                Assert.That(ordinary.Count(node => Offset(map, node.Position, out _) > 8f), Is.GreaterThanOrEqualTo(4), "Keep exploration away from the main course.");
                var reservations = new HashSet<int>();
                foreach (CoreEventFootprint footprint in map.EventFootprints)
                    foreach (int cell in footprint.ReservedCellIndices) Assert.That(reservations.Add(cell), Is.True, "Safety bands overlap.");
                foreach (CoreMapNode node in map.Nodes)
                    Assert.That(session.PlanNavigablePath(map.Departure, node.Position).Count, Is.GreaterThanOrEqualTo(2), "Unreachable " + node.Encounter.Id);
                Assert.That(Signature(Generate(2, route, sample, out _)), Is.EqualTo(Signature(map)), "The same chart must regenerate exactly.");
            }
            Assert.That(combinations.Count, Is.GreaterThanOrEqualTo(6));
        }

        private static CoreVoyageMap Generate(int version, int route, int sample, out CoreVoyageSession session)
        {
            session = new CoreVoyageSession();
            MethodInfo method = typeof(CoreVoyageSession).GetMethods(Private).Single(x => x.Name == "GenerateMap" && x.GetParameters().Length == 6);
            var map = (CoreVoyageMap)method.Invoke(session, new object[] { CoreVoyageSession.Routes[route], "PACING-" + sample,
                CoreVoyageSession.DestinationPorts[0], CoreVoyageSession.DestinationPorts[1 + sample % 5], null, version });
            typeof(CoreVoyageSession).GetProperty("Map").SetValue(session, map);
            return map;
        }

        private static float Offset(CoreVoyageMap map, VoyagePoint point, out float progress)
        {
            float dx = map.Destination.X - map.Departure.X, dy = map.Destination.Y - map.Departure.Y;
            progress = ((point.X - map.Departure.X) * dx + (point.Y - map.Departure.Y) * dy) / (dx * dx + dy * dy);
            float x = point.X - map.Departure.X - progress * dx, y = point.Y - map.Departure.Y - progress * dy;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private static string Signature(CoreVoyageMap map)
        {
            var text = new StringBuilder();
            foreach (CoreMapNode node in map.Nodes) text.Append(node.Id).Append(':').Append(node.Encounter.Id).Append(':')
                .Append(node.Position.X.ToString("R", CultureInfo.InvariantCulture)).Append(':').Append(node.Position.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            foreach (SeaTerrainType cell in map.TerrainCells) text.Append((int)cell).Append(',');
            foreach (int owner in map.AnchorNodeIds) text.Append(owner).Append(',');
            foreach (byte variant in map.TerrainVariants) text.Append(variant).Append(',');
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }
    }
}
