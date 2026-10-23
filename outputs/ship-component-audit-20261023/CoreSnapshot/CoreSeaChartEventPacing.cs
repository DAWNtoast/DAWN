using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredSail.Core.Gameplay
{
    [Serializable] public sealed class CoreSeaChartRandomHistory
    {
        public int routeIndex;
        public string[] recentEvents = Array.Empty<string>();
    }

    public sealed partial class CoreVoyageSession
    {
        private const int SeaChartRandomHistoryLimit = 16;
        private readonly Dictionary<int, List<string>> routeRandomHistory = new Dictionary<int, List<string>>();

        private static string[] SeaChartRandomPool(int routeIndex, int version = 2) =>
            (version == 1 ? CoreVoyageEventCatalog.LegacyRandomEventIds(Routes[routeIndex].Id) :
                CoreVoyageEventCatalog.RandomEventIds(Routes[routeIndex].Id))
                .Concat(new[] { "random_tailwind", "random_floating_supplies" }).ToArray();

        private void RefillSeaChartRandomBag(string previousFirst)
        {
            string[] pool = SeaChartRandomPool(activeSeaChart.RouteIndex, chartRandomVersion);
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int j = (int)(encounterRandom.NextDouble() * (i + 1));
                string item = pool[i]; pool[i] = pool[j]; pool[j] = item;
            }
            routeRandomHistory.TryGetValue(activeSeaChart.RouteIndex, out List<string> recent);
            if (chartRandomVersion == 1 && recent != null)
                recent = recent.Skip(Math.Max(0, recent.Count - 8)).ToList();
            // Stable ordering keeps random ties but prioritizes unseen/least-recent
            // content. History follows the sea, including when a new chart is drawn.
            chartRandomOrder = pool.OrderBy(id => recent == null ? -1 : recent.IndexOf(id)).ToArray();
            if (chartRandomOrder[0] == previousFirst)
            {
                string held = chartRandomOrder[0]; chartRandomOrder[0] = chartRandomOrder[1]; chartRandomOrder[1] = held;
            }
            // Vary the kind of decision as well as the event ID. Reorder only
            // equally recent candidates, so the sixteen-event exclusion survives
            // new charts, bag refills and checkpoint restoration.
            var remaining = new List<string>(chartRandomOrder);
            string previousKind = CoreVoyageEventCatalog.RandomEventCategory(recent?.LastOrDefault() ?? previousFirst);
            for (int i = 0; i < chartRandomOrder.Length; i++)
            {
                int age = recent == null ? -1 : recent.IndexOf(remaining[0]);
                var peers = remaining.Where(id => (recent == null ? -1 : recent.IndexOf(id)) == age).ToArray();
                string chosen = peers.Where(id => CoreVoyageEventCatalog.RandomEventCategory(id) != previousKind &&
                        (i != 0 || id != previousFirst))
                    .OrderByDescending(id => peers.Count(peer => CoreVoyageEventCatalog.RandomEventCategory(peer) ==
                        CoreVoyageEventCatalog.RandomEventCategory(id))).FirstOrDefault() ?? remaining[0];
                chartRandomOrder[i] = chosen;
                remaining.Remove(chosen);
                previousKind = CoreVoyageEventCatalog.RandomEventCategory(chosen);
            }
            chartRandomEncounters = chartRandomOrder.Select(CreateSeaChartRandomEncounter).ToArray();
            chartRandomIndex = 0;
        }

        private void RememberSeaChartRandomEvent(string id)
        {
            int route = activeSeaChart.RouteIndex;
            if (!routeRandomHistory.TryGetValue(route, out List<string> recent))
            { recent = new List<string>(); routeRandomHistory.Add(route, recent); }
            recent.Remove(id); recent.Add(id);
            int limit = chartRandomVersion == 1 ? 8 : SeaChartRandomHistoryLimit;
            if (recent.Count > limit) recent.RemoveRange(0, recent.Count - limit);
            TouchSeaChartProgress();
        }

        private float NextSeaChartRandomGap() => activeSeaChart.RouteIndex == 1
            ? 180f + (float)encounterRandom.NextDouble() * 100f
            : 220f + (float)encounterRandom.NextDouble() * 120f;

        private void SpaceRandomEventAfterFixedEncounter()
        {
            // A fixed encounter counts as activity; avoid an immediate second popup.
            if (activeSeaChart != null && chartRandomVersion > 0 && GuidedTutorialLeg == 0)
                nextChartRandomDistance = Math.Max(nextChartRandomDistance, TotalDistanceNm + 100f);
        }

        private static bool IsRouteRandomEvent(int route, string id) =>
            route >= 0 && route < Routes.Count && !string.IsNullOrEmpty(id) &&
            (Array.IndexOf(SeaChartRandomTemplates, id) >= 0 ||
                CoreVoyageEventCatalog.RandomEventIds(Routes[route].Id).Contains(id));
    }
}
