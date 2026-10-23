using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardRuleTextTests
    {
        [TestCase("card_school_fire_n01", "2×2火场", "2场地伤害")]
        [TestCase("card_school_arcane_n01", "2×2折射阵", "4点穿盾")]
        [TestCase("card_school_curse_n01", "2×2禁域", "额外造成2点伤害")]
        [TestCase("card_school_tide_n01", "2×2定向水流", "推动1格")]
        [TestCase("card_school_thunder_n01", "1×3雷栅", "停止本次主动航行")]
        [TestCase("card_school_blood_n01", "2×2血誓阵", "下张主动攻击首段+4")]
        public void FieldsKeepPlacementOnCardsAndSharedEffectsInAuthoredDefinitions(string id, string area, string effect)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(card.DescriptionTemplate, Does.Contain("放置本身不触发"), "Upgrade templates remain untouched.");
                Assert.That(Compact(CoreBattleCardText.FormatEffects(card.DescriptionTemplate)),
                    Does.Contain(Compact(CoreBattleCardText.FormatEffects(effect))), id + " keeps its field definition.");
                foreach (string description in new[] { card.Description, session.BattleCardFaceDescription(card, NavalBattleStage.Naval),
                    session.BattleCardEffectDescription(card, NavalBattleStage.Naval) })
                {
                    string text = Compact(description);
                    Assert.That(text, Does.Contain("范围1-4格：").And.Contain(area).And.Contain("铺设"), id + " +" + level);
                    Assert.That(text, Does.Not.Contain("放置").And.Not.Contain("每一格须合法")
                        .And.Not.Contain("第2次后续己方回合").And.Not.Contain("单结算链"));
                }
            }
        }

        [TestCase("card_school_fire_n04", "直到下个己方回合开始前，免疫2次火场伤害")]
        [TestCase("card_school_arcane_n03", "造成10点无视格挡伤害")]
        [TestCase("card_school_curse_n02", "装填减少6点")]
        [TestCase("card_school_tide_n03", "消耗1木板额外获得4点格挡，并将本牌格挡变为蓄潮格挡，且可在下个己方回合开始前拒绝一次强制位移")]
        [TestCase("card_school_tide_n04", "其下次主动移动上限减少1格")]
        [TestCase("card_school_thunder_n02", "这段额外伤害不受炮击或预充加成影响")]
        [TestCase("card_school_thunder_n03", "额外+2连锁")]
        [TestCase("card_school_thunder_n04", "直到下个敌方回合结束，目标造成的伤害减少4点；连锁2：改为减少8点")]
        [TestCase("card_school_blood_n03", "支付前己方船体不高于最大船体的40%")]
        public void CardSpecificExceptionsRemainVisible(string id, string constraint)
        {
            string text = Compact(CoreVoyageSession.CardLibrary[Index(id)].Description);
            string normalizedConstraint = Compact(CoreBattleCardText.FormatEffects(constraint));
            Assert.That(text, Does.Contain(normalizedConstraint), text);
        }

        [Test]
        public void RemovingFieldRulesDoesNotChangeTheGeneralRuleFormatter()
        {
            const string rule = "持续至第2次后续己方回合开始，放置本身不触发。";
            Assert.That(CoreBattleCardText.FormatEffects(rule), Does.Contain("放置本身不触发"));
            foreach (CoreBattleCard card in CoreNavalSchoolExpansionCatalog.All)
            {
                CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id);
                Assert.That(card.Description.Contains("同名牌每回合合计只能使用一次"), Is.EqualTo(spec.OncePerTurn), card.Id);
                if (spec.ResourceMaximum > 0)
                {
                    string cost = spec.ResourceMinimum + (spec.ResourceMinimum == spec.ResourceMaximum
                        ? string.Empty : "～" + spec.ResourceMaximum);
                    Assert.That(Compact(card.DescriptionTemplate), Does.Contain("消耗" + cost + spec.ResourceLabel),
                        card.Id + " keeps its authored payment in the definition.");
                    if (spec.ResourceMaximum > spec.ResourceMinimum)
                    {
                        string unit = spec.ResourceLabel == "咒印" || spec.ResourceLabel == "连锁" ? "层" : "点";
                        Assert.That(Compact(card.Description), Does.Contain("选择" + cost + unit + spec.ResourceLabel), card.Id);
                    }
                    else
                        Assert.That(Compact(card.Description), Does.Not.Contain("消耗" + spec.ResourceMinimum),
                            card.Id + " displays its fixed payment through the resource icon.");
                }
            }
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static string Compact(string value) => Regex.Replace(value, @"\s+", "");
    }
}
