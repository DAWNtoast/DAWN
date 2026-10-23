using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// Shared vocabulary for rendered card descriptions. This runs after authored
    /// upgrade substitutions; DescriptionTemplate must retain its original formulas.
    /// Only verbs, units and spacing change, never numeric expressions or game rules.
    /// </summary>
    internal static class CoreBattleCardWording
    {
        private const string Atom = @"(?:\d+(?:\.\d+)?X?|X|当前咒痕层数|触发时咒痕层数|出牌前连锁|出牌前自燃层数|当前自燃层数|所选咒印|所选潮势|移除格数|向下取整的\d+(?:\.\d+)?X)";
        private const string Formula = Atom + @"(?:[+＋×÷]" + Atom + @")*";
        private const string Amount = @"(?:\(" + Formula + @"\)×\d+(?:\.\d+)?（结果四舍五入）|" + Formula + @")(?:[/／]\d+)*";
        private const string Value = @"(?<value>" + Amount + @")(?![\dA-Za-z.%+＋×÷/])";
        private const string QuantityStart = @"(?<![\dA-Za-z.+＋×÷/（(])";

        private sealed class Rule
        {
            private readonly Regex expression;
            private readonly string replacement;

            public Rule(string pattern, string replacement)
            {
                expression = new Regex(pattern, RegexOptions.CultureInvariant);
                this.replacement = replacement;
            }

            public string Apply(string text) => expression.Replace(text, replacement);
        }

        private static readonly Rule[] Rules =
        {
            // Immediate gains are verbs; bonuses to later attacks retain that scope.
            new Rule(@"格挡\s*额外\s*[+＋]\s*" + Value, "额外获得 ${value} 点格挡"),
            new Rule(@"(?<extra>额外)?(?<kind>船员)?格挡\s*[+＋]\s*" + Value, "${extra}获得 ${value} 点${kind}格挡"),
            new Rule(@"额外\s*[+＋]\s*" + Value + @"\s*格挡", "额外获得 ${value} 点格挡"),
            new Rule(@"基础格挡\s*" + Value, "获得 ${value} 点基础格挡"),
            new Rule(@"敌舰船体\s*[-−]\s*" + Value, "造成 ${value} 点船体伤害"),
            new Rule(@"敌方船员\s*[-−]\s*" + Value, "造成 ${value} 点船员生命伤害"),
            new Rule(@"(?<kind>船体|船员)伤害\s*[+＋]\s*" + Value, "${kind}伤害提高 ${value} 点"),
            new Rule(@"(?<kind>船体|船员)伤害\s*" + Value, "造成 ${value} 点${kind}伤害"),
            new Rule(@"(?<prefix>^|[：；。\n])\s*" + Value + @"\s*点?(?<kind>船体|船员生命|船员|直接|炮击)?伤害", "${prefix}造成 ${value} 点${kind}伤害"),
            new Rule(@"伤害\s*额外\s*[+＋]\s*" + Value, "伤害提高 ${value} 点"),
            new Rule(@"额外伤害\s*[+＋]\s*" + Value, "伤害提高 ${value} 点"),
            new Rule(@"额外\s*[+＋]\s*" + Value + @"\s*伤害", "伤害提高 ${value} 点"),
            new Rule(@"伤害\s*[+＋]\s*" + Value, "伤害提高 ${value} 点"),
            new Rule(@"下次炮击\s*[+＋]\s*" + Value, "下次炮击伤害提高 ${value} 点"),
            new Rule(@"(?<attack>下张主动攻击首段|第二击)\s*[+＋]\s*" + Value, "${attack}伤害提高 ${value} 点"),
            new Rule(@"(?<condition>出牌前每有\s*\d+\s*层自燃)再\s*[+＋]\s*" + Value, "${condition}伤害提高 ${value} 点"),
            new Rule(@"(?<condition>支付前己方船体不高于最大船体\s*\d+%\s*时)额外\s*[+＋]\s*" + Value, "${condition}伤害提高 ${value} 点"),
            new Rule(@"(?<resource>法力|魔力)上限\s*[+＋]\s*" + Value, "${resource}上限提高 ${value} 点"),
            new Rule(@"(?<resource>法力|魔力)\s*[+＋]\s*" + Value, "恢复 ${value} 点${resource}"),
            new Rule(@"连锁\s*[+＋]\s*" + Value, "获得 ${value} 层连锁"),
            new Rule(@"额外\s*[+＋]\s*" + Value + @"\s*连锁", "额外获得 ${value} 层连锁"),
            new Rule(@"结算后\s*[+＋]\s*" + Value + @"\s*连锁", "结算后获得 ${value} 层连锁"),
            new Rule(@"(?<effect>新增格挡|新增装填|主动攻击伤害)\s*[−-]\s*" + Value, "${effect}减少 ${value} 点"),
            new Rule(@"(?<condition>已有至少\s*\d+\s*(?:层\s*)?连锁时)改为\s*[−-]\s*" + Value, "${condition}改为减少 ${value} 点"),

            new Rule(@"主动移动上限\s*[−-]\s*" + Value, "主动移动上限减少 ${value} 格"),

            // Supplies use one verb; hull repair and healing remain distinct effects.
            new Rule(@"(?:恢复额外|额外恢复)\s*[+＋]\s*" + Value, "恢复量提高 ${value} 点"),
            new Rule(@"恢复(?<kind>当前阶段生命|船员生命)\s*" + Value, "恢复 ${value} 点${kind}"),
            new Rule(@"恢复船员\s*" + Value, "恢复 ${value} 点船员生命"),
            new Rule(@"修复船体\s*" + Value, "修复 ${value} 点船体"),
            new Rule(@"(?<!消)耗\s*(?<value>\d+)\s*(?<kind>木板|火药|炮弹|药物)", "消耗 ${value} ${kind}"),
            new Rule(@"使用\s*(?<value>\d+)\s*药物", "消耗 ${value} 药物"),
            new Rule(@"恢复\s*" + Value + @"\s*(?:点\s*)?船体", "修复 ${value} 点船体"),
            new Rule(@"恢复\s*" + Value + @"(?=[，；。\n]|$)", "恢复 ${value} 点生命"),
            new Rule(@"仅获(?=\s*\d)", "仅获得"),
            new Rule(@"获(?=\s*" + Amount + @"\s*(?:点\s*)?格挡)", "获得"),
            new Rule(@"附加(?=\s*(?:\d|X))", "施加"),

            // Retain complete formulas, including rounded upgraded expressions.
            new Rule(QuantityStart + Value + @"\s*(?:点\s*)?(?<kind>雷霆)?直伤", "${value} 点${kind}直接伤害"),
            new Rule(QuantityStart + Value + @"\s*(?:点\s*)?(?<kind>基础|普通|蓄潮|额外|船员)?(?:的)?格挡", "${value} 点${kind}格挡"),
            new Rule(QuantityStart + Value + @"\s*(?:点\s*)?(?<kind>白刃与即时火焰混合|船员生命|船体|船员|即时火焰|咒术|雷霆直接|雷霆|场地|炮击|直接)?伤害", "${value} 点${kind}伤害"),
            new Rule(QuantityStart + Value + @"\s*(?:点\s*)?(?<kind>当前阶段生命|船员生命|生命|船体)", "${value} 点${kind}"),
            new Rule(QuantityStart + Value + @"\s*(?:点\s*)?(?<kind>临时法力|法力|魔力|能量|行动点|潮势)", "${value} 点${kind}"),
            new Rule(QuantityStart + Value + @"\s*(?:层\s*)?(?<kind>咒印|咒痕|连锁)", "${value} 层${kind}"),
            new Rule(@"(?<value>\d+)\s*(?<kind>火药|炮弹|木板|药物)", "${value} ${kind}"),
            new Rule(@"抽\s*(?<value>\d+)\s*张(?:牌)?(?!当前阶段|本阶段|其他|手牌|攻击牌|防御牌)", "抽 ${value} 张牌"),
            new Rule(@"选择\s*(?<value>\d+)\s*张(?:牌)?入手", "选择 ${value} 张牌入手"),
            new Rule(@"查看牌库顶\s*(?<value>\d+)\s*张(?:牌)?", "查看牌库顶 ${value} 张牌"),

            new Rule(@"(?<=[\u4e00-\u9fff])(?=\d)", " "),
            new Rule(@"(?<=\d)(?=[\u4e00-\u9fff])", " "),
            new Rule(@"(?<=[\u4e00-\u9fff])(?=X(?:\s|张))", " "),
            new Rule(@"(?<=X)(?=张)", " "),
        };

        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string text = value.Replace("药品", "药物").Replace("炮战", "海战").Replace("白刃战", "接舷战");
            foreach (Rule rule in Rules) text = rule.Apply(text);
            return text;
        }
    }
}
