using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// Twelve encounter-specific scripts. The first chart averages three equivalent
    /// player energy for its unupgraded starter deck; later bosses average four.
    /// Authored values use the player's unupgraded boarding scale (8 strike / 4 guard /
    /// 6 healing for one energy), scaled to recommended player upgrades: first route
    /// +0, second route +1, late route and legendary +2. Never follows player upgrades.
    /// Resource setup, conditional payoffs and self-damage are part of each card's budget.
    /// Energy is a design reference, not an enemy energy pool or a damage multiplier.
    /// </summary>
    public static class CoreBoardingBossCatalog
    {
        private static readonly IReadOnlyDictionary<string, CoreBoardingEnemyDeck> decks = Build();

        public static CoreBoardingEnemyDeck ForEncounter(string encounterId) =>
            encounterId != null && decks.TryGetValue(encounterId, out CoreBoardingEnemyDeck deck) ? deck : null;

        private static IReadOnlyDictionary<string, CoreBoardingEnemyDeck> Build()
        {
            var result = new Dictionary<string, CoreBoardingEnemyDeck>(StringComparer.Ordinal);

            // 裂桅号：3 / 2 / 4。新手档：引火施压，包扎共燃，再转火焚尽。
            Add(result, "boss_route_shattered_sail_reef",
                T("引火登舷",
                    C("炉卫引火", 1, CoreBoardingEnemyMechanic.FireSelfBurn, resource: 2, guard: 4),
                    C("焰刃破阵", 2, attack: 20, burn: 1)),
                T("共燃整备",
                    C("共燃护卫", 1, CoreBoardingEnemyMechanic.FireSelfBurn, resource: 2, guard: 4),
                    C("甲板包扎", 1, heal: 6)),
                T("移焰决击",
                    C("移焰", 1, CoreBoardingEnemyMechanic.FireTransfer, resource: 4),
                    C("焚尽强攻", 2, CoreBoardingEnemyMechanic.FireConsume, resource: 4, attack: 18, bonus: 15),
                    C("火场救护", 1, heal: 6)));

            // 幽礁潜猎号：2 / 4 / 2 / 4。新手档：留盾诱攻与低压回合交替。
            Add(result, "boss_route_shattered_sail_reef_tide",
                T("潜礁设伏", C("礁壁伏阵", 1, CoreBoardingEnemyMechanic.TideGuard, guard: 4),
                    C("探舷刺击", 1, attack: 8)),
                T("破浪伏击", C("耗潮伏击", 2, CoreBoardingEnemyMechanic.TideStrike, resource: 8, attack: 20, bonus: 5),
                    C("留潮护卫", 1, CoreBoardingEnemyMechanic.TideGuard, guard: 4), C("潜猎包扎", 1, heal: 6)),
                T("退潮休整", C("退潮包扎", 1, heal: 6), C("礁影守势", 1, CoreBoardingEnemyMechanic.TideGuard, guard: 4)),
                T("幽礁围猎", C("巨浪追猎", 2, CoreBoardingEnemyMechanic.TideStrike, resource: 8, attack: 20, bonus: 5),
                    C("侧翼补刀", 1, attack: 8), C("礁壁合围", 1, CoreBoardingEnemyMechanic.TideGuard, guard: 4)));

            // 税金女王号：5 / 3 / 4。收税付血，较长恢复窗口，随后追债。
            Add(result, "boss_route_golden_tide_trade",
                T("赤契征税", C("征税血刃", 3, CoreBoardingEnemyMechanic.BloodStrike, resource: 6, attack: 18, selfDamage: 3, bonus: 5),
                    C("税卫列阵", 1, guard: 4), C("军医随队", 1, heal: 6)),
                T("封账休整", C("整队救治", 2, heal: 10), C("账房护卫", 1, guard: 4)),
                T("偿血追债", C("血契收偿", 2, CoreBoardingEnemyMechanic.BloodHarvest, resource: 6, attack: 8, drain: 4, bonus: 5),
                    C("追债刺击", 1, attack: 8), C("税卫掩护", 1, guard: 4)));

            // 极光敕令号：4 / 2 / 6。短连击试探，护卫整队，再完整连击。
            Add(result, "boss_route_golden_tide_trade_thunder",
                T("敕令试锋", C("敕令电刃", 1, CoreBoardingEnemyMechanic.ThunderStrike, attack: 5),
                    C("短链决击", 2, CoreBoardingEnemyMechanic.ThunderFinisher, resource: 1, attack: 10, bonus: 30),
                    C("敕卫掩护", 1, guard: 4)),
                T("列队换防", C("列队掩护", 1, guard: 4), C("列队包扎", 1, heal: 6)),
                T("极光齐击", C("极光先击", 1, CoreBoardingEnemyMechanic.ThunderStrike, attack: 5),
                    C("电弧追击", 1, CoreBoardingEnemyMechanic.ThunderStrike, attack: 5),
                    C("敕令终结", 2, CoreBoardingEnemyMechanic.ThunderFinisher, resource: 2, attack: 10, bonus: 30),
                    C("齐击护卫", 1, guard: 4), C("战地包扎", 1, heal: 6)));

            // 月蚀圣骸：3 / 3 / 6。第二轮无直接攻击，第三轮才引爆咒痕。
            Add(result, "boss_route_moon_tide_ruins",
                T("蚀魂刻印", C("蚀魂印刃", 2, CoreBoardingEnemyMechanic.CurseMark, resource: 2, attack: 10),
                    C("骨卫刺击", 1, attack: 8)),
                T("静默结阵", C("咒卫续印", 1, CoreBoardingEnemyMechanic.CurseMark, resource: 1, guard: 3),
                    C("圣骸修复", 2, heal: 10)),
                T("月蚀处决", C("处决刻痕", 1, CoreBoardingEnemyMechanic.CurseMark, resource: 1, guard: 3),
                    C("月蚀引爆", 3, CoreBoardingEnemyMechanic.CurseDetonate, resource: 4, attack: 8, bonus: 30),
                    C("骨刃追击", 1, attack: 8), C("祭司援护", 1, heal: 6)));

            // 月织星槎号：2 / 5 / 3 / 6。两次充能，交替攻击与恢复。
            Add(result, "boss_route_moon_tide_ruins_arcane",
                T("星帆蓄能", C("月环聚能", 1, CoreBoardingEnemyMechanic.ArcaneGain, resource: 2, guard: 2),
                    C("星槎救护", 1, heal: 6)),
                T("星轨合击", C("双星释放", 2, CoreBoardingEnemyMechanic.ArcaneSpend, resource: 2, attack: 14, bonus: 30),
                    C("星卫刺击", 1, attack: 8), C("星卫掩护", 1, guard: 4), C("月光救护", 1, heal: 6)),
                T("月环校准", C("满月蓄能", 2, CoreBoardingEnemyMechanic.ArcaneGain, resource: 3, guard: 5),
                    C("校准包扎", 1, heal: 6)),
                T("织星释放", C("织星决击", 3, CoreBoardingEnemyMechanic.ArcaneSpend, resource: 3, attack: 24, bonus: 30),
                    C("星环护阵", 2, guard: 8), C("星光援护", 1, heal: 6)));

            // 铁誓不沉号：5 / 2 / 3 / 6。重甲起手，轻整备续印后反攻。
            Add(result, "legendary_pirate_iron_oath",
                T("铁誓压阵", C("铁誓印刃", 2, CoreBoardingEnemyMechanic.CurseMark, resource: 2, attack: 10),
                    C("重甲盾墙", 2, guard: 9), C("铁卫突刺", 1, attack: 8)),
                T("轮替整备", C("咒卫换防", 1, CoreBoardingEnemyMechanic.CurseMark, resource: 1, guard: 3),
                    C("甲后救护", 1, heal: 6)),
                T("封航续印", C("封航印刃", 2, CoreBoardingEnemyMechanic.CurseMark, resource: 2, attack: 10),
                    C("铁卫包扎", 1, heal: 6)),
                T("铁壁反攻", C("反攻刻印", 1, CoreBoardingEnemyMechanic.CurseMark, resource: 1, guard: 3),
                    C("铁誓引爆", 3, CoreBoardingEnemyMechanic.CurseDetonate, resource: 4, attack: 8, bonus: 30),
                    C("反攻盾列", 1, guard: 4), C("战后救护", 1, heal: 6)));

            // 雷冠审判号：6 / 2 / 6 / 2。双峰连击，每次后面都有喘息。
            Add(result, "legendary_pirate_thunder_crown",
                T("雷冠齐击", C("审判先击", 1, CoreBoardingEnemyMechanic.ThunderStrike, attack: 7),
                    C("雷冠连击", 1, CoreBoardingEnemyMechanic.ThunderStrike, attack: 7),
                    C("审判终结", 2, CoreBoardingEnemyMechanic.ThunderFinisher, resource: 2, attack: 14, bonus: 30),
                    C("雷卫护阵", 1, guard: 4), C("随军救护", 1, heal: 6)),
                T("静电整备", C("静电盾列", 1, guard: 4), C("换班包扎", 1, heal: 6)),
                T("落雷裁决", C("裁决电刃", 1, CoreBoardingEnemyMechanic.ThunderStrike, attack: 8),
                    C("落雷追击", 1, CoreBoardingEnemyMechanic.ThunderStrike, attack: 8),
                    C("裁决终结", 2, CoreBoardingEnemyMechanic.ThunderFinisher, resource: 2, attack: 12, bonus: 30),
                    C("雷冠护卫", 1, guard: 4), C("战地救治", 1, heal: 6)),
                T("余电换防", C("水手牵制", 1, attack: 8), C("余电包扎", 1, heal: 6)));

            // 绯刃寡妇号：5 / 2 / 4 / 5。抢攻、恢复、吸血与决死追击。
            Add(result, "legendary_pirate_red_widow",
                T("绯刃抢滩", C("登船血刃", 2, CoreBoardingEnemyMechanic.BloodStrike, resource: 6, attack: 18, selfDamage: 2, bonus: 5),
                    C("钩索补刀", 1, attack: 8), C("绯卫掩护", 1, guard: 4), C("登船急救", 1, heal: 6)),
                T("红帆救护", C("整队救治", 2, heal: 10)),
                T("偿血围猎", C("绯刃收偿", 2, CoreBoardingEnemyMechanic.BloodHarvest, resource: 6, attack: 14, drain: 4, bonus: 5),
                    C("侧舷追刀", 1, attack: 8), C("猎队掩护", 1, guard: 4)),
                T("决死赤誓", C("赤誓决击", 3, CoreBoardingEnemyMechanic.BloodStrike, resource: 12, attack: 25, selfDamage: 3, bonus: 5),
                    C("赤誓盾列", 1, guard: 4), C("绯刃救护", 1, heal: 6)));

            // 余烬方舟号：4 / 2 / 6。单次大爆发由护盾和治疗分担预算。
            Add(result, "legendary_pirate_ember_ark",
                T("炉火强压", C("炉卫引火", 1, CoreBoardingEnemyMechanic.FireSelfBurn, resource: 2, guard: 4),
                    C("余烬焰刃", 3, attack: 26, burn: 1)),
                T("炉舱整备", C("共燃守势", 1, CoreBoardingEnemyMechanic.FireSelfBurn, resource: 2, guard: 4),
                    C("炉舱救护", 1, heal: 6)),
                T("方舟爆燃", C("炉火转移", 1, CoreBoardingEnemyMechanic.FireTransfer, resource: 4),
                    C("方舟焚尽", 3, CoreBoardingEnemyMechanic.FireConsume, resource: 4, attack: 24, bonus: 15),
                    C("灰烬盾列", 1, guard: 4), C("火场救治", 1, heal: 6)));

            // 潮汐镇魂号：4 / 3 / 5。持续留盾，专门治疗轮，耗潮反扑。
            Add(result, "legendary_pirate_tide_requiem",
                T("镇魂潮壁", C("镇魂留潮", 2, CoreBoardingEnemyMechanic.TideGuard, guard: 9),
                    C("潮卫刺击", 1, attack: 8), C("镇魂援护", 1, heal: 6)),
                T("回潮疗阵", C("回潮治疗", 2, CoreBoardingEnemyMechanic.TideHeal, resource: 6, heal: 12),
                    C("回潮留盾", 1, CoreBoardingEnemyMechanic.TideGuard, guard: 4)),
                T("镇魂反扑", C("镇魂浪刃", 3, CoreBoardingEnemyMechanic.TideStrike, resource: 12, attack: 24, bonus: 5),
                    C("浪后追击", 1, attack: 8), C("潮卫留盾", 1, CoreBoardingEnemyMechanic.TideGuard, guard: 4)));

            // 终界观星号：2 / 3 / 7。两轮预告后合击；峰值是3攻+2盾+2治。
            Add(result, "legendary_pirate_last_horizon",
                T("远星定位", C("远星聚能", 1, CoreBoardingEnemyMechanic.ArcaneGain, resource: 1, guard: 2),
                    C("观星援护", 1, heal: 6)),
                T("终界校准", C("星轨聚能", 2, CoreBoardingEnemyMechanic.ArcaneGain, resource: 2, guard: 5),
                    C("观测卫刺击", 1, attack: 8)),
                T("终界合击", C("终界释放", 3, CoreBoardingEnemyMechanic.ArcaneSpend, resource: 3, attack: 21, bonus: 30),
                    C("地平护阵", 2, guard: 9), C("星光救治", 2, heal: 10)));

            return result;
        }

        private static CoreBoardingEnemyTurn T(string name, params CoreBoardingEnemyCard[] cards) =>
            new CoreBoardingEnemyTurn(name, cards);

        // Temporary cards use a ten-point reference attack/guard. Add() converts
        // every percentage into the encounter's baselines at the fixed target rank.
        // Bonus uses the same reference (e.g. 30% = 3 base points per resource).
        private static CoreBoardingEnemyCard C(string name, int energy,
            CoreBoardingEnemyMechanic mechanic = CoreBoardingEnemyMechanic.Basic, int resource = 0,
            int attack = 0, int guard = 0, int heal = 0, int burn = 0, int drain = 0,
            int selfDamage = 0, int bonus = 0) =>
            new CoreBoardingEnemyCard(string.Empty, name, attack * 10, guard * 10, burn * 10,
                heal * 10, drain * 10, selfDamage * 10, mechanic: mechanic,
                resourceAmount: resource, bonusPercent: bonus, energy: energy);

        private static void Add(Dictionary<string, CoreBoardingEnemyDeck> result, string id,
            params CoreBoardingEnemyTurn[] authored)
        {
            CoreRouteBoss route = CoreRouteBossCatalog.Find(id);
            CoreLegendaryWarship legendary = CoreLegendaryWarships.Find(id);
            CoreBattleProfile profile = route?.Profile ?? legendary?.Profile;
            if (profile == null) throw new ArgumentException("Unknown boarding boss: " + id);
            int rank = legendary != null || route.RouteId == CoreBattleBalance.ThirdRouteId ? 2 :
                route.RouteId == "route_golden_tide_trade" ? 1 : 0;
            decimal reference = 10m * CoreBattleUpgradeBalance.MultiplierForLevel(rank);
            decimal attackScale = reference / Math.Max(1, profile.MeleeDamage);
            decimal guardScale = reference / Math.Max(1, profile.BoardingGuard);
            var turns = new CoreBoardingEnemyTurn[authored.Length];
            for (int t = 0; t < authored.Length; t++)
            {
                var cards = new CoreBoardingEnemyCard[authored[t].Cards.Count];
                for (int c = 0; c < cards.Length; c++)
                {
                    CoreBoardingEnemyCard card = authored[t].Cards[c];
                    int guardPercent = CoreBattleBalance.ScaleBattleValue(card.GuardPercent, guardScale);
                    cards[c] = new CoreBoardingEnemyCard("enemy_boarding_" + id + "_" + (t + 1) + "_" + (c + 1),
                        card.Name, CoreBattleBalance.ScaleBattleValue(card.AttackPercent, attackScale), guardPercent,
                        CoreBattleBalance.ScaleBattleValue(card.BurnPercent, attackScale),
                        CoreBattleBalance.ScaleBattleValue(card.HealPercent, attackScale),
                        CoreBattleBalance.ScaleBattleValue(card.DrainPercent, attackScale),
                        CoreBattleBalance.ScaleBattleValue(card.SelfDamagePercent, attackScale), card.PiercePercent,
                        card.Mechanic, card.ResourceAmount, CoreBattleBalance.ScaleBattleValue(card.BonusPercent, attackScale), card.Energy);
                }
                turns[t] = new CoreBoardingEnemyTurn(authored[t].Name, cards);
            }
            result.Add(id, new CoreBoardingEnemyDeck(route?.SchoolId ?? legendary.SchoolId, turns));
        }
    }
}
