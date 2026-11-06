using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Fills a workshop region with paper without distorting the content on it.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorkshopResponsiveSurface : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceSize;
        [SerializeField] private RectTransform content;

        public Vector2 ReferenceSize => referenceSize;

        public void Configure(Vector2 size, RectTransform content = null)
        {
            referenceSize = size;
            this.content = content;
            ApplyLayout();
        }

        public void ApplyLayout()
        {
            if (content == null || referenceSize.x <= 0f || referenceSize.y <= 0f) return;
            Vector2 available = ((RectTransform)transform).rect.size;
            float scale = Mathf.Min(available.x / referenceSize.x, available.y / referenceSize.y);
            if (scale <= 0f) return;
            content.localScale = Vector3.one * scale;
            content.anchoredPosition = Vector2.zero;
        }

        private void OnEnable() => ApplyLayout();
        private void OnRectTransformDimensionsChange() => ApplyLayout();
    }
}
