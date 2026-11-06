using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Upgrades improve existing numbers without unlocking another effect or payment branch.</summary>
    public static class CoreBattleCardUpgradeStructure
    {
        // Only upgraded definitions consult the library. Rank-zero construction must
        // remain independent of the library that is currently being initialized.
        private static class BaseDefinitions
        {
            // Suppress beforefieldinit: the runtime must not build this cache while
            // compiling a rank-zero caller whose early return never reads Cards.
            static BaseDefinitions() { }
            internal static readonly Dictionary<string, CoreBattleCard> Cards = Build();
            private static Dictionary<string, CoreBattleCard> Build()
            {
                var result = new Dictionary<string, CoreBattleCard>(StringComparer.Ordinal);
                foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
                    if (card != null && !string.IsNullOrEmpty(card.Id)) result[card.Id] = card;
                return result;
            }
        }

        public static CoreBattleCard BaseDefinition(CoreBattleCard card, NavalBattleStage? stage = null)
        {
            if (card == null || card.UpgradeLevel == 0) return card;
            if (!BaseDefinitions.Cards.TryGetValue(card.Id, out CoreBattleCard basis)) return card;
            // A shared identity can use a different authored baseline in boarding.
            return stage == NavalBattleStage.Boarding || card.BoardingSpec != null
                ? CoreBoardingCardCatalog.ForBoarding(basis) : basis;
        }

        public static bool HasRepairSupply(CoreBattleCard card, NavalBattleStage? stage = null)
        {
            CoreBattleCard basis = BaseDefinition(card, stage);
            if (basis == null || CoreSpecialCardRedesign.UsesFreeRepair(basis.Id)) return false;
            // Authored boarding treatments explicitly pay medicine (including
            // conditional and delayed treatment) rather than infer a new payment.
            if (basis.BoardingSpec != null) return basis.Repair > 0;
            CoreSchoolMechanic mechanic = basis.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None;
            if (mechanic == CoreSchoolMechanic.BloodMend || mechanic == CoreSchoolMechanic.BloodDrain) return false;
            long repair = basis.Repair;
            if (mechanic == CoreSchoolMechanic.ArcaneMend || mechanic == CoreSchoolMechanic.CurseDrain ||
                mechanic == CoreSchoolMechanic.TideRenew || mechanic == CoreSchoolMechanic.FireHeal)
                repair += Math.Max(0, basis.SchoolEffect.Power);
            return repair > CoreBattleCardEffects.UnprovisionedRepair;
        }
    }
}
