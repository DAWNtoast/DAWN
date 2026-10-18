using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator NavalTactics_CannonRangeReadoutCountsFiveCellsForThreeAcrossAndTwoUp()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            MethodInfo pose = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            pose.Invoke(session.Battle, new object[] { true, new VoyagePoint(0f, 0f), 0f });
            pose.Invoke(session.Battle, new object[] { false, new VoyagePoint(300f, 200f), 180f });
            session.Cargo.Powder = session.Cargo.Cannonballs = 20;
            SetResponsiveHand(session, "card_chain_shot");
            RenderResponsiveBattle(controller);
            yield return null;
            Text range = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Single(text => text.name == "Battle Range");
            Assert.That(range.text, Is.EqualTo("5 格 / 炮击4格"));
            Assert.That(session.CardUnavailableReason(0), Is.EqualTo(CoreVoyageSession.NavalCannonOutOfRangeReason));
            Text rule = RequireTacticalBoard().GetComponentsInChildren<Text>().Single(text => text.name == "Tactical scale");
            Assert.That(rule.text, Does.Contain("炮击4格：横纵格数相加"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator NavalTactics_GuidedCourseAllowsAlternateHeadingsAndBothRealContacts()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return EnterGuidedCardCourse();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseBasicsDone, true);
                AssertTacticalPoint(controller.Session.Battle.PlayerPositionMeters, new VoyagePoint(-500f, 100f));
                Assert.That(controller.Session.Battle.PlayerHeadingDegrees, Is.EqualTo(180f));
                yield return PlayGuidedScriptAction(controller, false, null);
                yield return PlayGuidedScriptAction(controller, false, null, true);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseManeuverDone));
                AssertTacticalPoint(controller.Session.Battle.PlayerPositionMeters, new VoyagePoint(-500f, -100f));
                Assert.That(controller.Session.Battle.PlayerHeadingDegrees, Is.EqualTo(180f));
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseBoardContact, true);
                Assert.That(controller.Session.Battle.DistanceMeters, Is.EqualTo(100f));
                Assert.That(controller.Session.Battle.NavalActionPoints, Is.EqualTo(1));
                Assert.That(controller.Session.Battle.CardsPlayedThisTurn, Is.Zero,
                    "Ordinary boarding requires no preceding movement card.");
                Assert.That(controller.Session.CanBoard(out string reason), Is.True, reason);
                yield return PlayGuidedScriptAction(controller, false, null);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseBoardDone));
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseRamMove);
                // Choose a legal but tactically wrong arrival heading, then fix
                // it by actual movement and turns rather than resetting the lesson.
                yield return ClickGuidedCourseRoute(controller, new[] { new VoyagePoint(-100f, 0f) }, 90f);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamAction));
                yield return PlayGuidedScriptAction(controller, false, null);
                Assert.That(controller.Session.CanRam(out _), Is.False);
                var recoveryRoute = new[] { new VoyagePoint(-100f, 100f), new VoyagePoint(0f, 100f),
                    new VoyagePoint(100f, 100f), new VoyagePoint(100f, 0f) };
                var recoveryHeadings = new[] { 0f, 0f, 270f, 180f };
                for (int index = 0; index < recoveryRoute.Length; index++)
                {
                    yield return ClickGuidedCourseRoute(controller, new[] { recoveryRoute[index] }, recoveryHeadings[index]);
                    ClickAcceptanceButton("End Battle Turn");
                    yield return WaitGuidedBossAnimation(controller);
                    Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamContact));
                    Assert.That(controller.Session.Battle.NavalActionPoints, Is.EqualTo(1));
                }

                Assert.That(controller.Session.Battle.DistanceMeters, Is.EqualTo(100f));
                Assert.That(controller.Session.Battle.NavalActionPoints, Is.EqualTo(1));
                Assert.That(controller.Session.CanRam(out reason), Is.True, reason);
                yield return PlayGuidedScriptAction(controller, false, null);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamDone));
                Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator NavalTactics_OpeningUsesSixUnlabelledCellsAndPersistentLeftPlayer()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetTacticalReviewHand(session);
            RenderResponsiveBattle(controller);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(session.Battle.DistanceMeters, Is.EqualTo(600f).Within(.01f), "The first active naval turn must open six 100 m cells apart.");
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(3));
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f).Within(.01f));
            Assert.That(session.Battle.EnemyHeadingDegrees, Is.EqualTo(180f).Within(.01f));
            Component board = RequireTacticalBoard();
            float cell = TacticalProperty<float>(board, "CellSizePixels");
            Assert.That(cell, Is.GreaterThan(5f));
            Vector2 player = TacticalProperty<Vector2>(board, "PlayerScreenPosition");
            Vector2 enemy = TacticalProperty<Vector2>(board, "EnemyScreenPosition");
            Assert.That(enemy.x - player.x, Is.EqualTo(6f * cell).Within(1f));
            Assert.That(player.y, Is.EqualTo(enemy.y).Within(1f));
            AssertTacticalPlayerLeft(board);
            foreach (Text text in board.GetComponentsInChildren<Text>(true))
                Assert.That(text.text, Does.Not.Match("^[A-X]$|^[0-9]{1,2}$"), "The tactical chart must not restore row/column labels.");
            string allLabels = string.Join(" ", Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(text => text.text));
            foreach (string direction in new[] { "东", "南", "西", "北" }) Assert.That(allLabels, Does.Contain(direction));
            NavalBattleVisualCapture.Capture(controller, "Tactics_01_Opening.png", "首次海战：六格600米、双方船头相对，网格无编号，罗盘标注方向");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalTactics_ManualRouteUsesAllPointsAndRequiresLegalArrivalHeading()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_turn_port", "card_sail_forward", "card_brace", "card_chain_shot", "card_turn_about");
            RenderResponsiveBattle(controller);
            yield return null;
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(3));
            for (int index = 0; index < 2; index++)
            {
                CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
                Assert.That(card.Id, Is.EqualTo("card_approach"),
                    "Both retired turn-port and sail-forward IDs resolve to the same approach identity.");
                Assert.That(card.Energy, Is.Zero);
                int cost = session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
                int energyBefore = session.Battle.Energy;
                int handBefore = session.Battle.Hand.Count, playsBefore = session.Battle.CardsPlayedThisTurn;
                int draw = card.Draw;
                Assert.That(cost, Is.EqualTo(index),
                    "The shared zero-base-cost approach identity costs 0, then 1 after its first successful use.");
                RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
                yield return WaitResponsiveBattle(controller);
                Assert.That(session.Battle.Energy, Is.EqualTo(energyBefore - cost));
                Assert.That(session.Battle.Hand.Count, Is.EqualTo(handBefore - 1 + draw));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(playsBefore + 1));
                int nextApproach = session.Battle.Hand.First(cardIndex => session.GetBattleCard(cardIndex).Id == "card_approach");
                Assert.That(session.GetBattleCardEnergyCost(nextApproach), Is.EqualTo(index + 1),
                    "All remaining aliases share the successful-use counter; each play increments it exactly once.");
                AssertTacticalPoint(session.Battle.PlayerPositionMeters, start);
                Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f));
            }
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(3));
            Assert.That(session.Battle.Energy, Is.EqualTo(2));
            NavalTacticalMovementInput input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            Assert.That(input, Is.Not.Null);
            yield return DragTacticalRoute(controller, new[] { new VoyagePoint(start.X + 100f, start.Y),
                new VoyagePoint(start.X + 100f, start.Y + 100f) });
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(2));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(3), "Planning is free until the route is committed.");
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, start);
            NavalBattleVisualCapture.Capture(controller, "NavalAction_02_SelectedRoute.png", "玩家一次拖动选择东、北两格；松开保留预览，行动点3尚未消耗或移动");

            Component board = RequireTacticalBoard();
            float initialRotation = TacticalProperty<float>(board, "CameraRotationDegrees");
            ClickAcceptanceButton("Commit Naval Movement");
            input.CommitSelection();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero, "Even a two-cell route consumes the whole three-point pool.");
            Assert.That(RequiredResponsive<Button>("Battle Card 0").interactable, Is.False);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.False);
            int intermediateFrames = 0;
            bool captured = false;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                board = RequireTacticalBoard();
                AssertTacticalPlayerLeft(board);
                if (Mathf.Abs(Mathf.DeltaAngle(initialRotation, TacticalProperty<float>(board, "CameraRotationDegrees"))) > .1f)
                {
                    intermediateFrames++;
                    if (!captured)
                    {
                        NavalBattleVisualCapture.Capture(controller, "NavalAction_03_Travel.png", "确认航行后沿所选折线路径逐格移动，途中手牌与结束回合锁定");
                        captured = true;
                    }
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(intermediateFrames, Is.GreaterThan(1));
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, new VoyagePoint(start.X + 100f, start.Y + 100f));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            CollectionAssert.AreEquivalent(new[] { 0f, 90f, 180f }, session.GetNavalArrivalHeadings());
            Assert.That(RequiredResponsive<Button>("Battle Card 0").interactable, Is.False);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.False);
            int energy = session.Battle.Energy, cards = session.Battle.Hand.Count, turn = session.Battle.Turn;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            input.ChooseHeading(270f);
            yield return null;
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True, "South reverses the final northward leg and is unavailable.");
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(cards));
            Assert.That(session.Battle.Turn, Is.EqualTo(turn));
            NavalBattleVisualCapture.Capture(controller, "NavalAction_04_ArrivalChoice.png", "航行结束后只能选择东、北、西三个合法朝向；完成定向前禁止出牌与结束回合");
            Button west = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(item =>
                item.name.StartsWith("Naval Arrival Heading ") && item.GetComponentInChildren<Text>().text == "朝西");
            yield return WaitTacticalHeadingPointerReady(west.name);
            ClickAcceptanceButton(west.name);
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(RequiredResponsive<Button>("Battle Card 0").interactable, Is.False);
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(180f).Within(.01f));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            NavalBattleVisualCapture.Capture(controller, "NavalAction_05_HeadingConfirmed.png", "选择朝西并等待转向动画完成，恢复出牌；本次行动点保持清空");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalTactics_RightClickUndoClearAndTurnResetPreserveActionEconomy()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetTacticalReviewHand(session);
            RenderResponsiveBattle(controller);
            yield return null;
            NavalTacticalMovementInput input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            VoyagePoint east = new VoyagePoint(start.X + 100f, start.Y);
            yield return DragTacticalRoute(controller, new[] { east });
            Assert.That(controller.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Undo Naval Movement"), Is.False);
            var undoPointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right };
            input.OnPointerDown(undoPointer);
            input.OnPointerClick(undoPointer);
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            yield return DragTacticalRoute(controller, new[] { east });
            ClickAcceptanceButton("Clear Naval Movement");
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(3));
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, start);
            input.CommitSelection();
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "Empty route cannot commit.");
            yield return DragTacticalRoute(controller, new[] { east });
            input.CommitSelection();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            input.ChooseHeading(180f);
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            input.ChooseHeading(0f);
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.GetNavalLegalNextSteps().Count, Is.Zero);
            int turn = session.Battle.Turn;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Turn, Is.EqualTo(turn + 1));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(3));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalTactics_TopDownCannonThenBoardingRetainsLegacyCombatFlow()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            session.Battle.DistanceMeters = 250f;
            session.Battle.EnemyBlock = 0;
            SetResponsiveHand(session, "card_chain_shot", "card_turn_port", "card_sail_forward");
            RenderResponsiveBattle(controller);
            yield return null;
            Assert.That(RequiredResponsive<Image>("Player Ship Art"), Is.Not.Null, "Exercise the new Image-based top-down ship.");
            int hullBefore = session.Battle.EnemyHull;
            int ammoBefore = session.Cargo.Cannonballs;
            Assert.That(session.CanPlayCard(0), Is.True, session.CardUnavailableReason(0));
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.EnemyHull, Is.LessThan(hullBefore));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(ammoBefore - 1));
            AssertTacticalPlayerLeft(RequireTacticalBoard());
            NavalBattleVisualCapture.Capture(controller, "Tactics_05_CannonResolved.png", "俯视船真实炮击完成：扣除炮弹、敌舰掉血，操作恢复");

            // Arrange adjacent cells with matching headings; the click still
            // uses the production one-AP boarding path.
            ConfigureCloseNavalTestPose(session.Battle, false);
            session.Battle.PlannedBoardingApproach = NavalBoardingApproach.Alongside;
            session.Battle.Energy = session.Battle.MaxEnergy;
            RenderResponsiveBattle(controller);
            yield return null;
            Button board = RequiredResponsive<Button>("Battle Board");
            Assert.That(board.interactable, Is.True);
            RectTransform boardingPlayer = RequiredResponsive<RectTransform>("Player Ship Art");
            RectTransform boardingEnemy = RequiredResponsive<RectTransform>("Enemy Ship Art");
            float initialSeparation = Vector2.Distance(boardingPlayer.anchoredPosition, boardingEnemy.anchoredPosition);
            float maximumSeparation = initialSeparation;
            board.onClick.Invoke();
            int connectionFrames = 0;
            bool sawRopes = false;
            float connectionDeadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < connectionDeadline)
            {
                if (boardingPlayer != null && boardingEnemy != null)
                {
                    maximumSeparation = Mathf.Max(maximumSeparation,
                        Vector2.Distance(boardingPlayer.anchoredPosition, boardingEnemy.anchoredPosition));
                    connectionFrames++;
                    if (!sawRopes && GameObject.Find("Tactical Boarding Rope 0") != null)
                    {
                        sawRopes = true;
                        NavalBattleVisualCapture.Capture(controller, "NavalAction_06_BoardingConnection.png", "相邻格同向接舷：保持实际舰距，接舷绳连接双方后转入甲板");
                    }
                }
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(connectionFrames, Is.GreaterThan(1));
            Assert.That(sawRopes, Is.True);
            Assert.That(maximumSeparation, Is.LessThanOrEqualTo(initialSeparation + 1f),
                "An adjacent-cell boarding approach must never spread the top-down ships to legacy side-view anchors.");
            yield return null;
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(GameObject.Find("Naval Tactical Board"), Is.Null, "Boarding must replace the tactical sea with its existing deck scene.");
            BoardingCinematicAssertions.AssertScene(session);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            NavalBattleVisualCapture.Capture(controller, "Tactics_06_Boarding.png", "俯视战棋靠近接舷后正常进入原甲板战斗，双方船员与手牌完整");
            LogAssert.NoUnexpectedReceived();
        }

        private static void SetTacticalReviewHand(CoreVoyageSession session) => SetResponsiveHand(session,
            "card_turn_port", "card_sail_forward", "card_turn_starboard", "card_turn_about", "card_chain_shot");

        private static void AssertTacticalPoint(VoyagePoint actual, VoyagePoint expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(.05f));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(.05f));
        }

        private static void ClickTacticalCell(NavalTacticalMovementInput input, VoyagePoint point)
        {
            NavalTacticalBoardView board = input.GetComponent<NavalTacticalBoardView>();
            RectTransform rect = (RectTransform)input.transform;
            Canvas canvas = input.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(board.WorldToBoard(point)));
            var pointer = new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject, Is.EqualTo(input.gameObject), "Toolbar, cards or other UI must not intercept a highlighted route cell.");
            pointer.pressPosition = screen;
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static Component RequireTacticalBoard()
        {
            GameObject host = GameObject.Find("Naval Tactical Board");
            Assert.That(host, Is.Not.Null, "The actual naval stage must create its tactical board.");
            Component component = host.GetComponents<Component>().FirstOrDefault(item => item.GetType().Name == "NavalTacticalBoardView");
            Assert.That(component, Is.Not.Null);
            return component;
        }

        private static T TacticalProperty<T>(Component board, string name)
        {
            var property = board.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, "Missing inspectable tactical presentation property: " + name);
            return (T)property.GetValue(board);
        }

        private static void AssertTacticalPlayerLeft(Component board)
        {
            Vector2 player = TacticalProperty<Vector2>(board, "PlayerScreenPosition");
            Vector2 enemy = TacticalProperty<Vector2>(board, "EnemyScreenPosition");
            Assert.That(player.x, Is.LessThan(enemy.x - 1f), "Enemy-centered camera must keep the player on the left throughout movement.");
            Assert.That(player.y, Is.EqualTo(enemy.y).Within(1.5f), "The relative ship axis should remain horizontal while the world rotates.");
        }
    }
}
