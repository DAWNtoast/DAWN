using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A solid carved wooden arrow; only its silhouette is drawn.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WoodenPageArrowGraphic : MaskableGraphic
    {
        [SerializeField] private bool forward = true;
        [SerializeField] private Texture2D woodTexture;

        private static readonly Vector2[] Silhouette =
        {
            new Vector2(-.5f, -.17f), new Vector2(-.12f, -.17f),
            new Vector2(-.12f, -.5f), new Vector2(.5f, 0f),
            new Vector2(-.12f, .5f), new Vector2(-.12f, .17f),
            new Vector2(-.5f, .17f),
        };
        private static readonly Vector2 EdgeUv = new Vector2(.025f, .95f);

        public bool Forward => forward;
        public override Texture mainTexture => woodTexture != null ? woodTexture : Texture2D.whiteTexture;

        public void Configure(bool pointsRight)
        {
            forward = pointsRight;
            woodTexture = WoodButtonSkin.Face != null ? WoodButtonSkin.Face.texture : null;
            color = Color.white;
            raycastTarget = true;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 4f || rect.height <= 8f) return;
            Vector2 size = new Vector2(rect.width - 4f, Mathf.Min(56f, rect.height - 8f));
            Vector2 center = rect.center;
            // A solid lower edge gives the wooden block thickness without a plate or translucent shadow.
            Fill(mesh, center + Vector2.down * 2f, size, 1f, new Color(.32f, .19f, .09f, 1f), false);
            Fill(mesh, center, size, 1f, new Color(.65f, .43f, .22f, 1f), false);
            Fill(mesh, center, size, .84f, Color.white, true);
            for (int index = 0; index < Silhouette.Length; index++)
            {
                Vector2 first = Silhouette[index];
                Vector2 next = Silhouette[(index + 1) % Silhouette.Length];
                Color edge = first.y + next.y > 0f
                    ? new Color(.85f, .64f, .38f, 1f) : new Color(.40f, .25f, .12f, 1f);
                Bevel(mesh, Point(first, center, size, 1f), Point(next, center, size, 1f),
                    Point(next, center, size, .84f), Point(first, center, size, .84f), edge);
            }
        }

        private Vector2 Point(Vector2 normalized, Vector2 center, Vector2 size, float scale)
        {
            normalized.x *= forward ? 1f : -1f;
            return center + Vector2.Scale(normalized, size) * scale;
        }

        private void Fill(VertexHelper mesh, Vector2 center, Vector2 size, float scale, Color tint, bool grain)
        {
            int start = mesh.currentVertCount;
            Color opaque = color * tint;
            mesh.AddVert(center, opaque, grain ? new Vector2(.5f, .5f) : EdgeUv);
            foreach (Vector2 normalized in Silhouette)
            {
                Vector2 uv = grain ? new Vector2(.5f + normalized.x * .68f, .5f + normalized.y * .4f) : EdgeUv;
                mesh.AddVert(Point(normalized, center, size, scale), opaque, uv);
            }
            for (int index = 0; index < Silhouette.Length; index++)
                mesh.AddTriangle(start, start + 1 + index, start + 1 + (index + 1) % Silhouette.Length);
        }

        private void Bevel(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = mesh.currentVertCount;
            Color shaded = color * tint;
            mesh.AddVert(a, shaded, EdgeUv);
            mesh.AddVert(b, shaded, EdgeUv);
            mesh.AddVert(c, shaded, EdgeUv);
            mesh.AddVert(d, shaded, EdgeUv);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
