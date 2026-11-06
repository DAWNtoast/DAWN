from pathlib import Path
import re, json

ROOT=Path('D:/Game')
SRC=ROOT/'outputs/boarding-field-balance-20261024/Inputs/CoreSnapshot'
OUT=ROOT/'outputs/boarding-field-balance-20261024/experiments/card-a'
OUT.mkdir(parents=True,exist_ok=True)
files={}
def read(name):
    if name not in files: files[name]=(SRC/name).read_text(encoding='utf-8-sig')
    return files[name]
def rep(name,old,new):
    s=read(name)
    if old not in s: raise ValueError(f'missing {name}: {old}')
    files[name]=s.replace(old,new)
def regex(name,pattern,replacement):
    s=read(name); files[name]=re.sub(pattern,replacement,s)

files['CoreBoardingCardBalance.cs']='''using System;

namespace ShatteredSail.Core.Gameplay
{
    // Boarding-only authored units. Never apply this at damage settlement:
    // terrain, actual absorbed tide, echoes and lifesteal already use final units.
    public static class CoreBoardingCardBalance
    {
        public static int Linear(int basis) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, basis) * 2);
        public static int Guard(string key, int basis)
        {
            switch (key)
            {
                case "basic_04": case "tide_01": return basis == 4 ? 10 : Linear(basis);
                case "basic_05": return basis == 10 ? 24 : Linear(basis);
                case "arcane_02": return basis == 3 ? 8 : Linear(basis);
                case "arcane_08": return basis == 5 ? 14 : Linear(basis);
                default: return Linear(basis);
            }
        }
        private static int Rank(int rank, params int[] values) => values[Math.Max(0, Math.Min(values.Length - 1, rank))];
        public static int Burn(string key, int rank, bool improved = false)
        {
            switch (key)
            {
                case "fire_02": return Rank(rank, 3, 4, 5, 6);
                case "fire_03": return Rank(rank, 6, 7, 9);
                case "fire_04": return Rank(rank, 4, 5, 6, 7);
                case "fire_06": return improved ? Rank(rank, 4, 5, 6, 7) : Rank(rank, 3, 4, 5, 6);
                default: return 0;
            }
        }
        public static int BurnFuel(int amount, int rank) => 3 + 2 * amount + Math.Max(0, Math.Min(2, rank));
        public static int Mana(string key, int rank)
        {
            switch (key)
            {
                case "arcane_01": return Rank(rank, 2, 2, 3, 4);
                case "arcane_02": return Rank(rank, 2, 3, 4);
                case "arcane_03": return Rank(rank, 6, 7, 9);
                default: return 0;
            }
        }
        public static int Marks(string key, int rank)
        {
            switch (key)
            {
                case "curse_01": return Rank(rank, 2, 2, 3, 4);
                case "curse_02": return Rank(rank, 6, 8, 10);
                case "curse_03": return Rank(rank, 2, 3, 4);
                default: return 0;
            }
        }
        public static int Sustain(string key, int rank) => key == "tide_12"
            ? Rank(rank, 11, 13, 15, 18) : Rank(rank, 7, 8, 10, 12);
        public static int Repair(string key, int basis) => key == "basic_07" || key == "tide_12"
            ? Sustain(key, 0) : Linear(basis);
        public static int Cost(string key)
        {
            switch (key)
            {
                case "fire_14": return 4;
                case "arcane_07": case "arcane_08": case "arcane_09": case "arcane_12":
                case "arcane_13": case "arcane_14": case "curse_13": return 2;
                case "arcane_05": case "arcane_10": case "arcane_15":
                case "tide_10": case "tide_15": return 4;
                case "tide_09": return 8;
                case "tide_12": case "tide_13": case "tide_14": return 16;
                case "tide_11": return 12;
                case "blood_01": case "blood_03": case "blood_04": case "blood_05": return 6;
                case "blood_02": return 8;
                default: return 0;
            }
        }
    }
}
'''
name='CoreBoardingCardCatalog.cs'
rep(name,'            string school = key.Substring', '''            damage = CoreBoardingCardBalance.Linear(damage);
            block = CoreBoardingCardBalance.Guard(key, block);
            repair = CoreBoardingCardBalance.Repair(key, repair);
            string school = key.Substring''')

