from pathlib import Path
ROOT = Path('D:/Game')
SOURCE = ROOT / 'outputs/boarding-field-balance-20261024/experiments/combined-b/CoreBattleStatusPresentation.cs'
OUT = ROOT / 'outputs/boarding-field-balance-20261024/experiments/card-b'
OUT.mkdir(exist_ok=True)
text = SOURCE.read_text(encoding='utf-8-sig')
changes = [
    ('当前燃烧层数 ×2', '当前燃烧层数'),
    ('"每次因敌方主动攻击失去生命值时，给敌方施加 2 层咒印。"',
     '"每次因敌方主动攻击失去生命值时，给敌方施加 " + N((int)Math.Min(int.MaxValue, (long)Math.Max(1, b.CurseContracts.Count) * 4)) + " 层咒印。"'),
    ('各给敌方施加 1 层咒印', '各给敌方施加 2 层咒印'),
    ('Scale(b.CurseCounterCard, Sum(b.CurseMarks, b.CurseMarks))', 'BoardingWindowAmount(b.CurseCounters, b.CurseCounterCard, Sum(b.CurseMarks, b.CurseMarks))'),
    ('Scale(b.TideAmbushCard, 16)', 'BoardingWindowAmount(b.TideAmbushes, b.TideAmbushCard, 32)'),
    ('Scale(b.BloodDebtCard, 8)', 'Scale(b.BloodDebtCard, CoreBoardingCardBalance.Linear(8))'),
    ('Scale(b.BloodOathCard, 8)', 'Scale(b.BloodOathCard, CoreBoardingCardBalance.Linear(8))'),
    ('Scale(b.CurseWeakCard, 3 + b.CurseMarks)', 'BoardingWindowAmount(b.CurseWeaknesses, b.CurseWeakCard, Sum(6, b.CurseMarks))'),
    ('Scale(b.TideWeakCard, 3)', 'BoardingWindowAmount(b.TideWeaknesses, b.TideWeakCard, 6)'),
    ('Scale(b.CurseArmorCard, 2 + b.CurseMarks)', 'BoardingWindowAmount(b.CurseArmors, b.CurseArmorCard, Sum(4, b.CurseMarks))'),
    ('        private static int Scale(CoreBattleCard card, int amount)',
     '''        private static int BoardingWindowAmount(List<CoreBoardingEffectWindow> windows, CoreBattleCard fallback, int basis)
        {
            if (windows.Count == 0) return Scale(fallback, basis);
            int amount = 0;
            foreach (CoreBoardingEffectWindow effect in windows) amount = Sum(amount, Scale(effect.Card, basis));
            return amount;
        }
        private static int Scale(CoreBattleCard card, int amount)'''),
]
for before, after in changes:
    if before not in text: raise ValueError(before)
    text = text.replace(before, after)
# Gameplay remains frozen. All modifications are in AddBoarding or its display-only helper.
assert text[:text.index('        private static void AddBoarding(')] == SOURCE.read_text(encoding='utf-8-sig')[:text.index('        private static void AddBoarding(')]
(OUT / SOURCE.name).write_text(text, encoding='utf-8-sig', newline='\r\n')
print('Wrote HUD-only card-b overlay.')
