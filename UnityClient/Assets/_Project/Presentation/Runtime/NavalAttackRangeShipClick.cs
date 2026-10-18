using UnityEngine;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation
{
    [DisallowMultipleComponent]
    public sealed class NavalAttackRangeShipClick : MonoBehaviour, IPointerClickHandler, ICanvasRaycastFilter,
        IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private NavalAttackRangePreview preview;
        private NavalTacticalMovementInput Movement => preview == null ? null : preview.GetComponent<NavalTacticalMovementInput>();
        public void Configure(NavalAttackRangePreview view) => preview = view;
        // The ship art spans more than one cell; keep neighbouring route cells clickable.
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
            preview != null && preview.IsPlayerCell(screenPoint, eventCamera);
        public void OnPointerClick(PointerEventData eventData)
        {
            if (Movement != null) Movement.HandleShipClick(eventData);
            else if (eventData.button == PointerEventData.InputButton.Left && preview != null) preview.ToggleShipRange();
        }
        public void OnPointerDown(PointerEventData data) => Movement?.OnPointerDown(data);
        public void OnPointerUp(PointerEventData data) => Movement?.OnPointerUp(data);
        public void OnInitializePotentialDrag(PointerEventData data) => Movement?.OnInitializePotentialDrag(data);
        public void OnBeginDrag(PointerEventData data) => Movement?.OnBeginDrag(data);
        public void OnDrag(PointerEventData data) => Movement?.OnDrag(data);
        public void OnEndDrag(PointerEventData data) => Movement?.OnEndDrag(data);
    }
}
