using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Stable main-menu achievement IDs mapped to shared atlas art.</summary>
    public static class AchievementIconCatalog
    {
        public const string AtlasAResourcePath = "AchievementIcons/achievements_a_v1";
        public const string AtlasBResourcePath = "AchievementIcons/achievements_b_v1";
        public const string AtlasCResourcePath = "AchievementIcons/achievements_c_v1";

        // Top-left source-pixel bounds measured from the authored PNGs, including
        // five pixels of padding. Reference dimensions preserve UVs after import scaling.
        private const float AtlasAReferenceWidth = 1402f;
        private const float AtlasAReferenceHeight = 1122f;
        private const float AtlasBReferenceWidth = 1402f;
        private const float AtlasBReferenceHeight = 1122f;
        private const float AtlasCReferenceWidth = 1983f;
        private const float AtlasCReferenceHeight = 793f;
        private static readonly Rect[] AtlasABounds =
        {
            new Rect(23, 23, 246, 245), new Rect(300, 16, 248, 252),
            new Rect(577, 23, 249, 245), new Rect(851, 23, 248, 245), new Rect(1130, 23, 247, 246),
            new Rect(23, 289, 246, 245), new Rect(301, 290, 247, 245),
            new Rect(576, 289, 249, 245), new Rect(856, 289, 248, 246), new Rect(1128, 289, 249, 246),
            new Rect(24, 561, 248, 246), new Rect(299, 562, 250, 245),
            new Rect(577, 562, 249, 245), new Rect(847, 561, 264, 247), new Rect(1128, 562, 248, 246),
            new Rect(25, 818, 248, 250), new Rect(300, 821, 248, 248),
            new Rect(574, 819, 253, 251), new Rect(853, 820, 248, 248), new Rect(1127, 820, 251, 249),
        };
        private static readonly Rect[] AtlasBBounds =
        {
            new Rect(23, 21, 258, 255), new Rect(297, 21, 259, 256),
            new Rect(571, 21, 259, 255), new Rect(846, 20, 257, 256), new Rect(1121, 20, 257, 256),
            new Rect(22, 284, 260, 257), new Rect(297, 284, 260, 257),
            new Rect(572, 286, 258, 255), new Rect(845, 284, 259, 257), new Rect(1118, 284, 262, 257),
            new Rect(23, 549, 259, 257), new Rect(297, 549, 261, 257),
            new Rect(572, 549, 259, 257), new Rect(845, 549, 260, 257), new Rect(1119, 550, 260, 257),
            new Rect(21, 812, 261, 259), new Rect(296, 814, 263, 257),
            new Rect(572, 813, 259, 258), new Rect(845, 814, 259, 257), new Rect(1116, 808, 264, 263),
        };
        private static readonly Rect[] AtlasCBounds =
        {
            new Rect(38, 34, 361, 354), new Rect(419, 36, 378, 354),
            new Rect(807, 34, 363, 356), new Rect(1198, 36, 365, 356), new Rect(1579, 34, 363, 357),
            new Rect(37, 396, 362, 359), new Rect(422, 399, 362, 355),
            new Rect(807, 399, 362, 353), new Rect(1198, 398, 362, 355), new Rect(1586, 398, 357, 355),
        };

        // Authored in Presentation.AchievementCatalog order. Keep explicit IDs so
        // later catalogue reordering cannot silently assign a different medal.
        private static readonly Dictionary<string, int> Cells = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "tutorial_departure", 0 }, { "tutorial_reward", 1 }, { "tutorial_checkpoint", 2 },
            { "tutorial_deck", 3 }, { "tutorial_component", 4 }, { "tutorial_recovery", 5 },
            { "tutorial_navigator", 6 }, { "tutorial_warehouse", 7 }, { "component_loadout", 8 },
            { "gold_600", 9 }, { "gold_900", 10 }, { "gold_1200", 11 }, { "gold_2000", 12 },
            { "port_gray_gull", 13 }, { "port_white_shell", 14 }, { "port_bronze_bell", 15 },
            { "port_salt_crown", 16 }, { "port_twilight", 17 }, { "all_ports", 18 }, { "invest_port", 19 },
            { "invest_all_ports", 20 }, { "invest_ten", 21 }, { "ship_second", 22 }, { "ship_third", 23 },
            { "ship_fourth", 24 }, { "all_ships", 25 }, { "component_inventory", 26 }, { "component_cannon", 27 },
            { "component_full", 28 }, { "component_shop", 29 }, { "crew_one", 30 }, { "crew_three", 31 },
            { "crew_six", 32 }, { "crew_variants", 33 }, { "dispatch_prepare", 34 }, { "dispatch_long", 35 },
            { "dispatch_resolved", 36 }, { "warehouse_one", 37 }, { "warehouse_three", 38 }, { "warehouse_ten", 39 },
            { "warehouse_display_six", 40 }, { "chart_drawn", 41 }, { "battle_deck", 42 }, { "battle_stages", 43 },
            { "card_upgrade", 44 }, { "special_card", 45 }, { "cargo_ready", 46 }, { "cargo_all", 47 },
            { "fame", 48 }, { "seventh_harbor", 49 },
        };

        private static Texture2D atlasA;
        private static Texture2D atlasB;
        private static Texture2D atlasC;
        private static bool atlasALoaded;
        private static bool atlasBLoaded;
        private static bool atlasCLoaded;

        public static ItemIconArt ForId(string id)
        {
            CoreAchievementDefinition definition = CoreAchievementCatalog.Find(id);
            if (definition == null || !Cells.TryGetValue(definition.Id, out int index)) return default;
            if (index < 20)
                return AtlasCell(index, AtlasABounds, AtlasAReferenceWidth, AtlasAReferenceHeight,
                    AtlasAResourcePath, ref atlasA, ref atlasALoaded);
            if (index < 40)
                return AtlasCell(index - 20, AtlasBBounds, AtlasBReferenceWidth, AtlasBReferenceHeight,
                    AtlasBResourcePath, ref atlasB, ref atlasBLoaded);
            return AtlasCell(index - 40, AtlasCBounds, AtlasCReferenceWidth, AtlasCReferenceHeight,
                AtlasCResourcePath, ref atlasC, ref atlasCLoaded);
        }

        private static ItemIconArt AtlasCell(int cell, Rect[] bounds, float referenceWidth, float referenceHeight,
            string resourcePath, ref Texture2D texture, ref bool loaded)
        {
            if (cell < 0 || cell >= bounds.Length || referenceWidth <= 0f || referenceHeight <= 0f) return default;
            Rect source = bounds[cell];
            if (source.width <= 0f || source.height <= 0f) return default;
            if (!loaded)
            {
                texture = Resources.Load<Texture2D>(resourcePath);
                loaded = true;
            }
            return texture != null ? new ItemIconArt(texture, new Rect(source.x / referenceWidth,
                1f - source.yMax / referenceHeight, source.width / referenceWidth, source.height / referenceHeight)) : default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            atlasA = null;
            atlasB = null;
            atlasC = null;
            atlasALoaded = false;
            atlasBLoaded = false;
            atlasCLoaded = false;
        }
    }
}
