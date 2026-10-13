using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private static readonly string[] SeaChartRandomTemplates =
            { "random_tailwind", "random_floating_supplies", "random_food_spoilage", "random_ambush" };
        private Mulberry32Random encounterRandom;
        private CoreEncounter[] chartRandomEncounters = Array.Empty<CoreEncounter>();
        private string[] chartRandomOrder = Array.Empty<string>();
        private int chartRandomIndex;
        private float nextChartRandomDistance;
        private int seaChartCompletedRemovedNodes;
        public IReadOnlyList<string> CurrentVoyageRandomEventIds => Array.AsReadOnly(chartRandomOrder);

        /// <summary>Builds a detached chart for the departure preview without
        /// starting a voyage or recording a chart visit.</summary>
        public CoreVoyageMap PreviewSelectedSeaChart()
        {
            return Phase == CoreDemoPhase.Port && selectedSeaChart != null
                ? GenerateSelectedSeaChartMap()
                : null;
        }

        private CoreVoyageMap GenerateSelectedSeaChartMap()
        {
            CoreSeaChartDefinition chart = selectedSeaChart;
            CoreVoyageMap map = chart.TutorialWaters
                ? GenerateGuidedTutorialMap(chart.ForwardDeparturePort, chart.ForwardDestinationPort,
                    CoreGuidedTutorialContent.BossVoyageLeg, chart.GeographyHash)
                : GenerateMap(Routes[chart.RouteIndex], chart.Seed, chart.ForwardDeparturePort,
                    chart.ForwardDestinationPort, chart.GeographyHash, chart.MapGeometryVersion);
            map.Seed = chart.Seed;
            // A chart has one world coordinate system. Returning from its other
            // endpoint swaps port roles, not coastlines, currents or event IDs.
            if (CurrentPort != chart.ForwardDeparturePort)
            {
                VoyagePoint point = map.Departure; map.Departure = map.Destination; map.Destination = point;
                string port = map.DepartureName; map.DepartureName = map.DestinationName; map.DestinationName = port;
            }
            return map;
        }

        private void BeginSeaChartVoyage(CoreTreasureMapRecord treasure)
        {
            seaChartCompletedRemovedNodes = 0;
            activeSeaChart = treasure == null ? selectedSeaChart : null;
            if (treasure == null && GuidedTutorialLeg > 0 && TryChartPorts(0, Map.DepartureName, Map.DestinationName,
                out int a, out int b, out int forward))
                TryParseChartSeed(EncodeChartSeed(0, a, b, forward, 2, Map.Hash, 0), out activeSeaChart);
            chartRandomEncounters = Array.Empty<CoreEncounter>(); chartRandomOrder = Array.Empty<string>();
            encounterRandom = null; chartRandomIndex = 0;
            if (activeSeaChart == null) return;
            CoreSeaChartState state = GetOrCreateSeaChartState(activeSeaChart);
            state.visits = state.visits == int.MaxValue ? int.MaxValue : state.visits + 1;
            string routeBoss = activeSeaChart.TutorialWaters ? CoreGuidedTutorialContent.BossEventId : "boss_" + Map.Route.Id;
            bossDefeated = state.defeatedBosses.Contains(routeBoss);
            RemoveDefeatedSeaChartBosses(state);
            if (IsGuidedBossVoyage && bossDefeated)
            {
                GuidedTutorialBossDefeated = true;
                // An interrupted first victory keeps its guaranteed reward until
                // settlement is committed. Ordinary chart replay cannot claim it.
                if (state.pendingTutorialFragments == 1)
                {
                    voyageTreasureFragments = 1;
                    seaChartCompletedRemovedNodes = 1;
                }
            }
            if (GuidedTutorialLeg == 0)
            {
                encounterRandom = new Mulberry32Random(unchecked((uint)Guid.NewGuid().GetHashCode()) ^ Map.Hash ^ (uint)state.visits);
                chartRandomOrder = (string[])SeaChartRandomTemplates.Clone();
                for (int i = chartRandomOrder.Length - 1; i > 0; i--)
                {
                    int j = (int)(encounterRandom.NextDouble() * (i + 1));
                    string item = chartRandomOrder[i]; chartRandomOrder[i] = chartRandomOrder[j]; chartRandomOrder[j] = item;
                }
                // Fresh entropy can still repeat by chance; explicitly avoid
                // replaying the previous voyage's first dynamic template.
                if (chartRandomOrder[0] == state.lastRandomEvent)
                { string item = chartRandomOrder[0]; chartRandomOrder[0] = chartRandomOrder[1]; chartRandomOrder[1] = item; }
                state.lastRandomEvent = chartRandomOrder[0];
                chartRandomEncounters = chartRandomOrder.Select(CreateSeaChartRandomEncounter).ToArray();
                nextChartRandomDistance = 120f + (float)encounterRandom.NextDouble() * 120f;
            }
            TouchSeaChartProgress();
        }

        private void RemoveDefeatedSeaChartBosses(CoreSeaChartState state, bool countCompletedThisVoyage = false)
        {
            var removed = new HashSet<int>(Map.Nodes.Where(node => state.defeatedBosses.Contains(node.Encounter.Id)).Select(node => node.Id));
            if (removed.Count == 0) return;
            if (countCompletedThisVoyage)
                seaChartCompletedRemovedNodes += Map.Nodes.Count(node => removed.Contains(node.Id) && node.Visited && !node.Skipped);
            Map.Nodes.RemoveAll(node => removed.Contains(node.Id));
            foreach (CoreEventFootprint footprint in Map.MutableEventFootprints.Where(item => removed.Contains(item.NodeId)))
                foreach (int cell in footprint.ReservedCellIndices) Map.EventReservedCells[cell] = false;
            Map.MutableEventFootprints.RemoveAll(item => removed.Contains(item.NodeId));
            for (int i = 0; i < Map.CellCount; i++)
                if (removed.Contains(Map.AnchorNodeIds[i])) { Map.AnchorNodeIds[i] = -1; Map.EventAnchorCells[i] = false; }
            // Terrain/current arrays were generated with all original anchors.
            // Defeated ships disappear without moving islands or other events.
        }

        private static bool IsKnownChartRandomEvent(string id, bool allowEmpty) =>
            allowEmpty && string.IsNullOrEmpty(id) || Array.IndexOf(SeaChartRandomTemplates, id) >= 0;

        private CoreEncounter CreateSeaChartRandomEncounter(string id)
        {
            CoreEncounter result;
            switch (id)
            {
                case "random_tailwind":
                    result = new CoreEncounter(id, "顺风欢歌", "随机航行事件", "顺风吹满船帆，水手们用歌声互相鼓劲。", false, false, 1, 0,
                        new CoreEncounterChoice("随风前进", "士气 +1、声望 +2", 0, new VoyageEffect { Morale = 1, Fame = 2 }));
                    break;
                case "random_floating_supplies":
                    result = new CoreEncounter(id, "漂流补给桶", "随机航行事件", "浪间漂来几只封口完好的补给桶。", false, false, 1, 0,
                        new CoreEncounterChoice("捞起补给", "淡水 +8、食物 +8", .5, new VoyageEffect { Water = 8, Food = 8 }));
                    break;
                case "random_food_spoilage":
                    result = new CoreEncounter(id, "潮湿的粮袋", "随机航行事件", "潮气钻进了粮袋，需要及时处理。", true, false, 1, 0,
                        new CoreEncounterChoice("丢弃坏粮", "食物 -4", 0, new VoyageEffect { Food = -4 }),
                        new CoreEncounterChoice("封闭漏水货格", "船体 -1，不消耗物资", 0, new VoyageEffect { Hull = -1 }));
                    break;
                default: result = CreateRandomAmbush(encounterRandom.NextDouble() < AmbushDetectionChance); break;
            }
            result.RepeatPolicy = CoreEncounterRepeatPolicy.RandomVoyage;
            return result;
        }

        private CoreEncounter FindSeaChartTransientEncounter(IReadOnlyList<VoyagePoint> path, out VoyagePoint position, out float progress)
        {
            position = default(VoyagePoint); progress = float.MaxValue;
            if (activeSeaChart == null || GuidedTutorialLeg > 0 || chartRandomIndex >= chartRandomEncounters.Length || path == null || path.Count < 2)
                return null;
            float remaining = Math.Max(1f, nextChartRandomDistance - TotalDistanceNm) / Map.Route.NauticalMilesPerMapUnit;
            if (remaining >= PathDistance(path) - .001f) return null;
            float traversed = 0f;
            for (int index = 1; index < path.Count; index++)
            {
                float segment = Distance(path[index - 1], path[index]);
                if (traversed + segment >= remaining && segment > 0f)
                {
                    float t = (remaining - traversed) / segment;
                    position = new VoyagePoint(path[index - 1].X + (path[index].X - path[index - 1].X) * t,
                        path[index - 1].Y + (path[index].Y - path[index - 1].Y) * t);
                    progress = remaining;
                    return chartRandomEncounters[chartRandomIndex];
                }
                traversed += segment;
            }
            return null;
        }

        private void BeginSeaChartTransientEncounter(CoreEncounter encounter)
        {
            chartRandomIndex++;
            nextChartRandomDistance = TotalDistanceNm + 180f + (float)encounterRandom.NextDouble() * 240f;
            ActiveEncounter = encounter; activeNode = null; Phase = CoreDemoPhase.Encounter;
            AddLog("航行中遭遇「" + encounter.Name + "」；本次出航不会重复触发该随机事件。");
        }
    }
}