name='CoreBoardingBattleExecution.cs'
rep(name,'        private bool BOnceUsed', '''        private int BLinear(CoreBattleCard card, int value) => BValue(card, CoreBoardingCardBalance.Linear(value));
        private int BGuardValue(CoreBattleCard card, int value) => BValue(card, CoreBoardingCardBalance.Guard(card?.BoardingSpec?.Key, value));
        private bool BOnceUsed''')
regex(name,r'BDeal\(BValue\(card, (8|20|35|4)\)\)',r'BDeal(BLinear(card, \1))')
regex(name,r'BGuard\(BValue\(card, (4|10|2)\)\)',r'BGuard(BGuardValue(card, \1))')
rep(name,'BHeal(BValue(card, 6))','BHeal(BLinear(card, 6))')
rep(name,'BQueueDamage(BValue(card, 12))','BQueueDamage(BLinear(card, 12))')
rep(name,'heal = BValue(card, 5);','heal = CoreBoardingCardBalance.Sustain("basic_07", card.UpgradeLevel);')
rep(name,'SchoolProduct(BStackTotal(BState.SelfBurn), 2)','BStackTotal(BState.SelfBurn)')

name='CoreBoardingFireArcaneThunder.cs'
rep(name,'SchoolPlayerHealth <= 2','SchoolPlayerHealth <= CoreBoardingCardBalance.Cost(key)')
rep(name,'支付2生命后须至少剩1生命','支付4生命后须至少剩1生命')
rep(name,'mana < 2','mana < CoreBoardingCardBalance.Cost(key)')
rep(name,'mana < 1','mana < CoreBoardingCardBalance.Cost(key)')
rep(name,'有药治疗固定需要2魔力','有药治疗固定需要4魔力')
rep(name,'需要2魔力','需要4魔力')
rep(name,'需要1魔力','需要2魔力')
rep(name,'BPayLifeCost(2)','BPayLifeCost(CoreBoardingCardBalance.Cost("fire_14"))')
regex(name,r'BDeal\(BValue\(card, (28|8|6|10|3|4|7|5)\)\)',r'BDeal(BLinear(card, \1))')
regex(name,r'BHeal\(BValue\(card, (10|13)\)\)',r'BHeal(BLinear(card, \1))')
rep(name,'BAddEnemyBurn(BValue(card, 4))','BAddEnemyBurn(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel))')
rep(name,'BAddEnemyBurn(BValue(card, 8))','BAddEnemyBurn(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel))')
rep(name,'BAddEnemyBurn(BValue(card, 6))','BAddEnemyBurn(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel))')
rep(name,'new CoreBoardingTimedStacks(BValue(card, 6), 1)','new CoreBoardingTimedStacks(CoreBoardingCardBalance.Burn(key, card.UpgradeLevel), 1)')
rep(name,'BValue(card, BState.BurnedAttackCount > 0 ? 5 : 3)','CoreBoardingCardBalance.Burn(key, card.UpgradeLevel, BState.BurnedAttackCount > 0)')
rep(name,'BValue(card, 4 + 3 * amount)','CoreBoardingCardBalance.BurnFuel(amount, card.UpgradeLevel)')
rep(name,'BGuard(BValue(card, 2 + 4 * amount))','BGuard(BLinear(card, 2 + 4 * amount))')
rep(name,'SchoolSum(30, SchoolProduct(total, 2))','SchoolSum(60, SchoolProduct(total, 2))')
rep(name,'BGuard(BValue(card, 8))','BGuard(BGuardValue(card, 8))')
rep(name,'BGuard(BValue(card, 3))','BGuard(BGuardValue(card, 3))')
rep(name,'FATGainMana(1)','FATGainMana(CoreBoardingCardBalance.Mana(key, card.UpgradeLevel))')
rep(name,'FATGainMana(3)','FATGainMana(CoreBoardingCardBalance.Mana(key, card.UpgradeLevel))')
rep(name,'SchoolSum(24, SchoolProduct(6, mana))','SchoolSum(48, SchoolProduct(6, mana))')
rep(name,'SchoolSum(36, SchoolProduct(8, mana))','SchoolSum(72, SchoolProduct(8, mana))')
rep(name,'SchoolSum(2, mana)','SchoolSum(4, mana)')
rep(name,'BSpendMana(1)','BSpendMana(CoreBoardingCardBalance.Cost(key))')
rep(name,'BSpendMana(2)','BSpendMana(CoreBoardingCardBalance.Cost(key))')
rep(name,'Battle.EnemyBlock - BValue(card, 8)','Battle.EnemyBlock - BLinear(card, 8)')
rep(name,'guard = BValue(card, 5)','guard = BGuardValue(card, 5)')
rep(name,'SchoolSum(8, chain)','SchoolSum(16, SchoolProduct(2, chain))')
rep(name,'SchoolSum(14, chain)','SchoolSum(28, SchoolProduct(2, chain))')
rep(name,'Battle.EnemyBlock - BValue(card, 6)','Battle.EnemyBlock - BLinear(card, 6)')
rep(name,'BState.ThunderReady = BValue(card, 8)','BState.ThunderReady = BLinear(card, 8)')
rep(name,'BState.ThunderResidualDamage = BValue(card, 4)','BState.ThunderResidualDamage = BLinear(card, 4)')
rep(name,'SchoolSum(3, SchoolProduct(2, chain))','SchoolSum(6, SchoolProduct(3, chain))')
rep(name,'BValue(card, 10)','BLinear(card, 10)')
rep(name,'SchoolSum(20, SchoolProduct(5, chain))','SchoolSum(40, SchoolProduct(7, chain))')
rep(name,'SchoolSum(16, SchoolProduct(4, chain))','SchoolSum(32, SchoolProduct(6, chain))')
rep(name,'SchoolSum(28, SchoolProduct(6, chain))','SchoolSum(56, SchoolProduct(8, chain))')

