using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBoardingEnemySchoolTests
    {
        private static readonly string[] Schools = { "fire", "arcane", "thunder", "curse", "tide", "blood" };
        private static readonly string[] ActionNumbers = {
            "RawAttack", "AttackDamage", "PiercingDamage", "AbsorbedDamage", "HealthLost", "GuardGained", "Healed", "SelfDamage",
            "PlayerHealthBefore", "PlayerHealthAfter", "PlayerGuardBefore", "PlayerGuardAfter",
            "EnemyHealthBefore", "EnemyHealthAfter", "EnemyGuardBefore", "EnemyGuardAfter"
        };

        [TestCaseSource(nameof(Schools))]
        public void AuthoredCardOrderRepeatsWhileResourcesRemainBoundedAcrossSixCycles(string school)
        {
            CoreVoyageSession session = Battle(school);
            CoreBoardingEnemyDeck deck = session.Battle.BoardingEnemy.Deck;
            Assert.That(deck.Turns.Count, Is.EqualTo(4));
            for (int turn = 1; turn <= 24; turn++)
            {
                Assert.That(session.Battle.PhaseTurn, Is.EqualTo(turn));
                session.Battle.PlayerBlock = turn % 3 == 0 ? 100 : 0;
                CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
                CollectionAssert.AreEqual(deck.TurnAt(turn).Cards.Select(card => card.Id),
                    preview.Actions.Select(action => action.CardId), "The script repeats its authored cards, not an unchanging damage total.");
                Assert.That(session.GetEnemyIntent(), Does.Contain(deck.TurnAt(turn).Name));
                AssertPreviewCommitsExactly(session, preview);
                AssertResourceCaps(session.Battle.BoardingEnemy, session.Battle.Profile.BoardingGuard);
                foreach (CoreBoardingEnemyActionResult action in session.Battle.BoardingEnemy.LastActions)
                    AssertResourceCaps(action.ResourcesAfter, session.Battle.Profile.BoardingGuard);
                Assert.That(session.Battle.BoardingEnemy.ThunderChain, Is.Zero, "Chain is limited to the enemy's current action window.");
            }
        }

        private static IEnumerable<TestCaseData> ReactionPreviews()
        {
            foreach (string school in Schools)
                foreach (int guard in new[] { 0, 9, 100 })
                    yield return new TestCaseData(school, guard);
        }

        [TestCaseSource(nameof(ReactionPreviews))]
        public void RepeatedPreviewDoesNotSpendPlayerDebuffsAndMatchesEveryCommittedAction(string school, int guard)
        {
            CoreVoyageSession session = Battle(school, 4);
            session.Battle.PlayerBlock = guard;
            session.Battle.Boarding.ThunderWeakness = 5;
            session.Battle.Boarding.ThunderWeaknessTurns = 2;
            AddMarks(session, 3);
            SetField(session.Battle.Boarding, "CurseWeakTurns", 2);
            SetField(session.Battle.Boarding, "CurseWeakCard", Card("curse_07"));
            SetField(session.Battle.Boarding, "CurseCounterTurns", 2);
            SetField(session.Battle.Boarding, "CurseCounterCard", Card("curse_09"));
            string before = Snapshot(session);
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            for (int query = 0; query < 8; query++)
            {
                CoreBoardingEnemyPreview repeated = session.GetBoardingEnemyPreview();
                Assert.That(DescribeActions(repeated.Actions), Is.EqualTo(DescribeActions(preview.Actions)));
                Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(preview.AttackDamage));
                Assert.That(session.EnemyBoardingPiercingDamage, Is.EqualTo(preview.PiercingDamage));
                session.GetEnemyIntent();
                Assert.That(Snapshot(session), Is.EqualTo(before), "Intent and structured queries must not mutate live resources, effects or RNG.");
            }
            AssertPreviewCommitsExactly(session, preview);
        }

        [TestCaseSource(nameof(Schools))]
        public void StartingAnotherBattleDoesNotCarryAnySchoolResourcesOrCommittedResults(string school)
        {
            CoreVoyageSession session = Battle(school);
            CoreBoardingEnemyState previous = session.Battle.BoardingEnemy;
            foreach (string property in new[] { "PlayerBurnDamage", "PlayerBurnTurns", "SelfBurnDamage", "SelfBurnTurns", "Mana",
                "ThunderChain", "TidePower", "RetainedGuard", "BloodLoss" }) SetProperty(previous, property, 2);
            ((List<int>)GetField(previous, "CurseDurations")).AddRange(new[] { 2, 2 });
            session.Battle.EnemyBoardingHealth = 0;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            StartBattle(session, school);
            CoreBoardingEnemyState fresh = session.Battle.BoardingEnemy;
            Assert.That(fresh, Is.Not.SameAs(previous));
            foreach (string property in new[] { "PlayerBurnDamage", "PlayerBurnTurns", "SelfBurnDamage", "SelfBurnTurns", "Mana",
                "ThunderChain", "CurseMarks", "TidePower", "RetainedGuard", "BloodLoss", "LastAttackHealthLost", "LastBurnHealthLost" })
                Assert.That(typeof(CoreBoardingEnemyState).GetProperty(property).GetValue(fresh), Is.EqualTo(0), property);
            Assert.That(fresh.LastAttackExecuted, Is.False);
            Assert.That(fresh.LastActions, Is.Empty);
        }

        [Test]
        public void FireSelfBurnDecaysTransfersAndIsConsumedBeforeTheFollowingPlayerEnd()
        {
            CoreVoyageSession session = Battle("fire");
            SetDeck(session, "fire", Turn(Effect(CoreBoardingEnemyMechanic.FireSelfBurn, resource: 9)),
                Turn(Effect(CoreBoardingEnemyMechanic.FireTransfer, resource: 4),
                    Effect(CoreBoardingEnemyMechanic.FireConsume, resource: 3, attack: 50, bonus: 10)), Turn(Effect()));
            int enemy = session.Battle.EnemyBoardingHealth, player = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(session.Battle.BoardingEnemy.SelfBurnDamage, Is.EqualTo(8));
            Assert.That(session.Battle.BoardingEnemy.SelfBurnTurns, Is.EqualTo(8));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 9));
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.Actions[0].ResourcesAfter.SelfBurnDamage, Is.EqualTo(4));
            Assert.That(preview.Actions[0].ResourcesAfter.PlayerBurnDamage, Is.EqualTo(4));
            Assert.That(preview.Actions[0].ResourcesAfter.PlayerBurnTurns, Is.EqualTo(4), "Transferred layers join the target's decaying stack.");
            Assert.That(preview.Actions[1].RawAttack, Is.EqualTo(16)); // 20 * (50% + 3 * 10%).
            Assert.That(preview.Actions[1].ResourcesAfter.PlayerBurnDamage, Is.EqualTo(1));
            AssertPreviewCommitsExactly(session, preview);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 13));
            Assert.That(session.Battle.BoardingEnemy.SelfBurnDamage, Is.EqualTo(3));
            Assert.That(session.Battle.BoardingEnemy.SelfBurnTurns, Is.EqualTo(3));
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(player - 17));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnDamage, Is.Zero);
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnTurns, Is.Zero);
        }

        [Test]
        public void RepeatedIgnitionsAddDirectlyAndDoNotTickOnTheirApplicationTurn()
        {
            CoreVoyageSession session = Battle("fire");
            SetDeck(session, "fire", Turn(new CoreBoardingEnemyCard("ignite-a", "点燃一", attackPercent: 10, burnPercent: 100),
                new CoreBoardingEnemyCard("ignite-b", "点燃二", attackPercent: 10, burnPercent: 100)), Turn(Effect()));
            int health = session.Battle.PlayerBoardingHealth;
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - 4));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(40));
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - 44));
            Assert.That(session.Battle.BoardingEnemy.PlayerBurnDamage, Is.EqualTo(39));
        }

        [Test]
        public void ArcaneManaPersistsCapsAtThreeAndShortPaymentFallsBackWithoutOverdraft()
        {
            CoreVoyageSession session = Battle("arcane");
            CoreBoardingEnemyCard release = Effect(CoreBoardingEnemyMechanic.ArcaneSpend, resource: 5, attack: 50, bonus: 25);
            SetDeck(session, "arcane", Turn(Effect(CoreBoardingEnemyMechanic.ArcaneGain, resource: 9)), Turn(release), Turn(release));
            End(session);
            Assert.That(session.Battle.BoardingEnemy.Mana, Is.EqualTo(3));
            Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(25)); // 20 * (50% + 3 * 25%).
            End(session);
            Assert.That(session.Battle.BoardingEnemy.Mana, Is.Zero);
            Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(10));
            End(session);
            Assert.That(session.Battle.BoardingEnemy.Mana, Is.Zero);
            Assert.That(session.Battle.BoardingEnemy.LastActions.Single().RawAttack, Is.EqualTo(10));
        }

        [Test]
        public void ThunderUsesActionOrderSpendsOnlyExistingChainAndResetsAtEnemyEnd()
        {
            CoreVoyageSession session = Battle("thunder");
            var strikes = Enumerable.Range(0, 8).Select(i => new CoreBoardingEnemyCard("spark-" + i, "电火花", attackPercent: 5,
                mechanic: CoreBoardingEnemyMechanic.ThunderStrike)).ToList();
            strikes.Add(Effect(CoreBoardingEnemyMechanic.ThunderFinisher, resource: 9, bonus: 25));
            SetDeck(session, "thunder", Turn(strikes.ToArray()), Turn(Effect(CoreBoardingEnemyMechanic.ThunderFinisher, resource: 6, attack: 25, bonus: 25)));
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 6, 6, 0 }, preview.Actions.Select(action => action.ResourcesAfter.ThunderChain));
            Assert.That(preview.Actions.Last().AttackDamage, Is.EqualTo(30));
            AssertPreviewCommitsExactly(session, preview);
            Assert.That(session.Battle.BoardingEnemy.ThunderChain, Is.Zero);
            Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(5), "Next turn cannot reuse the previous six chains.");
        }

        [Test]
        public void CurseLayersAddAndDamageAtPlayerEndWhileDetonationConsumesLayers()
        {
            CoreVoyageSession session = Battle("curse");
            SetDeck(session, "curse", Turn(Effect(CoreBoardingEnemyMechanic.CurseMark, resource: 4)),
                Turn(Effect(CoreBoardingEnemyMechanic.CurseMark, resource: 4)), Turn(Effect()),
                Turn(Effect(CoreBoardingEnemyMechanic.CurseDetonate, resource: 1, attack: 50, bonus: 20)));
            int health = session.Battle.PlayerBoardingHealth;
            End(session); Assert.That(session.Battle.BoardingEnemy.CurseMarks, Is.EqualTo(4));
            End(session); Assert.That(session.Battle.BoardingEnemy.CurseMarks, Is.EqualTo(7));
            End(session); Assert.That(session.Battle.BoardingEnemy.CurseMarks, Is.EqualTo(6));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health - 11), "The owner's turn ends tick four and seven layers.");
            session.Battle.PlayerBlock = 100;
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Single().AttackDamage, Is.EqualTo(14));
            Assert.That(preview.Actions.Single().PiercingDamage, Is.EqualTo(14));
            Assert.That(preview.Actions.Single().ResourcesAfter.CurseMarks, Is.EqualTo(4));
            AssertPreviewCommitsExactly(session, preview);
            Assert.That(session.Battle.BoardingEnemy.CurseMarks, Is.EqualTo(4), "Enemy turn end does not age a status on the player.");
            SetDeck(session, "curse", Turn(Effect(CoreBoardingEnemyMechanic.CurseDetonate, resource: 6, attack: 50, bonus: 20)));
            session.Battle.PlayerBlock = 100;
            preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.AttackDamage, Is.EqualTo(22));
            Assert.That(preview.PiercingDamage, Is.EqualTo(22));
            AssertPreviewCommitsExactly(session, preview);
            Assert.That(session.Battle.BoardingEnemy.CurseMarks, Is.Zero);
            session.Battle.PlayerBlock = 100;
            preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.AttackDamage, Is.EqualTo(10));
            Assert.That(preview.PiercingDamage, Is.Zero, "An empty detonation falls back to a blockable base strike.");
            AssertPreviewCommitsExactly(session, preview);
        }

        [Test]
        public void TideGuardActuallyAbsorbsBuildsPowerAndRetainsOnlyOneEnemyWindow()
        {
            CoreVoyageSession session = Battle("tide");
            SetDeck(session, "tide", Turn(Effect(CoreBoardingEnemyMechanic.TideGuard, guard: 120)),
                Turn(Effect(CoreBoardingEnemyMechanic.TideStrike, resource: 8, attack: 60, bonus: 5)), Turn(Effect()));
            End(session);
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(12));
            Assert.That(session.Battle.BoardingEnemy.RetainedGuard, Is.EqualTo(10));
            Play(session, "basic_01");
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(4));
            Assert.That(session.Battle.BoardingEnemy.TidePower, Is.EqualTo(8));
            Assert.That(session.Battle.BoardingEnemy.RetainedGuard, Is.EqualTo(4));
            Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(20)); // 20 * (60% + 8 * 5%).
            End(session);
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(4));
            Assert.That(session.Battle.BoardingEnemy.TidePower, Is.Zero);
            Assert.That(session.Battle.BoardingEnemy.RetainedGuard, Is.Zero);
            End(session);
            Assert.That(session.Battle.EnemyBlock, Is.Zero, "Retained guard cannot persist indefinitely without another guard card.");
        }

        [Test]
        public void TidePowerCountsOnlyBlockedDamageCapsAtTwelveAndExpiresIfUnused()
        {
            CoreVoyageSession session = Battle("tide");
            session.Battle.EnemyBlock = 20;
            int enemy = session.Battle.EnemyBoardingHealth;
            Play(session, "basic_03");
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 15));
            Assert.That(session.Battle.BoardingEnemy.TidePower, Is.EqualTo(12));
            Play(session, "basic_01");
            Assert.That(session.Battle.BoardingEnemy.TidePower, Is.EqualTo(12), "Unblocked health loss does not generate tide power.");
            SetDeck(session, "tide", Turn(Effect()));
            End(session);
            Assert.That(session.Battle.BoardingEnemy.TidePower, Is.Zero);
        }

        [TestCase(0, 50, 0)] [TestCase(3, 50, 2)] [TestCase(6, 50, 5)] [TestCase(12, 98, 2)]
        public void TideHealingPaysActualPowerAndClampsToMissingHealth(int power, int health, int healed)
        {
            CoreVoyageSession session = Battle("tide");
            SetDeck(session, "tide", Turn(Effect(CoreBoardingEnemyMechanic.TideHeal, resource: 6, heal: 25)));
            SetProperty(session.Battle.BoardingEnemy, "TidePower", power);
            session.Battle.EnemyMaxBoardingHealth = 100; session.Battle.EnemyBoardingHealth = health;
            End(session);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(health + healed));
            Assert.That(session.Battle.BoardingEnemy.LastActions.Single().ResourcesAfter.TidePower, Is.EqualTo(Math.Max(0, power - 6)));
            Assert.That(session.Battle.BoardingEnemy.TidePower, Is.Zero);
        }

        [Test]
        public void BloodResourceUsesActualHealthLossCapsPersistsAndIsConsumedByItsAttack()
        {
            CoreVoyageSession session = Battle("blood");
            session.Battle.EnemyBlock = 20;
            Play(session, "basic_01");
            Assert.That(session.Battle.BoardingEnemy.BloodLoss, Is.Zero);
            Play(session, "basic_03");
            Assert.That(session.Battle.BoardingEnemy.BloodLoss, Is.EqualTo(12));
            SetDeck(session, "blood", Turn(Effect()), Turn(Effect(CoreBoardingEnemyMechanic.BloodStrike, resource: 9, attack: 50, bonus: 10)));
            End(session);
            Assert.That(session.Battle.BoardingEnemy.BloodLoss, Is.EqualTo(12));
            Assert.That(session.EnemyBoardingAttackDamage, Is.EqualTo(28));
            End(session);
            Assert.That(session.Battle.BoardingEnemy.BloodLoss, Is.EqualTo(3));
        }

        [TestCase(1, 0, 15)] [TestCase(2, 1, 17)] [TestCase(4, 3, 21)] [TestCase(41, 3, 21)]
        public void BloodLifePaymentLeavesOneAndFeedsTheAttackBeforeDesperateCheck(int health, int cost, int attack)
        {
            CoreVoyageSession session = Battle("blood");
            SetDeck(session, "blood", Turn(new CoreBoardingEnemyCard("blood-payment", "先支付后强化",
                attackPercent: 50, selfDamagePercent: 15, mechanic: CoreBoardingEnemyMechanic.BloodStrike,
                resourceAmount: 6, bonusPercent: 10)));
            session.Battle.EnemyMaxBoardingHealth = 100; session.Battle.EnemyBoardingHealth = health;
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Single().SelfDamage, Is.EqualTo(cost));
            Assert.That(preview.AttackDamage, Is.EqualTo(attack));
            AssertPreviewCommitsExactly(session, preview);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(health - cost));
            Assert.That(session.Battle.BoardingEnemy.BloodLoss, Is.Zero);
        }

        [TestCase(17, 50, 0)] [TestCase(16, 50, 1)] [TestCase(0, 50, 4)] [TestCase(0, 99, 1)] [TestCase(0, 100, 0)]
        public void BloodHarvestCannotHealMoreThanActualPenetrationOrMissingHealth(int guard, int health, int healed)
        {
            CoreVoyageSession session = Battle("blood", 3);
            session.Battle.EnemyMaxBoardingHealth = 100; session.Battle.EnemyBoardingHealth = health;
            session.Battle.PlayerBlock = guard;
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Single().HealthLost, Is.EqualTo(Math.Max(0, 17 - guard)));
            Assert.That(preview.Actions.Single().Healed, Is.EqualTo(healed));
            AssertPreviewCommitsExactly(session, preview);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(health + healed));
        }

        [Test]
        public void MultiActionAttackConsumesThunderWeaknessOnceButTriggersEveryCurseCounter()
        {
            CoreVoyageSession session = Battle("thunder");
            SetDeck(session, "thunder", Turn(Effect(CoreBoardingEnemyMechanic.ThunderStrike, attack: 100),
                Effect(CoreBoardingEnemyMechanic.ThunderStrike, attack: 100),
                Effect(CoreBoardingEnemyMechanic.ThunderFinisher, resource: 6, attack: 10, bonus: 10)));
            session.Battle.Boarding.ThunderWeakness = 5; session.Battle.Boarding.ThunderWeaknessTurns = 2;
            AddMarks(session, 3);
            SetField(session.Battle.Boarding, "CurseCounterTurns", 2);
            SetField(session.Battle.Boarding, "CurseCounterCard", Card("curse_09"));
            int enemy = session.Battle.EnemyBoardingHealth;
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            CollectionAssert.AreEqual(new[] { 15, 20, 6 }, preview.Actions.Select(action => action.AttackDamage));
            AssertPreviewCommitsExactly(session, preview);
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 21), "Three six-point counters precede the three-point natural curse tick.");
            Assert.That(session.Battle.Boarding.ThunderWeaknessTurns, Is.Zero);
        }

        [Test]
        public void PreviewDecaysPlayerSelfBurnBeforeCalculatingEveryFireRetaliation()
        {
            CoreVoyageSession session = Battle("thunder");
            SetDeck(session, "thunder", Turn(Effect(CoreBoardingEnemyMechanic.ThunderStrike, attack: 25),
                Effect(CoreBoardingEnemyMechanic.ThunderStrike, attack: 25),
                Effect(CoreBoardingEnemyMechanic.ThunderFinisher, resource: 6, attack: 25, bonus: 10)));
            session.Battle.Boarding.SelfBurn.Add(new CoreBoardingTimedStacks(3, 1));
            session.Battle.Boarding.FireRetaliations = 1;
            session.Battle.PlayerBlock = 100;
            int enemy = session.Battle.EnemyBoardingHealth, player = session.Battle.PlayerBoardingHealth;
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Count, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { enemy - 4, enemy - 8, enemy - 12 }, preview.Actions.Select(action => action.EnemyHealthAfter));
            string expected = DescribeActions(preview.Actions);
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(player - 3));
            Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(enemy - 12));
            Assert.That(DescribeActions(session.Battle.BoardingEnemy.LastActions), Is.EqualTo(expected));
        }

        [Test]
        public void PreviewResolvesScheduledPlayerHealingBeforeHostileBurnAndEnemyActions()
        {
            CoreVoyageSession session = Battle("fire");
            SetDeck(session, "fire", Turn(Effect(attack: 10)));
            session.Battle.PlayerBoardingHealth = 2;
            session.Battle.Boarding.HealingTicks.Add(new CoreBoardingHealingTick { Amount = 5, Remaining = 1 });
            SetProperty(session.Battle.BoardingEnemy, "PlayerBurnDamage", 4);
            SetProperty(session.Battle.BoardingEnemy, "PlayerBurnTurns", 1);
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.PlayerBurnDamage, Is.EqualTo(4));
            Assert.That(preview.Actions.Single().PlayerHealthBefore, Is.EqualTo(3));
            Assert.That(preview.Actions.Single().PlayerHealthAfter, Is.EqualTo(1));
            string expected = DescribeActions(preview.Actions);
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(1));
            Assert.That(DescribeActions(session.Battle.BoardingEnemy.LastActions), Is.EqualTo(expected));
        }

        [Test]
        public void CommittedIntentMatchesTheShownPreviewWithoutBudgetingHostileBurnTwice()
        {
            CoreVoyageSession session = Battle("fire");
            SetDeck(session, "fire", Turn(Effect(attack: 5)));
            session.Battle.PlayerBoardingHealth = 5;
            SetProperty(session.Battle.BoardingEnemy, "PlayerBurnDamage", 3);
            SetProperty(session.Battle.BoardingEnemy, "PlayerBurnTurns", 2);
            string intent = session.GetEnemyIntent();
            Assert.That(intent, Does.Contain("船员伤害 1"));
            Assert.That(intent, Does.Not.Contain("敌方无法行动"));
            End(session);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(1));
            Assert.That(session.Battle.BoardingEnemy.LastBurnHealthLost, Is.EqualTo(3));
            Assert.That(session.Battle.BoardingEnemy.LastAttackHealthLost, Is.EqualTo(1));
            Assert.That(session.Battle.LastAction, Does.Contain(intent),
                "Re-reading a player-end-inclusive preview after applying that end would incorrectly suppress the scheduled attack.");
        }

        [TestCase(false)] [TestCase(true)]
        public void DeathAfterFirstActionStopsAllRemainingAttacksAndResourceGains(bool counterKillsEnemy)
        {
            CoreVoyageSession session = Battle("thunder");
            SetDeck(session, "thunder", Turn(Effect(CoreBoardingEnemyMechanic.ThunderStrike, attack: 100),
                Effect(CoreBoardingEnemyMechanic.ArcaneGain, resource: 3), Effect(CoreBoardingEnemyMechanic.ThunderStrike, attack: 100)));
            if (counterKillsEnemy)
            {
                session.Battle.EnemyBoardingHealth = 4;
                AddMarks(session, 2);
                SetField(session.Battle.Boarding, "CurseCounterTurns", 2);
                SetField(session.Battle.Boarding, "CurseCounterCard", Card("curse_09"));
            }
            else session.Battle.PlayerBoardingHealth = 3;
            CoreBoardingEnemyState state = session.Battle.BoardingEnemy;
            CoreBoardingEnemyPreview preview = session.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Count, Is.EqualTo(1));
            Assert.That(preview.Actions[0].HealthLost, Is.EqualTo(counterKillsEnemy ? 0 : 3));
            End(session);
            Assert.That(state.LastActions.Count, Is.EqualTo(1));
            Assert.That(state.Mana, Is.Zero, "The later setup action must not run after either combatant dies.");
            Assert.That(DescribeActions(state.LastActions), Is.EqualTo(DescribeActions(preview.Actions)));
            Assert.That(session.Phase, Is.EqualTo(counterKillsEnemy ? CoreDemoPhase.Sailing : CoreDemoPhase.Settlement));
        }

        private static void AssertPreviewCommitsExactly(CoreVoyageSession session, CoreBoardingEnemyPreview preview)
        {
            int health = session.Battle.PlayerBoardingHealth;
            string expected = DescribeActions(preview.Actions);
            CoreBoardingEnemyState state = session.Battle.BoardingEnemy;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(DescribeActions(state.LastActions), Is.EqualTo(expected), "Every card's damage, guard, healing and resource snapshot must match the preview.");
            Assert.That(session.Battle, Is.Not.Null, "The high-health fixture must survive the complete scripted turn.");
            Assert.That(health - session.Battle.PlayerBoardingHealth, Is.EqualTo(preview.PlayerHealthLoss + preview.PlayerBurnDamage + preview.PlayerCurseDamage));
        }

        private static void AssertResourceCaps(CoreBoardingEnemyState state, int guard)
        {
            Assert.That(state.PlayerBurnDamage, Is.GreaterThanOrEqualTo(0));
            Assert.That(state.PlayerBurnTurns, Is.EqualTo(state.PlayerBurnDamage));
            Assert.That(state.SelfBurnDamage, Is.GreaterThanOrEqualTo(0));
            Assert.That(state.SelfBurnTurns, Is.EqualTo(state.SelfBurnDamage));
            Assert.That(state.Mana, Is.InRange(0, 3));
            Assert.That(state.ThunderChain, Is.InRange(0, 6));
            Assert.That(state.CurseMarks, Is.GreaterThanOrEqualTo(0));
            Assert.That(state.TidePower, Is.InRange(0, 12));
            Assert.That(state.RetainedGuard, Is.InRange(0, guard));
            Assert.That(state.BloodLoss, Is.InRange(0, 12));
        }

        private static string DescribeActions(IEnumerable<CoreBoardingEnemyActionResult> actions) => string.Join(";", actions.Select(action =>
            action.CardId + ":" + action.Name + ":" + string.Join(",", ActionNumbers.Select(name =>
                typeof(CoreBoardingEnemyActionResult).GetProperty(name).GetValue(action))) + ":" + ResourceSnapshot(action.ResourcesAfter)));

        private static string ResourceSnapshot(CoreBoardingEnemyState state) => string.Join(",", typeof(CoreBoardingEnemyState)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property => property.PropertyType == typeof(int) || property.PropertyType == typeof(bool))
            .OrderBy(property => property.Name).Select(property => property.Name + "=" + property.GetValue(state)));

        private static string Snapshot(CoreVoyageSession session) => string.Join("|", new object[] {
            ResourceSnapshot(session.Battle.BoardingEnemy), DescribeActions(session.Battle.BoardingEnemy.LastActions),
            session.Battle.PhaseTurn, session.Battle.Turn, session.Battle.PlayerBoardingHealth, session.Battle.EnemyBoardingHealth,
            session.Battle.PlayerBlock, session.Battle.EnemyBlock, session.Battle.Boarding.Revision,
            session.Battle.Boarding.ThunderWeakness, session.Battle.Boarding.ThunderWeaknessTurns,
            session.Battle.Boarding.CurseMarks, GetField(session.Battle.Boarding, "CurseWeakTurns"),
            GetField(session.Battle.Boarding, "CurseCounterTurns"), GetField(session.Battle.Boarding, "CurseCounterTriggered"),
            ((Mulberry32Random)GetField(session, "voyageRandom")).State,
            string.Join(",", session.Battle.Hand), string.Join(",", session.Battle.DrawPile), string.Join(",", session.Battle.DiscardPile),
            session.Battle.LastAction, string.Join(";", session.LogEntries)
        });

        private static CoreVoyageSession Battle(string school, int turn = 1)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.SetSeed("BOARDING-ENEMY-SCHOOL-TEST");
            session.StartVoyage();
            StartBattle(session, school);
            // These fixtures exercise the reusable school mechanics, while the
            // dedicated boss tests cover encounter-specific scripts and pacing.
            SetProperty(session.Battle.BoardingEnemy, "Deck", CoreBoardingEnemyCatalog.ForSchool(school));
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(session.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(session.Battle, session.Battle.EnemyHeadingDegrees);
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                10000, 20, 20, 79, 0, 28, 1, 1, healthMultiplier: 1, boardingGuard: 10);
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 10000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.PhaseTurn = turn;
            session.Cargo.Curse = 0;
            SetProperty(session, "Morale", 0);
            return session;
        }

        private static void StartBattle(CoreVoyageSession session, string school) => typeof(CoreVoyageSession)
            .GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { CoreRouteBossCatalog.All.Single(boss => boss.SchoolId == school).CreateEncounter(), true });

        private static CoreBattleCard Card(string key) => CoreBoardingCardCatalog.FindByKey(key);
        private static CoreBoardingEnemyCard Effect(CoreBoardingEnemyMechanic mechanic = CoreBoardingEnemyMechanic.Basic,
            int resource = 0, int attack = 0, int guard = 0, int heal = 0, int bonus = 0) =>
            new CoreBoardingEnemyCard("test-" + mechanic, mechanic.ToString(), attackPercent: attack, guardPercent: guard,
                healPercent: heal, mechanic: mechanic, resourceAmount: resource, bonusPercent: bonus);
        private static CoreBoardingEnemyTurn Turn(params CoreBoardingEnemyCard[] cards) => new CoreBoardingEnemyTurn("机制测试", cards);
        private static void SetDeck(CoreVoyageSession session, string school, params CoreBoardingEnemyTurn[] turns) =>
            SetProperty(session.Battle.BoardingEnemy, "Deck", new CoreBoardingEnemyDeck(school, turns));
        private static void End(CoreVoyageSession session) => Assert.That(session.EndBattleTurn(), Is.True);
        private static void Play(CoreVoyageSession session, string key)
        {
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key));
            session.Battle.Energy = 100;
            Assert.That(session.PlayCard(0), Is.True, key + ": " + session.CardUnavailableReason(0));
        }
        private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object GetField(object target, string name) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void AddMarks(CoreVoyageSession session, int amount) => typeof(CoreVoyageSession)
            .GetMethod("CTBAddMarks", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { amount });
    }
}
