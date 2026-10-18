using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavalAttackRange_ShipClickShowsFortyCellsWithoutSpendingOrSelectingMovement()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_chain_shot", "card_brace");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            NavalAttackRangePreview preview = Object.FindFirstObjectByType<NavalAttackRangePreview>();
            NavalTacticalAttackRangeGraphic graphic = Object.FindFirstObjectByType<NavalTacticalAttackRangeGraphic>();
            Assert.That(graphic.Cells, Is.Empty);
            int energy = session.Battle.Energy, points = session.Battle.NavalActionPoints;
            ClickAttackRangeShip();
            Assert.That(preview.ShipRangePinned, Is.True);
            Assert.That(graphic.Cells.Count, Is.EqualTo(40));
            AssertAttackCell(graphic, session, 4, 0, true);
            AssertAttackCell(graphic, session, 2, 2, true);
            AssertAttackCell(graphic, session, 3, 2, false);
            AssertAttackCell(graphic, session, 0, 0, false);
            Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(points));
            Assert.That(session.NavalMovementPlannedSteps, Is.Zero);
            RectTransform caption = RequiredResponsive<RectTransform>("Naval attack range caption");
            Rect viewport = FanWorldRect(FixedGameViewport.Bounds(caption.GetComponentInParent<Canvas>().rootCanvas));
            Assert.That(FanWorldRect(caption).xMin, Is.GreaterThanOrEqualTo(viewport.xMin + 10f));
            NavalBattleVisualCapture.Capture(controller, "AttackRange_01_Ship.png", "点击我舰：红格显示四格炮击范围，右三上二不在范围内");
            ClickAttackRangeShip();
            Assert.That(preview.ShipRangePinned, Is.False);
            Assert.That(graphic.Cells, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalAttackRange_CardHoverOverridesAndRestoresShipRangeEvenWhenCardIsUnavailable()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_chain_shot", "card_diver_sabotage", "card_load_shot", "card_brace");
            session.Battle.Energy = 0;
            session.Cargo.Powder = session.Cargo.Cannonballs = 0;
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            NavalAttackRangePreview preview = Object.FindFirstObjectByType<NavalAttackRangePreview>();
            NavalTacticalAttackRangeGraphic graphic = Object.FindFirstObjectByType<NavalTacticalAttackRangeGraphic>();
            Button cannon = RequiredResponsive<Button>("Battle Card 0");
            Button sabotage = RequiredResponsive<Button>("Battle Card 1");
            Button loading = RequiredResponsive<Button>("Battle Card 2");
            Assert.That(cannon.interactable, Is.False);
            HoverAttackCard(cannon, true);
            Assert.That(preview.ShowingCardId, Is.EqualTo("card_chain_shot"));
            Assert.That(graphic.Cells.Count, Is.EqualTo(40));
            HoverAttackCard(cannon, false);
            Assert.That(graphic.Cells, Is.Empty);

            ClickAttackRangeShip();
            HoverAttackCard(sabotage, true);
            Assert.That(preview.ShowingCardId, Is.EqualTo("card_diver_sabotage"));
            Assert.That(graphic.Cells.Count, Is.EqualTo(8));
            AssertAttackCell(graphic, session, 1, 1, true);
            AssertAttackCell(graphic, session, 2, 0, false);
            yield return new WaitForSecondsRealtime(.35f);
            NavalBattleVisualCapture.Capture(controller, "AttackRange_02_Sabotage.png", "悬停水下破坏：仅显示150米内的8个相邻格，牌因资源不足不可用但仍可预览");
            // A delayed exit from another card cannot clear the current card's preview.
            HoverAttackCard(cannon, false);
            Assert.That(graphic.Cells.Count, Is.EqualTo(8));
            HoverAttackCard(sabotage, false);
            Assert.That(graphic.Cells.Count, Is.EqualTo(40));
            HoverAttackCard(loading, true);
            Assert.That(preview.ShowingCardId, Is.Null, "Loading buffs a later attack rather than targeting a cell.");
            Assert.That(graphic.Cells.Count, Is.EqualTo(40));
            HoverAttackCard(loading, false);
            HoverAttackCard(sabotage, true);
            sabotage.gameObject.SetActive(false);
            Assert.That(graphic.Cells.Count, Is.EqualTo(40), "Removing the hovered card restores the pinned ship range.");
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Cargo.Powder, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalAttackRange_MovementStillReceivesDragAndRangeFollowsArrival()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_chain_shot", "card_brace");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            ClickAttackRangeShip();
            NavalTacticalAttackRangeGraphic graphic = Object.FindFirstObjectByType<NavalTacticalAttackRangeGraphic>();
            NavalTacticalMovementInput movement = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            yield return DragTacticalRoute(controller, new[] { session.GetNavalLegalNextSteps()[0] });
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(1));
            movement.CommitSelection();
            yield return null;
            yield return null;
            Assert.That(graphic.Cells, Is.Empty, "Do not show the final range over an in-flight ship.");
            yield return WaitResponsiveBattle(controller);
            movement.ChooseHeading(session.GetNavalArrivalHeadings()[1]);
            yield return WaitResponsiveBattle(controller);
            yield return null;
            Assert.That(graphic.Cells.Count, Is.EqualTo(40));
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.Not.EqualTo(start.X));
            AssertAttackCell(graphic, session, 4, 0, true);
            AssertAttackCell(graphic, session, -5, 0, false);
            HoverAttackCard(RequiredResponsive<Button>("Battle Card 0"), true);
            session.Battle.Stage = NavalBattleStage.Boarding;
            yield return null;
            yield return null;
            Assert.That(graphic.Cells, Is.Empty, "The outgoing sea overlay disappears on boarding.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalAttackRange_UnrestrictedCardUsesVisibleCellsAndCannonHoverNarrowsThem()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_school_curse_b03", "card_chain_shot");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            NavalTacticalAttackRangeGraphic graphic = Object.FindFirstObjectByType<NavalTacticalAttackRangeGraphic>();
            ClickAttackRangeShip();
            Assert.That(graphic.Cells.Count, Is.GreaterThan(40));
            AssertAttackCell(graphic, session, 5, 0, true);
            HoverAttackCard(RequiredResponsive<Button>("Battle Card 1"), true);
            Assert.That(graphic.Cells.Count, Is.EqualTo(40));
            AssertAttackCell(graphic, session, 3, 2, false);
            HoverAttackCard(RequiredResponsive<Button>("Battle Card 1"), false);
            Assert.That(graphic.Cells.Count, Is.GreaterThan(40));
            HoverAttackCard(RequiredResponsive<Button>("Battle Card 0"), true);
            Assert.That(RequiredResponsive<Text>("Naval attack range caption").text, Does.Contain("无距离限制"));
            session.Battle.DistanceMeters = 10000f;
            bool refreshed = (bool)typeof(CoreGameplayDemoController).GetMethod("TryRefreshRetainedBattleView",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controller, null);
            Assert.That(refreshed, Is.True);
            yield return null;
            yield return null;
            Assert.That(graphic.IsUnlimitedPreview, Is.True, "A distant camera must not build an oversized per-cell UI mesh.");
            Assert.That(graphic.Cells, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertAttackCell(NavalTacticalAttackRangeGraphic graphic, CoreVoyageSession session, int x, int y, bool expected)
        {
            VoyagePoint player = session.Battle.PlayerPositionMeters;
            bool found = graphic.Cells.Any(point => Mathf.Abs(point.X - player.X - x * 100f) < .01f && Mathf.Abs(point.Y - player.Y - y * 100f) < .01f);
            Assert.That(found, Is.EqualTo(expected), "Attack cell " + x + "," + y);
        }

        private static void ClickAttackRangeShip()
        {
            RectTransform ship = RequiredResponsive<RectTransform>("Player Ship Art");
            Canvas canvas = ship.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(camera, ship.position), button = PointerEventData.InputButton.Left };
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(ship.gameObject));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void HoverAttackCard(Button card, bool enter)
        {
            var pointer = new PointerEventData(EventSystem.current);
            if (enter) ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            else ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        }
    }
}
