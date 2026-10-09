using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class SupplyEnduranceTests
    {
        [Test]
        public void FreshVoyage_ReportsEachResourceUsingTheSelectedCrew()
        {
            var session = Voyage();
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(8));
            AssertHours(session, 30d, 60d);
        }

        [Test]
        public void PartialHour_ConsumesOneRationButOnlyReducesEnduranceByElapsedTime()
        {
            var session = Voyage();
            Wait(session, 0.5d);
            Assert.That(session.Cargo.Water, Is.EqualTo(9));
            Assert.That(session.Cargo.Food, Is.EqualTo(19));
            Assert.That(session.TotalHours, Is.EqualTo(0.5d));
            AssertHours(session, 29.5d, 59.5d);
        }

        [Test]
        public void EmptyInventory_RetainsPrepaidTimeUntilTheExactRationBoundary()
        {
            var session = Voyage();
            Wait(session, 0.5d);
            session.Cargo.Water = 0;
            session.Cargo.Food = 0;
            AssertHours(session, 2.5d, 2.5d);

            // This is a legal public encounter action despite the empty hold:
            // the first fractional ration has already paid for these hours.
            Wait(session, 2.5d);
            Assert.That(session.TotalHours, Is.EqualTo(3d));
            AssertHours(session, 0d, 0d);
        }

        [Test]
        public void FreshEmptyInventory_HasNoUnpaidTimeToOffer()
        {
            var session = Voyage();
            session.Cargo.Water = 0;
            session.Cargo.Food = 0;
            AssertHours(session, 0d, 0d);
        }

        [TestCase(0.8f, 41.6666666667d, 83.3333333333d)]
        [TestCase(1.2f, 27.7777777778d, 55.5555555556d)]
        public void CookAndComponentEffects_BothChangeResourceEndurance(
            float componentMultiplier, double waterHours, double foodHours)
        {
            var session = Voyage(cook: true);
            session.ConfigureComponentContextEffect(0, 0, 0, 0f, 0f, componentMultiplier);
            AssertHours(session, waterHours, foodHours);
        }

        [Test]
        public void ChangedComponentRate_RevaluesTheUnspentPrepaidFraction()
        {
            var session = Voyage();
            Wait(session, 0.5d);
            session.ConfigureComponentContextEffect(0, 0, 0, 0f, 0f, 0.8f);
            AssertHours(session, 36.875d, 74.375d);

            Wait(session, 1d);
            AssertHours(session, 35.875d, 73.375d);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NoCrew_RequiresNoRationsEvenWithAnEmptyHold(bool empty)
        {
            var session = Voyage(crew: 0);
            if (empty) session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.WaterSupplyHours, Is.EqualTo(double.PositiveInfinity));
            Assert.That(session.FoodSupplyHours, Is.EqualTo(double.PositiveInfinity));
            Wait(session, 24d);
            Assert.That(session.WaterSupplyHours, Is.EqualTo(double.PositiveInfinity));
            Assert.That(session.FoodSupplyHours, Is.EqualTo(double.PositiveInfinity));
        }

        [Test]
        public void Sailing_UpdatesBothEnduranceValuesByTheActualVoyageDuration()
        {
            var session = Voyage();
            session.Cargo.Water = 1000;
            session.Cargo.Food = 2000;
            // Keep generated terrain and currents. Mark authored encounters
            // complete so that the actual sailing action follows its forecast.
            foreach (CoreMapNode node in session.Map.Nodes) node.Visited = true;
            session.SetCourseTo(session.Map.Destination);
            SailingForecast forecast = session.GetForecast();
            Assert.That(forecast.Blocked, Is.False);
            Assert.That(forecast.Hours, Is.GreaterThan(0d));
            double waterBefore = session.WaterSupplyHours;
            double foodBefore = session.FoodSupplyHours;

            Assert.That(session.Sail(), Is.True);
            Assert.That(session.TotalHours, Is.EqualTo(forecast.Hours).Within(0.000001d));
            AssertHours(session, waterBefore - session.TotalHours, foodBefore - session.TotalHours);
        }

        private static CoreVoyageSession Voyage(int crew = 8, bool cook = false)
        {
            var session = new CoreVoyageSession();
            session.ConfigureDepartureCrew(crew, 0);
            session.ConfigureCrewCapabilities(false, false, false, false, cook, false);
            session.SetSeed("NONCOMBAT-SAIL-0");
            session.StartVoyage();
            session.Cargo.Water = 10;
            session.Cargo.Food = 20;
            return session;
        }

        private static void Wait(CoreVoyageSession session, double hours)
        {
            Set(session, "ActiveEncounter", new CoreEncounter("supply-endurance-test", "等待", "事件", "", true, false, 0, 0,
                new CoreEncounterChoice("等待", "消耗航行时间", hours, new VoyageEffect())));
            Set(session, "Phase", CoreDemoPhase.Encounter);
            Assert.That(session.ResolveChoice(0), Is.True);
        }

        private static void AssertHours(CoreVoyageSession session, double water, double food)
        {
            Assert.That(session.WaterSupplyHours, Is.EqualTo(water).Within(0.00001d), "Water endurance");
            Assert.That(session.FoodSupplyHours, Is.EqualTo(food).Within(0.00001d), "Food endurance");
        }

        private static void Set(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetProperty(name, BindingFlags.Instance | BindingFlags.Public).SetValue(session, value);
    }
}
