using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class AchievementProgressTests
    {
        [Test]
        public void VoyageThreeEvents_RecordIsPermanentAndIdempotent()
        {
            string id = CoreAchievementCatalog.VoyageThreeEventsId;
            Assert.That(Unlocked(id), Is.False);
            AchievementCatalog.RecordVoyageThreeEvents();
            AchievementCatalog.RecordVoyageThreeEvents();
            AchievementCatalog.CaptureProgress();
            Assert.That(Unlocked(id), Is.True);
            Assert.That(AchievementCatalog.CountUnlocked(), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("ShatteredSail.Achievements.Unlocked.v1." + id), Is.EqualTo(1));
            PlayerPrefs.Save();
            AchievementCatalog.CaptureProgress();
            Assert.That(Unlocked(id), Is.True, "Later captures without an active voyage preserve the confirmed record.");
        }

        [TestCase(CoreAchievementCatalog.VoyageHalfChartId)]
        [TestCase(CoreAchievementCatalog.VoyageEventArrivalId)]
        public void AdditionalVoyageAchievements_RecordIsPermanentAndIdempotent(string id)
        {
            Assert.That(Unlocked(id), Is.False);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                if (id == CoreAchievementCatalog.VoyageHalfChartId) AchievementCatalog.RecordVoyageHalfChart();
                else AchievementCatalog.RecordVoyageEventArrival();
            }
            AchievementCatalog.CaptureProgress();
            Assert.That(Unlocked(id), Is.True);
            Assert.That(AchievementCatalog.CountUnlocked(), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("ShatteredSail.Achievements.Unlocked.v1." + id), Is.EqualTo(1));
            PlayerPrefs.Save();
            AchievementCatalog.CaptureProgress();
            Assert.That(Unlocked(id), Is.True);
            AchievementCatalog.ClearProgress();
            AchievementCatalog.CaptureProgress();
            Assert.That(Unlocked(id), Is.False);
            Assert.That(AchievementCatalog.PersistenceKeys.Any(PlayerPrefs.HasKey), Is.False);
        }

        [Test]
        public void VoyageThreeEvents_OldDispatchRecordsDoNotGrantTheReplacement()
        {
            foreach (string legacyId in new[] { "dispatch_prepare", "dispatch_long", "dispatch_resolved" })
            {
                string key = "ShatteredSail.Achievements.Unlocked.v1." + legacyId;
                Preserve(key, false);
                PlayerPrefs.SetInt(key, 1);
            }
            AchievementCatalog.CaptureProgress();
            foreach (string id in new[] { CoreAchievementCatalog.VoyageThreeEventsId,
                CoreAchievementCatalog.VoyageHalfChartId, CoreAchievementCatalog.VoyageEventArrivalId })
            {
                Assert.That(Unlocked(id), Is.False);
                Assert.That(PlayerPrefs.HasKey("ShatteredSail.Achievements.Unlocked.v1." + id), Is.False);
            }
            Assert.That(AchievementCatalog.CountUnlocked(), Is.Zero);
        }

        [Test]
        public void VoyageThreeEvents_ClearProgressRemovesTheRecordWithoutRecreatingIt()
        {
            AchievementCatalog.RecordVoyageThreeEvents();
            Assert.That(Unlocked(CoreAchievementCatalog.VoyageThreeEventsId), Is.True);
            AchievementCatalog.ClearProgress();
            AchievementCatalog.CaptureProgress();
            Assert.That(Unlocked(CoreAchievementCatalog.VoyageThreeEventsId), Is.False);
            Assert.That(AchievementCatalog.PersistenceKeys.Any(PlayerPrefs.HasKey), Is.False);
        }
    }
}
