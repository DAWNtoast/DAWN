using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private BattleHandFlightMotion battleHandFlight;
        private readonly List<RectTransform> pendingBattleDealCards = new List<RectTransform>(8);

        private bool IsBattleHandFlying => battleHandFlight != null && battleHandFlight.IsPlaying;

        private void CompleteBattleHandFlight()
        {
            if (battleHandFlight != null) battleHandFlight.Complete();
            pendingBattleDealCards.Clear();
        }

        private BattleHandFlightMotion GetBattleHandFlight()
        {
            if (retainedBattleRoot == null) return null;
            if (battleHandFlight == null || battleHandFlight.transform != retainedBattleRoot)
            {
                if (battleHandFlight != null) battleHandFlight.Complete();
                battleHandFlight = retainedBattleRoot.GetComponent<BattleHandFlightMotion>();
                if (battleHandFlight == null) battleHandFlight = retainedBattleRoot.gameObject.AddComponent<BattleHandFlightMotion>();
            }
            return battleHandFlight;
        }

        private void DealInitialBattleHand()
        {
            pendingBattleDealCards.Clear();
            foreach (RetainedBattleHandCard view in retainedBattleHandViews)
                if (view.Button != null) pendingBattleDealCards.Add((RectTransform)view.Button.transform);
            DealPendingBattleHand();
        }

        private void DealPendingBattleHand()
        {
            if (Application.isPlaying && isActiveAndEnabled && !editorPreviewBuilding && pendingBattleDealCards.Count > 0)
            {
                BattleHandFlightMotion motion = GetBattleHandFlight();
                RectTransform source = retainedBattleRoot == null ? null : retainedBattleRoot.Find("Battle Draw Pile") as RectTransform;
                if (motion != null && source != null)
                {
                    motion.Deal(pendingBattleDealCards, source);
                    PlaySfx("battle_card_play", .28f);
                }
            }
            pendingBattleDealCards.Clear();
        }

        private IEnumerator DiscardBattleHandAtTurnEnd()
        {
            CoreBattleState battle = session.Battle;
            BattleHandFlightMotion motion = GetBattleHandFlight();
            if (battle == null || retainedBattleRoot == null || motion == null) yield break;
            var outgoing = new List<RetainedBattleHandCard>(retainedBattleHandViews.Count);
            var cards = new List<RectTransform>(retainedBattleHandViews.Count);
            foreach (RetainedBattleHandCard view in retainedBattleHandViews)
            {
                if (view.Button == null || !view.Button.gameObject.activeInHierarchy ||
                    view.Button.transform.parent != retainedBattleHand) continue;
                // Boarding retention belongs to a hand slot, never a card ID.
                // A different copy of the same card can still be discarded.
                if (battle.Stage == NavalBattleStage.Boarding && view.Index >= 0 &&
                    battle.Boarding.HeldCards.Count <= battle.Hand.Count &&
                    view.Index < battle.Boarding.HeldCards.Count)
                {
                    CoreBoardingHeldCard held = battle.Boarding.HeldCards[view.Index];
                    if (held.CardIndex == view.CardId && held.Retain && !held.RetainUsed) continue;
                }
                outgoing.Add(view);
                cards.Add((RectTransform)view.Button.transform);
            }
            if (cards.Count == 0) yield break;
            RectTransform destination = retainedBattleRoot.Find("Battle Discard Pile") as RectTransform;
            motion.Discard(cards, destination);
            PlaySfx("battle_card_play", .32f);
            while (motion != null && motion.IsPlaying) yield return null;
            if (motion != null) motion.Complete();
            foreach (RetainedBattleHandCard view in outgoing)
            {
                // Even if the next draw has the same ID, it is a new draw and
                // must fly out of the deck, not reuse an old card left in hand.
                retainedBattleHandViews.Remove(view);
                if (view.Button == null) continue;
                view.Button.gameObject.SetActive(false);
                Destroy(view.Button.gameObject);
            }
        }
    }
}
