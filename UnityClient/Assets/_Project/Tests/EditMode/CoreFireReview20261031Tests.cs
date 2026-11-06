using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreFireReview20261031Tests
    {
        [TestCase(0, 60, 2)]
        [TestCase(1, 75, 3)]
        [TestCase(2, 90, 5)]
        public void SelfBurnStrikeUsesAuthoredConstantAndCoefficientInTextQuoteAndPlay(int rank, int basis, int coefficient)
        {
            foreach (int beforeStacks in new[] { 0, 1, 7, 40 })
            {
                var session = Battle("fire_11", rank);
                if (beforeStacks > 0)
                    session.Battle.Boarding.SelfBurn.Add(new CoreBoardingTimedStacks(beforeStacks, beforeStacks));
                CoreBattleCard card = session.GetBattleCard(Index("fire_11"));
                int expected = basis + coefficient * (beforeStacks + 3);
                string text = Compact(card.Description);
                Assert.That(text, Does.Contain("造成" + basis + "+" + coefficient + "×自身燃烧层数点伤害"));
                Assert.That(text, Does.Not.Contain("四舍五入"));
                Assert.That(card.Damage, Is.EqualTo(basis), "The authored constant is also the inactive preview baseline.");
                Assert.That(session.GetBoardingCardDamageQuote(card), Is.EqualTo(expected));
                Assert.That(session.Battle.Boarding.SelfBurn.Sum(x => x.Amount), Is.EqualTo(beforeStacks), "Quoting is read-only.");
                int enemy = session.Battle.EnemyBoardingHealth;
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                Assert.That(enemy - session.Battle.EnemyBoardingHealth, Is.EqualTo(expected));
                Assert.That(session.Battle.Boarding.SelfBurn.Sum(x => x.Amount), Is.EqualTo(beforeStacks + 3));
            }
        }

        [TestCase(0, 3, 2)]
        [TestCase(1, 4, 3)]
        [TestCase(2, 5, 4)]
        public void BurnFuelUsesSelectedCardCountAndAuthoredFormula(int rank, int basis, int coefficient)
        {
            foreach (int selected in new[] { 1, 2, 3 })
            {
                var session = Battle("fire_07", rank, "basic_01", "basic_04", "basic_08");
                CoreBattleCard card = session.GetBattleCard(Index("fire_07"));
                Assert.That(Compact(card.Description), Does.Contain("施加" + basis + "+" + coefficient + "X层燃烧"));
                Assert.That(card.Description, Does.Not.Contain("四舍五入"));
                var choice = session.GetBoardingPlayChoice(0);
                Assert.That(choice.MinSelection, Is.EqualTo(1));
                Assert.That(choice.MaxSelection, Is.EqualTo(3));
                Assert.That(session.PlayCard(0, new CoreBoardingPlayOptions
                {
                    SnapshotToken = choice.SnapshotToken,
                    ResourceAmount = 99,
                    SelectionIndexes = Enumerable.Range(1, selected).ToList()
                }), Is.True);
                Assert.That(session.Battle.Boarding.EnemyBurn.Sum(x => x.Amount), Is.EqualTo(basis + coefficient * selected));
                Assert.That(session.Battle.Boarding.ExhaustedCards.Count, Is.EqualTo(selected));
            }
        }

        [TestCase(0, 1)]
        [TestCase(0, 5)]
        [TestCase(1, 1)]
        [TestCase(1, 5)]
        public void IgnitionIsPlayableAtOneHealthAndDoesNotSpendHealth(int rank, int health)
        {
            var session = Battle("fire_14", rank);
            session.Battle.PlayerBoardingHealth = health;
            CoreBattleCard card = session.GetBattleCard(Index("fire_14"));
            int energy = session.Battle.Energy;
            Assert.That(CoreBoardingCardBalance.Cost("fire_14"), Is.Zero);
            Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Empty);
            Assert.That(CardSpecialCostPresentation.CostText(card), Is.Empty);
            Assert.That(card.Description, Does.Not.Contain("支付").And.Not.Contain("消耗"));
            Assert.That(session.GetBoardingPlayChoice(0).MinResource, Is.Zero);
            Assert.That(session.CardUnavailableReason(0), Is.Empty);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health));
            Assert.That(session.Battle.Boarding.SelfBurn.Sum(x => x.Amount), Is.EqualTo(2));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - card.Energy + CoreBattleUpgradeBalance.EnergyRecovery(card)));
        }

        [Test]
        public void UncappedAuthoredFormulaSaturatesWithoutIntegerOverflow()
        {
            Assert.That(CoreBoardingCardBalance.SelfBurnDamage(int.MaxValue, 2), Is.EqualTo(int.MaxValue));
            Assert.That(CoreBoardingCardBalance.BurnFuel(int.MaxValue, 2), Is.EqualTo(int.MaxValue));
        }

        private static CoreVoyageSession Battle(string key, int rank, params string[] fuel)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_boarding_" + key, rank } });
            session.SetSeed("FIRE-REVIEW-20261031");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { NavalBattleStage.Boarding });
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.Boarding.SelfBurn.Clear(); session.Battle.Boarding.EnemyBurn.Clear();
            session.Battle.Hand.Clear(); session.Battle.DrawPile.Clear(); session.Battle.DiscardPile.Clear();
            session.Battle.Hand.Add(Index(key)); session.Battle.Hand.AddRange(fuel.Select(Index));
            session.Battle.Energy = 10;
            return session;
        }

        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key);
        private static string Compact(string text) => Regex.Replace(text, @"\s+", string.Empty);
    }
}
