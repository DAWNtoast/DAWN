using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Keeps only the fixed chart table and lower preparation area in front of the vessel.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DepartureDockForegroundGraphic : MaskableGraphic
    {
        public const float SourceWidth = 2048f;
        public const float SourceHeight = 1152f;
        public const float SourceWaterlineY = 548f;
        public const float SourceForegroundTopY = 700f;

        // Fixed foreground outlines expressed in the 2048 x 1152 art space.
        // The common facility header is drawn by its own unchanged prefab.
        private static readonly Vector2[][] ForegroundPieces =
        {
            // The chart table is the only raised edge beside the open berth.
            // No ship plaque, arrow, mooring post, rope or gangway crosses it.
            Points(319, 515, 552, 515, 575, 541, 549, 564, 316, 564, 295, 541),
            Points(0, 538, 847, 530, 866, 542, 883, 700, 0, 700),
        };
        [SerializeField] private Texture background;
        public override Texture mainTexture => background != null ? background : Texture2D.whiteTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        public void SetTexture(Texture texture)
        {
            raycastTarget = false;
            if (background == texture) return;
            background = texture;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (background == null) return;
            // Both this mesh and the background use the complete source UVs.
            // The lower painting contains every preparation/departure control.
            Quad(mesh, 0f, SourceForegroundTopY, SourceWidth, SourceHeight);
            foreach (Vector2[] outline in ForegroundPieces) ConvexPolygon(mesh, outline);
        }

        private static Vector2[] Points(params float[] coordinates)
        {
            Vector2[] points = new Vector2[coordinates.Length / 2];
            for (int index = 0; index < points.Length; index++)
                points[index] = new Vector2(coordinates[index * 2], coordinates[index * 2 + 1]);
            return points;
        }

        private void ConvexPolygon(VertexHelper mesh, Vector2[] outline)
        {
            Rect rect = rectTransform.rect;
            int first = mesh.currentVertCount;
            foreach (Vector2 point in outline) Add(mesh, rect, point.x, point.y);
            for (int index = 1; index < outline.Length - 1; index++)
                mesh.AddTriangle(first, first + index, first + index + 1);
        }

        private void Quad(VertexHelper mesh, float left, float top, float right, float bottom)
        {
            Rect rect = rectTransform.rect;
            int first = mesh.currentVertCount;
            Add(mesh, rect, left, top);
            Add(mesh, rect, right, top);
            Add(mesh, rect, right, bottom);
            Add(mesh, rect, left, bottom);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Add(VertexHelper mesh, Rect rect, float x, float y)
        {
            Vector2 uv = new Vector2(x / SourceWidth, 1f - y / SourceHeight);
            mesh.AddVert(rect.min + Vector2.Scale(rect.size, uv), color, uv);
        }
    }
}
