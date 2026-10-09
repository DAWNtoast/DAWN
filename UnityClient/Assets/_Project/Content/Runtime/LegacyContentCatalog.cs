using System;
using System.Collections.Generic;

namespace ShatteredSail.Content
{
    /// <summary>
    /// Files that must remain available while the Unity client consumes the
    /// existing CSV content pipeline.
    /// </summary>
    public static class LegacyContentCatalog
    {
        private static readonly IReadOnlyList<string> requiredRelativePaths = Array.AsReadOnly(new[]
        {
            "outputs/routes_v2/route_config.csv",
            "outputs/routes_v2/event_point_config.csv",
            "outputs/routes_v2/event_chain_config.csv",
            "outputs/routes_v2/art_manifest.csv",
            "outputs/balance_configs/ship_config.csv",
            "outputs/balance_configs/crew_config.csv",
            "outputs/balance_configs/card_config.csv",
            "outputs/balance_configs/port_config.csv",
            "outputs/balance_configs/dispatch_config.csv",
            "outputs/balance_configs/component_config.csv",
        });

        public static IReadOnlyList<string> RequiredRelativePaths => requiredRelativePaths;
    }
}

