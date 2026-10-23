using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Adapted route events. Historical CSV effects are not executed at runtime.
    /// All choices below use implemented VoyageEffect fields and supported crew roles.</summary>
    public static partial class CoreVoyageEventCatalog
    {
        private const string Reef = "route_shattered_sail_reef";
        private const string Trade = "route_golden_tide_trade";
        private const string Moon = "route_moon_tide_ruins";
        private static readonly IReadOnlyList<string> reefRandom = Array.AsReadOnly(new[] {
            "event_reef_rotten_rations", "event_reef_open_water_ambush", "event_reef_sudden_squall",
            "event_reef_rigging_snap", "event_reef_freshwater_slick", "event_reef_shoal_fish_run",
            "event_reef_crew_fever", "event_reef_drifting_crate", "event_reef_favorable_current", "event_reef_night_fog" });
        private static readonly IReadOnlyList<string> tradeRandom = Array.AsReadOnly(new[] {
            "event_trade_food_spoilage", "event_trade_open_water_ambush", "event_trade_customs_patrol",
            "event_trade_cargo_shift", "event_trade_market_rumor", "event_trade_merchant_distress",
            "event_trade_seasonal_wind", "event_trade_deck_fire", "event_trade_floating_auction", "event_trade_stowaway" });
        private static readonly IReadOnlyList<string> moonRandom = Array.AsReadOnly(new[] {
            "event_moon_food_spoilage", "event_moon_open_water_ambush", "event_moon_moon_sleepwalker",
            "event_moon_ghost_current", "event_moon_falling_starlight", "event_moon_relic_whisper",
            "event_moon_silver_fog", "event_moon_dead_calm", "event_moon_spectral_debris", "event_moon_curse_surge" });

        public static IReadOnlyList<string> RandomEventIds(string routeId)
        {
            string[] additions = routeId == Reef ? ReefExpandedRandomIds : routeId == Trade ? TradeExpandedRandomIds :
                routeId == Moon ? MoonExpandedRandomIds : Array.Empty<string>();
            return Array.AsReadOnly(LegacyRandomEventIds(routeId).Concat(additions).ToArray());
        }

        // Version 1 suspended voyages must retain their exact twelve-event bag.
        internal static IReadOnlyList<string> LegacyRandomEventIds(string routeId)
        {
            switch (routeId)
            {
                case Reef: return reefRandom;
                case Trade: return tradeRandom;
                case Moon: return moonRandom;
                default: return Array.Empty<string>();
            }
        }

        public static bool IsRandomEventId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return ContainsId(reefRandom, id) || ContainsId(tradeRandom, id) || ContainsId(moonRandom, id) ||
                ContainsId(ReefExpandedRandomIds, id) || ContainsId(TradeExpandedRandomIds, id) || ContainsId(MoonExpandedRandomIds, id);
        }

        private static bool ContainsId(IReadOnlyList<string> pool, string id)
        {
            for (int i = 0; i < pool.Count; i++) if (pool[i] == id) return true;
            return false;
        }

        public static CoreEncounter CreateRandomEncounter(string id, int recommendedPower)
        {
            int power = Math.Max(1, recommendedPower);
            switch (id)
            {
                case "event_reef_rotten_rations":
                    return R(id, "食物变质", "雨水浸湿了最外层的粮袋，霉味正从舱板缝里飘出来。", true, 0,
                        C("丢弃坏粮并晒舱", 1, new VoyageEffect { Food = -4, Morale = 1 }),
                        C("封存可疑粮袋，减少供餐次数", 0, new VoyageEffect { Morale = -1 }),
                        C("厨师挑出可用干粮", .5, new VoyageEffect { Food = -2, Morale = 1 }, "cook"));
                case "event_reef_open_water_ambush":
                    return R(id, "航路伏击", "两艘吃水很浅的掠夺艇同时从礁石后转出，试图从两侧夹击船尾。", true, power,
                        C("急转贴礁脱身", 0, new VoyageEffect { Hull = -5 }),
                        C("迎击掠夺艇", 1, new VoyageEffect { Gold = 70 }, battle: true),
                        C("炮手用警告炮逼退敌艇", .5, new VoyageEffect { Powder = -1, Cannonballs = -1, Fame = 4 }, "gunner"));
                case "event_reef_sudden_squall":
                    return R(id, "突发礁雨", "一阵急雨遮住礁顶，拉紧的帆索发出尖锐的颤声。", true, 0,
                        C("收帆等待雨势减弱", 3, new VoyageEffect()),
                        C("顶雨驶离礁边", 0, new VoyageEffect { Hull = -3 }),
                        C("航海士借云隙观察前路", 1, new VoyageEffect { SurveyBoostHours = 10 }, "navigator"));
                case "event_reef_rigging_snap":
                    return R(id, "索具断裂", "主帆支索突然弹断，松脱的滑轮扫过半边甲板。", true, 0,
                        C("架设临时支索", 1.5, new VoyageEffect { Wood = -2 }),
                        C("切掉断索，承受帆架撞击", 0, new VoyageEffect { Hull = -3 }),
                        C("木匠重整帆架", 1, new VoyageEffect { Wood = -2, Hull = 3, SailingBoostHours = 8 }, "carpenter"));
                case "event_reef_freshwater_slick":
                    return R(id, "漂桶淡水", "几只蜡封尚未破损的水桶漂在浅色水带上，远处还散落着几个桶盖。", false, 0,
                        C("捞起身边的水桶", .5, new VoyageEffect { Water = 12 }),
                        C("沿水带继续打捞", 2, new VoyageEffect { Water = 26 }),
                        C("厨师过滤残桶中的淡水", 1, new VoyageEffect { Water = 22, Morale = 1 }, "cook"));
                case "event_reef_shoal_fish_run":
                    return R(id, "鱼群迁徙", "银背鱼群挤过船底，海鸟正沿着鱼群的外缘掠水。", false, 0,
                        C("撒网捕一会儿鱼", 1, new VoyageEffect { Food = 12 }),
                        C("跟着鱼群继续捕捞", 3, new VoyageEffect { Food = 28 }),
                        C("厨师挑选鱼获并腌制", 1.5, new VoyageEffect { Food = 22 }, "cook"));
                case "event_reef_crew_fever":
                    return R(id, "船员发热", "值夜的水手发起了高烧，同舱的人担心会传染给其他船员。", true, 0,
                        C("使用船上药品", 1, new VoyageEffect { Medicine = -1 }),
                        C("隔开铺位，众人分担值勤", 0, new VoyageEffect { Morale = -1 }),
                        C("医生诊治并安抚船员", .5, new VoyageEffect { Medicine = -1, Morale = 2 }, "doctor"));
                case "event_reef_drifting_crate":
                    return R(id, "漂流货箱", "一只铜钉货箱在浪间翻滚，木框尚好，封缝里夹着干燥的帆布。", false, 0,
                        C("捞起并拆下木框", 1, new VoyageEffect { Wood = 6 }),
                        C("打开货箱，取走帆布", 2, new VoyageEffect { Cloth = 6 }),
                        C("木匠完整回收箱架", 1, new VoyageEffect { Wood = 9, Iron = 2 }, "carpenter"));
                case "event_reef_favorable_current":
                    return R(id, "顺流窗口", "两股潮线短暂合拢，露出平日被白浪遮蔽的礁缝。", false, 0,
                        C("立即借流赶路", 0, new VoyageEffect { SailingBoostHours = 6 }),
                        C("加挂辅帆追上主流", .5, new VoyageEffect { Cloth = -1, SailingBoostHours = 16 }),
                        C("航海士沿主流测绘支路", 1, new VoyageEffect { SurveyBoostHours = 12, Reveal = 1 }, "navigator"));
                case "event_reef_night_fog":
                    return R(id, "礁雾迷航", "礁间的潮雾忽然变浓，船钟的回声却从意想不到的方向传来。", true, 0,
                        C("降帆鸣钟，等待辨清方向", 3, new VoyageEffect()),
                        C("循着浪声强行转向", 0, new VoyageEffect { Hull = -2 }),
                        C("航海士辨认雾间暗标", 1, new VoyageEffect { SurveyBoostHours = 8, RevealEncounterId = "event_reef_hidden_cache" }, "navigator"));
                case "event_trade_food_spoilage":
                    return R(id, "食物变质", "装货时混入的湿面粉已经结块，水手要求当着大家的面开仓查验。", true, 0,
                        C("销毁湿面粉，重新分餐", 1, new VoyageEffect { Food = -6, Morale = 1 }),
                        C("封存这批面粉，缩减配餐", 0, new VoyageEffect { Morale = -1 }),
                        C("厨师烘干可用面粉", 2, new VoyageEffect { Food = -2, Morale = 1 }, "cook"));
                case "event_trade_open_water_ambush":
                    return R(id, "私掠船截击", "一艘涂去船名的快船横在商路上，船上的人开口索要放行费。", true, power,
                        C("贴浪急转，摆脱拦截", 0, new VoyageEffect { Hull = -5 }),
                        C("护住货舱迎战", 1, new VoyageEffect { Gold = 85 }, battle: true),
                        C("航海士以旧航单换取放行", .5, new VoyageEffect { Gold = -18, Fame = 3 }, "navigator"));
                case "event_trade_customs_patrol":
                    return R(id, "海军临检", "巡逻艇打出停船旗，检查员正在逐船核验沿途的港口印章。", true, 0,
                        C("排队等候查验", 3, new VoyageEffect { Fame = 3 }),
                        C("缴纳快检费用", .5, new VoyageEffect { Gold = -15 }),
                        C("航海士整理航程凭据", 1, new VoyageEffect { Fame = 5 }, "navigator"),
                        C("接受训诫后立即离开", 0, new VoyageEffect { Morale = -1 }));
                case "event_trade_cargo_shift":
                    return R(id, "货舱移位", "一道横浪打来，舱底的货箱全滑向右舷，挤得门框吱呀作响。", true, 0,
                        C("停船重新绑扎", 2, new VoyageEffect()),
                        C("顶住倾斜继续航行", 0, new VoyageEffect { Hull = -3 }),
                        C("木匠加固隔舱", 1, new VoyageEffect { Wood = -2, Hull = 3 }, "carpenter"));
                case "event_trade_market_rumor":
                    return R(id, "市场传闻", "过路货船的船长用一张小纸条招揽生意，上面记着隐秘停泊点。", false, 0,
                        C("购买香料沉船的坐标", 1, new VoyageEffect { Gold = -12, RevealEncounterId = "gold_wreck", SurveyBoostHours = 6 }),
                        C("夸大消息转卖给水手", 2, new VoyageEffect { Gold = 24, Fame = -4 }),
                        C("航海士核对失主货单", 2, new VoyageEffect { GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }, "navigator"));
                case "event_trade_merchant_distress":
                    return R(id, "商船求援", "一艘受损的商船放出求救烟火，后方已能望见追兵的三角帆。", false, power,
                        C("加固伤船，换取互助凭证", 3, new VoyageEffect { Wood = -3, Fame = 8, GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }),
                        C("替商船迎击追兵", 1, new VoyageEffect { Gold = 80, Fame = 5, GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }, battle: true),
                        C("炮手掩护，随商队撤离", 1, new VoyageEffect { Powder = -1, Cannonballs = -1, Fame = 8, SailingBoostHours = 12 }, "gunner"));
                case "event_trade_seasonal_wind":
                    return R(id, "季风顺航", "季风将远处商队的旗帜吹得猎猎作响，各船正用信号旗互通沿途的消息。", false, 0,
                        C("加入商队借风赶路", 0, new VoyageEffect { SailingBoostHours = 10 }),
                        C("靠近领航船交换观测记录", 1.5, new VoyageEffect { SurveyBoostHours = 16 }),
                        C("航海士测出通往沉船的支航道", .5, new VoyageEffect { SurveyBoostHours = 8, RevealEncounterId = "gold_wreck" }, "navigator"));
                case "event_trade_deck_fire":
                    return R(id, "甲板失火", "带着火星的炉灰被风吹到油布边上，细细的火苗正沿货舱盖蔓延。", true, 0,
                        C("用淡水压住火线", .5, new VoyageEffect { Water = -10 }),
                        C("用湿粮袋闷住火苗", .5, new VoyageEffect { Food = -6 }),
                        C("木匠拆板隔断火线", 1, new VoyageEffect { Wood = -2, Hull = 2 }, "carpenter"),
                        C("拆下着火的甲板，抛入海中", 0, new VoyageEffect { Hull = -5 }));
                case "event_trade_floating_auction":
                    return R(id, "临时拍卖", "两艘货船在海上相遇，临时搭起了拍卖台。最后几批货交易完，两船就要各自起航。", false, 0,
                        C("买下折价布料", 1, new VoyageEffect { Gold = -28, Cloth = 8 }),
                        C("出售闲置木材", .5, new VoyageEffect { Wood = -4, Gold = 30 }),
                        C("木匠鉴别并修整箱料", 2, new VoyageEffect { Wood = 5, Iron = 2 }, "carpenter"));
                case "event_trade_stowaway":
                    return R(id, "偷渡客现身", "货箱后藏着一名没有船票的逃难者，船员们对如何安置他争执不下。", true, 0,
                        C("分享口粮，听他讲失船经过", 1, new VoyageEffect { Food = -4, Fame = 4, GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }),
                        C("安排他做杂务抵偿船费，平息争执", 0, new VoyageEffect { Morale = -1 }),
                        C("医生检查后分发药品", 1, new VoyageEffect { Medicine = -1, Fame = 10 }, "doctor"));
                case "event_moon_food_spoilage":
                    return R(id, "月潮霉变", "粮袋表面生出银灰色菌丝，船员不肯再碰那批散发甜味的干粮。", true, 0,
                        C("丢弃银霉粮袋", 1, new VoyageEffect { Food = -4, Morale = 1 }),
                        C("封存货格，安抚受惊船员", 0, new VoyageEffect { Morale = -1 }),
                        C("厨师用酒清洗储粮罐", 1.5, new VoyageEffect { Beer = -1, Morale = 2 }, "cook"));
                case "event_moon_open_water_ambush":
                    return R(id, "月影伏击", "一艘暗船从银雾中滑出，却听不见半点水声。船灯下映着两道重叠的船影。", true, power,
                        C("熄灯急转，撞开漂木脱身", 0, new VoyageEffect { Hull = -4, Morale = -1 }),
                        C("迎击月影船", 1, new VoyageEffect { Gem = 1 }, battle: true),
                        C("航海士辨认真船的浪纹", 2, new VoyageEffect { Reveal = 1, Fame = 5 }, "navigator"));
                case "event_moon_moon_sleepwalker":
                    return R(id, "月潮梦游", "一名梦游水手正在向船舷走去，嘴里反复念着陌生港口的名字。", true, 0,
                        C("将梦游者带回舱内看守", 0, new VoyageEffect { Morale = -1 }),
                        C("全员轮班守夜", 3, new VoyageEffect()),
                        C("医生安抚水手，化解潮咒", 1, new VoyageEffect { Curse = -1 }, "doctor"),
                        C("记录梦中星位，追索旧港", 2, new VoyageEffect { Curse = 1, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }));
                case "event_moon_ghost_current":
                    return R(id, "幽灵洋流", "水下的冷光逆着海流移动，罗盘指针跟着它不停摆动。", true, 0,
                        C("降帆等冷光散去", 3, new VoyageEffect()),
                        C("强行穿过冷流", 0, new VoyageEffect { Hull = -4 }),
                        C("航海士循着冷光借流前行", 1, new VoyageEffect { SailingBoostHours = 8, SurveyBoostHours = 6 }, "navigator"));
                case "event_moon_falling_starlight":
                    return R(id, "坠落星辉", "一束冷光落入前方海面，几片仍在发亮的碎屑浮到浪尖。", false, 0,
                        C("收集漂浮星砂", 2, new VoyageEffect { AncientMaterial = 1 }),
                        C("触碰发光核心", 1, new VoyageEffect { Gem = 1, Curse = 1 }),
                        C("潜水员封存沉落碎片", 2, new VoyageEffect { AncientMaterial = 2 }, "diver"));
                case "event_moon_relic_whisper":
                    return R(id, "遗物低语", "一枚缠着海草的旧饰物撞上船舷，其中传来断断续续的报港声。", false, 0,
                        C("听遗物讲述旧港星图", 2, new VoyageEffect { Curse = 1, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }),
                        C("以酒祭奠，放归大海", 1, new VoyageEffect { Beer = -1, Morale = 2 }),
                        C("航海士辨出回声里的航标", 2, new VoyageEffect { SurveyBoostHours = 16, Fame = 4 }, "navigator"));
                case "event_moon_silver_fog":
                    return R(id, "银雾迷航", "银雾中，船头和船尾的灯光竟像是从同一方向照来，熟悉的航标也全都不见了。", true, 0,
                        C("抛锚鸣钟，等雾变薄", 4, new VoyageEffect()),
                        C("命令水手循浪摸索撤离", 0, new VoyageEffect { Morale = -1 }),
                        C("航海士测定雾中月位", 1, new VoyageEffect { SurveyBoostHours = 12 }, "navigator"));
                case "event_moon_dead_calm":
                    return R(id, "月下无风", "船帆突然垂下，月影却仍在船侧缓慢移动。海面安静得令人不安。", true, 0,
                        C("等候风重新吹起", 4, new VoyageEffect()),
                        C("轮流划桨，强行离开静水", 0, new VoyageEffect { Morale = -1 }),
                        C("航海士沿月影找到潮线", 1, new VoyageEffect { SailingBoostHours = 12 }, "navigator"));
                case "event_moon_spectral_debris":
                    return R(id, "幽灵残骸", "一段古船残舱浮在雾里，锚链碰撞声比海浪还慢半拍。", false, 0,
                        C("拆取外层金属", 2, new VoyageEffect { Iron = 8 }),
                        C("取出仍在转动的观测机关", 3, new VoyageEffect { AncientMaterial = 1, Curse = 1, SurveyBoostHours = 16 }),
                        C("木匠截断锚链，回收舱板", 1.5, new VoyageEffect { Wood = -2, Iron = 10, Cloth = 3 }, "carpenter"));
                case "event_moon_curse_surge":
                    return R(id, "诅咒涌动", "舱门内忽然挤满了重重影子，水手们听见自己的名字在舱中回荡。", true, 0,
                        C("封闭舱门，压住恐慌", 0, new VoyageEffect { Morale = -1 }),
                        C("分发啤酒稳住人心", 1, new VoyageEffect { Beer = -1, Morale = 1 }),
                        C("厨师准备醒神餐", 2, new VoyageEffect { Food = -6, Curse = -1 }, "cook"));
                default: return CreateReefExpandedRandom(id, power) ?? CreateTradeExpandedRandom(id, power) ?? CreateMoonExpandedRandom(id, power);
            }
        }

        public static IReadOnlyList<CoreEncounter> CreateFixedEncounters(string routeId)
        {
            switch (routeId)
            {
                case Reef: return Array.AsReadOnly(new[] {
                    F("event_reef_old_cannon", "废弃岸炮", "打捞", "废弃炮位露出半截炮管，附近还埋着未完全锈坏的弹药箱。", false, false,
                        C("拆下炮架上的金属零件", 3, new VoyageEffect { Iron = 8 }),
                        C("修复炮台上的测距镜", 2, new VoyageEffect { Iron = -2, SurveyBoostHours = 18 }),
                        C("炮手辨认可用弹药", 1.5, new VoyageEffect { Cannonballs = 4, Powder = 3 }, "gunner")),
                    F("event_reef_tidepool_pearls", "潮池珍珠", "宝藏", "退潮后的暗池中闪着珠光，深处的石缝只能容一人侧身进入。", true, false,
                        C("采集深池珍珠", 4, new VoyageEffect { Gem = 1, Hull = -2 }),
                        C("在浅水处采珠，卖给过路渔船", 2, new VoyageEffect { Gold = 35 }),
                        C("潜水员进入石缝采珠", 2, new VoyageEffect { Gem = 1 }, "diver")),
                    F("event_reef_lantern", "礁海灯塔", "航标", "孤零零的灯塔早已熄灭，守灯人把旧航标图留在了门后。", false, false,
                        C("航海士点亮旧灯", 1, new VoyageEffect { Beer = -1, Reveal = 1, SurveyBoostHours = 16 }, "navigator"),
                        C("取走备用航海灯", 3, new VoyageEffect { Lantern = 1 }),
                        C("修整灯室，查阅潮工图纸", 2, new VoyageEffect { Wood = -2, GrantDiscovery = "reef_dock_chart", RevealEncounterId = "event_reef_hidden_cache" })),
                    F("event_reef_drifting_sail", "漂流船帆", "打捞", "一张完好的船帆缠在漂木上，帆上的纹样十分鲜明，隔着老远就能认出来。", false, false,
                        C("回收完整帆布", 2, new VoyageEffect { Cloth = 8 }),
                        C("木匠裁帆改装顺风翼", 2, new VoyageEffect { Cloth = -2, SailingBoostHours = 20 }, "carpenter"),
                        C("与附近商船交换", 1, new VoyageEffect { Beer = -1, Gold = 35 })),
                    F("event_reef_powder_wreck", "火药舱残骸", "打捞", "礁缝里的船舱还留着密封火药桶，水面下的木壳随时可能塌落。", true, false,
                        C("从裂口取出火药桶", 2, new VoyageEffect { Powder = 4, Hull = -3 }),
                        C("用木架稳住残舱", 3, new VoyageEffect { Wood = -3, Powder = 6 }),
                        C("炮手寻找干燥弹药柜", 2, new VoyageEffect { Powder = 5, Cannonballs = 3 }, "gunner")),
                    F("event_reef_tidewright_platform", "潮工浮台", "交易", "潮工在开阔水面的浮台上修船，铁屑沿着甲板缝不断落进海里。", false, false,
                        C("付钱请潮工修船", 2, new VoyageEffect { Gold = -28, Hull = 8 }, renewable: true),
                        C("出售废铁", 1, new VoyageEffect { Iron = -5, Gold = 32 }, renewable: true),
                        C("木匠与潮工合力加固船壳", 2, new VoyageEffect { Wood = -3, Hull = 10 }, "carpenter", renewable: true)),
                    F("event_reef_shallow_whirlpool", "浅海漩涡", "险情", "浅海里卷起旋流，漂木随着水流打转，不时擦过船侧。", true, true,
                        C("抛锚等待水带散开", 3, new VoyageEffect()),
                        C("撞开漂木驶离旋流", 0, new VoyageEffect { Hull = -5 }),
                        C("航海士沿外侧水带加速", 1, new VoyageEffect { SailingBoostHours = 10 }, "navigator")),
                    F("event_reef_hidden_repair_dock", "隐藏修船坞", "修缮", "礁壁背后藏着潮工旧船坞，手摇吊架仍能托起轻船的船底。", true, false,
                        C("利用旧吊架修补船壳", 4, new VoyageEffect { Wood = -2, Hull = 10 }, renewable: true),
                        C("拆取废弃零件", 3, new VoyageEffect { Iron = 10 }),
                        C("木匠修整船底，减小阻力", 2, new VoyageEffect { Wood = -3, Hull = 8, SailingBoostHours = 16 }, "carpenter", renewable: true)),
                });
                case Trade: return Array.AsReadOnly(new[] {
                    F("event_trade_spice_auction", "香料拍卖", "交易", "香料商船的船主把一枚宝石摆上拍卖台，想卖掉它，凑齐继续远航的钱。", false, false,
                        C("竞拍抵押宝石", 1, new VoyageEffect { Gold = -80, Gem = 1 }, renewable: true),
                        C("帮拍卖商搬运货箱", 3, new VoyageEffect { Gold = 28 }),
                        C("厨师鉴定香料，收取酬劳", 1.5, new VoyageEffect { Gold = 32, Food = 6 }, "cook")),
                    F("event_trade_quarantine_flag", "检疫黄旗", "检疫", "狭窄的航道上升起黄旗，检疫艇拦下过往船只，要查验粮舱，检查船上的病患。", false, true,
                        C("停船完成检查", 3, new VoyageEffect { Fame = 3 }),
                        C("取消靠岸，安抚不满的水手", 0, new VoyageEffect { Morale = -1 }),
                        C("医生协助查验", 1, new VoyageEffect { Medicine = -1, Fame = 8 }, "doctor")),
                    F("event_trade_wedding_fleet", "商船婚礼", "庆典", "挂满彩旗的两艘商船并舷停泊，船长邀请过路人一同见证婚礼。", false, false,
                        C("送上贺礼", 2, new VoyageEffect { Gold = -20, Morale = 2 }),
                        C("请领航员喝酒，打听潮路", 1, new VoyageEffect { Beer = -1, SailingBoostHours = 16 }),
                        C("厨师承办宴席", 3, new VoyageEffect { Food = -10, Gold = 45, Morale = 1 }, "cook")),
                    F("event_trade_gilded_lighthouse", "镀金灯塔", "航标", "塔顶的铜镜把阳光投向支航道，收费员正兜售附近隐蔽泊位的记录。", false, false,
                        C("购买沉船坐标和观测记录", 1, new VoyageEffect { Gold = -20, RevealEncounterId = "gold_wreck", SurveyBoostHours = 12 }, renewable: true),
                        C("偷偷抄录航标，损害名声", 3, new VoyageEffect { Reveal = 1, Fame = -3 }),
                        C("航海士帮忙校准铜镜", 1, new VoyageEffect { Reveal = 2, Fame = 4 }, "navigator")),
                    F("event_trade_debt_collector", "债务小艇", "交涉", "追债人把一艘渔艇堵在航路上，渔户向你展示已经付过钱的收据。", false, false,
                        C("替渔户补款，获互助凭证", 1, new VoyageEffect { Gold = -25, Fame = 6, GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }),
                        C("威吓追债人索取钱袋", 2, new VoyageEffect { Gold = 35, Fame = -8 }),
                        C("航海士核对港口印章上的日期", 2, new VoyageEffect { Fame = 6, Gold = 12 }, "navigator")),
                    F("event_trade_medicine_convoy", "药材船队", "援助", "药材船队的一艘小船掉了队，断裂的拖缆还浮在船尾。", false, false,
                        C("修好拖缆，获互助凭证", 3, new VoyageEffect { Wood = -3, Medicine = 1, GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }),
                        C("完成护送，请领航船带路", 3, new VoyageEffect { Fame = 4, SailingBoostHours = 18 }),
                        C("炮手开炮驱离窥伺的小艇", 1, new VoyageEffect { Powder = -1, Cannonballs = -1, Gold = 35 }, "gunner")),
                    F("event_trade_insurance_wreck", "沉箱保险船", "打捞", "保险船的货舱陷在礁边，打捞登记员仍在守着漂浮的赔付箱。", true, false,
                        C("登记后协助打捞", 3, new VoyageEffect { Gold = 45, Fame = 4 }),
                        C("私取水下货包", 2, new VoyageEffect { Cloth = 9, Fame = -5 }),
                        C("木匠稳住货舱", 2, new VoyageEffect { Wood = -3, Gold = 55, Fame = 3 }, "carpenter"),
                        C("凭互助印章领取保全货", 2, new VoyageEffect { Gold = -15, Gem = 1, Cloth = 4 }, discovery: "trade_rescue_seal")),
                    F("event_trade_sea_theatre", "海上剧团", "演出", "两岛之间的背风水道里传来鼓声，剧团把几艘船并在一起，搭起了一座小舞台。", true, false,
                        C("带酒观看演出", 2, new VoyageEffect { Beer = -1, Morale = 2 }),
                        C("为剧团提供口粮", 1, new VoyageEffect { Food = -6, Fame = 8 }),
                        C("厨师为演出准备餐食", 3, new VoyageEffect { Food = -8, Gold = 40 }, "cook")),
                });
                case Moon: return Array.AsReadOnly(new[] {
                    F("event_moon_gate", "月潮石门", "遗迹", "潮水穿过双柱石门，门内的石壁上刻着通往失落宝库的路线，刻痕已有些残缺。", false, false,
                        C("深入石门拓印路线", 3, new VoyageEffect { MapFragments = 1, Curse = 1 }),
                        C("拼读门外的月相星图", 3, new VoyageEffect { GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }),
                        C("航海士校准潮位刻度", 1, new VoyageEffect { Beer = -1, Reveal = 2 }, "navigator")),
                    F("event_moon_star_compass", "星盘残片", "宝藏", "覆满贝壳的残盘藏在暗礁背后，盘上剩下的一根指针仍指向水下石室。", true, false,
                        C("拼回星盘针轴", 3, new VoyageEffect { AncientCompass = 1 }),
                        C("将残片卖给遗物商船", 1.5, new VoyageEffect { Gold = 75 }),
                        C("潜水员调校水下观测轴", 2, new VoyageEffect { SurveyBoostHours = 18, AncientMaterial = 1 }, "diver")),
                    F("event_moon_drowned_library", "沉没书库", "遗迹", "塌陷的石阶通往一间半淹在水里的藏书室，密封书匣压在断裂的梁柱下面。", true, false,
                        C("牵缆拖出深处书匣", 4, new VoyageEffect { Relic = 1, Hull = -5 }),
                        C("记录浅层碑文", 2, new VoyageEffect { Fame = 10 }),
                        C("医生照看潜水员，协助取回书匣", 3, new VoyageEffect { Medicine = -1, Relic = 1 }, "doctor"),
                        C("依星图开启侧门书室", 1, new VoyageEffect { AncientMaterial = -1, Relic = 1, SurveyBoostHours = 8 }, discovery: "moon_star_chart")),
                    F("event_moon_healing_spring", "月光泉", "休整", "浅滩上的泉眼映着淡蓝微光，干净的水沿石槽流向海面。", false, false,
                        C("让船员休息并汲水", 3, new VoyageEffect { Water = 20, Morale = 1 }),
                        C("医生收集药用泉水", 2, new VoyageEffect { Medicine = 2 }, "doctor"),
                        C("借泉声平复潮咒", 4, new VoyageEffect { Curse = -1 })),
                    F("event_moon_lunar_observatory", "月相观测台", "遗迹", "一座浮台靠古锚固定在开阔海面上，铜制观测盘正随着月亮的位置缓缓转动。", false, false,
                        C("拓印星图，寻找月痕方碑", 3, new VoyageEffect { GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk", SurveyBoostHours = 6 }),
                        C("拆取已松动的机关", 3, new VoyageEffect { AncientMaterial = 2 }),
                        C("航海士修复远望铜镜", 1, new VoyageEffect { Iron = -2, SurveyBoostHours = 20 }, "navigator")),
                    F("event_moon_relic_diver", "遗物潜客", "交易", "潜客的小船上整齐地挂着升降绳。他想用手里的残图，换些下次潜水需要的物资。", false, false,
                        C("购买月相星图与方碑坐标", 1, new VoyageEffect { Gold = -35, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }, renewable: true),
                        C("用修缮潜钟的材料换取古代罗盘", 2, new VoyageEffect { Iron = -6, Wood = -3, AncientCompass = 1 }),
                        C("潜水员与潜客一同核对残图", 3, new VoyageEffect { MapFragments = 1, Fame = 6 }, "diver")),
                    F("event_moon_mirror_pool", "月镜潮池", "奇遇", "薄雾下的潮池映出一座并不存在的港口，倒影里的航标格外清晰。", true, false,
                        C("凝视倒影中的完整星图", 1, new VoyageEffect { Curse = 1, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }),
                        C("取走石槽中的药用潮水", 2, new VoyageEffect { Medicine = 1 }),
                        C("航海士追踪真实灯影", 2, new VoyageEffect { SurveyBoostHours = 16 }, "navigator")),
                    F("event_moon_chain_bridge", "古链浮桥", "险情", "古桥上断开的铁链正从潮水中升起，粗重的链节横在船头。", true, true,
                        C("抛锚等待链桥沉下", 3, new VoyageEffect()),
                        C("顶着铁链强行转向驶离", 0, new VoyageEffect { Hull = -6 }),
                        C("航海士借潮窗驶离", 1, new VoyageEffect { SailingBoostHours = 8 }, "navigator")),
                });
                default: return Array.Empty<CoreEncounter>();
            }
        }

        public static bool TryGetFixedTerrain(string id, out EventTerrainConstraint terrain)
        {
            if (TryReefExpandedTerrain(id, out terrain) || TryTradeExpandedTerrain(id, out terrain) ||
                TryMoonExpandedTerrain(id, out terrain)) return true;
            switch (id)
            {
                case "event_reef_drifting_sail": case "event_reef_tidewright_platform":
                case "event_trade_spice_auction": case "event_trade_wedding_fleet":
                case "event_trade_debt_collector": case "event_trade_medicine_convoy":
                case "event_moon_lunar_observatory": case "event_moon_relic_diver":
                    terrain = EventTerrainConstraint.OpenSea; return true;
                case "event_reef_old_cannon": case "event_reef_lantern":
                case "event_trade_gilded_lighthouse": case "event_moon_drowned_library":
                case "event_moon_healing_spring":
                    terrain = EventTerrainConstraint.Beach; return true;
                case "event_reef_tidepool_pearls": case "event_reef_powder_wreck":
                case "event_reef_shallow_whirlpool": case "event_trade_insurance_wreck":
                case "event_moon_star_compass":
                    terrain = EventTerrainConstraint.Reef; return true;
                case "event_reef_hidden_repair_dock": case "event_trade_quarantine_flag":
                case "event_trade_sea_theatre": case "event_moon_gate": case "event_moon_chain_bridge":
                    terrain = EventTerrainConstraint.Strait; return true;
                case "event_moon_mirror_pool": terrain = EventTerrainConstraint.Fog; return true;
                default: terrain = EventTerrainConstraint.Any; return false;
            }
        }

        internal static bool TryClassifyDiscovery(string id, out EncounterDiscoveryKind kind)
        {
            if (IsRandomEventId(id)) { kind = EncounterDiscoveryKind.TransientEncounter; return true; }
            if (TryExpandedDiscovery(id, out kind)) return true;
            switch (id)
            {
                case "event_reef_tidepool_pearls": case "event_reef_powder_wreck":
                case "event_reef_shallow_whirlpool": case "event_reef_hidden_repair_dock":
                case "event_trade_insurance_wreck": case "event_trade_sea_theatre":
                case "event_moon_star_compass": case "event_moon_drowned_library":
                case "event_moon_mirror_pool": case "event_moon_chain_bridge":
                    kind = EncounterDiscoveryKind.HiddenLocation; return true;
                default:
                    kind = EncounterDiscoveryKind.SightedLocation;
                    return TryGetFixedTerrain(id, out _);
            }
        }

        private static CoreEncounter R(string id, string name, string description, bool mandatory, int power,
            params CoreEncounterChoice[] choices) => new CoreEncounter(id, name, RandomEventCategory(id), description,
                mandatory, false, power > 0 ? 3 : mandatory ? 2 : 1, power, choices)
                { RepeatPolicy = CoreEncounterRepeatPolicy.RandomVoyage };

        private static CoreEncounter F(string id, string name, string category, string description, bool hidden,
            bool mandatory, params CoreEncounterChoice[] choices) =>
            new CoreEncounter(id, name, category, description, mandatory, hidden, mandatory ? 2 : 1, 0, choices);

        // Describe effects from the same data used by the resolver.
        private static CoreEncounterChoice C(string label, double hours, VoyageEffect effect, string crew = "",
            bool battle = false, bool renewable = false, string discovery = "")
        {
            var details = new List<string>();
            void Add(string name, int value) { if (value != 0) details.Add(name + (value > 0 ? " +" : " ") + value); }
            Add("船体", effect.Hull); Add("淡水", effect.Water); Add("食物", effect.Food);
            Add("啤酒", effect.Beer); Add("药品", effect.Medicine); Add("木材", effect.Wood);
            Add("铁矿", effect.Iron); Add("布料", effect.Cloth); Add("火药", effect.Powder);
            Add("炮弹", effect.Cannonballs); Add("金币", effect.Gold); Add("声望", effect.Fame);
            Add("士气", effect.Morale); Add("显示隐藏点", effect.Reveal); Add("藏宝图碎片", effect.MapFragments);
            Add("宝石", effect.Gem); Add("古代材料", effect.AncientMaterial); Add("稀有遗物", effect.Relic);
            Add("古代罗盘", effect.AncientCompass); Add("航海灯", effect.Lantern);
            if (effect.Curse < 0) details.Add("清除至多 " + -(long)effect.Curse + " 层诅咒");
            else Add("诅咒", effect.Curse);
            var exploration = new List<string>();
            if (effect.SailingBoostHours > 0) exploration.Add("航速 +25% / " + effect.SailingBoostHours.ToString("0.#") + " 小时");
            if (effect.SurveyBoostHours > 0) exploration.Add("瞭望范围 +35% / " + effect.SurveyBoostHours.ToString("0.#") + " 小时");
            if (!string.IsNullOrEmpty(effect.GrantDiscovery)) exploration.Add("取得" + DiscoveryName(effect.GrantDiscovery) + "（本航次）");
            if (!string.IsNullOrEmpty(effect.RevealEncounterId)) exploration.Add("标出「" + DiscoveryTargetName(effect.RevealEncounterId) + "」");
            details.AddRange(exploration);
            string detail = details.Count == 0 ? "无额外资源变动；补给按实际耗时结算" : string.Join("、", details);
            // Battle cargo is converted to cash before loading. The authored
            // detail must quote the same rounded payout as the live dossier.
            if (battle) detail = CoreBattleRewardDescriptions.Victory(false, effect) +
                CoreBattleRewardDescriptions.AdditionalEffects(effect) +
                (exploration.Count == 0 ? string.Empty : "；胜利后：" + string.Join("、", exploration));
            if (effect.Curse > 0) detail += "（每层使战斗中受到的伤害增加 10%，可在港口净化；最多 10 层）";
            return new CoreEncounterChoice(label, detail, hours, effect, crew, battle, renewable: renewable,
                minimumCurse: effect.Curse < 0 ? 1 : 0, maximumCurse: effect.Curse > 0 ? 9 : int.MaxValue,
                requiredDiscovery: discovery);
        }
    }
}
