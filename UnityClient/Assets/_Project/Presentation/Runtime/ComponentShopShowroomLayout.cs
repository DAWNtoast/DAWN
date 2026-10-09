using System;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Whole-object placements on the central showroom floor, not across the wall shelves.</summary>
    public static class ComponentShopShowroomLayout
    {
        public const int PageSize = 3;

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
                throw new ArgumentOutOfRangeException(nameof(index), "A showroom group contains one to three complete assemblies.");
            if (visibleCount == 1) return new Spot(0f, -55f, 1f);
            if (visibleCount == 2) return new Spot(index == 0 ? -250f : 250f, -55f, 0.96f);
            return new Spot((index - 1) * 350f, -55f, 0.90f);
        }
    }
}
