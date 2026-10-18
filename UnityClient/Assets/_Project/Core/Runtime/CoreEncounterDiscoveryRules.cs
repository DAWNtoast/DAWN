namespace ShatteredSail.Core.Gameplay
{
    public enum EncounterDiscoveryKind
    {
        SightedLocation,
        HiddenLocation,
        OnboardIncident,
        TransientEncounter,
    }

    /// <summary>
    /// A hidden place can be charted from intelligence; a shipboard problem cannot.
    /// These classifications change disclosure only, never event placement or RNG.
    /// </summary>
    public static class CoreEncounterDiscoveryRules
    {
        public static EncounterDiscoveryKind Classify(string encounterId, bool hidden)
        {
            if (TryClassify(encounterId, out EncounterDiscoveryKind kind)) return kind;
            // Preserve the constructor contract for custom encounters. The authored
            // encounter audit requires every shipped event to have an explicit rule.
            return hidden ? EncounterDiscoveryKind.HiddenLocation : EncounterDiscoveryKind.SightedLocation;
        }

        public static bool TryClassify(string encounterId, out EncounterDiscoveryKind kind)
        {
            if (CoreVoyageEventCatalog.TryClassifyDiscovery(encounterId, out kind)) return true;
            if (CoreTreasureExpeditionCatalog.TryClassifyDiscovery(encounterId, out kind)) return true;
            switch (encounterId)
            {
                // No external location exists for a hull failure or interpreting
                // a document aboard ship, even if its scripted trigger has a position.
                case "event_reef_saltwater_leak":
                case "tutorial_crew_requirement_event":
                    kind = EncounterDiscoveryKind.OnboardIncident;
                    return true;

                // These are generated or announced at encounter time, not fixed
                // chart destinations. The navigator's ambush warning still triggers.
                case "random_ambush":
                case "random_ambush_warning":
                case "random_tailwind":
                case "random_floating_supplies":
                case "random_food_spoilage":
                case "tutorial_ambush_event":
                case "guided_tutorial_random_event":
                    kind = EncounterDiscoveryKind.TransientEncounter;
                    return true;

                // Concealed external places or phenomena can be located by clues.
                // Their names cannot be inferred from a distant visual silhouette.
                case "event_reef_shortcut":
                case "gold_wreck":
                case "moon_whisper":
                case "common_whale_song":
                case "tutorial_hidden_event":
                case "event_reef_hidden_cache":
                case "treasure_false_cache":
                case "treasure_reef_snare":
                case "treasure_drowned_shrine":
                case "treasure_siren_conch":
                case "treasure_smuggler_jetty":
                case "treasure_brine_geyser":
                case "treasure_coral_maze":
                case "treasure_pearl_crab":
                case "treasure_core":
                    kind = EncounterDiscoveryKind.HiddenLocation;
                    return true;

                // Physical ships, shore objects and weather have visible evidence.
                case "event_reef_broken_mast":
                case "event_reef_sharp_current":
                case "treasure_clue_bottle":
                case "treasure_clue_whalebone":
                case "treasure_clue_star":
                case "treasure_clue_fogbell":
                case "treasure_clue_twinmoon":
                case "treasure_rival_sloop":
                case "treasure_salt_spring":
                case "treasure_ghost_fleet":
                case "treasure_castaway_cartographer":
                case "event_reef_sunken_boat":
                case "event_reef_fisher_cache":
                case "event_reef_coral_ambush":
                case "gold_convoy":
                case "gold_customs":
                case "moon_obelisk":
                case "moon_ghost":
                case "common_beach_castaway":
                case "common_strait_beacon":
                case "common_floating_market":
                case "common_storm_debris":
                case "common_reef_divers":
                case "boss_route_shattered_sail_reef":
                case "boss_route_golden_tide_trade":
                case "boss_route_moon_tide_ruins":
                case "boss_route_shattered_sail_reef_tide":
                case "boss_route_golden_tide_trade_thunder":
                case "boss_route_moon_tide_ruins_arcane":
                case "treasure_boss_moon_vault":
                case "legendary_pirate_iron_oath":
                case "legendary_pirate_thunder_crown":
                case "legendary_pirate_red_widow":
                case "legendary_pirate_ember_ark":
                case "legendary_pirate_tide_requiem":
                case "legendary_pirate_last_horizon":
                case "tutorial_first_event":
                case "tutorial_optional_event":
                case "tutorial_navigator_event":
                case "tutorial_boss_event":
                    kind = EncounterDiscoveryKind.SightedLocation;
                    return true;
                default:
                    kind = default(EncounterDiscoveryKind);
                    return false;
            }
        }
    }
}
