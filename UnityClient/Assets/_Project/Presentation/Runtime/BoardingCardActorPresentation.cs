using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>Authored card performance; independent of translated labels and secondary combat results.</summary>
    public static class BoardingCardActorPresentation
    {
        public static BoardingActorAction ForCard(CoreBattleCard card, bool resolvingDelayed = false)
        {
            if (card == null || !card.IsAllowedInStage(NavalBattleStage.Boarding)) return BoardingActorAction.Idle;
            CoreSpecialCardDefinition specialist = CoreSpecialCardCatalog.FindByCardId(card.Id);
            if (specialist != null && specialist.SourceKind == CoreSpecialCardSourceKind.Crew)
            {
                if (specialist.LegacySourceKey == "doctor" || specialist.LegacySourceKey == "cook" ||
                    specialist.SourceId == "crew_twilight_tide_carpenter_orlan") return BoardingActorAction.Cast;
                if (specialist.SourceId == "crew_gray_gull_diver_noya") return BoardingActorAction.Cast;
                if (specialist.SourceId == "crew_salt_crown_diver_isha") return BoardingActorAction.Attack;
            }
            card = CoreBoardingCardCatalog.ForBoarding(card);
            string key = card.BoardingSpec?.Key;
            // Setting a future stab is preparation. The blade moves when that attack actually resolves.
            if (!resolvingDelayed && card.BoardingSpec?.IsDelayed == true)
                return key == "arcane_11" ? BoardingActorAction.Cast : BoardingActorAction.Prepare;
            if (CoreComponentFieldCards.IsFieldCard(card)) return BoardingActorAction.Cast;
            switch (key)
            {
                case "basic_01": case "basic_02": case "basic_03": case "basic_11": case "basic_13":
                case "fire_01": case "fire_02": case "fire_06": case "fire_11":
                case "arcane_01": case "curse_01":
                case "tide_04": case "tide_06": case "tide_07":
                case "thunder_02": case "thunder_03": case "thunder_05": case "thunder_11": case "thunder_12":
                case "blood_01": case "blood_02": case "blood_06": case "blood_07":
                case "blood_10": case "blood_11": case "blood_13":
                    return BoardingActorAction.Attack;
                case "basic_08": case "basic_09": case "basic_10":
                case "fire_09": case "fire_10": case "fire_14":
                case "arcane_03": case "arcane_09": case "arcane_10": case "arcane_12": case "arcane_14":
                case "curse_13": case "curse_14": case "tide_13": case "tide_14":
                case "thunder_06": case "thunder_07": case "thunder_08": case "thunder_09": case "thunder_10":
                case "blood_04": case "blood_05": case "blood_14":
                    return BoardingActorAction.Prepare;
                case "tide_05":
                    return BoardingActorAction.Guard;
            }
            if (BattleSchoolAttackPresentation.TryBoardingCast(card, out _, out BoardingSchoolCastRole role))
            {
                if (role == BoardingSchoolCastRole.Heal) return BoardingActorAction.Heal;
                if (role == BoardingSchoolCastRole.Guard) return BoardingActorAction.Guard;
                // Marks, fields, contracts, ranged magic and executions retain a spell gesture.
                return BoardingActorAction.Cast;
            }
            if (card.BoardingSpec?.IsAttack == true || card.Damage > 0) return BoardingActorAction.Attack;
            if (card.Repair > 0) return BoardingActorAction.Heal;
            if ((card.BoardingBlock ?? card.Block) > 0) return BoardingActorAction.Guard;
            return BoardingActorAction.Prepare;
        }

        public static BoardingActorAction ForResolved(string sourceCardId, string sourceSchoolId)
        {
            CoreBattleCard source = CoreBoardingCardCatalog.Find(sourceCardId);
            if (source == null && !string.IsNullOrEmpty(sourceCardId))
                foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
                    if (card.Id == sourceCardId) { source = card; break; }
            if (source != null)
            {
                BoardingActorAction action = ForCard(source, resolvingDelayed: true);
                if (action == BoardingActorAction.Attack || action == BoardingActorAction.Cast) return action;
            }
            return BattleSchoolAttackPresentation.TrySchool(sourceSchoolId, out _) ? BoardingActorAction.Cast : BoardingActorAction.Attack;
        }

        public static BoardingActorAction ForEnemyAttack(CoreBoardingEnemyDeck deck, string cardId)
        {
            if (deck != null)
                foreach (CoreBoardingEnemyCard card in deck.Cards)
                {
                    if (card.Id != cardId) continue;
                    switch (card.Mechanic)
                    {
                        case CoreBoardingEnemyMechanic.ArcaneSpend:
                        case CoreBoardingEnemyMechanic.CurseMark:
                        case CoreBoardingEnemyMechanic.CurseDetonate:
                        case CoreBoardingEnemyMechanic.FireConsume:
                            return BoardingActorAction.Cast;
                        default: return BoardingActorAction.Attack;
                    }
                }
            return BoardingActorAction.Attack;
        }

        public static float Duration(BoardingActorAction action) => action == BoardingActorAction.Prepare ? .5f :
            action == BoardingActorAction.Guard ? .42f : action == BoardingActorAction.Heal ? .56f : .58f;
    }
}
