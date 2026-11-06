using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardKeywordTextTests
    {
        [Test]
        public void ExtraLoadExampleContainsOnlyTheRequestedDefinition()
        {
            var entries = CoreBattleCardKeywordText.Build(null, "获得 6 点格挡；抽 1 张牌；额外装药：5。");

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Text, Is.EqualTo("额外装药：下一次炮击造成额外伤害"));
        }

        [Test]
        public void UsesOnlyTheVisibleDescriptionRatherThanHiddenCardRulesOrName()
        {
            CoreBattleCard card = Find("card_load_shot");
            Assert.That(CoreBattleCardKeywordText.Build(card).Select(entry => entry.Keyword),
                Is.EqualTo(new[] { "额外装药" }));
            Assert.That(CoreBattleCardKeywordText.Build(card, "获得 6 点格挡；抽 1 张牌。"), Is.Empty);
            Assert.That(CoreBattleCardKeywordText.Build(card, string.Empty), Is.Empty);
            Assert.That(CoreBattleCardKeywordText.Build(null), Is.Empty);
        }

        [Test]
        public void EntriesFollowReadingOrderAndRepeatedAliasesAppearOnlyOnce()
        {
            var entries = CoreBattleCardKeywordText.Build(null,
                "获得附火：2；施加 3 层燃烧；获得 1 连锁；燃烧中的目标再次获得燃烧，雷链提高。");

            Assert.That(entries.Select(entry => entry.Keyword), Is.EqualTo(new[] { "附火", "燃烧", "连锁" }));
        }

        [Test]
        public void ResourceNamesDoNotCreateKeywordEntries()
        {
            var entries = CoreBattleCardKeywordText.Build(null, "获得法力、魔力、临时法力、临时魔力、潮势；保留格挡；获得蓄潮格挡。");

            Assert.That(entries.Select(entry => entry.Keyword),
                Is.EqualTo(new[] { "蓄潮格挡" }));
        }

        [Test]
        public void RefractionExplainsManaBenefitAndIncomingAttackPenalty()
        {
            var entries = CoreBattleCardKeywordText.Build(null, "铺设持续 3 回合的 2×2 折射阵。");
            Assert.That(entries.Single().Text, Is.EqualTo("折射阵：区域中的角色自身回合开始时，获得等于折射阵剩余持续时间的法力；受到的主动攻击伤害提高 25%"));
        }

        [Test]
        public void TideDefinitionsDescribeGuardAndCurrentButNotTheResource()
        {
            Assert.That(CoreBattleCardKeywordText.Build(null, "潮势"), Is.Empty);
            Assert.That(CoreBattleCardKeywordText.Build(null, "蓄潮格挡").Single().Text,
                Is.EqualTo("蓄潮格挡：使用蓄潮格挡格挡伤害后，获得格挡伤害层数的潮势"));
            Assert.That(CoreBattleCardKeywordText.Build(null, "定向水流").Single().Meaning,
                Is.EqualTo("定向水流中的角色不可以向水流相反的方向移动，回合结束时，向水流方向移动 1 格"));
            Assert.That(CoreBattleCardKeywordText.Build(Find("card_special_instrument_reefstar_astrolabe"))
                .Select(entry => entry.Keyword), Is.EqualTo(new[] { "定向水流" }));
            Assert.That(CoreBattleCardKeywordText.Build(Find("card_boarding_tide_05"))
                .Select(entry => entry.Keyword), Is.EqualTo(new[] { "蓄潮格挡" }));
        }

        [Test]
        public void RemovedFireKeywordsNeverProduceGlossaryEntries()
        {
            Assert.That(CoreBattleCardKeywordText.Build(null, "烈焰强化、灼烧、燃尽、自燃、火场免疫"), Is.Empty);
        }

        [Test]
        public void BloodDefinitionsDescribeActualDrainAndCostsWithoutTheRetiredField()
        {
            Assert.That(CoreBattleCardKeywordText.Build(null, "血誓阵"), Is.Empty);
            Assert.That(CoreBattleCardKeywordText.Build(null, "吸血").Single().Meaning,
                Is.EqualTo("按目标实际扣除的船体或生命值，恢复等量自身船体或生命值"));
            Assert.That(CoreBattleCardKeywordText.Build(null, "禁疗").Single().Meaning, Is.EqualTo("无法恢复船体或生命值"));
            Assert.That(CoreBattleCardKeywordText.Build(null, "血池").Single().Meaning,
                Is.EqualTo("血池中的角色主动支付船体或生命值时，获得支付量两倍的格挡；自身回合结束时，流失等于血池剩余持续时间的船体或生命值，无视格挡、可致死，且不触发格挡返还"));
        }

        [Test]
        public void ThunderDefinitionsExcludeRemovedFieldsAndDescribeChainRetention()
        {
            Assert.That(CoreBattleCardKeywordText.Build(null, "雷链、潮汐格挡、预充电、定锚"), Is.Empty);
            Assert.That(CoreBattleCardKeywordText.Build(null, "连锁").Single().Meaning,
                Is.EqualTo("使用雷霆攻击牌后连锁数增加 1，使用非雷霆牌后归零"));
            Assert.That(CoreBattleCardKeywordText.Build(null, "雷栅").Single().Meaning,
                Does.Contain("剩余持续时间").And.Contain("不会清空或消耗").And.Contain("1000"));
        }

        [TestCase("附火", "下一次攻击造成对应层数的燃烧")]
        [TestCase("燃烧", "回合结束时受到对应层数的伤害")]
        [TestCase("火场", "区域中的角色主动攻击伤害提高 25%；自身回合结束时，获得等于火场剩余持续时间的燃烧层数")]
        [TestCase("浸油", "获得燃烧时，额外获得一倍的燃烧层数")]
        [TestCase("焚烧", "将指定卡牌移出本场战斗")]
        public void FireGlossaryMatchesTheRevisedDefinitions(string name, string meaning)
        {
            Assert.That(CoreBattleCardKeywordText.Build(null, name).Single().Meaning, Is.EqualTo(meaning));
        }

        [Test]
        public void RichTextDecorationDoesNotHideAKeyword()
        {
            var entries = CoreBattleCardKeywordText.Build(null, "<b>额外<color=#FFFF00>装药</color></b>：5。");

            Assert.That(entries.Single().Text, Is.EqualTo("额外装药：下一次炮击造成额外伤害"));
        }

        [Test]
        public void OrdinaryExtraBlockDoesNotBecomeThePersistentExtraGuardKeyword()
        {
            Assert.That(CoreBattleCardKeywordText.Build(null, "本次获得 3 点额外格挡。"), Is.Empty);
            Assert.That(CoreBattleCardKeywordText.Build(Find("card_deck_polish")).Select(entry => entry.Keyword),
                Does.Contain("额外格挡"));
        }

        [Test]
        public void EmphasisCoversEveryGlossaryKeywordAndEveryRepeatedOccurrence()
        {
            string[] keywords = { "额外装药", "额外格挡", "附火", "燃烧", "咒印", "持续恢复", "焚烧", "延迟",
                "蓄潮格挡", "连锁", "绝境", "吸血", "咒术伤害", "火场", "折射阵", "禁域", "定向水流",
                "雷栅", "血池", "浸油", "符文钳制", "装填封印", "禁疗" };
            string onePass = string.Join("；", keywords.Select(keyword => keyword + "：2"));
            string expectedPass = string.Join("；", keywords.Select(keyword => "<b>" + keyword + "</b>：2"));

            Assert.That(CoreBattleCardKeywordText.Build(null, onePass).Select(entry => entry.Keyword), Is.EqualTo(keywords));
            Assert.That(CoreBattleCardKeywordText.EmphasizeKeywords(onePass + "。" + onePass),
                Is.EqualTo(expectedPass + "。" + expectedPass));
        }

        [Test]
        public void EmphasisUsesTheSameContextRulesAsTheGlossary()
        {
            const string source = "范围1-4格：额外格挡：3；本次获得3点额外格挡；火场免疫；移除火场；潮势、法力、格挡、烈焰强化。";
            const string expected = "范围1-4格：<b>额外格挡</b>：3；本次获得3点额外格挡；火场免疫；移除<b>火场</b>；潮势、法力、格挡、烈焰强化。";
            Assert.That(CoreBattleCardKeywordText.EmphasizeKeywords(source), Is.EqualTo(expected));
        }

        [Test]
        public void EmphasisReplacesOldTagsIsIdempotentAndKeepsAllVisibleValues()
        {
            const string source = "<b>范围1-4格</b>：施加2层<color=#FFFF00>燃烧</color>；<b>额外</b>装药：-3；比例-12.5%；格挡+2；生命−4。";
            const string expected = "范围1-4格：施加2层<b>燃烧</b>；<b>额外装药</b>：-3；比例-12.5%；格挡+2；生命−4。";
            string formatted = CoreBattleCardKeywordText.EmphasizeKeywords(source);
            Assert.That(formatted, Is.EqualTo(expected));
            Assert.That(CoreBattleCardKeywordText.EmphasizeKeywords(formatted), Is.EqualTo(formatted));
            Assert.That(CoreBattleCardKeywordText.EmphasizeKeywords(null), Is.Empty);
            Assert.That(CoreBattleCardKeywordText.EmphasizeKeywords(string.Empty), Is.Empty);
        }

        [TestCase("card_boarding_fire_06", "焚烧")]
        [TestCase("card_boarding_fire_11", "燃烧")]
        [TestCase("card_boarding_curse_02", "咒印")]
        [TestCase("card_boarding_tide_01", "蓄潮格挡")]
        [TestCase("card_boarding_basic_07", "持续恢复")]
        [TestCase("card_boarding_basic_13", "延迟")]
        [TestCase("card_boarding_blood_06", "吸血")]
        [TestCase("card_boarding_blood_09", "绝境")]
        [TestCase("card_school_fire_n01", "火场")]
        [TestCase("card_school_arcane_n01", "折射阵")]
        [TestCase("card_school_curse_n01", "禁域")]
        [TestCase("card_school_tide_n01", "定向水流")]
        [TestCase("card_school_thunder_n01", "雷栅")]
        [TestCase("card_school_blood_n01", "血池")]
        public void CurrentNavalAndBoardingFacesHaveTheirSharedKeywordDefinitions(string id, string keyword)
        {
            Assert.That(CoreBattleCardKeywordText.Build(Find(id)).Select(entry => entry.Keyword), Does.Contain(keyword));
        }

        [Test]
        public void CurseDefinitionsExplainInstantExecutionWithoutDamageOrDecay()
        {
            var entries = CoreBattleCardKeywordText.Build(Find("card_boarding_curse_02"));
            Assert.That(entries.Single().Meaning, Is.EqualTo("当咒印层数大于或等于该角色剩余生命值的 100% 时，立即消灭该角色"));
            Assert.That(CoreBattleCardKeywordText.Build(null, "咒痕"), Is.Empty);
            Assert.That(CoreBattleCardKeywordText.Build(null, "禁域").Single().Meaning,
                Is.EqualTo("禁域中的角色受到的主动攻击伤害降低 25%；自身回合结束时，获得等于禁域剩余持续时间的咒印层数"));
            Assert.That(CoreBattleCardKeywordText.Build(null, "咒术伤害").Single().Meaning,
                Is.EqualTo("无视格挡造成伤害"));
        }

        [Test]
        public void UpgradesChangeCardValuesWithoutAddingCalculationsToTheGlossary()
        {
            var session = new CoreVoyageSession();
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_load_shot");
            string expected = CoreBattleCardKeywordText.Build(session.GetBattleCardAtLevel(index, 0)).Single().Text;

            for (int rank = 1; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                Assert.That(CoreBattleCardKeywordText.Build(session.GetBattleCardAtLevel(index, rank)).Single().Text,
                    Is.EqualTo(expected));

            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
                foreach (CoreBattleCardKeyword entry in CoreBattleCardKeywordText.Build(card))
                    Assert.That(entry.Meaning, Does.Not.Match(@"[=+×]|四舍五入|向下取整|当前伤害|预计伤害"), card.Id);
        }

        private static CoreBattleCard Find(string id) => CoreVoyageSession.CardLibrary.Single(card => card.Id == id);
    }
}
