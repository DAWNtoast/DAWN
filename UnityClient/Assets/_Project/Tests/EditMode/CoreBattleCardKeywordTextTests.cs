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
                "施加自燃：2；施加 3 层灼烧；获得 1 连锁；燃烧中的目标再次受到灼烧，连锁提高。");

            Assert.That(entries.Select(entry => entry.Keyword), Is.EqualTo(new[] { "自燃", "灼烧", "连锁" }));
        }

        [Test]
        public void CompleteKeywordsDoNotAlsoExposeTheirNestedKeywords()
        {
            var entries = CoreBattleCardKeywordText.Build(null, "获得临时法力；获得火场免疫；获得蓄潮格挡。");

            Assert.That(entries.Select(entry => entry.Keyword),
                Is.EqualTo(new[] { "临时法力", "火场免疫", "蓄潮格挡" }));
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

        [TestCase("card_boarding_fire_06", "焚烧")]
        [TestCase("card_boarding_fire_11", "自燃")]
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
        [TestCase("card_school_blood_n01", "血誓阵")]
        public void CurrentNavalAndBoardingFacesHaveTheirSharedKeywordDefinitions(string id, string keyword)
        {
            Assert.That(CoreBattleCardKeywordText.Build(Find(id)).Select(entry => entry.Keyword), Does.Contain(keyword));
        }

        [Test]
        public void StatusDefinitionsUseCurrentDecayRulesInsteadOfRetiredBatchRules()
        {
            var entries = CoreBattleCardKeywordText.Build(Find("card_boarding_curse_02"));
            Assert.That(entries.Single().Meaning, Does.Contain("无视格挡").And.Contain("逐回合减少")
                .And.Not.Contain("本身不造成伤害").And.Not.Contain("结算2次"));
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
