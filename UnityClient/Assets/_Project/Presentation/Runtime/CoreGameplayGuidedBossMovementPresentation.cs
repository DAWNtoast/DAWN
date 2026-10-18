using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool GuidedNavalMovementBlocked => tutorialActive && guidedTutorialStep != GuidedTutorialStep.Complete &&
            (!HasGuidedBossPresentation || !session.GuidedBossAllowsMovement);

        private bool GuidedBossAllowsControl(string name)
        {
            if (name == "Emergency Battle Return") return session.CanRequestEmergencyTow();
            if (name == "Retreat Battle") return false;
            if (name == "Guided Battle Inspect Resources" || name == "Guided Battle Inspect Enemy" ||
                name == "Guided Battle Inspect Deck" || name == "Battle Deck" || name == "Enemy Intent HUD") return true;
            if (name == "Guided Battle Retry") return session.GuidedBossCanRestart;
            if (name == "Guided Battle Continue") return session.GuidedBossCanContinue && !session.GuidedBossExerciseFailed;
            if (name == "End Battle Turn") return session.GuidedBossAllowsEndTurn;
            if (name == "Battle Board") return session.GuidedBossAllowsBoard;
            if (name == "Battle Ram") return session.GuidedBossAllowsRam;
            if (name.StartsWith("Naval Arrival Heading ", StringComparison.Ordinal))
                return session.GuidedBossAllowsMovement && session.Battle.NavalAwaitingHeadingSelection;
            if (name == "Commit Naval Movement" || name == "Undo Naval Movement" || name == "Clear Naval Movement")
                return session.GuidedBossAllowsMovement && !session.Battle.NavalAwaitingHeadingSelection;
            if (session.GuidedBossExpectedAction == GuidedBossActionKind.FreePlay) return true;
            return name == GuidedBossTargetControl && session.GuidedBossExpectedAction != GuidedBossActionKind.Inspect;
        }

        private string GuidedBossMovementInstruction
        {
            get
            {
                if (session.Battle.NavalAwaitingHeadingSelection)
                    return session.CanNavalArrivalContact(true, out _, out _) || session.CanNavalArrivalContact(false, out _, out _)
                        ? "到达后可用剩余行动点接舷或冲撞，也可选择合法朝向普通停船；普通停船清空余量，不能直接掉头。"
                        : "到达后任选一个亮起的合法终向。普通停船清空余量后可继续出牌；不能直接掉头。";
                if (session.NavalMovementPlannedSteps > 0)
                    return "已选择 " + session.NavalMovementPlannedSteps + " / " + session.Battle.NavalActionPoints +
                        " 格。可从末端继续拖动，或拖回已选格撤销；松开保留预览，普通停船后清空行动点。";
                return session.GuidedBossInstruction;
            }
        }

        private void BuildGuidedBossMovementControls()
        {
            if (!HasGuidedBossPresentation || retainedBattleRoot == null) return;
            GuidedBossMovementObserver observer = retainedBattleRoot.GetComponent<GuidedBossMovementObserver>();
            if (observer == null) observer = retainedBattleRoot.gameObject.AddComponent<GuidedBossMovementObserver>();
            observer.Configure(ReadGuidedBossMovementState, RefreshGuidedBattleCourse);
            NavalTacticalMovementInput movement = contentRoot.GetComponentInChildren<NavalTacticalMovementInput>();
            if (movement != null) movement.RefreshNow();
            if (!battleAnimationPlaying && !BattleInspectionBlocksInput)
                foreach (RetainedBattleHandCard view in retainedBattleHandViews)
                    if (view.Button != null && view.Button.transform.parent == retainedBattleHand && view.Index >= 0 &&
                        view.Index < session.Battle.Hand.Count && session.Battle.Hand[view.Index] == view.CardId)
                        RefreshRetainedHandCard(view, view.Index, session.Battle.Hand.Count);
            // Re-read availability after restoring tutorial snapshots: retained
            // views may have been refreshed just before an animation unlocked.
            RefreshNavalHandInputAvailability(retainedBattleRoot, session.Battle,
                battleAnimationPlaying || session.Battle.NavalAwaitingHeadingSelection || BattleInspectionBlocksInput);
            RefreshNavalSpecialActionControls(retainedBattleRoot);
        }

        private int ReadGuidedBossMovementState()
        {
            if (!isActiveAndEnabled || !HasGuidedBossPresentation || retainedBattleRoot == null ||
                !retainedBattleRoot.gameObject.activeInHierarchy || battleAnimationPlaying || BattleInspectionBlocksInput) return -1;
            return (int)session.GuidedBossStep * 1000 + session.NavalMovementPlannedSteps * 10 +
                (session.Battle.NavalAwaitingHeadingSelection ? 1 : 0);
        }
    }

    internal sealed class GuidedBossMovementObserver : MonoBehaviour
    {
        private Func<int> readState;
        private Action refresh;
        private int previous;

        public void Configure(Func<int> read, Action onChanged)
        {
            readState = read;
            refresh = onChanged;
            previous = readState();
        }

        private void LateUpdate()
        {
            if (readState == null || refresh == null) return;
            int current = readState();
            if (current < 0 || current == previous) return;
            previous = current;
            refresh();
        }
    }
}
