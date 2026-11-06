using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A transparent, polygon-shaped hit target over a painted workshop object.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopSceneHotspot : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, ICanvasRaycastFilter
    {
        [SerializeField] private Button owner;
        [SerializeField] private Text label;
        [SerializeField] private Graphic nameplate;
        [SerializeField] private Vector2[] polygon;
        [SerializeField] private Color restingLabelColor = Color.white;
        [SerializeField] private Color restingNameplateColor = Color.white;
        private bool validPolygon, hovered, focused, highlighted, watchingInteraction;

        private bool CanInteract => isActiveAndEnabled && validPolygon && owner != null &&
            owner.IsActive() && owner.IsInteractable();

        public void Configure(Button owner, Text label, Vector2[] polygon, Graphic nameplate = null)
        {
            ResetFeedback();
            this.owner = owner;
            this.label = label;
            this.nameplate = nameplate;
            if (label != null) restingLabelColor = label.color;
            if (nameplate != null) restingNameplateColor = nameplate.color;
            this.polygon = polygon == null ? null : (Vector2[])polygon.Clone();
            validPolygon = IsValidPolygon(this.polygon);
            color = Color.white;
            raycastTarget = true;
            canvasRenderer.SetColor(Color.white);
            if (owner != null)
            {
                owner.targetGraphic = this;
                owner.transition = Selectable.Transition.None;
                focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == owner.gameObject;
            }
            RefreshFeedback();
            SetAllDirty();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = eventData != null && IsRaycastLocationValid(eventData.position, eventData.enterEventCamera);
            RefreshFeedback();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            RefreshFeedback();
        }

        public void OnSelect(BaseEventData eventData)
        {
            focused = CanInteract;
            RefreshFeedback();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            focused = false;
            RefreshFeedback();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!CanInteract || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out Vector2 point)) return false;
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f || !rect.Contains(point)) return false;
            Vector2 normalized = new Vector2((point.x - rect.xMin) / rect.width, (point.y - rect.yMin) / rect.height);
            float winding = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                float cross = Cross(b - a, normalized - a);
                if (Mathf.Abs(cross) <= .00001f) continue;
                if (winding != 0f && cross * winding < 0f) return false;
                winding = cross;
            }
            return true;
        }

        private void RefreshFeedback()
        {
            if (!CanInteract) hovered = focused = false;
            bool next = CanInteract && (hovered || focused);
            if (highlighted != next)
            {
                highlighted = next;
                if (label != null) label.color = next
                    ? new Color(1f, .86f, .52f, restingLabelColor.a) : restingLabelColor;
                if (nameplate != null) nameplate.color = next
                    ? new Color(1.12f, 1.08f, .97f, restingNameplateColor.a) : restingNameplateColor;
            }
            // Button.interactable has no change event. Watch only an active highlight,
            // before the canvas rebuild, so disabling the button cannot leave it lit.
            if (watchingInteraction == next) return;
            watchingInteraction = next;
            if (next) Canvas.preWillRenderCanvases += RefreshFeedback;
            else Canvas.preWillRenderCanvases -= RefreshFeedback;
        }

        private void ResetFeedback()
        {
            if (watchingInteraction) Canvas.preWillRenderCanvases -= RefreshFeedback;
            watchingInteraction = false;
            hovered = focused = highlighted = false;
            if (label != null) label.color = restingLabelColor;
            if (nameplate != null) nameplate.color = restingNameplateColor;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = true;
            validPolygon = IsValidPolygon(polygon);
            RefreshFeedback();
        }

        protected override void OnDisable()
        {
            ResetFeedback();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ResetFeedback();
            base.OnDestroy();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!validPolygon) return;
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            // Keep the painted furniture untouched in every interaction state.
            // A transparent mesh preserves the original polygon click target.
            for (int i = 0; i < polygon.Length; i++)
                mesh.AddVert(LocalPoint(rect, polygon[i]), Color.clear, Vector2.zero);
            for (int i = 1; i < polygon.Length - 1; i++) mesh.AddTriangle(0, i, i + 1);
        }

        private static Vector2 LocalPoint(Rect rect, Vector2 point) =>
            new Vector2(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height);

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool IsValidPolygon(Vector2[] points)
        {
            if (points == null || points.Length < 3) return false;
            float winding = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Length], c = points[(i + 2) % points.Length];
                if (float.IsNaN(a.x) || float.IsNaN(a.y) || a.x < 0f || a.x > 1f || a.y < 0f || a.y > 1f)
                    return false;
                float cross = Cross(b - a, c - b);
                if (Mathf.Abs(cross) <= .00001f) continue;
                if (winding != 0f && cross * winding < 0f) return false;
                winding = cross;
            }
            return winding != 0f;
        }

    }
}
