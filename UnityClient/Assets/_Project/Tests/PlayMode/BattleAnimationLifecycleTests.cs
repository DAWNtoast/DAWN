using System.Collections;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator BattleLifecycle_SuspendBeforeCommitRestoresAPlayableUnconsumedHand()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_emergency_repair");
            RenderResponsiveBattle(controller);
            yield return null;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);

            controller.gameObject.SetActive(false);
            yield return null;
            controller.gameObject.SetActive(true);
            yield return null;
            yield return null;

            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "Suspension must release the presentation lock.");
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero, "An interrupted lift must not secretly commit after reactivation.");
            AssertResponsiveHandMatchesCore(session);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(2));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleLifecycle_SuspendAfterCommitReconcilesTheHandWithoutReplayingTheCard()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_brace", "card_emergency_repair");
            RenderResponsiveBattle(controller);
            yield return null;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (session.Battle.CardsPlayedThisTurn == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);

            controller.gameObject.SetActive(false);
            yield return null;
            controller.gameObject.SetActive(true);
            yield return null;
            yield return null;

            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(2));
            AssertResponsiveHandMatchesCore(session);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            Assert.That(GameObject.Find("Battle Card").GetComponentsInChildren<BattleFxGraphic>(), Is.Empty,
                "Interrupted feedback must not leave frozen particles or floating text over the restored hand.");
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(4));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleLifecycle_DisabledControllerBeforeCommitCannotSilentlyPlayTheCard()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_emergency_repair");
            RenderResponsiveBattle(controller);
            yield return null;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
            controller.enabled = false;
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero,
                "MonoBehaviour suspension must explicitly cancel nested feedback; Unity does not stop it for enabled=false.");
            controller.enabled = true;
            yield return null;
            yield return null;

            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            AssertResponsiveHandMatchesCore(session);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleLifecycle_DisabledControllerCannotLeaveCompletedFeedbackControlsLocked()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_emergency_repair");
            RenderResponsiveBattle(controller);
            yield return null;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (session.Battle.CardsPlayedThisTurn == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            controller.enabled = false;
            yield return new WaitForSecondsRealtime(1f);
            controller.enabled = true;
            yield return null;
            yield return null;

            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            AssertResponsiveHandMatchesCore(session);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleLifecycle_UnloadingSceneDuringCannonFeedbackDoesNotLeaveLateExceptions()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_chain_shot", "card_brace");
            session.Battle.DistanceMeters = 250f;
            RenderResponsiveBattle(controller);
            yield return null;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (session.Battle.CardsPlayedThisTurn == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(1));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
            CoreGameplayDemoController replacement = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(replacement, Is.Not.Null);
            Assert.That(replacement.IsBattleAnimationPlaying, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
