using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardLinearFormulaUpgradeTests
    {
        // These authored expectations are independent of the shared evaluator and
        // distinguish term-wise upgrades from the old rounded whole-expression result.
        [TestCase("arcane_04", "48:6/60:8/76:9", false, 1)]
        [TestCase("arcane_11", "72:8/84:9/104:12", true, 1)]
        [TestCase("curse_06", "12:1/15:1/18:2/23:2", false, 1)]
        [TestCase("curse_11", "40:8/48:10/60:12", false, 1)]
        [TestCase("tide_06", "12:1/16:1/20:2/26:2", false, 1)]
        [TestCase("tide_07", "32:1/40:1/52:2", false, 1)]
        [TestCase("tide_08", "60:1.5/72:2/90:2", false, 1)]
        [TestCase("thunder_02", "16:2/20:3/24:3/30:4", false, 1)]
        [TestCase("thunder_05", "28:2/34:2/44:3", true, 1)]
        [TestCase("thunder_11", "6:3/8:4/10:5/12:6", false, 2)]
        [TestCase("thunder_13", "40:7/50:9/64:11", false, 1)]
        [TestCase("thunder_14", "32:6/40:8/52:10", false, 1)]
        [TestCase("thunder_15", "56:8/68:10/84:12", false, 1)]
        public void EveryDynamicAttackRankMatchesPrintedTermsInQuoteAndResolution(
            string key, string expectedTerms, bool delayed, int hits)
        {
            string[] ranks = expectedTerms.Split('/');
            for (int rank = 0; rank < ranks.Length; rank++)
            foreach (int x in new[] { 0, 1, 3, 5, 12, 25 })
            {
                if (key == "curse_11" && x < 2) continue;
                CoreVoyageSession session = Battle(key, rank);
                SetVariable(session, key, x);
                CoreBattleCard card = session.GetBattleCard(Index(key));
                string[] terms = ranks[rank].Split(':');
                int constant = int.Parse(terms[0], CultureInfo.InvariantCulture);
                decimal coefficient = decimal.Parse(terms[1], CultureInfo.InvariantCulture);
                int expected = (int)decimal.Floor(constant + coefficient * x) * hits;
                if (key == "thunder_15")
                {
                    if (x >= 10) expected *= 2;
                    if (x >= 20) expected *= 2;
                }
                string context = key + " +" + rank + " X=" + x;
                Assert.That(card.Description, Does.Not.Contain("四舍五入").And.Not.Match(@"\)×\d"), context);
                int[] resources = Resources(session);
                Assert.That(session.GetBoardingCardDamageQuote(card), Is.EqualTo(expected), context);
                CollectionAssert.AreEqual(resources, Resources(session), context + " read-only quote");
                int before = session.Battle.EnemyBoardingHealth;
                Assert.That(session.PlayCard(0), Is.True, context + ": " + session.CardUnavailableReason(0));
                if (delayed)
                {
                    Assert.That(session.Battle.EnemyBoardingHealth, Is.EqualTo(before), context + " remains delayed");
                    Assert.That(session.Battle.Boarding.DelayedDamage.Single().Amount, Is.EqualTo(expected), context);
                    Invoke(session, "BResolveSelectedDelayed", 0);
                }
                Assert.That(before - session.Battle.EnemyBoardingHealth, Is.EqualTo(expected), context);
            }
        }

        [TestCase(0, 4, 8)]
        [TestCase(1, 6, 12)]
        [TestCase(2, 7, 14)]
        [TestCase(3, 9, 18)]
        public void BurnedCardGuardUsesEachRanksDirectTerms(int rank, int constant, int coefficient)
        {
            foreach (int x in new[] { 1, 2, 3 })
            {
                CoreVoyageSession session = Battle("fire_08", rank);
                session.Battle.Hand.AddRange(new[] { Index("basic_01"), Index("basic_04"), Index("basic_08") });
                var choice = session.GetBoardingPlayChoice(0);
                Assert.That(session.PlayCard(0, new CoreBoardingPlayOptions
                {
                    SnapshotToken = choice.SnapshotToken,
                    SelectionIndexes = Enumerable.Range(1, x).ToList()
                }), Is.True);
                Assert.That(session.Battle.PlayerBlock, Is.EqualTo(constant + coefficient * x));
            }
        }

        [TestCase(0, 8, 2)]
        [TestCase(1, 10, 3)]
        [TestCase(2, 12, 3)]
        [TestCase(3, 16, 4)]
        public void ArmorCurseRemovesGuardUsingPrintedTerms(int rank, int constant, int coefficient)
        {
            foreach (int marks in new[] { 0, 1, 5, 20 })
            {
                CoreVoyageSession session = Battle("curse_08", rank);
                SetVariable(session, "curse_08", marks);
                session.Battle.EnemyBlock = 1000;
                Assert.That(session.PlayCard(0), Is.True);
                Assert.That(session.Battle.EnemyBlock, Is.EqualTo(1000 - constant - coefficient * marks));
            }
        }

        [Test]
        public void FractionalBaseClampsNegativeXAndKeepsFloorBeforeUpgradedWholeCoefficients()
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey("tide_08");
            var baseline = CoreBattleCardLinearFormula.For(card, 60, 1.5m);
            Assert.That(baseline.Evaluate(-1), Is.EqualTo(60));
            Assert.That(baseline.Evaluate(int.MinValue), Is.EqualTo(60));
            Assert.That(baseline.Evaluate(1), Is.EqualTo(61));
            Assert.That(baseline.Evaluate(3), Is.EqualTo(64));
            Assert.That(baseline.Describe("X"), Is.EqualTo("60+1.5X"));
            var upgraded = CoreBattleCardLinearFormula.For(card, 60, 1.5m, 1);
            Assert.That(upgraded.Constant, Is.EqualTo(72));
            Assert.That(upgraded.Coefficient, Is.EqualTo(2m));
            Assert.That(upgraded.Evaluate(3), Is.EqualTo(78));
            Assert.That(upgraded.Describe("X"), Is.EqualTo("72+2X"));
        }

        [Test]
        public void LargeResourcePoolsSaturateWithoutOverflowOrWrappingNegative()
        {
            foreach (string key in new[] { "arcane_04", "curse_06", "tide_08", "thunder_13" })
            {
                CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
                var formula = CoreBattleCardLinearFormula.For(card, 60, 8m, 2);
                Assert.That(formula.Evaluate(int.MaxValue), Is.EqualTo(int.MaxValue), key);
                Assert.That(formula.Evaluate(int.MaxValue / 2), Is.EqualTo(int.MaxValue), key);
            }
            var maximum = CoreBattleCardLinearFormula.For(null, int.MaxValue, int.MaxValue, 3);
            Assert.That(maximum.Evaluate(int.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.That(maximum.Evaluate(0), Is.EqualTo(int.MaxValue));
        }

        private static CoreVoyageSession Battle(string key, int rank)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_boarding_" + key, rank } });
            session.SetSeed("LINEAR-FORMULA-20261031");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Invoke(session, "EnterBattleStage", NavalBattleStage.Boarding);
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 100000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.Boarding.SelfBurn.Clear(); session.Battle.Boarding.EnemyBurn.Clear();
            session.Battle.Hand.Clear(); session.Battle.DrawPile.Clear(); session.Battle.DiscardPile.Clear();
            session.Battle.Hand.Add(Index(key)); session.Battle.Energy = 100;
            session.Cargo.Powder = 100;
            return session;
        }

        private static void SetVariable(CoreVoyageSession session, string key, int amount)
        {
            if (key.StartsWith("arcane_", StringComparison.Ordinal)) session.Battle.Schools.Mana = amount;
            else if (key.StartsWith("thunder_", StringComparison.Ordinal)) session.Battle.Schools.ThunderChain = amount;
            else if (key.StartsWith("tide_", StringComparison.Ordinal)) session.Battle.Boarding.TideOverflow = amount;
            else if (key.StartsWith("curse_", StringComparison.Ordinal))
            {
                session.Battle.Boarding.CurseLayers.Clear();
                for (int i = 0; i < amount; i++) session.Battle.Boarding.CurseLayers.Add(new CoreBoardingCurseLayer());
            }
        }

        private static int[] Resources(CoreVoyageSession s) => new[]
            { s.Battle.Schools.AvailableMana, s.Battle.Schools.ThunderChain, s.Battle.Boarding.TidePower, s.Battle.Boarding.CurseMarks };
        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key);
        private static object Invoke(CoreVoyageSession session, string name, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, args);
    }
}
