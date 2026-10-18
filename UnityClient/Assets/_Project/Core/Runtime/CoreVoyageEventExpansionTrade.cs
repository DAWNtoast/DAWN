using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public static partial class CoreVoyageEventCatalog
    {
        internal static readonly string[] TradeExpandedRandomIds = {
            "event_trade_v2_courier_pigeon", "event_trade_v2_right_of_way",
            "event_trade_v2_sample_sails", "event_trade_v2_transfer_queue",
            "event_trade_v2_diving_bell", "event_trade_v2_ledger_forgery",
            "event_trade_v2_charter_crowding", "event_trade_v2_guild_manifest",
            "event_trade_v2_channel_moorings", "event_trade_v2_rain_collectors",
            "event_trade_v2_provision_survey", "event_trade_v2_powder_barge"
        };

        private static CoreEncounter CreateTradeExpandedRandom(string id, int power)
        {
            switch (id)
            {
                case "event_trade_v2_courier_pigeon":
                    return R(id, "信鸽落帆", "一只受伤的信鸽落进帆褶，脚环上绑着失船货单。追来的邮艇愿用商队凭证或领航记录交换帮助。", false, 0,
                        C("接济邮艇，归还失船货单", 1, new VoyageEffect { Food = -4, GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }),
                        C("把密信卖给沿途的货商", .5, new VoyageEffect { Gold = 35, Fame = -6 }),
                        C("医生处理信鸽伤口，换取观测记录", .5, new VoyageEffect { Medicine = -1, Fame = 6, SurveyBoostHours = 18 }, "doctor"));
                case "event_trade_v2_right_of_way":
                    return R(id, "双船争道", "两艘货船各持一面优先通行旗，横在船头争执。旁边的领航小艇正在寻找能让双方退让的办法。", true, 0,
                        C("停船调停，核对两边的旗约", 2, new VoyageEffect { Fame = 6 }),
                        C("用帆布接长拖带，跟随小艇绕行", 1, new VoyageEffect { Cloth = -2, SailingBoostHours = 12 }),
                        C("航海士标出另一条可走的潮线", .5, new VoyageEffect { SurveyBoostHours = 12 }, "navigator"),
                        C("收起通行旗，从争执外侧退开", 0, new VoyageEffect { Morale = -1 }));
                case "event_trade_v2_sample_sails":
                    return R(id, "借帆试航", "帆商把两套新裁的试航帆挂在货船旁。他需要材料、试航记录，也愿让懂船的人改装一套带走。", false, 0,
                        C("交出布料，借用轻型辅帆", 1, new VoyageEffect { Cloth = -3, SailingBoostHours = 18 }),
                        C("承接满载急转试验，收取试航费", 3, new VoyageEffect { Hull = -2, Gold = 38 }),
                        C("木匠用余料改装帆架", 2, new VoyageEffect { Wood = -2, Cloth = -1, Hull = 5, SailingBoostHours = 10 }, "carpenter"));
                case "event_trade_v2_transfer_queue":
                    return R(id, "转运排队", "临时转运船挤满航道，急需药品的搬运工与送餐艇正寻找空隙。留下等待、帮助工人或退出队列，都要现在决定。", true, 0,
                        C("等转运结束，领取船队分发的口粮", 3, new VoyageEffect { Food = 6 }),
                        C("赠药给搬运工，换取支航道记录", 1, new VoyageEffect { Medicine = -1, Fame = 8, SurveyBoostHours = 8 }),
                        C("厨师给工班做饭，收取餐费", 2, new VoyageEffect { Food = -6, Gold = 20, Morale = 1 }, "cook"),
                        C("立即退出队列，让出船位", 0, new VoyageEffect()));
                case "event_trade_v2_diving_bell":
                    return R(id, "潜钟回铃", "打捞船水下的潜钟连续敲响求援铃。摇架已经变形，船主愿以钱款或一枚货舱宝石酬谢把潜员带回的人。", false, 0,
                        C("拼出支架，缓慢提起潜钟", 2, new VoyageEffect { Iron = -3, Wood = -1, Gold = 35, Fame = 6 }),
                        C("潜水员用布条封住漏气接缝", 1, new VoyageEffect { Cloth = -2, Gem = 1 }, "diver"),
                        C("借本船船舷强行托起摇架", .5, new VoyageEffect { Hull = -4, Fame = 8, Gold = 25 }));
                case "event_trade_v2_ledger_forgery":
                    return R(id, "双份航单", "过船递来两份日期相同、去向相反的香料货单。商队抄写员怀疑有人伪造沉船记录，想请你当场核验。", false, 0,
                        C("逐项核对货单，找回商队托付", 3, new VoyageEffect { GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }),
                        C("公开伪造的印记，维护商路信誉", 1, new VoyageEffect { Fame = 10 }),
                        C("把假航单转卖给投机货商", .5, new VoyageEffect { Gold = 24, Fame = -6 }),
                        C("航海士从日期与潮位复原支航线", 1, new VoyageEffect { SurveyBoostHours = 18, Reveal = 1 }, "navigator"));
                case "event_trade_v2_charter_crowding":
                    return R(id, "争铺乘客", "客船上的乘客为最后几张铺位票争吵，小艇靠来求援。船员们也开始争论是否该把本船补给分给他们。", true, 0,
                        C("分送食水，让乘客留在客船等候", 1, new VoyageEffect { Food = -5, Water = -5, Fame = 8 }),
                        C("开一桶酒，调停争执并安抚本船", .5, new VoyageEffect { Beer = -1, Morale = 2 }),
                        C("医生核验急需卧铺的病人", 2, new VoyageEffect { Fame = 6, Morale = 1 }, "doctor"),
                        C("拒绝停靠，结束船员的争论", 0, new VoyageEffect { Morale = -1 }));
                case "event_trade_v2_guild_manifest":
                    return R(id, "船旗漏名", "行会登记船漏写了一支小商队的船名。对方既拿不到领航记录，也不能领取寄存货款，只好沿途寻找能作证的人。", false, 0,
                        C("凭互助印章作证，领取核验酬劳", 1, new VoyageEffect { Gold = 35, Fame = 5 }, discovery: "trade_rescue_seal"),
                        C("帮助重抄名册，换取沿途观察记录", 3, new VoyageEffect { Fame = 6, SurveyBoostHours = 8 }),
                        C("赠送新旗的布料，让小商队重挂旗号", 1, new VoyageEffect { Cloth = -3, Fame = 8 }),
                        C("航海士核对尾流，随登记船赶上商队", 1.5, new VoyageEffect { SailingBoostHours = 14 }, "navigator"));
                case "event_trade_v2_channel_moorings":
                    return R(id, "横漂系泊缆", "转运船的一根长缆漂到船头，系泊工正在招呼你借用旁侧浮标。继续停留会被后方船队赶上。", true, 0,
                        C("付钱让系泊工牵引通过", .5, new VoyageEffect { Gold = -15, SailingBoostHours = 10 }),
                        C("等工班收回长缆", 3, new VoyageEffect()),
                        C("木匠加装护舷，借浮标转过缆绳", 1, new VoyageEffect { Wood = -2, Iron = -1, Hull = 4 }, "carpenter"),
                        C("收起帆脚，从空出的外侧退开", 0, new VoyageEffect { Morale = -1 }));
                case "event_trade_v2_rain_collectors":
                    return R(id, "雨棚收水", "一排收水艇正撑开灰白雨棚，棚主允许过船用材料分得雨水。随风摆动的棚角也清楚标出了近处的风向。", false, 0,
                        C("提供帆布，接取一份雨水", 2, new VoyageEffect { Cloth = -2, Water = 22 }),
                        C("把棚角当风标，记录海面视线", .5, new VoyageEffect { SurveyBoostHours = 8 }),
                        C("厨师分送热食，换取净水", 1, new VoyageEffect { Food = -3, Water = 12, Morale = 1 }, "cook"));
                case "event_trade_v2_provision_survey":
                    return R(id, "船厨盲评", "几艘补给船把匿名样餐摆在船头，请过路船员评选耐储的海上口粮。主办人以钱款与样粮酬谢参与者。", false, 0,
                        C("用自家食材备餐，争取商队订单赏金", 2, new VoyageEffect { Food = -8, Gold = 35, Fame = 4 }),
                        C("带酒参加品评，让水手换换口味", 1, new VoyageEffect { Beer = -1, Morale = 2 }),
                        C("厨师鉴别样餐，领取留存样粮", 2, new VoyageEffect { Food = 14, Fame = 5 }, "cook"));
                case "event_trade_v2_powder_barge":
                    return R(id, "静默火药驳", "火药驳船连钟都不敢敲，船主用手势指向追来的劫货快艇。他需要护航，也急缺遮住货舱的厚布。", false, Math.Max(1, power),
                        C("接过护航赏金约，迎击劫货艇", 1, new VoyageEffect { Gold = 65, Fame = 4 }, battle: true),
                        C("用厚布遮住货舱，换走少量火药", 2, new VoyageEffect { Cloth = -3, Powder = 3 }),
                        C("炮手驶离驳船测炮，换取合口径炮弹", 1, new VoyageEffect { Powder = -1, Cannonballs = 6, Fame = 4 }, "gunner"));
                default: return null;
            }
        }

        private static IReadOnlyList<CoreEncounter> CreateTradeExpandedFixed() => Array.AsReadOnly(new[] {
            F("event_trade_v2_notary_barge", "公证驳船", "公证 · 商队线索", "公证人将印章拴在铁链上，为失船货主登记新的保全受托人。侧舱还在招募整理潮位账册的帮手。", false, false,
                C("缴付登记费，接下失船保全托付", 2, new VoyageEffect { Gold = -10, GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }),
                C("凭互助印章校对旧档，领取酬劳", 1, new VoyageEffect { Gold = 35, Fame = 3 }, discovery: "trade_rescue_seal"),
                C("航海士整理潮位账册，抄下观测记录", 1.5, new VoyageEffect { SurveyBoostHours = 18, Reveal = 1 }, "navigator"),
                C("帮书记员誊写船籍，赚取工钱", 3, new VoyageEffect { Gold = 20 })),
            F("event_trade_v2_abandoned_countinghouse", "潮退账房", "旧档 · 商队线索", "退潮露出旧商栈的低矮账房。保险箱已经破开，但浸湿的货主名册与一套互助印记仍压在书桌下。", true, false,
                C("晒干名册，复原失船货主的托付", 3, new VoyageEffect { GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }),
                C("先拿走尚未认领的现金", 1, new VoyageEffect { Gold = 55, Fame = -5 }),
                C("航海士拼接旧账背面的航图", 2, new VoyageEffect { MapFragments = 1, SurveyBoostHours = 8 }, "navigator")),
            F("event_trade_v2_sail_pattern_loft", "帆样工坊", "改装", "裁帆师把一座平底驳船改成工坊，桁架上挂满不同裁法的帆样。他只收现成材料，也接受工具料换货。", false, false,
                C("提供布料，换装轻型辅帆", 2, new VoyageEffect { Cloth = -4, Hull = 3, SailingBoostHours = 20 }),
                C("用铁料换取余木与帆布", 2, new VoyageEffect { Iron = -3, Wood = 4, Cloth = 4 }),
                C("木匠借工坊设备整修帆架", 3, new VoyageEffect { Wood = -3, Hull = 12 }, "carpenter")),
            F("event_trade_v2_tide_ticket_booth", "潮汐票亭", "航路调度", "小浮亭出售顺潮领航票，也公开展示下一轮潮汐的支线图。领航船很快就走，完整图册则要等值班员交接。", false, false,
                C("购买领航票，跟上顺潮船", .5, new VoyageEffect { Gold = -18, SailingBoostHours = 20 }),
                C("等候换班，抄录完整支线图", 4, new VoyageEffect { Reveal = 2 }),
                C("航海士用帆布拓下风潮图", 1, new VoyageEffect { Cloth = -1, SurveyBoostHours = 20 }, "navigator")),
            F("event_trade_v2_retired_pilot_station", "退役引水站", "观测", "海滩上的旧引水棚已不再收费。退休领航员守着最后一幅海面速写，收集船灯给沿岸的小舟使用。", false, false,
                C("听完沿岸口述，记录隐藏支路", 4, new VoyageEffect { SurveyBoostHours = 16, Reveal = 1 }),
                C("转让一盏船灯，领取维修会的补贴", 1, new VoyageEffect { Lantern = -1, Gold = 30, Fame = 5 }),
                C("航海士复原速写中的潮线", 2, new VoyageEffect { SailingBoostHours = 14, SurveyBoostHours = 10 }, "navigator")),
            F("event_trade_v2_weighing_raft", "浮秤仲裁场", "商事仲裁", "两家货行把浮筏上的大秤围住：一方说秤轴失准，另一方想用钱买通仲裁者。秤架旁还堆着待分配的抵押布料。", false, false,
                C("补换秤轴，公开重新称重", 2, new VoyageEffect { Iron = -2, Fame = 10 }),
                C("收钱替出价最高的货行说话", .5, new VoyageEffect { Gold = 45, Fame = -8 }),
                C("木匠撑平秤架，收取修理费", 1.5, new VoyageEffect { Wood = -2, Gold = 32, Fame = 4 }, "carpenter"),
                C("凭互助印章监督分货，领取劳务布料", 2, new VoyageEffect { Cloth = 6 }, discovery: "trade_rescue_seal")),
            F("event_trade_v2_dyers_anchorage", "漂染锚地", "工料交换", "染布船挂着尚未干透的蓝帆。染工缺净水和热食，愿拿加工费或现成布匹与过船交换。", false, false,
                C("提供白布，领取加工货款", 1, new VoyageEffect { Cloth = -4, Gold = 36 }),
                C("用淡水换取晒好的布匹", 2, new VoyageEffect { Water = -12, Cloth = 6 }),
                C("厨师为染工送热餐，换取余布", 1, new VoyageEffect { Food = -4, Cloth = 3, Morale = 1 }, "cook")),
            F("event_trade_v2_floating_school", "跨海学堂", "传习", "退役商船上的孩子正在学着画第一张海图。教员留有前辈的航图片段，希望过船分享见闻或修缮课堂。", false, false,
                C("讲述航行见闻，换取旧航图片段", 4, new VoyageEffect { Fame = 8, MapFragments = 1 }),
                C("航海士带学生测绘，留下一份观测副本", 1.5, new VoyageEffect { Cloth = -1, SurveyBoostHours = 20 }, "navigator"),
                C("木匠修补课室，领取资助会的工钱", 3, new VoyageEffect { Wood = -4, Gold = 24, Fame = 6 }, "carpenter"),
                C("赠送午餐，与孩子们分享海上的故事", 1, new VoyageEffect { Food = -8, Morale = 2 }))
        });

        private static string TradeExpandedCategory(string id)
        {
            switch (id)
            {
                case "event_trade_v2_courier_pigeon": case "event_trade_v2_ledger_forgery": return "线索调查";
                case "event_trade_v2_right_of_way": case "event_trade_v2_guild_manifest": return "商事交涉";
                case "event_trade_v2_sample_sails": return "试航改装";
                case "event_trade_v2_transfer_queue": return "转运协作";
                case "event_trade_v2_diving_bell": case "event_trade_v2_charter_crowding": return "救援抉择";
                case "event_trade_v2_channel_moorings": return "航路险情";
                case "event_trade_v2_rain_collectors": return "海上采集";
                case "event_trade_v2_provision_survey": return "船队交流";
                case "event_trade_v2_powder_barge": return "护航委托";
                default: return null;
            }
        }

        private static bool TryTradeExpandedTerrain(string id, out EventTerrainConstraint terrain)
        {
            switch (id)
            {
                case "event_trade_v2_abandoned_countinghouse": case "event_trade_v2_retired_pilot_station":
                    terrain = EventTerrainConstraint.Beach; return true;
                case "event_trade_v2_notary_barge": case "event_trade_v2_sail_pattern_loft":
                case "event_trade_v2_tide_ticket_booth": case "event_trade_v2_weighing_raft":
                case "event_trade_v2_dyers_anchorage": case "event_trade_v2_floating_school":
                    terrain = EventTerrainConstraint.OpenSea; return true;
                default: terrain = EventTerrainConstraint.Any; return false;
            }
        }
    }
}
