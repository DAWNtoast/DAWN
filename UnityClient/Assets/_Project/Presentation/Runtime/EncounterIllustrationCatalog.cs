using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Authored encounter-to-scene bindings for the refreshed encounter paintings.</summary>
    public sealed class EncounterIllustrationCatalog
    {
        public const string TreasureResourcePath = "EncounterIllustrations/legacy_treasure_v3";
        public const string BossSceneResourceRoot = "EncounterIllustrations/BossScenes/scene_";
        public const string ReviewedResourceRoot = "EncounterIllustrations/Reviewed20261023/";
        public readonly struct Location
        {
            public readonly string ResourcePath;
            public readonly Rect Uv;
            public Location(string resourcePath, Rect uv) { ResourcePath = resourcePath; Uv = uv; }
        }

        private static readonly Dictionary<string, Location> Locations = CreateLocations();
        private readonly Func<string, Texture2D> resourceLoader;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public EncounterIllustrationCatalog() : this(Resources.Load<Texture2D>) { }

        internal EncounterIllustrationCatalog(Func<string, Texture2D> resourceLoader)
        {
            this.resourceLoader = resourceLoader ?? throw new ArgumentNullException(nameof(resourceLoader));
        }

        public static bool TryGetLocation(string encounterId, out Location location, bool legacyTutorial = false)
        {
            // The retired standalone tutorial and the guided voyage share IDs but have different stories.
            if (legacyTutorial)
            {
                switch (encounterId)
                {
                    case "tutorial_first_event": return Locations.TryGetValue("event_reef_drifting_crate", out location);
                    case "tutorial_optional_event": location = Sheet(0, 2, 1); return true;
                    case "tutorial_crew_requirement_event": return Locations.TryGetValue("event_trade_v2_ledger_forgery", out location);
                    case "tutorial_navigator_event": return Locations.TryGetValue("event_moon_lunar_observatory", out location);
                }
            }
            if (!string.IsNullOrEmpty(encounterId)) return Locations.TryGetValue(encounterId, out location);
            location = default;
            return false;
        }

        public static bool TryGetLocation(CoreEncounter encounter, out Location location, bool legacyTutorial = false)
        {
            // These IDs survive checkpoints across different authored stories. Resolve from the
            // actual encounter, not the currently selected route or treasure map rank.
            if (!legacyTutorial && encounter != null)
            {
                string key = null;
                if (encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId)
                {
                    switch (encounter.Name)
                    {
                        case "沉船工坊主机舱": key = "core_weathered"; break;
                        case "商盟封存总仓": key = "core_gilded"; break;
                        case "三相月蚀圣所": key = "core_moonlit"; break;
                    }
                }
                else if (encounter.Id == CoreGuidedTutorialContent.RandomEventId)
                {
                    switch (encounter.Name)
                    {
                        case "顺风捎来的讯息": key = "tutorial_message_ship"; break;
                        case "水手发现漂流信筒": key = "tutorial_message_tube"; break;
                    }
                }
                if (key != null) { location = Reviewed(key); return true; }
            }
            return TryGetLocation(encounter?.Id, out location, legacyTutorial);
        }

        public bool TryLoad(string encounterId, out Texture2D texture, out Rect uv, bool legacyTutorial = false)
        {
            texture = null;
            uv = new Rect(0, 0, 1, 1);
            if (!TryGetLocation(encounterId, out Location location, legacyTutorial)) return false;
            return TryLoadLocation(location, out texture, out uv);
        }

        public bool TryLoad(CoreEncounter encounter, out Texture2D texture, out Rect uv, bool legacyTutorial = false)
        {
            texture = null;
            uv = new Rect(0, 0, 1, 1);
            if (!TryGetLocation(encounter, out Location location, legacyTutorial)) return false;
            return TryLoadLocation(location, out texture, out uv);
        }

        private bool TryLoadLocation(Location location, out Texture2D texture, out Rect uv)
        {
            uv = location.Uv;
            texture = Load(location.ResourcePath);
            // A recognized event remains recognized even if an installation is missing its asset.
            // Never replace its meaning with an unrelated hash-selected scene.
            return true;
        }

        public Texture2D RouteSheet(int routeIndex) => Load(RouteResourcePath(Mathf.Clamp(routeIndex, 0, 2)));

        public static string RouteResourcePath(int routeIndex) => "EncounterIllustrations/legacy_" +
            (routeIndex == 1 ? "trade" : routeIndex == 2 ? "moon" : "reef") + "_v3";

        private Texture2D Load(string resourcePath)
        {
            if (!textures.TryGetValue(resourcePath, out Texture2D texture))
            {
                texture = resourceLoader(resourcePath);
                if (texture != null)
                {
                    texture.filterMode = FilterMode.Trilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                }
                textures.Add(resourcePath, texture);
            }
            return texture;
        }

        public void ReleaseLoadedTextures() => textures.Clear();

        private static Location Reviewed(string key) => new Location(ReviewedResourceRoot + key, new Rect(0, 0, 1, 1));

        private static Location Sheet(int route, int column, int row)
        {
            return new Location(RouteResourcePath(route), AtlasCellUv(column, row));
        }

        public static Rect AtlasCellUv(int column, int rowFromTop)
        {
            // Painted panel edges vary slightly. Keep sampling inside the scene so a neighboring
            // illustration never appears along its edge when the atlas is filtered or fitted.
            Rect cell = LegacyDemoArtCatalog.CellUv(column, rowFromTop, 5, 4);
            float insetX = cell.width * 0.055f;
            float insetY = cell.height * 0.055f;
            return new Rect(cell.x + insetX, cell.y + insetY, cell.width - insetX * 2f, cell.height - insetY * 2f);
        }

        private static Dictionary<string, Location> CreateLocations()
        {
            var result = new Dictionary<string, Location>(StringComparer.Ordinal);
            void Standalone(params string[] ids)
            {
                foreach (string id in ids) result.Add(id, new Location("EncounterIllustrations/" + id + "_v1", new Rect(0, 0, 1, 1)));
            }
            void Retained(int route, int column, int row, params string[] ids)
            {
                foreach (string id in ids) result.Add(id, Sheet(route, column, row));
            }
            void Alias(string source, params string[] ids)
            {
                foreach (string id in ids) result.Add(id, result[source]);
            }
            void BossScene(string key, string id)
            {
                result.Add(id, new Location(BossSceneResourceRoot + key, new Rect(0, 0, 1, 1)));
            }

            // Event illustrations are complete sea scenes. The separately named transparent
            // ShipArtV2/Special paintings remain dedicated to sailing and battle ship bodies.
            BossScene("boss_fire", "boss_route_shattered_sail_reef");
            BossScene("boss_tide", "boss_route_shattered_sail_reef_tide");
            BossScene("boss_blood", "boss_route_golden_tide_trade");
            BossScene("boss_thunder", "boss_route_golden_tide_trade_thunder");
            BossScene("boss_curse", "boss_route_moon_tide_ruins");
            BossScene("boss_arcane", "boss_route_moon_tide_ruins_arcane");
            BossScene("legendary_fire", "legendary_pirate_ember_ark");
            BossScene("legendary_tide", "legendary_pirate_tide_requiem");
            BossScene("legendary_blood", "legendary_pirate_red_widow");
            BossScene("legendary_thunder", "legendary_pirate_thunder_crown");
            BossScene("legendary_curse", "legendary_pirate_iron_oath");
            BossScene("legendary_arcane", "legendary_pirate_last_horizon");

            Standalone(
                "event_reef_v2_message_gull", "event_reef_v2_entangled_calf", "event_reef_v2_backspin_winch",
                "event_reef_v2_luminous_wake", "event_reef_v2_work_song", "event_reef_v2_barnacle_ridge",
                "event_reef_v2_living_net", "event_reef_v2_salvage_argument", "event_reef_v2_sail_challenge",
                "event_reef_v2_compass_pulse", "event_reef_v2_empty_watch", "event_reef_v2_whaler_signal",
                "event_reef_v2_tidewright_register", "event_reef_v2_three_bell_array", "event_reef_v2_shell_lime_barge",
                "event_reef_v2_kelp_loom", "event_reef_v2_anchor_rubbing", "event_reef_v2_coral_resonator",
                "event_reef_v2_tidal_hoist", "event_reef_v2_diving_bell_station",
                "event_trade_v2_courier_pigeon", "event_trade_v2_right_of_way", "event_trade_v2_sample_sails",
                "event_trade_v2_transfer_queue", "event_trade_v2_diving_bell", "event_trade_v2_ledger_forgery",
                "event_trade_v2_charter_crowding", "event_trade_v2_guild_manifest", "event_trade_v2_channel_moorings",
                "event_trade_v2_rain_collectors", "event_trade_v2_provision_survey", "event_trade_v2_powder_barge",
                "event_trade_v2_notary_barge", "event_trade_v2_abandoned_countinghouse", "event_trade_v2_sail_pattern_loft",
                "event_trade_v2_tide_ticket_booth", "event_trade_v2_retired_pilot_station", "event_trade_v2_weighing_raft",
                "event_trade_v2_dyers_anchorage", "event_trade_v2_floating_school",
                "event_moon_v2_reversed_rubbing", "event_moon_v2_hollow_bell", "event_moon_v2_borrowed_shadow",
                "event_moon_v2_star_in_jar", "event_moon_v2_living_figurehead", "event_moon_v2_funerary_procession",
                "event_moon_v2_misplaced_echo", "event_moon_v2_false_ancestor", "event_moon_v2_tide_abacus",
                "event_moon_v2_moon_taxcollector", "event_moon_v2_astronomer_rescue", "event_moon_v2_silver_needle",
                "event_moon_v2_calendar_mosaic", "event_moon_v2_ossuary_buoy", "event_moon_v2_submerged_scriptorium",
                "event_moon_v2_pendulum_archive", "event_moon_v2_exorcist_kiln", "event_moon_v2_survey_barge",
                "event_moon_v2_votive_loom", "event_moon_v2_unlit_court",
                "event_reef_sudden_squall", "event_reef_rigging_snap", "event_reef_freshwater_slick",
                "event_reef_shoal_fish_run", "event_reef_crew_fever", "event_reef_drifting_crate",
                "event_reef_night_fog", "event_reef_open_water_ambush", "common_whale_song",
                "event_trade_quarantine_flag", "event_trade_wedding_fleet", "event_trade_medicine_convoy",
                "event_trade_insurance_wreck", "event_trade_cargo_shift", "event_trade_deck_fire", "event_trade_stowaway",
                "event_moon_moon_sleepwalker", "event_moon_ghost_current", "event_moon_falling_starlight",
                "event_moon_relic_whisper", "event_moon_silver_fog", "event_moon_dead_calm", "event_moon_curse_surge",
                "event_moon_relic_diver", "provisions_spoilage", "event_reef_drifting_sail",
                "weathered_tide_turbine", "weathered_cartographer_hulk", "moonlit_shadow_dial");

            Alias("provisions_spoilage", "event_reef_rotten_rations", "event_trade_food_spoilage", "event_moon_food_spoilage", "random_food_spoilage");
            Alias("event_reef_drifting_crate", "tutorial_optional_event");
            Alias("event_reef_freshwater_slick", "random_floating_supplies");
            Alias("event_reef_open_water_ambush", "event_reef_coral_ambush", "random_ambush", "random_ambush_warning", "tutorial_ambush_event");
            Alias("event_reef_v2_work_song", "random_tailwind");
            Alias("event_trade_v2_ledger_forgery", "tutorial_crew_requirement_event");
            Alias("event_trade_v2_powder_barge", "event_trade_merchant_distress");
            Retained(1, 1, 0, "gold_customs", "event_trade_customs_patrol");
            Retained(1, 3, 1, "event_trade_market_rumor");
            Alias("event_trade_v2_guild_manifest", "guided_tutorial_random_event");
            Alias("event_reef_v2_three_bell_array", "tutorial_navigator_event");
            Alias("event_reef_v2_shell_lime_barge", "event_reef_tidewright_platform");
            Alias("event_moon_silver_fog", "moon_whisper");
            Alias("event_reef_sudden_squall", "common_storm_debris");
            Alias("event_moon_relic_diver", "common_reef_divers");

            Retained(0, 0, 0, "event_reef_broken_mast");
            Retained(0, 1, 0, "event_reef_shortcut", "event_reef_favorable_current");
            Retained(0, 2, 0, "event_reef_sunken_boat", "event_reef_powder_wreck", "gold_wreck");
            Retained(0, 3, 0, "event_reef_saltwater_leak");
            Retained(0, 4, 0, "event_reef_fisher_cache");
            Retained(0, 0, 1, "event_reef_sharp_current");
            Retained(0, 1, 1, "event_reef_old_cannon");
            Retained(0, 4, 1, "tutorial_first_event");
            Retained(0, 1, 2, "event_reef_tidepool_pearls");
            Retained(0, 2, 2, "common_beach_castaway");
            Retained(0, 4, 2, "event_reef_lantern", "common_strait_beacon", "tutorial_hidden_event");
            Retained(0, 1, 3, "common_floating_market", "gold_convoy");
            Retained(0, 2, 3, "event_reef_shallow_whirlpool");
            Retained(0, 3, 3, "event_reef_hidden_repair_dock");
            Retained(0, 4, 3, "tutorial_boss_event");

            Retained(1, 1, 1, "event_trade_spice_auction", "event_trade_floating_auction");
            Retained(1, 2, 1, "event_trade_seasonal_wind");
            Retained(1, 0, 2, "event_trade_open_water_ambush");
            Retained(1, 1, 2, "event_trade_gilded_lighthouse");
            Retained(1, 4, 0, "event_trade_debt_collector");
            Retained(1, 0, 3, "event_trade_sea_theatre");
            Retained(1, 1, 3, "event_reef_hidden_cache");

            Retained(2, 0, 0, "event_moon_gate");
            Retained(2, 2, 0, "event_moon_star_compass");
            Retained(2, 0, 1, "moon_obelisk");
            Retained(2, 1, 1, "event_moon_spectral_debris");
            Retained(2, 3, 1, "event_moon_lunar_observatory");
            Retained(2, 0, 2, "event_moon_mirror_pool");
            Retained(2, 1, 2, "event_moon_drowned_library");
            Retained(2, 0, 3, "event_moon_chain_bridge");
            Retained(2, 1, 3, "event_moon_healing_spring");
            Retained(2, 4, 3, "event_moon_open_water_ambush", "moon_ghost", "treasure_boss_moon_vault");

            // The 20-panel treasure sheet retains its established cell order after repainting.
            string[] treasureCells = {
                "treasure_clue_bottle", "treasure_clue_whalebone", "treasure_clue_star", "treasure_clue_fogbell", "treasure_clue_twinmoon",
                "treasure_false_cache", "treasure_reef_snare", "treasure_rival_sloop", "treasure_drowned_shrine", "treasure_salt_spring",
                "treasure_siren_conch", "treasure_ghost_fleet", "treasure_smuggler_jetty", "treasure_brine_geyser", "treasure_coral_maze",
                "treasure_castaway_cartographer", "treasure_pearl_crab", "treasure_core"
            };
            for (int index = 0; index < treasureCells.Length; index++)
                result.Add(treasureCells[index], new Location(TreasureResourcePath, AtlasCellUv(index % 5, index / 5)));
            Alias("provisions_spoilage", "random_food_spoil");

            // Reuse the depicted object or activity, rather than the former unrelated treasure-cell aliases.
            Alias("event_reef_v2_backspin_winch", "weathered_winch");
            Alias("event_reef_v2_diving_bell_station", "weathered_bell");
            Alias("event_reef_hidden_repair_dock", "weathered_camp", "weathered_rustyard");
            Alias("event_moon_chain_bridge", "weathered_chain_gate");
            Alias("event_reef_v2_tidal_hoist", "weathered_keel_vault");
            Retained(2, 3, 3, "weathered_glass_dome");
            Alias("event_trade_insurance_wreck", "weathered_supply_barge");
            Alias("event_reef_v2_three_bell_array", "weathered_signal_buoy");
            Alias("event_reef_saltwater_leak", "weathered_broken_caisson");
            Alias("event_trade_v2_notary_barge", "gilded_manifest", "gilded_customs");
            Retained(1, 4, 2, "gilded_witness");
            Alias("event_trade_medicine_convoy", "gilded_relief");
            Retained(1, 2, 0, "gilded_bonded_vault");
            Alias("treasure_smuggler_jetty", "gilded_black_market");
            Alias("event_trade_v2_ledger_forgery", "gilded_counterfeit", "gilded_ledger_buoy");
            Alias("event_trade_floating_auction", "gilded_auction");
            Alias("event_trade_seasonal_wind", "gilded_convoy");
            Alias("event_reef_v2_shell_lime_barge", "gilded_insurance");
            Alias("event_trade_quarantine_flag", "gilded_quarantine");
            Retained(2, 4, 2, "moonlit_dawn", "moonlit_dusk", "moonlit_midnight");
            Alias("event_moon_healing_spring", "moonlit_cleanse");
            Alias("event_moon_mirror_pool", "moonlit_mirror_pool", "moonlit_moon_well");
            Alias("event_reef_v2_coral_resonator", "moonlit_choir");
            Retained(2, 4, 0, "moonlit_pearl_moon");
            Alias("event_moon_ghost_current", "moonlit_star_current");
            Alias("event_moon_drowned_library", "moonlit_echo_library");
            Retained(2, 2, 2, "moonlit_sleeping_guardian");

            // Scene-specific paintings replace approximate shared scenes after the text/art audit.
            result["tutorial_crew_requirement_event"] = Reviewed("tutorial_note");
            result["tutorial_navigator_event"] = Reviewed("tutorial_tide_buoy");
            result["event_reef_hidden_cache"] = Reviewed("reef_tide_cache");
            result["event_reef_broken_mast"] = Reviewed("reef_broken_mast");
            result["event_reef_sharp_current"] = Reviewed("reef_chart_current");
            result["common_storm_debris"] = Reviewed("storm_debris");
            result["common_reef_divers"] = Reviewed("pearl_divers");
            result["weathered_chain_gate"] = Reviewed("weathered_chain");
            result["weathered_keel_vault"] = Reviewed("weathered_keel");
            result["weathered_rustyard"] = Reviewed("weathered_rustyard");
            result["weathered_signal_buoy"] = Reviewed("weathered_buoy");
            result["weathered_broken_caisson"] = Reviewed("weathered_caisson");
            result["gilded_witness"] = Reviewed("gilded_witness");
            result["gilded_relief"] = Reviewed("gilded_relief");
            result["gilded_auction"] = Reviewed("gilded_auction");
            result["gilded_convoy"] = Reviewed("gilded_convoy");
            result["gilded_ledger_buoy"] = Reviewed("gilded_ledger_buoy");
            result["moonlit_dawn"] = Reviewed("moonlit_dawn");
            result["moonlit_dusk"] = Reviewed("moonlit_dusk");
            result["moonlit_midnight"] = Reviewed("moonlit_midnight");
            result["moonlit_choir"] = Reviewed("moonlit_choir");
            result["moonlit_sleeping_guardian"] = Reviewed("moonlit_guardian");
            result["moonlit_moon_well"] = Reviewed("moonlit_well");
            result[CoreBattleBalance.TreasureBossId] = Reviewed("treasure_guard_ship");
            // Keep copied aliases such as moonlit_mirror_pool on their own moon-reflection scene.
            // The voyage mirror event instead depicts the described mirror-black patch of sea.
            foreach (string id in new[] {
                "event_trade_debt_collector", "event_trade_merchant_distress", "event_moon_mirror_pool",
                "event_moon_spectral_debris", "event_moon_lunar_observatory", "moon_ghost", "event_moon_v2_borrowed_shadow",
                "event_moon_drowned_library", "event_moon_healing_spring"
            }) result[id] = Reviewed(id);
            result["moonlit_cleanse"] = result["event_moon_healing_spring"];
            return result;
        }
    }
}
