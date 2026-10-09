using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Non-blocking teaching annotation, rendered above the UI rather than under
    /// each button's masks. World corners follow resizing, scrolling and hover motion.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GuidedTutorialFocusGraphic : MaskableGraphic
    {
        private readonly Vector3[] corners = new Vector3[4];
        private Selectable selectable;
        public RectTransform Target { get; private set; }
        public bool HasVisibleTarget => Target != null && Target.gameObject.activeInHierarchy &&
            (selectable == null || selectable.IsInteractable());

        public void Configure(Selectable control)
        {
            selectable = control;
            Target = control.transform as RectTransform;
            color = new Color(1f, 0.15f, 0.12f, 1f);
            raycastTarget = false;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            // No flashing or screen tint: only the arrow gently approaches its target.
            if (HasVisibleTarget) SetVerticesDirty();
            else if (canvasRenderer != null) canvasRenderer.Clear();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!HasVisibleTarget) return;
            Target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = rectTransform.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            Rect viewport = rectTransform.rect;
            if (max.x < viewport.xMin || min.x > viewport.xMax || max.y < viewport.yMin || min.y > viewport.yMax) return;
            Rect box = Rect.MinMaxRect(Mathf.Max(min.x - 6f, viewport.xMin + 5f),
                Mathf.Max(min.y - 6f, viewport.yMin + 5f), Mathf.Min(max.x + 6f, viewport.xMax - 5f),
                Mathf.Min(max.y + 6f, viewport.yMax - 5f));
            Color shadow = new Color(0.16f, 0.025f, 0.018f, 0.95f);
            Border(vh, box, 7f, shadow);
            Border(vh, box, 3.5f, color);

            // Wide option rows use side arrows, leaving neighbouring rows readable.
            // Near screen edges choose a side with enough room for the whole arrow.
            Vector2 outward = box.width > 250f ? Vector2.left : Vector2.up;
            if (Room(box, viewport, outward) < 64f) outward = Vector2.right;
            if (Room(box, viewport, outward) < 64f) outward = Vector2.down;
            if (Room(box, viewport, outward) < 64f) outward = Vector2.up;
            Vector2 edge = box.center + Vector2.Scale(outward, box.size * 0.5f);
            float motion = Application.isPlaying ? 3f + 3f * Mathf.Sin(Time.unscaledTime * 3f) : 3f;
            Vector2 tip = edge + outward * (10f + motion);
            Vector2 tail = tip + outward * 36f;
            Vector2 basePoint = tip + outward * 17f;
            Vector2 side = new Vector2(-outward.y, outward.x);
            Segment(vh, tail, basePoint, 12f, shadow);
            Triangle(vh, tip - outward * 2f, basePoint + side * 15f, basePoint - side * 15f, shadow);
            Segment(vh, tail, basePoint, 7f, color);
            Triangle(vh, tip, basePoint + side * 11f, basePoint - side * 11f, color);
        }

        private static float Room(Rect box, Rect viewport, Vector2 direction)
        {
            if (direction == Vector2.left) return box.xMin - viewport.xMin;
            if (direction == Vector2.right) return viewport.xMax - box.xMax;
            return direction == Vector2.up ? viewport.yMax - box.yMax : box.yMin - viewport.yMin;
        }

        private static void Border(VertexHelper vh, Rect box, float width, Color tint)
        {
            Vector2 a = new Vector2(box.xMin, box.yMin), b = new Vector2(box.xMax, box.yMin);
            Vector2 c = new Vector2(box.xMax, box.yMax), d = new Vector2(box.xMin, box.yMax);
            Segment(vh, a, b, width, tint); Segment(vh, b, c, width, tint);
            Segment(vh, c, d, width, tint); Segment(vh, d, a, width, tint);
        }

        private static void Segment(VertexHelper vh, Vector2 start, Vector2 end, float width, Color tint)
        {
            Vector2 delta = (end - start).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * (width * 0.5f);
            int index = vh.currentVertCount;
            vh.AddVert(start - normal, tint, Vector2.zero); vh.AddVert(start + normal, tint, Vector2.zero);
            vh.AddVert(end + normal, tint, Vector2.zero); vh.AddVert(end - normal, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }

        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int index = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
        }
    }
}
