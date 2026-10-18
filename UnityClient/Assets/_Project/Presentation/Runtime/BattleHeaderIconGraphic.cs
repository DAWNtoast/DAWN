using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Brass silhouettes for the battle header, independent of font glyph support.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleHeaderIconGraphic : MaskableGraphic
    {
        [SerializeField] private bool settings;

        public void Configure(bool settingsIcon, Color accent)
        {
            settings = settingsIcon;
            color = Color.Lerp(accent, new Color(1f, .93f, .76f, 1f), .45f);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            float scale = Mathf.Min(rect.width, rect.height) / 34f;
            Vector2 center = rect.center;
            if (settings) Gear(mesh, center, scale);
            else Cards(mesh, center, scale);
        }

        private void Gear(VertexHelper mesh, Vector2 center, float scale)
        {
            const int segments = 64;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                float next = (index + 1) * Mathf.PI * 2f / segments;
                float radius = index % 8 >= 2 && index % 8 <= 5 ? 16f : 12.5f;
                Quad(mesh, center + Polar(angle, 7f * scale),
                    center + Polar(angle, radius * scale),
                    center + Polar(next, radius * scale),
                    center + Polar(next, 7f * scale), color);
            }
        }

        private void Cards(VertexHelper mesh, Vector2 center, float scale)
        {
            // Three offset cards make the three battle decks readable at header scale.
            OutlineCard(mesh, center + new Vector2(-5f, 4f) * scale, 20f * scale, 24f * scale);
            OutlineCard(mesh, center + new Vector2(-1f, 0f) * scale, 20f * scale, 24f * scale);
            OutlineCard(mesh, center + new Vector2(4f, -4f) * scale, 20f * scale, 24f * scale);
            Vector2 mark = center + new Vector2(4f, -4f) * scale;
            Quad(mesh, mark + Vector2.up * 5f * scale, mark + Vector2.right * 3.5f * scale,
                mark + Vector2.down * 5f * scale, mark + Vector2.left * 3.5f * scale, color);
        }

        private void OutlineCard(VertexHelper mesh, Vector2 center, float width, float height)
        {
            float line = width * .095f;
            float left = center.x - width * .5f, bottom = center.y - height * .5f;
            Fill(mesh, new Rect(left, bottom, width, height), new Color(.10f, .075f, .045f, 1f));
            Fill(mesh, new Rect(left, bottom, line, height), color);
            Fill(mesh, new Rect(left + width - line, bottom, line, height), color);
            Fill(mesh, new Rect(left, bottom, width, line), color);
            Fill(mesh, new Rect(left, bottom + height - line, width, line), color);
        }

        private static Vector2 Polar(float angle, float radius) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        private static void Fill(VertexHelper mesh, Rect rect, Color tint) => Quad(mesh,
            new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
            new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), tint);

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
