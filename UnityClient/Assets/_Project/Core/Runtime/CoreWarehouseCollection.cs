using System;
using System.Collections.Generic;
using ShatteredSail.Content;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreWarehouseTreasureDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string Story { get; }
        public string Source { get; }
        public string ArtResourcePath { get; }
        public string LoreRelationLabel => SeventhPortLoreCatalog.RelationLabelForEntity(Id);

        internal CoreWarehouseTreasureDefinition(string id, string name, string description,
            string story, string source, string artResourcePath)
        {
            Id = id;
            Name = name;
            Description = SeventhPortLoreCatalog.DescribeTreasure(id, description);
            Story = story;
            Source = CoreCollectionRewardCatalog.Find(id)?.Source ?? source;
            ArtResourcePath = artResourcePath;
        }
    }

    /// <summary>Permanent keepsakes and their warehouse display positions; no combat or sale value.</summary>
    public sealed class CoreWarehouseCollection
    {
        public const int DisplaySlotCount = 6;
        public const int CatalogCount = 40;
        public const string TutorialTreasureId = "first_victory_compass";
        public const string BoatswainBellTreasureId = "boatswain_bell";
        public const string SealedChartTreasureId = "sealed_chart";

        public static IReadOnlyList<CoreWarehouseTreasureDefinition> Catalog { get; } = Array.AsReadOnly(new[]
        {
            new CoreWarehouseTreasureDefinition(
                TutorialTreasureId,
                "破浪者的罗盘",
                "一枚带着海盐与战痕的黄铜罗盘。它是老船长第一次远航留下的指针，也是你继承自由旗的第一件遗物。",
                "你在近港试航中击败劫掠者首领，留下这枚罗盘。老船长曾用同样的铜壳为六港标出安全线；如今指针仍寻着愿为旁人留下泊位的岸。",
                "教学航行 · 首次击败劫掠者首领",
                "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition(
                BoatswainBellTreasureId,
                "航海长的铜钟",
                "一只刻着自由旗罗盘纹章的旧铜钟，钟沿布满盐霜与风蚀痕迹。敲到第十三响时，传说舰队会在远海显影。",
                "六港船队曾循这只钟穿过浓雾，分别后仍用钟声辨认彼此。老船长远征留下的第十三响，只为护送和救援鸣起，钟架换过几次，这个规矩没有换。",
                "老船长远征 · 铜钟港旧楼",
                "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition(
                SealedChartTreasureId,
                "封蜡航线图",
                "用深红封蜡锁住的旧海图，仍标着已经沉没的航标。六枚旧部蜡印叠在一起，坐标因此永远不会固定。",
                "探索新海路、收集部件、与传说舰队交锋，都会让蜡下添一道线。老船长离港前交出的这张图，须等行动者愿意共享方向，才会显出第七港口。",
                "老船长离港前 · 第七港口航图",
                "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("old_captain_free_flag", "老船长的自由旗", "褪色却未破的自由旗，六港水手都能认出。", "第一次拒缴港税时，这面旗升过桅顶。后来它经过许多双手，旗下的船都记得那条约定：靠岸不必先报封号。", "无名海旧灯塔", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("six_port_anchor_seal", "六港锚印", "六枚港锚熔在一起，铜印上还能分辨不同的铸纹。", "六港许下互救的誓约时，老船长留下这枚印。每次落在纸上，六种锚纹便同时印出，少一枚都不完整。", "六港联席旧厅", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("no_crown_logbook", "无冠航海日志", "盐霜结在书脊里，末尾十三页被撕去了，只剩参差的纸根。", "旧航线旁挤着各人的意见，粗笔细字都有。老船长并未抹去争论，平民水手决定去向的过程，就这样留在页边。", "灰鸥号失事舱柜", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("first_stormglass", "初渡风暴瓶", "细口瓶封着一汪蓝水，来自那次最早穿越的风暴。", "老船长穿过第一场风暴后留下这瓶水。如今舱灯映进去，瓶壁仍像下着雨，令人想起当年紧握同一根缆绳的手。", "风暴眼外缘打捞", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("starboard_copper_scope", "右舷铜望远镜", "铜筒磨出暗亮的包浆，刻着“先看见需要帮助的人”。", "老船长曾从这片镜里找到被神潮围住的渔船。镜筒上那行字很小，举镜的人却总会将它握在掌心。", "旧观测台", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("saltworn_quartermaster_key", "盐痕舵手钥匙", "六港仓门都曾被它打开，黄铜钥齿已磨得发圆。", "盐痕顺着掌纹留在柄上。这把钥匙交出去时，仓里的物资总先发给伤员和难民，装船清单排在后面。", "断锚仓库", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("first_berth_drydock_nail", "第一泊位船坞钉", "钉身锻自第一处免费泊位，拔出时还带着龙骨的木屑。", "老船长的第一处泊位没有挂牌。旧船解缆，新船便靠过来，系船人先接住绳，再问来处。", "第一港旧船坞", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("godbreaker_oar", "弑神者短桨", "短桨的叶面纵着裂纹，是与海神巨像搏斗留下的伤。", "小船划过神明的封锁线时，老船长手里只剩这柄裂桨。后来的渔船顺着那道水迹，驶进了原先不准通过的水道。", "神潮祭坛残骸", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("leviathan_bone_whistle", "海巨人骨哨", "白骨哨含在口中冰凉，吹过以后，远处会响起潮声。", "老船长曾与海巨人交换航路。这枚哨留在船上，提醒听见潮声的人，海上还有一种答话不必用言语。", "巨人背脊海沟", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("moon_tide_astrolabe", "月潮星盘", "以月蚀刻度校准潮汐方向。", "塞勒涅仍用它寻找老船长的旧航线，不肯以决斗换取答案。盐迹渐渐爬上盘缘，她将它擦净，继续核对那些没有抵达记录的线条。", "潮汐镇魂号航海室", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("iron_oath_shackle", "铁誓锁环", "从铁誓不沉号装甲上拆下的锁环。", "达恩曾以保护为名拦船收费。这只环曾锁住装甲，后来被拆下陈列，磨去的税印依旧摸得出来。", "挑战铁誓不沉号", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("thunder_crown_fuse", "雷冠炮引", "银芯在潮湿火药里仍能续燃，末端留着细白灰烬。", "索尔把救援炮改作审判炮，用的还是旧时的引线。火从一端走到另一端，受炮口指向的人却变了。", "挑战雷冠审判号", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("red_widow_grappling_hook", "绯刃钩索", "绯刃旧救援队使用过的双钩。", "同一根索救过落水者，也拦过付不起保护费的船。绯刃旧队究竟从哪一日改了用途，已没人说得准。", "挑战绯刃寡妇号", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("ember_ark_fireseed", "余烬方舟火种", "火种从不熄灭，收存时也须有人守着，不能留它独燃。", "伊诺受火神赐火，将从前的自由誓付之一炬。纸早已成灰，这一点火却还在烧。", "挑战余烬方舟号", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("tide_requiem_gauge", "镇魂潮汐尺", "海巨人骨粉嵌进潮尺，白色月潮刻度沿尺身排开。", "塞勒涅沿旧航路寻找老船长，只接受不会夺命的切磋。尺上的刻度一再擦洗，最初的航向始终没有磨去。", "潮汐镇魂号切磋", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("evelyn_six_port_ruler", "伊芙琳的六港测绘尺", "六港互助航线刻在折叠铜尺上，关节处留着长期开合的磨痕。", "伊芙琳曾用这把尺为远征测绘。交给继承者时，她展开六节尺身，旧线与尚待测量的空白便铺在同一张桌上。", "灰鸥港酒馆委托", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("barlow_living_hull_plane", "巴洛的活龙骨刨", "刨口夹着新鲜木屑，放进匣里后仍会慢慢生长。", "巴洛曾用它与老船长修补不会沉的龙骨。刃磨短了，柄上的握痕却越来越深，活木至今认得这道刨口。", "灰鸥港船坞", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("moer_storm_suture_kit", "莫尔的风暴缝针", "针包里帆线、皮线与缝针并排收着，针脚却总是同一种。", "莫尔从不先问伤者来自哪艘船。风浪里抬上甲板的人，都用同一包线缝合，针包上也没有敌我的标记。", "灰鸥港医务间", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("leisa_free_flag_badge", "雷萨的自由旗徽", "徽章背面刻着一行小字：炮口不对准求救者。", "雷萨护送难民船队时佩过它。正面的旗纹已被磨浅，背面的字藏在衣内，至今仍清楚。", "灰鸥港炮台", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("pepe_shared_pot_lid", "佩佩的共享锅盖", "锅盖边刻满分过口粮的人名，新刻痕挤进旧字之间。", "饥荒中，佩佩不许部下抢食。锅盖打开前先数人头，每添一个名字，就给锅里再添一勺水。", "灰鸥港厨房", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("lorne_first_berth_token", "洛恩的第一泊位牌", "断锚号最早的木制泊位牌，边角被系绳磨出了槽。", "洛恩记得陌生船第一次免费靠岸的情形。老船长接住缆绳时没有问价，后来泊位牌上的字便一直没添过数目。", "断锚酒馆旧账本", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("atang_white_shell_button", "阿棠的白壳纽扣", "一枚来自第一面自由帆的白壳纽扣，孔眼里还留着旧线。", "阿棠拆下战损商船的残帆，改成救援船的篷布。这枚纽扣被留下，白壳上还能认出那道剪裁的细痕。", "白壳商船修理台", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("ada_bronze_bell_bandage", "艾达的铜钟绷带扣", "小铜扣铸成钟形，贴近耳边，偶尔听得见细微潮响。", "艾达在雾战中用铜钟暗号找伤员，找到一人便收紧一处绷带。扣上的缺口，是那段忙乱日子留下的。", "铜钟港救护所", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("helen_thirteenth_cannon_coin", "赫莲的第十三响炮币", "铜币两面，一面刻炮响，一面刻泊位，边沿磨得发亮。", "赫莲一直记着第十三响的意思。听见它，该做的是撤离和救援；她翻过这枚币，从不把它当作开火的赌注。", "铜钟港炮库", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("noya_drowned_diver_lens", "诺雅的沉潜镜片", "镜片取自深海遗迹，边缘一圈盐晶怎样擦也留着淡痕。", "诺雅带着它寻找第七泊位的坐标。镜后的深海至今寂静，旧航向仍在她的潜水记录里，没有划去。", "灰鸥港潜水任务", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("broken_anchor_sextant", "断锚六分仪", "仪针总偏向最近的避风港，铜架上满是水下留下的锈点。", "断锚号失事后，它仍在人们手里传递。船名已经沉下海去，针尖指的那处岸，却让难民找到了过夜的地方。", "断锚号沉船", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("salt_crown_trade_weight", "盐冠商秤砝码", "砝码两面重量不同，上秤却从未短过一分一厘。", "盐冠商队将它带进贵族港口，与官秤并排放下。货物没有变重，少掉的只是那些藏在刻度里的盘剥。", "盐冠市场交换", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("twilight_tide_moon_shard", "暮潮月痕碎片", "平日黯淡的碎片，到了月蚀便显出一道隐流。", "暮潮船员循神明留下的月痕，找到了不必纳税的水道。那条线很细，只够小船依次通过。", "暮潮遗迹探索", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("empty_berth_registry", "空泊位登记册", "整本只写着留给下一个人。", "老船长离港后，六港仍拿它登记无条件的泊位。翻过一页，又是同一句话；谁先写下的，已无从查考。", "第六港公共码头", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("nameless_sailor_nameplate", "无名水手铭牌", "铭牌不刻姓名，只压着一枚帆旗凹痕，像尚未写完。", "远征里的普通水手往往没有姓名留在史书上。这块牌因此空着，替那些没被记下的人占住一行。", "无名海漂流木", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("storm_altar_fragment", "风暴神龛碎片", "半句祷词被凿去，石面剩下人的誓言和凿尖的白痕。", "老船长与神明谈价时打碎的祭坛，如今只余这一角。海水洗过断面，凿掉的字再没有回来。", "风暴神殿遗址", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("unfinished_seventh_berth_key", "第七泊位未完钥", "钥齿只有六格，第七格留白。", "旧海图没有交出一座现成的港。钥匙末格也未完成，持有者得自己接着锉，替后来的人试一试门。", "封蜡海图隐藏夹层", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("last_horizon_needle", "末日线针", "转动海图，细针仍指着最后那道地平线。", "老船长离港前，留下这最后的航向。后来海图换过许多张，针没有跟着改，也从未说明尽头有什么。", "老船长离开的无人灯塔", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("free_waters_mooring_ring", "自由海系泊环", "铁环没有锁孔，要几名船员合力才能将它固定。", "谁靠岸，谁便接过缆绳出一份力。环上没有领主的钥孔，港口也没有只为某个人留下的锁。", "第七港口传说海域", "WarehouseV2/first_victory_compass_v1"),
            new CoreWarehouseTreasureDefinition("sea_god_debt_token", "海神欠条", "神印压在欠条末尾，欠的是一条尚未交付的安全航路。", "老船长让神明认下普通人的通行权，才从石台前离开。印记仍在，欠下的路也仍写得清清楚楚。", "与海神谈判的石台", "WarehouseV2/warehouse_sealed_chart_v1"),
            new CoreWarehouseTreasureDefinition("inheritance_oath_inkstone", "继承誓言砚", "海水落进砚池，干涸的墨痕便重新显出旧誓词。", "旧誓不是抄完便能收好的字。每次招募、远航、拾回遗物或面对旧部之后，持砚的人读它，都会停在不同的一行。", "继承者试航终点", "WarehouseV2/warehouse_boatswain_bell_v1"),
            new CoreWarehouseTreasureDefinition("seventh_harbor_beacon_lantern", "第七港口引航灯", "灯芯未点燃，却映出所有曾被帮助过的船。", "关于它如何亮起，六港的传闻始终不同。只知道那些愿为别人留泊位的船，常在归途中看见一线没有标在图上的灯光。", "最终传说舰队线索", "WarehouseV2/first_victory_compass_v1")
        });

        private readonly HashSet<string> ownedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly string[] displayIds = new string[DisplaySlotCount];

        // Catalog order makes exports deterministic and prevents callers from mutating ownership.
        public IReadOnlyList<string> OwnedIds
        {
            get
            {
                var ordered = new List<string>();
                foreach (CoreWarehouseTreasureDefinition definition in Catalog)
                    if (ownedIds.Contains(definition.Id)) ordered.Add(definition.Id);
                return ordered.AsReadOnly();
            }
        }

        public bool Owns(string id) => id != null && ownedIds.Contains(id);

        public bool Grant(string id) => IsKnown(id) && ownedIds.Add(id);

        public string DisplayedAt(int slotIndex) => IsValidSlot(slotIndex)
            ? displayIds[slotIndex] ?? string.Empty : string.Empty;

        public bool IsDisplayed(string id)
        {
            if (!Owns(id)) return false;
            for (int slot = 0; slot < DisplaySlotCount; slot++)
                if (displayIds[slot] == id) return true;
            return false;
        }

        public bool TryDisplay(string id, int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || !Owns(id)) return false;
            for (int slot = 0; slot < DisplaySlotCount; slot++)
                if (displayIds[slot] == id) displayIds[slot] = null;
            // Replacing a display never changes permanent ownership of either keepsake.
            displayIds[slotIndex] = id;
            return true;
        }

        public bool TryRemoveDisplay(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || string.IsNullOrEmpty(displayIds[slotIndex])) return false;
            displayIds[slotIndex] = null;
            return true;
        }

        /// <summary>
        /// Restores a snapshot. Display CSV is positional (including empty fields); the first valid
        /// occurrence wins. Unknown, duplicate, unowned, and excess display entries are discarded.
        /// </summary>
        public void Import(string ownedCsv, string displayCsv)
        {
            ownedIds.Clear();
            Array.Clear(displayIds, 0, displayIds.Length);
            if (!string.IsNullOrEmpty(ownedCsv))
                foreach (string entry in ownedCsv.Split(',')) Grant(entry.Trim());

            if (string.IsNullOrEmpty(displayCsv)) return;
            string[] entries = displayCsv.Split(',');
            for (int slot = 0; slot < Math.Min(DisplaySlotCount, entries.Length); slot++)
            {
                string id = entries[slot].Trim();
                if (Owns(id) && !IsDisplayed(id)) displayIds[slot] = id;
            }
        }

        public string ExportOwned() => string.Join(",", OwnedIds);

        public string ExportDisplay() => string.Join(",", displayIds);

        private static bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < DisplaySlotCount;

        private static bool IsKnown(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (CoreWarehouseTreasureDefinition definition in Catalog)
                if (string.Equals(definition.Id, id, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
