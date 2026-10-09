using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BattleFxKind
    {
        MuzzleFlash, Smoke, Impact, Splash, Slash, Shockwave,
        CardShine, RuneRing, Trail, ArmorBreak
    }

    /// <summary>
    /// Small, deterministic UI effects without texture quads or additional materials.
    /// The caller owns lifetime and advances Progress using unscaled time. Directional
    /// effects point along local +X; rotate the RectTransform to aim them.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleFxGraphic : MaskableGraphic
    {
        [SerializeField] private BattleFxKind kind;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private int seed;
        [SerializeField, Range(0f, 1f)] private float progress;
        private Rect bounds;

        public BattleFxKind Kind => kind;
        public float Progress
        {
            get => progress;
            set => SetProgress(value);
        }

        public void Configure(BattleFxKind effectKind, Color effectTint, int effectSeed = 0)
        {
            kind = effectKind;
            tint = effectTint;
            seed = effectSeed;
            progress = 0f;
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void SetProgress(float value)
        {
            float safe = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, safe)) return;
            progress = safe;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            bounds = GetPixelAdjustedRect();
            if (progress >= 1f || bounds.width <= 0f || bounds.height <= 0f) return;
            switch (kind)
            {
                case BattleFxKind.MuzzleFlash: Muzzle(mesh); break;
                case BattleFxKind.Smoke: Smoke(mesh); break;
                case BattleFxKind.Impact: Impact(mesh, false); break;
                case BattleFxKind.Splash: Splash(mesh); break;
                case BattleFxKind.Slash: Slash(mesh); break;
                case BattleFxKind.Shockwave: Shockwave(mesh); break;
                case BattleFxKind.CardShine: Shine(mesh); break;
                case BattleFxKind.RuneRing: Runes(mesh); break;
                case BattleFxKind.Trail: Trail(mesh); break;
                case BattleFxKind.ArmorBreak: Impact(mesh, true); break;
            }
        }

        private void Muzzle(VertexHelper mesh)
        {
            float life = Mathf.Pow(1f - progress, 1.5f);
            float expansion = 0.65f + Mathf.Sin(progress * Mathf.PI) * 0.35f;
            Vector2 origin = new Vector2(-0.25f, 0f);
            Blob(mesh, origin, new Vector2(0.29f, 0.42f) * expansion,
                new Color(1f, 0.43f, 0.07f, life * 0.28f), 26);
            Star(mesh, origin, new Vector2(0.22f, 0.39f) * expansion, 11,
                new Color(1f, 0.40f, 0.06f, life * 0.78f), 0.22f);
            Star(mesh, origin, new Vector2(0.17f, 0.28f) * expansion, 8,
                new Color(1f, 0.81f, 0.36f, life), 0.41f);
            // Narrow forward tongues make this read as a cannon blast, not a sun disc.
            for (int i = 0; i < 5; i++)
            {
                float y = (Noise(i + 12) - 0.5f) * 0.36f;
                float length = (0.48f + Noise(i + 30) * 0.22f) * expansion;
                float half = 0.024f + Noise(i + 43) * 0.044f;
                Triangle(mesh, origin + new Vector2(-0.06f, y - half),
                    origin + new Vector2(length, y * 0.7f), origin + new Vector2(-0.06f, y + half),
                    new Color(1f, 0.85f, 0.42f, life), new Color(1f, 0.32f, 0.02f, 0f),
                    new Color(1f, 0.62f, 0.16f, life * 0.8f));
            }
            Blob(mesh, origin, new Vector2(0.085f, 0.14f) * expansion,
                new Color(1f, 0.98f, 0.80f, life), 20);
        }

        private void Smoke(VertexHelper mesh)
        {
            float appear = Mathf.Clamp01(progress * 8f);
            float fade = appear * Mathf.Pow(1f - progress, 1.3f);
            // Staggered scalloped puffs drift upward and spread after the hot flash.
            for (int i = 0; i < 13; i++)
            {
                float local = Mathf.Clamp01(progress - Noise(i + 100) * 0.12f);
                Vector2 center = new Vector2(-0.29f + Noise(i) * 0.50f + local * 0.10f,
                    (Noise(i + 18) - 0.5f) * 0.31f + local * 0.17f);
                float radius = (0.07f + Noise(i + 41) * 0.08f) * (0.60f + local * 1.5f);
                float shade = 0.54f + Noise(i + 79) * 0.30f;
                Blob(mesh, center, new Vector2(radius * 1.2f, radius),
                    new Color(shade, shade * 0.98f, shade * 0.91f, fade * 0.48f), 20);
            }
        }

        private void Impact(VertexHelper mesh, bool armor)
        {
            float fade = Mathf.Pow(1f - progress, 1.5f);
            float distance = 0.08f + progress * 0.31f;
            Color hot = armor ? new Color(0.87f, 0.97f, 1f, fade) : new Color(1f, 0.92f, 0.58f, fade);
            Color cold = armor ? new Color(0.57f, 0.79f, 0.92f, 0f) : new Color(1f, 0.31f, 0.04f, 0f);
            Blob(mesh, Vector2.zero, Vector2.one * (0.23f - progress * 0.09f),
                new Color(hot.r, hot.g, hot.b, fade * 0.43f), 24);
            for (int i = 0; i < 22; i++)
            {
                float angle = i * Mathf.PI * 2f / 22f + Noise(i + 5) * 0.18f;
                Vector2 direction = Direction(angle);
                float spread = distance * (0.60f + Noise(i + 55) * 0.46f);
                Vector2 head = direction * spread + Vector2.down * progress * progress * 0.11f;
                float length = (0.03f + Noise(i + 81) * 0.13f) * (1f - progress * 0.5f);
                Ribbon(mesh, head - direction * length, head, armor ? 0.009f : 0.005f, cold, hot);
                if (i % 3 == 0)
                {
                    Vector2 normal = new Vector2(-direction.y, direction.x);
                    Triangle(mesh, head - direction * 0.014f, head + direction * 0.018f,
                        head + normal * 0.018f, hot, hot, cold);
                }
            }
            Ring(mesh, Vector2.zero, new Vector2(distance, distance * 0.82f),
                0.011f * (1f - progress), hot * new Color(1f, 1f, 1f, 0.4f), 44, 0f, 1f);
        }

        private void Splash(VertexHelper mesh)
        {
            float fade = 1f - progress;
            for (int i = 0; i < 17; i++)
            {
                float phase = i / 16f;
                float x = (phase - 0.5f) * (0.2f + progress * 0.7f);
                float rise = (0.12f + Noise(i) * 0.33f) * Mathf.Sin(progress * Mathf.PI);
                Vector2 head = new Vector2(x, rise - progress * 0.08f);
                Ribbon(mesh, head + new Vector2(-x * 0.10f, -0.055f * fade), head,
                    (0.004f + Noise(i + 26) * 0.008f) * fade,
                    new Color(0.26f, 0.62f, 0.79f, 0f), new Color(0.86f, 0.98f, 1f, fade));
            }
            Ring(mesh, new Vector2(0f, -0.10f), new Vector2(0.12f + progress * 0.33f, 0.035f + progress * 0.07f),
                0.009f * fade, new Color(0.73f, 0.94f, 1f, fade * 0.7f), 44, 0f, 1f);
        }

        private void Slash(VertexHelper mesh)
        {
            float fade = Mathf.Sin(Mathf.Clamp01(progress * 1.1f) * Mathf.PI);
            float reveal = Mathf.Clamp01(progress * 2.5f);
            for (int i = 0; i < 38; i++)
            {
                float a = i / 38f;
                float b = (i + 1f) / 38f;
                if (a > reveal) break;
                Vector2 from = SlashPoint(a);
                Vector2 to = SlashPoint(Mathf.Min(b, reveal));
                float taper = Mathf.Sin(a * Mathf.PI);
                Ribbon(mesh, from, to, 0.026f * taper,
                    new Color(0.57f, 0.80f, 1f, fade * 0.23f), new Color(0.80f, 0.93f, 1f, fade * 0.37f));
                Ribbon(mesh, from, to, 0.006f * taper,
                    new Color(1f, 0.96f, 0.78f, fade * taper), new Color(1f, 1f, 1f, fade * taper));
            }
        }

        private static Vector2 SlashPoint(float value)
        {
            return new Vector2(Mathf.Lerp(-0.44f, 0.44f, value),
                Mathf.Lerp(-0.24f, 0.20f, value) + Mathf.Sin(value * Mathf.PI) * 0.22f);
        }

        private void Shockwave(VertexHelper mesh)
        {
            float radius = Mathf.Lerp(0.07f, 0.47f, 1f - Mathf.Pow(1f - progress, 2f));
            float fade = Mathf.Pow(1f - progress, 2f);
            Ring(mesh, Vector2.zero, new Vector2(radius, radius * 0.70f),
                0.017f * (1f - progress), new Color(1f, 0.95f, 0.74f, fade), 64, 0f, 1f);
            Ring(mesh, Vector2.zero, new Vector2(radius * 0.90f, radius * 0.63f),
                0.006f, new Color(0.54f, 0.80f, 0.87f, fade * 0.36f), 48, 0.04f, 0.94f);
        }

        private void Shine(VertexHelper mesh)
        {
            float x = Mathf.Lerp(-0.90f, 0.90f, progress);
            float alpha = Mathf.Sin(progress * Mathf.PI) * 0.55f;
            Color edge = new Color(1f, 0.87f, 0.51f, 0f);
            Color core = new Color(1f, 1f, 0.92f, alpha);
            Vector2 bottom = new Vector2(x - 0.23f, -0.5f);
            Vector2 top = new Vector2(x + 0.23f, 0.5f);
            Quad(mesh, bottom - Vector2.right * 0.11f, top - Vector2.right * 0.11f,
                top, bottom, edge, edge, core, core);
            Quad(mesh, bottom, top, top + Vector2.right * 0.11f, bottom + Vector2.right * 0.11f,
                core, core, edge, edge);
        }

        private void Runes(VertexHelper mesh)
        {
            float fade = Mathf.Sin(progress * Mathf.PI);
            float rotation = progress * 0.45f;
            Color ink = new Color(1f, 0.83f, 0.40f, fade * 0.82f);
            Ring(mesh, Vector2.zero, Vector2.one * 0.36f, 0.004f, ink, 64, 0f, 1f);
            Ring(mesh, Vector2.zero, Vector2.one * 0.41f, 0.0025f, ink * new Color(1f, 1f, 1f, 0.55f), 64, 0f, 1f);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f + rotation;
                Vector2 direction = Direction(angle);
                Vector2 tangent = new Vector2(-direction.y, direction.x);
                Vector2 center = direction * 0.386f;
                Ribbon(mesh, center - direction * 0.016f, center + direction * 0.016f, 0.002f, ink, ink);
                Ribbon(mesh, center - tangent * 0.009f, center + tangent * 0.009f + direction * 0.011f, 0.002f, ink, ink);
                if (i % 2 == 0)
                    Ribbon(mesh, direction * 0.31f, direction * 0.33f, 0.003f, ink, ink);
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f - rotation;
                Vector2 center = Direction(angle) * 0.31f;
                Star(mesh, center, Vector2.one * 0.025f, 4, ink, 0.2f);
            }
        }

        private void Trail(VertexHelper mesh)
        {
            float fade = Mathf.Pow(1f - progress, 0.75f);
            for (int i = 0; i < 30; i++)
            {
                float a = i / 30f;
                float b = (i + 1f) / 30f;
                Vector2 from = new Vector2(Mathf.Lerp(-0.49f, 0.43f, a), Mathf.Sin(a * 7f + progress * 2f) * 0.035f * (1f - a));
                Vector2 to = new Vector2(Mathf.Lerp(-0.49f, 0.43f, b), Mathf.Sin(b * 7f + progress * 2f) * 0.035f * (1f - b));
                Ribbon(mesh, from, to, 0.080f * a,
                    new Color(1f, 0.49f, 0.11f, a * fade * 0.14f), new Color(1f, 0.68f, 0.20f, b * fade * 0.22f));
                Ribbon(mesh, from, to, 0.019f * a,
                    new Color(1f, 0.86f, 0.43f, a * a * fade * 0.72f), new Color(1f, 1f, 0.83f, b * b * fade));
            }
            Blob(mesh, new Vector2(0.43f, 0f), new Vector2(0.06f, 0.15f),
                new Color(1f, 0.99f, 0.80f, fade), 20);
        }

        private void Star(VertexHelper mesh, Vector2 center, Vector2 radius, int rays, Color value, float inner)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, center, value);
            int points = rays * 2;
            for (int i = 0; i <= points; i++)
            {
                float length = i % 2 == 0 ? 1f : inner;
                Vector2 point = Direction(i * Mathf.PI * 2f / points + Noise(7) * 0.4f);
                Vertex(mesh, center + Vector2.Scale(point, radius) * length,
                    new Color(value.r, value.g, value.b, value.a * (i % 2 == 0 ? 0.04f : 0.78f)));
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Blob(VertexHelper mesh, Vector2 center, Vector2 radius, Color value, int segments)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, center, value);
            for (int i = 0; i <= segments; i++)
            {
                Vector2 point = center + Vector2.Scale(Direction(i * Mathf.PI * 2f / segments), radius);
                Vertex(mesh, point, new Color(value.r, value.g, value.b, 0f));
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Ring(VertexHelper mesh, Vector2 center, Vector2 radius, float width, Color value,
            int segments, float begin, float end)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(begin, end, i / (float)segments) * Mathf.PI * 2f;
                float b = Mathf.Lerp(begin, end, (i + 1f) / segments) * Mathf.PI * 2f;
                Vector2 inner = new Vector2(Mathf.Max(0f, radius.x - width), Mathf.Max(0f, radius.y - width));
                Quad(mesh, center + Vector2.Scale(Direction(a), inner), center + Vector2.Scale(Direction(a), radius),
                    center + Vector2.Scale(Direction(b), radius), center + Vector2.Scale(Direction(b), inner),
                    new Color(value.r, value.g, value.b, 0f), value, value, new Color(value.r, value.g, value.b, 0f));
            }
        }

        private void Ribbon(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tail, Color head)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * width;
            Quad(mesh, from - normal, from + normal, to + normal, to - normal, tail, tail, head, head);
        }

        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, a, ca); Vertex(mesh, b, cb); Vertex(mesh, c, cc); Vertex(mesh, d, cd);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, a, ca); Vertex(mesh, b, cb); Vertex(mesh, c, cc);
            mesh.AddTriangle(first, first + 1, first + 2);
        }

        private void Vertex(VertexHelper mesh, Vector2 point, Color value)
        {
            Vector2 position = new Vector2(bounds.xMin + Mathf.Clamp01(point.x + 0.5f) * bounds.width,
                bounds.yMin + Mathf.Clamp01(point.y + 0.5f) * bounds.height);
            mesh.AddVert(position, value * tint * color, Vector2.zero);
        }

        private static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        private float Noise(int index)
        {
            unchecked
            {
                uint value = (uint)(index * 374761393 + seed * 668265263);
                value = (value ^ (value >> 13)) * 1274126177u;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777215f;
            }
        }
    }
}
