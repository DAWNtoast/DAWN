using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    [Serializable] public sealed class CoreSeaChartProgress
    {
        public int version = 1;
        public CoreSeaChartState[] charts = Array.Empty<CoreSeaChartState>();
        public CoreSeaChartAlias[] aliases = Array.Empty<CoreSeaChartAlias>();
        // Optional in old saves. History is shared by sea, not tied to one seed.
        public CoreSeaChartRandomHistory[] randomHistory = Array.Empty<CoreSeaChartRandomHistory>();
    }
    [Serializable] public sealed class CoreSeaChartState
    {
        public string seed;
        public int visits;
        public int pendingTutorialFragments;
        public string lastRandomEvent;
        public CoreSeaChartEventClaim[] events = Array.Empty<CoreSeaChartEventClaim>();
        public string[] defeatedBosses = Array.Empty<string>();
    }
    [Serializable] public sealed class CoreSeaChartEventClaim { public string eventId; public int count; }
    [Serializable] public sealed class CoreSeaChartAlias { public string rawSeed; public string canonicalSeed; }

    public sealed partial class CoreVoyageSession
    {
        private readonly Dictionary<string, CoreSeaChartState> seaChartStates = new Dictionary<string, CoreSeaChartState>(StringComparer.Ordinal);
        private readonly List<CoreSeaChartAlias> seaChartAliases = new List<CoreSeaChartAlias>();
        private CoreSeaChartDefinition selectedSeaChart, activeSeaChart;
        public int SeaChartProgressRevision { get; private set; }
        public bool IsChartSeedSelected => selectedSeaChart != null;
        public string CurrentChartSeed => activeSeaChart?.Seed ?? string.Empty;
        public CoreSeaChartDefinition SelectedChartDefinition => selectedSeaChart;

        public void ClearChartSeedSelection()
        {
            if (Phase == CoreDemoPhase.Port) selectedSeaChart = null;
        }

        public bool TrySelectChartSeed(string seed, out string reason)
        {
            reason = Phase != CoreDemoPhase.Port ? "只能在港口选择海图。" :
                GuidedTutorialLeg > 0 ? "请先完成当前教学航行。" : string.Empty;
            if (reason.Length > 0) return false;
            string candidate = (seed ?? string.Empty).Trim();
            if (!TryParseChartSeed(candidate, out CoreSeaChartDefinition chart))
            {
                string[] aliases = seaChartAliases.Where(item => item.rawSeed == candidate)
                    .Select(item => item.canonicalSeed).Distinct(StringComparer.Ordinal).ToArray();
                if (aliases.Length != 1 || !TryParseChartSeed(aliases[0], out chart))
                {
                    reason = aliases.Length > 1 ? "旧种子对应多张海图，请从已绘记录中选择完整种子。" : "海图种子无效，或旧种子尚无已绘记录。";
                    return false;
                }
            }
            if (!chart.ContainsPort(CurrentPort))
            {
                reason = "这张海图连接" + chart.PortA + "与" + chart.PortB + "，只能从其中一港出航。";
                return false;
            }
            if (!CanSelectRoute(chart.RouteIndex, out reason)) return false;
            selectedSeaChart = chart;
            SelectedSeed = chart.Seed;
            SelectedRouteIndex = chart.RouteIndex;
            SelectedDestinationPort = chart.OtherPort(CurrentPort);
            selectedTreasureMapId = string.Empty;
            return true;
        }

        public bool CanStartSelectedChart(out string reason)
        {
            if (!CanStartWithDepartureCrew(out reason)) return false;
            if (selectedSeaChart == null || GuidedTutorialLeg > 0 || SelectedTreasureMap != null) return true;
            if (!CanSelectRoute(selectedSeaChart.RouteIndex, out reason)) return false;
            if (Phase != CoreDemoPhase.Port) reason = "只能在港口载入海图。";
            else if (!selectedSeaChart.ContainsPort(CurrentPort))
                reason = "这张海图仅连接" + selectedSeaChart.PortA + "与" + selectedSeaChart.PortB + "。";
            else if (SelectedRouteIndex != selectedSeaChart.RouteIndex || SelectedDestinationPort != selectedSeaChart.OtherPort(CurrentPort))
                reason = "海图的航线和两端港口不能更改，请重新载入海图。";
            return reason.Length == 0;
        }

        public CoreSeaChartProgress CaptureSeaChartProgress() => new CoreSeaChartProgress
        {
            charts = seaChartStates.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => CopySeaChartState(pair.Value)).ToArray(),
            randomHistory = routeRandomHistory.OrderBy(pair => pair.Key).Select(pair => new CoreSeaChartRandomHistory
                { routeIndex = pair.Key, recentEvents = pair.Value.ToArray() }).ToArray(),
            aliases = seaChartAliases.OrderBy(item => item.rawSeed, StringComparer.Ordinal).ThenBy(item => item.canonicalSeed, StringComparer.Ordinal)
                .Select(item => new CoreSeaChartAlias { rawSeed = item.rawSeed, canonicalSeed = item.canonicalSeed }).ToArray(),
        };

        public bool RestoreSeaChartProgress(CoreSeaChartProgress progress)
        {
            if (Phase != CoreDemoPhase.Port || progress == null || progress.version != 1) return false;
            var states = new Dictionary<string, CoreSeaChartState>(StringComparer.Ordinal);
            var aliases = new List<CoreSeaChartAlias>();
            var histories = new Dictionary<int, List<string>>();
            foreach (CoreSeaChartRandomHistory history in progress.randomHistory ?? Array.Empty<CoreSeaChartRandomHistory>())
            {
                if (history == null || history.routeIndex < 0 || history.routeIndex >= Routes.Count ||
                    histories.ContainsKey(history.routeIndex) || history.recentEvents == null ||
                    history.recentEvents.Length > SeaChartRandomHistoryLimit ||
                    history.recentEvents.Distinct(StringComparer.Ordinal).Count() != history.recentEvents.Length ||
                    history.recentEvents.Any(id => !IsRouteRandomEvent(history.routeIndex, id))) return false;
                histories.Add(history.routeIndex, new List<string>(history.recentEvents));
            }
            foreach (CoreSeaChartState value in progress.charts ?? Array.Empty<CoreSeaChartState>())
            {
                if (value == null || !TryParseChartSeed(value.seed, out CoreSeaChartDefinition definition) || value.visits < 0 ||
                    states.ContainsKey(definition.Seed) || !string.IsNullOrEmpty(value.lastRandomEvent) &&
                    !IsRouteRandomEvent(definition.RouteIndex, value.lastRandomEvent)) return false;
                var events = new HashSet<string>(StringComparer.Ordinal);
                foreach (CoreSeaChartEventClaim claim in value.events ?? Array.Empty<CoreSeaChartEventClaim>())
                    if (claim == null || claim.count < 0 || !IsKnownChartEncounter(definition, claim.eventId, false) || !events.Add(claim.eventId)) return false;
                var bosses = new HashSet<string>(StringComparer.Ordinal);
                foreach (string boss in value.defeatedBosses ?? Array.Empty<string>())
                    if (!IsKnownChartEncounter(definition, boss, true) || !bosses.Add(boss)) return false;
                if (value.pendingTutorialFragments < 0 || value.pendingTutorialFragments > 1 ||
                    value.pendingTutorialFragments == 1 && (!definition.TutorialWaters ||
                        !bosses.Contains(CoreGuidedTutorialContent.BossEventId))) return false;
                CoreSeaChartState copied = CopySeaChartState(value); copied.seed = definition.Seed;
                states.Add(definition.Seed, copied);
            }
            var aliasKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoreSeaChartAlias alias in progress.aliases ?? Array.Empty<CoreSeaChartAlias>())
            {
                if (alias == null || string.IsNullOrWhiteSpace(alias.rawSeed) || alias.rawSeed.Length > 512 ||
                    !TryParseChartSeed(alias.canonicalSeed, out CoreSeaChartDefinition definition) || !states.ContainsKey(definition.Seed)) return false;
                string raw = alias.rawSeed.Trim();
                if (!aliasKeys.Add(raw + "\n" + definition.Seed)) return false;
                aliases.Add(new CoreSeaChartAlias { rawSeed = raw, canonicalSeed = definition.Seed });
            }
            seaChartStates.Clear(); foreach (var pair in states) seaChartStates.Add(pair.Key, pair.Value);
            routeRandomHistory.Clear(); foreach (var pair in histories) routeRandomHistory.Add(pair.Key, pair.Value);
            seaChartAliases.Clear(); seaChartAliases.AddRange(aliases);
            TouchSeaChartProgress();
            return true;
        }

        public bool TryImportLegacyChart(string rawSeed, int routeIndex, string departurePort, string destinationPort,
            uint geographyHash, bool tutorialWaters, out string canonicalSeed, int geometryVersion = 0)
        {
            canonicalSeed = string.Empty;
            if (!ValidMapGeometryVersion(geometryVersion) || tutorialWaters && geometryVersion != 0 ||
                (Phase != CoreDemoPhase.Port && Phase != CoreDemoPhase.Settlement) || string.IsNullOrWhiteSpace(rawSeed) || rawSeed.Length > 512 ||
                !TryChartPorts(routeIndex, departurePort, destinationPort, out int a, out int b, out int forward)) return false;
            string raw = rawSeed.Trim();
            if (TryParseChartSeed(raw, out CoreSeaChartDefinition parsed))
            {
                if (parsed.GeographyHash != geographyHash || parsed.RouteIndex != routeIndex || !parsed.ContainsPort(departurePort) ||
                    parsed.OtherPort(departurePort) != destinationPort || parsed.TutorialWaters != tutorialWaters ||
                    parsed.MapGeometryVersion != geometryVersion) return false;
                canonicalSeed = parsed.Seed;
            }
            else
            {
                uint expected = SeedHash.Compute(tutorialWaters ? CoreGuidedTutorialContent.Seed + "::" + departurePort + "::" + destinationPort :
                    Routes[routeIndex].Id + "::" + raw + "::" + departurePort + "::" + destinationPort);
                if (geographyHash != expected || tutorialWaters && routeIndex != 0) return false;
                canonicalSeed = EncodeChartSeed(routeIndex, a, b, forward, tutorialWaters ? 2 : 1, geographyHash, 0, geometryVersion);
            }
            TryParseChartSeed(canonicalSeed, out CoreSeaChartDefinition definition);
            CoreSeaChartState state = GetOrCreateSeaChartState(definition);
            if (raw != canonicalSeed && !seaChartAliases.Any(alias => alias.rawSeed == raw && alias.canonicalSeed == definition.Seed))
                seaChartAliases.Add(new CoreSeaChartAlias { rawSeed = raw, canonicalSeed = definition.Seed });
            if (Map != null && Map.GeometryVersion == geometryVersion && Map.Hash == geographyHash && Map.DepartureName == departurePort && Map.DestinationName == destinationPort)
            {
                foreach (CoreMapNode node in Map.Nodes.Where(node => node.Visited && !node.Skipped))
                {
                    if (node.Encounter.BossKind != CoreBossKind.None || node.IsBoss) RecordSeaChartBoss(state, node.Encounter.Id);
                    else if (node.Encounter.RepeatPolicy != CoreEncounterRepeatPolicy.Renewable)
                        SetSeaChartClaimCount(state, node.Encounter.Id, Math.Max(1, SeaChartClaimCount(state, node.Encounter.Id)));
                }
                activeSeaChart = definition;
            }
            TouchSeaChartProgress();
            return true;
        }

        private bool IsKnownChartEncounter(CoreSeaChartDefinition chart, string eventId, bool bossOnly)
        {
            if (string.IsNullOrEmpty(eventId)) return false;
            if (chart.TutorialWaters) return eventId == CoreGuidedTutorialContent.BossEventId || !bossOnly &&
                new[] { CoreGuidedTutorialContent.FirstEvent().Id, CoreGuidedTutorialContent.OptionalEvent().Id,
                    CoreGuidedTutorialContent.CrewRequirementEvent().Id, CoreGuidedTutorialContent.NavigatorEventId }.Contains(eventId);
            CoreRouteBoss routeBoss = CoreRouteBossCatalog.Find(eventId);
            if (routeBoss != null && routeBoss.RouteId == Routes[chart.RouteIndex].Id) return true;
            if (chart.RouteIndex == 2 && (eventId == CoreBattleBalance.TreasureBossId || CoreLegendaryWarships.Find(eventId) != null)) return true;
            return !bossOnly && (CreateEncounterPool(Routes[chart.RouteIndex]).Any(item => item.Id == eventId) ||
                chart.MapGeometryVersion >= 2 && CoreVoyageEventCatalog.CreateFixedEncounters(Routes[chart.RouteIndex].Id).Any(item => item.Id == eventId) ||
                chart.MapGeometryVersion >= 3 && CoreVoyageEventCatalog.CreateExpandedFixedEncounters(Routes[chart.RouteIndex].Id).Any(item => item.Id == eventId));
        }

        private static CoreSeaChartState CopySeaChartState(CoreSeaChartState state) => new CoreSeaChartState
        {
            seed = state.seed, visits = state.visits, lastRandomEvent = state.lastRandomEvent ?? string.Empty,
            pendingTutorialFragments = state.pendingTutorialFragments,
            events = (state.events ?? Array.Empty<CoreSeaChartEventClaim>()).OrderBy(item => item.eventId, StringComparer.Ordinal)
                .Select(item => new CoreSeaChartEventClaim { eventId = item.eventId, count = item.count }).ToArray(),
            defeatedBosses = (state.defeatedBosses ?? Array.Empty<string>()).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
        };
        private CoreSeaChartState GetOrCreateSeaChartState(CoreSeaChartDefinition chart)
        {
            if (!seaChartStates.TryGetValue(chart.Seed, out CoreSeaChartState state))
            { state = new CoreSeaChartState { seed = chart.Seed, lastRandomEvent = string.Empty }; seaChartStates.Add(chart.Seed, state); }
            return state;
        }
        private CoreSeaChartState ActiveSeaChartState => activeSeaChart != null && seaChartStates.TryGetValue(activeSeaChart.Seed, out CoreSeaChartState state) ? state : null;
        private static int SeaChartClaimCount(CoreSeaChartState state, string eventId) => state?.events.FirstOrDefault(item => item.eventId == eventId)?.count ?? 0;
        private static void SetSeaChartClaimCount(CoreSeaChartState state, string eventId, int count)
        {
            CoreSeaChartEventClaim claim = state.events.FirstOrDefault(item => item.eventId == eventId);
            if (claim != null) claim.count = count;
            else state.events = state.events.Concat(new[] { new CoreSeaChartEventClaim { eventId = eventId, count = count } }).ToArray();
        }
        private static void RecordSeaChartBoss(CoreSeaChartState state, string eventId)
        {
            if (!state.defeatedBosses.Contains(eventId)) state.defeatedBosses = state.defeatedBosses.Concat(new[] { eventId }).ToArray();
        }
        private void TouchSeaChartProgress() => SeaChartProgressRevision = SeaChartProgressRevision == int.MaxValue ? 1 : SeaChartProgressRevision + 1;
    }
}
