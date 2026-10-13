using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Loads and indexes the original browser demo's pixel-art sheets. Keeping
    /// the crop metadata here avoids duplicating source images into many files.
    /// </summary>
    public sealed class LegacyDemoArtCatalog
    {
        private readonly Dictionary<string, Vector2Int> encounterCells = new Dictionary<string, Vector2Int>
        {
            { "event_reef_broken_mast", new Vector2Int(0, 0) },
            { "event_reef_shortcut", new Vector2Int(1, 0) },
            { "event_reef_sunken_boat", new Vector2Int(2, 0) },
            { "event_reef_saltwater_leak", new Vector2Int(3, 0) },
            { "event_reef_fisher_cache", new Vector2Int(4, 0) },
            { "event_reef_coral_ambush", new Vector2Int(0, 2) },
            { "gold_convoy", new Vector2Int(0, 0) },
            { "gold_customs", new Vector2Int(4, 0) },
            { "gold_wreck", new Vector2Int(2, 0) },
            { "moon_obelisk", new Vector2Int(3, 1) },
            { "moon_whisper", new Vector2Int(2, 2) },
            { "moon_ghost", new Vector2Int(0, 2) },
            { "random_ambush_warning", new Vector2Int(1, 2) },
            { "random_ambush", new Vector2Int(0, 2) },
            { "tutorial_first_event", new Vector2Int(0, 0) },
            { "tutorial_optional_event", new Vector2Int(2, 1) },
            { "tutorial_crew_requirement_event", new Vector2Int(3, 2) },
            { "tutorial_ambush_event", new Vector2Int(0, 2) },
            { "tutorial_navigator_event", new Vector2Int(1, 3) },
            { "tutorial_hidden_event", new Vector2Int(3, 3) },
            { "tutorial_boss_event", new Vector2Int(4, 3) },
        };

        private readonly Dictionary<string, Vector2Int> cardCells = new Dictionary<string, Vector2Int>
        {
            { "card_chain_shot", new Vector2Int(1, 0) },
            { "card_full_sail_turn", new Vector2Int(3, 0) },
            { "card_evasive_maneuver", new Vector2Int(0, 1) },
            { "card_boarding_hook", new Vector2Int(1, 1) },
            { "card_cutlass_raid", new Vector2Int(2, 1) },
            { "card_emergency_repair", new Vector2Int(3, 1) },
            { "card_cannon_barrage", new Vector2Int(0, 0) },
            { "card_brace", new Vector2Int(0, 2) },
        };

        // The generated 1254 px card sheet contains 16–24 px gutters and is not
        // evenly divisible into four art rectangles. These measured frame bounds
        // keep the navy gutter outside the RawImage UV instead of treating it as
        // part of the card illustration.
        private static readonly int[] CardLeftPixels = { 23, 329, 634, 939 };
        private static readonly int[] CardRightPixels = { 313, 619, 923, 1230 };
        private static readonly int[] CardTopPixels = { 25, 329, 634, 937 };
        private static readonly int[] CardBottomPixels = { 313, 618, 921, 1226 };

        private static readonly string[] RouteMapNames =
            { "shattered_sail_route_map_v2", "golden_tide_route_map_v2", "moon_tide_route_map_v2" };
        private static readonly string[] EventSheetNames =
            { "shattered_sail_event_sheet_v2", "golden_tide_event_sheet_v2", "moon_tide_event_sheet_v2" };
        private readonly Func<string, Texture2D> resourceLoader;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public LegacyDemoArtCatalog() : this(Resources.Load<Texture2D>) { }

        internal LegacyDemoArtCatalog(Func<string, Texture2D> resourceLoader)
        {
            this.resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        }

        public Texture2D ShipSheet => Load("ship_sprite_sheet");
        public Texture2D ResourceSheet => Load("resource_icon_sheet");
        public Texture2D CardSheet => Load("card_icon_sheet");
        public Texture2D CrewSheet => Load("crew_portrait_sheet");
        /// <summary>Releases cached references; live UI and Resources still own the borrowed textures.</summary>
        public void ReleaseLoadedTextures() => textures.Clear();

        // Availability is an explicit full validation; normal lookups load only their own art.
        public bool IsAvailable
        {
            get
            {
                bool available = true;
                for (int index = 0; index < RouteMapNames.Length; index++)
                {
                    available &= RouteMap(index) != null;
                    available &= EventSheet(index) != null;
                }
                available &= ShipSheet != null;
                available &= ResourceSheet != null;
                available &= CardSheet != null;
                available &= CrewSheet != null;
                return available;
            }
        }

        public Texture2D RouteMap(int routeIndex)
        {
            return Load(RouteMapNames[Mathf.Clamp(routeIndex, 0, RouteMapNames.Length - 1)]);
        }

        public Texture2D EventSheet(int routeIndex)
        {
            return Load(EventSheetNames[Mathf.Clamp(routeIndex, 0, EventSheetNames.Length - 1)]);
        }

        public Rect EncounterUv(CoreEncounter encounter)
        {
            if (encounter == null) return CellUv(0, 0, 5, 4);
            if (encounter.Id.StartsWith("boss_", StringComparison.Ordinal)) return CellUv(4, 3, 5, 4);
            return EncounterUv(encounter.Id);
        }

        public Rect EncounterUv(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId)) return CellUv(0, 0, 5, 4);
            if (!encounterCells.TryGetValue(encounterId, out Vector2Int cell))
            {
                uint hash = ShatteredSail.Core.Random.SeedHash.Compute(encounterId);
                cell = new Vector2Int((int)(hash % 5), (int)((hash / 5) % 4));
            }
            return CellUv(cell.x, cell.y, 5, 4);
        }

        public Rect CardUv(string cardId)
        {
            Vector2Int cell;
            if (!cardCells.TryGetValue(cardId, out cell))
            {
                cell = new Vector2Int(1, 2);
            }

            if (CardSheet == null)
            {
                return CellUv(cell.x, cell.y, 4, 4);
            }

            int column = Mathf.Clamp(cell.x, 0, CardLeftPixels.Length - 1);
            int row = Mathf.Clamp(cell.y, 0, CardTopPixels.Length - 1);
            float textureWidth = CardSheet.width;
            float textureHeight = CardSheet.height;
            int left = CardLeftPixels[column];
            int right = CardRightPixels[column];
            int top = CardTopPixels[row];
            int bottom = CardBottomPixels[row];
            return new Rect(
                left / textureWidth,
                (textureHeight - bottom) / textureHeight,
                (right - left) / textureWidth,
                (bottom - top) / textureHeight);
        }

        public Rect ShipUv(int column, int row)
        {
            return CellUv(column, row, 4, 2);
        }

        public Rect CrewUv(int column, int row)
        {
            return CellUv(column, row, 4, 3);
        }

        public Rect ResourceUv(int column, int row)
        {
            return CellUv(column, row, 6, 4);
        }

        public static Rect CellUv(int column, int rowFromTop, int columns, int rows)
        {
            float width = 1f / columns;
            float height = 1f / rows;
            return new Rect(column * width, 1f - (rowFromTop + 1) * height, width, height);
        }

        private Texture2D Load(string name)
        {
            if (textures.TryGetValue(name, out Texture2D texture)) return texture;
            texture = resourceLoader("LegacyDemo/" + name);
            if (texture != null)
            {
                bool mapThumbnail = name.EndsWith("_route_map_v2", StringComparison.Ordinal);
                bool paintedEvent = name.EndsWith("_event_sheet_v2", StringComparison.Ordinal);
                texture.filterMode = mapThumbnail ? FilterMode.Trilinear :
                    paintedEvent || name == "ship_sprite_sheet" ? FilterMode.Bilinear : FilterMode.Point;
                if (mapThumbnail) texture.anisoLevel = 4;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            textures.Add(name, texture);
            return texture;
        }
    }
}
