using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Non-combat Unity tuning; battle stats and legacy browser CSV rules are unchanged.</summary>
    public static class GameplayBalance
    {
        public const int Version = 2;
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

        public static int DispatchHours(int mission) => mission == 1 ? 6 : mission == 2 ? 10 : 2;
        // Dispatch is intentionally a pure-income activity; the mission itself has no entry cost.
        public static int DispatchCost(int mission) => 0;
        public static int DispatchGold(int mission) => mission == 1 ? 105 : mission == 2 ? 125 : 35;
        public static int DispatchWood(int mission) => mission == 0 ? 3 : 0;
        public static int DispatchIron(int mission) => mission == 2 ? 6 : 0;
        public static int DispatchFame(int mission) => mission == 1 ? 3 : mission == 2 ? 4 : 0;
        public static int DispatchSpecialist(int mission) => mission == 1 ? 3 : mission == 2 ? 0 : 5;

        public static int DispatchPercent(int mission, int hours, IReadOnlyList<int> crew)
        {
            var unique = new HashSet<int>();
            if (crew != null)
                foreach (int profession in crew)
                    if (profession >= 0 && profession < 6 && unique.Count < 2) unique.Add(profession);
            int baseline = DispatchHours(mission);
            double extension = Math.Max(0d, Math.Min(1d, (hours - baseline) / (double)baseline));
            return 100 + Round(extension * 75d) + unique.Count * 10 +
                (unique.Contains(DispatchSpecialist(mission)) ? 15 : 0);
        }

        public static int ScaleReward(int baseline, int percent) => baseline <= 0 ? 0 :
            Round(baseline * Math.Max(0, Math.Min(300, percent)) / 100d);
    }
}
