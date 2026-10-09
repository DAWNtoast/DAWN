using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Small unscaled feedback motion shared by runtime-built buttons. It keeps
    /// the interaction readable without delaying or changing the click callback.
    /// </summary>
    public sealed class NauticalButtonMotion : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        private RectTransform rect;
        private Button button;
        private Vector3 baseScale;
        private float targetScale = 1f;
        private bool pointerInside;
        private bool pointerDown;
        private bool selected;

        private void Awake()
        {
            rect = transform as RectTransform;
            button = GetComponent<Button>();
            if (rect != null) baseScale = rect.localScale;
        }

        private void OnEnable()
        {
            if (rect == null) rect = transform as RectTransform;
            if (button == null) button = GetComponent<Button>();
            if (rect != null) baseScale = rect.localScale;
            pointerInside = false;
            pointerDown = false;
            selected = false;
            targetScale = 1f;
        }

        private void Update()
        {
            if (rect == null) return;
            if (button != null && !button.interactable)
            {
                pointerInside = false;
                pointerDown = false;
                selected = false;
            }

            targetScale = pointerDown ? 0.965f : (pointerInside || selected ? 1.028f : 1f);
            Vector3 desiredScale = baseScale * targetScale;
            Vector3 currentScale = rect.localScale;
            if (currentScale == desiredScale)
            {
                // Snap once when the exponential tween converges, then avoid
                // dirtying every idle button's RectTransform on every frame.
                if (!currentScale.Equals(desiredScale)) rect.localScale = desiredScale;
                return;
            }
            float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            rect.localScale = Vector3.Lerp(currentScale, desiredScale, blend);
        }

        public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;
        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            pointerDown = false;
        }
        public void OnPointerDown(PointerEventData eventData) => pointerDown = true;
        public void OnPointerUp(PointerEventData eventData) => pointerDown = false;
        public void OnSelect(BaseEventData eventData) => selected = true;
        public void OnDeselect(BaseEventData eventData) => selected = false;

        private void OnDisable()
        {
            if (rect != null) rect.localScale = baseScale;
        }
    }
}
