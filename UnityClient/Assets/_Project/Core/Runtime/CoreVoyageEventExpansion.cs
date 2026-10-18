using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    public static partial class CoreVoyageEventCatalog
    {
        // SS4's eight-entry catalog remains frozen for saved charts and checkpoints.
        public static IReadOnlyList<CoreEncounter> CreateExpandedFixedEncounters(string routeId)
        {
            IReadOnlyList<CoreEncounter> additions = routeId == Reef ? CreateReefExpandedFixed() :
                routeId == Trade ? CreateTradeExpandedFixed() : routeId == Moon ? CreateMoonExpandedFixed() :
                Array.Empty<CoreEncounter>();
            return Array.AsReadOnly(CreateFixedEncounters(routeId).Concat(additions).ToArray());
        }

        private static bool TryExpandedDiscovery(string id, out EncounterDiscoveryKind kind)
        {
            kind = EncounterDiscoveryKind.SightedLocation;
            if (!TryReefExpandedTerrain(id, out _) && !TryTradeExpandedTerrain(id, out _) &&
                !TryMoonExpandedTerrain(id, out _)) return false;
            switch (id)
            {
                case "event_reef_v2_coral_resonator":
                case "event_trade_v2_abandoned_countinghouse":
                case "event_moon_v2_ossuary_buoy":
                case "event_moon_v2_submerged_scriptorium":
                case "event_moon_v2_votive_loom":
                case "event_moon_v2_unlit_court":
                    kind = EncounterDiscoveryKind.HiddenLocation;
                    break;
            }
            return true;
        }
    }

    public sealed partial class CoreVoyageSession
    {
        private static List<CoreEncounter> SelectVariedChartEncounters(CoreRouteDefinition route, uint hash,
            IReadOnlyList<CoreEncounter> original)
        {
            // Keep a source and destination for every exploration chain. All other
            // ordinary locations vary with the chart, without increasing popup density.
            string[] anchors = route.Id == Routes[0].Id ? new[] { "event_reef_broken_mast", "event_reef_hidden_cache" } :
                route.Id == Routes[1].Id ? new[] { "common_beach_castaway", "gold_wreck" } :
                new[] { "common_strait_beacon", "moon_obelisk" };
            var selected = original.Where(e => anchors.Contains(e.Id)).ToList();
            var remaining = original.Where(e => !anchors.Contains(e.Id)).ToList();
            var expansion = CoreVoyageEventCatalog.CreateExpandedFixedEncounters(route.Id).ToList();
            var selection = new Mulberry32Random(hash ^ 0x547EC9B1u);
            ShuffleChartChoices(remaining, selection);
            ShuffleChartChoices(expansion, selection);
            selected.AddRange(remaining.Take(3));
            // At least three visible opportunities are needed for the paced route.
            // Order visible entries first only until this minimum is secured.
            int visibleNeeded = Math.Max(0, 3 - selected.Count(e => e.CanDiscoverBySight));
            var visible = expansion.Where(e => e.CanDiscoverBySight).Take(visibleNeeded).ToArray();
            selected.AddRange(visible);
            selected.AddRange(expansion.Where(e => !visible.Contains(e)).Take(8 - visible.Length));
            return selected;
        }

        private static void ShuffleChartChoices(List<CoreEncounter> choices, Mulberry32Random random)
        {
            for (int index = choices.Count - 1; index > 0; index--)
            {
                int swap = (int)(random.NextDouble() * (index + 1));
                CoreEncounter held = choices[index]; choices[index] = choices[swap]; choices[swap] = held;
            }
        }
    }
}
