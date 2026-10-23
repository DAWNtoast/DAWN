using System;
using System.Collections.Generic;

namespace ShatteredSail.Content
{
    /// <summary>
    /// One chapter in the connected "Seventh Berth" port anthology.  Chapters
    /// are deliberately short enough to be read inside a crew dossier while
    /// still forming a continuous fifteen-part story when read in order.
    /// </summary>
    public sealed class CrewStoryDefinition
    {
        public CrewStoryDefinition(string id, int chapter, string title, string body, string previousId, string nextId)
        {
            Id = id ?? string.Empty;
            Chapter = chapter;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            PreviousId = previousId ?? string.Empty;
            NextId = nextId ?? string.Empty;
        }

        public string Id { get; }
        public int Chapter { get; }
        public string Title { get; }
        public string Body { get; }
        public string PreviousId { get; }
        public string NextId { get; }
        public string ChapterLabel => "航海异闻 · 第七港口志 · 第" + Chapter.ToString("00") + "篇《" + Title + "》";
    }

    /// <summary>
    /// Port-specific identity for one of the six gameplay professions.  The
    /// profession index remains compatible with the original six bool-based
    /// gameplay capabilities while Id is the durable identity used by the new
    /// regional roster and art pipeline.
    /// </summary>
    public sealed class CrewVariantDefinition
    {
        public CrewVariantDefinition(
            string id,
            string portName,
            int professionIndex,
            string professionId,
            string displayName,
            string roleSummary,
            string specialty,
            string skillDescription,
            string stats,
            int contractCost,
            string appearanceTags,
            string biography,
            string storyId,
            string portraitAssetName,
            bool isLegacy = false,
            bool wasOldCaptainCrew = false)
        {
            Id = id ?? string.Empty;
            PortName = portName ?? string.Empty;
            ProfessionIndex = professionIndex;
            ProfessionId = professionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            RoleSummary = roleSummary ?? string.Empty;
            Specialty = specialty ?? string.Empty;
            SkillDescription = skillDescription ?? string.Empty;
            Stats = stats ?? string.Empty;
            ContractCost = contractCost;
            AppearanceTags = appearanceTags ?? string.Empty;
            Biography = biography ?? string.Empty;
            StoryId = storyId ?? string.Empty;
            PortraitAssetName = portraitAssetName ?? string.Empty;
            IsLegacy = isLegacy;
            WasOldCaptainCrew = wasOldCaptainCrew;
        }

        public string Id { get; }
        public string PortName { get; }
        public int ProfessionIndex { get; }
        public string ProfessionId { get; }
        public string DisplayName { get; }
        public string RoleSummary { get; }
        public string Specialty { get; }
        public string SkillDescription { get; }
        public string Stats { get; }
        public int ContractCost { get; }
        public string AppearanceTags { get; }
        public string Biography { get; }
        public string StoryId { get; }
        public string PortraitAssetName { get; }
        public bool IsLegacy { get; }
        public bool WasOldCaptainCrew { get; }
        /// <summary>Legacy crew carry signed testimony; others begin as leads.</summary>
        public string LoreRelationLabel => WasOldCaptainCrew
            ? "老船长亲历 · 航海档案"
            : SeventhPortLoreCatalog.RelationLabelForStory(StoryId);
    }

    /// <summary>
    /// Immutable narrative source for all regional tavern rosters.  No Unity
    /// API is used here, keeping the story and character data independently
    /// testable and suitable for a future CSV/ScriptableObject importer.
    /// </summary>
    public static class CrewNarrativeCatalog
    {
        public const int ProfessionCount = 6;

        private static readonly string[] portNames =
        {
            "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港",
        };

        private static readonly string[] professionIds =
        {
            "navigator", "carpenter", "doctor", "gunner", "cook", "diver",
        };

