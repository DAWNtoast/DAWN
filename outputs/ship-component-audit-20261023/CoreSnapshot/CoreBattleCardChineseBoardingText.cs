using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    internal static partial class CoreBattleCardChineseText
    {
        // Work on the rendered text so authored upgrade values and whole-formula rounding
        // remain intact. These changes only clarify subjects, conditions and timing.
        private static string RewriteBoarding(string text, CoreBattleCard card)
        {
            string key = card?.BoardingSpec?.Key;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key)) return text;

            text = Regex.Replace(text, @"新增\s*(\d+)\s*层自燃", "自身增加 $1 层自燃");
            text = Regex.Replace(text, @"出牌前连锁(?!层数)", "出牌前连锁层数");

            switch (key)
            {
                case "basic_07":
                case "tide_12":
                    text = Regex.Replace(text,
                        @"施放时消耗\s*(?<cost>[^；。]+?)\s*额外恢复\s*(?<heal>\d+)\s*点生命值（上述两次各生效）",
                        "使用本牌时若消耗 ${cost}，则这两次治疗各额外恢复 ${heal} 点生命值");
                    break;
                case "fire_04":
                    text = text.Replace("接下来两次己方回合开始再各", "接下来的两个己方回合开始时，再各");
                    break;
                case "fire_06":
                    text = Regex.Replace(text, @"烧掉攻击牌时改为\s*(\d+)\s*层", "若烧掉的是攻击牌，则改为施加 $1 层灼烧");
                    break;
                case "fire_07":
                    text = text.Replace("附加", "施加").Replace("X层", "X 层");
                    break;
                case "fire_10":
                    text = text.Replace("本回合接下来两张需要火药的火焰牌，各免", "本回合接下来两张需要火药的火焰牌，各减免")
                        .Replace("火药费用并获得完整效果", "火药费用，仍获得完整效果");
                    break;
                case "fire_11":
                    text = Regex.Replace(text,
                        @"出牌前每有\s*(?<stack>\d+)\s*层自燃伤害提高\s*(?<bonus>\d+)\s*点，最多\s*\+\s*(?<cap>\d+)",
                        "出牌前每有 ${stack} 层自燃，伤害额外提高 ${bonus} 点，最多提高 ${cap} 点");
                    break;
                case "fire_12":
                    text = text.Replace("本回合和之后两个己方回合，每回合首次主动新增自燃时，为敌方附加相同层数的灼烧",
                        "本回合及接下来的两个己方回合内，每回合首次主动增加自燃时，对敌方施加等量灼烧");
                    break;
                case "fire_13":
                    text = Regex.Replace(text,
                        @"本回合首次实际用格挡吸收敌方攻击后，反伤\s*(?<damage>[^，；。]+)，最多\s*(?<cap>\d+)(?:\s*点(?:伤害)?)?",
                        "本回合首次用格挡抵挡敌方攻击的伤害后，对敌方造成 ${damage} 点反击伤害，最多 ${cap} 点");
                    break;
                case "fire_15":
                    text = text.Replace("自动将最多", "将最多")
                        .Replace("优先转移剩余次数较多的层", "优先转移剩余结算次数较多的自燃")
                        .Replace("保留剩余结算次数", "转移后的剩余结算次数不变");
                    break;
                case "arcane_08":
                    text = Regex.Replace(text,
                        @"本牌剩余格挡中的最多\s*(?<block>\d+)\s*点保留到(?<until>[^；。]+)",
                        "本牌提供的格挡若有剩余，最多保留 ${block} 点，持续至${until}");
                    break;
                case "arcane_06":
                    text = text.Replace("X枚", "X 枚");
                    break;
                case "arcane_09":
                    text = Regex.Replace(text, @"选择\s*(\d+)\s*张牌入手", "选择其中 $1 张加入手牌");
                    break;
                case "arcane_11":
                case "thunder_05":
                    text = text.Replace("施放时锁定", "伤害在使用本牌时确定");
                    break;
                case "arcane_12":
                    text = Regex.Replace(text,
                        @"该实体牌(?<turn>[^，；。]+?)首次打出的基础费用\s*-\s*(?<cost>\d+)\s*，最低\s*(?<min>\d+)",
                        "这张牌在${turn}首次打出时，基础能量费用降低 ${cost} 点，最低为 ${min}");
                    break;
                case "arcane_13":
                    text = Regex.Replace(text,
                        @"本回合下一张即时奥术攻击，在(?<time>[^，；。]+?)追加其直接伤害的\s*(?<percent>\d+)%\s*，向下取整，最多\s*(?<cap>\d+)(?:\s*点(?:伤害)?)?",
                        "本回合打出的下一张即时奥术攻击，会在${time}追加相当于其直接伤害 ${percent}% 的伤害（向下取整，最多 ${cap} 点）");
                    break;
                case "arcane_15":
                    text = text.Replace("自动结算最早设置的一项延迟攻击", "立即结算最早安排的一次延迟攻击");
                    break;
                case "curse_01":
                    text = text.Replace("被格挡仍附加", "即使伤害被格挡，也会施加咒痕");
                    break;
                case "curse_04":
                    text = Regex.Replace(text,
                        @"每回合目标攻击首次使己方实际失去生命时，为其施加\s*(\d+)\s*层咒痕",
                        "每回合己方首次因目标的攻击而失去生命值时，对其施加 $1 层咒痕");
                    text = text.Replace("自伤、灼烧和完全格挡不触发", "自伤、灼烧和被格挡完全抵挡的攻击均不触发");
                    break;
                case "curse_05":
                    text = text.Replace("各在结算后施加", "各在结算后对敌方施加");
                    break;
                case "curse_07":
                    text = Regex.Replace(text,
                        @"(?<turns>\d+)\s*个敌方回合内，下一次完整攻击总伤害减少\s*(?<amount>[^，；。]+)，最多\s*(?<cap>\d+)\s*且不超过整次伤害的一半（向下取整）",
                        "接下来的 ${turns} 个敌方回合内，使目标下一次攻击的总伤害减少 ${amount} 点，最多减少 ${cap} 点，且不超过该次攻击总伤害的一半（向下取整）");
                    text = text.Replace("触发一次消失", "触发后失效")
                        .Replace("同类取最高", "同类效果只取最高值")
                        .Replace("同类加成只保留最高值", "同类效果只取最高值");
                    break;
                case "curse_08":
                    text = Regex.Replace(text, @"立即移除(?!目标的)", "立即移除目标的");
                    text = Regex.Replace(text,
                        @"下次获得格挡减少\s*(?<amount>[^，；。]+)，最低\s*(?<min>\d+)",
                        "目标下次获得的格挡减少 ${amount} 点，最低降至 ${min} 点");
                    text = text.Replace("后续减盾只触发一次", "减少格挡的后续效果只触发一次");
                    break;
                case "curse_09":
                    text = Regex.Replace(text,
                        @"每回合目标第一次主动攻击结束后受到\s*(?<amount>[^，；。]+?)\s*的咒术伤害，上限\s*(?<cap>\d+)",
                        "每回合目标首次主动攻击结束后，对其造成 ${amount} 点咒术伤害，最多 ${cap} 点");
                    text = text.Replace("不耗咒痕", "不消耗咒痕");
                    break;
                case "curse_10":
                    text = Regex.Replace(text,
                        @"将目标所有尚未延长过的咒痕各延长\s*(\d+)\s*次敌方回合结束",
                        "目标身上所有尚未延长过的咒痕，剩余结算次数各增加 $1 次（在敌方回合结束时结算）");
                    break;
                case "curse_11":
                    text = Regex.Replace(text, @"需至少\s*(\d+)\s*层咒痕", "目标至少有 $1 层咒痕时才可使用");
                    break;
                case "tide_05":
                    text = text.Replace("自动将最多", "使最多").Replace("保留到再下一个己方回合结束", "持续到再下个己方回合结束");
                    break;
                case "tide_08":
                    text = Regex.Replace(text, @"允许\s*(\d+)\s*(?:点\s*)?潮势", "潮势为 $1 时也可使用");
                    break;
                case "tide_09":
                    text = Regex.Replace(text,
                        @"接下来\s*(?<turns>\d+)\s*个敌方回合内，目标下一次主动攻击结束后受到\s*(?<damage>\d+)\s*点伤害，触发一次消失",
                        "接下来的 ${turns} 个敌方回合内，目标下一次主动攻击结束后，对其造成 ${damage} 点伤害，随后失效");
                    break;
                case "tide_10":
                    text = Regex.Replace(text,
                        @"(?<turns>\d+)\s*个敌方回合内目标下一次完整攻击总伤害减少\s*(?<amount>\d+)\s*，最多减半，向下取整，(?:同类取最高|同类加成只保留最高值)",
                        "接下来的 ${turns} 个敌方回合内，使目标下一次攻击的总伤害减少 ${amount} 点，最多减少该次攻击总伤害的一半（向下取整）；同类效果只取最高值");
                    break;
                case "thunder_06":
                    text = Regex.Replace(text,
                        @"本回合下一张雷霆攻击总伤害提高\s*(\d+)\s*点，多段仅第一段增加",
                        "本回合下一张雷霆攻击的伤害提高 $1 点；若有多段伤害，仅提高第一段的伤害");
                    text = text.Replace("终结也可享受", "终结牌同样生效")
                        .Replace("迟雷的增伤一起延迟", "迟雷的额外伤害会随原攻击一同延迟");
                    break;
                case "thunder_07":
                    text = Regex.Replace(text, @"至少已有\s*(\d+)\s*层连锁才可用", "连锁达到 $1 层时才可使用");
                    break;
                case "thunder_08":
                    text = Regex.Replace(text, @"出牌前至少\s*(\d+)\s*层连锁时", "若出牌前已有至少 $1 层连锁，则");
                    break;
                case "thunder_10":
                    text = text.Replace("多段只追加一次", "多段攻击也只追加一次")
                        .Replace("迟雷追加一起延迟", "迟雷的额外伤害会随原攻击一同延迟");
                    break;
                case "thunder_12":
                    text = Regex.Replace(text,
                        @"目标下一次完整攻击总伤害减少\s*(?<amount>[^，；。]+)，最多\s*(?<cap>\d+)\s*且不超过一半",
                        "使目标下一次攻击的总伤害减少 ${amount} 点，最多减少 ${cap} 点，且不超过该次攻击总伤害的一半");
                    break;
                case "thunder_13":
                case "thunder_14":
                case "thunder_15":
                    text = Regex.Replace(text, @"允许\s*(\d+)\s*链", "连锁为 $1 时也可使用");
                    break;
                case "blood_06":
                    text = text.Replace("满血或未穿盾不耗次数", "己方生命值已满或本次攻击未使敌方失去生命值时，不消耗次数");
                    text = Regex.Replace(text, @"点生命(?!值)", "点生命值");
                    break;
                case "blood_07":
                    text = Regex.Replace(text, @"攻击两次，各\s*(\d+)\s*点伤害", "攻击两次，每次造成 $1 点伤害");
                    text = text.Replace("首击实际扣除敌方生命时", "若第一击使敌方失去生命值");
                    break;
                case "blood_08":
                    text = Regex.Replace(text,
                        @"每回合目标攻击首次使己方实际失去生命后，反击\s*(\d+)\s*点伤害",
                        "每回合己方首次因目标的攻击而失去生命值后，反击并造成 $1 点伤害");
                    break;
                case "blood_09":
                    text = Regex.Replace(text, @"，绝境时恢复\s*(\d+)\s*点生命值", "，若处于绝境则改为恢复 $1 点生命值");
                    text = Regex.Replace(text, @"，绝境时额外恢复\s*(\d+)\s*点生命值", "，若处于绝境则改为额外恢复 $1 点生命值");
                    text = text.Replace("按治疗前状态判断绝境", "是否处于绝境，以治疗前的状态为准");
                    break;
                case "blood_10":
                    text = text.Replace("实际扣除敌方生命时", "若使敌方失去生命值，则");
                    break;
                case "blood_11":
                    text = Regex.Replace(text, @"绝境时，改为\s*(\d+)(?!\d)(?!\s*点)", "处于绝境时，改为造成 $1 点伤害");
                    break;
                case "blood_12":
                    text = Regex.Replace(text, @"绝境时，改为\s*(\d+)(?!\d)(?!\s*点)", "处于绝境时，改为获得 $1 点格挡");
                    break;
                case "blood_13":
                    text = Regex.Replace(text, @"使用时处于绝境则改为\s*(\d+)(?!\d)(?!\s*点)", "若使用本牌时处于绝境，则改为造成 $1 点伤害");
                    text = text.Replace("施放时锁定", "伤害在使用本牌时确定");
                    break;
                case "blood_14":
                    text = text.Replace("仅绝境时可用", "只有处于绝境时才可使用").Replace("使用后移出本场。", "使用后移出本场战斗。");
                    break;
                case "blood_15":
                    text = Regex.Replace(text,
                        @"每回合第一张在绝境时打出的直接攻击伤害提高\s*(\d+)\s*点，多段只加一次",
                        "每回合在绝境时打出的第一张直接攻击牌，伤害提高 $1 点；多段攻击只加成一次");
                    text = text.Replace("延迟与反击不触发", "延迟攻击和反击不会触发");
                    break;
            }

            switch (key)
            {
                case "curse_06":
                case "tide_06":
                case "tide_07":
                case "tide_08":
                case "thunder_02":
                case "thunder_05":
                case "thunder_13":
                case "thunder_14":
                case "thunder_15":
                    text = Regex.Replace(text, @"([，；]最多)\s*(\d+)(?=[，；。]|$)", "$1 $2 点伤害");
                    break;
            }

            return text;
        }
    }
}
