using System.Collections;
using System.Linq;
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
            yield return WaitResponsiveBattle(controller);
            Assert.That(GameObject.Find("Battle Card Cost Rule Panel"), Is.Null);
            Assert.That(GameObject.Find("Battle Card Cost Rule"), Is.Null);
            AssertBattleRuleLayout(false);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("0"));
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").color, Is.EqualTo(Color.white));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").transform.Find("Card Repeat Cost Plate 0"), Is.Null);
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("1"));
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 1").text, Is.EqualTo("1"));
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").color, Is.EqualTo(new Color(1f, .24f, .2f, 1f)));
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 1").color, Is.EqualTo(new Color(1f, .24f, .2f, 1f)));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").transform.Find("Card Repeat Cost Plate 0"), Is.Null);
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").text, Is.EqualTo("2"));
            Assert.That(RequiredResponsive<Button>("Battle Card 0").interactable, Is.False);
            Assert.That(RequiredResponsive<Text>("Card Energy Cost Text 0").color, Is.EqualTo(new Color(1f, .24f, .2f, 1f)),
                "An unaffordable increased cost stays red on the disabled card.");
            Button unaffordable = RequiredResponsive<Button>("Battle Card 0");
            ExecuteEvents.Execute(unaffordable.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.3f);
            AssertKeywordOnlyCardHover(unaffordable, 0, session, session.GetBattleCard(brace), "unaffordable basic card");
            ExecuteEvents.Execute(unaffordable.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.3f);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_01_Shared_Cost.png",
                "同名牌成功打出两次后，剩余副本显示2费并因能量不足禁用", "Battle Card");

            ClickAcceptanceButton("End Battle Turn");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.GetBattleCardEnergyCost(brace), Is.Zero, "The preparation-to-naval transition resets repeated costs.");
            AssertResponsiveHandMatchesCore(session);
            for (int handIndex = 0; handIndex < session.Battle.Hand.Count; handIndex++)
                Assert.That(RequiredResponsive<Text>("Card Energy Cost Text " + handIndex).color, Is.EqualTo(Color.white),
                    "A stage change restores the normal cost color.");
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
            yield return WaitResponsiveBattle(controller);
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
            for (int handIndex = 0; handIndex < session.Battle.Hand.Count; handIndex++)
                Assert.That(RequiredResponsive<Text>("Card Energy Cost Text " + handIndex).color, Is.EqualTo(Color.white),
                    "A new turn restores the normal cost color.");
            NavalBattleVisualCapture.Capture(controller, "BattleRules_03_Turn_Reset.png",
                "同一炮战阶段进入新回合后费用恢复，费用数字恢复白色", "Battle Card");
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
            ConfigureCloseNavalTestPose(battle, true, enemyInitiated: true);
            SetResponsiveHand(session, "card_retreat", "card_approach", "card_sail_forward");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(CoreBattleIntentPresentation.Build(session).Any(intent => intent.Kind == CoreBattleIntentKind.Ram), Is.True);
            Assert.That(AssertBattleRuleIntents(session), Does.Contain("先手"));
            Assert.That(RequiredResponsive<Text>("Player Battle Name").text,
                Does.Contain("普通水手 " + battle.PlayerOrdinaryCrew + " 人").And.Contain("接舷生命上限 " + battle.PlayerMaxBoardingHealth));
            AssertBattleRuleLayout(true);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_04_Enemy_Ram_Intent.png",
                "近距离敌舰明确预告冲撞、接舷与先手伤害，双方显示普通水手及接舷生命上限", "Battle Card");
            battle.EnemyBurnTurns = 10;
            battle.EnemyBurnDamage = 10;
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(session.GetEnemyIntent(), Does.Contain("灼烧"));
            BattleStatusRow enemyStatuses = RequiredResponsive<BattleStatusRow>("Enemy Battle Status Row");
            foreach (CoreBattleStatus status in CoreBattleStatusPresentation.Build(session, false))
                Assert.That(enemyStatuses.transform.Find("Status " + status.Id + "/Amount").GetComponent<Text>().text,
                    Is.EqualTo(status.Value), "The active burning state remains visible beneath enemy health.");
            AssertBattleRuleLayout(true);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_04a_Burning_Ram_Intent.png",
                "回合末灼烧与撞击、先手及避让距离同时显示且保持可读字号", "Battle Card");
            battle.EnemyBurnTurns = 0;
            battle.EnemyBurnDamage = 0;
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            int sceneId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            int intentId = RequiredResponsive<RectTransform>("Enemy Intent Icons").GetInstanceID();
            Vector2 intentSize = RequiredResponsive<RectTransform>("Enemy Intent Icons").rect.size;
            ClickAcceptanceButton("Battle Card 0"); // Gain two total AP; the first leg must follow the north-facing bow.
            yield return WaitResponsiveBattle(controller);
            NavalTacticalMovementInput movement = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            Assert.That(movement, Is.Not.Null);
            yield return DragTacticalRoute(controller, new[] { new VoyagePoint(-100f, 100f), new VoyagePoint(-200f, 100f) });
            movement.CommitSelection();
            yield return WaitResponsiveBattle(controller);
            movement.ChooseHeading(270f); // Left turn from west, ready to start the return route south.
            yield return WaitResponsiveBattle(controller);
            Assert.That(battle.DistanceMeters, Is.EqualTo(Mathf.Sqrt(50000f)).Within(.01f));
            Assert.That(session.EnemyWillRam, Is.False);
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(sceneId));
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent Icons").GetInstanceID(), Is.EqualTo(intentId));
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent Icons").rect.size, Is.EqualTo(intentSize).Within(.01f));
            AssertBattleRuleIntents(session);
            AssertBattleRuleLayout(false);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_04b_Ram_Avoided.png",
                "真实拉开距离后保留原战斗视图，头顶意图图标更新且容器保持复用", "Battle Card");
            ClickAcceptanceButton("Battle Card 0"); // Both distinct movement cards fund the two-cell return.
            yield return WaitResponsiveBattle(controller);
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            yield return DragTacticalRoute(controller, new[] { new VoyagePoint(-200f, 0f), new VoyagePoint(-100f, 0f) });
            movement.CommitSelection();
            yield return WaitResponsiveBattle(controller);
            movement.ChooseHeading(90f);
            yield return WaitResponsiveBattle(controller);
            Assert.That(battle.DistanceMeters, Is.EqualTo(100f));
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent Icons").GetInstanceID(), Is.EqualTo(intentId));
            Assert.That(RequiredResponsive<RectTransform>("Enemy Intent Icons").rect.size, Is.EqualTo(intentSize).Within(.01f));
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
            foreach (string side in new[] { "Player", "Enemy" })
                Assert.That(controller.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == side + " Ship Art Formation Label").gameObject.activeInHierarchy, Is.False,
                    "The name board now carries the formation without duplicate text over the crew art.");
            Assert.That(RequiredResponsive<Text>("Player Battle Name").text,
                Does.Contain("普通水手 " + battle.PlayerOrdinaryCrew + " 人")
                    .And.Not.Contain("人数系数"));
            Assert.That(RequiredResponsive<Text>("Player Hull Numeric").text.Replace(" ", string.Empty),
                Is.EqualTo(battle.PlayerBoardingHealth + "/" + battle.PlayerMaxBoardingHealth));
            Assert.That(RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").DisplayedHull,
                Is.EqualTo(battle.PlayerBoardingHealth));
            AssertBattleRuleLayout(false);
            NavalBattleVisualCapture.Capture(controller, "BattleRules_05_Enemy_First_Strike.png",
                "敌方撞击进入接舷并先手一次，船员生命与HUD同步扣除", "Battle Card");

            // This presentation fixture preserves the actual ram transition and
            // crew snapshot above, then covers keyword and basic-card inspection
            // independently of the shuffled boarding draw. Pointer entry, rather
            // than SetActive, must reveal each tooltip through the normal UI.
            SetResponsiveHand(session, "card_boarding_hook", "card_crew_bandage", "card_boarding_fire_06", "card_boarding_basic_07");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            for (int handIndex = 0; handIndex < battle.Hand.Count; handIndex++)
            {
                CoreBattleCard card = session.GetBattleCard(battle.Hand[handIndex]);
                Button button = RequiredResponsive<Button>("Battle Card " + handIndex);
                Transform bubbleTransform = button.transform.Find("Card Keyword Bubble " + handIndex);
                RectTransform cardRect = (RectTransform)button.transform;
                RectTransform frameRect = button.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform;
                Vector2 restingPosition = cardRect.anchoredPosition;
                Vector3 restingScale = cardRect.localScale;
                Quaternion restingRotation = cardRect.localRotation;
                Vector2 restingFaceSize = BattleRuleFrameEdgeLengths(frameRect);
                Rect restingFrame = BattleRuleWorldRect(frameRect);
                if (bubbleTransform != null) Assert.That(bubbleTransform.gameObject.activeSelf, Is.False, "The keyword tooltip starts hidden.");
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                };
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.65f);
                Rect inspectedFrame = BattleRuleWorldRect(frameRect);
                Vector2 inspectedFaceSize = BattleRuleFrameEdgeLengths(frameRect);
                // A fan card also straightens during inspection. Its screen-aligned
                // bounding box can shrink even while both physical edges enlarge.
                Assert.That(cardRect.localScale.x, Is.GreaterThan(restingScale.x * 1.08f), card.Id);
                Assert.That(cardRect.localScale.y, Is.GreaterThan(restingScale.y * 1.08f), card.Id);
                Assert.That(inspectedFaceSize.x, Is.GreaterThan(restingFaceSize.x * 1.08f),
                    "Every complete card face becomes wider for inspection: " + card.Id);
                Assert.That(inspectedFaceSize.y, Is.GreaterThan(restingFaceSize.y * 1.08f),
                    "Every complete card face becomes taller for inspection: " + card.Id);
                Assert.That(Quaternion.Angle(cardRect.localRotation, Quaternion.identity), Is.LessThan(.1f));
                Assert.That(inspectedFrame.yMax, Is.GreaterThan(restingFrame.yMax + 1f),
                    "The inspected upper edge rises without requiring the taller card's center to rise by a fixed amount.");
                Assert.That(button.GetComponent<Canvas>().overrideSorting, Is.True);
                NavalBattleVisualCapture.Capture(controller,
                    "BattleRules_06_Boarding_Hover_" + handIndex + ".png",
                    "接舷悬停：" + card.Name + "，只显示卡面关键词及其含义", "Battle Card");
                AssertKeywordOnlyCardHover(button, handIndex, session, card, card.Id);
                AssertBattleRuleHoveredCardClear(button.GetComponent<RectTransform>());
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.4f);
                if (bubbleTransform != null) Assert.That(bubbleTransform.gameObject.activeSelf, Is.False);
                AssertBattleRuleRestoredCard(cardRect, restingPosition, restingScale, restingRotation);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleRules_AllCardRanksExplainVisibleKeywordsWithoutCalculations()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            session.Battle.PlayerBoardingHealth = 10;
            session.Cargo.Medicine = 2;
            MethodInfo tooltip = typeof(CoreGameplayDemoController).GetMethod("BattleCardTooltipText",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo faceText = typeof(CoreGameplayDemoController).GetMethod("BattleCardEffectTextForCurrentStage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(faceText, Is.Not.Null);
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
                        string text = (string)tooltip.Invoke(controller, new object[] { card, 0 });
                        string description = (string)faceText.Invoke(controller, new object[] { card });
                        string context = card.Id + " rank " + rank + " stage " + stage;
                        var keywords = CoreBattleCardKeywordText.Build(card, description);
                        Assert.That(text, Is.EqualTo(string.Join("\n\n", keywords.Select(entry => entry.Text))), context);
                        AssertKeywordTooltipOmitsCalculations(text, context);
                        checkedVariants++;
                    }
                }
            }
            Assert.That(checkedVariants, Is.GreaterThan(1000), "Audit every current and compatibility card at every upgrade rank and stage.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2), "All tooltip previews must be side-effect free.");
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10));
            LogAssert.NoUnexpectedReceived();
        }

        internal static string AssertKeywordOnlyCardHover(Button button, int handIndex, CoreVoyageSession session,
            CoreBattleCard card, string context)
        {
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.SameAs(session), context);
            MethodInfo faceText = typeof(CoreGameplayDemoController).GetMethod("BattleCardEffectTextForCurrentStage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string face = (string)faceText.Invoke(controller, new object[] { card });
            var keywords = CoreBattleCardKeywordText.Build(card, face);
            Transform bubble = button.transform.Find("Card Keyword Bubble " + handIndex);
            if (keywords.Count == 0)
            {
                Assert.That(bubble == null || !bubble.gameObject.activeInHierarchy, Is.True,
                    context + " has no keyword popup for ordinary effects or current unavailability.");
                return string.Empty;
            }
            Assert.That(bubble, Is.Not.Null, context);
            Assert.That(bubble.gameObject.activeInHierarchy, Is.True, context);
            Assert.That(bubble.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.5f), context);
            Text[] visibleLabels = bubble.GetComponentsInChildren<Text>(true).Where(label => label.gameObject.activeInHierarchy).ToArray();
            Text[] titles = visibleLabels.Where(label => label.name == "Keyword Bubble Title").ToArray();
            Text[] meanings = visibleLabels.Where(label => label.name == "Keyword Bubble Text").ToArray();
            CollectionAssert.AreEqual(keywords.Select(entry => entry.Keyword), titles.Select(label => label.text), context);
            CollectionAssert.AreEqual(keywords.Select(entry => entry.Meaning), meanings.Select(label => label.text), context);
            Assert.That(visibleLabels.Length, Is.EqualTo(keywords.Count * 2),
                context + " contains only a heading and meaning for each keyword.");
            Rect bubbleBounds = BattleRuleTooltipWorldRect((RectTransform)bubble);
            Rect frameBounds = BattleRuleTooltipWorldRect(button.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
            Assert.That(bubbleBounds.xMax, Is.LessThanOrEqualTo(frameBounds.xMin + .5f), context + " opens to the left of the card.");
            for (int index = 0; index < titles.Length; index++)
            {
                AssertBattleRuleTooltipFits(titles[index], 18);
                AssertBattleRuleTooltipFits(meanings[index], 18);
                AssertBattleRuleDisjoint(titles[index].rectTransform, meanings[index].rectTransform,
                    context + " separates each keyword from its explanation.");
            }
            foreach (Text label in titles.Concat(meanings))
            {
                Assert.That(label.raycastTarget, Is.False, context + " does not intercept card input.");
                Rect bounds = BattleRuleTooltipWorldRect(label.rectTransform);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(bubbleBounds.xMin - .5f), context);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(bubbleBounds.xMax + .5f), context);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(bubbleBounds.yMin - .5f), context);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(bubbleBounds.yMax + .5f), context);
            }
            string text = string.Join("\n", titles.Select((title, index) => title.text + "：" + meanings[index].text));
            AssertKeywordTooltipOmitsCalculations(text, context);
            return text;
        }

        private static void AssertBattleRuleHoveredCardClear(RectTransform card)
        {
            Canvas.ForceUpdateCanvases();
            CardSchoolFrameGraphic frame = card.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(frame, Is.Not.Null);
            Rect cardBounds = BattleRuleTooltipWorldRect(frame.rectTransform);
            Rect viewport = BattleRuleTooltipWorldRect(FixedGameViewport.Bounds(card.GetComponentInParent<Canvas>()));
            Assert.That(cardBounds.xMin, Is.GreaterThanOrEqualTo(viewport.xMin - .5f));
            Assert.That(cardBounds.xMax, Is.LessThanOrEqualTo(viewport.xMax + .5f));
            Assert.That(cardBounds.yMin, Is.GreaterThanOrEqualTo(viewport.yMin - .5f));
            Assert.That(cardBounds.yMax, Is.LessThanOrEqualTo(viewport.yMax + .5f),
                "Inspection reveals the entire frame and corner payment emblems inside the viewport.");
            foreach (string name in new[] { "Player Ship Art", "Enemy Ship Art", "End Battle Turn" })
                AssertBattleRuleDisjoint(frame.rectTransform, RequiredResponsive<RectTransform>(name),
                    "The lifted boarding card must preserve existing ship and end-turn clearance: " + name);
        }

        private static void AssertBattleRuleDisjoint(RectTransform first, RectTransform second, string context)
        {
            var a = new Vector3[4];
            var b = new Vector3[4];
            first.GetWorldCorners(a);
            second.GetWorldCorners(b);
            // Separating axes test the actual rotated rectangles, including edge
            // crossings and containment; an AABB includes empty triangular corners.
            foreach (Vector3[] corners in new[] { a, b })
            {
                for (int edge = 0; edge < 2; edge++)
                {
                    Vector2 direction = corners[edge + 1] - corners[edge];
                    Vector2 axis = new Vector2(-direction.y, direction.x).normalized;
                    float aMin = float.PositiveInfinity, aMax = float.NegativeInfinity;
                    float bMin = float.PositiveInfinity, bMax = float.NegativeInfinity;
                    for (int index = 0; index < 4; index++)
                    {
                        float aProjection = Vector2.Dot(a[index], axis);
                        float bProjection = Vector2.Dot(b[index], axis);
                        aMin = Mathf.Min(aMin, aProjection); aMax = Mathf.Max(aMax, aProjection);
                        bMin = Mathf.Min(bMin, bProjection); bMax = Mathf.Max(bMax, bProjection);
                    }
                    if (aMax <= bMin || bMax <= aMin) return;
                }
            }
            Assert.Fail(context + "; " + first.name + " corners=" + string.Join(", ", a.Select(point => point.ToString("F2"))) +
                "; " + second.name + " corners=" + string.Join(", ", b.Select(point => point.ToString("F2"))));
        }

        private static Vector2 BattleRuleFrameEdgeLengths(RectTransform frame)
        {
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            return new Vector2(Vector3.Distance(corners[0], corners[3]), Vector3.Distance(corners[0], corners[1]));
        }

        private static void AssertBattleRuleRestoredCard(RectTransform card, Vector2 position, Vector3 scale, Quaternion rotation)
        {
            Assert.That(card.GetComponent<Canvas>().overrideSorting, Is.False);
            Assert.That(Vector2.Distance(card.anchoredPosition, position), Is.LessThan(.5f));
            Assert.That(Vector3.Distance(card.localScale, scale), Is.LessThan(.005f));
            Assert.That(Quaternion.Angle(card.localRotation, rotation), Is.LessThan(.1f));
        }

        private static void AssertKeywordTooltipOmitsCalculations(string text, string context)
        {
            foreach (string removed in new[] { "当前结算", "当前不能使用", "生命支付预告", "支付后", "本次实际",
                "实际治疗", "合计可用", "接舷倍率", "计算使用未取整倍率", "分类 ·", "接舷规则" })
                Assert.That(text, Does.Not.Contain(removed), context + " contains keyword meanings without calculations or unavailable reasons.");
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
            Assert.That(!label.resizeTextForBestFit || label.resizeTextMinSize >= existingMinimumFontSize, Is.True,
                "Keyword explanations keep a readable minimum font.");
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
            CoreVoyageSession session = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
            foreach (string name in new[] { "Battle Card Cost Rule Panel", "Battle Card Cost Rule", "Battle Status", "Battle Collision Quality" })
                Assert.That(GameObject.Find(name), Is.Null, name + " stays removed during card and turn actions");
            RectTransform intent = RequiredResponsive<RectTransform>("Enemy Intent Icons");
            AssertBattleRuleDisjoint(intent, RequiredResponsive<RectTransform>("Enemy Battle Name Board"),
                "Enemy intent icons must not cover the enemy name board.");
            string[] leftNames = { "Battle Compact Resources", "Battle Draw Pile" };
            string[] rightNames = { "Battle Discard Pile", "End Battle Turn" };
            foreach (string name in leftNames)
                Assert.That(RequiredResponsive<RectTransform>(name).anchoredPosition.x, Is.LessThan(0f), name);
            foreach (string name in rightNames)
                Assert.That(RequiredResponsive<RectTransform>(name).anchoredPosition.x, Is.GreaterThan(0f), name);
            for (int line = 1; line <= 3; line++)
            {
                Rect row = BattleRuleWorldRect(RequiredResponsive<RectTransform>("Battle Resource Line " + line));
                Rect next = BattleRuleWorldRect(RequiredResponsive<RectTransform>(line < 3 ? "Battle Resource Line " + (line + 1) : "Battle Draw Pile"));
                Assert.That(row.yMin, Is.GreaterThan(next.yMax), "Three distinct resource rows stay above the draw pile.");
            }
            RectTransform handRect = RequiredResponsive<RectTransform>("Battle Hand");
            Rect hand = BattleRuleWorldRect(handRect);
            // Naval and boarding now share the same lower fan-hand region.
            Assert.That(handRect.sizeDelta, Is.EqualTo(new Vector2(1120f, 200f)));
            Assert.That(Vector2.Distance(handRect.anchoredPosition, new Vector2(0f, -350f)),
                Is.LessThanOrEqualTo(.01f), "Canvas layout may introduce sub-pixel float rounding.");
            RectTransform endTurn = RequiredResponsive<RectTransform>("End Battle Turn");
            Assert.That(BattleRuleWorldRect(endTurn).yMin,
                Is.GreaterThan(BattleRuleWorldRect(RequiredResponsive<RectTransform>("Battle Discard Pile")).yMax),
                "End turn keeps its own row above the discard pile.");
            foreach (CardSchoolFrameGraphic frame in handRect.GetComponentsInChildren<CardSchoolFrameGraphic>())
            {
                foreach (string name in leftNames)
                    AssertBattleRuleDisjoint(frame.rectTransform, RequiredResponsive<RectTransform>(name),
                        "A complete card frame must leave the fixed left decision panel clear: " + name);
                foreach (string name in rightNames)
                    AssertBattleRuleDisjoint(frame.rectTransform, RequiredResponsive<RectTransform>(name),
                        "A complete card frame must leave the fixed right decision panel clear: " + name);
            }
            foreach (string name in new[] { "Enemy Ship Art", "Enemy Battle Hull Bar Track", "Battle Hand", "End Battle Turn", "Battle Discard Pile" })
                AssertBattleRuleDisjoint(intent, RequiredResponsive<RectTransform>(name),
                    "Enemy intent icons must not cover " + name + " during " + session.Battle.Stage);
            if (session.Battle.Stage == NavalBattleStage.Naval)
            {
                RectTransform ramRect = RequiredResponsive<RectTransform>("Battle Ram");
                RectTransform boardRect = RequiredResponsive<RectTransform>("Battle Board");
                Rect ramBounds = BattleRuleWorldRect(ramRect);
                Rect boardBounds = BattleRuleWorldRect(boardRect);
                Assert.That(ramBounds.center.y, Is.EqualTo(boardBounds.center.y).Within(.01f));
                Assert.That(ramBounds.xMax, Is.LessThan(boardBounds.xMin), "Ram and boarding remain separate side-by-side actions.");
                Assert.That(ramRect.rect.width, Is.EqualTo(boardRect.rect.width).Within(.01f));
                foreach (RectTransform action in new[] { ramRect, boardRect })
                {
                    Rect actionBounds = BattleRuleWorldRect(action);
                    Assert.That(action.anchoredPosition.x, Is.GreaterThan(0f));
                    Assert.That(actionBounds.xMin, Is.GreaterThanOrEqualTo(BattleRuleWorldRect(endTurn).xMin - .1f));
                    Assert.That(actionBounds.xMax, Is.LessThanOrEqualTo(BattleRuleWorldRect(endTurn).xMax + .1f));
                    Assert.That(actionBounds.yMin, Is.GreaterThan(hand.yMax), "Right-side actions stay above the hand.");
                    Assert.That(actionBounds.yMin, Is.GreaterThan(BattleRuleWorldRect(endTurn).yMax));
                }
            }
            string explanation = AssertBattleRuleIntents(session);
            if (ram) Assert.That(explanation, Does.Contain("船员伤害").And.Contain("均穿透格挡")
                .And.Contain("双方存活则进入接舷，敌方先手攻击")
                .And.Contain("航行离开敌舰船头正前方的相邻格可避让"));
        }

        private static string AssertBattleRuleIntents(CoreVoyageSession session)
        {
            RectTransform row = RequiredResponsive<RectTransform>("Enemy Intent Icons");
            var intents = CoreBattleIntentPresentation.Build(session);
            Assert.That(row.GetComponentsInChildren<Button>(false).Length, Is.EqualTo(intents.Count));
            foreach (CoreBattleIntent intent in intents)
            {
                Transform badge = row.Find("Intent " + intent.Id);
                Assert.That(badge, Is.Not.Null, intent.Id);
                Text amount = badge.Find("Amount").GetComponent<Text>();
                Assert.That(amount.text, Is.EqualTo(intent.Value));
                Assert.That(amount.fontSize, Is.GreaterThanOrEqualTo(14), "Intent amounts retain readable type.");
                var pointer = new PointerEventData(EventSystem.current);
                Assert.That(ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerEnterHandler), Is.True);
                Transform tooltip = GameObject.Find("Battle Status Tooltip").transform;
                string visible = System.Text.RegularExpressions.Regex.Replace(tooltip.Find("Description").GetComponent<Text>().text,
                    "<[^>]+>", string.Empty);
                Assert.That(visible, Is.EqualTo(intent.Tooltip), "Hover retains the complete action and damage explanation.");
                ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            }
            return string.Join("\n", intents.Select(intent => intent.Tooltip));
        }

        private static Rect BattleRuleWorldRect(RectTransform rect) => BattleRuleTooltipWorldRect(rect);
    }
}
