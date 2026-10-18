using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Measured inner faces and plaques on treasure_worktable_background_v3 (1672 x 941),
    /// using top-left source-image coordinates. Frames and supports remain in the background;
    /// paper and its four fragments share the same shallow four-corner projection.
    /// </summary>
    public sealed class TreasureChartDisplayLayout
    {
        private static readonly Vector2 ReferenceSize = new Vector2(1672f, 941f);
        private readonly Vector2 bottomLeft, bottomRight, topLeft, topRight;

        private TreasureChartDisplayLayout(Vector2 upperLeft, Vector2 upperRight,
            Vector2 lowerLeft, Vector2 lowerRight, Vector2 nameCenter, Vector2 nameSize,
            Vector2 labelCenter, Vector2 labelSize)
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
            NamePosition = PixelToPanel(nameCenter);
            NameSize = PhaseSurfaceLayout.SourcePixelSizeToContent(nameSize, ReferenceSize);
            LabelPosition = PixelToPanel(labelCenter);
            LabelSize = PhaseSurfaceLayout.SourcePixelSizeToContent(labelSize, ReferenceSize);
        }

        public Vector2 Position { get; }
        public Vector2 Size { get; }
        public Vector2 NamePosition { get; }
        public Vector2 NameSize { get; }
        public Vector2 LabelPosition { get; }
        public Vector2 LabelSize { get; }

        public static TreasureChartDisplayLayout For(int index)
        {
            switch (index)
            {
                case 0: return new TreasureChartDisplayLayout(new Vector2(238f, 288f), new Vector2(593f, 288f),
                    new Vector2(185f, 537f), new Vector2(573f, 537f),
                    new Vector2(378f, 557f), new Vector2(178f, 24f),
                    new Vector2(375f, 596f), new Vector2(156f, 23f));
                case 1: return new TreasureChartDisplayLayout(new Vector2(678f, 288f), new Vector2(1029f, 288f),
                    new Vector2(657f, 538f), new Vector2(1047f, 538f),
                    new Vector2(851f, 557f), new Vector2(188f, 24f),
                    new Vector2(851f, 596f), new Vector2(188f, 23f));
                case 2: return new TreasureChartDisplayLayout(new Vector2(1105f, 288f), new Vector2(1463f, 288f),
                    new Vector2(1130f, 538f), new Vector2(1528f, 538f),
                    new Vector2(1333f, 557f), new Vector2(188f, 24f),
                    new Vector2(1337f, 596f), new Vector2(188f, 23f));
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        public void Project(Graphic graphic)
        {
            graphic.gameObject.AddComponent<TabletopQuadProjection>().Configure(bottomLeft, bottomRight, topLeft, topRight);
        }

        public Vector2 FragmentCenter(int index)
        {
            if (index < 0 || index > 3) throw new System.ArgumentOutOfRangeException(nameof(index));
            float u = index % 2 * .5f + .25f;
            float v = index < 2 ? .75f : .25f;
            Vector2 uv = Vector2.Lerp(Vector2.Lerp(bottomLeft, bottomRight, u),
                Vector2.Lerp(topLeft, topRight, u), v);
            return Vector2.Scale(uv - Vector2.one * .5f, Size - Vector2.one * 5f);
        }

        // Each fragment keeps the full paper's perspective, including its shared seams.
        public void ProjectFragment(Graphic graphic, int index)
        {
            float x = (index % 2) * .5f;
            float y = index < 2 ? .5f : 0f;
            Vector2 At(float u, float v) => Vector2.Lerp(
                Vector2.Lerp(bottomLeft, bottomRight, u), Vector2.Lerp(topLeft, topRight, u), v);
            graphic.gameObject.AddComponent<TabletopQuadProjection>().Configure(
                At(x, y), At(x + .5f, y), At(x, y + .5f), At(x + .5f, y + .5f));
        }

        public static Vector2 PixelToPanel(Vector2 pixel)
        {
            return PhaseSurfaceLayout.SourcePixelToContent(pixel, ReferenceSize) -
                new Vector2(0f, PhaseSurfaceLayout.StagePositionY);
        }
    }
}
