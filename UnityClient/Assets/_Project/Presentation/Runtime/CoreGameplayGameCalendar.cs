using System;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string GameClockKey = "ShatteredSail.GameClockHours.v1";
        public const string WorldEconomyScopeKey = "ShatteredSail.WorldEconomyScope.v1";
        public const string ComponentPurchaseAchievementKey = "ShatteredSail.ComponentPurchaseAchievement.v1";
        // A fresh campaign samples the local date once. Persist it immediately so
        // reopening the game on another day cannot move the campaign calendar.
        private void RestoreGameCalendar()
        {
            bool changed = !RestoreSavedGameCalendar(session);
            if (changed)
                PlayerPrefs.SetString(GameClockKey, session.ElapsedGameHours.ToString("R", CultureInfo.InvariantCulture));
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(WorldEconomyScopeKey, string.Empty)))
            {
                PlayerPrefs.SetString(WorldEconomyScopeKey, Guid.NewGuid().ToString("N"));
                changed = true;
            }
            if (changed) PlayerPrefs.Save();
        }

        public static void InitializeNewGameCalendar(DateTime systemDate)
        {
            double elapsed = (CoreVoyageSession.NewCampaignStartDate(systemDate) - CoreVoyageSession.GameClockOrigin).TotalHours;
            PlayerPrefs.SetString(GameClockKey, elapsed.ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool RestoreSavedGameCalendar(CoreVoyageSession clock)
        {
            if (double.TryParse(PlayerPrefs.GetString(GameClockKey, string.Empty), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double elapsed) && clock.RestoreGameClock(elapsed)) return true;
            clock.RestoreGameClock((CoreVoyageSession.NewCampaignStartDate(DateTime.Now) - CoreVoyageSession.GameClockOrigin).TotalHours);
            return false;
        }

        private void SaveGameCalendar()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null) return;
            // Commit beside cargo and profile at the existing save checkpoint;
            // never persist a newer month against an older, unspent cargo save.
            PlayerPrefs.SetString(GameClockKey, session.ElapsedGameHours.ToString("R", CultureInfo.InvariantCulture));
        }

        public static DateTime SavedGameDateTime()
        {
            var clock = new CoreVoyageSession();
            RestoreSavedGameCalendar(clock);
            return clock.GameDateTime;
        }

        private static string WorldEconomyScope => PlayerPrefs.GetString(WorldEconomyScopeKey, "legacy");
        private static DateTime MarketMonth(DateTime time) => new DateTime(time.Year, time.Month, 1);
    }
}
