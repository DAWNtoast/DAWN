using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageCheckpointTests
    {
        [Test]
        public void JsonRoundTripPreservesIndependentFleetBerths()
        {
            var source = new CoreVoyageSession();
            source.RestoreProfile(500, "白贝港");
            Assert.That(source.RestoreShipPortProgress(new CoreShipPortProgress
            {
                ships = new[]
                {
                    new CoreShipPortLocation { shipIndex = 0, port = "断锚港" },
                    new CoreShipPortLocation { shipIndex = 4, port = "白贝港" },
                    new CoreShipPortLocation { shipIndex = 5, port = "铜钟港" },
                },
            }, new[] { 0, 4, 5 }, 4), Is.True);
            Assert.That(source.SelectShip(4), Is.True);
            source.StartVoyage("暮潮港");
            CoreVoyageSession restored = RoundTrip(source);

            Assert.That(restored.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(restored.ShipPort(4), Is.EqualTo("白贝港"));
            Assert.That(restored.ShipPort(5), Is.EqualTo("铜钟港"));
            restored.SafeReturn();
            restored.ReturnToPortAfterSettlement();
            Assert.That(restored.SelectShip(0), Is.False);
            Assert.That(restored.SelectShip(5), Is.False);
            Assert.That(restored.SelectShip(4), Is.True);
        }

        [Test]
        public void LegacyJsonWithoutFleetFieldKeepsProfileBerths()
        {
            var source = new CoreVoyageSession();
            source.SelectShip(4);
            source.StartVoyage("白贝港");
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            string json = JsonUtility.ToJson(checkpoint);
            string fleetField = "\"shipPortProgress\":" + JsonUtility.ToJson(checkpoint.shipPortProgress) + ",";
            Assert.That(json, Does.Contain(fleetField));
            CoreVoyageCheckpoint legacy = JsonUtility.FromJson<CoreVoyageCheckpoint>(json.Replace(fleetField, string.Empty));
            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreShipPortProgress(new CoreShipPortProgress
            {
                ships = new[] { new CoreShipPortLocation { shipIndex = 5, port = "暮潮港" } },
            }, new[] { 0, 4, 5 }, 4), Is.True);
            Assert.That(restored.TryRestoreVoyageCheckpoint(legacy, out string reason), Is.True, reason);
            Assert.That(restored.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(restored.ShipPort(4), Is.EqualTo("断锚港"));
            Assert.That(restored.ShipPort(5), Is.EqualTo("暮潮港"));
        }

        [Test]
        public void PortBattleAndSettlementCannotCaptureUnsafeCheckpoint()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.CanCaptureVoyageCheckpoint, Is.False);
            Assert.Throws<InvalidOperationException>(() => session.CaptureVoyageCheckpoint());
            session.StartVoyage();
            // Use an ordinary battle, whose normal retreat can produce the
            // settlement boundary independently of the scripted tutorial.
            CoreEncounter encounter = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_coral_ambush").Encounter;
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.Throws<InvalidOperationException>(() => session.CaptureVoyageCheckpoint());
            Assert.That(session.RetreatFromBattle(), Is.True);
            Assert.Throws<InvalidOperationException>(() => session.CaptureVoyageCheckpoint());
        }

        [TestCase(false)] [TestCase(true)]
        public void CompletedBattleCanSuspendAndResumeWithItsOneShotLootAndUnbankedFragment(bool captured)
        {
            var source = new CoreVoyageSession();
            source.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            source.StartVoyage(); EnterNode(source, source.Map.Nodes[0]);
            Assert.That(source.ResolveChoice(0), Is.True);
            // Practice chapters deliberately intercept a defeated target and offer
            // a retry. Only the final battle can produce voyage rewards to save.
            CoreGuidedBossScriptTests.Reach(source, GuidedBossBattleStep.CourseFinal);
            Assert.That(source.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.FinalBattle));
            // Finish through the production battle terminal-action path. Seed 0
            // deterministically grants a boarding component on a captured ship.
            typeof(CoreVoyageSession).GetField("voyageRandom", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(source, new Mulberry32Random(0u));
            if (captured)
            {
                source.Battle.Stage = NavalBattleStage.Boarding;
                source.Battle.EnemyBoardingHealth = 0;
            }
            else source.Battle.EnemyHull = 0;
            Assert.That(source.EndBattleTurn(), Is.True);
            Assert.That(source.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(source.Battle, Is.Null); Assert.That(source.Settlement, Is.Null);
            Assert.That(source.CanCaptureVoyageCheckpoint, Is.True);
            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.GuidedTutorialBossDefeated, Is.True);
            Assert.That(restored.VoyageTreasureFragments, Is.EqualTo(1));
            Assert.That(restored.TreasureFragments, Is.Zero);
            Assert.That(restored.Map.Nodes.Any(n => n.IsBoss), Is.False);
            Assert.That(restored.TryConsumeBoardingComponentDrop(out int restoredSlot), Is.EqualTo(captured));
            Assert.That(source.TryConsumeBoardingComponentDrop(out int originalSlot), Is.EqualTo(captured));
            Assert.That(restoredSlot, Is.EqualTo(originalSlot));
            Assert.That(restored.TryConsumeBoardingComponentDrop(out _), Is.False);
            source.SafeReturn(); restored.SafeReturn();
            Assert.That(restored.Settlement.BankedGold, Is.EqualTo(source.Settlement.BankedGold));
            Assert.That(restored.Settlement.BankedFragments, Is.EqualTo(1));
            restored.ReturnToPortAfterSettlement(); decimal balance = restored.ProfileGold;
            restored.ReturnToPortAfterSettlement();
            Assert.That(restored.ProfileGold, Is.EqualTo(balance));
            Assert.That(restored.TreasureFragments, Is.EqualTo(1));
        }

        [Test]
        public void OrdinaryVoyageJsonRoundTripPreservesRationsResourcesCourseAndNextAction()
        {
            var source = new CoreVoyageSession();
            source.RestoreProfile(1234.5678m, "断锚港");
            source.SetSeed("CHECKPOINT-ORDINARY");
            source.ConfigureComponentBonuses(5, 2, 3, 1.2f, 8);
            source.StartVoyage();
            Invoke(source, "ConsumeHours", 13.25d);
            source.SetHeading(SailingDirection.SouthEast);
            source.SetDistance(40);
            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.ProfileGold, Is.EqualTo(source.ProfileGold));
            Assert.That(restored.Cargo.Water, Is.EqualTo(source.Cargo.Water));
            Assert.That(restored.TotalHours, Is.EqualTo(source.TotalHours));
            Assert.That(restored.Map.TerrainCells, Is.EqualTo(source.Map.TerrainCells));
            Invoke(source, "ConsumeHours", .4d); Invoke(restored, "ConsumeHours", .4d);
            Assert.That(restored.Cargo.Water, Is.EqualTo(source.Cargo.Water), "Fractional rations must not be charged again after loading.");
            source.SafeReturn(); restored.SafeReturn();
            Assert.That(restored.Settlement.BankedGold, Is.EqualTo(source.Settlement.BankedGold));
            Assert.That(restored.Settlement.BankedWood, Is.EqualTo(source.Settlement.BankedWood));
        }

        [Test]
        public void WrappedCalendarCheckpoint_RestoresMidnightAndContinuesThroughSaveEpoch()
        {
            var source = new CoreVoyageSession();
            var before = new DateTime(9999, 12, 31, 23, 30, 0);
            Assert.That(source.RestoreGameClock((before - CoreVoyageSession.GameClockOrigin).TotalHours), Is.True);
            source.StartVoyage();
            Invoke(source, "ConsumeHours", 1d);
            Assert.That(source.GameDateTime, Is.EqualTo(new DateTime(1, 1, 1, 0, 30, 0)));

            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.GameDateTime, Is.EqualTo(source.GameDateTime));
            Assert.That(restored.ElapsedGameHours, Is.EqualTo(source.ElapsedGameHours));
            Invoke(restored, "ConsumeHours", 8d);
            Assert.That(restored.GameDateTime, Is.EqualTo(new DateTime(1, 1, 1, 8, 30, 0)));
            Assert.That(restored.TotalHours, Is.EqualTo(9d));
        }

        [Test]
        public void VoyageResumePreservesIdentifiedFragmentsAndNextIdentificationResult()
        {
            var source = new CoreVoyageSession();
            source.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, fragments = 8,
                identifiedWeatheredFragments = 3, identifiedGildedFragments = 6, identifiedMoonlitFragments = 2,
                identificationSeed = "CHECKPOINT-FRAGMENTS", identificationSequence = 19, moonlitPityCount = 2 });
            Assert.That(source.TryIdentifyTreasureFragment(out _), Is.True);
            string treasureBefore = JsonUtility.ToJson(source.CaptureTreasureProgress());
            source.StartVoyage();
            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(JsonUtility.ToJson(restored.CaptureTreasureProgress()), Is.EqualTo(treasureBefore));
            source.SafeReturn(); restored.SafeReturn();
            source.ReturnToPortAfterSettlement(); restored.ReturnToPortAfterSettlement();
            Assert.That(source.TryIdentifyTreasureFragment(out string result), Is.True);
            Assert.That(restored.TryIdentifyTreasureFragment(out string resumedResult), Is.True);
            Assert.That(resumedResult, Is.EqualTo(result));
            Assert.That(JsonUtility.ToJson(restored.CaptureTreasureProgress()), Is.EqualTo(JsonUtility.ToJson(source.CaptureTreasureProgress())));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void StructuredChartRoundTripKeepsRandomSequenceAndBothSailingDirections(int route)
        {
            string seed = CoreVoyageSession.CreateChartSeed(route, "断锚港", "白贝港");
            foreach (string port in new[] { "断锚港", "白贝港" })
            {
                var source = new CoreVoyageSession(); source.RestoreProfile(2000, port);
                source.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
                Assert.That(source.TrySelectChartSeed(seed, out string reason), Is.True, reason);
                source.StartVoyage();
                CoreVoyageSession restored = RoundTrip(source);
                Assert.That(restored.Map.Departure, Is.EqualTo(source.Map.Departure));
                Assert.That(restored.Map.Destination, Is.EqualTo(source.Map.Destination));
                foreach (string field in new[] { "voyageRandom", "bossTreasureRandom", "encounterRandom" })
                    Assert.That(Field<Mulberry32Random>(restored, field).NextUInt(), Is.EqualTo(Field<Mulberry32Random>(source, field).NextUInt()), field);
                Assert.That(restored.CaptureSeaChartProgress().charts[0].visits, Is.EqualTo(1), "Resume must not count as another departure.");
            }
        }

        [Test]
        public void NarrowDepartureBandFallsBackWithoutLosingEventsSafetyOrCheckpointCompatibility()
        {
            const string seed = "SS6.2.0.1.0.1.6F92EB95.00000000.C80CACA9";
            foreach (string port in new[] { "断锚港", "白贝港" })
            {
                var source = new CoreVoyageSession();
                source.RestoreProfile(2000, port);
                source.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
                Assert.That(source.TrySelectChartSeed(seed, out string reason), Is.True, reason);
                source.StartVoyage();
                CoreVoyageSession restored = RoundTrip(source);
                Assert.That(restored.Map.Nodes.Select(node => node.Encounter.Id),
                    Is.EqualTo(source.Map.Nodes.Select(node => node.Encounter.Id)));
                Assert.That(restored.Map.Nodes.Select(node => node.Position),
                    Is.EqualTo(source.Map.Nodes.Select(node => node.Position)));
                Assert.That(restored.Map.TerrainCells, Is.EqualTo(source.Map.TerrainCells));
                Assert.That(source.Map.EventFootprints.Count, Is.EqualTo(source.Map.Nodes.Count));
                int[] reserved = source.Map.EventFootprints.SelectMany(footprint => footprint.ReservedCellIndices).ToArray();
                Assert.That(reserved.Distinct().Count(), Is.EqualTo(reserved.Length), "Safety bands must remain disjoint.");
                Assert.That(source.PlanNavigablePath(source.Map.Departure, source.Map.Destination).Count, Is.EqualTo(2));
                Assert.That(source.PlanNavigablePath(source.Map.Destination, source.Map.Departure).Count, Is.EqualTo(2));
                foreach (CoreMapNode node in source.Map.Nodes)
                    Assert.That(source.PlanNavigablePath(source.Map.Departure, node.Position).Count,
                        Is.GreaterThanOrEqualTo(2), "Unreachable event: " + node.Encounter.Id);

                // The three authored bands use the chart's forward direction, even
                // when a saved chart is sailed back from the other port.
                VoyagePoint from = port == "断锚港" ? source.Map.Departure : source.Map.Destination;
                VoyagePoint to = port == "断锚港" ? source.Map.Destination : source.Map.Departure;
                float dx = to.X - from.X, dy = to.Y - from.Y;
                var canSee = typeof(CoreVoyageSession).GetMethod("CanSeeCell", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int band = 0; band < 3; band++)
                {
                    float low = band == 0 ? .10f : band == 1 ? .40f : .80f;
                    float high = band == 0 ? .17f : band == 1 ? .58f : .90f;
                    Assert.That(source.Map.Nodes.Any(node =>
                    {
                        if (node.IsBoss || !node.Encounter.CanDiscoverBySight) return false;
                        float progress = ((node.Position.X - from.X) * dx + (node.Position.Y - from.Y) * dy) / (dx * dx + dy * dy);
                        if (progress < low || progress > high) return false;
                        var observer = new VoyagePoint(from.X + dx * progress, from.Y + dy * progress);
                        float x = node.Position.X - observer.X, y = node.Position.Y - observer.Y;
                        if (Math.Sqrt(x * x + y * y) > 2.1001f) return false;
                        VoyageGridCoordinate cell = source.Map.PointToCell(node.Position);
                        return (bool)canSee.Invoke(source, new object[] { observer, cell.Column, cell.Row, 120f / source.Map.Route.NauticalMilesPerMapUnit });
                    }), Is.True, "Missing visible course stop in band " + band);
                }
            }
        }
        [Test]
        public void DynamicChartEncounterRestoresExactlyOnceWithoutRerollingAmbush()
        {
            var source = ChartSession();
            CoreEncounter first = Field<CoreEncounter[]>(source, "chartRandomEncounters")[0];
            Invoke(source, "BeginSeaChartTransientEncounter", first);
            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.ActiveEncounter.Id, Is.EqualTo(first.Id));
            Assert.That(restored.ActiveEncounter.Choices.Select(c => c.Label), Is.EqualTo(first.Choices.Select(c => c.Label)));
            Assert.That(Field<int>(restored, "chartRandomIndex"), Is.EqualTo(1));
            Assert.That(restored.ResolveChoice(0), Is.EqualTo(source.ResolveChoice(0)));
            Assert.That(restored.Phase, Is.EqualTo(source.Phase));
            Assert.That(restored.Cargo.Gold, Is.EqualTo(source.Cargo.Gold));
            Assert.That(restored.Cargo.Food, Is.EqualTo(source.Cargo.Food));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void GuidedTutorialRestoresItsAuthoredEncounterAndCompletionFlags(int leg)
        {
            var source = new CoreVoyageSession(); source.ConfigureGuidedTutorialVoyage(leg); source.StartVoyage();
            EnterNode(source, source.Map.Nodes[0]);
            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.GuidedTutorialLeg, Is.EqualTo(leg));
            Assert.That(restored.ActiveEncounter.Id, Is.EqualTo(source.ActiveEncounter.Id));
            Assert.That(restored.ResolveChoice(0), Is.EqualTo(source.ResolveChoice(0)));
            Assert.That(restored.Phase, Is.EqualTo(source.Phase));
            Assert.That(restored.GuidedTutorialCompletedEncounterIds, Is.EqualTo(source.GuidedTutorialCompletedEncounterIds));
        }

        [TestCase("weathered")] [TestCase("gilded")] [TestCase("moonlit")]
        public void ConsumedTreasureMapAndDeferredCoreSurviveResumeWithoutReturningMapToInventory(string rank)
        {
            var source = new CoreVoyageSession();
            source.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400, selectedMapId = "active-map",
                maps = new[] { new CoreTreasureMapSave { id = "active-map", seed = "CHECKPOINT-TREASURE", rankId = rank } } });
            Assert.That(source.TryStartTreasureVoyage(out string reason), Is.True, reason);
            CoreMapNode core = source.Map.Nodes.Single(n => n.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
            EnterNode(source, core);
            int defer = core.Encounter.Choices.ToList().FindIndex(c => c.Effect.PostponeTreasureCore);
            Assert.That(source.ResolveChoice(defer), Is.True);
            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.IsTreasureVoyage, Is.True);
            Assert.That(restored.TreasureMaps, Is.Empty);
            Assert.That(restored.ActiveTreasureMap.Id, Is.EqualTo("active-map"));
            Assert.That(restored.Map.Nodes.Single(n => n.Id == core.Id).Skipped, Is.True);
            Assert.That(restored.Map.Nodes.Single(n => n.Id == core.Id).Visited, Is.False);
            source.Cargo.Gold += 100; restored.Cargo.Gold += 100;
            source.SafeReturn(); restored.SafeReturn();
            Assert.That(restored.Settlement.BankedGold, Is.EqualTo(source.Settlement.BankedGold), "Departure cargo must be restored, not recaptured from voyage gains.");
        }

        [Test]
        public void ClaimedChartRewardsAndRemovedBossesDoNotReappearOnResume()
        {
            var source = ChartSession();
            CoreMapNode node = source.Map.Nodes.First(n => !n.IsBoss && n.Encounter.Choices.Any(c => !c.StartsBattle && !c.Renewable));
            EnterNode(source, node); Invoke(source, "RecordSeaChartEncounterClaim");
            Assert.That(source.ActiveEncounterClaimCount, Is.EqualTo(1));
            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.ActiveEncounterClaimCount, Is.EqualTo(1));
            Assert.That(restored.ActiveEncounterRewardMultiplier, Is.EqualTo(source.ActiveEncounterRewardMultiplier));
            source.SkipEncounter();
            if (source.Phase == CoreDemoPhase.Encounter) Invoke(source, "CompleteEncounter", false);
            CoreSeaChartState chart = source.CaptureSeaChartProgress().charts[0];
            CoreMapNode boss = source.Map.Nodes.First(n => n.IsBoss);
            chart.defeatedBosses = new[] { boss.Encounter.Id };
            typeof(CoreVoyageSession).GetField("bossDefeated", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(source, true);
            typeof(CoreVoyageSession).GetField("currentRouteBossDefeated", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(source, true);
            var states = Field<System.Collections.Generic.Dictionary<string, CoreSeaChartState>>(source, "seaChartStates");
            states[chart.seed] = chart;
            Invoke(source, "RemoveDefeatedSeaChartBosses", chart, false);
            restored = RoundTrip(source);
            Assert.That(restored.Map.Nodes.Any(n => n.Encounter.Id == boss.Encounter.Id), Is.False);
        }

        [Test]
        public void HalfDayDistanceModeSurvivesJsonAndFollowsChangedSpeed()
        {
            CoreVoyageSession source = ChartSession();
            source.SetCourseTo(source.Map.Destination);
            int originalDistance = source.PlannedDistanceNm;
            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.UsesDefaultDistance, Is.True);
            Assert.That(restored.PlannedDistanceNm, Is.EqualTo(originalDistance));

            source.ConfigureComponentContextEffect(0, 0, 0, 3f, 0, 1f);
            restored.ConfigureComponentContextEffect(0, 0, 0, 3f, 0, 1f);
            Assert.That(restored.PlannedDistanceNm, Is.GreaterThan(originalDistance));
            Assert.That(restored.PlannedDistanceNm, Is.EqualTo(source.PlannedDistanceNm));
            Assert.That(restored.HasDirectCourseTarget, Is.True);
            Assert.That(restored.DirectCourseTarget.X, Is.EqualTo(source.DirectCourseTarget.X));
            Assert.That(restored.DirectCourseTarget.Y, Is.EqualTo(source.DirectCourseTarget.Y));
        }

        [Test]
        public void JsonPreservesManualBudgetAboveOldCapEvenDuringTemporarySlowdown()
        {
            CoreVoyageSession source = ChartSession();
            source.SetCourseTo(source.Map.Destination);
            source.ConfigureComponentContextEffect(0, 0, 0, 3f, 0, 1f);
            int selectedDistance = source.MaximumDistanceNm;
            Assert.That(selectedDistance, Is.GreaterThan(180));
            source.SetDistance(selectedDistance);
            source.ConfigureComponentContextEffect(0, 0, 0, -3f, 0, 1f);
            Assert.That(source.PlannedDistanceNm, Is.LessThan(selectedDistance));
            Assert.That(source.CaptureVoyageCheckpoint().plannedDistance, Is.EqualTo(selectedDistance),
                "Saving while slower must retain the player's requested budget.");

            CoreVoyageSession restored = RoundTrip(source);
            Assert.That(restored.UsesDefaultDistance, Is.False);
            Assert.That(restored.PlannedDistanceNm, Is.EqualTo(source.PlannedDistanceNm));
            Assert.That(restored.RequestedDistanceNm, Is.EqualTo(selectedDistance),
                "The distance control restores the original selection, not the temporary day limit.");
            Assert.That(restored.PlannedDistanceNm, Is.LessThanOrEqualTo(restored.MaximumDistanceNm));
            restored.ConfigureComponentContextEffect(0, 0, 0, 3f, 0, 1f);
            Assert.That(restored.PlannedDistanceNm, Is.EqualTo(selectedDistance));
            Assert.That(restored.HasDirectCourseTarget, Is.True);
        }

        [Test]
        public void LegacyJsonDistanceLoadsAsManualAndClampsToCurrentDayLimit()
        {
            var source = new CoreVoyageSession();
            source.ConfigureComponentBonuses(0, 0, 0, -11f, 0);
            source.StartVoyage();
            source.SetCourseTo(source.Map.Destination);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            checkpoint.plannedDistance = 180;
            string json = JsonUtility.ToJson(checkpoint)
                .Replace("\"distancePlanVersion\":1,", string.Empty)
                .Replace("\"useDefaultDistance\":true,", string.Empty);
            Assert.That(json, Does.Not.Contain("distancePlanVersion"));
            Assert.That(json, Does.Not.Contain("useDefaultDistance"));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(json), out string reason), Is.True, reason);
            Assert.That(restored.UsesDefaultDistance, Is.False);
            Assert.That(restored.MaximumDistanceNm, Is.LessThan(180));
            Assert.That(restored.PlannedDistanceNm, Is.EqualTo(restored.MaximumDistanceNm));
            Assert.That(restored.RequestedDistanceNm, Is.EqualTo(180));
            Assert.That(restored.HasDirectCourseTarget, Is.True);
            Assert.That(restored.DirectCourseTarget.X, Is.EqualTo(source.DirectCourseTarget.X));
            Assert.That(restored.DirectCourseTarget.Y, Is.EqualTo(source.DirectCourseTarget.Y));
        }

        [TestCase("version")] [TestCase("node")] [TestCase("fog")] [TestCase("cargo")] [TestCase("deck")] [TestCase("nan")]
        [TestCase("distance-version")] [TestCase("negative-distance")]
        [TestCase("identified-fragments")] [TestCase("identification-sequence")]
        public void InvalidCheckpointLeavesExistingProfileCompletelyUntouched(string corruption)
        {
            var source = ChartSession(); CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            switch (corruption)
            {
                case "version": checkpoint.version++; break;
                case "distance-version": checkpoint.distancePlanVersion++; break;
                case "negative-distance": checkpoint.plannedDistance = -1; break;
                case "node": checkpoint.nodes[0].encounterId = "unknown"; break;
                case "fog": checkpoint.exploredCells = new bool[1]; break;
                case "cargo": checkpoint.cargo.gold = -1; break;
                case "deck": checkpoint.battleDecks[0].cards[0] = int.MaxValue; break;
                case "identified-fragments": checkpoint.treasureProgress.identifiedWeatheredFragments = -1; break;
                case "identification-sequence": checkpoint.treasureProgress.identificationSequence = -1; break;
                default: checkpoint.x = float.NaN; break;
            }
            var target = new CoreVoyageSession(); target.RestoreProfile(987.6543m, "铜钟港"); target.Cargo.Wood = 37;
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.False);
            Assert.That(reason, Is.Not.Empty); Assert.That(target.ProfileGold, Is.EqualTo(987.6543m));
            Assert.That(target.CurrentPort, Is.EqualTo("铜钟港")); Assert.That(target.Cargo.Wood, Is.EqualTo(37));
            Assert.That(target.Map, Is.Null); Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(target.CaptureSeaChartProgress().charts, Is.Empty);
        }

        [Test]
        public void CapturedDtoAndRestoredSessionNeverAliasLiveState()
        {
            var source = ChartSession(); CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            var target = new CoreVoyageSession(); Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.True);
            checkpoint.cargo.water = 0; checkpoint.nodes[0].visited = true;
            checkpoint.exploredCells[0] = !checkpoint.exploredCells[0]; checkpoint.seaChartProgress.charts[0].visits = 50;
            Assert.That(target.Cargo.Water, Is.EqualTo(source.Cargo.Water));
            Assert.That(target.Map.Nodes[0].Visited, Is.EqualTo(source.Map.Nodes[0].Visited));
            Assert.That(target.Map.ExploredCells[0], Is.EqualTo(source.Map.ExploredCells[0]));
            Assert.That(target.CaptureSeaChartProgress().charts[0].visits, Is.EqualTo(1));
        }

        private static CoreVoyageSession ChartSession()
        {
            var source = new CoreVoyageSession();
            Assert.That(source.TrySelectChartSeed(CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港"), out _), Is.True);
            source.StartVoyage(); return source;
        }

        private static CoreVoyageSession RoundTrip(CoreVoyageSession source)
        {
            string json = JsonUtility.ToJson(source.CaptureVoyageCheckpoint());
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(json), out string reason), Is.True, reason);
            Assert.That(JsonUtility.ToJson(restored.CaptureVoyageCheckpoint()), Is.EqualTo(json));
            return restored;
        }

        private static void EnterNode(CoreVoyageSession s, CoreMapNode node)
        {
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(s, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(s, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(s, CoreDemoPhase.Encounter);
        }
        private static T Field<T>(CoreVoyageSession s, string name) => (T)typeof(CoreVoyageSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s);
        private static void Invoke(CoreVoyageSession s, string name, params object[] args) => typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
    }
}
