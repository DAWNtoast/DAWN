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
        public const string DispatchGameClockKey = "ShatteredSail.Dispatch.GameClock.v1";
        public const string DispatchRealClockKey = "ShatteredSail.Dispatch.RealClock.v1";

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

        private long DispatchDeadline(int hours)
        {
            long now = DateTime.UtcNow.Ticks;
            return now + Math.Min(DateTime.MaxValue.Ticks - now, Math.Max(0L, hours) * TimeSpan.TicksPerHour);
        }

        private void MigrateDispatchGameClock()
        {
            int mission = PlayerPrefs.GetInt(DispatchMissionKey, -1);
            if (mission < 0)
            {
                if (!PlayerPrefs.HasKey(DispatchGameClockKey) && !PlayerPrefs.HasKey(DispatchRealClockKey)) return;
                PlayerPrefs.DeleteKey(DispatchGameClockKey);
                PlayerPrefs.DeleteKey(DispatchRealClockKey);
                PlayerPrefs.Save();
                return;
            }
            if (PlayerPrefs.GetInt(DispatchRealClockKey, 0) == 1)
            {
                if (!PlayerPrefs.HasKey(DispatchGameClockKey)) return;
                PlayerPrefs.DeleteKey(DispatchGameClockKey);
                PlayerPrefs.Save();
                return;
            }
            if (PlayerPrefs.GetInt(DispatchGameClockKey, 0) == 1 &&
                CoreEconomyBalance.IsValidDispatchRecord(mission,
                PlayerPrefs.GetString(DispatchEndsAtKey, "0"), out long end))
            {
                int duration = Mathf.Clamp(PlayerPrefs.GetInt(DispatchDurationHoursKey, DispatchDurations[mission]),
                    DispatchDurations[mission], DispatchDurations[mission] * 2);
                // Preserve the remaining time of a game-clock job exactly once.
                // The saved game calendar is only read; dispatch never advances it.
                long remaining = Math.Min(duration * TimeSpan.TicksPerHour,
                    Math.Max(0L, end - session.GameDateTime.Ticks));
                long now = DateTime.UtcNow.Ticks;
                long translated = now + Math.Min(remaining, DateTime.MaxValue.Ticks - now);
                PlayerPrefs.SetString(DispatchEndsAtKey, translated.ToString(CultureInfo.InvariantCulture));
            }
            // Original UTC jobs keep their exact deadline, including completed
            // offline jobs. Never interpret their timestamp as game-calendar time.
            // Leave malformed records malformed so the existing recovery UI
            // can clear them without granting a reward.
            PlayerPrefs.SetInt(DispatchRealClockKey, 1);
            PlayerPrefs.DeleteKey(DispatchGameClockKey);
            PlayerPrefs.Save();
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
