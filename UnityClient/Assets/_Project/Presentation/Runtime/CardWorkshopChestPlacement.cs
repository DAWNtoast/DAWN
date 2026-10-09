using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Positions the painted feet, rather than transparent PNG bounds, on the workshop rug.</summary>
    public static class CardWorkshopChestPlacement
    {
        // Measured from the opaque brass feet of the four CardWorkshopV2 sprites, bottom-origin UV.
        private static readonly float[] FootY = { 0.058f, 0.055f, 0.070f, 0.057f };
        private static readonly Vector2[] FootX =
        {
            new Vector2(0.117f, 0.886f), new Vector2(0.153f, 0.859f),
            new Vector2(0.189f, 0.812f), new Vector2(0.216f, 0.779f),
        };
        private static readonly Vector2[] Floor =
        {
            new Vector2(-490f, -288f), new Vector2(-165f, -195f),
            new Vector2(165f, -195f), new Vector2(490f, -288f),
        };

        // Alpha >= 128 bounds of the source PNGs (bottom-origin UV). The transparent
        // margins differ substantially, especially on the former navigation chest.
        // Equal image rectangles therefore do not produce equally sized chests.
        private static readonly Rect[] PaintedBounds =
        {
            new Rect(78f / 1374f, 59f / 1145f, 1219f / 1374f, 1042f / 1145f),
            new Rect(120f / 1351f, 55f / 1164f, 1111f / 1351f, 1061f / 1164f),
            new Rect(216f / 1536f, 63f / 1024f, 1104f / 1536f, 918f / 1024f),
            new Rect(62f / 1402f, 56f / 1122f, 1276f / 1402f, 1057f / 1122f),
        };
        public const float PaintedWidth = 280f;

        public static Vector2 FloorPosition(int index) => Floor[index];
        public static Vector2 FootUv(int index, bool right) => new Vector2(right ? FootX[index].y : FootX[index].x, FootY[index]);
        public static Rect PaintedBoundsUv(int index) => PaintedBounds[index];
        public static Vector2 PaintedSize(int index, Vector2 artSize) => Vector2.Scale(PaintedBounds[index].size, artSize);
        public static Vector2 ArtSize(int index, float aspect)
        {
            // Keep the original proportions, but give every entrance the same visible
            // width. Floor depth only affects position/draw order, never chest scale.
            float width = PaintedWidth / PaintedBounds[index].width;
            return new Vector2(width, width / Mathf.Max(0.1f, aspect));
        }
        public static Vector2 ArtPosition => new Vector2(0f, 40f);
        public static float ContactOffset(int index, Vector2 size) => ArtPosition.y + (FootY[index] - 0.5f) * size.y;
        public static Vector2 ButtonPosition(int index, Vector2 size) => Floor[index] - Vector2.up * ContactOffset(index, size);
        public static float FootprintWidth(int index, Vector2 size) => (FootX[index].y - FootX[index].x) * size.x;
    }
}
