using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    internal sealed class CoreBoardingEffectWindow
    {
        internal CoreBattleCard Card;
        internal int Turns;
        internal CoreBoardingEffectWindow(CoreBattleCard card, int turns) { Card = card; Turns = turns; }
    }

    public sealed partial class CoreBoardingBattleState
    {
        internal readonly List<CoreBoardingEffectWindow> FirePacts = new List<CoreBoardingEffectWindow>();
        internal readonly List<CoreBoardingEffectWindow> CurseContracts = new List<CoreBoardingEffectWindow>();
        internal readonly List<CoreBoardingEffectWindow> CurseWeaknesses = new List<CoreBoardingEffectWindow>();
        internal readonly List<CoreBoardingEffectWindow> CurseArmors = new List<CoreBoardingEffectWindow>();
        internal readonly List<CoreBoardingEffectWindow> CurseCounters = new List<CoreBoardingEffectWindow>();
        internal readonly List<CoreBoardingEffectWindow> TideAmbushes = new List<CoreBoardingEffectWindow>();
        internal readonly List<CoreBoardingEffectWindow> TideWeaknesses = new List<CoreBoardingEffectWindow>();
        public int FirePactCopies => FirePacts.Count;
        public int CurseContractCopies => CurseContracts.Count;
        public int CurseCounterCopies => CurseCounters.Count;
    }

    public sealed partial class CoreVoyageSession
    {
        private static void BAdvanceWindows(List<CoreBoardingEffectWindow> windows)
        {
            foreach (var effect in windows) effect.Turns--;
            windows.RemoveAll(effect => effect.Turns <= 0);
        }

        private static int BWindowTurns(List<CoreBoardingEffectWindow> windows)
        { int turns = 0; foreach (var effect in windows) turns = Math.Max(turns, effect.Turns); return turns; }

        private int BWindowAmount(List<CoreBoardingEffectWindow> windows, int basis)
        { int amount = 0; foreach (var effect in windows) amount = SchoolSum(amount, BValue(effect.Card, basis)); return amount; }

        private int BWindowFormula(List<CoreBoardingEffectWindow> windows, int constant, decimal coefficient, int value)
        { int amount = 0; foreach (var effect in windows) amount = SchoolSum(amount, BFormula(effect.Card, constant, coefficient, value)); return amount; }

        private int BCurseContractAmount => BState.CurseContracts.Count > 0 ? SchoolProduct(BState.CurseContracts.Count, 4) : BState.CurseContractTurns > 0 ? 4 : 0;
        private int BCurseCounterAmount(int marks) => BState.CurseCounters.Count > 0
            ? BWindowFormula(BState.CurseCounters, 0, 2, marks)
            : BState.CurseCounterTurns > 0 ? BFormula(BState.CurseCounterCard, 0, 2, marks) : 0;
        private int BCurseArmorAmount(int marks) => BState.CurseArmors.Count > 0
            ? BWindowFormula(BState.CurseArmors, 4, 1, marks)
            : BState.CurseArmorTurns > 0 ? BFormula(BState.CurseArmorCard, 4, 1, marks) : 0;
        private int BContinuousAttackReduction(int marks)
        {
            int curse = BState.CurseWeaknesses.Count > 0 ? BWindowFormula(BState.CurseWeaknesses, 6, 1, marks)
                : BState.CurseWeakTurns > 0 ? BFormula(BState.CurseWeakCard, 6, 1, marks) : 0;
            int tide = BState.TideWeaknesses.Count > 0 ? BWindowAmount(BState.TideWeaknesses, 6)
                : BState.TideWeakTurns > 0 ? BValue(BState.TideWeakCard, 6) : 0;
            return SchoolSum(curse, tide);
        }
        private int BTideAmbushAmount => BState.TideAmbushes.Count > 0 ? BWindowAmount(BState.TideAmbushes, 32)
            : BState.TideAmbushTurns > 0 ? BValue(BState.TideAmbushCard, 32) : 0;

        private void BAdvanceManuscriptEnemyWindows()
        {
            BAdvanceWindows(BState.CurseContracts); BState.CurseContractTurns = BWindowTurns(BState.CurseContracts);
            BAdvanceWindows(BState.CurseWeaknesses); BState.CurseWeakTurns = BWindowTurns(BState.CurseWeaknesses);
            BAdvanceWindows(BState.CurseArmors); BState.CurseArmorTurns = BWindowTurns(BState.CurseArmors);
            BAdvanceWindows(BState.CurseCounters); BState.CurseCounterTurns = BWindowTurns(BState.CurseCounters);
            BAdvanceWindows(BState.TideAmbushes); BState.TideAmbushTurns = BWindowTurns(BState.TideAmbushes);
            BAdvanceWindows(BState.TideWeaknesses); BState.TideWeakTurns = BWindowTurns(BState.TideWeaknesses);
        }
    }
}
