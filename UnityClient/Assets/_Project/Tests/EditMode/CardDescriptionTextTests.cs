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
            "恢复14船员生命\n消耗3法力，恢复+18\n抽1张")]
        [TestCase("恢复 8 点己方船员生命。", "恢复8点己方船员生命")]
        [TestCase("格挡 +4；恢复 18 点船员生命；上一敌回合有效格挡：额外恢复 18。",
            "格挡+4，恢复18船员生命\n上一敌回合有效格挡：额外恢复18")]
        [TestCase("敌方燃烧：格挡 +4；恢复 18 点船员生命。",
            "敌方燃烧：格挡+4\n恢复18船员生命")]
        [TestCase("支付 10 当前阶段生命，额外伤害 +48（至少留1）。",
            "支付10本阶段生命，伤害+48（至少留1）")]
        [TestCase("敌有至少 3 咒印：格挡额外 +14。", "敌咒印≥3：格挡+14")]
        [TestCase("敌方生命不高于 40%：额外伤害 +30。",
            "敌生命≤40%：伤害+30")]
        [TestCase("出牌前敌方燃烧：伤害额外 +12。",
            "出牌前敌方燃烧：伤害+12")]
        [TestCase("此前连锁至少 2：抽 1 张；\n每回合一次；靠近 120 米并转为平行。抽 1 张当前阶段牌。",
            "此前连锁≥2：抽1张（每回合一次）\n靠近120米并转为平行\n抽1张本阶段牌")]
        [TestCase("法力 +4（上限 12，跨阶段保留）。",
            "法力+4（上限12，跨阶段保留）")]
        [TestCase("连锁 +3（上限 6，本回合有效）。",
            "连锁+3（上限6，本回合有效）")]
        [TestCase("原已燃烧则额外结算一次，每回合限一次。",
            "原已燃烧：额外结算一次（每回合限一次）")]
        [TestCase("上一敌回合吸收至少 8 伤害：额外伤害 +18。",
            "上一敌回合吸收伤害≥8：伤害+18")]
        [TestCase("接下来 2 个敌回合吸收至少 4 伤害后反击 10，每回合一次。",
            "下2个敌回合吸收≥4伤害后反击10，每回合一次")]
        [TestCase("施加 2 咒印，持续 3 回合；每层敌攻击 -4%，最多 6 层。",
            "施加2咒印（3回合）\n每层敌攻击-4%，最多6层")]
        [TestCase("恢复本次有效直伤的 50%，上限 28。",
            "恢复本次有效直伤的50%，上限28")]
        [TestCase("恢复已失生命的 35%，上限 18。",
            "恢复已失生命的35%，上限18")]
        [TestCase("消耗所有咒印，每层追加 6 伤害。",
            "消耗所有咒印，每层追加6伤害")]
        [TestCase("格挡 +8；消耗 2 法力，\n格挡额外 +16。",
            "格挡+8\n消耗2法力，格挡+16")]
        [TestCase("<b>4\u00a0格内</b>炮击；28 点船体伤害；消耗 5 法力，伤害额外 +44。",
            "<b>4格内</b>炮击：船体伤害28\n消耗5法力，伤害+44")]
        [TestCase("<b>350\u00a0米内</b>炮击；28 点船体伤害；消耗 5 法力，伤害额外 +44。",
            "<b>350米内</b>炮击：船体伤害28\n消耗5法力，伤害+44")]
        [TestCase("格挡+8\r抽1张", "格挡+8\n抽1张")]
        [TestCase("说明！\n格挡+8", "说明！\n格挡+8")]
        [TestCase("自身贡献上限 8、总上限 16。", "本牌上限8、总上限16")]
        [TestCase("造成1.5点船体伤害。", "造成1.5点船体伤害")]
        [TestCase("获得 1 点行动点；格挡 +4；抽 2 张；敌方燃烧：格挡 +8。",
            "获得1行动点，格挡+4，抽2张\n敌方燃烧：格挡+8")]
        [TestCase("敌方燃烧：格挡 +4；恢复 18 点船员生命；抽 1 张。",
            "敌方燃烧：格挡+4\n恢复18船员生命，抽1张")]
        [TestCase("格挡 +8；耗 1 木板；此盾挡伤蓄潮势；获得 1 点行动点；抽 2 张；无木板仅基础格挡2，不蓄势。",
            "格挡+8（耗1木板）\n此盾挡伤蓄潮势\n获得1行动点，抽2张\n无木板仅基础格挡2，不蓄势")]
        [TestCase("使用1药物恢复14，绝境时恢复18；无药物恢复2，绝境时恢复3。绝境：治疗前生命不高于最大生命40%。",
            "使用1药物恢复14，绝境时恢复18，无药物恢复2，绝境时恢复3\n绝境：治疗前生命≤40%上限")]
        [TestCase("新增2层自燃；获得 16 格挡。本回合首次实际用格挡吸收敌方攻击后，反伤当前自燃层数×2，最多8；反伤受格挡，不暴击。",
            "新增2层自燃，获得16格挡\n本回合首次格挡吸收敌方攻击后，反伤当前自燃层数×2，最多8\n反伤受格挡，不暴击")]
        [TestCase("本回合接下来3张主动雷霆攻击各追加8伤害；不暴击、不产链，多段只追加一次。迟雷追加一起延迟；不叠加。",
            "本回合下3张主动雷霆攻击各追加8伤害\n不暴击、不产链，多段只追加一次\n迟雷追加同延迟，不叠加")]
        [TestCase("咒痕上限6，每批持续3次敌方回合结束，本身不造成伤害。",
            "咒痕上限6，每批3次敌回合末后消失，无伤害")]
        [TestCase("下一己方回合开始造成(36+8X)×1.5（结果四舍五入）伤害，施放时锁定；受格挡，不暴击。",
            "下个己方回合初造成(36+8X)×1.5（四舍五入）伤害，施放锁定\n受格挡，不暴击")]
        [TestCase("本回合和之后两个己方回合，每回合首次主动新增自燃时，为敌方附加相同层数的灼烧，单次最多3层。不叠加、不刷新。",
            "本回合及随后两个己方回合，每回合首次主动新增自燃时，敌获等层灼烧，单次最多3层\n不叠加、不刷新")]
        [TestCase("格挡 +4；抽 2 张；每回合一次；恢复 18 点船员生命。",
            "格挡+4\n抽2张（每回合一次）\n恢复18船员生命")]
        [TestCase("消耗 3 火药；消耗 1 炮弹；实际造成直伤后灼烧 14×2 回合；目标出牌前已燃烧时额外结算一次燃烧，每回合一次。",
            "消耗3火药、1炮弹\n有效直伤后灼烧14×2回合\n出牌前目标已燃烧：额外结算一次燃烧（每回合一次）")]
        [TestCase("150 米内近距破坏；造成 28 点船体伤害。", "150米内近距破坏：船体伤害28")]
        [TestCase("激活本场海战专属规则：每回合首张其他秘术牌成功后，恢复 1 法力，不超过上限；同类不叠加，接舷或战斗结束清除。",
            "本场海战：每回合首张其他秘术牌成功后，恢复1法力，不超过上限\n同类不叠加，接舷或战斗结束清除")]
        [TestCase("本牌格挡实际吸收敌方攻击时等量获得潮势，上限 20；无木板时仅获得 2 普通格挡。",
            "本牌格挡吸收敌方攻击等量产潮势，上限20\n无木板仅获2普通格挡")]
        [TestCase("4 格曼哈顿范围，每一格须合法；最迟于目标下个回合结束失效。",
            "曼哈顿4格内，各格合法\n最迟目标下回合末失效")]
        [TestCase("每回合首张其他火焰牌成功后，点燃敌舰，造成 3×2 回合燃烧；同类不叠加，接舷或战斗结束清除。",
            "每回合首张其他火焰牌成功后，敌舰燃烧3×2回合\n同类不叠加，接舷或战斗结束清除")]
        [TestCase("同名副本每回合共用一次成功额度，衍生伤害不触发。", "同名每回合共限一次成功，衍生伤害不触发")]
        [TestCase("目标下一次完整攻击总伤害减少(2+向下取整的出牌前连锁÷2)×2.2（结果四舍五入），最多11且不超过一半。",
            "目标下次完整攻击减伤(2+向下取整的出牌前连锁÷2)×2.2（四舍五入），最多11且不超过一半")]
        public void Compact_HasExplicitMeaningPreservingResults(string source, string expected)
        {
            string actual = CardDescriptionText.Compact(source);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(CardDescriptionText.Compact(actual), Is.EqualTo(actual), "Compaction is idempotent.");
            CollectionAssert.AreEqual(NumericTokens(source), NumericTokens(actual),
                "Compaction preserves each number, sign and percentage in order.");
        }

        [Test]
        public void Compact_PreservesFireTimingLimitsAndBothLifePoolsInFullSentences()
        {
            const string source = "炮战修复 3 船体，白刃战恢复 3 船员生命；" +
                "本阶段每回合首次满额延烧：装填 +6；自身贡献上限 12、总上限 24；" +
                "命中灼烧 8×2回合；首次接舷灼烧 8×2回合；" +
                "接下来 2 个敌回合，自然燃烧造成伤害后获得 6 格挡。";
            const string expected = "炮战修复3船体，白刃战恢复3船员生命\n" +
                "本阶段每回合首次满额延烧：装填+6\n本牌上限12、总上限24\n" +
                "命中灼烧8×2回合\n首次接舷灼烧8×2回合\n" +
                "下2个敌回合，自然燃烧造成伤害后获得6格挡";
            Assert.That(CardDescriptionText.Compact(source), Is.EqualTo(expected));
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
                    CardFaceLayout.FitEffect(effect);
                    Assert.That(effect.text, Is.EqualTo(firstLayout), context + " repeat layout is stable");
                    Assert.That(effect.fontSize, Is.EqualTo(firstSize), context);
                    if (effect.fontSize == CardFaceLayout.EffectFontSize) sharedSizeCards++;
                    else reducedCards.Add(card.Id + "=" + effect.fontSize);
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
        public void WorldsEndVolley_ManaCostAndDamageBenefitStayOnOneActualLine()
        {
            GameObject host = CreateEffect(out Text effect);
            try
            {
                CoreBattleCard card = CardAtLevel("card_special_cannon_worlds_end_volley", 0);
                Assert.That(card.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ArcaneRelease));
                string paymentAndBenefit = "消耗" + card.SchoolEffect.Threshold + "法力，伤害+" + card.SchoolEffect.Power;
                effect.text = CardFaceLayout.FormatDescription(card.Description);
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text.Split('\n').Any(line => StripTags(line).Contains(paymentAndBenefit)),
                    Is.True, "The mana payment and its damage bonus remain on one measured line.");
                Assert.That(effect.fontSize, Is.EqualTo(CardFaceLayout.EffectFontSize));
                AssertLinesFitAndHaveContent(effect, card.Id);
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
                effect.rectTransform.sizeDelta = new Vector2(96f, 94f);
                CardFaceLayout.FitEffect(effect);
                AssertVisibleWordsAndClauseBoundaries(source, effect.text, "narrow effect region");
                effect.rectTransform.sizeDelta = new Vector2(176f, 94f);
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo(wideText), "Old soft wraps cannot become permanent paragraphs.");
                Assert.That(effect.fontSize, Is.EqualTo(wideSize));
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
                Assert.That(effect.fontSize, Is.EqualTo(CardFaceLayout.EffectFontSize));
                Assert.That(effect.text, Does.Contain("；"), "Short clauses share rows before shrinking.");
                AssertVisibleWordsAndClauseBoundaries(source, effect.text, "conditional bonus and independent draw");
                Assert.That(effect.preferredHeight, Is.LessThanOrEqualTo(94.01f));
                AssertLinesFitAndHaveContent(effect, "inline clauses");
                CollectionAssert.AreEqual(NumericTokens(source), NumericTokens(effect.text));
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo(compactLayout), "Repeated layout is stable.");
                effect.rectTransform.sizeDelta = new Vector2(176f, 180f);
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo(source), "Original paragraphs are preferred when they fit.");
                effect.rectTransform.sizeDelta = new Vector2(176f, 94f);
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.text, Is.EqualTo(compactLayout), "Reflow does not overwrite the original source.");
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
            const string pattern = @"[+\-−≥≤]?\d+(?:\.\d+)?(?:×\d+)?(?:%|米|格(?!挡)|回合|层|张|发)?";
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