name='CoreBoardingCurseTideBlood.cs'
# All fixed costs are in final boarding units; generated resources are handled separately.
for resource in ['mana']:
    pass
rep(name,'BState.CurseMarks < 1','BState.CurseMarks < 2')
rep(name,'至少需要1层咒印','至少需要2层咒印')
rep(name,'需要1层咒印','需要2层咒印')
# The existing <2 gates become four; the newly replaced min-one gate stays two.
rep(name,'case "curse_14":\n                    if (BState.CurseMarks < 2)','case "curse_14":\n                    if (BState.CurseMarks < 4)')
rep(name,'case "curse_15":\n                    if (BState.CurseMarks < 2)','case "curse_15":\n                    if (BState.CurseMarks < 4)')
rep(name,'需要2层咒印";\n                    break;','需要4层咒印";\n                    break;')
regex(name,r'BState.TidePower < (4|2|8)',r'BState.TidePower < CoreBoardingCardBalance.Cost(key)')
rep(name,'需要4点潮势','需要8点潮势')
rep(name,'需要2点潮势','需要4点潮势')
rep(name,'需要8点潮势','需要16点潮势')
# Restore the ambush label after ordered replacement.
rep(name,'case "tide_09":\n                    if (BState.TidePower < CoreBoardingCardBalance.Cost(key)) return "需要16点潮势"','case "tide_09":\n                    if (BState.TidePower < CoreBoardingCardBalance.Cost(key)) return "需要8点潮势"')
rep(name,'SchoolPlayerHealth <= 3','SchoolPlayerHealth <= CoreBoardingCardBalance.Cost(key)')
rep(name,'SchoolPlayerHealth <= 4','SchoolPlayerHealth <= CoreBoardingCardBalance.Cost(key)')
rep(name,'支付3生命后须至少剩1生命','支付6生命后须至少剩1生命')
rep(name,'支付4生命后须至少剩1生命','支付8生命后须至少剩1生命')
rep(name,'BPayLifeCost(4)','BPayLifeCost(CoreBoardingCardBalance.Cost(key))')
rep(name,'BPayLifeCost(3)','BPayLifeCost(CoreBoardingCardBalance.Cost(key))')
regex(name,r'BDeal\(BValue\(card, (8|6|16|34|9|12)\)\)',r'BDeal(BLinear(card, \1))')
regex(name,r'BGuard\(BValue\(card, (3|10|8|11)\)\)',r'BGuard(BGuardValue(card, \1))')
regex(name,r'CTBAddTideGuard\(BValue\(card, (4|15|2)\)\)',r'CTBAddTideGuard(BGuardValue(card, \1))')
rep(name,'CTBAddMarks(1); break;','CTBAddMarks(CoreBoardingCardBalance.Marks(key, card.UpgradeLevel)); break;')
rep(name,'CTBAddMarks(3); break;','CTBAddMarks(CoreBoardingCardBalance.Marks(key, card.UpgradeLevel)); break;')
rep(name,'SchoolSum(6, BState.CurseMarks)','SchoolSum(12, BState.CurseMarks)')
rep(name,'SchoolSum(4, SchoolProduct(2, BState.CurseMarks))','SchoolSum(8, SchoolProduct(2, BState.CurseMarks))')
rep(name,'SchoolSum(20, SchoolProduct(8, paid))','SchoolSum(40, SchoolProduct(8, paid))')
rep(name,'BState.CurseMarks >= 2','BState.CurseMarks >= 4')
rep(name,'BHeal(BValue(card, 13))','BHeal(BLinear(card, 13))')
rep(name,'CTBConsumeMarks(1)','CTBConsumeMarks(CoreBoardingCardBalance.Cost(key))')
rep(name,'Battle.EnemyBlock - BValue(card, 4)','Battle.EnemyBlock - BLinear(card, 4)')
rep(name,'SchoolSum(6, paid)','SchoolSum(12, paid)')
rep(name,'SchoolSum(16, paid)','SchoolSum(32, paid)')
rep(name,'SchoolSum(30, (int)','SchoolSum(60, (int)')
regex(name,r'CTBConsumeTide\((4|2|6|8)\)',r'CTBConsumeTide(CoreBoardingCardBalance.Cost(key))')
rep(name,'BState.TidePower >= 6','BState.TidePower >= CoreBoardingCardBalance.Cost(key)')
rep(name,'BState.TidePower >= 8','BState.TidePower >= CoreBoardingCardBalance.Cost(key)')
rep(name,'BHeal(BValue(card, 10))','BHeal(BLinear(card, 10))')
rep(name,'paid = BValue(card, 8)','paid = CoreBoardingCardBalance.Sustain(key, card.UpgradeLevel)')
rep(name,'BDeal(BValue(card, actual > 0 ? 16 : 12))','BDeal(BLinear(card, actual > 0 ? 16 : 12))')
rep(name,'BHeal(BValue(card, paid))','BHeal(BLinear(card, paid))')
rep(name,'BDeal(BValue(card, CTBDesperate ? 17 : 7))','BDeal(BLinear(card, CTBDesperate ? 17 : 7))')
rep(name,'BGuard(BValue(card, CTBDesperate ? 12 : 5))','BGuard(BGuardValue(card, CTBDesperate ? 12 : 5))')
rep(name,'BQueueDamage(BValue(card, CTBDesperate ? 22 : 12))','BQueueDamage(BLinear(card, CTBDesperate ? 22 : 12))')
rep(name,'BValue(BState.BloodDebtCard, 8)','BLinear(BState.BloodDebtCard, 8)')
rep(name,'BValue(BState.BloodOathCard, 8)','BLinear(BState.BloodOathCard, 8)')
rep(name,'CTBAddMarks(1);\n        }','CTBAddMarks(2);\n        }')

