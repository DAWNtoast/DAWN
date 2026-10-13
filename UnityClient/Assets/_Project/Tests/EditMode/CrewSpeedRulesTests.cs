using System;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CrewSpeedRulesTests
    {
        [TestCase(0, 0, 4f, 0f, 9f)]
        [TestCase(0, 4, 0.8f, 0f, 12.2f)]
        [TestCase(0, 5, 0f, 0f, 13f)]
        [TestCase(0, 8, 0f, 0f, 13f)]
        [TestCase(0, 9, 0f, 0.65f, 13.65f)]
        [TestCase(0, 10, 0f, 1.3f, 14.3f)]
        [TestCase(1, 12, 0f, 0f, 12f)]
        [TestCase(1, 13, 0f, 0.075f, 12.075f)]
        [TestCase(1, 16, 0f, 1.2f, 13.2f)]
        [TestCase(2, 17, 0f, 0f, 13f)]
        [TestCase(2, 18, 0f, 0.118181818f, 13.118181818f)]
        [TestCase(7, 22, 0f, 0f, 11f)]
        [TestCase(7, 23, 0f, 0.117857143f, 11.117857143f)]
        [TestCase(7, 28, 0f, 1.1f, 12.1f)]
        public void OrdinaryCountPreservesHalfStaffingPenaltyAndAddsSpeedOnlyAboveEightyPercent(
            int ship, int ordinary, float penalty, float bonus, float effectiveSpeed)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.SelectShip(ship), Is.True);
            Assert.That(session.GetCrewSpeedPenalty(ordinary), Is.EqualTo(penalty).Within(0.00001f));
            Assert.That(session.GetCrewSpeedBonus(ordinary), Is.EqualTo(bonus).Within(0.00001f));
            Assert.That(session.GetEffectiveSpeedKnots(ordinary), Is.EqualTo(effectiveSpeed).Within(0.00001f));
            session.ConfigureDepartureCrew(ordinary, 0);
            Assert.That(session.CrewSpeedPenalty, Is.EqualTo(penalty).Within(0.00001f));
            Assert.That(session.CrewSpeedBonus, Is.EqualTo(bonus).Within(0.00001f));
            Assert.That(session.EffectiveSpeedKnots, Is.EqualTo(effectiveSpeed).Within(0.00001f));
        }

        [TestCase(8, 2, 8, 0f, 13f)]
        [TestCase(9, 1, 9, 0.65f, 13.65f)]
        [TestCase(10, 2, 8, 0f, 13f)]
        [TestCase(0, 10, 0, 0f, 9f)]
        public void SpecialistsOccupyBerthsWithoutContributingToOrdinarySpeed(
            int ordinaryRequest, int specialists, int ordinary, float bonus, float effectiveSpeed)
        {
            var session = new CoreVoyageSession();
            session.ConfigureDepartureCrew(ordinaryRequest, specialists);
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(ordinary));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(specialists));
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(10));
            Assert.That(session.CrewSpeedBonus, Is.EqualTo(bonus).Within(0.00001f));
            Assert.That(session.EffectiveSpeedKnots, Is.EqualTo(effectiveSpeed).Within(0.00001f));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void PreviewClampsInvalidCountsWithoutMutatingTheDepartureFormation(int ship)
        {
            var session = new CoreVoyageSession();
            session.SelectShip(ship);
            session.ConfigureDepartureCrew(4, 2);
            Assert.That(session.GetCrewSpeedPenalty(int.MinValue), Is.EqualTo(session.MinimumOrdinaryCrew * 0.8f));
            Assert.That(session.GetCrewSpeedBonus(int.MinValue), Is.Zero);
            Assert.That(session.GetCrewSpeedPenalty(int.MaxValue), Is.Zero);
            Assert.That(session.GetCrewSpeedBonus(int.MaxValue), Is.EqualTo(session.SpeedKnots * 0.1f).Within(0.00001f));
            Assert.That(session.GetEffectiveSpeedKnots(int.MinValue), Is.GreaterThanOrEqualTo(2.5f));
            Assert.That(session.GetEffectiveSpeedKnots(int.MaxValue), Is.EqualTo(session.SpeedKnots * 1.1f).Within(0.00001f));
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(4));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(2));
        }

        [Test]
        public void ComponentSpeedIsIncludedInTheTenPercentBonusAndShortStaffingStillHasAFloor()
        {
            var session = new CoreVoyageSession();
            session.ConfigureComponentBonuses(0, 0, 0, 3f, 0f);
            Assert.That(session.SpeedKnots, Is.EqualTo(16f));
            Assert.That(session.GetCrewSpeedBonus(10), Is.EqualTo(1.6f).Within(0.00001f));
            Assert.That(session.GetEffectiveSpeedKnots(10), Is.EqualTo(17.6f).Within(0.00001f));
            session.ConfigureComponentBonuses(0, 0, 0, -100f, 0f);
            Assert.That(session.SpeedKnots, Is.EqualTo(2.5f));
            Assert.That(session.GetEffectiveSpeedKnots(0), Is.EqualTo(2.5f));
            Assert.That(session.GetEffectiveSpeedKnots(10), Is.EqualTo(2.75f));
        }

        [TestCase(4, 6, -0.8f)]
        [TestCase(8, 2, 0f)]
        [TestCase(9, 1, 0.65f)]
        [TestCase(10, 0, 1.3f)]
        public void ForecastAndActualSailingUseOrdinarySpeedWhileFullRosterRationsRemainUnchanged(
            int ordinary, int specialists, float speedDelta)
        {
            var baseline = NavigationSession(8, 2);
            var candidate = NavigationSession(ordinary, specialists);
            SailingForecast normal = baseline.GetForecast();
            SailingForecast forecast = candidate.GetForecast();
            Assert.That(normal.Blocked, Is.False);
            Assert.That(forecast.Blocked, Is.False);
            Assert.That(forecast.ActualDistanceNm, Is.EqualTo(normal.ActualDistanceNm).Within(0.00001f));
            Assert.That(forecast.EffectiveSpeedKnots - normal.EffectiveSpeedKnots,
                Is.EqualTo(speedDelta).Within(0.00001f));
            int water = candidate.Cargo.Water, food = candidate.Cargo.Food;
            Assert.That(candidate.Sail(), Is.True);
            Assert.That(candidate.TotalHours, Is.EqualTo(forecast.Hours).Within(0.000001d));
            Assert.That(candidate.TotalDistanceNm, Is.EqualTo(forecast.ActualDistanceNm).Within(0.001f));
            Assert.That(water - candidate.Cargo.Water, Is.EqualTo(forecast.WaterCost));
            Assert.That(food - candidate.Cargo.Food, Is.EqualTo(forecast.FoodCost));
            Assert.That(forecast.WaterCost, Is.EqualTo((int)Math.Ceiling(10d * forecast.Hours / 24d)));
            Assert.That(candidate.Battle, Is.Null);
        }

        private static CoreVoyageSession NavigationSession(int ordinary, int specialists)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.ConfigureDepartureCrew(ordinary, specialists);
            session.SetSeed("NONCOMBAT-SAIL-0");
            session.StartVoyage();
            // Keep real generated terrain and currents; isolate this sail from event stops.
            foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
            session.SetCourseTo(session.Map.Destination);
            return session;
        }
    }
}
