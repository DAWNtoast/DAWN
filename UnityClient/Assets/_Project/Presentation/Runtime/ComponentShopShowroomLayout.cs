using System;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Five catalogue entries aligned with the category buttons on the opposite side.</summary>
    public static class ComponentShopShowroomLayout
    {
        public const int PageSize = 5;

        public readonly struct Spot
        {
            public readonly Vector2 Position;
            public readonly float Scale;
            public Spot(float x, float y, float scale) { Position = new Vector2(x, y); Scale = scale; }
        }

        public static int PageCount(int total) => Mathf.Max(1, (Mathf.Max(0, total) + PageSize - 1) / PageSize);

        public static int VisibleCount(int total, int page)
        {
            int first = Mathf.Clamp(page, 0, PageCount(total) - 1) * PageSize;
            return Mathf.Clamp(total - first, 0, PageSize);
        }

        public static Spot GetDisplay(int visibleCount, int index)
        {
            if (visibleCount < 1 || visibleCount > PageSize || index < 0 || index >= visibleCount)
                throw new ArgumentOutOfRangeException(nameof(index), "A catalogue page contains one to five component thumbnails.");
            return new Spot(800f, 305f - index * 120f, 1f);
        }
    }
}
