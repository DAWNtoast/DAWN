using System;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSailingDurationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(8)]
        public void DefaultAndMaximumUseTwelveAndTwentyFourHoursOfTheSelectedShipsRealSpeed(int ship)
        {
            CoreVoyageSession session = Voyage(ship);
            session.SetCourseTo(new VoyagePoint(85f, 32f));
            float speed = session.EffectiveSpeedKnots;
            Assert.That(session.UsesDefaultDistance, Is.True);
            Assert.That(session.CurrentSailingSpeedKnots, Is.EqualTo(speed).Within(.00001f));
            Assert.That(session.MaximumDistanceNm, Is.EqualTo((int)Math.Floor(speed * 24d)));
            Assert.That(session.DefaultDistanceNm, Is.EqualTo((int)Math.Round(speed * 12d)));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.DefaultDistanceNm));
            SailingForecast forecast = session.GetForecast();
            Assert.That(forecast.ActualDistanceNm, Is.EqualTo(session.DefaultDistanceNm).Within(.001f));
            Assert.That(forecast.Hours, Is.EqualTo(12d).Within(.5d / speed + .00001d));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.TotalHours, Is.EqualTo(forecast.Hours).Within(.000001d));
            Assert.That(session.HasDirectCourseTarget, Is.True);
        }

        [Test]
        public void SharedSpeedIncludesCrewComponentsNavigatorWindCurrentAndHullDamage()
        {
            CoreVoyageSession session = Voyage(0, ordinary: 4, componentSpeed: 3f, navigator: true);
            session.ConfigureComponentContextEffect(0, 0, 0, 2f, 0f, 1f);
            SetProperty(session, "Hull", 30);
            SetSea(session, 8f, 2f);
            session.SetCourseTo(new VoyagePoint(85f, 32f));
            float expected = Math.Max(2.5f, session.EffectiveSpeedKnots + 1f + 2f * .72f + 8f * .28f - (1f - 30f / session.MaxHull) * 3.5f);
            SailingForecast forecast = session.GetForecast();
            Assert.That(forecast.EffectiveSpeedKnots, Is.EqualTo(expected).Within(.00001f));
            Assert.That(session.CurrentSailingSpeedKnots, Is.EqualTo(forecast.EffectiveSpeedKnots));
            Assert.That(session.MaximumDistanceNm, Is.EqualTo((int)Math.Floor(forecast.EffectiveSpeedKnots * 24d)));
            Assert.That(session.DefaultDistanceNm, Is.EqualTo((int)Math.Round(forecast.EffectiveSpeedKnots * 12d)));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.TotalHours, Is.EqualTo(forecast.Hours).Within(.000001d));
        }

        [Test]
        public void DefaultRecomputesAfterTurningAndHullDamageWithoutGetterMutations()
        {
            CoreVoyageSession session = Voyage();
            SetProperty(session, "Position", new VoyagePoint(50f, 32f));
            SetSea(session, 10f, 1f);
            session.SetHeading(SailingDirection.East);
            int favorable = session.PlannedDistanceNm;
            session.SetHeading(SailingDirection.West);
            Assert.That(session.UsesDefaultDistance, Is.True);
            Assert.That(session.PlannedDistanceNm, Is.LessThan(favorable));
            int beforeDamage = session.PlannedDistanceNm;
            SetProperty(session, "Hull", 5);
            Assert.That(session.PlannedDistanceNm, Is.LessThan(beforeDamage));
            int requested = GetRequested(session);
            VoyagePoint position = session.Position;
            SailingDirection heading = session.Heading;
            int distance = session.PlannedDistanceNm;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(session.GetForecast().PlannedDistanceNm, Is.EqualTo(distance));
                Assert.That(session.CurrentSailingSpeedKnots, Is.EqualTo(session.GetForecast().EffectiveSpeedKnots));
                Assert.That(session.DefaultDistanceNm, Is.EqualTo(distance));
            }
            Assert.That(GetRequested(session), Is.EqualTo(requested));
            Assert.That(session.Heading, Is.EqualTo(heading));
            Assert.That(session.Position.X, Is.EqualTo(position.X));
            Assert.That(session.Position.Y, Is.EqualTo(position.Y));
        }

        [Test]
        public void ManualBudgetUsesOneMileStepsAndRetainsPreferenceWhenSpeedDropsAndRecovers()
        {
            CoreVoyageSession session = Voyage();
            SetProperty(session, "Position", new VoyagePoint(50f, 32f));
            SetSea(session, 15f, 2f);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(401);
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(401));
            session.SetHeading(SailingDirection.West);
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.MaximumDistanceNm).And.LessThan(401));
            Assert.That(GetRequested(session), Is.EqualTo(401));
            session.SetHeading(SailingDirection.East);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(401));
            session.SetDistance(137);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(137));
        }

        [Test]
        public void NewTargetPreservesSelectedDistanceWhileNearTargetEndsAtItsExactPoint()
        {
            CoreVoyageSession session = Voyage();
            session.SetDistance(47);
            session.SetCourseTo(new VoyagePoint(85f, 32f));
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(47));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(47));
            session.SetDistance(81);
            var close = new VoyagePoint(10.5f, 32f);
            session.SetCourseTo(close);
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(81));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(81));
            Assert.That(session.GetForecast().Target.X, Is.EqualTo(close.X));
            Assert.That(session.GetForecast().Hours, Is.LessThan(12d));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(81));
        }

        [Test]
        public void SwitchingToAgainstWindTargetKeepsSelectionWhileTheLegRespectsItsDayLimit()
        {
            CoreVoyageSession session = Voyage();
            SetProperty(session, "Position", new VoyagePoint(50f, 32f));
            SetSea(session, 15f, 2f);
            var east = new VoyagePoint(85f, 32f);
            var west = new VoyagePoint(10f, 32f);
            session.SetCourseTo(east);
            session.SetDistance(401);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(401));
            session.SetCourseTo(west);
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(401));
            Assert.That(session.MaximumDistanceNm, Is.LessThan(401));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.MaximumDistanceNm));
            Assert.That(session.GetForecast().Hours, Is.LessThanOrEqualTo(24d));
            Assert.That(session.TotalDistanceNm, Is.Zero, "Switching targets only replans the course.");
            session.SetCourseTo(east);
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(401));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(401));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SlowShipIslandDetourNeverExceedsTwentyFourHoursAndUsesTheFirstNavigableHeading(bool direct)
        {
            CoreVoyageSession session = Voyage(0, ordinary: 5, componentSpeed: -20f);
            SetProperty(session, "Position", new VoyagePoint(28.8f, 32f));
            for (int row = 11; row <= 19; row++)
                session.Map.TerrainCells[session.Map.CellIndex(14, row)] = SeaTerrainType.Island;
            SetSea(session, 3f, 0f);
            if (direct) session.SetCourseTo(new VoyagePoint(50f, 32f));
            else session.SetHeading(SailingDirection.East);
            session.SetDistance(int.MaxValue);
            SailingForecast forecast = session.GetForecast();
            VoyagePoint a = forecast.PathPoints[0], b = forecast.PathPoints[1];
            double angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
            float expected = Math.Max(2.5f, session.EffectiveSpeedKnots + 3f * .28f * (float)Math.Cos(angle));
            Assert.That(forecast.EffectiveSpeedKnots, Is.EqualTo(expected).Within(.00001f));
            Assert.That(forecast.Hours, Is.LessThanOrEqualTo(24d));
            Assert.That(forecast.ActualDistanceNm, Is.LessThanOrEqualTo(session.MaximumDistanceNm + .001f));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.TotalHours, Is.EqualTo(forecast.Hours).Within(.000001d).And.LessThanOrEqualTo(24d));
        }

        [Test]
        public void SideOffsetEncounterInterruptionUsesThePlannedSpeedInsteadOfReprojectingWind()
        {
            CoreVoyageSession session = Voyage();
            SetSea(session, 10f, 2f);
            session.SetCourseTo(new VoyagePoint(70f, 32f));
            session.SetDistance(session.MaximumDistanceNm);
            SailingForecast planned = session.GetForecast();
            var node = new CoreMapNode { Id = 200, Position = new VoyagePoint(12f, 33f),
                Encounter = new CoreEncounter("duration-interruption", "Interruption", "Event", "", true, false, 1, 0,
                    new CoreEncounterChoice("Continue", "Continue", 0, new VoyageEffect())) };
            session.Map.Nodes.Add(node);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter, Is.SameAs(node.Encounter));
            Assert.That(session.TotalDistanceNm, Is.EqualTo(2f * session.Map.Route.NauticalMilesPerMapUnit).Within(.001f));
            Assert.That(session.TotalHours, Is.EqualTo(session.TotalDistanceNm / (double)planned.EffectiveSpeedKnots).Within(.000001d));
            Assert.That(session.HasDirectCourseTarget, Is.True);
            Assert.That(session.ResolveChoice(0), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InterruptedLegRetainsSelectedDistanceWhenTheEventChangesTheDayLimit(bool skip)
        {
            CoreVoyageSession session = Voyage();
            var target = new VoyagePoint(85f, 32f);
            session.SetCourseTo(target);
            int selected = session.MaximumDistanceNm;
            session.SetDistance(selected);
            var node = new CoreMapNode { Id = 201, Position = new VoyagePoint(12f, 32f),
                Encounter = new CoreEncounter("distance-storm", "Storm", "Event", "", false, false, 1, 0,
                    new CoreEncounterChoice("Continue", "Continue", 0, new VoyageEffect { Hull = -20 })) };
            session.Map.Nodes.Add(node);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.TotalDistanceNm, Is.LessThan(selected));
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(selected));
            Assert.That(session.UsesDefaultDistance, Is.False);
            if (skip) session.SkipEncounter();
            else Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(selected));
            Assert.That(session.CaptureVoyageCheckpoint().plannedDistance, Is.EqualTo(selected));
            if (!skip)
            {
                Assert.That(session.MaximumDistanceNm, Is.LessThan(selected));
                Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.MaximumDistanceNm));
            }
            // The common "continue toward that event" click must not reset the slider.
            session.SetCourseTo(target);
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(selected));
            Assert.That(session.HasDirectCourseTarget, Is.True);
            SailingForecast next = session.GetForecast();
            Assert.That(next.Hours, Is.LessThanOrEqualTo(24d));
            float beforeDistance = session.TotalDistanceNm;
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.TotalDistanceNm - beforeDistance, Is.EqualTo(next.ActualDistanceNm).Within(.001f));
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(selected));
        }

        [Test]
        public void ReopeningNearbyDeferredEncounterDoesNotResetDistanceSelection()
        {
            CoreVoyageSession session = Voyage();
            session.SetDistance(137);
            var node = new CoreMapNode { Id = 202, Position = session.Position, Skipped = true,
                Encounter = new CoreEncounter("distance-deferred", "Deferred", "Event", "", false, false, 1, 0,
                    new CoreEncounterChoice("Continue", "Continue", 0, new VoyageEffect(), requiredDiscovery: "reef_manifest")) };
            session.Map.Nodes.Add(node);
            session.SetCourseTo(node.Position);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(137));
            Assert.That(session.UsesDefaultDistance, Is.False);
            session.SkipEncounter();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(137));
            Assert.That(session.UsesDefaultDistance, Is.False);
        }

        [Test]
        public void ManualDockingExtensionAlsoStopsAtTwentyFourHoursBeforeCompletingNextLeg()
        {
            CoreVoyageSession session = Voyage(0, ordinary: 5, componentSpeed: -20f);
            session.Map.Destination = new VoyagePoint(13f, 33.5f);
            SetField(session, "bossDefeated", true);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(int.MaxValue);
            SailingForecast forecast = session.GetForecast();
            Assert.That(forecast.Hours, Is.LessThanOrEqualTo(24d));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.TotalHours, Is.LessThanOrEqualTo(24d));
            int furtherLegs = 0;
            while (session.Phase == CoreDemoPhase.Sailing && furtherLegs++ < 3)
            {
                double before = session.TotalHours;
                Assert.That(session.GetForecast().Hours, Is.LessThanOrEqualTo(24d));
                Assert.That(session.Sail(), Is.True);
                Assert.That(session.TotalHours - before, Is.LessThanOrEqualTo(24d));
            }
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
            Assert.That(session.Position.X, Is.EqualTo(session.Map.Destination.X).Within(.0001f));
            Assert.That(session.Position.Y, Is.EqualTo(session.Map.Destination.Y).Within(.0001f));
        }

        [Test]
        public void ExtremeRequestsAndFiniteSpeedsDoNotOverflowIntegerDistanceBounds()
        {
            CoreVoyageSession session = Voyage(0, componentSpeed: 1e20f);
            session.SetCourseTo(new VoyagePoint(70f, 32f));
            Assert.That(session.MaximumDistanceNm, Is.EqualTo(int.MaxValue));
            Assert.That(session.DefaultDistanceNm, Is.EqualTo(int.MaxValue));
            session.SetDistance(int.MaxValue);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(int.MaxValue));
            Assert.That(session.GetForecast().Hours, Is.GreaterThanOrEqualTo(0d).And.LessThanOrEqualTo(24d));
            session.SetDistance(int.MinValue);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(CoreVoyageSession.MinimumDistanceNm));
        }

        [Test]
        public void NavigableLineRejectsCornerCuttingAndBlockedBoundaryEndpoints()
        {
            CoreVoyageSession session = Voyage();
            session.Map.TerrainCells[session.Map.CellIndex(24, 17)] = SeaTerrainType.Island;
            Assert.That(NavigableLine(session, new VoyagePoint(49f, 36.5f), new VoyagePoint(51f, 38.5f)), Is.False,
                "A line through the exact grid corner must check both side cells.");
            Assert.That(NavigableLine(session, new VoyagePoint(48.5f, 36.5f), new VoyagePoint(49.5f, 37.4f)), Is.True,
                "Two passable points inside the same cell need no boundary crossing.");
            Assert.That(NavigableLine(session, new VoyagePoint(49f, 37f), new VoyagePoint(50f, 37f)), Is.False,
                "An endpoint on the island's left boundary belongs to the blocked cell.");
            Assert.That(NavigableLine(session, new VoyagePoint(float.NaN, 37f), new VoyagePoint(49f, 37f)), Is.False);
            Assert.That(NavigableLine(session, new VoyagePoint(49f, 37f), new VoyagePoint(float.PositiveInfinity, 37f)), Is.False);
        }

        private static bool NavigableLine(CoreVoyageSession session, VoyagePoint start, VoyagePoint end) =>
            (bool)typeof(CoreVoyageSession).GetMethod("HasNavigableLine", Private).Invoke(session, new object[] { start, end });

        private static CoreVoyageSession Voyage(int ship = 0, int ordinary = -1, float componentSpeed = 0f, bool navigator = false)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.SelectShip(ship), Is.True);
            session.ConfigureCrewCapabilities(navigator, false, false, false, false, false);
            session.ConfigureDepartureCrew(ordinary < 0 ? session.CrewCountValue : ordinary, 0);
            session.ConfigureComponentBonuses(0, 0, 0, componentSpeed, 0f);
            session.SetSeed("SAILING-HOUR-BUDGET");
            session.StartVoyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentVectors, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentStrengths, 0, session.Map.CellCount);
            session.Map.GlobalWindStrength = 0f;
            session.Map.Destination = new VoyagePoint(90f, 65f);
            SetProperty(session, "Position", new VoyagePoint(10f, 32f));
            SetField(session, "randomAmbushOccurred", true);
            session.Cargo.Water = session.Cargo.Food = 100;
            return session;
        }

        private static void SetSea(CoreVoyageSession session, float wind, float current)
        {
            session.Map.GlobalWindVector = new VoyagePoint(1f, 0f);
            session.Map.GlobalWindDirection = SailingDirection.East;
            session.Map.GlobalWindStrength = wind;
            for (int i = 0; i < session.Map.CellCount; i++)
            {
                session.Map.CurrentVectors[i] = new VoyagePoint(1f, 0f);
                session.Map.CurrentStrengths[i] = current;
            }
        }
        private static int GetRequested(CoreVoyageSession session) => (int)typeof(CoreVoyageSession).GetField("requestedDistanceNm", Private).GetValue(session);
        private static void SetProperty(CoreVoyageSession session, string name, object value) => typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);
        private static void SetField(CoreVoyageSession session, string name, object value) => typeof(CoreVoyageSession).GetField(name, Private).SetValue(session, value);
    }
}
