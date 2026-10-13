using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A fixed-size diamond on a list's border, independent of the visible content fraction.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class DeckListDiamondScrollbar : MonoBehaviour, IPointerDownHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private ScrollRect scroll;
        private RectTransform handle;
        private RectTransform track;
        private Graphic hitGraphic;
        private float grabOffset;

        public ScrollRect Scroll => scroll;
        public RectTransform Handle => handle;
        public bool HasOverflow => scroll != null && scroll.content != null && scroll.viewport != null &&
            scroll.content.rect.height > scroll.viewport.rect.height + .1f;

        public void Bind(ScrollRect target, RectTransform diamond)
        {
            if (scroll != null) scroll.onValueChanged.RemoveListener(OnScrollChanged);
            scroll = target;
            handle = diamond;
            track = (RectTransform)transform;
            hitGraphic = GetComponent<Graphic>();
            if (handle != null)
            {
                handle.anchorMin = handle.anchorMax = handle.pivot = new Vector2(.5f, .5f);
                foreach (Graphic graphic in handle.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
            if (scroll != null) scroll.onValueChanged.AddListener(OnScrollChanged);
            Refresh();
        }

        public void Refresh()
        {
            bool overflow = HasOverflow;
            if (hitGraphic != null) hitGraphic.raycastTarget = overflow;
            if (handle == null || track == null) return;
            handle.gameObject.SetActive(overflow);
            if (!overflow) return;
            float halfHeight = handle.rect.height * .5f;
            float low = track.rect.yMin + halfHeight;
            float high = track.rect.yMax - halfHeight;
            handle.anchoredPosition = new Vector2(0f, Mathf.Lerp(low, high, scroll.verticalNormalizedPosition));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanUse(eventData)) return;
            grabOffset = 0f;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position,
                eventData.pressEventCamera, out Vector2 local))
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(handle, eventData.position, eventData.pressEventCamera))
                    grabOffset = local.y - handle.anchoredPosition.y;
                SetFromPointer(eventData);
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (eventData != null) eventData.useDragThreshold = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanUse(eventData)) return;
            eventData.eligibleForClick = false;
            SetFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData) => SetFromPointer(eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            SetFromPointer(eventData);
            grabOffset = 0f;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !HasOverflow || eventData == null) return;
            scroll.OnScroll(eventData);
            Refresh();
        }

        private bool CanUse(PointerEventData eventData) => isActiveAndEnabled && HasOverflow && handle != null &&
            track != null && scroll.IsActive() && eventData != null && eventData.button == PointerEventData.InputButton.Left;

        private void SetFromPointer(PointerEventData eventData)
        {
            if (!CanUse(eventData) || !RectTransformUtility.ScreenPointToLocalPointInRectangle(track,
                eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            float halfHeight = handle.rect.height * .5f;
            float low = track.rect.yMin + halfHeight;
            float high = track.rect.yMax - halfHeight;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = Mathf.InverseLerp(low, high, local.y - grabOffset);
            Refresh();
        }

        private void OnScrollChanged(Vector2 _) => Refresh();
        private void LateUpdate() => Refresh();
        private void OnDestroy()
        {
            if (scroll != null) scroll.onValueChanged.RemoveListener(OnScrollChanged);
        }
    }
}
