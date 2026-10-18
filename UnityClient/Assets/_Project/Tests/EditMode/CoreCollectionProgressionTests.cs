using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCollectionProgressionTests
    {
        private static IEnumerable<string> AllBosses => new[] { CoreGuidedTutorialContent.BossEventId, CoreBattleBalance.TreasureBossId }
            .Concat(CoreRouteBossCatalog.All.Select(boss => boss.Id))
            .Concat(CoreLegendaryWarships.All.Select(boss => boss.Id));

        [TestCaseSource(nameof(AllBosses))]
        public void AuthoredBossVictoryIsPermanentOnceEvenWhenLaterVoyageFails(string id)
        {
            CoreVoyageSession session = BattleReady(id);
            Assert.That(session.CaptureCollectionProgress().defeatedBossIds, Is.Empty, "Encountering or challenging a boss is not a victory.");
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.CaptureCollectionProgress().defeatedBossIds, Is.EqualTo(new[] { id }));
            int revision = session.CollectionProgressRevision;
            Assert.That(session.EndBattleTurn(), Is.False);
            Assert.That(session.CollectionProgressRevision, Is.EqualTo(revision));
            session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            session.ReturnToPortAfterSettlement();
            var restored = new CoreVoyageSession();
            string json = JsonUtility.ToJson(session.CaptureCollectionProgress());
            Assert.That(restored.RestoreCollectionProgress(JsonUtility.FromJson<CoreCollectionProgress>(json)), Is.True);
            Assert.That(restored.CaptureCollectionProgress().defeatedBossIds, Is.EqualTo(new[] { id }));
            Assert.That(restored.CaptureCollectionProgress().completedChartSeeds, Is.Empty);
        }

        [TestCase("retreat")]
        [TestCase("defeat")]
        [TestCase("mutual-sinking")]
        public void RetreatAndDefeatNeverRecordBossCompletion(string result)
        {
            CoreVoyageSession session = BattleReady("boss_" + CoreVoyageSession.Routes[0].Id);
            if (result == "retreat") Assert.That(session.RetreatFromBattle(), Is.True);
            else
            {
                Set(session, "Hull", 0);
                if (result == "mutual-sinking") session.Battle.EnemyHull = 0;
                Assert.That(session.EndBattleTurn(), Is.True);
            }
            session.ReturnToPortAfterSettlement();
            Assert.That(session.CaptureCollectionProgress().defeatedBossIds, Is.Empty);
            Assert.That(session.CaptureCollectionProgress().completedChartSeeds, Is.Empty);
        }

        [Test]
        public void OrdinaryEnemyAndUnknownBossPrefixNeverBecomeCollectionBosses()
        {
            foreach (string id in new[] { "ordinary-test", "legendary_pirate_unknown", "boss_route_unknown" })
            {
                var session = new CoreVoyageSession(); session.StartVoyage();
                Enter(session, new CoreMapNode { Encounter = new CoreEncounter(id, "test", "test", "test", true, false, 1, 70,
                    new CoreEncounterChoice("fight", "fight", 0, new VoyageEffect(), startsBattle: true)) });
                Assert.That(session.ResolveChoice(0), Is.True);
                session.Battle.EnemyHull = 0;
                Assert.That(session.EndBattleTurn(), Is.True);
                Assert.That(session.CaptureCollectionProgress().defeatedBossIds, Is.Empty);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ConfirmedChartCrossingCountsOnceAcrossBothDirectionsAndReload(int route)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[route].UnlockLevel) });
            string seed = CoreVoyageSession.CreateChartSeed(route, "断锚港", "白贝港");
            Assert.That(session.TrySelectChartSeed(seed, out _), Is.True);
            session.StartVoyage();
            DefeatRouteBossAndArrive(session);
            Assert.That(session.CaptureCollectionProgress().completedChartSeeds, Is.Empty, "The settlement still needs confirmation.");
            session.ReturnToPortAfterSettlement();
            CoreCollectionProgress saved = session.CaptureCollectionProgress();
            Assert.That(saved.completedChartSeeds, Is.EqualTo(new[] { seed }));
            Assert.That(saved.completedRouteIds, Is.EqualTo(new[] { CoreVoyageSession.Routes[route].Id }));
            Assert.That(saved.visitedPorts, Is.EquivalentTo(new[] { "断锚港", "白贝港" }));
            int revision = session.CollectionProgressRevision;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.CollectionProgressRevision, Is.EqualTo(revision));

            var restored = new CoreVoyageSession();
            restored.RestoreProfile(500, "白贝港");
            restored.RestoreTreasureProgress(session.CaptureTreasureProgress());
            Assert.That(restored.RestoreCollectionProgress(saved), Is.True);
            Assert.That(restored.RestoreSeaChartProgress(session.CaptureSeaChartProgress()), Is.True);
            Assert.That(restored.TrySelectChartSeed(seed, out _), Is.True);
            restored.StartVoyage();
            Arrive(restored);
            restored.ReturnToPortAfterSettlement();
            Assert.That(restored.CaptureCollectionProgress().completedChartSeeds, Is.EqualTo(new[] { seed }));
            Assert.That(restored.CaptureCollectionProgress().visitedPorts, Is.EquivalentTo(saved.visitedPorts));
        }

        [Test]
        public void UndrawnRandomCrossingCountsByCanonicalGeographyAndSafeReturnDoesNot()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.SetSeed("COLLECTION-LEGACY"); session.StartVoyage();
            Assert.That(session.CurrentChartSeed, Is.Empty);
            uint hash = session.Map.Hash;
            DefeatRouteBossAndArrive(session);
            Assert.That(session.Settlement.ChartSaved, Is.False);
            session.ReturnToPortAfterSettlement();
            CoreCollectionProgress progress = session.CaptureCollectionProgress();
            Assert.That(CoreVoyageSession.TryParseChartSeed(progress.completedChartSeeds.Single(), out CoreSeaChartDefinition chart), Is.True);
            Assert.That(chart.GeographyHash, Is.EqualTo(hash));
            session.StartVoyage();
            Assert.That(session.SafeReturn(), Is.True); session.ReturnToPortAfterSettlement();
            Assert.That(session.CaptureCollectionProgress().completedChartSeeds, Is.EqualTo(progress.completedChartSeeds));
        }

        [Test]
        public void TutorialArrivalNeverCountsAsAnOrdinaryCompletedChart()
        {
            CoreVoyageSession session = BattleReady(CoreGuidedTutorialContent.BossEventId);
            session.Battle.EnemyHull = 0; Assert.That(session.EndBattleTurn(), Is.True);
            Arrive(session); session.ReturnToPortAfterSettlement();
            Assert.That(session.CaptureCollectionProgress().completedChartSeeds, Is.Empty);
            Assert.That(session.CaptureCollectionProgress().completedRouteIds, Is.Empty);
            Assert.That(session.CaptureCollectionProgress().visitedPorts.Count(), Is.EqualTo(2));
        }

        [TestCase("weathered")] [TestCase("gilded")] [TestCase("moonlit")]
        public void TreasureRewardRequiresCoreCompletionAndConfirmedReturn(string rank)
        {
            CoreVoyageSession session = TreasureReady(rank);
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId);
            Assert.That(session.ActiveTreasureMap.ContentVersion, Is.EqualTo(1));
            PrepareCurrentTreasureCore(session, rank);
            Enter(session, node);
            Assert.That(session.ResolveChoice(0), Is.True, session.ChoiceUnavailableReason(0));
            Assert.That(session.Settlement.TreasureCompleted, Is.True);
            Assert.That(session.CaptureCollectionProgress().completedTreasureSeeds, Is.Empty);
            session.ReturnToPortAfterSettlement();
            CoreCollectionProgress progress = session.CaptureCollectionProgress();
            Assert.That(progress.completedTreasureSeeds, Is.EqualTo(new[] { "COLLECTION-" + rank }));
            Assert.That(progress.completedTreasureRanks, Is.EqualTo(new[] { rank }));
            Assert.That(progress.completedChartSeeds, Is.Empty);
            int revision = session.CollectionProgressRevision;
            session.ReturnToPortAfterSettlement();
            Assert.That(session.CollectionProgressRevision, Is.EqualTo(revision));
        }

        [TestCase(false)] [TestCase(true)]
        public void UncompletedOrFailedTreasureCannotGrantSeedOrRank(bool fail)
        {
            CoreVoyageSession session = TreasureReady("weathered");
            Assert.That(session.TryStartTreasureVoyage(out _), Is.True);
            if (fail) session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.SafeReturn(), Is.True);
            if (fail) session.Settlement.TreasureCompleted = true; // A stale completion flag must not beat a failed outcome.
            session.ReturnToPortAfterSettlement();
            Assert.That(session.CaptureCollectionProgress().completedTreasureSeeds, Is.Empty);
            Assert.That(session.CaptureCollectionProgress().completedTreasureRanks, Is.Empty);
        }

        [Test]
        public void CaptureAndRestoreOwnTheirArraysAndRepeatedRestoreIsIdempotent()
        {
            var session = new CoreVoyageSession();
            CoreCollectionProgress progress = ValidProgress();
            Assert.That(session.RestoreCollectionProgress(progress), Is.True);
            int revision = session.CollectionProgressRevision;
            Assert.That(session.RestoreCollectionProgress(progress), Is.True);
            Assert.That(session.CollectionProgressRevision, Is.EqualTo(revision));
            progress.defeatedBossIds[0] = "mutated"; progress.visitedPorts[0] = "mutated";
            CoreCollectionProgress snapshot = session.CaptureCollectionProgress();
            Assert.That(snapshot.defeatedBossIds, Does.Not.Contain("mutated"));
            Assert.That(snapshot.visitedPorts, Does.Not.Contain("mutated"));
            snapshot.completedTreasureSeeds[0] = "mutated";
            Assert.That(session.CaptureCollectionProgress().completedTreasureSeeds, Does.Not.Contain("mutated"));
            session.StartVoyage();
            Assert.That(session.RestoreCollectionProgress(new CoreCollectionProgress()), Is.False);
        }

        [TestCase("version")] [TestCase("unknown-boss")] [TestCase("duplicate-boss")]
        [TestCase("invalid-chart")] [TestCase("missing-route")] [TestCase("unknown-route")]
        [TestCase("missing-port")] [TestCase("unknown-port")] [TestCase("blank-seed")]
        [TestCase("duplicate-seed")] [TestCase("unknown-rank")] [TestCase("unearned-ranks")]
        public void CorruptRestoreCannotPartiallyReplaceExistingProgress(string corruption)
        {
            var session = new CoreVoyageSession(); session.RecordCollectionPortVisit("灰鸥港");
            CoreCollectionProgress progress = ValidProgress();
            switch (corruption)
            {
                case "version": progress.version = 99; break;
                case "unknown-boss": progress.defeatedBossIds = new[] { "boss_route_unknown" }; break;
                case "duplicate-boss": progress.defeatedBossIds = new[] { progress.defeatedBossIds[0], progress.defeatedBossIds[0] }; break;
                case "invalid-chart": progress.completedChartSeeds = new[] { "RAW" }; break;
                case "missing-route": progress.completedRouteIds = Array.Empty<string>(); break;
                case "unknown-route": progress.completedRouteIds = new[] { "unknown" }; break;
                case "missing-port": progress.visitedPorts = new[] { "断锚港" }; break;
                case "unknown-port": progress.visitedPorts = new[] { "第七港" }; break;
                case "blank-seed": progress.completedTreasureSeeds = new[] { " " }; break;
                case "duplicate-seed": progress.completedTreasureSeeds = new[] { "OLD", "OLD" }; break;
                case "unknown-rank": progress.completedTreasureRanks = new[] { "unknown" }; break;
                case "unearned-ranks": progress.completedTreasureRanks = new[] { "weathered", "gilded" }; break;
            }
            int revision = session.CollectionProgressRevision;
            Assert.That(session.RestoreCollectionProgress(progress), Is.False);
            Assert.That(session.CollectionProgressRevision, Is.EqualTo(revision));
            Assert.That(session.CaptureCollectionProgress().visitedPorts, Is.EqualTo(new[] { "灰鸥港" }));
            Assert.That(session.CaptureCollectionProgress().defeatedBossIds, Is.Empty);
        }

        [Test]
        public void LegacyHistoryImportsOnlyProvenBossWinsAndCompletedTreasureSeeds()
        {
            var session = new CoreVoyageSession();
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            string boss = "boss_" + CoreVoyageSession.Routes[0].Id;
            var history = new CoreSeaChartProgress { charts = new[] {
                new CoreSeaChartState { seed = seed, visits = 1, defeatedBosses = new[] { boss, "boss_route_unknown", CoreLegendaryWarships.All[0].Id } },
                new CoreSeaChartState { seed = "invalid", visits = 1, defeatedBosses = new[] { CoreGuidedTutorialContent.BossEventId } },
            } };
            var treasure = new CoreTreasureProgress { completedSeeds = new[] { "OLD", "OLD", " " },
                maps = new[] { new CoreTreasureMapSave { id = "held", seed = "HELD", rankId = "moonlit" } } };
            session.ImportCollectionHistory(history, treasure);
            CoreCollectionProgress imported = session.CaptureCollectionProgress();
            Assert.That(imported.defeatedBossIds, Is.EqualTo(new[] { boss }));
            Assert.That(imported.completedTreasureSeeds, Is.EqualTo(new[] { "OLD" }));
            Assert.That(imported.completedTreasureRanks, Is.Empty);
            Assert.That(imported.completedChartSeeds, Is.Empty);
            Assert.That(imported.completedRouteIds, Is.Empty);
            Assert.That(imported.visitedPorts, Is.Empty);
            int revision = session.CollectionProgressRevision;
            session.ImportCollectionHistory(history, treasure);
            Assert.That(session.CollectionProgressRevision, Is.EqualTo(revision));
            session.RecordCollectionPortVisit("不存在的港口");
            Assert.That(session.CollectionProgressRevision, Is.EqualTo(revision));
        }

        private static CoreCollectionProgress ValidProgress() => new CoreCollectionProgress {
            defeatedBossIds = new[] { "boss_" + CoreVoyageSession.Routes[0].Id },
            completedChartSeeds = new[] { CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港") },
            completedRouteIds = new[] { CoreVoyageSession.Routes[0].Id }, visitedPorts = new[] { "断锚港", "白贝港" },
            completedTreasureSeeds = new[] { "OLD" }, completedTreasureRanks = new[] { "weathered" },
        };

        private static CoreVoyageSession BattleReady(string id)
        {
            var session = new CoreVoyageSession();
            if (id == CoreGuidedTutorialContent.BossEventId)
                Assert.That(session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg), Is.True);
            else for (int i = 0; i < CoreVoyageSession.Routes.Count; i++)
                if (CoreRouteBossCatalog.Find(id)?.RouteId == CoreVoyageSession.Routes[i].Id)
                {
                    session.RestoreTreasureProgress(new CoreTreasureProgress
                        { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[i].UnlockLevel) });
                    session.SelectRoute(i);
                    Assert.That(session.SelectedRouteIndex, Is.EqualTo(i));
                }
            session.StartVoyage();
            CoreMapNode node = session.Map.Nodes.FirstOrDefault(item => item.Encounter.Id == id);
            if (node == null && CoreRouteBossCatalog.Find(id) != null)
                node = new CoreMapNode { IsBoss = true, Encounter = CoreRouteBossCatalog.Find(id).CreateEncounter() };
            if (node == null) node = new CoreMapNode { Encounter = id == CoreBattleBalance.TreasureBossId
                ? CoreLegendaryWarships.CreateTreasureEncounter() : CoreLegendaryWarships.Find(id).CreateEncounter() };
            Enter(session, node);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            if (id == CoreGuidedTutorialContent.BossEventId)
            {
                CoreGuidedBossScriptTests.Reach(session, GuidedBossBattleStep.CourseFinal);
                Assert.That(session.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.FinalBattle));
            }
            return session;
        }

        private static void PrepareCurrentTreasureCore(CoreVoyageSession session, string rank)
        {
            void Choose(string id, int choice)
            {
                Enter(session, session.Map.Nodes.Single(node => node.Encounter.Id == id));
                Assert.That(session.ResolveChoice(choice), Is.True, session.ChoiceUnavailableReason(choice));
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            }
            if (rank == "weathered")
            {
                Choose("weathered_camp", 0);
                Choose("weathered_winch", 0);
                Choose("weathered_bell", 0);
            }
            else if (rank == "gilded")
            {
                Choose("gilded_manifest", 0);
                Choose("gilded_witness", 0);
            }
            else
            {
                Choose("moonlit_dawn", 1);
                Choose("moonlit_dusk", 1);
                Choose("moonlit_midnight", 1);
                Choose("moonlit_cleanse", 0);
            }
        }
        private static CoreVoyageSession TreasureReady(string rank)
        {
            var session = new CoreVoyageSession();
            string seed = "COLLECTION-" + rank;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1400,
                selectedMapId = "test:" + seed, maps = new[] { new CoreTreasureMapSave { id = "test:" + seed, seed = seed, rankId = rank } } });
            foreach (PropertyInfo property in typeof(VoyageCargo).GetProperties().Where(value => value.PropertyType == typeof(int))) property.SetValue(session.Cargo, 0);
            session.Cargo.Water = session.Cargo.Food = 100;
            return session;
        }
        private static void DefeatRouteBossAndArrive(CoreVoyageSession session)
        {
            Enter(session, session.Map.Nodes.Single(node => node.IsBoss));
            Assert.That(session.ResolveChoice(0), Is.True);
            session.Battle.EnemyHull = 0; Assert.That(session.EndBattleTurn(), Is.True);
            Arrive(session);
        }
        private static void Arrive(CoreVoyageSession session)
        {
            Set(session, "Position", session.Map.Destination);
            session.SetCourseTo(session.Map.Destination);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
        }
        // Enter authored encounters and isolate travel distance; production choice/combat/arrival/settlement APIs own the transitions.
        private static void Enter(CoreVoyageSession session, CoreMapNode node)
        {
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            Set(session, "ActiveEncounter", node.Encounter); Set(session, "Phase", CoreDemoPhase.Encounter);
        }
        private static void Set(CoreVoyageSession session, string property, object value) => typeof(CoreVoyageSession).GetProperty(property).SetValue(session, value);
    }
}
