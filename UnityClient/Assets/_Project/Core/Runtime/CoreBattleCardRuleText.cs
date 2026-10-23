using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// Removes shared-rule explanations from card copy only. Authored templates stay intact:
    /// upgrades and live quotes resolve their values before this presentation-only pass.
    /// General rule help and battle logs continue to use FormatEffects without this filter.
    /// </summary>
    internal static partial class CoreBattleCardRuleText
    {
        private static readonly Dictionary<string, Regex> PhrasePatterns = new Dictionary<string, Regex>(StringComparer.Ordinal);
        private static readonly Regex ExpansionId = new Regex(@"^card_school_(fire|arcane|curse|tide|thunder|blood)_n[0-9]{2}$");

        internal static string RemoveCommonRules(string value, CoreBattleCard card)
        {
            if (string.IsNullOrEmpty(value) || card == null) return value;
            string text = Remove(value, "同名牌本回合每成功使用一次，后续费用 +1。");
            // Do not consult either catalog here: this runs inside their card constructors.
            if (card.BoardingSpec != null) text = RemoveBoardingRules(text, card);
            else if (ExpansionId.IsMatch(card.Id ?? string.Empty)) text = RemoveExpansionRules(text, card);
            else text = RemoveStandardRules(text, card);
            text = Regex.Replace(text, @"\s*([，；。])\s*", "$1");
            text = Regex.Replace(text, @"[，；。]{2,}", match => match.Value.Contains("。") ? "。" :
                match.Value.Contains("；") ? "；" : "，");
            text = text.Trim().TrimStart('，', '；', '。').TrimEnd('，', '；');
            if (text.Length > 0 && text[text.Length - 1] != '。' && text[text.Length - 1] != '！' && text[text.Length - 1] != '？')
                text += "。";
            return text;
        }

        private static string Remove(string text, params string[] phrases)
        {
            foreach (string phrase in phrases) text = Rewrite(text, phrase, string.Empty);
            return text;
        }

        private static string Rewrite(string text, string source, string replacement)
        {
            Regex pattern;
            lock (PhrasePatterns)
            {
                if (!PhrasePatterns.TryGetValue(source, out pattern))
                {
                    var expression = new StringBuilder();
                    foreach (char c in source)
                    {
                        if (char.IsWhiteSpace(c)) continue;
                        if (expression.Length > 0) expression.Append(@"\s*");
                        expression.Append(Regex.Escape(c.ToString()));
                    }
                    pattern = new Regex(expression.ToString(), RegexOptions.CultureInvariant);
                    PhrasePatterns.Add(source, pattern);
                }
            }
            return pattern.Replace(text, match => replacement);
        }

        private static string RemoveExpansionRules(string text, CoreBattleCard card)
        {
            // Keep range and the field's own area, effect values and trigger limits.
            text = Rewrite(text, "4 格曼哈顿范围，每一格须合法", "4 格内");
            text = Remove(text, "持续至第 2 次后续己方回合开始，放置本身不触发。",
                "；放置不触发", "，结束回合停留补伤", "；单结算链最多一次场地推动",
                "，支付后至少留 1 点", "；不提前触发咒印奖励", "；咒印全部耗尽时清空整批状态",
                "，未选择的潮势保留", "；所选量不能超过现有格挡", "；不造成伤害");
            // Every active thunder attack grants one chain, including these multi-hit attacks.
            // Their bonus hit restrictions and explicit extra-chain effects remain card effects.
            text = Rewrite(text, "；整牌一次攻击奖励、1 连锁，额外段", "；额外段");
            text = Remove(text, "；结算后 +1 连锁");
            return text;
        }
    }
}
