using System;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Support-free component displays. Preserve each silhouette's native aspect
    /// and direction, with a separate regional sign below its complete image bounds.
    /// The clearance includes transparent padding and the enlarged rarity aura.
    /// </summary>
    public sealed class ComponentShopDisplayGeometry
    {
        public Rect ModelUv { get; private set; }
        public Vector2 ModelSize { get; private set; }
        public Vector2 ModelPosition { get; private set; }
        public Rect ModelBounds => new Rect(ModelPosition - ModelSize * 0.5f, ModelSize);
        public Rect PlaqueBounds => new Rect(PlaquePosition - PlaqueSize * 0.5f, PlaqueSize);
        // Keep this convenience property aspect-safe as well. Callers that
        // supply a custom fit box should use ShowroomModelPositionFor instead;
        // the default showroom placement must never assume the old fixed
        // 300x194.4 RawImage rectangle.
        public Vector2 ShowroomModelPosition => ShowroomModelPositionFor(FitShowroomModelSize(ModelUv));
        public Vector2 PlaqueSize { get; private set; } = new Vector2(252f, 168f);
        public Vector2 PlaquePosition { get; private set; }
        public const float PlaqueFloorY = -245f;
        public const float MinimumPlaqueClearance = 20f;
        public const float MaximumModelTop = 225f;
        // UIPreviewsForMe uses the left-hand component display as the authored
        // showroom reference. This is a *fit box*, not the final image size:
        // every source keeps its own UV aspect ratio inside the box. The old
        // runtime assigned this box directly to RawImage.rectTransform, which
        // stretched narrow instruments and tall sails horizontally.
        public static readonly Vector2 ShowroomModelSize = new Vector2(300f, 194.4f);
        // The dossier is intentionally larger than a shop card while still
        // fitting the same native UV region without a grey backing panel.
        public static readonly Vector2 DetailModelSize = new Vector2(360f, 300f);

        /// <summary>
        /// Fits a cropped atlas region into the authored showroom box without
        /// changing the region's aspect ratio. RawImage has no preserveAspect
        /// flag, so callers must use this result for the RectTransform size.
        /// </summary>
        public static Vector2 FitShowroomModelSize(Rect uv)
        {
            float width = Mathf.Max(0.0001f, uv.width);
            float height = Mathf.Max(0.0001f, uv.height);
            float scale = Mathf.Min(ShowroomModelSize.x / width, ShowroomModelSize.y / height);
            return new Vector2(width * scale, height * scale);
        }

        public static Vector2 FitDetailModelSize(Rect uv)
        {
            float width = Mathf.Max(0.0001f, uv.width);
            float height = Mathf.Max(0.0001f, uv.height);
            float scale = Mathf.Min(DetailModelSize.x / width, DetailModelSize.y / height);
            return new Vector2(width * scale, height * scale);
        }

        public Vector2 ShowroomModelPositionFor(Vector2 modelSize)
        {
            return new Vector2(ModelPosition.x,
                PlaqueBounds.yMax + MinimumPlaqueClearance + modelSize.y * 0.5f);
        }
        public static Rect PlaqueTextRect => new Rect(-93f, 9f, 186f, 46f);
        public Rect WritingArea { get; private set; } = PlaqueTextRect;
        public Rect PlaqueUv { get; private set; } = new Rect(0f, 0f, 1f, 1f);
        public RegionalShopFurniture RegionalFurniture { get; private set; }

        public static ComponentShopDisplayGeometry For(ComponentArtEntry entry) => For(entry, null);

        public static ComponentShopDisplayGeometry For(ComponentArtEntry entry, string port)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var result = new ComponentShopDisplayGeometry();
            // Keep measured feet on the same floor. Only the regional sign is used:
            // no rack, plinth, front lip or load-bearing contact is added to the part.
            result.PlaquePosition = new Vector2(0f,
                PlaqueFloorY - (42f / 1024f - 0.5f) * result.PlaqueSize.y);
            var regional = RegionalShopFurniture.For(port);
            if (regional != null)
            {
                result.RegionalFurniture = regional;
                var sign = regional.Sign;
                result.PlaqueSize = sign.Size;
                result.PlaqueUv = sign.Uv;
                float lowestFoot = float.MaxValue;
                foreach (Vector2 foot in sign.Feet)
                    lowestFoot = Mathf.Min(lowestFoot, sign.Local(foot).y);
                result.PlaquePosition = new Vector2(0f, PlaqueFloorY - lowestFoot);
                Vector2 tl = sign.Local(sign.WritingPixels.min), br = sign.Local(sign.WritingPixels.max);
                result.WritingArea = Rect.MinMaxRect(tl.x + 3f, br.y + 3f, br.x - 3f, tl.y - 3f);
            }

            // UIPreviewsForMe uses one enlarged writing bay for every component
            // plaque. Keep each regional sign's authored center, but expose the
            // shared 300x100 inner bay to runtime layout and validation.
            Vector2 writingCenter = result.WritingArea.center;
            result.WritingArea = new Rect(writingCenter - new Vector2(150f, 50f), new Vector2(300f, 100f));

            Rect opaque = OpaquePixels(entry.ArtSourceId);
            result.ModelUv = Rect.MinMaxRect(Mathf.Max(0f, opaque.xMin - 12f) / 512f,
                Mathf.Max(0f, 512f - opaque.yMax - 12f) / 512f,
                Mathf.Min(512f, opaque.xMax + 12f) / 512f,
                Mathf.Min(512f, 512f - opaque.yMin + 12f) / 512f);
            float bottom = result.PlaqueBounds.yMax + MinimumPlaqueClearance;
            float height = Mathf.Min(260f, MaximumModelTop - bottom);
            float width = entry.Slot == ComponentSlot.Instrument ? 230f : 300f;
            float scale = Mathf.Min(width / result.ModelUv.width, height / result.ModelUv.height);
            result.ModelSize = result.ModelUv.size * scale;
            result.ModelPosition = new Vector2(0f, bottom + result.ModelSize.y * 0.5f);
            return result;
        }

        private static Rect OpaquePixels(string id)
        {
            // Measured at alpha >= 8 from each normalized 1024 px sprite, expressed
            // in the existing 512 px coordinate space. The caller adds 24 native
            // pixels of clearance for translucent edges and fine rigging tips.
            switch (id)
            {
                case "bow_oak_ram": return Rect.MinMaxRect(49.5f, 111.0f, 463.5f, 401.5f);
                case "bow_ironwood_beak": return Rect.MinMaxRect(49.0f, 104.5f, 463.0f, 406.5f);
                case "bow_ashwave_wedge": return Rect.MinMaxRect(48.5f, 134.5f, 463.0f, 377.5f);
                case "bow_harborbreaker": return Rect.MinMaxRect(49.5f, 87.0f, 463.0f, 425.0f);
                case "bow_sawtooth_drift": return Rect.MinMaxRect(49.0f, 119.5f, 463.5f, 392.0f);
                case "bow_reef_cutter": return Rect.MinMaxRect(48.5f, 95.0f, 463.0f, 416.5f);
                case "bow_coral_parting_blade": return Rect.MinMaxRect(49.0f, 71.0f, 463.0f, 440.5f);
                case "bow_smuggler_needle": return Rect.MinMaxRect(49.5f, 167.5f, 463.0f, 345.0f);
                case "bow_tidehook_ram": return Rect.MinMaxRect(51.0f, 49.0f, 460.0f, 463.5f);
                case "bow_sprayfox_prow": return Rect.MinMaxRect(48.5f, 115.0f, 463.5f, 397.0f);
                case "bow_gilded_mermaid": return Rect.MinMaxRect(100.5f, 49.0f, 411.5f, 463.0f);
                case "bow_sunlion_figurehead": return Rect.MinMaxRect(51.0f, 49.0f, 460.5f, 463.5f);
                case "bow_pearl_siren": return Rect.MinMaxRect(48.5f, 62.0f, 463.0f, 449.5f);
                case "bow_guildbreaker_crest": return Rect.MinMaxRect(49.5f, 68.0f, 463.5f, 443.5f);
                case "bow_amber_albatross": return Rect.MinMaxRect(48.5f, 50.5f, 463.0f, 461.0f);
                case "bow_abyssal_fang": return Rect.MinMaxRect(49.0f, 149.0f, 463.5f, 363.0f);
                case "bow_nightkraken_tusk": return Rect.MinMaxRect(48.5f, 116.0f, 463.5f, 396.0f);
                case "bow_gravecurrent_spur": return Rect.MinMaxRect(48.5f, 136.5f, 463.0f, 375.5f);
                case "bow_blackreef_hammer": return Rect.MinMaxRect(49.5f, 67.5f, 463.5f, 444.5f);
                case "bow_voidray_edge": return Rect.MinMaxRect(60.5f, 49.0f, 452.5f, 463.5f);
                case "bow_leviathan_ram": return Rect.MinMaxRect(49.0f, 119.5f, 463.5f, 392.0f);
                case "bow_crownless_colossus": return Rect.MinMaxRect(49.5f, 53.5f, 463.0f, 459.0f);
                case "bow_worldsplitter": return Rect.MinMaxRect(48.5f, 74.5f, 463.0f, 437.0f);
                case "bow_dragonwake_ram": return Rect.MinMaxRect(49.0f, 58.5f, 463.5f, 453.0f);
                case "bow_last_horizon": return Rect.MinMaxRect(49.0f, 52.0f, 465.5f, 460.0f);
                case "sail_patched_lateen": return Rect.MinMaxRect(73.5f, 49.0f, 438.5f, 463.5f);
                case "sail_coast_runner": return Rect.MinMaxRect(49.0f, 83.0f, 463.0f, 429.0f);
                case "sail_tarred_square": return Rect.MinMaxRect(57.0f, 48.5f, 455.0f, 463.5f);
                case "sail_gullwing_cloth": return Rect.MinMaxRect(48.5f, 74.0f, 463.5f, 438.0f);
                case "sail_dawnstripe_canvas": return Rect.MinMaxRect(77.5f, 48.5f, 434.5f, 463.5f);
                case "sail_storm_canvas": return Rect.MinMaxRect(54.0f, 48.5f, 458.0f, 463.5f);
                case "sail_crosswind_lattice": return Rect.MinMaxRect(91.5f, 49.0f, 420.5f, 463.5f);
                case "sail_rainheron_wing": return Rect.MinMaxRect(125.0f, 49.0f, 387.0f, 463.5f);
                case "sail_saltfox_rig": return Rect.MinMaxRect(91.5f, 49.0f, 420.0f, 463.0f);
                case "sail_bluebell_canvas": return Rect.MinMaxRect(58.5f, 48.5f, 453.5f, 463.0f);
                case "sail_guild_silk": return Rect.MinMaxRect(49.0f, 55.5f, 463.0f, 453.0f);
                case "sail_emerald_trade_wing": return Rect.MinMaxRect(48.0f, 117.0f, 464.0f, 394.5f);
                case "sail_sunthread_spinnaker": return Rect.MinMaxRect(48.5f, 106.0f, 463.5f, 405.0f);
                case "sail_redroute_broadsail": return Rect.MinMaxRect(47.5f, 118.5f, 464.0f, 393.0f);
                case "sail_whisperveil": return Rect.MinMaxRect(48.5f, 94.0f, 463.5f, 418.0f);
                case "sail_moonweave": return Rect.MinMaxRect(49.0f, 49.0f, 463.5f, 463.0f);
                case "sail_eclipse_shroud": return Rect.MinMaxRect(56.0f, 48.0f, 456.0f, 463.5f);
                case "sail_starshear_rig": return Rect.MinMaxRect(70.5f, 48.0f, 441.5f, 463.5f);
                case "sail_ghostcurrent_canvas": return Rect.MinMaxRect(50.5f, 48.5f, 461.0f, 463.0f);
                case "sail_aurora_fan": return Rect.MinMaxRect(68.0f, 48.5f, 443.5f, 463.5f);
                case "sail_tempest_wing": return Rect.MinMaxRect(48.0f, 122.0f, 464.0f, 389.5f);
                case "sail_skybreaker_array": return Rect.MinMaxRect(57.0f, 48.5f, 455.0f, 463.5f);
                case "sail_crown_of_gales": return Rect.MinMaxRect(48.5f, 90.0f, 463.5f, 419.5f);
                case "sail_thunderphoenix": return Rect.MinMaxRect(48.0f, 117.0f, 464.0f, 393.5f);
                case "sail_last_free_wind": return Rect.MinMaxRect(48.0f, 108.0f, 463.5f, 404.0f);
                case "cannon_deck_swivel": return Rect.MinMaxRect(48.5f, 71.5f, 463.5f, 440.5f);
                case "cannon_harbor_popgun": return Rect.MinMaxRect(49.5f, 100.5f, 463.0f, 412.0f);
                case "cannon_grapeshot_cup": return Rect.MinMaxRect(48.5f, 66.5f, 463.0f, 445.5f);
                case "cannon_ropeguard_swivel": return Rect.MinMaxRect(48.5f, 59.5f, 463.5f, 452.5f);
                case "cannon_sparksnout": return Rect.MinMaxRect(49.0f, 120.5f, 463.0f, 390.5f);
                case "cannon_bronze_longgun": return Rect.MinMaxRect(48.5f, 112.5f, 462.5f, 399.5f);
                case "cannon_gullcry_chaser": return Rect.MinMaxRect(49.0f, 108.5f, 463.0f, 403.5f);
                case "cannon_breakwater_spear": return Rect.MinMaxRect(49.0f, 52.0f, 462.5f, 460.0f);
                case "cannon_saltflash_tube": return Rect.MinMaxRect(49.5f, 71.5f, 463.0f, 440.5f);
                case "cannon_twinwatch_gun": return Rect.MinMaxRect(49.5f, 61.0f, 462.5f, 451.0f);
                case "cannon_chain_repeater": return Rect.MinMaxRect(49.0f, 99.0f, 463.0f, 412.0f);
                case "cannon_anchorbreaker": return Rect.MinMaxRect(49.0f, 121.5f, 464.5f, 389.5f);
                case "cannon_redwake_repeater": return Rect.MinMaxRect(49.0f, 97.0f, 463.5f, 414.5f);
                case "cannon_stormnail_array": return Rect.MinMaxRect(48.5f, 139.0f, 463.5f, 373.5f);
                case "cannon_echochamber": return Rect.MinMaxRect(49.5f, 101.0f, 463.0f, 411.5f);
                case "cannon_starfire_battery": return Rect.MinMaxRect(53.0f, 48.5f, 458.5f, 463.5f);
                case "cannon_cometfall_battery": return Rect.MinMaxRect(55.0f, 48.5f, 457.0f, 463.5f);
                case "cannon_blueflare_organ": return Rect.MinMaxRect(48.5f, 116.5f, 463.0f, 395.0f);
                case "cannon_voidbell_mortar": return Rect.MinMaxRect(48.5f, 54.5f, 463.0f, 456.5f);
                case "cannon_sunforge_salvo": return Rect.MinMaxRect(48.5f, 112.0f, 463.5f, 399.0f);
                case "cannon_queens_broadside": return Rect.MinMaxRect(48.5f, 94.5f, 463.5f, 417.5f);
                case "cannon_krakens_thunder": return Rect.MinMaxRect(48.5f, 86.5f, 463.0f, 425.5f);
                case "cannon_worlds_end_volley": return Rect.MinMaxRect(49.0f, 57.0f, 463.0f, 455.5f);
                case "cannon_dragonbreath_battery": return Rect.MinMaxRect(49.5f, 78.5f, 462.5f, 433.5f);
                case "cannon_liberty_salute": return Rect.MinMaxRect(48.5f, 105.5f, 463.5f, 406.5f);
                case "plating_braced_ribs": return Rect.MinMaxRect(49.5f, 61.5f, 463.0f, 449.0f);
                case "plating_ropebound_ribs": return Rect.MinMaxRect(48.5f, 58.5f, 463.0f, 453.0f);
                case "plating_pinepatch_skin": return Rect.MinMaxRect(58.0f, 48.5f, 454.5f, 463.0f);
                case "plating_harboroak_band": return Rect.MinMaxRect(49.0f, 152.0f, 463.0f, 360.0f);
                case "plating_corkwave_liner": return Rect.MinMaxRect(49.0f, 49.0f, 463.0f, 463.0f);
                case "plating_iron_band": return Rect.MinMaxRect(49.0f, 50.0f, 462.5f, 461.5f);
                case "plating_riveted_scale": return Rect.MinMaxRect(49.5f, 55.0f, 462.5f, 457.0f);
                case "plating_wavebreaker_belt": return Rect.MinMaxRect(49.0f, 120.0f, 463.5f, 392.5f);
                case "plating_blacksmith_grid": return Rect.MinMaxRect(48.5f, 59.0f, 463.0f, 453.0f);
                case "plating_saltbronze_shell": return Rect.MinMaxRect(50.0f, 49.5f, 461.5f, 463.0f);
                case "plating_gilded_bulwark": return Rect.MinMaxRect(48.5f, 73.5f, 463.5f, 438.0f);
                case "plating_suncoin_rampart": return Rect.MinMaxRect(49.0f, 70.0f, 463.0f, 441.5f);
                case "plating_coralsteel_hide": return Rect.MinMaxRect(49.5f, 66.0f, 463.5f, 446.0f);
                case "plating_redbanner_wall": return Rect.MinMaxRect(58.0f, 48.5f, 454.0f, 463.5f);
                case "plating_merchants_oath": return Rect.MinMaxRect(48.5f, 93.5f, 463.5f, 418.0f);
                case "plating_moonsilver_skin": return Rect.MinMaxRect(56.0f, 48.5f, 455.0f, 463.5f);
                case "plating_eclipse_mail": return Rect.MinMaxRect(49.5f, 48.5f, 463.5f, 462.5f);
                case "plating_starseal_panels": return Rect.MinMaxRect(49.5f, 55.0f, 463.5f, 457.0f);
                case "plating_ghostreef_carapace": return Rect.MinMaxRect(60.0f, 49.0f, 451.5f, 463.0f);
                case "plating_aurora_laminate": return Rect.MinMaxRect(48.5f, 66.5f, 463.5f, 445.0f);
                case "plating_living_hull": return Rect.MinMaxRect(49.0f, 69.5f, 463.0f, 441.0f);
                case "plating_leviathan_scales": return Rect.MinMaxRect(49.0f, 66.0f, 463.0f, 445.5f);
                case "plating_worldtree_bark": return Rect.MinMaxRect(89.0f, 49.0f, 422.0f, 463.5f);
                case "plating_dragonwake_armor": return Rect.MinMaxRect(49.0f, 79.0f, 463.5f, 433.0f);
                case "plating_freedom_ark": return Rect.MinMaxRect(48.5f, 53.0f, 462.5f, 458.5f);
                case "instrument_brass_compass": return Rect.MinMaxRect(48.5f, 61.0f, 463.5f, 451.0f);
                case "instrument_harbor_dial": return Rect.MinMaxRect(63.0f, 48.5f, 449.0f, 465.5f);
                case "instrument_gullbone_needle": return Rect.MinMaxRect(49.0f, 127.5f, 463.0f, 384.5f);
                case "instrument_ropeknot_log": return Rect.MinMaxRect(49.0f, 72.0f, 463.0f, 439.5f);
                case "instrument_lantern_bearing": return Rect.MinMaxRect(49.0f, 49.0f, 463.0f, 462.5f);
                case "instrument_tideglass": return Rect.MinMaxRect(121.0f, 48.5f, 391.5f, 463.5f);
                case "instrument_rainline_scope": return Rect.MinMaxRect(49.0f, 65.0f, 463.5f, 447.0f);
                case "instrument_current_spoon": return Rect.MinMaxRect(52.0f, 49.0f, 460.0f, 463.5f);
                case "instrument_fogbell_chart": return Rect.MinMaxRect(62.5f, 48.5f, 449.0f, 463.5f);
                case "instrument_sunshadow_ring": return Rect.MinMaxRect(70.0f, 48.5f, 442.5f, 463.5f);
                case "instrument_guild_engine": return Rect.MinMaxRect(48.5f, 105.0f, 463.5f, 406.5f);
                case "instrument_starledger": return Rect.MinMaxRect(49.0f, 115.5f, 463.0f, 396.5f);
                case "instrument_corsair_decoder": return Rect.MinMaxRect(65.5f, 48.5f, 446.5f, 463.0f);
                case "instrument_deepwater_abacus": return Rect.MinMaxRect(98.5f, 49.0f, 412.5f, 463.0f);
                case "instrument_trade_wind_oracle": return Rect.MinMaxRect(49.0f, 55.5f, 463.0f, 456.5f);
                case "instrument_lunar_astrolabe": return Rect.MinMaxRect(84.5f, 49.0f, 428.0f, 463.5f);
                case "instrument_eclipse_orrery": return Rect.MinMaxRect(90.0f, 48.5f, 422.0f, 463.0f);
                case "instrument_whalestar_receiver": return Rect.MinMaxRect(48.5f, 55.0f, 463.0f, 456.5f);
                case "instrument_ghostmap_projector": return Rect.MinMaxRect(70.0f, 49.0f, 442.0f, 463.0f);
                case "instrument_aurora_calculator": return Rect.MinMaxRect(48.5f, 57.0f, 463.0f, 454.5f);
                case "instrument_allseeing_sextant": return Rect.MinMaxRect(62.0f, 49.0f, 449.0f, 463.0f);
                case "instrument_eye_of_depths": return Rect.MinMaxRect(81.5f, 49.0f, 430.0f, 463.5f);
                case "instrument_worldcompass": return Rect.MinMaxRect(48.5f, 95.0f, 463.5f, 416.5f);
                case "instrument_celestial_engine": return Rect.MinMaxRect(100.0f, 48.5f, 411.0f, 463.0f);
                case "instrument_last_horizon_lens": return Rect.MinMaxRect(49.0f, 69.0f, 463.0f, 442.5f);
                case "bow_mistglass_ram": return Rect.MinMaxRect(49.0f, 90.0f, 463.0f, 422.0f);
                case "bow_tidehook_spur": return Rect.MinMaxRect(48.5f, 95.5f, 463.5f, 416.0f);
                case "bow_sunwake_crest": return Rect.MinMaxRect(97.5f, 48.5f, 414.5f, 463.5f);
                case "bow_deepcurrent_fang": return Rect.MinMaxRect(49.0f, 94.0f, 463.0f, 418.0f);
                case "bow_freewake_lance": return Rect.MinMaxRect(48.5f, 90.0f, 463.5f, 421.5f);
                case "sail_dawnkite": return Rect.MinMaxRect(65.0f, 49.0f, 447.0f, 463.5f);
                case "sail_strait_lugger": return Rect.MinMaxRect(60.5f, 48.5f, 450.5f, 463.0f);
                case "sail_evening_silk": return Rect.MinMaxRect(48.5f, 104.0f, 463.5f, 408.0f);
                case "sail_fogshroud_wing": return Rect.MinMaxRect(48.5f, 48.5f, 463.5f, 463.0f);
                case "sail_thunderreef_wing": return Rect.MinMaxRect(129.0f, 49.0f, 383.0f, 463.5f);
                case "cannon_fogbell_saker": return Rect.MinMaxRect(49.0f, 78.0f, 463.0f, 433.5f);
                case "cannon_crosswind_pivot": return Rect.MinMaxRect(48.5f, 104.0f, 463.5f, 407.5f);
                case "cannon_tradeguard_battery": return Rect.MinMaxRect(48.5f, 98.5f, 463.5f, 413.5f);
                case "cannon_deepstar_lance": return Rect.MinMaxRect(48.5f, 123.0f, 463.5f, 389.5f);
                case "cannon_stormcrown_broadsides": return Rect.MinMaxRect(49.0f, 70.5f, 463.5f, 441.0f);
                case "plating_saltfoam_skin": return Rect.MinMaxRect(49.5f, 98.5f, 462.5f, 413.0f);
                case "plating_strait_laminate": return Rect.MinMaxRect(48.5f, 51.5f, 463.5f, 460.5f);
                case "plating_trade_oathwall": return Rect.MinMaxRect(90.0f, 48.5f, 421.0f, 463.5f);
                case "plating_fogiron_carapace": return Rect.MinMaxRect(48.5f, 95.5f, 463.0f, 416.0f);
                case "plating_stormark_bulwark": return Rect.MinMaxRect(49.5f, 48.5f, 462.5f, 463.5f);
                case "instrument_dawnseeker": return Rect.MinMaxRect(66.5f, 49.0f, 445.0f, 462.5f);
                case "instrument_strait_echo": return Rect.MinMaxRect(53.5f, 48.5f, 459.0f, 463.5f);
                case "instrument_tradeledger_oracle": return Rect.MinMaxRect(49.0f, 124.5f, 463.0f, 387.0f);
                case "instrument_reefstar_astrolabe": return Rect.MinMaxRect(84.5f, 49.0f, 427.5f, 463.0f);
                case "instrument_stormoracle": return Rect.MinMaxRect(128.5f, 48.5f, 383.0f, 463.0f);
                default:
                    throw new ArgumentException("Missing measured component art: " + id, nameof(id));
            }
        }
    }
}
