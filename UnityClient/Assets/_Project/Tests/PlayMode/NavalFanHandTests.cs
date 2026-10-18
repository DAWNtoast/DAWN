using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator NavalSpecialActions_ActionPointsRefreshButtonsAndBothWorkAtZeroEnergy()
        {
            foreach (bool ram in new[] { false, true })
            {
                yield return LoadResponsiveBattleScene();
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
                SetResponsiveHand(session, "card_approach", "card_brace");
                ConfigureCloseNavalTestPose(session.Battle, ram);
                // Eligibility must use the actual adjacent positions and headings,
                // even if this old summary enum is stale.
                session.Battle.Alignment = NavalBattleAlignment.Maneuvering;
                typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, 0);
                session.Battle.Energy = 0;
                RenderResponsiveBattle(controller);
                yield return WaitResponsiveBattle(controller);

                string actionName = ram ? "Battle Ram" : "Battle Board";
                Button action = RequiredResponsive<Button>(actionName);
                Assert.That(action.interactable, Is.False, "The adjacent pose still needs one action point.");
                Assert.That(action.GetComponentInChildren<Text>().text, Is.EqualTo(ram ? "冲撞" : "接舷"));
                Assert.That(NavalHudTooltipText(actionName), Does.Contain("消耗 1 行动点"));

                Assert.That(session.GetBattleCardEnergyCost(session.Battle.Hand[0]), Is.Zero,
                    "The first approach grants an AP at its actual zero energy cost.");
                RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
                yield return WaitResponsiveBattle(controller);
                Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
                Assert.That(session.Battle.Energy, Is.Zero);
                Assert.That(RequiredResponsive<Button>(actionName), Is.SameAs(action));
                Assert.That(action.interactable, Is.True, "An AP card must immediately enable the retained action even with zero energy.");
                Assert.That(NavalHudTooltipText(actionName),
                    Does.Contain(ram ? "垂直" : "同向").And.Contain("不耗能量"));

                // AP changes without rebuilding the scene still refresh these
                // controls through the same observer that updates the resource HUD.
                typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, 0);
                yield return null;
                yield return null;
                Assert.That(action.interactable, Is.False);
                typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(session.Battle, 1);
                yield return null;
                yield return null;
                Assert.That(action.interactable, Is.True);

                action.onClick.Invoke();
                Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(session.Battle.NavalActionPoints, Is.Zero);
                int playerHull = session.Hull, enemyHull = session.Battle.EnemyHull;
                action.onClick.Invoke();
                Assert.That(session.Hull, Is.EqualTo(playerHull));
                Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
                yield return null;
                yield return null;
                Assert.That(NavalActionPointAmount().text, Is.EqualTo("0"),
                    "The outgoing naval HUD must show the committed AP while the boarding transition plays.");
                Assert.That(action.interactable, Is.False);
                yield return WaitResponsiveBattle(controller);
                Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(session.Battle.Energy, Is.EqualTo(3), "The new boarding phase uses its ordinary energy reset.");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator NavalActionHud_ImmediateCardsRefreshPointsAndLockThroughArrivalHeading()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_turn_port", "card_brace", "card_brace");
            session.Battle.DistanceMeters = 600f;
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);

            Assert.That(NavalActionPointAmount().text, Is.EqualTo("1"));
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            float initialHeading = session.Battle.PlayerHeadingDegrees;
            Button retainedGuard = RequiredResponsive<Button>("Battle Card 1");
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(NavalActionPointAmount().text, Is.EqualTo("2"));
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(start.X));
            Assert.That(session.Battle.PlayerPositionMeters.Y, Is.EqualTo(start.Y));
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(initialHeading),
                "Playing a movement card grants AP immediately without moving or turning the ship.");
            Assert.That(RequiredResponsive<Button>("Battle Card 0"), Is.SameAs(retainedGuard));
            RectTransform guardRect = (RectTransform)retainedGuard.transform;
            Quaternion restingRotation = guardRect.localRotation;
            Vector2 restingPosition = guardRect.anchoredPosition;
            int energyBefore = session.Battle.Energy;
            int handBefore = session.Battle.Hand.Count;
            int playsBefore = session.Battle.CardsPlayedThisTurn;

            NavalTacticalMovementInput movement = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            Assert.That(movement, Is.Not.Null);
            yield return DragTacticalRoute(controller, new[] { session.GetNavalLegalNextSteps()[0] });
            movement.CommitSelection();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(retainedGuard.interactable, Is.False);
            retainedGuard.onClick.Invoke();
            yield return null;
            Assert.That(session.Battle.NavalActionPoints, Is.Zero, "The committed route spends the ordinary movement budget.");
            Assert.That(NavalActionPointAmount().text, Is.EqualTo(session.Battle.NavalArrivalActionPoints.ToString()),
                "The visible badge shows the contact-only remainder while the ship travels.");
            Assert.That(NavalHudTooltipText("Resource ActionPoint"), Does.Contain("抵达剩余行动点"));
            yield return WaitResponsiveBattle(controller);

            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(retainedGuard.interactable, Is.False);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(1));
            Assert.That(NavalActionPointAmount().text, Is.EqualTo("1"));
            Assert.That(NavalHudTooltipText("Resource ActionPoint"),
                Does.Contain("冲撞或接舷").And.Contain("选择到达朝向后清空"));
            retainedGuard.onClick.Invoke();
            yield return null;
            Assert.That(session.Battle.Energy, Is.EqualTo(energyBefore));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(handBefore));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(playsBefore));

            float arrivalHeading = session.GetNavalArrivalHeadings()[1];
            movement.ChooseHeading(arrivalHeading);
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(retainedGuard.interactable, Is.False,
                "Clearing the Core heading choice cannot unlock cards before its visible turn finishes.");
            retainedGuard.onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(arrivalHeading));
            Assert.That(NavalActionPointAmount().text, Is.EqualTo("0"));
            Assert.That(NavalHudTooltipText("Resource ActionPoint"), Does.Contain("用于海战航行、冲撞和接舷"));
            Assert.That(retainedGuard.interactable, Is.True);
            Assert.That(Quaternion.Angle(guardRect.localRotation, restingRotation), Is.LessThan(.1f));
            Assert.That(Vector2.Distance(guardRect.anchoredPosition, restingPosition), Is.LessThan(.1f));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(handBefore));
            retainedGuard.onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(playsBefore + 1));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(handBefore - 1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFanHand_HoverKeepsOriginalPointerRevealsCardAndRestoresFanAfterPlay() =>
            AssertBattleFanHoverAndPlay(NavalBattleStage.Naval, 5);

        [UnityTest]
        public IEnumerator BoardingFanHand_HoverKeepsOriginalPointerRevealsCardAndRestoresFanAfterPlay() =>
            AssertBattleFanHoverAndPlay(NavalBattleStage.Boarding, 5);

        [UnityTest]
        public IEnumerator BoardingFanHand_EightCardsKeepCostsInspectableAndRetainFanAfterPlay() =>
            AssertBattleFanHoverAndPlay(NavalBattleStage.Boarding, 8);

        private static IEnumerator AssertBattleFanHoverAndPlay(NavalBattleStage stage, int count)
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, stage);
            if (stage == NavalBattleStage.Boarding)
            {
                session.Battle.Stage = stage;
                session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 200;
                session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 200;
            }
            var cards = new string[count];
            for (int index = 0; index < count; index++)
                cards[index] = stage == NavalBattleStage.Boarding ? "card_crew_guard" : "card_brace";
            SetResponsiveHand(session, cards);
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            RectTransform hand = RequiredResponsive<RectTransform>("Battle Hand");
            Button first = RequiredResponsive<Button>("Battle Card 0");
            Button second = RequiredResponsive<Button>("Battle Card 1");
            RectTransform firstRect = (RectTransform)first.transform;
            Vector2 restingPosition = firstRect.anchoredPosition;
            Quaternion restingRotation = firstRect.localRotation;
            Assert.That(Quaternion.Angle(restingRotation, Quaternion.identity), Is.GreaterThan(10f));
            Assert.That(Quaternion.Angle(firstRect.localRotation, second.transform.localRotation), Is.GreaterThan(1f));
            Assert.That(FanWorldRect(first.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform)
                .Overlaps(FanWorldRect(second.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform)), Is.True,
                "The resting hand intentionally overlaps to leave the battle scene clear.");

            Canvas canvas = first.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform energy = RequiredResponsive<RectTransform>("Card Energy Cost 0");
            Vector2 pointerPosition = RectTransformUtility.WorldToScreenPoint(camera, energy.position);
            AssertNavalFanPointer(pointerPosition, first);
            var pointer = new PointerEventData(EventSystem.current) { position = pointerPosition };
            ExecuteEvents.Execute(first.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.65f);
            AssertNavalFanPointer(pointerPosition, first);
            Assert.That(firstRect.anchoredPosition.y, Is.GreaterThan(restingPosition.y + 40f));
            Assert.That(Quaternion.Angle(firstRect.localRotation, Quaternion.identity), Is.LessThan(.1f));
            Assert.That(first.GetComponent<Canvas>().overrideSorting, Is.True);
            Assert.That(first.GetComponentInChildren<CardSchoolFrameGraphic>().GetComponent<Outline>().enabled, Is.True);
            Rect inspection = FanWorldRect(first.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
            Rect viewport = FanWorldRect(FixedGameViewport.Bounds(canvas));
            Assert.That(inspection.yMin, Is.GreaterThanOrEqualTo(viewport.yMin));
            Assert.That(inspection.yMax, Is.LessThanOrEqualTo(viewport.yMin + viewport.height * .32f),
                "Inspection remains in the bottom portion rather than covering the battle.");

            ExecuteEvents.Execute(first.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(Quaternion.Angle(firstRect.localRotation, restingRotation), Is.LessThan(.1f));
            Assert.That(Vector2.Distance(firstRect.anchoredPosition, restingPosition), Is.LessThan(.1f));
            Assert.That(first.GetComponent<Canvas>().overrideSorting, Is.False);

            ExecuteEvents.Execute(first.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(count - 1));
            Assert.That(RequiredResponsive<Button>("Battle Card 0"), Is.SameAs(second),
                "Playing a fanned card preserves the existing retained-card identity and callback.");
            Assert.That(second.transform.parent, Is.SameAs(hand));
            Assert.That(Quaternion.Angle(second.transform.localRotation, Quaternion.identity), Is.GreaterThan(1f));
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        private static Text NavalActionPointAmount() => RequiredResponsive<RectTransform>("Resource ActionPoint")
            .Find("Amount").GetComponent<Text>();

        private static string NavalHudTooltipText(string targetName)
        {
            GameObject target = RequiredResponsive<RectTransform>(targetName).gameObject;
            var pointer = new PointerEventData(EventSystem.current);
            Assert.That(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerEnterHandler), Is.True,
                targetName + " exposes its current rules through the visible hover UI.");
            BattleStatusTooltip tooltip = RequiredResponsive<BattleStatusTooltip>("Battle Status Tooltip");
            string explanation = tooltip.GetComponentInChildren<Text>().text;
            Assert.That(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerExitHandler), Is.True);
            Assert.That(tooltip.gameObject.activeInHierarchy, Is.False);
            return explanation;
        }

        private static void AssertNavalFanPointer(Vector2 point, Button expected)
        {
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(expected.gameObject),
                "The visible energy tab and its stationary pointer keep addressing the same fanned card.");
        }

        private static Rect FanWorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 minimum = corners[0], maximum = corners[0];
            foreach (Vector3 corner in corners)
            {
                minimum = Vector2.Min(minimum, corner);
                maximum = Vector2.Max(maximum, corner);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }
    }
}
