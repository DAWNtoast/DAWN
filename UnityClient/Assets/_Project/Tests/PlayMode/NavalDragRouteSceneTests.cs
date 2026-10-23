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
        public IEnumerator NavalDragRoute_ShipClickShowsWholeReachableAreaAlongsideAttackAndCardHover()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            yield return PrepareThreePointDragHand(controller, session);
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            var attack = Object.FindFirstObjectByType<NavalAttackRangePreview>();
            var attackGraphic = Object.FindFirstObjectByType<NavalTacticalAttackRangeGraphic>();
            Assert.That(input.IsShipSelected, Is.False);
            string unchanged = GuidedBossFingerprint(session);
            ClickTacticalShipForRoute(input);
            yield return null;
            Assert.That(input.IsShipSelected, Is.True);
            Assert.That(attack.ShipRangePinned, Is.True);
            AssertAttackCell(attackGraphic, session, 7, 0, true);
            AssertAttackCell(attackGraphic, session, 8, 0, false);
            AssertAttackCell(attackGraphic, session, -5, 0, false);
            Assert.That(input.ReachableCells.Count, Is.GreaterThan(session.GetNavalLegalNextSteps().Count),
                "The green area contains every legal endpoint within all AP, not only the next cell.");
            CollectionAssert.AreEquivalent(session.GetNavalReachableCells().Select(TacticalPointKey),
                input.ReachableCells.Select(TacticalPointKey));
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            Assert.That(input.ReachableCells.Any(point => TacticalPointsMatch(point,
                new VoyagePoint(start.X + 300f, start.Y))), Is.True);
            Assert.That(input.ReachableCells.Any(point => TacticalPointsMatch(point,
                new VoyagePoint(start.X + 100f, start.Y + 200f))), Is.True);
            Assert.That(input.ReachableCells.Any(point => TacticalPointsMatch(point, start)), Is.False,
                "Three AP cannot make a legal forward-first loop back to the origin.");
            var shown = input.ReachableCells.Select(TacticalPointKey).ToArray();
            var routeGraphic = input.GetComponentInChildren<NavalTacticalRouteGraphic>(true);
            Assert.That(routeGraphic.gameObject.activeInHierarchy, Is.True);
            Assert.That(routeGraphic.raycastTarget, Is.False);
            CollectionAssert.AreEquivalent(shown, routeGraphic.ReachableCells.Select(TacticalPointKey),
                "The rendered green area must receive the complete reachable set.");
            ClickTacticalCell(input, new VoyagePoint(start.X + 100f, start.Y));
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero,
                "An ordinary left click on a sea cell no longer appends a route.");
            Button cannon = RequiredResponsive<Button>("Battle Card 0");
            var pointer = TacticalCardPointer(cannon);
            AssertGuidedBattleTopRaycastButton(pointer.position, cannon, "Range hover keeps the actual card accessible");
            ExecuteEvents.Execute(cannon.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(attack.ShowingCardId, Is.EqualTo("card_chain_shot"));
            CollectionAssert.AreEquivalent(shown, input.ReachableCells.Select(TacticalPointKey));
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            ExecuteEvents.Execute(cannon.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(attack.ShowingCardId, Is.Null);
            AssertAttackCell(attackGraphic, session, 7, 0, true);
            AssertAttackCell(attackGraphic, session, 8, 0, false);
            AssertAttackCell(attackGraphic, session, -5, 0, false);
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged));
            NavalBattleVisualCapture.Capture(controller, "NavalDrag_01_AllReachable.png",
                "点击我舰后显示全部3行动点可达海域，攻击红格与卡牌悬停独立共存", "Naval Tactical Board");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalDragRoute_ContinuousDragBacktracksAndRejectsIllegalOrUnaffordableSegments()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            yield return PrepareThreePointDragHand(controller, session);
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            var first = new VoyagePoint(start.X + 100f, start.Y);
            var second = new VoyagePoint(start.X + 100f, start.Y + 100f);
            var third = new VoyagePoint(start.X + 200f, start.Y + 100f);
            string unchanged = GuidedBossFingerprint(session);
            ClickTacticalShipForRoute(input);
            var drag = new TacticalDragPointer(input);
            drag.DragTo(new VoyagePoint(start.X - 100f, start.Y));
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero, "Dragging astern cannot make the first leg reverse.");
            drag.DragTo(start); // Return along the actual pointer path before trying a different leg.
            drag.DragTo(new VoyagePoint(start.X, start.Y + 100f));
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero, "The first leg still follows the bow.");
            drag.DragTo(start);
            drag.DragTo(first);
            drag.DragTo(second);
            drag.DragTo(third);
            AssertTacticalPlannedPath(session, first, second, third);
            Assert.That(input.IsDraggingRoute, Is.True, "All three legs belong to one continuous held-pointer gesture.");
            drag.DragTo(new VoyagePoint(start.X + 300f, start.Y + 100f));
            AssertTacticalPlannedPath(session, first, second, third);
            drag.DragTo(third);
            drag.DragTo(second);
            AssertTacticalPlannedPath(session, first, second);
            drag.DragTo(first);
            AssertTacticalPlannedPath(session, first);
            drag.DragTo(start);
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero, "Dragging back to the ship clears only the preview.");
            drag.DragTo(first);
            drag.DragTo(second);
            drag.Release();
            yield return null;
            AssertTacticalPlannedPath(session, first, second);
            Assert.That(input.IsDraggingRoute, Is.False);
            Assert.That(RequiredResponsive<Button>("Commit Naval Movement").IsInteractable(), Is.True);
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged), "Mouse release cannot spend AP or move the ship.");
            NavalBattleVisualCapture.Capture(controller, "NavalDrag_02_PreviewAfterRelease.png",
                "同次拖动选择折线并回拖撤销；松开保留两格路线，行动点与船位未变化", "Naval Tactical Board");

            ClickAcceptanceButton("Clear Naval Movement");
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            var fast = new TacticalDragPointer(input);
            fast.DragTo(new VoyagePoint(start.X + 300f, start.Y));
            AssertTacticalPlannedPath(session, first, new VoyagePoint(start.X + 200f, start.Y),
                new VoyagePoint(start.X + 300f, start.Y));
            fast.Release();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged),
                "One fast drag event still traverses each crossed cell without submitting movement.");
            ClickAcceptanceButton("Clear Naval Movement");
            // A close-enemy fixture checks collision through the same real pointer path.
            MethodInfo pose = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            pose.Invoke(session.Battle, new object[] { true, new VoyagePoint(-300f, 0f), 0f });
            pose.Invoke(session.Battle, new object[] { false, new VoyagePoint(0f, 0f), 180f });
            typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, 4);
            RenderResponsiveBattle(controller);
            yield return null;
            yield return WaitForGuidedCaptureScreenReveal(controller);
            input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            unchanged = GuidedBossFingerprint(session);
            var collision = new TacticalDragPointer(input);
            collision.DragTo(new VoyagePoint(-200f, 0f));
            collision.DragTo(new VoyagePoint(-100f, 0f));
            collision.DragTo(new VoyagePoint(0f, 0f));
            AssertTacticalPlannedPath(session, new VoyagePoint(-200f, 0f), new VoyagePoint(-100f, 0f), new VoyagePoint(0f, 0f));
            Assert.That(input.ReachableCells.Any(point => TacticalPointsMatch(point, new VoyagePoint(0f, 0f))), Is.True);
            Assert.That(session.NavalMovementEndsInRam, Is.True);
            Assert.That(input.GetComponentInChildren<NavalTacticalRouteGraphic>().EndsInRam, Is.True);
            Assert.That(RequiredResponsive<Button>("Commit Naval Movement").GetComponentInChildren<Text>().text, Is.EqualTo("确定冲撞"));
            collision.DragTo(new VoyagePoint(100f, 0f));
            AssertTacticalPlannedPath(session, new VoyagePoint(-200f, 0f), new VoyagePoint(-100f, 0f), new VoyagePoint(0f, 0f));
            Assert.That(RequiredResponsive<Text>("Naval movement instruction").text, Does.Contain("敌舰格结束"));
            collision.Release();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalDragRoute_DisableEnableCancelsGestureAndRestoresPreviewControls()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            yield return PrepareThreePointDragHand(controller, session);
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            var first = new VoyagePoint(start.X + 100f, start.Y);
            var second = new VoyagePoint(start.X + 100f, start.Y + 100f);
            var third = new VoyagePoint(start.X + 200f, start.Y + 100f);
            string unchanged = GuidedBossFingerprint(session);
            var interrupted = new TacticalDragPointer(input);
            interrupted.DragTo(first);
            interrupted.DragTo(second);
            input.enabled = false;
            Assert.That(input.IsDraggingRoute, Is.False);
            interrupted.DragTo(third);
            AssertTacticalPlannedPath(session, first, second);
            input.enabled = true;
            yield return null;
            yield return null;
            interrupted.Release();
            Assert.That(input.IsDraggingRoute, Is.False, "A stale release cannot restart the interrupted gesture.");
            AssertTacticalPlannedPath(session, first, second);
            Button confirm = RequiredResponsive<Button>("Commit Naval Movement");
            Assert.That(confirm.IsInteractable(), Is.True, "Re-enabled route controls must show the preserved preview.");
            AssertGuidedBattleTopRaycastButton(TacticalCardPointer(confirm).position, confirm, "Restored confirm control");
            var resumed = new TacticalDragPointer(input, second);
            resumed.DragTo(third);
            resumed.Release();
            AssertTacticalPlannedPath(session, first, second, third);
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged));
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalDragRoute_RotatedCameraMapsPointerToWorldAndLocksUntilArrivalHeading()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            MethodInfo pose = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            pose.Invoke(session.Battle, new object[] { true, new VoyagePoint(0f, -600f), 90f });
            pose.Invoke(session.Battle, new object[] { false, new VoyagePoint(0f, 0f), 270f });
            yield return PrepareThreePointDragHand(controller, session);
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            NavalTacticalBoardView board = input.GetComponent<NavalTacticalBoardView>();
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, board.CameraRotationDegrees)), Is.GreaterThan(45f));
            var first = new VoyagePoint(0f, -500f);
            var second = new VoyagePoint(100f, -500f);
            var third = new VoyagePoint(100f, -400f);
            Vector2 screenStart = TacticalWorldScreen(input, session.Battle.PlayerPositionMeters);
            Vector2 screenFirst = TacticalWorldScreen(input, first);
            Assert.That(screenFirst.x, Is.GreaterThan(screenStart.x), "North in world space appears right under the rotated camera.");
            Assert.That(screenFirst.y, Is.EqualTo(screenStart.y).Within(1f));
            yield return DragTacticalRoute(controller, new[] { first, second, third });
            AssertTacticalPlannedPath(session, first, second, third);
            Assert.That(session.NavalMovementPath[1].HeadingDegrees, Is.EqualTo(90f));
            Assert.That(session.NavalMovementPath[2].HeadingDegrees, Is.EqualTo(0f));
            Assert.That(session.NavalMovementPath[3].HeadingDegrees, Is.EqualTo(90f));
            int energy = session.Battle.Energy, cards = session.Battle.Hand.Count, turn = session.Battle.Turn;
            Button oldCard = RequiredResponsive<Button>("Battle Card 0");
            var oldCommit = RequiredResponsive<Button>("Commit Naval Movement").onClick;
            ClickAcceptanceButton("Commit Naval Movement");
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(oldCard.IsInteractable(), Is.False);
            oldCommit.Invoke();
            oldCard.onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, third);
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(oldCard.IsInteractable(), Is.False);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.False);
            oldCard.onClick.Invoke();
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(cards));
            Assert.That(session.Battle.Turn, Is.EqualTo(turn));
            CollectionAssert.AreEquivalent(new[] { 90f, 180f, 0f }, session.GetNavalArrivalHeadings());
            int heading = session.GetNavalArrivalHeadings().ToList().IndexOf(0f);
            yield return WaitTacticalHeadingPointerReady("Naval Arrival Heading " + heading);
            NavalBattleVisualCapture.Capture(controller, "NavalDrag_RotatedBeforeHeading.png",
                "旋转后终向按钮已真实绘制，点击前的实际画面", "Naval Tactical Board");
            yield return WaitTacticalHeadingPointerReady("Naval Arrival Heading " + heading);
            ClickAcceptanceButton("Naval Arrival Heading " + heading);
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.True);
            NavalBattleVisualCapture.Capture(controller, "NavalDrag_03_RotatedCameraArrival.png",
                "旋转镜头下按世界北、东、北拖出折线；确认与终向选择走正式动画并清空行动点", "Naval Tactical Board");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator PrepareThreePointDragHand(CoreGameplayDemoController controller, CoreVoyageSession session)
        {
            SetResponsiveHand(session, "card_turn_port", "card_sail_forward", "card_chain_shot", "card_brace");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            yield return WaitForGuidedCaptureScreenReveal(controller);
            for (int index = 0; index < 2; index++)
            {
                ClickAcceptanceButton("Battle Card 0");
                yield return WaitResponsiveBattle(controller);
            }
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(3));
        }

        private static IEnumerator WaitTacticalHeadingPointerReady(string targetName)
        {
            Button target = RequiredResponsive<Button>(targetName);
            float deadline = Time.realtimeSinceStartup + 2f;
            var hits = new List<RaycastResult>();
            do
            {
                // A newly enabled heading (or a Canvas restored after a visual
                // capture) needs a real render pass before GraphicRaycaster sees it.
                yield return null;
                hits.Clear();
                EventSystem.current.RaycastAll(TacticalCardPointer(target), hits);
                if (target.IsInteractable() && target.targetGraphic.depth >= 0 &&
                    hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == target) yield break;
            } while (Time.realtimeSinceStartup < deadline);
            Assert.Fail("The real heading control did not become pointer-ready: " + targetName +
                "; depth=" + target.targetGraphic.depth + "; active=" + target.gameObject.activeInHierarchy +
                "; culled=" + target.targetGraphic.canvasRenderer.cull +
                "; hits=" + string.Join(" | ", hits.Select(hit => hit.gameObject.name)));
        }

        private static IEnumerator DragTacticalRoute(CoreGameplayDemoController controller, IList<VoyagePoint> route)
        {
            yield return WaitResponsiveBattle(controller);
            yield return WaitForGuidedCaptureScreenReveal(controller);
            var session = controller.Session;
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            Assert.That(input, Is.Not.Null);
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero, "Start a fresh real drag from the player's ship.");
            string unchanged = GuidedBossFingerprint(session);
            var drag = new TacticalDragPointer(input);
            for (int index = 0; index < route.Count; index++)
            {
                drag.DragTo(route[index]);
                yield return null;
                Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(index + 1));
                Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged));
            }
            drag.Release();
            yield return null;
            AssertTacticalPlannedPath(session, route.ToArray());
            Assert.That(input.IsDraggingRoute, Is.False);
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged), "Preview and release never submit movement.");
        }

        private static void AssertTacticalPlannedPath(CoreVoyageSession session, params VoyagePoint[] points)
        {
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(points.Length));
            for (int index = 0; index < points.Length; index++)
                AssertTacticalPoint(session.NavalMovementPath[index + 1].PositionMeters, points[index]);
        }

        private static bool TacticalPointsMatch(VoyagePoint a, VoyagePoint b) =>
            Mathf.Abs(a.X - b.X) < .01f && Mathf.Abs(a.Y - b.Y) < .01f;
        private static string TacticalPointKey(VoyagePoint point) => point.X.ToString("F2") + "," + point.Y.ToString("F2");

        private static Vector2 TacticalWorldScreen(NavalTacticalMovementInput input, VoyagePoint point)
        {
            Canvas.ForceUpdateCanvases();
            NavalTacticalBoardView board = input.GetComponent<NavalTacticalBoardView>();
            Canvas canvas = input.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(camera,
                input.transform.TransformPoint(board.WorldToBoard(point)));
        }

        private static RaycastResult TacticalPointerHit(Vector2 screen)
        {
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            Assert.That(hits, Is.Not.Empty, "The real pointer must hit the board or a ship at " + screen);
            return hits[0];
        }

        private static PointerEventData TacticalCardPointer(Button card)
        {
            RectTransform rect = (RectTransform)card.transform;
            Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
        }

        private static void ClickTacticalShipForRoute(NavalTacticalMovementInput input)
        {
            RectTransform ship = RequiredResponsive<RectTransform>("Player Ship Art");
            Canvas canvas = ship.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, ship.position);
            RaycastResult hit = TacticalPointerHit(screen);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject), Is.SameAs(ship.gameObject));
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = screen, pressPosition = screen, button = PointerEventData.InputButton.Left,
                pointerCurrentRaycast = hit, pointerPressRaycast = hit, eligibleForClick = true
            };
            pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private sealed class TacticalDragPointer
        {
            private readonly NavalTacticalMovementInput input;
            private readonly PointerEventData pointer;
            private readonly GameObject dragTarget;

            internal TacticalDragPointer(NavalTacticalMovementInput movement, VoyagePoint? startPoint = null)
            {
                input = movement;
                if (!input.IsShipSelected) ClickTacticalShipForRoute(input);
                RectTransform ship = RequiredResponsive<RectTransform>("Player Ship Art");
                Canvas canvas = ship.GetComponentInParent<Canvas>().rootCanvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 screen = startPoint.HasValue ? TacticalWorldScreen(input, startPoint.Value) :
                    RectTransformUtility.WorldToScreenPoint(camera, ship.position);
                RaycastResult hit = TacticalPointerHit(screen);
                dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject);
                Assert.That(dragTarget, Is.Not.Null, "The real ship hit must capture and forward a drag to its route planner.");
                Assert.That(dragTarget, Is.SameAs(startPoint.HasValue ? input.gameObject : ship.gameObject));
                pointer = new PointerEventData(EventSystem.current)
                {
                    position = screen, pressPosition = screen, button = PointerEventData.InputButton.Left,
                    pointerCurrentRaycast = hit, pointerPressRaycast = hit, pointerDrag = dragTarget,
                    useDragThreshold = false, eligibleForClick = false
                };
                pointer.pointerPress = ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                pointer.rawPointerPress = hit.gameObject;
                ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.initializePotentialDrag);
                ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.beginDragHandler);
                pointer.dragging = true;
                Assert.That(input.IsDraggingRoute, Is.True);
            }

            internal void DragTo(VoyagePoint point)
            {
                Vector2 screen = TacticalWorldScreen(input, point);
                RaycastResult hit = TacticalPointerHit(screen);
                Assert.That(hit.gameObject == input.gameObject || hit.gameObject.name == "Player Ship Art" ||
                    hit.gameObject.name == "Enemy Ship Art", Is.True,
                    "A route cell cannot be intercepted by cards, annotations or controls: " + hit.gameObject.name);
                pointer.delta = screen - pointer.position;
                pointer.position = screen;
                pointer.pointerCurrentRaycast = hit;
                ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.dragHandler);
            }

            internal void Release()
            {
                ExecuteEvents.Execute(pointer.pointerPress, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.endDragHandler);
                pointer.dragging = false;
                pointer.pointerPress = null;
                pointer.pointerDrag = null;
            }
        }
    }
}
