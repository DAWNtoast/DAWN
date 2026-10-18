using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BattleResolutionKind { NavalSinking, BoardingDefeat }

    /// <summary>Local, code-drawn water/smoke or surrender cloth; never a full-screen flash.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleResolutionGraphic : MaskableGraphic
    {
        private Vector2 span = new Vector2(260f, 170f);
        private float direction = 1f;
        public BattleResolutionKind Kind { get; private set; }
        public float Progress { get; private set; }
        public bool IsTopDown { get; private set; }

        public void Configure(BattleResolutionKind kind, Vector2 extent, float facing, bool topDown = false)
        {
            Kind = kind;
            IsTopDown = topDown;
            span = new Vector2(Mathf.Clamp(extent.x, 160f, 620f), Mathf.Clamp(extent.y, 100f, 360f));
            direction = facing < 0f ? -1f : 1f;
            raycastTarget = false;
            SetProgress(0f);
        }

        public void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (Progress <= 0f || Progress >= 1f) return;
            if (Kind == BattleResolutionKind.NavalSinking) DrawSinking(helper);
            else DrawSurrender(helper);
        }

        private void DrawSinking(VertexHelper helper)
        {
            float t = Progress;
            float waterLife = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .95f));
            for (int ring = 0; ring < 3; ring++)
            {
                float spread = Mathf.Clamp01(t * 1.25f - ring * .10f);
                Color foam = new Color(.60f, .91f, .94f, waterLife * (.72f - ring * .16f));
                Ellipse(helper, new Vector2(0f, -ring * 4f),
                    new Vector2(span.x * (.22f + .36f * spread), IsTopDown
                        ? span.y * (.18f + .32f * spread) : 9f + 18f * spread), foam, 2.5f);
            }
            for (int i = 0; i < 12; i++)
            {
                float u = Mathf.Clamp01((t - .08f - (i % 3) * .025f) / .72f);
                float side = i % 2 == 0 ? -1f : 1f;
                float speed = .30f + (i % 6) * .055f;
                Vector2 point = new Vector2(side * span.x * speed * u,
                    span.y * (.30f + (i % 4) * .06f) * Mathf.Sin(u * Mathf.PI) - u * 18f);
                Color spray = new Color(.78f, .96f, 1f, Mathf.Sin(u * Mathf.PI) * .9f);
                Line(helper, point, point + new Vector2(side * 3f, 8f - u * 12f), spray, 3f);
            }
            for (int i = 0; i < 7; i++)
            {
                float rise = Mathf.Clamp01((t - i * .025f) / .9f);
                Vector2 center = new Vector2((i - 3) * span.x * .052f + direction * rise * 17f,
                    span.y * (.14f + rise * .50f) + (i % 2) * 9f);
                float radius = span.x * (.035f + rise * .035f) * (1f + (i % 3) * .12f);
                Color smoke = new Color(.16f, .19f, .20f, Mathf.Sin(rise * Mathf.PI) * .37f);
                Disc(helper, center, new Vector2(radius, radius * 1.18f), smoke);
            }
        }

        private void DrawSurrender(VertexHelper helper)
        {
            float t = Progress;
            float fade = Mathf.Sin(Mathf.PI * t);
            float lower = Mathf.SmoothStep(0f, 1f, t);
            Vector2 poleBase = new Vector2(direction * span.x * .31f, 3f);
            Vector2 poleTop = poleBase + Vector2.up * span.y * .70f;
            Line(helper, poleBase, poleTop, new Color(.38f, .27f, .17f, fade * .85f), 3f);
            float flagY = Mathf.Lerp(poleTop.y - 6f, poleBase.y + span.y * .16f, lower);
            float droop = 4f + lower * 22f;
            Vector2 a = new Vector2(poleTop.x, flagY);
            Vector2 b = a + new Vector2(direction * span.x * .15f, -droop);
            Vector2 c = b + Vector2.down * span.y * .13f;
            Vector2 d = a + Vector2.down * span.y * .18f;
            Quad(helper, a, b, c, d, new Color(.96f, .92f, .80f, fade * .94f));
            Line(helper, a, b, new Color(.64f, .56f, .39f, fade * .55f), 1.5f);
            for (int i = 0; i < 6; i++)
            {
                float u = Mathf.Clamp01((t - .18f) / .75f);
                float x = (i - 2.5f) * span.x * (.065f + .04f * u);
                Color dust = new Color(.69f, .57f, .38f, Mathf.Sin(u * Mathf.PI) * .27f);
                Disc(helper, new Vector2(x, -3f + (i % 2) * 5f), new Vector2(13f + 10f * u, 3f + 3f * u), dust);
            }
        }

        private static void Ellipse(VertexHelper helper, Vector2 center, Vector2 radius, Color tint, float width)
        {
            const int count = 24;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count, b = (i + 1) * Mathf.PI * 2f / count;
                Line(helper, center + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), radius),
                    center + Vector2.Scale(new Vector2(Mathf.Cos(b), Mathf.Sin(b)), radius), tint, width);
            }
        }

        private static void Disc(VertexHelper helper, Vector2 center, Vector2 radius, Color tint)
        {
            const int count = 12;
            int first = helper.currentVertCount;
            helper.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                helper.AddVert(center + Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radius), tint, Vector2.zero);
            }
            for (int i = 0; i < count; i++) helper.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % count);
        }

        private static void Quad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int first = helper.currentVertCount;
            helper.AddVert(a, tint, Vector2.zero); helper.AddVert(b, tint, Vector2.zero);
            helper.AddVert(c, tint, Vector2.zero); helper.AddVert(d, tint, Vector2.zero);
            helper.AddTriangle(first, first + 1, first + 2); helper.AddTriangle(first, first + 2, first + 3);
        }

        private static void Line(VertexHelper helper, Vector2 from, Vector2 to, Color tint, float width) =>
            NavalTacticalTrackGraphic.AddLine(helper, from, to, width, tint);
    }
}
