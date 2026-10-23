using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardRuleTextStandardTests
    {
        [Test]
        public void LoadingUsesItsAuthoredKeywordWithPowderCostInMetadata()
        {
            CoreBattleCard card = Find("card_load_shot");
            string text = Compact(card.Description);
            Assert.That(text, Does.Contain("额外装药：" + card.StrategyAmount));
            Assert.That(card.PowderCost, Is.EqualTo(1));
            Assert.That(text, Does.Not.Contain("消耗1火药"));
            Assert.That(text, Does.Not.Contain("开炮另付").And.Not.Contain("清空增益"));
        }

        [Test]
        public void CurseShowsAppliedStacksAndTheSeparateSettlementDamageBonus()
        {
            CoreBattleCard mark = Find("card_school_curse_b03");
            string text = Compact(mark.Description);
            Assert.That(text, Does.Contain("施加" + mark.SchoolEffect.Power + "层咒印"));
            Assert.That(text, Does.Not.Contain("持续").And.Not.Contain("每层咒印扣除"));
            Assert.That(text, Does.Not.Contain("不造成直接伤害"));
            string detonate = Compact(Find("card_school_curse_b05").Description);
            Assert.That(detonate, Does.Contain("咒印额外扣除" + Find("card_school_curse_b05").SchoolEffect.Power + "点生命值"));
        }

        [TestCase("card_school_thunder_b03")]
        [TestCase("card_school_thunder_b06")]
        [TestCase("card_special_crew_bronze_bell_gunner_helen")]
        public void ThunderAttackKeepsItsScalingAndCostsWithoutRepeatingResourceRules(string id)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = Find(id);
            string definition = Compact(card.Description);
            Assert.That(definition, Does.Contain("连锁每有一层"));
            Assert.That(definition, Does.Contain(card.SchoolEffect.Power.ToString()));
            if (card.SchoolEffect.Mechanic == CoreSchoolMechanic.ThunderFinish)
            {
                Assert.That(definition, Does.Contain("出牌前的连锁每有一层，本次伤害提高" + card.SchoolEffect.Power + "点")
                    .And.Not.Contain("消耗全部已有连锁"));
                Assert.That(card.DescriptionTemplate, Does.Contain("消耗全部已有连锁"));
            }
            foreach (string text in new[] { definition, Compact(session.BattleCardFaceDescription(card, NavalBattleStage.Naval)) })
                Assert.That(text, Does.Not.Contain("攻击后连锁").And.Not.Contain("雷攻后连锁")
                    .And.Not.Contain("上限6").And.Not.Contain("回合结束清空"));
        }

        [TestCase("card_school_tide_b04")]
        [TestCase("card_special_crew_white_shell_navigator_saina")]
        public void PaidTideGuardKeepsItsKeywordCostAndOrdinaryFallback(string id)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = Find(id);
            foreach (string text in new[] { Compact(card.Description), Compact(session.BattleCardFaceDescription(card, NavalBattleStage.Naval)) })
            {
                Assert.That(text, Does.Contain("蓄潮格挡").And.Contain("木板").And.Contain("获得2点格挡"));
                Assert.That(text, Does.Not.Contain("此盾挡伤蓄潮势").And.Not.Contain("付费护盾吸收伤害")
                    .And.Not.Contain("上限20").And.Not.Contain("保底不随强化增长"));
            }
        }

        [Test]
        public void LegendaryInstrumentsDescribeTheirFieldOperationsWithoutRetiredDoctrineRules()
        {
            foreach (CoreBattleCard card in CoreSpecialCardCatalog.All.Select(definition => definition.Card)
                .Where(card => card.SchoolEffect?.Mechanic == CoreSchoolMechanic.ComponentFieldMastery))
            {
                string text = Compact(card.Description);
                Assert.That(text, Does.Contain(CoreComponentFieldCards.FieldName(CoreComponentFieldCards.Kind(card)))
                    .And.Contain("新增" + card.SchoolEffect.Power + "格")
                    .And.Contain("延长" + card.SchoolEffect.Secondary + "回合")
                    .And.Not.Contain("每回合首次").And.Not.Contain("同类不叠加"), card.Id);
            }
        }

        [TestCase(NavalBattleStage.Preparation)]
        [TestCase(NavalBattleStage.Naval)]
        public void BraceKeepsOnlyItsGuardEffectAtEveryLegalUpgrade(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            session.Battle.Stage = stage;
            session.Battle.PlayerDeckBuff = 0;
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(value => CoreVoyageSession.CardLibrary[value].Id == "card_brace");
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                foreach (string text in new[] { card.Description,
                    session.BattleCardFaceDescription(card, stage),
                    session.BattleCardEffectDescription(card, stage) })
                    Assert.That(Compact(text), Is.EqualTo("获得" + card.Block + "点格挡。"),
                        stage + " +" + rank);
            }
        }

        [Test]
        public void CardSpecificHealingLimitsRemainVisible()
        {
            CoreBattleCard heal = Find("card_school_blood_b04");
            Assert.That(Compact(heal.Description), Does.Contain("最多恢复" + heal.SchoolEffect.Limit + "点"));
        }

        private static CoreBattleCard Find(string id) => CoreVoyageSession.CardLibrary.Single(card => card.Id == id);
        private static string Compact(string value) => Regex.Replace(value, @"\s+", string.Empty);
    }
}
