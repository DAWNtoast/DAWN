using System;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>One source for the printed and executed fire/arcane expansion values.</summary>
    public static class CoreNavalFireArcanePower
    {
        public static int BurnLayers(CoreBattleCard card)
        {
            if (card == null) return 0;
            switch (card.Id)
            {
                case "card_school_fire_n07": return AtRank(card.UpgradeLevel, 6, 10, 24, 32);
                case "card_school_fire_n08": return AtRank(card.UpgradeLevel, 3, 5, 12, 16);
                default: return 0;
            }
        }

        public static int ManaGain(CoreBattleCard card) => card?.Id == "card_school_arcane_n06"
            ? AtRank(card.UpgradeLevel, 4, 5, 7, 10) : 0;

        public static int CapacityGain(CoreBattleCard card) => card?.Id == "card_school_arcane_n05"
            ? AtRank(card.UpgradeLevel, 1, 2, 4, 6) : 0;

        public static string RenderTemplate(string template, CoreBattleCard card) => (template ?? string.Empty)
            .Replace("{synergyBurn}", BurnLayers(card).ToString(CultureInfo.InvariantCulture))
            .Replace("{synergyMana}", ManaGain(card).ToString(CultureInfo.InvariantCulture))
            .Replace("{synergyCapacity}", CapacityGain(card).ToString(CultureInfo.InvariantCulture));

        private static int AtRank(int rank, int zero, int one, int two, int three)
        {
            switch (Math.Max(0, Math.Min(3, rank)))
            {
                case 0: return zero;
                case 1: return one;
                case 2: return two;
                default: return three;
            }
        }
    }

    public sealed partial class CoreVoyageSession
    {
        /// <summary>Called after shared validation/payment and, for fire cards, after their cannon hit.</summary>
        private void ApplyNavalFireArcanePower(CoreBattleCard card)
        {
            switch (card.Id)
            {
                case "card_school_fire_n07":
                case "card_school_fire_n08":
                    // Fire's existing application owns oil doubling, immunity and layer saturation.
                    ApplyFireBurn(false, CoreNavalFireArcanePower.BurnLayers(card));
                    break;
                case "card_school_arcane_n05":
                    AddSchoolGuard(SchoolSum(card.Block, Battle.PlayerDeckBuff));
                    // Expansion raises future refills only. It does not refill the current pool.
                    Battle.Schools.ManaLimit = SchoolSum(Battle.Schools.ManaLimit,
                        CoreNavalFireArcanePower.CapacityGain(card));
                    break;
                case "card_school_arcane_n06":
                    AddSchoolGuard(SchoolSum(card.Block, Battle.PlayerDeckBuff));
                    GainBattleMana(CoreNavalFireArcanePower.ManaGain(card));
                    break;
            }
        }
    }
}
