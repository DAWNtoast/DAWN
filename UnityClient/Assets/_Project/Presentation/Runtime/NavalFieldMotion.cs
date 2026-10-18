using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public readonly struct NavalFieldMotionCue
    {
        public readonly CoreNavalFieldPresentationEvent Event;
        public readonly float Progress;
        public NavalFieldMotionCue(CoreNavalFieldPresentationEvent fact, float progress)
        { Event = fact; Progress = Mathf.Clamp01(progress); }
    }

    /// <summary>Consumes bounded, authoritative field facts once; animations never write battle state.</summary>
    public sealed class NavalFieldMotion : MonoBehaviour
    {
        private readonly List<NavalFieldMotionCue> cues = new List<NavalFieldMotionCue>(CoreNavalFieldState.PresentationCapacity);
        private readonly List<PendingTrigger> pending = new List<PendingTrigger>(CoreNavalFieldState.PresentationCapacity);
        private readonly struct PendingTrigger
        {
            public readonly CoreNavalFieldPresentationEvent Event;
            public readonly int MovementRevision;
            public PendingTrigger(CoreNavalFieldPresentationEvent fact, int revision)
            { Event = fact; MovementRevision = revision; }
        }
        private CoreNavalFieldState state;
        private NavalFieldGraphic graphic;
        private NavalTacticalBoardView board;
        private Behaviour owner;
        private int expectedMovementRevision;
        private bool active, paused, focusLost, hadOwner;
        public IReadOnlyList<NavalFieldMotionCue> ActiveCues => cues;
        public int PendingTriggerCount => pending.Count;
        public long LastConsumedSequence { get; private set; }
        public int TotalPlacedCues { get; private set; }
        public int TotalRetiredCues { get; private set; }
        public int TotalTriggeredCues { get; private set; }
        public float AmbientTime { get; private set; }
        public bool Suspended => paused || focusLost || (hadOwner && (owner == null || !owner.isActiveAndEnabled));

        public void Configure(NavalFieldGraphic target, CoreNavalFieldState initialState, Behaviour motionOwner = null,
            NavalTacticalBoardView tacticalBoard = null)
        {
            graphic = target; owner = motionOwner; hadOwner = owner != null;
            board = tacticalBoard; state = initialState; active = true;
            LastConsumedSequence = state == null ? 0L : state.PresentationSequence;
            cues.Clear(); pending.Clear();
            graphic.SetMotion(AmbientTime, cues);
        }

        public void Synchronize(CoreNavalFieldState current, bool visible, int movementRevision = -1)
        {
            active = visible;
            if (movementRevision >= 0) expectedMovementRevision = movementRevision;
            if (!ReferenceEquals(state, current))
            {
                // A restored checkpoint or prediction copy is a new baseline, never a replay.
                state = current;
                CancelTransient();
                return;
            }
            if (!active || Suspended || !isActiveAndEnabled) { CancelTransient(); return; }
            if (state == null) return;
            for (int i = 0; i < state.PresentationEventCount; i++)
            {
                CoreNavalFieldPresentationEvent fact = state.PresentationEventAt(i);
                if (fact.Sequence <= LastConsumedSequence) continue;
                if (fact.Change == CoreNavalFieldChangeKind.Triggered && fact.MovementTrigger && board != null)
                {
                    if (pending.Count == CoreNavalFieldState.PresentationCapacity) pending.RemoveAt(0);
                    pending.Add(new PendingTrigger(fact, expectedMovementRevision));
                }
                else Activate(fact);
                LastConsumedSequence = fact.Sequence;
            }
            ActivateReachedTriggers();
            if (graphic != null) graphic.SetMotion(AmbientTime, cues);
        }

        private void Activate(CoreNavalFieldPresentationEvent fact)
        {
            if (cues.Count == CoreNavalFieldState.PresentationCapacity) cues.RemoveAt(0);
            cues.Add(new NavalFieldMotionCue(fact, 0f));
            if (fact.Change == CoreNavalFieldChangeKind.Placed) TotalPlacedCues++;
            else if (fact.Change == CoreNavalFieldChangeKind.Triggered) TotalTriggeredCues++;
            else TotalRetiredCues++;
        }

        private void ActivateReachedTriggers()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingTrigger item = pending[i];
                VoyagePoint shown = board == null ? default : item.Event.TargetPlayer
                    ? board.DisplayedPlayerPositionMeters : board.DisplayedEnemyPositionMeters;
                float dx = shown.X - item.Event.X * CoreVoyageSession.NavalGridCellMeters;
                float dy = shown.Y - item.Event.Y * CoreVoyageSession.NavalGridCellMeters;
                // Rules commit the entire path first. The revision gate prevents a cue firing
                // in the frame before that path starts; completion covers skipped intermediate frames.
                bool started = board == null || board.DisplayedMovementRevision >= item.MovementRevision;
                bool reached = started && (board == null || dx * dx + dy * dy <= 55f * 55f);
                bool completed = board != null && started && !board.IsAnimating;
                if (!reached && !completed) continue;
                Activate(item.Event);
                pending.RemoveAt(i);
            }
        }

        public static float Duration(CoreNavalFieldChangeKind change) =>
            change == CoreNavalFieldChangeKind.Triggered ? .58f :
            change == CoreNavalFieldChangeKind.Placed ? .4f : .46f;

        public void AdvancePresentation(float seconds)
        {
            if (!active || Suspended || !isActiveAndEnabled) { CancelTransient(); return; }
            ActivateReachedTriggers();
            if (graphic == null || (graphic.Cells.Count == 0 && cues.Count == 0)) return;
            AmbientTime += Mathf.Max(0f, seconds);
            for (int i = cues.Count - 1; i >= 0; i--)
            {
                NavalFieldMotionCue cue = cues[i];
                float next = cue.Progress + Mathf.Max(0f, seconds) / Duration(cue.Event.Change);
                if (next >= 1f) cues.RemoveAt(i);
                else cues[i] = new NavalFieldMotionCue(cue.Event, next);
            }
            graphic.SetMotion(AmbientTime, cues);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            // Overlay synchronization supplies the revision after rules have committed. This
            // component must not consume new facts first using the previous frame's revision.
            AdvancePresentation(Mathf.Min(.05f, Time.unscaledDeltaTime));
        }

        public void CancelTransient()
        {
            long sequence = state == null ? 0L : state.PresentationSequence;
            if (cues.Count == 0 && pending.Count == 0 && LastConsumedSequence == sequence) return;
            cues.Clear(); pending.Clear();
            LastConsumedSequence = sequence;
            if (graphic != null) graphic.SetMotion(AmbientTime, cues);
        }

        private void OnApplicationPause(bool value) { paused = value; if (value) CancelTransient(); }
        private void OnApplicationFocus(bool focused) { focusLost = !focused; if (!focused) CancelTransient(); }
        private void OnEnable() => CancelTransient();
        private void OnDisable() => CancelTransient();
        private void OnDestroy() { cues.Clear(); pending.Clear(); state = null; graphic = null; board = null; owner = null; }
    }
}
