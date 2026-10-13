using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Converts a pointer click on the chart into coordinates local to the map.
    /// It is intentionally independent from the voyage rules so tests and other
    /// input sources can submit the same local position.
    /// </summary>
    public sealed class MapCourseInputSurface : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform mapRect;
        private Action<Vector2> selectLocalPoint;

        public void Initialize(RectTransform rect, Action<Vector2> selection)
        {
            mapRect = rect;
            selectLocalPoint = selection;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (mapRect == null || eventData == null || eventData.dragging ||
                (mapRect.GetComponent<NavigationMapViewport>()?.SuppressesClick ?? false))
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mapRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                SelectLocalPoint(localPoint);
            }
        }

        public void SelectLocalPoint(Vector2 localPoint)
        {
            selectLocalPoint?.Invoke(localPoint);
        }
    }
}