name='CoreBoardingManuscriptEffects.cs'
rep(name,'SchoolProduct(BState.CurseContracts.Count, 2) : BState.CurseContractTurns > 0 ? 2','SchoolProduct(BState.CurseContracts.Count, 4) : BState.CurseContractTurns > 0 ? 4')
rep(name,'SchoolSum(2, marks)','SchoolSum(4, marks)')
rep(name,'SchoolSum(3, marks)','SchoolSum(6, marks)')
rep(name,'BWindowAmount(BState.TideWeaknesses, 3)','BWindowAmount(BState.TideWeaknesses, 6)')
rep(name,'BValue(BState.TideWeakCard, 3)','BValue(BState.TideWeakCard, 6)')
rep(name,'BWindowAmount(BState.TideAmbushes, 16)','BWindowAmount(BState.TideAmbushes, 32)')
rep(name,'BValue(BState.TideAmbushCard, 16)','BValue(BState.TideAmbushCard, 32)')

name='CoreBoardingEnemySimulation.cs'
rep(name,'BValue(BState.BloodDebtCard, 8)','BLinear(BState.BloodDebtCard, 8)')
rep(name,'SchoolProduct(c.FireRetaliations, SchoolProduct(c.State.PlayerBurnDamage, 2))','SchoolProduct(c.FireRetaliations, c.State.PlayerBurnDamage)')

