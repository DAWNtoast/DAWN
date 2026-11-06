using System;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// Ship stats scale naval cannon damage without changing authored cards or enemy profiles.
    /// The starter Gray Gull (6 firepower, 2 armor) is the reference point.
    /// </summary>
    public static class CoreNavalStatRules
    {
        public const int ReferenceFirepower = 6;
        public const int FirepowerPercentPerPoint = 8;
        public const int ReferenceArmor = 2;
        public const int ArmorResistanceBase = 10;
        public const int MinimumArmorDamagePercent = 50;

        // No gameplay multiplier cap: each firepower point keeps adding the same base-damage percentage.
        public static decimal CannonDamageMultiplier(int effectiveFirepower) =>
            (100m + ((long)Math.Max(0, effectiveFirepower) - ReferenceFirepower) * FirepowerPercentPerPoint) / 100m;

        /// <summary>Scale only the upgraded cannon base; loading and fixed school bonuses are added afterwards.</summary>
        public static int ScaleCannonDamage(int baseDamage, int effectiveFirepower)
        {
            if (baseDamage <= 0) return 0;
            decimal scaled = decimal.Round(baseDamage * CannonDamageMultiplier(effectiveFirepower),
                0, MidpointRounding.AwayFromZero);
            return (int)Math.Min(int.MaxValue, scaled);
        }

        public static decimal ArmorDamageMultiplier(int effectiveArmor) =>
            Math.Max(MinimumArmorDamagePercent / 100m,
                (ArmorResistanceBase + ReferenceArmor) / (ArmorResistanceBase + (decimal)Math.Max(0, effectiveArmor)));

        /// <summary>
        /// Armor precedes curse, pulse and guard. Round up so armor alone never erases a positive hit;
        /// it can remove at most 50%. Integer fractions avoid rounding drift at exact boundaries.
        /// </summary>
        public static int ReduceIncomingCannonDamage(int rawDamage, int effectiveArmor)
        {
            if (rawDamage <= 0) return 0;
            long resistance = ArmorResistanceBase + (long)Math.Max(0, effectiveArmor);
            long armored = ((long)rawDamage * (ArmorResistanceBase + ReferenceArmor) + resistance - 1) / resistance;
            long minimum = ((long)rawDamage * MinimumArmorDamagePercent + 99L) / 100L;
            return (int)Math.Min(int.MaxValue, Math.Max(armored, minimum));
        }
    }
}
