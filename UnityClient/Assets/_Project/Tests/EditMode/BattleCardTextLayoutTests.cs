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
                RectTransform surcharge = face.transform.Find("Card Repeat Cost Plate 0") as RectTransform;
                MethodInfo formatter = typeof(CoreGameplayDemoController).GetMethod("BattleCardEffectTextForCurrentStage", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo layout = typeof(CoreGameplayDemoController).GetMethod("RefreshBattleCardTextLayout", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(formatter, Is.Not.Null);
                Assert.That(layout, Is.Not.Null);
                CoreVoyageSession session = new CoreVoyageSession();
                int checkedCards = 0;
                for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                    if (card == null || card.AllowedStages == NavalBattleStages.None) continue;
                    label.text = (string)formatter.Invoke(controller, new object[] { card });
                    string presentationBeforeLayout = label.text;
                    name.text = CardFaceLayout.DisplayName(card);
                    Assert.That(name.text, Is.EqualTo(card.Name + (level > 0 ? "+" + level : string.Empty)));
                    foreach (bool repeatedCost in new[] { false, true })
                    {
                        surcharge.gameObject.SetActive(repeatedCost);
                        layout.Invoke(null, new object[] { label });
                        Assert.That(label.resizeTextForBestFit, Is.False, card.Id);
                        Assert.That(label.supportRichText, Is.True, card.Id + " renders condition emphasis instead of visible tags");
                        Assert.That(name.resizeTextForBestFit, Is.False, card.Id);
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .01f), card.Id);
                        string expected = controller.Session.BattleCardFaceDescription(card, controller.Session.Battle.Stage);
                        Assert.That(Compact(label.text), Is.EqualTo(Compact(presentationBeforeLayout)),
                            card.Id + " wrapping retains the complete presentation quote");
                        CollectionAssert.AreEqual(NumericTokens(expected), NumericTokens(label.text),
                            card.Id + " retains every authoritative current-stage number, sign and percentage");
                        Assert.That(Compact(label.text), Does.Not.Contain("实际").And.Not.Contain("当前炮击").And.Not.Contain("无药基础"), card.Id);
                        Assert.That(NameFits(name), Is.True, card.Id + " retains the complete one-line name and upgrade rank");
                        AssertFixedLayout(face, name, label, card.Id);
                    }
                    checkedCards++;
                }
                Assert.That(checkedCards, Is.EqualTo(218), "Every published card, excluding retired slots, is audited at every rank.");
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
                effect.text = referenceEffect + "\n" + referenceEffect;
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
            Assert.That(text, Does.Contain("<b>平行且距离不超过60米</b>可接舷"));
            Assert.That(text, Does.Not.Contain("<b>靠近"), "Movement is an effect, not a prerequisite.");
        }

        [Test]
        public void DisplayText_WrapsLongEffectsAtSentenceBoundaries()
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

            Assert.That(text, Does.Contain("船体伤害18\n消耗1火药、1炮弹"));
            Assert.That(text, Does.Not.Contain("350 米"));
            Assert.That(text, Does.Contain("<b>350米内</b>"));
        }

        [TestCase("350米内：8 点船体伤害。", "350米内", "350米内：船体伤害8")]
        [TestCase("350 米内：8 点船体伤害。", "350米内", "350米内：船体伤害8")]
        [TestCase("150 米外无法使用。", "150米外", "150米外无法使用")]
        [TestCase("60 米以内可接舷。", "60米以内", "60米以内可接舷")]
        [TestCase("60 米以外无法接舷。", "60米以外", "60米以外无法接舷")]
        [TestCase("距离不超过 60 米可接舷。", "距离不超过60米", "距离不超过60米可接舷")]
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
            Assert.That(Compact(formatted), Is.EqualTo("靠近60米，远离120米船体伤害16消耗1火药、1炮弹格挡+6，最多+12"),
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
            Assert.That(Compact(once), Is.EqualTo("350米内：船体伤害8靠近60米"));
        }

        private static string Compact(string value) => Regex.Replace(value, @"</?(?:b|i|size|color|material)(?:=[^>]*)?>", string.Empty)
            .Replace("\u00a0", " ").Replace("\n", string.Empty).Replace("\r", string.Empty);

        private static string[] NumericTokens(string value) => Regex.Matches(value, @"[+\-−]?\s*\d+(?:\.\d+)?%?")
            .Cast<Match>().Select(match => Regex.Replace(match.Value, @"\s", string.Empty)).ToArray();

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
