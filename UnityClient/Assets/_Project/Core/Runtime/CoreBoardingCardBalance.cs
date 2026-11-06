using System;

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
        public static int BurnFuelBase(int rank) => Rank(rank, 3, 4, 5);
        public static int BurnFuelCoefficient(int rank) => Rank(rank, 2, 3, 4);
        public static int BurnFuel(int amount, int rank) => SaturatedFormula(BurnFuelBase(rank), BurnFuelCoefficient(rank), amount);
        public static int SelfBurnDamageBase(int rank) => Rank(rank, 60, 75, 90);
        public static int SelfBurnDamageCoefficient(int rank) => Rank(rank, 2, 3, 5);
        public static int SelfBurnDamage(int stacks, int rank) => SaturatedFormula(SelfBurnDamageBase(rank), SelfBurnDamageCoefficient(rank), stacks);
        private static int SaturatedFormula(int basis, int coefficient, int amount)
            => (int)Math.Min(int.MaxValue, (long)basis + (long)coefficient * Math.Max(0, amount));
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
