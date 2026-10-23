using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    // The manuscript owns these effects. Generate every rank from the same rule,
    // retaining whole-formula rounding rather than rounding independent terms.
    internal static class CoreBoardingManuscriptDescriptions
    {
        internal static string ForKey(string key, CoreBattleCard card, int level)
        {
            int S(int value) => level <= 0 ? value : CoreCardUpgradeDesign.ScaleEffect(card, value, level);
            string F(string expression) => level <= 0 ? expression : "(" + expression + ")×" +
                CoreCardUpgradeDesign.EffectMultiplier(card, level).ToString(CultureInfo.InvariantCulture) + "（结果四舍五入）";
            switch (key)
            {
                case "basic_07": return "获得持续恢复：2；消耗 1 药物额外获得持续恢复：" + (S(5) - 2) + "。";
                case "basic_09": case "basic_10": return "获得 {energyRecovery} 点能量。";
                case "basic_13": return "延迟：造成 {damage} 点伤害。";
                case "fire_01": return "造成 {damage} 点伤害。";
                case "fire_02": return "造成 {damage} 点伤害；施加 " + S(4) + " 层灼烧。";
                case "fire_03": return "施加 " + S(8) + " 层灼烧。";
                case "fire_04": return "施加 " + S(6) + " 层灼烧；己方回合开始时，再施加 " + S(6) + " 层灼烧。";
                case "fire_05": return "恢复 2 点生命值；消耗 1 药物和 1 火药额外恢复 " + (S(10) - 2) + " 点生命值。";
                case "fire_06": return "焚烧 1 张其他手牌；造成 {damage} 点伤害，施加 " + S(3) + " 层灼烧；若焚烧的是攻击牌，则改为施加 " + S(5) + " 层灼烧。";
                case "fire_07": return "焚烧 X 张其他手牌（1～3 张）；施加 " + F("4+3X") + " 层灼烧。";
                case "fire_08": return "焚烧 X 张其他手牌（1～3 张）；获得 " + F("2+4X") + " 点格挡。";
                case "fire_09": return "焚烧 1 张其他手牌，再抽 {draw} 张牌。";
                case "fire_10": return "焚烧本卡牌；本回合接下来两张需要火药的火焰牌，各减免 1 火药费用。";
                case "fire_11": return "施加 3 层自燃；造成 " + F("30+2×出牌前自燃层数") + " 点伤害。";
                case "fire_12": return "直到下个己方回合结束：施加自燃时，对敌方施加等量灼烧。";
                case "fire_13": return "施加 2 层自燃；获得 {block} 点格挡；格挡敌方攻击时，对敌方造成 当前自燃层数×2 点反击伤害。";
                case "fire_14": return "施加自燃：2；获得 {energyRecovery} 点能量。";
                case "fire_15": return "转移至多 4 层自燃。";
                case "arcane_01": return "造成 {damage} 点伤害；获得 1 点魔力。";
                case "arcane_02": return "获得 1 点魔力；获得 {block} 点格挡。";
                case "arcane_03": return "获得 3 点魔力。";
                case "arcane_04": return "造成 " + F("24+6X") + " 点伤害。";
                case "arcane_05": return "恢复 2 点生命值；消耗 1 药物和 2 点魔力额外恢复 " + (S(13) - 2) + " 点生命值。";
                case "arcane_06": return "造成 " + S(5) + " 点伤害 2+X 次。";
                case "arcane_07": return "移除最多 " + S(8) + " 点格挡，造成 {damage} 点伤害。";
                case "arcane_08": return "获得 {block} 点格挡，保留格挡到下个敌方回合结束。";
                case "arcane_09": return "查看牌库顶 5 张牌，选择 {draw} 张加入手牌，其余按原顺序放回牌库底。";
                case "arcane_10": return "获得 {energyRecovery} 点能量。";
                case "arcane_11": return "延迟：造成 " + F("36+8X") + " 点伤害。";
                case "arcane_12": return "选择 1 张其他手牌保留到下一回合，这张牌在下一回合首次打出时，基础能量费用降低 1 点。";
                case "arcane_13": return level <= 0 ? "下一张秘术攻击牌获得效果：延迟：造成其伤害的 50%。"
                    : "下一张秘术攻击牌获得效果：延迟：造成 " + F("向下取整的该牌伤害×50%") + " 点伤害。";
                case "arcane_14": return "选择弃牌堆中 1 张秘术攻击牌，将其加入手牌。";
                case "arcane_15": return "立即结算一个延迟效果。";
                case "curse_01": return "造成 {damage} 点伤害，施加 1 层咒印。";
                case "curse_02": return "施加 3 层咒印。";
                case "curse_03": return "获得 {block} 点格挡；施加 1 层咒印。";
                case "curse_04": return "直到下下个敌方回合结束，我方受到敌方伤害时，施加 2 层咒印。";
                case "curse_05": return "本回合接下来使用的两张攻击牌对敌方施加 1 层咒印。";
                case "curse_06": return "造成 " + F("6+当前咒印层数") + " 点伤害。";
                case "curse_07": return "直到下下个敌方回合结束，敌方造成的伤害降低 " + F("3+咒印层数") + " 点。";
                case "curse_08": return "移除 " + F("4+2×咒印层数") + " 点格挡；直到下下个敌方回合结束，敌方获得的格挡降低 " + F("2+咒印层数") + " 点。";
                case "curse_09": return "直到下下个敌方回合结束，敌方造成伤害前受到 " + F("2×咒印层数") + " 点咒术伤害。";
                case "curse_10": return "咒印伤害额外结算 1 次。";
                case "curse_11": return "目标至少有 1 层咒印时才可使用；造成 " + F("20+8X") + " 点咒术伤害。";
                case "curse_12": return "恢复 2 点生命值；消耗 1 药物和 2 层咒印额外恢复 " + (S(13) - 2) + " 点生命值。";
                case "curse_13": return "抽 {draw} 张牌。";
                case "curse_14": return "获得 {energyRecovery} 点能量。";
                case "curse_15": return "获得 {block} 点格挡。";
                case "tide_01": case "tide_02": return "获得 {block} 点蓄潮格挡。";
                case "tide_03": return "获得 {block} 点蓄潮格挡；抽 {draw} 张牌。";
                case "tide_04": return "移除敌方 " + S(4) + " 点格挡，造成 {damage} 点伤害。";
                case "tide_05": return "获得 " + S(6) + " 点潮势。";
                case "tide_06": return "造成 " + F("6+X") + " 点伤害。";
                case "tide_07": return "造成 " + F("16+X") + " 点伤害。";
                case "tide_08": return "造成 " + F("30+向下取整的X×150%") + " 点伤害。";
                case "tide_09": return "直到下下个敌方回合结束，敌方在第一次攻击后受到 " + S(16) + " 点伤害。";
                case "tide_10": return "造成 {damage} 点伤害；直到下下个敌方回合结束，敌方造成的伤害减少 " + S(3) + " 点。";
                case "tide_11": return "恢复 2 点生命值；消耗 1 药物和 6 点潮势额外恢复 " + (S(10) - 2) + " 点生命值。";
                case "tide_12": return "获得持续恢复：2；消耗 1 药物和 8 点潮势额外获得持续恢复：" + (S(8) - 2) + "。";
                case "tide_13": return "抽 {draw} 张牌。";
                case "tide_14": return "获得 {energyRecovery} 点能量；抽 {draw} 张牌。";
                case "tide_15": return "获得 {block} 点格挡。";
                default: return null;
            }
        }
    }
}
