using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    // Pointer clicks, hover, and drag continue to belong to the original card.
    // Only wheel input is forwarded to its complete, non-blocking description.
    public sealed class BattleCardReadabilityScroll : MonoBehaviour, IScrollHandler
    {
        public ScrollRect Face { get; set; }
        public ScrollRect Details { get; set; }

        public void OnScroll(PointerEventData eventData)
        {
            ScrollRect target = Details != null && Details.gameObject.activeInHierarchy &&
                Details.content.rect.height > Details.viewport.rect.height + .5f ? Details : Face;
            if (target == null || !target.gameObject.activeInHierarchy || eventData == null) return;
            target.OnScroll(eventData);
            eventData.Use();
        }

        private readonly Vector3[] corners = new Vector3[4];

        // Hover lifts, scales and rotates the hand card every frame. Clamp after
        // that animation so even the first/last card keeps its full reading panel.
        private void LateUpdate() => ClampDetailsToCanvas();

        public void ClampDetailsToCanvas()
        {
            if (Details == null || !Details.gameObject.activeInHierarchy) return;
            RectTransform bubble = Details.transform.parent as RectTransform;
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform bounds = canvas == null ? null : canvas.rootCanvas.transform as RectTransform;
            if (bubble == null || bounds == null) return;
            bubble.GetWorldCorners(corners);
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector3 point = bounds.InverseTransformPoint(corner);
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
            }
            Rect safe = bounds.rect;
            const float padding = 16f;
            float x = left < safe.xMin + padding ? safe.xMin + padding - left :
                right > safe.xMax - padding ? safe.xMax - padding - right : 0f;
            float y = bottom < safe.yMin + padding ? safe.yMin + padding - bottom :
                top > safe.yMax - padding ? safe.yMax - padding - top : 0f;
            bubble.position += bounds.TransformVector(new Vector3(x, y, 0f));
        }
    }
}
