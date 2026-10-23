using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Natural Chinese sentence order for card effects, after costs and live values are resolved.</summary>
    internal static partial class CoreBattleCardChineseText
    {
        internal static string Rewrite(string text, CoreBattleCard card)
        {
            if (string.IsNullOrEmpty(text) || card == null) return text;
            // Work on semantic clauses; the final shared formatter restores their line breaks.
            text = Regex.Replace(text, @"(?<=[，；。])\s+", string.Empty);
            text = text.Replace("下一己方回合", "下个己方回合")
                .Replace("下一个敌方回合", "下个敌方回合")
                .Replace("同名所有副本共享每回合一次", "同名牌每回合合计只能使用一次")
                .Replace("同名所有副本共享每战一次", "同名牌每场战斗合计只能使用一次")
                .Replace("同名副本共享每回合一次成功吸血", "同名牌每回合合计只能成功吸血一次")
                .Replace("同名每回合一次", "同名牌每回合合计只能使用一次")
                .Replace("同类取最高", "同类加成只保留最高值");
            text = Regex.Replace(text, @"每枚\s*(\d+)\s*点伤害", "每枚造成 $1 点伤害");
            text = Regex.Replace(text, @"每次\s*([^，；。]+?)\s*点伤害", match =>
                match.Groups[1].Value.StartsWith("造成") ? match.Value : "每次造成 " + match.Groups[1].Value + " 点伤害");
            text = Regex.Replace(text, @"攻击两次，各\s*(\d+)\s*点伤害", "攻击两次，每次造成 $1 点伤害");
            text = Regex.Replace(text, @"造成\s*(\d+)\s*点船员生命伤害", "对敌方船员造成 $1 点伤害");
            text = Regex.Replace(text, @"选择\s*(\d+)\s*张牌入手", "选择 $1 张加入手牌");
            text = Regex.Replace(text, @"(下次炮击伤害提高\s*\d+\s*点)，(?:最多|可叠至)\s*\+(\d+)",
                "$1，加成最多叠加至 $2 点");
            text = Regex.Replace(text, @"本场船体防御牌额外获得\s*(\d+)\s*点格挡，(?:可叠至|最多)\s*\+(\d+)",
                "本场战斗中，船体防御牌提供的格挡提高 $1 点，最多提高 $2 点");

            text = card.BoardingSpec != null ? RewriteBoarding(text, card) : RewriteNaval(text);
            return Regex.Replace(text, @"(下个己方回合(?:开始|结束))(?!时|前|后)", "$1时");
        }
    }
}
