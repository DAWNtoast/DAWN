using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Maps the active navigation cell to a local side-view battle environment.</summary>
    public sealed class BattleEnvironmentArtCatalog
    {
        private readonly Dictionary<string, Texture2D> backgrounds = new Dictionary<string, Texture2D>();

        public BattleEnvironmentArtCatalog()
        {
            backgrounds["open"] = Load("battle_open_ocean_v1");
            backgrounds["beach"] = Load("battle_beach_cove_v1");
            backgrounds["strait"] = Load("battle_rocky_strait_v1");
            backgrounds["reef"] = Load("battle_coral_reef_v1");
            backgrounds["fog"] = Load("battle_misty_sea_v1");
            backgrounds["storm"] = Load("battle_storm_sea_v1");
        }

        public int Count => backgrounds.Count;

        public Texture2D ForTerrain(SeaTerrainType terrain)
        {
            return backgrounds[KeyFor(terrain)];
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

        private static Texture2D Load(string fileName)
        {
            Texture2D texture = Resources.Load<Texture2D>("BattleEnvironments/" + fileName);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            return texture;
        }
    }
}
