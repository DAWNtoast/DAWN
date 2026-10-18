using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Five finite, deterministic UI fireworks. No textures, global random state or screen flash.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SettlementFireworksGraphic : MaskableGraphic
    {
        public const float Duration = 3.25f;
        public Vector2 Focus { get; set; } = new Vector2(320f, 130f);
        public float Elapsed { get; private set; } = -1f;
        public bool IsEmitting => Elapsed >= 0f && Elapsed < Duration;

        private static readonly Vector2[] Centers =
        {
            new Vector2(-265f, 95f), new Vector2(245f, 125f), new Vector2(-25f, 160f),
            new Vector2(-420f, -5f), new Vector2(370f, -35f),
        };
        private static readonly float[] Starts = { .04f, .40f, .78f, 1.14f, 1.52f };
        private static readonly Color[] Palette =
        {
            new Color(1f, .75f, .25f), new Color(.28f, .82f, .73f), new Color(1f, .52f, .34f),
            new Color(1f, .88f, .46f), new Color(.80f, .50f, .70f),
        };

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void SetTime(float seconds)
        {
            Elapsed = float.IsNaN(seconds) ? -1f : seconds;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!IsEmitting) return;
            for (int burst = 0; burst < Centers.Length; burst++)
            {
                float age = Elapsed - Starts[burst];
                if (age < 0f || age > 1.7f) continue;
                Vector2 center = Focus + Centers[burst];
                Color tint = Palette[burst];
                const float launch = .24f;
                if (age < launch)
                {
                    float t = age / launch;
                    Vector2 head = center + Vector2.down * (1f - t) * 150f;
                    AddTrail(vh, head, head + Vector2.down * 30f, 2.8f, WithAlpha(tint, Mathf.Sin(t * Mathf.PI) * .80f));
                    continue;
                }
                float life = Mathf.Clamp01((age - launch) / 1.46f);
                float opacity = Mathf.Pow(1f - life, .9f) * .98f;
                float expansion = 1f - Mathf.Pow(1f - life, 2.2f);
                for (int particle = 0; particle < 24; particle++)
                {
                    float angle = particle * Mathf.PI * 2f / 24f + burst * .39f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float reach = (100f + ((particle * 7 + burst * 3) % 6) * 15f) * 1.1f;
                    Vector2 position = center + direction * reach * expansion + Vector2.down * life * life * 63f;
                    float size = (particle % 3 == 0 ? 5.5f : 3.8f) * (1f - life * .55f);
                    Color c = Color.Lerp(particle % 4 == 0 ? Palette[0] : tint, Color.white, .18f);
                    float twinkle = .78f + .22f * Mathf.Sin(particle * 2.3f + life * 16f);
                    c = WithAlpha(c, opacity * twinkle);
                    Vector2 tail = position - direction * (12f + 22f * (1f - life));
                    AddGlow(vh, position, size * 2.9f, WithAlpha(c, c.a * .28f));
                    AddTrail(vh, position, tail, size * .68f, WithAlpha(c, c.a * .80f));
                    AddDiamond(vh, position, size, c);
                    if (particle % 6 == 0)
                        AddDiamond(vh, position + direction * -17f + Vector2.down * life * 9f,
                            size * .60f, WithAlpha(c, c.a * .65f));
                }
                // Small local golden core, fading immediately after each burst.
                if (life < .14f)
                    AddDiamond(vh, center, 12f * (1f - life / .14f), WithAlpha(Color.Lerp(Palette[0], Color.white, .35f), .65f * (1f - life / .14f)));
            }
        }

        private static Color WithAlpha(Color value, float alpha)
        {
            value.a = Mathf.Clamp01(alpha);
            return value;
        }

        // Each glow fades from its colored center to transparent edges; no full-screen overlay.
        private static void AddGlow(VertexHelper vh, Vector2 center, float size, Color tint)
        {
            int n = vh.currentVertCount;
            Color edge = WithAlpha(tint, 0f);
            vh.AddVert(center, tint, Vector2.zero);
            vh.AddVert(center + Vector2.up * size * 1.35f, edge, Vector2.zero);
            vh.AddVert(center + Vector2.right * size, edge, Vector2.zero);
            vh.AddVert(center + Vector2.down * size * 1.35f, edge, Vector2.zero);
            vh.AddVert(center + Vector2.left * size, edge, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
            vh.AddTriangle(n, n + 3, n + 4);
            vh.AddTriangle(n, n + 4, n + 1);
        }
        private static void AddDiamond(VertexHelper vh, Vector2 center, float size, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(center + Vector2.up * size * 1.5f, tint, Vector2.zero);
            vh.AddVert(center + Vector2.right * size, tint, Vector2.zero);
            vh.AddVert(center + Vector2.down * size * 1.5f, tint, Vector2.zero);
            vh.AddVert(center + Vector2.left * size, tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
        }

        private static void AddTrail(VertexHelper vh, Vector2 head, Vector2 tail, float width, Color tint)
        {
            Vector2 direction = (head - tail).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * width;
            Color faded = WithAlpha(tint, 0f);
            int n = vh.currentVertCount;
            vh.AddVert(head + side, tint, Vector2.zero);
            vh.AddVert(head - side, tint, Vector2.zero);
            vh.AddVert(tail - side * .2f, faded, Vector2.zero);
            vh.AddVert(tail + side * .2f, faded, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
