using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class LegendaryWarshipTests
    {
        [Test]
        public void Catalog_HasFiveUniqueStoriesArchetypesAndRewardPackages()
        {
            var ships = CoreLegendaryWarships.All;
            Assert.That(ships.Count, Is.EqualTo(5));
            Assert.That(ships.Select(x => x.Id).Distinct().Count(), Is.EqualTo(5));
            Assert.That(ships.Select(x => x.Name).Distinct().Count(), Is.EqualTo(5));
            Assert.That(ships.Select(x => x.Profile.Archetype).Distinct().Count(), Is.EqualTo(5));
            Assert.That(ships.Select(x => x.Trophy).Distinct().Count(), Is.EqualTo(5));
            foreach (CoreLegendaryWarship ship in ships)
            {
                Assert.That(ship.Description.Length, Is.GreaterThan(35));
                Assert.That(ship.CreateEncounter().Choices[0].Effect.Gold, Is.EqualTo(ship.Gold));
                Assert.That(CoreLegendaryWarships.Find(ship.Id), Is.SameAs(ship));
            }
            Assert.That(CoreLegendaryWarships.Find("missing"), Is.Null);
        }

        [Test]
        public void OptionalBossRewardLabels_IncludeTheSeparateBaseBattlePayout()
        {
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
            {
                CoreEncounter encounter = ship.CreateEncounter();
                Assert.That(encounter.Choices[0].Detail, Does.Contain(CoreBattleBalance.BossVictoryBonusDescription));
            }
            Assert.That(CoreLegendaryWarships.CreateTreasureEncounter().Choices[0].Detail,
                Does.Contain(CoreBattleBalance.BossVictoryBonusDescription));
        }

        [Test]
        public void LegendaryProfiles_KeepDistinctStrengthsAndUseOrdinarySailorHealth()
        {
            CoreBattleProfile treasure = CoreBattleBalance.ThirdTreasureBoss;
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
            {
                CoreBattleProfile profile = ship.Profile;
                Assert.That(profile.BossKind, Is.EqualTo(CoreBossKind.Legendary));
                Assert.That(profile.EnemyHull > treasure.EnemyHull || profile.BoardingHealth > treasure.BoardingHealth,
                    Is.True, ship.Name + " needs an endurance advantage, not superiority on every axis.");
                Assert.That(profile.CannonDamage > treasure.CannonDamage || profile.MeleeDamage > treasure.MeleeDamage,
                    Is.True, ship.Name + " needs an offensive specialty.");
                Assert.That(profile.OrdinaryCrewCount, Is.GreaterThan(0), ship.Name);
                Assert.That(profile.BoardingHealth,
                    Is.EqualTo((int)Math.Round(CoreBattleBalance.BoardingHealthForOrdinaryCrew(profile.OrdinaryCrewCount) *
                        profile.BoardingHealthMultiplier, MidpointRounding.AwayFromZero)), ship.Name);
                Assert.That(profile.RamChance, Is.InRange(0d, 1d), ship.Name);
                Assert.That(profile.RamRangeMeters, Is.GreaterThan(0f), ship.Name);
            }
            Assert.That(treasure.EnemyHull, Is.GreaterThan(CoreBattleBalance.ThirdRouteBoss.EnemyHull));
            Assert.That(treasure.BoardingHealth, Is.GreaterThan(CoreBattleBalance.ThirdRouteBoss.BoardingHealth));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void FirstTwoCharts_DoNotContainOptionalEndgameBosses(int route)
        {
            CoreVoyageMap map = Voyage(route, "EARLY-CHART").Map;
            Assert.That(map.Nodes.Any(x => x.Encounter.BossKind == CoreBossKind.Legendary ||
                x.Encounter.BossKind == CoreBossKind.Treasure), Is.False);
            Assert.That(map.Nodes.Count(x => x.IsBoss), Is.EqualTo(1));
        }

        [Test]
        public void ThirdChart_ContainsAllFiveWarshipsAndSeparateTreasureBoss()
        {
            CoreVoyageMap map = Voyage(2, "ENDGAME-CHART").Map;
            Assert.That(map.Nodes.Count(x => x.IsBoss), Is.EqualTo(1), "Only the route guardian controls port access.");
            Assert.That(map.Nodes.Count(x => x.Encounter.BossKind == CoreBossKind.Route), Is.EqualTo(1));
            Assert.That(map.Nodes.Count(x => x.Encounter.BossKind == CoreBossKind.Treasure), Is.EqualTo(1));
            Assert.That(map.Nodes.Count(x => x.Encounter.BossKind == CoreBossKind.Legendary), Is.EqualTo(5));
            Assert.That(map.Nodes.Select(x => x.Encounter.Id).Distinct().Count(), Is.EqualTo(map.Nodes.Count));
            foreach (CoreMapNode node in map.Nodes.Where(IsOptionalBoss))
            {
                Assert.That(node.IsBoss, Is.False);
                Assert.That(node.Encounter.Mandatory, Is.False);
                Assert.That(node.Encounter.Hidden, Is.False, "A visible ship can be discovered normally, without a specific crew.");
                Assert.That(node.Encounter.Choices[0].StartsBattle, Is.True);
                Assert.That(node.Encounter.Choices[1].StartsBattle, Is.False);
                Assert.That(node.Encounter.Choices[1].Hours, Is.Zero);
                Assert.That(node.Footprint, Is.Not.Null);
            }
        }

        [Test]
        public void WarshipPositions_AreSeedBoundSeparatedReachableOpenSea()
        {
            HashSet<string> positions = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++)
            {
                string seedText = "LEGENDARY-POSITION-" + seed;
                CoreVoyageMap map = Voyage(2, seedText).Map;
                CoreVoyageMap same = Voyage(2, seedText).Map;
                HashSet<int> reachable = ReachableCells(map);
                foreach (CoreMapNode node in map.Nodes.Where(x => x.Encounter.BossKind == CoreBossKind.Legendary))
                {
                    CoreMapNode duplicate = same.Nodes.Single(x => x.Encounter.Id == node.Encounter.Id);
                    Assert.That(node.Position.X, Is.EqualTo(duplicate.Position.X));
                    Assert.That(node.Position.Y, Is.EqualTo(duplicate.Position.Y));
                    Assert.That(node.TerrainConstraint, Is.EqualTo(EventTerrainConstraint.OpenSea));
                    Assert.That(map.TerrainAt(node.Position), Is.EqualTo(SeaTerrainType.OpenWater));
                    Assert.That(reachable.Contains(map.CellIndex(node.Position)), Is.True, node.Encounter.Name + " must be reachable from port.");
                    Assert.That(node.Footprint.CellIndices.All(x => map.TerrainCells[x] == SeaTerrainType.OpenWater), Is.True);
                    foreach (CoreMapNode other in map.Nodes.Where(x => x.Id != node.Id))
                        Assert.That(Distance(node.Position, other.Position), Is.GreaterThanOrEqualTo(map.Route.MinimumEventSpacing - .001f));
                    positions.Add(node.Encounter.Id + ":" + map.CellIndex(node.Position));
                }
            }
            Assert.That(positions.Count, Is.GreaterThan(65), "Independent seeds must not place every legend at a fixed coordinate.");
        }

        [TestCase(CoreBossKind.Route)]
        [TestCase(CoreBossKind.Treasure)]
        [TestCase(CoreBossKind.Legendary)]
        public void Winning_OnlyRouteBossUnlocksPortAndEveryRewardIsOneShot(CoreBossKind kind)
        {
            CoreVoyageSession session = Voyage(2, "BOSS-WIN-" + kind);
            CoreMapNode node = session.Map.Nodes.First(x => x.Encounter.BossKind == kind);
            EnterEncounter(session, node);
            int goldBefore = session.Cargo.Gold;
            int fameBefore = session.Cargo.Fame;
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Battle.IsBoss, Is.True, "Optional bosses must use boss battle profiles.");
            Assert.That(session.Battle.Profile.BossKind, Is.EqualTo(kind));
            Assert.That(session.BossDefeated, Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(goldBefore), "Loot is not awarded for merely accepting a challenge.");
            session.Battle.EnemyHull = 0;
            Invoke(session, "ResolveBattleOutcome");
            Assert.That(session.BossDefeated, Is.EqualTo(kind == CoreBossKind.Route));
            Assert.That(session.Cargo.Gold, Is.EqualTo(goldBefore + node.Encounter.Choices[0].Effect.Gold + 120));
            Assert.That(session.Cargo.Fame, Is.EqualTo(fameBefore + node.Encounter.Choices[0].Effect.Fame + 18));
            Assert.That(node.Visited, Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            int after = session.Cargo.Gold;
            Assert.That(session.ResolveChoice(0), Is.False);
            Invoke(session, "ResolveBattleOutcome");
            Assert.That(session.Cargo.Gold, Is.EqualTo(after));
        }

        [TestCase(CoreBossKind.Treasure)]
        [TestCase(CoreBossKind.Legendary)]
        public void OptionalBoss_CanBeSkippedWithoutTimeCostOrUnlock(CoreBossKind kind)
        {
            CoreVoyageSession session = Voyage(2, "BOSS-SKIP-" + kind);
            CoreMapNode node = session.Map.Nodes.First(x => x.Encounter.BossKind == kind);
            EnterEncounter(session, node);
            Assert.That(session.SkipEncounter(), Is.True);
            Assert.That(node.Visited, Is.False, "A deferred optional challenge is not a defeated ship.");
            Assert.That(node.Skipped, Is.True);
            Assert.That(session.TotalHours, Is.Zero);
            Assert.That(session.BossDefeated, Is.False);
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Map.Nodes.Count(x => x.Encounter.Id == node.Encounter.Id), Is.EqualTo(1));
            Set(session, "Position", node.Position);
            session.SetCourseTo(node.Position);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter), "Clicking the nearby marker must reopen the challenge.");
            Assert.That(session.ActiveEncounter, Is.SameAs(node.Encounter));
            Assert.That(node.Skipped, Is.False);
            Assert.That(session.ResolveChoice(1), Is.True, "The explicit safe departure choice must also postpone, not solve.");
            Assert.That(node.Visited, Is.False);
            Assert.That(node.Skipped, Is.True);
        }

        [Test]
        public void LegendaryLoss_DoesNotAwardLootOrUnlockPort()
        {
            CoreVoyageSession session = Voyage(2, "BOSS-LOSS");
            CoreMapNode node = session.Map.Nodes.First(x => x.Encounter.BossKind == CoreBossKind.Legendary);
            EnterEncounter(session, node);
            int gold = session.Cargo.Gold;
            Assert.That(session.ResolveChoice(0), Is.True);
            Set(session, "Hull", 0);
            Invoke(session, "ResolveBattleOutcome");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.BossDefeated, Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(node.Visited, Is.False);
        }

        [Test]
        public void Resolve_RecognizesOptionalBossEvenWhenMapBossFlagIsFalse()
        {
            CoreLegendaryWarship ship = CoreLegendaryWarships.All[0];
            Assert.That(CoreBattleBalance.Resolve(ship.CreateEncounter(), 2, false), Is.SameAs(ship.Profile));
            Assert.That(CoreBattleBalance.Resolve(CoreLegendaryWarships.CreateTreasureEncounter(), 2, false),
                Is.SameAs(CoreBattleBalance.ThirdTreasureBoss));
            Assert.That(CoreBattleBalance.Resolve(null, -1).EnemyHull, Is.GreaterThan(0));
        }

        private static CoreVoyageSession Voyage(int route, string seed)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[route].UnlockLevel) });
            session.SelectRoute(route);
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(route));
            session.SetSeed(seed);
            session.StartVoyage();
            return session;
        }

        private static bool IsOptionalBoss(CoreMapNode node) => node.Encounter.BossKind == CoreBossKind.Treasure || node.Encounter.BossKind == CoreBossKind.Legendary;
        private static float Distance(VoyagePoint a, VoyagePoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        private static void Set(CoreVoyageSession session, string property, object value) =>
            typeof(CoreVoyageSession).GetProperty(property).SetValue(session, value);
        private static void Invoke(CoreVoyageSession session, string method) =>
            typeof(CoreVoyageSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, null);
        private static void EnterEncounter(CoreVoyageSession session, CoreMapNode node)
        {
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, node);
            Set(session, "ActiveEncounter", node.Encounter);
            Set(session, "Phase", CoreDemoPhase.Encounter);
        }

        private static HashSet<int> ReachableCells(CoreVoyageMap map)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            int start = map.CellIndex(map.Departure);
            visited.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                int x = cell % CoreVoyageMap.GridColumns, y = cell / CoreVoyageMap.GridColumns;
                int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + dx[d], ny = y + dy[d];
                    if (!map.IsPassable(nx, ny)) continue;
                    int next = map.CellIndex(nx, ny);
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited;
        }
    }
}
