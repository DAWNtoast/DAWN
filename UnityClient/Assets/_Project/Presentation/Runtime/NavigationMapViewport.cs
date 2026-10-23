using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation
{
    /// <summary>A fixed, full-chart view. Resizing never hides or changes the generated world.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class NavigationMapViewport : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private int columns;
        [SerializeField] private int rows;
        [SerializeField] private Vector2 viewCenter = Vector2.one * .5f;
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
            // Previously saved pan positions no longer crop the chart.
            viewCenter = Vector2.one * .5f;
        }

        public void ApplyLayout(RectTransform window, RectTransform plottedSea)
        {
            map = (RectTransform)transform;
            frame = window;
            sea = plottedSea;
            if (columns <= 0 || rows <= 0 || sea.rect.width <= 0 || sea.rect.height <= 0) return;
            // Fit both axes, including legacy charts with a different row count.
            // The visible border is always the actual edge of the whole world.
            map.localScale = new Vector3(frame.rect.width / sea.rect.width,
                frame.rect.height / sea.rect.height, 1f);
            CenterMap();
        }

        public void PanBy(Vector2 frameLocalDelta)
        {
            if (map == null || frame == null || sea == null) return;
            CenterMap();
        }

        public void EnsureVisible(Vector2 mapLocalPoint, float margin = 60f)
        {
            if (map == null || frame == null || sea == null) return;
            // Every point is already visible. Ship motion and locate actions
            // must not move the map or expose another region beyond its border.
            CenterMap();
        }

        private void CenterMap()
        {
            map.anchoredPosition = frame.rect.center - Vector2.Scale(sea.rect.center, map.localScale);
            viewCenter = Vector2.one * .5f;
            changed?.Invoke(viewCenter);
        }

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
