using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public static partial class CoreVoyageEventCatalog
    {
        // Added to the current event pool only; historical chart versions retain their authored pool.
        internal static readonly string[] ReefExpandedRandomIds = {
            "event_reef_v2_message_gull", "event_reef_v2_entangled_calf", "event_reef_v2_backspin_winch",
            "event_reef_v2_luminous_wake", "event_reef_v2_work_song", "event_reef_v2_barnacle_ridge",
            "event_reef_v2_living_net", "event_reef_v2_salvage_argument", "event_reef_v2_sail_challenge",
            "event_reef_v2_compass_pulse", "event_reef_v2_empty_watch", "event_reef_v2_whaler_signal"
        };

        private static CoreEncounter CreateReefExpandedRandom(string id, int power)
        {
            switch (id)
            {
                case "event_reef_v2_message_gull":
                    return R(id, "信鸥寻主", "一只带铜环的信鸥落在舷边，脚上绑着一张便条，是滞留在附近的潮工写来的。它不时飞向一面打过补丁的三角帆。", false, 0,
                        C("送去木料，换取潮工船坞图", 2, new VoyageEffect { Wood = -3, GrantDiscovery = "reef_dock_chart", RevealEncounterId = "event_reef_hidden_cache" }),
                        C("送去口粮，请潮工指点顺流航路", 1, new VoyageEffect { Food = -4, SailingBoostHours = 12, Fame = 3 }),
                        C("航海士辨认便条上的水深标记", 1.5, new VoyageEffect { SurveyBoostHours = 12, RevealEncounterId = "event_reef_hidden_cache" }, "navigator"));
                case "event_reef_v2_entangled_calf":
                    return R(id, "幼鲸缠索", "幼鲸拖着沉重的废网在船边浮沉。成年鲸群守在远处，受惊的幼鲸不时甩尾，搅起大片白浪。", false, 0,
                        C("搭软垫护架，慢慢解开废网", 2, new VoyageEffect { Wood = -2, Cloth = -1, Fame = 8, Morale = 1 }),
                        C("循鲸群外围观测深水航道", 1, new VoyageEffect { SurveyBoostHours = 10 }),
                        C("潜水员从网底割开绳结", 1, new VoyageEffect { Fame = 6, SailingBoostHours = 14 }, "diver"));
                case "event_reef_v2_backspin_winch":
                    return R(id, "绞盘反扯", "抢流时拉紧的拖锚绳突然反卷，绞盘开始带着制动柄倒转。若想继续借这股潮流前进，就得赶紧重新固定拖锚。", true, 0,
                        C("松开拖绳，放弃这次抢流", 0, new VoyageEffect()),
                        C("用铁箍锁紧绞盘", 1, new VoyageEffect { Iron = -3, SailingBoostHours = 16 }),
                        C("卸下滑轮，慢慢重新穿索", 3, new VoyageEffect { Wood = 2 }),
                        C("木匠加装防倒转的楔块", .5, new VoyageEffect { Wood = -2, Hull = 4, SailingBoostHours = 10 }, "carpenter"));
                case "event_reef_v2_luminous_wake":
                    return R(id, "荧浪绘岸", "成片发光浮游物把水下礁脊描成蓝色细线。光带沿深水处延伸，近处的荧光已开始暗淡。", false, 0,
                        C("逐段描下亮起的礁脊", 2, new VoyageEffect { Reveal = 1, SurveyBoostHours = 8 }),
                        C("沿发光深水带赶路", .5, new VoyageEffect { SailingBoostHours = 10 }),
                        C("医生分离药用浮游样本", 2, new VoyageEffect { Cloth = -1, Medicine = 2 }, "doctor"));
                case "event_reef_v2_work_song":
                    return R(id, "旧船号子", "一艘返港工船上传来节奏古怪的船歌。老潮工说，号子中的停顿对应着旧船坞开门时的潮位。", false, 0,
                        C("请老潮工喝酒，记下开门顺序", 2, new VoyageEffect { Beer = -1, GrantDiscovery = "reef_dock_chart", RevealEncounterId = "event_reef_hidden_cache" }),
                        C("与工船上的水手合唱，振奋船员", 1, new VoyageEffect { Morale = 2 }),
                        C("木匠以船坞图核对修船口诀", 1.5, new VoyageEffect { Wood = -2, Hull = 10, SailingBoostHours = 8 }, "carpenter", discovery: "reef_dock_chart"));
                case "event_reef_v2_barnacle_ridge":
                    return R(id, "船底附壳", "透过清澈的海水，可以看见船底附着的层层硬壳，一直蔓延到舵轴。甲板上几道接缝也已被海水冲开。", false, 0,
                        C("轮班刮除水线附近附壳", 2, new VoyageEffect { SailingBoostHours = 12 }),
                        C("磨壳调灰，修补甲板接缝", 2, new VoyageEffect { Cloth = -1, Hull = 7 }),
                        C("潜水员清理舵轴和船底", 1, new VoyageEffect { SailingBoostHours = 20, Iron = 2 }, "diver"));
                case "event_reef_v2_living_net":
                    return R(id, "活网拦流", "海民的育鱼网随涨潮浮起，横在船头。网主人使劲挥旗，示意网底还拴着成片新种的海草。", true, 0,
                        C("沿网缘退回外侧，放弃穿越", 0, new VoyageEffect()),
                        C("分出食物，请海民收网引航", 1, new VoyageEffect { Food = -4, Fame = 5, SailingBoostHours = 10 }),
                        C("协助升起整片网架，获赠织网材料", 3, new VoyageEffect { Cloth = 5 }),
                        C("厨师换取育鱼饵料的配方", 2, new VoyageEffect { Water = -5, Food = 16 }, "cook"));
                case "event_reef_v2_salvage_argument":
                    return R(id, "双艇争锚", "两艘打捞艇分别拉住古锚的两端，船上的人正为谁先发现它争执不休。古锚上的船坞徽记还没有被刮掉。", false, 0,
                        C("付钱买下双方争抢的锚件", 1, new VoyageEffect { Gold = -35, AncientMaterial = 1 }),
                        C("主持分割锚链，只收一段作酬劳", 2, new VoyageEffect { Iron = 4, Fame = 5 }),
                        C("依船坞图辨认锚铭，换取完整拓片", 2, new VoyageEffect { MapFragments = 1, SurveyBoostHours = 8 }, discovery: "reef_dock_chart"));
                case "event_reef_v2_sail_challenge":
                    return R(id, "帆影邀赛", "一艘轻帆艇与你们并行，船长邀你们在两座浮标之间比试调帆。无论输赢，他都愿意传授在这片海域配帆的诀窍。", false, 0,
                        C("用备用帆布尝试新的配帆方法", 2, new VoyageEffect { Cloth = -2, SailingBoostHours = 18 }),
                        C("停船交换心得，趁机休整", 3, new VoyageEffect { Morale = 2, SurveyBoostHours = 6 }),
                        C("航海士领航参赛", 1, new VoyageEffect { Fame = 8, SailingBoostHours = 10 }, "navigator"));
                case "event_reef_v2_compass_pulse":
                    return R(id, "罗盘跳针", "海浪每涌起一次，罗盘指针就偏转一下，船底却没有传来撞礁的声响。测深绳触到一块覆沙的金属盘，磁力正从那里传来。", false, 0,
                        C("搭木架吊起金属盘", 3, new VoyageEffect { Wood = -3, AncientMaterial = 1 }),
                        C("只记录跳针方向，补全附近海床图", 2, new VoyageEffect { Reveal = 1 }),
                        C("航海士反向校准磁差", 1, new VoyageEffect { Iron = -1, SurveyBoostHours = 18 }, "navigator"));
                case "event_reef_v2_empty_watch":
                    return R(id, "空岗夜哨", "交班时瞭望位空无一人。值哨的水手在梯子下睡着了，其他人不愿再替他连着值班，争吵声惊醒了舱里的船员。", true, 0,
                        C("船长接班，先让争执平息", 0, new VoyageEffect()),
                        C("停船轮休，补一顿热食", 3, new VoyageEffect { Food = -5, Morale = 2 }),
                        C("缩短轮班时间，集中人手观察前路", 2, new VoyageEffect { SurveyBoostHours = 10 }),
                        C("医生照料过劳的水手，安排恢复值勤", 1, new VoyageEffect { Medicine = -1, Morale = 2, SurveyBoostHours = 6 }, "doctor"));
                case "event_reef_v2_whaler_signal":
                    return R(id, "捕鲸船的求救旗", "一艘老捕鲸船被劫掠艇钩住尾舷，来不及调转笨重的船身。劫掠者全盯着猎物，尚未察觉你们靠近。", false, Math.Max(1, power),
                        C("驶入交战水面，击退劫掠艇", 1.5, new VoyageEffect { Gold = 55, Fame = 10 }, battle: true),
                        C("从另一侧放下木筏，接走船员", 2, new VoyageEffect { Wood = -4, Cloth = -1, Fame = 8, Morale = 1 }),
                        C("炮手打断钩索，掩护捕鲸船撤出", .5, new VoyageEffect { Powder = -2, Cannonballs = -1, Fame = 6, SurveyBoostHours = 6 }, "gunner"));
                default: return null;
            }
        }

        private static IReadOnlyList<CoreEncounter> CreateReefExpandedFixed() => Array.AsReadOnly(new[] {
            F("event_reef_v2_tidewright_register", "潮工记潮台", "测绘 · 潮工线索", "浮台上保存着历代潮工的记潮木片。管理员正伏案誊写，手边摊着旧船坞的工法记录，脚下的支柱随着潮水吱呀作响。", false, false,
                C("等候潮汐变化，拼出船坞开门图", 3, new VoyageEffect { GrantDiscovery = "reef_dock_chart", RevealEncounterId = "event_reef_hidden_cache", SurveyBoostHours = 6 }),
                C("替换浮台支柱，换取修船服务", 2, new VoyageEffect { Wood = -4, Hull = 10, Fame = 3 }),
                C("木匠按船坞图校正古材榫件", 2, new VoyageEffect { Iron = -2, AncientMaterial = 1, SailingBoostHours = 12 }, "carpenter", discovery: "reef_dock_chart")),
            F("event_reef_v2_three_bell_array", "三铃测深阵", "观测", "三只铜铃浮标通过水下绳轮相连。按浪声的先后敲铃，就能分辨附近礁脊的高度。", false, false,
                C("听完一轮潮汐的铃声，绘出礁脊", 3, new VoyageEffect { Reveal = 2 }),
                C("更换损坏的铃轴，借铃阵观测海况", 1, new VoyageEffect { Iron = -2, SurveyBoostHours = 18 }),
                C("航海士由铃音推算通航潮窗", 1.5, new VoyageEffect { SailingBoostHours = 16, Fame = 4 }, "navigator")),
            F("event_reef_v2_shell_lime_barge", "贝灰养护船", "工艺", "一艘平底工作船上正在烧制贝灰。工匠用灰浆封住船缝，也向来客演示如何养护船底，免得贝壳越附越多。", false, false,
                C("用淡水调灰，封补船缝", 2, new VoyageEffect { Water = -8, Hull = 12 }),
                C("用布料换取船底养护", 2, new VoyageEffect { Cloth = -2, SailingBoostHours = 18 }),
                C("木匠回收废楔，修正舵座", 3, new VoyageEffect { Iron = -1, Hull = 6, Wood = 5 }, "carpenter")),
            F("event_reef_v2_kelp_loom", "海草织场", "采集 · 手艺", "成束海草挂在浮架上晾晒。织工说，这些海草既能编成帆衬，也能做成食物或止血敷料。", false, false,
                C("帮工编织，领取耐潮帆衬", 3, new VoyageEffect { Cloth = 6 }),
                C("用木料扩展晾架，请织工改良配帆", 2, new VoyageEffect { Wood = -3, SailingBoostHours = 16 }),
                C("厨师盐渍可食海草", 2, new VoyageEffect { Water = -4, Food = 18 }, "cook"),
                C("医生挑出止血草叶", 2, new VoyageEffect { Cloth = -1, Medicine = 2 }, "doctor")),
            F("event_reef_v2_anchor_rubbing", "旧锚拓印台", "铭文 · 潮工线索", "收藏旧船锚的海民把铭牌陈列在木排上。一枚潮工锚上刻着开启船坞隔舱的顺序，其他锚上的铭文则指向更远的航路。", false, false,
                C("以铁料换取船坞锚铭拓片", 1.5, new VoyageEffect { Iron = -3, GrantDiscovery = "reef_dock_chart", RevealEncounterId = "event_reef_hidden_cache" }),
                C("修补木排，领取报酬和饮水", 2, new VoyageEffect { Wood = -3, Gold = 24, Water = 10 }),
                C("按船坞图辨出外海锚铭", 3, new VoyageEffect { MapFragments = 1, AncientMaterial = 1 }, discovery: "reef_dock_chart")),
            F("event_reef_v2_coral_resonator", "共鸣珊瑚架", "礁海调查", "礁脊间的金属支架随着潮声震动，管口被珊瑚包覆。远处的回声从管内传来，中央轴芯随每次震动缓缓偏转。", true, false,
                C("慢慢清理管口，记录回声", 3, new VoyageEffect { SurveyBoostHours = 16, Reveal = 1 }),
                C("拆出轴芯，放弃本次观测", 2, new VoyageEffect { AncientMaterial = 1, Iron = 3 }),
                C("潜水员嵌入古材校准轴芯", 1.5, new VoyageEffect { AncientMaterial = -1, AncientCompass = 1 }, "diver")),
            F("event_reef_v2_tidal_hoist", "潮差吊台", "打捞 · 取舍", "这座靠潮汐涨落运转的旧吊台，只剩一组吊钩还能使用。一侧挂着废船骨架，另一侧挂着封好的机件；要重新穿缆，还得用上船里的材料。", false, false,
                C("花时间拆取露出的船骨", 4, new VoyageEffect { Wood = 9, Iron = 3 }),
                C("用布条重编吊缆，提取封存机件", 2, new VoyageEffect { Cloth = -3, AncientMaterial = 1 }),
                C("木匠用废船木撑起本船，进行修补", 3, new VoyageEffect { Hull = 14, Wood = 2 }, "carpenter")),
            F("event_reef_v2_diving_bell_station", "潜钟试验站", "试验 · 深潜", "几名制钟匠把新潜钟吊在工作船侧。他们需要有人测试密封、采集海床样本，或帮助记录气泡上升的位置。", false, false,
                C("送出铁箍，协助密封并取样", 2, new VoyageEffect { Iron = -3, AncientMaterial = 1, Fame = 4 }),
                C("守在水面记录气泡，绘出海床图", 3, new VoyageEffect { Reveal = 1, SurveyBoostHours = 10 }),
                C("潜水员驾驶潜钟回收观测轴", 2, new VoyageEffect { Wood = -2, AncientCompass = 1 }, "diver"),
                C("医生照管试潜人员，领取药材酬劳", 2, new VoyageEffect { Medicine = 2, Fame = 6 }, "doctor")),
        });

        private static string ReefExpandedCategory(string id)
        {
            switch (id)
            {
                case "event_reef_v2_message_gull": case "event_reef_v2_work_song":
                case "event_reef_v2_compass_pulse": return "线索调查";
                case "event_reef_v2_entangled_calf": case "event_reef_v2_whaler_signal": return "救援抉择";
                case "event_reef_v2_backspin_winch": case "event_reef_v2_empty_watch": return "船务危机";
                case "event_reef_v2_luminous_wake": case "event_reef_v2_sail_challenge": return "航路机遇";
                case "event_reef_v2_barnacle_ridge": return "船体养护";
                case "event_reef_v2_living_net": return "海上采集";
                case "event_reef_v2_salvage_argument": return "海上调解";
                default: return null;
            }
        }

        private static bool TryReefExpandedTerrain(string id, out EventTerrainConstraint terrain)
        {
            switch (id)
            {
                case "event_reef_v2_tidewright_register": case "event_reef_v2_three_bell_array":
                case "event_reef_v2_shell_lime_barge": case "event_reef_v2_kelp_loom":
                case "event_reef_v2_anchor_rubbing": case "event_reef_v2_tidal_hoist":
                case "event_reef_v2_diving_bell_station":
                    terrain = EventTerrainConstraint.OpenSea; return true;
                case "event_reef_v2_coral_resonator":
                    terrain = EventTerrainConstraint.Reef; return true;
                default: terrain = EventTerrainConstraint.Any; return false;
            }
        }
    }
}
