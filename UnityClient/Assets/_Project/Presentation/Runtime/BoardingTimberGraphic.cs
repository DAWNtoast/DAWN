using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BoardingTimberKind { Deck, Beam, Gangplank, Rope, FootShadow }

    /// <summary>Small textured geometry for the deck camera; never an opaque UI panel.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BoardingTimberGraphic : MaskableGraphic
    {
        private BoardingTimberKind kind;
        private Color tone = Color.white;
        public override Texture mainTexture => kind == BoardingTimberKind.FootShadow || WoodButtonSkin.Face == null
            ? Texture2D.whiteTexture : WoodButtonSkin.Face.texture;

        public void Configure(BoardingTimberKind surfaceKind, Color tint)
        {
            kind = surfaceKind;
            tone = tint;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (kind == BoardingTimberKind.FootShadow)
            {
                mesh.AddVert(Vector3.zero, new Color(0.04f, 0.025f, 0.012f, 0.40f), Vector2.zero);
                for (int point = 0; point <= 20; point++)
                {
                    float angle = point * Mathf.PI * 0.1f;
                    mesh.AddVert(new Vector2(Mathf.Cos(angle) * r.width * 0.5f, Mathf.Sin(angle) * r.height * 0.5f), Color.clear, Vector2.zero);
                    if (point > 0) mesh.AddTriangle(0, point, point + 1);
                }
                return;
            }
            if (kind == BoardingTimberKind.Rope)
            {
                for (int twist = 0; twist < 24; twist++)
                {
                    float x = Mathf.Lerp(r.xMin, r.xMax, twist / 24f);
                    float next = Mathf.Lerp(r.xMin, r.xMax, (twist + 1f) / 24f);
                    Strip(mesh, new Vector2(x, -1f), new Vector2(next, 1f), r.height * 0.30f,
                        twist % 2 == 0 ? new Color(0.86f, 0.70f, 0.43f) : new Color(0.58f, 0.43f, 0.24f), true);
                }
                return;
            }
            if (kind == BoardingTimberKind.Beam)
            {
                bool vertical = r.height > r.width;
                Quad(mesh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax - 1f, r.yMax), new Vector2(r.xMin + 1f, r.yMax),
                    tone, new Rect(0.20f, 0.37f, vertical ? 0.022f : 0.45f, vertical ? 0.17f : 0.025f));
                Strip(mesh, new Vector2(r.xMin + 1f, r.yMax - 1f), new Vector2(r.xMax - 1f, r.yMax - 1f), 0.8f, new Color(0.69f, 0.52f, 0.30f), true);
                Strip(mesh, new Vector2(r.xMin, r.yMin + 1f), new Vector2(r.xMax, r.yMin + 1f), 1.2f, new Color(0.14f, 0.095f, 0.055f), false);
                if (r.width > 40f)
                {
                    Nail(mesh, new Vector2(r.xMin + 9f, 0f), 1.9f);
                    Nail(mesh, new Vector2(r.xMax - 9f, 0f), 1.9f);
                }
                return;
            }

            bool deck = kind == BoardingTimberKind.Deck;
            int boards = deck ? 7 : 10;
            for (int board = 0; board < boards; board++)
            {
                float first = board / (float)boards;
                float last = (board + 1f) / boards;
                Vector2 a, b, c, d;
                if (deck)
                {
                    float lowerInset = r.width * 0.085f * first;
                    float upperInset = r.width * 0.085f * last;
                    a = new Vector2(r.xMin + lowerInset, Mathf.Lerp(r.yMin, r.yMax, first));
                    b = new Vector2(r.xMax - lowerInset, a.y);
                    d = new Vector2(r.xMin + upperInset, Mathf.Lerp(r.yMin, r.yMax, last) - 0.8f);
                    c = new Vector2(r.xMax - upperInset, d.y);
                }
                else
                {
                    a = new Vector2(Mathf.Lerp(r.xMin, r.xMax, first), r.yMin);
                    b = new Vector2(Mathf.Lerp(r.xMin, r.xMax, last) - 0.7f, r.yMin);
                    c = new Vector2(b.x, r.yMax);
                    d = new Vector2(a.x, r.yMax);
                }
                float shade = 0.91f + (board % 3) * 0.045f;
                Quad(mesh, a, b, c, d, tone * new Color(shade, shade, shade, 1f),
                    new Rect(0.14f + (board % 2) * 0.07f, 0.17f + board * 0.052f, deck ? 0.54f : 0.035f, deck ? 0.032f : 0.14f));
                Strip(mesh, a, b, 0.65f, new Color(0.25f, 0.15f, 0.075f), false);
                Strip(mesh, d, c, 0.45f, new Color(0.75f, 0.57f, 0.32f), true);
                // Small bright/dark edges remain legible at the same pixel scale
                // as the painted hull's cannon ports, unlike stretched flat bars.
                Nail(mesh, Vector2.Lerp(a, d, 0.5f) + Vector2.right * (deck ? 11f : 4f), 1.35f);
                Nail(mesh, Vector2.Lerp(b, c, 0.5f) - Vector2.right * (deck ? 11f : 4f), 1.35f);
                if (deck)
                {
                    for (int grain = 0; grain < 2; grain++)
                    {
                        float u = 0.12f + grain * 0.43f + (board % 3) * 0.03f;
                        Vector2 start = Vector2.Lerp(Vector2.Lerp(a, d, 0.30f), Vector2.Lerp(b, c, 0.30f), u);
                        Strip(mesh, start, start + new Vector2(25f + board * 3f, 0.7f), 0.30f, new Color(0.53f, 0.37f, 0.20f), true);
                    }
                    float joint = r.xMin + r.width * (board % 2 == 0 ? 0.31f : 0.67f);
                    Strip(mesh, new Vector2(joint, a.y + 1f), new Vector2(joint, d.y - 1f), 0.6f, new Color(0.12f, 0.075f, 0.04f), false);
                }
            }
            // Thick front edge tucks into the painted hull instead of extending
            // across it as a floating platform; it is only six screen pixels.
            Quad(mesh, new Vector2(r.xMin, r.yMin - 6f), new Vector2(r.xMax, r.yMin - 6f), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMin),
                new Color(0.53f, 0.42f, 0.28f), new Rect(0.14f, 0.4f, 0.64f, 0.012f));
        }

        private void Nail(VertexHelper mesh, Vector2 center, float radius)
        {
            int first = mesh.currentVertCount;
            Vector2 metal = new Vector2(0.021f, 0.953f);
            for (int n = 0; n < 6; n++)
            {
                float angle = n * Mathf.PI / 3f;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    n < 3 ? new Color(0.86f, 0.79f, 0.62f) : new Color(0.32f, 0.26f, 0.16f), metal);
            }
            for (int n = 1; n < 5; n++) mesh.AddTriangle(first, first + n, first + n + 1);
        }

        private void Strip(VertexHelper mesh, Vector2 a, Vector2 b, float halfWidth, Color tint, bool bright)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * halfWidth;
            Rect uv = bright ? new Rect(0.025f, 0.95f, 0.004f, 0.006f) : new Rect(0.32f, 0.42f, 0.002f, 0.002f);
            Quad(mesh, a - normal, b - normal, b + normal, a + normal, tint, uv);
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint, Rect uv)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, tint, new Vector2(uv.xMin, uv.yMin));
            mesh.AddVert(b, tint, new Vector2(uv.xMax, uv.yMin));
            mesh.AddVert(c, tint, new Vector2(uv.xMax, uv.yMax));
            mesh.AddVert(d, tint, new Vector2(uv.xMin, uv.yMax));
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
