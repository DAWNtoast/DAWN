using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator GuidedBossScript_PauseDuringRealRamResumesCommittedLessonWithoutReplay()
        {
            using (var storage = new VoyageCargoStorageScope())
            {
                yield return LoadGuidedTutorialScene();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                yield return DriveGuidedTutorial(controller, "TutorialBoss", new HashSet<string>(), false);
                ClickAcceptanceButton("Encounter Choice 0");
                yield return null;
                int actions = 0;
                while (controller.Session.GuidedBossStep != GuidedBossBattleStep.NavalRam && actions++ < 16)
                    yield return PlayGuidedScriptAction(controller, false, null);
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.NavalRam));
                AssertGuidedBossHandAndTurn(controller.Session);
                Assert.That(controller.Session.CanCaptureVoyageCheckpoint, Is.False,
                    "The scripted battle retains the ordinary no-mid-battle-save boundary.");
                int hull = controller.Session.Hull;
                var oldClick = RequireGuidedButton("Battle Ram").onClick;
                ClickAcceptanceButton("Battle Ram");
                Assert.That(controller.Session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(controller.Session.Hull, Is.EqualTo(hull - 9));
                Assert.That(controller.Session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.BoardingAttack1));
                string committed = GuidedBossFingerprint(controller.Session);
                yield return null;
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Not.Null);
                MethodInfo pause = typeof(CoreGameplayDemoController).GetMethod("OnApplicationPause",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pause, Is.Not.Null);
                pause.Invoke(controller, new object[] { true });
                yield return null;
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null);
                Assert.That(GameObject.Find("Boarding Scene Transition"), Is.Null);
                Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(committed));
                pause.Invoke(controller, new object[] { false });
                yield return null;
                yield return null;
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
                Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(committed));
                Assert.DoesNotThrow(() => oldClick.Invoke());
                Assert.That(GuidedBossFingerprint(controller.Session), Is.EqualTo(committed));
                Assert.That(GameObject.Find("Battle Ram Comic Overlay"), Is.Null, "Resume must not replay the already committed ram.");
                Assert.That(RequireGuidedButton("Battle Card 0").IsInteractable(), Is.True);
                yield return DriveGuidedTutorial(controller, "Warehouse", new HashSet<string>(), false);
                Assert.That(controller.Session.TreasureFragments, Is.EqualTo(1));
                Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        // Shared by the complete, unmodified-new-profile UI route and the explicit
        // recovery boundary. Expectations are fixed test data, not a loop which
        // simply accepts whatever action the production state machine requests.
        private static IEnumerator PlayGuidedScriptAction(CoreGameplayDemoController controller,
            bool capture, HashSet<string> captured)
        {
            CoreVoyageSession session = controller.Session;
            Assert.That(session.IsGuidedBossBattle, Is.True);
            CoreBattleState battle = session.Battle;
            GuidedBossBattleStep step = session.GuidedBossStep;
            string expectedCard = GuidedBossCardForStep(step);
            GuidedBossActionKind action = GuidedBossKindForStep(step);
            Assert.That(session.GuidedBossExpectedCardId, Is.EqualTo(expectedCard), step.ToString());
            Assert.That(session.GuidedBossExpectedAction, Is.EqualTo(action), step.ToString());
            AssertGuidedBossHandAndTurn(session);
            string target = action == GuidedBossActionKind.EndTurn ? "End Battle Turn" :
                action == GuidedBossActionKind.Ram ? "Battle Ram" :
                "Battle Card " + battle.Hand.FindIndex(index => session.GetBattleCard(index).Id == expectedCard);
            Button button = RequireGuidedButton(target);
            Assert.That(button.IsInteractable(), Is.True, "The next lesson action must be physically clickable: " + step);
            AssertGuidedFocusTarget(target);
            string unchanged = GuidedBossFingerprint(session);
            var controls = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(control => IsGuidedBattleActionName(control.name)).ToArray();
            foreach (Button other in controls)
            {
                if (other == button || other.name == "Emergency Battle Return") continue;
                Assert.That(other.IsInteractable(), Is.False, "Only the instructed battle action is enabled: " + step + "/" + other.name);
                other.onClick.Invoke();
                Assert.That(GuidedBossFingerprint(session), Is.EqualTo(unchanged), "Disabled callbacks must not bypass the lesson: " + other.name);
            }
            if (capture && captured != null &&
                (step == GuidedBossBattleStep.NavalRetreat || step == GuidedBossBattleStep.NavalRangeAttempt ||
                 step == GuidedBossBattleStep.NavalLoadAttempt || step == GuidedBossBattleStep.NavalRam ||
                 step == GuidedBossBattleStep.BoardingGuard1 || step == GuidedBossBattleStep.BoardingHeal) &&
                captured.Add("boss-" + step))
            {
                yield return WaitForGuidedCaptureScreenReveal(controller);
                NavalBattleVisualCapture.Capture(controller, "GuidedBoss_" + step + ".png",
                    "真实教学动作：" + step, "Battle Scene Art");
            }
            var before = new GuidedBossUiState(session, expectedCard);
            var oldClick = button.onClick;
            if (action == GuidedBossActionKind.BlockedCard)
            {
                int index = battle.Hand.FindIndex(card => session.GetBattleCard(card).Id == expectedCard);
                Assert.That(session.CanPlayCard(index), Is.False, "The ordinary rule still rejects the attempted card.");
                Assert.That(session.CardUnavailableReason(index), Is.EqualTo(step == GuidedBossBattleStep.NavalRangeAttempt
                    ? "炮击需要距离不超过 350 米" : "火药不足"));
                string inventory = GuidedBossFingerprint(session, false);
                ClickAcceptanceButton(target);
                Assert.That(controller.IsBattleAnimationPlaying, Is.False, "A failed observation must not play a successful-card animation.");
                Assert.That(GuidedBossFingerprint(session, false), Is.EqualTo(inventory),
                    "Failed attempts preserve the hand, resources, health, action points and card-use counter.");
                Assert.That(session.GuidedBossStep, Is.EqualTo(step + 1));
                string observed = GuidedBossFingerprint(session);
                oldClick.Invoke();
                Assert.That(GuidedBossFingerprint(session), Is.EqualTo(observed), "The old failed-attempt callback cannot observe twice.");
                yield return null;
                yield break;
            }
            ClickAcceptanceButton(target);
            Assert.That(controller.IsBattleAnimationPlaying, Is.True, "Successful lesson actions keep their normal presentation: " + step);
            string committed = GuidedBossFingerprint(session);
            oldClick.Invoke();
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed), "A second click during animation must not commit twice.");
            foreach (Button control in controls)
                if (control != null && control.isActiveAndEnabled)
                    Assert.That(control.IsInteractable(), Is.False, "Animation must lock battle inputs: " + control.name);
            yield return WaitGuidedBossAnimation(controller);
            AssertGuidedBossActionResult(session, battle, before, step, action);
            committed = GuidedBossFingerprint(session);
            Assert.DoesNotThrow(() => oldClick.Invoke());
            Assert.That(GuidedBossFingerprint(session), Is.EqualTo(committed), "A stale same-page callback cannot submit the next lesson step.");
        }

        private static IEnumerator WaitGuidedBossAnimation(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                AdvanceRamComicIfWaiting(controller);
                yield return null;
            }
            Assert.That(controller.IsBattleAnimationPlaying, Is.False, "The real battle animation must finish within the existing watchdog.");
            yield return null; // A newly created canvas needs one rendered frame before pointer raycasts.
        }

        private static IEnumerator WaitForGuidedCaptureScreenReveal(CoreGameplayDemoController controller)
        {
            // Screen changes reveal Phase Content over 0.24 unscaled seconds.
            // Observe its existing coroutine; never force alpha or finish gameplay.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo routine = typeof(CoreGameplayDemoController).GetField("transitionRoutine", flags);
            FieldInfo groupField = typeof(CoreGameplayDemoController).GetField("transitionCanvasGroup", flags);
            Assert.That(routine, Is.Not.Null);
            Assert.That(groupField, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (routine.GetValue(controller) != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(routine.GetValue(controller), Is.Null, "The normal page reveal must complete before its screenshot.");
            var group = (CanvasGroup)groupField.GetValue(controller);
            Assert.That(group, Is.Not.Null);
            Assert.That(group.alpha, Is.EqualTo(1f), "Capture the fully revealed page, not a translucent transition frame.");
            yield return null;
        }

        private static bool IsGuidedBattleActionName(string name) => name.StartsWith("Battle Card ", StringComparison.Ordinal) ||
            name == "End Battle Turn" || name == "Battle Ram" || name == "Battle Board" || name == "Retreat Battle" || name == "Emergency Battle Return";

        private static GuidedBossActionKind GuidedBossKindForStep(GuidedBossBattleStep step)
        {
            switch (step)
            {
                case GuidedBossBattleStep.NavalRangeAttempt:
                case GuidedBossBattleStep.NavalLoadAttempt: return GuidedBossActionKind.BlockedCard;
                case GuidedBossBattleStep.NavalEndTurn1:
                case GuidedBossBattleStep.NavalEndTurn2:
                case GuidedBossBattleStep.BoardingEndTurn1:
                case GuidedBossBattleStep.BoardingEndTurn2: return GuidedBossActionKind.EndTurn;
                case GuidedBossBattleStep.NavalRam: return GuidedBossActionKind.Ram;
                default: return GuidedBossActionKind.Card;
            }
        }

        private static string GuidedBossCardForStep(GuidedBossBattleStep step)
        {
            switch (step)
            {
                case GuidedBossBattleStep.NavalRetreat: return "card_retreat";
                case GuidedBossBattleStep.NavalLoad:
                case GuidedBossBattleStep.NavalLoadAttempt: return "card_load_shot";
                case GuidedBossBattleStep.NavalGuard:
                case GuidedBossBattleStep.NavalGuard2: return "card_brace";
                case GuidedBossBattleStep.NavalRangeAttempt:
                case GuidedBossBattleStep.NavalFire: return "card_chain_shot";
                case GuidedBossBattleStep.NavalApproach2:
                case GuidedBossBattleStep.NavalApproach3: return "card_approach";
                case GuidedBossBattleStep.NavalCircle: return "card_circle_enemy";
                case GuidedBossBattleStep.BoardingAttack1:
                case GuidedBossBattleStep.BoardingAttack2:
                case GuidedBossBattleStep.BoardingAttack3: return "card_crew_thrust";
                case GuidedBossBattleStep.BoardingGuard1:
                case GuidedBossBattleStep.BoardingGuard2: return "card_crew_guard";
                case GuidedBossBattleStep.BoardingHeal: return "card_crew_bandage";
                default: return string.Empty;
            }
        }

        private static void AssertGuidedBossHandAndTurn(CoreVoyageSession session)
        {
            CoreBattleState battle = session.Battle;
            GuidedBossBattleStep step = session.GuidedBossStep;
            bool boarding = step >= GuidedBossBattleStep.BoardingAttack1;
            int turn = boarding ? step >= GuidedBossBattleStep.BoardingAttack3 ? 3 :
                step >= GuidedBossBattleStep.BoardingHeal ? 2 : 1 :
                step >= GuidedBossBattleStep.NavalLoadAttempt ? 3 : step >= GuidedBossBattleStep.NavalRangeAttempt ? 2 : 1;
            Assert.That(battle.Stage, Is.EqualTo(boarding ? NavalBattleStage.Boarding : NavalBattleStage.Naval));
            Assert.That(battle.PhaseTurn, Is.EqualTo(turn), "Exactly three naval turns and no more than three boarding turns.");
            string[] hand = null;
            switch (step)
            {
                case GuidedBossBattleStep.NavalRetreat:
                    hand = new[] { "card_retreat", "card_load_shot", "card_brace" };
                    Assert.That(battle.DistanceMeters, Is.EqualTo(400));
                    Assert.That(session.Cargo.Powder, Is.EqualTo(2));
                    Assert.That(session.Cargo.Cannonballs, Is.EqualTo(1));
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
                    break;
                case GuidedBossBattleStep.NavalRangeAttempt:
                    hand = new[] { "card_chain_shot", "card_approach", "card_brace" };
                    Assert.That(battle.DistanceMeters, Is.EqualTo(430));
                    Assert.That(battle.EnemyBlock, Is.EqualTo(3));
                    break;
                case GuidedBossBattleStep.NavalLoadAttempt:
                    hand = new[] { "card_load_shot", "card_circle_enemy", "card_approach" };
                    Assert.That(battle.DistanceMeters, Is.EqualTo(100));
                    Assert.That(session.Cargo.Powder, Is.Zero);
                    break;
                case GuidedBossBattleStep.NavalRam:
                    Assert.That(battle.DistanceMeters, Is.EqualTo(30));
                    Assert.That(battle.PlayerMovingTowardEnemy, Is.True);
                    Assert.That(battle.Alignment, Is.EqualTo(NavalBattleAlignment.EnemyBroadside));
                    Assert.That(battle.Energy, Is.EqualTo(1));
                    Assert.That(session.CanRam(out string ramReason), Is.True, ramReason);
                    break;
                case GuidedBossBattleStep.BoardingAttack1:
                    hand = new[] { "card_crew_thrust", "card_crew_guard" };
                    Assert.That(battle.PlayerOrdinaryCrew, Is.EqualTo(10));
                    Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(InitialBoardingHealth(battle.PlayerMaxBoardingHealth,
                        battle.NavalStartPlayerHull, session.Hull, session.MaxHull)));
                    Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(InitialBoardingHealth(battle.EnemyMaxBoardingHealth,
                        battle.NavalStartEnemyHull, battle.EnemyHull, battle.EnemyMaxHull)));
                    break;
                case GuidedBossBattleStep.BoardingHeal:
                    hand = new[] { "card_crew_bandage", "card_crew_thrust", "card_crew_guard" };
                    Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(InitialBoardingHealth(battle.PlayerMaxBoardingHealth,
                        battle.NavalStartPlayerHull, session.Hull, session.MaxHull) - 3));
                    Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(InitialBoardingHealth(battle.EnemyMaxBoardingHealth,
                        battle.NavalStartEnemyHull, battle.EnemyHull, battle.EnemyMaxHull) - 8));
                    break;
                case GuidedBossBattleStep.BoardingAttack3:
                    hand = new[] { "card_crew_thrust" };
                    Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(Math.Min(battle.PlayerMaxBoardingHealth,
                        InitialBoardingHealth(battle.PlayerMaxBoardingHealth, battle.NavalStartPlayerHull,
                            session.Hull, session.MaxHull) - 3 + 6)), "The fixed enemy attacks remain 7 and 3; the authored guard is 4 and medicine enables the full 6-point bandage.");
                    Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(Math.Max(0, InitialBoardingHealth(battle.EnemyMaxBoardingHealth,
                        battle.NavalStartEnemyHull, battle.EnemyHull, battle.EnemyMaxHull) - 16)));
                    break;
            }
            if (hand != null)
            {
                CollectionAssert.AreEqual(hand, battle.Hand.Select(index => session.GetBattleCard(index).Id).ToArray(),
                    "Each new lesson turn deals the authored hand through the real battle state.");
                Assert.That(battle.Energy, Is.EqualTo(3));
                Assert.That(battle.CardsPlayedThisTurn, Is.Zero);
            }
        }

        private sealed class GuidedBossUiState
        {
            internal readonly int Hull, EnemyHull, PlayerHp, EnemyHp, Energy, Played, Hand, Powder, Balls, Medicine, Guard, Turn;
            internal readonly int CardBlock;
            internal GuidedBossUiState(CoreVoyageSession session, string expectedCard)
            {
                CoreBattleState b = session.Battle;
                Hull = session.Hull; EnemyHull = b.EnemyHull; PlayerHp = b.PlayerBoardingHealth; EnemyHp = b.EnemyBoardingHealth;
                Energy = b.Energy; Played = b.CardsPlayedThisTurn; Hand = b.Hand.Count; Guard = b.PlayerBlock; Turn = b.Turn;
                Powder = session.Cargo.Powder; Balls = session.Cargo.Cannonballs; Medicine = session.Cargo.Medicine;
                CoreBattleCard card = b.Hand.Select(session.GetBattleCard).FirstOrDefault(item => item.Id == expectedCard);
                if (card != null)
                {
                    int block = session.GetBattleCardEffects(card, b.Stage).Block;
                    CardBlock = block + (block > 0 && b.Stage != NavalBattleStage.Boarding ? b.PlayerDeckBuff : 0);
                }
            }
        }

        private static void AssertGuidedBossActionResult(CoreVoyageSession session, CoreBattleState battle,
            GuidedBossUiState before, GuidedBossBattleStep step, GuidedBossActionKind action)
        {
            if (action == GuidedBossActionKind.Card)
            {
                int cost = step == GuidedBossBattleStep.NavalGuard || step == GuidedBossBattleStep.NavalGuard2 ? 0 : 1;
                Assert.That(battle.Energy, Is.EqualTo(before.Energy - cost));
                Assert.That(battle.CardsPlayedThisTurn, Is.EqualTo(before.Played + 1));
                Assert.That(battle.Hand.Count, Is.EqualTo(before.Hand - 1 + (step == GuidedBossBattleStep.NavalLoad ? 1 : 0)),
                    "The authored loading card draws one card after paying its powder.");
                if (session.Phase == CoreDemoPhase.Battle)
                {
                    Assert.That(session.Cargo.Powder, Is.EqualTo(before.Powder -
                        (step == GuidedBossBattleStep.NavalLoad || step == GuidedBossBattleStep.NavalFire ? 1 : 0)));
                    Assert.That(session.Cargo.Cannonballs, Is.EqualTo(before.Balls - (step == GuidedBossBattleStep.NavalFire ? 1 : 0)));
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(before.Medicine - (step == GuidedBossBattleStep.BoardingHeal ? 1 : 0)));
                }
            }
            switch (step)
            {
                case GuidedBossBattleStep.NavalRetreat:
                    Assert.That(battle.DistanceMeters, Is.EqualTo(530));
                    Assert.That(before.CardBlock, Is.GreaterThan(0), "The taught retreat must also grant defensive cover.");
                    Assert.That(battle.PlayerBlock, Is.EqualTo(before.Guard + before.CardBlock),
                        "The real click must apply the retreat card's effects quoted before the action.");
                    break;
                case GuidedBossBattleStep.NavalLoad:
                    Assert.That(battle.PlayerLoadedShot, Is.EqualTo(5)); break;
                case GuidedBossBattleStep.NavalGuard:
                case GuidedBossBattleStep.NavalGuard2:
                    Assert.That(battle.PlayerBlock, Is.EqualTo(before.Guard + 2)); break;
                case GuidedBossBattleStep.BoardingGuard1:
                case GuidedBossBattleStep.BoardingGuard2:
                    Assert.That(before.CardBlock, Is.GreaterThan(0), "The taught boarding guard must add defensive cover.");
                    Assert.That(battle.PlayerBlock, Is.EqualTo(before.Guard + before.CardBlock),
                        "The real click must apply the guard card's effects quoted before the action.");
                    break;
                case GuidedBossBattleStep.NavalEndTurn1:
                    Assert.That(battle.DistanceMeters, Is.EqualTo(430)); Assert.That(battle.EnemyBlock, Is.EqualTo(3));
                    Assert.That(session.Hull, Is.EqualTo(before.Hull)); break;
                case GuidedBossBattleStep.NavalApproach2:
                    Assert.That(battle.DistanceMeters, Is.EqualTo(280)); break;
                case GuidedBossBattleStep.NavalFire:
                    Assert.That(battle.EnemyHull, Is.EqualTo(before.EnemyHull - 12));
                    Assert.That(battle.EnemyBlock, Is.Zero); Assert.That(battle.PlayerLoadedShot, Is.Zero); break;
                case GuidedBossBattleStep.NavalEndTurn2:
                    Assert.That(battle.DistanceMeters, Is.EqualTo(100)); Assert.That(session.Hull, Is.EqualTo(before.Hull - 1));
                    Assert.That(battle.LastEnemyGuardAbsorbed, Is.EqualTo(2)); break;
                case GuidedBossBattleStep.NavalCircle:
                    Assert.That(battle.DistanceMeters, Is.EqualTo(40)); Assert.That(battle.Alignment, Is.EqualTo(NavalBattleAlignment.EnemyBroadside)); break;
                case GuidedBossBattleStep.NavalApproach3:
                    Assert.That(battle.DistanceMeters, Is.EqualTo(30), "The real pointer approach leaves room before ramming."); break;
                case GuidedBossBattleStep.NavalRam:
                    Assert.That(session.Hull, Is.EqualTo(Math.Max(0, before.Hull - 9)));
                    Assert.That(battle.EnemyHull, Is.EqualTo(before.EnemyHull - 13));
                    if (before.Hull > 9)
                    {
                        Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                        Assert.That(session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.BoardingAttack1));
                        Assert.That(battle.PhaseTurn, Is.EqualTo(1));
                    }
                    else Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
                    break;
                case GuidedBossBattleStep.BoardingAttack1:
                case GuidedBossBattleStep.BoardingAttack2:
                case GuidedBossBattleStep.BoardingAttack3:
                    Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(Math.Max(0, before.EnemyHp - 8)),
                        "The authored thrust deals its full 8 damage regardless of the ten-sailor tutorial roster."); break;
                case GuidedBossBattleStep.BoardingEndTurn1:
                    Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(before.PlayerHp - Math.Max(0, 7 - before.Guard)));
                    Assert.That(battle.LastEnemyGuardAbsorbed, Is.EqualTo(Math.Min(7, before.Guard))); break;
                case GuidedBossBattleStep.BoardingHeal:
                    Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(Math.Min(battle.PlayerMaxBoardingHealth, before.PlayerHp + 6)),
                        "Medicine enables the authored 6-point bandage; crew count no longer reduces its value."); break;
                case GuidedBossBattleStep.BoardingEndTurn2:
                    Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(before.PlayerHp - Math.Max(0, 3 - before.Guard)));
                    Assert.That(battle.LastEnemyGuardAbsorbed, Is.EqualTo(Math.Min(3, before.Guard))); break;
            }
            if (action == GuidedBossActionKind.EndTurn)
            {
                if (session.Phase == CoreDemoPhase.Settlement)
                {
                    Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
                    Assert.That(session.Battle, Is.Null);
                    Assert.That(battle.Turn, Is.EqualTo(before.Turn),
                        "A lethal enemy action resolves the failure before starting another player turn.");
                }
                else Assert.That(battle.Turn, Is.EqualTo(before.Turn + 1));
            }
            if (session.Phase == CoreDemoPhase.Sailing &&
                (step == GuidedBossBattleStep.BoardingAttack1 || step == GuidedBossBattleStep.BoardingAttack2 || step == GuidedBossBattleStep.BoardingAttack3))
            {
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(session.GuidedTutorialBossDefeated, Is.True);
                Assert.That(session.VoyageTreasureFragments, Is.EqualTo(1));
                Assert.That(session.LastBattleRetreated, Is.False);
            }
            else if (session.Phase == CoreDemoPhase.Battle)
                Assert.That(session.GuidedBossStep, Is.EqualTo(step + 1), "One UI action advances exactly one authored step.");
        }

        private static int InitialBoardingHealth(int maxBoardingHealth, int startHull, int currentHull, int maxHull)
        {
            return (int)Math.Ceiling(maxBoardingHealth * (1d - Math.Max(0, startHull - currentHull) / (2d * maxHull)));
        }

        private static string GuidedBossFingerprint(CoreVoyageSession session, bool includeStep = true)
        {
            CoreBattleState b = session.Battle;
            return (includeStep ? session.GuidedBossStep.ToString() : string.Empty) + "|" + session.Phase + "|" + session.Hull + "|" +
                session.Cargo.Powder + ":" + session.Cargo.Cannonballs + ":" + session.Cargo.Medicine + ":" + session.Cargo.Water + ":" +
                session.Cargo.Food + ":" + session.Cargo.Gold + ":" + session.TotalHours + "|" + (b == null ? "null" :
                b.Stage + ":" + b.Turn + ":" + b.PhaseTurn + ":" + b.DistanceMeters + ":" + b.Alignment + ":" + b.Energy + ":" +
                b.EnemyHull + ":" + b.PlayerBlock + ":" + b.EnemyBlock + ":" + b.PlayerBoardingHealth + ":" + b.EnemyBoardingHealth + ":" +
                b.PlayerLoadedShot + ":" + b.CardsPlayedThisTurn + "|" + string.Join(",", b.Hand) + "|" +
                string.Join(",", b.DrawPile) + "|" + string.Join(",", b.DiscardPile));
        }
    }
}
