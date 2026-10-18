using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Small water-only UV motion. The painted sky, horizon and image edges stay fixed.</summary>
    public sealed class NavigationLookoutOceanGraphic : RawImage
    {
        private float motionTime;
        public void SetMotionTime(float value)
        {
            if (Mathf.Approximately(motionTime, value)) return;
            motionTime = value;
            if (color.a > .001f) SetVerticesDirty();
        }

        public static Vector2 WaterUvOffset(Vector2 uv, Rect crop, float seconds)
        {
            const float horizon = .612f;
            if (uv.y >= horizon || crop.width <= 0f || crop.height <= 0f) return Vector2.zero;
            float depth = Mathf.Clamp01((horizon - uv.y) / Mathf.Max(.01f, horizon - crop.yMin));
            float edge = Mathf.Clamp01(Mathf.Min(uv.x - crop.xMin, crop.xMax - uv.x) / .025f) *
                Mathf.Clamp01((uv.y - crop.yMin) / .025f);
            float weight = depth * depth * edge;
            return new Vector2(Mathf.Sin(seconds * 1.35f + uv.y * 73f) * .0019f * weight,
                Mathf.Sin(seconds * 1.08f + uv.x * 35f + uv.y * 51f) * .00075f * weight);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            Rect crop = uvRect;
            const int columns = 24, rows = 18;
            float horizon = Mathf.Clamp01((.612f - crop.y) / Mathf.Max(.0001f, crop.height));
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            for (int y = 0; y <= rows; y++)
            {
                // An explicit row pins the authored horizon even with a vertical cover crop.
                float fy = y <= 12 ? horizon * y / 12f : horizon + (1f - horizon) * (y - 12) / 6f;
                for (int x = 0; x <= columns; x++)
                {
                    float fx = x / (float)columns;
                    Vector2 uv = new Vector2(crop.x + crop.width * fx, crop.y + crop.height * fy);
                    Vector2 offset = Application.isPlaying ? WaterUvOffset(uv, crop, motionTime) : Vector2.zero;
                    vertex.position = new Vector3(bounds.x + bounds.width * fx, bounds.y + bounds.height * fy);
                    vertex.uv0 = uv + offset;
                    mesh.AddVert(vertex);
                }
            }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int i = y * (columns + 1) + x;
                    mesh.AddTriangle(i, i + columns + 1, i + 1);
                    mesh.AddTriangle(i + 1, i + columns + 1, i + columns + 2);
                }
        }
    }
}
