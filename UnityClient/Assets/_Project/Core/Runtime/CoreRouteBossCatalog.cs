using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreRouteBoss
    {
        internal CoreRouteBoss(string id, string routeId, string name, string schoolId,
            string description, CoreBattleProfile profile)
        { Id = id; RouteId = routeId; Name = name; SchoolId = schoolId; Description = description; Profile = profile; }

        public string Id { get; }
        public string RouteId { get; }
        public string Name { get; }
        public string SchoolId { get; }
        public string Description { get; }
        public CoreBattleProfile Profile { get; }
        public IReadOnlyList<string> ComponentIds => CoreWarshipDesignCatalog.Components(SchoolId, false);
        public string ArtResourcePath => CoreWarshipDesignCatalog.ArtResourcePath(SchoolId, false);

        public CoreEncounter CreateEncounter()
        {
            var reward = new VoyageEffect { Gold = 180, Fame = 20 };
            return new CoreEncounter(Id, Name,
                "区域首领 · " + CoreWarshipDesignCatalog.SchoolName(SchoolId),
                Description + "\n本海图两艘首领舰每次出海出现一艘；击败本次首领后开放新港航道。舰装取自" +
                    CoreWarshipDesignCatalog.SchoolName(SchoolId) + "系史诗部件。\n" + CoreBattleBalance.BossVictoryBonusDescription +
                    "。夺船部件基准掉率：50% 基础掉落；独立 50% 获得本舰史诗部件 ×1，均受海图收益倍率影响。",
                true, false, 4, Profile.EnemyHull,
                new CoreEncounterChoice("挑战首领", CoreBattleRewardDescriptions.Victory(true, reward),
                    1, reward, startsBattle: true));
        }
    }

    public static class CoreRouteBossCatalog
    {
        // Keep the original three IDs: existing victory and collection records stay valid.
        private static readonly IReadOnlyList<CoreRouteBoss> all = Array.AsReadOnly(new[]
        {
            new CoreRouteBoss("boss_route_shattered_sail_reef", "route_shattered_sail_reef", "裂桅号", "fire",
                "裂桅号用夜海妖巨齿刺破礁浪，蓝焰风琴炮沿焦黑船舷依次点亮。裂开的主桅撑起裁星索帆，星印封舱板护住炮舱，鲸歌星讯仪从烟中捕捉敌船。",
                CoreBattleBalance.FirstRouteBoss),
            new CoreRouteBoss("boss_route_shattered_sail_reef_tide", "route_shattered_sail_reef", "幽礁潜猎号", "tide",
                "幽礁潜猎号藏身背风礁影，幽流逆帆在浪谷里无声张开。深渊鲸牙与幽礁甲壳贴近水线，礁星潮盘将海潮导向深星穿潮炮口。",
                new CoreBattleProfile(CoreBossKind.Route, CoreBattleArchetype.Tempest,
                    64, 9, 10, 5, 0, 9, 1.9f, 1.7f, .16d, boardingHealthMultiplier: 1.875m, boardingGuard: 6)),
            new CoreRouteBoss("boss_route_golden_tide_trade", "route_golden_tide_trade", "税金女王号", "blood",
                "税金女王号把商路欠契悬在雾幕隐翼帆之下。深流鲸牙与彗落重炮列由暗红铜管相连，雾铁甲壳包围着记录债名的幽图投影仪。",
                CoreBattleBalance.SecondRouteBoss),
            new CoreRouteBoss("boss_route_golden_tide_trade_thunder", "route_golden_tide_trade", "极光敕令号", "thunder",
                "极光敕令号奉帝国敕令封锁商路。黑礁碎舰槌压开浪脊，极光扇帆、极光叠层甲与极光航算机共同校准日炉齐射炮的雷光齐射。",
                new CoreBattleProfile(CoreBossKind.Route, CoreBattleArchetype.Artillery,
                    108, 15, 15, 7, 0, 16, 2.4f, 2.4f, .22d, boardingHealthMultiplier: 1.75m, boardingGuard: 6)),
            new CoreRouteBoss("boss_route_moon_tide_ruins", "route_moon_tide_ruins", "月蚀圣骸", "curse",
                "月蚀圣骸披着蚀月影帆巡守废墟，墓潮骨刺像一段露出海面的碑骨。虚钟曲射炮、蚀影链甲壳与蚀月天仪在同一声低鸣中苏醒。",
                CoreBattleBalance.ThirdRouteBoss),
            new CoreRouteBoss("boss_route_moon_tide_ruins_arcane", "route_moon_tide_ruins", "月织星槎号", "arcane",
                "月织星槎号沿着遗迹的隐秘星轨航行。虚空鳐翼首托起月织静风帆，月银覆板围住星火炮列，月潮星盘缓缓投下同心光环。",
                new CoreBattleProfile(CoreBossKind.Route, CoreBattleArchetype.Balanced,
                    164, 20, 19, 10, 0, 25, 3.1f, 3.0f, .30d, 120f,
                    boardingHealthMultiplier: CoreBattleBalance.LateBossBoardingHealthMultiplier, boardingGuard: 8)),
        });

        public static IReadOnlyList<CoreRouteBoss> All => all;

        public static CoreRouteBoss Find(string id)
        {
            foreach (CoreRouteBoss boss in all) if (boss.Id == id) return boss;
            return null;
        }

        public static IReadOnlyList<CoreRouteBoss> ForRoute(string routeId)
        {
            var result = new List<CoreRouteBoss>(2);
            foreach (CoreRouteBoss boss in all) if (boss.RouteId == routeId) result.Add(boss);
            return result.AsReadOnly();
        }

        public static CoreRouteBoss Choose(string routeId, uint selectionSeed)
        {
            IReadOnlyList<CoreRouteBoss> candidates = ForRoute(routeId);
            return candidates.Count == 0 ? null : candidates[(int)(selectionSeed % (uint)candidates.Count)];
        }
    }
}
