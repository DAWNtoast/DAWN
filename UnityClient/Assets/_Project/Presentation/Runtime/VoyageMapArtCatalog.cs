using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Central resource contract for the V2 procedural voyage chart. The map can
    /// still render when one or more textures are absent; presentation code then
    /// falls back to deterministic chart colours and line work.
    /// </summary>
    public sealed class VoyageMapArtCatalog
    {
        public const string ResourceRoot = "VoyageMapV2/";

        private readonly Texture2D[] openWater = new Texture2D[3];
        private readonly Dictionary<SeaTerrainType, Texture2D> terrain = new Dictionary<SeaTerrainType, Texture2D>();
        private CoreVoyageMap cachedCoastMap;
        private VoyageChartCoastField cachedCoastField;

        public VoyageMapArtCatalog()
        {
            ChartBase = Load("water_chart_base_v2", false);
            GroundDetail = Load("terrain_ground_detail_v3", false);
            openWater[0] = Load("water_open_a_v2", false);
            openWater[1] = Load("water_open_b_v2", false);
            openWater[2] = Load("water_open_c_v2", false);

            terrain[SeaTerrainType.Shoal] = Load("terrain_shoal_v2", true);
            terrain[SeaTerrainType.Island] = Load("terrain_island_v2", true);
            terrain[SeaTerrainType.Reef] = Load("terrain_reef_v2", true);
            terrain[SeaTerrainType.KelpField] = Load("terrain_kelp_v2", true);
            terrain[SeaTerrainType.FogBank] = Load("terrain_fog_v2", true);
            terrain[SeaTerrainType.StormFront] = Load("terrain_storm_v2", true);
            terrain[SeaTerrainType.Strait] = Load("terrain_strait_v2", true);
        }

        public int LoadedTextureCount
        {
            get
            {
                int count = ChartBase == null ? 0 : 1;
                if (GroundDetail != null) count += 1;
                for (int index = 0; index < openWater.Length; index += 1)
                {
                    if (openWater[index] != null) count += 1;
                }

                foreach (KeyValuePair<SeaTerrainType, Texture2D> entry in terrain)
                {
                    if (entry.Value != null) count += 1;
                }
                return count;
            }
        }

        public bool IsAvailable => LoadedTextureCount == 12;

        public Texture2D ChartBase { get; }
        public Texture2D GroundDetail { get; }

        public int WaterVariantCount => openWater.Length;

        /// <summary>
        /// Retains only the most recently displayed map's managed coast field.
        /// Rebuilding the UI while exploring or changing currents must not redo
        /// the distance transform. A real terrain/seed edit still invalidates it.
        /// Textures and materials remain owned by each surface graphic.
        /// </summary>
        public VoyageChartCoastField GetCoastField(CoreVoyageMap map)
        {
            if (map == null) throw new System.ArgumentNullException(nameof(map));
            string fingerprint = VoyageChartCoastField.Fingerprint(map);
            if (ReferenceEquals(cachedCoastMap, map) && cachedCoastField != null
                && cachedCoastField.GeometryFingerprint == fingerprint)
                return cachedCoastField;

            VoyageChartCoastField field = VoyageChartCoastField.Build(map);
            cachedCoastMap = map;
            cachedCoastField = field;
            return field;
        }

        public Texture2D WaterVariant(int index)
        {
            return openWater[Mathf.Clamp(index, 0, openWater.Length - 1)];
        }

        public Texture2D WaterAt(uint mapHash, int column, int row)
        {
            int preferred = (int)(VisualHash(mapHash, column, row, 0xA341316Cu) % (uint)openWater.Length);
            Texture2D texture = openWater[preferred];
            if (texture != null) return texture;

            for (int index = 0; index < openWater.Length; index += 1)
            {
                if (openWater[index] != null) return openWater[index];
            }
            return null;
        }

        public Texture2D TerrainFor(SeaTerrainType type)
        {
            if (type == SeaTerrainType.Beach)
            {
                return terrain[SeaTerrainType.Shoal];
            }

            return terrain.TryGetValue(type, out Texture2D texture) ? texture : null;
        }

        public Texture2D BeachLandOverlay => terrain[SeaTerrainType.Island];

        public static uint VisualHash(uint mapHash, int column, int row, uint salt = 0u)
        {
            unchecked
            {
                uint value = mapHash ^ salt;
                value ^= (uint)(column + 1) * 0x9E3779B9u;
                value = (value << 13) | (value >> 19);
                value ^= (uint)(row + 1) * 0x85EBCA6Bu;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                return value;
            }
        }

        private static Texture2D Load(string fileName, bool transparent)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + fileName);
            if (texture == null) return null;

            texture.name = fileName;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = transparent ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            return texture;
        }
    }
}
