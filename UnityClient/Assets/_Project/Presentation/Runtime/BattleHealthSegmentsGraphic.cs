using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Legacy scene compatibility for the retired ten-health overlay. Health is
    /// now represented by the continuous hull fill and its current/maximum label.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
    public sealed class BattleHealthSegmentsGraphic : MaskableGraphic
    {
        // Retained for older callers; this no longer controls any rendering.
        public const int HealthPerSegment = 10;
        private const string ChildName = "Health Segments";
        [SerializeField] private int maximumHealth = 1;

        public int MaximumHealth => maximumHealth;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

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
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            // Keep the component loadable for existing scenes and prefabs, but
            // never draw the old ten-point divisions over the continuous bar.
        }
    }
}
