using System;
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

        private static readonly string[] WaterNames = { "water_open_a_v2", "water_open_b_v2", "water_open_c_v2" };
        private static readonly Dictionary<SeaTerrainType, string> TerrainNames = new Dictionary<SeaTerrainType, string>
        {
            { SeaTerrainType.Shoal, "terrain_shoal_v2" }, { SeaTerrainType.Island, "terrain_island_v2" },
            { SeaTerrainType.Reef, "terrain_reef_v2" }, { SeaTerrainType.KelpField, "terrain_kelp_v2" },
            { SeaTerrainType.FogBank, "terrain_fog_v2" }, { SeaTerrainType.StormFront, "terrain_storm_v2" },
            { SeaTerrainType.Strait, "terrain_strait_v2" },
        };
        private readonly Func<string, Texture2D> resourceLoader;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private CoreVoyageMap cachedCoastMap;
        private VoyageChartCoastField cachedCoastField;

        public VoyageMapArtCatalog() : this(Resources.Load<Texture2D>) { }

        internal VoyageMapArtCatalog(Func<string, Texture2D> resourceLoader)
        {
            this.resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        }

        public int LoadedTextureCount
        {
            get
            {
                int count = 0;
                foreach (Texture2D texture in textures.Values) if (texture != null) count++;
                return count;
            }
        }

        public bool IsAvailable
        {
            get
            {
                bool available = ChartBase != null;
                available &= GroundDetail != null;
                for (int index = 0; index < WaterNames.Length; index++) available &= WaterVariant(index) != null;
                foreach (SeaTerrainType type in TerrainNames.Keys) available &= TerrainFor(type) != null;
                return available;
            }
        }

        public Texture2D ChartBase => Load("water_chart_base_v2", false);
        public Texture2D GroundDetail => Load("terrain_ground_detail_v3", false);

        public int WaterVariantCount => WaterNames.Length;

        /// <summary>Releases borrowed texture references while preserving the managed coast geometry cache.</summary>
        public void ReleaseLoadedTextures() => textures.Clear();

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
            return Load(WaterNames[Mathf.Clamp(index, 0, WaterNames.Length - 1)], false);
        }

        public Texture2D WaterAt(uint mapHash, int column, int row)
        {
            int preferred = (int)(VisualHash(mapHash, column, row, 0xA341316Cu) % (uint)WaterNames.Length);
            Texture2D texture = WaterVariant(preferred);
            if (texture != null) return texture;

            for (int index = 0; index < WaterNames.Length; index += 1)
            {
                Texture2D fallback = WaterVariant(index);
                if (fallback != null) return fallback;
            }
            return null;
        }

        public Texture2D TerrainFor(SeaTerrainType type)
        {
            if (type == SeaTerrainType.Beach)
            {
                type = SeaTerrainType.Shoal;
            }

            return TerrainNames.TryGetValue(type, out string name) ? Load(name, true) : null;
        }

        public Texture2D BeachLandOverlay => TerrainFor(SeaTerrainType.Island);

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

        private Texture2D Load(string fileName, bool transparent)
        {
            if (textures.TryGetValue(fileName, out Texture2D texture)) return texture;
            texture = resourceLoader(ResourceRoot + fileName);
            textures.Add(fileName, texture);
            if (texture == null) return null;

            texture.name = fileName;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = transparent ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            return texture;
        }
    }
}