        private static readonly CrewStoryDefinition[] storyArray =
        {
            Story("seventh_berth_01", 1, "老船长与无名海", "第一次出海，老船长没有旗，只有一艘补过七次的旧船。海上诸神索要活人税，他解开缆绳，载着六港逃亡者冲进断锚风暴。漏水的船舱里，众人轮流舀水，他在舱壁刻下誓言：愿担风浪的人，都有走这条海路的权利。", "", "seventh_berth_02"),
            Story("seventh_berth_02", 2, "第一面自由旗", "白贝采珠人带来绳索，铜钟炮匠扛来工具，盐冠厨师抱着一口锅，应着那句誓言登上旧船。老船长裁下一幅帆，擦去王徽，让每个人在原处签名。有人不会写字，就按了一个指印。后来，第七港口连门墙都还没建起，这块碎帆就已挂在入口。", "seventh_berth_01", "seventh_berth_03"),
            Story("seventh_berth_03", 3, "穿过千山万水", "船队穿过浮冰山、赤潮海与移动的岛群，舱中装着送回六港的种子和药品，也载着寻回的失踪者。大雾里看不见前船，航海长便敲铜钟，一声接一声传到队尾；破浪罗盘上也渐渐刻满了潮汐记号。一双双平凡的手，就这样连通了最远的航线。", "seventh_berth_02", "seventh_berth_04"),
            Story("seventh_berth_04", 4, "与神明谈价", "海神索要一名船员，风暴之神要六港永远放弃航权。老船长把封蜡图摊在神殿门前，用湿透的袖口压住翘起的图角：船上的人，一个也不拿来谈价。海啸升起时，六港灯火同时亮了。桨叶劈开水面，船队驶过了神谕划下的界线。", "seventh_berth_03", "seventh_berth_05"),
            Story("seventh_berth_05", 5, "第七港口的誓言", "六港之外有一片海湾，王国的界碑立不住，潮水一夜便将它冲倒。老船长想在这里建第七港口，让人能靠岸、做买卖、学手艺，再自行离开。蓝火巫师伊岚与海妖弥赛拉在潮边见证这份誓言，把歌声留给了守门的潮汐。", "seventh_berth_04", "seventh_berth_06"),
            Story("seventh_berth_06", 6, "巨人背上的星图", "为寻一条避开神明目光的航道，老船长在海巨人背上走了七昼夜。巨人背上的旧伤入夜便显成星图。他递给老船长石质星盘，说在风暴中给别人留个位置，那里也能成为港口。老船长带着新航线回到船上，却迟迟没有发令，怕自己的愿望也成了命令。", "seventh_berth_05", "seventh_berth_07"),
            Story("seventh_berth_07", 7, "不会沉的龙骨", "六港船匠把海兽蜕骨、珊瑚胶与旧船木接在一起，造出不会沉的龙骨。船下水时，最强壮的人伸手把舵，老船长却请乘客都说说想去哪里。龙骨在脚下轻响，记住每个人的重量；后来有人靠岸，总有船匠俯身听它是否还记得。", "seventh_berth_06", "seventh_berth_08"),
            Story("seventh_berth_08", 8, "分航之前", "远征第八年，众人在饭桌上第一次争起第七港口该归谁。炮手要添护港舰，商人摊开税账，有人只问何时能见家人。老船长一直听到灯油将尽，也没有替众人裁决。分航前，每人领到一枚铜牌，各自作了决定，也各自担起责任。", "seventh_berth_07", "seventh_berth_09"),
            Story("seventh_berth_09", 9, "最后一门旧炮", "最后一炮震落潮门的巨链，通向第七港口的水道终于敞开。老船长与部下把旧炮拖上岸，命人卸下车轮，不许再拿它征服邻港。雷萨记住了那道命令。可欢呼声还没散尽，已有别的炮口悄悄转向曾并肩驶来的船。", "seventh_berth_08", "seventh_berth_10"),
            Story("seventh_berth_10", 10, "老船长离开了", "第七港口的栈桥还没铺完，老船长已望见更远的风暴。他把盐锅、罗盘、铜钟和六份钥匙交给部下，独自驶入风暴眼，要去寻找无人能够占有的终点。岸上的人追问谁来接管，他隔着水回答：别找我的王座，让更多人能自己选路。", "seventh_berth_09", "seventh_berth_11"),
            Story("seventh_berth_11", 11, "忘记初心的人", "旧部的船又出现在六港外，碎帆旗却换成了黑帆。铁誓不沉号拦航收税，雷冠审判号借神谕判罪，绯刃寡妇号将救援变成劫掠，余烬方舟号以自由誓换取永燃之火。港册仍保留它们最初的名字，旧墨之上，添了遇难船只的新名。", "seventh_berth_10", "seventh_berth_12"),
            Story("seventh_berth_12", 12, "仍在寻找的人", "潮汐镇魂号仍沿旧航路寻找老船长。船上的人愿与来者切磋，一旦对方无力再战，便会收炮。六港流传的海图上，传说舰队所经之处不时留下旧港印；有人循印挑战堕落者，有人先护送难民。潮声盖过争论，镇魂号的灯还在远处移动。", "seventh_berth_11", "seventh_berth_13"),
            Story("seventh_berth_13", 13, "虚无缥缈的港口", "后来者校准罗盘，始终找不到第七港口的固定坐标。救起敌船的人说，雾后曾露出栈桥；分出最后一桶淡水的人，见过岸灯亮起。罗盘、铜钟与封蜡图都在，却没有谁能单靠它们开门。拒绝贡品的那一夜，海湾又近了一些。", "seventh_berth_12", "seventh_berth_14"),
            Story("seventh_berth_14", 14, "继承者的试航", "小船驶离港口时，帆上还留着修补的针脚。酒馆旧部带来各自的手艺，船坞里活龙骨重新接合，档案室渐渐摆满有来历的旧物。部件的刻痕、船员的口述与卡牌上的图纹拼出旧航迹；等传说舰队横在前方，新船长也须作出自己的决定。", "seventh_berth_13", "seventh_berth_15"),
            Story("seventh_berth_15", 15, "成为新的船长", "堕落舰队黑帆落下，仍在寻人的船收炮致意。新船长未找到可坐的王座，却已替陌生人挡过风浪，与自然和神明争过一条去路。第七港口的自由旗再次升起。下一艘小船抵岸时，有人递去缆绳，又在海图上为它留下一段空白。", "seventh_berth_14", ""),
        };

