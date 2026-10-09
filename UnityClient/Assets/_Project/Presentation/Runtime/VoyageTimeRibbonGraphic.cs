using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// A compact nautical timeline for the lookout.  It replaces the old
    /// collection of circular stars with a readable dawn/day/dusk/night route
    /// and a single marker that moves as voyage hours advance.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageTimeRibbonGraphic : MaskableGraphic
    {
        private const float TrackHeight = 12f;
        [SerializeField] private double elapsedHours;

        public double ElapsedHours => elapsedHours;
        public double LocalHour => NavigationTimeOfDayUtility.LocalHour(elapsedHours);
        public NavigationTimeOfDay TimeOfDay => NavigationTimeOfDayUtility.FromVoyageHours(elapsedHours);

        public void SetVoyageHours(double value)
        {
            elapsedHours = double.IsNaN(value) || double.IsInfinity(value) ? 0d : Mathf.Max(0f, (float)value);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = rectTransform.rect;
            float left = rect.xMin + 24f;
            float right = rect.xMax - 24f;
            // Keep the moving marker between the readout and the legend lanes.
            float trackY = rect.yMin + 43f;

            // Draw directly on the shared wooden header; only the time track,
            // ticks and marker have a visible surface.
            float[] hours = { 0f, 6f, 8f, 17f, 20f, 24f };
            Color32[] colors =
            {
                new Color32(29, 49, 78, 235),
                new Color32(151, 105, 87, 235),
                new Color32(205, 156, 77, 235),
                new Color32(184, 99, 55, 235),
                new Color32(29, 49, 78, 235),
            };
            for (int index = 0; index < colors.Length; index += 1)
            {
                float start = Mathf.Lerp(left, right, hours[index] / 24f);
                float end = Mathf.Lerp(left, right, hours[index + 1] / 24f);
                AddQuad(vertexHelper, Rect.MinMaxRect(start, trackY, end, trackY + TrackHeight), colors[index]);
            }

            float[] markers = { 0f, 6f, 8f, 12f, 17f, 20f, 24f };
            for (int index = 0; index < markers.Length; index += 1)
            {
                float x = Mathf.Lerp(left, right, markers[index] / 24f);
                float height = markers[index] == 12f ? 18f : 10f;
                AddQuad(vertexHelper, Rect.MinMaxRect(x - 0.75f, trackY - 3f, x + 0.75f, trackY + TrackHeight + height),
                    new Color32(234, 215, 164, 180));
            }

            AddDiamond(vertexHelper, Mathf.Lerp(left, right, 6f / 24f), trackY + TrackHeight + 16f, 5f,
                new Color32(243, 180, 95, 235));
            AddSunGlyph(vertexHelper, Mathf.Lerp(left, right, 12f / 24f), trackY + TrackHeight + 18f);
            AddDiamond(vertexHelper, Mathf.Lerp(left, right, 17f / 24f), trackY + TrackHeight + 16f, 5f,
                new Color32(235, 132, 69, 235));
            AddStarGlyph(vertexHelper, Mathf.Lerp(left, right, 22f / 24f), trackY + TrackHeight + 17f);

            float currentX = Mathf.Lerp(left, right, NavigationTimeOfDayUtility.CycleProgress(elapsedHours));
            Color32 accent = ToColor32(NavigationTimeOfDayUtility.AccentColor(TimeOfDay));
            AddQuad(vertexHelper, Rect.MinMaxRect(currentX - 1.5f, trackY - 10f, currentX + 1.5f, trackY + TrackHeight + 28f), accent);
            AddDiamond(vertexHelper, currentX, trackY - 12f, 7f, accent);
        }

        private static void AddSunGlyph(VertexHelper vertexHelper, float x, float y)
        {
            AddDiamond(vertexHelper, x, y, 6f, new Color32(255, 226, 151, 245));
            AddLine(vertexHelper, x - 11f, y, x - 6f, y, 1f, new Color32(255, 226, 151, 190));
            AddLine(vertexHelper, x + 6f, y, x + 11f, y, 1f, new Color32(255, 226, 151, 190));
            AddLine(vertexHelper, x, y - 11f, x, y - 6f, 1f, new Color32(255, 226, 151, 190));
            AddLine(vertexHelper, x, y + 6f, x, y + 11f, 1f, new Color32(255, 226, 151, 190));
        }

        private static void AddStarGlyph(VertexHelper vertexHelper, float x, float y)
        {
            AddDiamond(vertexHelper, x, y, 4f, new Color32(194, 215, 247, 235));
            AddLine(vertexHelper, x - 8f, y, x + 8f, y, 1f, new Color32(194, 215, 247, 160));
            AddLine(vertexHelper, x, y - 8f, x, y + 8f, 1f, new Color32(194, 215, 247, 160));
        }

        private static void AddDiamond(VertexHelper vertexHelper, float x, float y, float radius, Color32 color)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector3(x, y + radius), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(x + radius, y), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(x, y - radius), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(x - radius, y), color, Vector2.zero);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddLine(VertexHelper vertexHelper, float x0, float y0, float x1, float y1, float thickness, Color32 color)
        {
            Vector2 direction = new Vector2(x1 - x0, y1 - y0).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * thickness * 0.5f;
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector3(x0 + normal.x, y0 + normal.y), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(x1 + normal.x, y1 + normal.y), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(x1 - normal.x, y1 - normal.y), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(x0 - normal.x, y0 - normal.y), color, Vector2.zero);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddQuad(VertexHelper vertexHelper, Rect rect, Color32 color)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }

        private static Color32 ToColor32(Color source)
        {
            return source;
        }
    }
}
