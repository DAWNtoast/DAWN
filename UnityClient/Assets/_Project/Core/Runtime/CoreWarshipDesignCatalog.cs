using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Ship decoration follows the school of the card granted by a component,
    /// rather than inferring a school from the component's name or colour.</summary>
    public static class CoreWarshipDesignCatalog
    {
        public static string ArtResourcePath(string schoolId, bool legendary) =>
            "ShipArtV2/Special/" + (legendary ? "legendary_" : "boss_") + schoolId;

        public static IReadOnlyList<string> Components(string schoolId, bool legendary)
        {
            var result = new List<string>(5);
            string prefix = "card_school_" + schoolId + (legendary ? "_l" : "_e");
            foreach (CoreBattleCard card in CoreSchoolCardCatalog.All)
                if (card.SchoolId == schoolId && card.ArtId.StartsWith(prefix, StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(card.RequiredComponent)) result.Add(card.RequiredComponent);
            return result.AsReadOnly();
        }

        public static string SchoolName(string schoolId) => CoreSchoolUnlockCatalog.Find(schoolId)?.Name ?? schoolId;
    }
}
