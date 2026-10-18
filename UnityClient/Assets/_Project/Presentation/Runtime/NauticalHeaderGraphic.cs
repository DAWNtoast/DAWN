using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Resolution-independent brass rules over the regional painted timber.</summary>
    [DisallowMultipleComponent]
    public sealed class NauticalHeaderGraphic : MaskableGraphic
    {
        [SerializeField] private Color accent = new Color(.82f, .65f, .39f, 1f);
        [SerializeField] private bool inset;

        public void Configure(Color value, bool panel = false)
        {
            accent = value;
            inset = panel;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            if (inset)
            {
                Quad(mesh, r, new Color(.055f, .066f, .063f, .84f), new Color(.11f, .12f, .10f, .65f));
                Rule(mesh, r.xMin, r.yMin, r.width, 1, Tint(.26f));
                Rule(mesh, r.xMin, r.yMax - 1, r.width, 1, Tint(.12f));
                Rule(mesh, r.xMin, r.yMin, 2, r.height, Tint(.5f));
                return;
            }
            // The translucent stain retains each port's actual painted material.
            Quad(mesh, r, new Color(.045f, .044f, .037f, .91f), new Color(.085f, .078f, .058f, .78f));
            Rule(mesh, r.xMin, r.yMax - 2, r.width, 2, Tint(.36f));
            Rule(mesh, r.xMin, r.yMin + 3, r.width, 2, Tint(.8f));
            Rule(mesh, r.xMin, r.yMin, r.width, 3, new Color(.02f, .025f, .02f, 1));
            Rule(mesh, r.xMin + 18, r.yMin + 8, r.width - 36, 1, Tint(.18f));
            foreach (float x in new[] { r.xMin + 14, r.xMax - 14 })
            {
                Rule(mesh, x, r.yMin + 18, 1, r.height - 36, Tint(.35f));
                Diamond(mesh, new Vector2(x + .5f, r.yMin + 12), 3, Tint(.75f));
                Diamond(mesh, new Vector2(x + .5f, r.yMax - 12), 3, Tint(.75f));
            }
        }

        private Color Tint(float alpha) => new Color(accent.r, accent.g, accent.b, alpha);
        private static void Rule(VertexHelper mesh, float x, float y, float w, float h, Color c)
            => Quad(mesh, new Rect(x, y, w, h), c, c);
        private static void Quad(VertexHelper mesh, Rect r, Color bottom, Color top)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(new Vector3(r.xMin, r.yMin), bottom, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMin, r.yMax), top, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax, r.yMax), top, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax, r.yMin), bottom, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
            mesh.AddTriangle(i, i + 2, i + 3);
        }
        private static void Diamond(VertexHelper mesh, Vector2 center, float radius, Color c)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(center + Vector2.up * radius, c, Vector2.zero);
            mesh.AddVert(center + Vector2.right * radius, c, Vector2.zero);
            mesh.AddVert(center + Vector2.down * radius, c, Vector2.zero);
            mesh.AddVert(center + Vector2.left * radius, c, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
            mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
