using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private readonly HashSet<string> collectionBosses = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> collectionCharts = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> collectionRoutes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> collectionPorts = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> collectionTreasureSeeds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> collectionTreasureRanks = new HashSet<string>(StringComparer.Ordinal);
        public int CollectionProgressRevision { get; private set; }

        public CoreCollectionProgress CaptureCollectionProgress() => new CoreCollectionProgress
        {
            defeatedBossIds = CollectionSnapshot(collectionBosses),
            completedChartSeeds = CollectionSnapshot(collectionCharts),
            completedRouteIds = CollectionSnapshot(collectionRoutes),
            visitedPorts = CollectionSnapshot(collectionPorts),
            completedTreasureSeeds = CollectionSnapshot(collectionTreasureSeeds),
            completedTreasureRanks = CollectionSnapshot(collectionTreasureRanks),
        };

        /// <summary>Validates the entire record before replacing any permanent facts.</summary>
        public bool RestoreCollectionProgress(CoreCollectionProgress progress)
        {
            if (Phase != CoreDemoPhase.Port || progress == null || progress.version != 1) return false;
            if (!TryCollectionSet(progress.defeatedBossIds, CoreCollectionRewardCatalog.IsKnownBoss, out var bosses) ||
                !TryCollectionSet(progress.completedChartSeeds, IsCollectionChartSeed, out var charts) ||
                !TryCollectionSet(progress.completedRouteIds, id => Routes.Any(route => route.Id == id), out var completedRoutes) ||
                !TryCollectionSet(progress.visitedPorts, port => DestinationPorts.Contains(port), out var ports) ||
                !TryCollectionSet(progress.completedTreasureSeeds, IsCollectionTreasureSeed, out var treasures) ||
                !TryCollectionSet(progress.completedTreasureRanks, rank => CoreTreasureHuntCatalog.FindRank(rank) != null, out var ranks)) return false;
            var chartRoutes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string seed in charts)
            {
                TryParseChartSeed(seed, out CoreSeaChartDefinition chart);
                chartRoutes.Add(Routes[chart.RouteIndex].Id);
                if (!ports.Contains(chart.PortA) || !ports.Contains(chart.PortB)) return false;
            }
            if (!chartRoutes.SetEquals(completedRoutes) || ranks.Count > treasures.Count) return false;
            bool changed = ReplaceCollectionSet(collectionBosses, bosses);
            changed |= ReplaceCollectionSet(collectionCharts, charts);
            changed |= ReplaceCollectionSet(collectionRoutes, completedRoutes);
            changed |= ReplaceCollectionSet(collectionPorts, ports);
            changed |= ReplaceCollectionSet(collectionTreasureSeeds, treasures);
            changed |= ReplaceCollectionSet(collectionTreasureRanks, ranks);
            if (changed) TouchCollectionProgress();
            return true;
        }

        public void RecordCollectionPortVisit(string port)
        {
            if (DestinationPorts.Contains(port) && collectionPorts.Add(port)) TouchCollectionProgress();
        }

        /// <summary>Old records prove boss wins and treasure completions only. Visits,
        /// drawn charts and held maps cannot prove a successful crossing or treasure rank.</summary>
        public void ImportCollectionHistory(CoreSeaChartProgress history, CoreTreasureProgress treasure)
        {
            if (Phase != CoreDemoPhase.Port) return;
            bool changed = false;
            if (history != null && history.version == 1)
                foreach (CoreSeaChartState state in history.charts ?? Array.Empty<CoreSeaChartState>())
                {
                    if (state == null || state.visits <= 0 || !TryParseChartSeed(state.seed, out CoreSeaChartDefinition chart)) continue;
                    foreach (string boss in state.defeatedBosses ?? Array.Empty<string>())
                        if (CoreCollectionRewardCatalog.IsKnownBoss(boss) && IsKnownChartEncounter(chart, boss, true))
                            changed |= collectionBosses.Add(boss);
                }
            foreach (string seed in treasure?.completedSeeds ?? Array.Empty<string>())
            {
                string value = seed?.Trim();
                if (IsCollectionTreasureSeed(value)) changed |= collectionTreasureSeeds.Add(value);
            }
            if (changed) TouchCollectionProgress();
        }

        private void RecordCollectionBattleVictory()
        {
            string boss = ActiveEncounter?.Id;
            if (Phase == CoreDemoPhase.Battle && Battle != null &&
                CoreCollectionRewardCatalog.IsKnownBoss(boss) && collectionBosses.Add(boss)) TouchCollectionProgress();
        }

        private void CommitCollectionVoyageSettlement()
        {
            if (Map == null || Settlement == null) return;
            bool changed = DestinationPorts.Contains(Map.DepartureName) && collectionPorts.Add(Map.DepartureName);
            if (Settlement.Outcome == VoyageOutcome.ArrivedAtPort && !IsTreasureVoyage &&
                GuidedTutorialLeg == 0 && activeSeaChart?.TutorialWaters != true)
            {
                int route = -1;
                for (int i = 0; i < Routes.Count; i++) if (Routes[i].Id == Map.Route.Id) route = i;
                if (Settlement.Destination == Map.DestinationName &&
                    TryChartPorts(route, Map.DepartureName, Map.DestinationName, out int a, out int b, out int forward))
                {
                    string seed = activeSeaChart?.Seed ?? EncodeChartSeed(route, a, b, forward, 1, Map.Hash, 0, Map.GeometryVersion);
                    changed |= collectionCharts.Add(seed);
                    changed |= collectionRoutes.Add(Map.Route.Id);
                    changed |= collectionPorts.Add(Map.DestinationName);
                }
            }
            // Tutorial arrivals still count as visiting a real port, never as completing a normal chart.
            if (Settlement.Outcome == VoyageOutcome.ArrivedAtPort && DestinationPorts.Contains(Settlement.Destination))
                changed |= collectionPorts.Add(Settlement.Destination);
            if (Settlement.Outcome != VoyageOutcome.Failure && Settlement.TreasureCompleted &&
                IsTreasureVoyage && ActiveTreasureMap != null && IsCollectionTreasureSeed(ActiveTreasureMap.Seed) &&
                CoreTreasureHuntCatalog.FindRank(ActiveTreasureMap.RankId) != null)
            {
                changed |= collectionTreasureSeeds.Add(ActiveTreasureMap.Seed);
                changed |= collectionTreasureRanks.Add(ActiveTreasureMap.RankId);
            }
            if (changed) TouchCollectionProgress();
        }

        private static bool IsCollectionChartSeed(string seed) =>
            TryParseChartSeed(seed, out CoreSeaChartDefinition chart) && !chart.TutorialWaters && chart.Seed == seed;
        private static bool IsCollectionTreasureSeed(string seed) =>
            !string.IsNullOrWhiteSpace(seed) && seed.Length <= 64 && seed == seed.Trim();
        private static string[] CollectionSnapshot(HashSet<string> values) => values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        private static bool TryCollectionSet(string[] values, Func<string, bool> valid, out HashSet<string> result)
        {
            result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
                if (value == null || !valid(value) || !result.Add(value)) return false;
            return true;
        }
        private static bool ReplaceCollectionSet(HashSet<string> target, HashSet<string> source)
        {
            if (target.SetEquals(source)) return false;
            target.Clear(); target.UnionWith(source); return true;
        }
        private void TouchCollectionProgress() =>
            CollectionProgressRevision = CollectionProgressRevision == int.MaxValue ? 1 : CollectionProgressRevision + 1;
    }
}
