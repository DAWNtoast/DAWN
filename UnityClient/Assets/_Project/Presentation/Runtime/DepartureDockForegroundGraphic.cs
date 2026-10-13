using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Restores the painted controls and near pier above the independent vessel.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DepartureDockForegroundGraphic : MaskableGraphic
    {
        public const float SourceWidth = 2048f;
        public const float SourceHeight = 1152f;
        public const float SourceWaterlineY = 594f;

        // Outlines traced in the textless 4K painting, expressed at 2048 x 1152.
        // The common facility header is drawn by its own unchanged prefab.
        private static readonly Vector2[][] ForegroundPieces =
        {
            // Blank ship nameplate and crew panel remain in front of the vessel.
            Points(1008, 314, 1236, 314, 1258, 339, 1239, 365,
                1007, 365, 984, 339),
            Points(1244, 380, 1519, 380, 1530, 394, 1530, 493,
                1518, 505, 1240, 505, 1230, 494, 1230, 392),
            // Raised chart table rail and its attached heading plaque.
            Points(319, 515, 552, 515, 575, 541, 549, 564, 316, 564, 295, 541),
            Points(0, 538, 847, 530, 866, 542, 883, 594, 0, 594),
            // Near mooring post and rope collar beside the gangway.
            Points(1041, 454, 1078, 454, 1091, 469, 1085, 594, 1024, 594, 1030, 477),
            Points(1031, 470, 1077, 464, 1099, 480, 1091, 490, 1029, 489, 1021, 479),
            Points(1026, 541, 1077, 535, 1090, 550, 1080, 564, 1023, 561, 1014, 551),
            // Gangway deck and the uprights nearest the selected ship.
            Points(1090, 503, 1199, 503, 1280, 594, 1141, 594),
            Points(1195, 457, 1208, 457, 1218, 482, 1223, 528, 1197, 516),
            Points(1278, 526, 1297, 526, 1307, 594, 1278, 594),
            Points(1216, 481, 1286, 529, 1283, 535, 1213, 488),
            // Right hand bollard in front of the berth.
            Points(1627, 471, 1674, 471, 1690, 489, 1684, 594, 1618, 594, 1620, 490),
            Points(1621, 493, 1679, 487, 1692, 506, 1684, 518, 1612, 518, 1609, 504),
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
            Quad(mesh, 0f, SourceWaterlineY, SourceWidth, SourceHeight);
            foreach (Vector2[] outline in ForegroundPieces) ConvexPolygon(mesh, outline);
            Ellipse(mesh, 704f, 355f, 34f, 34f);
            Ellipse(mesh, 1632f, 355f, 35f, 35f);
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

        private void Ellipse(VertexHelper mesh, float x, float y, float radiusX, float radiusY)
        {
            const int segments = 40;
            Rect rect = rectTransform.rect;
            int first = mesh.currentVertCount;
            Add(mesh, rect, x, y);
            for (int index = 0; index < segments; index++)
            {
                float angle = index * (Mathf.PI * 2f / segments);
                Add(mesh, rect, x + Mathf.Cos(angle) * radiusX, y + Mathf.Sin(angle) * radiusY);
            }
            for (int index = 0; index < segments; index++)
                mesh.AddTriangle(first, first + 1 + index, first + 1 + (index + 1) % segments);
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
