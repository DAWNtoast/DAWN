using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreGameCalendarTests
    {
        [Test]
        public void UninitializedCalendar_UsesStableSaveEpoch()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.ElapsedGameHours, Is.Zero);
            Assert.That(session.GameDateTime, Is.EqualTo(new DateTime(1, 1, 1, 8, 0, 0)));
            Assert.That(session.GameDateTime.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        }

        [TestCase(2026, 10, 7, 77, 10, 7)]
        [TestCase(2027, 1, 1, 78, 1, 1)]
        [TestCase(2024, 2, 29, 75, 2, 28)]
        [TestCase(2000, 2, 29, 51, 2, 28)]
        [TestCase(1953, 2, 28, 4, 2, 28)]
        [TestCase(1949, 12, 31, 1, 12, 31)]
        [TestCase(1948, 2, 29, 1, 2, 28)]
        [TestCase(1, 1, 1, 1, 1, 1)]
        [TestCase(9999, 12, 31, 8050, 12, 31)]
        public void NewCampaignDate_UsesSystemDateWithYearOffsetAndValidCalendarDay(
            int systemYear, int systemMonth, int systemDay, int gameYear, int gameMonth, int gameDay)
        {
            var systemDate = new DateTime(systemYear, systemMonth, systemDay, 23, 59, 48);
            DateTime start = CoreVoyageSession.NewCampaignStartDate(systemDate);

            Assert.That(start, Is.EqualTo(new DateTime(gameYear, gameMonth, gameDay, 8, 0, 0)));
            Assert.That(start.Kind, Is.EqualTo(DateTimeKind.Unspecified));
            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreGameClock((start - CoreVoyageSession.GameClockOrigin).TotalHours), Is.True);
            Assert.That(restored.GameDateTime, Is.EqualTo(start), "The mapped date must round-trip through the existing save epoch.");
        }

        [TestCase(DateTimeKind.Local)]
        [TestCase(DateTimeKind.Utc)]
        [TestCase(DateTimeKind.Unspecified)]
        public void NewCampaignDate_UsesSuppliedCalendarComponentsWithoutTimezoneConversion(DateTimeKind kind)
        {
            var systemDate = new DateTime(2026, 1, 1, 0, 0, 0, kind);
            DateTime start = CoreVoyageSession.NewCampaignStartDate(systemDate);

            Assert.That(start, Is.EqualTo(new DateTime(77, 1, 1, 8, 0, 0)));
            Assert.That(start.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        }

        [Test]
        public void TwoVoyages_AccumulateCampaignTimeWhileVoyageDurationResets()
        {
            var session = new CoreVoyageSession();
            StartTutorialVoyage(session);
            SailToFirstEncounter(session);
            Assert.That(session.TotalHours, Is.GreaterThan(0d));
            double firstSailingHours = session.TotalHours;
            double eventHours = session.ActiveEncounter.Choices[0].Hours;
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.ElapsedGameHours, Is.EqualTo(firstSailingHours + eventHours).Within(1e-9d));
            Assert.That(session.SafeReturn(), Is.True);
            double firstVoyageHours = session.ElapsedGameHours;
            Assert.That(session.Settlement.Hours, Is.EqualTo(firstVoyageHours));
            session.ReturnToPortAfterSettlement();
            session.ReturnToPortAfterSettlement();
            Assert.That(session.ElapsedGameHours, Is.EqualTo(firstVoyageHours));

            StartTutorialVoyage(session);
            Assert.That(session.TotalHours, Is.Zero);
            Assert.That(session.ElapsedGameHours, Is.EqualTo(firstVoyageHours));
            SailToFirstEncounter(session);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.ElapsedGameHours, Is.EqualTo(firstVoyageHours + session.TotalHours).Within(1e-9d));
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Hours, Is.EqualTo(session.TotalHours));
            session.ReturnToPortAfterSettlement();

            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreGameClock(session.ElapsedGameHours), Is.True);
            Assert.That(restored.GameDateTime, Is.EqualTo(session.GameDateTime));
            Assert.That(restored.TotalHours, Is.Zero, "Loading the campaign clock does not fabricate a new voyage duration.");
        }

        [TestCase(1, 1, 31, 1, 2, 1)]
        [TestCase(1, 2, 28, 1, 3, 1)]
        [TestCase(4, 2, 28, 4, 2, 29)]
        [TestCase(4, 2, 29, 4, 3, 1)]
        [TestCase(1, 12, 31, 2, 1, 1)]
        public void PaidEncounter_AdvancesAcrossGregorianMonthAndYearBoundaries(
            int year, int month, int day, int nextYear, int nextMonth, int nextDay)
        {
            var session = new CoreVoyageSession();
            DateTime before = new DateTime(year, month, day, 23, 30, 0);
            Assert.That(session.RestoreGameClock((before - CoreVoyageSession.GameClockOrigin).TotalHours), Is.True);
            StartTutorialVoyage(session);
            EnterAuthoredEncounter(session, CoreGuidedTutorialContent.FirstEventId);
            Assert.That(session.ActiveEncounter.Choices[0].Hours, Is.EqualTo(1d));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.GameDateTime, Is.EqualTo(new DateTime(nextYear, nextMonth, nextDay, 0, 30, 0)));
            Assert.That(session.TotalHours, Is.EqualTo(1d));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidRestore_LeavesExistingCalendarIntact(double invalidHours)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RestoreGameClock(1234.5d), Is.True);
            DateTime before = session.GameDateTime;
            Assert.That(session.RestoreGameClock(invalidHours), Is.False);
            Assert.That(session.ElapsedGameHours, Is.EqualTo(1234.5d));
            Assert.That(session.GameDateTime, Is.EqualTo(before));
        }

        [Test]
        public void RestoreDuringVoyage_IsRejected()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RestoreGameClock(120d), Is.True);
            StartTutorialVoyage(session);
            Assert.That(session.RestoreGameClock(0d), Is.False);
            Assert.That(session.ElapsedGameHours, Is.EqualTo(120d));
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidEncounterDuration_CannotPoisonCalendarOrConsumeCargo(double invalidHours)
        {
            var session = new CoreVoyageSession();
            StartTutorialVoyage(session);
            var encounter = new CoreEncounter("invalid_calendar_duration", "无效事件", "测试", "", true, false, 0, 0,
                new CoreEncounterChoice("无效时间", "", invalidHours, new VoyageEffect()));
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            int water = session.Cargo.Water;
            int food = session.Cargo.Food;
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.ElapsedGameHours, Is.Zero);
            Assert.That(session.GameDateTime, Is.EqualTo(CoreVoyageSession.GameClockOrigin));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
        }

        [Test]
        public void RejectedAndSkippedActions_DoNotAdvanceCalendar()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.Sail(), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.ElapsedGameHours, Is.Zero);
            StartTutorialVoyage(session);
            session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.Sail(), Is.False);
            EnterAuthoredEncounter(session, CoreGuidedTutorialContent.FirstEventId);
            Assert.That(session.ResolveChoice(0), Is.False, "An unaffordable duration must not advance the calendar.");
            Assert.That(session.SkipEncounter(), Is.False);
            Assert.That(session.ResolveChoice(-1), Is.False);
            Assert.That(session.ElapsedGameHours, Is.Zero);

            EnterAuthoredEncounter(session, CoreGuidedTutorialContent.OptionalEventId);
            Assert.That(session.SkipEncounter(), Is.True);
            Assert.That(session.ElapsedGameHours, Is.Zero, "Skipping an encounter remains a zero-time action.");
            Assert.That(session.TotalHours, Is.Zero);
        }

        [Test]
        public void BattleRetreat_AdvancesCampaignClockByItsPaidHour()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.StartVoyage();
            // Navigation is outside this one-hour accounting test. A normal
            // battle retains retreat; the fixed tutorial boss teaches ramming.
            CoreEncounter encounter = session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_coral_ambush").Encounter;
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { encounter, false });
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.ElapsedGameHours, Is.Zero, "Starting the battle fixture costs no voyage time.");
            Assert.That(session.RetreatFromBattle(), Is.True);
            Assert.That(session.ElapsedGameHours, Is.EqualTo(1d));
            Assert.That(session.GameDateTime, Is.EqualTo(CoreVoyageSession.GameClockOrigin.AddHours(1d)));
        }

        [Test]
        public void CalendarLimit_SaturatesWithoutOverflowOrWrapping()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.RestoreGameClock(CoreVoyageSession.MaximumElapsedGameHours - 0.5d), Is.True);
            StartTutorialVoyage(session);
            EnterAuthoredEncounter(session, CoreGuidedTutorialContent.FirstEventId);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.GameDateTime, Is.EqualTo(DateTime.MaxValue));
            Assert.That(session.ElapsedGameHours, Is.EqualTo(CoreVoyageSession.MaximumElapsedGameHours));

            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreGameClock(double.MaxValue), Is.True);
            Assert.That(restored.GameDateTime, Is.EqualTo(DateTime.MaxValue));
        }

        private static void StartTutorialVoyage(CoreVoyageSession session)
        {
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            Assert.That(session.ConfigureGuidedTutorialVoyage(1), Is.True);
            session.StartVoyage();
        }

        private static void SailToFirstEncounter(CoreVoyageSession session)
        {
            session.SetHeading(SailingDirection.East);
            session.SetDistance(CoreGuidedTutorialContent.PlannedDistanceNm);
            for (int attempt = 0; attempt < 3 && session.Phase == CoreDemoPhase.Sailing; attempt++)
                Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter?.Id, Is.EqualTo(CoreGuidedTutorialContent.FirstEventId));
        }

        // Boundary fixtures enter real authored events directly so the exact
        // one-hour cost is isolated from navigation and random sea conditions.
        private static void EnterAuthoredEncounter(CoreVoyageSession session, string id)
        {
            CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id == id);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
        }
    }
}
