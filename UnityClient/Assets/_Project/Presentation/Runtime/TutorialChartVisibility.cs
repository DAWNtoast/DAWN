using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;

namespace ShatteredSail.Presentation
{
    /// <summary>Both tutorial views follow the same location rules as the live chart.</summary>
    public static class TutorialChartVisibility
    {
        public static bool IsNodeVisible(TutorialVoyageSession session, int markerIndex)
        {
            if (session == null || markerIndex < 0 || markerIndex > 4) return false;
            if (markerIndex == 0 || (session.Leg == 2 && markerIndex == 4)) return true;
            int encounterIndex = markerIndex - 1;
            if (encounterIndex >= session.CurrentLegEncounters.Count) return false;
            TutorialEncounter encounter = session.CurrentLegEncounters[encounterIndex];
            EncounterDiscoveryKind kind = CoreEncounterDiscoveryRules.Classify(encounter.Id, encounter.Hidden);
            if (kind == EncounterDiscoveryKind.OnboardIncident || kind == EncounterDiscoveryKind.TransientEncounter) return false;
            if (kind == EncounterDiscoveryKind.HiddenLocation)
                return session.HiddenLocationRevealed || session.CurrentNodeIndex >= encounterIndex;
            return encounterIndex <= session.CurrentNodeIndex + 1;
        }
    }
}
