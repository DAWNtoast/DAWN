using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// One seed-bound shoreline field for the ENTIRE chart, not independent
    /// rectangular event pictures. Positive distance is solid land. Only the
    /// core's impassable Island cells contribute land; Beach/Strait stay water.
    /// Pure presentation: never changes events, pathfinding or current arrays.
    /// </summary>
    public sealed class VoyageChartCoastField
    {
        public const int SamplesPerCell = 12;
        private const float Range = 2f;
        private readonly float[] distances;
        public int Width { get; }
        public int Height { get; }
        public Color32[] Pixels { get; }
        public string GeometryFingerprint { get; }

        private VoyageChartCoastField(CoreVoyageMap map)
        {
            Width = map.Columns * SamplesPerCell + 1;
            Height = map.Rows * SamplesPerCell + 1;
            GeometryFingerprint = Fingerprint(map);
            int length = Width * Height;
            bool[] land = new bool[length];
            float[] toLand = new float[length];
            float[] toWater = new float[length];
            distances = new float[length];
            Pixels = new Color32[length];
            for (int y = 0; y < Height; ++y)
            for (int x = 0; x < Width; ++x)
            {
                float gx = x / (float)SamplesPerCell;
                float gy = y / (float)SamplesPerCell;
                // Shared domain warp breaks grid-aligned banks into coves and
                // headlands. The <= .20-cell offset preserves every cell centre.
                float warpedX = gx + (Noise(gx * 1.33f, gy * 1.33f, map.Hash ^ 0xAC564B21u) - .5f) * .40f;
                float warpedY = gy + (Noise(gx * 1.33f, gy * 1.33f, map.Hash ^ 0x617D95F3u) - .5f) * .40f;
                float density = SampleTerrain(map, warpedX, warpedY, SeaTerrainType.Island);
                // Bounded, world-coordinate coast roughness. Its amplitude is
                // too small to change any cell centre or close a one-cell strait.
                float roughness = (Noise(gx * 2.7f, gy * 2.7f, map.Hash) - .5f) * .26f
                    + (Noise(gx * 7.3f, gy * 7.3f, map.Hash ^ 0xD1B54A35u) - .5f) * .09f;
                float border = Mathf.Min(Mathf.Min(gx, map.Columns - gx),
                    Mathf.Min(gy, map.Rows - gy));
                bool solid = density + roughness > .5f && border > .20f;
                int i = y * Width + x;
                land[i] = solid;
                toLand[i] = solid ? 0f : 10000f;
                toWater[i] = solid ? 10000f : 0f;
                float shoal = Mathf.Max(SampleTerrain(map, gx, gy, SeaTerrainType.Shoal),
                    SampleTerrain(map, gx, gy, SeaTerrainType.Beach));
                float reef = SampleTerrain(map, gx, gy, SeaTerrainType.Reef);
                Pixels[i] = new Color32(0, (byte)Mathf.RoundToInt(shoal * 255f),
                    (byte)Mathf.RoundToInt(reef * 255f), 255);
            }
            DistanceTransform(toLand, Width, Height);
            DistanceTransform(toWater, Width, Height);
            for (int i = 0; i < length; ++i)
            {
                float d = land[i] ? toWater[i] - .5f : .5f - toLand[i];
                d = Mathf.Clamp(d / SamplesPerCell, -Range, Range);
                distances[i] = d;
                Color32 pixel = Pixels[i];
                pixel.r = (byte)Mathf.RoundToInt((d / (Range * 2f) + .5f) * 255f);
                Pixels[i] = pixel;
            }
        }

        public static VoyageChartCoastField Build(CoreVoyageMap map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return new VoyageChartCoastField(map);
        }

        public static string Fingerprint(CoreVoyageMap map)
        {
            unchecked
            {
                uint value = map.Hash ^ 2166136261u;
                for (int i = 0; i < map.TerrainCells.Length; ++i)
                    value = (value ^ (uint)map.TerrainCells[i]) * 16777619u;
                return value.ToString("X8");
            }
        }

        public float SampleSignedDistance(float gridX, float gridY)
        {
            float x = Mathf.Clamp(gridX * SamplesPerCell, 0f, Width - 1f);
            float y = Mathf.Clamp(gridY * SamplesPerCell, 0f, Height - 1f);
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            int nx = Mathf.Min(ix + 1, Width - 1), ny = Mathf.Min(iy + 1, Height - 1);
            return Mathf.Lerp(Mathf.Lerp(distances[iy * Width + ix], distances[iy * Width + nx], x - ix),
                Mathf.Lerp(distances[ny * Width + ix], distances[ny * Width + nx], x - ix), y - iy);
        }

        private static float SampleTerrain(CoreVoyageMap map, float gx, float gy, SeaTerrainType type)
        {
            float x = gx - .5f, y = gy - .5f;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float tx = x - ix, ty = y - iy;
            // Smooth interpolation rounds the cell corners, while shared global
            // samples join event-owned and natural land without any seam.
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(Mathf.Lerp(IsTerrain(map, ix, iy, type), IsTerrain(map, ix + 1, iy, type), tx),
                Mathf.Lerp(IsTerrain(map, ix, iy + 1, type), IsTerrain(map, ix + 1, iy + 1, type), tx), ty);
        }

        private static float IsTerrain(CoreVoyageMap map, int x, int y, SeaTerrainType type)
        {
            return map.IsInBounds(x, y) && map.TerrainCells[y * map.Columns + x] == type ? 1f : 0f;
        }

        private static void DistanceTransform(float[] values, int width, int height)
        {
            const float diagonal = 1.41421356237f;
            for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x)
            {
                int i = y * width + x;
                float d = values[i];
                if (x > 0) d = Mathf.Min(d, values[i - 1] + 1f);
                if (y > 0)
                {
                    d = Mathf.Min(d, values[i - width] + 1f);
                    if (x > 0) d = Mathf.Min(d, values[i - width - 1] + diagonal);
                    if (x + 1 < width) d = Mathf.Min(d, values[i - width + 1] + diagonal);
                }
                values[i] = d;
            }
            for (int y = height - 1; y >= 0; --y)
            for (int x = width - 1; x >= 0; --x)
            {
                int i = y * width + x;
                float d = values[i];
                if (x + 1 < width) d = Mathf.Min(d, values[i + 1] + 1f);
                if (y + 1 < height)
                {
                    d = Mathf.Min(d, values[i + width] + 1f);
                    if (x > 0) d = Mathf.Min(d, values[i + width - 1] + diagonal);
                    if (x + 1 < width) d = Mathf.Min(d, values[i + width + 1] + diagonal);
                }
                values[i] = d;
            }
        }

        private static float Noise(float x, float y, uint seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float tx = x - ix, ty = y - iy;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, seed), Hash(ix + 1, iy, seed), tx),
                Mathf.Lerp(Hash(ix, iy + 1, seed), Hash(ix + 1, iy + 1, seed), tx), ty);
        }

        private static float Hash(int x, int y, uint seed)
        {
            return (VoyageMapArtCatalog.VisualHash(seed, x, y, 0x63D83595u) & 65535u) / 65535f;
        }
    }
}
