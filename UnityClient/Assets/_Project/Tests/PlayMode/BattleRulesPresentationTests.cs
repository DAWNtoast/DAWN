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
            Assert.That(GameObject.Find("Battle Card Cost Rule Panel"), Is.Null);
            Assert.That(GameObject.Find("Battle Card Cost Rule"), Is.Null);
            AssertBattleRuleLayout(false);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("0"));
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("1"));
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 1").text, Is.EqualTo("1"));
            Assert.That(RequiredResponsive<Text>("Card Repeat Cost Text 0").text, Is.EqualTo("+1"));
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("2"));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").interactable, Is.False);
            Assert.That(RequiredResponsive<Button>("Battle Card 0").transform.Find("Card Keyword Bubble 0"), Is.Null,
                "Repeated energy costs remain on the badge without a general keyword tooltip.");
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
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent HUD").rect.size, Is.EqualTo(new Vector2(230f, 120f)).Within(.01f));
            Assert.That(RequiredResponsive<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + session.GetEnemyIntent()));
            AssertBattleRuleLayout(false);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_04b_Ram_Avoided.png",
                "真实拉开距离后保留原战斗视图，侧栏意图保持与左侧对应的大小和位置", "Battle Card");
            ClickAcceptanceButton("Battle Card 0"); // Actually approach: 210 -> 60 m.
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent HUD").GetInstanceID(), Is.EqualTo(intentId));
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent HUD").rect.size, Is.EqualTo(new Vector2(230f, 120f)).Within(.01f));
            AssertBattleRuleLayout(true);
            int crewBefore = battle.PlayerBoardingHealth;
            int hullBefore = session.Hull;
            int firstStrike = session.EnemyRamFirstStrikeDamage;
            ClickAcceptanceButton("End Battle Turn");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(battle.BoardingInitiatedByEnemy, Is.True);
            int expectedBoardingHealth = (int)System.Math.Ceiling(crewBefore *
                (1d - System.Math.Max(0, hullBefore - session.Hull) / (2d * session.MaxHull)));
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(expectedBoardingHealth - firstStrike));
            Assert.That(RequiredResponsive<Text>("Battle Alignment").text, Does.Contain("接舷先手：敌方"));
            Assert.That(RequiredResponsive<Text>("Player Battle Name").text, Does.Contain("守备队"));
            Assert.That(RequiredResponsive<Text>("Enemy Battle Name").text, Does.Contain("登船队"));
            Assert.That(RequiredResponsive<Text>("Player Ship Art Formation Label").text, Does.Contain("守备队"));
            Assert.That(RequiredResponsive<Text>("Enemy Ship Art Formation Label").text, Does.Contain("登船队"));
            Assert.That(RequiredResponsive<Text>("Battle Active Buffs").text,
                Does.Contain("普通水手 " + battle.PlayerOrdinaryCrew + " 人")
                    .And.Not.Contain("人数系数")
                    .And.Contain(battle.PlayerBoardingHealth + "/" + battle.PlayerMaxBoardingHealth));
            Assert.That(RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").DisplayedHull,
                Is.EqualTo(battle.PlayerBoardingHealth));
            AssertBattleRuleLayout(false);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_05_Enemy_First_Strike.png",
                "敌方撞击进入接舷并先手一次，船员生命与HUD同步扣除", "Battle Card");

            // This presentation fixture preserves the actual ram transition and
            // crew snapshot above, then covers damage and healing calculations
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
                Transform bubbleTransform = button.transform.Find("Card Keyword Bubble " + handIndex);
                bool hasCalculation = card.Damage > 0 || card.Repair > 0;
                Assert.That(bubbleTransform != null, Is.EqualTo(hasCalculation),
                    "Only boarding damage and healing cards need calculation tooltips: " + card.Id);
                Vector2 restingPosition = button.GetComponent<RectTransform>().anchoredPosition;
                Rect restingFrame = BattleRuleWorldRect(button.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
                if (hasCalculation) Assert.That(bubbleTransform.gameObject.activeSelf, Is.False, "The boarding tooltip starts hidden.");
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                };
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.18f);
                Rect inspectedFrame = BattleRuleWorldRect(button.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
                Assert.That(inspectedFrame.height, Is.GreaterThan(restingFrame.height * 1.08f),
                    "Every card enlarges for inspection even when no calculation tooltip is needed.");
                Assert.That(inspectedFrame.yMax, Is.GreaterThan(restingFrame.yMax + 1f),
                    "The inspected upper edge rises without requiring the taller card's center to rise by a fixed amount.");
                Assert.That(button.GetComponent<Canvas>().overrideSorting, Is.True);
                if (!hasCalculation)
                {
                    Assert.That(button.transform.Find("Card Keyword Bubble " + handIndex), Is.Null);
                    AssertBattleRuleHoveredCardClear(button.GetComponent<RectTransform>());
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                    yield return new WaitForSecondsRealtime(.4f);
                    Assert.That(button.GetComponent<Canvas>().overrideSorting, Is.False);
                    Assert.That(button.GetComponent<RectTransform>().anchoredPosition.y, Is.EqualTo(restingPosition.y).Within(4f));
                    continue;
                }
                RectTransform bubble = bubbleTransform.GetComponent<RectTransform>();
                Assert.That(bubble.gameObject.activeInHierarchy, Is.True);
                Assert.That(bubble.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.5f));
                Text explanation = bubble.Find("Keyword Bubble Text").GetComponent<Text>();
                Text title = bubble.Find("Keyword Bubble Title").GetComponent<Text>();
                // Capture before content and layout assertions so any regression
                // leaves the real hovered state, including a clipped explanation.
                NavalBattleVisualCapture.Capture(controller,
                    "BattleRules_06_Boarding_Hover_" + handIndex + ".png",
                    "接舷悬停：" + card.Name + "，卡牌数值不再按人数缩放，实际效果完整显示", "Battle Card");
                string text = explanation.text.Replace("\n", string.Empty).Replace('\u00a0', ' ');
                Assert.That(text, Does.Contain("卡牌效果不受人数影响")
                    .And.Not.Contain("接舷倍率").And.Not.Contain("人为基准"));
                AssertCalculationTooltipOmitsGeneralKeywords(text);
                if (card.Damage > 0) Assert.That(text, Does.Contain("船员伤害 " + session.GetBattleCardDamage(card, NavalBattleStage.Boarding)));
                if (card.Repair > 0) Assert.That(text, Does.Contain("本次实际恢复 " + effects.ActualRepair)
                    .And.Contain("无药保底 " + effects.FallbackRepair));
                Assert.That(title.text, Does.StartWith(card.Name));
                Assert.That(title.text, Does.Contain("分类 · " + card.Type));
                Assert.That(title.fontSize, Is.GreaterThanOrEqualTo(24), "The card name must remain larger than its category.");
                Assert.That(!title.resizeTextForBestFit || title.resizeTextMinSize >= 22, Is.True);
                Assert.That(explanation.fontSize, Is.GreaterThanOrEqualTo(20));
                Assert.That(!explanation.resizeTextForBestFit || explanation.resizeTextMinSize >= 18, Is.True,
                    "Calculation explanations preserve the existing readable font size.");
                AssertBattleRuleTooltipFits(explanation, 11);
                AssertBattleRuleTooltipFits(title, 12);
                Assert.That(BattleRuleTooltipWorldRect(title.rectTransform).Overlaps(BattleRuleTooltipWorldRect(explanation.rectTransform)), Is.False,
                    "Card identity and the complete effect explanation retain separate rows.");
                Rect bubbleBounds = BattleRuleTooltipWorldRect(bubble);
                CardSchoolFrameGraphic completeFrame = button.GetComponentInChildren<CardSchoolFrameGraphic>();
                Assert.That(bubbleBounds.Overlaps(BattleRuleTooltipWorldRect(completeFrame.rectTransform)), Is.False,
                    "Calculation tooltips leave the complete frame and its corner payment crystals uncovered.");
                foreach (Text label in new[] { title, explanation })
                {
                    Rect bounds = BattleRuleTooltipWorldRect(label.rectTransform);
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

        [UnityTest]
        public IEnumerator BattleRules_AllCardRanksOnlyExposeBoardingDamageAndHealingCalculations()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            session.Battle.PlayerBoardingHealth = 10;
            session.Cargo.Medicine = 2;
            MethodInfo tooltip = typeof(CoreGameplayDemoController).GetMethod("BattleCardTooltipText",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tooltip, Is.Not.Null);
            int checkedVariants = 0;
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
            {
                session.Battle.Stage = stage;
                for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                {
                    if (CoreVoyageSession.CardLibrary[index].AllowedStages == NavalBattleStages.None) continue;
                    session.Battle.Hand.Clear();
                    session.Battle.Hand.Add(index);
                    for (int rank = 0; rank <= CoreBattleUpgradeBalance.MaximumLevel; rank++)
                    {
                        CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                        string text = ((string)tooltip.Invoke(controller, new object[] { card, 0 }))
                            .Replace("\n", string.Empty).Replace('\u00a0', ' ');
                        string context = card.Id + " rank " + rank + " stage " + stage;
                        bool hasSchoolMechanism = card.SchoolEffect != null;
                        bool needsCalculation = !hasSchoolMechanism && stage == NavalBattleStage.Boarding &&
                            card.IsAllowedInStage(stage) && (card.Damage > 0 || card.Repair > 0);
                        Assert.That(string.IsNullOrEmpty(text), Is.EqualTo(!hasSchoolMechanism && !needsCalculation), context);
                        if (hasSchoolMechanism)
                        {
                            CoreBattleCardEffects effects = session.GetBattleCardEffects(card, stage);
                            Assert.That(text, Does.Contain("当前结算").And.Contain("伤害 " + session.GetBattleCardDamage(card, stage))
                                .And.Contain("格挡 " + effects.Block).And.Contain("实际治疗 " + effects.ActualRepair), context);
                            Assert.That(text.Contains("卡牌效果不受人数影响"),
                                Is.EqualTo(stage == NavalBattleStage.Boarding), context);
                            Assert.That(text, Does.Not.Contain("计算使用未取整倍率").And.Not.Contain("名普通水手折算"),
                                context + " school effects no longer inherit a crew multiplier");
                            string reason = session.CardUnavailableReason(0);
                            if (reason.Length > 0)
                                Assert.That(text, Does.Contain("当前不能使用").And.Contain(reason), context);
                            else Assert.That(text, Does.Not.Contain("当前不能使用"), context);
                        }
                        if (needsCalculation)
                        {
                            CoreBattleCardEffects effects = session.GetBattleCardEffects(card, stage);
                            Assert.That(text, Does.Contain("卡牌效果不受人数影响")
                                .And.Not.Contain("接舷倍率").And.Not.Contain("人为基准")
                                .And.Not.Contain("30%～160%").And.Not.Contain("计算使用未取整倍率"), context);
                            AssertCalculationTooltipOmitsGeneralKeywords(text);
                            if (card.Damage > 0)
                            {
                                Assert.That(effects.Damage, Is.EqualTo(card.Damage), context + " preserves the authored upgraded damage");
                                Assert.That(text, Does.Contain("白刃")
                                    .And.Contain("船员伤害 " + session.GetBattleCardDamage(card, stage)), context);
                            }
                            if (card.Repair > 0)
                            {
                                Assert.That(effects.FullRepair, Is.EqualTo(card.Repair), context + " preserves the authored upgraded treatment");
                                Assert.That(text, Does.Contain("治疗").And.Contain("有药完整 " + card.Repair)
                                    .And.Contain("无药保底 " + effects.FallbackRepair)
                                    .And.Contain("本次实际恢复 " + effects.ActualRepair), context);
                            }
                        }
                        checkedVariants++;
                    }
                }
            }
            Assert.That(checkedVariants, Is.GreaterThan(1000), "Audit every current and compatibility card at every upgrade rank and stage.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2), "All tooltip previews must be side-effect free.");
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10));
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertBattleRuleHoveredCardClear(RectTransform card)
        {
            Canvas.ForceUpdateCanvases();
            CardSchoolFrameGraphic frame = card.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(frame, Is.Not.Null);
            Rect cardBounds = BattleRuleTooltipWorldRect(frame.rectTransform);
            foreach (string name in new[] { "Player Ship Art", "Enemy Ship Art", "End Battle Turn" })
                Assert.That(cardBounds.Overlaps(BattleRuleTooltipWorldRect(RequiredResponsive<RectTransform>(name))), Is.False,
                    "The lifted boarding card must preserve existing ship and end-turn clearance: " + name);
        }

        private static void AssertCalculationTooltipOmitsGeneralKeywords(string text)
        {
            foreach (string removed in new[] { "能量", "格挡", "抽牌", "火药", "炮弹", "船体", "基础费用", "本回合已用" })
                Assert.That(text, Does.Not.Contain(removed), "Calculation tooltip must omit general keyword and cost explanations.");
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
            foreach (string name in new[] { "Battle Card Cost Rule Panel", "Battle Card Cost Rule", "Battle Status", "Battle Collision Quality" })
                Assert.That(GameObject.Find(name), Is.Null, name + " stays removed during card and turn actions");
            Rect intent = BattleRuleWorldRect(RequiredResponsive<RectTransform>("Enemy Intent HUD"));
            Assert.That(intent.Overlaps(BattleRuleWorldRect(RequiredResponsive<RectTransform>("Enemy Battle Name Board"))), Is.False);
            RectTransform intentRect = RequiredResponsive<RectTransform>("Enemy Intent HUD");
            Assert.That(intentRect.rect.size, Is.EqualTo(new Vector2(230f, 120f)).Within(.01f));
            Assert.That(intentRect.anchoredPosition, Is.EqualTo(new Vector2(735f, -196f)).Within(.01f));
            string[] leftNames = { "Player Resource HUD", "Battle Buff Panel", "Retreat Battle" };
            string[] rightNames = { "Enemy Intent HUD", "Enemy Resource HUD", "End Battle Turn" };
            for (int i = 0; i < leftNames.Length; i++)
            {
                RectTransform left = RequiredResponsive<RectTransform>(leftNames[i]);
                RectTransform right = RequiredResponsive<RectTransform>(rightNames[i]);
                Assert.That(right.rect.size, Is.EqualTo(left.rect.size).Within(.01f), rightNames[i] + " mirrors row size");
                Assert.That(right.anchoredPosition.x, Is.EqualTo(-left.anchoredPosition.x).Within(.01f));
                Assert.That(right.anchoredPosition.y, Is.EqualTo(left.anchoredPosition.y).Within(.01f));
            }
            RectTransform handRect = RequiredResponsive<RectTransform>("Battle Hand");
            Rect hand = BattleRuleWorldRect(handRect);
            Assert.That(handRect.sizeDelta, Is.EqualTo(CardFaceLayout.HandSize));
            Assert.That(Vector2.Distance(handRect.anchoredPosition, CardFaceLayout.HandPosition),
                Is.LessThanOrEqualTo(.01f), "Canvas layout may introduce sub-pixel float rounding.");
            Assert.That(intentRect.anchoredPosition.y + intentRect.rect.height * .5f, Is.EqualTo(-136f).Within(.01f),
                "Decision panels keep their fixed top edge while the complete hand scales independently.");
            RectTransform endTurn = RequiredResponsive<RectTransform>("End Battle Turn");
            Assert.That(endTurn.anchoredPosition, Is.EqualTo(new Vector2(735f, -414f)).Within(.01f));
            foreach (CardSchoolFrameGraphic frame in handRect.GetComponentsInChildren<CardSchoolFrameGraphic>())
            {
                Rect fullFrame = BattleRuleWorldRect(frame.rectTransform);
                foreach (string name in leftNames)
                    Assert.That(fullFrame.Overlaps(BattleRuleWorldRect(RequiredResponsive<RectTransform>(name))), Is.False,
                        "A complete card frame must leave the fixed left decision panel clear: " + name);
                foreach (string name in rightNames)
                    Assert.That(fullFrame.Overlaps(BattleRuleWorldRect(RequiredResponsive<RectTransform>(name))), Is.False,
                        "A complete card frame must leave the fixed right decision panel clear: " + name);
            }
            foreach (string name in new[] { "Enemy Ship Art", "Enemy Battle Hull Bar Track", "Battle Hand", "End Battle Turn", "Enemy Resource HUD" })
                Assert.That(intent.Overlaps(BattleRuleWorldRect(RequiredResponsive<RectTransform>(name))), Is.False,
                    "Intent sidebar must not cover " + name);
            if (Object.FindFirstObjectByType<CoreGameplayDemoController>().Session.Battle.Stage == NavalBattleStage.Naval)
            {
                RectTransform ramRect = RequiredResponsive<RectTransform>("Battle Ram");
                RectTransform boardRect = RequiredResponsive<RectTransform>("Battle Board");
                Rect ramBounds = BattleRuleWorldRect(ramRect);
                Rect boardBounds = BattleRuleWorldRect(boardRect);
                Assert.That(ramRect.anchoredPosition.x, Is.EqualTo(boardRect.anchoredPosition.x).Within(.01f));
                Assert.That(ramRect.rect.width, Is.EqualTo(boardRect.rect.width).Within(.01f));
                foreach (RectTransform action in new[] { ramRect, boardRect })
                {
                    Rect actionBounds = BattleRuleWorldRect(action);
                    Assert.That(action.anchoredPosition.x, Is.GreaterThan(0f));
                    Assert.That(actionBounds.xMin, Is.GreaterThanOrEqualTo(intent.xMin - .1f));
                    Assert.That(actionBounds.xMax, Is.LessThanOrEqualTo(intent.xMax + .1f));
                    Assert.That(actionBounds.yMin, Is.GreaterThan(hand.yMax), "Right-side actions stay above the hand.");
                }
                Assert.That(ramBounds.yMin, Is.GreaterThan(boardBounds.yMax), "Ram is above boarding in the action column.");
                Assert.That(boardBounds.yMin, Is.GreaterThan(intent.yMax), "Actions stay above the mirrored information panels.");
            }
            Text label = RequiredResponsive<Text>("Enemy Intent Text");
            Assert.That(label.resizeTextMinSize, Is.EqualTo(14));
            Assert.That(label.resizeTextMaxSize, Is.EqualTo(18));
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = 14;
            float fullHeight = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            Assert.That(fullHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f),
                "The complete intent, including ram range and first strike, must fit at a readable 14px.");
            if (ram) Assert.That(label.text, Does.Contain("船员伤害").And.Contain("米外"));
        }

        private static Rect BattleRuleWorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
