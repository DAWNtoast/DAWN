using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared surface coordinates for paper, contact shadows and pointer hit areas.</summary>
    public static class DispatchTabletopLayout
    {
        public static Vector2 BackgroundSize => PhaseSurfaceLayout.BackgroundSize;
        // Match the authored UIPreviewsForMe dispatch panel. The stage is
        // lowered so its top edge meets the header exactly; the panel owns
        // the authored horizontal offset.
        public static Vector2 StagePosition => new Vector2(0f, -133f);

        private static readonly Vector2[] AuthoredChartPositions =
        {
            new Vector2(-186.2756f, -30.446426f),
            new Vector2(14.499069f, -44.767433f),
            new Vector2(215.27373f, -59.0884f),
        };

        private static readonly Vector2[] AuthoredChartSizes =
        {
            new Vector2(303.4679f, 82.86902f),
            new Vector2(282.97903f, 86.12282f),
            new Vector2(262.49005f, 89.37654f),
        };

        // Normalized projected corners copied from the authored preview.
        private static readonly Vector2[,] AuthoredChartProjection =
        {
            {
                new Vector2(0f, 0.17036216f),
                new Vector2(0.61556363f, 0f),
                new Vector2(0.44420484f, 1f),
                new Vector2(1f, 0.86439466f),
            },
            {
                new Vector2(0f, 0.16392651f),
                new Vector2(0.66013336f, 0f),
                new Vector2(0.4039625f, 1f),
                new Vector2(1f, 0.869518f),
            },
            {
                new Vector2(0f, 0.15795797f),
                new Vector2(0.7116607f, 0f),
                new Vector2(0.35743803f, 1f),
                new Vector2(0.99999994f, 0.8742681f),
            },
        };

        // Measured inner wood in dispatch_room_integrated_v3 (1672 x 941).
        // Room coordinates use the image's top-left origin. Plane UV runs near to far.
        // The table, its shadows and the foreground miniatures are one image;
        // all dynamic paper coordinates derive from that same room, not a sprite rect.
        public static Vector2 RoomPoint(Vector2 imageUv)
        {
            return PhaseSurfaceLayout.SourceUvToContent(imageUv, new Vector2(1672f, 941f)) - StagePosition;
        }

        public static Vector2 TablePoint(Vector2 planeUv)
        {
            Vector2 near = Vector2.LerpUnclamped(new Vector2(490f / 1672f, 583f / 941f), new Vector2(1100f / 1672f, 634f / 941f), planeUv.x);
            Vector2 far = Vector2.LerpUnclamped(new Vector2(645f / 1672f, 500f / 941f), new Vector2(1180f / 1672f, 538f / 941f), planeUv.x);
            return RoomPoint(Vector2.LerpUnclamped(near, far, planeUv.y));
        }

        public static Vector2 ChartCorner(int index, Vector2 uv)
        {
            int safe = Mathf.Clamp(index, 0, AuthoredChartPositions.Length - 1);
            Rect bounds = ChartBounds(safe);
            Vector2 bottom = Vector2.Lerp(AuthoredChartProjection[safe, 0], AuthoredChartProjection[safe, 1], uv.x);
            Vector2 top = Vector2.Lerp(AuthoredChartProjection[safe, 2], AuthoredChartProjection[safe, 3], uv.x);
            Vector2 projected = Vector2.Lerp(bottom, top, uv.y);
            return new Vector2(bounds.xMin + projected.x * bounds.width,
                bounds.yMin + projected.y * bounds.height);
        }

        public static Rect ChartBounds(int index)
        {
            int safe = Mathf.Clamp(index, 0, AuthoredChartPositions.Length - 1);
            Vector2 size = AuthoredChartSizes[safe];
            return new Rect(AuthoredChartPositions[safe] - size * 0.5f, size);
        }

        public static void ConfigureProjection(TabletopQuadProjection effect, int index)
        {
            int safe = Mathf.Clamp(index, 0, AuthoredChartPositions.Length - 1);
            effect.Configure(AuthoredChartProjection[safe, 0], AuthoredChartProjection[safe, 1],
                AuthoredChartProjection[safe, 2], AuthoredChartProjection[safe, 3]);
        }
    }
}
