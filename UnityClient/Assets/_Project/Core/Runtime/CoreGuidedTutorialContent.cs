using System;
using System.Collections.Generic;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Authored first-chart lessons, resolved by the normal voyage and battle systems.</summary>
    public static class CoreGuidedTutorialContent
    {
        public const string Seed = "GUIDED-FIRST-CHART";
        public const string FirstEventId = "tutorial_first_event";
        public const string OptionalEventId = "tutorial_optional_event";
        public const string CrewRequirementEventId = "tutorial_crew_requirement_event";
        public const string NavigatorEventId = "tutorial_navigator_event";
        public const string RandomEventId = "guided_tutorial_random_event";
        public const string AmbushEventId = "tutorial_ambush_event";
        public const string BossEventId = "tutorial_boss_event";
        // Newly departed navigator voyages continue through the authored ambush
        // and boss. Leg 3 remains the retry/legacy entry point for old saves.
        public const int NavigatorVoyageLeg = 2;
        public const int BossVoyageLeg = 3;
        public const int PlannedDistanceNm = 120;

        // The guided encounter uses an authored action script. Its encounter ID
        // alone does not enable the script on an ordinary replayed sea chart.
        public static readonly CoreBattleProfile BossProfile = new CoreBattleProfile(
            CoreBossKind.Route, CoreBattleArchetype.Balanced, 52, 7, 5, 3, 0, 2, 1f, 1f, healthMultiplier: 1m);

        internal static CoreEncounter FirstEvent() => new CoreEncounter(FirstEventId,
            "航道断桅", "强制事件", "一截拖着缆绳的断桅横在船前。不处理就无法继续沿航道前进。", true, false, 1, 0,
            new CoreEncounterChoice("清理缆绳，继续航行", "耗时 1 小时；船体 -1", 1,
                new VoyageEffect { Hull = -1 }));

        internal static CoreEncounter OptionalEvent() => new CoreEncounter(OptionalEventId,
            "漂流补给箱", "可选事件", "潮水送来一只补给箱，完好的蜡封在浪间时隐时现。", false, false, 1, 0,
            new CoreEncounterChoice("靠近打捞", "耗时 1 小时；金币 +12", 1, new VoyageEffect { Gold = 12 }));

        internal static CoreEncounter CrewRequirementEvent() => new CoreEncounter(CrewRequirementEventId,
            "无法辨认的潮汐笔记", "船上事件", "水手从老船长留下的箱子里翻出一张六港誓约残页。只有航海士才能读懂上面的符号，找出通往第七港口的航路。", true, true, 0, 0,
            new CoreEncounterChoice("请航海士解读", "需要航海士；金币 +12", 1,
                new VoyageEffect { Gold = 12 }, "navigator"),
            new CoreEncounterChoice("收好笔记，返回海图", "保留线索；返回海图后，可返港招募航海士", 0, new VoyageEffect()));

        internal static CoreEncounter NavigatorEvent() => new CoreEncounter(NavigatorEventId,
            "潮汐暗标", "特殊船员事件", "笔记上的潮线与前方浮标吻合，交错的暗记指向礁间水道。浮标下拴着一只密封小匣，随潮水轻轻晃动。", true, false, 1, 0,
            new CoreEncounterChoice("请航海士辨认暗标", "需要航海士；耗时 1 小时；金币 +24", 1,
                new VoyageEffect { Gold = 24 }, "navigator"),
            new CoreEncounterChoice("保留笔记，返港整备", "暂缓解读；照常返航或请求拖带，结算后补充物资再来", 0, new VoyageEffect()));

        internal static CoreEncounter RandomEvent(bool luckyWind) => new CoreEncounter(RandomEventId,
            luckyWind ? "顺风捎来的讯息" : "水手发现漂流信筒", "随机事件",
            luckyWind ? "一艘过路快船临时靠近，把报酬和一封港口急信交给你。" :
                "刚刚掠过船侧的信筒里装着商会遗失的凭条。", false, true, 0, 0,
            new CoreEncounterChoice("接收讯息，记入日志", "耗时 0.5 小时；金币 +12、声望 +1", 0.5,
                new VoyageEffect { Gold = 12, Fame = 1 }));

        internal static CoreEncounter AmbushEvent() => new CoreEncounter(AmbushEventId,
            "礁影伏击", "航路伏击", "两道炮烟从礁影中升起。航海士认出一条绕行水道；避开这场伏击，仍可继续追踪拦路的首领。", true, true, 2, 70,
            new CoreEncounterChoice("避开炮烟，继续航行", "离开伏击区，继续当前航程", 0,
                new VoyageEffect()));

        internal static CoreEncounter BossEvent() => new CoreEncounter(BossEventId,
            "残帆掠夺者", "近港首领", "这艘海盗船曾受老船长庇护，如今却把自由旗抛在脑后，拦路收起了过路费。破帆下的船身横在航道上，炮口正缓缓转向你们。", true, false, 2, 50,
            new CoreEncounterChoice("迎击首领", "获胜后获得藏宝图碎片 ×1；返港领取白色部件与「破浪者的罗盘」", 0,
                new VoyageEffect(), startsBattle: true));
    }

    public sealed partial class CoreVoyageSession
    {
        private readonly List<string> guidedTutorialCompletedIds = new List<string>();
        private int guidedTutorialLayoutVersion = 1;
        public bool UsesCombinedGuidedTutorialVoyage => GuidedTutorialLeg == CoreGuidedTutorialContent.NavigatorVoyageLeg && guidedTutorialLayoutVersion == 1;
        private bool IsGuidedBossVoyage => GuidedTutorialLeg == CoreGuidedTutorialContent.BossVoyageLeg || UsesCombinedGuidedTutorialVoyage;
        public int GuidedTutorialLeg { get; private set; }
        public int GuidedTutorialCompletedEncounters => guidedTutorialCompletedIds.Count;
        public IReadOnlyList<string> GuidedTutorialCompletedEncounterIds => guidedTutorialCompletedIds.AsReadOnly();
        public bool GuidedTutorialRandomEventTriggered { get; private set; }
        public bool GuidedTutorialAmbushTriggered { get; private set; }
        public bool GuidedTutorialBossDefeated { get; private set; }
        public bool LastBattleRetreated { get; private set; }

        public bool ConfigureGuidedTutorialVoyage(int leg)
        {
            if (Phase != CoreDemoPhase.Port || leg < 0 || leg > CoreGuidedTutorialContent.BossVoyageLeg) return false;
            GuidedTutorialLeg = leg;
            guidedTutorialLayoutVersion = 1;
            if (leg > 0) SelectedRouteIndex = 0;
            return true;
        }

        private void ResetGuidedTutorialVoyageProgress()
        {
            guidedTutorialCompletedIds.Clear();
            GuidedTutorialRandomEventTriggered = false;
            GuidedTutorialAmbushTriggered = false;
            GuidedTutorialBossDefeated = false;
            LastBattleRetreated = false;
            GuidedBossStep = GuidedBossBattleStep.None;
        }

        private CoreVoyageMap GenerateGuidedTutorialMap(string departureName, string destinationOverride = null, int? replayLeg = null, uint? geographyHash = null)
        {
            int leg = replayLeg ?? GuidedTutorialLeg;
            CoreRouteDefinition source = routes[0];
            // A close-up of the same first chart, not a globally reduced sailing cost.
            var route = new CoreRouteDefinition(source.Id, source.Name, "近港引航水道", source.UnlockLevel,
                source.RecommendedPower, source.RecommendedVoyageDays, 10f, source.MinimumEventSpacing,
                0d, source.AmbushDetectionChance, "残帆掠夺者", 50);
            string destinationName = NormalizeDestination(departureName, destinationOverride ?? SelectedDestinationPort);
            uint hash = geographyHash ?? SeedHash.Compute(CoreGuidedTutorialContent.Seed + "::" + departureName + "::" + destinationName);
            var map = new CoreVoyageMap
            {
                Seed = CoreGuidedTutorialContent.Seed, Hash = hash, Route = route,
                Departure = new VoyagePoint(8f, 33f), Destination = new VoyagePoint(90f, 33f),
                DepartureName = departureName, DestinationName = destinationName,
                GlobalWindDirection = SailingDirection.East, GlobalWindVector = new VoyagePoint(1f, 0f),
                GlobalWindStrength = 6f, CurrentPhaseA = 0.7d, CurrentPhaseB = 1.2d,
            };
            CoreEncounter[] geographyAnchors = { CoreGuidedTutorialContent.FirstEvent(),
                CoreGuidedTutorialContent.OptionalEvent(), CoreGuidedTutorialContent.CrewRequirementEvent() };
            for (int index = 0; index < geographyAnchors.Length; index++)
                map.Nodes.Add(new CoreMapNode { Id = index, Position = new VoyagePoint(20f + index * 12f, 33f),
                    Encounter = geographyAnchors[index], TerrainConstraint = EventTerrainConstraint.OpenSea });
            // Identical coasts across tutorial templates, including the legacy
            // ambush template; all use the normal renderer's terrain/footprints.
            VoyageMapGenerator.GenerateFixedLayout(map);
            if (leg > 1)
            {
                bool combined = leg == CoreGuidedTutorialContent.NavigatorVoyageLeg && guidedTutorialLayoutVersion == 1;
                map.Nodes[0].Encounter = leg == 2 ? CoreGuidedTutorialContent.NavigatorEvent() : CoreGuidedTutorialContent.BossEvent();
                map.Nodes[0].IsBoss = leg == CoreGuidedTutorialContent.BossVoyageLeg;
                int retained = combined ? 2 : 1;
                if (combined)
                {
                    map.Nodes[1].Encounter = CoreGuidedTutorialContent.BossEvent();
                    map.Nodes[1].IsBoss = true;
                }
                map.Nodes.RemoveRange(retained, 3 - retained);
                foreach (CoreEventFootprint footprint in map.MutableEventFootprints)
                {
                    if (footprint.NodeId < retained) continue;
                    foreach (int cell in footprint.ReservedCellIndices) map.EventReservedCells[cell] = false;
                }
                map.MutableEventFootprints.RemoveAll(footprint => footprint.NodeId >= retained);
                for (int index = 0; index < map.CellCount; index++)
                {
                    if (map.AnchorNodeIds[index] < retained) continue;
                    map.AnchorNodeIds[index] = -1;
                    map.EventAnchorCells[index] = false;
                    map.EventReservedCells[index] = false;
                }
                // The random encounter and ambush are not hidden map nodes. No map,
                // lookout, telescope or reveal-intelligence API can expose them.
            }
            GenerateTerrainCurrents(map, new Mulberry32Random(hash ^ 0x6A09E667u));
            return map;
        }

        private CoreEncounter FindGuidedTutorialTransientEncounter(IReadOnlyList<VoyagePoint> path,
            out VoyagePoint position, out float progress)
        {
            position = default(VoyagePoint);
            progress = float.MaxValue;
            if (!UsesCombinedGuidedTutorialVoyage || GuidedTutorialAmbushTriggered ||
                !guidedTutorialCompletedIds.Contains(CoreGuidedTutorialContent.NavigatorEventId) || path == null || path.Count < 2) return null;
            float length = PathDistance(path);
            if (length <= .01f) return null;
            float remaining = Math.Min(3f, length * .5f);
            float traversed = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                float segment = Distance(path[i - 1], path[i]);
                if (segment > 0 && traversed + segment >= remaining)
                {
                    float t = (remaining - traversed) / segment;
                    position = new VoyagePoint(path[i - 1].X + (path[i].X - path[i - 1].X) * t,
                        path[i - 1].Y + (path[i].Y - path[i - 1].Y) * t);
                    progress = remaining;
                    return CoreGuidedTutorialContent.AmbushEvent();
                }
                traversed += segment;
            }
            return null;
        }

        private void BeginGuidedTutorialTransientEncounter(CoreEncounter encounter)
        {
            if (encounter.Id == CoreGuidedTutorialContent.RandomEventId)
                encounter = CoreGuidedTutorialContent.RandomEvent(voyageRandom.NextDouble() < 0.5d);
            GuidedTutorialRandomEventTriggered |= encounter.Id == CoreGuidedTutorialContent.RandomEventId;
            GuidedTutorialAmbushTriggered |= encounter.Id == CoreGuidedTutorialContent.AmbushEventId;
            ActiveEncounter = encounter;
            activeNode = null;
            Phase = CoreDemoPhase.Encounter;
            AddLog("航行中遭遇「" + encounter.Name + "」。");
        }

        private void RecordGuidedTutorialEncounterCompleted(CoreEncounter encounter)
        {
            if (GuidedTutorialLeg == 0 || encounter == null || guidedTutorialCompletedIds.Contains(encounter.Id)) return;
            guidedTutorialCompletedIds.Add(encounter.Id);
        }

        public bool CanRetreatBattle => Phase == CoreDemoPhase.Battle && Battle != null &&
            !IsGuidedBossBattle && Battle.Stage != NavalBattleStage.Boarding && Hull > 0 &&
            Cargo.Water >= RationCostFor(1d) + 2 && Cargo.Food >= RationCostFor(1d) + 1;

        /// <summary>Normal pre-boarding escape: travel time, pursuit damage, then the ordinary return settlement.</summary>
        public bool RetreatFromBattle()
        {
            if (!CanRetreatBattle) return false;
            int pursuit = Battle.Stage == NavalBattleStage.Preparation ? 2 : 5;
            ConsumeHours(1d);
            Cargo.Water -= 2;
            Cargo.Food -= 1;
            Hull = Math.Max(0, Hull - pursuit);
            LastBattleRetreated = true;
            AddLog("撤离海战：耗时 1 小时，追击造成船体 -" + pursuit + "；放弃本场战利品并返港结算。");
            pendingBattleReward = null;
            FinishVoyage(Hull <= 0 ? VoyageOutcome.Failure : VoyageOutcome.SafeReturn);
            return true;
        }
    }
}
