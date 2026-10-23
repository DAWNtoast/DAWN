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
                Description,
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
                "裂桅号迎着礁浪驶来，船首的夜海妖巨齿破开浪头，焦黑船舷上的蓝焰风琴炮依次燃起火光。裂开的主桅撑着裁星索帆，星印封舱板护住炮舱，鲸歌星讯仪隔着浓烟探寻敌船的踪迹。",
                CoreBattleBalance.FirstRouteBoss),
            new CoreRouteBoss("boss_route_shattered_sail_reef_tide", "route_shattered_sail_reef", "幽礁潜猎号", "tide",
                "幽礁潜猎号藏在背风的礁影里，幽流逆帆在浪谷中悄然张开。深渊鲸牙与幽礁甲壳贴着水面，礁星潮盘引来海潮，汇向深星穿潮炮的炮口。",
                new CoreBattleProfile(CoreBossKind.Route, CoreBattleArchetype.Tempest,
                    64, 9, 10, 5, 0, 9, 1.9f, 1.7f, .16d, boardingHealthMultiplier: 1.875m, boardingGuard: 6)),
            new CoreRouteBoss("boss_route_golden_tide_trade", "route_golden_tide_trade", "税金女王号", "blood",
                "税金女王号的雾幕隐翼帆下挂满了商路上的欠契。暗红色铜管连接着深流鲸牙与彗落重炮列，雾铁甲壳护住幽图投影仪，仪中映着一个个欠债者的名字。",
                CoreBattleBalance.SecondRouteBoss),
            new CoreRouteBoss("boss_route_golden_tide_trade_thunder", "route_golden_tide_trade", "极光敕令号", "thunder",
                "极光敕令号奉帝国敕令封锁商路。黑礁碎舰槌劈开浪头，极光扇帆、极光叠层甲与极光航算机一同为日炉齐射炮校准方位，炮口随即迸出雷光。",
                new CoreBattleProfile(CoreBossKind.Route, CoreBattleArchetype.Artillery,
                    108, 15, 15, 7, 0, 16, 2.4f, 2.4f, .22d, boardingHealthMultiplier: 1.75m, boardingGuard: 6)),
            new CoreRouteBoss("boss_route_moon_tide_ruins", "route_moon_tide_ruins", "月蚀圣骸", "curse",
                "月蚀圣骸披着蚀月影帆，在废墟间巡弋。墓潮骨刺探出海面，宛如一截白骨墓碑。一声低鸣响起，虚钟曲射炮、蚀影链甲壳与蚀月天仪一同苏醒。",
                CoreBattleBalance.ThirdRouteBoss),
            new CoreRouteBoss("boss_route_moon_tide_ruins_arcane", "route_moon_tide_ruins", "月织星槎号", "arcane",
                "月织星槎号沿着遗迹中隐秘的星轨航行。虚空鳐翼首上方，月织静风帆缓缓展开；星火炮列四周覆着月银覆板，月潮星盘投下一圈圈光环。",
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
