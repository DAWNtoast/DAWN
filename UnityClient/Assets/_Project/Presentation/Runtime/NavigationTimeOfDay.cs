using System;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public enum NavigationTimeOfDay
    {
        Day,
        Dusk,
        Night,
    }

    /// <summary>
    /// Converts elapsed voyage time into the lighting state shown in the forward lookout.
    /// A new voyage starts at 08:00 so the first preview is readable in daylight, then
    /// moves through dusk and night as travel and encounters consume hours.
    /// </summary>
    public static class NavigationTimeOfDayUtility
    {
        public const double VoyageStartHour = 8d;
        public const double HoursPerDay = 24d;
        public const double DuskStartHour = 17d;
        public const double NightStartHour = 20d;
        public const double DayStartHour = 6d;

        public static double LocalHour(double elapsedHours)
        {
            if (double.IsNaN(elapsedHours) || double.IsInfinity(elapsedHours))
            {
                return VoyageStartHour;
            }

            return (VoyageStartHour + Math.Max(0d, elapsedHours)) % HoursPerDay;
        }

        public static int VoyageDay(double elapsedHours)
        {
            if (double.IsNaN(elapsedHours) || double.IsInfinity(elapsedHours)) return 1;
            return (int)Math.Floor(Math.Max(0d, elapsedHours) / HoursPerDay) + 1;
        }

        public static string ClockLabel(double elapsedHours)
        {
            double localHour = LocalHour(elapsedHours);
            int hour = (int)Math.Floor(localHour);
            int minute = (int)Math.Round((localHour - hour) * 60d);
            if (minute >= 60)
            {
                hour = (hour + 1) % 24;
                minute = 0;
            }

            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        public static string Readout(double elapsedHours)
        {
            NavigationTimeOfDay timeOfDay = FromVoyageHours(elapsedHours);
            return "航行第 " + VoyageDay(elapsedHours) + " 日　" + ClockLabel(elapsedHours) + "　·　" + Label(timeOfDay);
        }

        public static float CycleProgress(double elapsedHours)
        {
            return (float)(LocalHour(elapsedHours) / HoursPerDay);
        }

        public static NavigationTimeOfDay FromVoyageHours(double elapsedHours)
        {
            if (double.IsNaN(elapsedHours) || double.IsInfinity(elapsedHours))
            {
                return NavigationTimeOfDay.Day;
            }

            double localHour = LocalHour(elapsedHours);
            if (localHour >= DuskStartHour && localHour < NightStartHour)
            {
                return NavigationTimeOfDay.Dusk;
            }

            if (localHour >= NightStartHour || localHour < DayStartHour)
            {
                return NavigationTimeOfDay.Night;
            }

            return NavigationTimeOfDay.Day;
        }

        public static string Label(NavigationTimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case NavigationTimeOfDay.Dusk: return "黄昏";
                case NavigationTimeOfDay.Night: return "夜晚";
                default: return "白天";
            }
        }

        public static Color OverlayColor(NavigationTimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case NavigationTimeOfDay.Dusk:
                    return new Color(0.88f, 0.33f, 0.12f, 0.23f);
                case NavigationTimeOfDay.Night:
                    return new Color(0.035f, 0.075f, 0.24f, 0.56f);
                default:
                    return new Color(1f, 1f, 1f, 0f);
            }
        }

        public static Color AccentColor(NavigationTimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case NavigationTimeOfDay.Dusk: return new Color(1f, 0.67f, 0.30f, 0.95f);
                case NavigationTimeOfDay.Night: return new Color(0.74f, 0.84f, 1f, 0.95f);
                default: return new Color(1f, 0.92f, 0.62f, 0.95f);
            }
        }
    }
}
