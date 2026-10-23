using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSeaChartProgressTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void StructuredSeedsCarryTheirRouteAndCanonicalPortsAcrossEveryPortPair(int route)
        {
            foreach (string from in CoreVoyageSession.DestinationPorts)
            foreach (string to in CoreVoyageSession.DestinationPorts.Where(port => port != from))
            {
                string seed = CoreVoyageSession.CreateChartSeed(route, from, to);
                Assert.That(CoreVoyageSession.TryParseChartSeed(" " + seed.ToLowerInvariant() + " ", out CoreSeaChartDefinition chart), Is.True);
                Assert.That(chart.Seed, Is.EqualTo(seed));
                Assert.That(chart.RouteIndex, Is.EqualTo(route));
                Assert.That(chart.ForwardDeparturePort, Is.EqualTo(from));
                Assert.That(chart.ForwardDestinationPort, Is.EqualTo(to));
                Assert.That(chart.OtherPort(from), Is.EqualTo(to));
                Assert.That(chart.OtherPort(to), Is.EqualTo(from));
                Assert.That(chart.TutorialWaters, Is.False);
            }
        }

        [TestCase(null)] [TestCase("")] [TestCase("RAW-UNREGISTERED")]
        [TestCase("SS2.0.0.1.0.0.00000000.00000000.00000000")]
        [TestCase("SS2.0.0.0.0.0.00000000.00000000.00000000")]
        public void InvalidSeedsCannotChangeTheSelectedChartOrAssets(string bad)
        {
            CoreVoyageSession session = ChartReady();
            string original = session.SelectedSeed;
            decimal gold = session.ProfileGold;
            Assert.That(session.TrySelectChartSeed(bad, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(session.SelectedSeed, Is.EqualTo(original));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.CaptureSeaChartProgress().charts, Is.Empty);
        }

        [Test]
        public void UnknownButValidSharedSeedWorksAtBothEndpointsAndRejectsTheThirdPort()
        {
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            CoreVoyageSession forward = ChartReady(seed: seed); forward.StartVoyage();
            CoreVoyageSession reverse = ChartReady(seed: seed, port: "白贝港"); reverse.StartVoyage();
            Assert.That(reverse.Map.Hash, Is.EqualTo(forward.Map.Hash));
            Assert.That(reverse.Map.Departure, Is.EqualTo(forward.Map.Destination));
            Assert.That(reverse.Map.Destination, Is.EqualTo(forward.Map.Departure));
            Assert.That(reverse.Map.DepartureName, Is.EqualTo("白贝港"));
            Assert.That(reverse.Map.DestinationName, Is.EqualTo("断锚港"));
            CollectionAssert.AreEqual(forward.Map.TerrainCells, reverse.Map.TerrainCells);
            CollectionAssert.AreEqual(forward.Map.CurrentVectors, reverse.Map.CurrentVectors);
            CollectionAssert.AreEqual(forward.Map.Nodes.Select(node => node.Position), reverse.Map.Nodes.Select(node => node.Position));
            var elsewhere = new CoreVoyageSession(); elsewhere.RestoreProfile(500, "铜钟港");
            Assert.That(elsewhere.TrySelectChartSeed(seed, out _), Is.False);
            elsewhere.SetSeed(seed);
            Assert.Throws<InvalidOperationException>(() => elsewhere.StartVoyage());
            Assert.That(elsewhere.ProfileGold, Is.EqualTo(500));
            Assert.That(elsewhere.Map, Is.Null);
        }

        [Test]
        public void SelectedChartPreviewMatchesDepartureWithoutStartingOrSpending()
        {
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            foreach (string port in new[] { "断锚港", "白贝港" })
            {
                CoreVoyageSession session = ChartReady(seed: seed, port: port);
                decimal gold = session.ProfileGold;
                int cargoGold = session.Cargo.Gold, water = session.Cargo.Water, food = session.Cargo.Food;
                int revision = session.SeaChartProgressRevision;
                CoreSeaChartDefinition selection = session.SelectedChartDefinition;
                CoreVoyageMap preview = session.PreviewSelectedSeaChart();

                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.DepartureName, Is.EqualTo(port));
                Assert.That(preview.DestinationName, Is.EqualTo(selection.OtherPort(port)));
                Assert.That(session.Map, Is.Null);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
                Assert.That(session.Cargo.Water, Is.EqualTo(water));
                Assert.That(session.Cargo.Food, Is.EqualTo(food));
                Assert.That(session.SeaChartProgressRevision, Is.EqualTo(revision));
                Assert.That(session.CaptureSeaChartProgress().charts, Is.Empty);
                Assert.That(session.SelectedChartDefinition, Is.SameAs(selection));

                session.StartVoyage();
                Assert.That(session.Map.Hash, Is.EqualTo(preview.Hash));
                Assert.That(session.Map.Departure, Is.EqualTo(preview.Departure));
                Assert.That(session.Map.Destination, Is.EqualTo(preview.Destination));
                CollectionAssert.AreEqual(session.Map.TerrainCells, preview.TerrainCells);
                CollectionAssert.AreEqual(session.Map.CurrentVectors, preview.CurrentVectors);
            }
        }

        [TestCase(1)] [TestCase(2)]
        public void ASharedSeedPreservesTheExistingOpenRouteSelection(int route)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            string seed = CoreVoyageSession.CreateChartSeed(route, "断锚港", "白贝港");
            Assert.That(session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        [Test]
        public void SelectedChartRejectsADifferentDestinationBeforeAnyDeparturePayment()
        {
            CoreVoyageSession session = ChartReady();
            decimal gold = session.ProfileGold;
            int aboard = session.Cargo.Gold;
            Assert.That(session.SelectDestinationPort("铜钟港"), Is.True);
            Assert.That(session.CanStartSelectedChart(out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => session.StartVoyage("铜钟港"));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(aboard));
            Assert.That(session.CaptureSeaChartProgress().charts, Is.Empty);
        }

        [Test]
        public void RepeatedEventUsesHundredFiftyTwentyFiveZeroAfterCrewBonuses()
        {
            CoreVoyageSession session = ChartReady();
            session.ConfigureCrewCapabilities(false, false, false, false, false, true);
            int[] expected = { 15, 7, 3, 0 };
            double[] factors = { 1d, .5d, .25d, 0d };
            for (int visit = 0; visit < expected.Length; visit++)
            {
                Refill(session); session.StartVoyage();
                Enter(session, "event_reef_sunken_boat");
                Assert.That(session.ActiveEncounterClaimCount, Is.EqualTo(visit));
                Assert.That(session.ActiveEncounterRewardMultiplier, Is.EqualTo(factors[visit]));
                int wood = session.Cargo.Wood;
                double hours = session.TotalHours;
                Assert.That(session.ResolveChoice(0), Is.EqualTo(expected[visit] > 0));
                Assert.That(session.Cargo.Wood - wood, Is.EqualTo(expected[visit]));
                Assert.That(session.TotalHours - hours, Is.EqualTo(expected[visit] > 0 ? 3d : 0d),
                    "A diminished reward pays full time; an exhausted reward cannot charge time.");
                Assert.That(session.ResolveChoice(0), Is.False, "A stale confirmation cannot claim again.");
                if (expected[visit] == 0) Assert.That(session.SkipEncounter(), Is.True);
                Return(session);
            }
            Assert.That(session.CaptureSeaChartProgress().charts.Single().events.Single(item => item.eventId == "event_reef_sunken_boat").count, Is.EqualTo(3));
        }

        [Test]
        public void NegativeResourceCostsStayFullWhileAlternativeChoicesShareTheClaimHistory()
        {
            CoreVoyageSession session = ChartReady();
            // Opening the stone chest requires a salvage ship. Arrange that
            // eligibility before isolating repeat rewards and resource costs.
            int salvage = Enumerable.Range(0, CoreVoyageSession.Ships.Count)
                .Single(index => CoreVoyageSession.Ships[index].Id == "salvage");
            Assert.That(session.SelectShip(salvage), Is.True);
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.StartVoyage(); Enter(session, "event_reef_hidden_cache");
            Assert.That(session.ResolveChoice(0), Is.True); Return(session);
            Refill(session); session.Cargo.Wood = 10; session.StartVoyage(); Enter(session, "event_reef_hidden_cache");
            Assert.That(session.ActiveEncounterClaimCount, Is.EqualTo(1));
            VoyageEffect effective = session.EffectiveChoiceEffect(1);
            Assert.That(effective.Wood, Is.EqualTo(-6));
            Assert.That(effective.Gold, Is.EqualTo(60));
            int gold = session.Cargo.Gold;
            Assert.That(session.ResolveChoice(1), Is.True);
            Assert.That(session.Cargo.Wood, Is.EqualTo(4));
            Assert.That(session.Cargo.Gold - gold, Is.EqualTo(60));
        }

        [TestCase("common_floating_market", 0)]
        [TestCase("event_reef_fisher_cache", 0)]
        [TestCase("event_reef_saltwater_leak", 0)]
        public void ExplicitRenewableTradesAndRepairsKeepTheirQuotedEffects(string id, int choice)
        {
            CoreVoyageSession session = ChartReady();
            for (int visit = 0; visit < 4; visit++)
            {
                Refill(session); session.Cargo.Wood = 8; session.StartVoyage(); Enter(session, id);
                Assert.That(session.ActiveEncounter.Choices[choice].Renewable, Is.True);
                VoyageEffect source = session.ActiveEncounter.Choices[choice].Effect, effective = session.EffectiveChoiceEffect(choice);
                Assert.That(effective.Gold, Is.EqualTo(source.Gold));
                Assert.That(effective.Food, Is.EqualTo(source.Food));
                Assert.That(effective.Hull, Is.EqualTo(source.Hull));
                Assert.That(session.ResolveChoice(choice), Is.True); Return(session);
            }
            Assert.That(session.CaptureSeaChartProgress().charts.Single().events.Any(item => item.eventId == id), Is.False);
        }

        [Test]
        public void DecliningUnaffordableChoicesAndSkippedEventsDoNotClaimRewards()
        {
            CoreVoyageSession session = ChartReady(); session.StartVoyage();
            Enter(session, "event_reef_hidden_cache"); session.Cargo.Wood = 0;
            Assert.That(session.ResolveChoice(1), Is.False);
            Assert.That(session.SkipEncounter(), Is.True);
            Assert.That(session.CaptureSeaChartProgress().charts.Single().events, Is.Empty);
        }

        [TestCase(false)] [TestCase(true)]
        public void RepeatedOrdinaryBattleScalesBaseLootAndDelayedRewardTogether(bool captured)
        {
            CoreVoyageSession session = ChartReady(route: 1);
            for (int visit = 0; visit < 4; visit++)
            {
                Refill(session); session.StartVoyage(); Enter(session, "gold_customs");
                Assert.That(session.ResolveChoice(1), Is.True);
                int before = session.Cargo.Gold;
                Win(session, captured);
                // 115, 57.5 and 28.75 all pay the positive minimum of 100;
                // the fourth claim is zero before rounding. Capture doubles last.
                int expected = visit < 3 ? (captured ? 200 : 100) : 0;
                Assert.That(session.Cargo.Gold - before, Is.EqualTo(expected));
                Assert.That(session.CaptureSeaChartProgress().charts.Single().events.Single(item => item.eventId == "gold_customs").count, Is.EqualTo(visit + 1));
                Return(session);
            }
        }

        [Test]
        public void RetreatedBattleDoesNotClaimThePendingReward()
        {
            CoreVoyageSession session = ChartReady(route: 1); session.StartVoyage(); Enter(session, "gold_customs");
            Assert.That(session.ResolveChoice(1), Is.True);
            Assert.That(session.RetreatFromBattle(), Is.True);
            Assert.That(session.CaptureSeaChartProgress().charts.Single().events, Is.Empty);
        }

        [Test]
        public void DefeatedRouteBossStaysGoneThisVoyageButNextVoyageSelectsANewGuardian()
        {
            CoreVoyageSession session = ChartReady(); string seed = session.SelectedSeed;
            session.StartVoyage();
            SeaTerrainType[] terrain = (SeaTerrainType[])session.Map.TerrainCells.Clone();
            int totalEvents = session.TotalEventCount();
            Enter(session, session.CurrentRouteBossId); Assert.That(session.ResolveChoice(0), Is.True); Win(session, false);
            Assert.That(session.BossDefeated, Is.True);
            Assert.That(session.Map.Nodes.Any(node => node.IsBoss), Is.False);
            Assert.That(session.CompletedEventCount(), Is.EqualTo(1));
            Assert.That(session.TotalEventCount(), Is.EqualTo(totalEvents));
            CollectionAssert.AreEqual(terrain, session.Map.TerrainCells);
            session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.CompletedEvents, Is.EqualTo(1));
            Assert.That(session.Settlement.TotalEvents, Is.EqualTo(totalEvents));
            CoreSeaChartProgress saved = session.CaptureSeaChartProgress();
            var restored = ChartReady(seed: seed, port: "白贝港");
            Assert.That(restored.RestoreSeaChartProgress(saved), Is.True);
            restored.StartVoyage();
            Assert.That(restored.BossDefeated, Is.True, "Previously opened port access survives another guardian's appearance.");
            Assert.That(restored.CurrentRouteBossDefeated, Is.False);
            Assert.That(restored.Map.Nodes.Count(node => node.IsBoss), Is.EqualTo(1));
            Assert.That(restored.VoyageTreasureFragments, Is.Zero);
            CollectionAssert.AreEqual(terrain, restored.Map.TerrainCells);
        }

        [Test]
        public void OptionalBossRemovalDoesNotUnlockTheRouteOrMoveItsGeography()
        {
            CoreVoyageSession session = ChartReady(route: 2); string seed = session.SelectedSeed;
            session.StartVoyage(); string optionalId = CoreLegendaryWarships.All[0].Id;
            Enter(session, optionalId); Assert.That(session.ResolveChoice(0), Is.True); Win(session, false);
            Assert.That(session.BossDefeated, Is.False);
            Assert.That(session.Map.Nodes.Any(node => node.Encounter.Id == optionalId), Is.False);
            CoreSeaChartProgress saved = session.CaptureSeaChartProgress();
            var restored = ChartReady(seed: seed, route: 2); Assert.That(restored.RestoreSeaChartProgress(saved), Is.True); restored.StartVoyage();
            Assert.That(restored.Map.Nodes.Any(node => node.Encounter.Id == optionalId), Is.False);
            Assert.That(restored.Map.Nodes.Any(node => node.IsBoss), Is.True);
            Assert.That(restored.BossDefeated, Is.False);
        }

        [Test]
        public void TutorialChartReplaysTheSameShortWatersWithoutTutorialOrARespawnedBoss()
        {
            var tutorial = new CoreVoyageSession(); Refill(tutorial);
            tutorial.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg); tutorial.StartVoyage();
            string seed = tutorial.CurrentChartSeed; Assert.That(CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition definition), Is.True);
            Assert.That(definition.TutorialWaters, Is.True);
            SeaTerrainType[] terrain = (SeaTerrainType[])tutorial.Map.TerrainCells.Clone();
            Enter(tutorial, CoreGuidedTutorialContent.BossEventId); Assert.That(tutorial.ResolveChoice(0), Is.True); Win(tutorial, false);
            Assert.That(tutorial.VoyageTreasureFragments, Is.EqualTo(1));
            Assert.That(tutorial.Map.Nodes, Is.Empty);
            Assert.That(tutorial.CompletedEventCount(), Is.EqualTo(1));
            Assert.That(tutorial.TotalEventCount(), Is.EqualTo(1));
            var replay = ChartReady(seed: seed, port: "白贝港");
            Assert.That(replay.RestoreSeaChartProgress(tutorial.CaptureSeaChartProgress()), Is.True);
            replay.StartVoyage();
            Assert.That(replay.GuidedTutorialLeg, Is.Zero);
            Assert.That(replay.Map.Route.NauticalMilesPerMapUnit, Is.EqualTo(10f));
            Assert.That(replay.BossDefeated, Is.True);
            Assert.That(replay.Map.Nodes, Is.Empty);
            Assert.That(replay.VoyageTreasureFragments, Is.Zero);
            CollectionAssert.AreEqual(terrain, replay.Map.TerrainCells);
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void InterruptedTutorialVictoryKeepsOnePendingFragmentUntilSettlementCommits(bool closeAtSettlement, bool failedReturn)
        {
            var tutorial = new CoreVoyageSession(); Refill(tutorial);
            tutorial.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 40 });
            tutorial.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg); tutorial.StartVoyage();
            Enter(tutorial, CoreGuidedTutorialContent.BossEventId);
            Assert.That(tutorial.ResolveChoice(0), Is.True); Win(tutorial, false);
            if (closeAtSettlement) Assert.That(tutorial.SafeReturn(), Is.True);
            CoreSeaChartProgress pending = tutorial.CaptureSeaChartProgress();
            Assert.That(pending.charts.Single().pendingTutorialFragments, Is.EqualTo(1));
            Assert.That(tutorial.TreasureFragments, Is.Zero);

            var retry = new CoreVoyageSession(); Refill(retry);
            retry.RestoreTreasureProgress(tutorial.CaptureTreasureProgress());
            Assert.That(retry.RestoreSeaChartProgress(pending), Is.True);
            retry.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg); retry.StartVoyage();
            Assert.That(retry.GuidedTutorialBossDefeated, Is.True);
            Assert.That(retry.Map.Nodes, Is.Empty);
            Assert.That(retry.VoyageTreasureFragments, Is.EqualTo(1));
            Assert.That(retry.CompletedEventCount(), Is.EqualTo(1));
            Assert.That(retry.TotalEventCount(), Is.EqualTo(1));
            if (failedReturn) retry.Cargo.Water = retry.Cargo.Food = 0;
            Assert.That(retry.SafeReturn(), Is.True);
            Assert.That(retry.Settlement.Outcome, Is.EqualTo(failedReturn ? VoyageOutcome.Failure : VoyageOutcome.SafeReturn));
            Assert.That(retry.Settlement.BankedFragments, Is.EqualTo(1));
            Assert.That(retry.Settlement.BankedPirateExperience, Is.EqualTo(80));
            Assert.That(retry.CaptureSeaChartProgress().charts.Single().pendingTutorialFragments, Is.EqualTo(1));
            int revision = retry.SeaChartProgressRevision;
            retry.ReturnToPortAfterSettlement();
            Assert.That(retry.TreasureFragments, Is.EqualTo(1));
            Assert.That(retry.PirateExperience, Is.EqualTo(120));
            Assert.That(retry.CaptureSeaChartProgress().charts.Single().pendingTutorialFragments, Is.Zero);
            Assert.That(retry.SeaChartProgressRevision, Is.GreaterThan(revision));
            retry.ReturnToPortAfterSettlement();
            Assert.That(retry.TreasureFragments, Is.EqualTo(1));

            var committed = new CoreVoyageSession(); Refill(committed);
            committed.RestoreTreasureProgress(retry.CaptureTreasureProgress());
            Assert.That(committed.RestoreSeaChartProgress(retry.CaptureSeaChartProgress()), Is.True);
            committed.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg); committed.StartVoyage();
            Assert.That(committed.Map.Nodes, Is.Empty);
            Assert.That(committed.VoyageTreasureFragments, Is.Zero);
            Assert.That(committed.CompletedEventCount(), Is.Zero);
            Return(committed);
            Assert.That(committed.TreasureFragments, Is.EqualTo(1));
            Assert.That(committed.PirateExperience, Is.EqualTo(120));
        }

        [TestCase(-1, true)] [TestCase(2, true)] [TestCase(1, false)]
        public void InvalidPendingTutorialFragmentRecordsAreRejectedAtomically(int pending, bool defeatedBoss)
        {
            var tutorial = new CoreVoyageSession(); Refill(tutorial);
            tutorial.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg); tutorial.StartVoyage();
            CoreSeaChartProgress save = tutorial.CaptureSeaChartProgress();
            save.charts[0].pendingTutorialFragments = pending;
            if (defeatedBoss) save.charts[0].defeatedBosses = new[] { CoreGuidedTutorialContent.BossEventId };
            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreSeaChartProgress(save), Is.False);
            Assert.That(restored.CaptureSeaChartProgress().charts, Is.Empty);
            Assert.That(restored.SeaChartProgressRevision, Is.Zero);
        }

        [Test]
        public void SameChartKeepsFixedPlacesButChangesTheFirstDynamicTemplateOnEveryReload()
        {
            CoreVoyageSession session = ChartReady(); string previous = null; VoyagePoint[] places = null;
            for (int visit = 0; visit < 8; visit++)
            {
                Refill(session); session.StartVoyage();
                string first = session.CurrentVoyageRandomEventIds[0];
                Assert.That(first, Is.Not.EqualTo(previous));
                Assert.That(session.CurrentVoyageRandomEventIds.Distinct().Count(), Is.EqualTo(session.CurrentVoyageRandomEventIds.Count));
                if (places != null) CollectionAssert.AreEqual(places, session.Map.Nodes.Select(node => node.Position));
                places = session.Map.Nodes.Select(node => node.Position).ToArray(); previous = first;
                Return(session);
                var loaded = ChartReady(seed: session.SelectedSeed);
                Assert.That(loaded.RestoreSeaChartProgress(session.CaptureSeaChartProgress()), Is.True);
                session = loaded;
            }
        }

        [Test]
        public void LegacyDrawImportCarriesCompletedEventsAndBossesWithoutChangingTheOldMap()
        {
            var session = new CoreVoyageSession { MapGeometryVersion = 0 }; Refill(session); session.SetSeed("LEGACY-CHART"); session.StartVoyage();
            uint hash = session.Map.Hash; Assert.That(session.CurrentChartSeed, Is.Empty);
            Enter(session, "event_reef_sunken_boat"); Assert.That(session.ResolveChoice(0), Is.True);
            string defeatedBoss = session.CurrentRouteBossId;
            Enter(session, defeatedBoss); Assert.That(session.ResolveChoice(0), Is.True); Win(session, false);
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.TryImportLegacyChart(session.Map.Seed, 0, session.Map.DepartureName, session.Map.DestinationName,
                hash, false, out string seed), Is.True);
            Assert.That(session.CurrentChartSeed, Is.EqualTo(seed));
            CoreSeaChartState state = session.CaptureSeaChartProgress().charts.Single();
            Assert.That(state.events.Single(item => item.eventId == "event_reef_sunken_boat").count, Is.EqualTo(1));
            Assert.That(state.defeatedBosses, Does.Contain(defeatedBoss));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.TrySelectChartSeed("LEGACY-CHART", out _), Is.True);
            Refill(session); session.StartVoyage();
            Assert.That(session.Map.Hash, Is.EqualTo(hash)); Assert.That(session.BossDefeated, Is.True);
            Assert.That(session.CurrentRouteBossDefeated, Is.False);
            Enter(session, "event_reef_sunken_boat"); Assert.That(session.ActiveEncounterRewardMultiplier, Is.EqualTo(.5d));
        }

        [TestCase("visits")] [TestCase("claim")] [TestCase("unknownEvent")] [TestCase("unknownBoss")]
        [TestCase("duplicateChart")] [TestCase("duplicateEvent")] [TestCase("alias")] [TestCase("pendingNormalChart")]
        public void InvalidProgressRestoreIsAtomicAndCannotDuplicateRewards(string corruption)
        {
            CoreVoyageSession session = ChartReady(); session.StartVoyage(); Enter(session, "event_reef_sunken_boat");
            session.ResolveChoice(0); Return(session);
            CoreSeaChartProgress save = session.CaptureSeaChartProgress();
            int revision = session.SeaChartProgressRevision;
            if (corruption == "visits") save.charts[0].visits = -1;
            if (corruption == "claim") save.charts[0].events[0].count = -1;
            if (corruption == "unknownEvent") save.charts[0].events[0].eventId = "unknown";
            if (corruption == "unknownBoss") save.charts[0].defeatedBosses = new[] { "unknown" };
            if (corruption == "duplicateChart") save.charts = new[] { save.charts[0], save.charts[0] };
            if (corruption == "duplicateEvent") save.charts[0].events = new[] { save.charts[0].events[0], save.charts[0].events[0] };
            if (corruption == "alias") save.aliases = new[] { new CoreSeaChartAlias { rawSeed = "x", canonicalSeed = "bad" } };
            if (corruption == "pendingNormalChart") save.charts[0].pendingTutorialFragments = 1;
            Assert.That(session.RestoreSeaChartProgress(save), Is.False);
            Assert.That(session.SeaChartProgressRevision, Is.EqualTo(revision));
            Assert.That(session.CaptureSeaChartProgress().charts.Single().events.Single().count, Is.EqualTo(1));
        }

        private static CoreVoyageSession ChartReady(int route = 0, string seed = null, string port = "断锚港")
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            session.RestoreProfile(1000, port);
            Refill(session);
            // Reward and claim tests target named legacy locations. SS5 deliberately
            // samples these locations, so use a stable historical chart here.
            if (seed == null)
            {
                string payload = "SS3." + route + ".0.1.0.1." + SeedHash.Compute("PROGRESS-FIXTURE-" + route).ToString("X8") + ".00000000";
                seed = payload + "." + SeedHash.Compute(payload).ToString("X8");
            }
            Assert.That(session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            return session;
        }
        private static void Refill(CoreVoyageSession session)
        {
            foreach (PropertyInfo property in typeof(VoyageCargo).GetProperties().Where(item => item.PropertyType == typeof(int))) property.SetValue(session.Cargo, 0);
            session.Cargo.Water = session.Cargo.Food = 100;
        }
        private static void Return(CoreVoyageSession session)
        {
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
        }
        private static void Enter(CoreVoyageSession session, string id)
        {
            CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id == id);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
        }
        private static void Win(CoreVoyageSession session, bool captured)
        {
            if (session.IsGuidedBossBattle)
            {
                // The current lesson guards its health until the teaching actions
                // finish; play those actions before asserting reward persistence.
                CoreGuidedBossScriptTests.PlayToCompletion(session);
                return;
            }
            if (captured) { session.Battle.Stage = NavalBattleStage.Boarding; session.Battle.EnemyBoardingHealth = 0; }
            else session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }
    }
}
