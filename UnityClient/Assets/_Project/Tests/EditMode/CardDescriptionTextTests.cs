using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class CardDescriptionTextTests
    {
        [TestCase("恢复 14 点船员生命；消耗 3 法力，恢复额外 +18；抽 1 张。",
            "恢复14点船员生命\n消耗3点法力，恢复量提高18点\n抽1张牌")]
        [TestCase("恢复 8 点己方船员生命。", "恢复8点己方船员生命")]
        [TestCase("格挡 +4；恢复 18 点船员生命；上一敌回合有效格挡：额外恢复 18。",
            "获得4点格挡，恢复18点船员生命\n上一敌回合有效格挡：额外恢复18点生命")]
        [TestCase("敌方燃烧：格挡 +4；恢复 18 点船员生命。",
            "敌方燃烧：获得4点格挡\n恢复18点船员生命")]
        [TestCase("支付 10 当前阶段生命，额外伤害 +48（至少留1）。",
            "支付10点当前阶段生命，伤害提高48点（至少留1）")]
        [TestCase("敌有至少 3 咒印：格挡额外 +14。", "敌咒印≥3：额外获得14点格挡")]
        [TestCase("敌方生命不高于 40%：额外伤害 +30。",
            "敌生命≤40%：伤害提高30点")]
        [TestCase("出牌前敌方燃烧：伤害额外 +12。",
            "出牌前敌方燃烧：伤害提高12点")]
        [TestCase("此前连锁至少 2：抽 1 张；\n每回合一次；靠近 120 米并转为平行。抽 1 张当前阶段牌。",
            "此前连锁≥2：抽1张牌（每回合一次）\n靠近120米并转为平行\n抽1张本阶段牌")]
        [TestCase("法力 +4（上限 12，跨阶段保留）。",
            "恢复4点法力（上限12，跨阶段保留）")]
        [TestCase("连锁 +3（上限 6，本回合有效）。",
            "获得3层连锁（上限6，本回合有效）")]
        [TestCase("原已燃烧则额外结算一次，每回合限一次。",
            "原已燃烧时，额外结算一次（每回合限一次）")]
        [TestCase("上一敌回合吸收至少 8 伤害：额外伤害 +18。",
            "上一敌回合吸收伤害≥8：伤害提高18点")]
        [TestCase("接下来 2 个敌回合吸收至少 4 伤害后反击 10，每回合一次。",
            "下2个敌回合吸收≥4点伤害后反击10，每回合一次")]
        [TestCase("施加 2 咒印，持续 3 回合；每层敌攻击 -4%，最多 6 层。",
            "施加2层咒印（3回合）\n每层敌攻击-4%，最多6层")]
        [TestCase("恢复本次有效直伤的 50%，上限 28。",
            "恢复本次有效直伤的50%，上限28")]
        [TestCase("恢复已失生命的 35%，上限 18。",
            "恢复已失生命的35%，上限18")]
        [TestCase("消耗所有咒印，每层追加 6 伤害。",
            "消耗所有咒印，每层追加6点伤害")]
        [TestCase("格挡 +8；消耗 2 法力，\n格挡额外 +16。",
            "获得8点格挡\n消耗2点法力，额外获得16点格挡")]
        [TestCase("<b>4\u00a0格内</b>炮击；28 点船体伤害；消耗 5 法力，伤害额外 +44。",
            "<b>4格内</b>炮击：造成28点船体伤害\n消耗5点法力，伤害提高44点")]
        [TestCase("<b>350\u00a0米内</b>炮击；28 点船体伤害；消耗 5 法力，伤害额外 +44。",
            "<b>350米内</b>炮击：造成28点船体伤害\n消耗5点法力，伤害提高44点")]
        [TestCase("格挡+8\r抽1张", "获得8点格挡\n抽1张牌")]
        [TestCase("说明！\n格挡+8", "说明！\n获得8点格挡")]
        [TestCase("自身贡献上限 8、总上限 16。", "本牌上限8、总上限16")]
        [TestCase("造成1.5点船体伤害。", "造成1.5点船体伤害")]
        [TestCase("获得 1 点行动点；格挡 +4；抽 2 张；敌方燃烧：格挡 +8。",
            "获得1点行动点，获得4点格挡，抽2张牌\n敌方燃烧：获得8点格挡")]
        [TestCase("敌方燃烧：格挡 +4；恢复 18 点船员生命；抽 1 张。",
            "敌方燃烧：获得4点格挡\n恢复18点船员生命，抽1张牌")]
        [TestCase("格挡 +8；耗 1 木板；此盾挡伤蓄潮势；获得 1 点行动点；抽 2 张；无木板仅基础格挡2，不蓄势。",
            "获得8点格挡（消耗1木板）\n此盾挡伤蓄潮势\n获得1点行动点，抽2张牌\n无木板仅获得2点基础格挡，不蓄势")]
        [TestCase("使用1药物恢复14，绝境时恢复18；无药物恢复2，绝境时恢复3。绝境：治疗前生命不高于最大生命40%。",
            "消耗1药物恢复14点生命（绝境18点），无药物恢复2点生命（绝境3点）\n绝境：治疗前生命≤40%上限")]
        [TestCase("新增2层自燃；获得 16 格挡。本回合首次实际用格挡吸收敌方攻击后，反伤当前自燃层数×2，最多8；反伤受格挡，不暴击。",
            "新增2层自燃，获得16点格挡\n本回合首次格挡吸收敌方攻击后，反伤当前自燃层数×2，最多8\n反伤受格挡，不暴击")]
        [TestCase("本回合接下来3张主动雷霆攻击各追加8伤害；不暴击、不产链，多段只追加一次。迟雷追加一起延迟；不叠加。",
            "本回合下3张主动雷霆攻击各追加8点伤害\n不暴击、不产链，多段只追加一次\n迟雷的追加伤害也会延迟，不叠加")]
        [TestCase("咒痕上限6，每批持续3次敌方回合结束，本身不造成伤害。",
            "咒痕上限6，每批在3次敌方回合结束后消失，本身不造成伤害")]
        [TestCase("下一己方回合开始造成(36+8X)×1.5（结果四舍五入）伤害，施放时锁定；受格挡，不暴击。",
            "下个己方回合初造成(36+8X)×1.5（四舍五入）点伤害，施放时锁定\n受格挡，不暴击")]
        [TestCase("本回合和之后两个己方回合，每回合首次主动新增自燃时，为敌方附加相同层数的灼烧，单次最多3层。不叠加、不刷新。",
            "本回合及随后两个己方回合，每回合首次主动新增自燃时，为敌方施加相同层数的灼烧，单次最多3层\n不叠加、不刷新")]
        [TestCase("格挡 +4；抽 2 张；每回合一次；恢复 18 点船员生命。",
            "获得4点格挡\n抽2张牌（每回合一次）\n恢复18点船员生命")]
        [TestCase("消耗 3 火药；消耗 1 炮弹；实际造成直伤后灼烧 14×2 回合；目标出牌前已燃烧时额外结算一次燃烧，每回合一次。",
            "消耗3火药、1炮弹\n造成有效直伤后灼烧14×2回合\n出牌前目标已燃烧时，额外结算一次燃烧（每回合一次）")]
        [TestCase("150 米内近距破坏；造成 28 点船体伤害。", "150米内破坏：造成28点船体伤害")]
        [TestCase("激活本场海战专属规则：每回合首张其他秘术牌成功后，恢复 1 法力，不超过上限；同类不叠加，接舷或战斗结束清除。",
            "仅本场海战有效：每回合首张其他秘术牌成功后，恢复1点法力，不超上限\n同类不叠加")]
        [TestCase("本牌格挡实际吸收敌方攻击时等量获得潮势，上限 20；无木板时仅获得 2 普通格挡。",
            "本牌格挡吸收敌方攻击时，获得等量潮势，上限20\n无木板时仅获得2点普通格挡")]
        [TestCase("4 格曼哈顿范围，每一格须合法；最迟于目标下个回合结束失效。",
            "曼哈顿4格内，各格合法\n最迟在目标下回合末失效")]
        [TestCase("每回合首张其他火焰牌成功后，点燃敌舰，造成 3×2 回合燃烧；同类不叠加，接舷或战斗结束清除。",
            "每回合首张其他火焰牌成功后，点燃敌舰，每回合造成3点燃烧伤害，持续2回合\n同类不叠加，接舷或战斗结束清除")]
        [TestCase("同名副本每回合共用一次成功额度，衍生伤害不触发。", "同名每回合共限一次成功，衍生伤害不触发")]
        [TestCase("目标下一次完整攻击总伤害减少(2+向下取整的出牌前连锁÷2)×2.2（结果四舍五入），最多11且不超过一半。",
            "目标下次完整攻击的总伤害减少(2+向下取整的出牌前连锁÷2)×2.2（四舍五入），最多11且不超过一半")]
        [TestCase("持续3个敌方回合；每回合目标攻击首次使己方实际失去生命时，为其施加2层咒痕。自伤、灼烧和完全格挡不触发；不叠加、不刷新。",
            "持续3个敌方回合\n每回合首次被目标攻击失血时，为其施加2层咒痕\n自伤、灼烧和完全格挡不触发\n不叠加、不刷新")]
        [TestCase("修复3点船体；恢复2点当前阶段生命；造成4点船体伤害；获得2点普通格挡；抽1张牌。",
            "修复3点船体，恢复2点当前阶段生命，造成4点船体伤害，获得2点普通格挡，抽1张牌")]
        [TestCase("接下来2个敌方回合内，目标下一次主动攻击结束后受到16点伤害，受格挡、不暴击，触发一次消失；不叠加、不刷新。",
            "下2个敌方回合内，目标下次主动攻击结束后受到16点伤害，受格挡、不暴击，触发一次后消失\n不叠加、不刷新")]
        [TestCase("有药物时固定消耗1药物和8潮势，本回合和下一己方回合结束各恢复8生命；无药物各恢复2生命，不消耗潮势；不叠加、不刷新。",
            "有药物时消耗1药物和8点潮势，本次及下次己方回合末各恢复8点生命\n无药物各恢复2点生命，不消耗潮势\n不叠加、不刷新")]
        [TestCase("本次攻击后获得1层连锁，上限6，回合结束清空。",
            "攻击后获得1层连锁，上限6，回合末清空")]
        [TestCase("同名所有副本共享每回合一次成功吸血；后续减盾只触发一次，不叠加。",
            "同名每回合共限一次成功吸血\n后续减盾只触发一次，不叠加")]
        [TestCase("恢复7点船员生命，绝境时恢复9点船员生命；恢复2点当前阶段生命，绝境时恢复3点当前阶段生命。",
            "恢复7点船员生命（绝境9点）\n恢复2点当前阶段生命（绝境3点）")]
        [TestCase("恢复7点船员生命，绝境时恢复9点当前阶段生命。",
            "恢复7点船员生命，绝境时恢复9点当前阶段生命")]
        [TestCase("已有连锁强化伤害；消耗全部已有连锁强化伤害；结算后获得1层连锁。",
            "已有连锁会提高伤害\n消耗全部已有连锁会提高伤害\n结算后获得1层连锁")]
        [TestCase("施加2层咒印，每层在回合末造成2点伤害，持续2回合。",
            "施加2层咒印，每层回合末造成2点伤害，共2回合")]
        [TestCase("持续至第2次后续己方回合开始，放置本身不触发。",
            "至第2次后续己方回合初，放置不触发")]
        [TestCase("获得3点格挡；无需弹药；敌方燃烧：获得4点格挡。",
            "获得3点格挡，无需弹药\n敌方燃烧：获得4点格挡")]
        [TestCase("放置不触发。持续至第2次后续己方回合开始，放置本身不触发。",
            "放置不触发\n至第2次后续己方回合初")]
        [TestCase("首次放置不触发。持续至第2次后续己方回合开始，放置本身不触发。",
            "首次放置不触发\n至第2次后续己方回合初，放置不触发")]
        [TestCase("支付5点船体，支付后至少留1点；支付前己方船体不高于最大船体40%时伤害提高12点。",
            "支付5点船体（至少留1点）\n支付前己方船体≤40%上限时伤害提高12点")]
        [TestCase("施加浸油至下个己方回合结束；直到下个己方回合开始有效。",
            "施加浸油至下个己方回合末\n至下个己方回合初有效")]
        public void Compact_HasExplicitMeaningPreservingResults(string source, string expected)
        {
            string actual = CardDescriptionText.Compact(source);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(CardDescriptionText.Compact(actual), Is.EqualTo(actual), "Compaction is idempotent.");
            string coreWording = CoreBattleCardText.FormatEffects(source);
            Assert.That(CardDescriptionText.Compact(coreWording), Is.EqualTo(actual),
                "Already normalized Core descriptions use the same final card-face vocabulary.");
            CollectionAssert.AreEqual(NumericTokens(coreWording), NumericTokens(actual),
                "Compaction preserves every Core number, formula sign and percentage in order.");
        }

        [Test]
        public void Compact_PreservesFireTimingLimitsAndBothLifePoolsInFullSentences()
        {
            const string source = "炮战修复 3 船体，白刃战恢复 3 船员生命；" +
                "本阶段每回合首次满额延烧：装填 +6；自身贡献上限 12、总上限 24；" +
                "命中灼烧 8×2回合；首次接舷灼烧 8×2回合；" +
                "接下来 2 个敌回合，自然燃烧造成伤害后获得 6 格挡。";
            const string expected = "海战修复3点船体，接舷战恢复3点船员生命\n" +
                "本阶段每回合首次满额延烧：装填+6\n本牌上限12、总上限24\n" +
                "命中灼烧8×2回合\n首次接舷灼烧8×2回合\n" +
                "下2个敌回合，自然燃烧造成伤害后获得6点格挡";
            Assert.That(CardDescriptionText.Compact(source), Is.EqualTo(expected));
        }

        [TestCase("fire_01", "点伤害")]
        [TestCase("fire_07", "4+3X")]
        [TestCase("fire_08", "2+4X")]
        [TestCase("fire_11", "出牌前")]
        [TestCase("arcane_04", "24+6X")]
        [TestCase("arcane_06", "2+X")]
        [TestCase("arcane_11", "36+8X")]
        [TestCase("curse_11", "20+8X")]
        [TestCase("thunder_11", "3+出牌前连锁层数")]
        [TestCase("blood_07", "第二击")]
        public void FormulaOnlyCards_KeepTheirFormulaNumbersAndTimingThroughRealTextLayout(string key, string formula)
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                for (int rank = 0; rank <= 3; rank++)
                {
                    CoreBattleCard card = CardAtLevel("card_boarding_" + key, rank);
                    string quote = new CoreVoyageSession().BattleCardFaceDescription(card, NavalBattleStage.Boarding);
                    string source = CardFaceLayout.FormatDescription(quote);
                    effect.text = source;
                    CardFaceLayout.FitEffect(effect);
                    string visible = Regex.Replace(StripTags(effect.text), @"\s+", string.Empty);
                    string context = card.Id + " rank " + rank;
                    Assert.That(visible, Does.Contain(formula).And.Not.Contain("总计").And.Not.Contain("分别造成")
                        .And.Not.Match(@"即\d|\d/\d"), context);
                    CollectionAssert.AreEqual(NumericTokens(quote), NumericTokens(effect.text),
                        context + " retains every coefficient, upgrade multiplier, limit and duration after examples are removed");
                    CollectionAssert.AreEqual(Regex.Matches(quote, @"[X×÷]").Cast<Match>().Select(match => match.Value).ToArray(),
                        Regex.Matches(effect.text, @"[X×÷]").Cast<Match>().Select(match => match.Value).ToArray(), context);
                    if (quote.Contains("四舍五入")) Assert.That(visible, Does.Contain("四舍五入"), context);
                    if (key == "arcane_11") Assert.That(visible, Does.Contain("延迟："), context);
                    if (key == "fire_11") Assert.That(visible, Does.Contain("自燃"), context);
                    AssertVisibleWordsAndClauseBoundaries(source, effect.text, context);
                    Assert.That(effect.preferredHeight, Is.LessThanOrEqualTo(94.01f), context);
                    AssertLinesFitAndHaveContent(effect, context);
                    Assert.That(CardDescriptionText.Compact(CardDescriptionText.Compact(quote)),
                        Is.EqualTo(CardDescriptionText.Compact(quote)), context);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryPublishedCardAtEachRank_FitsFixedEffectAreaAndExceeds90PercentAtSharedSize(int level)
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                CoreVoyageSession session = new CoreVoyageSession();
                int checkedCards = 0, sharedSizeCards = 0;
                var reducedCards = new List<string>();
                var auditedIds = new List<string>();
                for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                {
                    // Retired save-index aliases resolve to live cards, so filter the published source slots first.
                    if (CoreVoyageSession.CardLibrary[index].AllowedStages == NavalBattleStages.None) continue;
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                    if (card == null || card.AllowedStages == NavalBattleStages.None) continue;
                    NavalBattleStage stage = card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding :
                        card.IsAllowedInStage(NavalBattleStage.Naval) ? NavalBattleStage.Naval : NavalBattleStage.Preparation;
                    string quote = session.BattleCardFaceDescription(card, stage);
                    string source = CardFaceLayout.FormatDescription(quote);
                    effect.text = source;
                    CardFaceLayout.FitEffect(effect);
                    string context = card.Id + " rank " + level;
                    Assert.That(effect.rectTransform.sizeDelta, Is.EqualTo(new Vector2(176f, 94f)), context);
                    Assert.That(effect.rectTransform.anchoredPosition, Is.EqualTo(CardFaceLayout.EffectPosition), context);
                    Assert.That(effect.resizeTextForBestFit, Is.False, context);
                    Assert.That(effect.resizeTextMaxSize, Is.EqualTo(CardFaceLayout.EffectFontSize), context);
                    Assert.That(effect.fontSize, Is.InRange(1, CardFaceLayout.EffectFontSize), context);
                    Assert.That(effect.preferredHeight, Is.LessThanOrEqualTo(94.01f), context);
                    AssertVisibleWordsAndClauseBoundaries(source, effect.text, context);
                    CollectionAssert.AreEqual(NumericTokens(quote), NumericTokens(effect.text),
                        context + " keeps authoritative Core numbers, signs and percentages");
                    Assert.That(CardDescriptionText.Compact(CardDescriptionText.Compact(quote)),
                        Is.EqualTo(CardDescriptionText.Compact(quote)), context);
                    AssertLinesFitAndHaveContent(effect, context);
                    AssertNumericTermsUnbroken(source, effect.text, context);
                    string firstLayout = effect.text;
                    int firstSize = effect.fontSize;
                    float firstSpacing = effect.lineSpacing;
                    CardFaceLayout.FitEffect(effect);
                    Assert.That(effect.text, Is.EqualTo(firstLayout), context + " repeat layout is stable");
                    Assert.That(effect.fontSize, Is.EqualTo(firstSize), context);
                    Assert.That(effect.lineSpacing, Is.EqualTo(firstSpacing), context + " repeat line spacing is stable");
                    Assert.That(effect.lineSpacing, Is.InRange(.86f, 1f), context + " uses the visually verified leading range");
                    if (effect.fontSize < CardFaceLayout.EffectFontSize)
                        Assert.That(effect.lineSpacing, Is.EqualTo(1f), context + " smaller type restores normal leading");
                    if (effect.fontSize == CardFaceLayout.EffectFontSize) sharedSizeCards++;
                    else
                    {
                        reducedCards.Add(card.Id + "=" + effect.fontSize);
                        TestContext.WriteLine(context + " size=" + effect.fontSize + ": " + source.Replace('\n', '；'));
                    }
                    checkedCards++;
                    auditedIds.Add(card.Id);
                }
                Assert.That(CoreBoardingCardCatalog.All.Count, Is.EqualTo(103), "The new canonical boarding catalog is included.");
                Assert.That(CoreNavalSchoolExpansionCatalog.All.Count, Is.EqualTo(27), "The six-school naval expansion adds 27 cards; its nine reused cards retain their existing identities.");
                int publishedCards = CoreVoyageSession.CardLibrary.Count(card => card.AllowedStages != NavalBattleStages.None);
                Assert.That(publishedCards, Is.EqualTo(216 + 103 + 27), "Existing naval/legacy cards, 103 canonical boarding cards and 27 appended naval expansion cards; retired aliases are excluded.");
                Assert.That(checkedCards, Is.EqualTo(publishedCards), "Retired slots are excluded, all published cards included.");
                Assert.That(auditedIds.Distinct().Count(), Is.EqualTo(publishedCards), "Each published identity is audited once.");
                CollectionAssert.IsSubsetOf(CoreBoardingCardCatalog.All.Select(card => card.Id).ToArray(), auditedIds,
                    "All 103 new canonical boarding cards pass the same actual text checks.");
                CollectionAssert.IsSubsetOf(CoreNavalSchoolExpansionCatalog.All.Select(card => card.Id).ToArray(), auditedIds,
                    "All 27 naval expansion cards pass the same actual text checks.");
                TestContext.WriteLine("Rank " + level + ": " + sharedSizeCards + "/" + checkedCards + " cards at shared font size " + CardFaceLayout.EffectFontSize);
                Assert.That(sharedSizeCards, Is.GreaterThan(checkedCards * .9),
                    "Each rank independently needs more than 90% of " + publishedCards + " cards at the shared size. Reduced: " +
                    string.Join(", ", reducedCards));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void WorldsEndVolley_FieldBonusRemainsWithoutRetiredManaCost()
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                CoreBattleCard card = CardAtLevel("card_special_cannon_worlds_end_volley", 0);
                Assert.That(card.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ComponentFieldStrike));
                string benefit = "伤害提高" + card.SchoolEffect.Power + "点";
                effect.text = CardFaceLayout.FormatDescription(card.Description);
                CardFaceLayout.FitEffect(effect);
                Assert.That(VisibleWithoutBreaks(effect.text), Does.Contain(benefit),
                    "The conditional field bonus must remain visible on the card face.");
                Assert.That(VisibleWithoutBreaks(effect.text), Does.Not.Contain("消耗"));
                Assert.That(CardSpecialCostPresentation.ResourceType(card), Is.Empty);
                Assert.That(VisibleWithoutBreaks(effect.text), Does.Contain("己方折射阵"));
                Assert.That(effect.fontSize, Is.EqualTo(CardFaceLayout.EffectFontSize));
                AssertLinesFitAndHaveContent(effect, card.Id);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Wrap_KeepsNormalizedNumbersUnitsAndResourcesTogether()
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                const string source = "此次攻击造成 28 点船体伤害并恢复 6 点生命；额外获得 3 层连锁，抽 2 张牌";
                string wrapped = CardDescriptionText.Wrap(effect, source, CardFaceLayout.EffectFontSize);
                Assert.That(VisibleWithoutBreaks(wrapped), Is.EqualTo(source));
                foreach (string term in new[] { "28 点", "船体伤害", "6 点", "3 层", "连锁", "2 张" })
                    Assert.That(wrapped, Does.Contain(term), "Normalized term cannot be split: " + term);
                effect.text = wrapped;
                AssertLinesFitAndHaveContent(effect, "normalized numbers and units");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Wrap_PreservesBloodPaymentPercentagesAndBalancedBoldWithoutPunctuationOnlyRows()
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                const string source = "<b>350米内</b>炮击\n船体伤害56\n" +
                    "支付10本阶段生命，伤害+48（至少留1）\n" +
                    "恢复本次有效直伤的48%，上限56";
                string wrapped = CardDescriptionText.Wrap(effect, source, CardFaceLayout.EffectFontSize);
                Assert.That(VisibleWithoutBreaks(wrapped), Is.EqualTo(VisibleWithoutBreaks(source)));
                Assert.That(StripTags(wrapped), Does.Contain("+48").And.Contain("48%").And.Contain("至少留1"));
                Assert.That(wrapped, Does.Contain("<b>350米内</b>"));
                effect.text = wrapped;
                effect.fontSize = CardFaceLayout.EffectFontSize;
                AssertLinesFitAndHaveContent(effect, "blood payment and percentage");
                AssertNumericTermsUnbroken(source, wrapped, "blood payment and percentage");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void FitEffect_ReflowsOriginalParagraphsAfterWidthChanges()
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                const string source = "<b>350米内</b>炮击\n船体伤害24\n消耗5法力，伤害+44\n消耗1火药、1炮弹";
                effect.text = source;
                CardFaceLayout.FitEffect(effect);
                string wideText = effect.text;
                int wideSize = effect.fontSize;
                float wideSpacing = effect.lineSpacing;
                effect.rectTransform.sizeDelta = new Vector2(96f, 94f);
                CardFaceLayout.FitEffect(effect);
                AssertVisibleWordsAndClauseBoundaries(source, effect.text, "narrow effect region");
                effect.rectTransform.sizeDelta = new Vector2(176f, 94f);
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo(wideText), "Old soft wraps cannot become permanent paragraphs.");
                Assert.That(effect.fontSize, Is.EqualTo(wideSize));
                Assert.That(effect.lineSpacing, Is.EqualTo(wideSpacing), "Width restoration restores the chosen leading.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void FitEffect_ReflowsShortClausesBeforeShrinkingAndRestoresOriginalParagraphs()
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                const string source = "格挡+8\n敌方燃烧：伤害+4\n抽1张\n恢复6\n法力+2\n上限12";
                effect.text = source;
                CardFaceLayout.FitEffect(effect);
                string compactLayout = effect.text;
                float compactSpacing = effect.lineSpacing;
                Assert.That(effect.fontSize, Is.EqualTo(CardFaceLayout.EffectFontSize));
                Assert.That(effect.text, Does.Contain("；"), "Short clauses share rows before shrinking.");
                AssertVisibleWordsAndClauseBoundaries(source, effect.text, "conditional bonus and independent draw");
                Assert.That(effect.preferredHeight, Is.LessThanOrEqualTo(94.01f));
                AssertLinesFitAndHaveContent(effect, "inline clauses");
                CollectionAssert.AreEqual(NumericTokens(source), NumericTokens(effect.text));
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo(compactLayout), "Repeated layout is stable.");
                Assert.That(effect.lineSpacing, Is.EqualTo(compactSpacing));
                effect.rectTransform.sizeDelta = new Vector2(176f, 180f);
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo(source), "Original paragraphs are preferred when they fit.");
                Assert.That(effect.lineSpacing, Is.EqualTo(1f), "A taller area restores normal leading.");
                effect.rectTransform.sizeDelta = new Vector2(176f, 94f);
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo(compactLayout), "Reflow does not overwrite the original source.");
                Assert.That(effect.lineSpacing, Is.EqualTo(compactSpacing));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void FitEffect_LongToShortTextRestoresNormalLeadingAndSharedSize()
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                const string line = "造成16点船体伤害并获得3点格挡";
                string source = string.Join("\n", Enumerable.Repeat(line, 12));
                effect.text = source;
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.fontSize, Is.LessThan(CardFaceLayout.EffectFontSize));
                Assert.That(effect.lineSpacing, Is.EqualTo(1f), "Reduced font sizes use normal leading.");
                AssertVisibleWordsAndClauseBoundaries(source, effect.text, "long description");

                // A reused card face may arrive with the preceding card's tighter leading.
                effect.lineSpacing = .86f;
                effect.text = "获得3点格挡";
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo("获得3点格挡"));
                Assert.That(effect.fontSize, Is.EqualTo(CardFaceLayout.EffectFontSize));
                Assert.That(effect.lineSpacing, Is.EqualTo(1f), "Short replacements must not inherit tighter leading.");
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.lineSpacing, Is.EqualTo(1f));
                Assert.That(effect.fontSize, Is.EqualTo(CardFaceLayout.EffectFontSize));
            }
            finally { Object.DestroyImmediate(host); }
        }

        internal static void AssertVisibleWordsAndClauseBoundaries(string source, string rendered, string context)
        {
            string pattern = "\\A" + string.Join("(?:\\n+|；\\n*)", StripTags(source).Replace("\r", string.Empty)
                .Split('\n').Select(paragraph => string.Join("\\n*", paragraph.Select(character => Regex.Escape(character.ToString()))))) + "\\z";
            Assert.That(StripTags(rendered), Does.Match(pattern),
                context + " preserves every visible character and every original clause boundary as a newline or semicolon");
        }

        private static CoreBattleCard CardAtLevel(string id, int level)
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(value => CoreVoyageSession.CardLibrary[value].Id == id);
            return new CoreVoyageSession().GetBattleCardAtLevel(index, level);
        }

        private static GameObject CreateEffect(out Text effect)
        {
            GameObject host = new GameObject("Description Text Audit", typeof(Canvas));
            host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            effect = new GameObject("Effect", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            effect.transform.SetParent(host.transform, false);
            effect.font = GameTypography.BodyFont;
            Assert.That(effect.font, Is.Not.Null, "The actual embedded body font must be available.");
            effect.fontStyle = FontStyle.Normal;
            effect.fontSize = CardFaceLayout.EffectFontSize;
            effect.lineSpacing = 1f;
            effect.supportRichText = true;
            effect.resizeTextForBestFit = false;
            effect.alignment = TextAnchor.MiddleCenter;
            effect.rectTransform.sizeDelta = new Vector2(176f, 94f);
            effect.rectTransform.anchoredPosition = CardFaceLayout.EffectPosition;
            return host;
        }

        private static void AssertLinesFitAndHaveContent(Text text, string context)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.scaleFactor = 1f;
            settings.fontSize = text.fontSize;
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.richText = true;
            using (var generator = new TextGenerator())
                foreach (string line in text.text.Split('\n'))
                {
                    string visible = StripTags(line);
                    Assert.That(Regex.IsMatch(visible, @"^[\s，。；：！？、（）【】《》%+\-−≥≤]+$"), Is.False,
                        context + " cannot have a punctuation-only row: " + visible);
                    Assert.That(generator.GetPreferredWidth(line, settings),
                        Is.LessThanOrEqualTo(text.rectTransform.rect.width + .05f), context + ": " + visible);
                    Assert.That(Regex.Matches(line, "<b>").Count, Is.EqualTo(Regex.Matches(line, "</b>").Count),
                        context + " each wrapped row keeps balanced emphasis");
                }
        }

        private static void AssertNumericTermsUnbroken(string source, string wrapped, string context)
        {
            const string pattern = @"[+\-−≥≤]?\d+(?:\.\d+)?(?:×\d+)?(?: *(?:%|点|米|格(?!挡)|回合|层|张|发))?";
            string before = StripTags(source), after = StripTags(wrapped);
            foreach (string term in Regex.Matches(before, pattern).Cast<Match>().Select(match => match.Value).Distinct())
                Assert.That(Regex.Matches(after, Regex.Escape(term)).Count,
                    Is.EqualTo(Regex.Matches(before, Regex.Escape(term)).Count), context + " split numeric term: " + term);
        }

        private static string StripTags(string value) => Regex.Replace(value, "<[^>]*>", string.Empty);
        private static string VisibleWithoutBreaks(string value) => StripTags(value).Replace("\r", string.Empty).Replace("\n", string.Empty);
        private static string[] NumericTokens(string value) => Regex.Matches(StripTags(value), @"[+\-−]?\s*\d+(?:\.\d+)?%?")
            .Cast<Match>().Select(match => Regex.Replace(match.Value, @"\s", string.Empty)).ToArray();
    }
}

