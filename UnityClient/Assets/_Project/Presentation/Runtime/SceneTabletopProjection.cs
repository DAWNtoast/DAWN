using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Projects a UI paper surface into the room's receding table plane.
    /// Vertices and pointer hit-testing use the same trapezoid; the bitmap is unchanged.</summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class SceneTabletopProjection : BaseMeshEffect, ICanvasRaycastFilter
    {
        public const float BackLeft = 0.23f;
        public const float BackRight = 0.86f;

        public static Vector2 ProjectNormalized(Vector2 uv)
        {
            return new Vector2(Mathf.Lerp(0f, BackLeft, uv.y) +
                uv.x * Mathf.Lerp(1f, BackRight - BackLeft, uv.y), uv.y);
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            Rect rect = graphic.rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            UIVertex vertex = new UIVertex();
            for (int index = 0; index < mesh.currentVertCount; index++)
            {
                mesh.PopulateUIVertex(ref vertex, index);
                Vector2 normalized = new Vector2((vertex.position.x - rect.xMin) / rect.width,
                    (vertex.position.y - rect.yMin) / rect.height);
                Vector2 projected = ProjectNormalized(normalized);
                vertex.position = new Vector3(rect.xMin + projected.x * rect.width,
                    rect.yMin + projected.y * rect.height, vertex.position.z);
                mesh.SetUIVertex(vertex, index);
            }
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            RectTransform rectTransform = graphic.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 point)) return false;
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            float y = (point.y - rect.yMin) / rect.height;
            float x = (point.x - rect.xMin) / rect.width;
            return y >= 0f && y <= 1f && x >= Mathf.Lerp(0f, BackLeft, y) && x <= Mathf.Lerp(1f, BackRight, y);
        }
    }
}
