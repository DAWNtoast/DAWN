namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Authored enemy-target range, shared by play validation and the painted grid.</summary>
    public static class CoreNavalCardRangeRules
    {
        public static int MaximumCells(CoreBattleCard card)
        {
            if (card == null) return 0;
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id) != null) return 4;
            if (card.Action == NavalBattleCardAction.Sabotage) return 2;
            if (card.Action == NavalBattleCardAction.Cannon) return 4;
            if (card.SchoolId.Length > 0 && card.Damage > 0) return 4;
            return 0;
        }

        public static bool Contains(CoreBattleCard card, VoyagePoint origin, VoyagePoint target)
        {
            int maximum = MaximumCells(card);
            int distance = CoreVoyageSession.NavalGridDistance(origin, target);
            return maximum <= 0 || distance >= 1 && distance <= maximum;
        }
    }
}
