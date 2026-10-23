using System;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>Supply costs use a separate slot from school resources.</summary>
    public static class CardSupplyCostPresentation
    {
        public static NavalBattleStage PreviewStage(CoreBattleCard card) =>
            card != null && card.IsAllowedInStage(NavalBattleStage.Boarding) ? NavalBattleStage.Boarding :
            card != null && card.IsAllowedInStage(NavalBattleStage.Naval) ? NavalBattleStage.Naval : NavalBattleStage.Preparation;

        public static string ResourceType(CoreBattleCard card, NavalBattleStage? stage = null)
        {
            if (card == null) return string.Empty;
            NavalBattleStage context = stage ?? PreviewStage(card);
            if (!card.IsAllowedInStage(context)) return string.Empty;
            if (context == NavalBattleStage.Boarding) card = CoreBoardingCardCatalog.ForBoarding(card);
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.WoodCost > 0) return "wood";
            if (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideWard && card.SchoolEffect.WoodCost > 0)
                return "wood";
            if (card.BoardingSpec != null) return card.Repair > 0 ? "medicine" : string.Empty;
            CoreSchoolMechanic mechanic = card.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None;
            if (mechanic == CoreSchoolMechanic.BloodMend || mechanic == CoreSchoolMechanic.BloodDrain) return string.Empty;
            int repair = card.Repair;
            if (mechanic == CoreSchoolMechanic.ArcaneMend || mechanic == CoreSchoolMechanic.CurseDrain ||
                mechanic == CoreSchoolMechanic.TideRenew || mechanic == CoreSchoolMechanic.FireHeal)
                repair = (int)Math.Min(int.MaxValue, (long)repair + card.SchoolEffect.Power);
            return repair > CoreBattleCardEffects.UnprovisionedRepair
                ? (context == NavalBattleStage.Boarding ? "medicine" : "wood") : string.Empty;
        }

        public static int BaseCost(CoreBattleCard card, NavalBattleStage? stage = null)
        {
            if (ResourceType(card, stage).Length == 0) return 0;
            if ((stage ?? PreviewStage(card)) == NavalBattleStage.Boarding)
                card = CoreBoardingCardCatalog.ForBoarding(card);
            int wood = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.WoodCost ?? 0;
            if (wood > 0) return wood;
            if (card.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideWard) return card.SchoolEffect.WoodCost;
            return 1;
        }

        public static string CostText(CoreBattleCard card, CoreVoyageSession session = null, NavalBattleStage? stage = null)
        {
            NavalBattleStage context = stage ?? PreviewStage(card);
            string type = ResourceType(card, context);
            if (type.Length == 0) return string.Empty;
            int baseline = BaseCost(card, context);
            if (session?.Battle == null || session.Phase != CoreDemoPhase.Battle || session.Battle.Stage != context)
                return baseline.ToString();
            if (context == NavalBattleStage.Boarding) card = CoreBoardingCardCatalog.ForBoarding(card);
            if (type == "medicine" && card.BoardingSpec != null)
            {
                if (session.Cargo.Medicine <= 0) return "0";
                // Bandaging charges only above its fallback; the other authored
                // treatments pay on cast, including delayed and fixed healing.
                return card.BoardingSpec.Key == "basic_06" &&
                    session.Battle.PlayerMaxBoardingHealth - session.Battle.PlayerBoardingHealth <= 2 ? "0" : "1";
            }
            int fixedWood = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.WoodCost ?? 0;
            if (fixedWood > 0) return session.Cargo.Wood >= fixedWood ? fixedWood.ToString() : "0";
            CoreBattleCardEffects quote = session.GetBattleCardEffects(card, context);
            if (type == "medicine") return quote.MedicineCost.ToString();
            int schoolWood = card.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideWard &&
                session.Cargo.Wood >= card.SchoolEffect.WoodCost ? card.SchoolEffect.WoodCost : 0;
            return (quote.WoodCost + schoolWood).ToString();
        }
    }
}