        private static readonly CrewVariantDefinition[] crewArray =
        {
            // 断锚港：沉船木、锈铁与暴潮塑造的粗粝救捞者。
            Crew("crew_broken_anchor_navigator_lorne", "断锚港", 0, "测潮师 洛恩", "暴潮领航与残骸航路测绘", "断链测潮", 105, "五十岁深褐肤色；长窄脸与鹰钩鼻；灰白绳辫；左眼浑浊；瘦高驼背；锈红测潮尺", "洛恩量潮时总把浑浊的左眼朝向风口。断锚之夜，他是最早登上老船长无名船的人；如今那本潮汐笔记仍缝在袖口，纸边磨得像鱼鳃。快船嫌他标线太慢，他便指指雾后：还有一艘没跟上。", "seventh_berth_01", "crew_broken_anchor_navigator_lorne_fullbody"),
            Crew("crew_broken_anchor_carpenter_kia", "断锚港", 1, "铆骨匠 琪娅", "重型龙骨加固与沉船复原", "沉木铆接", 125, "二十七岁古铜肤色；方下颌；黑色寸发；鼻梁横疤；结实矮壮；铜铆钉围裙", "琪娅辨沉船，先闻木头，再摸钉孔。她最先发现无名船的龙骨会自行愈合，从此不许学徒把旧料一把烧光。围裙暗袋里藏着一片刨花，贴近指腹，仍有心跳般的震动；她只说那是木头还没睡。", "seventh_berth_07", "crew_broken_anchor_carpenter_kia_fullbody"),
            Crew("crew_broken_anchor_doctor_hector", "断锚港", 2, "瘢痕医师 赫克托", "溺水急救与海兽创伤处置", "黑沙清创", 145, "六十岁苍白肤色；凹陷长脸；秃顶白络腮胡；双颊缝合疤；清瘦；骨柄手术箱", "赫克托的手术箱里，药瓶与黑沙各占一层。一个无名伤员醒来，竟叫出他早夭兄长的名字。从那以后，老人总会另起一页，记下病人的第一句话：深海也许保存着记忆，却未必肯把它还给原来的人。", "seventh_berth_08", "crew_broken_anchor_doctor_hector_fullbody"),
            Crew("crew_broken_anchor_gunner_mira", "断锚港", 3, "沉钟炮师 米拉", "近距破障与链弹校射", "锚链齐射", 170, "三十五岁黑肤；圆脸高颧骨；短卷银发；右眉裂痕；宽肩；背负双筒短炮", "米拉校炮前会敲一敲捞来的沉船钟。潮门巨链升起那晚，她听出每一环的回声都对应一座港钟，写下最后一炮的六段装药表。旁人只记得断链的巨响，她却能哼出开火前那一瞬的低鸣。", "seventh_berth_09", "crew_broken_anchor_gunner_mira_fullbody"),
            Crew("crew_broken_anchor_cook_tam", "断锚港", 4, "烟熏厨娘 塔姆", "耐储口粮与低温烟熏", "残火熏舱", 185, "四十二岁麦色肤色；宽脸酒窝；栗色卷发包头巾；丰腴体型；烟熏铜锅与鱼叉勺", "塔姆翻锅盖时，总先擦掉背面的烟灰。那里记着断锚港失踪船只的最后一餐，鱼汤旁也许就是一个再没回来的人名。盐冠配方传来后，她把烟熏次序逐项比过，竟与无名船六枚港印的排列相同。", "seventh_berth_10", "crew_broken_anchor_cook_tam_fullbody"),
            Crew("crew_broken_anchor_diver_udo", "断锚港", 5, "锚墓潜手 乌铎", "深水残骸切割与重物起吊", "断链潜行", 220, "三十岁黝黑肤色；菱形脸；剃光头；下唇铜环；精瘦；不对称锈铁潜盔", "乌铎在港底锚墓长大，闭气时靠数心跳认路。他从潮门巨链下带回守夜人的铜牌，却不肯谈那口潜水钟。独处时，他摸摸尚无皱纹的脸——钟里那个年老的自己，也做过同样的动作。", "seventh_berth_11", "crew_broken_anchor_diver_udo_fullbody"),

            // 白贝港：明亮珊瑚、珍珠贸易与热带药草形成的轻盈风格。
            Crew("crew_white_shell_navigator_saina", "白贝港", 0, "珊潮领航员 赛娜", "珊瑚潮沟与季风航线", "白沫读潮", 115, "二十四岁棕肤；心形脸与雀斑；蓬松黑卷发夹白贝；轻盈修长；青绿纱披肩", "赛娜听人讲鬼故事，常要对方再学一遍浪声。她从银壳中逐字抄下求救信，认出了话语间海浪拍击的节奏，并据此标出通往铜钟港的隐秘坐标。问她是否相信幽灵，她只把耳朵贴回壳口：先别出声。", "seventh_berth_02", "crew_white_shell_navigator_saina_fullbody"),
            Crew("crew_white_shell_carpenter_atang", "白贝港", 1, "贝骨船匠 阿棠", "轻船壳与珊瑚胶修补", "贝骨补片", 135, "三十一岁东方面孔；鹅蛋脸；乌黑高马尾；颊侧贝粉纹；纤细有力；象牙色贝骨锯", "阿棠的掌心常沾着珊瑚胶，六色木屑护符从不离身。她与老船长造过不会沉的龙骨，也为第七港口打下第一根系船桩。教徒弟排泊位时，她总擦掉最靠岸的一格，留给那艘迟迟赶不上的慢船。", "seventh_berth_07", "crew_white_shell_carpenter_atang_fullbody"),
            Crew("crew_white_shell_doctor_mirei", "白贝港", 2, "香潮医师 米蕾", "热带草药与歌声镇痛", "海息药浴", 155, "五十岁深棕肤色；柔和圆脸；银灰爆炸卷；无疤；丰润体型；花草药囊", "米蕾试药浴的水温，也听病人的呼吸。那名无名伤员只有银壳唱响时才肯吸气，她便按着旋律的节拍搅药、换水。雾气散去，水手终于说全了“归潮舰队”；米蕾没有追问，先递去一杯温水。", "seventh_berth_14", "crew_white_shell_doctor_mirei_fullbody"),
            Crew("crew_white_shell_gunner_gallo", "白贝港", 3, "珠炮手 加洛", "轻炮跳弹与珊礁防卫", "珍珠跳弹", 175, "十九岁浅褐肤色；少年圆脸；金棕蓬发；门牙缺角；瘦小灵活；珍珠镶柄信号炮", "加洛擦炮时爱用缺了角的门牙咬住布条。他曾照银壳坐标试射采珠小炮，亲眼看着炮弹在海面跳了十三次。带去铜钟港的弹痕补全了薄铜海图的一条线，可他至今最得意的，仍是自己一次也没数漏。", "seventh_berth_03", "crew_white_shell_gunner_gallo_fullbody"),
            Crew("crew_white_shell_cook_flora", "白贝港", 4, "柑潮厨师 芙萝", "清凉饮食与坏血病预防", "青柑蜜藏", 195, "二十八岁金棕肤色；长脸翘鼻；橙红短卷发；酒窝；高挑；柑橘串与白釉汤锅", "芙萝切青柑从不丢皮，指尖整日留着清苦的香气。她在盐冠配方里辨出白贝青柑，熬汤唤回旧舰队水手的味觉。那人捧碗怔了许久，忽然说起第七港口开门时的风向；她便记在菜谱空白处。", "seventh_berth_10", "crew_white_shell_cook_flora_fullbody"),
            Crew("crew_white_shell_diver_kela", "白贝港", 5, "采珠人 珂拉", "自由潜水与活珊瑚勘探", "银壳寻音", 230, "三十六岁深棕肤色；方脸；剃侧长辫；鼻梁白贝彩绘；健美；珍珠网袋与短蹼", "珂拉收网后，总把那枚银壳单独放在枕边。壳中的求救声与船歌像极了失踪多年的母亲，她却不敢认定那就是母亲的声音。凡是驶向第七港口的船，她都愿意登上，盼着有一天能亲口问明白。", "seventh_berth_02", "crew_white_shell_diver_kela_fullbody"),

            // 铜钟港：帝国旧工业、黄铜仪器与锅炉烟尘。
            Crew("crew_bronze_bell_navigator_vick", "铜钟港", 0, "黄铜测绘师 维克", "机械测绘与帝国密码图", "十三分仪", 125, "四十八岁白肤；瘦削矩形脸；油黑八字胡；单片机械目镜；笔挺身形；多臂黄铜六分仪", "维克校六分仪时，连袖口也不许碰偏刻度。薄铜海图最后一行像六港旧旗，也像父亲的笔迹，却查不出究竟出自谁手。他只在纸背试译了一句“钟响十三次，门还没关”，便压住图角，不再向人解释。", "seventh_berth_12", "crew_bronze_bell_navigator_vick_fullbody"),
            Crew("crew_bronze_bell_carpenter_osser", "铜钟港", 1, "齿轮船匠 奥瑟", "金属骨架与机械舵机", "钟摆校骨", 145, "五十六岁灰肤；宽额方颌；钢灰平头；烧伤覆盖右颊；魁梧；齿轮义手", "奥瑟习惯用齿轮义手敲遍船骨，逐处听回声。他曾把活龙骨锁进黄铜框架，一夜之后，木纹竟将框架撑裂。如今修船，他会先问乘客坐在哪里；那根木头记得的重量，量尺和算盘都算不出来。", "seventh_berth_07", "crew_bronze_bell_carpenter_osser_fullbody"),
            Crew("crew_bronze_bell_doctor_ada", "铜钟港", 2, "退役军医 艾达", "炮创外科与舰队检疫", "铜线缝合", 165, "四十岁东方面孔；锐利长眼；齐耳黑发夹银丝；唇角旧伤；挺拔；黄铜扣军医大衣", "艾达拆线时，仍把每一截铜丝摆得笔直。她随老船长远征，曾挡在黑沙伤员与登岸守卫之间，只说“伤者先靠岸”。那八章旧病历，她一直带在身边，纸角沾着海盐；她教新人看伤口，别先看衣领上的徽记。", "seventh_berth_08", "crew_bronze_bell_doctor_ada_fullbody"),
            Crew("crew_bronze_bell_gunner_helen", "铜钟港", 3, "钟楼炮官 赫莲", "岸防巨炮与远距校射", "第十三响", 185, "三十三岁苍白肤色；高颧窄脸；铜红色短发；左耳缺角；修长；黑金帝国炮官制服", "赫莲点引信前，总要多等半息，让小艇先撤开。她曾替老船长打出击断巨链的一炮，把第十三响认作人们自行决定去向的信号。六段装药表仍压在炮台下，她教炮手的第一件事，是认清哪一片水面必须留空。", "seventh_berth_03", "crew_bronze_bell_gunner_helen_fullbody"),
            Crew("crew_bronze_bell_cook_basin", "铜钟港", 4, "锅炉厨师 巴辛", "高压蒸煮与轮班供餐", "蒸汽军粮", 205, "六十二岁黑肤；椭圆脸白眉；光头；蒸汽烫疤环绕双臂；壮硕；压力锅背架", "巴辛听锅炉泄气，能听出一整班水手何时该吃饭。他曾掌管帝国舰队的灶火，一眼认出盐锅王冠的第六道缺口：那是旧军灶的泄压记号。开门之前须留下一口回程蒸汽，这条规矩，他念得比食谱还熟。", "seventh_berth_10", "crew_bronze_bell_cook_basin_fullbody"),
            Crew("crew_bronze_bell_diver_duke", "铜钟港", 5, "铜盔潜工 杜克", "重装潜水与海底工程", "钟压下潜", 240, "四十四岁浅棕肤色；粗眉方脸；棕色络腮胡；鼻骨歪斜；极魁梧；球形铜盔与配重靴", "杜克下水前会把每件工具在铜盔上敲一遍。他打开第一座会呼吸的潜水钟时，里面的工具竟还温热。井壁另一端传来工人暗号，敲击节奏毫无差错；可那个答话的人，坚持说自己活在二十年前。", "seventh_berth_11", "crew_bronze_bell_diver_duke_fullbody"),

            // 盐冠港：盐晶、沙海、香料商路与无旗走私船。
            Crew("crew_salt_crown_navigator_ashir", "盐冠港", 0, "盐路航海士 阿希尔", "无标海岸与盐雾定向", "晶风辨位", 135, "三十八岁棕肤；瘦长脸；剃光头留靛蓝长辫；白色眉毛；高瘦；盐晶罗盘披肩", "阿希尔追上无旗盐船，看见盐人长着一张和自己一模一样的脸，正回头望来。此后他不再照抄账册里的港名，而把它们逐一换成航向。六港恰好落在罗盘的六个刻度，连线又像一面无王徽的旗；每次重画，他都留着手边那撮盐。", "seventh_berth_04", "crew_salt_crown_navigator_ashir_fullbody"),
            Crew("crew_salt_crown_carpenter_saf", "盐冠港", 1, "晒骨匠 萨芙", "耐盐船材与兽骨结构", "白骨封缝", 155, "五十二岁深褐肤色；三角脸；白色短脏辫；额头晒斑；精瘦硬朗；兽骨支架与盐蜡锤", "萨芙晒船材时，从不许人踩乱盐田里的影子。活龙骨在烈日下显出归潮舰队历次返航的日期，末行却始终空白。有人请她补刻一个祭日，她把凿子收回腰间：船还没回来，怎么就急着替它写完。", "seventh_berth_07", "crew_salt_crown_carpenter_saf_fullbody"),
            Crew("crew_salt_crown_doctor_naji", "盐冠港", 2, "荒潮药师 娜吉", "脱水症与矿盐药剂", "苦盐回魂", 175, "二十九岁褐肤；杏眼圆脸；黑发盘成双环；眼下金色盐纹；娇小；彩釉药瓶腰带", "娜吉的药盘里，六种矿盐须按粗细摆齐。她用它们分离伤员体内的黑沙，细沙竟排成第七港口的平面图，粗沙则聚成一颗心脏的模样。那颗心仍在风暴眼里跳动，她看着盘底的起伏，迟迟没有倒掉药液。", "seventh_berth_08", "crew_salt_crown_doctor_naji_fullbody"),
            Crew("crew_salt_crown_gunner_rokan", "盐冠港", 3, "白焰炮手 罗坎", "高温火药与逆风炮战", "盐硝白焰", 195, "四十一岁浅褐肤色；厚唇方脸；焦白莫西干发；双手灼伤；宽肩；白陶火药罐", "罗坎握装药勺的手满是旧灼痕，动作却轻得像舀糖。他为潮门旧炮配出无烟白焰，巨链断裂时，已故的炮术老师竟站在火中，以手势报出下一次齐射的坐标。罗坎把那组数刻进陶罐，谁也不许擦。", "seventh_berth_09", "crew_salt_crown_gunner_rokan_fullbody"),
            Crew("crew_salt_crown_cook_maz", "盐冠港", 4, "香路厨娘 玛兹", "香料保存与商队大锅", "六味盐冠", 215, "三十七岁橄榄肤色；菱形脸绿眼；黑色波浪长发；鼻侧痣；丰满；六格香料盘与银锅", "玛兹总说，祖传的锅比掌勺的人还挑嘴。锅底六道缺口一直补不上，直到她依次煮遍六港菜谱，蒸汽中才浮出盐冠的影子，显出开门的次序。厨房深处，那位沉睡的旧舰队厨子也在此时睁开了眼。", "seventh_berth_10", "crew_salt_crown_cook_maz_fullbody"),
            Crew("crew_salt_crown_diver_isha", "盐冠港", 5, "盐井潜客 伊沙", "地下盐水井与狭洞潜行", "井路穿港", 250, "二十二岁深褐肤色；圆脸大眼；短白卷发；耳廓盐晶穿孔；柔韧矮小；皮革软盔与晶灯", "伊沙钻窄洞时，会把晶灯先推过最黑的一段。她在盐井底打开潜水钟的门，迎面却是暮潮港井房的湿风。沿路留下的灯后来被画进同一张图，六段光点拼成一把钥匙；她仍记得每一盏灯挂在哪块石头上。", "seventh_berth_11", "crew_salt_crown_diver_isha_fullbody"),

            // 暮潮港：月潮、蓝焰巫术、深海生物与梦境医学。
            Crew("crew_twilight_tide_navigator_selene", "暮潮港", 0, "月潮占星师 塞勒涅", "夜航星位与巨兽迁徙", "巨背星座", 145, "六十五岁深肤；细长脸银色瞳孔；及腰白直发；无眉；高挑清瘦；石质星盘与月纱", "塞勒涅拨星盘时，指尖常停在中央的缺口。她曾随老船长在海巨人背上度过七昼夜，把巨人背上的旧伤画成星图。回到船上后，老船长把巨人赠予的石盘交给她保管。那块石盘恰能嵌入断锚黑玻璃。可她的航图到“第七”处总少一笔，只推说是潮汐擦掉了。", "seventh_berth_06", "crew_twilight_tide_navigator_selene_fullbody"),
            Crew("crew_twilight_tide_carpenter_orlan", "暮潮港", 1, "骨舟匠 欧兰", "海兽骨材与静音船体", "鲸骨共鸣", 165, "三十四岁苍白肤色；尖下巴；紫黑长发遮半脸；颈侧鳃状刺青；纤长；弯月骨锯", "欧兰锯骨之前，会贴近骨节听上片刻。活龙骨一遍遍向他说“回家”，他便把这个词写在工坊门内。六港船匠拼合刨花那天，第七港口的船竟同时应声；自那以后，他夜里收工也不肯把门完全关上。", "seventh_berth_07", "crew_twilight_tide_carpenter_orlan_fullbody"),
            Crew("crew_twilight_tide_doctor_viya", "暮潮港", 2, "梦疫医师 维娅", "梦魇病与深海诅咒", "借梦问诊", 185, "四十六岁橄榄肤色；疲惫杏眼；深蓝短卷发；额心银点；瘦削；装满梦砂的玻璃面罩", "维娅问诊，总先问梦里闻见了什么。她让无名伤员走进同一个梦：晨光照着石港，账簿堆着，等人点火。醒来时，一把现实中不该存在的钥匙硌在她掌心，而井底少了一座潜水钟；她把两件事记在同一页。", "seventh_berth_12", "crew_twilight_tide_doctor_viya_fullbody"),
            Crew("crew_twilight_tide_gunner_yilan", "暮潮港", 3, "蓝火炮巫 伊岚", "咒弹、驱歌与灵体炮击", "静歌蓝焰", 205, "二十六岁东方面孔；锐利凤眼；乌发夹钴蓝挑染；右颊符文；修长；蓝火符管长炮", "伊岚倒空弹壳时，会留意壳内残存的半句誓词。她曾用咒弹击败海妖弥赛拉，却放下了引火杖，听完对方的警告。蓝火烧去的字只在拒绝贡品时显现，她沿着这些字，寻找归潮舰队仍然存在的证据。", "seventh_berth_05", "crew_twilight_tide_gunner_yilan_fullbody"),
            Crew("crew_twilight_tide_cook_lume", "暮潮港", 4, "夜宴厨师 露姆", "夜光菌料理与安神茶", "月菌夜宴", 225, "三十岁蓝黑肤色；娃娃脸；蓬松紫发；面颊荧光雀斑；圆润；发光菌篮与黑釉茶壶", "露姆试汤先熄灶，让夜光菌把碗沿照亮。她为获释的海妖摆过一桌无盐夜宴，弥赛拉吃完，唱起归潮舰队的安魂曲。唱到一处停顿，露姆忽然放下汤勺：盐冠配方故意删去的那一拍，原来就在这里。", "seventh_berth_13", "crew_twilight_tide_cook_lume_fullbody"),
            Crew("crew_twilight_tide_diver_naive", "暮潮港", 5, "幽灯潜者 奈芙", "黑水潜行与幽光追踪", "幽灯引路", 260, "五十八岁苍白肤色；长方脸；剃顶留两侧银辫；一道贯穿鼻梁的蓝疤；高瘦；水母灯与骨质呼吸管", "奈芙让水母灯走在前面，自己贴着暗处下潜。她循伊沙的晶灯穿过六口井，在第七港口下方找到了钥匙槽。槽边的新刻痕还锋利，写着开门者须先听完海妖的赎价；她收起钥匙，沿原路退回水中。", "seventh_berth_13", "crew_twilight_tide_diver_naive_fullbody"),

            // 灰鸥港：原六名角色保留为旧存档、旧索引和策划案 ID 的兼容名册。
            Crew("crew_gray_gull_navigator_evelyn", "灰鸥港", 0, "星盘手 伊芙琳", "远航领航与迷雾测绘", "星盘定位", 90, "三十二岁橄榄肤色；长椭圆脸与灰绿色眼睛；黑色波浪发配青头巾；修长；金星盘", "伊芙琳在酒馆抄图，墨还没干就肯借人看。她曾任老船长的领航副手，将巨人星图誊进六港航册，熬过浓雾长夜。如今有人出高价买断航线，她便把图摊得更开，只问下一位来客愿不愿让别人也平安走过。", "seventh_berth_06", "crew_navigator_evelyn_fullbody", true, true),
            Crew("crew_gray_gull_carpenter_barlow", "灰鸥港", 1, "船匠 巴洛", "船体维护与航后抢修", "龙骨急修", 115, "四十五岁晒红肤色；宽鼻方脸；红头巾下深棕短发；浓胡须；魁梧；木槌与绳圈", "巴洛进坞先点人数，才拎起腰间的木槌。他是老船长亲手挑中的龙骨匠，见过第七港口第一根系船桩入水。只要还有一名船员没能靠岸，修船的清单列得再周全，也得先搁下；等人齐了，他才蹲下去摸第一块坏板。", "seventh_berth_07", "crew_carpenter_barlow_fullbody", true, true),
            Crew("crew_gray_gull_doctor_moer", "灰鸥港", 2, "船医 莫尔", "伤病处置与药品管理", "冷静诊断", 140, "三十九岁浅棕肤色；窄脸与圆眼镜；黑色中分卷发；下巴短须；清瘦；青玻璃药瓶", "莫尔收治伤员时，会先把对方腰间的刀放远些。他随老船长远征时记下的病历，第一页写着“伤者先靠岸”，后面才按病症分类。他一直想开一间没有门槛的诊所，也在等一位肯把敌人抬上甲板的船长。", "seventh_berth_08", "crew_doctor_moer_fullbody", true, true),
            Crew("crew_gray_gull_gunner_leisa", "灰鸥港", 3, "炮术官 雷萨", "舰炮校准与火力指挥", "火线校准", 165, "三十岁深棕肤色；棱角脸；赤褐长发束高马尾；右眉缺口；健壮；长引火杖", "雷萨在酒馆教炮术，先让学徒摆出获救小艇的位置。她与老船长共同击断过潮门巨链，也见过旧部把同样的炮转向昔日伙伴。她很少夸谁射得准，只反复检查炮口：这一发下去，谁还能平安回家。", "seventh_berth_09", "crew_gunner_leisa_fullbody", true, true),
            Crew("crew_gray_gull_cook_pepe", "灰鸥港", 4, "厨师 佩佩", "远航膳食与口粮管理", "盐锅配给", 190, "五十四岁棕肤；饱满圆脸；深灰卷发藏于白头巾；笑纹明显；丰腴；刻痕盐锅", "佩佩盛汤，总给空凳子也留一只碗。她曾为老船长掌厨，把六港菜谱分给返乡船，如今把盐锅王冠藏在酒馆后厨。谈起第七港口，她不画宏伟的码头，只把火拨旺一些：人到了，至少得有口热的。", "seventh_berth_10", "crew_cook_pepe_fullbody", true, true),
            Crew("crew_gray_gull_diver_noya", "灰鸥港", 5, "潜水员 诺雅", "沉船打捞与宝藏勘探", "深水寻迹", 215, "二十五岁浅褐肤色；心形脸；乌黑双辫；鼻梁细疤；娇小敏捷；青铜潜水镜", "诺雅收好潜水镜，才肯展开那面碎帆旗。那是老船长离港前亲手托付的东西，她舍不得让缝线再沾一次盐。沉底铜牌背面的刻痕已指明出发方向，她仍留在酒馆，只等有人能把六港的人请上同一艘船。", "seventh_berth_15", "crew_diver_noya_fullbody", true),
        };

