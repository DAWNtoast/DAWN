using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// The 13 neutral and 90 school boarding designs. New definitions are appended
    /// after every historic library slot; compatibility aliases never shift saves.
    /// Artwork is deliberately reused from existing authored illustrations.
    /// </summary>
    public static class CoreBoardingCardCatalog
    {
        private const string Prefix = "card_boarding_";
        private static readonly IReadOnlyList<CoreBattleCard> all = Create();
        private static readonly Dictionary<string, CoreBattleCard> byKey = Index();
        private static readonly Dictionary<string, string> legacyKeys = LegacyKeys();

        public static IReadOnlyList<CoreBattleCard> All => all;
        public static CoreBattleCard Find(string id) => !string.IsNullOrEmpty(id) && id.StartsWith(Prefix, StringComparison.Ordinal)
            ? FindByKey(id.Substring(Prefix.Length)) : null;
        public static CoreBattleCard FindByKey(string key) => key != null && byKey.TryGetValue(key, out CoreBattleCard card) ? card : null;

        public static bool IsLegacyAlias(CoreBattleCard card) => card != null && legacyKeys.ContainsKey(card.Id);

        /// <summary>Only boarding-only slots are replaced globally. Shared ship cards retain their ship definition.</summary>
        public static CoreBattleCard ReplaceLegacyBoarding(CoreBattleCard card) => card != null && card.AllowedStages == NavalBattleStages.Boarding
            ? ForBoarding(card) : card;

        /// <summary>Used when quoting or playing a shared card in boarding, preserving its source identity and saved ID.</summary>
        public static CoreBattleCard ForBoarding(CoreBattleCard card)
        {
            // Current source cards carry their own authored rules (and, where needed, current boarding specs).
            if (card != null && card.Id.StartsWith("card_special_", StringComparison.Ordinal)) return card;
            if (card == null || card.BoardingSpec != null || !card.IsAllowedInStage(NavalBattleStage.Boarding) ||
                !legacyKeys.TryGetValue(card.Id, out string key)) return card;
            CoreBattleCard design = FindByKey(key);
            int level = card.UpgradeLevel;
            // A shared ship/boarding card owns one saved identity and one growth curve.
            // Rebuild its boarding baseline without looking up session definitions or
            // upgrade caches, which may still be initializing this catalogue.
            CoreBattleCard basis = new CoreBattleCard(card.Id, design.Name, design.Type, design.Energy,
                design.Damage, design.Block, design.Repair, design.Draw, design.PowderCost,
                design.DescriptionTemplate, card.AllowedStages, design.Action, card.RequiredCrew,
                card.ArtId, design.CannonballCost, card.RequiredComponent,
                schoolId: design.SchoolId, boardingSpec: design.BoardingSpec);
            return new CoreBattleCard(card.Id, design.Name, design.Type, design.Energy,
                CoreCardUpgradeDesign.Scale(basis, design.Damage, level),
                CoreCardUpgradeDesign.ScaleBlock(basis, design.Block, level),
                design.BoardingSpec.Key == "basic_07" || design.BoardingSpec.Key == "tide_12"
                    ? CoreBoardingCardBalance.Sustain(design.BoardingSpec.Key, level)
                    : CoreCardUpgradeDesign.ScaleRepair(basis, design.Repair, level),
                CoreBattleUpgradeBalance.UpgradeDraw(basis, level),
                design.PowderCost, CoreBoardingCardDescriptions.AtLevel(basis, level), card.AllowedStages,
                design.Action, card.RequiredCrew, card.ArtId, design.CannonballCost, card.RequiredComponent,
                card.UpgradeLevel, design.SchoolId, null, null, design.BoardingSpec);
        }

        public static void ApplyToLibrary(List<CoreBattleCard> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i] = ReplaceLegacyBoarding(cards[i]);
                ids.Add(cards[i].Id);
            }
            foreach (CoreBattleCard card in all) if (ids.Add(card.Id)) cards.Add(card);
        }

        private static Dictionary<string, CoreBattleCard> Index()
        {
            var result = new Dictionary<string, CoreBattleCard>(StringComparer.Ordinal);
            foreach (CoreBattleCard card in all) result.Add(card.BoardingSpec.Key, card);
            return result;
        }

        private static CoreBattleCard C(string key, string name, int energy, string description,
            int damage = 0, int block = 0, int repair = 0, int draw = 0, int powder = 0,
            bool attack = false, bool delayed = false, string art = "")
        {
            damage = CoreBoardingCardBalance.Linear(damage);
            block = CoreBoardingCardBalance.Guard(key, block);
            repair = CoreBoardingCardBalance.Repair(key, repair);
            string school = key.Substring(0, key.IndexOf('_'));
            if (school == "basic") school = string.Empty;
            if (art.Length == 0)
                art = school.Length > 0 ? "card_school_" + school + "_b07" :
                    repair > 0 ? "card_emergency_repair" : block > 0 ? "card_brace" :
                    attack ? "card_cutlass_raid" : "card_full_sail_turn";
            string type = attack ? "登船" : repair > 0 ? "维修" : block > 0 ? "防御" : "策略";
            description = CoreBoardingManuscriptDescriptions.ForKey(key, null, 0) ?? description;
            return new CoreBattleCard(Prefix + key, name, type, energy, damage, block, repair, draw, powder,
                description, NavalBattleStages.Boarding, NavalBattleCardAction.Standard, artId: art,
                schoolId: school, boardingSpec: new CoreBoardingCardSpec(key, attack, delayed));
        }

        private static IReadOnlyList<CoreBattleCard> Create() => Array.AsReadOnly(new[]
        {
            // Neutral: the agreed 13-card baseline. Medicine is paid once when setting a two-tick treatment.
            C("basic_01", "水手刺击", 1, "造成 {damage} 点伤害。", damage: 8, attack: true),
            C("basic_02", "弯刀突袭", 2, "造成 {damage} 点伤害。", damage: 20, attack: true),
            C("basic_03", "重刃终击", 3, "造成 {damage} 点伤害。", damage: 35, attack: true),
            C("basic_04", "盾阵掩护", 1, "获得 {block} 格挡。", block: 4),
            C("basic_05", "坚守阵线", 2, "获得 {block} 格挡。", block: 10),
            C("basic_06", "战地包扎", 1, "使用1药物恢复 {repair} 生命；无药物恢复2。", repair: 6),
            C("basic_07", "持续救护", 2, "施放时使用1药物；本回合和下一己方回合结束各恢复 {repair} 生命。无药物每次恢复2，共2次。", repair: 5),
            C("basic_08", "战术筹划", 2, "抽 {draw} 张牌。", draw: 3),
            C("basic_09", "整顿号令", 0, "恢复 {energyRecovery} 能量。", art: "card_brace"),
            C("basic_10", "蓄势号令", 2, "先支付2能量，再恢复 {energyRecovery} 能量。"),
            C("basic_11", "试探突击", 1, "造成 {damage} 伤害；抽 {draw} 张。", damage: 4, draw: 1, attack: true),
            C("basic_12", "掩护调度", 1, "获得 {block} 格挡；抽 {draw} 张。", block: 2, draw: 1),
            C("basic_13", "伏击预令", 1, "下一己方回合开始造成 {damage} 伤害；受格挡，不暴击。", damage: 12, attack: true, delayed: true),

            // Fire: powder, exhaust and a shared burn status on either combatant.
            C("fire_01", "爆燃斩", 2, "消耗1火药；16白刃+12即时火焰，合计 {damage} 伤害，作为一次攻击结算。", damage: 28, powder: 1, attack: true, art: "card_school_fire_b08"),
            C("fire_02", "火药淬刃", 1, "消耗 1 火药；造成 {damage} 点伤害；施加 4 层燃烧。", damage: 8, powder: 1, attack: true),
            C("fire_03", "燃粉投掷", 1, "消耗 1 火药；施加 8 层燃烧。", powder: 1, art: "card_school_fire_b05"),
            C("fire_04", "余烬火罐", 2, "消耗 1 火药；施加 6 层燃烧；下个己方回合开始时，再施加 6 层燃烧。", powder: 1, art: "card_school_fire_b10"),
            C("fire_05", "温火疗法", 1, "有药物时固定消耗1药物和1火药，恢复10生命；无药物恢复2，不消耗火药。", repair: 10, powder: 1, art: "card_school_fire_c02"),
            C("fire_06", "焚刃突击", 1, "焚烧 1 张其他手牌；造成 {damage} 点伤害，施加 3 层燃烧；若焚烧的是攻击牌，则改为施加 5 层燃烧。", damage: 8, attack: true, art: "card_school_fire_l04"),
            C("fire_07", "焚牌咒火", 1, "焚烧 X 张其他手牌（1～3 张）；施加 3+2X 层燃烧。", art: "card_school_fire_b05"),
            C("fire_08", "焚帆护身", 1, "焚烧 X 张其他手牌（1～3 张）；获得 2+4X 点格挡。", block: 2, art: "card_school_fire_b09"),
            C("fire_09", "焚书寻策", 1, "焚烧 1 张其他手牌，再抽 {draw} 张牌。", draw: 3, art: "card_school_fire_e05"),
            C("fire_10", "燃尽供火", 0, "焚烧本卡牌；本回合接下来两张需要火药的火焰牌，各减免 1 火药费用。", art: "card_school_fire_b01"),
            C("fire_11", "浴火斩", 2, "对自身施加 3 层燃烧；造成 60+2×自身燃烧层数 点伤害。", damage: 30, attack: true, art: "card_school_fire_l03"),
            C("fire_12", "共燃契约", 1, "直到下个己方回合结束：对自身施加燃烧时，对敌方施加等量燃烧。", art: "card_school_fire_c01"),
            C("fire_13", "灼身反击", 1, "对自身施加 2 层燃烧；获得 {block} 点格挡；格挡敌方攻击时，对敌方造成 2×自身燃烧层数 点反击伤害。", block: 8, art: "card_school_fire_e04"),
            C("fire_14", "引火入体", 0, "对自身施加 2 层燃烧；获得 {energyRecovery} 点能量。", art: "card_school_fire_b02"),
            C("fire_15", "移焰", 2, "将自己的燃烧效果转移到敌方。", art: "card_school_fire_b10"),

            // Arcane: acquire, allocate and schedule mana; gains may exceed the turn refill limit.
            C("arcane_01", "符刃", 1, "造成 {damage} 伤害；获得1魔力。", damage: 6, attack: true),
            C("arcane_02", "护身刻印", 1, "获得 {block} 格挡和1魔力。", block: 3, art: "card_school_arcane_b09"),
            C("arcane_03", "凝神聚能", 1, "获得3魔力。", art: "card_school_arcane_b01"),
            C("arcane_04", "魔力洪流", 2, "消耗全部可用魔力；造成24+6X伤害。", damage: 24, attack: true, art: "card_school_arcane_b08"),
            C("arcane_05", "星屑缝合", 1, "有药物时固定消耗1药物和2魔力，恢复13生命；无药物恢复2，不消耗魔力。", repair: 13, art: "card_school_arcane_b10"),
            C("arcane_06", "奥术飞弹", 1, "消耗全部可用魔力；发射2+X枚飞弹，每枚5伤害。多段合为一次出牌。", damage: 10, attack: true, art: "card_school_arcane_b03"),
            C("arcane_07", "解构射线", 1, "固定消耗1魔力；先移除敌方最多8格挡，再造成 {damage} 伤害。", damage: 10, attack: true, art: "card_school_arcane_e01"),
            C("arcane_08", "星幕结界", 1, "固定消耗1魔力；获得 {block} 格挡，本牌剩余格挡中的最多4点保留到下一个敌方回合结束。", block: 5, art: "card_school_arcane_b05"),
            C("arcane_09", "奥术研读", 2, "固定消耗1魔力；查看牌库顶5张，选择 {draw} 张入手，其余按原顺序放回牌库底。", draw: 3, art: "card_school_arcane_c01"),
            C("arcane_10", "以太换能", 0, "消耗2魔力，恢复 {energyRecovery} 能量；同名所有副本共享每回合一次。", art: "card_school_arcane_b06"),
            C("arcane_11", "星坠预兆", 2, "消耗全部可用魔力；下一己方回合开始造成36+8X伤害，施放时锁定；受格挡，不暴击。", damage: 36, attack: true, delayed: true, art: "card_school_arcane_l03"),
            C("arcane_12", "法术封存", 0, "固定消耗1魔力；选择1张其他手牌保留到下一回合，该实体牌下一回合首次打出的基础费用-1，最低0；不抵消同名加价。", art: "card_school_arcane_b02"),
            C("arcane_13", "回响刻印", 1, "消耗1魔力；本回合下一张即时奥术攻击，在下一己方回合开始追加其直接伤害的50%，向下取整，最多18；不复制其他效果，不叠加。", art: "card_school_arcane_l05"),
            C("arcane_14", "星辉回收", 0, "消耗1魔力；将弃牌堆中1张奥术攻击取回手牌，再次使用正常付费；不能取回燃尽或烧掉的牌。", art: "card_school_arcane_e05"),
            C("arcane_15", "时序收束", 0, "固定消耗2魔力；自动结算最早设置的一项延迟攻击，删除原定触发，不重复结算或触发出牌收益。", art: "card_school_arcane_l01"),

            // Curse marks persist until removed and execute immediately above the target's health threshold.
            C("curse_01", "咒刃伤痕", 1, "造成 {damage} 点伤害；施加 1 层咒印。", damage: 8, attack: true),
            C("curse_02", "蚀心咒", 1, "施加 3 层咒印。", art: "card_school_curse_b01"),
            C("curse_03", "替身草偶", 1, "获得 {block} 点格挡；施加 1 层咒印。", block: 3, art: "card_school_curse_b09"),
            C("curse_04", "恶兆契约", 1, "直到下下个敌方回合结束，我方受到敌方伤害时，施加 2 层咒印。", art: "card_school_curse_c02"),
            C("curse_05", "连环咒印", 0, "本回合接下来使用的两张攻击牌对敌方施加 1 层咒印。", art: "card_school_curse_b02"),
            C("curse_06", "苦痛牵引", 1, "造成 6+当前咒印层数 点伤害。", damage: 6, attack: true, art: "card_school_curse_b03"),
            C("curse_07", "衰败咒", 1, "直到下下个敌方回合结束，敌方造成的伤害降低 3+咒印层数 点。", art: "card_school_curse_b06"),
            C("curse_08", "蚀甲咒", 1, "移除 4+2×咒印层数 点格挡；直到下下个敌方回合结束，敌方获得的格挡降低 2+咒印层数 点。", art: "card_school_curse_b04"),
            C("curse_09", "逆刃咒", 1, "直到下下个敌方回合结束，敌方造成伤害前受到 2×咒印层数 点咒术伤害。", art: "card_school_curse_e04"),
            C("curse_10", "缠咒", 0, "本场战斗中，咒印消灭阈值降低 " + CoreCurseRules.EntangleThresholdReductionPercent + " 个百分点（可叠加）；降至 0% 时立即消灭敌方。", art: "card_school_curse_e05"),
            C("curse_11", "终祷", 3, "敌方咒印层数 ≥ 1 才可以使用：造成 20+8X 点咒术伤害；清除咒印效果。", damage: 20, art: "card_school_curse_b10"),
            C("curse_12", "夺魄咒", 1, "恢复 2 点生命值；敌方咒印层数 ≥ 2 时，消耗 1 药物额外恢复 11 点生命值。", repair: 13, art: "card_school_curse_b08"),
            C("curse_13", "窥魂术", 1, "消耗 1 层咒印；抽 {draw} 张牌。", draw: 3, art: "card_school_curse_c01"),
            C("curse_14", "摄魂汲能", 0, "敌方咒印层数 ≥ 2 才可以使用：获得 {energyRecovery} 点能量。", art: "card_school_curse_l05"),
            C("curse_15", "移祸护身", 1, "敌方咒印层数 ≥ 2 才可以使用：获得 {block} 点格挡。", block: 10, art: "card_school_curse_l04"),

            // Tide momentum persists until spent or until battle ends.
            C("tide_01", "潮汐守势", 1, "获得 {block} 点蓄潮格挡。", block: 4, art: "card_school_tide_b08"),
            C("tide_02", "礁壁护阵", 2, "获得 {block} 点蓄潮格挡。", block: 15, art: "card_school_tide_e04"),
            C("tide_03", "掠波架势", 1, "获得 {block} 点蓄潮格挡；抽 {draw} 张牌。", block: 2, draw: 1, art: "card_school_tide_b05"),
            C("tide_04", "破浪突刺", 1, "先移除敌方最多4格挡，再造成 {damage} 伤害。", damage: 6, attack: true, art: "card_school_tide_e01"),
            C("tide_05", "留潮", 0, "本场战斗中，将当前全部格挡及后续获得的格挡转换为等量蓄潮格挡。", art: "card_school_tide_b02"),
            C("tide_06", "踏浪斩", 1, "消耗全部潮势X，造成6+X点伤害。", damage: 6, attack: true),
            C("tide_07", "回澜反击", 2, "消耗全部潮势X，造成16+X点伤害。", damage: 16, attack: true, art: "card_school_tide_b10"),
            C("tide_08", "怒海决堤", 3, "消耗全部潮势X，造成30+1.5X点伤害。", damage: 30, attack: true, art: "card_school_tide_l03"),
            C("tide_09", "潜流伏击", 1, "消耗4潮势；接下来2个敌方回合内，目标下一次主动攻击结束后受到16伤害，受格挡、不暴击，触发一次消失；不叠加、不刷新。", art: "card_school_tide_l04"),
            C("tide_10", "涡流牵制", 1, "消耗2潮势；造成 {damage} 伤害；2个敌方回合内目标下一次完整攻击总伤害减少3，最多减半，向下取整，同类取最高。", damage: 6, attack: true, art: "card_school_tide_b04"),
            C("tide_11", "潮息复苏", 1, "有药物时固定消耗1药物和6潮势，恢复10生命；无药物恢复2，不消耗潮势。", repair: 10, art: "card_school_tide_b09"),
            C("tide_12", "海息药浴", 2, "有药物时固定消耗1药物和8潮势，本回合和下一己方回合结束各恢复8；无药物各恢复2，不消耗潮势；不叠加、不刷新。", repair: 8, art: "card_school_tide_c02"),
            C("tide_13", "观潮", 1, "消耗8潮势；抽 {draw} 张。", draw: 3, art: "card_school_tide_c01"),
            C("tide_14", "借潮行舟", 0, "消耗8潮势；恢复 {energyRecovery} 能量，抽 {draw} 张。同名所有副本共享每回合一次。", draw: 1, art: "card_school_tide_e02"),
            C("tide_15", "回澜屏障", 1, "消耗2潮势；获得 {block} 点格挡。", block: 8, art: "card_school_tide_b06"),

            // Thunder: six-chain cap, one chain per actively played non-finisher, reset at own turn end.
            C("thunder_01", "电火花", 0, "造成 {damage} 点伤害。", damage: 3, attack: true, art: "card_school_thunder_b03"),
            C("thunder_02", "闪步刺击", 1, "造成 8+出牌前连锁层数 点伤害。", damage: 8, attack: true),
            C("thunder_03", "追电刺", 1, "造成 {damage} 点伤害；抽 {draw} 张牌。", damage: 4, draw: 1, attack: true, art: "card_school_thunder_e01"),
            C("thunder_04", "裂甲雷击", 1, "移除 6 点格挡，造成 {damage} 点伤害。", damage: 7, attack: true, art: "card_school_thunder_b04"),
            C("thunder_05", "迟雷刺", 1, "延迟：造成 14+出牌前连锁层数 点伤害。", damage: 14, attack: true, delayed: true, art: "card_school_thunder_l01"),
            C("thunder_06", "电弧导流", 1, "抽 {draw} 张牌；本回合下一张雷霆攻击的伤害提高 8 点。", draw: 1, art: "card_school_thunder_e04"),
            C("thunder_07", "风暴续令", 0, "连锁 2：抽 {draw} 张牌。", draw: 1, art: "card_school_thunder_l05"),
            C("thunder_08", "雷云搜寻", 2, "抽 {draw} 张牌；连锁 3：额外抽 1 张牌。", draw: 3, art: "card_school_thunder_c01"),
            C("thunder_09", "导电回流", 0, "连锁 2：获得 {energyRecovery} 点能量。", art: "card_school_thunder_b01"),
            C("thunder_10", "雷网残响", 1, "本回合接下来 3 张主动雷霆攻击额外造成 4 点伤害。", art: "card_school_thunder_l04"),
            C("thunder_11", "雷鸣连斩", 1, "造成 3+2×出牌前连锁层数 点伤害 2 次。", damage: 6, attack: true, art: "card_school_thunder_b09"),
            C("thunder_12", "麻痹刺击", 1, "造成 {damage} 点伤害；连锁 2：敌方下一次攻击伤害降低 10 点。", damage: 5, attack: true, art: "card_school_thunder_b08"),
            C("thunder_13", "落雷收束", 2, "造成 20+5X 点伤害。", damage: 20, attack: true, art: "card_school_thunder_b10"),
            C("thunder_14", "穿云落雷", 2, "造成 16+4X 点无视格挡的伤害。", damage: 16, attack: true, art: "card_school_thunder_e03"),
            C("thunder_15", "风暴终幕", 3, "造成 28+6X 点伤害，连锁 10：伤害翻倍，连锁 20：伤害翻倍。", damage: 28, attack: true, art: "card_school_thunder_l03"),

            // Blood: real life costs, full effective-damage life drain, desperation at <=40% maximum health.
            C("blood_01", "割掌誓击", 1, "造成 {damage} 点伤害。", damage: 16, attack: true),
            C("blood_02", "滴血裁决", 2, "造成 {damage} 点伤害。", damage: 34, attack: true, art: "card_school_blood_b10"),
            C("blood_03", "血祭屏障", 1, "获得 {block} 点格挡。", block: 11, art: "card_school_blood_e04"),
            C("blood_04", "禁忌启示", 1, "抽 {draw} 张牌。", draw: 3, art: "card_school_blood_b02"),
            C("blood_05", "血祭汲能", 0, "获得 {energyRecovery} 点能量。", art: "card_school_blood_b01"),
            C("blood_06", "饮血斩", 1, "造成 {damage} 点伤害；吸血。", damage: 9, attack: true, art: "card_school_blood_b08"),
            C("blood_07", "裂脉双斩", 2, "造成 12 点伤害 2 次；若第一击使敌方失去生命值，第二击伤害提高 4 点。", damage: 24, attack: true, art: "card_school_blood_c02"),
            C("blood_08", "血债追偿", 1, "直到下下个敌方回合结束，敌方对我方造成伤害时，对其造成 8 点伤害。", art: "card_school_blood_e05"),
            C("blood_09", "缝魂契约", 1, "恢复 2 点生命值，绝境：改为恢复 5 点生命值；消耗 1 药物额外恢复 4 点生命值，绝境：改为额外恢复 8 点生命值。", repair: 6, art: "card_school_blood_b09"),
            C("blood_10", "逐命追击", 1, "造成 {damage} 点伤害；若使敌方失去生命值，则抽 {draw} 张牌。", damage: 6, draw: 1, attack: true, art: "card_school_blood_e01"),
            C("blood_11", "残躯狂斩", 1, "造成 {damage} 点伤害；绝境：额外造成 10 点伤害。", damage: 7, attack: true, art: "card_school_blood_l05"),
            C("blood_12", "殊死架势", 1, "获得 {block} 点格挡；绝境：改为获得 12 点格挡。", block: 5, art: "card_school_blood_l04"),
            C("blood_13", "遗命之刃", 1, "延迟：造成 {damage} 点伤害；绝境：改为造成 22 点伤害。", damage: 12, attack: true, delayed: true, art: "card_school_blood_l01"),
            C("blood_14", "逆命一搏", 0, "绝境时才可使用；获得 {energyRecovery} 点能量，抽 {draw} 张牌，使用后移出本场战斗。", draw: 1, art: "card_school_blood_c01"),
            C("blood_15", "绝境誓约", 1, "绝境时才可使用：直到下个我方回合结束，攻击牌额外造成 8 点伤害。", art: "card_school_blood_l02"),
        });

        private static Dictionary<string, string> LegacyKeys() => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "card_boarding_hook", "basic_11" }, { "card_cutlass_raid", "basic_02" },
            { "card_crew_thrust", "basic_01" }, { "card_crew_guard", "basic_04" },
            { "card_crew_rally", "basic_12" }, { "card_crew_bandage", "basic_06" },
            { "card_crew_pistol", "basic_03" }, { "card_doctor_sutures", "basic_06" },
            { "card_school_fire_b07", "fire_02" }, { "card_school_fire_b08", "fire_01" },
            { "card_school_fire_b09", "fire_08" }, { "card_school_fire_b10", "fire_04" },
            { "card_special_crew_broken_anchor_cook_tam", "fire_05" },
            { "card_special_plating_starseal_panels", "fire_13" },
            { "card_special_instrument_whalestar_receiver", "fire_09" },
            { "card_special_plating_dragonwake_armor", "fire_06" },
            { "card_school_arcane_b07", "arcane_01" }, { "card_school_arcane_b08", "arcane_04" },
            { "card_school_arcane_b09", "arcane_02" }, { "card_school_arcane_b10", "arcane_05" },
            { "card_special_crew_twilight_tide_doctor_viya", "arcane_05" },
            { "card_special_plating_moonsilver_skin", "arcane_08" },
            { "card_special_plating_worldtree_bark", "arcane_08" },
            { "card_school_curse_b07", "curse_01" }, { "card_school_curse_b08", "curse_12" },
            { "card_school_curse_b09", "curse_03" }, { "card_school_curse_b10", "curse_11" },
            { "card_special_crew_twilight_tide_carpenter_orlan", "curse_04" },
            { "card_special_plating_eclipse_mail", "curse_09" },
            { "card_special_plating_freedom_ark", "curse_15" },
            { "card_school_tide_b07", "tide_06" }, { "card_school_tide_b08", "tide_01" },
            { "card_school_tide_b09", "tide_11" }, { "card_school_tide_b10", "tide_07" },
            { "card_special_crew_white_shell_doctor_mirei", "tide_12" },
            { "card_special_plating_ghostreef_carapace", "tide_02" },
            { "card_special_plating_leviathan_scales", "tide_09" },
            { "card_special_instrument_worldcompass", "tide_11" },
            { "card_school_thunder_b07", "thunder_02" }, { "card_school_thunder_b08", "thunder_12" },
            { "card_school_thunder_b09", "thunder_11" }, { "card_school_thunder_b10", "thunder_13" },
            { "card_special_plating_aurora_laminate", "thunder_06" },
            { "card_special_plating_stormark_bulwark", "thunder_10" },
            { "card_special_instrument_stormoracle", "thunder_07" },
            { "card_school_blood_b07", "blood_01" }, { "card_school_blood_b08", "blood_06" },
            { "card_school_blood_b09", "blood_09" }, { "card_school_blood_b10", "blood_02" },
            { "card_special_crew_broken_anchor_doctor_hector", "blood_09" },
            { "card_special_crew_salt_crown_diver_isha", "blood_07" },
            { "card_special_plating_fogiron_carapace", "blood_03" },
            { "card_special_instrument_ghostmap_projector", "blood_08" },
            { "card_special_plating_living_hull", "blood_12" },
            { "card_special_instrument_allseeing_sextant", "blood_11" },
            // Neutral source cards keep their crew/component ownership and use a baseline effect.
            { "card_special_crew_gray_gull_diver_noya", "basic_11" },
            { "card_special_bow_sunlion_figurehead", "basic_11" },
            { "card_special_sail_crown_of_gales", "basic_05" },
            { "card_special_plating_redbanner_wall", "basic_12" },
            { "card_special_crew_bronze_bell_doctor_ada", "basic_06" },
            { "card_special_crew_salt_crown_doctor_naji", "basic_07" },
            { "card_special_crew_gray_gull_doctor_moer", "basic_06" },
            { "card_special_crew_white_shell_cook_flora", "basic_06" },
            { "card_special_crew_bronze_bell_cook_basin", "basic_12" },
            { "card_special_crew_twilight_tide_cook_lume", "basic_07" },
        };
    }
}


