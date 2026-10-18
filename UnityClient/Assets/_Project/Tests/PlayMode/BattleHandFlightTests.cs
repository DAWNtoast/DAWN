using System.Collections;
using System.Linq;
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
        public IEnumerator BattleHandFlight_DeckInspectionCompletesFlightAndClosingRestoresHover()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_brace", "card_brace", "card_brace", "card_brace");
            RenderResponsiveBattle(controller);
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            RequiredResponsive<Button>("Battle Draw Pile").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Battle Deck Overlay"), Is.Not.Null);
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(GameObject.Find("Battle Flight Back"), Is.Null);
            RequiredResponsive<Button>("Close Battle Deck").onClick.Invoke();
            yield return null;
            Button card = RequiredResponsive<Button>("Battle Card 0");
            RectTransform rect = (RectTransform)card.transform;
            Vector3 restingPosition = rect.localPosition;
            Quaternion restingRotation = rect.localRotation;
            Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.position)
            };
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(rect.localPosition.y, Is.GreaterThan(restingPosition.y + 40f));
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(Vector3.Distance(rect.localPosition, restingPosition), Is.LessThan(.1f));
            Assert.That(Quaternion.Angle(rect.localRotation, restingRotation), Is.LessThan(.1f));
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleHandFlight_OpeningDealsFromPileAndCompleteRestoresEveryCardPose()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_chain_shot", "card_emergency_repair", "card_brace", "card_full_sail_turn");
            int[] originalHand = session.Battle.Hand.ToArray();
            RenderResponsiveBattle(controller);
            var motion = Object.FindFirstObjectByType<BattleHandFlightMotion>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.IsPlaying, Is.True, "The first rendered hand starts at its draw pile.");
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(RequiredResponsive<Button>("Battle Card 4").GetComponent<CanvasGroup>().alpha, Is.Zero,
                "A later card face must stay hidden until its staggered departure.");
            float start = Time.realtimeSinceStartup;
            yield return WaitResponsiveBattle(controller);
            Assert.That(Time.realtimeSinceStartup - start, Is.LessThan(1.1f));
            CollectionAssert.AreEqual(originalHand, session.Battle.Hand, "Flying cards cannot mutate the battle model.");
            AssertResponsiveHandMatchesCore(session);

            RectTransform[] cards = Enumerable.Range(0, originalHand.Length)
                .Select(index => RequiredResponsive<RectTransform>("Battle Card " + index)).ToArray();
            Vector3[] positions = cards.Select(card => card.localPosition).ToArray();
            Vector3[] scales = cards.Select(card => card.localScale).ToArray();
            Quaternion[] rotations = cards.Select(card => card.localRotation).ToArray();
            RectTransform drawPile = RequiredResponsive<RectTransform>("Battle Draw Pile");
            Vector3 pileScale = drawPile.localScale;
            motion.Deal(cards, drawPile);
            yield return null;
            Assert.That(Vector3.Distance(cards[0].localPosition, positions[0]), Is.GreaterThan(20f));
            motion.Complete();
            motion.Complete();
            for (int index = 0; index < cards.Length; index++)
            {
                Assert.That(Vector3.Distance(cards[index].localPosition, positions[index]), Is.LessThan(.1f));
                Assert.That(Vector3.Distance(cards[index].localScale, scales[index]), Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(cards[index].localRotation, rotations[index]), Is.LessThan(.1f));
                Assert.That(cards[index].GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                Assert.That(cards[index].GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            Assert.That(drawPile.localScale, Is.EqualTo(pileScale));
            Assert.That(motion.IsPlaying, Is.False);
            yield return null;
            Assert.That(GameObject.Find("Battle Flight Back"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleHandFlight_EndTurnDiscardsOldCopiesAndDealsDuplicateIdsAsNewViews()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_brace");
            SetResponsiveDrawPile(session, "card_brace", "card_brace", "card_brace", "card_brace", "card_brace");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            int[] oldIds = { RequiredResponsive<Button>("Battle Card 0").GetInstanceID(), RequiredResponsive<Button>("Battle Card 1").GetInstanceID() };
            int sceneId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            int turn = session.Battle.Turn;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            var motion = Object.FindFirstObjectByType<BattleHandFlightMotion>();
            Assert.That(motion.IsPlaying, Is.True, "The old hand must visibly leave before the next turn commits.");
            Assert.That(session.Battle.Turn, Is.EqualTo(turn));
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.False);
            bool sawNewDeal = false;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                if (session.Battle.Turn == turn + 1 && motion != null && motion.IsPlaying) sawNewDeal = true;
                yield return null;
            }
            yield return WaitResponsiveBattle(controller);
            Assert.That(sawNewDeal, Is.True, "A repeated ID from the next draw still launches from the deck.");
            Assert.That(session.Battle.Turn, Is.EqualTo(turn + 1));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(5));
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(sceneId));
            foreach (Button card in RequiredResponsive<RectTransform>("Battle Hand").GetComponentsInChildren<Button>())
                CollectionAssert.DoesNotContain(oldIds, card.GetInstanceID());
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleHandFlight_BoardingRetainKeepsItsViewWhileTheOtherCopyIsDiscarded()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Preparation);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 200;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 200;
            SetResponsiveHand(session, "card_crew_bandage", "card_crew_bandage");
            SetResponsiveDrawPile(session, "card_crew_bandage", "card_crew_bandage", "card_crew_bandage", "card_crew_bandage", "card_crew_bandage");
            session.Battle.Boarding.HeldCards.Clear();
            session.Battle.Boarding.HeldCards.Add(new CoreBoardingHeldCard { CardIndex = session.Battle.Hand[0], Retain = true });
            session.Battle.Boarding.HeldCards.Add(new CoreBoardingHeldCard { CardIndex = session.Battle.Hand[1] });
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            Button kept = RequiredResponsive<Button>("Battle Card 0");
            int discardedId = RequiredResponsive<Button>("Battle Card 1").GetInstanceID();
            Vector3 keptPosition = kept.transform.localPosition;
            Quaternion keptRotation = kept.transform.localRotation;
            Assert.That(Quaternion.Angle(keptRotation, Quaternion.identity), Is.GreaterThan(1f),
                "Initial boarding dealing must finish at the fanned resting pose.");
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return null;
            Assert.That(kept.transform.localPosition, Is.EqualTo(keptPosition), "Retained cards never join the outgoing flight.");
            Assert.That(Quaternion.Angle(kept.transform.localRotation, keptRotation), Is.LessThan(.1f));
            Assert.That(kept.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            yield return WaitResponsiveBattle(controller);
            Assert.That(RequiredResponsive<Button>("Battle Card 0"), Is.SameAs(kept));
            Assert.That(session.Battle.Boarding.HeldCards[0].RetainUsed, Is.True);
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(6));
            Assert.That(Quaternion.Angle(kept.transform.localRotation, Quaternion.identity), Is.GreaterThan(10f),
                "The retained card joins the enlarged fan after the fresh cards finish dealing.");
            foreach (Button card in RequiredResponsive<RectTransform>("Battle Hand").GetComponentsInChildren<Button>())
                Assert.That(card.GetInstanceID(), Is.Not.EqualTo(discardedId));
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BattleHandFlight_DisableDuringDiscardRestoresUncommittedHandAndClearsTemporaryBacks()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_chain_shot", "card_brace");
            RenderResponsiveBattle(controller);
            yield return WaitResponsiveBattle(controller);
            int[] originalHand = session.Battle.Hand.ToArray();
            int turn = session.Battle.Turn;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            controller.enabled = false;
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(session.Battle.Turn, Is.EqualTo(turn));
            CollectionAssert.AreEqual(originalHand, session.Battle.Hand);
            controller.enabled = true;
            yield return null;
            yield return WaitResponsiveBattle(controller);
            AssertResponsiveHandMatchesCore(session);
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            Assert.That(GameObject.Find("Battle Flight Back"), Is.Null);
            Assert.That(Object.FindObjectsByType<BattleHandFlightMotion>(FindObjectsSortMode.None).Any(motion => motion.IsPlaying), Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
