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
        public void CurseShowsAppliedStacksAndTheImmediateCurseDamageFormula()
        {
            CoreBattleCard mark = Find("card_school_curse_b03");
            string text = Compact(mark.Description);
            Assert.That(text, Does.Contain("施加" + mark.SchoolEffect.Power + "层咒印"));
            Assert.That(text, Does.Not.Contain("持续").And.Not.Contain("每层咒印扣除"));
            Assert.That(text, Does.Not.Contain("不造成直接伤害"));
            string detonate = Compact(Find("card_school_curse_b05").Description);
            Assert.That(detonate, Does.Contain("造成" + Find("card_school_curse_b05").SchoolEffect.Power + "×咒印层数点咒术伤害"));
            Assert.That(CoreBattleCardKeywordText.Build(Find("card_school_curse_b05"))
                .Single(entry => entry.Keyword == "咒术伤害").Meaning, Is.EqualTo("无视格挡造成伤害"));
        }

        [TestCase("card_school_thunder_b03")]
        [TestCase("card_school_thunder_b06")]
        [TestCase("card_special_crew_bronze_bell_gunner_helen")]
        public void ThunderAttackKeepsItsScalingAndCostsWithoutRepeatingResourceRules(string id)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = Find(id);
            string definition = Compact(card.Description);
            string condition = card.Id == "card_school_thunder_b03"
                ? "连锁" + card.SchoolEffect.Threshold + "：额外造成" + card.SchoolEffect.Power + "点伤害"
                : card.SchoolEffect.Mechanic == CoreSchoolMechanic.ThunderFinish
                    ? "造成" + card.Damage + "+" + card.SchoolEffect.Power + "X点船体伤害"
                    : "出牌前每有一层连锁，本次伤害提高" + card.SchoolEffect.Power + "点";
            Assert.That(definition, Does.Contain(condition));
            if (card.SchoolEffect.Mechanic == CoreSchoolMechanic.ThunderFinish)
            {
                Assert.That(definition, Does.Contain("结算后清空连锁，出牌时处于雷栅则不清空"));
                Assert.That(card.DescriptionTemplate, Does.Contain("结算后清空连锁，出牌时处于雷栅则不清空"));
            }
            else Assert.That(definition, Does.Not.Contain("清空连锁").And.Not.Contain("消耗连锁"));
            foreach (string text in new[] { definition, Compact(session.BattleCardFaceDescription(card, NavalBattleStage.Naval)) })
            {
                Assert.That(text, Does.Contain(condition));
                Assert.That(text, Does.Not.Contain("攻击后连锁").And.Not.Contain("雷攻后连锁")
                    .And.Not.Contain("上限6").And.Not.Contain("回合结束清空"));
            }
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
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(1), "All friendly fields expand one cell in each cardinal direction.");
                Assert.That(card.SchoolEffect.Secondary, Is.Zero, "Expansion no longer also extends field duration.");
                Assert.That(text, Does.Contain("抽" + card.Draw + "张牌")
                    .And.Contain("将所有己方" + CoreComponentFieldCards.FieldName(CoreComponentFieldCards.Kind(card)) + "向上下左右各延伸1格")
                    .And.Not.Contain("延长")
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
        public void UncappedLifestealKeepsItsKeywordAndActualHealthLossMeaning()
        {
            CoreBattleCard heal = Find("card_school_blood_b04");
            Assert.That(heal.SchoolEffect.Power, Is.EqualTo(100));
            Assert.That(heal.SchoolEffect.Limit, Is.Zero, "The revised drain heals actual health loss without a separate card cap.");
            Assert.That(Compact(heal.Description), Does.Contain("吸血").And.Not.Contain("最多恢复"));
            Assert.That(CoreBattleCardKeywordText.Build(heal).Single(entry => entry.Keyword == "吸血").Meaning,
                Is.EqualTo("按目标实际扣除的船体或生命值，恢复等量自身船体或生命值"));
        }

        private static CoreBattleCard Find(string id) => CoreVoyageSession.CardLibrary.Single(card => card.Id == id);
        private static string Compact(string value) => Regex.Replace(value, @"\s+", string.Empty);
    }
}
