using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A quiet clock surface and a continuous 24-hour rail with one live marker.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageTimeRibbonGraphic : MaskableGraphic
    {
        public const float DesignWidth = 720f;
        public const float RailLeft = 252f;
        public const float RailRight = 692f;
        [SerializeField] private double elapsedHours;

        public double ElapsedHours => elapsedHours;
        public double LocalHour => NavigationTimeOfDayUtility.LocalHour(elapsedHours);
        public NavigationTimeOfDay TimeOfDay => NavigationTimeOfDayUtility.FromVoyageHours(elapsedHours);

        public void SetVoyageHours(double value)
        {
            elapsedHours = double.IsNaN(value) || double.IsInfinity(value) ? 0d : Math.Max(0d, value);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            RoundedRect(mesh, rect, 12f, new Color32(11, 28, 35, 255), new Color32(25, 47, 53, 255));

            Color accent = NavigationTimeOfDayUtility.AccentColor(TimeOfDay);
            Color pill = accent;
            pill.a = .12f;
            RoundedRect(mesh, new Rect(rect.xMin + 159f, rect.center.y - 23f, 68f, 26f), 13f, pill, pill);

            float left = rect.xMin + RailLeft;
            float right = rect.xMin + RailRight;
            float y = rect.center.y - 2f;
            Color night = new Color32(66, 92, 127, 255);
            RoundedRect(mesh, Rect.MinMaxRect(left, y - 5f, right, y + 5f), 5f, night, night);
            float dayStart = Mathf.Lerp(left, right, (float)(NavigationTimeOfDayUtility.DayStartHour / 24d));
            float duskStart = Mathf.Lerp(left, right, (float)(NavigationTimeOfDayUtility.DuskStartHour / 24d));
            float nightStart = Mathf.Lerp(left, right, (float)(NavigationTimeOfDayUtility.NightStartHour / 24d));
            Color day = new Color32(204, 168, 98, 255);
            Color dusk = new Color32(207, 117, 70, 255);
            RoundedRect(mesh, Rect.MinMaxRect(dayStart, y - 5f, duskStart, y + 5f), 0f, day, day);
            RoundedRect(mesh, Rect.MinMaxRect(duskStart, y - 5f, nightStart, y + 5f), 0f, dusk, dusk);

            float currentX = Mathf.Lerp(left, right, NavigationTimeOfDayUtility.CycleProgress(elapsedHours));
            Color glow = accent;
            glow.a = .13f;
            Circle(mesh, new Vector2(currentX, y), 12f, glow);
            Circle(mesh, new Vector2(currentX, y), 8f, new Color32(8, 23, 29, 255));
            Circle(mesh, new Vector2(currentX, y), 6f, accent);
            Circle(mesh, new Vector2(currentX, y), 2.5f, new Color32(255, 250, 225, 255));
        }

        private static void Circle(VertexHelper mesh, Vector2 center, float radius, Color color)
        {
            RoundedRect(mesh, new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f),
                radius, color, color);
        }

        private static void RoundedRect(VertexHelper mesh, Rect rect, float radius, Color bottom, Color top)
        {
            radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * .5f);
            int start = mesh.currentVertCount;
            mesh.AddVert(rect.center, Color.Lerp(bottom, top, .5f), Vector2.zero);
            const int steps = 8;
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius;
                float y = corner < 2 ? rect.yMax - radius : rect.yMin + radius;
                for (int step = 0; step <= steps; step++)
                {
                    float angle = (corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                    Vector2 point = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                    mesh.AddVert(point, Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)), Vector2.zero);
                }
            }
            int count = 4 * (steps + 1);
            for (int index = 0; index < count; index++)
                mesh.AddTriangle(start, start + 1 + (index + 1) % count, start + 1 + index);
        }
    }
}
