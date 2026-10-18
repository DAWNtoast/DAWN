using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>One treasure gesture; the warehouse controller resolves and commits display changes.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class WarehouseTreasureDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        private const int InvalidTarget = -2;
        private Func<bool> canDrag;
        private Func<RectTransform> createGhost;
        private Func<PointerEventData, int> resolveDrop;
        private Action<int> commit;
        private Action<int> highlightTarget;
        private Button button;
        private RectTransform ghost;
        private PointerEventData pressedPointer;
        private PointerEventData dragPointer;
        private bool dragging;
        private int highlightedTarget = InvalidTarget;
        private int? suppressedPointerId;
        private int? lastSuppressedPointerId;
        private int suppressedReleaseFrame = -1;

        // PointerClick shares the release frame; a later keyboard Submit must remain usable.
        public bool SuppressClick => dragging || suppressedPointerId.HasValue || suppressedReleaseFrame == Time.frameCount;

        public void Bind(Func<bool> canDrag, Func<RectTransform> createGhost,
            Func<PointerEventData, int> resolveDrop, Action<int> commit, Action<int> highlightTarget)
        {
            CancelDrag();
            this.canDrag = canDrag;
            this.createGhost = createGhost;
            this.resolveDrop = resolveDrop;
            this.commit = commit;
            this.highlightTarget = highlightTarget;
            button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || dragging ||
                (pressedPointer != null && eventData.pointerId != pressedPointer.pointerId)) return;
            suppressedPointerId = null;
            lastSuppressedPointerId = null;
            suppressedReleaseFrame = -1;
            pressedPointer = eventData;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (OwnsDrag(eventData)) SuppressPointerClick(eventData);
            ReleaseSuppressedPointer(eventData);
            if (pressedPointer != null && eventData.pointerId == pressedPointer.pointerId) pressedPointer = null;
            // EndDrag follows PointerUp, so only EndDrag evaluates and commits the drop.
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null) eventData.useDragThreshold = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || dragging) return;
            // CancelDrag clears pressedPointer, so only a fresh PointerDown can re-arm this gesture.
            if (pressedPointer == null || eventData.pointerId != pressedPointer.pointerId || !CanDrag()) return;
            dragging = true;
            dragPointer = eventData;
            SuppressPointerClick(eventData);
            if (UiEscapeInput.IsPressed || !IsOnScreen(eventData.position)) { CancelDrag(); return; }
            try
            {
                ghost = createGhost();
                if (ghost == null) { CancelDrag(); return; }
                CanvasGroup group = ghost.GetComponent<CanvasGroup>();
                if (group == null) group = ghost.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                foreach (Graphic graphic in ghost.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                ghost.SetAsLastSibling();
                MoveGhost(eventData);
                SetHighlight(ResolveTarget(eventData));
            }
            catch
            {
                CancelDrag();
                throw;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!OwnsDrag(eventData)) return;
            SuppressPointerClick(eventData);
            try
            {
                if (!CanDrag() || ghost == null || UiEscapeInput.IsPressed || !IsOnScreen(eventData.position))
                {
                    CancelDrag();
                    return;
                }
                MoveGhost(eventData);
                SetHighlight(ResolveTarget(eventData));
            }
            catch
            {
                CancelDrag();
                throw;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!OwnsDrag(eventData))
            {
                ReleaseSuppressedPointer(eventData);
                return;
            }
            SuppressPointerClick(eventData);
            int target = InvalidTarget;
            Action<int> acceptedCommit = commit;
            try
            {
                if (CanDrag() && ghost != null && !UiEscapeInput.IsPressed && IsOnScreen(eventData.position))
                    target = ResolveTarget(eventData);
            }
            finally
            {
                // A successful commit may rebuild or destroy this entire interface synchronously.
                try { CancelDrag(); }
                finally { ReleaseSuppressedPointer(eventData); }
            }
            if (target != InvalidTarget) acceptedCommit?.Invoke(target);
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (TryCancelGesture()) eventData?.Use();
        }

        private void Update()
        {
            if ((dragging || pressedPointer != null) && UiEscapeInput.IsPressed) CancelDrag();
            else if (dragging && (!CanDrag() || ghost == null || dragPointer == null || !IsOnScreen(dragPointer.position)))
                CancelDrag();
        }

        private bool CanDrag()
        {
            if (button == null) button = GetComponent<Button>();
            return isActiveAndEnabled && button != null && button.IsActive() && button.IsInteractable() &&
                canDrag != null && createGhost != null && resolveDrop != null && commit != null && canDrag();
        }

        private bool OwnsDrag(PointerEventData eventData) => dragging && dragPointer != null && eventData != null &&
            eventData.button == PointerEventData.InputButton.Left && eventData.pointerId == dragPointer.pointerId;

        private int ResolveTarget(PointerEventData eventData)
        {
            if (!IsOnScreen(eventData.position)) return InvalidTarget;
            int target = resolveDrop(eventData);
            return target >= -1 && target < 6 ? target : InvalidTarget;
        }

        private static bool IsOnScreen(Vector2 point) =>
            point.x >= 0f && point.y >= 0f && point.x < Screen.width && point.y < Screen.height;

        private void MoveGhost(PointerEventData eventData)
        {
            if (ghost == null || !(ghost.parent is RectTransform parent)) return;
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null :
                canvas != null && canvas.worldCamera != null ? canvas.worldCamera : eventData.pressEventCamera;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, eventData.position, camera, out Vector3 world))
                ghost.position = world;
        }

        private void SuppressPointerClick(PointerEventData eventData)
        {
            PointerEventData pointer = eventData ?? dragPointer ?? pressedPointer;
            if (pointer != null)
            {
                suppressedPointerId = pointer.pointerId;
                lastSuppressedPointerId = pointer.pointerId;
            }
            if (eventData != null) eventData.eligibleForClick = false;
            if (pressedPointer != null) pressedPointer.eligibleForClick = false;
            if (dragPointer != null) dragPointer.eligibleForClick = false;
        }

        private void ReleaseSuppressedPointer(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left ||
                !lastSuppressedPointerId.HasValue || eventData.pointerId != lastSuppressedPointerId.Value) return;
            eventData.eligibleForClick = false;
            suppressedPointerId = null;
            // Retain the cancelled pointer identity across disable/rebind until a fresh PointerDown.
            // Its delayed release must still suppress that frame's stale PointerClick.
            suppressedReleaseFrame = Time.frameCount;
        }

        private void FinishClickSuppression()
        {
            if (!suppressedPointerId.HasValue) return;
            suppressedPointerId = null;
            suppressedReleaseFrame = Time.frameCount;
        }

        private void SetHighlight(int target)
        {
            if (highlightedTarget == target) return;
            highlightedTarget = target;
            try { highlightTarget?.Invoke(target); }
            catch (MissingReferenceException) { /* The controller may already be destroying the target. */ }
        }

        private void CancelDrag()
        {
            bool hadGesture = dragging || ghost != null || pressedPointer != null;
            if (hadGesture && UiEscapeInput.IsPressed) UiEscapeInput.TryConsume();
            if (hadGesture) SuppressPointerClick(dragPointer);
            dragging = false;
            dragPointer = null;
            pressedPointer = null;
            RectTransform oldGhost = ghost;
            ghost = null;
            try { SetHighlight(InvalidTarget); }
            finally
            {
                if (oldGhost != null)
                {
                    oldGhost.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(oldGhost.gameObject);
                    else DestroyImmediate(oldGhost.gameObject);
                }
            }
        }

        internal bool TryCancelGesture()
        {
            if (!dragging && ghost == null && pressedPointer == null) return false;
            CancelDrag();
            return true;
        }

        private void CancelUnavailableGesture()
        {
            try { CancelDrag(); }
            finally
            {
                // Inactive or unfocused UI may never receive its pending PointerUp.
                FinishClickSuppression();
            }
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelUnavailableGesture(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelUnavailableGesture(); }
        private void OnDisable() => CancelUnavailableGesture();
        private void OnDestroy() => CancelUnavailableGesture();
    }
}
