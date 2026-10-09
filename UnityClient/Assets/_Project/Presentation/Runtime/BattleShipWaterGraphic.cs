using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void CreateBattleShipWater(string name, Transform parent, Vector2 position, bool foam, float phase)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(370f, 54f), position);
            rect.gameObject.AddComponent<BattleShipWaterGraphic>().Configure(foam, phase);
        }
    }

    /// <summary>
    /// Water-plane contact, not a platform attached to a bobbing sprite.
    /// A feathered submerged shadow is behind the hull; broken foam strokes
    /// are drawn in front. No opaque quad and no pointer interception.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleShipWaterGraphic : MaskableGraphic
    {
        [SerializeField] private bool foam;
        [SerializeField] private float phase;
        private float motion;

        public void Configure(bool foregroundFoam, float offset)
        {
            foam = foregroundFoam;
            phase = offset;
            raycastTarget = false;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!foam || !Application.isPlaying) return;
            motion = Time.unscaledTime * 0.65f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (!foam)
            {
                Color center = new Color(0.025f, 0.13f, 0.17f, 0.38f);
                helper.AddVert(Vector3.zero, center * color, Vector2.zero);
                const int segments = 64;
                for (int n = 0; n <= segments; n++)
                {
                    float angle = n * Mathf.PI * 2f / segments;
                    helper.AddVert(new Vector3(Mathf.Cos(angle) * rect.width * 0.49f,
                        Mathf.Sin(angle) * rect.height * 0.40f, 0f), new Color(0.02f, 0.15f, 0.2f, 0f), Vector2.zero);
                    if (n > 0) helper.AddTriangle(0, n, n + 1);
                }
                return;
            }

            // Interrupted shallow arcs follow the near-side keel. Low contrast
            // and varied widths let the painted sea remain visible between them.
            for (int band = 0; band < 3; band++)
            {
                const int segments = 60;
                float width = rect.width * (0.43f + band * 0.035f);
                float depth = 7f + band * 6f;
                for (int n = 0; n < segments; n++)
                {
                    float t = n / (float)segments;
                    float next = (n + 1f) / segments;
                    float breakup = Mathf.Sin(n * 1.93f + band * 2.7f + phase);
                    if (breakup < -0.40f) continue;
                    Vector2 a = WaterPoint(t, width, depth, band);
                    Vector2 b = WaterPoint(next, width, depth, band);
                    float alpha = Mathf.Sin(t * Mathf.PI) * (0.31f - band * 0.075f);
                    Color tint = new Color(0.63f, 0.83f, 0.81f, alpha);
                    Stroke(helper, a, b, (1.2f - band * 0.22f) * (0.85f + breakup * 0.15f), tint);
                }
            }
        }

        private Vector2 WaterPoint(float t, float width, float depth, int band)
        {
            float x = Mathf.Lerp(-width, width, t);
            float y = -Mathf.Sin(t * Mathf.PI) * depth + 3f - band * 2.5f;
            y += Mathf.Sin(t * 35f + phase + motion + band) * 1.4f;
            return new Vector2(x, y);
        }

        private void Stroke(VertexHelper helper, Vector2 a, Vector2 b, float thickness, Color tint)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * thickness;
            int first = helper.currentVertCount;
            helper.AddVert(a - normal, tint * color, Vector2.zero);
            helper.AddVert(a + normal, tint * color, Vector2.zero);
            helper.AddVert(b + normal, tint * color, Vector2.zero);
            helper.AddVert(b - normal, tint * color, Vector2.zero);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }
    }
}
