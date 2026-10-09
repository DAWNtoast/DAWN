using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class VoyageLookoutProjectorTests
    {
        private static CoreVoyageMap Sea() => new CoreVoyageMap { Route = CoreVoyageSession.Routes[0], Hash = 14217, Seed = "LOOKOUT" };
        private static CoreMapNode Ship(CoreVoyageMap map, float x, float y, int id = 1)
        {
            var node = new CoreMapNode { Id = id, Position = new VoyagePoint(x, y), Discovered = true,
                Encounter = new CoreEncounter("pirate_" + id, "瞭望目标", "战斗", "目标", true, false, 1, 70) };
            map.Nodes.Add(node); return node;
        }

        [Test]
        public void Turn_ChangesBearingAndRejectsBehindShip()
        {
            var map = Sea(); Ship(map, 53, 33);
            var origin = new VoyagePoint(50, 32);
            var east = VoyageLookoutProjector.Project(map, origin, SailingDirection.East).Single();
            Assert.That(east.Horizontal, Is.LessThan(0), "North of east is port, not starboard.");
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.West), Is.Empty);
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.North), Is.Empty, "Outside 110 degree cone.");
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.NorthEast).Single().Horizontal, Is.GreaterThan(0));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Approach_IncreasesApparentSizeAndShowsTrueNauticalDistance(int routeIndex)
        {
            var map = Sea(); map.Route = CoreVoyageSession.Routes[routeIndex]; Ship(map, 53, 32);
            var far = VoyageLookoutProjector.Project(map, new VoyagePoint(50, 32), SailingDirection.East).Single();
            var near = VoyageLookoutProjector.Project(map, new VoyagePoint(52, 32), SailingDirection.East).Single();
            // The points are three and one chart units apart; each route owns
            // its nautical-mile scale instead of sharing the old fixed 30.
            Assert.That(far.DistanceNm, Is.EqualTo(3f * map.Route.NauticalMilesPerMapUnit).Within(0.01));
            Assert.That(near.DistanceNm, Is.EqualTo(map.Route.NauticalMilesPerMapUnit).Within(0.01));
            Assert.That(near.Scale, Is.GreaterThan(far.Scale));
            Assert.That(near.Proximity, Is.GreaterThan(far.Proximity));
        }

        [Test]
        public void HiddenIntelOnlyResolvedAndOutOfRangeEvents_AreNeverLeaked()
        {
            var map = Sea(); var node = Ship(map, 53, 32);
            var origin = new VoyagePoint(50, 32);
            node.Discovered = false; node.IntelRevealed = true;
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.East), Is.Empty);
            node.Discovered = true; node.Visited = true;
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.East), Is.Empty);
            node.Visited = false; node.Skipped = true;
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.East), Is.Empty);
            node.Skipped = false; node.Encounter = new CoreEncounter("secret", "隐藏剧情", "秘密", "未知", false, true, 0, 0);
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.East), Is.Empty);
            node.Encounter = new CoreEncounter("pirate", "海盗", "战斗", "目标", true, false, 0, 70); node.Position = new VoyagePoint(60, 32);
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.East), Is.Empty);
        }

        [Test]
        public void LandBlocksVessels_WhileStraitsRemainTransparent()
        {
            var map = Sea(); Ship(map, 58, 33);
            var origin = new VoyagePoint(50, 33);
            int cell = 16 * CoreVoyageMap.GridColumns + 26;
            map.TerrainCells[cell] = SeaTerrainType.Island;
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.East, 300).Any(x => x.IsEvent), Is.False);
            map.TerrainCells[cell] = SeaTerrainType.Strait;
            Assert.That(VoyageLookoutProjector.Project(map, origin, SailingDirection.East, 300).Single().IsEvent, Is.True);
        }

        [Test]
        public void WeatherAttenuatesSight_ConnectedTerrainDoesNotBecomeRepeatedStickers()
        {
            var map = Sea(); Ship(map, 59, 33);
            var origin = new VoyagePoint(50, 33);
            var clear = VoyageLookoutProjector.Project(map, origin, SailingDirection.East, 360).Single();
            for (int x = 25; x < 28; x++) map.TerrainCells[16 * CoreVoyageMap.GridColumns + x] = SeaTerrainType.FogBank;
            var foggy = VoyageLookoutProjector.Project(map, origin, SailingDirection.East, 360);
            Assert.That(foggy.Count(x => x.Kind == LookoutSceneryKind.Fog), Is.EqualTo(1));
            Assert.That(foggy.Where(x => x.IsEvent).All(x => x.Visibility < clear.Visibility), Is.True);
        }

        [Test]
        public void ShipsAndTerrainShareOneWaterlineDepth_AcrossDifferentRecognitionRanges()
        {
            var map = Sea(); Ship(map, 53, 33);
            map.TerrainCells[16 * 48 + 28] = SeaTerrainType.Island;
            var sight = VoyageLookoutProjector.Project(map, new VoyagePoint(50, 33), SailingDirection.East);
            var ship = sight.Single(x => x.Kind == LookoutSceneryKind.PirateShip);
            var coast = sight.Single(x => x.Kind == LookoutSceneryKind.Island);
            Assert.That(ship.DistanceNm, Is.LessThan(coast.DistanceNm));
            Assert.That(ship.Depth, Is.GreaterThan(coast.Depth), "Nearer ship must occupy foreground water, not float behind a farther island.");
        }

        [Test]
        public void StandingInsideWeather_ShowsSurroundingRainEvenWhenLookingAway()
        {
            var map = Sea();
            map.TerrainCells[16 * CoreVoyageMap.GridColumns + 24] = SeaTerrainType.StormFront;
            var origin = map.CellCenter(24, 16);
            var storm = VoyageLookoutProjector.Project(map, origin, SailingDirection.West).Single();
            Assert.That(storm.Kind, Is.EqualTo(LookoutSceneryKind.Storm));
            Assert.That(storm.DistanceNm, Is.Zero);
            Assert.That(storm.Scale, Is.GreaterThan(1));
        }

        [Test]
        public void ProjectionIsStableFarToNear_AndNeverMutatesExplorationOrNodeState()
        {
            var map = Sea(); Ship(map, 53, 32, 1); Ship(map, 51, 32, 2);
            var origin = new VoyagePoint(50, 32);
            var first = VoyageLookoutProjector.Project(map, origin, SailingDirection.East);
            var second = VoyageLookoutProjector.Project(map, origin, SailingDirection.East);
            Assert.That(first.Select(x => x.Key), Is.EqualTo(second.Select(x => x.Key)));
            Assert.That(first.Select(x => x.Scale), Is.EqualTo(second.Select(x => x.Scale)));
            Assert.That(first.First().DistanceNm, Is.GreaterThan(first.Last().DistanceNm));
            Assert.That(map.ExploredCells.All(x => !x), Is.True);
            Assert.That(map.Nodes.All(x => x.Discovered && !x.Visited && !x.Skipped), Is.True);
        }
    }
}
