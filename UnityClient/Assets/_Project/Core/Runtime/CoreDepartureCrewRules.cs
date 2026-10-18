using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>Ordinary sailors must fill at least 40% of the ship's total berths before departure.</summary>
        public int MinimumDepartureOrdinaryCrew => (int)((2L * CrewCountValue + 4L) / 5L);
        public bool HasEnoughOrdinaryCrewForDeparture => SelectedOrdinaryCrew >= MinimumDepartureOrdinaryCrew;

        /// <summary>Specialists occupy berths but cannot replace the ordinary sailors needed to sail.</summary>
        public bool CanStartWithDepartureCrew(out string reason)
        {
            reason = HasEnoughOrdinaryCrewForDeparture ? string.Empty :
                "普通船员不足，至少需要 " + MinimumDepartureOrdinaryCrew + " 人（船员承载的 40%）；当前 " + SelectedOrdinaryCrew + " 人。";
            return reason.Length == 0;
        }
    }
}
