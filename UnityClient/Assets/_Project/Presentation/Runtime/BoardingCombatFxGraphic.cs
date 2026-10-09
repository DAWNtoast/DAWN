using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BoardingCombatFxKind { Dash, Slash, Guard, GuardBreak, Heal }

    /// <summary>
    /// Deterministic deck-space effects. The owning animation advances normalized
    /// progress; shared circle geometry and fixed particle counts avoid per-frame
    /// objects, random-state changes and temporary vertex collections.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BoardingCombatFxGraphic : MaskableGraphic
    {
        private const int CircleSteps = 64;
        private static readonly Vector2[] Circle = BuildCircle();
        private static readonly Vector2[] Shield =
        {
            new Vector2(-.24f, .24f), new Vector2(0f, .33f), new Vector2(.24f, .24f),
            new Vector2(.20f, -.10f), new Vector2(0f, -.34f), new Vector2(-.20f, -.10f)
        };
        [SerializeField] private BoardingCombatFxKind kind;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Range(0f, 1f)] private float progress;
        private float direction = 1f;
        private Rect bounds;

        public BoardingCombatFxKind Kind => kind;
        public float Progress
        {
            get => progress;
            set
            {
                float next = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
                if (Mathf.Approximately(progress, next)) return;
                progress = next;
                SetVerticesDirty();
            }
        }

        public void Configure(BoardingCombatFxKind effectKind, Color effectTint, float facing = 1f)
        {
            kind = effectKind;
            tint = effectTint;
            direction = facing < 0f ? -1f : 1f;
            progress = 0f;
            raycastTarget = false;
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
                case BoardingCombatFxKind.Dash: DrawDash(mesh); break;
                case BoardingCombatFxKind.Slash: DrawSlash(mesh); break;
                case BoardingCombatFxKind.Guard: DrawGuard(mesh); break;
                case BoardingCombatFxKind.GuardBreak: DrawGuardBreak(mesh); break;
                case BoardingCombatFxKind.Heal: DrawHeal(mesh); break;
            }
        }

        private void DrawDash(VertexHelper mesh)
        {
            float life = Mathf.Sin(Mathf.PI * progress);
            for (int i = 0; i < 7; i++)
            {
                float y = -.24f + i * .077f;
                float x = -.40f + .14f * progress + (i % 3) * .026f;
                Line(mesh, new Vector2(x, y), new Vector2(.15f + .15f * progress, y + .012f),
                    .007f + (i % 2) * .004f, Tone(new Color(.91f, .88f, .68f), life * .65f), true);
            }
        }

        private void DrawSlash(VertexHelper mesh)
        {
            float life = (1f - progress) * (1f - progress);
            float sweep = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, progress * 2.5f));
            Arc(mesh, new Vector2(-.03f, .03f), new Vector2(.38f, .34f), 8, 30,
                .014f + .019f * life, Tone(new Color(1f, .77f, .33f), life * .72f), sweep, true);
            Arc(mesh, new Vector2(-.03f, .03f), new Vector2(.38f, .34f), 8, 30,
                .005f + .006f * life, Tone(Color.white, life), sweep, true);
            Arc(mesh, new Vector2(.07f, -.02f), new Vector2(.29f, .29f), 39, 17,
                .007f, Tone(Color.white, life * .85f), sweep, true);
            for (int i = 0; i < 12; i++)
            {
                Vector2 outward = Circle[(i * 5 + 2) % CircleSteps];
                float travel = .05f + progress * (.16f + (i % 3) * .035f);
                Vector2 start = outward * travel;
                Vector2 end = start + outward * (.045f + life * .035f);
                Line(mesh, start, end, .006f, Tone(i % 3 == 0 ? Color.white : new Color(1f, .72f, .25f), life), false);
            }
        }

        private void DrawGuard(VertexHelper mesh)
        {
            float life = Mathf.Sin(Mathf.PI * Mathf.Pow(progress, .55f));
            float scale = .80f + .18f * Mathf.SmoothStep(0f, 1f, progress);
            Arc(mesh, Vector2.zero, new Vector2(.39f, .40f) * scale, 0, CircleSteps,
                .024f, Tone(new Color(.76f, .88f, 1f), life * .20f), 1f, false);
            Arc(mesh, Vector2.zero, new Vector2(.36f, .37f) * scale, 0, CircleSteps,
                .007f, Tone(Color.white, life * .80f), 1f, false);
            for (int i = 0; i < Shield.Length; i++)
                Line(mesh, Shield[i] * scale, Shield[(i + 1) % Shield.Length] * scale,
                    .012f, Tone(Color.white, life), false);
            Line(mesh, new Vector2(0f, -.13f), new Vector2(0f, .17f), .009f, Tone(Color.white, life * .75f), false);
            Line(mesh, new Vector2(-.10f, .03f), new Vector2(.10f, .03f), .009f, Tone(Color.white, life * .75f), false);
        }

        private void DrawGuardBreak(VertexHelper mesh)
        {
            float life = 1f - progress;
            for (int i = 0; i < 14; i++)
            {
                Vector2 outward = Circle[(i * 9 + 3) % CircleSteps];
                Vector2 center = outward * (.12f + progress * .24f);
                center.y -= progress * progress * .035f;
                Vector2 along = Circle[(i * 7 + Mathf.FloorToInt(progress * 8f)) % CircleSteps];
                Vector2 across = new Vector2(-along.y, along.x);
                float size = .020f + (i % 3) * .008f;
                Triangle(mesh, center + along * size, center - along * size + across * size * .5f,
                    center - across * size, Tone(Color.white, life * .95f));
            }
            Arc(mesh, Vector2.zero, Vector2.one * (.18f + progress * .22f), 0, CircleSteps,
                .005f, Tone(new Color(.82f, .92f, 1f), life * .48f), 1f, false);
        }

        private void DrawHeal(VertexHelper mesh)
        {
            float life = Mathf.Sin(Mathf.PI * progress);
            Color green = new Color(.57f, 1f, .69f);
            Color warm = new Color(1f, .94f, .65f);
            Vector2 feet = new Vector2(0f, -.34f);
            Arc(mesh, feet, new Vector2(.35f, .075f), 0, CircleSteps,
                .012f, Tone(green, life * .72f), 1f, false);
            Arc(mesh, feet, new Vector2(.29f, .055f), 0, CircleSteps,
                .005f, Tone(warm, life * .88f), 1f, false);
            for (int i = 0; i < 14; i++)
            {
                float phase = Mathf.Repeat(progress * .83f + i * .137f, 1f);
                float x = -.27f + (i % 5) * .133f + Circle[(i * 7) % CircleSteps].x * .025f * phase;
                float y = -.31f + phase * .68f;
                float size = .009f + (i % 3) * .0035f;
                float alpha = life * Mathf.Sin(Mathf.PI * phase);
                Color particle = Tone(i % 3 == 0 ? warm : green, alpha);
                Line(mesh, new Vector2(x - size, y), new Vector2(x + size, y), .005f, particle, false);
                Line(mesh, new Vector2(x, y - size * 1.6f), new Vector2(x, y + size * 1.6f), .005f, particle, false);
            }
        }

        private Color Tone(Color value, float alpha)
        {
            // The effect's recognizable white/gold/green stays intact while the
            // caller's accent provides a restrained side/card tint.
            Color mixed = Color.Lerp(value, tint, .12f);
            mixed.a = Mathf.Clamp01(alpha) * tint.a * color.a;
            return mixed;
        }

        private void Arc(VertexHelper mesh, Vector2 center, Vector2 radius, int first, int steps,
            float thickness, Color shade, float reveal, bool mirror)
        {
            int visible = Mathf.Clamp(Mathf.CeilToInt(steps * reveal), 0, steps);
            for (int i = 0; i < visible; i++)
            {
                Vector2 a = center + Vector2.Scale(Circle[(first + i) % CircleSteps], radius);
                Vector2 b = center + Vector2.Scale(Circle[(first + i + 1) % CircleSteps], radius);
                Line(mesh, a, b, thickness, shade, mirror);
            }
        }

        private void Line(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color shade, bool mirror)
        {
            if (shade.a <= 0f) return;
            if (mirror) { a.x *= direction; b.x *= direction; }
            Vector2 delta = b - a;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized * (thickness * .5f);
            int start = mesh.currentVertCount;
            Add(mesh, a - side, shade); Add(mesh, a + side, shade);
            Add(mesh, b + side, shade); Add(mesh, b - side, shade);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color shade)
        {
            int start = mesh.currentVertCount;
            Add(mesh, a, shade); Add(mesh, b, shade); Add(mesh, c, shade);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private void Add(VertexHelper mesh, Vector2 point, Color shade)
        {
            mesh.AddVert(new Vector3(bounds.center.x + point.x * bounds.width,
                bounds.center.y + point.y * bounds.height), shade, Vector2.zero);
        }

        private static Vector2[] BuildCircle()
        {
            Vector2[] points = new Vector2[CircleSteps];
            for (int i = 0; i < CircleSteps; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSteps;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return points;
        }
    }
}
