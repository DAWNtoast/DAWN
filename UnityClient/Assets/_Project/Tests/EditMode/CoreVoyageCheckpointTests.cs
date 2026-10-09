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

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void StructuredChartRoundTripKeepsRandomSequenceAndBothSailingDirections(int route)
        {
            string seed = CoreVoyageSession.CreateChartSeed(route, "断锚港", "白贝港");
            foreach (string port in new[] { "断锚港", "白贝港" })
            {
                var source = new CoreVoyageSession(); source.RestoreProfile(2000, port);
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
            var states = Field<System.Collections.Generic.Dictionary<string, CoreSeaChartState>>(source, "seaChartStates");
            states[chart.seed] = chart;
            Invoke(source, "RemoveDefeatedSeaChartBosses", chart, false);
            restored = RoundTrip(source);
            Assert.That(restored.Map.Nodes.Any(n => n.Encounter.Id == boss.Encounter.Id), Is.False);
        }

        [TestCase("version")] [TestCase("node")] [TestCase("fog")] [TestCase("cargo")] [TestCase("deck")] [TestCase("nan")]
        public void InvalidCheckpointLeavesExistingProfileCompletelyUntouched(string corruption)
        {
            var source = ChartSession(); CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            switch (corruption)
            {
                case "version": checkpoint.version++; break;
                case "node": checkpoint.nodes[0].encounterId = "unknown"; break;
                case "fog": checkpoint.exploredCells = new bool[1]; break;
                case "cargo": checkpoint.cargo.gold = -1; break;
                case "deck": checkpoint.battleDecks[0].cards[0] = int.MaxValue; break;
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
