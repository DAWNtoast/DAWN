using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation
{
    // Only pointer hover is handled here so wheel/drag events keep bubbling to
    // the enclosing ScrollRect, including on non-removable special-card rows.
    [DisallowMultipleComponent]
    public sealed class DeckCardPreviewHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private Action show;
        private Action hide;

        public void Bind(Action onEnter, Action onExit)
        {
            show = onEnter;
            hide = onExit;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isActiveAndEnabled && !eventData.dragging) show?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData) => hide?.Invoke();

        // Scrolling can hide a preview without moving the pointer into another
        // row. Allow movement within that same row to show its card again.
        public void OnPointerMove(PointerEventData eventData) => OnPointerEnter(eventData);

        private void OnDisable() => hide?.Invoke();
    }
}
