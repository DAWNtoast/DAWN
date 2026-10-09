using System;
using System.Collections.Generic;

namespace ShatteredSail.Content
{
    /// <summary>
    /// The archive deliberately separates testimony from a lead.  A confirmed
    /// thread is written in a surviving log, while an inferred thread is only
    /// assembled from a tool mark, a route, or a witness who arrived too late.
    /// Keeping the distinction in the data makes the mystery visible without
    /// turning every item into an exposition dump.
    /// </summary>
    public enum SeventhPortLoreRelation
    {
        ConfirmedOldCaptainRecord,
        InferredFromClues,
    }

    /// <summary>
    /// Shared world-building anchors for the game "第七港口".  Gameplay
    /// catalogs keep their old IDs for save compatibility; this catalog gives
    /// ships, crew, components, and keepsakes one explicit narrative map.
    /// </summary>
    public sealed class SeventhPortFormerLieutenant
    {
        public SeventhPortFormerLieutenant(string id, string name, string fleetId, string fate, bool stillSearching)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            FleetId = fleetId ?? string.Empty;
            Fate = fate ?? string.Empty;
            StillSearching = stillSearching;
        }

        public string Id { get; }
        public string Name { get; }
        public string FleetId { get; }
        public string Fate { get; }
        public bool StillSearching { get; }
    }

    public sealed class SeventhPortStoryLink
    {
        public SeventhPortStoryLink(string chapterId, string[] shipIds, string[] crewIds,
            string[] componentIds, string[] treasureIds)
        {
            ChapterId = chapterId ?? string.Empty;
            ShipIds = Array.AsReadOnly(shipIds ?? new string[0]);
            CrewIds = Array.AsReadOnly(crewIds ?? new string[0]);
            ComponentIds = Array.AsReadOnly(componentIds ?? new string[0]);
            TreasureIds = Array.AsReadOnly(treasureIds ?? new string[0]);
        }

        public string ChapterId { get; }
        public IReadOnlyList<string> ShipIds { get; }
        public IReadOnlyList<string> CrewIds { get; }
        public IReadOnlyList<string> ComponentIds { get; }
        public IReadOnlyList<string> TreasureIds { get; }
    }

    /// <summary>
    /// A compact item-level lore card.  Half of the authored cards name the
    /// old captain directly; the remaining cards deliberately use witnesses,
    /// dates and repeated symbols so players can infer the connection instead
    /// of receiving a definitive answer.
    /// </summary>
    public sealed class SeventhPortLoreEntry
    {
        public SeventhPortLoreEntry(string id, string description, bool namesOldCaptain)
        {
            Id = id ?? string.Empty;
            Description = description ?? string.Empty;
            NamesOldCaptain = namesOldCaptain;
        }

        public string Id { get; }
        public string Description { get; }
        public bool NamesOldCaptain { get; }
        public SeventhPortLoreRelation Relation => NamesOldCaptain
            ? SeventhPortLoreRelation.ConfirmedOldCaptainRecord
            : SeventhPortLoreRelation.InferredFromClues;
        public string RelationLabel => NamesOldCaptain ? "老船长亲历 · 航海档案" : "线索推断 · 尚待考证";
    }

    public static class SeventhPortLoreCatalog
    {
        public const string GameTitle = "第七港口";
        public const string OldCaptainTitle = "无冠老船长";
        public const string Premise =
            "年少离家的老船长曾把六港的逃亡者、渔民和工匠带上同一面无王徽的帆。他们跨过千山万水，与风暴、海神和收税的炮台争路，只为建一座没有贵族泊位的第七港口，让每个人都能带着自己的名字靠岸。";
        public const string CurrentIntroduction =
            "胜利短暂地打开了潮门，老船长却发现有些部下已把自由旗换成了黑帆：有人收起港税，有人借神谕审判，有人把救援变成掠夺。他难过而不甘，留下罗盘、铜钟和一张未完成的海图，独自驶向无人灯塔。如今你寻找的不是一座现成的城，而是在收集部件、招募船员、探索海路和面对旧部的选择中，逐渐成为愿为天下留泊位的新船长。";
        public const string PlayerPromise =
            "每件部件、船只、船员和收藏品都只保存一半答案：一半来自老船长亲历的航海档案，另一半只能从磨痕、歌声和失真的传闻中推断。你要击败忘记初心的旧部，与仍在寻找老船长的人切磋，并在建设第七港口的过程中把自由旗交给下一位后来者。";

        private static readonly IReadOnlyList<string> formerCrewIds = Array.AsReadOnly(new[]
        {
            "crew_gray_gull_navigator_evelyn",
            "crew_gray_gull_carpenter_barlow",
            "crew_gray_gull_doctor_moer",
            "crew_gray_gull_gunner_leisa",
            "crew_gray_gull_cook_pepe",
        });

        private static readonly IReadOnlyList<SeventhPortFormerLieutenant> formerLieutenants = Array.AsReadOnly(new[]
        {
            new SeventhPortFormerLieutenant("iron_oath", "铁誓·达恩", "legendary_pirate_iron_oath", "投靠帝国，以装甲和港税锁住航道。", false),
            new SeventhPortFormerLieutenant("thunder_crown", "索尔", "legendary_pirate_thunder_crown", "把雷暴当神谕，审判不缴税的船。", false),
            new SeventhPortFormerLieutenant("red_widow", "绯刃·玛拉", "legendary_pirate_red_widow", "把救援队变成收取保护费的登船队。", false),
            new SeventhPortFormerLieutenant("ember_ark", "余烬·伊诺", "legendary_pirate_ember_ark", "向火神换取永燃之火，烧毁了自由誓。", false),
            new SeventhPortFormerLieutenant("tide_requiem", "潮汐·塞勒涅", "legendary_pirate_tide_requiem", "仍沿旧航路寻找老船长，接受非致命的切磋。", true),
        });

        private static readonly string[] formerCrewIdsArray =
        {
            "crew_gray_gull_navigator_evelyn", "crew_gray_gull_carpenter_barlow",
            "crew_gray_gull_doctor_moer", "crew_gray_gull_gunner_leisa", "crew_gray_gull_cook_pepe",
        };

        private static readonly IReadOnlyList<SeventhPortStoryLink> links = Array.AsReadOnly(new[]
        {
            Link("seventh_berth_01", new[] { "sloop", "salvage" }, new[] { "crew_broken_anchor_navigator_lorne" }, new[] { "cannon_deck_swivel" }, new[] { "first_victory_compass" }),
            Link("seventh_berth_02", new[] { "sloop", "merchant", "courier", "provision" }, new[] { "crew_white_shell_carpenter_atang" }, new[] { "sail_whisperveil", "cannon_gullcry_chaser" }, new string[0]),
            Link("seventh_berth_03", new[] { "patrol", "gunship" }, new[] { "crew_bronze_bell_gunner_helen" }, new[] { "cannon_voidbell_mortar" }, new[] { "boatswain_bell" }),
            Link("seventh_berth_04", new[] { "merchant", "barge", "privateer" }, new[] { "crew_salt_crown_navigator_ashir" }, new[] { "instrument_guild_engine" }, new string[0]),
            Link("seventh_berth_05", new[] { "patrol" }, new[] { "crew_twilight_tide_gunner_yilan" }, new[] { "bow_gilded_mermaid", "cannon_blueflare_organ" }, new string[0]),
            Link("seventh_berth_06", new[] { "relic", "occult" }, new[] { "crew_twilight_tide_navigator_selene" }, new[] { "instrument_lunar_astrolabe", "bow_voidray_edge" }, new string[0]),
            Link("seventh_berth_07", new[] { "sloop", "relic" }, new[] { "crew_white_shell_carpenter_atang" }, new[] { "plating_living_hull", "sail_ghostcurrent_canvas" }, new string[0]),
            Link("seventh_berth_08", new[] { "patrol" }, new[] { "crew_bronze_bell_doctor_ada" }, new[] { "plating_redbanner_wall", "instrument_deepwater_abacus" }, new string[0]),
            Link("seventh_berth_09", new[] { "patrol" }, new[] { "crew_bronze_bell_gunner_helen" }, new[] { "cannon_anchorbreaker", "cannon_chain_repeater" }, new string[0]),
            Link("seventh_berth_10", new[] { "merchant", "clipper" }, new[] { "crew_gray_gull_cook_pepe" }, new[] { "plating_suncoin_rampart", "sail_emerald_trade_wing" }, new string[0]),
            Link("seventh_berth_11", new[] { "relic" }, new[] { "crew_gray_gull_diver_noya" }, new[] { "instrument_deepwater_abacus" }, new[] { "boatswain_bell", "sealed_chart" }),
            Link("seventh_berth_12", new[] { "relic" }, new[] { "crew_gray_gull_diver_noya" }, new[] { "sail_last_free_wind" }, new[] { "sealed_chart" }),
            Link("seventh_berth_13", new string[0], new[] { "crew_twilight_tide_gunner_yilan" }, new[] { "bow_gilded_mermaid" }, new string[0]),
            Link("seventh_berth_14", new[] { "sloop", "merchant", "patrol", "relic" }, formerCrewIdsArray, new[] { "sail_last_free_wind" }, new[] { "first_victory_compass", "boatswain_bell", "sealed_chart" }),
            Link("seventh_berth_15", new[] { "sloop", "merchant", "patrol", "relic" }, formerCrewIdsArray, new[] { "bow_last_horizon", "sail_last_free_wind" }, new[] { "first_victory_compass", "boatswain_bell", "sealed_chart" }),
        });

        // These short cards are intentionally split 50/50 between explicit
        // history and suggestive evidence.  The longer chapter text remains
        // in CrewNarrativeCatalog; these entries are suitable for shipyard,
        // component-shop and collection detail panels.
        private static readonly IReadOnlyList<SeventhPortLoreEntry> componentLore = Array.AsReadOnly(new[]
        {
            Lore("cannon_deck_swivel", "这门转炮守过第一座公共泊位。炮座背面仍钉着老船长的命令：让开归港水道，只向风暴与封路的铁链开火。", true),
            Lore("instrument_brass_compass", "黄铜齿轮的凹槽积着细盐。老船长曾用同样的齿轮校过六港潮线，留下的刻痕不许任何港主独占这条路。", true),
            Lore("plating_living_hull", "老船长的船匠共同铸成这层活龙骨。乘客走过甲板，木纹便记下他们的重量；人离船后，痕迹仍留在原处。", true),
            Lore("sail_last_free_wind", "离港那日，老船长剪下这幅帆角。针脚一直朝向海图的空白处，谁也没能把第七泊位画在缝线尽头。", true),
            Lore("bow_last_horizon", "老船长独自驶入风暴前留下的船首像。他说前头若有拦路的浪，船首须先替陌生人撞开。", true),
            Lore("bow_oak_ram", "断锚号旧账只记了“橡木五铆”，没有署名。铆钉排列却与六港公共船坞相同，像同一把尺量过。", false),
            Lore("cannon_chain_repeater", "连射停下后，总还多出一声回响。铜钟港老人说那是第十三响的回声；谁校过炮膛，却无人知晓。", false),
            Lore("instrument_lunar_astrolabe", "月蚀时，指针偏向海图上没有的航线。潮汐镇魂号的日志也空着同一段，未写抵达，亦未写返航。", false),
            Lore("sail_moonweave", "第一匹月丝从哪里来，暮潮织工始终不说。只记得织机旁有人哼过一支自由歌，没有歌词，也没有唱完。", false),
            Lore("plating_moonsilver_skin", "银壳上叠着六港不同的补片，先后次序恰与那场远征的年份相合。没有船匠肯说自己见过它最初的模样。", false),
        });

        private static readonly IReadOnlyList<SeventhPortLoreEntry> shipLore = Array.AsReadOnly(new[]
        {
            Lore("sloop", "老船长修好的第一艘船没有王徽，备用舱里添了几排吊床钩，留给六港逃亡者。断锚港旧账与洛恩的潮汐笔记，都记着穿过碎帆礁海、升起第一面自由旗的那一夜。", true),
            Lore("patrol", "炮塔上留着第十三道校炮痕，槽里压满海盐。潮门之战中，老船长把巡防舰改作护航舰，让炮口守住撤离水道。刻痕记下的不是击沉数，而是伤员与难民船通过时的角度。", true),
            Lore("merchant", "舱壁刻着六个港名，税印早已刮净。金潮号的船员说，它从不错过集市，也总空出一格舱位给付不起船钱的归乡人。账页与木屑指向同一条旧航线，尚不能证明这与老船长有关。", false),
            Lore("relic", "无月夜里，月蚀号的龙骨自行转向北方。有人认出紫帆上那枚缺角星盘，说它记着老船长独自驶入无人灯塔的末次航迹。也有人只当这是寻宝者借来的故事；两种说法都无从证实。", false),
            Lore("salvage", "断锚港的拾潮船出自老船长主持的公共船坞。宽舱先装沉船木料，再让获救水手登船。账册末页写得清楚：先修好下一艘求援的船，再来分这次打捞所得。墨迹已渗进纸背。", true),
            Lore("courier", "六色线缝在白羽邮船的帆脚，像六封未能投递的家书。白贝港邮差说，它曾追上那次没有归期的远征。旧邮袋翻过一遍又一遍，始终找不到老船长的回信，也无人知道信是否寄出。", false),
            Lore("provision", "丰穗船的舷侧还看得出封起炮位的接缝。老船长让白贝港船匠腾出这片地方，装淡水桶与粮袋。首航名册逐一写着六港饥民的姓名；货主的封号，在整本册子里找不到一处。", true),
            Lore("gunship", "雷铸重炮舰的炮座沿用潮门之战的图纸，铜钟港还存着老船长签署的装配令。厚甲须朝向撤离船队的外侧，重弹留给封港铁链。新炮手上舰时，总要先读完这张被油浸透的纸。", true),
            Lore("barge", "盐山货驳的舱梁刻满运输单，件数还在，税额一栏却全是空白。货物堆到最满时，底舱仍留一条窄路通向甲板。盐冠港搬运工说，那是给后来者留的；刻字的人早已没有姓名。", false),
            Lore("privateer", "誓金号的船舵后钉着一纸褪色协约，老船长与盐冠商会的签名尚可辨认。它凭这份约定护送粮船，炮手薪酬从运费中支取。末行写得很重：不得再向过路的船索取一枚钱。", true),
            Lore("occult", "浓雾里，夜灯船的舷灯只照得见近旁的水面。舱内十二张吊床旁偏偏多出一枚空钩，上面留着旧罗盘磨出的浅痕。暮潮港有人认得那个印子，却无人肯说，老船长是否曾在这里过夜。", false),
            Lore("clipper", "逐风船的长帆拼着六港不同的剪裁，远看几乎分不出接缝。货舱门内有一句未署名的话：快一天，就能多等一个人。灰鸥港船匠修船时从不刨掉这行字，只说是老习惯，不肯再往下讲。", false),
        });

        private static readonly IReadOnlyList<SeventhPortLoreEntry> crewLore = Array.AsReadOnly(new[]
        {
            Lore("crew_gray_gull_navigator_evelyn", "伊芙琳是老船长的领航副手，曾把巨人星图抄进六港航册，并坚持公开安全航线。", true),
            Lore("crew_gray_gull_carpenter_barlow", "巴洛由老船长亲手挑作龙骨匠；他先确认所有人能靠岸，才决定修哪一块木板。", true),
            Lore("crew_gray_gull_doctor_moer", "莫尔保存着老船长远征的原始病历，第一页写着‘伤者先靠岸’，敌我不分。", true),
            Lore("crew_gray_gull_gunner_leisa", "雷萨与老船长共同击断潮门巨链，把炮术定义为护航而非征服。", true),
            Lore("crew_gray_gull_cook_pepe", "佩佩是老船长远征的掌厨，她把六港菜谱分给返乡船，让第七港口先从一碗热汤开始。", true),
            Lore("crew_twilight_tide_navigator_selene", "塞勒涅的航图在‘第七’处总会少一笔；她说那是潮汐擦掉的，也可能是有人故意留下的空位。", false),
            Lore("crew_twilight_tide_gunner_yilan", "伊岚的咒弹壳内刻着半句旧誓词，另一半被蓝火烧掉，只有在拒绝贡品时才会显字。", false),
            Lore("crew_white_shell_diver_kela", "珂拉从银壳听见一段熟悉的船歌；歌声来自谁、为何指向第七泊位，仍没有定论。", false),
            Lore("crew_bronze_bell_navigator_vick", "维克破解薄铜海图时故意留下一行不译的密码，笔画像六港旧旗，却找不到原作者。", false),
            Lore("crew_salt_crown_navigator_ashir", "阿希尔把账册港名改成航向，六个刻度恰好围成一面没有王徽的旗。", false),
        });

        private static readonly IReadOnlyList<SeventhPortLoreEntry> treasureLore = Array.AsReadOnly(new[]
        {
            Lore("first_victory_compass", "老船长初次远航用过的黄铜罗盘，壳上留着盐斑。指针不追逐宝藏，始终偏向那些愿为旁人留下泊位的岸。", true),
            Lore("boatswain_bell", "钟沿已被盐风磨薄，第十三响仍听得清。老船长远征时用它召集六港船队；此后，只有护送与救援会再敲响它。", true),
            Lore("sealed_chart", "离港前，老船长把这张封蜡海图交给六港。旧线条隐在蜡下，互助之后才浮出一段，像有人隔着海补上未写完的航程。", true),
            Lore("old_captain_free_flag", "这是老船长第一次拒缴港税时升起的旗。颜色褪了，六港水手仍认得帆布上的旧针脚，知道旗下还有一处可以靠岸。", true),
            Lore("unfinished_seventh_berth_key", "六格钥齿已经锉好，第七格还留着粗坯。老船长把最后这道活，留给愿替天下过路人守住泊位的人。", true),
            Lore("moon_tide_astrolabe", "月蚀时，盘上多出一道海图未记的潮线。塞勒涅循线航行，却从不说明潮线的尽头，是否有人等她。", false),
            Lore("iron_oath_shackle", "税印曾被磨掉三次。最后一层锉痕斜向一旁，恰与六港公共码头的石阶同向；锁环没有留下动手者的名字。", false),
            Lore("red_widow_grappling_hook", "一端钩便于托住落水的人，另一端适合拖船。绯刃旧队何时把救人的索变成拦船的索，众人的说法并不一致。", false),
            Lore("empty_berth_registry", "每页都写着“留给下一个人”，没有落款。纸张出自老船长离港的那一年，执笔者是谁，至今没有查清。", false),
            Lore("seventh_harbor_beacon_lantern", "未点的灯芯旁，浮着一艘艘受过帮助的船影。有人说灯亮了，有人只看见水光；谁也说不清，究竟是什么在回应。", false),
        });

        public static IReadOnlyList<string> FormerCaptainCrewIds => formerCrewIds;
        public static IReadOnlyList<SeventhPortFormerLieutenant> FormerLieutenants => formerLieutenants;
        public static IReadOnlyList<SeventhPortStoryLink> StoryLinks => links;
        public static SeventhPortLoreEntry ShipLoreFor(string shipId)
        {
            for (int index = 0; index < shipLore.Count; index += 1)
                if (string.Equals(shipLore[index].Id, shipId, StringComparison.Ordinal)) return shipLore[index];
            return null;
        }

        public static SeventhPortLoreRelation RelationForStory(string storyId)
        {
            // Chapters 01–07 and the old cannon chapter are signed testimony;
            // the departure, fallen fleets and the unfinished port survive only
            // as clues. This intentionally leaves the player room to disagree.
            if (string.IsNullOrWhiteSpace(storyId)) return SeventhPortLoreRelation.InferredFromClues;
            for (int index = 0; index < links.Count; index += 1)
            {
                if (!string.Equals(links[index].ChapterId, storyId, StringComparison.Ordinal)) continue;
                if (storyId.Length < 2) return SeventhPortLoreRelation.InferredFromClues;
                string suffix = storyId.Substring(storyId.Length - 2);
                if (suffix == "01" || suffix == "02" || suffix == "03" || suffix == "04" ||
                    suffix == "05" || suffix == "06" || suffix == "07" || suffix == "09")
                    return SeventhPortLoreRelation.ConfirmedOldCaptainRecord;
                return SeventhPortLoreRelation.InferredFromClues;
            }
            return SeventhPortLoreRelation.InferredFromClues;
        }

        public static string RelationLabelForStory(string storyId)
        {
            return RelationForStory(storyId) == SeventhPortLoreRelation.ConfirmedOldCaptainRecord
                ? "老船长亲历 · 航海档案"
                : "线索推断 · 尚待考证";
        }
        public static IReadOnlyList<SeventhPortLoreEntry> ComponentLore => componentLore;
        public static IReadOnlyList<SeventhPortLoreEntry> ShipLore => shipLore;
        public static IReadOnlyList<SeventhPortLoreEntry> CrewLore => crewLore;
        public static IReadOnlyList<SeventhPortLoreEntry> TreasureLore => treasureLore;

        public static string DescriptionFor(IReadOnlyList<SeventhPortLoreEntry> source, string id, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(id))
                for (int index = 0; index < source.Count; index += 1)
                    if (string.Equals(source[index].Id, id, StringComparison.Ordinal)) return source[index].Description;
            return fallback ?? string.Empty;
        }

        public static string DescribeComponent(string id, string fallback) => DescriptionFor(componentLore, id, fallback);
        public static string DescribeShip(string id, string fallback) => DescriptionFor(shipLore, id, fallback);
        public static string DescribeCrew(string id, string fallback) => DescriptionFor(crewLore, id, fallback);
        public static string DescribeTreasure(string id, string fallback) => DescriptionFor(treasureLore, id, fallback);

        public static string RelationLabelFor(IReadOnlyList<SeventhPortLoreEntry> source, string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
                for (int index = 0; index < source.Count; index += 1)
                    if (string.Equals(source[index].Id, id, StringComparison.Ordinal)) return source[index].RelationLabel;
            return "线索推断 · 尚待考证";
        }

        public static string RelationLabelForEntity(string id, string storyId = "")
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                IReadOnlyList<SeventhPortLoreEntry>[] sources = { componentLore, shipLore, crewLore, treasureLore };
                for (int group = 0; group < sources.Length; group += 1)
                {
                    string label = RelationLabelFor(sources[group], id);
                    if (label != "线索推断 · 尚待考证" || DescriptionFor(sources[group], id, string.Empty).Length > 0) return label;
                }
            }
            return RelationLabelForStory(storyId);
        }

        public static bool IsFormerCaptainCrew(string crewId)
        {
            if (string.IsNullOrEmpty(crewId)) return false;
            for (int index = 0; index < formerCrewIds.Count; index += 1)
                if (string.Equals(formerCrewIds[index], crewId, StringComparison.Ordinal)) return true;
            return false;
        }

        public static SeventhPortFormerLieutenant FormerLieutenantForFleet(string fleetId)
        {
            for (int index = 0; index < formerLieutenants.Count; index += 1)
                if (string.Equals(formerLieutenants[index].FleetId, fleetId, StringComparison.Ordinal)) return formerLieutenants[index];
            return null;
        }

        private static SeventhPortStoryLink Link(string chapterId, string[] ships, string[] crew,
            string[] components, string[] treasures)
        {
            return new SeventhPortStoryLink(chapterId, ships, crew, components, treasures);
        }

        private static SeventhPortLoreEntry Lore(string id, string description, bool namesOldCaptain)
        {
            return new SeventhPortLoreEntry(id, description, namesOldCaptain);
        }
    }
}
