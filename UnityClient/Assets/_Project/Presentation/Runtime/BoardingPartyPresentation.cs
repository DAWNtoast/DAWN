using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Identifies a grounded boarding formation independently of optional UI labels.</summary>
    [DisallowMultipleComponent]
    public sealed class BoardingPartyPresentation : MonoBehaviour
    {
        private readonly List<BoardingActorGraphic> actors = new List<BoardingActorGraphic>();
        public bool MotionSuspended { get; private set; }
        public bool HasActors => actors.Count > 0;
        public BoardingActorGraphic Performer { get; private set; }
        private int enemyAttackIndex;

        private BoardingActorGraphic Leader => actors.Count == 0 ? null :
            actors.Find(actor => actor != null && (actor.Identity == "captain" || actor.Identity == "enemy_party_center")) ?? actors[0];

        private void OnEnable()
        {
            actors.Clear();
            Performer = null;
            MotionSuspended = false;
            enemyAttackIndex = 0;
            foreach (BoardingActorGraphic actor in GetComponentsInChildren<BoardingActorGraphic>(true)) Register(actor);
        }

        public void Register(BoardingActorGraphic actor)
        {
            if (actor == null) return;
            if (!actors.Contains(actor)) actors.Add(actor);
            actor.SetMotionSuspended(MotionSuspended);
        }

        public void PlayAction(BoardingActorAction action, float duration = .45f)
        {
            if (MotionSuspended) return;
            if (action == BoardingActorAction.Death)
            {
                Performer = null;
                foreach (BoardingActorGraphic actor in actors)
                    if (actor != null) actor.PlayAction(action, duration);
                return;
            }
            Performer = Leader;
            if (Performer != null && Performer.Identity == "enemy_party_center" && actors.Count == 3)
            {
                if (action == BoardingActorAction.Attack) Performer = actors[(1 + enemyAttackIndex++ % 3) % 3];
                else if (action == BoardingActorAction.Cast || action == BoardingActorAction.Heal) Performer = actors[2];
            }
            if (Performer != null) Performer.PlayAction(action, duration);
        }

        public void React(BattleSchoolAttackOutcome outcome)
        {
            if (MotionSuspended || outcome == BattleSchoolAttackOutcome.Missed) return;
            BoardingActorAction reaction = outcome == BattleSchoolAttackOutcome.Guarded ? BoardingActorAction.Guard : BoardingActorAction.Hit;
            foreach (BoardingActorGraphic actor in actors)
                if (actor != null) actor.React(reaction, .34f);
        }

        public void PlayCard(CoreBattleCard card)
        {
            if (card == null || !card.IsAllowedInStage(NavalBattleStage.Boarding)) return;
            PlaySource(card.Id, card.RequiredCrew, BoardingCardActorPresentation.ForCard(card));
        }

        public void PlaySource(string cardId, string crewId, BoardingActorAction action)
        {
            if (MotionSuspended) return;
            CoreSpecialCardDefinition specialist = CoreSpecialCardCatalog.FindByCardId(cardId) ?? CoreSpecialCardCatalog.ForCrew(crewId);
            if (specialist != null && specialist.SourceKind == CoreSpecialCardSourceKind.Crew)
                crewId = specialist.SourceId;
            Performer = actors.Find(actor => actor != null && !string.IsNullOrEmpty(actor.CardId) &&
                (actor.CardId == cardId || actor.Identity == crewId));
            if (Performer == null && specialist != null && specialist.SourceKind == CoreSpecialCardSourceKind.Crew &&
                specialist.Card.IsAllowedInStage(NavalBattleStage.Boarding))
            {
                // A voyage can bring more specialists than there are places on deck.
                // The owner takes the outer station for this card and returns it after the gesture.
                BoardingActorGraphic outer = actors.FindLast(actor => actor != null && actor.UsesActionAtlas && !string.IsNullOrEmpty(actor.CardId));
                Texture2D sheet = outer != null ? BoardingBattleArtCatalog.ForActorActions(crewId) : null;
                if (sheet != null)
                {
                    Performer = outer;
                    outer.PlayGuestAction(crewId, specialist.Card.Id, specialist.SourceName, sheet, action,
                        BoardingCardActorPresentation.Duration(action));
                    return;
                }
            }
            if (Performer == null) Performer = Leader;
            if (Performer != null) Performer.PlayAction(action, BoardingCardActorPresentation.Duration(action));
        }

        public void SetDeathProgress(float progress, float duration)
        {
            foreach (BoardingActorGraphic actor in actors)
                if (actor != null && actor.Action == BoardingActorAction.Death)
                    actor.SetPresentationTime(Mathf.Clamp01(progress) * duration);
        }

        public void ClearActions()
        {
            Performer = null;
            foreach (BoardingActorGraphic actor in actors)
                if (actor != null) actor.PlayAction(BoardingActorAction.Idle);
        }

        public void SetMotionSuspended(bool suspended)
        {
            MotionSuspended = suspended;
            if (suspended) Performer = null;
            foreach (BoardingActorGraphic actor in actors)
                if (actor != null) actor.SetMotionSuspended(suspended);
        }

        public static Vector2 LungeOffset(float progress, float direction)
        {
            return Vector2.zero;
        }
    }
}
