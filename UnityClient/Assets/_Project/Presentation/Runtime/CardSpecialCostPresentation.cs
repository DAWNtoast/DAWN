using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>Displays fixed resource costs and X for cards that spend their available resource.</summary>
    public static class CardSpecialCostPresentation
    {
        public static string ResourceType(CoreBattleCard card)
        {
            CoreNavalExpansionCardSpec naval = CoreNavalSchoolExpansionCatalog.FindSpec(card?.Id);
            if (naval != null)
            {
                if (naval.ManaCost > 0) return "mana";
                if (naval.HullCost > 0) return "health";
                if (naval.ResourceLabel == "咒印") return "curse";
                if (naval.ResourceLabel == "潮势") return "tide";
                if (naval.ResourceLabel == "连锁" || naval.Key == "L06") return "chain";
                return string.Empty;
            }
            if (card?.BoardingSpec != null)
            {
                string key = card.BoardingSpec.Key;
                if (BoardingCostText(key).Length == 0) return string.Empty;
                if (key == "fire_14") return "health";
                if (key.StartsWith("arcane_")) return "mana";
                if (key.StartsWith("curse_")) return "curse";
                if (key.StartsWith("tide_")) return "tide";
                if (key.StartsWith("thunder_")) return "chain";
                if (key.StartsWith("blood_")) return "health";
                return string.Empty;
            }
            CoreSchoolCardEffect effect = card?.SchoolEffect;
            if (effect == null) return string.Empty;
            switch (effect.Mechanic)
            {
                case CoreSchoolMechanic.ArcaneRelease:
                case CoreSchoolMechanic.ArcaneWard:
                case CoreSchoolMechanic.ArcaneAttackReserve:
                case CoreSchoolMechanic.ArcaneGuardReserve:
                case CoreSchoolMechanic.ArcaneMend:
                case CoreSchoolMechanic.ArcaneEcho:
                    return effect.Threshold > 0 ? "mana" : string.Empty;
                case CoreSchoolMechanic.BloodRisk:
                case CoreSchoolMechanic.BloodWard:
                    return effect.Threshold > 0 ? "health" : string.Empty;
                case CoreSchoolMechanic.CurseDrain:
                case CoreSchoolMechanic.CurseWard:
                    return effect.Threshold > 0 ? "curse" : string.Empty;
                case CoreSchoolMechanic.TideRiposte:
                case CoreSchoolMechanic.TideCleanse:
                case CoreSchoolMechanic.TideRenew: return "tide";
                case CoreSchoolMechanic.ThunderFinish: return "chain";
                default: return string.Empty;
            }
        }

        public static string CostText(CoreBattleCard card, CoreBattleState battle = null, int? availableWood = null,
            int? availableMedicine = null)
        {
            if (ResourceType(card) == "health" && CoreSpecialCardRules.WaivesBloodCost(card, battle)) return "0";
            CoreNavalExpansionCardSpec naval = CoreNavalSchoolExpansionCatalog.FindSpec(card?.Id);
            if (naval != null)
            {
                if (naval.ManaCost > 0) return naval.ManaCost.ToString();
                if (naval.HullCost > 0) return naval.HullCost.ToString();
                if (naval.Key == "L06" || naval.ConsumesAllResource) return "X";
                if (naval.ResourceMaximum > 0) return naval.ResourceMinimum == naval.ResourceMaximum
                    ? naval.ResourceMinimum.ToString() : naval.ResourceMinimum + "–" + naval.ResourceMaximum;
                return string.Empty;
            }
            if (card?.BoardingSpec != null)
            {
                string key = card.BoardingSpec.Key;
                if (availableMedicine == 0 && (key == "arcane_05" || key == "curse_12" ||
                    key == "tide_11" || key == "tide_12")) return "0";
                return BoardingCostText(key);
            }
            if (ResourceType(card).Length == 0) return string.Empty;
            switch (card.SchoolEffect.Mechanic)
            {
                case CoreSchoolMechanic.TideRiposte:
                case CoreSchoolMechanic.ThunderFinish: return "X";
                default: return card.SchoolEffect.Threshold.ToString();
            }
        }

        private static string BoardingCostText(string key)
        {
            switch (key)
            {
                case "fire_14": return "2";
                case "arcane_04": case "arcane_06": case "arcane_11": return "X";
                case "arcane_07": case "arcane_08": case "arcane_09": case "arcane_12":
                case "arcane_13": case "arcane_14": case "curse_13": return "1";
                case "arcane_05": case "arcane_10": case "arcane_15": case "curse_12": case "curse_14":
                case "curse_15": case "tide_10": case "thunder_09": return "2";
                case "curse_11": case "tide_06": case "tide_07": case "tide_08":
                case "thunder_13": case "thunder_14": case "thunder_15": return "X";
                case "tide_09": return "4";
                case "tide_12": case "tide_13": case "tide_14": return "8";
                case "tide_11": case "tide_15": return "6";
                case "blood_01": case "blood_03": case "blood_04": case "blood_05": return "3";
                case "blood_02": return "4";
                default: return string.Empty;
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
