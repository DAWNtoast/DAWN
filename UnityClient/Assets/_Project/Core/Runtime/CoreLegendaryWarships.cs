using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreLegendaryWarship
    {
        public CoreLegendaryWarship(string id, string name, string specialty, string description,
            string trophy, CoreBattleProfile profile, int gold, int fame, int iron, int wood, int powder,
            string formerLieutenantId = "", bool stillSearching = false,
            string formerLieutenantName = "", string formerLieutenantFate = "", string schoolId = "")
        {
            Id = id; Name = name; Specialty = specialty; Description = description; Trophy = trophy;
            Profile = profile; Gold = gold; Fame = fame; Iron = iron; Wood = wood; Powder = powder;
            FormerLieutenantId = formerLieutenantId ?? string.Empty;
            StillSearching = stillSearching;
            FormerLieutenantName = formerLieutenantName ?? string.Empty;
            FormerLieutenantFate = formerLieutenantFate ?? string.Empty;
            SchoolId = schoolId ?? string.Empty;
        }
        public string Id { get; }
        public string Name { get; }
        public string Specialty { get; }
        public string Description { get; }
        public string Trophy { get; }
        public CoreBattleProfile Profile { get; }
        public int Gold { get; }
        public int Fame { get; }
        public int Iron { get; }
        public int Wood { get; }
        public int Powder { get; }
        public string FormerLieutenantId { get; }
        public bool StillSearching { get; }
        public bool IsFormerCaptainCrew => !string.IsNullOrEmpty(FormerLieutenantId);
        public string FormerLieutenantName { get; }
        public string FormerLieutenantFate { get; }
        public string SchoolId { get; }
        public IReadOnlyList<string> ComponentIds => CoreWarshipDesignCatalog.Components(SchoolId, true);
        public string ArtResourcePath => CoreWarshipDesignCatalog.ArtResourcePath(SchoolId, true);

        public CoreEncounter CreateEncounter()
        {
            var reward = new VoyageEffect { Gold = Gold, Fame = Fame, Iron = Iron, Wood = Wood, Powder = Powder };
            string loot = CoreBattleRewardDescriptions.Victory(true, reward);
            string legacyNote = !IsFormerCaptainCrew ? string.Empty :
                "\n旧部身份：「" + FormerLieutenantName + "」——" + FormerLieutenantFate +
                (StillSearching ? "这是寻找老船长的继承者，可用切磋回应。" : "这是忘记初心的堕落者，击败它以夺回故事碎片。");
            string encounterType = !IsFormerCaptainCrew ? "传奇战舰" : StillSearching ? "继承者舰队 · 随机切磋" : "堕落旧部 · 随机传说舰队";
            return new CoreEncounter(Id, Name, encounterType + " · " + Specialty,
                Description + legacyNote + "\n舰装：" + CoreWarshipDesignCatalog.SchoolName(SchoolId) + "系传说部件。\n战法：" + Specialty + "。传说舰队会随航线种子在开放海域出现，可自由绕行。\n战利品「" + Trophy + "」折价计入金币。基准奖励：" + loot + "。" + CoreBattleBalance.BossVictoryBonusDescription + "。\n夺船部件基准掉率：50% 基础掉落；独立 50% 获得本舰传说部件 ×1，均受海图收益倍率影响。",
                false, false, 5, Profile.EnemyHull,
                new CoreEncounterChoice(!IsFormerCaptainCrew ? "挑战传奇战舰" : StillSearching ? "切磋传说舰队" : "挑战堕落旧部", loot, 0,
                    reward, startsBattle: true),
                new CoreEncounterChoice("保持距离，避开传奇", "安全绕行，不消耗补给时间", 0, new VoyageEffect()));
        }
    }

    public static class CoreLegendaryWarships
    {
        private static readonly IReadOnlyList<CoreLegendaryWarship> all = Array.AsReadOnly(new[]
        {
            new CoreLegendaryWarship("legendary_pirate_iron_oath", "铁誓不沉号", "铁壁轮替 · 重甲反攻",
                "帝国解散铁誓舰队时，老舰长把最后一道撤退令熔进了船壳。此后每一道炮痕都被新的装甲覆盖；船钟依旧点着早已失踪的水手的名字。",
                "铁誓装甲板", new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Ironclad,
                    244, 22, 21, 12, 8, 55, 4.3f, 5.0f, .40d, 120f,
                    boardingHealthMultiplier: 1.35m, boardingGuard: 14), 700, 55, 18, 0, 0, "iron_oath", false, "铁誓·达恩", "投靠帝国，以装甲和港税锁住航道。", "curse"),
            new CoreLegendaryWarship("legendary_pirate_thunder_crown", "雷冠审判号", "远程齐射 · 精密装填",
                "铜钟港的铸炮师曾为一场不可能的雷暴造出十二门同音炮。试航夜里，雷声与炮声再也没有分开；雷冠驶过的海面总比闪电先一步沸腾。",
                "雷冠炮闩", new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Artillery,
                    228, 28, 22, 9, 8, 52, 4.3f, 4.4f, .35d, 100f,
                    boardingHealthMultiplier: 1.375m, boardingGuard: 8), 780, 55, 8, 0, 16, "thunder_crown", false, "索尔", "把雷暴当神谕，审判不缴税的船。", "thunder"),
            new CoreLegendaryWarship("legendary_pirate_red_widow", "绯刃寡妇号", "逼近接战 · 舷侧突袭",
                "舰长曾从沉月王庭救出一船被献祭的孩子。如今他们已长成最默契的登船队；每当红帆从雾中斜切而来，绳钩便先于第一声战吼落下。",
                "绯刃钩索箱", new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Raider,
                    216, 22, 24, 10, 8, 66, 4.8f, 4.3f, .65d, 140f,
                    boardingHealthMultiplier: 1.15m, boardingGuard: 8), 760, 65, 6, 16, 0, "red_widow", false, "绯刃·玛拉", "把救援队变成收取保护费的登船队。", "blood"),
            new CoreLegendaryWarship("legendary_pirate_ember_ark", "余烬方舟号", "炽热火力 · 炮舱强压",
                "巫师与海妖交战的那一夜，火焰本应在海底熄灭。方舟却载着那团不肯死去的火返航，船员只在清晨换班，因为无人愿意看见炉门后的面孔。",
                "余烬密封火药", new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Ember,
                    236, 26, 23, 10, 8, 60, 4.4f, 4.5f, .45d, 120f,
                    boardingHealthMultiplier: 1.25m, boardingGuard: 10), 820, 60, 0, 8, 22, "ember_ark", false, "余烬·伊诺", "向火神换取永燃之火，烧毁了自由誓。", "fire"),
            new CoreLegendaryWarship("legendary_pirate_tide_requiem", "潮汐镇魂号", "风暴机动 · 全向压制",
                "一位航海士曾向海巨人借走月潮的航线，约好用自己的归途偿还。镇魂号从此没有母港；它每绕过一座岛，海图上的那座岛便像记起了一个死者。",
                "海巨人潮汐仪", new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Tempest,
                    236, 24, 20, 10, 8, 62, 4.6f, 4.6f, .55d, 130f,
                    boardingHealthMultiplier: 1.275m, boardingGuard: 16), 880, 70, 12, 10, 8, "tide_requiem", true, "潮汐·塞勒涅", "仍沿旧航路寻找老船长，接受非致命的切磋。", "tide"),
            new CoreLegendaryWarship("legendary_pirate_last_horizon", "终界观星号", "星轨校准 · 秘术合击",
                "终界观星号由寻找世界尽头的星图学者共同建造。它以终界航标首指引前路，以海上世界树皮护住观测舱；最后自由风帆、天涯终幕炮与终界地平镜每夜共同校准一条尚未存在的航路。",
                "终界星图透镜", new CoreBattleProfile(CoreBossKind.Legendary, CoreBattleArchetype.Balanced,
                    232, 26, 22, 10, 8, 58, 4.6f, 4.7f, .45d, 125f,
                    boardingHealthMultiplier: 1.30m, boardingGuard: 12), 860, 70, 10, 12, 10,
                schoolId: "arcane"),
        });

        public static IReadOnlyList<CoreLegendaryWarship> All => all;

        // Treasure chapters introduce the legendary fleet in three deliberate
        // steps. Keeping the mapping here makes the map generator, battle
        // resolver and presentation use the same stable encounter ids.
        public static IReadOnlyList<CoreEncounter> CreateTreasureEncounters(string rankId, bool includeSixthWarship = true)
        {
            string[] ids;
            switch (rankId)
            {
                case "weathered": ids = new[] { "legendary_pirate_iron_oath", "legendary_pirate_red_widow" }; break;
                case "gilded": ids = new[] { "legendary_pirate_thunder_crown", "legendary_pirate_ember_ark" }; break;
                case "moonlit": ids = includeSixthWarship
                    ? new[] { "legendary_pirate_tide_requiem", "legendary_pirate_last_horizon" }
                    : new[] { "legendary_pirate_tide_requiem" }; break;
                default: ids = Array.Empty<string>(); break;
            }
            var encounters = new List<CoreEncounter>(ids.Length);
            for (int index = 0; index < ids.Length; index++)
            {
                CoreLegendaryWarship warship = Find(ids[index]);
                if (warship != null) encounters.Add(warship.CreateEncounter());
            }
            return encounters.AsReadOnly();
        }

        public static CoreLegendaryWarship Find(string id)
        {
            for (int index = 0; index < all.Count; index++)
                if (all[index].Id == id) return all[index];
            return null;
        }

        public static CoreEncounter CreateTreasureEncounter()
        {
            var reward = new VoyageEffect { Gold = 480, Fame = 38, Iron = 12, Wood = 10 };
            return new CoreEncounter(CoreBattleBalance.TreasureBossId, "沉月王庭 · 守藏舰", "藏宝图首领",
                "三张月痕拓片拼出了王庭宝库的入口。守藏舰在空荡荡的港池中巡弋，舰艏的无眼石像始终朝向最接近宝藏的人。\n这是比月蚀圣骸更强的可选挑战，建议整副卡组完成两次强化后再来。胜利不会代替区域首领解锁航道。\n物资战利品已折为金币；夺船有 50% 基准概率获得基础部件战利品，受海图收益倍率影响。" + CoreBattleBalance.BossVictoryBonusDescription + "。",
                false, false, 5, CoreBattleBalance.ThirdTreasureBoss.EnemyHull,
                new CoreEncounterChoice("开启宝库，挑战守藏舰", CoreBattleRewardDescriptions.Victory(true, reward), 0,
                    reward, startsBattle: true),
                new CoreEncounterChoice("封存藏宝图，暂不挑战", "安全离开宝库入口", 0, new VoyageEffect()));
        }

        internal static void AddThirdChartEncounters(CoreVoyageMap map, bool includeSixthWarship = true)
        {
            if (map == null || map.Route == null || map.Route.Id != CoreBattleBalance.ThirdRouteId) return;
            AddNode(map, CreateTreasureEncounter(), EventTerrainConstraint.Fog);
            // Ordinary fixed-event placement owns the geography and separate seeded RNG.
            // Open-sea footprints introduce no islands or mandatory combat roadblocks.
            for (int index = 0; index < all.Count; index++)
            {
                if (!includeSixthWarship && all[index].Id == "legendary_pirate_last_horizon") continue;
                AddNode(map, all[index].CreateEncounter(), EventTerrainConstraint.OpenSea);
            }
        }

        private static void AddNode(CoreVoyageMap map, CoreEncounter encounter, EventTerrainConstraint terrain)
        {
            int nextId = 0;
            for (int index = 0; index < map.Nodes.Count; index++)
            {
                if (map.Nodes[index].Encounter.Id == encounter.Id) return;
                nextId = Math.Max(nextId, map.Nodes[index].Id + 1);
            }
            map.Nodes.Add(new CoreMapNode { Id = nextId, Encounter = encounter,
                IsBoss = false, TerrainConstraint = terrain });
        }
    }
}
