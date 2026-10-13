using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ProhibitionMarkGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            if (size <= 0f) return;
            Vector2 center = rect.center;
            float radius = size * .45f;
            float thickness = size * .044f;
            Color shadow = new Color(.26f, .055f, .035f, color.a * .8f);
            DrawRing(mesh, center, radius, thickness + 3f, shadow);
            DrawRing(mesh, center, radius, thickness, color);
            Vector2 diagonal = new Vector2(1f, -1f).normalized * (radius - thickness * .4f);
            DrawLine(mesh, center - diagonal, center + diagonal, thickness + 3f, shadow);
            DrawLine(mesh, center - diagonal, center + diagonal, thickness, color);
        }

        private static void DrawRing(VertexHelper mesh, Vector2 center, float radius, float thickness, Color tint)
        {
            const int segments = 96;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 start = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 end = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(mesh, center + start * (radius - thickness * .5f), center + start * (radius + thickness * .5f),
                    center + end * (radius + thickness * .5f), center + end * (radius - thickness * .5f), tint);
            }
        }

        private static void DrawLine(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color tint)
        {
            Vector2 delta = b - a;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (thickness * .5f);
            Quad(mesh, a - normal, a + normal, b + normal, b - normal, tint);
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
