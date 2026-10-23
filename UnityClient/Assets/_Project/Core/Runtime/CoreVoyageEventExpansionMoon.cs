using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public static partial class CoreVoyageEventCatalog
    {
        internal static readonly string[] MoonExpandedRandomIds = {
            "event_moon_v2_reversed_rubbing", "event_moon_v2_hollow_bell",
            "event_moon_v2_borrowed_shadow", "event_moon_v2_star_in_jar",
            "event_moon_v2_living_figurehead", "event_moon_v2_funerary_procession",
            "event_moon_v2_misplaced_echo", "event_moon_v2_false_ancestor",
            "event_moon_v2_tide_abacus", "event_moon_v2_moon_taxcollector",
            "event_moon_v2_astronomer_rescue", "event_moon_v2_silver_needle"
        };

        private static CoreEncounter CreateMoonExpandedRandom(string id, int power)
        {
            switch (id)
            {
                case "event_moon_v2_reversed_rubbing":
                    return R(id, "逆写拓片", "一卷蜡封拓片撞上船舷，所有字都朝着纸背生长。透过湿纸，能看见月痕方碑的轮廓。", false, 0,
                        C("以新布转印反字，复原星图", 2.5,
                            new VoyageEffect { Cloth = -2, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }),
                        C("航海士比对纸背的观测点", 2,
                            new VoyageEffect { Reveal = 1, SurveyBoostHours = 12 }, "navigator"),
                        C("把原件卖给路过的收购艇", 1, new VoyageEffect { Gold = 24 }),
                        C("依已有星图拆解封蜡中的机关", 1.5,
                            new VoyageEffect { AncientMaterial = 1 }, discovery: "moon_star_chart"));
                case "event_moon_v2_hollow_bell":
                    return R(id, "无声铜铃", "捞起的铜铃没有铃舌，却震得满船的钉子一齐颤动。船员们都说，自己听见了同一个陌生名字。", true, 0,
                        C("缠布封住铜铃，平复潮咒", 1,
                            new VoyageEffect { Cloth = -2, Curse = -1 }),
                        C("将铜铃挂上桅顶，追踪异响", .5,
                            new VoyageEffect { Curse = 1, SurveyBoostHours = 16 }),
                        C("炮手定向击碎铃壳，取出内芯", 1,
                            new VoyageEffect { Powder = -1, Cannonballs = -1, AncientMaterial = 1 }, "gunner"),
                        C("解开吊绳，将铜铃放回海中", 0, new VoyageEffect()));
                case "event_moon_v2_borrowed_shadow":
                    return R(id, "借影摆渡人", "一叶没有倒影的小舟与船并行。摆渡人想借走一道影子，愿拿水下的见闻作交换；一盏能照见自己的灯，他也肯收。", false, 0,
                        C("交出航海灯，请他带走潮咒", 1,
                            new VoyageEffect { Lantern = -1, Curse = -2 }),
                        C("借出影子，请他指明水下地点", .5,
                            new VoyageEffect { Curse = 1, Reveal = 2 }),
                        MoonChoiceAtTime(C("夜间请航海士核对舟尾星线", 2,
                            new VoyageEffect { SailingBoostHours = 16 }, "navigator"), CoreVoyageTimeBand.Night),
                        C("静看船灯倒影，记下异常折射", 3,
                            new VoyageEffect { SurveyBoostHours = 10 }));
                case "event_moon_v2_star_in_jar":
                    return R(id, "瓶中星虫", "一只玻璃罐浮上水面，罐内的微光随海底地形明灭。光点会聚成星座，也会在药液旁停留。", false, 0,
                        C("用药液与滤布制成航海灯", 2,
                            new VoyageEffect { Medicine = -1, Cloth = -1, Lantern = 1 }),
                        C("观察光点变化，改进瞭望方法", 1.5,
                            new VoyageEffect { SurveyBoostHours = 14 }),
                        C("医生分离罐中的药用沉淀", 2,
                            new VoyageEffect { Medicine = 1 }, "doctor"),
                        C("打开罐子放走星虫，让船员观看星群", .5,
                            new VoyageEffect { Morale = 1 }));
                case "event_moon_v2_living_figurehead":
                    return R(id, "活木船首", "漂来的旧船首竟还在生长。年轮的纹路勾勒出几条早已消失的航道，木头深处却嵌着一枚冰冷的机关芯。", false, 0,
                        C("木匠嫁接活木，修补船壳", 2,
                            new VoyageEffect { Wood = -3, Hull = 8 }, "carpenter"),
                        C("剖开木头，取出机关芯", 1,
                            new VoyageEffect { AncientMaterial = 1, Curse = 1 }),
                        C("用帆布拓下木纹航道", 3,
                            new VoyageEffect { Cloth = -1, SailingBoostHours = 14 }),
                        C("将船首完整送还大海，举行送船礼", .5,
                            new VoyageEffect { Morale = 1 }));
                case "event_moon_v2_funerary_procession":
                    return R(id, "送葬灯列", "一列小灯舟载着写有姓名的牌子，从船旁缓缓漂过。末尾那只小舟的铭牌正沉入水中，远处还有几盏灯停在海图未标注的水域。", false, 0,
                        C("献酒补行送别礼", 1,
                            new VoyageEffect { Beer = -1, Curse = -1, Fame = 6 }),
                        C("潜水员用布包好水下铭牌，带回船上", 3,
                            new VoyageEffect { Cloth = -1, AncientMaterial = 2 }, "diver"),
                        C("越过灯列，追查停灯地点", 2,
                            new VoyageEffect { Reveal = 2, Fame = -4 }),
                        C("为最后一舟唱完挽歌", 3,
                            new VoyageEffect { Morale = 2 }));
                case "event_moon_v2_misplaced_echo":
                    return R(id, "错位炮声", "海上传来的炮声比甲板上的敲击更早响起。每敲一下，舱内的空箱就向声源挪动一寸。", true, 0,
                        C("炮手打一发测距弹，校准回声", 1,
                            new VoyageEffect { Powder = -1, Cannonballs = -1, SurveyBoostHours = 20 }, "gunner"),
                        C("用炮弹作配重，压住松动的舱板", 2,
                            new VoyageEffect { Cannonballs = -3, Hull = 6 }),
                        C("依星图追溯回声的水下来源", 2,
                            new VoyageEffect { Reveal = 1, AncientMaterial = 1 }, discovery: "moon_star_chart"),
                        C("停止敲击，解开拖绳离开共振区", 0, new VoyageEffect()));
                case "event_moon_v2_false_ancestor":
                    return R(id, "假名祖灵", "一团水光自称是船员的祖先，却把每个人的故乡都说错了。它声称知道一处封存的墓藏，条件是让你记住它真正的名字。", false, 0,
                        C("记下禁名，换取墓藏拓片", .5,
                            new VoyageEffect { Curse = 1, MapFragments = 1 }),
                        C("医生以药香帮船员摆脱幻觉", 1,
                            new VoyageEffect { Medicine = -1, Curse = -2 }, "doctor"),
                        C("逐一讲述故乡，拆穿冒名者", 3,
                            new VoyageEffect { Morale = 2 }),
                        C("依星图指出它伪造的月位", 1,
                            new VoyageEffect { Fame = 8, Reveal = 1 }, discovery: "moon_star_chart"));
                case "event_moon_v2_tide_abacus":
                    return R(id, "潮汐算筹", "漂木上的铜制算筹正自行拨动。它缺了一颗算珠，翻来覆去都只显示同一个月相。", false, 0,
                        C("依星图补入机关材料，组装罗盘", 3,
                            new VoyageEffect { AncientMaterial = -1, AncientCompass = 1 }, discovery: "moon_star_chart"),
                        C("航海士推算下一轮潮窗", 2,
                            new VoyageEffect { SailingBoostHours = 18 }, "navigator"),
                        C("拆回完整的计数珠和铜轴", 1,
                            new VoyageEffect { Iron = 5 }),
                        C("在新布上记下算筹的一轮变化", 3.5,
                            new VoyageEffect { Cloth = -1, SurveyBoostHours = 16 }));
                case "event_moon_v2_moon_taxcollector":
                    return R(id, "月影收租人", "披着空甲的收租人划着小艇靠近，把海面上的月影称作私人泊位。甲缝里夹着旧王庭欠契。", false, Math.Max(90, power),
                        C("交出航海灯，请它注销潮咒欠契", 1,
                            new VoyageEffect { Lantern = -1, Curse = -2 }),
                        C("以星图指出契约上的虚假月位", 2,
                            new VoyageEffect { Fame = 8, SurveyBoostHours = 8 }, discovery: "moon_star_chart"),
                        C("击破执契甲胄，取回封存物", 1,
                            new VoyageEffect { Gold = 60, AncientMaterial = 1 }, battle: true),
                        C("收起舷灯，驶离它的影子", 0, new VoyageEffect()));
                case "event_moon_v2_astronomer_rescue":
                    return R(id, "星象师的空艇", "救生艇上只有一名仍在计算月位的学者。他的水囊已空，手里却紧攥着方碑的星位抄本和一面黄铜观测镜。", false, 0,
                        C("补足返程口粮，换回星图抄本", 2,
                            new VoyageEffect { Food = -6, Water = -6, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }),
                        C("医生救治脱水学者", 1,
                            new VoyageEffect { Medicine = -1, Morale = 2, Fame = 8 }, "doctor"),
                        C("以修艇金属换取黄铜观测镜", 1,
                            new VoyageEffect { Iron = -4, SurveyBoostHours = 20 }),
                        C("协助核算，抄下他记录的一处坐标", 3,
                            new VoyageEffect { Reveal = 1 }));
                case "event_moon_v2_silver_needle":
                    return R(id, "银针雨", "没有乌云，细如发丝的银针却垂直落在海面上。每根针停住时，都指向同一颗看不见的星。", false, 0,
                        C("铺开滤布收集完整银针", 2,
                            new VoyageEffect { Cloth = -2, Iron = 7 }),
                        C("用针阵校准测角仪，修正航路", 3,
                            new VoyageEffect { SurveyBoostHours = 12, SailingBoostHours = 6 }),
                        C("炮手把空心针管制成散弹", 1,
                            new VoyageEffect { Powder = -1, Cannonballs = 4 }, "gunner"),
                        C("撤回滤网，结束观测", 0, new VoyageEffect()));
                default: return null;
            }
        }

        private static IReadOnlyList<CoreEncounter> CreateMoonExpandedFixed() => Array.AsReadOnly(new[] {
            F("event_moon_v2_calendar_mosaic", "碎历拼台", "解谜 · 月相星图",
                "海上系泊着一座浮台，台上摆着十二块旧历板。边框已有缺口，历板总在滑动；拼对后，板上的刻线就会指向月痕方碑。", false, false,
                C("修好边框，拓印完整星历", 3,
                    new VoyageEffect { Wood = -2, Cloth = -1, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }),
                C("依星图复原远望镜的机关座", 2,
                    new VoyageEffect { AncientMaterial = -1, SurveyBoostHours = 24 }, discovery: "moon_star_chart"),
                MoonChoiceAtTime(C("日间比对影针，核准观测刻度", 2,
                    new VoyageEffect { SurveyBoostHours = 16 }), CoreVoyageTimeBand.Day),
                C("拆取浮台的备用铜轴", 2, new VoyageEffect { Iron = 8 })),
            F("event_moon_v2_ossuary_buoy", "白骨浮标", "葬仪 · 封存物",
                "一座隐在水下的骨白浮标系着小匣。匣盖记载亡者姓名，浮标底部挂着当年送葬医者的药罐。", true, false,
                C("用航海灯照亮骨辞，开启封匣", 2,
                    new VoyageEffect { Lantern = -1, Relic = 1, Curse = 1 }),
                C("补写亡者名字，献酒安魂", 3,
                    new VoyageEffect { Beer = -1, Curse = -2, Fame = 8 }),
                C("医生包裹并取回未破的药罐", 2,
                    new VoyageEffect { Cloth = -2, Medicine = 2 }, "doctor"),
                C("解下新近缠住浮标的渔网", 1.5, new VoyageEffect { Cloth = 4 })),
            F("event_moon_v2_submerged_scriptorium", "潮下誊写席", "考古 · 月相星图",
                "岸边的一排誊写席只剩桌面露出水面。桌沿星槽的排列暗示着解读方碑的顺序，抽屉里的印石下则压着密封卷轴。", true, false,
                C("铺布拓下桌沿的纹样，复原星图", 3,
                    new VoyageEffect { Cloth = -3, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }),
                C("按星图开启密封卷轴匣", 3,
                    new VoyageEffect { AncientMaterial = -1, Relic = 1 }, discovery: "moon_star_chart"),
                C("潜水员只取水下刻印石", 2,
                    new VoyageEffect { AncientMaterial = 2 }, "diver"),
                C("整理已露出水面的铭文", 4, new VoyageEffect { Fame = 10 })),
            F("event_moon_v2_pendulum_archive", "摆锤档案船", "机关 · 航路计算",
                "废弃档案船里的摆锤仍在记录潮汐。船上留下的旧修缮记录写着整修船底的方法，馆员的兑换册上则记着以遗物抵账的办法。", false, false,
                C("按修缮记录整修船底", 3,
                    new VoyageEffect { Wood = -2, Iron = -2, SailingBoostHours = 20 }),
                C("出售一件遗物，领取测绘档案", 1,
                    new VoyageEffect { Relic = -1, Gold = 95, SurveyBoostHours = 14 }),
                C("航海士修好摆锤的定位轴", 1.5,
                    new VoyageEffect { Iron = -2, SurveyBoostHours = 18 }, "navigator"),
                C("回收空置的防潮书柜", 4, new VoyageEffect { Wood = 9, Cloth = 3 })),
            F("event_moon_v2_exorcist_kiln", "驱咒小窑", "净化 · 炼制",
                "海滩上的旧窑内余烬未熄，窑壁刻着驱咒、炼灯和焙药的配方。几束干药草挂在窑边，陶罐口凝着细密水珠。", false, false,
                C("添柴焚去附身潮咒", 3,
                    new VoyageEffect { Wood = -4, Curse = -2 }),
                C("用古代材料炼制净咒灯芯", 2,
                    new VoyageEffect { AncientMaterial = -1, Curse = -1, Lantern = 1 }),
                C("厨师用酒液萃取窑边药草", 2,
                    new VoyageEffect { Beer = -1, Medicine = 2 }, "cook"),
                C("收集窑边陶罐中的蒸馏水", 2, new VoyageEffect { Water = 16 })),
            F("event_moon_v2_survey_barge", "测深石舟", "考古 · 水下测绘",
                "一艘木驳船停在开阔的海面上，船上系着石制的测深槽。铅锤、样品盒和古人的斜线记录都还在，取自深水处的样品仍封存在船底的绳篮里。", false, false,
                C("航海士重制铅锤，测量断层", 2,
                    new VoyageEffect { Iron = -2, Reveal = 2, SurveyBoostHours = 12 }, "navigator"),
                C("用炮弹逐段测深，记下可直行的水道", 3,
                    new VoyageEffect { Cannonballs = -2, SailingBoostHours = 18 }),
                C("回收敞开的古代样品盒", 2, new VoyageEffect { AncientMaterial = 1 }),
                C("沿星图上的斜线，拖起深水处的样品", 3,
                    new VoyageEffect { Cloth = -2, Relic = 1, Curse = 1 }, discovery: "moon_star_chart")),
            F("event_moon_v2_votive_loom", "还愿织机", "仪式 · 船帆改装",
                "沉在浮筏夹层里的织机无人操纵，空梭却还在来回穿行。旧铭文说它能把誓约织进帆布，也能将附身的潮咒留在纬线里。", true, false,
                C("献出遗物，将潮咒织入空帆", 2,
                    new VoyageEffect { Relic = -1, Curse = -4, Cloth = 6 }),
                C("木匠修复断梭，重织减阻翼帆", 3,
                    new VoyageEffect { Wood = -3, Cloth = -2, SailingBoostHours = 22 }, "carpenter"),
                C("献出旧帆，复原失落的月纹", 4,
                    new VoyageEffect { Cloth = -4, Fame = 12, MapFragments = 1 }),
                C("拆回尚未染色的纬线", 2, new VoyageEffect { Cloth = 6 })),
            F("event_moon_v2_unlit_court", "熄灯审议台", "解谜 · 旧庭证词",
                "翻覆的浮台背面藏着议席和证物槽。空席上摆着刻有不同月相的印章，几盏熄灭的见证灯围着中央的封存匣。", true, false,
                C("依星图重排证物，开启封存匣", 3,
                    new VoyageEffect { AncientMaterial = -1, Relic = 1, Fame = 6 }, discovery: "moon_star_chart"),
                C("接受无名证人的记忆", 1,
                    new VoyageEffect { Curse = 1, Reveal = 3 }),
                C("留下一盏灯，请证人化解潮咒", 2,
                    new VoyageEffect { Lantern = -1, Curse = -2, SurveyBoostHours = 12 }),
                MoonChoiceAtTime(C("夜间听完席间证词，整理判词", 3,
                    new VoyageEffect { Fame = 12 }), CoreVoyageTimeBand.Night))
        });

        private static string MoonExpandedCategory(string id)
        {
            switch (id)
            {
                case "event_moon_v2_reversed_rubbing": case "event_moon_v2_tide_abacus": return "考古解读";
                case "event_moon_v2_hollow_bell": case "event_moon_v2_misplaced_echo": return "器物异象";
                case "event_moon_v2_borrowed_shadow": case "event_moon_v2_false_ancestor": return "异象交涉";
                case "event_moon_v2_star_in_jar": case "event_moon_v2_silver_needle": return "博物观测";
                case "event_moon_v2_living_figurehead": return "船首遗物";
                case "event_moon_v2_funerary_procession": return "海上葬仪";
                case "event_moon_v2_moon_taxcollector": return "王庭欠契";
                case "event_moon_v2_astronomer_rescue": return "学者救援";
                default: return null;
            }
        }

        private static bool TryMoonExpandedTerrain(string id, out EventTerrainConstraint terrain)
        {
            switch (id)
            {
                case "event_moon_v2_submerged_scriptorium": case "event_moon_v2_exorcist_kiln":
                    terrain = EventTerrainConstraint.Beach; return true;
                case "event_moon_v2_calendar_mosaic": case "event_moon_v2_ossuary_buoy":
                case "event_moon_v2_pendulum_archive": case "event_moon_v2_survey_barge":
                case "event_moon_v2_votive_loom": case "event_moon_v2_unlit_court":
                    terrain = EventTerrainConstraint.OpenSea; return true;
                default: terrain = EventTerrainConstraint.Any; return false;
            }
        }

        private static CoreEncounterChoice MoonChoiceAtTime(CoreEncounterChoice choice, CoreVoyageTimeBand time) =>
            new CoreEncounterChoice(choice.Label, choice.Detail, choice.Hours, choice.Effect,
                choice.RequiredCrew, choice.StartsBattle, renewable: choice.Renewable, requiredTime: time,
                minimumCurse: choice.MinimumCurse, maximumCurse: choice.MaximumCurse,
                requiredDiscovery: choice.RequiredDiscovery);
    }
}
