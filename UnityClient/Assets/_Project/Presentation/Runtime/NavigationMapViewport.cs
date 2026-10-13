using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation
{
    /// <summary>A bounded chart viewport. Cell size is uniform; the world never changes on resize.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class NavigationMapViewport : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private int columns;
        [SerializeField] private int rows;
        [SerializeField] private Vector2 viewCenter = Vector2.one * .5f;
        [SerializeField] private bool hasViewCenter;
        private RectTransform map;
        private RectTransform frame;
        private RectTransform sea;
        private Vector2 lastPointer;
        private bool dragging;
        private int dragFrame = -1;
        private Action<Vector2> changed;

        public int Columns => columns;
        public int Rows => rows;
        public int FineColumns => columns * 2;
        public int FineRows => rows * 2;
        public Vector2 ViewCenter => viewCenter;
        public bool SuppressesClick => dragging || dragFrame == Time.frameCount;

        public void Initialize(int columnCount, int rowCount, Vector2? center = null, Action<Vector2> onViewChanged = null)
        {
            columns = columnCount;
            rows = rowCount;
            changed = onViewChanged;
            if (center.HasValue) { viewCenter = center.Value; hasViewCenter = true; }
        }

        public void ApplyLayout(RectTransform window, RectTransform plottedSea)
        {
            map = (RectTransform)transform;
            frame = window;
            sea = plottedSea;
            if (columns <= 0 || rows <= 0 || sea.rect.width <= 0 || sea.rect.height <= 0) return;
            float cellSide = Mathf.Max(frame.rect.width / columns, frame.rect.height / rows);
            // Older saved previews can contain rectangular authored cells. This
            // also makes those cells square without modifying their saved world.
            map.localScale = new Vector3(cellSide * columns / sea.rect.width,
                cellSide * rows / sea.rect.height, 1f);
            if (!hasViewCenter)
            {
                RectTransform ship = map.Find("Player Ship") as RectTransform;
                viewCenter = ship == null ? Vector2.one * .5f : Normalize(ship.anchoredPosition);
                hasViewCenter = true;
            }
            Vector2 point = new Vector2(Mathf.Lerp(sea.rect.xMin, sea.rect.xMax, viewCenter.x),
                Mathf.Lerp(sea.rect.yMin, sea.rect.yMax, viewCenter.y));
            SetPosition(-Vector2.Scale(point, map.localScale));
        }

        public void PanBy(Vector2 frameLocalDelta)
        {
            if (map == null || frame == null || sea == null) return;
            SetPosition(map.anchoredPosition + frameLocalDelta);
        }

        public void EnsureVisible(Vector2 mapLocalPoint, float margin = 60f)
        {
            if (map == null || frame == null || sea == null) return;
            Vector2 point = map.anchoredPosition + Vector2.Scale(mapLocalPoint, map.localScale);
            Vector2 safeHalf = Vector2.Max(Vector2.zero, frame.rect.size * .5f - Vector2.one * margin);
            Vector2 target = new Vector2(Mathf.Clamp(point.x, -safeHalf.x, safeHalf.x),
                Mathf.Clamp(point.y, -safeHalf.y, safeHalf.y));
            PanBy(target - point);
        }

        private void SetPosition(Vector2 position)
        {
            Vector2 extent = Vector2.Max(Vector2.zero,
                (Vector2.Scale(sea.rect.size, map.localScale) - frame.rect.size) * .5f);
            map.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -extent.x, extent.x),
                Mathf.Clamp(position.y, -extent.y, extent.y));
            viewCenter = Normalize(new Vector2(-map.anchoredPosition.x / map.localScale.x,
                -map.anchoredPosition.y / map.localScale.y));
            changed?.Invoke(viewCenter);
        }

        private Vector2 Normalize(Vector2 point) => new Vector2(
            Mathf.InverseLerp(sea.rect.xMin, sea.rect.xMax, point.x),
            Mathf.InverseLerp(sea.rect.yMin, sea.rect.yMax, point.y));

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || frame == null) return;
            dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, eventData.position,
                eventData.pressEventCamera, out lastPointer);
            if (dragging) eventData.eligibleForClick = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || !RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, eventData.position,
                    eventData.pressEventCamera, out Vector2 pointer)) return;
            PanBy(pointer - lastPointer);
            lastPointer = pointer;
            eventData.eligibleForClick = false;
            dragFrame = Time.frameCount;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            dragFrame = Time.frameCount;
            eventData.eligibleForClick = false;
        }
    }
}
