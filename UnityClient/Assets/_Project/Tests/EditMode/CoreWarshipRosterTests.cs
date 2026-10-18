using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreWarshipRosterTests
    {
        [Test]
        public void SixSchoolsEachHaveOneEpicGuardianAndOneLegendaryWithFiveMatchingComponents()
        {
            string[] schools = CoreSchoolUnlockCatalog.All.Select(school => school.Id).ToArray();
            Assert.That(CoreRouteBossCatalog.All.Count, Is.EqualTo(6));
            Assert.That(CoreLegendaryWarships.All.Count, Is.EqualTo(6));
            CollectionAssert.AreEquivalent(schools, CoreRouteBossCatalog.All.Select(ship => ship.SchoolId));
            CollectionAssert.AreEquivalent(schools, CoreLegendaryWarships.All.Select(ship => ship.SchoolId));
            foreach (string school in schools)
                foreach (bool legendary in new[] { false, true })
                {
                    var components = CoreWarshipDesignCatalog.Components(school, legendary);
                    Assert.That(components.Count, Is.EqualTo(5), school);
                    Assert.That(components.Select(id => id.Split('_')[0]).Distinct().Count(), Is.EqualTo(5), school);
                    foreach (string component in components)
                    {
                        CoreBattleCard card = CoreSchoolCardCatalog.ForSource(component);
                        Assert.That(card.SchoolId, Is.EqualTo(school));
                        Assert.That(card.ArtId, Does.StartWith("card_school_" + school + (legendary ? "_l" : "_e")));
                    }
                    Assert.That(CoreWarshipDesignCatalog.ArtResourcePath(school, legendary),
                        Is.EqualTo("ShipArtV2/Special/" + (legendary ? "legendary_" : "boss_") + school));
                }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EachRouteHasTwoDistinctValidBossesAndSelectionCoversBoth(int route)
        {
            string routeId = CoreVoyageSession.Routes[route].Id;
            var bosses = CoreRouteBossCatalog.ForRoute(routeId);
            Assert.That(bosses.Count, Is.EqualTo(2));
            Assert.That(bosses[0].Id, Is.EqualTo("boss_" + routeId), "Retain the original save ID.");
            Assert.That(CoreRouteBossCatalog.Choose(routeId, 0), Is.SameAs(bosses[0]));
            Assert.That(CoreRouteBossCatalog.Choose(routeId, 1), Is.SameAs(bosses[1]));
            foreach (CoreRouteBoss boss in bosses)
            {
                Assert.That(CoreBattleBalance.Resolve(boss.CreateEncounter(), route), Is.SameAs(boss.Profile));
                Assert.That(CoreEncounterDiscoveryRules.TryClassify(boss.Id, out _), Is.True);
                Assert.That(CoreCollectionRewardCatalog.IsKnownBoss(boss.Id), Is.True);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void BothChoicesKeepTheSameGeographyAndSurviveRepeatedCheckpointRestores(int route)
        {
            var source = Ready(route);
            source.StartVoyage();
            var terrain = (SeaTerrainType[])source.Map.TerrainCells.Clone();
            var positions = source.Map.Nodes.Select(node => node.Position).ToArray();
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.ForRoute(source.Map.Route.Id))
            {
                Assign(source, boss);
                CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
                for (int reload = 0; reload < 3; reload++)
                {
                    var restored = new CoreVoyageSession();
                    Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
                    Assert.That(restored.CurrentRouteBossId, Is.EqualTo(boss.Id));
                    Assert.That(restored.Map.Nodes.Single(node => node.IsBoss).Encounter.Id, Is.EqualTo(boss.Id));
                    CollectionAssert.AreEqual(terrain, restored.Map.TerrainCells);
                    CollectionAssert.AreEqual(positions, restored.Map.Nodes.Select(node => node.Position));
                    checkpoint = restored.CaptureVoyageCheckpoint();
                }
            }
        }

        [Test]
        public void PriorVictoriesNeverRemoveEitherGuardianAtDepartureButCurrentVictoryStaysGoneOnReload()
        {
            var source = Ready(0);
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            Assert.That(source.TrySelectChartSeed(seed, out _), Is.True);
            source.StartVoyage();
            CoreSeaChartProgress progress = source.CaptureSeaChartProgress();
            progress.charts.Single().defeatedBosses = CoreRouteBossCatalog.ForRoute(source.Map.Route.Id).Select(boss => boss.Id).ToArray();
            var next = Ready(0);
            Assert.That(next.RestoreSeaChartProgress(progress), Is.True);
            Assert.That(next.TrySelectChartSeed(seed, out _), Is.True);
            next.StartVoyage();
            Assert.That(next.BossDefeated, Is.True, "An already-open chart must not lock its destination again.");
            Assert.That(next.CurrentRouteBossDefeated, Is.False);
            CoreMapNode node = next.Map.Nodes.Single(item => item.IsBoss);
            SetField(next, "activeNode", node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(next, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(next, CoreDemoPhase.Encounter);
            Assert.That(next.ResolveChoice(0), Is.True);
            next.Battle.EnemyHull = 0;
            Assert.That(next.EndBattleTurn(), Is.True);
            Assert.That(next.BossDefeated, Is.True);
            Assert.That(next.CurrentRouteBossDefeated, Is.True);
            Assert.That(next.Map.Nodes.Any(item => item.IsBoss), Is.False);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(next.CaptureVoyageCheckpoint(), out string reason), Is.True, reason);
            Assert.That(restored.Map.Nodes.Any(item => item.IsBoss), Is.False);
            Assert.That(restored.CurrentRouteBossId, Is.EqualTo(node.Encounter.Id));
        }

        [Test]
        public void LegacyThirdChartCheckpointKeepsItsFiveWarshipsAndOriginalGuardian()
        {
            var source = Ready(2);
            source.StartVoyage();
            SetField(source, "shipRosterVersion", 0);
            SetField(source, "voyageRouteBossId", string.Empty);
            MethodInfo generate = typeof(CoreVoyageSession).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(method => method.Name == "GenerateMap" && method.GetParameters().Length == 6);
            CoreVoyageMap oldMap = (CoreVoyageMap)generate.Invoke(source, new object[] { source.Map.Route, source.Map.Seed,
                source.Map.DepartureName, source.Map.DestinationName, (uint?)source.Map.Hash, source.Map.GeometryVersion });
            typeof(CoreVoyageSession).GetProperty("Map").SetValue(source, oldMap);
            CoreVoyageCheckpoint saved = source.CaptureVoyageCheckpoint();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(saved, out string reason), Is.True, reason);
            Assert.That(restored.Map.Nodes.Count(node => node.Encounter.BossKind == CoreBossKind.Legendary), Is.EqualTo(5));
            Assert.That(restored.Map.Nodes.Single(node => node.IsBoss).Encounter.Id, Is.EqualTo("boss_" + oldMap.Route.Id));
        }

        [Test]
        public void CollectionRequiresAllSixSchoolsAndAcceptsEitherFirstGuardian()
        {
            var progress = new CoreCollectionProgress { defeatedBossIds = CoreLegendaryWarships.All.Take(5).Select(ship => ship.Id).ToArray() };
            Assert.That(CoreCollectionRewardCatalog.Find("no_crown_logbook").IsSatisfied(progress, null, null), Is.False);
            progress.defeatedBossIds = CoreLegendaryWarships.All.Select(ship => ship.Id).ToArray();
            Assert.That(CoreCollectionRewardCatalog.Find("no_crown_logbook").IsSatisfied(progress, null, null), Is.True);
            progress.defeatedBossIds = CoreRouteBossCatalog.All.Select(boss => boss.Id).ToArray();
            Assert.That(CoreCollectionRewardCatalog.Find("moon_tide_astrolabe").IsSatisfied(progress, null, null), Is.True);
            progress.defeatedBossIds = new[] { "boss_route_shattered_sail_reef_tide" };
            Assert.That(CoreCollectionRewardCatalog.Find("first_stormglass").IsSatisfied(progress, null, null), Is.True);
        }

        [TestCase("wrong-route")] [TestCase("unknown")] [TestCase("missing")] [TestCase("inconsistent-victory")]
        public void InvalidGuardianCheckpointIsRejectedWithoutChangingTheTarget(string corruption)
        {
            var source = Ready(0); source.StartVoyage();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            if (corruption == "wrong-route") checkpoint.routeBossId = "boss_route_moon_tide_ruins_arcane";
            if (corruption == "unknown") checkpoint.routeBossId = "boss_route_invented";
            if (corruption == "missing") checkpoint.routeBossId = string.Empty;
            if (corruption == "inconsistent-victory") checkpoint.currentRouteBossDefeated = true;
            var target = new CoreVoyageSession(); target.RestoreProfile(888, "铜钟港");
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(target.ProfileGold, Is.EqualTo(888));
            Assert.That(target.CurrentPort, Is.EqualTo("铜钟港"));
            Assert.That(target.Map, Is.Null);
        }

        [Test]
        public void TutorialReplayCheckpointDoesNotAcquireAnOrdinaryGuardian()
        {
            var tutorial = new CoreVoyageSession();
            tutorial.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            tutorial.StartVoyage();
            string seed = tutorial.CurrentChartSeed;
            var replay = Ready(0);
            Assert.That(replay.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            replay.StartVoyage();
            Assert.That(replay.CurrentRouteBossId, Is.Empty);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(replay.CaptureVoyageCheckpoint(), out reason), Is.True, reason);
            Assert.That(restored.Map.Nodes.Any(node => CoreRouteBossCatalog.Find(node.Encounter.Id) != null), Is.False);
            Assert.That(restored.Map.Nodes.Any(node => node.Encounter.Id == CoreGuidedTutorialContent.BossEventId), Is.True);
        }

        private static CoreVoyageSession Ready(int route)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            session.SelectRoute(route);
            session.SetSeed("SIX-SCHOOL-CHECKPOINT-" + route);
            return session;
        }

        private static void Assign(CoreVoyageSession session, CoreRouteBoss boss)
        {
            typeof(CoreVoyageSession).GetMethod("AssignRouteBossEncounter", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { session.Map, boss });
            SetField(session, "voyageRouteBossId", boss.Id);
        }

        private static void SetField(CoreVoyageSession session, string field, object value) =>
            typeof(CoreVoyageSession).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);
    }
}
