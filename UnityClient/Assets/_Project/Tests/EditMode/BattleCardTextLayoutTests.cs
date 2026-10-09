using System.Reflection;
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
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EveryCardAndUpgrade_FitsHandWithoutShrinkingOrClipping(int level)
        {
            GameObject host = new GameObject("Card Text Layout Audit");
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                Button face = host.GetComponentsInChildren<Button>(true).First(item => item.name == "Battle Card 0");
                Text label = face.transform.Find("Label Viewport/Content/Label").GetComponent<Text>();
                MethodInfo formatter = typeof(CoreGameplayDemoController).GetMethod("BattleCardDisplayText", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo layout = typeof(CoreGameplayDemoController).GetMethod("RefreshBattleCardTextLayout", BindingFlags.Static | BindingFlags.NonPublic);
                CoreVoyageSession session = new CoreVoyageSession();
                for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                    if (card == null) continue;
                    label.text = (string)formatter.Invoke(null, new object[] { card, BattleCardStylePalette.ForType(card.Type) });
                    layout.Invoke(null, new object[] { label });
                    Assert.That(label.resizeTextForBestFit, Is.False, card.Id);
                    Assert.That(label.fontSize, Is.EqualTo(GameReadability.FontSize(22)), card.Id);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height), card.Id);
                    ScrollRect scroll = label.GetComponentInParent<ScrollRect>();
                    Assert.That(scroll, Is.Not.Null, card.Id);
                    Assert.That(scroll.viewport.rect.height, Is.LessThanOrEqualTo(222f), card.Id);
                    Assert.That(scroll.content.rect.height, Is.GreaterThanOrEqualTo(label.rectTransform.rect.height), card.Id);
                    Assert.That(label.text.Replace("\u00a0", " "), Does.EndWith(card.Description), card.Id + " retains the complete effect");
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void DisplayText_UsesSeparateRowsAndKeepsDistanceUnitTogether()
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
            Assert.That(text, Does.Contain("</color>\n<size=" + GameReadability.FontSize(18) + ">机动</size>\n"));
            Assert.That(text, Does.Contain("60\u00A0米"));
            Assert.That(text, Does.Not.Contain("60 米"));
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

            Assert.That(text, Does.Contain("伤害；\n"));
            Assert.That(text, Does.Contain("炮弹。"));
            Assert.That(text, Does.Not.Contain("350 米"));
        }
    }
}
