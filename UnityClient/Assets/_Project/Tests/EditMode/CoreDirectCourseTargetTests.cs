using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreDirectCourseTargetTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void FarTargetSurvivesEachLegUntilItsPrecisePositionIsReached()
        {
            CoreVoyageSession session = OpenVoyage();
            var target = new VoyagePoint(29f, 35f);
            session.SetCourseTo(target);
            AssertPoint(session.DirectCourseTarget, target);
            int legs = 0;
            while (session.HasDirectCourseTarget && legs++ < 8)
            {
                SailingForecast forecast = session.GetForecast();
                AssertLegWithinBudget(session, forecast);
                Assert.That(session.Sail(), Is.True);
                AssertPoint(session.Position, forecast.Target);
                AssertPoint(session.DirectCourseTarget, target);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                if (Distance(session.Position, target) > 0.0001f)
                    Assert.That(session.HasDirectCourseTarget, Is.True, "A completed leg must not cancel the final target.");
            }
            Assert.That(legs, Is.GreaterThan(1).And.LessThan(8));
            Assert.That(session.HasDirectCourseTarget, Is.False);
            AssertPoint(session.Position, target);
        }

        [Test]
        public void DistanceAdjustmentKeepsTargetAndForecastIsReadOnlyButManualHeadingCancels()
        {
            CoreVoyageSession session = OpenVoyage();
            var target = new VoyagePoint(28f, 36f);
            session.SetCourseTo(target);
            session.SetDistance(60);
            SailingDirection heading = session.Heading;
            SailingForecast forecast = session.GetForecast();
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertPoint(session.DirectCourseTarget, target);
            Assert.That(forecast.ActualDistanceNm, Is.EqualTo(60f).Within(0.001f));
            Assert.That(session.Heading, Is.EqualTo(heading));
            AssertPoint(session.Position, new VoyagePoint(10f, 32f));
            session.SetHeading(SailingDirection.North);
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(session.GetForecast().Target.X, Is.EqualTo(session.Position.X).Within(0.001f));
            Assert.That(session.GetForecast().Target.Y, Is.GreaterThan(session.Position.Y));
        }

        [Test]
        public void NewSelectionReplacesFinalTargetRatherThanOnlyChangingItsFirstLeg()
        {
            CoreVoyageSession session = OpenVoyage();
            session.SetCourseTo(new VoyagePoint(40f, 32f));
            Assert.That(session.Sail(), Is.True);
            var replacement = new VoyagePoint(30f, 50f);
            session.SetCourseTo(replacement);
            AssertPoint(session.DirectCourseTarget, replacement);
            SailingForecast forecast = session.GetForecast();
            AssertLegWithinBudget(session, forecast);
            Assert.That(forecast.Target.Y, Is.GreaterThan(session.Position.Y));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertPoint(session.DirectCourseTarget, replacement);
        }

        [Test]
        public void IslandDetourIsClippedAlongNavigablePathAndRefreshesHeadingForEveryLeg()
        {
            CoreVoyageSession session = OpenVoyage();
            SetProperty(session, "Position", new VoyagePoint(20f, 32f));
            for (int row = 11; row <= 19; row++)
                session.Map.TerrainCells[session.Map.CellIndex(14, row)] = SeaTerrainType.Island;
            var target = new VoyagePoint(40f, 32f);
            session.SetCourseTo(target);
            var headings = new HashSet<SailingDirection>();
            int legs = 0;
            while (session.HasDirectCourseTarget && legs++ < 12)
            {
                SailingForecast forecast = session.GetForecast();
                AssertLegWithinBudget(session, forecast);
                AssertHeadingFollowsPath(session, forecast);
                headings.Add(session.Heading);
                for (int i = 1; i < forecast.PathPoints.Count; i++)
                {
                    VoyagePoint a = forecast.PathPoints[i - 1], b = forecast.PathPoints[i];
                    int samples = (int)Math.Ceiling(Distance(a, b) / .1f);
                    for (int sample = 0; sample <= samples; sample++)
                    {
                        float t = samples == 0 ? 0f : sample / (float)samples;
                        var point = new VoyagePoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                        Assert.That(session.Map.TerrainCells[session.Map.CellIndex(point)], Is.Not.EqualTo(SeaTerrainType.Island));
                    }
                }
                Assert.That(session.Sail(), Is.True);
                AssertPoint(session.Position, forecast.Target);
            }
            Assert.That(legs, Is.LessThan(12));
            Assert.That(headings.Count, Is.GreaterThan(1), "Heading must follow the changing route around the island.");
            AssertPoint(session.Position, target);
            Assert.That(session.HasDirectCourseTarget, Is.False);
        }

        [Test]
        public void IntermediateFixedEncounterKeepsTheSelectedEventUntilArrival()
        {
            CoreVoyageSession session = OpenVoyage();
            CoreMapNode intermediate = AddEvent(session, 1, new VoyagePoint(14f, 32f));
            CoreMapNode target = AddEvent(session, 2, new VoyagePoint(25f, 32f));
            session.SetCourseTo(target.Position);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter, Is.SameAs(intermediate.Encounter));
            Assert.That(session.HasDirectCourseTarget, Is.True);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            int legs = 0;
            while (session.Phase == CoreDemoPhase.Sailing && legs++ < 5)
                Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter, Is.SameAs(target.Encounter));
            AssertPoint(session.Position, target.Position);
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(target.Visited, Is.True);
        }

        [Test]
        public void RandomEncounterJustBeforeTargetKeepsLockAndSubNauticalMileFinalLegTriggersOnlySelectedEvent()
        {
            CoreVoyageSession session = OpenVoyage(chart: true);
            CoreMapNode target = AddEvent(session, 2, new VoyagePoint(12f, 32f));
            // A different nearby event must not gain the selected target's short-leg exception.
            CoreMapNode nearby = AddEvent(session, 1, new VoyagePoint(12f, 32.4f));
            SetField(session, "chartRandomVersion", 0);
            SetField(session, "chartRandomIndex", 0);
            SetField(session, "chartRandomOrder", new[] { "random_tailwind" });
            var random = (CoreEncounter)typeof(CoreVoyageSession).GetMethod("CreateSeaChartRandomEncounter", Private)
                .Invoke(session, new object[] { "random_tailwind" });
            SetField(session, "chartRandomEncounters", new[] { random });
            float distance = Distance(session.Position, target.Position) * session.Map.Route.NauticalMilesPerMapUnit;
            SetField(session, "nextChartRandomDistance", distance - .6f);
            session.SetCourseTo(target.Position);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo("random_tailwind"));
            Assert.That(session.HasDirectCourseTarget, Is.True);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            Assert.That(session.GetForecast().ActualDistanceNm, Is.EqualTo(.6f).Within(.001f));
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter, Is.SameAs(target.Encounter));
            Assert.That(nearby.Visited, Is.False);
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(target.Visited, Is.True);
        }

        [TestCase(0f)]
        [TestCase(.02f)]
        [TestCase(.15f)]
        public void SelectingAnExactNearbyUnvisitedEventCanCompleteItsShortApproach(float offset)
        {
            CoreVoyageSession session = OpenVoyage();
            CoreMapNode target = AddEvent(session, 1, new VoyagePoint(session.Position.X + offset, session.Position.Y));
            session.SetCourseTo(target.Position);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter, Is.SameAs(target.Encounter));
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(target.Visited, Is.True);
        }

        [Test]
        public void InsufficientSuppliesKeepThePendingTargetAndSafeReturnClearsIt()
        {
            CoreVoyageSession session = OpenVoyage();
            var target = new VoyagePoint(30f, 32f);
            session.SetCourseTo(target);
            session.Cargo.Water = session.Cargo.Food = 0;
            Assert.That(session.Sail(), Is.False);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertPoint(session.DirectCourseTarget, target);
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.HasDirectCourseTarget, Is.False);
        }

        [Test]
        public void DockingApproachOfLockedCourseStaysWithinItsAdjustedDistanceBudget()
        {
            CoreVoyageSession session = OpenVoyage();
            session.Map.Destination = new VoyagePoint(17f, 33.5f);
            SetField(session, "bossDefeated", true);
            session.SetCourseTo(new VoyagePoint(18f, 32f));
            session.SetDistance(60);
            int legs = 0;
            while (session.Phase == CoreDemoPhase.Sailing && legs++ < 8)
            {
                SailingForecast forecast = session.GetForecast();
                AssertLegWithinBudget(session, forecast);
                Assert.That(session.Sail(), Is.True);
            }
            Assert.That(legs, Is.GreaterThan(1).And.LessThan(8));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
            Assert.That(session.HasDirectCourseTarget, Is.False);
            AssertPoint(session.Position, session.Map.Destination);
        }

        [Test]
        public void CheckpointRetainsFinalTargetAndTheNextLegBudget()
        {
            var source = new CoreVoyageSession();
            Assert.That(source.TrySelectChartSeed("SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6", out string reason), Is.True, reason);
            source.StartVoyage();
            source.SetCourseTo(source.Map.Destination);
            source.SetDistance(60);
            CoreVoyageCheckpoint checkpoint = source.CaptureVoyageCheckpoint();
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out reason), Is.True, reason);
            Assert.That(restored.HasDirectCourseTarget, Is.True);
            AssertPoint(restored.DirectCourseTarget, source.Map.Destination);
            Assert.That(restored.PlannedDistanceNm, Is.EqualTo(60));
            AssertPoint(restored.GetForecast().Target, source.GetForecast().Target);
            AssertLegWithinBudget(restored, restored.GetForecast());
        }

        private static CoreVoyageSession OpenVoyage(bool chart = false)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            if (chart)
                Assert.That(session.TrySelectChartSeed("SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6", out string reason), Is.True, reason);
            else session.SetSeed("COURSE-LOCK-REGRESSION");
            session.StartVoyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentVectors, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentStrengths, 0, session.Map.CellCount);
            session.Map.GlobalWindStrength = 0f;
            session.Map.Destination = new VoyagePoint(90f, 60f);
            SetProperty(session, "Position", new VoyagePoint(10f, 32f));
            SetField(session, "randomAmbushOccurred", true);
            session.Cargo.Water = session.Cargo.Food = 100;
            return session;
        }

        private static CoreMapNode AddEvent(CoreVoyageSession session, int id, VoyagePoint position)
        {
            var node = new CoreMapNode { Id = id, Position = position,
                Encounter = new CoreEncounter("course-event-" + id, "Course event " + id, "Event", "", true, false, 0, 0,
                    new CoreEncounterChoice("Continue", "Continue", 0d, new VoyageEffect())) };
            session.Map.Nodes.Add(node);
            return node;
        }

        private static void AssertLegWithinBudget(CoreVoyageSession session, SailingForecast forecast)
        {
            Assert.That(forecast.Blocked, Is.False);
            Assert.That(forecast.ActualDistanceNm, Is.LessThanOrEqualTo(session.PlannedDistanceNm + .001f));
            Assert.That(forecast.ActualDistanceNm, Is.LessThanOrEqualTo(session.MaximumDistanceNm + .001f));
            Assert.That(forecast.Hours, Is.LessThanOrEqualTo(24d));
            float distance = 0f;
            for (int i = 1; i < forecast.PathPoints.Count; i++) distance += Distance(forecast.PathPoints[i - 1], forecast.PathPoints[i]);
            Assert.That(forecast.ActualDistanceNm, Is.EqualTo(distance * session.Map.Route.NauticalMilesPerMapUnit).Within(.001f));
        }

        private static void AssertHeadingFollowsPath(CoreVoyageSession session, SailingForecast forecast)
        {
            VoyagePoint a = forecast.PathPoints[0], b = forecast.PathPoints[1];
            double angle = Math.Atan2(b.Y - a.Y, b.X - a.X);
            var expected = (SailingDirection)typeof(CoreVoyageSession).GetMethod("ClosestDirection", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { angle });
            Assert.That(session.Heading, Is.EqualTo(expected));
        }

        private static float Distance(VoyagePoint a, VoyagePoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        private static void AssertPoint(VoyagePoint actual, VoyagePoint expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(.0001f));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(.0001f));
        }
        private static void SetProperty(CoreVoyageSession session, string name, object value) => typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);
        private static void SetField(CoreVoyageSession session, string name, object value) => typeof(CoreVoyageSession).GetField(name, Private).SetValue(session, value);
    }
}
