using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageExplorationTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(0, "event_reef_broken_mast", "event_reef_hidden_cache", "reef_dock_chart")]
        [TestCase(1, "common_beach_castaway", "gold_wreck", "trade_rescue_seal")]
        [TestCase(2, "common_strait_beacon", "moon_obelisk", "moon_star_chart")]
        public void AuthoredDiscoveryChain_AlwaysExistsAndPaysItsDistinctFinalReward(
            int route, string sourceId, string targetId, string clue)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
            session.SelectRoute(route);
            session.SetSeed("EXPLORATION-CHAIN-20261015-" + route);
            session.Cargo.Cloth = 2;
            session.Cargo.AncientMaterial = route == 2 ? 1 : 0;
            session.StartVoyage();
            CoreMapNode source = session.Map.Nodes.Single(node => node.Encounter.Id == sourceId);
            CoreMapNode target = session.Map.Nodes.Single(node => node.Encounter.Id == targetId);
            int sourceChoice = source.Encounter.Choices.Count - (route == 2 ? 2 : 1);
            int targetChoice = target.Encounter.Choices.Count - (route == 2 ? 2 : 1);
            target.Discovered = target.IntelRevealed = false;
            EnterNode(session, target);
            Assert.That(session.IsChoiceAvailable(targetChoice), Is.False, "The final branch needs its authored clue.");

            EnterNode(session, source);
            Assert.That(session.IsChoiceAvailable(sourceChoice), Is.True);
            Assert.That(session.ResolveChoice(sourceChoice), Is.True);
            Assert.That(session.HasDiscovery(clue), Is.True);
            Assert.That(target.IsChartVisible, Is.True);
            Assert.That(target.Visited, Is.False);
            EnterNode(session, target);
            Assert.That(session.IsChoiceAvailable(targetChoice), Is.True);
            int ancientBefore = session.Cargo.AncientMaterial;
            int gemsBefore = session.Cargo.Gem;
            int relicsBefore = session.Cargo.Relic;
            Assert.That(session.ResolveChoice(targetChoice), Is.True);
            Assert.That(target.Visited, Is.True);
            Assert.That(session.ExplorationStatusSummary, Does.Contain(target.Encounter.Name + "（已探索）"));
            if (route == 0)
            {
                Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(ancientBefore + 2));
                Assert.That(session.SailingBoostHoursRemaining, Is.GreaterThan(0));
            }
            else if (route == 1) Assert.That(session.Cargo.Gem, Is.EqualTo(gemsBefore + 1));
            else
            {
                Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(ancientBefore - 1));
                Assert.That(session.Cargo.Relic, Is.EqualTo(relicsBefore + 1));
                Assert.That(session.SurveyBoostHoursRemaining, Is.GreaterThan(0));
            }
            CoreEncounter whales = session.Map.Nodes.Single(node => node.Encounter.Id == "common_whale_song").Encounter;
            Assert.That(whales.Choices.Any(choice => choice.Effect.SailingBoostHours > 0), Is.True);
            Assert.That(whales.Choices.Any(choice => choice.Effect.SurveyBoostHours > 0), Is.True);
        }

        [Test]
        public void DiscoverySummary_OnlyMarksExploredWhenAllMatchingLocationsWereVisited()
        {
            CoreVoyageSession session = Ready();
            Resolve(session, new VoyageEffect { GrantDiscovery = "reef_dock_chart" });
            CoreMapNode target = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_hidden_cache");
            Assert.That(session.ExplorationStatusSummary, Does.Not.Contain("已探索"));
            target.Visited = true;
            Assert.That(session.ExplorationStatusSummary, Does.Contain("潮痕秘藏（已探索）"));
            var another = new CoreMapNode { Id = 990, Encounter = target.Encounter, Position = target.Position };
            session.Map.Nodes.Add(another);
            Assert.That(session.ExplorationStatusSummary, Does.Not.Contain("已探索"), "An unvisited matching location must retain the destination cue.");
            session.Map.Nodes.Remove(target);
            session.Map.Nodes.Remove(another);
            Assert.That(session.ExplorationStatusSummary, Does.Contain("潮痕秘藏（本海图无对应地点）"));
            Assert.That(session.ExplorationStatusSummary, Does.Not.Contain("已探索"), "Absent target is not a completed objective.");
        }

        [Test]
        public void RegionalRandomEvents_PresentAtLeastSevenDifferentActivityTypes()
        {
            var categories = CoreVoyageSession.Routes.SelectMany(route => CoreVoyageEventCatalog.RandomEventIds(route.Id))
                .Select(id => CoreVoyageEventCatalog.CreateRandomEncounter(id, 95).Category).Distinct().ToArray();
            Assert.That(categories.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(categories, Does.Not.Contain("随机航行事件"));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void FreshRandomBag_SpacesDifferentActivitiesWithoutDroppingAnEvent(int route)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
            string chart = CoreVoyageSession.CreateChartSeed(route, "断锚港", "白贝港");
            Assert.That(session.TrySelectChartSeed(chart, out string reason), Is.True, reason);
            session.StartVoyage();
            string[] ids = session.CurrentVoyageRandomEventIds.ToArray();
            CollectionAssert.AreEquivalent(CoreVoyageEventCatalog.RandomEventIds(CoreVoyageSession.Routes[route].Id)
                .Concat(new[] { "random_tailwind", "random_floating_supplies" }), ids);
            string[] categories = ids.Select(RandomCategory).ToArray();
            for (int index = 1; index < categories.Length; index++)
                Assert.That(categories[index], Is.Not.EqualTo(categories[index - 1]),
                    ids[index - 1] + " should not be immediately followed by the same activity type: " + ids[index]);
        }

        [TestCase(12d)]
        [TestCase(.5d)]
        public void SailingWindow_ChangesActualTravelTimeAndExpiresAtItsTrueBoundary(double duration)
        {
            CoreVoyageSession session = Ready();
            OpenSea(session);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(80);
            SailingForecast ordinary = session.GetForecast();
            Assert.That(ordinary.Blocked, Is.False);
            Assert.That(ordinary.Hours, Is.GreaterThan(.625d));

            Resolve(session, new VoyageEffect { SailingBoostHours = duration });
            SailingForecast boosted = session.GetForecast();
            double expectedHours = ordinary.Hours <= duration * 1.25d
                ? ordinary.Hours / 1.25d : ordinary.Hours - duration * .25d;
            Assert.That(boosted.ActualDistanceNm, Is.EqualTo(ordinary.ActualDistanceNm).Within(.001));
            Assert.That(boosted.Hours, Is.EqualTo(expectedHours).Within(.0001),
                "Only the hours inside the current window earn the 25% speed bonus.");
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.TotalHours, Is.EqualTo(expectedHours).Within(.0001));
            Assert.That(session.TotalDistanceNm, Is.EqualTo(boosted.ActualDistanceNm).Within(.001));
            Assert.That(session.SailingBoostHoursRemaining,
                Is.EqualTo(Math.Max(0d, duration - expectedHours)).Within(.0001));
        }

        [Test]
        public void SurveyWindow_RevealsMoreSeaWithoutTurningHiddenLocationsIntoSightedLocations()
        {
            CoreVoyageSession session = Ready();
            OpenSea(session, preserveNodes: true);
            CoreMapNode hidden = session.Map.Nodes.First(node => node.Encounter.CanRevealByIntel);
            hidden.Position = new VoyagePoint(session.Position.X + 1f, session.Position.Y);
            hidden.Discovered = hidden.IntelRevealed = false;
            ClearExploration(session);
            Invoke(session, "RevealAt", session.Position);
            bool[] ordinary = (bool[])session.Map.ChartExploredCells.Clone();
            int ordinaryCount = ordinary.Count(value => value);

            Resolve(session, new VoyageEffect { SurveyBoostHours = 6d });
            Invoke(session, "RevealAt", session.Position);
            Assert.That(session.Map.ChartExploredCells.Count(value => value), Is.GreaterThan(ordinaryCount));
            Assert.That(hidden.IsChartVisible, Is.False, "A wider lookout must not bypass the clue mechanic.");
            for (int index = 0; index < ordinary.Length; index++)
                if (ordinary[index]) Assert.That(session.Map.ChartExploredCells[index], Is.True);

            Resolve(session, new VoyageEffect(), 6d);
            Assert.That(session.SurveyBoostHoursRemaining, Is.Zero);
            ClearExploration(session);
            Invoke(session, "RevealAt", session.Position);
            CollectionAssert.AreEqual(ordinary, session.Map.ChartExploredCells,
                "After the window expires, observation uses the original radius.");
        }

        [Test]
        public void SurveyWindow_ExpiresAlongTheRealSailingPathInsteadOfCoveringTheWholeLeg()
        {
            CoreVoyageSession[] sessions = { Ready(), Ready(), Ready() };
            double[] windows = { 0d, .5d, 12d };
            for (int index = 0; index < sessions.Length; index++)
            {
                CoreVoyageSession session = sessions[index];
                OpenSea(session);
                ClearExploration(session);
                session.SetHeading(SailingDirection.East);
                session.SetDistance(160);
                if (windows[index] > 0d) Resolve(session, new VoyageEffect { SurveyBoostHours = windows[index] });
                Assert.That(session.Sail(), Is.True);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            }
            bool[] ordinary = sessions[0].Map.ChartExploredCells;
            bool[] shortWindow = sessions[1].Map.ChartExploredCells;
            bool[] fullWindow = sessions[2].Map.ChartExploredCells;
            Assert.That(shortWindow.Count(value => value), Is.GreaterThan(ordinary.Count(value => value)),
                "The short window must still reveal its wider observation area near departure.");
            Assert.That(shortWindow.Count(value => value), Is.LessThan(fullWindow.Count(value => value)),
                "The early bonus must not be applied to the later part of the sailing path.");
            Assert.That(sessions[1].SurveyBoostHoursRemaining, Is.Zero);
            CoreVoyageSession full = sessions[2];
            float ordinaryRadius = (120f + (full.HasNavigator ? 60f : 0f) + full.VisionBonusNm) / full.Map.Route.NauticalMilesPerMapUnit;
            Assert.That(Enumerable.Range(0, fullWindow.Length).Any(index =>
            {
                VoyagePoint point = full.Map.ChartCellCenter(index % full.Map.FineColumns, index / full.Map.FineColumns);
                return point.X >= full.Position.X - 1f && Math.Abs(point.Y - full.Position.Y) > ordinaryRadius + .5f &&
                    fullWindow[index] && !shortWindow[index] && !ordinary[index];
            }), Is.True, "Only the full-duration lookout sees the far lateral area near the end of the leg.");
        }

        [Test]
        public void Windows_RefreshWithoutStackingAndWaitingConsumesTheExistingWindow()
        {
            CoreVoyageSession session = Ready();
            Resolve(session, new VoyageEffect { SailingBoostHours = 8d, SurveyBoostHours = 12d });
            Enter(session, new CoreEncounterChoice("重复购买短期增益", "无新增收益", 0,
                new VoyageEffect { Gold = -1, SailingBoostHours = 3d, SurveyBoostHours = 4d }));
            Assert.That(session.IsChoiceAvailable(0), Is.False, "A shorter window alone must not take another payment.");
            Resolve(session, new VoyageEffect { Gold = 1, SailingBoostHours = 3d, SurveyBoostHours = 4d });
            Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(8d));
            Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(12d));
            Resolve(session, new VoyageEffect { SailingBoostHours = 5d }, 4d);
            Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(5d), "The new window starts after the event's waiting cost.");
            Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(8d));
            Resolve(session, new VoyageEffect { SailingBoostHours = 200d, SurveyBoostHours = 200d });
            Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(24d));
            Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(24d));
            Resolve(session, new VoyageEffect(), 24d);
            Assert.That(session.SailingBoostHoursRemaining, Is.Zero);
            Assert.That(session.SurveyBoostHoursRemaining, Is.Zero);
        }

        [TestCase("reef_dock_chart")]
        [TestCase("trade_rescue_seal")]
        [TestCase("moon_star_chart")]
        public void Discovery_UnlocksItsChoiceWithoutUnlockingUnrelatedRequirements(string key)
        {
            CoreVoyageSession session = Ready();
            var gated = new CoreEncounterChoice("按线索深入", "金币 +11", 0,
                new VoyageEffect { Gold = 11 }, requiredDiscovery: key);
            Enter(session, gated);
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.EncounterChoiceConditionReason(gated), Is.Not.Empty);
            Assert.That(session.EncounterChoiceConditionSummary(gated), Is.Not.Empty);

            Resolve(session, new VoyageEffect { GrantDiscovery = key });
            Assert.That(session.HasDiscovery(key), Is.True);
            Assert.That(session.HasDiscovery("unknown_discovery"), Is.False);
            Enter(session, gated);
            int before = session.Cargo.Gold;
            Assert.That(session.IsChoiceAvailable(0), Is.True);
            Assert.That(session.EncounterChoiceConditionReason(gated), Is.Empty);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Cargo.Gold, Is.EqualTo(before + 11));
        }

        [Test]
        public void DirectedIntel_RevealsTheNamedDestinationInsteadOfTheNearestHiddenEvent()
        {
            CoreVoyageSession session = Ready();
            CoreMapNode target = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_hidden_cache");
            CoreMapNode nearer = session.Map.Nodes.First(node => node != target && node.Encounter.CanRevealByIntel);
            foreach (CoreMapNode node in session.Map.Nodes) node.Discovered = node.IntelRevealed = false;
            target.Position = new VoyagePoint(85f, 15f);
            nearer.Position = new VoyagePoint(session.Position.X + .5f, session.Position.Y);
            Resolve(session, new VoyageEffect { GrantDiscovery = "reef_dock_chart", RevealEncounterId = target.Encounter.Id });

            Assert.That(target.IsChartVisible, Is.True);
            Assert.That(target.Visited, Is.False, "Finding the coordinates must still require travelling to the location.");
            Assert.That(nearer.IsChartVisible, Is.False);
            Assert.That(session.HasDiscovery("reef_dock_chart"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PureClueService_KnownOrExhaustedIntelCannotTakeAnotherPayment(bool exhausted)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TrySelectChartSeed(CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港"), out string reason), Is.True, reason);
            session.StartVoyage();
            CoreMapNode source = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_broken_mast");
            CoreMapNode target = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_hidden_cache");
            target.Discovered = target.IntelRevealed = false;
            if (exhausted)
                typeof(CoreVoyageSession).GetMethod("SetSeaChartClaimCount", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { typeof(CoreVoyageSession).GetProperty("ActiveSeaChartState", Hidden).GetValue(session), source.Encounter.Id, 3 });
            else Resolve(session, new VoyageEffect { GrantDiscovery = "reef_dock_chart", RevealEncounterId = target.Encounter.Id });
            EnterNode(session, source);
            var choice = new CoreEncounterChoice("付费购买线索", "金币 -10", 1d,
                new VoyageEffect { Gold = -10, GrantDiscovery = "reef_dock_chart", RevealEncounterId = target.Encounter.Id });
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session,
                new CoreEncounter(source.Encounter.Id, "线索服务", "情报", "纯情报交易", false, false, 1, 0, choice));
            int goldBefore = session.Cargo.Gold;
            double hoursBefore = session.TotalHours;
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(goldBefore));
            Assert.That(session.TotalHours, Is.EqualTo(hoursBefore));
        }

        [Test]
        public void BattleExplorationRewards_AreGrantedOnlyAfterVictory()
        {
            CoreVoyageSession session = Ready();
            var effect = new VoyageEffect
            {
                SailingBoostHours = 5d,
                SurveyBoostHours = 7d,
                GrantDiscovery = "trade_rescue_seal",
                RevealEncounterId = "event_reef_hidden_cache",
            };
            CoreMapNode target = session.Map.Nodes.Single(node => node.Encounter.Id == effect.RevealEncounterId);
            target.Discovered = target.IntelRevealed = false;
            var choice = new CoreEncounterChoice("护航迎战", "胜利后获得航路援助", 1d, effect, startsBattle: true);
            Enter(session, choice);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session,
                new CoreEncounter("event_reef_coral_ambush", "护航迎战", "战斗", "战胜后获取航路援助", true, false, 3, 95, choice));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.SailingBoostHoursRemaining, Is.Zero);
            Assert.That(session.SurveyBoostHoursRemaining, Is.Zero);
            Assert.That(session.HasDiscovery("trade_rescue_seal"), Is.False);
            Assert.That(target.IsChartVisible, Is.False);

            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(5d));
            Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(7d));
            Assert.That(session.HasDiscovery("trade_rescue_seal"), Is.True);
            Assert.That(target.IsChartVisible, Is.True);
        }

        [Test]
        public void Checkpoint_PreservesWindowsCluesAndTheirContinuedExpiry()
        {
            CoreVoyageSession source = Ready();
            Resolve(source, new VoyageEffect { SailingBoostHours = 9d, SurveyBoostHours = 13d, GrantDiscovery = "moon_star_chart" });
            Resolve(source, new VoyageEffect(), 1.25d);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.SailingBoostHoursRemaining, Is.EqualTo(7.75d));
            Assert.That(restored.SurveyBoostHoursRemaining, Is.EqualTo(11.75d));
            Assert.That(restored.HasDiscovery("moon_star_chart"), Is.True);
            Assert.That(restored.ExplorationStatusSummary, Is.EqualTo(source.ExplorationStatusSummary));
            Assert.That(restored.GetForecast().Hours, Is.EqualTo(source.GetForecast().Hours).Within(.0001));
            Resolve(source, new VoyageEffect(), 2d);
            Resolve(restored, new VoyageEffect(), 2d);
            Assert.That(restored.SailingBoostHoursRemaining, Is.EqualTo(source.SailingBoostHoursRemaining));
            Assert.That(restored.SurveyBoostHoursRemaining, Is.EqualTo(source.SurveyBoostHoursRemaining));
            Assert.That(restored.Cargo.Food, Is.EqualTo(source.Cargo.Food));
        }

        [TestCase("sailingBoostHoursRemaining", double.NaN)]
        [TestCase("sailingBoostHoursRemaining", double.PositiveInfinity)]
        [TestCase("sailingBoostHoursRemaining", -1d)]
        [TestCase("surveyBoostHoursRemaining", 24.01d)]
        public void Checkpoint_RejectsInvalidWindowsWithoutMutatingTheActiveVoyage(string field, double value)
        {
            CoreVoyageSession session = Ready();
            Resolve(session, new VoyageEffect { SailingBoostHours = 8d, GrantDiscovery = "trade_rescue_seal" });
            CoreVoyageCheckpoint invalid = session.CaptureVoyageCheckpoint();
            typeof(CoreVoyageCheckpoint).GetField(field).SetValue(invalid, value);
            Assert.That(session.TryRestoreVoyageCheckpoint(invalid, out _), Is.False);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(8d));
            Assert.That(session.HasDiscovery("trade_rescue_seal"), Is.True);
        }

        [Test]
        public void Checkpoint_RejectsUnknownCluesAndAcceptsLegacyMissingExplorationState()
        {
            CoreVoyageSession source = Ready();
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            FieldInfo discoveries = typeof(CoreVoyageCheckpoint).GetField("explorationDiscoveries");
            discoveries.SetValue(checkpoint, new[] { "unknown_discovery" });
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            discoveries.SetValue(checkpoint, null);
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.SailingBoostHoursRemaining, Is.Zero);
            Assert.That(restored.SurveyBoostHoursRemaining, Is.Zero);
            Assert.That(restored.HasDiscovery("reef_dock_chart"), Is.False);
        }

        [Test]
        public void ReturnAndNextDeparture_ClearTemporaryDiscoveriesAndWindows()
        {
            CoreVoyageSession session = Ready();
            Resolve(session, new VoyageEffect { SailingBoostHours = 8d, SurveyBoostHours = 10d, GrantDiscovery = "reef_dock_chart" });
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.SailingBoostHoursRemaining, Is.Zero);
            Assert.That(session.SurveyBoostHoursRemaining, Is.Zero);
            Assert.That(session.HasDiscovery("reef_dock_chart"), Is.False);
            session.StartVoyage();
            Assert.That(session.SailingBoostHoursRemaining, Is.Zero);
            Assert.That(session.SurveyBoostHoursRemaining, Is.Zero);
            Assert.That(session.HasDiscovery("reef_dock_chart"), Is.False);
        }

        private static CoreVoyageSession Ready()
        {
            var session = new CoreVoyageSession();
            session.SetSeed("EXPLORATION-MECHANICS-20261015");
            session.StartVoyage();
            return session;
        }

        private static void Enter(CoreVoyageSession session, params CoreEncounterChoice[] choices)
        {
            typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(session, null);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session,
                new CoreEncounter("exploration_test", "探索机制测试", "测试", "受控事件", false, false, 1, 0, choices));
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
        }

        private static void EnterNode(CoreVoyageSession session, CoreMapNode node)
        {
            typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, node.Position);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
        }

        private static string RandomCategory(string id) => id == "random_tailwind" ? "航路机遇" :
            id == "random_floating_supplies" ? "海上采集" : CoreVoyageEventCatalog.CreateRandomEncounter(id, 95).Category;

        private static void Resolve(CoreVoyageSession session, VoyageEffect effect, double hours = 0d)
        {
            Enter(session, new CoreEncounterChoice("执行测试事件", "测试效果", hours, effect));
            Assert.That(session.IsChoiceAvailable(0), Is.True, "Test setup must fund its actual supply and cargo costs.");
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        // A controlled open sea isolates speed and observation from random land
        // placement. Both tests still execute production forecasts and effects.
        private static void OpenSea(CoreVoyageSession session, bool preserveNodes = false)
        {
            for (int index = 0; index < session.Map.CellCount; index++)
            {
                session.Map.TerrainCells[index] = SeaTerrainType.OpenWater;
                session.Map.CurrentStrengths[index] = 0f;
            }
            session.Map.GlobalWindStrength = 0f;
            if (!preserveNodes) session.Map.Nodes.Clear();
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(50f, 32f));
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", Hidden).SetValue(session, true);
        }

        private static void ClearExploration(CoreVoyageSession session)
        {
            Array.Clear(session.Map.ExploredCells, 0, session.Map.ExploredCells.Length);
            Array.Clear(session.Map.VoyageExploredCells, 0, session.Map.VoyageExploredCells.Length);
            Array.Clear(session.Map.ChartExploredCells, 0, session.Map.ChartExploredCells.Length);
        }

        private static object Invoke(CoreVoyageSession session, string method, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(method, Hidden).Invoke(session, args);
    }
}
