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
    public sealed class CoreBattleCardFormulaTextTests
    {
        [Test]
        public void EveryCardAndRankOmitsCalculatedExamplesAcrossDescriptionSurfaces()
        {
            var session = new CoreVoyageSession();
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
                if (original.AllowedStages == NavalBattleStages.None) continue;
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    var texts = new List<string> { card.Description, session.BattleCardPortDescription(card) };
                    foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                    {
                        if (!card.IsAllowedInStage(stage)) continue;
                        texts.Add(session.BattleCardFaceDescription(card, stage));
                        texts.Add(session.BattleCardEffectDescription(card, stage));
                    }
                    foreach (string text in texts)
                    {
                        string context = card.Id + " +" + rank + ": " + text;
                        Assert.That(text, Does.Not.Match(@"(?:即|总计|合计)\s*\d+\s*(?:[/／]|至|或)\s*\d+"), context);
                        Assert.That(text, Does.Not.Contain("分别造成"), context);
                        Assert.That(text, Does.Not.Match(@"^[，；。]|[，；。]\s*[，；。]"), context);
                    }
                }
            }
        }

        [TestCase("fire_07", "4+3X", "焚烧X张其他手牌（1～3张）")]
        [TestCase("fire_08", "2+4X", "焚烧X张其他手牌（1～3张）")]
        [TestCase("arcane_04", "24+6X", "造成")]
        [TestCase("arcane_06", "2+X次", "点伤害")]
        [TestCase("arcane_11", "36+8X", "延迟：")]
        [TestCase("curse_11", "20+8X", "目标至少有1层咒印时才可使用")]
        [TestCase("thunder_11", "3+出牌前连锁层数", "点伤害2次")]
        [TestCase("blood_07", "第二击伤害提高", "若第一击使敌方失去生命值")]
        public void EveryRankPreservesItsFormulaAndNecessaryConditions(string key, string formula, string condition)
        {
            var session = new CoreVoyageSession();
            int index = Index(key);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                string text = Compact(card.Description);
                Assert.That(text, Does.Contain(formula).And.Contain(condition), card.Id + " +" + rank);
                if (rank > 0 && key != "arcane_06" && key != "blood_07")
                    Assert.That(text, Does.Contain("（结果四舍五入）"), text);
            }
        }

        [Test]
        public void FireAttackShowsOneDamageTotalAtEveryRank()
        {
            var session = new CoreVoyageSession();
            int index = Index("fire_01");
            Assert.That(Compact(session.GetBattleCardAtLevel(index, 0).Description),
                Is.EqualTo("造成28点伤害。"));
            for (int rank = 1; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(Compact(card.Description), Is.EqualTo("造成" + card.Damage + "点伤害。"));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void UpgradedSelfBurnFormulaMatchesUncappedActualDamage(int rank)
        {
            const string id = "card_boarding_fire_11";
            for (int stacks = 0; stacks <= 4; stacks++)
            {
                var session = new CoreVoyageSession();
                session.RestoreTreasureProgress(new CoreTreasureProgress
                    { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
                session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
                Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
                typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { NavalBattleStage.Boarding });
                typeof(CoreVoyageSession).GetProperty("Morale").SetValue(session, 0);
                session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
                session.Battle.EnemyBlock = 0;
                session.Battle.Energy = 10;
                session.Battle.Boarding.SelfBurn.Clear();
                if (stacks > 0) session.Battle.Boarding.SelfBurn.Add(new CoreBoardingTimedStacks(stacks, 2));
                int index = Index("fire_11");
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                string text = Compact(card.Description);
                Match formula = Regex.Match(text, @"造成\(30\+2×出牌前自燃层数\)×(?<multiplier>\d+(?:\.\d+)?)（结果四舍五入）点伤害");
                Assert.That(formula.Success, Is.True, text);
                decimal multiplier = decimal.Parse(formula.Groups["multiplier"].Value, CultureInfo.InvariantCulture);
                Assert.That(text, Does.Not.Contain("最多"));
                int expected = (int)decimal.Round((30 + 2 * stacks) * multiplier, 0, MidpointRounding.AwayFromZero);
                Assert.That(session.GetBoardingCardDamageQuote(card), Is.EqualTo(expected), text);
                session.Battle.Hand.Clear();
                session.Battle.Hand.Add(index);
                int before = session.Battle.EnemyBoardingHealth;
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                Assert.That(before - session.Battle.EnemyBoardingHealth, Is.EqualTo(expected), "rank=" + rank + " stacks=" + stacks);
                Assert.That(session.Battle.Boarding.SelfBurn.Sum(batch => batch.Amount), Is.EqualTo(stacks + 3));
            }
        }

        private static int Index(string key) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key);
        private static string Compact(string text) => Regex.Replace(text, @"\s+", string.Empty);
    }
}
