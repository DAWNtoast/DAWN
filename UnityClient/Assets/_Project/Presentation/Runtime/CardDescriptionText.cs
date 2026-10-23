using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Compact semantic rows using Core's shared card vocabulary and authoritative values.</summary>
    public static class CardDescriptionText
    {
        private sealed class LayoutSource { public string Source, Rendered; }
        private static readonly ConditionalWeakTable<Text, LayoutSource> Sources = new ConditionalWeakTable<Text, LayoutSource>();
        private static readonly Regex UnbrokenTerms = new Regex(
            @"(?:[+\-−≥≤]?\d+(?:\.\d+)?(?:×\d+)?(?: *(?:%|点|米|格(?!挡)|回合|层|张|发))?)" +
            @"|上一|敌回合|本阶段|每回合|限一次|至少留|船员生命|船体伤害|船员伤害" +
            @"|本回合|有效|跨阶段|保留|首次|接舷|直伤|穿过|清空|增益|取消|转为|平行" +
            @"|额外|结算|一次|下次|炮击|装药|距离|不超过|造成|满额|延烧|点燃" +
            @"|强化|层数|提高|增伤|恢复|药品|出牌前" +
            @"|伤害|格挡|法力|连锁|咒印|灼烧|燃烧|上限|最多|至少|船体|船员|敌舰|冲撞航行");

        public static string Compact(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            // Recover semantic clauses before wrapping them for the actual face.
            string text = CoreBattleCardText.FormatEffects(value.Replace('\u00a0', ' ')
                .Replace("（四舍五入）", "（结果四舍五入）"));
            // Core may have separated a comma and its result. Rejoin only that
            // punctuation; existing unpunctuated presentation rows stay rows.
            text = Regex.Replace(text, @"(?<=[，；。])\n+", string.Empty);
            text = Regex.Replace(text, @"(?<=[\u4e00-\u9fff]) +(?=[+\-]?\d)|(?<=\d) +(?=[\u4e00-\u9fff%])", string.Empty);
            text = text.Replace("张当前阶段牌", "张本阶段牌")
                .Replace("自身贡献上限", "本牌上限");
            text = Regex.Replace(text, @"敌有至少(\d+)(?:层)?咒印", "敌咒印≥$1");
            text = Regex.Replace(text, @"敌方生命不高于(\d+)%", "敌生命≤$1%");
            text = Regex.Replace(text, @"此前连锁至少(\d+)", "此前连锁≥$1");
            text = Regex.Replace(text, @"上一敌回合吸收至少(\d+)(?:点)?伤害", "上一敌回合吸收伤害≥$1");
            text = Regex.Replace(text, @"施加(\d+)(?:层)?咒印，持续(\d+)回合", "施加$1层咒印（$2回合）");
            text = Regex.Replace(text, @"吸收至少(\d+)(?:点)?伤害后", "吸收≥$1点伤害后");
            text = Regex.Replace(text, @"接下来(\d+)个敌(方?)回合", "下$1个敌$2回合");
            // Compact repeated rule wording without dropping its timing, target,
            // rounding rule or exception. Values still come from Core verbatim.
            text = text.Replace("下一己方回合开始", "下个己方回合初")
                .Replace("下一己方回合结束", "下个己方回合末")
                .Replace("下个己方回合结束", "下个己方回合末")
                .Replace("直到下个己方回合开始", "至下个己方回合初")
                .Replace("下个敌方回合结束", "下个敌方回合末")
                .Replace("每回合目标攻击首次使己方实际失去生命时，为其附加", "每回合首次被目标攻击失血时，给其")
                .Replace("每回合目标攻击首次使己方实际失去生命时，为其施加", "每回合首次被目标攻击失血时，为其施加")
                .Replace("每回合目标攻击首次使己方实际失去生命后，", "每回合首次被目标攻击失血后，")
                .Replace("炮击穿过格挡造成伤害后点燃敌舰", "炮击穿过格挡造成伤害后，点燃敌舰")
                .Replace("本回合首次实际用格挡吸收敌方攻击后", "本回合首次格挡吸收敌方攻击后")
                .Replace("每回合目标第一次主动攻击结束后受到", "每回合目标首次主动攻击结束后受到")
                .Replace("目标下一次主动攻击结束后受到", "目标下次主动攻击结束后受到")
                .Replace("按敌方实际生命损失吸血", "按敌方实际失血吸血")
                .Replace("同名副本共享每回合一次成功吸血", "同名每回合共限一次成功吸血")
                .Replace("同名所有副本共享每回合一次", "同名每回合共限一次")
                .Replace("同名所有副本共享每战一次", "同名每战共限一次")
                .Replace("多段仅第一段增加", "多段攻击仅首段增加")
                .Replace("迟雷的增伤一起延迟", "迟雷的增伤也会延迟")
                .Replace("迟雷追加一起延迟", "迟雷的追加伤害也会延迟")
                .Replace("迟雷的追加伤害也会延迟；不叠加", "迟雷的追加伤害也会延迟，不叠加")
                .Replace("原已燃烧则额外结算一次，每回合限一次", "原已燃烧时，额外结算一次（每回合限一次）")
                .Replace("本回合和之后两个己方回合，每回合首次主动新增自燃时，为敌方附加相同层数的灼烧",
                    "本回合及随后两个己方回合，每回合首次主动新增自燃时，为敌方施加相同层数的灼烧")
                .Replace("实际造成直伤后", "造成有效直伤后")
                .Replace("目标出牌前已燃烧时额外结算一次燃烧，每回合一次", "出牌前目标已燃烧时，额外结算一次燃烧（每回合一次）")
                .Replace("激活本场海战专属规则：", "本场海战：")
                .Replace("本牌格挡实际吸收敌方攻击时等量获得潮势", "本牌格挡吸收敌方攻击时，获得等量潮势")
                .Replace("格曼哈顿范围，每一格须合法", "格曼哈顿范围，各格须合法")
                .Replace("最迟于目标下个回合结束失效", "最迟在目标下回合末失效")
                .Replace("同名副本每回合共用一次成功额度", "同名每回合共限一次成功")
                .Replace("下一次完整攻击总伤害减少", "下次完整攻击的总伤害减少")
                .Replace("有药物时固定消耗", "有药物时消耗")
                .Replace("本回合和下个己方回合末各恢复", "本次及下次己方回合末各恢复")
                .Replace("已有连锁强化伤害", "已有连锁会提高伤害")
                .Replace("不超过上限", "不超上限")
                .Replace("回合开始位于阵内", "回合初位于阵内")
                .Replace("本次攻击后获得", "攻击后获得")
                .Replace("回合结束清空", "回合末清空")
                .Replace("触发一次消失", "触发一次后消失");
            // Only the complete naval-only rule can share its lifetime with
            // the prefix. Standalone cleanup clauses keep their full scope.
            text = Regex.Replace(text, @"本场海战：([^。\n]*?)；同类不叠加，接舷或战斗结束清除", "仅本场海战有效：$1；同类不叠加");
            text = Regex.Replace(text, @"支付(\d+)点船体，支付后至少留(\d+)点", "支付$1点船体（至少留$2点）");
            text = Regex.Replace(text, @"支付前己方船体不高于最大船体(\d+)%\s*时", "支付前己方船体≤$1%上限时");
            text = Regex.Replace(text, @"每批持续(\d+)次敌方回合结束，本身不造成伤害", "每批在$1次敌方回合结束后消失，本身不造成伤害");
            text = Regex.Replace(text, @"点燃敌舰，造成(\d+)×(\d+)回合燃烧", "点燃敌舰，每回合造成$1点燃烧伤害，持续$2回合");
            text = Regex.Replace(text, @"(\d+)格曼哈顿范围，各格须合法", "曼哈顿$1格内，各格合法");
            text = Regex.Replace(text, @"本回合接下来(?=\d+张)", "本回合下");
            text = Regex.Replace(text, @"生命不高于最大生命(\d+)%", "生命≤$1%上限");
            text = Regex.Replace(text, @"生命不高于(\d+)%", "生命≤$1%");
            // Both values share the same explicitly named healing action and
            // life pool; the medicine branch and threshold remain in the text.
            text = Regex.Replace(text, @"恢复(\d+)点(当前阶段生命|船员生命|生命)，绝境时恢复(\d+)点\2", "恢复$1点$2（绝境$3点）");
            text = Regex.Replace(text, @"每层在回合末造成(\d+)点伤害，持续(\d+)回合", "每层回合末造成$1点伤害，共$2回合");
            text = Regex.Replace(text, @"持续至第(\d+)次后续己方回合开始，放置本身不触发", "至第$1次后续己方回合初，放置不触发");
            // Adjacent unqualified field-placement exceptions share one scope.
            // Do not deduplicate qualified rules such as first placement.
            text = Regex.Replace(text, @"(^|[。；])放置不触发[。；](至第\d+次后续己方回合初)，放置不触发", "$1放置不触发。$2");
            text = Regex.Replace(text, @"((?:<b>)?\d+米内(?:</b>)?)近距破坏", "$1破坏");
            // A once-per-turn qualifier belongs to its triggering effect.
            text = text.Replace("；每回合一次；", "（每回合一次）；")
                .Replace("；每回合一次。", "（每回合一次）。");
            // These short clauses have explicit independent subjects/conditions.
            // Joining them does not extend a previous conditional bonus.
            text = Regex.Replace(text, @"((?:<b>)?\d+(?:格|米)内(?:</b>)?(?:炮击|近距破坏|破坏)?)；(?:造成)?(\d+)点船体伤害", "$1：造成$2点船体伤害");
            text = Regex.Replace(text, @"消耗(\d+)火药；消耗(\d+)炮弹", "消耗$1火药、$2炮弹");
            text = Regex.Replace(text, @"消耗(\d+)木板；(?=本牌格挡)", "消耗$1木板，");
            text = Regex.Replace(text, @"(^|[；。])获得(\d+)点格挡；消耗(\d+)木板", "$1获得$2点格挡（消耗$3木板）");
            const string basic = @"(?:获得\d+点(?:行动点|(?:基础|普通|蓄潮|额外|船员)?格挡)|恢复\d+点(?:船员生命|当前阶段生命|生命)|修复\d+点船体|造成\d+点(?:船体|船员生命|船员|直接|炮击)?伤害|抽\d+张牌|新增\d+层自燃|无需弹药)";
            text = Regex.Replace(text, @"(^|[；。])(" + basic + @"(?:[；。]" + basic + @")+)(?=[；。]|$)",
                match => match.Groups[1].Value + Regex.Replace(match.Groups[2].Value, "[；。]", "，"));
            text = text.Replace("；无药物恢复", "，无药物恢复");
            // Layout compaction must not replace the shared verbs, units or
            // spacing with a second card-face dialect. Restore Core wording
            // after the shorthand matchers, retaining the semantic rows above.
            text = CoreBattleCardText.FormatEffects(text);
            text = Regex.Replace(text, @"(?<=[，；。])\n+", string.Empty);
            // The small card face uses the same words and units as Core, with
            // tight Chinese numeric typography. Do not spend line width on
            // optional spaces or make the complete effect one indivisible token.
            text = Regex.Replace(text, @"(?<=[\u4e00-\u9fff]) +(?=[+\-]?\d)|(?<=\d) +(?=[\u4e00-\u9fff%])", string.Empty);
            text = text.Replace("（结果四舍五入）", "（四舍五入）");
            text = Regex.Replace(text, @"(?<=[）)]) +(?=点)", string.Empty);
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
