using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Authored expeditions for new maps. The legacy catalog remains available for old checkpoints.</summary>
    public static class CoreTreasureExpeditionCatalog
    {
        public static string ThemeName(string rankId)
        {
            switch (rankId)
            {
                case "weathered": return "残旧海图 · 沉船工坊";
                case "gilded": return "鎏金海图 · 商盟密约";
                case "moonlit": return "月蚀海图 · 三相仪式";
                default: return "藏宝海图";
            }
        }

        public static string Overview(string rankId)
        {
            switch (rankId)
            {
                case "weathered": return "先到潮工营地备料，修复绞盘与潜水钟，解锁深层打捞与航道工程。完整秘藏：古代材料、罗盘、航海灯；也可粗拆撤离。";
                case "gilded": return "先登记公证货单，赚取并留足 3 信用、通缉不高于 1，领取金币、珠宝与药品；走私可得黑珍珠，但会伤船并招来诅咒。";
                case "moonlit": return "先寻昼辉祭坛，依白昼、黄昏、夜间点亮三相；可等待或带诅咒强行共鸣。齐备后到圣泉净化，取得遗物与黑珍珠。";
                default: return "探索海图上的独立地标，寻找本次秘藏的开启方式。";
            }
        }

        public static IReadOnlyList<string> GuaranteedEventIds(string rankId)
        {
            switch (rankId)
            {
                case "weathered": return Array.AsReadOnly(new[] { "weathered_camp", "weathered_winch", "weathered_bell", "weathered_tide_turbine" });
                case "gilded": return Array.AsReadOnly(new[] { "gilded_manifest", "gilded_witness", "gilded_relief", "gilded_customs", "gilded_convoy" });
                case "moonlit": return Array.AsReadOnly(new[] { "moonlit_dawn", "moonlit_dusk", "moonlit_midnight", "moonlit_cleanse", "moonlit_star_current" });
                default: return Array.AsReadOnly(Array.Empty<string>());
            }
        }

        public static IReadOnlyList<CoreEncounter> CreateEncounters(CoreTreasureMapRank rank)
        {
            if (rank == null) throw new ArgumentNullException(nameof(rank));
            switch (rank.Id)
            {
                case "weathered": return Weathered();
                case "gilded": return Gilded();
                case "moonlit": return Moonlit();
                default: throw new ArgumentException("Unknown treasure expedition rank.", nameof(rank));
            }
        }

        private static CoreEncounterChoice C(string label, string detail, double hours, VoyageEffect effect,
            string crew = "", string mark = "", int credit = 0, int heat = 5, int resonance = 0,
            CoreVoyageTimeBand time = CoreVoyageTimeBand.Any, int minimumCurse = 0, int maximumCurse = int.MaxValue,
            bool battle = false) => new CoreEncounterChoice(label, detail, hours, effect, requiredCrew: crew,
                startsBattle: battle, requiredTime: time, minimumCurse: minimumCurse, maximumCurse: maximumCurse,
                requiredTreasureMark: mark, minimumTreasureCredit: credit, maximumTreasureHeat: heat,
                minimumTreasureResonance: resonance);

        private static CoreEncounter E(string id, string name, string category, string description,
            params CoreEncounterChoice[] choices) => new CoreEncounter(id, name, category, description, false, HiddenSideEvent(id), 2, 0, choices);

        private static bool HiddenSideEvent(string id) =>
            TryClassifyDiscovery(id, out EncounterDiscoveryKind kind) && kind == EncounterDiscoveryKind.HiddenLocation;

        public static bool TryClassifyDiscovery(string id, out EncounterDiscoveryKind kind)
        {
            switch (id)
            {
                // Deep wreck interiors, secret trading locations and concealed ritual sites
                // need close investigation or intelligence before appearing on the chart.
                case "weathered_keel_vault": case "weathered_glass_dome":
                case "weathered_signal_buoy": case "weathered_broken_caisson":
                case "gilded_black_market": case "gilded_counterfeit":
                case "gilded_ledger_buoy": case "gilded_insurance":
                case "moonlit_mirror_pool": case "moonlit_pearl_moon":
                case "moonlit_echo_library": case "moonlit_sleeping_guardian":
                    kind = EncounterDiscoveryKind.HiddenLocation;
                    return true;

                // Equipment landmarks, public service ships and the three altars have
                // visible silhouettes. No expedition side event is an onboard incident.
                case "weathered_winch": case "weathered_bell": case "weathered_camp":
                case "weathered_chain_gate": case "weathered_supply_barge": case "weathered_rustyard":
                case "weathered_tide_turbine": case "weathered_cartographer_hulk":
                case "gilded_manifest": case "gilded_witness": case "gilded_relief": case "gilded_customs":
                case "gilded_bonded_vault": case "gilded_auction": case "gilded_convoy": case "gilded_quarantine":
                case "moonlit_dawn": case "moonlit_dusk": case "moonlit_midnight": case "moonlit_cleanse":
                case "moonlit_choir": case "moonlit_shadow_dial": case "moonlit_star_current": case "moonlit_moon_well":
                    kind = EncounterDiscoveryKind.SightedLocation;
                    return true;
                default:
                    kind = default(EncounterDiscoveryKind);
                    return false;
            }
        }

        private static CoreEncounter Core(string name, string description, params CoreEncounterChoice[] choices) =>
            new CoreEncounter(CoreTreasureHuntCatalog.CoreEncounterId, name, "秘藏", description, true, true, 4, 0, choices);

        private static CoreEncounterChoice ReturnToChart() => C("标记位置，继续探索", "返回海图，可再次靠近此处；藏宝图不会返还。", 0,
            new VoyageEffect { PostponeTreasureCore = true });

        private static IReadOnlyList<CoreEncounter> Weathered()
        {
            return Array.AsReadOnly(new[] {
                E("weathered_winch", "断链绞盘台", "工程", "老绞盘能拉起封死的舱盖。修复后随船携带，本次海图中的吊装作业都可使用。",
                    C("备料修复绞盘", "木材 -6、铁矿 -4；获得吊装绞盘。", 2,
                        new VoyageEffect { Wood = -6, Iron = -4, TreasureMark = "weathered_winch" }),
                    C("木匠重接棘轮", "需要木匠；获得吊装绞盘。", 1,
                        new VoyageEffect { TreasureMark = "weathered_winch" }, crew: "carpenter"),
                    C("拆旧架慢慢修复", "不额外消耗材料；获得吊装绞盘，仍消耗全员口粮。", 6,
                        new VoyageEffect { TreasureMark = "weathered_winch" })),
                E("weathered_bell", "倒扣潜水钟", "潜水装备", "岸边的铜钟仍能隔绝海水。带上它，才能进入沉船深处的密封舱。",
                    C("修补铜钟与气管", "铁矿 -4、布料 -3；获得潜水钟。", 2,
                        new VoyageEffect { Iron = -4, Cloth = -3, TreasureMark = "weathered_bell" }),
                    C("潜水员检修气阀", "需要潜水员；获得潜水钟。", 1,
                        new VoyageEffect { TreasureMark = "weathered_bell" }, crew: "diver"),
                    C("收集旧管逐段替换", "不额外消耗材料；获得潜水钟，仍消耗全员口粮。", 6,
                        new VoyageEffect { TreasureMark = "weathered_bell" })),
                E("weathered_camp", "潮工营地", "整备", "废弃营地留下整套修复原料，也能改作补给站。选好本次远行最需要的支援。",
                    C("整备两件工具的原料", "木材 +6、铁矿 +8、布料 +3；建立潮工营地。", 2,
                        new VoyageEffect { Wood = 6, Iron = 8, Cloth = 3, TreasureMark = "weathered_camp" }),
                    C("整理口粮与修船床", "食物 +45、淡水 +45、船体 +12；建立潮工营地。", 3,
                        new VoyageEffect { Food = 45, Water = 45, Hull = 12, TreasureMark = "weathered_camp" })),
                E("weathered_chain_gate", "横海沉链", "通航工程", "两艘旧船之间的铁链挡住了顺流航道。绞盘能把障碍变成航行优势。",
                    C("用绞盘清开水道", "需要吊装绞盘；顺流加速 18 小时、铁矿 +8。", 2,
                        new VoyageEffect { SailingBoostHours = 18, Iron = 8 }, mark: "weathered_winch"),
                    C("沿链逐段拆除", "船体 -4、铁矿 +4；不需工具。", 5, new VoyageEffect { Hull = -4, Iron = 4 }),
                    C("绕过链阵", "安全绕行，消耗时间口粮。", 2, new VoyageEffect())),
                E("weathered_keel_vault", "龙骨封舱", "重型打捞", "船腹被整根龙骨压住。吊起龙骨只能打开入口，潜入内匣还需要潜水钟。",
                    C("双工具取出完整机芯", "需要绞盘与潜水钟；古代材料 +5、古代罗盘 +1。", 3,
                        new VoyageEffect { AncientMaterial = 5, AncientCompass = 1 }, mark: "weathered_winch+weathered_bell"),
                    C("绞盘拉出外层铜架", "需要绞盘；铁矿 +10、古代材料 +2。", 2,
                        new VoyageEffect { Iron = 10, AncientMaterial = 2 }, mark: "weathered_winch"),
                    C("只拾取散落铆钉", "铁矿 +5；完整机芯留在水下。", 1, new VoyageEffect { Iron = 5 })),
                E("weathered_glass_dome", "玻璃穹顶舱", "密舱考察", "玻璃后是一间保存完整的领航室。水压很高，砸开穹顶会毁掉其中的图纸。",
                    C("潜水钟内抄录图纸", "需要潜水钟；航海灯 +1、远眺视野 18 小时。", 3,
                        new VoyageEffect { Lantern = 1, SurveyBoostHours = 18 }, mark: "weathered_bell"),
                    C("拆取外侧金属环", "铁矿 +6、布料 +3。", 2, new VoyageEffect { Iron = 6, Cloth = 3 })),
                E("weathered_supply_barge", "卡礁补给驳船", "补给打捞", "密封补给箱沉在驳船下层。吊装设备能将整箱提出水面，徒手只能搬走甲板货物。",
                    C("绞盘整箱吊运", "需要绞盘；食物 +70、淡水 +70、药品 +1。", 2,
                        new VoyageEffect { Food = 70, Water = 70, Medicine = 1 }, mark: "weathered_winch"),
                    C("搬走甲板补给", "食物 +30、淡水 +30。", 3, new VoyageEffect { Food = 30, Water = 30 })),
                E("weathered_rustyard", "锈锚拆船场", "材料加工", "拆船架上的旧铁可铸成构件，也能卖掉。营地炉具可以把普通废料炼成稀有修复料。",
                    C("借营地炉具重铸", "需要潮工营地；铁矿 -6、古代材料 +3。", 4,
                        new VoyageEffect { Iron = -6, AncientMaterial = 3 }, mark: "weathered_camp"),
                    C("拆走可用梁材", "木材 +10、铁矿 +6。", 4, new VoyageEffect { Wood = 10, Iron = 6 }),
                    C("卖给过路拆船人", "金币 +90。", 1, new VoyageEffect { Gold = 90 })),
                E("weathered_tide_turbine", "停转潮轮", "动力修复", "潮轮曾为整片船坞引流。恢复动力能把回头路变成顺水路，拆掉则能立刻带走零件。",
                    C("用绞盘重启潮轮", "需要绞盘；木材 -3、顺流加速 24 小时。", 3,
                        new VoyageEffect { Wood = -3, SailingBoostHours = 24 }, mark: "weathered_winch"),
                    C("拆下潮轮轴承", "古代材料 +2、铁矿 +5。", 4, new VoyageEffect { AncientMaterial = 2, Iron = 5 })),
                E("weathered_signal_buoy", "沉没测深浮标", "测绘", "浮标下的刻度盘记着沉船位置。有人必须潜入底座，让测深装置重新浮起。",
                    C("潜水钟扶正刻度盘", "需要潜水钟；显示 2 个隐藏点、远眺视野 12 小时。", 2,
                        new VoyageEffect { Reveal = 2, SurveyBoostHours = 12 }, mark: "weathered_bell"),
                    C("航海士重新测深", "需要航海士；远眺视野 12 小时。", 3,
                        new VoyageEffect { SurveyBoostHours = 12 }, crew: "navigator"),
                    C("收走浮标灯具", "航海灯 +1。", 2, new VoyageEffect { Lantern = 1 })),
                E("weathered_broken_caisson", "裂缝沉箱", "船体险情", "一只工事沉箱被水压挤裂。潜水钟能让船员从内侧封住裂缝，也可花材料架设临时支撑。",
                    C("潜水钟进入内侧封堵", "需要潜水钟；船体 +14、古代材料 +2。", 3,
                        new VoyageEffect { Hull = 14, AncientMaterial = 2 }, mark: "weathered_bell"),
                    C("架木回收沉箱", "木材 -4、船体 +8、铁矿 +5。", 3, new VoyageEffect { Wood = -4, Hull = 8, Iron = 5 }),
                    C("放弃沉箱驶离", "安全离开。", 1, new VoyageEffect())),
                E("weathered_cartographer_hulk", "测绘师遗船", "航海遗产", "遗船上的罗盘仍连着沉重的观测架。取走仪器或保留航道记录，决定这次发现能如何帮助远航。",
                    C("完整吊出观测架", "需要绞盘；古代罗盘 +1、古代材料 +2。", 3,
                        new VoyageEffect { AncientCompass = 1, AncientMaterial = 2 }, mark: "weathered_winch"),
                    C("抄录近岸安全航线", "顺流加速 12 小时、声望 +6。", 2,
                        new VoyageEffect { SailingBoostHours = 12, Fame = 6 })),
                Core("沉船工坊主机舱", "主机舱仍是完整的古代工坊。绞盘打开上层，潜水钟保护精密构件；两件工具齐备，才能把工坊的遗产完整带走。",
                    C("双工具完整复原工坊", "需要绞盘与潜水钟；古代材料 +10、古代罗盘 +1、航海灯 +1、金币 +180；完成寻宝。", 5,
                        new VoyageEffect { AncientMaterial = 10, AncientCompass = 1, Lantern = 1, Gold = 180, TreasureComplete = true },
                        mark: "weathered_winch+weathered_bell"),
                    C("粗拆外壳后撤离", "船体 -8；古代材料 +4、铁矿 +12、金币 +100；放弃精密构件，完成寻宝。", 5,
                        new VoyageEffect { Hull = -8, AncientMaterial = 4, Iron = 12, Gold = 100, TreasureComplete = true }),
                    ReturnToChart())
            });
        }

        private static IReadOnlyList<CoreEncounter> Gilded()
        {
            return Array.AsReadOnly(new[] {
                E("gilded_manifest", "公证浮台", "契约登记", "商盟遗产只认登记过的货单。登记获得合法领取资格；偷改名册则能快速借来信用，也会留下通缉记录。",
                    C("登记原始货单", "取得公证货单、信用 +1。", 2,
                        new VoyageEffect { TreasureMark = "gilded_manifest", TreasureCredit = 1 }),
                    C("伪造优先货主身份", "取得公证货单、信用 +3、通缉 +2。", 1,
                        new VoyageEffect { TreasureMark = "gilded_manifest", TreasureCredit = 3, TreasureHeat = 2 })),
                E("gilded_witness", "失踪押运员", "证人救援", "押运员躲在漂流艇中。他的证词能兑换商盟信用，也知道绕过封存仓巡查的暗道。",
                    C("接济并保护押运员", "食物 -12、淡水 -12；获得证人证词、信用 +2、声望 +8。", 2,
                        new VoyageEffect { Food = -12, Water = -12, TreasureMark = "gilded_witness", TreasureCredit = 2, Fame = 8 }),
                    C("慢慢拖艇返回安全水域", "无额外物资要求；获得证人证词、信用 +2。", 6,
                        new VoyageEffect { TreasureMark = "gilded_witness", TreasureCredit = 2 }),
                    C("买下暗道口述", "金币 -60；获得证人证词、通缉 +1。", 1,
                        new VoyageEffect { Gold = -60, TreasureMark = "gilded_witness", TreasureHeat = 1 })),
                E("gilded_relief", "赈济交割船", "商盟委托", "交割船缺少能立刻发放的补给。信用可在封存仓与沿途仓库之间分配，留足三点才能合法领取核心遗产。",
                    C("交付赈济口粮", "食物 -20、淡水 -20；信用 +3。", 2,
                        new VoyageEffect { Food = -20, Water = -20, TreasureCredit = 3 }),
                    C("协助搬运等待核销", "不额外消耗物资；信用 +3，仍消耗全员口粮。", 6,
                        new VoyageEffect { TreasureCredit = 3 }),
                    C("挪走无主货箱", "食物 +50、淡水 +50、通缉 +2。", 1,
                        new VoyageEffect { Food = 50, Water = 50, TreasureHeat = 2 })),
                E("gilded_customs", "巡查登记船", "通缉处理", "巡查员允许主动核验身份。清除通缉能恢复合法领取资格，耗费的时间却必须由全船承担。",
                    C("持货单申请核验", "需要公证货单；清除至多 5 点通缉。", 2,
                        new VoyageEffect { TreasureHeat = -5 }, mark: "gilded_manifest"),
                    C("接受完整搜检", "不需要证件或金币；清除至多 5 点通缉。", 7,
                        new VoyageEffect { TreasureHeat = -5 }),
                    C("贿赂巡查员离开", "金币 -80、通缉 -2。", 1, new VoyageEffect { Gold = -80, TreasureHeat = -2 })),
                E("gilded_bonded_vault", "海上保税仓", "信用兑换", "仓管愿意用信用交付药材，私下搬走珍宝也有门路。花掉信用后，核心遗产可能需要另一种取法。",
                    C("凭信用领取救急包", "信用 -2；药品 +3、航海灯 +1；通缉不高于 1。", 1,
                        new VoyageEffect { TreasureCredit = -2, Medicine = 3, Lantern = 1 }, credit: 2, heat: 1),
                    C("绕后搬走珠宝匣", "宝石 +2、金币 +120、通缉 +2。", 3,
                        new VoyageEffect { Gem = 2, Gold = 120, TreasureHeat = 2 }),
                    C("只核对仓号", "声望 +4。", 1, new VoyageEffect { Fame = 4 })),
                E("gilded_black_market", "无旗暗市", "走私交易", "无旗商人只付现钱。他们愿意高价买走公证货单的抄本，却会让整片海域知道你的船名。",
                    C("出售货单抄本", "需要公证货单；金币 +260、通缉 +2。", 2,
                        new VoyageEffect { Gold = 260, TreasureHeat = 2 }, mark: "gilded_manifest"),
                    C("购买密封补给", "金币 -80；食物 +45、淡水 +45；通缉 +1。", 1,
                        new VoyageEffect { Gold = -80, Food = 45, Water = 45, TreasureHeat = 1 }),
                    C("拒绝交易", "不改变信用或通缉。", .5, new VoyageEffect())),
                E("gilded_counterfeit", "伪币铸印船", "揭伪", "铸印船用假章冒领赈济。证词可以揭穿他们，默许交易则能分到一笔黑钱。",
                    C("让证人指认假章", "需要证人证词；信用 +2、通缉 -1、声望 +10。", 2,
                        new VoyageEffect { TreasureCredit = 2, TreasureHeat = -1, Fame = 10 }, mark: "gilded_witness"),
                    C("收下封口钱", "金币 +180、通缉 +2。", 1, new VoyageEffect { Gold = 180, TreasureHeat = 2 }),
                    C("抄下印章破绽", "信用 +1。", 4, new VoyageEffect { TreasureCredit = 1 })),
                E("gilded_auction", "封缄拍卖艇", "竞价", "拍卖艇接受信用优先购货，也收普通金币。一盏可靠的航海灯能帮助后面的远航。",
                    C("以信用竞得领航套具", "信用 -1、金币 -40；航海灯 +1、远眺视野 18 小时。", 1,
                        new VoyageEffect { TreasureCredit = -1, Gold = -40, Lantern = 1, SurveyBoostHours = 18 }, credit: 1),
                    C("现钱购买领航套具", "金币 -120；航海灯 +1、远眺视野 18 小时。", 1,
                        new VoyageEffect { Gold = -120, Lantern = 1, SurveyBoostHours = 18 }),
                    C("留下协助清点拍品", "信用 +1。", 4, new VoyageEffect { TreasureCredit = 1 })),
                E("gilded_convoy", "逆风商队", "护航契约", "商队等着一位可靠的领航者。公开护航能修复商盟评价，暗中搬货则能更快填满钱袋。",
                    C("引导商队穿过逆风口", "信用 +1、通缉 -1、顺流加速 12 小时。", 4,
                        new VoyageEffect { TreasureCredit = 1, TreasureHeat = -1, SailingBoostHours = 12 }),
                    C("趁换帆取走贵重箱", "金币 +140、宝石 +1、通缉 +2。", 1,
                        new VoyageEffect { Gold = 140, Gem = 1, TreasureHeat = 2 })),
                E("gilded_ledger_buoy", "海底账册浮标", "审计线索", "密封账册记录着商盟旧债。把它与公证货单对照能抵销自己的通缉，也可送回商盟换信用。",
                    C("对照货单核销旧案", "需要公证货单；通缉 -2、金币 +70。", 3,
                        new VoyageEffect { TreasureHeat = -2, Gold = 70 }, mark: "gilded_manifest"),
                    C("递交完整账册", "信用 +2。", 3, new VoyageEffect { TreasureCredit = 2 }),
                    C("卖掉账册暗码", "金币 +150、通缉 +1。", 1, new VoyageEffect { Gold = 150, TreasureHeat = 1 })),
                E("gilded_insurance", "共济修理船", "保险理赔", "持有货单的船可以用信用申请修理。若声名狼藉，修理船只肯收现钱并从头检查货舱。",
                    C("申请共济理赔", "需要公证货单、通缉不高于 1；信用 -1、船体 +24。", 2,
                        new VoyageEffect { TreasureCredit = -1, Hull = 24 }, mark: "gilded_manifest", credit: 1, heat: 1),
                    C("支付现钱修船", "金币 -70、船体 +16。", 3, new VoyageEffect { Gold = -70, Hull = 16 }),
                    C("帮工换取木料", "木材 +8、布料 +4。", 4, new VoyageEffect { Wood = 8, Cloth = 4 })),
                E("gilded_quarantine", "黄旗检疫泊位", "救济抉择", "检疫船缺药，仓内却封存着高价货。公开救济与秘密卸货会把这次寻宝引向不同的结局。",
                    C("捐药换取商盟担保", "药品 -1；信用 +2、通缉 -2。", 2,
                        new VoyageEffect { Medicine = -1, TreasureCredit = 2, TreasureHeat = -2 }),
                    C("承担无人愿做的消杀", "信用 +1、声望 +8。", 5, new VoyageEffect { TreasureCredit = 1, Fame = 8 }),
                    C("取走封存珍珠", "黑珍珠 +1、诅咒 +1、通缉 +2。", 2,
                        new VoyageEffect { BlackPearl = 1, Curse = 1, TreasureHeat = 2 }, maximumCurse: 9)),
                Core("商盟封存总仓", "封存总仓保管着不同货主的遗产。信用与清白身份可领取合法份额；证人的暗道则通向来历不明的黑货。",
                    C("凭公证与信用领取遗产", "需公证货单、信用 3、通缉不高于 1；信用 -3，金币 +480、宝石 +2、药品 +3、声望 +30；完成寻宝。", 3,
                        new VoyageEffect { TreasureCredit = -3, Gold = 480, Gem = 2, Medicine = 3, Fame = 30, TreasureComplete = true },
                        mark: "gilded_manifest", credit: 3, heat: 1),
                    C("循证人暗道运走黑货", "需证人证词；金币 +650、宝石 +3、黑珍珠 +1；船体 -18、诅咒 +3、通缉 +3，带伤完成寻宝。", 5,
                        new VoyageEffect { Gold = 650, Gem = 3, BlackPearl = 1, Hull = -18, Curse = 3, TreasureHeat = 3, TreasureComplete = true },
                        mark: "gilded_witness", maximumCurse: 7),
                    C("领取外仓无主货后撤离", "金币 +160、食物 +40、淡水 +40；放弃封存遗产，完成寻宝。", 4,
                        new VoyageEffect { Gold = 160, Food = 40, Water = 40, TreasureComplete = true }),
                    ReturnToChart())
            });
        }

        private static CoreEncounter Altar(string id, string name, string bandName, CoreVoyageTimeBand band) =>
            E(id, name, "月相仪式", "祭坛只在" + bandName + "回应。等待能保全船员，强行点亮则会将诅咒带上船；每座祭坛只能贡献一次共鸣。",
                C("顺应" + bandName + "点亮祭坛", "仅限" + bandName + "；点亮此相、共鸣 +1，不增加诅咒。", 1,
                    new VoyageEffect { TreasureMark = id, TreasureResonance = 1 }, time: band),
                C("以血强行点亮祭坛", "点亮此相、共鸣 +1、诅咒 +1。", 2,
                    new VoyageEffect { TreasureMark = id, TreasureResonance = 1, Curse = 1 }, maximumCurse: 9),
                C("等候时辰后再访", "等待 3 小时，仅消耗全员口粮；祭坛保留，可再次进入。", 3,
                    new VoyageEffect { PostponeTreasureCore = true }));

        private static IReadOnlyList<CoreEncounter> Moonlit()
        {
            return Array.AsReadOnly(new[] {
                Altar("moonlit_dawn", "昼辉祭坛", "白昼", CoreVoyageTimeBand.Day),
                Altar("moonlit_dusk", "暮潮祭坛", "黄昏", CoreVoyageTimeBand.Dusk),
                Altar("moonlit_midnight", "夜幕祭坛", "夜间", CoreVoyageTimeBand.Night),
                E("moonlit_cleanse", "洗月圣泉", "仪式净化", "泉水能洗去强行唤醒祭坛留下的印记。先完成冒险，再来净化，可以保住完整仪式的资格。",
                    C("守泉洗去三重印记", "清除至多 3 层诅咒；不额外消耗材料。", 6, new VoyageEffect { Curse = -3 }, minimumCurse: 1),
                    C("燃尽航海灯净化", "航海灯 -1；清除至多 5 层诅咒。", 1,
                        new VoyageEffect { Lantern = -1, Curse = -5 }, minimumCurse: 1),
                    C("记下泉位，稍后再访", "返回海图并保留地点，之后可来净化。", 0,
                        new VoyageEffect { PostponeTreasureCore = true })),
                E("moonlit_mirror_pool", "倒月镜池", "映月观测", "镜中出现海面之外的星位。昼辉能稳定倒影；没有点亮祭坛，船员只能听见令人不安的低语。",
                    C("借昼辉辨认倒影", "需要昼辉相；远眺视野 24 小时、显示 2 个隐藏点。", 2,
                        new VoyageEffect { SurveyBoostHours = 24, Reveal = 2 }, mark: "moonlit_dawn"),
                    C("触碰镜中月影", "远眺视野 12 小时、诅咒 +1。", 1,
                        new VoyageEffect { SurveyBoostHours = 12, Curse = 1 }, maximumCurse: 9),
                    C("沿池岸采集淡水", "淡水 +45。", 3, new VoyageEffect { Water = 45 })),
                E("moonlit_choir", "无声唱诗礁", "潮音共振", "暮潮点亮后，礁缝中无声的合唱才有了旋律。和声能够安抚船员，也能被强行折成一件遗物。",
                    C("随暮潮完成和声", "需要暮潮相；士气 +2，清除至多 1 层诅咒。", 2,
                        new VoyageEffect { Morale = 2, Curse = -1 }, mark: "moonlit_dusk"),
                    C("折取共鸣骨笛", "稀有遗物 +1、诅咒 +1、士气 -1。", 3,
                        new VoyageEffect { Relic = 1, Curse = 1, Morale = -1 }, maximumCurse: 9),
                    C("安静听完潮声", "士气 +1。", 4, new VoyageEffect { Morale = 1 })),
                E("moonlit_shadow_dial", "逆行影晷", "时相解谜", "影晷上的指针逆着月影转动。夜幕相能使它停下，露出古代航海仪的内层。",
                    C("以夜幕相止住指针", "需要夜幕相；古代罗盘 +1、远眺视野 12 小时。", 2,
                        new VoyageEffect { AncientCompass = 1, SurveyBoostHours = 12 }, mark: "moonlit_midnight"),
                    C("记下指针经过的礁影", "显示 1 个隐藏点。", 3, new VoyageEffect { Reveal = 1 }),
                    C("拆掉表面银饰", "金币 +100、诅咒 +1。", 1,
                        new VoyageEffect { Gold = 100, Curse = 1 }, maximumCurse: 9)),
                E("moonlit_pearl_moon", "黑月蚌床", "月潮采集", "黑珍珠随月潮浮出蚌壳。两相共鸣让采集者不必伸手进入蚌群，急取则会惊动水下的影子。",
                    C("以双相共鸣引珠", "共鸣至少 2；黑珍珠 +2，不增加诅咒。", 3,
                        new VoyageEffect { BlackPearl = 2 }, resonance: 2),
                    C("伸手强取黑珍珠", "黑珍珠 +1、诅咒 +1、船体 -4。", 1,
                        new VoyageEffect { BlackPearl = 1, Curse = 1, Hull = -4 }, maximumCurse: 9),
                    C("只捡浅滩食蚌", "食物 +45。", 3, new VoyageEffect { Food = 45 })),
                E("moonlit_star_current", "星屑潮汐", "仪式航路", "共鸣越深，海流中的星屑越接近真实航道。点亮三相后，整条星潮都可以为船队引路。",
                    C("三相引导星潮", "共鸣 3；顺流加速 24 小时、远眺视野 12 小时。", 2,
                        new VoyageEffect { SailingBoostHours = 24, SurveyBoostHours = 12 }, resonance: 3),
                    C("借一相辨认潮线", "共鸣至少 1；顺流加速 12 小时。", 3,
                        new VoyageEffect { SailingBoostHours = 12 }, resonance: 1),
                    C("沿外围缓慢通过", "安全通过，消耗时间口粮。", 4, new VoyageEffect())),
                E("moonlit_echo_library", "回声藏书窟", "古卷解读", "书页只在两个时相交叠时显形。白昼与暮潮的印记齐备，才能将一部完整古卷带出水面。",
                    C("合昼暮两相取出古卷", "需要昼辉相与暮潮相；稀有遗物 +1、声望 +12。", 3,
                        new VoyageEffect { Relic = 1, Fame = 12 }, mark: "moonlit_dawn+moonlit_dusk"),
                    C("抄录残页上的星位", "远眺视野 12 小时、声望 +4。", 3,
                        new VoyageEffect { SurveyBoostHours = 12, Fame = 4 }),
                    C("取走封卷黑珠", "黑珍珠 +1、诅咒 +1。", 1,
                        new VoyageEffect { BlackPearl = 1, Curse = 1 }, maximumCurse: 9)),
                E("moonlit_sleeping_guardian", "沉睡守月像", "仪式试炼", "石像怀中护着一枚月核。平静的双相共鸣可以让它张开手掌；诅咒也能唤醒它，却会留下更深的印记。",
                    C("以无瑕双相请求月核", "共鸣至少 2、诅咒不高于 1；稀有遗物 +1。", 4,
                        new VoyageEffect { Relic = 1 }, resonance: 2, maximumCurse: 1),
                    C("让诅咒唤醒石像", "需要至少 1 层诅咒；黑珍珠 +2、诅咒 +1。", 2,
                        new VoyageEffect { BlackPearl = 2, Curse = 1 }, minimumCurse: 1, maximumCurse: 9),
                    C("留在外围刻录石像", "声望 +8。", 3, new VoyageEffect { Fame = 8 })),
                E("moonlit_moon_well", "不落月井", "月泉整备", "月井中的水映着永不落下的月亮。夜幕相能分出清水与银砂，没有印记也可取走井边的普通补给。",
                    C("借夜幕相分离银砂", "需要夜幕相；黑珍珠 +1、淡水 +60。", 3,
                        new VoyageEffect { BlackPearl = 1, Water = 60 }, mark: "moonlit_midnight"),
                    C("搬走井边补给", "食物 +35、淡水 +35、药品 +1。", 3,
                        new VoyageEffect { Food = 35, Water = 35, Medicine = 1 })),
                Core("三相月蚀圣所", "三座祭坛的回声汇入圣所。完整且洁净的共鸣能唤醒遗物；不完整的仪式会留下黑珍珠与更深的诅咒。",
                    C("完成洁净三相仪式", "需三相齐备、共鸣 3、诅咒不高于 1；稀有遗物 +3、黑珍珠 +2、古代罗盘 +1；完成寻宝。", 4,
                        new VoyageEffect { Relic = 3, BlackPearl = 2, AncientCompass = 1, TreasureComplete = true },
                        mark: "moonlit_dawn+moonlit_dusk+moonlit_midnight", resonance: 3, maximumCurse: 1),
                    C("以双相强开月蚀裂隙", "共鸣至少 2；稀有遗物 +1、黑珍珠 +3、诅咒 +2；完成寻宝。", 2,
                        new VoyageEffect { Relic = 1, BlackPearl = 3, Curse = 2, TreasureComplete = true }, resonance: 2, maximumCurse: 8),
                    C("取走外坛祭品后撤离", "黑珍珠 +1、古代材料 +2；放弃圣所遗物，完成寻宝。", 3,
                        new VoyageEffect { BlackPearl = 1, AncientMaterial = 2, TreasureComplete = true }),
                    ReturnToChart())
            });
        }
    }
}
