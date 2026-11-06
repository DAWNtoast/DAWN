using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum NavalCommandIconKind { Swords, Anchor, Navigation, Hourglass, Chevron }

    /// <summary>Small engraved line symbols for naval commands, kept separate from resource artwork.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalCommandIconGraphic : MaskableGraphic
    {
        private NavalCommandIconKind kind;
        private Rect drawing;
        private float scale;

        public void Configure(NavalCommandIconKind value) { kind = value; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            drawing = GetPixelAdjustedRect(); scale = Mathf.Min(drawing.width, drawing.height);
            if (kind == NavalCommandIconKind.Swords)
            {
                Sword(helper, false); Sword(helper, true);
            }
            else if (kind == NavalCommandIconKind.Anchor)
            {
                Circle(helper, new Vector2(0f, .32f), .075f);
                Line(helper, new Vector2(0f, .24f), new Vector2(0f, -.36f));
                Line(helper, new Vector2(-.16f, .09f), new Vector2(.16f, .09f));
                Path(helper, new Vector2(-.33f, -.08f), new Vector2(-.29f, -.24f), new Vector2(-.16f, -.34f),
                    new Vector2(0f, -.39f), new Vector2(.16f, -.34f), new Vector2(.29f, -.24f), new Vector2(.33f, -.08f));
                Path(helper, new Vector2(-.38f, -.16f), new Vector2(-.33f, -.08f), new Vector2(-.23f, -.13f));
                Path(helper, new Vector2(.38f, -.16f), new Vector2(.33f, -.08f), new Vector2(.23f, -.13f));
            }
            else if (kind == NavalCommandIconKind.Navigation)
            {
                Path(helper, new Vector2(-.37f, .03f), new Vector2(.38f, .37f), new Vector2(.04f, -.38f),
                    new Vector2(-.06f, -.06f), new Vector2(-.37f, .03f));
                Line(helper, new Vector2(-.06f, -.06f), new Vector2(.38f, .37f));
            }
            else if (kind == NavalCommandIconKind.Hourglass)
            {
                Line(helper, new Vector2(-.26f, .39f), new Vector2(.26f, .39f));
                Line(helper, new Vector2(-.26f, -.39f), new Vector2(.26f, -.39f));
                Path(helper, new Vector2(-.20f, .37f), new Vector2(-.18f, .22f), new Vector2(.15f, -.17f), new Vector2(.20f, -.37f));
                Path(helper, new Vector2(.20f, .37f), new Vector2(.18f, .22f), new Vector2(-.15f, -.17f), new Vector2(-.20f, -.37f));
                Line(helper, new Vector2(-.12f, -.29f), new Vector2(.12f, -.29f));
            }
            else Path(helper, new Vector2(-.12f, .30f), new Vector2(.17f, 0f), new Vector2(-.12f, -.30f));
        }

        private void Sword(VertexHelper helper, bool mirror)
        {
            float sign = mirror ? -1f : 1f;
            Path(helper, new Vector2(-.23f * sign, -.20f), new Vector2(.30f * sign, .37f),
                new Vector2(.39f * sign, .39f), new Vector2(.37f * sign, .28f), new Vector2(-.17f * sign, -.26f));
            Line(helper, new Vector2(-.33f * sign, -.14f), new Vector2(-.10f * sign, -.35f));
            Line(helper, new Vector2(-.23f * sign, -.25f), new Vector2(-.36f * sign, -.39f));
        }

        private void Circle(VertexHelper helper, Vector2 center, float radius)
        {
            for (int index = 0; index < 20; index++)
            {
                float first = index * Mathf.PI * .1f, second = (index + 1) * Mathf.PI * .1f;
                Line(helper, center + new Vector2(Mathf.Cos(first), Mathf.Sin(first)) * radius,
                    center + new Vector2(Mathf.Cos(second), Mathf.Sin(second)) * radius);
            }
        }

        private void Path(VertexHelper helper, params Vector2[] points)
        { for (int index = 1; index < points.Length; index++) Line(helper, points[index - 1], points[index]); }

        private void Line(VertexHelper helper, Vector2 from, Vector2 to)
        {
            Vector2 a = drawing.center + from * scale, b = drawing.center + to * scale;
            Vector2 direction = (b - a).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * Mathf.Max(.55f, scale * .028f);
            int start = helper.currentVertCount;
            helper.AddVert(a - side, color, Vector2.zero); helper.AddVert(a + side, color, Vector2.zero);
            helper.AddVert(b + side, color, Vector2.zero); helper.AddVert(b - side, color, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
