using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator BattleRules_ActualPointerCardsQuoteSharedCostAndResetOnTurnAndStage()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            int brace = ResponsiveCardIndex("card_brace");
            SetResponsiveHand(session, "card_brace", "card_brace", "card_brace");
            session.Battle.Energy = 1;
            RenderResponsiveBattle(controller);
            yield return null;
            Assert.That(RequiredResponsive<Text>("Battle Card Cost Rule").text, Does.Contain("副本共享").And.Contain("切换阶段"));
            AssertBattleRuleLayout(false);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("0"));
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("1"));
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 1").text, Is.EqualTo("1"));
            Assert.That(RequiredResponsive<Text>("Card Repeat Cost Text 0").text, Does.Contain("已用 1 次"));
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("2"));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").interactable, Is.False);
            Text explanation = RequiredResponsive<Button>("Battle Card 0").transform
                .Find("Card Keyword Bubble 0/Keyword Bubble Text Viewport/Content/Keyword Bubble Text").GetComponent<Text>();
            Assert.That(explanation.text.Replace("\n", string.Empty), Does.Contain("基础 0").And.Contain("已用 2").And.Contain("切换阶段"));
            NavalBattleVisualCapture.Capture(controller, "BattleRules_01_Shared_Cost.png",
                "同名牌成功打出两次后，剩余副本显示2费并因能量不足禁用", "Battle Card");

            ClickAcceptanceButton("End Battle Turn");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.GetBattleCardEnergyCost(brace), Is.Zero, "The preparation-to-naval transition resets repeated costs.");
            AssertResponsiveHandMatchesCore(session);
            AssertBattleRuleLayout(false);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_02_Stage_Reset.png",
                "备战转炮战后恢复基础费用，所有手牌费标与实际费用一致", "Battle Card");

            // Arrange a same-stage repeat independently of the transition's
            // shuffled draw. Only pointer actions perform the tested mutations.
            SetResponsiveHand(session, "card_brace", "card_brace", "card_brace");
            SetResponsiveDrawPile(session, "card_brace", "card_brace", "card_brace", "card_brace", "card_brace");
            session.Battle.DistanceMeters = 200f;
            session.Battle.Energy = 1;
            RenderResponsiveBattle(controller);
            yield return null;
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.GetBattleCardEnergyCost(brace), Is.EqualTo(2));
            int turn = session.Battle.Turn;
            ClickAcceptanceButton("End Battle Turn");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.Turn, Is.EqualTo(turn + 1));
            Assert.That(session.GetBattleCardEnergyCost(brace), Is.Zero);
            AssertResponsiveHandMatchesCore(session);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_03_Turn_Reset.png",
                "同一炮战阶段进入新回合后费用恢复，重复牌加费短签消失", "Battle Card");
            Assert.That(GameObject.Find("Card Repeat Cost Text 0"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleRules_EnemyRamIntentAndPointerEndTurnExposeFirstStrikeAndCrewHealth()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            CoreBattleState battle = session.Battle;
            var profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Raider,
                battle.EnemyMaxHull, 8, 11, 0, 0, battle.EnemyOrdinaryCrew, 1f, 1f, 1d, 120f);
            typeof(CoreBattleState).GetProperty("Profile", BindingFlags.Instance | BindingFlags.Public)
                .SetValue(battle, profile);
            battle.DistanceMeters = 80f;
            battle.Alignment = NavalBattleAlignment.HeadOn;
            SetResponsiveHand(session, "card_retreat", "card_approach");
            RenderResponsiveBattle(controller);
            yield return null;
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(RequiredResponsive<Text>("Enemy Intent Glyph").text, Is.EqualTo("撞"));
            Assert.That(RequiredResponsive<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + session.GetEnemyIntent()));
            Assert.That(RequiredResponsive<Text>("Enemy Intent Text").text, Does.Contain("先手"));
            Assert.That(RequiredResponsive<Text>("Player Battle Name").text,
                Does.Contain("普通水手 " + battle.PlayerOrdinaryCrew + " 人").And.Contain("接舷生命上限 " + battle.PlayerMaxBoardingHealth));
            AssertBattleRuleLayout(true);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_04_Enemy_Ram_Intent.png",
                "近距离敌舰明确预告冲撞、接舷与先手伤害，双方显示普通水手及接舷生命上限", "Battle Card");
            battle.EnemyBurnTurns = 2;
            battle.EnemyBurnDamage = 10;
            RenderResponsiveBattle(controller);
            yield return null;
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(RequiredResponsive<Text>("Enemy Intent Text").text, Does.Contain("灼烧"));
            AssertBattleRuleLayout(true);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_04a_Burning_Ram_Intent.png",
                "灼烧结算前缀与撞击、先手及避让距离同时显示且保持可读字号", "Battle Card");
            battle.EnemyBurnTurns = 0;
            battle.EnemyBurnDamage = 0;
            RenderResponsiveBattle(controller);
            yield return null;
            int sceneId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            int intentId = RequiredResponsive<RectTransform>("Enemy Intent HUD").GetInstanceID();
            ClickAcceptanceButton("Battle Card 0"); // Actually play retreat: 80 -> 210 m.
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.EnemyWillRam, Is.False);
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(sceneId));
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent HUD").GetInstanceID(), Is.EqualTo(intentId));
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent HUD").rect.width, Is.EqualTo(210f).Within(.01f));
            Assert.That(RequiredResponsive<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + session.GetEnemyIntent()));
            AssertBattleRuleLayout(false);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_04b_Ram_Avoided.png",
                "真实拉开距离后保留原战斗视图，侧栏意图按当前内容调整高度", "Battle Card");
            ClickAcceptanceButton("Battle Card 0"); // Actually approach: 210 -> 60 m.
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent HUD").GetInstanceID(), Is.EqualTo(intentId));
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent HUD").rect.width, Is.EqualTo(210f).Within(.01f));
            AssertBattleRuleLayout(true);
            int crewBefore = battle.PlayerBoardingHealth;
            int firstStrike = session.EnemyRamFirstStrikeDamage;
            ClickAcceptanceButton("End Battle Turn");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(battle.BoardingInitiatedByEnemy, Is.True);
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(crewBefore - firstStrike));
            Assert.That(RequiredResponsive<Text>("Battle Alignment").text, Does.Contain("接舷先手：敌方"));
            Assert.That(RequiredResponsive<Text>("Player Battle Name").text, Does.Contain("守备队"));
            Assert.That(RequiredResponsive<Text>("Enemy Battle Name").text, Does.Contain("登船队"));
            Assert.That(RequiredResponsive<Text>("Player Ship Art Formation Label").text, Does.Contain("守备队"));
            Assert.That(RequiredResponsive<Text>("Enemy Ship Art Formation Label").text, Does.Contain("登船队"));
            Assert.That(RequiredResponsive<Text>("Battle Active Buffs").text,
                Does.Contain("普通水手 " + battle.PlayerOrdinaryCrew + " 人")
                    .And.Contain("卡牌效果 " + (CoreBattleCardEffects.BoardingScaleFor(battle.PlayerOrdinaryCrew) * 100m)
                        .ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%")
                    .And.Contain(battle.PlayerBoardingHealth + "/" + battle.PlayerMaxBoardingHealth));
            Assert.That(RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").DisplayedHull,
                Is.EqualTo(battle.PlayerBoardingHealth));
            AssertBattleRuleLayout(false);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_05_Enemy_First_Strike.png",
                "敌方撞击进入接舷并先手一次，船员生命与HUD同步扣除", "Battle Card");

            // This presentation fixture preserves the actual ram transition and
            // crew snapshot above, then covers damage, block and healing tooltips
            // independently of the shuffled boarding draw. Pointer entry, rather
            // than SetActive, must reveal each tooltip through the normal UI.
            SetResponsiveHand(session, "card_boarding_hook", "card_crew_guard", "card_crew_bandage", "card_crew_rally");
            RenderResponsiveBattle(controller);
            yield return null;
            for (int handIndex = 0; handIndex < battle.Hand.Count; handIndex++)
            {
                CoreBattleCard card = session.GetBattleCard(battle.Hand[handIndex]);
                CoreBattleCardEffects effects = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                Button button = RequiredResponsive<Button>("Battle Card " + handIndex);
                RectTransform bubble = button.transform.Find("Card Keyword Bubble " + handIndex).GetComponent<RectTransform>();
                Assert.That(bubble.gameObject.activeSelf, Is.False, "The boarding tooltip starts hidden.");
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                };
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.18f);
                Assert.That(bubble.gameObject.activeInHierarchy, Is.True);
                Assert.That(bubble.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.5f));
                Text explanation = bubble.Find("Keyword Bubble Text Viewport/Content/Keyword Bubble Text").GetComponent<Text>();
                Text title = bubble.Find("Keyword Bubble Title").GetComponent<Text>();
                // Capture before content and layout assertions so any regression
                // leaves the real hovered state, including a clipped explanation.
                NavalBattleVisualCapture.Capture(controller,
                    "BattleRules_06_Boarding_Hover_" + handIndex + ".png",
                    "接舷悬停：" + card.Name + "，实际普通水手、倍率与效果完整显示", "Battle Card");
                string text = explanation.text.Replace("\n", string.Empty).Replace('\u00a0', ' ');
                Assert.That(text, Does.Contain("当前 " + battle.PlayerOrdinaryCrew + " 名普通水手")
                    .And.Contain("接舷效果 " + (effects.BoardingScale * 100m)
                        .ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%")
                    .And.Contain("以 " + CoreBattleCardEffects.ReferenceOrdinaryCrew + " 人为基准")
                    .And.Contain("切换阶段恢复基础费用"));
                // The final effect ends in a full stop; intermediate effects use
                // semicolons. Preserve the numeric boundary (1 must not match 10).
                string effectText = text.Replace('。', '；');
                if (card.Damage > 0) Assert.That(effectText, Does.Contain("船员伤害 " + effects.Damage + "；"));
                if (card.Block > 0) Assert.That(effectText, Does.Contain("格挡 +" + effects.Block + "；"));
                if (card.Repair > 0) Assert.That(effectText, Does.Contain("恢复船员 " + effects.Repair + "；"));
                Assert.That(title.text, Does.StartWith(card.Name));
                Assert.That(title.text, Does.Contain("分类 · " + card.Type));
                AssertBattleRuleTooltipFits(explanation, 11);
                AssertBattleRuleTooltipFits(title, 12);
                Assert.That(BattleRuleTooltipWorldRect(title.rectTransform).Overlaps(BattleRuleTooltipWorldRect(explanation.rectTransform)), Is.False,
                    "Card identity and the complete effect explanation retain separate rows.");
                Rect bubbleBounds = BattleRuleTooltipWorldRect(bubble);
                foreach (Text label in new[] { title, explanation })
                {
                    RectTransform visibleRect = label.GetComponentInParent<ScrollRect>()?.viewport ?? label.rectTransform;
                    Rect bounds = BattleRuleTooltipWorldRect(visibleRect);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(bubbleBounds.xMin - .5f));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(bubbleBounds.xMax + .5f));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(bubbleBounds.yMin - .5f));
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(bubbleBounds.yMax + .5f));
                }
                AssertBattleRuleHoveredCardClear(button.GetComponent<RectTransform>());
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(bubble.gameObject.activeSelf, Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertBattleRuleHoveredCardClear(RectTransform card)
        {
            Canvas.ForceUpdateCanvases();
            Rect cardBounds = BattleRuleTooltipWorldRect(card);
            foreach (string name in new[] { "Player Ship Art", "Enemy Ship Art", "End Battle Turn" })
                Assert.That(cardBounds.Overlaps(BattleRuleTooltipWorldRect(RequiredResponsive<RectTransform>(name))), Is.False,
                    "The lifted boarding card must preserve existing ship and end-turn clearance: " + name);
        }

        private static Rect BattleRuleTooltipWorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 minimum = corners[0], maximum = corners[0];
            for (int index = 1; index < corners.Length; index++)
            {
                minimum = Vector2.Min(minimum, corners[index]);
                maximum = Vector2.Max(maximum, corners[index]);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static void AssertBattleRuleTooltipFits(Text label, int existingMinimumFontSize)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(label.resizeTextMinSize, Is.GreaterThanOrEqualTo(existingMinimumFontSize),
                "Do not hide additional crew/effect text by lowering the existing readable minimum font.");
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = label.resizeTextForBestFit ? label.resizeTextMinSize : label.fontSize;
            Assert.That(settings.fontSize, Is.GreaterThanOrEqualTo(existingMinimumFontSize));
            float fullHeight = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            Assert.That(fullHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f),
                label.name + " must fit completely at its existing minimum font: " + settings.fontSize + "px.");
        }

        private static void AssertBattleRuleLayout(bool ram)
        {
            Canvas.ForceUpdateCanvases();
            Rect rule = BattleRuleWorldRect(RequiredResponsive<RectTransform>("Battle Card Cost Rule Panel"));
            foreach (string name in new[] { "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track",
                "Player Battle Name Board", "Enemy Battle Name Board", "Enemy Intent HUD", "Battle Badge" })
                Assert.That(rule.Overlaps(BattleRuleWorldRect(RequiredResponsive<RectTransform>(name))), Is.False,
                    "Card-cost rule must not cover " + name);
            Rect intent = BattleRuleWorldRect(RequiredResponsive<RectTransform>("Enemy Intent HUD"));
            Assert.That(intent.Overlaps(BattleRuleWorldRect(RequiredResponsive<RectTransform>("Enemy Battle Name Board"))), Is.False);
            RectTransform intentRect = RequiredResponsive<RectTransform>("Enemy Intent HUD");
            bool boarding = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session.Battle.Stage == NavalBattleStage.Boarding;
            Assert.That(intentRect.rect.width, Is.EqualTo(boarding ? 230f : 210f).Within(.01f));
            Assert.That(intentRect.rect.height, Is.GreaterThanOrEqualTo(96f));
            if (boarding)
                Assert.That(intentRect.anchoredPosition, Is.EqualTo(new Vector2(735f, -198f)).Within(.01f));
            else
                Assert.That(intentRect.anchoredPosition.y - intentRect.rect.height * .5f, Is.EqualTo(-82f).Within(.01f));
            foreach (string name in new[] { "Enemy Ship Art", "Enemy Battle Hull Bar Track", "Battle Hand", "End Battle Turn" })
                Assert.That(intent.Overlaps(BattleRuleWorldRect(RequiredResponsive<RectTransform>(name))), Is.False,
                    "Intent sidebar must not cover " + name);
            if (!ram) return;
            Text label = RequiredResponsive<Text>("Enemy Intent Text");
            Assert.That(label.resizeTextMinSize, Is.GreaterThanOrEqualTo(14));
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = 14;
            float fullHeight = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            Assert.That(fullHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f),
                "Every ram-intent line, including range and first strike, must fit at a readable 14px.");
            Assert.That(label.text, Does.Contain("船员伤害").And.Contain("米外"));
        }

        private static Rect BattleRuleWorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