name='CoreBoardingCardQuotes.cs'
for old,new in [('SchoolSum(30,','SchoolSum(60,'),('SchoolSum(24,','SchoolSum(48,'),('SchoolSum(36,','SchoolSum(72,'),('SchoolSum(2, mana)','SchoolSum(4, mana)'),('SchoolSum(6, marks)','SchoolSum(12, marks)'),('SchoolSum(20, SchoolProduct(8, marks))','SchoolSum(40, SchoolProduct(8, marks))'),('SchoolSum(6, tide)','SchoolSum(12, tide)'),('SchoolSum(16, tide)','SchoolSum(32, tide)'),('SchoolSum(8, chain)','SchoolSum(16, SchoolProduct(2, chain))'),('SchoolSum(14, chain)','SchoolSum(28, SchoolProduct(2, chain))'),('SchoolSum(3, SchoolProduct(2, chain))','SchoolSum(6, SchoolProduct(3, chain))'),('SchoolSum(20, SchoolProduct(5, chain))','SchoolSum(40, SchoolProduct(7, chain))'),('SchoolSum(16, SchoolProduct(4, chain))','SchoolSum(32, SchoolProduct(6, chain))')]: rep(name,old,new)
rep(name,'BValue(card, 12)','BLinear(card, 12)')
rep(name,'damage = SchoolSum(BLinear(card, 12), BValue(card,','damage = SchoolSum(BLinear(card, 12), BLinear(card,')
rep(name,'BValue(card, CTBDesperate ? 17 : 7)','BLinear(card, CTBDesperate ? 17 : 7)')
rep(name,'BValue(card, CTBDesperate ? 22 : 12)','BLinear(card, CTBDesperate ? 22 : 12)')
rep(name,'BValue(BState.BloodOathCard, 8)','BLinear(BState.BloodOathCard, 8)')

name='CoreBoardingCardChoices.cs'
rep(name,'case "fire_15": return BStackTotal(BState.SelfBurn);','case "fire_15": return BStackTotal(BState.SelfBurn);\n                case "fire_14": return CoreBoardingCardBalance.Cost(key);')
rep(name,'return Cargo.Medicine > 0 ? 2 : 0;','return Cargo.Medicine > 0 ? CoreBoardingCardBalance.Cost(key) : 0;')
rep(name,'return Cargo.Medicine > 0 ? 6 : 0;','return Cargo.Medicine > 0 ? CoreBoardingCardBalance.Cost(key) : 0;')
rep(name,'return Cargo.Medicine > 0 ? 8 : 0;','return Cargo.Medicine > 0 ? CoreBoardingCardBalance.Cost(key) : 0;')
regex(name,r'(case "(?:arcane_[^\n]+|curse_13|tide_09|tide_10|tide_13|tide_15|blood_01|blood_02)"[^\n]*return )\d+;',r'\1CoreBoardingCardBalance.Cost(key);')

name='CoreCardUpgradeDesign.Standard.cs'
rep(name,'arcane_03|0|100|0|0||固定法力生成','arcane_03|2|100|0|0||法力生成6/7/9')
rep(name,'curse_02|0|100|0|0||固定咒印施加','curse_02|2|100|0|0||咒印生成6/8/10')
rep(name,'符刃直伤成长，法力固定','符刃直伤与法力2/2/3/4共同成长')
rep(name,'刻印防护成长，法力固定','刻印防护与法力2/3/4共同成长')
rep(name,'咒刃直伤，施咒数固定','咒刃直伤与咒印2/2/3/4共同成长')
rep(name,'草偶格挡成长','草偶格挡与咒印2/3/4共同成长')