        private static readonly IReadOnlyList<string> ports = Array.AsReadOnly(portNames);
        private static readonly IReadOnlyList<string> professions = Array.AsReadOnly(professionIds);
        private static readonly IReadOnlyList<CrewStoryDefinition> stories = Array.AsReadOnly(storyArray);
        private static readonly IReadOnlyList<CrewVariantDefinition> allCrew = Array.AsReadOnly(crewArray);
        private static readonly Dictionary<string, CrewStoryDefinition> storyById = BuildStoryLookup();
        private static readonly Dictionary<string, IReadOnlyList<CrewVariantDefinition>> rosterByPort = BuildPortRosters();
        private static readonly Dictionary<string, CrewVariantDefinition> crewById = BuildCrewLookup();

        public static IReadOnlyList<string> Ports => ports;
        public static IReadOnlyList<string> ProfessionIds => professions;
        public static IReadOnlyList<CrewStoryDefinition> Stories => stories;
        public static IReadOnlyList<CrewVariantDefinition> AllCrew => allCrew;

        public static IReadOnlyList<CrewVariantDefinition> ForPort(string portName)
        {
            if (!string.IsNullOrWhiteSpace(portName) && rosterByPort.TryGetValue(portName, out IReadOnlyList<CrewVariantDefinition> roster))
            {
                return roster;
            }

            return rosterByPort["灰鸥港"];
        }

