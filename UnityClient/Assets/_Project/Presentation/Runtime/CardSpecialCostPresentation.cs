using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>Displays only resources actually paid by PaySchoolCardCost.</summary>
    public static class CardSpecialCostPresentation
    {
        public static string ResourceType(CoreBattleCard card)
        {
            CoreSchoolCardEffect effect = card?.SchoolEffect;
            if (effect == null) return string.Empty;
            switch (effect.Mechanic)
            {
                case CoreSchoolMechanic.ArcaneRelease:
                case CoreSchoolMechanic.ArcaneWard:
                case CoreSchoolMechanic.ArcaneMend:
                case CoreSchoolMechanic.ArcaneEcho:
                    return effect.Threshold > 0 ? "mana" : string.Empty;
                case CoreSchoolMechanic.BloodRisk:
                case CoreSchoolMechanic.BloodWard:
                    return effect.Threshold > 0 ? "health" : string.Empty;
                case CoreSchoolMechanic.CurseDrain:
                case CoreSchoolMechanic.CurseWard:
                    return effect.Threshold > 0 ? "curse" : string.Empty;
                case CoreSchoolMechanic.TideWard: return effect.WoodCost > 0 ? "wood" : string.Empty;
                case CoreSchoolMechanic.TideRiposte:
                case CoreSchoolMechanic.TideCleanse:
                case CoreSchoolMechanic.TideRenew: return "tide";
                case CoreSchoolMechanic.ThunderFinish: return "chain";
                default: return string.Empty;
            }
        }

        public static string CostText(CoreBattleCard card, CoreBattleState battle = null, int? availableWood = null)
        {
            if (ResourceType(card).Length == 0) return string.Empty;
            switch (card.SchoolEffect.Mechanic)
            {
                case CoreSchoolMechanic.TideWard:
                    return battle != null && availableWood.HasValue && availableWood.Value < card.SchoolEffect.WoodCost
                        ? "0" : card.SchoolEffect.WoodCost.ToString();
                case CoreSchoolMechanic.TideRiposte:
                    return battle == null ? "全" : battle.Schools.TidePower.ToString();
                case CoreSchoolMechanic.ThunderFinish:
                    return battle == null ? "全" : battle.Schools.ThunderChain.ToString();
                default: return card.SchoolEffect.Threshold.ToString();
            }
        }

        public static CardOrnamentKind OrnamentKind(CoreBattleCard card)
        {
            switch (ResourceType(card))
            {
                case "mana": return CardOrnamentKind.Mana;
                case "health": return CardOrnamentKind.Health;
                case "curse": return CardOrnamentKind.Curse;
                case "wood": return CardOrnamentKind.Wood;
                case "tide": return CardOrnamentKind.Tide;
                default: return CardOrnamentKind.Chain;
            }
        }
    }
}
