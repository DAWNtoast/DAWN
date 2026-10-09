using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Save-wide, monotonic milestones. Seeds identify distinct completed voyages.</summary>
    [Serializable]
    public sealed class CoreCollectionProgress
    {
        public int version = 1;
        public string[] defeatedBossIds = Array.Empty<string>();
        public string[] completedChartSeeds = Array.Empty<string>();
        public string[] completedRouteIds = Array.Empty<string>();
        public string[] visitedPorts = Array.Empty<string>();
        public string[] completedTreasureSeeds = Array.Empty<string>();
        public string[] completedTreasureRanks = Array.Empty<string>();
    }

    /// <summary>Existing profile ownership used to reconcile collection rewards.</summary>
    public sealed class CoreCollectionFacts
    {
        public string[] recruitedCrewIds = Array.Empty<string>();
    }

    public sealed class CoreCollectionRewardRule
    {
        private readonly Func<CoreCollectionProgress, CoreCollectionFacts, IReadOnlyCollection<string>, bool> condition;

        public string Id { get; }
        public string Source { get; }

        internal CoreCollectionRewardRule(string id, string source,
            Func<CoreCollectionProgress, CoreCollectionFacts, IReadOnlyCollection<string>, bool> condition)
        {
            Id = id;
            Source = source;
            this.condition = condition;
        }

        // Evaluation does not grant, consume, or mutate anything. Callers union ownership across ports.
        public bool IsSatisfied(CoreCollectionProgress progress, CoreCollectionFacts facts,
            IReadOnlyCollection<string> ownedIds) =>
            condition(progress ?? new CoreCollectionProgress(), facts ?? new CoreCollectionFacts(),
                ownedIds ?? Array.Empty<string>());
    }

    /// <summary>Every keepsake has one finite, deterministic path within the same save.</summary>
    public static class CoreCollectionRewardCatalog
    {
        private const string ReefRoute = "route_shattered_sail_reef";
        private const string GoldenRoute = "route_golden_tide_trade";
        private const string MoonRoute = "route_moon_tide_ruins";
        private const string ChartCompletion = "（非教学普通航程抵达目标港并确认结算，不要求全探索）";
        private const string TreasureCompletion = "（开启秘藏后确认结算）";
        private const string FinalTreasure = "seventh_harbor_beacon_lantern";
        private static readonly string[] routeIds = { ReefRoute, GoldenRoute, MoonRoute };
        private static readonly string[] legendaryBossIds =
        {
            "legendary_pirate_iron_oath", "legendary_pirate_thunder_crown", "legendary_pirate_red_widow",
            "legendary_pirate_ember_ark", "legendary_pirate_tide_requiem"
        };

        public static IReadOnlyList<CoreCollectionRewardRule> All { get; } = Array.AsReadOnly(new[]
        {
            Rule("first_victory_compass", "首领 · 首次击败任意首领（含教学首领）",
                (p, f, o) => CountDistinct(p.defeatedBossIds, IsKnownBoss) >= 1),
            Boss("boatswain_bell", "boss_" + GoldenRoute, "黄金潮汐商路首领：税金女王号"),
            Rule("sealed_chart", "海图 · 完成 3 张不同普通海图" + ChartCompletion,
                (p, f, o) => CountDistinct(p.completedChartSeeds) >= 3),
            Rule("old_captain_free_flag", "海图 · 完成碎帆礁海、黄金潮汐商路、月潮遗迹海各一次" + ChartCompletion,
                (p, f, o) => ContainsAll(p.completedRouteIds, routeIds)),
            Rule("six_port_anchor_seal", "航海 · 到访全部 6 个港口（含起始港）",
                (p, f, o) => ContainsAll(p.visitedPorts, CoreVoyageSession.DestinationPorts)),
            Rule("no_crown_logbook", "首领 · 战胜全部 5 艘传说舰（潮汐镇魂号为切磋）",
                (p, f, o) => ContainsAll(p.defeatedBossIds, legendaryBossIds)),
            Boss("first_stormglass", "boss_" + ReefRoute, "碎帆礁海首领：裂桅号"),
            Rule("starboard_copper_scope", "首领 · 战胜 3 种不同首领（含教学首领）",
                (p, f, o) => CountDistinct(p.defeatedBossIds, IsKnownBoss) >= 3),
            Rule("saltworn_quartermaster_key", "海图 · 完成 1 张普通海图" + ChartCompletion,
                (p, f, o) => CountDistinct(p.completedChartSeeds) >= 1),
            Rule("first_berth_drydock_nail", "航海 · 到访 3 个不同港口（含起始港）",
                (p, f, o) => CountDistinct(p.visitedPorts, IsKnownPort) >= 3),
            Boss("godbreaker_oar", "boss_" + MoonRoute, "月潮遗迹海首领：月蚀圣骸"),
            TreasureRank("leviathan_bone_whistle", "weathered", "残旧"),
            Rule("moon_tide_astrolabe", "首领 · 战胜碎帆礁海、黄金潮汐商路、月潮遗迹海的全部区域首领",
                (p, f, o) => Contains(p.defeatedBossIds, "boss_" + ReefRoute) &&
                    Contains(p.defeatedBossIds, "boss_" + GoldenRoute) && Contains(p.defeatedBossIds, "boss_" + MoonRoute)),
            Boss("iron_oath_shackle", "legendary_pirate_iron_oath", "传说舰：铁誓不沉号"),
            Boss("thunder_crown_fuse", "legendary_pirate_thunder_crown", "传说舰：雷冠审判号"),
            Boss("red_widow_grappling_hook", "legendary_pirate_red_widow", "传说舰：绯刃寡妇号"),
            Boss("ember_ark_fireseed", "legendary_pirate_ember_ark", "传说舰：余烬方舟号"),
            Rule("tide_requiem_gauge", "首领 · 首次与潮汐镇魂号切磋获胜",
                (p, f, o) => Contains(p.defeatedBossIds, "legendary_pirate_tide_requiem")),
            Crew("evelyn_six_port_ruler", "crew_gray_gull_navigator_evelyn", "灰鸥港", "星盘手 伊芙琳"),
            Crew("barlow_living_hull_plane", "crew_gray_gull_carpenter_barlow", "灰鸥港", "船匠 巴洛"),
            Crew("moer_storm_suture_kit", "crew_gray_gull_doctor_moer", "灰鸥港", "船医 莫尔"),
            Crew("leisa_free_flag_badge", "crew_gray_gull_gunner_leisa", "灰鸥港", "炮术官 雷萨"),
            Crew("pepe_shared_pot_lid", "crew_gray_gull_cook_pepe", "灰鸥港", "厨师 佩佩"),
            Crew("lorne_first_berth_token", "crew_broken_anchor_navigator_lorne", "断锚港", "测潮师 洛恩"),
            Crew("atang_white_shell_button", "crew_white_shell_carpenter_atang", "白贝港", "贝骨船匠 阿棠"),
            Crew("ada_bronze_bell_bandage", "crew_bronze_bell_doctor_ada", "铜钟港", "退役军医 艾达"),
            Crew("helen_thirteenth_cannon_coin", "crew_bronze_bell_gunner_helen", "铜钟港", "钟楼炮官 赫莲"),
            Crew("noya_drowned_diver_lens", "crew_gray_gull_diver_noya", "灰鸥港", "潜水员 诺雅"),
            Rule("broken_anchor_sextant", "海图 · 完成 5 张不同普通海图" + ChartCompletion,
                (p, f, o) => CountDistinct(p.completedChartSeeds) >= 5),
            Rule("salt_crown_trade_weight", "海图 · 完成黄金潮汐商路" + ChartCompletion,
                (p, f, o) => Contains(p.completedRouteIds, GoldenRoute)),
            TreasureRank("twilight_tide_moon_shard", "gilded", "鎏金"),
            TreasureCount("empty_berth_registry", 3),
            TreasureCount("nameless_sailor_nameplate", 1),
            TreasureRank("storm_altar_fragment", "moonlit", "月蚀"),
            Rule("unfinished_seventh_berth_key", "藏宝图 · 完成残旧、鎏金、月蚀三种品质各一张" + TreasureCompletion,
                (p, f, o) => ContainsAll(p.completedTreasureRanks, new[] { "weathered", "gilded", "moonlit" })),
            TreasureCount("last_horizon_needle", 5),
            Rule("free_waters_mooring_ring", "收集 · 全存档拥有 20 件不同藏品（六港合计，不消耗）",
                (p, f, o) => CountKnownCollections(o) >= 20),
            TreasureCount("sea_god_debt_token", 8),
            Rule("inheritance_oath_inkstone", "收集 · 全存档拥有 30 件不同藏品（六港合计，不消耗）",
                (p, f, o) => CountKnownCollections(o) >= 30),
            Rule(FinalTreasure, "收集 · 集齐其余 39 件藏品（六港合计，不消耗）",
                (p, f, o) => OwnsEveryOtherCollection(o))
        });

        public static CoreCollectionRewardRule Find(string id)
        {
            foreach (CoreCollectionRewardRule rule in All)
                if (string.Equals(rule.Id, id, StringComparison.Ordinal)) return rule;
            return null;
        }

        public static bool IsKnownBoss(string id) => id == CoreGuidedTutorialContent.BossEventId ||
            id == CoreBattleBalance.TreasureBossId || id == "boss_" + ReefRoute || id == "boss_" + GoldenRoute ||
            id == "boss_" + MoonRoute || Contains(legendaryBossIds, id);

        private static CoreCollectionRewardRule Rule(string id, string source,
            Func<CoreCollectionProgress, CoreCollectionFacts, IReadOnlyCollection<string>, bool> condition) =>
            new CoreCollectionRewardRule(id, source, condition);

        private static CoreCollectionRewardRule Boss(string id, string bossId, string name) =>
            Rule(id, "首领 · 首次战胜" + name, (p, f, o) => Contains(p.defeatedBossIds, bossId));

        private static CoreCollectionRewardRule Crew(string id, string crewId, string port, string name) =>
            Rule(id, "招募 · 在" + port + "酒馆招募“" + name + "”", (p, f, o) => HasCrew(f.recruitedCrewIds, crewId));

        private static CoreCollectionRewardRule TreasureRank(string id, string rankId, string name) =>
            Rule(id, "藏宝图 · 完成 1 张" + name + "藏宝图" + TreasureCompletion,
                (p, f, o) => Contains(p.completedTreasureRanks, rankId));

        private static CoreCollectionRewardRule TreasureCount(string id, int count) =>
            Rule(id, "藏宝图 · 完成 " + count + " 张不同藏宝图" + TreasureCompletion,
                (p, f, o) => CountDistinct(p.completedTreasureSeeds) >= count);

        private static bool HasCrew(IEnumerable<string> ids, string target)
        {
            if (ids == null) return false;
            foreach (string id in ids)
                if (string.Equals(id, target, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool IsKnownPort(string port) => Contains(CoreVoyageSession.DestinationPorts, port);

        private static bool Contains(IEnumerable<string> values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target)) return false;
            foreach (string value in values)
                if (string.Equals(value, target, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool ContainsAll(IEnumerable<string> values, IEnumerable<string> required)
        {
            foreach (string value in required)
                if (!Contains(values, value)) return false;
            return true;
        }

        private static int CountDistinct(IEnumerable<string> values, Func<string, bool> valid = null)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (values != null)
                foreach (string value in values)
                    if (!string.IsNullOrWhiteSpace(value) && (valid == null || valid(value))) unique.Add(value);
            return unique.Count;
        }

        private static int CountKnownCollections(IEnumerable<string> ownedIds) =>
            CountDistinct(ownedIds, id => Find(id) != null);

        private static bool OwnsEveryOtherCollection(IEnumerable<string> ownedIds)
        {
            foreach (CoreCollectionRewardRule rule in All)
                if (rule.Id != FinalTreasure && !Contains(ownedIds, rule.Id)) return false;
            return true;
        }
    }
}
