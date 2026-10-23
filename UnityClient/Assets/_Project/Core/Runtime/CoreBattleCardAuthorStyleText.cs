using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Shared manuscript style, applied after values and optional supply effects are resolved.</summary>
    internal static class CoreBattleCardAuthorStyleText
    {
        internal static string Rewrite(string text, CoreBattleCard card)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"(?<=[，；。])\s+", string.Empty);
            CoreNavalExpansionCardSpec field = CoreNavalSchoolExpansionCatalog.FindSpec(card?.Id);
            // Field and oil behavior is shared by every source of that keyword.
            // The card states what it places; the field/status tooltip states how it behaves.
            if (field?.IsField == true)
            {
                Match placement = Regex.Match(text, @"^[^；。]*铺设[^；。]+");
                if (placement.Success) text = placement.Value + "。";
            }
            if (field?.Mechanic == CoreNavalExpansionMechanic.OilMark)
                return "范围 1-4 格：施加浸油。";
            if (card?.SchoolEffect?.Mechanic == CoreSchoolMechanic.FireLoad)
            {
                // Four burn stacks are the shared base attached-fire rule. Upgraded or
                // special payloads remain explicit so their numerical benefit is visible.
                text = Regex.Replace(text, @"获得\s*(\d+)\s*发附火，最多储备\s*\d+\s*发[；。](?:命中后)?施加\s*(\d+)\s*层燃烧",
                    match => "获得 " + match.Groups[1].Value + " 发附火" +
                        (match.Groups[2].Value == "4" ? string.Empty : "（燃烧 " + match.Groups[2].Value + " 层）"));
            }
            text = text.Replace("咒痕", "咒印").Replace("烧掉", "焚烧").Replace("燃尽本牌", "焚烧本卡牌")
                .Replace("普通格挡", "格挡");
            text = Regex.Replace(text, @"恢复\s*(\d+)\s*点(能量|魔力|法力)", "获得 $1 点$2");
            text = Regex.Replace(text,
                @"(?:(?:额外装药|校准火控)：)?下次炮击伤害提高\s*(\d+)\s*点(?:，加成最多叠加至\s*\d+\s*点)?",
                "额外装药：$1");
            text = Regex.Replace(text,
                @"本场战斗中，船体防御牌提供的格挡提高\s*(\d+)\s*点，最多提高\s*\d+\s*点", "额外格挡：$1");
            text = Regex.Replace(text, @"(?:对|炮击)\s*4\s*格内的敌舰(?:发动近距破坏|施法)?[，,]?\s*(先)?造成", "范围 1-4 格：$1造成");
            text = Regex.Replace(text, @"对\s*150\s*米内的敌舰(?:发动近距破坏)?[，,]?\s*(先)?造成", "范围 1-2 格：$1造成");
            text = Regex.Replace(text, @"对\s*350\s*米内的敌舰(?:施法)?[，,]?\s*(先)?造成", "范围 1-4 格：$1造成");
            text = Regex.Replace(text, @"(^|[；。])\s*4\s*格内[；：]", "$1范围 1-4 格：");
            text = Regex.Replace(text, @"在\s*4\s*格内(铺设|沿横向或纵向铺设|选择)", "范围 1-4 格：$1");
            text = Regex.Replace(text, @"使\s*4\s*格内目标", "范围 1-4 格：使目标");
            text = Regex.Replace(text, @"下个己方回合开始时造成", "延迟：造成");
            text = Regex.Replace(text, @"[；，]伤害在使用本牌时确定", string.Empty);
            text = Regex.Replace(text, @"攻击\s*(\d+)\s*次，每次造成\s*([^；。]+?)\s*点伤害", "造成 $2 点伤害 $1 次");
            text = Regex.Replace(text, @"攻击两次，每次造成\s*([^；。]+?)\s*点伤害", "造成 $1 点伤害 2 次");
            text = Regex.Replace(text, @"连锁达到\s*(\d+)\s*层时才可使用[；，]", "连锁 $1：");
            text = Regex.Replace(text, @"(?:若出牌前已有至少|已有至少)\s*(\d+)\s*层连锁(?:时|，则)", "连锁 $1：");
            // Keyword rules belong to the shared help, while card-specific limits remain visible.
            text = text.Replace("，不消耗连锁", string.Empty)
                .Replace("。是否处于绝境，以治疗前的状态为准", string.Empty);
            return text;
        }
    }
}
