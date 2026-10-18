using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleIntentPresentationTests
    {
        [Test]
        public void NoBattle_ReturnsNoIntent()
        {
            Assert.That(CoreBattleIntentPresentation.Build(null), Is.Empty);
            Assert.That(CoreBattleIntentPresentation.Build(new CoreVoyageSession()), Is.Empty);
        }

        [Test]
        public void CannonNumber_IsBeforeGuard_AndMatchesExecutionWithoutConsumingPreviewEffects()
        {
            var session = Training(NavalBattleStage.Naval);
            session.Cargo.Curse = 2;
            session.Battle.NavalExpansion.PulseReduction = 4;
            session.Battle.PlayerBlock = 999;
            int hull = session.Hull, turn = session.Battle.Turn, energy = session.Battle.Energy;
            CoreBattleIntent first = CoreBattleIntentPresentation.Build(session).Single(intent => intent.Kind == CoreBattleIntentKind.Cannon);
            CoreBattleIntent second = CoreBattleIntentPresentation.Build(session).Single(intent => intent.Kind == CoreBattleIntentKind.Cannon);
            Assert.That(first.Amount, Is.GreaterThan(0), "The icon is attack damage, never post-guard health loss.");
            Assert.That(second.Value, Is.EqualTo(first.Value));
            Assert.That(first.Tooltip, Does.Contain("装甲后、格挡前"));
            Assert.That(session.Battle.NavalExpansion.PulseReduction, Is.EqualTo(4));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(999));
            Assert.That(session.Battle.Turn, Is.EqualTo(turn));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty);
            session.Battle.PlayerBlock = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(hull - session.Hull, Is.EqualTo(first.Amount.Value));
        }

        [Test]
        public void LethalNavalBurn_HasNoCannonOrRamIntent()
        {
            var session = Training(NavalBattleStage.Naval);
            session.Battle.EnemyHull = 5;
            session.Battle.EnemyBurnDamage = 5;
            session.Battle.EnemyBurnTurns = 2;
            var intents = CoreBattleIntentPresentation.Build(session);
            Assert.That(intents.Count, Is.EqualTo(1));
            Assert.That(intents[0].Kind, Is.EqualTo(CoreBattleIntentKind.Wait));
            Assert.That(intents[0].Value, Is.Empty);
            Assert.That(intents[0].Tooltip, Does.Contain("燃烧"));
            Assert.That(intents.Any(intent => intent.IsAttack), Is.False);
        }

        [Test]
        public void HealingIntent_UsesActualMissingHealthAndHealingBan()
        {
            var session = Training(NavalBattleStage.Naval, 1);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull - 1;
            session.Battle.NavalExpansion.EnemyHealingHalved = true;
            var heal = CoreBattleIntentPresentation.Build(session).Single(intent => intent.Kind == CoreBattleIntentKind.Heal);
            Assert.That(heal.Amount, Is.EqualTo(0));
            int before = session.Battle.EnemyHull;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(before));
        }

        [Test]
        public void ApproachIntent_SeparatesMovementGuardAndLoading_WithoutAnAttack()
        {
            var session = Training(NavalBattleStage.Naval);
            session.Battle.Profile = Profile();
            session.Battle.DistanceMeters = 600f;
            var intents = CoreBattleIntentPresentation.Build(session);
            Assert.That(intents.Select(intent => intent.Kind), Is.EquivalentTo(new[] {
                CoreBattleIntentKind.Move, CoreBattleIntentKind.Guard, CoreBattleIntentKind.Load }));
            Assert.That(intents.Single(intent => intent.Kind == CoreBattleIntentKind.Move).Amount, Is.GreaterThan(0));
            Assert.That(intents.Any(intent => intent.IsAttack), Is.False);
        }

        [Test]
        public void RamIntent_ShowsHullImpactAndTheSeparateCrewOpening()
        {
            var session = Training(NavalBattleStage.Naval);
            session.Battle.Profile = Profile(ramChance: 1d);
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, 90f);
            session.Battle.PlayerBlock = 999;
            Assert.That(session.EnemyWillRam, Is.True);
            var intents = CoreBattleIntentPresentation.Build(session);
            Assert.That(intents.Single(intent => intent.Kind == CoreBattleIntentKind.Ram).Tooltip, Does.Contain("穿透格挡"));
            Assert.That(intents.Single(intent => intent.Id == "enemy_boarding_first_strike").Amount,
                Is.EqualTo(session.EnemyRamFirstStrikeDamage));
            Assert.That(intents.Any(intent => intent.Kind == CoreBattleIntentKind.Cannon), Is.False);
        }

        [Test]
        public void ThunderIntent_KeepsIndividualHits_AndConsumesWeaknessOnlyInThePrediction()
        {
            var session = Boarding("thunder");
            session.Battle.PlayerBlock = 100;
            session.Battle.Boarding.ThunderWeakness = 4;
            session.Battle.Boarding.ThunderWeaknessTurns = 1;
            var attacks = CoreBattleIntentPresentation.Build(session).Where(intent => intent.IsAttack).ToArray();
            Assert.That(attacks.Select(intent => intent.Amount.Value), Is.EqualTo(new[] { 2, 5, 7 }));
            Assert.That(attacks.Select(intent => intent.Sequence), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(attacks.All(intent => intent.Tooltip.Contains("格挡前")), Is.True);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(100));
            Assert.That(session.Battle.Boarding.ThunderWeakness, Is.EqualTo(4));
            Assert.That(session.Battle.Boarding.ThunderWeaknessTurns, Is.EqualTo(1));
            Assert.That(session.Battle.BoardingEnemy.ThunderChain, Is.Zero);
            Assert.That(CoreBattleIntentPresentation.Build(session).Where(intent => intent.IsAttack).Select(intent => intent.Value),
                Is.EqualTo(attacks.Select(intent => intent.Value)));
        }

        [Test]
        public void CounterThatKillsEnemy_OmitsAttacksThatWillNeverExecute()
        {
            var session = Boarding("thunder");
            session.Battle.EnemyBoardingHealth = 6;
            typeof(CoreBoardingBattleState).GetField("TideAmbushTurns", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session.Battle.Boarding, 2);
            var intents = CoreBattleIntentPresentation.Build(session);
            Assert.That(intents.Count(intent => intent.IsAttack), Is.EqualTo(1));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(6));
        }

        [Test]
        public void LethalPlayerEndBurn_OmitsAllEnemyActions()
        {
            var session = Boarding("thunder");
            session.Battle.PlayerBoardingHealth = 2;
            session.Battle.Boarding.SelfBurn.Add(new CoreBoardingTimedStacks(3, 2));
            var intents = CoreBattleIntentPresentation.Build(session);
            Assert.That(intents.Count, Is.EqualTo(1));
            Assert.That(intents[0].Kind, Is.EqualTo(CoreBattleIntentKind.Wait));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(2));
        }

        [TestCase("neutral")]
        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("thunder")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("blood")]
        public void EveryAuthoredSchoolRound_UsesTheResolvedActionsAndUniqueIconIds(string school)
        {
            var session = Boarding(school);
            var deck = session.Battle.BoardingEnemy.Deck;
            for (int turn = 1; turn <= deck.Turns.Count; turn++)
            {
                session.Battle.PhaseTurn = turn;
                CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
                var intents = CoreBattleIntentPresentation.Build(session);
                Assert.That(intents, Is.Not.Empty, school + " turn " + turn);
                Assert.That(intents.Select(intent => intent.Id).Distinct().Count(), Is.EqualTo(intents.Count));
                Assert.That(intents.Where(intent => intent.IsAttack).Select(intent => intent.Amount.Value),
                    Is.EqualTo(preview.Actions.Where(action => action.RawAttack > 0).Select(action => action.AttackDamage)), school + " turn " + turn);
                Assert.That(intents.All(intent => !string.IsNullOrEmpty(intent.Tooltip)), Is.True);
            }
        }

        private static CoreVoyageSession Training(NavalBattleStage stage, int target = 0)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[target]), Is.True);
            session.Battle.Stage = stage;
            session.Battle.DistanceMeters = 200f;
            return session;
        }

        private static CoreVoyageSession Boarding(string school)
        {
            var session = Training(NavalBattleStage.Boarding);
            session.Battle.Profile = Profile();
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 1000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(session.Battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForSchool(school));
            return session;
        }

        private static CoreBattleProfile Profile(double ramChance = 0d) => new CoreBattleProfile(
            CoreBossKind.None, CoreBattleArchetype.Balanced, 1000, 10, 20, 6, 0, 20, 1f, 1f,
            ramChance: ramChance, healthMultiplier: 1m);
    }
}
