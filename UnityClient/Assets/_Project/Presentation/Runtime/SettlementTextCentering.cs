using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    [RequireComponent(typeof(Text))]
    [DisallowMultipleComponent]
    public sealed class SettlementTextCentering : BaseMeshEffect
    {
        private bool hasRendered;
        private Matrix4x4 renderedTransform, renderedProjection;
        private Rect renderedPixelRect;

        private void LateUpdate()
        {
            if (!hasRendered || graphic == null || !graphic.IsActive()) return;
            ReadScreenMapping(out Matrix4x4 projection, out Rect pixelRect);
            // Reveal tracks and the final number pulse move without changing
            // Text.text. Their last frame must receive a fresh pixel correction.
            if (transform.localToWorldMatrix != renderedTransform || projection != renderedProjection ||
                pixelRect != renderedPixelRect) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            if (vertices.currentVertCount == 0)
            {
                hasRendered = false;
                return;
            }
            renderedTransform = transform.localToWorldMatrix;
            ReadScreenMapping(out renderedProjection, out renderedPixelRect);
            hasRendered = true;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            Vector3 alignmentVertex = default;
            UIVertex vertex = default;
            for (int start = 0; start + 3 < vertices.currentVertCount; start += 4)
            {
                Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int i = start; i < start + 4; i++)
                {
                    vertices.PopulateUIVertex(ref vertex, i);
                    min = Vector2.Min(min, vertex.position);
                    max = Vector2.Max(max, vertex.position);
                }
                // Spaces and line breaks can have empty quads at the baseline.
                if (max.x - min.x <= .001f || max.y - min.y <= .001f) continue;
                if (float.IsInfinity(bottom))
                {
                    vertices.PopulateUIVertex(ref vertex, start);
                    alignmentVertex = vertex.position;
                }
                bottom = Mathf.Min(bottom, min.y);
                top = Mathf.Max(top, max.y);
            }
            if (float.IsInfinity(bottom))
            {
                hasRendered = false;
                return;
            }
            float offset = ((RectTransform)transform).rect.center.y - (bottom + top) * .5f;
            offset = AlignToScreenPixel(alignmentVertex, offset);
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position.y += offset;
                vertices.SetUIVertex(vertex, i);
            }
        }

        private float AlignToScreenPixel(Vector3 vertex, float offset)
        {
            Canvas canvas = graphic.canvas;
            if (canvas == null) return offset;
            Canvas root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.WorldSpace) return offset;

            // A single vertical translation can preserve the raster grid only
            // for an upright label. Leave rotated, tilted or mirrored text alone.
            Matrix4x4 relative = root.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            Vector3 right = relative.MultiplyVector(Vector3.right);
            Vector3 up = relative.MultiplyVector(Vector3.up);
            if (right.x <= 0f || up.y <= 0f || Mathf.Abs(right.y) > .0001f || Mathf.Abs(right.z) > .0001f ||
                Mathf.Abs(up.x) > .0001f || Mathf.Abs(up.z) > .0001f) return offset;

            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            if (root.renderMode == RenderMode.ScreenSpaceCamera && camera == null) return offset;
            vertex.y += offset;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(vertex));
            Vector2 screenUp = RectTransformUtility.WorldToScreenPoint(camera,
                transform.TransformPoint(vertex + Vector3.up)) - screen;
            if (screenUp.y <= .0001f || Mathf.Abs(screenUp.x) > .001f) return offset;

            // Text's own pixel adjustment happens before this mesh effect (and
            // is disabled on non-pixel-perfect canvases). Snap the final glyph
            // vertex, not the offset: the transform may already have a fraction
            // of a screen pixel in its position. Centering changes by <= .5 px.
            return offset + (Mathf.Round(screen.y) - screen.y) / screenUp.y;
        }

        private void ReadScreenMapping(out Matrix4x4 projection, out Rect pixelRect)
        {
            Canvas root = graphic.canvas == null ? null : graphic.canvas.rootCanvas;
            Camera camera = root != null && root.renderMode == RenderMode.ScreenSpaceCamera ? root.worldCamera : null;
            projection = camera == null ? Matrix4x4.identity : camera.projectionMatrix * camera.worldToCameraMatrix;
            pixelRect = camera != null ? camera.pixelRect : root == null ? Rect.zero : root.pixelRect;
        }
    }
}
