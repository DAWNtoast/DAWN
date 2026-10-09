using System;
using System.Collections.Generic;
using A = ShatteredSail.Core.Gameplay.NavalBattleCardAction;
using S = ShatteredSail.Core.Gameplay.NavalBattleStages;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreSpecialCardSourceKind { Crew, Component }

    public sealed class CoreSpecialCardDefinition
    {
        public CoreSpecialCardDefinition(CoreBattleCard card, string sourceId, string sourceName,
            string lore, string storyId, CoreSpecialCardSourceKind sourceKind)
        {
            Card = card;
            SourceId = sourceId;
            SourceName = sourceName;
            Lore = lore;
            StoryId = storyId;
            SourceKind = sourceKind;
        }

        public CoreBattleCard Card { get; }
        public string SourceId { get; }
        public string SourceName { get; }
        public string Lore { get; }
        public string StoryId { get; }
        public CoreSpecialCardSourceKind SourceKind { get; }
        // Only migrates the retired generic card family, never another identity's XP.
        public string LegacySourceKey
        {
            get
            {
                if (SourceKind == CoreSpecialCardSourceKind.Component)
                {
                    int separator = SourceId.IndexOf('_');
                    return separator > 0 ? SourceId.Substring(0, separator) : string.Empty;
                }
                if (SourceId.IndexOf("_navigator_", StringComparison.Ordinal) >= 0) return "navigator";
                if (SourceId.IndexOf("_carpenter_", StringComparison.Ordinal) >= 0) return "carpenter";
                if (SourceId.IndexOf("_doctor_", StringComparison.Ordinal) >= 0) return "doctor";
                if (SourceId.IndexOf("_gunner_", StringComparison.Ordinal) >= 0) return "gunner";
                if (SourceId.IndexOf("_diver_", StringComparison.Ordinal) >= 0) return "diver";
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Authored combat expressions of existing identities and equipment stories.
    /// Core cannot depend on Content or Unity Resources. These stable records are
    /// checked against CrewNarrativeCatalog and component_catalog_v2.csv in tests.
    /// Legacy aliases resolve identities, never create extra collectible cards.
    /// </summary>
    public static class CoreSpecialCardCatalog
    {
        private static readonly IReadOnlyList<CoreSpecialCardDefinition> all = Build();
        private static readonly Dictionary<string, CoreSpecialCardDefinition> byCard = Index(false);
        private static readonly Dictionary<string, CoreSpecialCardDefinition> bySource = Index(true);

        public static IReadOnlyList<CoreSpecialCardDefinition> All => all;

        public static CoreSpecialCardDefinition FindByCardId(string id) =>
            !string.IsNullOrWhiteSpace(id) && byCard.TryGetValue(id, out CoreSpecialCardDefinition value) ? value : null;

        public static CoreSpecialCardDefinition ForComponent(string id) => ForSource(id, CoreSpecialCardSourceKind.Component);

        public static CoreSpecialCardDefinition ForCrew(string id)
        {
            switch (id)
            {
                case "navigator": case "crew_mad_navigator": case "crew_navigator_evelyn": case "tavern_crew_0":
                    id = "crew_gray_gull_navigator_evelyn"; break;
                case "carpenter": case "crew_old_carpenter": case "crew_carpenter_barlow": case "tavern_crew_1":
                    id = "crew_gray_gull_carpenter_barlow"; break;
                case "doctor": case "crew_cold_doctor": case "crew_doctor_moer": case "tavern_crew_2":
                    id = "crew_gray_gull_doctor_moer"; break;
                case "gunner": case "crew_one_eye_gunner": case "crew_gunner_leisa": case "tavern_crew_3":
                    id = "crew_gray_gull_gunner_leisa"; break;
                case "diver": case "crew_treasure_diver": case "crew_diver_noya": case "tavern_crew_5":
                    id = "crew_gray_gull_diver_noya"; break;
            }
            return ForSource(id, CoreSpecialCardSourceKind.Crew);
        }

        private static CoreSpecialCardDefinition ForSource(string id, CoreSpecialCardSourceKind kind) =>
            !string.IsNullOrWhiteSpace(id) && bySource.TryGetValue(id, out CoreSpecialCardDefinition value) && value.SourceKind == kind ? value : null;

        private static Dictionary<string, CoreSpecialCardDefinition> Index(bool source)
        {
            var result = new Dictionary<string, CoreSpecialCardDefinition>(StringComparer.Ordinal);
            foreach (CoreSpecialCardDefinition definition in all)
                result.Add(source ? definition.SourceId : definition.Card.Id, definition);
            return result;
        }

        private static IReadOnlyList<CoreSpecialCardDefinition> Build()
        {
            var cards = new List<CoreSpecialCardDefinition>();

            // Six ports keep distinct tactical signatures as well as faces and histories.
            C(cards, "crew_broken_anchor_navigator_lorne", "测潮师 洛恩", "断链退潮线", 1, "洛恩摸着袖中守夜人的潮汐记录，命船队退进残骸之间。暴潮从空出的航道卷过，他却在乱浪背后看见无名船归来的路。", A.Retreat, block: 6, draw: 1);
            C(cards, "crew_broken_anchor_carpenter_kia", "铆骨匠 琪娅", "沉木铆骨", 7, "琪娅把会跳动的刨花压进裂开的舱壁，铜铆钉随心跳一枚枚落下。等敌舰再次装好炮弹，沉木已经把整条伤口咬住。", A.Standard, block: 9, repair: 5);
            C(cards, "crew_broken_anchor_gunner_mira", "沉钟炮师 米拉", "六钟断链弹", 9, "米拉按六座港钟的回声分好装药。炮口不追逐飘摇的旗帜，只等巨链最薄的铆口经过同一条瞄线；钟声未落，第一弹已出膛。", A.Cannon, S.Naval, damage: 14, block: 3);
            C(cards, "crew_broken_anchor_diver_udo", "锚墓潜手 乌铎", "锚墓凿击", 11, "乌铎沿着系锚索潜到船底，摸索时仍惦记铜牌旁那个年老的自己。他割开最紧的一束木缝便转身，绝不在水下等一个答案。", A.Sabotage, S.Naval, damage: 12, block: 2);

            C(cards, "crew_white_shell_navigator_saina", "珊潮领航员 赛娜", "白沫借道", 2, "赛娜从银壳的求救声里辨出两次浪拍。她让船顺着第二道白沫切向侧舷，第一次把隐藏的坐标用在了一场近在眼前的海战。", A.Turn, block: 4, draw: 1);
            C(cards, "crew_white_shell_carpenter_atang", "贝骨船匠 阿棠", "珊胶续枝", 7, "阿棠把珊瑚胶抹在活龙骨的护符上，枝杈便沿着裂缝舒展。她记下新枝偏转的方向：修好的船仍想驶向地图空白处。", A.Standard, repair: 10, draw: 1);
            C(cards, "crew_white_shell_gunner_gallo", "珠炮手 加洛", "第十三次跳弹", 3, "加洛记得珠炮第一次试射时的十三道弹痕。他把小炮抬低半寸，借浪面再送出一记跳弹，随即在试炮纸上补上下一轮的落点。", A.Cannon, S.Naval, damage: 10, draw: 1);
            C(cards, "crew_white_shell_diver_kela", "采珠人 珂拉", "银壳回响", 2, "珂拉听见母亲的声音又从敌舰底下响起。她凿开舱板寻找银壳的回声，浮出海面时没有解释，只把摸清的船底结构交给同伴。", A.Sabotage, S.Naval, damage: 8, draw: 1);

            C(cards, "crew_bronze_bell_navigator_vick", "黄铜测绘师 维克", "十三分仪解算", 12, "维克拨动六分仪第十三枚齿轮，让父亲未译完的密文对上敌舰航线。两个不同年代的坐标重合，己舰恰好抢进尾舷。", A.Navigator, block: 4);
            C(cards, "crew_bronze_bell_carpenter_osser", "齿轮船匠 奥瑟", "钟摆校骨", 7, "奥瑟曾想用钢架囚住活龙骨，如今只让齿轮顺着木纹摆动。他把甲板的支点校到同一节拍，船身终于不再抗拒每次转舵。", A.Polish, repair: 4);
            C(cards, "crew_bronze_bell_gunner_helen", "钟楼炮官 赫莲", "第十三响校射", 3, "赫莲没有急着点火。第十二声钟响之后，她听见失踪舰队在风中报距，于是把最后一格装药轻轻压入旧炮，等那不存在的第十三响。", A.PrecisionLoad, block: 4);
            C(cards, "crew_bronze_bell_diver_duke", "铜盔潜工 杜克", "钟压拆舱", 11, "杜克循着潜水钟的工人暗号抵达敌舰龙骨。铜盔替他扛住水压，配重靴把身体钉稳；他用修海底闸门的工具卸下第一道舱板。", A.Sabotage, S.Naval, damage: 8, block: 6);

            C(cards, "crew_salt_crown_navigator_ashir", "盐路航海士 阿希尔", "六印晶风", 4, "阿希尔把盐船账册里的六个港名写上罗盘。看清盐雾里自己的倒影后，他反而不再犹豫，沿星点之间的缝隙带船绕向敌舰。", A.Circle, block: 5, draw: 1);
            C(cards, "crew_salt_crown_carpenter_saf", "晒骨匠 萨芙", "白骨封缝", 7, "萨芙把晒出的返航日期留在舱内，只将盐蜡敲进外侧裂口。最后一行仍然空白，她说修船就是替那一天保留落笔的地方。", A.Standard, block: 5, repair: 9);
            C(cards, "crew_salt_crown_gunner_rokan", "白焰炮手 罗坎", "盐硝白焰", 9, "罗坎循着已故老师的手势重配盐硝，把白焰藏进待发的炮弹。他不再追问火光里的人从何而来，只求下一炮照亮潮门的铆口。", A.FireRounds, block: 2);
            C(cards, "crew_salt_crown_diver_isha", "盐井潜客 伊沙", "井路侧袭", 11, "伊沙沿晶灯找到狭窄的舷门，像走过连通六港的井路那样钻了进去。门闩落下的一瞬，她的短刀已替后来的同伴清出一个身位。", A.Standard, S.Boarding, damage: 10, block: 4);

            C(cards, "crew_twilight_tide_navigator_selene", "月潮占星师 塞勒涅", "巨背星座", 6, "塞勒涅依次摸过石盘上如伤痕的刻线。她记得海巨人抬肩前海水怎样倾斜，于是让舵手提前转入那道仍未形成的浪谷。", A.Navigator, draw: 1);
            C(cards, "crew_twilight_tide_carpenter_orlan", "骨舟匠 欧兰", "鲸骨回家谣", 7, "欧兰伏在龙骨旁听完那句回家，才把弯月骨锯收起来。他让新骨顺着旧骨的声音贴合，舱壁停止呻吟，同伴也听清了下一道指令。", A.Standard, repair: 7, block: 3, draw: 1);
            C(cards, "crew_twilight_tide_gunner_yilan", "蓝火炮巫 伊岚", "静歌蓝焰", 5, "伊岚把击败弥赛拉时留下的回音咒封入空心弹。蓝焰只随真正命中的炮火燃起；每次点火，她都会想起海妖那句比胜利更贵的警告。", A.FireRounds, draw: 1);
            C(cards, "crew_twilight_tide_diver_naive", "幽灯潜者 奈芙", "幽灯断后", 13, "奈芙没有把钥匙插进槽里，也不肯让船队盲目冲向深海。她举起水母灯沿敌舰尾流折返，为同伴标出一条能暂时脱身的暗水道。", A.Retreat, block: 8);

            C(cards, "crew_gray_gull_navigator_evelyn", "星盘手 伊芙琳", "也许的归航线", 6, "伊芙琳抹去星位旁那个过分肯定的箭头，改画一条并行航线。巨人星图教会她留住另一种可能，敌舰擦肩而过时，她已经备好下一手。", A.Parallel, block: 3, draw: 1);
            C(cards, "crew_gray_gull_carpenter_barlow", "船匠 巴洛", "归家龙骨", 7, "巴洛在炮火里听见木材叫出自己的乳名。他把旧绳圈拴紧断梁，一锤一锤敲回原位；这回轮到他答应，谁也不能把这艘船留在海里。", A.Standard, repair: 8, block: 6);
            C(cards, "crew_gray_gull_gunner_leisa", "炮术官 雷萨", "潮门火线", 9, "雷萨将六段装药表压在炮架旁，认出了教官明令禁用的射法。她接下第二门炮的位置，用一条不肯偏移的火线补上第一炮后的空隙。", A.PrecisionLoad, draw: 1);
            C(cards, "crew_gray_gull_diver_noya", "潜水员 诺雅", "碎帆递旗", 15, "诺雅从船尾翻上甲板，把铜牌和碎帆旗一并递给身后的伙伴。她仍说这只是一次散步，手中钩索却已经拦住追来的敌兵。", A.Standard, S.Boarding, damage: 7, block: 3, draw: 1);

            // Only rare / epic / legendary equipment grants cards: five of each per slot.
            P(cards, "bow_gilded_mermaid", "鎏金海妖像", "金像静歌", 5, "伊岚没有擦去金像眼角的火药痕，而是顺着回音咒添上最后一笔。船首不再重唱海妖的歌，只把那次驱歌留下的火种递给炮手。", A.FireRounds, block: 3);
            P(cards, "bow_sunlion_figurehead", "日狮镇浪像", "晨狮列阵", 4, "盐船账册记着铸狮人的旧名。朝阳落在狮鬃上时，水手像认出了久违的号令，在逼近的敌舰前把盾与短刀排成整齐一线。", A.Standard, S.Boarding, damage: 6, block: 8);
            P(cards, "bow_pearl_siren", "珠母歌姬首", "鳞衣引航", 13, "弥赛拉补好的鳞片在夜浪中依次发亮。舵手循着她留下的守门歌调整航向，船首擦过危险水域，稳稳进入可与敌舰并行的浪线。", A.Navigator, block: 4);
            P(cards, "bow_guildbreaker_crest", "破会徽角", "六港护航誓", 14, "熔化的私掠徽章不再替任何商会封路。船长将新冲角朝前一压，护航舰便挡到同盟货船之前，带着完整的舷侧迎向来敌。", A.Approach, block: 8);
            P(cards, "bow_amber_albatross", "琥珀信天翁首", "归羽追风", 15, "晨曦照亮琥珀时，里面的羽毛偏离藏宝海沟，指向旧水手的故乡。掌舵者顺着倒影提前转舷，把新的归航线记在手边。", A.Turn, block: 3, draw: 1);
            P(cards, "bow_abyssal_fang", "深渊鲸牙", "活骨破舱", 7, "琪娅把鲸牙与活龙骨的纤维接在一起，低鸣便沿着舰艏传来。贴近敌船后，牙尖在坚木里找出最薄的一缝，己舰却被新生骨纹护住。", A.Sabotage, S.Naval, damage: 14, block: 5);
            P(cards, "bow_nightkraken_tusk", "夜海妖巨齿", "断腕余焰", 5, "炮长终于承认，当夜救他的蓝焰比巨齿更锋利。工匠把焦痕留在齿根，让船员在近距凿击时记住从腕足之间活着回来的走位。", A.Sabotage, S.Naval, energy: 2, damage: 23);
            P(cards, "bow_gravecurrent_spur", "墓潮骨刺", "沉舰点名", 12, "第七港口升起时，骨刺上的失落舰名逐一亮起。船长照着名字重排海图，在浓雾里拉开航距，留出认清每一艘旧船的时间。", A.Retreat, block: 5, draw: 1);
            P(cards, "bow_blackreef_hammer", "黑礁碎舰槌", "松铆重击", 9, "槌首先敲松封港外链，赫莲才有机会打断主链。此后每次贴近敌舰，持有人都会先找铆钉，把海堡工事的拆法重新用上一遍。", A.Sabotage, S.Naval, energy: 2, damage: 20, block: 6);
            P(cards, "bow_voidray_edge", "虚空鳐翼首", "巨掌侧切", 6, "海巨人曾用手掌托住试航的鳐翼首。掌舵者记下那次转弯时的倾角，从此敢沿着最陡的浪面切入敌舰侧方，而不丢失下一步航线。", A.Turn, block: 6, draw: 1);
            P(cards, "bow_leviathan_ram", "海兽王冲角", "碎阵救生路", 14, "船员把避战旗撕成六份系上救生艇，海兽王冲角则留在最前面。它向敌舰推进，船身承受的第一阵震动替身后的小艇争得一条路。", A.Approach, block: 13);
            P(cards, "bow_crownless_colossus", "无冠巨像首", "无冠破锁", 6, "巨人说古像的王冠是一副枷锁。铸匠于是磨锐冠下的断口，让船首在贴近王舰时专找连接处下手，再不替任何主人把旧锁补上。", A.Sabotage, S.Naval, energy: 2, damage: 25, block: 6);
            P(cards, "bow_worldsplitter", "裂海天楔", "晨曦白浪", 15, "天楔劈开的白浪通向晨曦海。水手记得浪线合拢前必须留下救生艇的宽度，因此每次抢入尾舷，都会顺手标出另一条离开的线。", A.Navigator, block: 7, draw: 1);
            P(cards, "bow_dragonwake_ram", "龙醒赤角", "三鼓赤角", 9, "三记鼓点唤亮龙角焦痕，撞击在船身前方被接住。装弹手借鼓点间的空隙重排炮位，等第三声落下，旧炮已多了一份装药。", A.Load, block: 10);
            P(cards, "bow_last_horizon", "终界航标首", "迟归者航标", 15, "第七港口隐去后，航标仍朝着东方。船长依照它的指引绕开追兵，一边确认新海，一边给那名迟到水手留下可以追随的船影。", A.Circle, block: 6, draw: 2, energy: 2);

            P(cards, "sail_guild_silk", "商会金丝帆", "共享金丝码", 14, "金丝识别码不再向六港收费。掌舵者把它展在高处，失散护航船便顺着同一条航线靠拢，彼此交出尚未用完的战术。", A.Parallel, block: 3, draw: 1);
            P(cards, "sail_emerald_trade_wing", "翡翠商翼帆", "第十三港徽", 10, "新缝的无名港徽里藏着青柑汤配方。船员循着绿色帆缘重整队形，腾出的甲板足够把松脱的舱板补好，再准备下一次交锋。", A.Standard, block: 7, repair: 4);
            P(cards, "sail_sunthread_spinnaker", "日线追风帆", "借最后的日光", 15, "日线把黄昏最后的风收紧。归潮水手曾靠它赶在黎明前追上同伴，此后每次追敌，船长都会把这短暂暖光留给最需要推进的一刻。", A.Approach, block: 3, draw: 1);
            P(cards, "sail_redroute_broadsail", "红航路阔帆", "赤线炮位", 9, "赫莲在阔帆红线上依次点出六支舰队的位置。舵手转到侧舷，炮手便有空收回一根松索，把敌舰重新放回自己的瞄线上。", A.Circle, block: 6);
            P(cards, "sail_whisperveil", "低语雾帆", "掌纹下的静航", 6, "赛娜认出海兽头顶的掌纹后，船员再没有把沉默当作安全。帆布吸走绳索声，舵手向后撤出一段距离，给瞭望员留下一次确认。", A.Retreat, draw: 1, block: 3);
            P(cards, "sail_moonweave", "月织静风帆", "石港月潮", 12, "第七港口没有风，月织帆却始终向出港处微动。掌舵者把那道牵引记在手上，纵使炮烟遮天，也能把己舰带到敌舰旁边。", A.Navigator, block: 6);
            P(cards, "sail_eclipse_shroud", "蚀月影帆", "月影无声切入", 13, "弥赛拉藏在帆心的暗光只照亮最近的浪尖。黑帆贴着那道边界切向侧舷，敌方看清月环时，己舰已经准备好下一次动作。", A.Turn, block: 7, draw: 1);
            P(cards, "sail_starshear_rig", "裁星索帆", "五结抢风", 6, "五颗航星与石盘刻线一一重合。赛娜按星位拉紧索结，帆便把高空风送到船首，船员在推进间隙重新分好手中的航令。", A.Approach, draw: 2, energy: 2);
            P(cards, "sail_ghostcurrent_canvas", "幽流逆帆", "逆流拒葬", 1, "无名船的旧帆记得吞没主人的潮水。浪向再次相同时，它反向鼓起，催掌舵者离开旧航线；船上连最紧张的水手也来得及抓稳护栏。", A.Retreat, block: 9);
            P(cards, "sail_aurora_fan", "极光扇帆", "不冻北航道", 8, "旧水手看着帆上的极光，想起沉船那夜没有结冰的航道。船长照着他的手势转舷，把回忆变成可以避开下一轮炮火的路线。", A.Circle, block: 5, draw: 1);
            P(cards, "sail_tempest_wing", "暴风翼帆", "风眼归途", 15, "旧船长翻过气象图，看见晨曦海入口。他张开翼帆折转侧风，让整艘船沿风暴的内壁抢向敌舰，船员则收紧最后几道安全索。", A.Navigator, block: 10);
            P(cards, "sail_skybreaker_array", "裂空九翼帆", "九翼同拍", 14, "奥瑟分出的九翼骨架让重舰学会同一节奏。副翼依次张开，炮烟里的船影同时转舷，每一声索响都给甲板上的下一道命令留了位置。", A.Turn, block: 8, draw: 1);
            P(cards, "sail_crown_of_gales", "万风王冠帆", "六旗万风", 14, "六种领航旗被金顶帆照亮。失散的水手朝着同一侧列队，风声把短促的号令送到每个角落；靠舷前，所有人都知道自己该站在哪里。", A.Standard, S.Boarding, block: 9, draw: 2, energy: 2);
            P(cards, "sail_thunderphoenix", "雷凰浴火帆", "雷纹余焰", 5, "巫师用雷雨洗掉帆上的咒音，凤凰纹却留下一点不会烧穿布料的火。装弹手在纹路下接住火种，准备把它交给真正命中的下一炮。", A.FireRounds, block: 7);
            P(cards, "sail_last_free_wind", "最后自由风帆", "六港自由风", 15, "第六座港口送来最后一根缝线，帆布终于不再彼此拉扯。船长从不同船队的愿望中选出同一条并行航线，又为每艘船备好下一条。", A.Parallel, draw: 2, block: 6, energy: 2);

            P(cards, "cannon_chain_repeater", "链弹连发炮", "双铆链击", 9, "乌铎带回的巨链露出两个薄铆口。炮手将两枚链弹对着同一节潮汐打出，随后依照回收的弹痕调整位置，准备下一次齐射。", A.Cannon, S.Naval, damage: 11, draw: 1);
            P(cards, "cannon_anchorbreaker", "断锚双联炮", "封锁链断", 9, "赫莲从断锚的酒馆传说里听出了办法：不必打沉整条封锁线。双炮改瞄最厚的连接处，齐响时海面先传来铁环绷断的声音。", A.Cannon, S.Naval, damage: 18, energy: 2, powder: 1);
            P(cards, "cannon_redwake_repeater", "赤尾连火炮", "退艇后的红光", 14, "米拉等最后一艘救生艇驶离射界才升起赤尾信号。连续红光把进攻时刻传给六支舰队，甲板水手则顺势压低身体守住舷边。", A.Cannon, S.Naval, damage: 12, block: 4);
            P(cards, "cannon_stormnail_array", "风暴钉炮组", "钉住风眼", 15, "钉炮曾把翻覆的桅板稳在风眼边缘。此后炮手宁可少装一把药，也要让弹丸顶住侧风落向同一位置，为下一艘船争得安全的舷面。", A.Cannon, S.Naval, damage: 13, block: 3);
            P(cards, "cannon_echochamber", "回声膛炮", "看不见的射表", 5, "伊岚把海妖歌声送进测距筒，画出一张没有舰影的射表。炮手闭眼听完回声再装药，每一次校准都替真正开火留下更清楚的落点。", A.PrecisionLoad, draw: 1);
            P(cards, "cannon_starfire_battery", "星火炮列", "巨背星河齐射", 6, "试炮纸上的星河与巨人旧伤重合。炮手没有拆散古代导轨，而是让所有引线同刻燃起，顺便把敌舰的下一次偏转记回星图。", A.Cannon, S.Naval, energy: 2, damage: 21, draw: 1, powder: 1);
            P(cards, "cannon_cometfall_battery", "彗落重炮列", "旧港流星雨", 12, "石港守军认出了二十年前请求关门的白焰信号。炮长这次装入实弹，让弧线重返那片天空，直到最坚硬的舷板也露出裂口。", A.Cannon, S.Naval, energy: 2, damage: 25, powder: 1);
            P(cards, "cannon_blueflare_organ", "蓝焰风琴炮", "十二管驱歌", 5, "十二根炮管分别收好一段回音咒。伊岚依次封住蓝火，不许它在船上散开；只有射中敌舰的炮弹才会把驱歌余焰留在海面。", A.FireRounds, draw: 1, block: 3);
            P(cards, "cannon_voidbell_mortar", "虚钟曲射炮", "迟来的第十三响", 3, "虚钟开火时甲板听不见爆音。炮手要等远处钟鸣传回才记录落点，赫莲便从那声迟来的第十三响中知道，封存的武器又一次醒了。", A.Cannon, S.Naval, energy: 2, damage: 20, block: 6, powder: 1);
            P(cards, "cannon_sunforge_salvo", "日炉齐射炮", "三夜铜套", 9, "奥瑟守炉三夜铸齐铜套，炮手因此敢在余温未散时准备下一次装填。他们没有白费这份手艺，把刚才的弹痕也一同送上射表。", A.Load, block: 5, draw: 1);
            P(cards, "cannon_queens_broadside", "海盗女王舷炮", "六港决战名册", 14, "女王将六个未曾战胜的港名刻在炮口。它们这次不是战果，而是同伴；齐射点燃的一瞬，甲板的人为每一个名字守紧了自己的舷位。", A.Cannon, S.Naval, energy: 2, damage: 28, block: 6, powder: 1);
            P(cards, "cannon_krakens_thunder", "海怪雷鸣炮", "门前雷鸣", 13, "弥赛拉听出箍炮甲壳属于守门巨兽。船长没有朝深渊乱开火，而把雷鸣留给逼近的敌舰；声浪退去后，瞭望手终于听清下一道号令。", A.Cannon, S.Naval, energy: 2, damage: 27, draw: 1, powder: 1);
            P(cards, "cannon_worlds_end_volley", "天涯终幕炮", "归航星火射表", 15, "地平线后的信号弹只剩一枚，炮手把回船的角度仔细记下。下次实弹装填时，他以那次照亮迟归者的耐心，重新校准最近的敌舰。", A.PrecisionLoad, draw: 2, energy: 2);
            P(cards, "cannon_dragonbreath_battery", "赤龙吐息炮列", "绕礁龙息", 5, "巫师曾让赤龙火线绕开浅滩上的海妖。炮手学会先封好余焰再寻找落点，不让胜利变成滥烧；炮弹命中后，龙息才沿敌舷绵延。", A.FireRounds, block: 8, draw: 1, energy: 2);
            P(cards, "cannon_liberty_salute", "自由海二十一响", "决战后的礼炮", 15, "归潮舰队驶向晨光那天，二十一响都是空包。下一次敌舰封路时，炮手才把实弹推入其中，盼望这轮齐射之后仍有鸣礼炮的机会。", A.Cannon, S.Naval, energy: 2, damage: 30, powder: 1);

            P(cards, "plating_gilded_bulwark", "鎏金舷墙", "星光赴宴", 4, "盐人坐在照常举行的宴席边，胸口微光映上层叠钢木。炮火敲打外壁，水手只重新顶紧横梁，等宴会结束再问那位客人的来历。", A.Standard, block: 12);
            P(cards, "plating_suncoin_rampart", "日币护壁", "六缺口旧币", 10, "压住盐冠的旧金币被重新嵌回护壁。船员沿六道缺口的位置给夹层补上支点，从此每次举盾守舷，都会先检查身后的木梁。", A.Polish, block: 3);
            P(cards, "plating_coralsteel_hide", "珊钢复合壳", "避沙新珊瑚", 7, "米蕾发现活珊瑚躲开黑沙，阿棠便顺着它的生长修补船壳。炮痕下新生的枝丫没有神迹的耀眼光芒，却给船员留出一次喘息。", A.Standard, block: 6, repair: 7);
            P(cards, "plating_redbanner_wall", "赤旗舷垒", "透光旧战旗", 8, "旧水手从透明护板里认出自己的舰名。接舷号响起，他把盾举向旗上熟悉的方向，几名原本散开的同伴也随之站回队列。", A.Standard, S.Boarding, block: 10, draw: 1);
            P(cards, "plating_merchants_oath", "商誓护甲", "背面的六港誓", 14, "三家商会把另外三港写在誓约背面。轻合金护板不再只是保货的工具，水手顶住第一轮震动后，把腾出的空隙让给传令员。", A.Standard, block: 8, draw: 1);
            P(cards, "plating_moonsilver_skin", "月银覆板", "记住归途的裂缝", 7, "守夜人的纸条压在月银下方，板缝仍在涨潮时轻响。船匠顺着声音收拢松脱的碎片，让会记路的那部分船身先完整起来。", A.Standard, repair: 11, block: 6);
            P(cards, "plating_eclipse_mail", "蚀影链甲壳", "锁时黑银环", 13, "弥赛拉认出锁住舰队时间的黑银，便让船匠别再把它铆死。环片留出呼吸的余量，遇上链弹时收紧一瞬，又给甲板送回安静。", A.Standard, block: 17);
            P(cards, "plating_starseal_panels", "星印封舱板", "循星补漏", 6, "伊芙琳把巨人旧伤刻成封板星图。补漏工不必再猜水声从哪里传来，沿最亮的刻痕敲下木楔，还能把下一道裂口告诉船长。", A.Standard, repair: 10, draw: 1);
            P(cards, "plating_ghostreef_carapace", "幽礁甲壳", "巨肩护舷", 6, "巨人轻触旧肩甲，船员才明白幽礁为何会移动。装上舰身后，他们按那次触碰的力道重排支撑，让每一块护板都能共同承力。", A.Polish, block: 6);
            P(cards, "plating_aurora_laminate", "极光叠层甲", "未来炮痕", 12, "旧港船身的光带指向尚未留下的炮痕。船匠不愿把预兆当命运，先在那一侧加好木楔，并把最薄的两处记在备用封板背面。", A.Standard, block: 11, repair: 4, draw: 1, energy: 2);
            P(cards, "plating_living_hull", "活潮船壳", "重逢的骨与壳", 7, "活潮壳认回无名船的骨骼，潮湿木纹开始彼此牵引。船匠只需守住最大的裂口，便能看见散落碎片一点点归回同一艘船。", A.Standard, repair: 15, block: 8, energy: 2);
            P(cards, "plating_leviathan_scales", "海兽王鳞甲", "守门者忍耐", 13, "海妖制止了无休止的试炮。真正面对敌舰时，船员把厚鳞收紧到受击的一侧；他们记得每一道浅白炮痕，都不该再让守兽误会。", A.Standard, block: 24, energy: 2);
            P(cards, "plating_worldtree_bark", "海上世界树皮", "晨曦新叶", 15, "归潮水手把晨曦海第一片新叶留给六港。船上树皮再被击破时，嫩芽从断口探出；木匠护住新叶，连同它脚下的裂缝一起包扎。", A.Standard, repair: 12, draw: 1);
            P(cards, "plating_dragonwake_armor", "龙醒熔鳞壳", "余热再装填", 9, "米拉让赤纹迎住巨链前的炮火，散开的热量替装弹手争来一轮时间。铜套还热，他们已经把新药包压稳，重新伏在护板后。", A.Load, block: 12);
            P(cards, "plating_freedom_ark", "自由方舟外壳", "第六根系船桩", 14, "没有城门的第六港送来旧系船桩，它撑住了方舟尾舵。水手沿六处接缝共同加固甲板，再把省下的木料塞回炮击留下的豁口。", A.Polish, repair: 7, block: 4);

            P(cards, "instrument_guild_engine", "商路推演机", "被删去的航路", 4, "盐船账册让齿轮停在第七港口。领航员没有强转机器，而把那条被商会删掉的航线展开，从另一个方向寻找可以退出交火区的水路。", A.Retreat, draw: 1, block: 4);
            P(cards, "instrument_starledger", "星位账簿机", "未归者星账", 4, "财务官将六港账本倒着录入，机器吐出的不是利润，而是未归水手的星位。他把两套账目并排放好，让船长重新选择接下来的行动。", A.Standard, S.Ship, draw: 2, energy: 1);
            P(cards, "instrument_corsair_decoder", "私掠旗语译机", "六种旗语同盟", 14, "译机终于不用来让敌舰互相猜疑。六种旗语同时报出侧方空隙，舵手循令绕行，传令员则在甲板上继续翻译下一条消息。", A.Circle, draw: 1, block: 3);
            P(cards, "instrument_deepwater_abacus", "深水算珠仪", "不动的蓝珠", 8, "无名伤员上船时算珠没有动，艾达由此发现时间的异常。补给官学会同时核对重量与人名，腾出的物资被及时送往受损舱壁。", A.Standard, repair: 5, draw: 1, block: 3);
            P(cards, "instrument_trade_wind_oracle", "信风谕盘", "第八阵新风", 15, "七片薄叶在晨曦海一同竖起，旧商路从未见过这样的风。舵手顺着最先颤动的一片转舷，把陌生季风变成近在眼前的机动。", A.Turn, draw: 1, block: 4);
            P(cards, "instrument_lunar_astrolabe", "月潮星盘", "伤痕与星潮", 6, "伊芙琳将石盘旧伤与真实天空重叠，两种周期终于指向同一条路。船长照着月轮移船，在敌舰尾侧找到一次安稳的并行。", A.Navigator, draw: 1, block: 5);
            P(cards, "instrument_eclipse_orrery", "蚀月天仪", "月影先行", 13, "微型月轮先于天空投下阴影，弥赛拉便说出了舰队锁住时间的秘密。船长不与暗潮争辩，提前拉开距离，把影子留在炮击之外。", A.Retreat, block: 8, draw: 1);
            P(cards, "instrument_whalestar_receiver", "鲸歌星讯仪", "鲸歌与驱歌谱", 5, "伊岚将鲸歌与海妖回声并列录入，星针指向同一道门。炮手循着重合的音节精密装药，不再把听不懂的提醒当成噪声。", A.PrecisionLoad, block: 4, draw: 1);
            P(cards, "instrument_ghostmap_projector", "幽图投影仪", "黑玻璃港图", 12, "薄雾穿过黑玻璃，沉没海岸一点点重合。船长从完整港图里挑出两条备用计划，又用镜组指出己舰最需要避开的受击方向。", A.Standard, draw: 2, block: 6, energy: 2);
            P(cards, "instrument_aurora_calculator", "极光航算机", "巨人记忆中的北", 6, "碎晶落入机轮，北方不再只是磁针方向。航海士借巨人记忆的颜色修正舵角，船影掠向敌舰侧方，手中仍留着另一次选择。", A.Circle, draw: 1, block: 7);
            P(cards, "instrument_allseeing_sextant", "全视六分仪", "未建灯塔的旧名", 12, "归潮水手叫出了镜中灯塔二十年前的旧名。船长把旧称与新图对照，从迷雾里挑出两条清楚的战术，再安排水手守住最薄的舷侧。", A.Standard, draw: 2, block: 10, energy: 2);
            P(cards, "instrument_eye_of_depths", "深渊观测眼", "求救的光圈", 13, "黑透镜收缩时，守门兽却在后退。弥赛拉提醒船长先分清求救与威胁；他于是后撤一段航距，留出看清深水的时间。", A.Retreat, block: 12, draw: 1);
            P(cards, "instrument_worldcompass", "万海归一罗盘", "第六份港砂", 14, "第六份港砂让互相排斥的指针一齐转向会师点。六港船长第一次无需争抢领航位，就能把各自舰船带入同一条并行线。", A.Navigator, block: 8, draw: 1);
            P(cards, "instrument_celestial_engine", "天穹推演圣机", "最慢那艘的时刻", 14, "维克拆掉贸易优先权的金齿轮，让医疗船也能准时抵港。战斗中，圣机同样为最迟的一门炮留下装填余量，再把备选命令送回甲板。", A.Load, draw: 2, block: 4, energy: 2);
            P(cards, "instrument_last_horizon_lens", "终界地平镜", "归来才是序章", 15, "最后一艘归潮船隐入新地平线，镜中却留下六港灯火。船长依照这份回望转向，把下一个决定交给仍站在自己身边的水手。", A.Turn, draw: 2, block: 7, energy: 2);

            // Append new identities: saved battle decks refer to the existing library's integer positions.
            // Doctors heal boarding crew; they never repair the ship or invent new combat statuses.
            C(cards, "crew_broken_anchor_doctor_hector", "瘢痕医师 赫克托", "黑沙清创", 8, "赫克托听见伤员再次叫出兄长的名字，手中的骨柄刀却没有停。他把嵌进伤口的黑沙逐粒挑出，宁肯记忆随潮退去，也要先让甲板上的人活下来。", A.Standard, S.Boarding, energy: 2, repair: 22);
            C(cards, "crew_white_shell_doctor_mirei", "香潮医师 米蕾", "海息药浴", 14, "米蕾把银壳的歌声拆成四次呼吸，让负伤水手依着节拍吞下草药。伤口止血之后，她仍轻轻哼唱，直到那人能把看见的敌方动向完整告诉同伴。", A.Standard, S.Boarding, repair: 9, draw: 1);
            C(cards, "crew_bronze_bell_doctor_ada", "退役军医 艾达", "铜线缝合", 8, "艾达合上第八章病历，把伤者先靠岸的旧约交给挡在前面的水手。铜线穿过裂开的衣襟，她一边缝合伤口，一边教人用盾墙给下一位伤员留出通道。", A.Standard, S.Boarding, repair: 6, block: 8);
            C(cards, "crew_salt_crown_doctor_naji", "荒潮药师 娜吉", "苦盐回魂", 8, "娜吉把六种矿盐依次溶进净水，盯着药碗底下沉淀的黑沙。她暂时不去辨认那颗风暴眼里的心脏，只按住伤员的手腕，等真实的脉搏重新有力。", A.Standard, S.Boarding, repair: 14);
            C(cards, "crew_twilight_tide_doctor_viya", "梦疫医师 维娅", "借梦问诊", 12, "维娅隔着梦砂面罩听完伤员的呓语，把不存在的钥匙收回口袋。她借那个共同的梦唤醒同伴，再让醒来的人各自报出一条退路，免得下一次仍困在同一处。", A.Standard, S.Boarding, energy: 2, repair: 8, draw: 2);
            C(cards, "crew_gray_gull_doctor_moer", "船医 莫尔", "冷静诊断", 8, "莫尔把原始病历压在青玻璃药瓶下，先替最重的伤口止血，再让还能站立的人护住诊治的角落。他没有问伤员来自哪艘船，只要醒来的人帮忙传递下一道救援指令。", A.Standard, S.Boarding, repair: 7, block: 4, draw: 1);
            return cards.AsReadOnly();
        }

        private static void C(List<CoreSpecialCardDefinition> list, string id, string sourceName, string name,
            int chapter, string lore, A action, S stages = S.Ship, int energy = 1, int damage = 0,
            int block = 0, int repair = 0, int draw = 0, int powder = 0) =>
            Add(list, CoreSpecialCardSourceKind.Crew, id, sourceName, name, chapter, lore,
                action, stages, energy, damage, block, repair, draw, powder);

        private static void P(List<CoreSpecialCardDefinition> list, string id, string sourceName, string name,
            int chapter, string lore, A action, S stages = S.Ship, int energy = 1, int damage = 0,
            int block = 0, int repair = 0, int draw = 0, int powder = 0) =>
            Add(list, CoreSpecialCardSourceKind.Component, id, sourceName, name, chapter, lore,
                action, stages, energy, damage, block, repair, draw, powder);

        private static void Add(List<CoreSpecialCardDefinition> list, CoreSpecialCardSourceKind kind,
            string id, string sourceName, string name, int chapter, string lore, A action, S stages,
            int energy, int damage, int block, int repair, int draw, int powder)
        {
            bool boarding = stages == S.Boarding;
            string type = action == A.Cannon ? "炮击" : action == A.Sabotage || boarding && damage > 0 ? "登船" :
                IsManeuver(action) ? "机动" : repair > 0 ? "维修" : action == A.Standard || action == A.Polish ? "防御" : "策略";
            string art = action == A.Cannon ? "card_cannon_barrage" : action == A.Sabotage ? "card_boarding_hook" :
                IsManeuver(action) ? "card_full_sail_turn" : action == A.Load || action == A.PrecisionLoad || action == A.FireRounds ? "card_cannon_barrage" :
                boarding && damage > 0 ? "card_cutlass_raid" : repair > 0 ? "card_emergency_repair" : "card_brace";
            var card = new CoreBattleCard("card_special_" + id, name, type, energy, damage, block, repair, draw, powder,
                Describe(action, stages, damage, block, repair, draw, powder), stages, action,
                kind == CoreSpecialCardSourceKind.Crew ? id : string.Empty, art,
                action == A.Cannon ? 1 : 0, kind == CoreSpecialCardSourceKind.Component ? id : string.Empty);
            list.Add(new CoreSpecialCardDefinition(card, id, sourceName, lore,
                "seventh_berth_" + chapter.ToString("00"), kind));
        }

        private static bool IsManeuver(A action) => action == A.Turn || action == A.Approach || action == A.Retreat ||
            action == A.Circle || action == A.Parallel || action == A.Navigator;

        // Rules are composed from implemented actions, not atmospheric prose.
        // In particular there is no fictitious silence / range extension / armour pierce.
        private static string Describe(A action, S stages, int damage, int block, int repair, int draw, int powder)
        {
            var parts = new List<string>();
            switch (action)
            {
                case A.Turn: parts.Add("靠近 80 米，舰艏转向敌舰侧舷并进入冲撞航行"); break;
                case A.Approach: parts.Add("靠近 150 米并进入冲撞航行"); break;
                case A.Retreat: parts.Add("远离 130 米，取消冲撞航行"); break;
                case A.Circle: parts.Add("靠近 60 米，舰艏转向敌舰侧舷，再次推进后可冲撞"); break;
                case A.Parallel: parts.Add("靠近 60 米并转为平行，60 米内可接舷"); break;
                case A.Navigator: parts.Add("靠近 180 米并转为平行，60 米内可从尾舷接舷"); break;
                case A.Polish: parts.Add("本场船体防御牌额外格挡 +2，最多 +6"); break;
                case A.Load: parts.Add("下次炮击伤害 +4，装填最多 +12"); break;
                case A.PrecisionLoad: parts.Add("下次炮击伤害 +8，装填最多 +12"); break;
                case A.FireRounds: parts.Add("获得 2 发烈焰强化，最多 4 发；炮击穿过格挡造成伤害后点燃敌舰，连续 2 回合灼烧 5 船体"); break;
            }
            if (damage > 0) parts.Add((action == A.Cannon ? "350 米内炮击：" : action == A.Sabotage ? "150 米内近距破坏：" : string.Empty) +
                "造成 " + damage + " 点" + (stages == S.Boarding ? "船员" : "船体") + "伤害");
            if (block > 0) parts.Add("格挡 +" + block);
            if (repair > 0) parts.Add("恢复 " + repair + " 点" + (stages == S.Boarding ? "船员生命" : "船体"));
            if (draw > 0) parts.Add("抽 " + draw + " 张牌");
            if (action == A.Cannon) parts.Add("消耗 1 炮弹" + (powder > 0 ? "、" + powder + " 火药" : string.Empty));
            if (action == A.Sabotage) parts.Add("无需弹药");
            return CoreBattleCardText.FormatEffects(string.Join("；", parts) + "。");
        }
    }
}
