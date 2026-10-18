using ShatteredSail.Core.Gameplay;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool HasGuidedBossPresentation => tutorialActive && session != null && session.IsGuidedBossBattle;
        private GuidedBossBattleStep GuidedBossPresentationStep => HasGuidedBossPresentation
            ? session.GuidedBossStep : GuidedBossBattleStep.None;
        private string GuidedBossObjective => session.GuidedBossLessonTitle;
        private string GuidedBossInstruction => session.GuidedBossExpectedAction == GuidedBossActionKind.Movement
            ? GuidedBossMovementInstruction : session.GuidedBossInstruction;

        private string GuidedBossTargetControl
        {
            get
            {
                if (!HasGuidedBossPresentation) return string.Empty;
                switch (session.GuidedBossExpectedAction)
                {
                    case GuidedBossActionKind.EndTurn: return "End Battle Turn";
                    case GuidedBossActionKind.Ram: return "Battle Ram";
                    case GuidedBossActionKind.Board: return "Battle Board";
                    case GuidedBossActionKind.Continue: return session.GuidedBossExerciseFailed ? "Guided Battle Retry" : "Guided Battle Continue";
                    case GuidedBossActionKind.Inspect:
                        if (session.GuidedBossInspectionTopic == "resources") return "Guided Battle Inspect Resources";
                        if (session.GuidedBossInspectionTopic == "enemy") return "Guided Battle Inspect Enemy";
                        if (session.GuidedBossInspectionTopic == "deck") return "Battle Deck";
                        return session.Battle.Hand.Count > 0 ? "Battle Card 0" : string.Empty;
                    case GuidedBossActionKind.Movement:
                        return !session.Battle.NavalAwaitingHeadingSelection && session.NavalMovementPlannedSteps > 0
                            ? "Commit Naval Movement" : string.Empty;
                    case GuidedBossActionKind.Card:
                    case GuidedBossActionKind.BlockedCard:
                        for (int index = 0; index < session.Battle.Hand.Count; index++)
                            if (session.GetBattleCard(session.Battle.Hand[index]).Id == session.GuidedBossExpectedCardId)
                                return "Battle Card " + index;
                        break;
                }
                return string.Empty;
            }
        }

        private bool IsGuidedBossBlockedCardControl(string name) => HasGuidedBossPresentation &&
            session.GuidedBossExpectedAction == GuidedBossActionKind.BlockedCard && name == GuidedBossTargetControl;

        private bool TryHandleGuidedBossCardAttempt(Button source, int handIndex, CoreBattleCard battleCard)
        {
            if (!HasGuidedBossPresentation || session.GuidedBossExpectedAction != GuidedBossActionKind.BlockedCard)
                return false;
            if (!isActiveAndEnabled || profileSaveBlocked || battleAnimationPlaying || source == null ||
                !source.gameObject.activeInHierarchy || !source.IsInteractable() ||
                retainedBattleRoot == null || !source.transform.IsChildOf(retainedBattleRoot) ||
                handIndex < 0 || handIndex >= session.Battle.Hand.Count || battleCard == null ||
                battleCard.Id != session.GuidedBossExpectedCardId || source.name != GuidedBossTargetControl ||
                session.GetBattleCard(session.Battle.Hand[handIndex]).Id != battleCard.Id)
                return true;
            if (session.TryObserveGuidedBossBlockedCard(handIndex, out _)) RefreshGuidedBattleCourse();
            return true;
        }
    }
}
