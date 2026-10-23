using System;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Preserves saved upgrade ranks without leaving rank-zero literals on a card face.</summary>
    public static class CoreBoardingCardDescriptions
    {
        public static string AtLevel(CoreBattleCard design, int level)
        {
            if (design == null) return string.Empty;
            string text = design.DescriptionTemplate;
            if (design.BoardingSpec == null || level <= 0) return text;
            string authored = CoreBoardingManuscriptDescriptions.ForKey(design.BoardingSpec.Key, design, level);
            if (authored != null) return authored;
            int S(int value) => CoreCardUpgradeDesign.ScaleEffect(design, value, level);
            string Values(params int[] values) => string.Join("/", Array.ConvertAll(values, value => S(value).ToString()));
            string Formula(string expression) => "(" + expression + ")×" +
                CoreCardUpgradeDesign.EffectMultiplier(design, level).ToString(CultureInfo.InvariantCulture) + "（结果四舍五入）";
            switch (design.BoardingSpec.Key)
            {
                case "fire_01":
                    // The two flavors resolve as one hit: rounding each flavor independently would lie about the total.
                    return "消耗1火药；造成 {damage} 点白刃与即时火焰混合伤害，作为一次攻击结算。";
                case "fire_02": return text.Replace("附加4层", "附加" + S(4) + "层");
                case "fire_03": return text.Replace("附加8层", "附加" + S(8) + "层");
                case "fire_04": return text.Replace("附加6层", "附加" + S(6) + "层");
                case "fire_05": return text.Replace("恢复10生命", "恢复 {repair} 生命");
                case "fire_06": return text.Replace("附加3层", "附加" + S(3) + "层").Replace("改为5层", "改为" + S(5) + "层");
                case "fire_07": return text.Replace("4+3X层灼烧，即7/10/13层", Formula("4+3X") + "层灼烧，即" + Values(7, 10, 13) + "层");
                case "fire_08": return text.Replace("2+4X格挡，即6/10/14", Formula("2+4X") + "格挡，即" + Values(6, 10, 14));
                case "fire_11": return text.Replace("造成30伤害，出牌前每有1层自燃再+2，最多+6",
                    "按出牌前0/1/2/至少3层自燃，分别造成" + Values(30, 32, 34, 36) + "伤害");
                case "arcane_04": return text.Replace("24+6X伤害，即24/30/36/42", Formula("24+6X") + "伤害，即" + Values(24, 30, 36, 42));
                case "arcane_05": return text.Replace("恢复13生命", "恢复 {repair} 生命");
                case "arcane_06": return text.Replace("每枚5伤害，总计10/15/20/25", "每枚" + S(5) + "伤害，总计" +
                    2 * S(5) + "/" + 3 * S(5) + "/" + 4 * S(5) + "/" + 5 * S(5));
                case "arcane_07": return text.Replace("最多8格挡", "最多" + S(8) + "格挡");
                case "arcane_08": return text.Replace("最多4点", "最多" + S(4) + "点");
                case "arcane_11": return text.Replace("36+8X伤害，即36/44/52/60", Formula("36+8X") + "伤害，即" + Values(36, 44, 52, 60));
                case "arcane_13": return text.Replace("最多18", "最多" + S(18));
                case "curse_06": return text.Replace("8+当前咒痕层数伤害，最多14", Formula("8+当前咒痕层数") + "伤害，最多" + S(14));
                case "curse_07": return text.Replace("减少3+触发时咒痕层数，最多9", "减少" + Formula("3+触发时咒痕层数") + "，最多" + S(9));
                case "curse_08": return text.Replace("4+2×当前咒痕层数的格挡", Formula("4+2×当前咒痕层数") + "的格挡")
                    .Replace("减少2+触发时咒痕层数", "减少" + Formula("2+触发时咒痕层数"));
                case "curse_09": return text.Replace("当前咒痕层数×2的咒术伤害，上限10",
                    Formula("当前咒痕层数×2") + "的咒术伤害，上限" + S(10));
                case "curse_11": return text.Replace("20+8X咒术伤害，即28/36/44/52/60/68", Formula("20+8X") + "咒术伤害，即" + Values(28, 36, 44, 52, 60, 68));
                case "curse_12": return text.Replace("恢复13生命", "恢复 {repair} 生命");
                case "curse_15": return text.Replace("获得10格挡", "获得 {block} 格挡");
                case "tide_04": return text.Replace("最多4格挡", "最多" + S(4) + "格挡");
                case "tide_06": return text.Replace("6+X伤害，最多18", Formula("6+X") + "伤害，最多" + S(18));
                case "tide_07": return text.Replace("16+X伤害，最多28", Formula("16+X") + "伤害，最多" + S(28));
                case "tide_08": return text.Replace("30+向下取整的1.5X伤害，最多48", Formula("30+向下取整的1.5X") + "伤害，最多" + S(48));
                case "tide_09": return text.Replace("受到16伤害", "受到" + S(16) + "伤害");
                case "tide_10": return text.Replace("总伤害减少3", "总伤害减少" + S(3));
                case "tide_11": return text.Replace("恢复10生命", "恢复 {repair} 生命");
                case "tide_12": return text.Replace("各恢复8", "各恢复 {repair}");
                case "thunder_02": return text.Replace("8+出牌前连锁伤害，最多14", Formula("8+出牌前连锁") + "伤害，最多" + S(14));
                case "thunder_04": return text.Replace("最多6格挡", "最多" + S(6) + "格挡");
                case "thunder_05": return text.Replace("14+出牌前连锁伤害，最多20", Formula("14+出牌前连锁") + "伤害，最多" + S(20));
                case "thunder_06": return text.Replace("总伤害+8", "总伤害+" + S(8));
                case "thunder_10": return text.Replace("各追加4伤害", "各追加" + S(4) + "伤害");
                case "thunder_11": return text.Replace("每次3+出牌前连锁伤害，总计6至18", "每次" + Formula("3+出牌前连锁") + "伤害，总计" + 2 * S(3) + "至" + 2 * S(9));
                case "thunder_12": return text.Replace("造成5伤害", "造成 {damage} 伤害")
                    .Replace("减少2+向下取整的出牌前连锁÷2，最多5", "减少" + Formula("2+向下取整的出牌前连锁÷2") + "，最多" + S(5));
                case "thunder_13": return text.Replace("20+5X伤害，最多50", Formula("20+5X") + "伤害，最多" + S(50));
                case "thunder_14": return text.Replace("16+4X伤害，最多40", Formula("16+4X") + "伤害，最多" + S(40));
                case "thunder_15": return text.Replace("28+6X伤害，最多64", Formula("28+6X") + "伤害，最多" + S(64));
                case "blood_02": return text.Replace("造成34伤害", "造成 {damage} 伤害");
                case "blood_06": return text.Replace("吸血，最多2", "吸血，最多" + S(2));
                case "blood_07": return text.Replace("各12伤害", "各" + S(12) + "伤害")
                    .Replace("第二击+4，总计24或28", "第二击+" + (S(16) - S(12)) + "，总计" + 2 * S(12) + "或" + (S(12) + S(16)));
                case "blood_08": return text.Replace("反击8伤害", "反击" + S(8) + "伤害");
                case "blood_09": return text.Replace("使用1药物恢复7，绝境时恢复9", "使用1药物恢复" + S(7) + "，绝境时恢复" + S(9));
                case "blood_11": return text.Replace("造成10伤害", "造成 {damage} 伤害").Replace("改为14", "改为" + S(14));
                case "blood_12": return text.Replace("获得5格挡", "获得 {block} 格挡").Replace("改为8", "改为" + S(8));
                case "blood_13": return text.Replace("造成12伤害", "造成 {damage} 伤害").Replace("改为18", "改为" + S(18));
                case "blood_15": return text.Replace("攻击伤害+8", "攻击伤害+" + S(8));
                default: return text;
            }
        }
    }
}
