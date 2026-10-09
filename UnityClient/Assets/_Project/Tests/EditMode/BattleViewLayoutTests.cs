using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleViewLayoutTests
    {
        [TestCase(600f)]
        [TestCase(900f)]
        [TestCase(10000f)]
        public void LongRange_UsesCenteredCoverCropWithoutStretching(float distance)
        {
            Rect uv = BattleViewLayout.BackdropUv(distance);
            Assert.That(uv.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(uv.width, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(uv.y, Is.EqualTo(0.07012101f).Within(0.0001f));
            Assert.That(uv.height, Is.EqualTo(0.85975796f).Within(0.0001f));
            Assert.That(BattleViewLayout.Proximity(distance), Is.Zero);
        }

        [TestCase(-100f)]
        [TestCase(0f)]
        [TestCase(50f)]
        public void CloseRange_ClampsToDetailedViewWithinTextureBounds(float distance)
        {
            Rect uv = BattleViewLayout.BackdropUv(distance);
            Assert.That(uv.width, Is.LessThan(0.7f));
            Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(uv.xMax, Is.LessThanOrEqualTo(1f));
            Assert.That(uv.yMax, Is.LessThanOrEqualTo(1f));
            Assert.That(BattleViewLayout.Proximity(distance), Is.EqualTo(1f));
        }

        [Test]
        public void InvalidDistance_ProducesFiniteFullViewRatherThanCorruptingUiTransforms()
        {
            foreach (float distance in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Rect uv = BattleViewLayout.BackdropUv(distance);
                Assert.That(uv.x, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(uv.width, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(uv.y, Is.EqualTo(0.07012101f).Within(0.0001f));
                Assert.That(uv.height, Is.EqualTo(0.85975796f).Within(0.0001f));
                Assert.That(float.IsNaN(BattleViewLayout.ShipWidth(distance, NavalBattleStage.Naval)), Is.False);
                Assert.That(float.IsNaN(BattleViewLayout.WaterY(distance, NavalBattleStage.Naval)), Is.False);
            }
        }

        [Test]
        public void DistanceMapping_IsMonotonicAndContinuous_WithIncreasingHullSize()
        {
            float previousExtent = 1.01f;
            float previousShipWidth = 0f;
            for (float distance = 600f; distance >= 50f; distance -= 5f)
            {
                Rect uv = BattleViewLayout.BackdropUv(distance);
                float shipWidth = BattleViewLayout.ShipWidth(distance, NavalBattleStage.Naval);
                Assert.That(uv.width, Is.LessThanOrEqualTo(previousExtent));
                Assert.That(shipWidth, Is.GreaterThanOrEqualTo(previousShipWidth));
                if (previousShipWidth > 0f)
                {
                    Assert.That(previousExtent - uv.width, Is.LessThan(0.02f));
                    Assert.That(shipWidth - previousShipWidth, Is.LessThan(3f));
                }
                previousExtent = uv.width;
                previousShipWidth = shipWidth;
            }
        }

        [Test]
        public void Alignments_HaveDistinctModelYawAndBoardingGeometry()
        {
            float headOn = BattleViewLayout.EnemyYaw(NavalBattleAlignment.HeadOn);
            float side = BattleViewLayout.EnemyYaw(NavalBattleAlignment.EnemyBroadside);
            float parallel = BattleViewLayout.EnemyYaw(NavalBattleAlignment.Parallel);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(headOn, side)), Is.GreaterThan(15f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(side, parallel)), Is.GreaterThan(15f));
            Assert.That(BattleViewLayout.BoardingPosition(true, NavalBoardingApproach.Quarter),
                Is.Not.EqualTo(BattleViewLayout.BoardingPosition(true, NavalBoardingApproach.Alongside)));
            Assert.That(BattleViewLayout.BoardingBridgeAngle(NavalBoardingApproach.Quarter),
                Is.Not.EqualTo(BattleViewLayout.BoardingBridgeAngle(NavalBoardingApproach.Alongside)));
        }
    }
}
