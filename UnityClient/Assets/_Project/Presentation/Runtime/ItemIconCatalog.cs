using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public readonly struct ItemIconArt
    {
        public ItemIconArt(Texture2D texture, Rect uv)
        {
            Texture = texture;
            Uv = uv;
        }

        public Texture2D Texture { get; }
        public Rect Uv { get; }
        public bool IsAvailable => Texture != null;
    }

    /// <summary>Shared item art for RawImage controls, without allocating per-item sprites.</summary>
    public static class ItemIconCatalog
    {
        public const string AtlasResourcePath = "ItemIcons/items_small_v1";
        public const string KeepsakeAtlasAResourcePath = "ItemIcons/keepsakes_a_small_v1";
        public const string KeepsakeAtlasBResourcePath = "ItemIcons/keepsakes_b_small_v1";
        private const float AtlasReferenceWidth = 1402f;
        private const float AtlasReferenceHeight = 1122f;
        private static readonly Rect FullImage = new Rect(0f, 0f, 1f, 1f);
        // Top-left pixel bounds measured from the authored PNG, including six
        // pixels of transparent padding. Generated subjects are not evenly spaced.
        private static readonly Rect[] AtlasBounds =
        {
            new Rect(51, 30, 201, 254), new Rect(297, 62, 256, 223),
            new Rect(576, 51, 250, 230), new Rect(839, 64, 277, 213), new Rect(1136, 58, 248, 223),
            new Rect(65, 284, 163, 279), new Rect(292, 317, 251, 235),
            new Rect(579, 306, 239, 244), new Rect(850, 322, 259, 234), new Rect(1146, 327, 227, 222),
            new Rect(47, 583, 204, 218), new Rect(295, 570, 243, 244),
            new Rect(574, 566, 240, 244), new Rect(846, 577, 258, 229), new Rect(1142, 563, 228, 248),
            new Rect(59, 802, 177, 281), new Rect(322, 827, 207, 254),
            new Rect(546, 813, 274, 260), new Rect(831, 816, 278, 264), new Rect(1119, 818, 266, 265),
        };
        private static readonly Dictionary<string, int> CargoCells = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "water", 0 }, { "food", 1 }, { "wood", 2 }, { "iron", 3 }, { "cloth", 4 },
            { "beer", 5 }, { "medicine", 6 }, { "powder", 7 }, { "cannonballs", 8 },
            { "gem", 10 }, { "ancientmaterial", 11 }, { "relic", 12 },
            { "blackpearl", 13 }, { "ancientcompass", 14 }, { "lantern", 15 },
        };
        private static readonly Dictionary<string, int> SpecialCells = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "gold", 9 }, { "treasurefragment", 16 },
            { "treasuremap_weathered", 17 }, { "treasuremap_gilded", 18 }, { "treasuremap_moonlit", 19 },
        };
        private static readonly Dictionary<string, int> TreasureCells = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "first_victory_compass", 0 }, { "boatswain_bell", 1 }, { "sealed_chart", 2 },
            { "old_captain_free_flag", 3 }, { "six_port_anchor_seal", 4 }, { "no_crown_logbook", 5 },
            { "first_stormglass", 6 }, { "starboard_copper_scope", 7 }, { "saltworn_quartermaster_key", 8 },
            { "first_berth_drydock_nail", 9 }, { "godbreaker_oar", 10 }, { "leviathan_bone_whistle", 11 },
            { "moon_tide_astrolabe", 12 }, { "iron_oath_shackle", 13 }, { "thunder_crown_fuse", 14 },
            { "red_widow_grappling_hook", 15 }, { "ember_ark_fireseed", 16 }, { "tide_requiem_gauge", 17 },
            { "evelyn_six_port_ruler", 18 }, { "barlow_living_hull_plane", 19 }, { "moer_storm_suture_kit", 20 },
            { "leisa_free_flag_badge", 21 }, { "pepe_shared_pot_lid", 22 }, { "lorne_first_berth_token", 23 },
            { "atang_white_shell_button", 24 }, { "ada_bronze_bell_bandage", 25 }, { "helen_thirteenth_cannon_coin", 26 },
            { "noya_drowned_diver_lens", 27 }, { "broken_anchor_sextant", 28 }, { "salt_crown_trade_weight", 29 },
            { "twilight_tide_moon_shard", 30 }, { "empty_berth_registry", 31 }, { "nameless_sailor_nameplate", 32 },
            { "storm_altar_fragment", 33 }, { "unfinished_seventh_berth_key", 34 }, { "last_horizon_needle", 35 },
            { "free_waters_mooring_ring", 36 }, { "sea_god_debt_token", 37 }, { "inheritance_oath_inkstone", 38 },
            { "seventh_harbor_beacon_lantern", 39 },
        };
        private const float KeepsakeAReferenceWidth = 1402f;
        private const float KeepsakeAReferenceHeight = 1122f;
        private const float KeepsakeBReferenceWidth = 1402f;
        private const float KeepsakeBReferenceHeight = 1122f;
        private static readonly Rect[] KeepsakeABounds =
        {
            new Rect(22, 41, 240, 229), new Rect(335, 28, 169, 254),
            new Rect(550, 60, 289, 195), new Rect(883, 24, 243, 257), new Rect(1163, 56, 221, 197),
            new Rect(25, 313, 251, 247), new Rect(339, 298, 165, 258),
            new Rect(552, 334, 284, 201), new Rect(865, 306, 254, 252), new Rect(1164, 318, 211, 233),
            new Rect(16, 572, 250, 250), new Rect(298, 585, 230, 215),
            new Rect(577, 552, 241, 264), new Rect(855, 590, 244, 223), new Rect(1148, 599, 240, 192),
            new Rect(25, 830, 253, 247), new Rect(318, 800, 219, 273),
            new Rect(626, 831, 140, 252), new Rect(846, 858, 273, 204), new Rect(1140, 843, 246, 232),
        };
        private static readonly Rect[] KeepsakeBBounds =
        {
            new Rect(28, 30, 264, 259), new Rect(309, 31, 243, 257),
            new Rect(567, 70, 280, 203), new Rect(889, 19, 217, 277), new Rect(1139, 58, 233, 225),
            new Rect(20, 330, 256, 217), new Rect(302, 305, 247, 250),
            new Rect(582, 300, 241, 251), new Rect(855, 306, 272, 260), new Rect(1151, 312, 230, 244),
            new Rect(43, 566, 218, 247), new Rect(272, 591, 307, 226),
            new Rect(580, 617, 283, 177), new Rect(883, 571, 228, 254), new Rect(1140, 573, 240, 231),
            new Rect(26, 836, 232, 241), new Rect(297, 838, 235, 258),
            new Rect(576, 837, 255, 258), new Rect(867, 843, 278, 239), new Rect(1184, 804, 172, 288),
        };
        private static Texture2D atlas;
        private static bool atlasLoaded;
        private static Texture2D treasureAtlasA;
        private static Texture2D treasureAtlasB;
        private static bool treasureAtlasALoaded;
        private static bool treasureAtlasBLoaded;

        public static ItemIconArt ForCargo(string key) =>
            key != null && CargoCells.TryGetValue(key.Trim(), out int cell) ? AtlasCell(cell) : default;

        public static ItemIconArt ForComponent(ComponentArtEntry entry) =>
            entry != null && entry.Texture != null ? new ItemIconArt(entry.Texture, FullImage) : default;

        public static ItemIconArt ForTreasure(CoreWarehouseTreasureDefinition definition)
        {
            if (definition == null || definition.Id == null || !TreasureCells.TryGetValue(definition.Id, out int index))
                return default;
            bool firstAtlas = index < 20;
            int cell = index % 20;
            Rect[] bounds = firstAtlas ? KeepsakeABounds : KeepsakeBBounds;
            if (cell >= bounds.Length) return default;
            if (firstAtlas)
            {
                if (!treasureAtlasALoaded)
                {
                    treasureAtlasA = Resources.Load<Texture2D>(KeepsakeAtlasAResourcePath);
                    treasureAtlasALoaded = true;
                }
                return FromBounds(treasureAtlasA, bounds[cell], KeepsakeAReferenceWidth, KeepsakeAReferenceHeight);
            }
            if (!treasureAtlasBLoaded)
            {
                treasureAtlasB = Resources.Load<Texture2D>(KeepsakeAtlasBResourcePath);
                treasureAtlasBLoaded = true;
            }
            return FromBounds(treasureAtlasB, bounds[cell], KeepsakeBReferenceWidth, KeepsakeBReferenceHeight);
        }

        public static ItemIconArt ForTreasureMap(string rankId)
        {
            if (string.Equals(rankId, "weathered", StringComparison.OrdinalIgnoreCase)) return AtlasCell(17);
            if (string.Equals(rankId, "gilded", StringComparison.OrdinalIgnoreCase)) return AtlasCell(18);
            if (string.Equals(rankId, "moonlit", StringComparison.OrdinalIgnoreCase)) return AtlasCell(19);
            return default;
        }

        public static ItemIconArt ForSpecial(string key) =>
            key != null && SpecialCells.TryGetValue(key.Trim(), out int cell) ? AtlasCell(cell) : default;

        private static ItemIconArt AtlasCell(int cell)
        {
            if (!atlasLoaded)
            {
                atlas = Resources.Load<Texture2D>(AtlasResourcePath);
                atlasLoaded = true;
            }
            return FromBounds(atlas, AtlasBounds[cell], AtlasReferenceWidth, AtlasReferenceHeight);
        }

        private static ItemIconArt FromBounds(Texture2D texture, Rect bounds, float referenceWidth, float referenceHeight) =>
            texture != null ? new ItemIconArt(texture, new Rect(bounds.x / referenceWidth,
                1f - bounds.yMax / referenceHeight, bounds.width / referenceWidth, bounds.height / referenceHeight)) : default;

        // These are borrowed Resources textures. Visible RawImages keep their
        // own references while the engine collects art from the previous scene.
        public static void ReleaseLoadedTextures()
        {
            atlas = null;
            atlasLoaded = false;
            treasureAtlasA = null;
            treasureAtlasB = null;
            treasureAtlasALoaded = false;
            treasureAtlasBLoaded = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => ReleaseLoadedTextures();
    }
}
