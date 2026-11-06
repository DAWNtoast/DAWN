using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreThunderBoardingRevisionTests
    {
        [TestCase("thunder_07")]
        [TestCase("thunder_09")]
        public void ThresholdUtilitiesBelowTwoCanBePlayedWithoutEffectOrChainSpend(string key)
        {
            var s = Battle(key); Chain(s, 1);
            s.Battle.DrawPile.Add(Index("basic_04"));
            int energy = s.Battle.Energy;
            Assert.That(s.GetBoardingPlayChoice(0).MaxResource, Is.Zero);
            Play(s, key);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            Assert.That(s.Battle.Hand, Is.Empty);
            Assert.That(s.Battle.DrawPile.Count, Is.EqualTo(1));
        }

        [Test]
        public void ThresholdUtilitiesReadPrePlayChainAndDoNotConsumeOrIncreaseIt()
        {
            var s = Battle("thunder_07", "thunder_09"); Chain(s, 2);
            s.Battle.DrawPile.Add(Index("basic_04"));
            Play(s, "thunder_07");
            Assert.That(s.Battle.Hand, Does.Contain(Index("basic_04")));
            int energy = s.Battle.Energy;
            Play(s, "thunder_09");
            Assert.That(s.Battle.Energy, Is.EqualTo(energy + 2));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2));
        }

        [Test]
        public void SuccessfulNonThunderPlayResetsWhileFailedPlayLeavesChainUntouched()
        {
            var s = Battle("basic_04", "thunder_02"); Chain(s, 17);
            s.Battle.Energy = 0;
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(17));
            s.Battle.Energy = 4;
            Play(s, "basic_04");
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            Play(s, "thunder_02");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
        }

        [Test]
        public void MultipleHitsUsePrePlayChainAndAwardOnlyOneUncappedChain()
        {
            var s = Battle("thunder_11"); Chain(s, 9);
            int before = s.Battle.EnemyBoardingHealth;
            Assert.That(s.GetBoardingCardDamageQuote(Card("thunder_11")), Is.EqualTo(66));
            Play(s, "thunder_11");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(66));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(10));
        }

        [TestCase("thunder_13", 110)]
        [TestCase("thunder_14", 92)]
        public void FinishersSpendAllPrePlayChainThenAwardOne(string key, int damage)
        {
            var s = Battle(key); Chain(s, 10);
            int before = s.Battle.EnemyBoardingHealth;
            Assert.That(s.GetBoardingCardDamageQuote(Card(key)), Is.EqualTo(damage));
            Play(s, key);
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(damage));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
        }

        [TestCase(9, 128)]
        [TestCase(10, 272)]
        [TestCase(19, 416)]
        [TestCase(20, 864)]
        [TestCase(21, 896)]
        public void FinaleMultipliesAtEachReachedThresholdAndQuoteMatches(int chain, int expected)
        {
            var s = Battle("thunder_15"); Chain(s, chain);
            int before = s.Battle.EnemyBoardingHealth;
            Assert.That(s.GetBoardingCardDamageQuote(Card("thunder_15")), Is.EqualTo(expected));
            Play(s, "thunder_15");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(expected));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(1));
        }

        [Test]
        public void SaturatedChainAndDamageDoNotWrapNegative()
        {
            var s = Battle("thunder_02"); Chain(s, int.MaxValue);
            Assert.That(s.GetBoardingCardDamageQuote(Card("thunder_02")), Is.EqualTo(int.MaxValue));
            Play(s, "thunder_02");
            Assert.That(s.Battle == null || s.Battle.Schools.ThunderChain >= 0, Is.True);
        }

        [Test]
        public void ChainSurvivesOwnTurnEndAndClearsAtNextOwnStart()
        {
            var s = Battle("thunder_01"); Chain(s, 8);
            Invoke(s, "BPlayerEnd");
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(8));
            Invoke(s, "BPlayerStart");
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
        }

        [TestCase(1, 0)]
        [TestCase(2, 20)]
        public void ParalysisChecksThresholdBeforeItsOwnAttackAddsChain(int chain, int weakness)
        {
            var s = Battle("thunder_12"); Chain(s, chain);
            Play(s, "thunder_12");
            Assert.That(s.Battle.Boarding.ThunderWeakness, Is.EqualTo(weakness));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(chain + 1));
        }

        [Test]
        public void ParalysisPersistsAcrossIdleTurnsAndReducesNextAttackWithoutHalfDamageCap()
        {
            var s = Battle("thunder_12"); Chain(s, 2); Play(s, "thunder_12");
            Invoke(s, "BEnemyEnd"); Invoke(s, "BPlayerStart"); Invoke(s, "BEnemyEnd");
            Assert.That(s.Battle.Boarding.ThunderWeakness, Is.EqualTo(20));
            Assert.That((int)Invoke(s, "BEnemyAttack", 8, false), Is.Zero);
            Assert.That(s.Battle.Boarding.ThunderWeakness, Is.EqualTo(20), "A preview cannot spend the next-attack debuff.");
            Assert.That((int)Invoke(s, "BEnemyAttack", 8, true), Is.Zero);
            Assert.That((int)Invoke(s, "BEnemyAttack", 8, true), Is.EqualTo(8));
            Assert.That(s.Battle.Boarding.ThunderWeakness, Is.Zero);
        }

        [Test]
        public void ResidualHitsImmediatelyForDelayedAttackAndConsumesOneChargePerCard()
        {
            var s = Battle("thunder_10", "thunder_05", "thunder_11");
            int before = s.Battle.EnemyBoardingHealth;
            Play(s, "thunder_10"); Play(s, "thunder_05");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(8));
            Assert.That(s.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(28));
            Assert.That(s.Battle.Boarding.ThunderResidualCharges, Is.EqualTo(2));
            Play(s, "thunder_11");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(34), "Two nine-damage authored hits plus one eight-damage residual hit, after the first residual.");
            Assert.That(s.Battle.Boarding.ThunderResidualCharges, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(2));
            Invoke(s, "BPlayerStart");
            Assert.That(before - s.Battle.EnemyBoardingHealth, Is.EqualTo(62));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero, "Delayed settlement does not count as another played card.");
        }

        [TestCase("thunder_01")]
        [TestCase("thunder_07")]
        [TestCase("thunder_09")]
        public void CardsWithoutOnceTextCanBeRepeatedWithNormalSameNameSurcharge(string key)
        {
            var s = Battle(key, key); Chain(s, 2);
            Play(s, key); Play(s, key);
            Assert.That(s.Battle.SuccessfulCardUses(key), Is.EqualTo(2));
        }

        [Test]
        public void EnemyNonThunderCardResetsChainAndPreviewMatchesCommit()
        {
            var s = Battle();
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                10000, 0, 20, 0, 0, 28, 1, 1, healthMultiplier: 1);
            var turn = new CoreBoardingEnemyTurn("混合连锁",
                new CoreBoardingEnemyCard("one", "雷击", attackPercent: 10, mechanic: CoreBoardingEnemyMechanic.ThunderStrike),
                new CoreBoardingEnemyCard("guard", "格挡", guardPercent: 20),
                new CoreBoardingEnemyCard("finish", "终结", attackPercent: 10, mechanic: CoreBoardingEnemyMechanic.ThunderFinisher, resourceAmount: 99, bonusPercent: 100));
            Set(s.Battle.BoardingEnemy, "Deck", new CoreBoardingEnemyDeck("thunder", turn));
            var preview = s.GetBoardingEnemyPreview();
            Assert.That(preview.Actions.Select(a => a.ResourcesAfter.ThunderChain), Is.EqualTo(new[] { 1, 0, 1 }));
            Assert.That(s.Battle.BoardingEnemy.ThunderChain, Is.Zero);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.BoardingEnemy.LastActions.Select(a => a.ResourcesAfter.ThunderChain), Is.EqualTo(new[] { 1, 0, 1 }));
            Assert.That(s.Battle.BoardingEnemy.ThunderChain, Is.EqualTo(1));
        }

        private static CoreVoyageSession Battle(params string[] keys)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("THUNDER-REVISION-20261024");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Set(s, "Hull", 1000);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.DistanceMeters = 100f;
            Set(s.Battle, "EnemyHeadingDegrees", 90f); Set(s.Battle, "PlayerHeadingDegrees", 90f);
            s.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(s.TryBoard(), Is.True); Set(s, "Morale", 0);
            s.Battle.PlayerBoardingHealth = s.Battle.PlayerMaxBoardingHealth = 10000;
            s.Battle.EnemyBoardingHealth = s.Battle.EnemyMaxBoardingHealth = 10000;
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Battle.Hand.Clear(); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            s.Battle.Hand.AddRange(keys.Select(Index)); s.Battle.Energy = 20;
            return s;
        }

        private static CoreBattleCard Card(string key) => CoreVoyageSession.CardLibrary[Index(key)];
        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_boarding_" + key);
        private static void Chain(CoreVoyageSession s, int value) => Set(s.Battle.Schools, "ThunderChain", value);
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static object Invoke(CoreVoyageSession s, string name, params object[] args) => typeof(CoreVoyageSession)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s, args);
        private static void Play(CoreVoyageSession s, string key)
        {
            int slot = s.Battle.Hand.IndexOf(Index(key));
            Assert.That(slot, Is.GreaterThanOrEqualTo(0));
            Assert.That(s.PlayCard(slot), Is.True, key + ": " + s.CardUnavailableReason(slot));
        }
    }
}
