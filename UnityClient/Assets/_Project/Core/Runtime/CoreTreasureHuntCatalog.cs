using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreTreasureMapRank
    {
        internal CoreTreasureMapRank(string id, string name, int level, int power, int days, int clues,
            int points, float nm, float spacing, double ambush, double detection, double scale,
            int gold, int gems, int materials, int relics, int breachGold, int breachMaterials)
        {
            Id = id; Name = name; UnlockLevel = level; RecommendedPower = power; RecommendedVoyageDays = days;
            RequiredClues = clues; SidePointCount = points; NauticalMilesPerMapUnit = nm; MinimumEventSpacing = spacing;
            AmbushChance = ambush; AmbushDetection = detection; RewardScale = scale;
            FullGold = gold; FullGems = gems; FullAncientMaterials = materials; FullRelics = relics;
            BreachGold = breachGold; BreachAncientMaterials = breachMaterials;
        }
        public string Id { get; }
        public string Name { get; }
        public int UnlockLevel { get; }
        public int RecommendedPower { get; }
        public int RecommendedVoyageDays { get; }
        public int RequiredClues { get; }
        public int SidePointCount { get; }
        public float NauticalMilesPerMapUnit { get; }
        public float MinimumEventSpacing { get; }
        public double AmbushChance { get; }
        public double AmbushDetection { get; }
        public double RewardScale { get; }
        public int FullGold { get; }
        public int FullGems { get; }
        public int FullAncientMaterials { get; }
        public int FullRelics { get; }
        public int BreachGold { get; }
        public int BreachAncientMaterials { get; }
    }

    public sealed class CoreTreasureMapRecord
    {
        internal CoreTreasureMapRecord(string id, string seed, string rankId, int geometryVersion = 1, int contentVersion = 0)
        { Id = id; Seed = seed; RankId = rankId; MapGeometryVersion = geometryVersion; ContentVersion = contentVersion; }
        public string Id { get; }
        public string Seed { get; }
        public string RankId { get; }
        public int MapGeometryVersion { get; }
        public int ContentVersion { get; }
        public CoreTreasureMapRank Rank => CoreTreasureHuntCatalog.FindRank(RankId);
        public string Name => Rank.Name + "藏宝图";
    }

    /// <summary>A consumed map remains in the folio even after retreat or failure.</summary>
    public sealed class CoreUsedTreasureMapRecord
    {
        internal CoreUsedTreasureMapRecord(string id, string seed, string rankId, string departurePort,
            bool hasExplorationCoverage, float explorationCoverage, int geometryVersion = 0, int contentVersion = 0)
        {
            Id = id; Seed = seed; RankId = rankId ?? string.Empty; DeparturePort = departurePort ?? string.Empty;
            HasExplorationCoverage = hasExplorationCoverage; ExplorationCoverage = explorationCoverage;
            MapGeometryVersion = geometryVersion;
            ContentVersion = contentVersion;
        }
        public string Id { get; }
        public string Seed { get; }
        public string RankId { get; }
        public int MapGeometryVersion { get; }
        public int ContentVersion { get; }
        // Legacy seed-only saves did not record the map's rank or exploration.
        public CoreTreasureMapRank Rank => CoreTreasureHuntCatalog.FindRank(RankId);
        public string Name => Rank == null ? "藏宝图" : Rank.Name + "藏宝图";
        public string DeparturePort { get; }
        public bool HasExplorationCoverage { get; private set; }
        public float ExplorationCoverage { get; private set; }
        internal void RecordExploration(float coverage)
        {
            HasExplorationCoverage = true;
            ExplorationCoverage = coverage;
        }
    }

    /// <summary>Unity's treasure rules mirror treasure_map_config.csv; ordinary route bosses remain separate.</summary>
    public static class CoreTreasureHuntCatalog
    {
        public const int FragmentCost = 4;
        public const int MaxHeldMaps = 6;
        public const int TemplateCount = 18;
        public const string CoreEncounterId = "treasure_core";
        public const int MoonlitUnlockLevel = 5;
        public const int MoonlitPityCap = 6;
        public const double MoonlitBaseChance = .15d;
        public const double MoonlitPityStep = .15d;
        public const double MoonlitMaxChance = 1d;
        private static readonly IReadOnlyList<CoreTreasureMapRank> ranks = Array.AsReadOnly(new[] {
            new CoreTreasureMapRank("weathered", "残旧", 2, 90, 18, 2, 9, 24f, 9f, .008, .24, 1, 420, 1, 6, 0, 300, 4),
            new CoreTreasureMapRank("gilded", "鎏金", 3, 130, 22, 2, 10, 27f, 9f, .012, .20, 1.35, 570, 2, 8, 1, 405, 5),
            new CoreTreasureMapRank("moonlit", "月蚀", MoonlitUnlockLevel, 175, 26, 3, 11, 30f, 9.5f, .018, .16, 1.75, 735, 2, 10, 1, 525, 7),
        });
        public static IReadOnlyList<CoreTreasureMapRank> Ranks => ranks;
        public static CoreTreasureMapRank FindRank(string id)
        {
            foreach (CoreTreasureMapRank rank in ranks) if (rank.Id == id) return rank;
            return null;
        }

        public static IReadOnlyList<CoreEncounter> CreateEncounters(CoreTreasureMapRank rank)
        {
            if (rank == null) throw new ArgumentNullException(nameof(rank));
            int Scale(int value) => GameplayBalance.Round(value * rank.RewardScale);
            CoreEncounterChoice C(string label, string detail, double hours, VoyageEffect effect,
                string crew = "", bool battle = false, int clues = 0, string ship = "",
                CoreVoyageTimeBand time = CoreVoyageTimeBand.Any,
                CoreVoyageWeather weather = CoreVoyageWeather.Any,
                EventTerrainConstraint terrain = EventTerrainConstraint.Any,
                int minimumCurse = 0, int maximumCurse = int.MaxValue) =>
                new CoreEncounterChoice(label, battle ? CoreBattleRewardDescriptions.Victory(false, effect) : detail, hours, effect, crew, battle, clues, false,
                    ship, time, weather, terrain, minimumCurse, maximumCurse);
            CoreEncounter E(string id, string name, string category, bool hidden, bool mandatory,
                int risk, int power, params CoreEncounterChoice[] choices) =>
                new CoreEncounter(id, name, category, name + "藏着前往秘藏的航路线索。选择会消耗实际时间与全员口粮。",
                    mandatory, hidden, risk, power, choices);
            var events = new List<CoreEncounter> {
                E("treasure_clue_bottle", "盐封漂流瓶", "线索", false, false, 1, 0,
                    C("拆读盐封纸条", "线索 +1、声望 +5", 3, new VoyageEffect { TreasureClues = 1, Fame = 5 }),
                    C("航海士辨认潮记", "需要航海士。线索 +1、声望 +5", 1, new VoyageEffect { TreasureClues = 1, Fame = 5 }, "navigator"),
                    C("回收防水布", "布料 +" + Scale(8), 1, new VoyageEffect { Cloth = Scale(8) })),
                E("treasure_clue_whalebone", "鲸骨方位标", "线索", false, false, 2, 0,
                    C("拓印骨纹", "线索 +1、古代材料 +" + Scale(4), 4, new VoyageEffect { TreasureClues = 1, AncientMaterial = Scale(4) }),
                    C("潜水员辨位", "线索 +1、宝石 +1", 2, new VoyageEffect { TreasureClues = 1, Gem = 1 }, "diver"),
                    C("拆取配重", "铁矿 +" + Scale(10), 4, new VoyageEffect { Iron = Scale(10) })),
                E("treasure_clue_star", "倒悬星盘", "线索", false, false, 2, 0,
                    C("依星位校准", "线索 +1、士气 +1（仅限夜航）", 6, new VoyageEffect { TreasureClues = 1, Morale = 1 }, time: CoreVoyageTimeBand.Night),
                    C("航海士复原星盘", "线索 +1、古代罗盘 +1", 1, new VoyageEffect { TreasureClues = 1, AncientCompass = 1 }, "navigator"),
                    C("强转星轴", "线索 +1、船体 -5", 2, new VoyageEffect { TreasureClues = 1, Hull = -5 })),
                E("treasure_clue_fogbell", "雾钟灯塔", "线索", false, false, 2, 0,
                    C("记录雾钟节律", "线索 +1、声望 +6", 3, new VoyageEffect { TreasureClues = 1, Fame = 6 }),
                    C("航海士辨认灯语", "线索 +1、航海灯 +1", 1, new VoyageEffect { TreasureClues = 1, Lantern = 1 }, "navigator"),
                    C("搜集灯塔药品", "药品 +1", 2, new VoyageEffect { Medicine = 1 }),
                    C("留下航海灯接续灯语", "航海灯 -1、线索 +1、显示 1 个隐藏点", .5, new VoyageEffect { Lantern = -1, TreasureClues = 1, Reveal = 1 })),
                E("treasure_clue_twinmoon", "双月潮碑", "线索", false, false, 2, 0,
                    C("描下潮痕", "线索 +1、古代材料 +" + Scale(4) + "（仅限黄昏）", 5, new VoyageEffect { TreasureClues = 1, AncientMaterial = Scale(4) }, time: CoreVoyageTimeBand.Dusk),
                    C("航海士解读月位", "线索 +1、古代罗盘 +1", 2, new VoyageEffect { TreasureClues = 1, AncientCompass = 1 }, "navigator"),
                    C("撬开潮盘", "线索 +1、船体 -4", 2, new VoyageEffect { TreasureClues = 1, Hull = -4 })),
                E("treasure_false_cache", "赝品石匣", "宝藏", true, false, 3, 0,
                    C("开启石匣", "金币 +" + Scale(90) + "、诅咒 +1（每层战斗承伤 +10%，港口净化 30 金/层）；诅咒达到 10 层后不可继续叠加", 3, new VoyageEffect { Gold = Scale(90), Curse = 1 }, maximumCurse: 9),
                    C("拆走铁扣", "铁矿 +" + Scale(10) + "、火药 +2", 5, new VoyageEffect { Iron = Scale(10), Powder = 2 }),
                    C("潜水员辨真伪", "宝石 +1", 1, new VoyageEffect { Gem = 1 }, "diver")),
                E("treasure_reef_snare", "沉链礁阵", "险情", true, true, 3, 0,
                    C("架木脱困", "木材 -8、船体 -3", 2, new VoyageEffect { Wood = -8, Hull = -3 }, terrain: EventTerrainConstraint.Reef),
                    C("缓慢拖离", "船体 -10，无额外物资要求", 4, new VoyageEffect { Hull = -10 }),
                    C("木匠拆链", "船体 +4", 2, new VoyageEffect { Hull = 4 }, "carpenter"),
                    C("嵌入古材稳住船壳", "古代材料 -3、船体 +12；快速脱困", 1, new VoyageEffect { AncientMaterial = -3, Hull = 12 }, terrain: EventTerrainConstraint.Reef)),
                E("treasure_rival_sloop", "红帆寻宝队", "战斗", false, false, 3, GameplayBalance.Round(rank.RecommendedPower * .82),
                    C("迎战寻宝队", "胜利金币 +" + Scale(135) + "、声望 +12，另计海战奖励", 1, new VoyageEffect { Gold = Scale(135), Fame = 12 }, battle: true,
                        ship: "patrol|gunship|privateer"),
                    C("购买航路情报", "金币 -80、线索 +1", 2, new VoyageEffect { Gold = -80, TreasureClues = 1 }),
                    C("谨慎绕行", "不获取战利品", 3, new VoyageEffect()),
                    C("用宝石换取航路情报", "宝石 -1、线索 +1；免于交战", 1, new VoyageEffect { Gem = -1, TreasureClues = 1 })),
                E("treasure_drowned_shrine", "没潮神龛", "宝藏", true, false, 2, 0,
                    C("打捞供物", "金币 +" + Scale(105) + "、古代材料 +" + Scale(4), 6, new VoyageEffect { Gold = Scale(105), AncientMaterial = Scale(4) }),
                    C("献上啤酒", "啤酒 -1、士气 +2", 1, new VoyageEffect { Beer = -1, Morale = 2 }),
                    C("潜水员深入神龛", "黑珍珠 +1", 4, new VoyageEffect { BlackPearl = 1 }, "diver"),
                    C("归还遗物唤醒潮纹", "稀有遗物 -1、线索 +1、显示 2 个隐藏点", 1, new VoyageEffect { Relic = -1, TreasureClues = 1, Reveal = 2 })),
                E("treasure_salt_spring", "盐洞淡泉", "补给", false, false, 1, 0,
                    C("灌满水桶", "淡水 +" + Scale(70), 4, new VoyageEffect { Water = Scale(70) }),
                    C("寻找泉边食物", "淡水 +30、食物 +20", 3, new VoyageEffect { Water = 30, Food = 20 }),
                    C("厨师整备淡泉", "淡水 +50、食物 +30", 2, new VoyageEffect { Water = 50, Food = 30 }, "cook")),
                E("treasure_siren_conch", "歌潮海螺", "诅咒", true, false, 3, 0,
                    C("听完潮歌", "线索 +1、士气 -1", 3, new VoyageEffect { TreasureClues = 1, Morale = -1 }),
                    C("航海士分辨方位", "线索 +1", 1, new VoyageEffect { TreasureClues = 1 }, "navigator"),
                    C("以酒换取遗物", "啤酒 -1、古代材料 +" + Scale(4), 2, new VoyageEffect { Beer = -1, AncientMaterial = Scale(4) }),
                    C("雾中灯火镇潮", "雾海中消耗 1 层诅咒，线索 +1（需要至少 1 层诅咒）", 1,
                        new VoyageEffect { Curse = -1, TreasureClues = 1 }, weather: CoreVoyageWeather.Fog,
                        minimumCurse: 1)),
                E("treasure_ghost_fleet", "鬼灯船列", "战斗", false, false, 4, GameplayBalance.Round(rank.RecommendedPower * .90),
                    C("迎击鬼灯船", "胜利金币 +" + Scale(170) + "、古代罗盘 +1、声望 +15", 3, new VoyageEffect { Gold = Scale(170), AncientCompass = 1, Fame = 15 }, battle: true),
                    C("炮手压制鬼灯", "炮手先行射击；胜利金币 +" + Scale(170) + "、声望 +15", 2, new VoyageEffect { Gold = Scale(170), Fame = 15 }, "gunner", true),
                    C("熄灯潜行", "啤酒 -1、士气 +1", 4, new VoyageEffect { Beer = -1, Morale = 1 }),
                    C("以黑珍珠换取引渡", "黑珍珠 -1、线索 +1、声望 +12；免于交战", .5, new VoyageEffect { BlackPearl = -1, TreasureClues = 1, Fame = 12 })),
                E("treasure_smuggler_jetty", "弃置走私栈桥", "交易", true, false, 2, 0,
                    C("购买密封补给", "金币 -120、药品 +3、火药 +4", 2, new VoyageEffect { Gold = -120, Medicine = 3, Powder = 4 }, terrain: EventTerrainConstraint.Strait),
                    C("取走诅咒货箱", "诅咒 +1（每层战斗承伤 +10%，港口净化 30 金/层）、布料 +" + Scale(12) + "；诅咒达到 10 层后不可继续叠加", 4, new VoyageEffect { Curse = 1, Cloth = Scale(12) }, maximumCurse: 9),
                    C("收好账册离开", "不取走货物", 2, new VoyageEffect())),
                E("treasure_brine_geyser", "盐卤喷泉", "险情", true, true, 3, 0,
                    C("冲洗甲板", "淡水 -70", 1.5, new VoyageEffect { Water = -70 }),
                    C("掩住水桶绕行", "淡水 -25、船体 -8", 3, new VoyageEffect { Water = -25, Hull = -8 }),
                    C("木匠封住喷口", "淡水 -10", 2, new VoyageEffect { Water = -10 }, "carpenter")),
                E("treasure_coral_maze", "活珊瑚迷阵", "险情", true, true, 3, 0,
                    C("等待潮退慢行", "无额外物资要求，仍消耗时间口粮", 5, new VoyageEffect(), terrain: EventTerrainConstraint.Reef),
                    C("火药炸开通路", "火药 -2、船体 -4", 2, new VoyageEffect { Powder = -2, Hull = -4 }),
                    C("航海士引航", "缩短通过时间", 2, new VoyageEffect(), "navigator"),
                    C("拆用古罗盘勘测通路", "古代罗盘 -1、显示 2 个隐藏点；安全穿行", .5, new VoyageEffect { AncientCompass = -1, Reveal = 2 })),
                E("treasure_castaway_cartographer", "落难绘图师", "线索", false, false, 2, 0,
                    C("救援绘图师", "淡水 -30、食物 -30、线索 +1、声望 +10", 1, new VoyageEffect { Water = -30, Food = -30, TreasureClues = 1, Fame = 10 }),
                    C("购买残页", "金币 -60、线索 +1、声望 +6", 1, new VoyageEffect { Gold = -60, TreasureClues = 1, Fame = 6 }),
                    C("分享食物听取口述", "食物 -20、线索 +1、声望 +6", 6, new VoyageEffect { Food = -20, TreasureClues = 1, Fame = 6 })),
                E("treasure_pearl_crab", "珠蟹浅滩", "宝藏", true, false, 2, 0,
                    C("捕捞蟹群", "食物 +40、宝石 +1", 4, new VoyageEffect { Food = 40, Gem = 1 }),
                    C("用口粮诱离", "食物 -25、宝石 +2", 2, new VoyageEffect { Food = -25, Gem = 2 }),
                    C("潜水员掘蟹巢", "黑珍珠 +1、古代材料 +" + Scale(4), 6, new VoyageEffect { BlackPearl = 1, AncientMaterial = Scale(4) }, "diver")),
            };
            VoyageEffect Full(bool diver) => new VoyageEffect { Gold = rank.FullGold, Gem = rank.FullGems,
                AncientMaterial = rank.FullAncientMaterials, Relic = rank.FullRelics, BlackPearl = diver ? 1 : 0, TreasureComplete = true };
            string full = "金币 +" + rank.FullGold + "、宝石 +" + rank.FullGems + "、古代材料 +" + rank.FullAncientMaterials +
                (rank.FullRelics > 0 ? "、稀有遗物 +" + rank.FullRelics : "") + "；开启后按 95% 保护率撤出";
            events.Add(new CoreEncounter(CoreEncounterId, "秘藏核心", "秘藏", "藏宝图的叉号落在潮蚀石门上。收集方位线索后选择开启方式；撤离或失败不返还藏宝图。",
                true, true, 4, 0,
                C("按线索开启", "需要 " + rank.RequiredClues + " 条线索。" + full, 8, Full(false), clues: rank.RequiredClues),
                C("火药强拆", "需要 " + (rank.RequiredClues - 1) + " 条线索；火药 -3、船体 -12；金币 +" + rank.BreachGold +
                    "、宝石 +1、古代材料 +" + rank.BreachAncientMaterials + "；按 95% 保护率撤出", 5,
                    new VoyageEffect { Powder = -3, Hull = -12, Gold = rank.BreachGold, Gem = 1, AncientMaterial = rank.BreachAncientMaterials, TreasureComplete = true }, clues: rank.RequiredClues - 1),
                C("潜水员找暗门", "需要宝藏潜水员和 " + (rank.RequiredClues - 1) + " 条线索。" + full + "、黑珍珠 +1", 6, Full(true), "diver", clues: rank.RequiredClues - 1),
                C("标记位置，继续探索", "退回海域寻找线索，之后可点击叉号重访；如需放弃，可在航行界面返港，藏宝图不返还", 0, new VoyageEffect { PostponeTreasureCore = true })));
            return events.AsReadOnly();
        }
    }
}
