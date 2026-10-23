using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class BattleCardTextLayoutTests
    {
        [TestCase(.3333333f)]
        [TestCase(.5f)]
        [TestCase(1f)]
        public void RibbonTitle_InactiveCreationAndCanvasChangesKeepReadableLogicalGlyphs(float canvasScale)
        {
            GameObject host = new GameObject("Ribbon Raster Scale Audit", typeof(Canvas));
            try
            {
                Canvas canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.scaleFactor = canvasScale;
                GameObject preview = new GameObject("Inactive Hover Preview", typeof(RectTransform));
                preview.transform.SetParent(host.transform, false);
                preview.SetActive(false);
                Text title = new GameObject("Actual Dynamic Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                title.transform.SetParent(preview.transform, false);
                title.rectTransform.sizeDelta = CardFaceLayout.NameSize;
                title.alignment = TextAnchor.MiddleCenter;
                foreach (string content in new[] { "链弹", "炮火齐射+3", CardFaceLayout.ReferenceName })
                {
                    title.text = content;
                    CardFaceLayout.FitName(title);
                    int inactiveFontSize = title.fontSize;
                    Assert.That(inactiveFontSize, Is.GreaterThanOrEqualTo(15), content);
                    preview.SetActive(true);
                    CardFaceLayout.FitName(title);
                    Assert.That(title.fontSize, Is.EqualTo(inactiveFontSize), content + " activation cannot change the logical font");
                    CardRibbonTitleEffect ribbon = title.GetComponent<CardRibbonTitleEffect>();
                    using (var mesh = new VertexHelper())
                    {
                        ribbon.ModifyMesh(mesh);
                        Rect scaledBounds = ribbon.RenderedBounds;
                        Assert.That(ribbon.FitsAuthoredRibbon, Is.True, content);
                        Assert.That(scaledBounds.height, Is.GreaterThanOrEqualTo(16f), content);
                        Assert.That(ribbon.RenderedGlyphCount, Is.EqualTo(content.Count(c => !char.IsWhiteSpace(c))));
                        canvas.scaleFactor = 1f;
                        CardFaceLayout.FitName(title);
                        ribbon.ModifyMesh(mesh);
                        Assert.That(title.fontSize, Is.EqualTo(inactiveFontSize), content + " screenshot conversion cannot shrink or grow titles");
                        Assert.That(ribbon.RenderedBounds, Is.EqualTo(scaledBounds), content + " fitting and drawing use the same logical glyph raster");
                    }
                    canvas.scaleFactor = canvasScale;
                    preview.SetActive(false);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryCardAndUpgrade_KeepsFixedGeometryAndFitsItsCompleteNameAndEffect(int level)
        {
            GameObject host = new GameObject("Card Text Layout Audit");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                Button face = host.GetComponentsInChildren<Button>(true).First(item => item.name == "Battle Card 0");
                Text label = face.transform.Find("Label").GetComponent<Text>();
                Text name = face.GetComponentsInChildren<Text>(true).Single(item => item.name == "Card Name 0");
                Assert.That(face.transform.Find("Card Repeat Cost Plate 0"), Is.Null, "Increased costs use the number color without an extra label.");
                MethodInfo formatter = typeof(CoreGameplayDemoController).GetMethod("BattleCardEffectTextForCurrentStage", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo layout = typeof(CoreGameplayDemoController).GetMethod("RefreshBattleCardTextLayout", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(formatter, Is.Not.Null);
                Assert.That(layout, Is.Not.Null);
                CoreVoyageSession session = new CoreVoyageSession();
                int checkedCards = 0;
                var auditedIds = new System.Collections.Generic.List<string>();
                for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                {
                    // Retired save-index aliases resolve to live cards, so filter the published source slots first.
                    if (CoreVoyageSession.CardLibrary[index].AllowedStages == NavalBattleStages.None) continue;
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                    if (card == null || card.AllowedStages == NavalBattleStages.None) continue;
                    label.text = (string)formatter.Invoke(controller, new object[] { card });
                    string presentationBeforeLayout = label.text;
                    name.text = CardFaceLayout.DisplayName(card);
                    int expectedLevel = System.Math.Min(level, session.GetCardMaximumUpgradeLevel(index));
                    Assert.That(card.UpgradeLevel, Is.EqualTo(expectedLevel), card.Id + " uses its authored upgrade cap");
                    Assert.That(name.text, Is.EqualTo(card.Name + (expectedLevel > 0 ? "+" + expectedLevel : string.Empty)));
                    layout.Invoke(null, new object[] { label });
                    Assert.That(label.resizeTextForBestFit, Is.False, card.Id);
                    Assert.That(label.supportRichText, Is.True, card.Id + " renders condition emphasis instead of visible tags");
                    Assert.That(name.resizeTextForBestFit, Is.False, card.Id);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .01f), card.Id);
                    string expected = controller.Session.BattleCardFaceDescription(card, controller.Session.Battle.Stage);
                    CardDescriptionTextTests.AssertVisibleWordsAndClauseBoundaries(presentationBeforeLayout, label.text,
                        card.Id + " wrapping retains the complete presentation quote");
                    CollectionAssert.AreEqual(NumericTokens(expected), NumericTokens(label.text),
                        card.Id + " retains every authoritative current-stage number, sign and percentage");
                    Assert.That(Compact(label.text), Does.Not.Contain("人接舷实际").And.Not.Contain("当前炮击").And.Not.Contain("无药基础"), card.Id);
                    Assert.That(NameFits(name), Is.True, card.Id + " retains the complete one-line name and upgrade rank");
                    AssertFixedLayout(face, name, label, card.Id);
                    checkedCards++;
                    auditedIds.Add(card.Id);
                }
                Assert.That(CoreBoardingCardCatalog.All.Count, Is.EqualTo(103), "The new canonical boarding catalog is included.");
                Assert.That(CoreNavalSchoolExpansionCatalog.All.Count, Is.EqualTo(27), "The six-school naval expansion adds 27 cards; its nine reused cards retain their existing identities.");
                int publishedCards = CoreVoyageSession.CardLibrary.Count(card => card.AllowedStages != NavalBattleStages.None);
                Assert.That(publishedCards, Is.EqualTo(216 + 103 + 27), "Existing naval/legacy cards, 103 canonical boarding cards and 27 appended naval expansion cards; retired aliases are excluded.");
                Assert.That(checkedCards, Is.EqualTo(publishedCards), "Every published card, excluding retired slots, is audited at every rank.");
                Assert.That(auditedIds.Distinct().Count(), Is.EqualTo(publishedCards), "Each published identity is audited once.");
                CollectionAssert.IsSubsetOf(CoreBoardingCardCatalog.All.Select(card => card.Id).ToArray(), auditedIds,
                    "All 103 new canonical boarding cards pass the same actual layout checks.");
                CollectionAssert.IsSubsetOf(CoreNavalSchoolExpansionCatalog.All.Select(card => card.Id).ToArray(), auditedIds,
                    "All 27 naval expansion cards pass the same actual layout checks.");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ReferenceNameAndSharedEffectFont_KeepIndependentMaximumSizes()
        {
            GameObject host = new GameObject("Card Font Calibration Audit");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                Button face = host.GetComponentsInChildren<Button>(true).First(item => item.name == "Battle Card 0");
                Text effect = face.transform.Find("Label").GetComponent<Text>();
                Text name = face.GetComponentsInChildren<Text>(true).Single(item => item.name == "Card Name 0");
                int turnIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_full_sail_turn");
                CoreBattleCard turn = new CoreVoyageSession().GetBattleCardAtLevel(turnIndex, 3);
                string referenceName = "领航员 · 抢占航线+3";
                string referenceEffect = CardFaceLayout.FormatDescription(turn.Description);
                name.text = referenceName;
                effect.text = referenceEffect;
                CardFaceLayout.FitName(name);
                CardFaceLayout.FitEffect(effect);
                int maximumNameSize = name.fontSize;
                int maximumEffectSize = CardFaceLayout.EffectFontSize;
                Assert.That(NameFits(name), Is.True);
                Assert.That(EffectFits(effect), Is.True);
                Assert.That(effect.fontSize, Is.LessThanOrEqualTo(maximumEffectSize));
                Assert.That(effect.resizeTextMaxSize, Is.EqualTo(maximumEffectSize));
                name.fontSize = maximumNameSize + 1;
                Assert.That(NameFits(name), Is.False, "The reference name should use the largest size that fits its fixed field.");

                name.text = "齐射";
                effect.text = "船体伤害 8。";
                CardFaceLayout.FitName(name);
                CardFaceLayout.FitEffect(effect);
                Assert.That(name.fontSize, Is.EqualTo(maximumNameSize), "Short names do not enlarge beyond the shared reference size.");
                Assert.That(effect.fontSize, Is.EqualTo(maximumEffectSize), "Short effects use the shared effect size, independent of the reference sentence.");
                name.text = referenceName + "与舰队共同驶向远方航道";
                CardFaceLayout.FitName(name);
                Assert.That(name.fontSize, Is.LessThan(maximumNameSize));
                Assert.That(NameFits(name), Is.True);
                Assert.That(effect.fontSize, Is.EqualTo(maximumEffectSize), "A long name does not shrink its effect.");
                int longNameSize = name.fontSize;
                // Keep this independent-size fixture long even when a real
                // card's wording is compacted without changing its effect.
                effect.text = string.Join("\n", Enumerable.Repeat("造成8点船体伤害。", 12));
                CardFaceLayout.FitEffect(effect);
                Assert.That(effect.fontSize, Is.LessThan(maximumEffectSize));
                Assert.That(EffectFits(effect), Is.True);
                Assert.That(name.fontSize, Is.EqualTo(longNameSize), "A long effect does not resize its name.");
                AssertFixedLayout(face, name, effect, "reference texts");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void DisplayText_UsesSeparateFieldsAndCompactDistanceConditions()
        {
            CoreBattleCard card = new CoreBattleCard(
                "layout_test", "并排行船", "机动", 1, 0, 0, 0, 0, 0,
                "靠近 60 米并转为平行；平行且距离不超过 60 米可接舷。",
                NavalBattleStages.Ship, NavalBattleCardAction.Parallel);
            MethodInfo formatter = typeof(CoreGameplayDemoController).GetMethod(
                "BattleCardDisplayText", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(formatter, Is.Not.Null);
            BattleCardPalette palette = BattleCardStylePalette.ForType(card.Type);
            string text = (string)formatter.Invoke(null, new object[] { card, palette });

            Assert.That(text, Does.Contain("并排行船"));
            Assert.That(text, Does.Contain("</color>\n<size=13>机动</size>\n"));
            Assert.That(text, Does.Contain("60米"));
            Assert.That(text, Does.Not.Contain("60 米"));
            Assert.That(text, Does.Not.Contain("舷侧相邻").And.Not.Contain("行动点接舷"),
                "Shared boarding requirements do not belong in a movement card's effect text.");
            Assert.That(text, Does.Not.Contain("距离不超过60米"), "Retired distance-only boarding wording must not override the tactical grid rule.");
            Assert.That(text, Does.Not.Contain("<b>靠近"), "Movement is an effect, not a prerequisite.");
        }

        [Test]
        public void DisplayText_KeepsDamageAndRangeWhileCostsUseIcons()
        {
            CoreBattleCard card = new CoreBattleCard(
                "wrap_test", "炮火齐射", "炮击", 2, 18, 0, 0, 0, 1,
                "350 米内：18 点船体伤害；消耗 1 火药、1 炮弹。",
                NavalBattleStages.Naval, NavalBattleCardAction.Cannon);
            MethodInfo formatter = typeof(CoreGameplayDemoController).GetMethod(
                "BattleCardDisplayText", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(formatter, Is.Not.Null);
            BattleCardPalette palette = BattleCardStylePalette.ForType(card.Type);
            string text = (string)formatter.Invoke(null, new object[] { card, palette });

            Assert.That(text, Does.Contain("造成18点船体伤害"));
            Assert.That(text, Does.Not.Contain("消耗").And.Not.Contain("火药").And.Not.Contain("炮弹"));
            Assert.That(text, Does.Not.Contain("350"));
            Assert.That(Compact(text), Does.Contain("范围1-4格：造成18点船体伤害"));
        }

        [TestCase("4格内：8 点船体伤害。", "4格内", "4格内：造成8点船体伤害")]
        [TestCase("4 格内：8 点船体伤害。", "4格内", "4格内：造成8点船体伤害")]
        [TestCase("4 格以内可炮击。", "4格以内", "4格以内可炮击")]
        [TestCase("距离不超过 4 格可炮击。", "距离不超过4格", "距离不超过4格可炮击")]
        [TestCase("350米内：8 点船体伤害。", "350米内", "350米内：造成8点船体伤害")]
        [TestCase("350 米内：8 点船体伤害。", "350米内", "350米内：造成8点船体伤害")]
        [TestCase("150 米外无法使用。", "150米外", "150米外无法使用")]
        [TestCase("60 米以内可接舷。", "60米以内", "60米以内可接舷")]
        [TestCase("60 米以外无法接舷。", "60米以外", "60米以外无法接舷")]
        [TestCase("距离不超过 60 米可接舷。", "距离不超过60米", "距离不超过60米可接舷")]
        [TestCase("两舰平行且舷侧相邻时，消耗1行动点接舷。", "两舰平行且舷侧相邻", "两舰平行且舷侧相邻时，消耗1点行动点接舷")]
        [TestCase("两舰平行且舷侧相邻（100 米）时，消耗 1 行动点接舷。", "两舰平行且舷侧相邻（100米）", "两舰平行且舷侧相邻（100米）时，消耗1点行动点接舷")]
        [TestCase("须再次推进才能冲撞。", "须再次推进才能冲撞", "须再次推进才能冲撞")]
        [TestCase("须推进后才能冲撞。", "须推进后才能冲撞", "须推进后才能冲撞")]
        [TestCase("靠近 60 米，再次推进后可冲撞。", "再次推进后", "靠近60米，再次推进后可冲撞")]
        public void FormatDescription_EmphasizesConditionsAndUsesExplicitCompactWording(string source, string condition, string expectedVisible)
        {
            string formatted = CardFaceLayout.FormatDescription(source);
            Assert.That(formatted, Does.Contain("<b>" + condition + "</b>"));
            Assert.That(Regex.Matches(formatted, "<b>").Count, Is.EqualTo(1));
            Assert.That(Compact(formatted), Is.EqualTo(expectedVisible));
        }

        [Test]
        public void FormatDescription_LeavesMovementDamageCostsAndStackLimitsUnemphasized()
        {
            const string source = "靠近 60 米，远离 120 米；船体伤害 16；消耗 1 火药、1 炮弹；格挡 +6，最多 +12。";
            string formatted = CardFaceLayout.FormatDescription(source);
            Assert.That(formatted, Does.Not.Contain("<b>"));
            Assert.That(Compact(formatted), Is.EqualTo("靠近60米，远离120米造成16点船体伤害消耗1火药、1炮弹获得6点格挡，最多+12"),
                "The compact wording retains movement, target, costs and stack cap without inventing prerequisites.");
        }

        [Test]
        public void FormatDescription_PreservesExistingBoldAndIsIdempotent()
        {
            const string source = "<b>350 米内</b>：8 点船体伤害；<b>靠近 60 米</b>。";
            string once = CardFaceLayout.FormatDescription(source);
            string twice = CardFaceLayout.FormatDescription(once);
            Assert.That(twice, Is.EqualTo(once));
            Assert.That(once, Does.Contain("<b>350米内</b>").And.Contain("<b>靠近60米</b>"));
            Assert.That(Regex.Matches(once, "<b>").Count, Is.EqualTo(2));
            Assert.That(Compact(once), Is.EqualTo("350米内：造成8点船体伤害靠近60米"));
        }

        private static string Compact(string value) => Regex.Replace(value, @"</?(?:b|i|size|color|material)(?:=[^>]*)?>", string.Empty)
            .Replace("\u00a0", " ").Replace("\n", string.Empty).Replace("\r", string.Empty);

        [Test]
        public void NumericTokens_PreservesGridRangesAcrossRichTextAndWrapping()
        {
            string[] expected = { "1-4", "1" };
            const string source = "将1-4格内的己方1格火场移动到相邻的格子。";
            CollectionAssert.AreEqual(expected, NumericTokens(source));
            CollectionAssert.AreEqual(expected, NumericTokens(CardFaceLayout.FormatDescription(source)));
            CollectionAssert.AreEqual(expected, NumericTokens("将1-<b>4格内</b>的己方1格火场移动到相邻的格子。"));
            CollectionAssert.AreEqual(expected, NumericTokens("将1–\n<b>4格内</b>的己方1格火场移动到相邻的格子。"));
            CollectionAssert.AreEqual(expected, NumericTokens("将 1-4 格内的己方 1 格火场移动到相邻的格子。"));
        }

        [Test]
        public void FormatDescription_PreservesRealNegativeAndPositiveEffectValues()
        {
            const string source = "范围1-4格：伤害修正 -3 点；比例 -12.5%；格挡修正 +2 点；生命修正 −4 点。";
            string[] expected = { "1-4", "-3", "-12.5%", "+2", "−4" };
            CollectionAssert.AreEqual(expected, NumericTokens(source));
            CollectionAssert.AreEqual(expected, NumericTokens(CardFaceLayout.FormatDescription(source)));
            CollectionAssert.AreNotEqual(NumericTokens("修正 -3 点"), NumericTokens("修正 3 点"),
                "A real negative effect must never compare equal after losing its minus sign.");
        }

        private static string[] NumericTokens(string value)
        {
            string visible = Regex.Replace(value, @"</?(?:b|i|size|color|material)(?:=[^>]*)?>", string.Empty);
            // A grid range is one token; its separator is not a negative sign.
            // Strip rich tags first because condition emphasis can split 1-<b>4格内</b>.
            return Regex.Matches(visible, @"(?<range>\s*\d+(?:\.\d+)?\s*[-–—～~]\s*\d+(?:\.\d+)?)(?=\s*格)|[+\-−]?\s*\d+(?:\.\d+)?%?")
                .Cast<Match>().Select(match =>
                {
                    string token = Regex.Replace(match.Value, @"\s", string.Empty);
                    return match.Groups["range"].Success ? Regex.Replace(token, "[–—～~]", "-") : token;
                }).ToArray();
        }

        private static bool NameFits(Text name) => name.preferredWidth <= name.rectTransform.rect.width + .01f &&
            name.preferredHeight <= name.rectTransform.rect.height + .01f &&
            name.GetComponent<CardRibbonTitleEffect>() != null && name.GetComponent<CardRibbonTitleEffect>().GeneratedTextFits(name);

        private static bool EffectFits(Text effect) => effect.preferredHeight <= effect.rectTransform.rect.height + .01f;

        private static void AssertFixedLayout(Button face, Text name, Text effect, string context)
        {
            RectTransform namePlate = face.transform.Find("Card Name Plate 0") as RectTransform;
            RectTransform art = face.transform.Find("Card Art 0") as RectTransform;
            Assert.That(namePlate, Is.Not.Null, context);
            Assert.That(art, Is.Not.Null, context);
            Assert.That(face.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.CardSize), context);
            Assert.That(art.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).size), context);
            Assert.That(art.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(CardFaceLayout.IllustrationLayout(art.GetComponent<RawImage>()).center), context);
            Assert.That(namePlate.sizeDelta, Is.EqualTo(CardFaceLayout.NamePlateSize), context);
            Assert.That(namePlate.anchoredPosition, Is.EqualTo(CardFaceLayout.NamePlateLayout(namePlate)), context);
            Assert.That(name.rectTransform.sizeDelta, Is.EqualTo(new Vector2(174f, 26f)), context);
            Assert.That(effect.rectTransform.sizeDelta, Is.EqualTo(new Vector2(176f, 94f)), context);
            Assert.That(effect.rectTransform.anchoredPosition, Is.EqualTo(CardFaceLayout.EffectPosition), context);
            Assert.That(face.transform.Find("Card Type Plate 0"), Is.Null, context + " reserves no category strip");
            Assert.That(face.GetComponentsInChildren<Text>(true).Any(item => item.name == "Card Type 0"), Is.False,
                context + " communicates category through its frame instead of a text row");
            Assert.That(LocalRect(namePlate).yMin, Is.GreaterThanOrEqualTo(LocalRect(effect.rectTransform).yMax), context);
            // The curved PNG ribbon is not the title's rectangular text box.
            // Native alpha coverage validates its seam; UI preserves full UVs.
            Assert.That(art.GetComponent<RawImage>().uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), context);
            CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(frame, Is.Not.Null, context);
            Assert.That(art.GetSiblingIndex(), Is.LessThan(frame.transform.GetSiblingIndex()), context);
            Assert.That(namePlate.GetSiblingIndex(), Is.GreaterThan(frame.transform.GetSiblingIndex()),
                context + " live title is above the opaque PNG strip");
            Assert.That(effect.transform.GetSiblingIndex(), Is.GreaterThan(frame.transform.GetSiblingIndex()), context);
        }

        private static Rect LocalRect(RectTransform transform)
        {
            Rect rect = transform.rect;
            rect.position += transform.anchoredPosition;
            return rect;
        }
    }
}
