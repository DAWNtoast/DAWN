using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCurseBoardingRevisionTests
    {
        private CoreBattleState battle;
        [TestCase(20, 19, false)]
        [TestCase(20, 20, true)]
        [TestCase(20, 21, true)]
        [TestCase(21, 20, false)]
        [TestCase(21, 21, true)]
        [TestCase(21, 22, true)]
        public void ApplyingMarksUsesInclusiveCurrentHealthThreshold(int health, int marks, bool executed)
        {
            var s = Battle(); battle.EnemyBoardingHealth = health; battle.EnemyBlock = 500;
            AddMarks(s, marks);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(executed ? 0 : health));
            Assert.That(battle.EnemyBlock, Is.EqualTo(500));
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.EqualTo(executed));
        }

        [TestCase(20, 19, false)]
        [TestCase(20, 20, true)]
        [TestCase(20, 21, true)]
        [TestCase(21, 20, false)]
        [TestCase(21, 21, true)]
        [TestCase(21, 22, true)]
        public void PlayerMarksExecuteAtOrAboveAllRemainingHealth(int health, int marks, bool executed)
        {
            var s = Battle(); battle.PlayerBoardingHealth = health; battle.PlayerBlock = 500;
            AddPlayerMarks(s, marks);
            Invoke(s, "BResolveCurseExecutions");
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(executed ? 0 : health));
            Assert.That(battle.PlayerBlock, Is.EqualTo(500), "Execution must not consume guard.");
            Assert.That(battle.Boarding.PlayerExecutedByCurse, Is.EqualTo(executed));
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(1000));
        }

        [Test]
        public void OrdinaryDamageImmediatelyExecutesAndReportsOnlyActualDamage()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 20; AddMarks(s, 19);
            Assert.That((int)Invoke(s, "BDeal", 1, false, false), Is.EqualTo(1));
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.True);
        }

        [Test]
        public void BlockedDamageCannotLowerExecutionThreshold()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 20; AddMarks(s, 19); battle.EnemyBlock = 10;
            Assert.That((int)Invoke(s, "BDeal", 10, false, false), Is.Zero);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(20));
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.False);
        }

        [Test]
        public void MarksPersistAcrossBothTurnEndsWithoutDamage()
        {
            var s = Battle(); AddMarks(s, 7); AddPlayerMarks(s, 7);
            for (int i = 0; i < 4; i++) Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.Boarding.CurseMarks, Is.EqualTo(7));
            Assert.That(battle.BoardingEnemy.CurseMarks, Is.EqualTo(7));
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(1000));
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(1000));
        }

        [Test]
        public void CurseSupportThresholdsDoNotConsumeMarks()
        {
            var s = Battle(); AddMarks(s, 4);
            Play(s, "curse_14"); Assert.That(battle.Energy, Is.EqualTo(102));
            Play(s, "curse_15"); Assert.That(battle.PlayerBlock, Is.EqualTo(20));
            battle.PlayerBoardingHealth = 900; s.Cargo.Medicine = 1;
            Play(s, "curse_12");
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(926));
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Assert.That(battle.Boarding.CurseMarks, Is.EqualTo(4));
            Play(s, "curse_13"); Assert.That(battle.Boarding.CurseMarks, Is.EqualTo(2));
        }

        [Test]
        public void MedicineWithoutEnoughMarksFallsBackToFreeBasicHealing()
        {
            var s = Battle(); AddMarks(s, 1); battle.PlayerBoardingHealth = 900; s.Cargo.Medicine = 1;
            Play(s, "curse_12");
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(902));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(1));
            Assert.That(battle.Boarding.CurseMarks, Is.EqualTo(1));
        }

        [Test]
        public void FinalePaysMarksBeforePiercingDamage()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 100; AddMarks(s, 6); battle.EnemyBlock = 100;
            Play(s, "curse_11");
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(12));
            Assert.That(battle.EnemyBlock, Is.EqualTo(100));
            Assert.That(battle.Boarding.CurseMarks, Is.Zero);
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.False);
        }

        [Test]
        public void EntanglementStacksAcrossTurnsAndTwentiethCopyExecutesWithoutMarks()
        {
            var s = Battle();
            Assert.That(battle.Schools.EnemyCurseExecutionThresholdPercent, Is.EqualTo(100));
            for (int i = 1; i <= 19; i++)
            {
                Play(s, "curse_10");
                Assert.That(battle.Schools.EnemyCurseExecutionThresholdPercent, Is.EqualTo(100 - i * 5));
                Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(1000), "No marks: the first nineteen casts must not execute.");
                Assert.That(battle.Boarding.CurseMarks, Is.Zero);
                Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.False);
                Assert.That(s.EndBattleTurn(), Is.True);
            }
            Play(s, "curse_10");
            Assert.That(battle.Schools.EnemyCurseExecutionThresholdPercent, Is.Zero);
            Assert.That(battle.Boarding.CurseMarks, Is.Zero, "Zero threshold executes even with no marks.");
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.True);
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(s.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
        }

        [TestCase(94, false)]
        [TestCase(95, true)]
        [TestCase(96, true)]
        public void LoweringThresholdImmediatelyRechecksExistingMarksWithInclusiveEquality(int marks, bool executed)
        {
            var s = Battle(); battle.EnemyBoardingHealth = 100; AddMarks(s, marks);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(100), "Both fixtures survive the initial 100% threshold.");
            Play(s, "curse_10");
            Assert.That(battle.Schools.EnemyCurseExecutionThresholdPercent, Is.EqualTo(95));
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(executed ? 0 : 100));
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.EqualTo(executed));
            Assert.That(battle.Boarding.CurseMarks, Is.EqualTo(marks));
        }

        [TestCase(20, 4)]
        [TestCase(25, 5)]
        [TestCase(70, 14)]
        [TestCase(22, 5)]
        public void ExecutionUsesTheConfiguredThresholdRatherThanAFixedPlayCount(int initial, int casts)
        {
            var s = Battle(); Set(battle.Schools, "EnemyCurseExecutionThresholdPercent", initial);
            for (int i = 1; i < casts; i++)
            {
                Play(s, "curse_10");
                Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(1000));
            }
            Play(s, "curse_10");
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.Schools.EnemyCurseExecutionThresholdPercent, Is.EqualTo(initial - casts * CoreCurseRules.EntangleThresholdReductionPercent));
        }

        [TestCase("blood_01", 11, 5)]
        public void LifePaymentExecutionStopsTheCardBeforeEffects(string key, int health, int marks)
        {
            var s = Battle(); battle.PlayerBoardingHealth = health; AddPlayerMarks(s, marks);
            int enemy = battle.EnemyBoardingHealth;
            Play(s, key);
            Assert.That(battle.PlayerBoardingHealth, Is.Zero);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            Assert.That(battle.Boarding.SelfBurnStacks, Is.Zero);
            Assert.That(battle.Energy, Is.EqualTo(100 - Card(key).Energy));
        }

        [Test]
        public void FireIgnitionDoesNotLowerHealthIntoCurseExecution()
        {
            var s = Battle(); battle.PlayerBoardingHealth = 5; AddPlayerMarks(s, 1);
            Play(s, "fire_14");
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(5));
            Assert.That(battle.Boarding.SelfBurnStacks, Is.EqualTo(2));
            Assert.That(battle.Energy, Is.GreaterThan(100 - Card("fire_14").Energy));
        }

        [Test]
        public void HealingCannotRescueAnAlreadyExecutedState()
        {
            var s = Battle(); battle.PlayerBoardingHealth = 5; AddPlayerMarks(s, 5);
            Assert.That((int)Invoke(s, "BHeal", 100), Is.Zero);
            Assert.That(battle.PlayerBoardingHealth, Is.Zero);
        }

        [TestCase(20, 5)]
        [TestCase(25, 10)]
        public void MissileAggregationStopsOnTheThirdHitAtExactThreshold(int health, int marks)
        {
            var s = Battle(); battle.EnemyBoardingHealth = health; AddMarks(s, marks);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(health), "The target must survive until missile hits lower its health to the mark count.");
            Set(battle.Schools, "Mana", 20);
            Play(s, "arcane_06");
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.True);
            Assert.That(battle.Boarding.ActiveAuthoredDamage, Is.EqualTo(15), "Only three 5-damage hits land; equality must stop the fourth hit.");
            Assert.That(battle.Schools.Mana, Is.Zero, "Mana is paid before the missile sequence starts.");
        }

        [Test]
        public void EnemyMarkExecutesBeforeItsAttackAndBeforeLaterCards()
        {
            var s = Battle(); battle.PlayerBoardingHealth = 6; battle.PlayerBlock = 100;
            SetDeck(s, new CoreBoardingEnemyCard("mark", "咒印", attackPercent: 50,
                mechanic: CoreBoardingEnemyMechanic.CurseMark, resourceAmount: 6), Attack(100));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Count, Is.EqualTo(1));
            Assert.That(preview.PlayerExecutedByCurse, Is.True);
            Assert.That(preview.Actions[0].AttackDamage, Is.Zero);
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(6));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.PlayerBoardingHealth, Is.Zero);
            Assert.That(battle.PlayerBlock, Is.EqualTo(100));
            Assert.That(battle.BoardingEnemy.LastActions.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnemyAttackExecutionStopsLaterAttacksAndMatchesPreview()
        {
            var s = Battle(); battle.PlayerBoardingHealth = 8; AddPlayerMarks(s, 4);
            SetDeck(s, Attack(20), Attack(100));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Count, Is.EqualTo(1));
            Assert.That(preview.Actions[0].HealthLost, Is.EqualTo(4));
            Assert.That(preview.Actions[0].PlayerHealthAfter, Is.Zero);
            Assert.That(preview.PlayerExecutedByCurse, Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.BoardingEnemy.LastAttackHealthLost, Is.EqualTo(4));
            Assert.That(battle.BoardingEnemy.LastActions.Single().PlayerHealthAfter, Is.Zero);
        }

        [Test]
        public void EnemySelfPaymentExecutionPreventsHealingAndAttack()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 20; AddMarks(s, 17);
            SetDeck(s, new CoreBoardingEnemyCard("pay", "献血", attackPercent: 100, healPercent: 100, selfDamagePercent: 15));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.EnemyExecutedByCurse, Is.True);
            Assert.That(preview.Actions.Single().SelfDamage, Is.EqualTo(3));
            Assert.That(preview.Actions.Single().Healed, Is.Zero);
            Assert.That(preview.AttackDamage, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(1000));
        }

        [Test]
        public void CounterDamageCanTriggerExecutionBeforeEnemyAttack()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 24; AddMarks(s, 8); Play(s, "curse_09");
            SetDeck(s, Attack(100), Attack(100));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.EnemyExecutedByCurse, Is.True);
            Assert.That(preview.Actions.Count, Is.EqualTo(1));
            Assert.That(preview.AttackDamage, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(1000));
        }

        [Test]
        public void ContractMarksStopTheNextEnemyCardImmediately()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 8; Play(s, "curse_04");
            SetDeck(s, Attack(5), Attack(5), Attack(5));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Count, Is.EqualTo(2));
            Assert.That(preview.EnemyExecutedByCurse, Is.True);
            Assert.That(preview.AttackDamage, Is.EqualTo(2));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.BoardingEnemy.LastActions.Count, Is.EqualTo(2));
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(998));
        }

        [Test]
        public void PlayerBurnExecutionOccursBeforeRecoveryOfEnemyTurnAndReactions()
        {
            var s = Battle(); battle.PlayerBoardingHealth = 8; AddPlayerMarks(s, 5);
            Set(battle.BoardingEnemy, "PlayerBurnDamage", 3); Play(s, "curse_04"); SetDeck(s, Attack(100));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.PlayerBurnDamage, Is.EqualTo(3));
            Assert.That(preview.PlayerExecutedByCurse, Is.True);
            Assert.That(preview.Actions, Is.Empty);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.PlayerBoardingHealth, Is.Zero);
            Assert.That(battle.Boarding.CurseMarks, Is.Zero);
        }

        [Test]
        public void EnemyBurnDamageImmediatelyTriggersExecutionAtEnemyEnd()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 20; AddMarks(s, 17);
            Set(battle.BoardingEnemy, "SelfBurnDamage", 3);
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.EnemySelfBurnDamage, Is.EqualTo(3));
            Assert.That(preview.EnemyExecutedByCurse, Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.BoardingEnemy.LastSelfBurnHealthLost, Is.EqualTo(3));
        }

        [Test]
        public void PureExecutionReportDoesNotCallRemovedHealthDamage()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 6;
            Play(s, "curse_02");
            string report = s.LogEntries.Last(line => line.Contains("打出「"));
            Assert.That(report, Does.Contain("咒印消灭敌方"));
            Assert.That(report, Does.Not.Contain("伤害"));
        }

        [Test]
        public void DamageExecutionReportCountsOnlyTheHitAndStopsFollowupDraw()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 17; AddMarks(s, 9);
            Play(s, "basic_11");
            Assert.That(s.LogEntries.Any(line => line.Contains("伤害 8；咒印消灭敌方")), Is.True);
            Assert.That(battle.Hand, Is.Empty);
        }

        [Test]
        public void DamageExecutionStopsFollowupManaGain()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 15; AddMarks(s, 3); Set(battle.Schools, "Mana", 0);
            Play(s, "arcane_01");
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.Schools.Mana, Is.Zero);
        }

        [Test]
        public void DelayedAttackRecordsActualDamageAndZeroFinalHealthSeparately()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 30; AddMarks(s, 6);
            Play(s, "basic_13"); Assert.That(s.EndBattleTurn(), Is.True);
            CoreBoardingResolvedAttack attack = battle.Boarding.ResolvedAttacks.Single();
            Assert.That(attack.HealthBefore, Is.EqualTo(30));
            Assert.That(attack.HealthAfter, Is.Zero);
            Assert.That(attack.HealthLost, Is.EqualTo(24));
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.True);
        }

        [Test]
        public void DamageExecutionStopsFollowupDebuffAndChain()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 13; AddMarks(s, 3);
            Play(s, "thunder_12");
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.Boarding.ThunderWeaknessTurns, Is.Zero);
            Assert.That(battle.Schools.ThunderChain, Is.Zero);
        }

        [Test]
        public void LinkedCurseExecutionStopsThunderChainAfterTheAttackSurvives()
        {
            var s = Battle(); battle.EnemyBoardingHealth = 10; AddMarks(s, 2);
            Play(s, "curse_05");
            Play(s, "thunder_01");
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.Boarding.CurseMarks, Is.EqualTo(4));
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.True);
            Assert.That(battle.Schools.ThunderChain, Is.Zero);
        }

        [TestCase(false, 2)]
        [TestCase(true, 4)]
        public void EnemyDrainHealsActualHitLossWithoutIncludingCurseExecutionRemainder(bool curse, int healed)
        {
            var s = Battle(); battle.PlayerBoardingHealth = curse ? 8 : 2; battle.EnemyBoardingHealth = 900;
            if (curse) AddPlayerMarks(s, 4);
            SetDeck(s, new CoreBoardingEnemyCard("drain", "汲血", attackPercent: 20, drainPercent: 100));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Single().Healed, Is.EqualTo(healed));
            Assert.That(preview.PlayerExecutedByCurse, Is.EqualTo(curse));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(900 + healed));
            Assert.That(battle.BoardingEnemy.LastActions.Single().Healed, Is.EqualTo(healed));
        }

        private CoreVoyageSession Battle()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("CURSE-REVISION-BOARDING"); s.StartVoyage();
            Invoke(s, "StartBattle", CoreRouteBossCatalog.All.Single(b => b.SchoolId == "curse").CreateEncounter(), true);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000; s.Battle.DistanceMeters = 100f;
            Set(s.Battle, "EnemyHeadingDegrees", 90f); Set(s.Battle, "PlayerHeadingDegrees", 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 20, 20, 0, 0, 28, 1, 1, healthMultiplier: 1, boardingGuard: 10);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Cargo.Curse = 0; s.Cargo.Medicine = 0; Set(s, "Morale", 0);
            SetDeck(s, new CoreBoardingEnemyCard("idle", "等待"));
            battle = s.Battle; return s;
        }

        private static CoreBoardingEnemyCard Attack(int percent) => new CoreBoardingEnemyCard("attack", "攻击", attackPercent: percent);
        private static void SetDeck(CoreVoyageSession s, params CoreBoardingEnemyCard[] cards) =>
            Set(s.Battle.BoardingEnemy, "Deck", new CoreBoardingEnemyDeck("curse", new[] { new CoreBoardingEnemyTurn("测试", cards) }));
        private static CoreBattleCard Card(string key) => CoreBoardingCardCatalog.FindByKey(key);
        private static void Play(CoreVoyageSession s, string key)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_" + key)); s.Battle.Energy = 100;
            Assert.That(s.PlayCard(0), Is.True, key);
        }
        private static void AddMarks(CoreVoyageSession s, int amount) => Invoke(s, "CTBAddMarks", amount);
        private static void AddPlayerMarks(CoreVoyageSession s, int amount) =>
            ((List<int>)Field(s.Battle.BoardingEnemy, "CurseDurations")).AddRange(Enumerable.Repeat(1, amount));
        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static object Invoke(CoreVoyageSession s, string name, params object[] args) => typeof(CoreVoyageSession)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
    }
}