name='CoreBattleCardEffects.cs'
rep(name,'BValue(card, 12) - card.Block','BGuardValue(card, 12) - card.Block')
rep(name,'BValue(card, desperate ? (doctor ? 16 : 13) : (doctor ? 7 : 6))','BLinear(card, desperate ? (doctor ? 16 : 13) : (doctor ? 7 : 6))')
rep(name,'BState.TidePower < 6','BState.TidePower < CoreBoardingCardBalance.Cost("tide_11")')
rep(name,'BState.TidePower < 8','BState.TidePower < CoreBoardingCardBalance.Cost("tide_12")')

name='CoreSpecialCardRedesign.cs'
rep(name,'? 7 : b.Repair','? CoreBoardingCardBalance.Linear(7) : b.Repair')

# Published rank-zero fields for continuous recovery are layer counts, not linear HP.
name='CoreBattleCardUpgrades.cs'
rep(name,'CoreCardUpgradeDesign.ScaleRepair(original, original.Repair, level),','original.BoardingSpec?.Key == "basic_07" || original.BoardingSpec?.Key == "tide_12"\n                    ? CoreBoardingCardBalance.Sustain(original.BoardingSpec.Key, level)\n                    : CoreCardUpgradeDesign.ScaleRepair(original, original.Repair, level),')
name='CoreBoardingCardCatalog.cs'
rep(name,'CoreCardUpgradeDesign.ScaleRepair(basis, design.Repair, level),','design.BoardingSpec.Key == "basic_07" || design.BoardingSpec.Key == "tide_12"\n                    ? CoreBoardingCardBalance.Sustain(design.BoardingSpec.Key, level)\n                    : CoreCardUpgradeDesign.ScaleRepair(basis, design.Repair, level),')

name='CoreBoardingManuscriptDescriptions.cs'
read(name)
# Template helpers distinguish resource layers from direct numerical HP effects.
rep(name,'            string F(string expression)', '''            int L(int value) => S(CoreBoardingCardBalance.Linear(value));
            int G(int value) => S(CoreBoardingCardBalance.Guard(key, value));
            int Burn(bool improved = false) => CoreBoardingCardBalance.Burn(key, level, improved);
            string F(string expression)''')
rep(name,'(S(5) - 2)','(CoreBoardingCardBalance.Sustain(key, level) - 2)')
rep(name,'" + S(4) + " 层燃烧','" + Burn() + " 层燃烧')
rep(name,'" + S(8) + " 层燃烧','" + Burn() + " 层燃烧')
rep(name,'" + S(6) + " 层燃烧','" + Burn() + " 层燃烧')
rep(name,'" + S(3) + " 层燃烧','" + Burn() + " 层燃烧')
rep(name,'" + S(5) + " 层燃烧','" + Burn(true) + " 层燃烧')
rep(name,'F("4+3X")','(level <= 0 ? "3+2X" : (3 + Math.Min(2, level)) + "+2X")')
rep(name,'F("2+4X")','F("4+8X")')
rep(name,'30+2×自身燃烧层数','60+2×自身燃烧层数')
rep(name,'2×自身燃烧层数 点反击伤害','自身燃烧层数 点反击伤害')
rep(name,'获得 1 点魔力。','获得 " + CoreBoardingCardBalance.Mana(key, level) + " 点魔力。')
rep(name,'获得 1 点魔力；','获得 " + CoreBoardingCardBalance.Mana(key, level) + " 点魔力；')
rep(name,'获得 3 点魔力。','获得 " + CoreBoardingCardBalance.Mana(key, level) + " 点魔力。')
rep(name,'24+6X','48+6X');rep(name,'36+8X','72+8X')
rep(name,'点伤害 2+X 次','点伤害 4+X 次')
rep(name,'S(8) + " 点格挡，造成','L(8) + " 点格挡，造成')
rep(name,'施加 1 层咒印。','施加 " + CoreBoardingCardBalance.Marks(key, level) + " 层咒印。')
rep(name,'施加 3 层咒印。','施加 " + CoreBoardingCardBalance.Marks(key, level) + " 层咒印。')
rep(name,'施加 2 层咒印。','施加 4 层咒印。')
# Link marks are a fixed two rather than the direct generation table.
rep(name,'case "curse_05": return "本回合接下来使用的两张攻击牌对敌方施加 " + CoreBoardingCardBalance.Marks(key, level) + " 层咒印。";','case "curse_05": return "本回合接下来使用的两张攻击牌对敌方施加 2 层咒印。";')
rep(name,'6+当前咒印层数','12+当前咒印层数');rep(name,'3+咒印层数','6+咒印层数')
rep(name,'4+2×咒印层数','8+2×咒印层数');rep(name,'2+咒印层数','4+咒印层数')
rep(name,'层数 ≥ 1','层数 ≥ 2');rep(name,'20+8X','40+8X')
# Existing two-mark support gates are now four; terminal stays two.
for k in ['curse_12','curse_14','curse_15']:
    regex(name,r'(case "'+k+r'"[^\n]*)层数 ≥ 2',r'\1层数 ≥ 4')
