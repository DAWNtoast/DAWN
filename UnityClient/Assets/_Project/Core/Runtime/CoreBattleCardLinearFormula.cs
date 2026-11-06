using System;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>A printed constant and coefficient shared by card text, previews and resolution.</summary>
    public readonly struct CoreBattleCardLinearFormula
    {
        private CoreBattleCardLinearFormula(int constant, decimal coefficient)
        {
            Constant = constant;
            Coefficient = coefficient;
        }

        public int Constant { get; }
        public decimal Coefficient { get; }

        public static CoreBattleCardLinearFormula For(CoreBattleCard card, int baseConstant,
            decimal baseCoefficient, int? levelOverride = null)
        {
            int level = Math.Max(0, levelOverride ?? card?.UpgradeLevel ?? 0);
            if (level == 0) return new CoreBattleCardLinearFormula(baseConstant, baseCoefficient);
            int constant = CoreCardUpgradeDesign.ScaleEffect(card, baseConstant, level);
            decimal coefficient = decimal.Round(baseCoefficient * CoreCardUpgradeDesign.EffectMultiplier(card, level),
                0, MidpointRounding.AwayFromZero);
            return new CoreBattleCardLinearFormula(constant, Math.Max(0m, Math.Min(int.MaxValue, coefficient)));
        }

        public int Evaluate(int amount)
        {
            // Base designs with fractional coefficients keep their existing floor.
            // Upgraded coefficients are already whole numbers; never rescale the total.
            decimal total = Constant + Coefficient * Math.Max(0, amount);
            return (int)Math.Max(0m, Math.Min(int.MaxValue, decimal.Floor(total)));
        }

        public string Describe(string variable)
        {
            if (Coefficient == 0m) return Constant.ToString(CultureInfo.InvariantCulture);
            string term = Coefficient == 1m ? variable : Coefficient.ToString("0.############################", CultureInfo.InvariantCulture) +
                (variable == "X" ? string.Empty : "×") + variable;
            return Constant == 0 ? term : Constant.ToString(CultureInfo.InvariantCulture) + "+" + term;
        }
    }
}