        public static CrewVariantDefinition ForPortAndProfession(string portName, int professionIndex)
        {
            IReadOnlyList<CrewVariantDefinition> roster = ForPort(portName);
            int safe = Math.Max(0, Math.Min(ProfessionCount - 1, professionIndex));
            return roster[safe];
        }

        public static CrewVariantDefinition LegacyForProfession(int professionIndex)
        {
            return ForPortAndProfession("灰鸥港", professionIndex);
        }

        public static CrewStoryDefinition StoryFor(CrewVariantDefinition crew)
        {
            return crew != null && storyById.TryGetValue(crew.StoryId, out CrewStoryDefinition story) ? story : storyArray[0];
        }

        public static CrewVariantDefinition FindById(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && crewById.TryGetValue(id, out CrewVariantDefinition crew) ? crew : null;
        }

        // Regional identities have distinct life stories, not unimplemented combat
        // passives. Keep the visible rules in sync with CoreVoyageSession.
        public static string GameplaySkillForProfession(int professionIndex)
        {
            switch (Math.Max(0, Math.Min(ProfessionCount - 1, professionIndex)))
            {
                case 0: return "每位随船航海士在出航时揭示 2 个固定事件点，可叠加；视野 +60 海里、航速 +1 节、伏击预警 +38% 不叠加；开启领航选项，非失败返航可保存海图。";
                case 1: return "每次赢得海战后恢复 2 点船体；开启需要木匠的事件选项。";
                case 2: return "事件药品消耗降低 25%（向上取整，至少 1）；事件恢复船体时额外恢复 2 点。";
                case 3: return "提供炮手专属战斗卡牌；开启需要炮手的事件选项。";
                case 4: return "每日口粮消耗降低 10%；开启需要厨师的事件选项。";
                default: return "事件获得的木材和铁矿增加 25%（向下取整，至少额外 1）。";
            }
        }

