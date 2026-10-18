using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class BossTreasureFragmentDropTests
    {
        [TestCase(CoreBossKind.Route, false, 0, 1)]
        [TestCase(CoreBossKind.Route, false, 1, 0)]
        [TestCase(CoreBossKind.Route, true, 0, 1)]
        [TestCase(CoreBossKind.Route, true, 1, 0)]
        [TestCase(CoreBossKind.Treasure, false, 0, 1)]
        [TestCase(CoreBossKind.Treasure, false, 1, 0)]
        [TestCase(CoreBossKind.Treasure, true, 0, 1)]
        [TestCase(CoreBossKind.Treasure, true, 1, 0)]
        [TestCase(CoreBossKind.Legendary, false, 0, 1)]
        [TestCase(CoreBossKind.Legendary, false, 1, 0)]
        [TestCase(CoreBossKind.Legendary, true, 0, 1)]
        [TestCase(CoreBossKind.Legendary, true, 1, 0)]
        public void EveryBossKind_RollsOnceAtFiftyPercent_AndCaptureStillAwardsOnlyOne(
            CoreBossKind kind, bool captured, int seed, int expectedFragments)
        {
            CoreVoyageSession session = StartBoss(kind);
            Assert.That(session.Battle.Profile.BossKind, Is.EqualTo(kind));
            Assert.That(CoreBattleBalance.BossTreasureFragmentDropChance, Is.EqualTo(.5d));
            Mulberry32Random random = SetDropRandom(session, (uint)seed);
            var expectedRandom = new Mulberry32Random((uint)seed);
            Assert.That(expectedRandom.NextDouble() < .5d, Is.EqualTo(expectedFragments == 1),
                "These fixed seeds exercise opposite sides of the probability boundary.");

            Win(session, captured);

            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(expectedFragments));
            Assert.That(session.TreasureFragments, Is.Zero, "Voyage fragments bank only at return.");
            Assert.That(random.NextUInt(), Is.EqualTo(expectedRandom.NextUInt()), "A boss victory draws once.");
            Assert.That(session.EndBattleTurn(), Is.False);
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(expectedFragments), "Repeated actions cannot award again.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GuidedFinalBoss_GuaranteesExactlyOneFragment_WithoutRolling(bool captured)
        {
            CoreVoyageSession session = StartTutorialBoss();
            // Seed 1 would miss an ordinary boss drop; the tutorial must not roll it.
            Mulberry32Random random = SetDropRandom(session, 1u);
            var expectedRandom = new Mulberry32Random(1u);

            Win(session, captured);

            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(1));
            Assert.That(random.NextUInt(), Is.EqualTo(expectedRandom.NextUInt()), "The guaranteed tutorial drop consumes no roll.");
            Assert.That(session.EndBattleTurn(), Is.False);
            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OrdinaryEnemy_NeverAwardsBossFragment_AndDoesNotRoll(bool captured)
        {
            CoreVoyageSession session = StartOrdinaryEnemy();
            Mulberry32Random random = SetDropRandom(session, 0u);
            var expectedRandom = new Mulberry32Random(0u);

            Win(session, captured);

            Assert.That(session.VoyageTreasureFragments, Is.Zero);
            Assert.That(random.NextUInt(), Is.EqualTo(expectedRandom.NextUInt()));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BossDrop_DoesNotChangeCombatAndVoyageRandomSequence(bool drops)
        {
            CoreVoyageSession session = StartBoss(CoreBossKind.Route);
            SetDropRandom(session, drops ? 0u : 1u);
            var combatRandom = new Mulberry32Random(731u);
            var expectedCombatRandom = new Mulberry32Random(731u);
            SetField(session, "voyageRandom", combatRandom);

            Win(session, false);

            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(drops ? 1 : 0));
            Assert.That(combatRandom.NextUInt(), Is.EqualTo(expectedCombatRandom.NextUInt()),
                "Fragment drops must not change future card draws or sea encounters.");
        }

        [TestCase("defeat")]
        [TestCase("mutual-sinking")]
        [TestCase("retreat")]
        public void LosingOrRetreatingFromBoss_NeverAwardsOrRolls(string outcome)
        {
            CoreVoyageSession session = StartBoss(CoreBossKind.Route);
            Mulberry32Random random = SetDropRandom(session, 0u);
            var expectedRandom = new Mulberry32Random(0u);
            if (outcome == "retreat")
                Assert.That(session.RetreatFromBattle(), Is.True);
            else
            {
                SetProperty(session, "Hull", 0);
                if (outcome == "mutual-sinking") session.Battle.EnemyHull = 0;
                Assert.That(session.EndBattleTurn(), Is.True);
            }

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.BankedFragments, Is.Zero);
            Assert.That(session.VoyageTreasureFragments, Is.Zero);
            Assert.That(random.NextUInt(), Is.EqualTo(expectedRandom.NextUInt()));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.TreasureFragments, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DroppedFragment_BanksOnceAndSurvivesSerializedProgress(bool tutorial)
        {
            CoreVoyageSession session = tutorial ? StartTutorialBoss() : StartBoss(CoreBossKind.Route);
            SetDropRandom(session, 0u);
            Win(session, false);
            Assert.That(session.CaptureTreasureProgress().fragments, Is.Zero);

            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedFragments, Is.EqualTo(1), "The single fragment is not reduced by cargo protection.");
            Assert.That(session.TreasureFragments, Is.Zero);
            session.ReturnToPortAfterSettlement();
            session.ReturnToPortAfterSettlement();
            Assert.That(session.TreasureFragments, Is.EqualTo(1));

            string saved = JsonUtility.ToJson(session.CaptureTreasureProgress());
            var restored = new CoreVoyageSession();
            restored.RestoreTreasureProgress(JsonUtility.FromJson<CoreTreasureProgress>(saved));
            Assert.That(restored.TreasureFragments, Is.EqualTo(1));
            restored.ReturnToPortAfterSettlement();
            Assert.That(restored.TreasureFragments, Is.EqualTo(1));
        }

        [Test]
        public void BossDefeatedEarlierInTutorialLeg_DoesNotReceiveFinalBossGuarantee()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg), Is.True);
            session.StartVoyage();
            CoreEncounter encounter = CoreLegendaryWarships.All[0].CreateEncounter();
            EnterAndChooseBattle(session, new CoreMapNode { Encounter = encounter });
            SetDropRandom(session, 1u);

            Win(session, false);

            Assert.That(session.GuidedTutorialBossDefeated, Is.False);
            Assert.That(session.VoyageTreasureFragments, Is.Zero, "Only the authored final tutorial boss is guaranteed.");
        }

        private static CoreVoyageSession StartBoss(CoreBossKind kind)
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            CoreMapNode node = kind == CoreBossKind.Route
                ? session.Map.Nodes.Single(item => item.IsBoss)
                : new CoreMapNode
                {
                    Encounter = kind == CoreBossKind.Treasure
                        ? CoreLegendaryWarships.CreateTreasureEncounter()
                        : CoreLegendaryWarships.All[0].CreateEncounter(),
                };
            EnterAndChooseBattle(session, node);
            return session;
        }

        private static CoreVoyageSession StartTutorialBoss()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg), Is.True);
            session.StartVoyage();
            EnterAndChooseBattle(session, session.Map.Nodes.Single(node => node.Encounter.Id == CoreGuidedTutorialContent.BossEventId));
            // Exercise victories are deliberately non-rewarding. Finish the
            // lessons through their real actions before testing the final drop.
            CoreGuidedBossScriptTests.Reach(session, GuidedBossBattleStep.CourseFinal);
            Assert.That(session.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.FinalBattle));
            return session;
        }

        private static CoreVoyageSession StartOrdinaryEnemy()
        {
            var session = new CoreVoyageSession();
            session.StartVoyage();
            EnterAndChooseBattle(session, new CoreMapNode
            {
                Encounter = new CoreEncounter("fragment-test-ordinary", "普通敌船", "海战", "非首领敌舰", true, false, 1, 70,
                    new CoreEncounterChoice("战斗", "进入海战", 0, new VoyageEffect(), startsBattle: true)),
            });
            return session;
        }

        // Isolated rule tests enter authored encounters; PlayMode tests own sailing and pointer input.
        private static void EnterAndChooseBattle(CoreVoyageSession session, CoreMapNode node)
        {
            SetField(session, "activeNode", node);
            SetProperty(session, "ActiveEncounter", node.Encounter);
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.VoyageTreasureFragments, Is.Zero, "Merely starting a boss battle grants no fragment.");
        }

        private static void Win(CoreVoyageSession session, bool captured)
        {
            if (captured)
            {
                session.Battle.Stage = NavalBattleStage.Boarding;
                session.Battle.EnemyBoardingHealth = 0;
            }
            else session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Battle, Is.Null);
        }

        private static Mulberry32Random SetDropRandom(CoreVoyageSession session, uint seed)
        {
            var random = new Mulberry32Random(seed);
            SetField(session, "bossTreasureRandom", random);
            return random;
        }

        private static void SetField(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);

        private static void SetProperty(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);
    }
}
