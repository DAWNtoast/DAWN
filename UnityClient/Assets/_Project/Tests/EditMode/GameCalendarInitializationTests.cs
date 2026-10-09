using System;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class GameCalendarInitializationTests
    {
        private const string ClockKey = CoreGameplayDemoController.GameClockKey;
        private const string ScopeKey = CoreGameplayDemoController.WorldEconomyScopeKey;
        private bool hadClock, hadScope;
        private string savedClock, savedScope;

        [SetUp]
        public void PreserveCalendarPreferences()
        {
            hadClock = PlayerPrefs.HasKey(ClockKey);
            savedClock = PlayerPrefs.GetString(ClockKey);
            hadScope = PlayerPrefs.HasKey(ScopeKey);
            savedScope = PlayerPrefs.GetString(ScopeKey);
            PlayerPrefs.DeleteKey(ClockKey);
            PlayerPrefs.SetString(ScopeKey, "CalendarInitializationTestScope");
        }

        [TearDown]
        public void RestoreCalendarPreferences()
        {
            if (hadClock) PlayerPrefs.SetString(ClockKey, savedClock);
            else PlayerPrefs.DeleteKey(ClockKey);
            if (hadScope) PlayerPrefs.SetString(ScopeKey, savedScope);
            else PlayerPrefs.DeleteKey(ScopeKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void NewGameInitialization_PersistsMappedDateUsingExistingSaveEpoch()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                CoreGameplayDemoController.InitializeNewGameCalendar(new DateTime(2026, 10, 7, 21, 45, 12));

                Assert.That(PlayerPrefs.HasKey(ClockKey), Is.True);
                double elapsed = double.Parse(PlayerPrefs.GetString(ClockKey), CultureInfo.InvariantCulture);
                var expected = new DateTime(77, 10, 7, 8, 0, 0);
                Assert.That(elapsed, Is.EqualTo((expected - CoreVoyageSession.GameClockOrigin).TotalHours));
                Assert.That(CoreGameplayDemoController.SavedGameDateTime(), Is.EqualTo(expected));
                Assert.That(PlayerPrefs.GetString(ScopeKey), Is.EqualTo("CalendarInitializationTestScope"));
            }
            finally { CultureInfo.CurrentCulture = previousCulture; }
        }

        [TestCase("0", 0d)]
        [TestCase("1234.5", 1234.5d)]
        public void SavedGameDateTime_PreservesValidExistingCampaignDates(string record, double hours)
        {
            PlayerPrefs.SetString(ClockKey, record);

            Assert.That(CoreGameplayDemoController.SavedGameDateTime(),
                Is.EqualTo(CoreVoyageSession.GameClockOrigin.AddHours(hours)));
            Assert.That(PlayerPrefs.GetString(ClockKey), Is.EqualTo(record));
        }

        [Test]
        public void StartingAnotherNewGame_ReplacesPriorStartDate()
        {
            CoreGameplayDemoController.InitializeNewGameCalendar(new DateTime(2026, 10, 7));
            Assert.That(CoreGameplayDemoController.SavedGameDateTime(), Is.EqualTo(new DateTime(77, 10, 7, 8, 0, 0)));

            CoreGameplayDemoController.InitializeNewGameCalendar(new DateTime(2028, 2, 29));
            Assert.That(CoreGameplayDemoController.SavedGameDateTime(), Is.EqualTo(new DateTime(79, 2, 28, 8, 0, 0)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("broken")]
        [TestCase("-1")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void SavedGameDateTime_FallsBackToSystemDateWithoutMutatingPreferences(string invalidRecord)
        {
            if (invalidRecord != null) PlayerPrefs.SetString(ClockKey, invalidRecord);
            DateTime before = CoreVoyageSession.NewCampaignStartDate(DateTime.Now);

            DateTime fallback = CoreGameplayDemoController.SavedGameDateTime();

            DateTime after = CoreVoyageSession.NewCampaignStartDate(DateTime.Now);
            Assert.That(fallback == before || fallback == after, Is.True, "The fallback must use the current local date, including a midnight boundary.");
            Assert.That(PlayerPrefs.HasKey(ClockKey), Is.EqualTo(invalidRecord != null));
            if (invalidRecord != null) Assert.That(PlayerPrefs.GetString(ClockKey), Is.EqualTo(invalidRecord));
            Assert.That(PlayerPrefs.GetString(ScopeKey), Is.EqualTo("CalendarInitializationTestScope"));
        }

        [TestCase(null)]
        [TestCase("broken")]
        [TestCase("-1")]
        [TestCase("NaN")]
        public void FirstCalendarRestore_StoresOneStableStartDateForLaterLoads(string invalidRecord)
        {
            if (invalidRecord != null) PlayerPrefs.SetString(ClockKey, invalidRecord);
            var host = new GameObject("Calendar Initialization Test Host");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                var session = new CoreVoyageSession();
                SetSession(controller, session);
                DateTime before = CoreVoyageSession.NewCampaignStartDate(DateTime.Now);

                RestoreCalendar(controller);

                DateTime after = CoreVoyageSession.NewCampaignStartDate(DateTime.Now);
                Assert.That(session.GameDateTime == before || session.GameDateTime == after, Is.True);
                Assert.That(PlayerPrefs.HasKey(ClockKey), Is.True);
                string initializedRecord = PlayerPrefs.GetString(ClockKey);
                Assert.That(CoreGameplayDemoController.SavedGameDateTime(), Is.EqualTo(session.GameDateTime));

                var reloaded = new CoreVoyageSession();
                SetSession(controller, reloaded);
                RestoreCalendar(controller);
                Assert.That(reloaded.GameDateTime, Is.EqualTo(session.GameDateTime));
                Assert.That(PlayerPrefs.GetString(ClockKey), Is.EqualTo(initializedRecord));
                Assert.That(PlayerPrefs.GetString(ScopeKey), Is.EqualTo("CalendarInitializationTestScope"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase("0", 0d)]
        [TestCase("1234.5", 1234.5d)]
        public void RestoringAnExistingGame_PreservesItsOriginalCalendar(string record, double hours)
        {
            PlayerPrefs.SetString(ClockKey, record);
            var host = new GameObject("Existing Calendar Test Host");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                var session = new CoreVoyageSession();
                SetSession(controller, session);

                RestoreCalendar(controller);

                Assert.That(session.GameDateTime, Is.EqualTo(CoreVoyageSession.GameClockOrigin.AddHours(hours)));
                Assert.That(PlayerPrefs.GetString(ClockKey), Is.EqualTo(record));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static void SetSession(CoreGameplayDemoController controller, CoreVoyageSession session)
        {
            typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, session);
        }

        private static void RestoreCalendar(CoreGameplayDemoController controller)
        {
            typeof(CoreGameplayDemoController).GetMethod("RestoreGameCalendar", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
        }
    }
}
