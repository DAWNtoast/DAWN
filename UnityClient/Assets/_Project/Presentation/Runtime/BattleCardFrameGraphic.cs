using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared category frame for cards of any aspect ratio; the centre stays open.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Shattered Sail/Card Type Frame")]
    public sealed class BattleCardFrameGraphic : MaskableGraphic
    {
        [SerializeField] private string cardType = "炮击";
        [SerializeField] private bool available = true;
        public string CardType => cardType;

        public void Configure(string type, bool canPlay)
        {
            raycastTarget = false;
            if (cardType == type && available == canPlay) return;
            cardType = type;
            available = canPlay;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            BattleCardFrameGeometry.Draw(helper, GetPixelAdjustedRect(),
                BattleCardStylePalette.ForType(cardType), available, color);
        }
    }

    // A single UGUI mesh, also appended to the battle card's existing surface.
    // No per-frame updates, textures, materials, child objects or random state.
    internal static class BattleCardFrameGeometry
    {
        public static void Draw(VertexHelper helper, Rect rect, BattleCardPalette palette, bool available, Color tint)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            var pen = new FramePen(helper, rect, available, tint);
            float w = pen.Width, h = pen.Height;
            Color dark = Color.Lerp(palette.Surface, palette.Ink, .65f);
            Color light = Color.Lerp(palette.Accent, Color.white, .16f);
            Color body = Color.Lerp(palette.Surface, palette.Accent, .22f);

            // The silhouette reads as a frame even when illustrations or paper
            // cover its interior. All decorative vertices stay in the margins.
            pen.Quad(0, 0, w, 6, dark);
            pen.Quad(0, h - 8, w, 8, dark);
            pen.Quad(0, 6, 8, h - 14, dark);
            pen.Quad(w - 8, 6, 8, h - 14, dark);
            pen.Quad(1.5f, 1.5f, w - 3, 3, body);
            pen.Quad(1.5f, h - 6.5f, w - 3, 5, body);
            pen.Quad(1.5f, 6, 5, h - 12, body);
            pen.Quad(w - 6.5f, 6, 5, h - 12, body);
            pen.Quad(1, 1, 1, h - 2, light);
            pen.Quad(1, h - 2, w - 2, 1, light);
            pen.Quad(7.5f, 18, .7f, h - 36, palette.Accent);
            pen.Quad(w - 8.2f, 18, .7f, h - 36, palette.Accent);

            switch (palette.Material)
            {
                case BattleCardSurfaceMaterial.Copper:
                    DrawCopper(pen, dark, light, body);
                    break;
                case BattleCardSurfaceMaterial.Sailcloth:
                    DrawRope(pen, dark, light, palette.Parchment);
                    break;
                case BattleCardSurfaceMaterial.Leather:
                    DrawLeather(pen, dark, light, palette.Parchment);
                    break;
                case BattleCardSurfaceMaterial.Timber:
                    DrawTimber(pen, dark, light, body);
                    break;
                case BattleCardSurfaceMaterial.Steel:
                    DrawSteel(pen, dark, light, body);
                    break;
                case BattleCardSurfaceMaterial.Chart:
                    DrawChart(pen, dark, light, palette.Accent);
                    break;
            }
        }

        private static void DrawCopper(FramePen p, Color dark, Color light, Color body)
        {
            // Repeated cast-metal plates with round cannon-bronze rivets.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 0 : p.Width - 8;
                for (float y = 24; y <= p.Height - 24; y += 38)
                {
                    p.Quad(x + 1, y - 11, 6, 22, body);
                    p.Quad(x + 1, y + 11, 6, 1.2f, light);
                    p.Quad(x + 1, y - 12, 6, 1.2f, dark);
                    p.Disc(x + 4, y, 3.2f, dark);
                    p.Disc(x + 3.7f, y + .3f, 2.1f, light);
                }
            }
            for (int corner = 0; corner < 4; corner++)
            {
                p.CornerL(corner, 18, 6, light);
                p.CornerL(corner, 16, 3, body);
                p.CornerDiamond(corner, 5, 5, 2, 2, dark);
            }
            for (float x = 26; x < p.Width - 20; x += 24)
                p.Quad(x, p.Height - 6, 12, 2, light);
        }

        private static void DrawRope(FramePen p, Color dark, Color light, Color thread)
        {
            // Two interwoven strands; widely spaced canvas stitches along the cap.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 4 : p.Width - 4;
                for (float y = 20; y + 12 < p.Height - 18; y += 14)
                {
                    p.Line(x - 2, y, x + 2, y + 6, 2, thread);
                    p.Line(x + 2, y + 6, x - 2, y + 12, 2, light);
                    p.Line(x + 2, y, x - 2, y + 6, 1.1f, dark);
                    p.Line(x - 2, y + 6, x + 2, y + 12, 1.1f, dark);
                }
            }
            for (float x = 22; x < p.Width - 20; x += 12)
                p.Line(x, p.Height - 6, x + 5, p.Height - 3, 1.4f, thread);
            for (int corner = 0; corner < 4; corner++)
            {
                p.CornerL(corner, 18, 5, thread);
                p.CornerL(corner, 17, 2, light);
                p.CornerLine(corner, 3, 12, 12, 3, 2, dark);
                p.CornerLine(corner, 5, 14, 14, 5, 1.3f, thread);
            }
        }

        private static void DrawLeather(FramePen p, Color dark, Color light, Color thread)
        {
            // Heavy leather binding and unmistakable crossed lacing.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 4 : p.Width - 4;
                p.Quad(x - 3, 18, 6, p.Height - 36, dark);
                for (float y = 22; y + 7 < p.Height - 18; y += 14)
                {
                    p.Line(x - 2, y, x + 2, y + 7, 1.3f, thread);
                    p.Line(x + 2, y, x - 2, y + 7, 1.3f, light);
                }
            }
            for (int corner = 0; corner < 4; corner++)
            {
                p.CornerL(corner, 18, 6, dark);
                p.CornerLine(corner, 2, 14, 14, 2, 4, light);
                p.CornerLine(corner, 3, 14, 14, 3, 1.5f, dark);
                p.CornerDiamond(corner, 4, 4, 2, 2, thread);
            }
            for (float x = 24; x + 5 < p.Width - 18; x += 14)
            {
                p.Line(x, p.Height - 6, x + 5, p.Height - 3, 1, thread);
                p.Line(x, p.Height - 3, x + 5, p.Height - 6, 1, thread);
            }
        }

        private static void DrawTimber(FramePen p, Color dark, Color light, Color body)
        {
            // Long-grain timbers, oval knots and repair straps rather than rivets.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 4 : p.Width - 4;
                p.Line(x - 1.8f, 18, x - .8f, p.Height - 18, .9f, dark);
                p.Line(x + 1, 18, x + 2, p.Height - 18, .8f, light);
                for (float y = 33; y < p.Height - 22; y += 49)
                {
                    p.Diamond(x, y, 1.8f, 6, dark);
                    p.Diamond(x, y, .6f, 3, body);
                }
                for (int band = 1; band < 4; band++)
                {
                    float y = p.Height * band / 4;
                    p.Quad(x - 4, y - 3, 8, 6, dark);
                    p.Quad(x - 4, y, 8, 2, light);
                }
            }
            p.Line(20, p.Height - 5, p.Width - 20, p.Height - 4, .8f, dark);
            for (int corner = 0; corner < 4; corner++)
            {
                p.CornerL(corner, 18, 5, light);
                p.CornerL(corner, 16, 2, dark);
                p.CornerDiamond(corner, 12, 3.5f, 1.5f, 1.5f, dark);
                p.CornerDiamond(corner, 3.5f, 12, 1.5f, 1.5f, dark);
            }
        }

        private static void DrawSteel(FramePen p, Color dark, Color light, Color body)
        {
            // Overlapping armour scales and pointed shield clasps on the rails.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 4 : p.Width - 4;
                for (float y = 25; y + 12 < p.Height - 18; y += 28)
                {
                    p.Quad(x - 3, y - 5, 6, 13, body);
                    p.Line(x - 3, y - 5, x, y - 9, 1.8f, light);
                    p.Line(x, y - 9, x + 3, y - 5, 1.8f, light);
                    p.Quad(x - 3, y + 8, 6, 1.2f, dark);
                    p.Diamond(x, y + 3, 1.4f, 1.4f, light);
                }
            }
            for (int corner = 0; corner < 4; corner++)
            {
                p.CornerL(corner, 18, 6, light);
                p.CornerL(corner, 16, 3, body);
                p.CornerLine(corner, 6, 14, 14, 6, 2, light);
                p.CornerDiamond(corner, 4, 4, 2.2f, 2.2f, dark);
            }
            for (float x = 24; x < p.Width - 18; x += 24)
                p.Diamond(x, p.Height - 4.5f, 5, 2, light);
        }

        private static void DrawChart(FramePen p, Color dark, Color light, Color brass)
        {
            // Navigational rules and elongated compass roses give strategy cards
            // their own geometry, including when viewed without category colours.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 4 : p.Width - 4;
                for (float y = 22; y < p.Height - 18; y += 12)
                {
                    p.Quad(x - 3, y, 6, .8f, brass);
                    p.Quad(x - 1.5f, y + 5, 3, .7f, brass);
                }
                foreach (float y in new[] { p.Height * .28f, p.Height * .72f })
                {
                    p.Quad(x - 3.8f, y - 10, 7.6f, 20, dark);
                    p.Diamond(x, y, 3.7f, 9, brass);
                    p.Diamond(x, y, 1.6f, 5, dark);
                    p.Line(x - 3.2f, y, x + 3.2f, y, .8f, light);
                }
            }
            for (int corner = 0; corner < 4; corner++)
            {
                p.CornerL(corner, 18, 1.5f, brass);
                p.CornerLine(corner, 4, 15, 4, 4, 1, light);
                p.CornerLine(corner, 4, 4, 15, 4, 1, light);
                p.CornerDiamond(corner, 8, 8, 3, 3, brass);
                p.CornerDiamond(corner, 8, 8, 1.4f, 1.4f, dark);
            }
            for (float x = 24; x < p.Width - 20; x += 10)
                p.Quad(x, p.Height - 6, .8f, 3.5f, brass);
        }

        private readonly struct FramePen
        {
            private readonly VertexHelper mesh;
            private readonly Rect rect;
            private readonly bool available;
            private readonly Color tint;
            private readonly float unit;
            public float Width => rect.width / unit;
            public float Height => rect.height / unit;

            public FramePen(VertexHelper mesh, Rect rect, bool available, Color tint)
            {
                this.mesh = mesh; this.rect = rect; this.available = available; this.tint = tint;
                unit = Mathf.Min(rect.width / 202f, rect.height / 220f);
            }

            public void Quad(float x, float y, float width, float height, Color color)
            {
                Four(new Vector2(x, y), new Vector2(x, y + height),
                    new Vector2(x + width, y + height), new Vector2(x + width, y), color);
            }

            public void Diamond(float x, float y, float width, float height, Color color)
            {
                Four(new Vector2(x - width, y), new Vector2(x, y + height),
                    new Vector2(x + width, y), new Vector2(x, y - height), color);
            }

            public void Line(float x1, float y1, float x2, float y2, float width, Color color)
            {
                Vector2 a = new Vector2(x1, y1), b = new Vector2(x2, y2);
                Vector2 delta = b - a;
                Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
                Four(a - normal, a + normal, b + normal, b - normal, color);
            }

            public void Disc(float x, float y, float radius, Color color)
            {
                int start = mesh.currentVertCount;
                Vertex(new Vector2(x, y), color);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    Vertex(new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius), color);
                }
                for (int i = 0; i < 8; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
            }

            public void CornerL(int corner, float length, float width, Color color)
            {
                Four(Corner(corner, 0, 0), Corner(corner, 0, width),
                    Corner(corner, length, width), Corner(corner, length, 0), color);
                Four(Corner(corner, 0, width), Corner(corner, 0, length),
                    Corner(corner, width, length), Corner(corner, width, width), color);
            }

            public void CornerLine(int corner, float x1, float y1, float x2, float y2, float width, Color color)
            {
                Vector2 a = Corner(corner, x1, y1), b = Corner(corner, x2, y2);
                Line(a.x, a.y, b.x, b.y, width, color);
            }

            public void CornerDiamond(int corner, float x, float y, float width, float height, Color color)
            {
                Vector2 centre = Corner(corner, x, y);
                Diamond(centre.x, centre.y, width, height, color);
            }

            private Vector2 Corner(int corner, float x, float y) =>
                new Vector2((corner & 1) == 0 ? x : Width - x, corner < 2 ? y : Height - y);

            private void Four(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
            {
                int first = mesh.currentVertCount;
                Vertex(a, color); Vertex(b, color); Vertex(c, color); Vertex(d, color);
                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first, first + 2, first + 3);
            }

            private void Vertex(Vector2 point, Color color)
            {
                if (!available) color = Color.Lerp(color, new Color(color.grayscale, color.grayscale, color.grayscale, 1), .3f) * .88f;
                color.a = 1;
                mesh.AddVert(new Vector3(rect.xMin + point.x * unit, rect.yMin + point.y * unit, 0), color * tint, Vector2.zero);
            }
        }
    }
}
