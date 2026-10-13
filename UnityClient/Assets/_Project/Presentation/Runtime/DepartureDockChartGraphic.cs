using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Places an independent chart on the dock's sloping parchment, without stretching its cartography.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DepartureDockChartGraphic : RawImage
    {
        private static readonly string[] ChartPaths =
        {
            "PortFacilities/DepartureCharts4K/chart-shattered-reef",
            "PortFacilities/DepartureCharts4K/chart-golden-trade",
            "PortFacilities/DepartureCharts4K/chart-moon-ruins",
        };

        public static Texture2D LoadChart(int routeIndex) => Resources.Load<Texture2D>(
            ChartPaths[Mathf.Clamp(routeIndex, 0, ChartPaths.Length - 1)]);

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (texture == null) return;
            // Source art is 16:9; crop a horizontal strip to fit the wide map
            // window. A narrow transparent edge blends into the original paper.
            const float averageWidth = 858.5f, height = 186f;
            float uvHeight = Mathf.Min(1f, height / averageWidth * texture.width / texture.height);
            float uvBottom = .5f - uvHeight * .5f;
            float[] columns = { 0f, .008f, .992f, 1f };
            float[] rows = { 0f, .035f, .965f, 1f };
            Rect rect = rectTransform.rect;
            for (int row = 0; row < rows.Length; row++)
            {
                float v = rows[row];
                float left = Mathf.Lerp(0f, 34f, v), right = Mathf.Lerp(907f, 846f, v);
                float y = Mathf.Lerp(893f, 707f, v);
                for (int column = 0; column < columns.Length; column++)
                {
                    float u = columns[column];
                    Vector2 position = new Vector2(Mathf.Lerp(left, right, u) / 2048f, 1f - y / 1152f);
                    Color tint = color;
                    if (row == 0 || row == rows.Length - 1 || column == 0 || column == columns.Length - 1) tint.a = 0f;
                    mesh.AddVert(rect.min + Vector2.Scale(rect.size, position), tint, new Vector2(u, uvBottom + v * uvHeight));
                }
            }
            for (int row = 0; row < rows.Length - 1; row++)
                for (int column = 0; column < columns.Length - 1; column++)
                {
                    int start = row * columns.Length + column;
                    mesh.AddTriangle(start, start + columns.Length, start + 1);
                    mesh.AddTriangle(start + 1, start + columns.Length, start + columns.Length + 1);
                }
        }
    }
}
