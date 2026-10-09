using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation
{
    // Slider.onValueChanged fires during layout and throughout a drag. Use the
    // completed user gesture to advance guidance while retaining pointer capture.
    public sealed class GuidedTutorialDistanceInput : MonoBehaviour, IPointerUpHandler,
        IPointerClickHandler, IEndDragHandler, ISubmitHandler
    {
        private Action confirm;

        public void Initialize(Action callback) { confirm = callback; }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left) Confirm();
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left) Confirm();
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left) Confirm();
        }
        public void OnSubmit(BaseEventData eventData) { Confirm(); }
        private void Confirm() { if (isActiveAndEnabled) confirm?.Invoke(); }
    }
}
