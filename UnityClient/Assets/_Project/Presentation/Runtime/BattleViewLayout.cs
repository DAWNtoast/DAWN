using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>One water-plane coordinate system for static rendering and animated maneuvers.</summary>
    public static class BattleViewLayout
    {
        public static float Proximity(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance)) distance = 600f;
            return Mathf.InverseLerp(600f, 50f, distance);
        }

        public static Rect BackdropUv(float distance)
        {
            return BackdropUv(distance, new Vector2(1860f, 900f));
        }

        public static Rect BackdropUv(float distance, Vector2 targetSize)
        {
            float close = Proximity(distance);
            // First cover the requested surface aspect so the painting is never
            // stretched, then zoom toward the lower sea for close encounters.
            float sourceAspect = 1672f / 941f;
            float targetAspect = targetSize.x / Mathf.Max(1f, targetSize.y);
            float coverHeight = Mathf.Min(1f, sourceAspect / targetAspect);
            float coverY = (1f - coverHeight) * 0.5f;
            float extent = Mathf.Lerp(1f, 0.62f, close);
            float width = extent;
            float height = coverHeight * extent;
            float y = coverY + (coverHeight - height) * 0.5f + 0.025f * close;
            return new Rect((1f - width) * 0.5f, y, width, height);
        }

        public static float ShipWidth(float distance, NavalBattleStage stage) =>
            stage == NavalBattleStage.Boarding ? 650f : Mathf.Lerp(280f, 430f, Proximity(distance));

        public static float ShipX(float distance, NavalBattleStage stage) =>
            stage == NavalBattleStage.Boarding ? 355f : Mathf.Lerp(505f, 330f, Proximity(distance));

        public static float WaterY(float distance, NavalBattleStage stage) =>
            stage == NavalBattleStage.Boarding ? -61f : Mathf.Lerp(-34f, -46f, Proximity(distance));

        // The source enemy UV always faces left. Rotate the actual image plane
        // around the vertical axis; never rotate a ship upright around Z.
        public static float EnemyYaw(NavalBattleAlignment alignment) =>
            alignment == NavalBattleAlignment.Parallel ? 180f :
            alignment == NavalBattleAlignment.EnemyBroadside ? 55f : 0f;

        public static Vector2 BoardingPosition(bool enemy, NavalBoardingApproach approach) =>
            new Vector2(enemy ? 355f : -355f,
                approach == NavalBoardingApproach.Quarter ? (enemy ? 28f : -18f) : 0f);

        public static float BoardingBridgeAngle(NavalBoardingApproach approach) =>
            approach == NavalBoardingApproach.Quarter ? 12f : 0f;
    }
}
