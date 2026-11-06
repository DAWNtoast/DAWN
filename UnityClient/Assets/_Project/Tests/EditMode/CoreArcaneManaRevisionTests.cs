using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreArcaneManaRevisionTests
    {
        [TestCase("card_school_arcane_b01", 0, 2, 1, false, true)]
        [TestCase("card_school_arcane_b01", 1, 3, 1, false, true)]
        [TestCase("card_school_arcane_b02", 0, 1, 1, true, true)]
        [TestCase("card_school_arcane_b02", 1, 2, 1, true, true)]
        [TestCase("card_school_arcane_b06", 0, 2, 1, false, false)]
        [TestCase("card_school_arcane_b06", 1, 3, 2, false, false)]
        public void ResourceRanksExecuteTheirAuthoredManaCapacityAndDrawWithoutChangingRefillRules(
            string id, int rank, int resource, int draw, bool capacity, bool preparation)
        {
            CoreVoyageSession s = Start(id, rank);
            if (!preparation) Assert.That(s.EndBattleTurn(), Is.True);
            Set(s.Battle.Schools, "Mana", 7);
            Set(s.Battle.Schools, "ManaLimit", 3);
            s.Battle.Energy = 100;
            s.Battle.Hand.Clear(); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            s.Battle.Hand.Add(Index(id));
            foreach (string filler in new[] { "card_brace", "card_chain_shot", "card_retreat" })
                s.Battle.DrawPile.Add(Index(filler));
            CoreBattleCard card = s.GetBattleCard(Index(id));
            Assert.That(s.GetCardMaximumUpgradeLevel(Index(id)), Is.EqualTo(1));
            Assert.That(card.UpgradeLevel, Is.EqualTo(rank));
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(resource));
            Assert.That(card.Draw, Is.EqualTo(draw));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(draw));
            Assert.That(s.Battle.DrawPile.Count, Is.EqualTo(3 - draw));
            Assert.That(s.Battle.Energy, Is.EqualTo(99));
            int expectedMana = capacity ? 7 : 7 + resource;
            int expectedLimit = capacity ? 3 + resource : 3;
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(expectedMana),
                "Capacity growth must not immediately refill the current pool.");
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(expectedLimit));
            if (preparation)
            {
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(expectedMana),
                    "Preparation mana enters the first naval turn without a refill.");
                Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(expectedLimit));
            }
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(expectedLimit),
                "Later turns discard excess mana and refill exactly to the current limit.");
        }

        [Test]
        public void PreparationManaAndCapacityCarryIntoNavalThenRefillExactlyAtLaterTurns()
        {
            CoreVoyageSession s = Start();
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(3));
            Play(s, "card_school_arcane_b02");
            Play(s, "card_school_arcane_b01");
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(4));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(5));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(5), "Preparation gains enter naval combat intact.");
            Set(s.Battle.Schools, "Mana", 9);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(4), "Excess is replaced by the refill limit.");
            Set(s.Battle.Schools, "Mana", 0);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(4));
        }

        [Test]
        public void BoardingEntryPreservesCurrentManaAndBattleCapacityWithoutASecondRefill()
        {
            CoreVoyageSession s = Start();
            Assert.That(s.EndBattleTurn(), Is.True);
            Set(s.Battle.Schools, "ManaLimit", 6); Set(s.Battle.Schools, "Mana", 8);
            Board(s);
            Assert.That(s.Battle.Schools.ManaLimit, Is.EqualTo(6));
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(8));
            Set(s.Battle.Schools, "Mana", 1);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(6));
        }

        [Test]
        public void NavalAndBoardingManaGainsExceedTheLimitAndUseOnePaymentPool()
        {
            CoreVoyageSession s = Start();
            Assert.That(s.EndBattleTurn(), Is.True);
            Play(s, "card_school_arcane_b06");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(5));
            Board(s);
            Play(s, "card_boarding_arcane_03");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(11));
            Play(s, "card_boarding_arcane_10");
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(7));
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
        }

        [TestCase("arcane_04", 96)]
        [TestCase("arcane_06", 60)]
        [TestCase("arcane_11", 136)]
        public void XCardsSpendTheEntireOverLimitPoolAndQuotesMatchExecution(string key, int damage)
        {
            CoreVoyageSession s = Start(); s.EndBattleTurn(); Board(s);
            Set(s.Battle.Schools, "Mana", 8);
            CoreBattleCard card = s.GetBattleCard(Index("card_boarding_" + key));
            Assert.That(s.GetBoardingCardDamageQuote(card), Is.EqualTo(damage));
            int health = s.Battle.EnemyBoardingHealth;
            Play(s, card.Id);
            Assert.That(s.Battle.Schools.Mana, Is.Zero);
            if (key == "arcane_11") Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(damage));
            else Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(health - damage));
        }

        [TestCase("arcane_04")]
        [TestCase("arcane_06")]
        [TestCase("arcane_11")]
        public void ExtremeManaHasSaturatedQuotesAndResolvesWithoutAnUnboundedHitLoop(string key)
        {
            CoreVoyageSession s = Start(); s.EndBattleTurn(); Board(s);
            Set(s.Battle.Schools, "Mana", int.MaxValue);
            CoreBattleCard card = s.GetBattleCard(Index("card_boarding_" + key));
            Assert.That(s.GetBoardingCardDamageQuote(card), Is.EqualTo(int.MaxValue));
            CoreBattleState battle = s.Battle;
            battle.EnemyBoardingHealth = battle.EnemyMaxBoardingHealth = int.MaxValue;
            battle.EnemyBlock = int.MaxValue;
            Play(s, card.Id);
            Assert.That(battle.Schools.Mana, Is.Zero);
            if (key == "arcane_11") Assert.That(battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(int.MaxValue));
            else if (key == "arcane_06") Assert.That(battle.EnemyBoardingHealth, Is.Zero);
            else Assert.That(battle.EnemyBlock, Is.Zero);
        }

        [TestCase(false, "tide")]
        [TestCase(true, "tide")]
        [TestCase(false, "blood")]
        [TestCase(true, "blood")]
        public void MissileBatchMatchesSequentialGuardHealthResourcesAndCriticalHits(bool critical, string school)
        {
            CoreVoyageSession s = Start(); s.EndBattleTurn(); Board(s);
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                10000, 20, 20, 79, 0, 28, 1, 1, healthMultiplier: 1, boardingGuard: 10);
            Set(s.Battle.BoardingEnemy, "Deck", CoreBoardingEnemyCatalog.ForSchool(school));
            s.Battle.EnemyBlock = 17;
            if (school == "tide") Set(s.Battle.BoardingEnemy, "TideGuard", 17);
            SetField(s.Battle.Boarding, "ActiveAttack", true);
            SetField(s.Battle.Boarding, "ActiveCritical", critical);
            SetField(s.Battle.Boarding, "ActiveAttackBonus", 3);
            int health = s.Battle.EnemyBoardingHealth;
            Invoke(s, "BDealArcaneMissiles", 5, 7);
            int total = (35 + 3) * (critical ? 2 : 1);
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(health - total + 17));
            Assert.That(GetField(s.Battle.Boarding, "ActiveAuthoredDamage"), Is.EqualTo(38));
            Assert.That(GetField(s.Battle.Boarding, "ActiveAttackBonus"), Is.EqualTo(0));
            Assert.That(s.Battle.BoardingEnemy.TidePower, Is.EqualTo(school == "tide" ? 17 : 0));
            Assert.That(s.Battle.BoardingEnemy.BloodLoss, Is.EqualTo(school == "blood" ? 12 : 0));
        }

        private static CoreVoyageSession Start(string upgradeId = "", int rank = 0)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            if (upgradeId.Length > 0)
                s.ConfigureCardUpgrades(new Dictionary<string, int> { { upgradeId, rank } });
            s.SetSeed("ARCANE-MANA-20261024"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            Set(s, "Hull", 1000); Set(s, "Morale", 0);
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            return s;
        }

        private static void Board(CoreVoyageSession s)
        {
            s.Battle.DistanceMeters = 100f;
            Set(s.Battle, "EnemyHeadingDegrees", 90f); Set(s.Battle, "PlayerHeadingDegrees", 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 10000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Play(CoreVoyageSession s, string id)
        {
            s.Battle.Energy = 100; s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
        }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetField(object target, string name, object value) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static object GetField(object target, string name) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        private static object Invoke(CoreVoyageSession s, string method, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
    }
}
