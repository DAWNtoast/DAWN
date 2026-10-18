using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>One card gesture; the controller owns the real card renderer and deck rules.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class DeckBuilderCardDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        ICancelHandler, IScrollHandler
    {
        private RectTransform surface;
        private RectTransform deckTarget;
        private bool fromDeck;
        private Func<RectTransform> createGhost;
        private Action commit;
        private Action<bool> highlightTarget;
        private Button button;
        private RectTransform ghost;
        private PointerEventData pressedPointer;
        private PointerEventData dragPointer;
        private bool dragging;
        private bool targetHighlighted;

        public bool SuppressClick { get; private set; }

        public void Bind(RectTransform surface, RectTransform deckTarget, bool fromDeck,
            Func<RectTransform> createGhost, Action commit, Action<bool> highlightTarget)
        {
            CancelDrag();
            this.surface = surface;
            this.deckTarget = deckTarget;
            this.fromDeck = fromDeck;
            this.createGhost = createGhost;
            this.commit = commit;
            this.highlightTarget = highlightTarget;
            button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || dragging) return;
            SuppressClick = false;
            pressedPointer = eventData;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
            if (dragging && dragPointer != null && eventData.pointerId == dragPointer.pointerId)
                SuppressPointerClick(eventData);
            if (pressedPointer != null && eventData.pointerId == pressedPointer.pointerId)
                pressedPointer = null;
            // EndDrag follows PointerUp. Keep the drag alive until its drop is evaluated.
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null) eventData.useDragThreshold = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || dragging || !CanDrag()) return;
            // Cancelling before the threshold also cancels the rest of that press.
            // Only a fresh PointerDown can re-arm this card's drag and click.
            if (SuppressClick) { SuppressPointerClick(eventData); return; }
            dragging = true;
            dragPointer = eventData;
            SuppressPointerClick(eventData);
            if (EscapePressed()) { CancelDrag(); return; }
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
                SetHighlight(ValidDrop(eventData));
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
            if (!CanDrag() || ghost == null || EscapePressed()) { CancelDrag(); return; }
            MoveGhost(eventData);
            SetHighlight(ValidDrop(eventData));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!OwnsDrag(eventData)) return;
            SuppressPointerClick(eventData);
            bool accepted = CanDrag() && ghost != null && !EscapePressed() && ValidDrop(eventData);
            Action acceptedCommit = commit;
            // A successful commit can destroy this entire interface synchronously.
            CancelDrag();
            if (accepted) acceptedCommit?.Invoke();
        }

        public void OnCancel(BaseEventData eventData)
        {
            bool hadGesture = dragging || ghost != null || pressedPointer != null;
            CancelDrag();
            if (hadGesture) eventData?.Use();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (pressedPointer != null || dragging)
            {
                SuppressPointerClick(eventData);
                CancelDrag();
            }
            // Handling scroll on a card must retain its enclosing ScrollRect's wheel behavior.
            if (transform.parent != null && eventData != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.scrollHandler);
        }

        private void Update()
        {
            if ((dragging || pressedPointer != null) && EscapePressed()) CancelDrag();
            else if (dragging && (!CanDrag() || ghost == null)) CancelDrag();
        }

        private bool CanDrag()
        {
            if (button == null) button = GetComponent<Button>();
            return isActiveAndEnabled && button != null && button.IsActive() && button.IsInteractable() &&
                surface != null && surface.gameObject.activeInHierarchy && deckTarget != null &&
                deckTarget.gameObject.activeInHierarchy && createGhost != null && commit != null;
        }

        private bool OwnsDrag(PointerEventData eventData) => dragging && dragPointer != null && eventData != null &&
            eventData.button == PointerEventData.InputButton.Left && eventData.pointerId == dragPointer.pointerId;

        private bool ValidDrop(PointerEventData eventData)
        {
            Vector2 point = eventData.position;
            if (point.x < 0f || point.y < 0f || point.x >= Screen.width || point.y >= Screen.height ||
                !RectTransformUtility.RectangleContainsScreenPoint(surface, point, EventCamera(surface, eventData))) return false;
            bool insideDeck = RectTransformUtility.RectangleContainsScreenPoint(deckTarget, point, EventCamera(deckTarget, eventData));
            return fromDeck ? !insideDeck : insideDeck;
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (ghost == null || !(ghost.parent is RectTransform parent)) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, eventData.position,
                EventCamera(parent, eventData), out Vector3 world)) ghost.position = world;
        }

        private static Camera EventCamera(RectTransform rect, PointerEventData eventData)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas != null && canvas.worldCamera != null ? canvas.worldCamera : eventData.pressEventCamera;
        }

        private static bool EscapePressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        private void SuppressPointerClick(PointerEventData eventData)
        {
            SuppressClick = true;
            if (eventData != null) eventData.eligibleForClick = false;
            if (pressedPointer != null) pressedPointer.eligibleForClick = false;
            if (dragPointer != null) dragPointer.eligibleForClick = false;
        }

        private void SetHighlight(bool highlighted)
        {
            if (targetHighlighted == highlighted) return;
            targetHighlighted = highlighted;
            try { highlightTarget?.Invoke(highlighted); }
            catch (MissingReferenceException) { /* The controller may already be destroying the target. */ }
        }

        private void CancelDrag()
        {
            if ((dragging || ghost != null || pressedPointer != null) && EscapePressed()) UiEscapeInput.TryConsume();
            if (dragging || ghost != null || pressedPointer != null) SuppressPointerClick(dragPointer);
            dragging = false;
            dragPointer = null;
            pressedPointer = null;
            RectTransform oldGhost = ghost;
            ghost = null;
            try { SetHighlight(false); }
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

        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelDrag(); }
        private void OnDisable() => CancelDrag();
        private void OnDestroy() => CancelDrag();
    }
}
