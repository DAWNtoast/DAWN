using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Warps existing UI artwork onto a convex tabletop quad without modifying the texture.</summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class TabletopQuadProjection : BaseMeshEffect, ICanvasRaycastFilter
    {
        [SerializeField] private Vector2 bottomLeft = Vector2.zero;
        [SerializeField] private Vector2 bottomRight = Vector2.right;
        [SerializeField] private Vector2 topLeft = Vector2.up;
        [SerializeField] private Vector2 topRight = Vector2.one;

        public void Configure(Vector2 nearLeft, Vector2 nearRight, Vector2 farLeft, Vector2 farRight)
        {
            bottomLeft = nearLeft;
            bottomRight = nearRight;
            topLeft = farLeft;
            topRight = farRight;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public Vector2 Project(Vector2 uv)
        {
            return Vector2.LerpUnclamped(Vector2.LerpUnclamped(bottomLeft, bottomRight, uv.x),
                Vector2.LerpUnclamped(topLeft, topRight, uv.x), uv.y);
        }

        public bool Contains(Vector2 point)
        {
            return Cross(bottomRight - bottomLeft, point - bottomLeft) >= -0.00001f &&
                Cross(topRight - bottomRight, point - bottomRight) >= -0.00001f &&
                Cross(topLeft - topRight, point - topRight) >= -0.00001f &&
                Cross(bottomLeft - topLeft, point - topLeft) >= -0.00001f;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            Rect rect = graphic.rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            if (graphic is RawImage raw)
            {
                // Subdivision avoids the diagonal UV kink from stretching only two triangles.
                const int segments = 12;
                mesh.Clear();
                Rect textureUv = raw.uvRect;
                for (int y = 0; y <= segments; y++)
                    for (int x = 0; x <= segments; x++)
                    {
                        Vector2 uv = new Vector2(x / (float)segments, y / (float)segments);
                        Vector2 p = Project(uv);
                        mesh.AddVert(new Vector3(rect.xMin + p.x * rect.width, rect.yMin + p.y * rect.height, 0f),
                            graphic.color, new Vector2(textureUv.x + uv.x * textureUv.width, textureUv.y + uv.y * textureUv.height));
                    }
                for (int y = 0; y < segments; y++)
                    for (int x = 0; x < segments; x++)
                    {
                        int a = y * (segments + 1) + x;
                        mesh.AddTriangle(a, a + segments + 1, a + 1);
                        mesh.AddTriangle(a + 1, a + segments + 1, a + segments + 2);
                    }
                return;
            }
            // A UI Image (the low, tabletop hover glow) retains its original UVs and sprite.
            UIVertex vertex = new UIVertex();
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Vector2 p = Project(new Vector2((vertex.position.x - rect.xMin) / rect.width,
                    (vertex.position.y - rect.yMin) / rect.height));
                vertex.position = new Vector3(rect.xMin + p.x * rect.width, rect.yMin + p.y * rect.height, vertex.position.z);
                mesh.SetUIVertex(vertex, i);
            }
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!IsActive()) return true;
            RectTransform rt = graphic.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCamera, out Vector2 local)) return false;
            Rect rect = rt.rect;
            return rect.width > 0f && rect.height > 0f &&
                Contains(new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height));
        }
    }
}
