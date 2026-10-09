using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Measured faces on treasure_room_boards_v2 (1672 x 941), top-left image coordinates.
    /// Supports are part of the room, so hover never moves the paper or repaints the feet.
    /// A shallow four-corner projection follows each front-facing face, not a tabletop tilt.
    /// </summary>
    public sealed class TreasureChartDisplayLayout
    {
        private static readonly Vector2 ReferenceSize = new Vector2(1672f, 941f);
        private readonly Vector2 bottomLeft, bottomRight, topLeft, topRight;

        private TreasureChartDisplayLayout(Vector2 upperLeft, Vector2 upperRight,
            Vector2 lowerLeft, Vector2 lowerRight, Vector2 labelCenter)
        {
            Vector2 min = Vector2.Min(Vector2.Min(upperLeft, upperRight), Vector2.Min(lowerLeft, lowerRight));
            Vector2 max = Vector2.Max(Vector2.Max(upperLeft, upperRight), Vector2.Max(lowerLeft, lowerRight));
            Vector2 extent = max - min;
            Position = PixelToPanel((min + max) * 0.5f);
            Size = PhaseSurfaceLayout.SourcePixelSizeToContent(extent, ReferenceSize);
            topLeft = new Vector2((upperLeft.x - min.x) / extent.x, (max.y - upperLeft.y) / extent.y);
            topRight = new Vector2((upperRight.x - min.x) / extent.x, (max.y - upperRight.y) / extent.y);
            bottomLeft = new Vector2((lowerLeft.x - min.x) / extent.x, (max.y - lowerLeft.y) / extent.y);
            bottomRight = new Vector2((lowerRight.x - min.x) / extent.x, (max.y - lowerRight.y) / extent.y);
            LabelPosition = PixelToPanel(labelCenter);
            LabelSize = new Vector2(Size.x - 6f, 24f);
        }

        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public Vector2 LabelPosition { get; }
        public Vector2 LabelSize { get; }

        public static TreasureChartDisplayLayout For(int index)
        {
            switch (index)
            {
                case 0: return new TreasureChartDisplayLayout(new Vector2(314f, 438f), new Vector2(584f, 438f),
                    new Vector2(301f, 612f), new Vector2(578f, 612f), new Vector2(437f, 633f));
                case 1: return new TreasureChartDisplayLayout(new Vector2(686f, 438f), new Vector2(978f, 438f),
                    new Vector2(678f, 612f), new Vector2(983f, 612f), new Vector2(832f, 633f));
                case 2: return new TreasureChartDisplayLayout(new Vector2(1085f, 438f), new Vector2(1356f, 438f),
                    new Vector2(1091f, 612f), new Vector2(1367f, 612f), new Vector2(1234f, 633f));
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        public void Project(Graphic graphic)
        {
            graphic.gameObject.AddComponent<TabletopQuadProjection>().Configure(bottomLeft, bottomRight, topLeft, topRight);
        }

        public static Vector2 PixelToPanel(Vector2 pixel)
        {
            Vector2 uv = pixel / ReferenceSize;
            return PhaseSurfaceLayout.SourcePixelToContent(pixel, ReferenceSize) -
                new Vector2(0f, PhaseSurfaceLayout.StagePositionY);
        }
    }
}
