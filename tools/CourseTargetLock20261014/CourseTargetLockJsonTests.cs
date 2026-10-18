using System;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CourseTargetLockJsonTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RealJsonRetainsTheFinalFarTargetAndCurrentLeg(bool afterOneSail)
        {
            CoreVoyageSession source = Start();
            VoyagePoint final = source.Map.Destination;
            source.SetCourseTo(final);
            Assert.That(Distance(source.Position, final) * source.Map.Route.NauticalMilesPerMapUnit, Is.GreaterThan(180f));
            if (afterOneSail) Assert.That(source.Sail(), Is.True);
            Assert.That(source.HasDirectCourseTarget, Is.True);
            Point(source.DirectCourseTarget, final);
            CoreVoyageCheckpoint decoded = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(source.CaptureVoyageCheckpoint()));
            Assert.That(decoded.hasDirectTarget, Is.True);
            Assert.That(decoded.targetX, Is.EqualTo(final.X));
            Assert.That(decoded.targetY, Is.EqualTo(final.Y));
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(decoded, out string reason), Is.True, reason);
            Assert.That(restored.HasDirectCourseTarget, Is.True);
            Point(restored.DirectCourseTarget, final);
            Point(restored.Position, source.Position);
            Assert.That(restored.Phase, Is.EqualTo(source.Phase));
            if (restored.Phase == CoreDemoPhase.Sailing)
            {
                SailingForecast forecast = restored.GetForecast();
                Assert.That(forecast.ActualDistanceNm, Is.LessThanOrEqualTo(CoreVoyageSession.MaximumDistanceNm + .01f));
                Point(forecast.Target, source.GetForecast().Target);
            }
        }

        [Test]
        public void OldJsonShortTargetRemainsItsStoredPointWithoutInventingADistantGoal()
        {
            CoreVoyageSession source = Start();
            source.SetCourseTo(source.Map.Destination);
            SailingForecast path = source.GetForecast();
            Assert.That(path.PathPoints.Count, Is.GreaterThanOrEqualTo(2));
            VoyagePoint first = path.PathPoints[0], second = path.PathPoints[1];
            VoyagePoint shortPoint = new VoyagePoint((first.X + second.X) * .5f, (first.Y + second.Y) * .5f);
            CoreVoyageCheckpoint oldCheckpoint = source.CaptureVoyageCheckpoint();
            // The old format already stored only this short target. Its unknown
            // original mouse click cannot be reconstructed from these fields.
            oldCheckpoint.targetX = shortPoint.X;
            oldCheckpoint.targetY = shortPoint.Y;
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(oldCheckpoint)), out string reason), Is.True, reason);
            Point(restored.DirectCourseTarget, shortPoint);
            Point(restored.GetForecast().Target, shortPoint);
            Assert.That(restored.Sail(), Is.True);
            Point(restored.Position, shortPoint);
            Assert.That(restored.HasDirectCourseTarget, Is.False);
        }

        private static CoreVoyageSession Start()
        {
            var session = new CoreVoyageSession();
            session.Cargo.Water = session.Cargo.Food = 100;
            session.SetSeed("COURSE-LOCK-JSON-20261014");
            session.StartVoyage();
            return session;
        }
        private static float Distance(VoyagePoint a, VoyagePoint b) =>
            (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        private static void Point(VoyagePoint actual, VoyagePoint expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(.001f));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(.001f));
        }
    }
}
