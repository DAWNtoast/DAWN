using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private NavalCardChoiceOverlay navalCardChoiceOverlay;
        public bool IsNavalCardChoiceOpen => navalCardChoiceOverlay != null;

        private bool TryBeginNavalCardChoice(Button source, int handIndex, CoreBattleCard card)
        {
            if (session?.Battle == null || session.Battle.Stage == NavalBattleStage.Boarding) return false;
            CoreNavalCardChoice choice = session.GetNavalCardChoice(handIndex);
            if (choice == null || !choice.RequiresChoice) return false;
            NavalTacticalBoardView board = retainedBattleRoot == null ? null : retainedBattleRoot.GetComponentInChildren<NavalTacticalBoardView>();
            if (board == null) board = source.GetComponentInParent<Canvas>()?.GetComponentInChildren<NavalTacticalBoardView>();
            if (board == null) return true;
            CloseNavalCardChoice();
            RectTransform boardRect = (RectTransform)board.transform;
            RectTransform overlay = CreateRect("Naval Card Target Choice", boardRect.parent, boardRect.rect.size, boardRect.anchoredPosition);
            overlay.SetAsLastSibling();
            navalCardChoiceOverlay = overlay.gameObject.AddComponent<NavalCardChoiceOverlay>();
            navalCardChoiceOverlay.Configure(session, board, interfaceFont, handIndex, card, choice, options =>
            {
                CloseNavalCardChoice();
                if (source != null && source.gameObject.activeInHierarchy && !battleAnimationPlaying)
                    StartCoroutine(AnimateBattleCard(source, handIndex, card, null, options));
            }, CloseNavalCardChoice);
            board.GetComponent<NavalTacticalMovementInput>()?.RefreshNow();
            board.GetComponent<NavalAttackRangePreview>()?.RefreshNow();
            return true;
        }

        private void CloseNavalCardChoice()
        {
            NavalCardChoiceOverlay overlay = navalCardChoiceOverlay;
            navalCardChoiceOverlay = null;
            if (overlay == null) return;
            overlay.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject);
        }
    }
}