rep(name,'F("6+X")','F("12+X")');rep(name,'F("16+X")','F("32+X")');rep(name,'30+1.5X','60+1.5X')
rep(name,'" + S(4) + " 点格挡，造成','" + L(4) + " 点格挡，造成')
rep(name,'" + S(16) + " 点伤害','" + L(16) + " 点伤害')
rep(name,'" + S(3) + " 点。','" + L(3) + " 点。')
rep(name,'(S(8) - 2)','(CoreBoardingCardBalance.Sustain(key, level) - 2)')
rep(name,'消耗 1 药物和 6 点潮势','消耗 1 药物和 12 点潮势')
rep(name,'消耗 1 药物和 8 点潮势','消耗 1 药物和 16 点潮势')
rep(name,'消耗 1 药物和 2 点魔力','消耗 1 药物和 4 点魔力')
rep(name,'8+出牌前连锁层数','16+2×出牌前连锁层数');rep(name,'14+出牌前连锁层数','28+2×出牌前连锁层数')
rep(name,'伤害提高 1 点','伤害提高 2 点')
rep(name,'" + S(6) + " 点格挡','" + L(6) + " 点格挡')
rep(name,'" + S(8) + " 点。','" + L(8) + " 点。')
rep(name,'" + S(4) + " 点伤害','" + L(4) + " 点伤害')
rep(name,'造成 3 点伤害 2 次，出牌前每有一层连锁，本次伤害提高 2 点','造成 6 点伤害 2 次，出牌前每有一层连锁，每次伤害提高 3 点')
rep(name,'3+2×出牌前连锁层数','6+3×出牌前连锁层数')
rep(name,'" + S(10) + " 点。','" + L(10) + " 点。')
rep(name,'20+5X','40+7X');rep(name,'16+4X','32+6X');rep(name,'28+6X','56+8X')
rep(name,'" + S(12) + " 点伤害','" + L(12) + " 点伤害')
rep(name,'(S(16) - S(12))','(L(16) - L(12))')
rep(name,'" + S(8) + " 点伤害','" + L(8) + " 点伤害')
rep(name,'(S(17) - S(7))','(L(17) - L(7))')
rep(name,'" + S(12) + " 点格挡','" + G(12) + " 点格挡')
rep(name,'" + S(22) + " 点伤害','" + L(22) + " 点伤害')
# Medicine heals are linear HP, unlike the explicit sustain-layer cases above.
for v in [10,13]: rep(name,f'(S({v}) - 2)',f'(L({v}) - 2)')
rep(name,'S(doctor ? 7 : 6)','L(doctor ? 7 : 6)')
rep(name,'S(doctor ? 16 : 13)','L(doctor ? 16 : 13)')
files[name]='using System;\n'+files[name]

# Cost icons must use the same table as payment. This is a Presentation file.
name='CardSpecialCostPresentation.cs'
files[name]=(ROOT/'UnityClient/Assets/_Project/Presentation/Runtime'/name).read_text(encoding='utf-8-sig')
rep(name,'            switch (key)\n            {\n                case "fire_14": return "2";', '''            int fixedCost = CoreBoardingCardBalance.Cost(key);
            if (fixedCost > 0) return fixedCost.ToString();
            switch (key)
            {
                case "fire_14": return "4";''')

for name,text in files.items():
    (OUT/name).write_text(text,encoding='utf-8')
(OUT/'manifest.json').write_text(json.dumps({'basis':str(SRC),'files':list(files),'presentation':['CardSpecialCostPresentation.cs'],'note':'candidate only, no production edits'},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Wrote {len(files)} candidate files to {OUT}')
