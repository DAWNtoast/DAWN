using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private const float NavalContactTolerance = .01f;

        public bool NavalShipsAreOrthogonallyAdjacent => Battle != null &&
            NavalPositionsAreOrthogonallyAdjacent(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters);

        public static bool NavalPositionsAreOrthogonallyAdjacent(VoyagePoint first, VoyagePoint second)
        {
            float dx = Math.Abs(second.X - first.X), dy = Math.Abs(second.Y - first.Y);
            return (dx < NavalContactTolerance && Math.Abs(dy - NavalGridCellMeters) < NavalContactTolerance) ||
                (dy < NavalContactTolerance && Math.Abs(dx - NavalGridCellMeters) < NavalContactTolerance);
        }

        private bool NavalHeadingsMatch => Battle != null &&
            Math.Abs(NavalAngleDelta(Battle.PlayerHeadingDegrees, Battle.EnemyHeadingDegrees)) < NavalContactTolerance;

        private bool NavalHeadingsArePerpendicular => Battle != null &&
            Math.Abs(Math.Abs(NavalAngleDelta(Battle.PlayerHeadingDegrees, Battle.EnemyHeadingDegrees)) - 90f) < NavalContactTolerance;

        private bool NavalShipPointsAtOpponent(bool player)
        {
            VoyagePoint from = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            VoyagePoint to = player ? Battle.EnemyPositionMeters : Battle.PlayerPositionMeters;
            float heading = player ? Battle.PlayerHeadingDegrees : Battle.EnemyHeadingDegrees;
            return Math.Abs(NavalAngleDelta(heading, NavalBearing(from, to))) < NavalContactTolerance;
        }

        private bool NavalRamGeometryIsLegal(bool player) => NavalShipsAreOrthogonallyAdjacent &&
            NavalHeadingsArePerpendicular && NavalShipPointsAtOpponent(player);

        private static float QuantizeNavalEnemyTravel(float meters) => meters <= 0f ? 0f :
            Math.Max(1f, (float)Math.Floor(meters / NavalGridCellMeters + .5f)) * NavalGridCellMeters;
    }
}
