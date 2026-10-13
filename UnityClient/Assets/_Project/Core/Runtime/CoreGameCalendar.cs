using System;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        // Permanent storage epoch: changing it would move dates in existing saves.
        // Only a new campaign derives its starting date from the local system;
        // subsequent time is campaign state advanced by gameplay.
        public static DateTime GameClockOrigin { get; } = new DateTime(1, 1, 1, 8, 0, 0, DateTimeKind.Unspecified);
        private static readonly long GameCalendarCycleTicks = DateTime.MaxValue.Ticks + 1L;
        public static double GameCalendarCycleHours { get; } =
            GameCalendarCycleTicks / (double)TimeSpan.TicksPerHour;

        // Offset from the existing 08:00 save epoch, modulo one calendar cycle.
        // The last eight hours of this range represent year 1, January 1, 00:00–08:00.
        public double ElapsedGameHours { get; private set; }

        public static string FormatGameDate(DateTime date, bool includeTime = false) =>
            date.Year.ToString(CultureInfo.InvariantCulture) +
            date.ToString(includeTime ? "-MM-dd HH:mm" : "-MM-dd", CultureInfo.InvariantCulture);

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
                long elapsedTicks = (long)Math.Round(ElapsedGameHours * TimeSpan.TicksPerHour);
                long ticks = (GameClockOrigin.Ticks + elapsedTicks) % GameCalendarCycleTicks;
                return new DateTime(ticks, DateTimeKind.Unspecified);
            }
        }

        /// <summary>Restores persisted campaign time while preparing at port.</summary>
        public bool RestoreGameClock(double elapsedHours)
        {
            if (Phase != CoreDemoPhase.Port || !IsValidGameDuration(elapsedHours)) return false;
            ElapsedGameHours = elapsedHours % GameCalendarCycleHours;
            return true;
        }

        private static bool IsValidGameDuration(double hours) =>
            !double.IsNaN(hours) && !double.IsInfinity(hours) && hours >= 0d;

        private void AdvanceGameClock(double hours)
        {
            if (!IsValidGameDuration(hours)) throw new ArgumentOutOfRangeException(nameof(hours));
            // Reduce first so even an oversized finite duration cannot overflow.
            // The date wraps at midnight after 9999-12-31, retaining any extra time.
            ElapsedGameHours = (ElapsedGameHours + hours % GameCalendarCycleHours) % GameCalendarCycleHours;
        }
    }
}
