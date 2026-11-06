using System;

namespace ShatteredSail.Core.Gameplay
{
    // The manuscript owns these effects. Generate every rank from the same rule,
    // using the same printed constant and coefficient as actual resolution.
    internal static class CoreBoardingManuscriptDescriptions
    {
        internal static string ForKey(string key, CoreBattleCard card, int level)
        {
            int S(int value) => level <= 0 ? value : CoreCardUpgradeDesign.ScaleEffect(card, value, level);
            int L(int value) => S(CoreBoardingCardBalance.Linear(value));
            int G(int value) => S(CoreBoardingCardBalance.Guard(key, value));
            int Burn(bool improved = false) => CoreBoardingCardBalance.Burn(key, level, improved);
            string F(int constant, decimal coefficient, string variable = "X") =>
                CoreBattleCardLinearFormula.For(card, constant, coefficient, level).Describe(variable);
            switch (key)
            {
                case "basic_07": return "获得持续恢复：2；消耗 1 药物额外获得持续恢复：" + (CoreBoardingCardBalance.Sustain(key, level) - 2) + "。";
                case "basic_09": case "basic_10": return "获得 {energyRecovery} 点能量。";
                case "basic_13": return "延迟：造成 {damage} 点伤害。";
                case "fire_01": return "造成 {damage} 点伤害。";
                case "fire_02": return "造成 {damage} 点伤害；施加 " + Burn() + " 层燃烧。";
                case "fire_03": return "施加 " + Burn() + " 层燃烧。";
                case "fire_04": return "施加 " + Burn() + " 层燃烧；下个己方回合开始时，再施加 " + Burn() + " 层燃烧。";
                case "fire_05": return "恢复 2 点生命值；消耗 1 药物和 1 火药额外恢复 " + (L(10) - 2) + " 点生命值。";
                case "fire_06": return "焚烧 1 张其他手牌；造成 {damage} 点伤害，施加 " + Burn() + " 层燃烧；若焚烧的是攻击牌，则改为施加 " + Burn(true) + " 层燃烧。";
                case "fire_07": return "焚烧 X 张其他手牌（1～3 张）；施加 " + CoreBoardingCardBalance.BurnFuelBase(level) + "+" +
                    CoreBoardingCardBalance.BurnFuelCoefficient(level) + "X 层燃烧。";
                case "fire_08": return "焚烧 X 张其他手牌（1～3 张）；获得 " + F(4, 8) + " 点格挡。";
                case "fire_09": return "焚烧 1 张其他手牌，再抽 {draw} 张牌。";
                case "fire_10": return "焚烧本卡牌；本回合接下来两张需要火药的火焰牌，各减免 1 火药费用。";
                case "fire_11": return "对自身施加 3 层燃烧；造成 " + CoreBoardingCardBalance.SelfBurnDamageBase(level) + "+" +
                    CoreBoardingCardBalance.SelfBurnDamageCoefficient(level) + "×自身燃烧层数 点伤害。";
                case "fire_12": return "直到下个己方回合结束：对自身施加燃烧时，对敌方施加等量燃烧。";
                case "fire_13": return "对自身施加 2 层燃烧；获得 {block} 点格挡；格挡敌方攻击时，对敌方造成 自身燃烧层数 点反击伤害。";
                case "fire_14": return "对自身施加 2 层燃烧；获得 {energyRecovery} 点能量。";
                case "fire_15": return "将自己的燃烧效果转移到敌方。";
                case "arcane_01": return "造成 {damage} 点伤害；获得 " + CoreBoardingCardBalance.Mana(key, level) + " 点魔力。";
                case "arcane_02": return "获得 " + CoreBoardingCardBalance.Mana(key, level) + " 点魔力；获得 {block} 点格挡。";
                case "arcane_03": return "获得 " + CoreBoardingCardBalance.Mana(key, level) + " 点魔力。";
                case "arcane_04": return "造成 " + F(48, 6) + " 点伤害。";
                case "arcane_05": return "恢复 2 点生命值；消耗 1 药物和 4 点魔力额外恢复 " + (L(13) - 2) + " 点生命值。";
                case "arcane_06": return "造成 " + S(5) + " 点伤害 4+X 次。";
                case "arcane_07": return "移除 " + L(8) + " 点格挡，造成 {damage} 点伤害。";
                case "arcane_08": return "获得 {block} 点格挡，保留格挡到下个敌方回合结束。";
                case "arcane_09": return "查看牌库顶 5 张牌，选择 {draw} 张加入手牌，其余按原顺序放回牌库底。";
                case "arcane_10": return "获得 {energyRecovery} 点能量。";
                case "arcane_11": return "延迟：造成 " + F(72, 8) + " 点伤害。";
                case "arcane_12": return "选择 1 张其他手牌保留到下一回合，这张牌在下一回合首次打出时，基础能量费用降低 1 点。";
                case "arcane_13": return "下一张秘术攻击牌获得效果：延迟：造成其伤害的 " + S(50) + "% 的伤害。";
                case "arcane_14": return "选择弃牌堆中 1 张秘术攻击牌，将其加入手牌。";
                case "arcane_15": return "立即结算一个延迟效果。";
                case "curse_01": return "造成 {damage} 点伤害，施加 " + CoreBoardingCardBalance.Marks(key, level) + " 层咒印。";
                case "curse_02": return "施加 " + CoreBoardingCardBalance.Marks(key, level) + " 层咒印。";
                case "curse_03": return "获得 {block} 点格挡；施加 " + CoreBoardingCardBalance.Marks(key, level) + " 层咒印。";
                case "curse_04": return "直到下下个敌方回合结束，我方受到敌方伤害时，施加 4 层咒印。";
                case "curse_05": return "本回合接下来使用的两张攻击牌对敌方施加 2 层咒印。";
                case "curse_06": return "造成 " + F(12, 1, "当前咒印层数") + " 点伤害。";
                case "curse_07": return "直到下下个敌方回合结束，敌方造成的伤害降低 " + F(6, 1, "咒印层数") + " 点。";
                case "curse_08": return "移除 " + F(8, 2, "咒印层数") + " 点格挡；直到下下个敌方回合结束，敌方获得的格挡降低 " + F(4, 1, "咒印层数") + " 点。";
                case "curse_09": return "直到下下个敌方回合结束，敌方造成伤害前受到 " + F(0, 2, "咒印层数") + " 点咒术伤害。";
                case "curse_10": return "本场战斗中，咒印消灭阈值降低 " + S(CoreCurseRules.EntangleThresholdReductionPercent) + " 个百分点（可叠加）；降至 0% 时立即消灭敌方。";
                case "curse_11": return "敌方咒印层数 ≥ 2 才可以使用：造成 " + F(40, 8) + " 点咒术伤害；清除咒印效果。";
                case "curse_12": return "恢复 2 点生命值；敌方咒印层数 ≥ 4 时，消耗 1 药物额外恢复 " + (L(13) - 2) + " 点生命值。";
                case "curse_13": return "抽 {draw} 张牌。";
                case "curse_14": return "敌方咒印层数 ≥ 4 才可以使用：获得 {energyRecovery} 点能量。";
                case "curse_15": return "敌方咒印层数 ≥ 4 才可以使用：获得 {block} 点格挡。";
                case "tide_01": case "tide_02": return "获得 {block} 点蓄潮格挡。";
                case "tide_03": return "获得 {block} 点蓄潮格挡；抽 {draw} 张牌。";
                case "tide_04": return "移除敌方 " + L(4) + " 点格挡，造成 {damage} 点伤害。";
                case "tide_05": return "本场战斗中，将当前全部格挡及后续获得的格挡转换为等量蓄潮格挡。";
                case "tide_06": return "造成 " + F(12, 1) + " 点伤害。";
                case "tide_07": return "造成 " + F(32, 1) + " 点伤害。";
                case "tide_08": return "造成 " + F(60, 1.5m) + " 点伤害。";
                case "tide_09": return "直到下下个敌方回合结束，敌方在第一次攻击后受到 " + L(16) + " 点伤害。";
                case "tide_10": return "造成 {damage} 点伤害；直到下下个敌方回合结束，敌方造成的伤害减少 " + L(3) + " 点。";
                case "tide_11": return "恢复 2 点生命值；消耗 1 药物和 12 点潮势额外恢复 " + (L(10) - 2) + " 点生命值。";
                case "tide_12": return "获得持续恢复：2；消耗 1 药物和 16 点潮势额外获得持续恢复：" + (CoreBoardingCardBalance.Sustain(key, level) - 2) + "。";
                case "tide_13": return "抽 {draw} 张牌。";
                case "tide_14": return "获得 {energyRecovery} 点能量；抽 {draw} 张牌。";
                case "tide_15": return "获得 {block} 点格挡。";
                case "thunder_01": return "造成 {damage} 点伤害。";
                case "thunder_02": return level <= 0 ? "造成 {damage} 点伤害，出牌前每有一层连锁，本次伤害提高 2 点。"
                    : "造成 " + F(16, 2, "出牌前连锁层数") + " 点伤害。";
                case "thunder_03": return "造成 {damage} 点伤害；抽 {draw} 张牌。";
                case "thunder_04": return "移除 " + L(6) + " 点格挡，造成 {damage} 点伤害。";
                case "thunder_05": return level <= 0 ? "延迟：造成 {damage} 点伤害，出牌前每有一层连锁，本次伤害提高 2 点。"
                    : "延迟：造成 " + F(28, 2, "出牌前连锁层数") + " 点伤害。";
                case "thunder_06": return "抽 {draw} 张牌；本回合下一张雷霆攻击的伤害提高 " + L(8) + " 点。";
                case "thunder_07": return "连锁 2：抽 {draw} 张牌。";
                case "thunder_08": return "抽 {draw} 张牌；连锁 3：额外抽 1 张牌。";
                case "thunder_09": return "连锁 2：获得 {energyRecovery} 点能量。";
                case "thunder_10": return "本回合接下来 3 张主动雷霆攻击额外造成 " + L(4) + " 点伤害。";
                case "thunder_11": return level <= 0 ? "造成 6 点伤害 2 次，出牌前每有一层连锁，每次伤害提高 3 点。"
                    : "造成 " + F(6, 3, "出牌前连锁层数") + " 点伤害 2 次。";
                case "thunder_12": return "造成 {damage} 点伤害；连锁 2：敌方下一次攻击伤害降低 " + L(10) + " 点。";
                case "thunder_13": return "造成 " + F(40, 7) + " 点伤害。";
                case "thunder_14": return "造成 " + F(32, 6) + " 点无视格挡的伤害。";
                case "thunder_15": return "造成 " + F(56, 8) + " 点伤害，连锁 10：伤害翻倍，连锁 20：伤害翻倍。";
                case "blood_01": case "blood_02": return "造成 {damage} 点伤害。";
                case "blood_03": return "获得 {block} 点格挡。";
                case "blood_04": return "抽 {draw} 张牌。";
                case "blood_05": return "获得 {energyRecovery} 点能量。";
                case "blood_06": return "造成 {damage} 点伤害；吸血。";
                case "blood_07": return "造成 " + L(12) + " 点伤害 2 次；若第一击使敌方失去生命值，第二击伤害提高 " + (L(16) - L(12)) + " 点。";
                case "blood_08": return "直到下下个敌方回合结束，敌方对我方造成伤害时，对其造成 " + L(8) + " 点伤害。";
                case "blood_09":
                    bool doctor = card?.Id == "card_special_crew_broken_anchor_doctor_hector";
                    int desperateBase = doctor ? 4 : 5;
                    return "恢复 2 点生命值，绝境：改为恢复 " + desperateBase + " 点生命值；消耗 1 药物额外恢复 " +
                        (L(doctor ? 7 : 6) - 2) + " 点生命值，绝境：改为额外恢复 " +
                        (L(doctor ? 16 : 13) - desperateBase) + " 点生命值。";
                case "blood_10": return "造成 {damage} 点伤害；若使敌方失去生命值，则抽 {draw} 张牌。";
                case "blood_11": return "造成 {damage} 点伤害；绝境：额外造成 " + (L(17) - L(7)) + " 点伤害。";
                case "blood_12": return "获得 {block} 点格挡；绝境：改为获得 " + G(12) + " 点格挡。";
                case "blood_13": return "延迟：造成 {damage} 点伤害；绝境：改为造成 " + L(22) + " 点伤害。";
                case "blood_14": return "绝境时才可使用；获得 {energyRecovery} 点能量，抽 {draw} 张牌，使用后移出本场战斗。";
                case "blood_15": return "绝境时才可使用：直到下个我方回合结束，攻击牌额外造成 " + L(8) + " 点伤害。";
                default: return null;
            }
        }
    }
}
