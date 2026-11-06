using System;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Authored naval-only coefficients shared by execution, catalogue and live card faces.</summary>
    public static class CoreNavalExpansionBalance
    {
        public static int DesperateCannonBonus(int upgradeLevel) =>
            upgradeLevel <= 0 ? 16 : upgradeLevel == 1 ? 35 : 75;

        public static int TideStrikeDamageCoefficient(int upgradeLevel) =>
            upgradeLevel <= 0 ? 2 : upgradeLevel == 1 ? 4 : upgradeLevel == 2 ? 6 : 7;

        public static int ManaBurstDamageCoefficient(int upgradeLevel) =>
            upgradeLevel <= 0 ? 5 : upgradeLevel == 1 ? 9 : 20;

        public static int ThunderCollapseChainCoefficient(int upgradeLevel) =>
            8 * (1 + Math.Max(0, Math.Min(2, upgradeLevel)));

        public static int ThunderCollapseDamage(int baseDamage, int upgradeLevel, int chain, int removedCells) =>
            (int)Math.Min(int.MaxValue, Math.Max(0L, baseDamage) +
                (long)ThunderCollapseChainCoefficient(upgradeLevel) * Math.Max(0, chain) +
                4L * Math.Max(0, removedCells));
    }
}
