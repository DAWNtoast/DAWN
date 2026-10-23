using System;
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
        [UnityTest, Timeout(180000)]
        public IEnumerator GuidedBossScript_HandDealRestoresOnlyLegalFocusWithoutAnotherRender()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return EnterGuidedCardCourse();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseLoad);
                yield return WaitGuidedBossAnimation(controller);
                CoreVoyageSession session = controller.Session;
                int hand = session.Battle.Hand.FindIndex(index => session.GetBattleCard(index).Id == "card_load_shot");
                Assert.That(hand, Is.GreaterThanOrEqualTo(0));
                string targetName = "Battle Card " + hand;
                int energy = session.Battle.Energy;

                // A real resource restriction must not be mistaken for the temporary deal lock.
                session.Battle.Energy = 0;
                Assert.That(session.CanPlayCard(hand), Is.False);
                RenderResponsiveBattle(controller);
                Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                Assert.That(RequireGuidedButton(targetName).interactable, Is.False);
                Assert.That(controller.GetComponentsInChildren<GuidedTutorialFocusGraphic>(), Is.Empty,
                    "An unavailable course card must not register a focus even while dealing.");

                session.Battle.Energy = energy;
                Assert.That(session.CanPlayCard(hand), Is.True);
                RenderResponsiveBattle(controller);
                Assert.That(controller.IsBattleAnimationPlaying, Is.True);
                Button target = RequireGuidedButton(targetName);
                Assert.That(target.interactable, Is.True);
                Assert.That(target.IsInteractable(), Is.False, "The deal CanvasGroup temporarily blocks input.");
                GuidedTutorialFocusGraphic[] focuses = controller.GetComponentsInChildren<GuidedTutorialFocusGraphic>();
                Assert.That(focuses, Has.Length.EqualTo(1), "Only the permitted course target is registered.");
                GuidedTutorialFocusGraphic focus = focuses[0];
                Assert.That(focus.Target, Is.SameAs(target.transform));
                Assert.That(focus.HasVisibleTarget, Is.False, "The red annotation stays hidden during the deal lock.");
                Button[] lockedCards = target.transform.parent.GetComponentsInChildren<Button>()
                    .Where(button => button != target).ToArray();
                Assert.That(lockedCards, Is.Not.Empty);
                Assert.That(lockedCards.All(button => !button.interactable), Is.True,
                    "Other lesson cards remain locked and receive no focus.");
                string unchanged = GuidedBossFingerprint(session);

                // Do not Render or rebuild the overlay: the original marker must recover on its own.
                yield return WaitGuidedBossAnimation(controller);
                Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged));
                Assert.That(RequireGuidedButton(targetName), Is.SameAs(target));
                Assert.That(controller.GetComponentsInChildren<GuidedTutorialFocusGraphic>().Single(), Is.SameAs(focus));
                Assert.That(target.IsInteractable(), Is.True);
                AssertGuidedFocusTarget(targetName, "CourseLoad after the original deal completes");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator GuidedBossScript_PauseDuringRealRamResumesCommittedLessonWithoutReplay()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return EnterGuidedCardCourse();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseRamContact);
                Assert.That(controller.Session.CanCaptureVoyageCheckpoint, Is.False);
                var battle = controller.Session.Battle;
                int extraMove = battle.Hand.FindIndex(index => controller.Session.GetBattleCard(index).Id == "card_approach");
                Assert.That(extraMove, Is.GreaterThanOrEqualTo(0));
                Assert.That(controller.Session.GetBattleCardEnergyCost(battle.Hand[extraMove]), Is.EqualTo(1));
                int handBefore = battle.Hand.Count, playsBefore = battle.CardsPlayedThisTurn;
                ClickAcceptanceButton("Battle Card " + extraMove);
                yield return WaitGuidedBossAnimation(controller);
                Assert.That(battle.Energy, Is.EqualTo(2), "The second use of the zero-base-cost approach costs one energy.");
                Assert.That(battle.Hand.Count, Is.EqualTo(handBefore - 1));
                Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(playsBefore + 1));
                Assert.That(battle.NavalActionPoints, Is.EqualTo(2));
                // Draw the actual next hand and spend its 1 + 2 energy attacks.
                // Do not inject energy or use the former one-energy approach cost.
                ClickAcceptanceButton("End Battle Turn");
                yield return WaitGuidedBossAnimation(controller);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamContact));
                Assert.That(battle.Energy, Is.EqualTo(3));
                Assert.That(battle.NavalActionPoints, Is.EqualTo(1));
                foreach (string attackId in new[] { "card_chain_shot", "card_cannon_barrage" })
                {
                    int attack = battle.Hand.FindIndex(index => controller.Session.GetBattleCard(index).Id == attackId);
                    Assert.That(attack, Is.GreaterThanOrEqualTo(0));
                    var beforeAttack = new GuidedCourseState(controller.Session, attack);
                    Assert.That(beforeAttack.Cost, Is.EqualTo(attackId == "card_chain_shot" ? 1 : 2));
                    Assert.That(controller.Session.CanPlayCard(attack), Is.True);
                    ClickAcceptanceButton("Battle Card " + attack);
                    yield return WaitGuidedBossAnimation(controller);
                    Assert.That(battle.Energy, Is.EqualTo(beforeAttack.Energy - beforeAttack.Cost));
                    Assert.That(battle.Hand.Count, Is.EqualTo(beforeAttack.Hand - 1));
                    Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(beforeAttack.Played + 1));
                    Assert.That(controller.Session.Cargo.Powder, Is.EqualTo(beforeAttack.Powder - 1));
                    Assert.That(controller.Session.Cargo.Cannonballs, Is.EqualTo(beforeAttack.Balls - 1));
                    Assert.That(battle.EnemyHull, Is.LessThan(beforeAttack.EnemyHull));
                }
                Assert.That(battle.Energy, Is.Zero, "Real attack cards exhaust energy before the one-AP contact.");
                Assert.That(battle.NavalActionPoints, Is.EqualTo(1));
                Assert.That(controller.Session.CanRam(out string reason), Is.True, reason);
                int hull = controller.Session.Hull;
                var oldClick = RequireGuidedButton("Battle Ram").onClick;
                ClickAcceptanceButton("Battle Ram");
                Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(controller.Session.Hull, Is.LessThan(hull));
                Assert.That(battle.Energy, Is.EqualTo(3), "The ordinary boarding phase restores its energy after a zero-energy ram.");
                Assert.That(battle.NavalActionPoints, Is.Zero);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamDone));
                string committed = GuidedBossFingerprint(controller.Session);
                yield return null;
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Not.Null, RamComicFailureState(controller));
                MethodInfo pause = typeof(CoreGameplayDemoController).GetMethod("OnApplicationPause",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pause, Is.Not.Null);
                pause.Invoke(controller, new object[] { true }); yield return null;
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                Assert.That(GameObject.Find("Boarding Scene Transition"), Is.Null);
                Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(committed));
                pause.Invoke(controller, new object[] { false }); yield return null; yield return null;
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(committed));
                oldClick.Invoke();
                Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(committed));
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                Assert.That(RequireGuidedButton("Guided Battle Continue").IsInteractable(), Is.True);
                Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.False);
                yield return DriveGuidedTutorial(controller, "Warehouse", new HashSet<string>(), false);
                Assert.That(controller.Session.TreasureFragments, Is.EqualTo(1));
                Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator GuidedBossScript_ExerciseFailureRestartsOnlyCurrentChapterWithoutRewards()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return EnterGuidedCardCourse();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseRamContact);
                // Fault injection only establishes a lethal recoil boundary;
                // every preceding lesson, route and the ram itself was played.
                typeof(CoreVoyageSession).GetProperty("Hull").SetValue(controller.Session, 1);
                RenderResponsiveBattle(controller);
                yield return null;
                ClickAcceptanceButton("Battle Ram");
                yield return WaitGuidedBossAnimation(controller);
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRetry));
                Assert.That(controller.Session.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.RamContact));
                Assert.That(controller.Session.GuidedBossExerciseFailed, Is.True);
                Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.False);
                Assert.That(controller.Session.VoyageTreasureFragments, Is.Zero);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
                var retry = RequireGuidedButton("Guided Battle Retry").onClick;
                ClickAcceptanceButton("Guided Battle Retry");
                yield return null; yield return null;
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamMove));
                Assert.That(controller.Session.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.RamContact));
                Assert.That(controller.Session.GuidedBossExerciseFailed, Is.False);
                Assert.That(controller.Session.Hull, Is.EqualTo(controller.Session.MaxHull));
                AssertTacticalPoint(controller.Session.Battle.PlayerPositionMeters, new VoyagePoint(-200f, 0f));
                Assert.That(controller.Session.Battle.NavalActionPoints, Is.EqualTo(1));
                string committed = GuidedBossFingerprint(controller.Session);
                retry.Invoke();
                Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(committed), "A retired retry cannot reset again.");
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseBoardingAttack);
                Assert.That(controller.Session.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.BoardingCards));
                Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator GuidedBossScript_ContinuousRecoveryDecaysAndInspectionExplainsRemainingLayers()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return EnterGuidedCardCourse();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseAid);
                Assert.That(controller.Session.Battle.Boarding.ContinuousRecovery, Is.Zero);
                int medicine = controller.Session.Cargo.Medicine;
                yield return PlayGuidedScriptAction(controller, false, null);
                Assert.That(controller.Session.Battle.Boarding.ContinuousRecovery, Is.EqualTo(5));
                yield return DriveGuidedCardCourseTo(controller, GuidedBossBattleStep.CourseEffectsInspect);
                Assert.That(controller.Session.Battle.Boarding.ContinuousRecovery, Is.EqualTo(4));
                yield return PlayGuidedScriptAction(controller, false, null);
                yield return PlayGuidedScriptAction(controller, false, null);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseBoardingDone));
                Assert.That(controller.Session.Battle.Boarding.ContinuousRecovery, Is.EqualTo(3));
                Assert.That(controller.Session.Cargo.Medicine, Is.EqualTo(medicine - 1));
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static void AssertGuidedCourseCoverage(ISet<string> seen)
        {
            foreach (GuidedBossLesson lesson in Enum.GetValues(typeof(GuidedBossLesson)))
                if (lesson != GuidedBossLesson.None)
                    Assert.That(seen, Does.Contain("BattleLesson:" + lesson), "The real UI route must visit " + lesson);
            foreach (GuidedBossBattleStep step in new[] { GuidedBossBattleStep.CourseCardInspect,
                GuidedBossBattleStep.CourseFreeMove, GuidedBossBattleStep.CourseManeuver,
                GuidedBossBattleStep.CoursePrepareEnd, GuidedBossBattleStep.CourseRangeAttempt,
                GuidedBossBattleStep.CourseApproach2, GuidedBossBattleStep.CourseDraw,
                GuidedBossBattleStep.CourseBoardContact, GuidedBossBattleStep.CourseRamContact,
                GuidedBossBattleStep.CourseAid, GuidedBossBattleStep.CourseAmbush,
                GuidedBossBattleStep.CourseBoardingEnd4, GuidedBossBattleStep.CourseFinal })
                Assert.That(seen, Does.Contain("BattleStep:" + step), "The real UI route must execute " + step);
        }

        private static IEnumerator EnterGuidedCardCourse()
        {
            yield return LoadGuidedTutorialScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "TutorialBoss", new HashSet<string>(), false);
            ClickAcceptanceButton("Encounter Choice 0"); yield return null;
            yield return WaitForGuidedCaptureScreenReveal(controller);
            Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseResources));
        }

        private static IEnumerator DriveGuidedCardCourseTo(CoreGameplayDemoController controller,
            GuidedBossBattleStep target, bool alternateHeadings = false)
        {
            int actions = 0;
            while (controller.Session.IsGuidedBossBattle && controller.Session.GuidedBossStep != target && actions++ < 90)
                yield return PlayGuidedScriptAction(controller, false, null, alternateHeadings);
            Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(target),
                "Actual card, movement and lesson controls must reach the requested boundary.");
        }

        // Independent course contract: the driver does not accept an arbitrary
        // action merely because the production state machine requests it.
        private static GuidedBossActionKind GuidedBossKindForStep(GuidedBossBattleStep step)
        {
            switch (step)
            {
                case GuidedBossBattleStep.CourseResources:
                case GuidedBossBattleStep.CourseCardInspect:
                case GuidedBossBattleStep.CourseEnemyInspect:
                case GuidedBossBattleStep.CourseDeckInspect:
                case GuidedBossBattleStep.CourseEffectsInspect: return GuidedBossActionKind.Inspect;
                case GuidedBossBattleStep.CourseFreeMove:
                case GuidedBossBattleStep.CourseSecondMove:
                case GuidedBossBattleStep.CourseManeuver:
                case GuidedBossBattleStep.CourseGunneryMove:
                case GuidedBossBattleStep.CourseRamMove: return GuidedBossActionKind.Movement;
                case GuidedBossBattleStep.CourseBasicsDone:
                case GuidedBossBattleStep.CourseManeuverDone:
                case GuidedBossBattleStep.CourseGunneryDone:
                case GuidedBossBattleStep.CourseBoardDone:
                case GuidedBossBattleStep.CourseRamDone:
                case GuidedBossBattleStep.CourseBoardingDone:
                case GuidedBossBattleStep.CourseRetry: return GuidedBossActionKind.Continue;
                case GuidedBossBattleStep.CoursePrepareEnd:
                case GuidedBossBattleStep.CourseEnemyTurn:
                case GuidedBossBattleStep.CourseBoardingEnd1:
                case GuidedBossBattleStep.CourseBoardingEnd2:
                case GuidedBossBattleStep.CourseBoardingEnd3:
                case GuidedBossBattleStep.CourseBoardingEnd4: return GuidedBossActionKind.EndTurn;
                case GuidedBossBattleStep.CourseRangeAttempt: return GuidedBossActionKind.BlockedCard;
                case GuidedBossBattleStep.CourseBoardContact:
                case GuidedBossBattleStep.CourseRamContact:
                case GuidedBossBattleStep.CourseFinal: return GuidedBossActionKind.FreePlay;
                default:
                    Assert.That(GuidedBossCardForStep(step), Is.Not.Empty, "Unexpected step " + step);
                    return GuidedBossActionKind.Card;
            }
        }

        private static string GuidedBossCardForStep(GuidedBossBattleStep step)
        {
            switch (step)
            {
                case GuidedBossBattleStep.CourseCardInspect:
                case GuidedBossBattleStep.CourseActionCard:
                case GuidedBossBattleStep.CourseApproach1:
                case GuidedBossBattleStep.CourseApproach2:
                case GuidedBossBattleStep.CourseRamAction: return "card_approach";
                case GuidedBossBattleStep.CourseLoad: return "card_load_shot";
                case GuidedBossBattleStep.CoursePrepareGuard:
                case GuidedBossBattleStep.CourseNavalGuard: return "card_brace";
                case GuidedBossBattleStep.CourseRangeAttempt:
                case GuidedBossBattleStep.CourseFire: return "card_chain_shot";
                case GuidedBossBattleStep.CourseEnergy: return "card_energy_order";
                case GuidedBossBattleStep.CourseRepair: return "card_emergency_repair";
                case GuidedBossBattleStep.CourseDraw:
                case GuidedBossBattleStep.CourseBoardingDraw: return "card_tactical_draw";
                case GuidedBossBattleStep.CourseBoardingAttack: return "card_crew_thrust";
                case GuidedBossBattleStep.CourseBoardingGuard: return "card_crew_guard";
                case GuidedBossBattleStep.CourseBandage: return "card_crew_bandage";
                case GuidedBossBattleStep.CourseAid: return "card_boarding_basic_07";
                case GuidedBossBattleStep.CourseAmbush: return "card_boarding_basic_13";
                default: return string.Empty;
            }
        }

        private static GuidedBossBattleStep GuidedCourseNext(GuidedBossBattleStep step) =>
            step == GuidedBossBattleStep.CourseResources ? GuidedBossBattleStep.CourseCardInspect :
            step == GuidedBossBattleStep.CourseCardInspect ? GuidedBossBattleStep.CourseFreeMove :
            step == GuidedBossBattleStep.CourseGunneryDone ? GuidedBossBattleStep.CourseBoardContact : step + 1;

        private static bool CaptureGuidedCourseStep(GuidedBossBattleStep step) =>
            step == GuidedBossBattleStep.CourseResources || step == GuidedBossBattleStep.CourseActionCard ||
            step == GuidedBossBattleStep.CourseManeuver || step == GuidedBossBattleStep.CourseBoardContact ||
            step == GuidedBossBattleStep.CourseRamContact || step == GuidedBossBattleStep.CourseAid ||
            step == GuidedBossBattleStep.CourseAmbush || step == GuidedBossBattleStep.CourseFinal;

        private static IEnumerator PlayGuidedScriptAction(CoreGameplayDemoController controller,
            bool capture, HashSet<string> captured, bool alternateHeadings = false)
        {
            yield return WaitGuidedBossAnimation(controller);
            var session = controller.Session;
            Assert.That(session.IsGuidedBossBattle, Is.True);
            GuidedBossBattleStep step = session.GuidedBossStep;
            GuidedBossActionKind kind = GuidedBossKindForStep(step);
            string cardId = GuidedBossCardForStep(step);
            Assert.That(session.GuidedBossExpectedAction, Is.EqualTo(kind), step.ToString());
            Assert.That(session.GuidedBossExpectedCardId, Is.EqualTo(cardId), step.ToString());
            Assert.That(session.GuidedBossLessonTitle, Is.Not.Empty);
            Assert.That(session.GuidedBossGoalText, Is.Not.Empty);
            Assert.That(session.GuidedTutorialBossDefeated, Is.False);
            Assert.That(session.VoyageTreasureFragments, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
            if (capture && captured != null && CaptureGuidedCourseStep(step) && captured.Add("course-" + step))
            {
                yield return WaitForGuidedCaptureScreenReveal(controller);
                Button hover = null;
                PointerEventData pointer = null;
                if (step == GuidedBossBattleStep.CourseActionCard)
                {
                    int slot = session.Battle.Hand.FindIndex(index => session.GetBattleCard(index).Id == cardId);
                    hover = RequireGuidedButton("Battle Card " + slot);
                    Canvas canvas = hover.GetComponentInParent<Canvas>().rootCanvas;
                    RectTransform rect = (RectTransform)hover.transform;
                    pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center)) };
                    ExecuteEvents.Execute(hover.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                    yield return new WaitForSecondsRealtime(.4f);
                }
                NavalBattleVisualCapture.Capture(controller, "GuidedBoss_" + step + ".png",
                    "完整卡牌课程真实操作：" + step, "Battle Card");
                if (hover != null)
                {
                    ExecuteEvents.Execute(hover.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                    yield return new WaitForSecondsRealtime(.4f);
                    Assert.That(session.GuidedBossStep, Is.EqualTo(step), "Reading a playable card must not play it.");
                }
            }
            if (kind == GuidedBossActionKind.Movement)
            { yield return PlayGuidedNavalMovement(controller, alternateHeadings, capture); yield break; }
            if (kind == GuidedBossActionKind.Inspect)
            { yield return InspectGuidedBattle(controller, step); yield break; }
            if (step == GuidedBossBattleStep.CourseFinal)
            { yield return PlayGuidedFinalBattleAction(controller); yield break; }
            Assert.That(step, Is.Not.EqualTo(GuidedBossBattleStep.CourseRetry),
                "The normal driver cannot conceal an unexpected loss by restarting.");
            var battle = session.Battle;
            int hand = cardId.Length == 0 ? -1 : battle.Hand.FindIndex(index => session.GetBattleCard(index).Id == cardId);
            string target = kind == GuidedBossActionKind.Continue ? "Guided Battle Continue" :
                kind == GuidedBossActionKind.EndTurn ? "End Battle Turn" :
                step == GuidedBossBattleStep.CourseBoardContact ? "Battle Board" :
                step == GuidedBossBattleStep.CourseRamContact ? "Battle Ram" : "Battle Card " + hand;
            Button button = RequireGuidedButton(target);
            Assert.That(button.IsInteractable(), Is.True, step + " must be physically clickable.");
            if (kind != GuidedBossActionKind.FreePlay) AssertGuidedFocusTarget(target, step.ToString());
            if (kind != GuidedBossActionKind.FreePlay) AssertGuidedCourseLocks(controller, button, cardId);
            var before = new GuidedCourseState(session, hand);
            var oldClick = button.onClick;
            if (kind == GuidedBossActionKind.BlockedCard)
            {
                Assert.That(session.CanPlayCard(hand), Is.False);
                Assert.That(CoreVoyageSession.NavalGridDistance(battle.PlayerPositionMeters, battle.EnemyPositionMeters),
                    Is.GreaterThan(4), "The blocked exercise is outside the four-cell Manhattan cannon range.");
                Assert.That(session.CardUnavailableReason(hand), Is.EqualTo(CoreVoyageSession.NavalCannonOutOfRangeReason));
                string inventory = GuidedBossFingerprint(session, false);
                ClickAcceptanceButton(target);
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(GuidedBossFingerprint(session, false), Is.EqualTo(inventory));
            }
            else
            {
                ClickAcceptanceButton(target);
                if (kind != GuidedBossActionKind.Continue)
                    Assert.That(controller.IsBattleAnimationPlaying, Is.True, "Normal presentation for " + step);
            }
            string committed = GuidedBossFingerprint(session);
            oldClick.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed), "Immediate callback cannot commit twice.");
            yield return WaitGuidedBossAnimation(controller);
            AssertGuidedCourseResult(session, battle, before, step, kind);
            committed = GuidedBossFingerprint(session);
            oldClick.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed), "Retired callback cannot submit next lesson.");
        }

        private static void AssertGuidedCourseLocks(CoreGameplayDemoController controller, Button target, string allowedCard)
        {
            string unchanged = GuidedBossFingerprint(controller.Session);
            foreach (Button other in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(control => IsGuidedBattleActionName(control.name)))
            {
                if (other == target || other.name == "Emergency Battle Return") continue;
                if (other.name.StartsWith("Battle Card ", StringComparison.Ordinal) &&
                    int.TryParse(other.name.Substring("Battle Card ".Length), out int hand) &&
                    hand < controller.Session.Battle.Hand.Count &&
                    controller.Session.GetBattleCard(controller.Session.Battle.Hand[hand]).Id == allowedCard) continue;
                Assert.That(other.IsInteractable(), Is.False, "Lesson restriction: " + other.name);
                other.onClick.Invoke();
                Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(unchanged), "Disabled callback " + other.name);
            }
        }

        private static IEnumerator InspectGuidedBattle(CoreGameplayDemoController controller, GuidedBossBattleStep step)
        {
            var session = controller.Session;
            string topic = step == GuidedBossBattleStep.CourseEnemyInspect ? "enemy" :
                step == GuidedBossBattleStep.CourseDeckInspect ? "deck" :
                step == GuidedBossBattleStep.CourseCardInspect ? "card" : "resources";
            Assert.That(session.GuidedBossInspectionTopic, Is.EqualTo(topic));
            string inventory = GuidedBossFingerprint(session, false);
            GameObject retainedRoot = GameObject.Find("Battle Card"), retainedHand = GameObject.Find("Battle Hand");
            int[] cardInstances = retainedHand.GetComponentsInChildren<Button>().Select(button => button.GetInstanceID()).ToArray();
            if (topic == "card")
            {
                int hand = session.Battle.Hand.FindIndex(i => session.GetBattleCard(i).Id == "card_approach");
                Button card = RequireGuidedButton("Battle Card " + hand);
                Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    ((RectTransform)card.transform).TransformPoint(((RectTransform)card.transform).rect.center)) };
                AssertGuidedBattleTopRaycastButton(pointer.position, card, "Read-card lesson pointer");
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.8f);
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.35f);
            }
            else
            {
                string target = topic == "deck" ? "Battle Deck" : topic == "enemy"
                    ? "Guided Battle Inspect Enemy" : "Guided Battle Inspect Resources";
                Assert.That(RequireGuidedButton(target).IsInteractable(), Is.True);
                AssertGuidedFocusTarget(target);
                var oldClick = RequireGuidedButton(target).onClick;
                ClickAcceptanceButton(target); yield return null;
                Assert.That(GameObject.Find("Battle Deck Overlay"), Is.Not.Null);
                if (topic != "deck")
                {
                    Text detail = GameObject.Find("Guided Battle Inspection Details").GetComponent<Text>();
                    Assert.That(detail.text, Does.Contain(topic == "enemy" ? session.Battle.EnemyName : "能量 " + session.Battle.Energy));
                    Assert.That(detail.text, Does.Contain(topic == "enemy"
                        ? "船体 " + session.Battle.EnemyHull : "行动点 " + session.Battle.NavalActionPoints));
                    if (topic == "resources")
                    {
                        Assert.That(detail.text, Does.Contain("火药 " + session.Cargo.Powder));
                        Assert.That(detail.text, Does.Contain("药品 " + session.Cargo.Medicine));
                        Assert.That(detail.text, Does.Contain("抽牌堆 " + session.Battle.DrawPile.Count));
                        if (step == GuidedBossBattleStep.CourseEffectsInspect)
                        {
                            Assert.That(session.Battle.Boarding.ContinuousRecovery, Is.EqualTo(4));
                            Assert.That(detail.text, Does.Contain("持续恢复：4 层")
                                .And.Contain("下次己方回合结束恢复 4 点船员生命")
                                .And.Contain("然后减少 1 层")
                                .And.Contain("之后按剩余层数继续恢复"));
                            Assert.That(detail.text, Does.Not.Contain("次，每次恢复"));
                            Canvas.ForceUpdateCanvases();
                            Assert.That(detail.fontSize, Is.GreaterThanOrEqualTo(24));
                            Assert.That(detail.preferredHeight, Is.LessThanOrEqualTo(detail.rectTransform.rect.height + .5f),
                                "The recovery layers and decay explanation must fit the reading surface.");
                        }
                    }
                }
                ClickAcceptanceButton("Close Battle Deck"); yield return null;
                Assert.That(GameObject.Find("Battle Deck Overlay"), Is.Null);
                string committed = GuidedBossFingerprint(session);
                oldClick.Invoke();
                Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed));
                if (ActiveButton("Close Battle Deck") != null) ClickAcceptanceButton("Close Battle Deck");
            }
            Assert.That(GuidedBossFingerprint(session, false), Is.EqualTo(inventory), "Inspection cannot spend or play.");
            Assert.That(GameObject.Find("Battle Card"), Is.SameAs(retainedRoot));
            Assert.That(GameObject.Find("Battle Hand"), Is.SameAs(retainedHand));
            CollectionAssert.AreEqual(cardInstances, retainedHand.GetComponentsInChildren<Button>()
                .Select(button => button.GetInstanceID()).ToArray(), "Inspection retains the real hand instances.");
            Assert.That(session.GuidedBossStep, Is.EqualTo(GuidedCourseNext(step)));
            yield return null;
        }

        private static IEnumerator PlayGuidedNavalMovement(CoreGameplayDemoController controller,
            bool alternateHeadings, bool capture = false)
        {
            var session = controller.Session;
            GuidedBossBattleStep step = session.GuidedBossStep;
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            int ap = session.Battle.NavalActionPoints;
            var route = new List<VoyagePoint> { CourseForward(start, session.Battle.PlayerHeadingDegrees) };
            if (step == GuidedBossBattleStep.CourseManeuver)
                route.Add(new VoyagePoint(route[0].X, route[0].Y + (alternateHeadings ? -100f : 100f)));
            if (step == GuidedBossBattleStep.CourseGunneryMove)
            {
                route.Add(new VoyagePoint(start.X + 200f, start.Y));
                route.Add(new VoyagePoint(start.X + 300f, start.Y));
            }
            float arrival = step == GuidedBossBattleStep.CourseManeuver ? (alternateHeadings ? 180f : 0f) :
                alternateHeadings && step == GuidedBossBattleStep.CourseFreeMove ? 90f :
                alternateHeadings && step == GuidedBossBattleStep.CourseSecondMove ? 180f :
                alternateHeadings && step == GuidedBossBattleStep.CourseGunneryMove ? 270f : 0f;
            yield return ClickGuidedCourseRoute(controller, route, arrival, capture && step == GuidedBossBattleStep.CourseManeuver);
            Assert.That(session.GuidedBossStep, Is.EqualTo(GuidedCourseNext(step)));
            if (step == GuidedBossBattleStep.CourseManeuver)
                Assert.That(ap, Is.GreaterThan(route.Count), "Short route still spends the complete prepared AP pool.");
            if (step == GuidedBossBattleStep.CourseGunneryMove)
                Assert.That(CoreVoyageSession.NavalGridDistance(session.Battle.PlayerPositionMeters,
                    session.Battle.EnemyPositionMeters), Is.LessThanOrEqualTo(4));
            if (step == GuidedBossBattleStep.CourseRamMove)
                Assert.That(session.Battle.DistanceMeters, Is.EqualTo(100f).Within(.01f));
        }

        private static VoyagePoint CourseForward(VoyagePoint start, float heading) =>
            new VoyagePoint(start.X + Mathf.Round(Mathf.Cos(heading * Mathf.Deg2Rad)) * 100f,
                start.Y + Mathf.Round(Mathf.Sin(heading * Mathf.Deg2Rad)) * 100f);

        private static IEnumerator ClickGuidedCourseRoute(CoreGameplayDemoController controller,
            IList<VoyagePoint> route, float arrival, bool capture = false)
        {
            var session = controller.Session;
            GuidedBossBattleStep step = session.GuidedBossStep;
            VoyagePoint start = session.Battle.PlayerPositionMeters;
            int ap = session.Battle.NavalActionPoints, energy = session.Battle.Energy;
            Assert.That(route.Count, Is.LessThanOrEqualTo(ap));
            var input = Object.FindFirstObjectByType<NavalTacticalMovementInput>();
            Assert.That(input, Is.Not.Null);
            yield return DragTacticalRoute(controller, route);
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, start);
            Assert.That(session.Battle.NavalActionPoints, Is.EqualTo(ap));
            if (capture) NavalBattleVisualCapture.Capture(controller, "GuidedBoss_Maneuver_SelectedRoute.png",
                "从己船连续拖出两格转弯路线，松开预览后确认航行并自主选择终向", "Naval Tactical Board");
            var oldCommit = RequireGuidedButton("Commit Naval Movement").onClick;
            ClickAcceptanceButton("Commit Naval Movement");
            Assert.That(controller.IsBattleAnimationPlaying, Is.True);
            Assert.That(session.Battle.NavalActionPoints, Is.Zero);
            string committed = GuidedBossFingerprint(session);
            oldCommit.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed));
            yield return WaitGuidedBossAnimation(controller);
            Assert.That(session.GuidedBossStep, Is.EqualTo(step), "Travel waits for arrival heading.");
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            AssertTacticalPoint(session.Battle.PlayerPositionMeters, route[route.Count - 1]);
            Assert.That(RequireGuidedButton("End Battle Turn").IsInteractable(), Is.False);
            var headings = session.GetNavalArrivalHeadings();
            Assert.That(headings.Count, Is.EqualTo(3), "All three legal arrival headings remain available.");
            CollectionAssert.Contains(headings, arrival);
            for (int index = 0; index < headings.Count; index++)
                Assert.That(RequireGuidedButton("Naval Arrival Heading " + index).IsInteractable(), Is.True);
            string headingTarget = "Naval Arrival Heading " + headings.ToList().IndexOf(arrival);
            yield return WaitTacticalHeadingPointerReady(headingTarget);
            ClickAcceptanceButton(headingTarget);
            yield return WaitGuidedBossAnimation(controller);
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.False);
            Assert.That(session.Battle.PlayerHeadingDegrees, Is.EqualTo(arrival).Within(.01f));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
        }

        private sealed class GuidedCourseState
        {
            internal readonly int Hull, EnemyHull, PlayerHp, EnemyHp, Energy, Ap, Played, Hand, Powder, Balls, Medicine, Wood, Guard, Turn;
            internal readonly int Cost, Recovery, Shuffles, ContinuousRecovery, Delays, EnemyGuard, Discard, AttackQuote, CriticalChance;
            internal GuidedCourseState(CoreVoyageSession session, int hand)
            {
                var b = session.Battle;
                Hull = session.Hull; EnemyHull = b.EnemyHull; PlayerHp = b.PlayerBoardingHealth; EnemyHp = b.EnemyBoardingHealth;
                Energy = b.Energy; Ap = b.NavalActionPoints; Played = b.CardsPlayedThisTurn; Hand = b.Hand.Count;
                Guard = b.PlayerBlock; Turn = b.Turn; Powder = session.Cargo.Powder; Balls = session.Cargo.Cannonballs;
                Medicine = session.Cargo.Medicine; Wood = session.Cargo.Wood; Shuffles = session.GuidedBossDeckShuffles;
                ContinuousRecovery = b.Boarding.ContinuousRecovery; Delays = b.Boarding.DelayedDamage.Count;
                EnemyGuard = b.EnemyBlock; Discard = b.DiscardPile.Count; CriticalChance = session.BoardingCriticalChancePercent;
                if (hand < 0) return;
                CoreBattleCard card = session.GetBattleCard(b.Hand[hand]);
                Cost = b.Stage == NavalBattleStage.Boarding && card.BoardingSpec != null
                    ? session.GetBoardingHandEnergyCost(hand) : session.GetBattleCardEnergyCost(b.Hand[hand]);
                Recovery = session.GetBattleCardEnergyRecovery(b.Hand[hand]);
                AttackQuote = session.GetBattleCardDamage(card, b.Stage);
            }
        }

        private static void AssertGuidedCourseResult(CoreVoyageSession session, CoreBattleState battle,
            GuidedCourseState before, GuidedBossBattleStep step, GuidedBossActionKind kind)
        {
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle), "Exercises cannot pay victory rewards.");
            Assert.That(session.GuidedBossStep, Is.EqualTo(GuidedCourseNext(step)), "Exactly one course objective advances.");
            if (kind == GuidedBossActionKind.Continue) return;
            if (kind == GuidedBossActionKind.Card)
            {
                Assert.That(battle.Energy, Is.EqualTo(before.Energy - before.Cost + before.Recovery));
                Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(before.Played + 1));
            }
            switch (step)
            {
                case GuidedBossBattleStep.CourseActionCard:
                case GuidedBossBattleStep.CourseApproach1:
                case GuidedBossBattleStep.CourseApproach2:
                case GuidedBossBattleStep.CourseRamAction:
                    Assert.That(battle.NavalActionPoints, Is.EqualTo(before.Ap + 1));
                    Assert.That(before.Cost, Is.EqualTo(step == GuidedBossBattleStep.CourseApproach2 ? 1 : 0)); break;
                case GuidedBossBattleStep.CourseLoad:
                    Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
                    Assert.That(battle.PlayerLoadedShot, Is.EqualTo(5));
                    Assert.That(session.Cargo.Powder, Is.EqualTo(before.Powder - 1));
                    Assert.That(battle.Hand.Count, Is.EqualTo(before.Hand)); break;
                case GuidedBossBattleStep.CoursePrepareGuard:
                case GuidedBossBattleStep.CourseNavalGuard:
                    Assert.That(before.Cost, Is.Zero);
                    Assert.That(battle.PlayerBlock, Is.EqualTo(before.Guard + 2)); break;
                case GuidedBossBattleStep.CoursePrepareEnd:
                    Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
                    Assert.That(session.Hull, Is.EqualTo(before.Hull));
                    Assert.That(battle.PlayerLoadedShot, Is.EqualTo(5));
                    Assert.That(battle.PlayerBlock, Is.EqualTo(before.Guard)); break;
                case GuidedBossBattleStep.CourseEnergy:
                    Assert.That(before.Cost, Is.Zero);
                    Assert.That(battle.Energy, Is.EqualTo(before.Energy + 1));
                    Assert.That(battle.NavalActionPoints, Is.EqualTo(before.Ap)); break;
                case GuidedBossBattleStep.CourseFire:
                    Assert.That(battle.EnemyHull, Is.LessThan(before.EnemyHull));
                    Assert.That(battle.PlayerLoadedShot, Is.Zero);
                    Assert.That(session.Cargo.Powder, Is.EqualTo(before.Powder - 1));
                    Assert.That(session.Cargo.Cannonballs, Is.EqualTo(before.Balls - 1)); break;
                case GuidedBossBattleStep.CourseRepair:
                    Assert.That(session.Hull, Is.EqualTo(Math.Min(session.MaxHull, before.Hull + 5)));
                    Assert.That(session.Cargo.Wood, Is.EqualTo(before.Wood - 1)); break;
                case GuidedBossBattleStep.CourseDraw:
                    Assert.That(session.GuidedBossDeckShuffles, Is.GreaterThan(before.Shuffles));
                    Assert.That(battle.Hand.Count, Is.EqualTo(before.Hand + 1)); break;
                case GuidedBossBattleStep.CourseBoardContact:
                case GuidedBossBattleStep.CourseRamContact:
                    Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                    Assert.That(before.Ap, Is.GreaterThanOrEqualTo(1));
                    Assert.That(battle.NavalActionPoints, Is.Zero, "The new boarding phase clears naval AP.");
                    Assert.That(battle.Energy, Is.EqualTo(3), "Entering the boarding phase restores energy after contact.");
                    if (step == GuidedBossBattleStep.CourseRamContact)
                    { Assert.That(session.Hull, Is.LessThan(before.Hull)); Assert.That(battle.EnemyHull, Is.LessThan(before.EnemyHull)); }
                    else Assert.That(session.Hull, Is.EqualTo(before.Hull)); break;
                case GuidedBossBattleStep.CourseBoardingAttack:
                    Assert.That(battle.Boarding.Initialized, Is.True);
                    // This is a formal attack: current morale may double the one
                    // quoted hit. Never force a non-critical tutorial-only result.
                    Assert.That(before.AttackQuote, Is.GreaterThan(0));
                    var legalHits = new List<int>();
                    if (before.CriticalChance < 100) legalHits.Add(before.AttackQuote);
                    if (before.CriticalChance > 0) legalHits.Add(before.AttackQuote * 2);
                    Assert.That(legalHits.Any(hit =>
                        battle.EnemyBoardingHealth == before.EnemyHp - Math.Min(before.EnemyHp, Math.Max(0, hit - before.EnemyGuard)) &&
                        battle.EnemyBlock == Math.Max(0, before.EnemyGuard - hit)), Is.True,
                        "One quoted attack must resolve with the actual guard and morale critical chance (" + before.CriticalChance + "%).");
                    Assert.That(battle.Hand.Count, Is.EqualTo(before.Hand - 1));
                    Assert.That(battle.DiscardPile.Count, Is.EqualTo(before.Discard + 1));
                    Assert.That(session.Cargo.Powder, Is.EqualTo(before.Powder));
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(before.Medicine));
                    Assert.That(battle.LastAction, Does.Contain("伤害 " + (before.EnemyHp - battle.EnemyBoardingHealth)));
                    break;
                case GuidedBossBattleStep.CourseBoardingGuard:
                    Assert.That(battle.PlayerBlock, Is.EqualTo(before.Guard + 4)); break;
                case GuidedBossBattleStep.CourseBandage:
                    Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(Math.Min(battle.PlayerMaxBoardingHealth, before.PlayerHp + 6)));
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(before.Medicine - 1)); break;
                case GuidedBossBattleStep.CourseBoardingDraw:
                    Assert.That(before.Cost, Is.EqualTo(2));
                    Assert.That(battle.Hand.Count, Is.EqualTo(before.Hand + 2)); break;
                case GuidedBossBattleStep.CourseAid:
                    Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(before.PlayerHp));
                    Assert.That(battle.Boarding.ContinuousRecovery, Is.EqualTo(before.ContinuousRecovery + 5));
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(before.Medicine - 1)); break;
                case GuidedBossBattleStep.CourseAmbush:
                    Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(before.EnemyHp));
                    Assert.That(battle.Boarding.DelayedDamage.Count, Is.EqualTo(before.Delays + 1)); break;
                case GuidedBossBattleStep.CourseBoardingEnd3:
                    Assert.That(battle.Boarding.DelayedDamage, Is.Empty);
                    Assert.That(battle.EnemyBoardingHealth, Is.LessThan(before.EnemyHp));
                    Assert.That(before.ContinuousRecovery, Is.EqualTo(5));
                    AssertGuidedRecoveryEnd(battle, before);
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(before.Medicine)); break;
                case GuidedBossBattleStep.CourseBoardingEnd4:
                    Assert.That(before.ContinuousRecovery, Is.EqualTo(4));
                    AssertGuidedRecoveryEnd(battle, before);
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(before.Medicine)); break;
            }
            if (kind == GuidedBossActionKind.EndTurn)
            {
                Assert.That(battle.Turn, Is.EqualTo(before.Turn + 1));
                Assert.That(battle.Energy, Is.EqualTo(3));
                Assert.That(battle.CardsPlayedThisTurn, Is.Zero);
            }
        }

        private static void AssertGuidedRecoveryEnd(CoreBattleState battle, GuidedCourseState before)
        {
            Assert.That(battle.Boarding.ContinuousRecovery, Is.EqualTo(before.ContinuousRecovery - 1));
            int healedHealth = Math.Min(battle.PlayerMaxBoardingHealth, before.PlayerHp + before.ContinuousRecovery);
            if (battle.BoardingEnemy.LastAttackExecuted)
                Assert.That(battle.BoardingEnemy.LastAttackPlayerHealth, Is.EqualTo(healedHealth),
                    "The current recovery layers heal before the enemy acts.");
            Assert.That(battle.PlayerBoardingHealth,
                Is.EqualTo(Math.Max(0, healedHealth - battle.BoardingEnemy.LastAttackHealthLost)),
                "Each tick heals its current layer count, capped at maximum health, before real enemy damage.");
        }

        private static IEnumerator PlayGuidedFinalBattleAction(CoreGameplayDemoController controller)
        {
            var session = controller.Session;
            var battle = session.Battle;
            Assert.That(session.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.FinalBattle));
            Assert.That(battle.Turn, Is.LessThan(30), "Free combat must win without injected progress.");
            Assert.That(RequireGuidedButton("End Battle Turn").IsInteractable(), Is.True);
            if (session.CanBoard(out _)) ClickAcceptanceButton("Battle Board");
            else if (battle.Stage == NavalBattleStage.Naval && !session.NavalCannonTargetInRange && battle.NavalActionPoints > 0)
            {
                VoyagePoint forward = CourseForward(battle.PlayerPositionMeters, battle.PlayerHeadingDegrees);
                if (session.GetNavalLegalNextSteps().Any(p => Math.Abs(p.X - forward.X) < .01f && Math.Abs(p.Y - forward.Y) < .01f))
                { yield return ClickGuidedCourseRoute(controller, new[] { forward }, battle.PlayerHeadingDegrees); yield break; }
                ClickAcceptanceButton("End Battle Turn");
            }
            else
            {
                int selected = -1; float best = float.NegativeInfinity;
                for (int hand = 0; hand < battle.Hand.Count; hand++)
                {
                    if (!session.CanPlayCard(hand)) continue;
                    Assert.That(RequireGuidedButton("Battle Card " + hand).IsInteractable(), Is.True, "Every legal free-play card unlocks.");
                    CoreBattleCard card = session.GetBattleCard(battle.Hand[hand]);
                    float score = card.Damage * 3f + card.Draw * 3f + card.Block +
                        Math.Min(session.MaxHull - session.Hull, card.Repair);
                    if (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad) score += 12f;
                    if (card.NavalActionPointGain > 0) score += !session.NavalCannonTargetInRange ? 30f : -20f;
                    if (score > best) { best = score; selected = hand; }
                }
                ClickAcceptanceButton(selected >= 0 && best >= 0f ? "Battle Card " + selected : "End Battle Turn");
            }
            yield return WaitGuidedBossAnimation(controller);
            if (session.Phase != CoreDemoPhase.Battle)
            {
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(session.GuidedTutorialBossDefeated, Is.True);
                Assert.That(session.VoyageTreasureFragments, Is.EqualTo(1));
                Assert.That(session.LastBattleRetreated, Is.False);
            }
        }

        private static IEnumerator WaitGuidedBossAnimation(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            { AdvanceRamComicIfWaiting(controller); yield return null; }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "The real battle animation must finish.");
            yield return null;
        }

        private static IEnumerator WaitForGuidedCaptureScreenReveal(CoreGameplayDemoController controller)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo routine = typeof(CoreGameplayDemoController).GetField("transitionRoutine", flags);
            FieldInfo groupField = typeof(CoreGameplayDemoController).GetField("transitionCanvasGroup", flags);
            Assert.That(routine, Is.Not.Null); Assert.That(groupField, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (routine.GetValue(controller) != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(routine.GetValue(controller), Is.Null);
            var group = (CanvasGroup)groupField.GetValue(controller);
            Assert.That(group, Is.Not.Null); Assert.That(group.alpha, Is.EqualTo(1f));
            yield return null;
        }

        private static bool IsGuidedBattleActionName(string name) => name.StartsWith("Battle Card ", StringComparison.Ordinal) ||
            name == "End Battle Turn" || name == "Battle Ram" || name == "Battle Board" || name == "Retreat Battle" || name == "Emergency Battle Return";

        private static string GuidedBossFingerprint(CoreVoyageSession session, bool includeStep = true)
        {
            var b = session.Battle;
            return (includeStep ? session.GuidedBossStep.ToString() : string.Empty) + "|" + session.Phase + "|" + session.Hull + "|" +
                session.Cargo.Powder + ":" + session.Cargo.Cannonballs + ":" + session.Cargo.Medicine + ":" + session.Cargo.Wood + ":" +
                session.Cargo.Water + ":" + session.Cargo.Food + ":" + session.Cargo.Gold + ":" + session.TotalHours + ":" + session.Morale + "|" +
                (b == null ? "null" : b.Stage + ":" + b.Turn + ":" + b.PhaseTurn + ":" + b.DistanceMeters + ":" + b.Energy + ":" +
                b.NavalActionPoints + ":" + b.NavalArrivalActionPoints + ":" + b.NavalAwaitingHeadingSelection + ":" + b.PlayerPositionMeters.X + ":" + b.PlayerPositionMeters.Y + ":" +
                b.PlayerHeadingDegrees + ":" + b.EnemyPositionMeters.X + ":" + b.EnemyPositionMeters.Y + ":" + b.EnemyHeadingDegrees + ":" +
                b.EnemyHull + ":" + b.PlayerBlock + ":" + b.EnemyBlock + ":" + b.PlayerBoardingHealth + ":" + b.EnemyBoardingHealth + ":" +
                b.PlayerLoadedShot + ":" + b.CardsPlayedThisTurn + ":" + b.Boarding.HealingTicks.Count + ":" + b.Boarding.ContinuousRecovery + ":" + b.Boarding.DelayedDamage.Count + "|" +
                string.Join(",", b.Hand) + "|" + string.Join(",", b.DrawPile) + "|" + string.Join(",", b.DiscardPile));
        }
    }
}
