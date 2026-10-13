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
            @"(?:[+\-−≥≤]?\d+(?:\.\d+)?(?:×\d+)?(?:%|米|回合|层|张|发)?)" +
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
                .Replace("当前阶段生命", "本阶段生命").Replace("张当前阶段牌", "张本阶段牌");
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
            // Basic protection and healing are simultaneous, unconditional
            // effects. Share a paragraph so support cards retain readable type
            // after gaining a small guard value; conditional clauses stay apart.
            text = Regex.Replace(text, @"^格挡([+\-]?\d+)；恢复(\d+)船员生命", "格挡$1，恢复$2船员生命");
            // A once-per-turn qualifier belongs to its triggering effect.
            text = text.Replace("；每回合一次；", "（每回合一次）；")
                .Replace("；每回合一次。", "（每回合一次）。");
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
        public static string Wrap(Text text, string value, int fontSize)
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
            if (generator.GetPreferredWidth(paragraph, settings) <= width) return paragraph;
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
                if (closing.IndexOf(content[i]) >= 0 || opening.IndexOf(content[i - 1]) >= 0) allowed[i] = false;
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
                    double penalty = end < length && "，：、".IndexOf(content[end - 1]) < 0 ? 90 : 0;
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
                if (bold[i] != active) { result.Append(bold[i] ? "<b>" : "</b>"); active = bold[i]; }
                result.Append(content[i]);
            }
            if (active) result.Append("</b>");
            return result.ToString();
        }
    }
}
