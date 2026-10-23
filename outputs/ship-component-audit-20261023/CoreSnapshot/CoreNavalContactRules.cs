using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private const float NavalContactTolerance = .01f;

        public bool NavalShipsAreOrthogonallyAdjacent => Battle != null &&
            NavalPositionsAreOrthogonallyAdjacent(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters);

        public NavalBoardingConnection PlayerRamConnection => Battle == null
            ? NavalBoardingConnection.Planned : NavalRamConnection(true);

        public NavalBoardingConnection EnemyRamConnection => Battle == null
            ? NavalBoardingConnection.Planned : NavalRamConnection(false);

        public static bool NavalPositionsAreOrthogonallyAdjacent(VoyagePoint first, VoyagePoint second)
        {
            float dx = Math.Abs(second.X - first.X), dy = Math.Abs(second.Y - first.Y);
            return (dx < NavalContactTolerance && Math.Abs(dy - NavalGridCellMeters) < NavalContactTolerance) ||
                (dy < NavalContactTolerance && Math.Abs(dx - NavalGridCellMeters) < NavalContactTolerance);
        }

        private static bool NavalHeadingsAreParallel(float first, float second)
        {
            float turn = Math.Abs(NavalAngleDelta(first, second));
            return turn < NavalContactTolerance || Math.Abs(turn - 180f) < NavalContactTolerance;
        }

        private bool NavalBoardingGeometryIsLegal(float playerHeading) => NavalShipsAreOrthogonallyAdjacent &&
            NavalHeadingsAreParallel(playerHeading, Battle.EnemyHeadingDegrees) &&
            Math.Abs(Math.Abs(NavalAngleDelta(playerHeading,
                NavalBearing(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters))) - 90f) < NavalContactTolerance;

        private bool NavalShipPointsAtOpponent(bool player)
        {
            VoyagePoint from = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            VoyagePoint to = player ? Battle.EnemyPositionMeters : Battle.PlayerPositionMeters;
            float heading = player ? Battle.PlayerHeadingDegrees : Battle.EnemyHeadingDegrees;
            return Math.Abs(NavalAngleDelta(heading, NavalBearing(from, to))) < NavalContactTolerance;
        }

        private bool NavalRamGeometryIsLegal(bool player) => NavalShipsAreOrthogonallyAdjacent &&
            NavalShipPointsAtOpponent(player);

        // Classify the struck end from the target's actual pose, never the legacy alignment flag.
        private NavalBoardingConnection NavalRamConnection(bool player)
        {
            VoyagePoint attacker = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            VoyagePoint target = player ? Battle.EnemyPositionMeters : Battle.PlayerPositionMeters;
            float targetHeading = player ? Battle.EnemyHeadingDegrees : Battle.PlayerHeadingDegrees;
            float angle = Math.Abs(NavalAngleDelta(targetHeading, NavalBearing(target, attacker)));
            if (angle < NavalContactTolerance) return NavalBoardingConnection.RamBowToBow;
            if (Math.Abs(angle - 180f) < NavalContactTolerance) return NavalBoardingConnection.RamBowToStern;
            return NavalBoardingConnection.RamBowToBroadside;
        }

        private float NavalRamTargetQuality(bool player) => NavalRamConnection(player) == NavalBoardingConnection.RamBowToBow
            ? (player ? Battle.EnemyBowQuality : Battle.PlayerBowQuality)
            : (player ? Battle.EnemyHullQuality : Battle.PlayerHullQuality);

        private static float QuantizeNavalEnemyTravel(float meters) => meters <= 0f ? 0f :
            Math.Max(1f, (float)Math.Floor(meters / NavalGridCellMeters + .5f)) * NavalGridCellMeters;
    }
}
