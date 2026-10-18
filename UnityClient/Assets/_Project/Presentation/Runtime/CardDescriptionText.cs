using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shorter card-face wording; authoritative values and rules remain in Core.</summary>
    public static class CardDescriptionText
    {
        private sealed class LayoutSource { public string Source, Rendered; }
        private static readonly ConditionalWeakTable<Text, LayoutSource> Sources = new ConditionalWeakTable<Text, LayoutSource>();
        private static readonly Regex UnbrokenTerms = new Regex(
            @"(?:[+\-−≥≤]?\d+(?:\.\d+)?(?:×\d+)?(?:%|米|格(?!挡)|回合|层|张|发)?)" +
            @"|上一敌回合|本阶段|每回合限一次|每回合一次|至少留|船员生命|船体伤害|船员伤害" +
            @"|本回合有效|跨阶段保留|首次接舷|有效直伤|穿过格挡|清空增益|取消冲撞|转为平行" +
            @"|额外结算一次|下次炮击|额外装药|距离不超过|造成伤害|满额延烧|点燃敌舰" +
            @"|强化伤害|强化格挡|强化恢复|层数|提高|增伤|恢复|药品|出牌前" +
            @"|伤害|格挡|法力|连锁|咒印|灼烧|燃烧|上限|最多|至少|船体|船员|敌舰|冲撞航行");

        public static string Compact(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            // Recover semantic clauses before wrapping them for the actual face.
            string text = value.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\u00a0', ' ');
            // Core may have separated a comma and its result. Rejoin only that
            // punctuation; existing unpunctuated presentation rows stay rows.
            text = Regex.Replace(text, @"(?<=[，；。])\n+", string.Empty);
            text = Regex.Replace(text, @"(?<=[\u4e00-\u9fff]) +(?=[+\-]?\d)|(?<=\d) +(?=[\u4e00-\u9fff%])", string.Empty);
            text = text.Replace("伤害额外+", "伤害+").Replace("额外伤害+", "伤害+")
                .Replace("格挡额外+", "格挡+").Replace("额外格挡+", "格挡+")
                .Replace("恢复额外+", "恢复+").Replace("额外恢复+", "恢复+")
                .Replace("当前阶段生命", "本阶段生命").Replace("张当前阶段牌", "张本阶段牌")
                .Replace("自身贡献上限", "本牌上限");
            text = Regex.Replace(text, @"(?<![\d.])(?:造成)?(\d+)点船体伤害", "船体伤害$1");
            text = Regex.Replace(text, @"(?<![\d.])(?:造成)?(\d+)点船员伤害", "船员伤害$1");
            text = Regex.Replace(text, @"恢复(\d+)点船员生命", "恢复$1船员生命");
            text = Regex.Replace(text, @"修复(\d+)点船体", "修复$1船体");
            text = Regex.Replace(text, @"敌有至少(\d+)咒印", "敌咒印≥$1");
            text = Regex.Replace(text, @"敌方生命不高于(\d+)%", "敌生命≤$1%");
            text = Regex.Replace(text, @"此前连锁至少(\d+)", "此前连锁≥$1");
            text = Regex.Replace(text, @"上一敌回合吸收至少(\d+)伤害", "上一敌回合吸收伤害≥$1");
            text = Regex.Replace(text, @"施加(\d+)咒印，持续(\d+)回合", "施加$1咒印（$2回合）");
            text = Regex.Replace(text, @"吸收至少(\d+)伤害后", "吸收≥$1伤害后");
            text = Regex.Replace(text, @"接下来(\d+)个敌回合", "下$1个敌回合");
            // Compact repeated rule wording without dropping its timing, target,
            // rounding rule or exception. Values still come from Core verbatim.
            text = text.Replace("点行动点", "行动点")
                .Replace("（结果四舍五入）", "（四舍五入）")
                .Replace("下一己方回合开始", "下个己方回合初")
                .Replace("下一己方回合结束", "下个己方回合末")
                .Replace("下个敌方回合结束", "下个敌方回合末")
                .Replace("每回合目标攻击首次使己方实际失去生命时，为其附加", "每回合首次被目标攻击失血时，给其")
                .Replace("每回合目标攻击首次使己方实际失去生命后，", "每回合首次被目标攻击失血后，")
                .Replace("炮击穿过格挡造成伤害后点燃敌舰", "炮击穿盾伤敌后点燃敌舰")
                .Replace("本回合首次实际用格挡吸收敌方攻击后", "本回合首次格挡吸收敌方攻击后")
                .Replace("每回合目标第一次主动攻击结束后受到", "目标每回合首次主动攻击后受到")
                .Replace("目标下一次主动攻击结束后受到", "目标下次主动攻击后受到")
                .Replace("按敌方实际生命损失吸血", "按敌方实际失血吸血")
                .Replace("同名副本共享每回合一次成功吸血", "同名每回合共限一次成功吸血")
                .Replace("同名所有副本共享每战一次", "同名每战共限一次")
                .Replace("多段仅第一段增加", "仅首段增伤")
                .Replace("迟雷的增伤一起延迟", "迟雷增伤同延迟")
                .Replace("迟雷追加一起延迟", "迟雷追加同延迟")
                .Replace("迟雷追加同延迟；不叠加", "迟雷追加同延迟，不叠加")
                .Replace("原已燃烧则额外结算一次，每回合限一次", "原已燃烧：额外结算一次（每回合限一次）")
                .Replace("本回合和之后两个己方回合，每回合首次主动新增自燃时，为敌方附加相同层数的灼烧",
                    "本回合及随后两个己方回合，每回合首次主动新增自燃时，敌获等层灼烧")
                .Replace("使用时处于绝境则改为", "使用时绝境改为")
                .Replace("实际造成直伤后", "有效直伤后")
                .Replace("目标出牌前已燃烧时额外结算一次燃烧，每回合一次", "出牌前目标已燃烧：额外结算一次燃烧（每回合一次）")
                .Replace("激活本场海战专属规则：", "本场海战：")
                .Replace("本牌格挡实际吸收敌方攻击时等量获得潮势", "本牌格挡吸收敌方攻击等量产潮势")
                .Replace("无木板时仅获得", "无木板仅获")
                .Replace("格曼哈顿范围，每一格须合法", "格曼哈顿范围，各格须合法")
                .Replace("最迟于目标下个回合结束失效", "最迟目标下回合末失效")
                .Replace("同名副本每回合共用一次成功额度", "同名每回合共限一次成功")
                .Replace("下一次完整攻击总伤害减少", "下次完整攻击减伤")
                .Replace("施放时锁定", "施放锁定");
            text = Regex.Replace(text, @"每批持续(\d+)次敌方回合结束，本身不造成伤害", "每批$1次敌回合末后消失，无伤害");
            text = Regex.Replace(text, @"点燃敌舰，造成(\d+)×(\d+)回合燃烧", "敌舰燃烧$1×$2回合");
            text = Regex.Replace(text, @"(\d+)格曼哈顿范围，各格须合法", "曼哈顿$1格内，各格合法");
            text = Regex.Replace(text, @"本回合接下来(?=\d+张)", "本回合下");
            text = Regex.Replace(text, @"生命不高于最大生命(\d+)%", "生命≤$1%上限");
            text = Regex.Replace(text, @"生命不高于(\d+)%", "生命≤$1%");
            // A once-per-turn qualifier belongs to its triggering effect.
            text = text.Replace("；每回合一次；", "（每回合一次）；")
                .Replace("；每回合一次。", "（每回合一次）。");
            // These short clauses have explicit independent subjects/conditions.
            // Joining them does not extend a previous conditional bonus.
            text = Regex.Replace(text, @"((?:<b>)?\d+(?:格|米)内(?:</b>)?(?:炮击|近距破坏)?)；船体伤害(\d+)", "$1：船体伤害$2");
            text = Regex.Replace(text, @"消耗(\d+)火药；消耗(\d+)炮弹", "消耗$1火药、$2炮弹");
            text = Regex.Replace(text, @"(^|[；。])格挡([+\-]?\d+)；耗(\d+)木板", "$1格挡$2（耗$3木板）");
            const string basic = @"(?:格挡[+\-]?\d+|获得\d+(?:行动点|格挡)|恢复\d+船员生命|抽\d+张(?:牌)?|新增\d+层自燃)";
            text = Regex.Replace(text, @"(^|[；。])(" + basic + @"(?:[；。]" + basic + @")+)(?=[；。]|$)",
                match => match.Groups[1].Value + Regex.Replace(match.Groups[2].Value, "[；。]", "，"));
            text = text.Replace("；无药物恢复", "，无药物恢复");
            // Newlines already separate effects: redundant row-end full stops
            // and semicolons need not occupy a second line by themselves.
            // A cost and the result after its comma stay in the same paragraph.
            return Regex.Replace(text, @"[；。][ \t]*(?:\n|$)?", "\n").TrimEnd('\n');
        }

        // Repeated hover/layout refreshes must reflow the original paragraphs,
        // rather than treating a previous soft wrap as an authored paragraph.
        internal static string SourceFor(Text text)
        {
            LayoutSource state = Sources.GetValue(text, _ => new LayoutSource());
            if (text.text != state.Rendered) state.Source = text.text;
            return state.Source ?? string.Empty;
        }

        internal static void RememberLayout(Text text, string source)
        {
            LayoutSource state = Sources.GetValue(text, _ => new LayoutSource());
            state.Source = source;
            state.Rendered = text.text;
        }

        /// <summary>Wrap complete semantic terms using the same font and width as the live card.</summary>
        public static string Wrap(Text text, string value, int fontSize, bool inlineClauses = false)
        {
            if (text == null || text.font == null || string.IsNullOrEmpty(value)) return value ?? string.Empty;
            // Card descriptions use bold prerequisites. Leave unknown rich tags
            // intact rather than accidentally displaying or deleting markup.
            if (Regex.IsMatch(value, @"<(?!/?b>)[^>]+>")) return value;
            TextGenerationSettings settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.fontSize = fontSize;
            settings.scaleFactor = 1f;
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.richText = true;
            float width = text.rectTransform.rect.width;
            var output = new StringBuilder();
            using (var generator = new TextGenerator())
            {
                if (inlineClauses)
                    return WrapParagraph(value.Replace("\r", string.Empty), width, settings, generator);
                foreach (string paragraph in value.Replace("\r", string.Empty).Split('\n'))
                {
                    if (output.Length > 0) output.Append('\n');
                    output.Append(WrapParagraph(paragraph, width, settings, generator));
                }
            }
            return output.ToString();
        }

        private static string WrapParagraph(string paragraph, float width, TextGenerationSettings settings, TextGenerator generator)
        {
            string inline = paragraph.Replace('\n', '；');
            if (generator.GetPreferredWidth(inline, settings) <= width) return inline;
            var plain = new StringBuilder();
            var bold = new List<bool>();
            bool isBold = false;
            for (int i = 0; i < paragraph.Length; i++)
            {
                if (paragraph.IndexOf("<b>", i, StringComparison.Ordinal) == i) { isBold = true; i += 2; continue; }
                if (paragraph.IndexOf("</b>", i, StringComparison.Ordinal) == i) { isBold = false; i += 3; continue; }
                plain.Append(paragraph[i]); bold.Add(isBold);
            }
            string content = plain.ToString();
            int length = content.Length;
            var allowed = new bool[length + 1];
            for (int i = 0; i <= length; i++) allowed[i] = true;
            foreach (Match match in UnbrokenTerms.Matches(content))
                for (int i = match.Index + 1; i < match.Index + match.Length; i++) allowed[i] = false;
            const string closing = "，、：；。！？）】》%";
            const string opening = "（【《";
            for (int i = 1; i < length; i++)
                if (closing.IndexOf(content[i]) >= 0 || content[i] == '\n' || opening.IndexOf(content[i - 1]) >= 0) allowed[i] = false;
            var cost = new double[length + 1];
            var next = new int[length + 1];
            cost[length] = 0;
            for (int start = length - 1; start >= 0; start--)
            {
                cost[start] = double.PositiveInfinity;
                if (!allowed[start]) continue;
                for (int end = start + 1; end <= length; end++)
                {
                    if (!allowed[end] || double.IsPositiveInfinity(cost[end])) continue;
                    string line = RichSubstring(content, bold, start, end);
                    float lineWidth = generator.GetPreferredWidth(line, settings);
                    if (lineWidth > width) break;
                    // Prefer complete comma clauses and balanced lines, while
                    // retaining the smallest possible number of readable rows.
                    double gap = width - lineWidth;
                    double penalty = end < length && "，：、；\n".IndexOf(content[end - 1]) < 0 ? 90 : 0;
                    double candidate = 10000 + gap * gap * .01 + penalty + cost[end];
                    if (candidate >= cost[start]) continue;
                    cost[start] = candidate; next[start] = end;
                }
            }
            if (double.IsPositiveInfinity(cost[0])) return paragraph; // The caller retries at a smaller font.
            var result = new StringBuilder();
            for (int start = 0; start < length; start = next[start])
            {
                if (start > 0) result.Append('\n');
                result.Append(RichSubstring(content, bold, start, next[start]));
            }
            return result.ToString();
        }

        private static string RichSubstring(string content, List<bool> bold, int start, int end)
        {
            var result = new StringBuilder();
            bool active = false;
            for (int i = start; i < end; i++)
            {
                // An authored clause break remains a line break at the row end,
                // or becomes an explicit semicolon when two clauses share a row.
                if (content[i] == '\n' && i == end - 1) continue;
                if (bold[i] != active) { result.Append(bold[i] ? "<b>" : "</b>"); active = bold[i]; }
                result.Append(content[i] == '\n' ? '；' : content[i]);
            }
            if (active) result.Append("</b>");
            return result.ToString();
        }
    }
}
