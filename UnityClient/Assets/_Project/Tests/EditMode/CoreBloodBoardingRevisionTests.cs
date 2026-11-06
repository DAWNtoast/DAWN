using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBloodBoardingRevisionTests
    {
        [TestCase("blood_11", 401, 14)]
        [TestCase("blood_11", 400, 34)]
        [TestCase("blood_13", 401, 24)]
        [TestCase("blood_13", 400, 44)]
        public void DesperateDamageUsesInclusiveFortyPercentAndMatchesQuote(string key, int health, int damage)
        {
            var s = Battle(); s.Battle.PlayerBoardingHealth = health;
            int before = s.Battle.EnemyBoardingHealth;
            Assert.That(s.GetBoardingCardDamageQuote(Card(key)), Is.EqualTo(damage));
            Play(s, key);
            if (key == "blood_13")
            {
                Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(damage));
                s.Battle.PlayerBoardingHealth = 900;
                Invoke(s, "BPlayerStart");
            }
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(damage));
        }

        [TestCase(401, 10)]
        [TestCase(400, 24)]
        public void DesperateGuardUsesRevisedAmounts(int health, int expected)
        {
            var s = Battle(); s.Battle.PlayerBoardingHealth = health;
            Play(s, "blood_12");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(expected));
        }

        [TestCase("blood_09", 500, 0, 2)]
        [TestCase("blood_09", 400, 0, 5)]
        [TestCase("blood_09", 500, 1, 12)]
        [TestCase("blood_09", 400, 1, 26)]
        [TestCase("card_special_crew_broken_anchor_doctor_hector", 500, 0, 2)]
        [TestCase("card_special_crew_broken_anchor_doctor_hector", 400, 0, 4)]
        [TestCase("card_special_crew_broken_anchor_doctor_hector", 500, 1, 14)]
        [TestCase("card_special_crew_broken_anchor_doctor_hector", 400, 1, 32)]
        public void OrdinaryAndSpecialHealingReadDesperationBeforeAnyRecovery(string key, int health, int medicine, int heal)
        {
            var s = Battle(); s.Battle.PlayerBoardingHealth = health; s.Cargo.Medicine = medicine;
            var quote = s.GetBattleCardEffects(Card(key), NavalBattleStage.Boarding);
            Assert.That(quote.ActualRepair, Is.EqualTo(heal));
            Play(s, key);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(health + heal));
            Assert.That(s.Cargo.Medicine, Is.Zero);
        }

        [Test]
        public void LifePaymentsBypassGuardAndEachEnergyCopyCanBeUsed()
        {
            var s = Battle(); s.Battle.PlayerBlock = 100;
            Play(s, "blood_05"); Play(s, "blood_05");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(988));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(100));
            Assert.That(s.Battle.SuccessfulCardUses("blood_05"), Is.EqualTo(2));
            s.Battle.PlayerBoardingHealth = 3; Put(s, "blood_05");
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(3));
        }

        [Test]
        public void LastResortExhaustsOnlyThePlayedCopy()
        {
            var s = Battle(); s.Battle.PlayerBoardingHealth = 400;
            Play(s, "blood_14"); Play(s, "blood_14");
            Assert.That(s.Battle.Boarding.ExhaustedCards.Count, Is.EqualTo(2));
            Assert.That(s.Battle.DiscardPile, Has.No.Member(Index("blood_14")));
            s.Battle.PlayerBoardingHealth = 401; Put(s, "blood_14");
            Assert.That(s.PlayCard(0), Is.False);
        }

        [Test]
        public void OathRequiresDesperationWhenPlayedAndRemainsAfterHealing()
        {
            var s = Battle(); s.Battle.PlayerBoardingHealth = 401; Put(s, "blood_15");
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(100));
            s.Battle.PlayerBoardingHealth = 400; Play(s, "blood_15");
            s.Battle.PlayerBoardingHealth = 900;
            int before = s.Battle.EnemyBoardingHealth;
            Assert.That(s.GetBoardingCardDamageQuote(Card("basic_01")), Is.EqualTo(32));
            Play(s, "basic_01"); Play(s, "basic_01");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(64));
            Invoke(s, "CTBPlayerEnd");
            Play(s, "basic_01");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(96));
            Invoke(s, "CTBPlayerEnd");
            Play(s, "basic_01");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(112));
        }

        [Test]
        public void OathBonusIsLockedIntoDelayedAttackAndAppliedOncePerPlayedCard()
        {
            var s = Battle(); s.Battle.PlayerBoardingHealth = 400; Play(s, "blood_15");
            Assert.That(s.GetBoardingCardDamageQuote(Card("blood_13")), Is.EqualTo(60));
            Play(s, "blood_13");
            Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(60));
            int before = s.Battle.EnemyBoardingHealth;
            Assert.That(s.GetBoardingCardDamageQuote(Card("blood_07")), Is.EqualTo(72));
            Play(s, "blood_07");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(72));
            Invoke(s, "CTBPlayerEnd"); Invoke(s, "CTBPlayerEnd");
            Invoke(s, "BPlayerStart");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(132));
        }

        [TestCase(0, 1000, 18)]
        [TestCase(8, 1000, 10)]
        [TestCase(20, 1000, 0)]
        [TestCase(0, 3, 3)]
        public void LifeDrainUsesOnlyActualHealthLostAndNeverBlockedOrOverkillDamage(int guard, int enemy, int healing)
        {
            var s = Battle(); var battle = s.Battle;
            battle.PlayerBoardingHealth = 500; battle.EnemyBoardingHealth = enemy; battle.EnemyBlock = guard;
            Play(s, "blood_06");
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(500 + healing));
        }

        [Test]
        public void LifeDrainDoesNotIncludeHealthRemovedByImmediateCurseExecution()
        {
            var s = Battle(); var battle = s.Battle;
            battle.PlayerBoardingHealth = 500; battle.EnemyBoardingHealth = 100;
            Invoke(s, "CTBAddMarks", 82);
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(100), "The target survives until 18 actual damage lowers health to exactly 82 marks.");
            Play(s, "blood_06");
            Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            Assert.That(battle.Boarding.EnemyExecutedByCurse, Is.True);
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(518));
        }

        [Test]
        public void EveryDrainCopyHealsAndHealingBanBlocksDrainAndRecoveryUntilOwnEndsExpire()
        {
            var s = Battle(); s.Battle.PlayerBoardingHealth = 500;
            Play(s, "blood_06"); Play(s, "blood_06");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(536));
            s.ApplyBattleHealingBan(true, 2);
            s.Cargo.Medicine = 1;
            Assert.That(s.GetBattleCardEffects(Card("blood_09"), NavalBattleStage.Boarding).ActualRepair, Is.Zero);
            Play(s, "blood_09"); Play(s, "blood_06");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(536));
            Assert.That(s.Cargo.Medicine, Is.Zero);
            Invoke(s, "BQueueHeal", 5, 2);
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(536));
            Assert.That(s.Battle.NavalExpansion.PlayerHealingBanTurns, Is.EqualTo(1));
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(536));
            Assert.That(s.IsBattleHealingBlocked(true), Is.False);
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(539));
        }

        [Test]
        public void DebtRetaliatesForEveryAttackIncludingFullyBlockedHitsAndPreviewMatches()
        {
            var s = Battle(); Play(s, "blood_08"); s.Battle.PlayerBlock = 100;
            SetEnemyCards(s, Attack("a"), Attack("b"), Attack("c"));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Count, Is.EqualTo(3));
            Assert.That(preview.Actions.Select(a => a.EnemyHealthAfter), Is.EqualTo(new[] { 984, 968, 952 }));
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000), "Preview must be read-only.");
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(952));
            Assert.That(s.Battle.BoardingEnemy.LastActions.Select(a => a.EnemyHealthAfter),
                Is.EqualTo(preview.Actions.Select(a => a.EnemyHealthAfter)));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(904));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(904));
        }

        [Test]
        public void DebtDoesNotReactToLifePaymentBurnOrZeroDamageAttack()
        {
            var s = Battle(); Play(s, "blood_08"); Play(s, "blood_05");
            typeof(CoreBoardingEnemyState).GetProperty("PlayerBurnDamage").SetValue(s.Battle.BoardingEnemy, 5);
            typeof(CoreBoardingEnemyState).GetProperty("PlayerBurnTurns").SetValue(s.Battle.BoardingEnemy, 5);
            Invoke(s, "TickBoardingEnemyFire");
            Invoke(s, "CTBAfterEnemyAttack", 0, 0);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(1000));
        }

        [TestCase(false, 30)]
        [TestCase(true, 0)]
        public void EnemyHealingAndFullLifeDrainRespectBanInPreviewAndCommit(bool banned, int healing)
        {
            var s = Battle(); s.Battle.EnemyBoardingHealth = 500;
            SetEnemyCards(s, new CoreBoardingEnemyCard("drain", "吸血", attackPercent: 100, healPercent: 200, drainPercent: 1));
            if (banned) s.ApplyBattleHealingBan(false, 2);
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Single().Healed, Is.EqualTo(healing));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(500 + healing));
            Assert.That(s.Battle.BoardingEnemy.LastActions.Single().Healed, Is.EqualTo(healing));
            if (banned) Assert.That(s.Battle.NavalExpansion.EnemyHealingBanTurns, Is.EqualTo(1));
        }

        private static CoreBoardingEnemyCard Attack(string id) => new CoreBoardingEnemyCard(id, id, attackPercent: 100);
        private static void SetEnemyCards(CoreVoyageSession s, params CoreBoardingEnemyCard[] cards)
        {
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(s.Battle.BoardingEnemy,
                new CoreBoardingEnemyDeck("blood", new CoreBoardingEnemyTurn("测试敌方行动", cards)));
        }

        private static CoreVoyageSession Battle()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureSpecialCardSources(new[] { "crew_broken_anchor_doctor_hector" }, Array.Empty<string>());
            s.SetSeed("BLOOD-BOARDING-REVISION");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Battle.DistanceMeters = 100f;
            typeof(CoreBattleState).GetProperty("EnemyHeadingDegrees").SetValue(s.Battle, 90f);
            typeof(CoreBattleState).GetProperty("PlayerHeadingDegrees").SetValue(s.Battle, 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 1000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 1000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 10, 0, 0, 28, 1, 1, healthMultiplier: 1);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 0);
            s.Cargo.Medicine = 0;
            return s;
        }

        private static string Id(string key) => key.StartsWith("card_", StringComparison.Ordinal) ? key : "card_boarding_" + key;
        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == Id(key));
        private static CoreBattleCard Card(string key) => CoreBoardingCardCatalog.ForBoarding(CoreVoyageSession.CardLibrary[Index(key)]);
        private static void Put(CoreVoyageSession s, string key)
        { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(key)); s.Battle.Energy = 100; }
        private static void Play(CoreVoyageSession s, string key)
        { Put(s, key); Assert.That(s.PlayCard(0), Is.True, key); }
        private static object Invoke(CoreVoyageSession s, string method, params object[] args)
            => typeof(CoreVoyageSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s, args);
    }
}
