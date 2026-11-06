using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardRuleTextTests
    {
        [TestCase("card_school_fire_n01", "3×3火场", 5, "区域中的角色主动攻击伤害提高25%；自身回合结束时，获得等于火场剩余持续时间的燃烧层数")]
        [TestCase("card_school_arcane_n01", "2×2折射阵", 3, "区域中的角色自身回合开始时，获得等于折射阵剩余持续时间的法力；受到的主动攻击伤害提高25%")]
        [TestCase("card_school_curse_n01", "2×2禁域", 5, "禁域中的角色受到的主动攻击伤害降低25%；自身回合结束时，获得等于禁域剩余持续时间的咒印层数")]
        [TestCase("card_school_tide_n01", "4×4定向水流", 5, "定向水流中的角色不可以向水流相反的方向移动，回合结束时，向水流方向移动1格")]
        [TestCase("card_school_thunder_n01", "7×7雷栅", 5, "每移动至雷栅中的一格，获得等于该格雷栅剩余持续时间的连锁；处于雷栅时连锁不会清空或消耗；连锁增加至至少50时，受到1000点伤害")]
        [TestCase("card_school_blood_n01", "2×2血池", 5, "血池中的角色主动支付船体或生命值时，获得支付量两倍的格挡；自身回合结束时，流失等于血池剩余持续时间的船体或生命值，无视格挡、可致死，且不触发格挡返还")]
        public void FieldsKeepPlacementOnCardsAndSharedEffectsInKeywordExplanations(string id, string area, int duration, string effect)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(CoreNavalSchoolExpansionCatalog.FindSpec(id).IsField, Is.True);
                string template = Compact(card.DescriptionTemplate);
                Assert.That(template, Does.Contain(area).And.Match("铺设持续(?:时间为)?" + duration + "回合"),
                    id + " retains its authored footprint and duration.");
                Assert.That(Compact(CoreBattleCardKeywordText.Build(card).Single().Meaning), Is.EqualTo(effect),
                    id + " explains the complete current field effect outside the card body.");
                foreach (string description in new[] { card.Description, session.BattleCardFaceDescription(card, NavalBattleStage.Naval),
                    session.BattleCardEffectDescription(card, NavalBattleStage.Naval) })
                {
                    string text = Compact(description);
                    Assert.That(text, Does.Contain("范围1-4格：").And.Contain(area)
                        .And.Match("铺设持续(?:时间为)?" + duration + "回合"), id + " +" + level);
                    Assert.That(text, Does.Not.Contain(effect), "Shared field rules belong in the keyword explanation.");
                    Assert.That(text, Does.Not.Contain("放置").And.Not.Contain("每一格须合法")
                        .And.Not.Contain("第2次后续己方回合").And.Not.Contain("单结算链"));
                }
            }
        }

        // The 2026-10-27 protection revision expires at the next owner-turn start.
        [TestCase("card_school_fire_n04", "直到下个己方回合开始时，免疫火场负面效果影响")]
        [TestCase("card_school_arcane_n03", "造成10点无视格挡伤害")]
        [TestCase("card_school_curse_n02", "敌方咒印层数≥1才可以使用：敌方下一次炮击伤害降低6点")]
        [TestCase("card_school_tide_n03", "消耗1木板额外获得6点格挡，并将当前全部格挡变为蓄潮格挡，直到下个己方回合开始时，免疫定向水流负面效果影响")]
        [TestCase("card_school_tide_n04", "使敌方向后移动1格，敌方下次移动时至多移动1格")]
        [TestCase("card_school_thunder_n02", "连锁2：额外造成12点伤害")]
        [TestCase("card_school_thunder_n03", "额外+2连锁")]
        [TestCase("card_school_thunder_n04", "直到下个敌方回合结束，目标造成的伤害减少4点；连锁2：改为减少8点")]
        [TestCase("card_school_blood_n03", "绝境：消耗5点船体额外造成16点伤害")]
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
