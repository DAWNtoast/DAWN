using System;
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
        private static readonly string[] CrewFullBodyNames =
        {
            "CrewFullBody/crew_navigator_evelyn_fullbody", "CrewFullBody/crew_carpenter_barlow_fullbody",
            "CrewFullBody/crew_doctor_moer_fullbody", "CrewFullBody/crew_gunner_leisa_fullbody",
            "CrewFullBody/crew_cook_pepe_fullbody", "CrewFullBody/crew_diver_noya_fullbody",
        };
        private readonly Func<string, Texture2D> resourceLoader;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private Texture2D RegionalShipSheet => LoadResource("LegacyDemo/ship_sprite_sheet");
        // Save identities and atlas grids remain stable. The clipper now has
        // its own four-mast flush-deck hull instead of a tinted supply vessel.
        private static readonly int[] RegionalShipCells = { 2, 0, 1, 5, 6, 3, 4, 7 };
        private static readonly Dictionary<string, string> PortBackgroundNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "断锚港", "port_hub_broken_anchor_v3" }, { "白贝港", "port_hub_white_shell_v3" },
            { "铜钟港", "port_hub_bronze_bell_v3" }, { "盐冠港", "port_hub_salt_crown_v3" },
            { "暮潮港", "port_hub_twilight_tide_v3" }, { "灰鸥港", "port_hub_gray_gull_v3" },
        };
        private readonly Dictionary<string, Texture2D> regionalCrewFullBodies = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Texture2D> regionalCrewAtlases = new Dictionary<string, Texture2D>();

        public PortArtCatalog() : this(Resources.Load<Texture2D>) { }

        internal PortArtCatalog(Func<string, Texture2D> resourceLoader)
        {
            this.resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        }

        public Texture2D PortBackground => Load("port_hub_broken_anchor_v3");
        public Texture2D ShipSheet => Load("ship_roster_sheet");
        public Texture2D SpecialCrewSheet => Load("special_crew_sheet");
        // Ordinary/hostile portraits retain their own atlas, separate from special crew.
        public Texture2D BoardingPortraitSheet => LoadResource("LegacyDemo/crew_portrait_sheet");
        public Texture2D UpgradeSheet => Load("upgrade_parts_sheet");
        public Texture2D BattleBackdropSheet => Load("battle_backdrop_sheet");
        public Texture2D TavernCrewDossierParchment => Load("UI/tavern_crew_dossier_parchment_v1");
        public Texture2D DossierFrame => Load("UI/tavern_crew_dossier_frame_4k_layers_v1");

        /// <summary>The crew portrait is painted into its complete parchment; load only the selected identity.</summary>
        public Texture2D CrewDossier(string crewId) => string.IsNullOrWhiteSpace(crewId)
            ? TavernCrewDossierParchment
            : Load("CrewDossiers/" + crewId + "_dossier_v1") ?? TavernCrewDossierParchment;

        /// <summary>Loads only the selected crew's background and half-body portrait.</summary>
        public Texture2D CrewDossierPortrait(string crewId) => string.IsNullOrWhiteSpace(crewId)
            ? null
            : Load("CrewDossiers/" + crewId + "_portrait_v2");

        public Texture2D TavernDossierTrialAtlas(string crewId) => TryTavernDossierTrialUv(crewId, out _)
            ? Load("CrewRegional/crew_broken_anchor_portraits_4k_layers_v1")
            : null;

        public Texture2D TavernDossierTrialFrame(string crewId) => TryTavernDossierTrialUv(crewId, out _)
            ? DossierFrame
            : null;

        public static bool TryTavernDossierTrialUv(string crewId, out Rect uv)
        {
            int index;
            switch (crewId)
            {
                case "crew_broken_anchor_navigator_lorne": index = 0; break;
                case "crew_broken_anchor_carpenter_kia": index = 1; break;
                case "crew_broken_anchor_doctor_hector": index = 2; break;
                case "crew_broken_anchor_gunner_mira": index = 3; break;
                case "crew_broken_anchor_cook_tam": index = 4; break;
                case "crew_broken_anchor_diver_udo": index = 5; break;
                default: uv = default; return false;
            }

            // Reviewed exclusive pixel bounds omit the generated black grid
            // and its JPEG fringe. Preserve the original image; crop only UVs.
            int column = index % 3;
            int left = column == 0 ? 0 : column == 1 ? 1372 : 2740;
            int right = column == 0 ? 1356 : column == 1 ? 2723 : 4096;
            int top = index < 3 ? 0 : 2056;
            int bottom = index < 3 ? 2040 : 4096;
            uv = new Rect(left / 4096f, 1f - bottom / 4096f, (right - left) / 4096f, (bottom - top) / 4096f);
            return true;
        }

        /// <summary>Releases catalog references without destroying art still borrowed by live UI.</summary>
        public void ReleaseLoadedTextures()
        {
            textures.Clear();
            regionalCrewFullBodies.Clear();
            regionalCrewAtlases.Clear();
        }

        public bool IsAvailable
        {
            get
            {
                bool available = PortBackgroundNames.Count == 6;
                foreach (string name in PortBackgroundNames.Values) available &= Load(name) != null;
                available &= ShipSheet != null;
                available &= SpecialCrewSheet != null;
                available &= BoardingPortraitSheet != null;
                available &= RegionalShipSheet != null;
                available &= UpgradeSheet != null;
                available &= BattleBackdropSheet != null;
                available &= TavernCrewDossierParchment != null;
                for (int index = 0; index < CrewFullBodyNames.Length; index++) available &= CrewFullBody(index) != null;
                return available;
            }
        }

        public int CrewFullBodyCount => CrewFullBodyNames.Length;
        public int PortBackgroundCount => PortBackgroundNames.Count;

        public Texture2D PortBackgroundFor(string portName)
        {
            return !string.IsNullOrWhiteSpace(portName) && PortBackgroundNames.TryGetValue(portName, out string name)
                ? Load(name)
                : PortBackground;
        }

        public Texture2D CrewFullBody(int index)
        {
            return Load(CrewFullBodyNames[Mathf.Clamp(index, 0, CrewFullBodyNames.Length - 1)]);
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
            index >= 4 && index < 4 + RegionalShipCells.Length ? RegionalShipSheet : ShipSheet;

        public Color ShipTint(int index) => Color.white;

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

        private Texture2D Load(string name)
        {
            string path = "PortArt/" + name;
            if (textures.TryGetValue(path, out Texture2D texture)) return texture;
            texture = resourceLoader(path);
            if (texture != null)
            {
                // Port backdrops are painted illustrations.  Bilinear sampling
                // keeps older regional scenes visually consistent with the newer
                // v2 backgrounds when they are cropped to the 1920x932 viewport.
                // Redrawn ships have authored alpha and padded cells; sample
                // their fine painted rigging smoothly. Other icon atlases keep
                // their existing point sampling.
                bool atlas = name.EndsWith("_sheet", System.StringComparison.Ordinal) && name != "ship_roster_sheet";
                texture.filterMode = atlas ? FilterMode.Point : FilterMode.Bilinear;
                texture.anisoLevel = atlas ? 0 : 2;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            textures.Add(path, texture);
            return texture;
        }

        private Texture2D LoadResource(string path)
        {
            if (textures.TryGetValue(path, out Texture2D texture)) return texture;
            texture = resourceLoader(path);
            if (texture != null)
            {
                texture.filterMode = path == "LegacyDemo/ship_sprite_sheet" ? FilterMode.Bilinear : FilterMode.Point;
                texture.anisoLevel = 0;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            textures.Add(path, texture);
            return texture;
        }
    }
}