        private static CrewStoryDefinition Story(string id, int chapter, string title, string body, string previousId, string nextId)
        {
            return new CrewStoryDefinition(id, chapter, title, body, previousId, nextId);
        }

        private static CrewVariantDefinition Crew(
            string id,
            string port,
            int professionIndex,
            string name,
            string role,
            string specialty,
            int cost,
            string appearance,
            string biography,
            string storyId,
            string asset,
            bool legacy = false,
            bool formerOldCaptain = false)
        {
            return new CrewVariantDefinition(id, port, professionIndex, professionIds[professionIndex], name, role, specialty,
                GameplaySkillForProfession(professionIndex), professionIndex == 0 ? "揭示固定事件点按人数叠加，其他同职业能力不叠加" : "同职业能力不叠加", cost, appearance,
                biography, storyId, asset, legacy, formerOldCaptain);
        }

        private static Dictionary<string, CrewStoryDefinition> BuildStoryLookup()
        {
            Dictionary<string, CrewStoryDefinition> result = new Dictionary<string, CrewStoryDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < storyArray.Length; index += 1) result[storyArray[index].Id] = storyArray[index];
            return result;
        }

        private static Dictionary<string, CrewVariantDefinition> BuildCrewLookup()
        {
            Dictionary<string, CrewVariantDefinition> result = new Dictionary<string, CrewVariantDefinition>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < crewArray.Length; index += 1) result[crewArray[index].Id] = crewArray[index];

