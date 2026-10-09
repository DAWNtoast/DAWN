using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Fixed ten-health dividers over the existing animated health and damage fills.</summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class BattleHealthSegmentsGraphic : MaskableGraphic
    {
        public const int HealthPerSegment = 10;
        private const string ChildName = "Health Segments";
        private const int MaximumVisibleDividers = 1024;
        [SerializeField] private int maximumHealth = 1;

        public int MaximumHealth => maximumHealth;

        public static BattleHealthSegmentsGraphic Attach(Image hull, int maximum)
        {
            if (hull == null) return null;
            Transform existing = hull.transform.Find(ChildName);
            BattleHealthSegmentsGraphic graphic;
            if (existing == null)
            {
                GameObject host = new GameObject(ChildName, typeof(RectTransform), typeof(BattleHealthSegmentsGraphic));
                host.layer = hull.gameObject.layer;
                host.transform.SetParent(hull.transform, false);
                graphic = host.GetComponent<BattleHealthSegmentsGraphic>();
            }
            else
            {
                graphic = existing.GetComponent<BattleHealthSegmentsGraphic>();
                if (graphic == null) graphic = existing.gameObject.AddComponent<BattleHealthSegmentsGraphic>();
            }
            RectTransform rect = graphic.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            graphic.Configure(maximum);
            return graphic;
        }

        public void Configure(int maximum)
        {
            maximumHealth = Mathf.Max(1, maximum);
            color = new Color(.015f, .022f, .024f, .94f);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            int maximum = Mathf.Max(1, maximumHealth);
            int dividerCount = (maximum - 1) / HealthPerSegment;
            if (dividerCount == 0) return;

            // Normal game health draws every ten-point boundary. When an extreme
            // value makes those boundaries subpixel, coalesce only their drawing
            // to a bounded mesh; the fill and numerical health stay exact.
            int visibleLimit = Mathf.Clamp(Mathf.FloorToInt(rect.width / 3f), 1, MaximumVisibleDividers);
            long stride = ((long)dividerCount + visibleLimit - 1L) / visibleLimit;
            float halfWidth = Mathf.Min(.75f, rect.width * .25f);
            Color32 tint = color;
            for (long divider = stride; divider <= dividerCount; divider += stride)
            {
                float x = rect.xMin + rect.width * (float)(divider * HealthPerSegment / (double)maximum);
                float left = Mathf.Max(rect.xMin, x - halfWidth);
                float right = Mathf.Min(rect.xMax, x + halfWidth);
                int first = mesh.currentVertCount;
                mesh.AddVert(new Vector3(left, rect.yMin), tint, Vector2.zero);
                mesh.AddVert(new Vector3(left, rect.yMax), tint, Vector2.zero);
                mesh.AddVert(new Vector3(right, rect.yMax), tint, Vector2.zero);
                mesh.AddVert(new Vector3(right, rect.yMin), tint, Vector2.zero);
                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
