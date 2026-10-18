using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavalArrivalContact_BoardingUsesReservedPointAfterRealDrag()
        {
            yield return ExerciseNavalArrivalContact(true);
        }

        [UnityTest]
        public IEnumerator NavalArrivalContact_RamUsesReservedPointAfterRealDrag()
        {
            yield return ExerciseNavalArrivalContact(false);
        }

        [UnityTest]
        public IEnumerator NavalArrivalContact_OrdinaryHeadingDiscardsReservedPoints()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            yield return PrepareNavalArrivalContact(controller, session, 0f);
            yield return DragAndCommitNavalContactArrival(controller);

            Assert.That(session.CanNavalArrivalContact(true, out float contactHeading, out string reason), Is.True, reason);
            Assert.That(contactHeading, Is.EqualTo(0f));
            var board = RequiredResponsive<Button>("Battle Board");
            var abandonedContact = board.onClick;
            Assert.That(board.IsInteractable(), Is.True);
            int hull = session.Hull, enemyHull = session.Battle.EnemyHull;
            int hand = session.Battle.Hand.Count, plays = session.Battle.CardsPlayedThisTurn;
            int energy = session.Battle.Energy;

            yield return WaitTacticalHeadingPointerReady("Naval Arrival Heading 0");
            ClickAcceptanceButton("Naval Arrival Heading 0");
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero,
                "Ordinary heading confirmation discards the contact-only remainder immediately.");
            Assert.That(session.Battle.NavalActionPoints, Is.Zero,
                "The reserved remainder must never become ordinary movement AP.");
            string stopped = GuidedBossFingerprint(session);
            abandonedContact.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(stopped));
            yield return WaitResponsiveBattle(controller);

            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f));
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.CanNavalArrivalContact(true, out _, out _), Is.False);
            Assert.That(session.CanBoard(out string ordinaryReason), Is.False);
            Assert.That(ordinaryReason, Does.Contain("行动点"),
                "The same adjacent, matching heading is legal geometry but no longer has a paid contact.");
            Assert.That(RequiredResponsive<Button>("Battle Board").IsInteractable(), Is.False);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.True);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(hand));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(plays));
            stopped = GuidedBossFingerprint(session);
            abandonedContact.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(stopped),
                "The old arrival callback cannot spend discarded AP after ordinary stopping finishes.");
            NavalBattleVisualCapture.Capture(controller, "NavalArrival_03_OrdinaryStop.png",
                "普通选向完成后丢弃到达余量：同向相邻仍需重新获得行动点才能接舷");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator ExerciseNavalArrivalContact(bool boarding)
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            // Both encounters use the same northern enemy heading. Boarding must
            // turn north at arrival; ramming keeps the east-facing bow at its side.
            yield return PrepareNavalArrivalContact(controller, session, 90f);
            yield return DragAndCommitNavalContactArrival(controller);

            Assert.That(session.CanNavalArrivalContact(boarding, out float heading, out string reason), Is.True, reason);
            Assert.That(heading, Is.EqualTo(boarding ? 90f : 0f));
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(0f),
                "Querying and displaying contact choices cannot select their final heading.");
            Assert.That(session.CanBoard(out _), Is.False);
            Assert.That(session.CanRam(out _), Is.False,
                "Ordinary contact entry points stay locked until arrival chooses an action.");
            string name = boarding ? "Battle Board" : "Battle Ram";
            var action = RequiredResponsive<Button>(name);
            var repeatedContact = action.onClick;
            Assert.That(action.IsInteractable(), Is.True);
            Assert.That(action.GetComponentInChildren<Text>().text, Is.EqualTo(boarding ? "接舷" : "冲撞"));
            Assert.That(NavalHudTooltipText(name), Does.Contain("消耗 1 行动点")
                .And.Contain("到达剩余 1 点").And.Contain(boarding ? "自动朝北" : "自动朝东"));
            Assert.That(session.Battle.Energy, Is.Zero,
                "Arrival contact must work without paying for an extra energy card.");
            NavalBattleVisualCapture.Capture(controller,
                boarding ? "NavalArrival_01_BoardingReady.png" : "NavalArrival_02_RamReady.png",
                boarding ? "航行一格后保留1点：直接接舷并自动转向与敌舰同向" :
                    "航行一格后保留1点：直接以船头冲撞垂直敌舰，无需停船再出牌");

            int playerHull = session.Hull, enemyHull = session.Battle.EnemyHull;
            yield return WaitTacticalHeadingPointerReady(name);
            ClickAcceptanceButton(name);
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(heading));
            Assert.That(session.Battle.Energy, Is.EqualTo(3),
                "Contact consumes the reserved AP; entering the formal boarding phase refreshes energy.");
            Assert.That(action.IsInteractable(), Is.False);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.False);
            Assert.That(session.Battle.BoardingConnection, Is.EqualTo(boarding
                ? NavalBoardingConnection.Planned : NavalBoardingConnection.RamBowToBroadside));
            if (boarding)
            {
                Assert.That(session.Hull, Is.EqualTo(playerHull));
                Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemyHull));
            }
            else
            {
                Assert.That(session.Hull, Is.LessThan(playerHull));
                Assert.That(session.Battle.EnemyHull, Is.LessThan(enemyHull));
            }

            string committed = GuidedBossFingerprint(session);
            repeatedContact.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed),
                "A second callback during the contact animation cannot charge AP or apply hull damage twice.");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed),
                "Finishing the presentation must not replay the committed contact transaction.");
            repeatedContact.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed),
                "A retired naval button cannot repeat contact after the boarding controls appear.");
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator PrepareNavalArrivalContact(CoreGameplayDemoController controller,
            CoreVoyageSession session, float enemyHeading)
        {
            MethodInfo pose = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pose, Is.Not.Null);
            pose.Invoke(session.Battle, new object[] { true, new VoyagePoint(-200f, 0f), 0f });
            pose.Invoke(session.Battle, new object[] { false, new VoyagePoint(0f, 0f), enemyHeading });
            SetResponsiveHand(session, "card_approach", "card_brace");
            session.Battle.Energy = 0;
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(1));
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            yield return WaitForGuidedCaptureScreenReveal(controller);
            int hand = session.Battle.Hand.Count, plays = session.Battle.CardsPlayedThisTurn;
            CoreBattleCard approach = session.GetBattleCard(session.Battle.Hand[0]);
            Assert.That(approach.Id, Is.EqualTo("card_approach"));
            Assert.That(session.GetBattleCardEnergyCost(session.Battle.Hand[0]), Is.Zero);
            yield return WaitTacticalHeadingPointerReady("Battle Card 0");
            ClickAcceptanceButton("Battle Card 0");
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(hand - 1 + approach.Draw));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(plays + 1));
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, new VoyagePoint(-200f, 0f));
        }

        private static IEnumerator DragAndCommitNavalContactArrival(CoreGameplayDemoController controller)
        {
            CoreVoyageSession session = controller.Session;
            yield return DragTacticalRoute(controller, new[] { new VoyagePoint(-100f, 0f) });
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.Zero);
            var board = RequiredResponsive<Button>("Battle Board");
            var ram = RequiredResponsive<Button>("Battle Ram");
            var duplicateCommit = RequiredResponsive<Button>("Commit Naval Movement").onClick;
            ClickAcceptanceButton("Commit Naval Movement");
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(1));
            Assert.That(board.IsInteractable(), Is.False);
            Assert.That(ram.IsInteractable(), Is.False);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.False);
            string travelling = GuidedBossFingerprint(session);
            board.onClick.Invoke();
            ram.onClick.Invoke();
            duplicateCommit.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(travelling),
                "Travel locks both contact callbacks and prevents a second movement charge.");
            yield return WaitResponsiveBattle(controller);
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, new VoyagePoint(-100f, 0f));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            Assert.That(session.Battle.NavalArrivalActionPoints, Is.EqualTo(1));
            Assert.That(RequiredResponsive<Button>("End Battle Turn").IsInteractable(), Is.False);
            Assert.That(NavalActionPointAmount().text, Is.EqualTo("1"));
            Assert.That(NavalHudTooltipText("Resource ActionPoint"), Does.Contain("抵达剩余行动点")
                .And.Contain("冲撞或接舷").And.Contain("选择到达朝向后清空"));
        }
    }
}
