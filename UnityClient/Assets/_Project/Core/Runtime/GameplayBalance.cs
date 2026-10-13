using System;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Non-combat Unity tuning; battle stats and legacy browser CSV rules are unchanged.</summary>
    public static class GameplayBalance
    {
        public const double CookRationMultiplier = 0.9d;

        public static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        public static int SupplyDays(int route) => route == 1 ? 14 : route == 2 ? 18 : 16;

        public static int MaterialSalePrice(string material) => material == "wood" ? 2 : material == "iron" ? 6 : material == "cloth" ? 4 : 0;

        public static float ComponentPriceMultiplier(int rarity)
        {
            switch (rarity)
            {
                case 1: return 0.8f;
                case 2: return 0.65f;
                case 3: return 0.55f;
                case 4: return 0.45f;
                default: return 1f;
            }
        }
    }
}
