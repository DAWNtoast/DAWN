using System;

namespace ShatteredSail.Core.Gameplay
{
    public static partial class CoreBattleUpgradeBalance
    {
        // Use the printed/base cost, never temporary discounts or same-turn surcharges.
        // These limits apply to effects: a mixed card can still grow its other numbers at +3.
        public static int UtilityMaximumLevel(CoreBattleCard card)
        {
            if (card == null) return 0;
            CoreCardUpgradeProfile profile = CoreCardUpgradeDesign.Find(card);
            return profile != null ? Math.Max(profile.LastActionPointGrowthLevel,
                Math.Max(profile.LastDrawGrowthLevel, profile.LastEnergyGrowthLevel)) :
                card.Energy <= 1 ? 1 : card.Energy == 2 ? 2 : MaximumLevel;
        }

        public static int UtilityBonus(CoreBattleCard card, int level) =>
            Math.Max(0, Math.Min(level, UtilityMaximumLevel(card)));

        public static int UpgradeDraw(CoreBattleCard original, int level) => original == null || original.Draw <= 0 ? 0 :
            (int)Math.Min(int.MaxValue, (long)original.Draw + CoreCardUpgradeDesign.DrawBonus(original, level));

        public static int BaseEnergyRecovery(CoreBattleCard card)
        {
            if (card?.BoardingSpec != null)
            {
                switch (card.BoardingSpec.Key)
                {
                    case "basic_09": case "tide_14": case "blood_14": return 1;
                    case "basic_10": return 3;
                    case "fire_14": case "arcane_10": case "curse_14":
                    case "thunder_09": case "blood_05": return 2;
                    default: return 0;
                }
            }
            return card?.Action == NavalBattleCardAction.Energize ? 1 : 0;
        }

        public static int EnergyRecovery(CoreBattleCard card)
        {
            int baseline = BaseEnergyRecovery(card);
            return baseline == 0 ? 0 : baseline + CoreCardUpgradeDesign.EnergyBonus(card, card.UpgradeLevel);
        }

        public static bool HasUtilityEffect(CoreBattleCard card) => card != null &&
            (card.Draw > 0 || BaseEnergyRecovery(card) > 0 || IsSchoolDraw(card.SchoolEffect) ||
                card.NavalActionPointGain > 0 && CoreCardUpgradeDesign.Find(card) != null);

        private static bool IsSchoolDraw(CoreSchoolCardEffect effect) => effect != null &&
            (effect.Mechanic == CoreSchoolMechanic.ArcaneEcho || effect.Mechanic == CoreSchoolMechanic.ThunderEcho);

        /// <summary>Published cards use their authored cap. The legacy calculation is
        /// retained for external/test definitions that have no authored profile.</summary>
        public static int MaximumLevelFor(CoreBattleCard card)
        {
            if (card == null || card.AllowedStages == NavalBattleStages.None) return 0;
            CoreCardUpgradeProfile authored = CoreCardUpgradeDesign.Find(card);
            if (authored != null) return authored.MaximumLevel;
            // The expansion upgrades only printed direct damage and paid guard.
            // Its draw, resources, areas, statuses and timings remain fixed.
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id) != null)
                return Math.Max(LastNumericalGrowthLevel(card.Damage), LastNumericalGrowthLevel(card.Block));
            int maximum = Math.Max(Math.Max(LastNumericalGrowthLevel(card.Damage), LastNumericalGrowthLevel(card.Block)),
                Math.Max(LastNumericalGrowthLevel(card.Repair), LastNumericalGrowthLevel(card.BoardingBlock ?? 0)));
            if (card.BoardingSpec != null)
            {
                // These effects scale in the boarding interpreter, outside the three
                // printed damage/guard/heal fields. Keep the list explicit and auditable.
                switch (card.BoardingSpec.Key)
                {
                    case "fire_03": case "fire_04": case "fire_07": case "arcane_13":
                    case "curse_07": case "curse_08": case "curse_09": case "tide_09":
                    case "thunder_06": case "thunder_10": case "blood_08": case "blood_15":
                        return MaximumLevel;
                }
            }
            else
            {
                if (card.Action == NavalBattleCardAction.Polish || card.Action == NavalBattleCardAction.Load ||
                    card.Action == NavalBattleCardAction.PrecisionLoad || card.Action == NavalBattleCardAction.FireRounds)
                    return MaximumLevel;
                CoreSchoolCardEffect effect = card.SchoolEffect;
                if (effect != null)
                {
                    CoreSchoolCardEffect previous = effect;
                    for (int level = 1; level <= MaximumLevel; level++)
                    {
                        CoreSchoolCardEffect grown = effect.AtLevel(level, false);
                        if ((!IsSchoolDraw(effect) && previous.Power != grown.Power) || previous.Secondary != grown.Secondary ||
                            previous.Threshold != grown.Threshold || previous.Duration != grown.Duration ||
                            previous.Limit != grown.Limit || previous.Movement != grown.Movement)
                            maximum = Math.Max(maximum, level);
                        previous = grown;
                    }
                }
            }
            return Math.Max(maximum, HasUtilityEffect(card) ? UtilityMaximumLevel(card) : 0);
        }

        private static int LastNumericalGrowthLevel(int value)
        {
            int maximum = 0, previous = value;
            for (int level = 1; level <= MaximumLevel; level++)
            {
                int grown = ScaleNumerical(value, level);
                if (grown != previous) maximum = level;
                previous = grown;
            }
            // Trim only the trailing plateau. For example, 1/1/2/2 ends at +2,
            // while 2/2/3/4 still ends at +3 despite the unchanged first rank.
            return maximum;
        }
    }

    public sealed partial class CoreVoyageSession
    {
        public int GetCardMaximumUpgradeLevel(int index) => index < 0 || index >= cardLibrary.Count ? 0 :
            UpgradedCardDefinitions.MaximumLevels[CanonicalBattleCardIndex(index)];

        public int GetCardMaximumUpgradeLevel(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            for (int i = 0; i < cardLibrary.Count; i++)
                if (cardLibrary[i].Id == id) return GetCardMaximumUpgradeLevel(i);
            return 0;
        }

        private static int[] BuildCardMaximumLevels()
        {
            var result = new int[cardLibrary.Count];
            for (int i = 0; i < result.Length; i++)
            {
                CoreBattleCard card = cardLibrary[i];
                // Shared source cards have one saved rank. Their ship and boarding
                // effects apply their own limits without erasing the other stage's rank.
                result[i] = Math.Max(CoreBattleUpgradeBalance.MaximumLevelFor(card),
                    CoreBattleUpgradeBalance.MaximumLevelFor(CoreBoardingCardCatalog.ForBoarding(card)));
            }
            return result;
        }
    }
}
