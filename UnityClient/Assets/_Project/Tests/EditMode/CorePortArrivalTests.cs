using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CorePortArrivalTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ManualApproachWithinPortRadius_ForecastsAndChargesTheFinalDockingLeg(int route)
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(47.8f, 32f), route);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(20);
            int water = session.Cargo.Water;
            int food = session.Cargo.Food;

            SailingForecast forecast = session.GetForecast();

            AssertPoint(forecast.Target, session.Map.Destination);
            AssertPoint(forecast.PathPoints[forecast.PathPoints.Count - 1], session.Map.Destination);
            Assert.That(forecast.ActualDistanceNm, Is.EqualTo(2.2f * session.Map.Route.NauticalMilesPerMapUnit).Within(0.001f));
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
            Assert.That(session.TotalDistanceNm, Is.EqualTo(forecast.ActualDistanceNm).Within(0.001f));
            Assert.That(session.TotalHours, Is.EqualTo(forecast.Hours).Within(0.00001d));
            Assert.That(session.Cargo.Water, Is.EqualTo(water - forecast.WaterCost));
            Assert.That(session.Cargo.Food, Is.EqualTo(food - forecast.FoodCost));
        }

        [Test]
        public void PassingAtTheCaptureRadius_IncludesTheLateralDockingLeg()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(46f, 2f));
            // Subtracting 2 from 3.7 preserves the same float as the 1.7 radius.
            session.Map.Destination = new VoyagePoint(50f, 3.7f);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(180);

            SailingForecast forecast = session.GetForecast();

            AssertPoint(forecast.Target, session.Map.Destination);
            Assert.That(forecast.ActualDistanceNm, Is.EqualTo((4f + 1.7f) * session.Map.Route.NauticalMilesPerMapUnit).Within(0.001f),
                "The final turn into port must count toward travel time and supplies.");
            Assert.That(forecast.Rerouted, Is.False,
                "Turning into a port on open water is not an island detour.");
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
            Assert.That(session.TotalDistanceNm, Is.EqualTo(forecast.ActualDistanceNm).Within(0.001f));
        }

        [Test]
        public void EndingOutsideThePortRadius_DoesNotExtendTheCaptureRange()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(47.5f, 32f));
            session.SetHeading(SailingDirection.East);
            session.SetDistance(20);

            SailingForecast forecast = session.GetForecast();

            Assert.That(forecast.Target.X, Is.EqualTo(47.5f + 20f / session.Map.Route.NauticalMilesPerMapUnit).Within(0.001f));
            Assert.That(session.Map.Destination.X - forecast.Target.X, Is.GreaterThan(1.7f));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Settlement, Is.Null);
        }

        [Test]
        public void AlreadyInsidePortRadius_DocksDespiteAnOutwardHeading()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(49f, 32f));
            session.SetHeading(SailingDirection.West);
            session.SetDistance(180);

            SailingForecast forecast = session.GetForecast();

            AssertPoint(forecast.Target, session.Map.Destination);
            Assert.That(forecast.ActualDistanceNm, Is.EqualTo(session.Map.Route.NauticalMilesPerMapUnit).Within(0.001f));
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
        }

        [Test]
        public void AlreadyAtDestination_DocksWithoutTravelOrSupplies()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(50f, 32f));
            session.Cargo.Water = session.Cargo.Food = 0;
            session.SetHeading(SailingDirection.West);
            session.SetCourseTo(session.Map.Destination);

            SailingForecast forecast = session.GetForecast();

            AssertPoint(forecast.Target, session.Map.Destination);
            Assert.That(forecast.ActualDistanceNm, Is.Zero);
            Assert.That(forecast.Hours, Is.Zero);
            Assert.That(forecast.WaterCost, Is.Zero);
            Assert.That(forecast.FoodCost, Is.Zero);
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
            Assert.That(session.TotalDistanceNm, Is.Zero);
            Assert.That(session.TotalHours, Is.Zero);
        }

        [Test]
        public void DestinationWithinClickDeadZone_CompletesASubNauticalMileFinalLeg()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(49.98f, 32f));
            session.SetHeading(SailingDirection.West);
            int water = session.Cargo.Water;
            session.SetCourseTo(session.Map.Destination);

            Assert.That(session.HasDirectCourseTarget, Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            SailingForecast forecast = session.GetForecast();
            AssertPoint(forecast.Target, session.Map.Destination);
            Assert.That(forecast.ActualDistanceNm, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(forecast.WaterCost, Is.EqualTo(1));
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
            Assert.That(session.TotalDistanceNm, Is.EqualTo(forecast.ActualDistanceNm).Within(0.001f));
            Assert.That(session.Cargo.Water, Is.EqualTo(water - forecast.WaterCost));
        }

        [Test]
        public void TinyClickAwayFromThePort_StillKeepsTheExistingCourse()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(20f, 32f));
            session.SetHeading(SailingDirection.North);
            session.SetCourseTo(new VoyagePoint(20.1f, 32f));

            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(session.Heading, Is.EqualTo(SailingDirection.North));
        }

        [TestCase(46f)]
        [TestCase(49f)]
        [TestCase(50f)]
        public void RouteBossStillAlive_NeverUnlocksAutomaticArrival(float startX)
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(startX, 32f), bossDefeated: false);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(180);

            Assert.That(session.Sail(), Is.True);

            Assert.That(session.BossDefeated, Is.False);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Settlement, Is.Null);
        }

        [Test]
        public void EarlierEncounter_InterruptsBeforePortArrivalAndChargesOnlyReachedDistance()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(44f, 32f));
            var node = new CoreMapNode
            {
                Id = 0,
                Position = new VoyagePoint(46f, 32f),
                Encounter = new CoreEncounter("port-approach-event", "Approach event", "Event", "", true, false, 0, 0,
                    new CoreEncounterChoice("Continue", "Continue sailing", 0d, new VoyageEffect())),
            };
            session.Map.Nodes.Add(node);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(180);

            Assert.That(session.Sail(), Is.True);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter, Is.SameAs(node.Encounter));
            Assert.That(session.Settlement, Is.Null);
            AssertPoint(session.Position, node.Position);
            Assert.That(session.TotalDistanceNm, Is.EqualTo(2f * session.Map.Route.NauticalMilesPerMapUnit).Within(0.001f));
            Assert.That(session.ResolveChoice(0), Is.True);
            session.SetCourseTo(session.Map.Destination);
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
        }

        [TestCase(44f)]
        [TestCase(49f)]
        [TestCase(49.98f)]
        public void InsufficientSupplies_CannotCompleteOrReceiveTravelForTheDockingLeg(float startX)
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(startX, 32f));
            session.Cargo.Water = session.Cargo.Food = 0;
            VoyagePoint start = session.Position;
            session.SetCourseTo(session.Map.Destination);

            Assert.That(session.GetForecast().WaterCost, Is.GreaterThan(0));
            Assert.That(session.Sail(), Is.False);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Settlement, Is.Null);
            AssertPoint(session.Position, start);
            Assert.That(session.TotalDistanceNm, Is.Zero);
            Assert.That(session.TotalHours, Is.Zero);
            Assert.That(session.GetSpecialCardExperience("card_navigator_course"), Is.Zero);
        }

        [Test]
        public void NearbyPortAcrossBlockedTerrain_DoesNotSnapAcrossTheIsland()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(48.6f, 31.6f));
            session.Map.Destination = new VoyagePoint(51.2f, 32.4f);
            session.Map.TerrainCells[session.Map.CellIndex(24, 15)] = SeaTerrainType.Island;
            session.SetHeading(SailingDirection.East);
            session.SetDistance(20);

            SailingForecast forecast = session.GetForecast();

            Assert.That(forecast.Target.X, Is.EqualTo(48.6f + 20f / session.Map.Route.NauticalMilesPerMapUnit).Within(0.001f));
            Assert.That(forecast.Target.Y, Is.EqualTo(31.6f).Within(0.001f));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Settlement, Is.Null);
            AssertPoint(session.Position, forecast.Target);
        }

        [Test]
        public void IslandDetourEndingNearPort_SnapsDespiteFloatRoundingAcrossSegments()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(37.7f, 31.65f));
            session.Map.Destination = new VoyagePoint(44f, 31.65f);
            session.Map.TerrainCells[session.Map.CellIndex(19, 15)] = SeaTerrainType.Island;
            session.SetCourseTo(new VoyagePoint(43.04f, 31.65f));

            SailingForecast forecast = session.GetForecast();

            // This island produces a turn at (40.625, 29). Summing the two
            // segment lengths then subtracting the first leaves a float
            // remainder just larger than the final segment's own length.
            Assert.That(forecast.PathPoints.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(forecast.ActualDistanceNm, Is.LessThanOrEqualTo(session.PlannedDistanceNm + .001f));
            Assert.That(forecast.Hours, Is.LessThanOrEqualTo(24d));
            Assert.That(forecast.Rerouted, Is.True);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.HasDirectCourseTarget, Is.True, "The island detour exceeds one leg's budget.");
            float firstLegDistance = session.TotalDistanceNm;
            SailingForecast finalApproach = session.GetForecast();
            AssertPoint(finalApproach.Target, session.Map.Destination);
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
            Assert.That(session.TotalDistanceNm, Is.EqualTo(firstLegDistance + finalApproach.ActualDistanceNm).Within(0.001f));
        }

        [TestCase(1)]
        [TestCase(CoreVoyageSession.ShipMaxHull)]
        public void DockingBeforeThePlannedBoundary_DoesNotApplyUnreachedBorderDamage(int hull)
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(93f, 32f));
            session.Map.Destination = new VoyagePoint(95f, 32f);
            SetProperty(session, "Hull", hull);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(180);

            SailingForecast forecast = session.GetForecast();

            AssertPoint(forecast.Target, session.Map.Destination);
            Assert.That(forecast.Clipped, Is.False,
                "The original course would touch x=98, but docking stops it at x=95.");
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
            Assert.That(session.Hull, Is.EqualTo(hull),
                "A ship with one hull remaining must arrive safely without contacting the planned border.");
            Assert.That(session.TotalDistanceNm, Is.EqualTo(2f * session.Map.Route.NauticalMilesPerMapUnit).Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PassingDeparturePort_DoesNotAutomaticallyReturn(bool bossDefeated)
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(9f, 32f), bossDefeated: bossDefeated);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(180);

            Assert.That(session.Sail(), Is.True);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Settlement, Is.Null);
            Assert.That(session.Position.X, Is.GreaterThan(session.Map.Departure.X));
        }

        [Test]
        public void DockingSettlesOnlyOnce_AndPortTransitionUsesTheDestination()
        {
            CoreVoyageSession session = Voyage(new VoyagePoint(49f, 32f));
            session.SetCourseTo(session.Map.Destination);
            Assert.That(session.Sail(), Is.True);
            AssertArrived(session);
            VoyageSettlement settlement = session.Settlement;
            float distance = session.TotalDistanceNm;
            double hours = session.TotalHours;
            int water = session.Cargo.Water;
            int gold = session.Cargo.Gold;
            int experience = session.GetSpecialCardExperience("card_navigator_course");
            string destination = session.Map.DestinationName;
            Assert.That(experience, Is.GreaterThan(0));

            Assert.That(session.Sail(), Is.False);

            Assert.That(session.Settlement, Is.SameAs(settlement));
            Assert.That(session.TotalDistanceNm, Is.EqualTo(distance));
            Assert.That(session.TotalHours, Is.EqualTo(hours));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(session.GetSpecialCardExperience("card_navigator_course"), Is.EqualTo(experience));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.CurrentPort, Is.EqualTo(destination));
        }

        private static CoreVoyageSession Voyage(VoyagePoint position, int route = 0, bool bossDefeated = true)
        {
            var session = new CoreVoyageSession();
            session.SelectRoute(route);
            session.SetSeed("PORT-ARRIVAL-REGRESSION");
            session.StartVoyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentVectors, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentStrengths, 0, session.Map.CellCount);
            session.Map.GlobalWindStrength = 0f;
            session.Map.Departure = new VoyagePoint(10f, 32f);
            session.Map.Destination = new VoyagePoint(50f, 32f);
            SetProperty(session, "Position", position);
            SetField(session, "bossDefeated", bossDefeated);
            SetField(session, "randomAmbushOccurred", true);
            return session;
        }

        private static void AssertArrived(CoreVoyageSession session)
        {
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement, Is.Not.Null);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
            Assert.That(session.Settlement.Destination, Is.EqualTo(session.Map.DestinationName));
            AssertPoint(session.Position, session.Map.Destination);
        }

        private static void AssertPoint(VoyagePoint actual, VoyagePoint expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.0001f));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.0001f));
        }

        private static void SetProperty(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);

        private static void SetField(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);
    }
}
