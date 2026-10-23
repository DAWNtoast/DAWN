using System;
using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Moves card costs to their existing icon quotes while retaining effect conditions and formulas.</summary>
    internal static class CoreBattleCardCostText
    {
        private const string Resource = @"(?:当前阶段生命|船员生命|生命值?|船体|能量|火药|炮弹|木板|药物|药品|法力|魔力|咒印|咒痕|潮势|连锁)";
        private const string Quantity = @"\d+\s*(?:点|层|发|份|个)?\s*" + Resource;
        private static readonly Regex FixedCost = new Regex(
            @"(?<prefix>^|[，；。\r\n：])\s*(?:施放时\s*)?(?:先\s*)?(?:固定\s*)?(?:消耗|支付|使用|耗)\s*" +
            Quantity + @"(?:\s*(?:、|和|及)\s*" + Quantity + @")*(?:\s*（血契特权）)?(?:\s*(?:，\s*再|并))?");

        internal static string RemoveIconCosts(string text, CoreBattleCard card)
        {
            if (string.IsNullOrEmpty(text) || card == null) return text;
            // Quoted live totals already include these bonuses. A condition such as "敌方燃烧时"
            // remains meaningful and is deliberately not covered by this standalone-clause rule.
            text = Regex.Replace(text, @"消耗全部已有连锁\s*强化伤害", string.Empty);
            text = RewriteVariableCosts(text);
            text = FixedCost.Replace(text, match =>
            {
                string following = text.Substring(match.Index + match.Length).TrimStart();
                // SupplyText describes a true optional payment and the difference it purchases.
                // The payment is essential to that effect even though its current quote also has an icon.
                if (Regex.IsMatch(following, @"^额外(?:恢复|修复|获得)")) return match.Value;
                return match.Groups["prefix"].Value;
            });
            text = Regex.Replace(text, @"(^|[，；。\r\n：])\s*强化(?:伤害|格挡|恢复)\s*(?=[，；。\r\n]|$)", "$1");
            text = Regex.Replace(text, @"\s*([，；。])\s*", "$1");
            text = Regex.Replace(text, @"[，；。]{2,}", match => match.Value.Contains("。") ? "。" :
                match.Value.Contains("；") ? "；" : "，");
            text = Regex.Replace(text, @"(^|[；。])\s*再(?=恢复|修复)", "$1");
            text = text.Trim().Trim('，', '；', '。');
            return text.Length == 0 || text.EndsWith("！", StringComparison.Ordinal) || text.EndsWith("？", StringComparison.Ordinal)
                ? text : text + "。";
        }

        private static string RewriteVariableCosts(string text)
        {
            // Keep selectable ranges: these tell the player how X / "所选" feeds the result.
            text = Regex.Replace(text,
                @"消耗\s*(\d+)\s*[～~至]\s*(\d+)\s*(点|层)?\s*(咒印|咒痕|潮势|连锁|魔力|法力)",
                match => "选择 " + match.Groups[1].Value + "～" + match.Groups[2].Value + " " +
                    match.Groups[3].Value + match.Groups[4].Value);

            // Percentage retaliation uses the paid amount as its damage base, not as a second cost sentence.
            text = Regex.Replace(text, @"消耗全部潮势\s*，\s*按\s*(\d+)\s*%\s*(追加|折入)伤害",
                "按出牌前潮势的 $1% $2伤害");
            text = Regex.Replace(text, @"消耗(?:全部|所有)(?:已有)?连锁\s*，\s*每层追加",
                "每层出牌前连锁追加");
            text = Regex.Replace(text, @"消耗(?:全部|所有)咒印\s*，\s*每层追加", "每层出牌前咒印追加");
            text = Regex.Replace(text, @"消耗(?:全部|所有)咒印\s*，\s*伤害随咒印层数提高", "伤害随出牌前咒印层数提高");

            // All-resource costs use X on the matching resource icon. The effect keeps
            // its formula without repeating what X means in the description.
            text = Regex.Replace(text, @"并\s*消耗全部连锁\s*X", string.Empty);
            text = Regex.Replace(text, @"消耗全部可用(?:魔力|法力)\s*，\s*最多\s*(\d+)\s*点",
                string.Empty);
            text = Regex.Replace(text, @"消耗(?:全部|所有)咒痕\s*，\s*至少\s*(\d+)\s*层",
                "需至少 $1 层咒痕");
            text = Regex.Replace(text, @"消耗全部潮势\s*，\s*允许\s*0", "允许 0 潮势");
            text = Regex.Replace(text, @"消耗全部潮势\s*X?\s*，?", string.Empty);
            text = Regex.Replace(text, @"消耗全部(?:已有)?连锁\s*X?\s*，?", string.Empty);
            // Selecting cards to burn is not an all-resource payment. Keep its count
            // and allowed range together with the action, rather than defining X separately.
            text = Regex.Replace(text, @"烧掉\s*X\s*张其他手牌\s*，\s*X为\s*(\d+)\s*至\s*(\d+)",
                "烧掉 X 张其他手牌（$1～$2 张）");
            return text;
        }
    }
}
