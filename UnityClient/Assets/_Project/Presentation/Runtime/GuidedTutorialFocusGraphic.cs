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
        private Canvas targetCanvas, focusCanvas;
        public RectTransform Target { get; private set; }
        public RectTransform VisualTarget
        {
            get
            {
                // Compass buttons share a bounding square for radial hit testing.
                // Keep the real control target, but point at its readable direction.
                if (Target != null && selectable != null && selectable.targetGraphic is NavigationCompassSectorGraphic)
                {
                    RectTransform label = Target.Find("Label") as RectTransform;
                    if (label != null) return label;
                }
                // The selectable body is deliberately smaller than the authored
                // card frame and its resource corners. Follow the complete face.
                if (Target != null)
                {
                    CardSchoolFrameGraphic frame = Target.GetComponentInChildren<CardSchoolFrameGraphic>();
                    if (frame != null && frame.isActiveAndEnabled) return frame.rectTransform;
                }
                return Target;
            }
        }
        public bool HasVisibleTarget => TryGetVisibleTargetBounds(out _, out _);

        public void Configure(Selectable control)
        {
            selectable = control;
            Target = control.transform as RectTransform;
            targetCanvas = control.GetComponent<Canvas>();
            color = new Color(1f, 0.15f, 0.12f, 1f);
            raycastTarget = false;
            RefreshSorting();
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            // No flashing or screen tint: only the arrow gently approaches its target.
            RefreshSorting();
            if (HasVisibleTarget) SetVerticesDirty();
            else if (canvasRenderer != null) canvasRenderer.Clear();
        }

        private void RefreshSorting()
        {
            // Inspection lifts a hand card onto its own canvas. Its annotation
            // must stay above that canvas while retaining ordinary ordering at rest.
            if (targetCanvas == null && Target != null) targetCanvas = Target.GetComponent<Canvas>();
            bool raised = targetCanvas != null && targetCanvas.overrideSorting;
            if (raised && focusCanvas == null) focusCanvas = gameObject.AddComponent<Canvas>();
            if (focusCanvas == null) return;
            focusCanvas.overrideSorting = raised;
            if (!raised) return;
            focusCanvas.sortingLayerID = targetCanvas.sortingLayerID;
            focusCanvas.sortingOrder = targetCanvas.sortingOrder + 1;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!TryGetFocusGeometry(out Rect box, out Vector2 outward)) return;
            Color shadow = new Color(0.16f, 0.025f, 0.018f, 0.95f);
            Border(vh, box, 7f, shadow);
            Border(vh, box, 3.5f, color);

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

        /// <summary>Button, border and the full arrow animation in the requested surface's coordinates.</summary>
        public bool TryGetProtectedRect(RectTransform relativeTo, out Rect bounds)
        {
            bounds = default;
            if (relativeTo == null || !isActiveAndEnabled ||
                !TryGetFocusGeometry(out Rect box, out Vector2 outward)) return false;
            // The widest border extends 3.5px. Reserve the arrow's entire 6px
            // travel, including its shadow, so the paper never chases its pulse.
            Vector2 edge = box.center + Vector2.Scale(outward, box.size * .5f);
            Vector2 side = new Vector2(-outward.y, outward.x);
            Vector2 arrowEnd = edge + outward * 52f;
            Vector2 min = Vector2.Min(box.min - Vector2.one * 3.5f,
                Vector2.Min(arrowEnd + side * 15f, arrowEnd - side * 15f));
            Vector2 max = Vector2.Max(box.max + Vector2.one * 3.5f,
                Vector2.Max(arrowEnd + side * 15f, arrowEnd - side * 15f));
            Rect local = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            corners[0] = new Vector3(local.xMin, local.yMin);
            corners[1] = new Vector3(local.xMin, local.yMax);
            corners[2] = new Vector3(local.xMax, local.yMax);
            corners[3] = new Vector3(local.xMax, local.yMin);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = relativeTo.InverseTransformPoint(rectTransform.TransformPoint(corner));
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private bool TryGetFocusGeometry(out Rect box, out Vector2 outward)
        {
            box = default;
            outward = Vector2.up;
            if (!TryGetVisibleTargetBounds(out Rect target, out Rect clip)) return false;
            Rect viewport = VisibleViewport();
            box = Rect.MinMaxRect(Mathf.Max(target.xMin - 6f, clip.xMin + 5f),
                Mathf.Max(target.yMin - 6f, clip.yMin + 5f), Mathf.Min(target.xMax + 6f, clip.xMax - 5f),
                Mathf.Min(target.yMax + 6f, clip.yMax - 5f));
            if (box.width <= 0f || box.height <= 0f) return false;
            if (VisualTarget.GetComponent<CardSchoolFrameGraphic>() != null)
            {
                outward = CardArrowDirection(box, viewport);
                return true;
            }
            // Wide option rows use side arrows, leaving neighbouring rows readable.
            // Near screen edges choose a side with enough room for the whole arrow.
            outward = box.width > 250f ? Vector2.left : Vector2.up;
            if (Room(box, viewport, outward) < 64f) outward = Vector2.right;
            if (Room(box, viewport, outward) < 64f) outward = Vector2.down;
            if (Room(box, viewport, outward) < 64f) outward = Vector2.up;
            return true;
        }

        private Vector2 CardArrowDirection(Rect box, Rect viewport)
        {
            // The outer side of a hand avoids the hull readouts above it. A
            // middle card instead uses a free direction, never another card face.
            Vector2 side = box.center.x <= viewport.center.x ? Vector2.left : Vector2.right;
            if (CardArrowIsClear(box, viewport, side)) return side;
            if (CardArrowIsClear(box, viewport, -side)) return -side;
            if (CardArrowIsClear(box, viewport, Vector2.up)) return Vector2.up;
            if (CardArrowIsClear(box, viewport, Vector2.down)) return Vector2.down;
            // Extremely tight layouts still keep the annotation inside the screen.
            Vector2 best = Vector2.up;
            foreach (Vector2 direction in new[] { side, -side, Vector2.down })
                if (Room(box, viewport, direction) > Room(box, viewport, best)) best = direction;
            return best;
        }

        private bool CardArrowIsClear(Rect box, Rect viewport, Vector2 direction)
        {
            if (Room(box, viewport, direction) < 64f) return false;
            Vector2 edge = box.center + Vector2.Scale(direction, box.size * .5f);
            Vector2 sideways = new Vector2(-direction.y, direction.x) * 17f;
            Vector2 start = edge + direction * 8f, end = edge + direction * 56f;
            Vector2 min = Vector2.Min(start - sideways, start + sideways);
            Vector2 max = Vector2.Max(end - sideways, end + sideways);
            Rect arrow = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            Transform hand = Target.parent;
            if (hand == null) return true;
            foreach (Transform sibling in hand)
            {
                if (sibling == Target || !sibling.gameObject.activeInHierarchy) continue;
                CardSchoolFrameGraphic frame = sibling.GetComponentInChildren<CardSchoolFrameGraphic>();
                if (frame != null && arrow.Overlaps(BoundsInAnnotation(frame.rectTransform, frame.rectTransform.rect))) return false;
            }
            if (hand.parent != null)
                foreach (string name in new[] { "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track" })
                {
                    RectTransform hud = hand.parent.Find(name) as RectTransform;
                    if (hud != null && hud.gameObject.activeInHierarchy && arrow.Overlaps(BoundsInAnnotation(hud, hud.rect))) return false;
                }
            return true;
        }

        private bool TryGetVisibleTargetBounds(out Rect bounds, out Rect clip)
        {
            bounds = default;
            clip = VisibleViewport();
            if (Target == null || !Target.gameObject.activeInHierarchy ||
                selectable != null && !selectable.IsInteractable()) return false;
            RectTransform visual = VisualTarget;
            bounds = BoundsInAnnotation(visual, visual.rect);
            // A focus lives outside the scroll mask so its arrow remains visible.
            // Nevertheless, it may only frame the part of a row the player can see.
            for (Transform ancestor = visual.parent; ancestor != null; ancestor = ancestor.parent)
            {
                RectMask2D mask = ancestor.GetComponent<RectMask2D>();
                if (mask == null || !mask.isActiveAndEnabled) continue;
                Rect masked = mask.rectTransform.rect;
                Vector4 padding = mask.padding;
                masked = Rect.MinMaxRect(masked.xMin + padding.x, masked.yMin + padding.y,
                    masked.xMax - padding.z, masked.yMax - padding.w);
                if (masked.width <= 0f || masked.height <= 0f) return false;
                clip = Intersection(clip, BoundsInAnnotation(mask.rectTransform, masked));
            }
            bounds = Intersection(bounds, clip);
            return bounds.width > 0f && bounds.height > 0f;
        }

        private Rect VisibleViewport()
        {
            Canvas owner = canvas;
            RectTransform viewport = FixedGameViewport.Bounds(owner);
            return viewport != null ? BoundsInAnnotation(viewport, viewport.rect) : rectTransform.rect;
        }

        private Rect BoundsInAnnotation(RectTransform target, Rect local)
        {
            corners[0] = new Vector3(local.xMin, local.yMin);
            corners[1] = new Vector3(local.xMin, local.yMax);
            corners[2] = new Vector3(local.xMax, local.yMax);
            corners[3] = new Vector3(local.xMax, local.yMin);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = rectTransform.InverseTransformPoint(target.TransformPoint(corner));
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Intersection(Rect first, Rect second) => Rect.MinMaxRect(
            Mathf.Max(first.xMin, second.xMin), Mathf.Max(first.yMin, second.yMin),
            Mathf.Min(first.xMax, second.xMax), Mathf.Min(first.yMax, second.yMax));

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
