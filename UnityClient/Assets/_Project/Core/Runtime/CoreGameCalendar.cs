using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        // Permanent storage epoch: changing it would move dates in existing saves.
        // Only a new campaign derives its starting date from the local system;
        // subsequent time is campaign state advanced by gameplay.
        public static DateTime GameClockOrigin { get; } = new DateTime(1, 1, 1, 8, 0, 0, DateTimeKind.Unspecified);
        public static double MaximumElapsedGameHours { get; } =
            (DateTime.MaxValue.Ticks - GameClockOrigin.Ticks) / (double)TimeSpan.TicksPerHour;

        public double ElapsedGameHours { get; private set; }

        public static DateTime NewCampaignStartDate(DateTime systemDate)
        {
            int year = Math.Max(1, systemDate.Year - 1949);
            // Shifting the year can turn February 29 into an invalid date.
            int day = Math.Min(systemDate.Day, DateTime.DaysInMonth(year, systemDate.Month));
            return new DateTime(year, systemDate.Month, day, 8, 0, 0, DateTimeKind.Unspecified);
        }

        public DateTime GameDateTime
        {
            get
            {
                if (ElapsedGameHours >= MaximumElapsedGameHours) return DateTime.MaxValue;
                long elapsedTicks = (long)Math.Round(ElapsedGameHours * TimeSpan.TicksPerHour);
                // The double-to-ticks conversion can round a few ticks above
                // the supported calendar range at extremely large save values.
                long ticks = GameClockOrigin.Ticks + Math.Min(
                    DateTime.MaxValue.Ticks - GameClockOrigin.Ticks, Math.Max(0L, elapsedTicks));
                return new DateTime(ticks, DateTimeKind.Unspecified);
            }
        }

        /// <summary>Restores persisted campaign time while preparing at port.</summary>
        public bool RestoreGameClock(double elapsedHours)
        {
            if (Phase != CoreDemoPhase.Port || !IsValidGameDuration(elapsedHours)) return false;
            ElapsedGameHours = Math.Min(MaximumElapsedGameHours, elapsedHours);
            return true;
        }

        private static bool IsValidGameDuration(double hours) =>
            !double.IsNaN(hours) && !double.IsInfinity(hours) && hours >= 0d;

        private void AdvanceGameClock(double hours)
        {
            if (!IsValidGameDuration(hours)) throw new ArgumentOutOfRangeException(nameof(hours));
            // Compare before adding so even a finite but oversized duration
            // cannot overflow or wrap the clock back to the first month.
            ElapsedGameHours = hours >= MaximumElapsedGameHours - ElapsedGameHours
                ? MaximumElapsedGameHours
                : ElapsedGameHours + hours;
        }
    }
}