            // Old browser-demo IDs and the original six profession IDs resolve
            // to the preserved Gray Gull identities rather than losing saves.
            string[][] aliases =
            {
                new[] { "navigator", "crew_mad_navigator", "crew_navigator_evelyn", "tavern_crew_0" },
                new[] { "carpenter", "crew_old_carpenter", "crew_carpenter_barlow", "tavern_crew_1" },
                new[] { "doctor", "crew_cold_doctor", "crew_doctor_moer", "tavern_crew_2" },
                new[] { "gunner", "crew_one_eye_gunner", "crew_gunner_leisa", "tavern_crew_3" },
                new[] { "cook", "crew_salt_pot_cook", "crew_cook_pepe", "tavern_crew_4" },
                new[] { "diver", "crew_treasure_diver", "crew_diver_noya", "tavern_crew_5" },
            };
            for (int profession = 0; profession < aliases.Length; profession += 1)
            {
                CrewVariantDefinition legacy = LegacyForProfession(profession);
                for (int index = 0; index < aliases[profession].Length; index += 1) result[aliases[profession][index]] = legacy;
            }
            return result;
        }

        private static Dictionary<string, IReadOnlyList<CrewVariantDefinition>> BuildPortRosters()
        {
            Dictionary<string, IReadOnlyList<CrewVariantDefinition>> result = new Dictionary<string, IReadOnlyList<CrewVariantDefinition>>(StringComparer.Ordinal);
            for (int portIndex = 0; portIndex < portNames.Length; portIndex += 1)
            {
                CrewVariantDefinition[] roster = new CrewVariantDefinition[ProfessionCount];
                for (int crewIndex = 0; crewIndex < crewArray.Length; crewIndex += 1)
                {
                    CrewVariantDefinition crew = crewArray[crewIndex];
                    if (crew.PortName == portNames[portIndex]) roster[crew.ProfessionIndex] = crew;
                }
                result[portNames[portIndex]] = Array.AsReadOnly(roster);
            }
            return result;
        }
    }
}
