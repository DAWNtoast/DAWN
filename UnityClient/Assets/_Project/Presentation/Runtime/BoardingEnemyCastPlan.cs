using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    public static class BoardingEnemyCastPlan
    {
        // IDs and authored mechanics only; translated labels never select an animation.
        public static bool TryResolve(CoreBoardingEnemyDeck deck, CoreBoardingEnemyActionResult result,
            out BattleSchoolAttackKind school, out BoardingSchoolCastRole role)
        {
            role = BoardingSchoolCastRole.Empower;
            if (!BattleSchoolAttackPresentation.TrySchool(deck?.SchoolId, out school) || result == null || result.RawAttack > 0) return false;
            CoreBoardingEnemyCard source = null;
            foreach (CoreBoardingEnemyCard card in deck.Cards) if (card.Id == result.CardId) { source = card; break; }
            if (source == null || source.AttackPercent > 0) return false;
            if (source.Mechanic == CoreBoardingEnemyMechanic.CurseMark || source.Mechanic == CoreBoardingEnemyMechanic.FireTransfer ||
                source.BurnPercent > 0 && source.Mechanic != CoreBoardingEnemyMechanic.FireSelfBurn)
                role = BoardingSchoolCastRole.Affliction;
            else if (result.Healed > 0 || source.Mechanic == CoreBoardingEnemyMechanic.TideHeal)
                role = BoardingSchoolCastRole.Heal;
            else if (result.GuardGained > 0 || source.GuardPercent > 0 || source.Mechanic == CoreBoardingEnemyMechanic.TideGuard)
                role = BoardingSchoolCastRole.Guard;
            else if (source.Mechanic == CoreBoardingEnemyMechanic.ArcaneGain || source.Mechanic == CoreBoardingEnemyMechanic.FireSelfBurn)
                role = BoardingSchoolCastRole.Charge;
            return true;
        }
    }
}
