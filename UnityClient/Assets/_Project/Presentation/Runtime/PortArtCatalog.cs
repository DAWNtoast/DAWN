using System.Collections.Generic;
using ShatteredSail.Content;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Accesses the generated port-outfitting art without requiring individual
    /// sprite assets. Atlas cells stay deterministic and inexpensive to load.
    /// </summary>
    public sealed class PortArtCatalog
    {
        private readonly Texture2D[] crewFullBodies;
        private readonly Texture2D regionalShipSheet;
        // New regional vessels reuse the established wide hull illustrations.
        // The first four identities keep their original atlas cells for saves.
        private static readonly int[] RegionalShipCells = { 2, 0, 1, 5, 6, 3, 4, 1 };
        private readonly Dictionary<string, Texture2D> portBackgrounds;
        private readonly Dictionary<string, Texture2D> regionalCrewFullBodies = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Texture2D> regionalCrewAtlases = new Dictionary<string, Texture2D>();

        public PortArtCatalog()
        {
            PortBackground = Load("port_hub_broken_anchor_v3");
            portBackgrounds = new Dictionary<string, Texture2D>
            {
                { "断锚港", PortBackground },
                { "白贝港", Load("port_hub_white_shell_v3") },
                { "铜钟港", Load("port_hub_bronze_bell_v3") },
                { "盐冠港", Load("port_hub_salt_crown_v3") },
                { "暮潮港", Load("port_hub_twilight_tide_v3") },
                { "灰鸥港", Load("port_hub_gray_gull_v3") },
            };
            ShipSheet = Load("ship_roster_sheet");
            regionalShipSheet = LoadResource("LegacyDemo/ship_sprite_sheet");
            SpecialCrewSheet = Load("special_crew_sheet");
            // This legacy sheet is intentionally kept separate from the
            // special-crew atlas. Its cells provide the fixed ordinary sailors
            // and the dedicated hostile boarding portraits.
            BoardingPortraitSheet = LoadResource("LegacyDemo/crew_portrait_sheet");
            UpgradeSheet = Load("upgrade_parts_sheet");
            BattleBackdropSheet = Load("battle_backdrop_sheet");
            TavernCrewDossierParchment = Load("UI/tavern_crew_dossier_parchment_v1");
            crewFullBodies = new[]
            {
                Load("CrewFullBody/crew_navigator_evelyn_fullbody"),
                Load("CrewFullBody/crew_carpenter_barlow_fullbody"),
                Load("CrewFullBody/crew_doctor_moer_fullbody"),
                Load("CrewFullBody/crew_gunner_leisa_fullbody"),
                Load("CrewFullBody/crew_cook_pepe_fullbody"),
                Load("CrewFullBody/crew_diver_noya_fullbody"),
            };
        }

        public Texture2D PortBackground { get; }
        public Texture2D ShipSheet { get; }
        public Texture2D SpecialCrewSheet { get; }
        public Texture2D BoardingPortraitSheet { get; }
        public Texture2D UpgradeSheet { get; }
        public Texture2D BattleBackdropSheet { get; }
        public Texture2D TavernCrewDossierParchment { get; }

        public bool IsAvailable
        {
            get
            {
                if (PortBackground == null || portBackgrounds.Count != 6 || ShipSheet == null || SpecialCrewSheet == null ||
                    BoardingPortraitSheet == null || regionalShipSheet == null ||
                    UpgradeSheet == null || BattleBackdropSheet == null || TavernCrewDossierParchment == null)
                {
                    return false;
                }

                foreach (Texture2D background in portBackgrounds.Values)
                {
                    if (background == null) return false;
                }

                for (int index = 0; index < crewFullBodies.Length; index += 1)
                {
                    if (crewFullBodies[index] == null) return false;
                }
                return true;
            }
        }

        public int CrewFullBodyCount => crewFullBodies.Length;
        public int PortBackgroundCount => portBackgrounds.Count;

        public Texture2D PortBackgroundFor(string portName)
        {
            return !string.IsNullOrWhiteSpace(portName) && portBackgrounds.TryGetValue(portName, out Texture2D background)
                ? background
                : PortBackground;
        }

        public Texture2D CrewFullBody(int index)
        {
            return crewFullBodies[Mathf.Clamp(index, 0, crewFullBodies.Length - 1)];
        }

        /// <summary>
        /// Resolves a port-specific dossier portrait and keeps the original six
        /// full-body paintings as a safe fallback while regional art is being
        /// produced.  Generated variants live under PortArt/CrewRegional.
        /// </summary>
        public Texture2D CrewFullBody(string assetName, int fallbackIndex)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return CrewFullBody(fallbackIndex);
            if (!regionalCrewFullBodies.TryGetValue(assetName, out Texture2D texture))
            {
                texture = Load("CrewRegional/" + assetName);
                if (texture == null) texture = Load("CrewFullBody/" + assetName);
                regionalCrewFullBodies[assetName] = texture;
            }

            return texture != null ? texture : CrewFullBody(fallbackIndex);
        }

        public bool HasRegionalCrewFullBody(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return false;
            if (!regionalCrewFullBodies.TryGetValue(assetName, out Texture2D texture))
            {
                texture = Load("CrewRegional/" + assetName);
                if (texture == null) texture = Load("CrewFullBody/" + assetName);
                regionalCrewFullBodies[assetName] = texture;
            }
            return texture != null;
        }

        public Texture2D CrewPortrait(CrewVariantDefinition crew)
        {
            if (crew == null) return CrewFullBody(0);
            Texture2D atlas = RegionalCrewAtlas(crew.PortName);
            return atlas != null ? atlas : CrewFullBody(crew.PortraitAssetName, crew.ProfessionIndex);
        }

        public Rect CrewPortraitUv(CrewVariantDefinition crew, bool headOnly = false)
        {
            int index = crew == null ? 0 : Mathf.Clamp(crew.ProfessionIndex, 0, 5);
            bool regionalPortrait = crew != null && RegionalCrewAtlas(crew.PortName) != null;
            Rect cell = regionalPortrait
                ? LegacyDemoArtCatalog.CellUv(index % 3, index / 3, 3, 2)
                : new Rect(0f, 0f, 1f, 1f);
            // Crop within the selected cell, never across neighbouring identities.
            // v2 contains square parchment-backed busts, not transparent bodies.
            if (headOnly && regionalPortrait)
                return new Rect(cell.x + cell.width * 0.15f, cell.y + cell.height * 0.30f,
                    cell.width * 0.7f, cell.height * 0.65f);
            return headOnly
                ? new Rect(cell.x + cell.width * 0.15f, cell.y + cell.height * 0.48f, cell.width * 0.7f, cell.height * 0.45f)
                : cell;
        }

        public Texture2D RegionalCrewAtlas(string portName)
        {
            string slug;
            switch (portName)
            {
                case "断锚港": slug = "broken_anchor"; break;
                case "白贝港": slug = "white_shell"; break;
                case "铜钟港": slug = "bronze_bell"; break;
                case "盐冠港": slug = "salt_crown"; break;
                case "暮潮港": slug = "twilight_tide"; break;
                default: slug = "gray_gull"; break;
            }
            if (!regionalCrewAtlases.TryGetValue(slug, out Texture2D atlas))
            {
                // Reject the earlier seated v1 commission: its painted checker
                // background and cross-cell feet make it unsuitable as a cutout.
                atlas = Load("CrewRegional/crew_" + slug + "_portraits_v2");
                regionalCrewAtlases[slug] = atlas;
            }
            return atlas;
        }

        public Rect ShipUv(int index)
        {
            if (index >= 4 && index < 4 + RegionalShipCells.Length)
            {
                int cell = RegionalShipCells[index - 4];
                return LegacyDemoArtCatalog.CellUv(cell % 4, cell / 4, 4, 2);
            }
            int safe = Mathf.Clamp(index, 0, 3);
            return LegacyDemoArtCatalog.CellUv(safe % 2, safe / 2, 2, 2);
        }

        public Texture2D ShipTexture(int index) =>
            index >= 4 && index < 4 + RegionalShipCells.Length ? regionalShipSheet : ShipSheet;

        public Color ShipTint(int index) => index == 6 ? new Color(0.86f, 0.91f, 1f) :
            index == 11 ? new Color(0.85f, 1f, 0.93f) : Color.white;

        public Rect SpecialCrewUv(int index)
        {
            int safe = Mathf.Clamp(index, 0, 5);
            return LegacyDemoArtCatalog.CellUv(safe % 3, safe / 3, 3, 2);
        }

        /// <summary>Portrait cells reserved for hostile boarding parties.</summary>
        public Rect EnemyBoardingPortraitUv(int index)
        {
            // Distinct faces from the legacy sailor sheet; none come from the
            // six-cell special-crew atlas used by the player's roster.
            int[] cells = { 0, 5, 11 };
            int safe = cells[Mathf.Clamp(index, 0, cells.Length - 1)];
            return LegacyDemoArtCatalog.CellUv(safe % 4, safe / 4, 4, 3);
        }

        /// <summary>Fixed ordinary sailor cells used when the player has fewer than three special crew.</summary>
        public Rect OrdinaryBoardingPortraitUv(int index)
        {
            int[] cells = { 4, 7, 10 };
            int safe = cells[Mathf.Clamp(index, 0, cells.Length - 1)];
            return LegacyDemoArtCatalog.CellUv(safe % 4, safe / 4, 4, 3);
        }

        public Rect UpgradeUv(int index)
        {
            int safe = Mathf.Clamp(index, 0, 5);
            return LegacyDemoArtCatalog.CellUv(safe % 3, safe / 3, 3, 2);
        }

        public Rect BattleBackdropUv(int routeIndex, bool isBoss)
        {
            int cell = isBoss ? 3 : Mathf.Clamp(routeIndex, 0, 2);
            return LegacyDemoArtCatalog.CellUv(cell % 2, cell / 2, 2, 2);
        }

        private static Texture2D Load(string name)
        {
            Texture2D texture = Resources.Load<Texture2D>("PortArt/" + name);
            if (texture != null)
            {
                // Port backdrops are painted illustrations.  Bilinear sampling
                // keeps older regional scenes visually consistent with the newer
                // v2 backgrounds when they are cropped to the 1920x932 viewport.
                // The roster/crew/upgrade sheets are atlases, however: preserve
                // point sampling there so the cutout shader cannot blend a cell
                // separator into a neighbouring ship or portrait.
                bool atlas = name.EndsWith("_sheet", System.StringComparison.Ordinal);
                texture.filterMode = atlas ? FilterMode.Point : FilterMode.Bilinear;
                texture.anisoLevel = atlas ? 0 : 2;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            return texture;
        }

        private static Texture2D LoadResource(string path)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.anisoLevel = 0;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            return texture;
        }
    }
}
