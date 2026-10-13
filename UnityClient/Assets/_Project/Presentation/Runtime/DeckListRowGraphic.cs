using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Preserves the authored end ornaments while the middle fits the list's exact clear width.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DeckListRowGraphic : MaskableGraphic
    {
        [SerializeField] private string assetId = "neutral";
        [SerializeField] private Texture2D authoredTexture;
        public string SchoolId => CardSchoolArtCatalog.NormalizeSchool(assetId);
        public bool HasAuthoredTexture => authoredTexture != null;
        public override Texture mainTexture => authoredTexture != null ? authoredTexture : Texture2D.whiteTexture;

        public void Configure(string id)
        {
            assetId = id == "outer" ? id : CardSchoolArtCatalog.NormalizeSchool(id);
            authoredTexture = DeckListArtCatalog.Texture(assetId);
            raycastTarget = false;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            DeckListArtCatalog.Layout layout = DeckListArtCatalog.Bounds(assetId);
            if (authoredTexture == null || layout == null) return;
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            bool outer = assetId == "outer";
            float left = outer ? 8f : 54f, right = outer ? 18f : 42f;
            float[] x = { rect.xMin, rect.xMin + left, rect.xMax - right, rect.xMax };
            float[] u = { layout.x, layout.x + layout.width * layout.left,
                layout.x + layout.width * (1f - layout.right), layout.x + layout.width };
            if (!outer)
            {
                for (int i = 0; i < 3; i++) Quad(mesh, Rect.MinMaxRect(x[i], rect.yMin, x[i+1], rect.yMax),
                    Rect.MinMaxRect(u[i], layout.y, u[i+1], layout.y + layout.height), color);
                return;
            }
            // These exact inner edges match the 306 x 640 viewport inside a 332 x 718 frame.
            float[] y = { rect.yMin, rect.yMin + 20f, rect.yMax - 58f, rect.yMax };
            float[] v = { layout.y, layout.y + layout.height * layout.bottom,
                layout.y + layout.height * (1f - layout.top), layout.y + layout.height };
            for (int row = 0; row < 3; row++) for (int col = 0; col < 3; col++)
                Quad(mesh, Rect.MinMaxRect(x[col], y[row], x[col+1], y[row+1]),
                    Rect.MinMaxRect(u[col], v[row], u[col+1], v[row+1]), color);
        }

        private static void Quad(VertexHelper mesh, Rect box, Rect uv, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(box.xMin, box.yMin), tint, new Vector2(uv.xMin, uv.yMin));
            mesh.AddVert(new Vector3(box.xMin, box.yMax), tint, new Vector2(uv.xMin, uv.yMax));
            mesh.AddVert(new Vector3(box.xMax, box.yMax), tint, new Vector2(uv.xMax, uv.yMax));
            mesh.AddVert(new Vector3(box.xMax, box.yMin), tint, new Vector2(uv.xMax, uv.yMin));
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
