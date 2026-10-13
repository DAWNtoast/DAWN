using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Maps the active navigation cell to a local side-view battle environment.</summary>
    public sealed class BattleEnvironmentArtCatalog
    {
        private static readonly Dictionary<string, string> BackgroundNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "open", "battle_open_ocean_v1" }, { "beach", "battle_beach_cove_v1" },
            { "strait", "battle_rocky_strait_v1" }, { "reef", "battle_coral_reef_v1" },
            { "fog", "battle_misty_sea_v1" }, { "storm", "battle_storm_sea_v1" },
        };
        private readonly Dictionary<string, Texture2D> backgrounds = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Func<string, Texture2D> resourceLoader;

        public BattleEnvironmentArtCatalog() : this(Resources.Load<Texture2D>) { }

        internal BattleEnvironmentArtCatalog(Func<string, Texture2D> resourceLoader)
        {
            this.resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        }

        public int Count => BackgroundNames.Count;

        /// <summary>Releases cached references without destroying textures used by the current battle.</summary>
        public void ReleaseLoadedTextures() => backgrounds.Clear();

        public Texture2D ForTerrain(SeaTerrainType terrain)
        {
            return Load(BackgroundNames[KeyFor(terrain)]);
        }

        public static string LabelFor(SeaTerrainType terrain)
        {
            switch (KeyFor(terrain))
            {
                case "beach": return "海滩浅湾";
                case "strait": return "岛礁峡谷";
                case "reef": return "珊瑚礁区";
                case "fog": return "迷雾海域";
                case "storm": return "风暴海域";
                default: return "开阔海面";
            }
        }

        private static string KeyFor(SeaTerrainType terrain)
        {
            switch (terrain)
            {
                case SeaTerrainType.Beach:
                case SeaTerrainType.Shoal:
                case SeaTerrainType.KelpField:
                case SeaTerrainType.Island: return "beach";
                case SeaTerrainType.Strait: return "strait";
                case SeaTerrainType.Reef: return "reef";
                case SeaTerrainType.FogBank: return "fog";
                case SeaTerrainType.StormFront: return "storm";
                default: return "open";
            }
        }

        private Texture2D Load(string fileName)
        {
            if (backgrounds.TryGetValue(fileName, out Texture2D texture)) return texture;
            texture = resourceLoader("BattleEnvironments/" + fileName);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            backgrounds.Add(fileName, texture);
            return texture;
        }
    }
}
