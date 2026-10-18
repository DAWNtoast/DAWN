using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>World coordinates are metres: X east, Y north; headings are 0 east, 90 north.</summary>
    public readonly struct CoreNavalTacticalFrame
    {
        public CoreNavalTacticalFrame(VoyagePoint player, VoyagePoint enemy, float playerHeading, float enemyHeading)
        {
            PlayerPositionMeters = player;
            EnemyPositionMeters = enemy;
            PlayerHeadingDegrees = playerHeading;
            EnemyHeadingDegrees = enemyHeading;
        }

        public VoyagePoint PlayerPositionMeters { get; }
        public VoyagePoint EnemyPositionMeters { get; }
        public float PlayerHeadingDegrees { get; }
        public float EnemyHeadingDegrees { get; }
    }

    /// <summary>An immutable movement result. Frames include the starting pose and each turn/movement step.</summary>
    public sealed class CoreNavalTacticalMovement
    {
        internal CoreNavalTacticalMovement(int revision, List<CoreNavalTacticalFrame> frames)
        {
            Revision = revision;
            Frames = new List<CoreNavalTacticalFrame>(frames).AsReadOnly();
        }
        public int Revision { get; }
        public IReadOnlyList<CoreNavalTacticalFrame> Frames { get; }
    }

    public sealed partial class CoreBattleState
    {
        private float tacticalDistanceMeters;
        public VoyagePoint PlayerPositionMeters { get; private set; }
        public VoyagePoint EnemyPositionMeters { get; private set; }
        public float PlayerHeadingDegrees { get; private set; }
        public float EnemyHeadingDegrees { get; private set; } = 180f;
        public int TacticalRevision { get; private set; }
        public CoreNavalTacticalMovement LastTacticalMovement { get; private set; }
        public CoreNavalTacticalFrame TacticalFrame => new CoreNavalTacticalFrame(
            PlayerPositionMeters, EnemyPositionMeters, PlayerHeadingDegrees, EnemyHeadingDegrees);

        // Compatibility for authored tutorial ranges and callers that configure a battle by distance.
        // Normal manoeuvres update positions first and derive their range from the resulting geometry.
        private void SetLegacyTacticalDistance(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            var frames = new List<CoreNavalTacticalFrame> { TacticalFrame };
            float dx = PlayerPositionMeters.X - EnemyPositionMeters.X;
            float dy = PlayerPositionMeters.Y - EnemyPositionMeters.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            float range = Math.Max(0f, value);
            if (length < 0.001f) { dx = -1f; dy = 0f; length = 1f; }
            PlayerPositionMeters = new VoyagePoint(EnemyPositionMeters.X + dx / length * range,
                EnemyPositionMeters.Y + dy / length * range);
            tacticalDistanceMeters = range;
            if (TacticalRevision == 0) frames[0] = TacticalFrame;
            frames.Add(TacticalFrame);
            CommitTacticalMovement(frames);
        }

        internal void SetTacticalPose(bool player, VoyagePoint position, float heading)
        {
            heading = CoreVoyageSession.NormalizeNavalHeading(heading);
            if (player) { PlayerPositionMeters = position; PlayerHeadingDegrees = heading; }
            else { EnemyPositionMeters = position; EnemyHeadingDegrees = heading; }
            float dx = PlayerPositionMeters.X - EnemyPositionMeters.X;
            float dy = PlayerPositionMeters.Y - EnemyPositionMeters.Y;
            tacticalDistanceMeters = (float)Math.Sqrt(dx * dx + dy * dy);
        }

        internal void CommitTacticalMovement(List<CoreNavalTacticalFrame> frames)
        {
            TacticalRevision = TacticalRevision == int.MaxValue ? 1 : TacticalRevision + 1;
            LastTacticalMovement = new CoreNavalTacticalMovement(TacticalRevision, frames);
        }
    }

    public sealed partial class CoreVoyageSession
    {
        public const float NavalGridCellMeters = 100f;
        public const int NavalCannonRangeCells = 4;
        public const string NavalCannonOutOfRangeReason = "炮击需要横纵相隔格数之和不超过 4 格";
        private const float NavalDegreesToRadians = (float)(Math.PI / 180d);

        // Use the same world-cell centres as the tactical board's click projection.
        // Camera rotation and the straight-line distance between ships do not affect range.
        public static int NavalGridDistance(VoyagePoint first, VoyagePoint second)
        {
            if (float.IsNaN(first.X) || float.IsNaN(first.Y) || float.IsNaN(second.X) || float.IsNaN(second.Y) ||
                float.IsInfinity(first.X) || float.IsInfinity(first.Y) || float.IsInfinity(second.X) || float.IsInfinity(second.Y))
                return int.MaxValue;
            double columns = Math.Abs(Math.Round((double)first.X / NavalGridCellMeters) - Math.Round((double)second.X / NavalGridCellMeters));
            double rows = Math.Abs(Math.Round((double)first.Y / NavalGridCellMeters) - Math.Round((double)second.Y / NavalGridCellMeters));
            return (int)Math.Min(int.MaxValue, columns + rows);
        }

        public static bool NavalCannonInRange(VoyagePoint first, VoyagePoint second) =>
            NavalGridDistance(first, second) <= NavalCannonRangeCells;

        public int NavalDistanceCells => Battle == null ? 0 : NavalGridDistance(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters);
        public bool NavalCannonTargetInRange => Battle != null && NavalCannonInRange(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters);

        private static void AppendRetiredNavalTacticsSlots(List<CoreBattleCard> cards)
        {
            cards.Add(RetiredBattleCardSlot("card_turn_port"));
            cards.Add(RetiredBattleCardSlot("card_turn_starboard"));
            cards.Add(RetiredBattleCardSlot("card_turn_about"));
            cards.Add(RetiredBattleCardSlot("card_sail_forward"));
        }

        internal static float NormalizeNavalHeading(float heading)
        {
            if (float.IsNaN(heading) || float.IsInfinity(heading)) return 0f;
            heading %= 360f;
            return heading < 0f ? heading + 360f : heading;
        }

        private static float NavalAngleDelta(float from, float to)
        {
            float delta = NormalizeNavalHeading(to) - NormalizeNavalHeading(from);
            if (delta > 180f) delta -= 360f;
            if (delta < -180f) delta += 360f;
            return delta;
        }

        private static float NavalBearing(VoyagePoint from, VoyagePoint to) =>
            NormalizeNavalHeading((float)Math.Atan2(to.Y - from.Y, to.X - from.X) / NavalDegreesToRadians);

        private void AdvanceTacticalEnemy(float meters, float? arrivalHeading = null)
        {
            if (NavalFieldsActive && (Battle.NavalFields.Cells.Count > 0 || FieldShip(false).MovementSlow > 0))
            {
                AdvanceNavalEnemyThroughFields(meters, arrivalHeading);
                return;
            }
            var frames = new List<CoreNavalTacticalFrame> { Battle.TacticalFrame };
            float initialRange = Battle.DistanceMeters;
            float target = NormalizeNavalHeading((float)Math.Floor(NavalBearing(Battle.EnemyPositionMeters,
                Battle.PlayerPositionMeters) / 90f + .5f) * 90f);
            // Enemy turns and travel finish on the same cardinal grid used by the player.
            float turn = Math.Max(-90f, Math.Min(90f, NavalAngleDelta(Battle.EnemyHeadingDegrees, target)));
            TurnNavalShip(false, Battle.EnemyHeadingDegrees + turn, frames);
            int cells = (int)(QuantizeNavalEnemyTravel(meters) / NavalGridCellMeters);
            for (int index = 0; index < cells; index++)
            {
                float angle = Battle.EnemyHeadingDegrees * NavalDegreesToRadians;
                VoyagePoint next = new VoyagePoint(
                    Battle.EnemyPositionMeters.X + (float)Math.Round(Math.Cos(angle)) * NavalGridCellMeters,
                    Battle.EnemyPositionMeters.Y + (float)Math.Round(Math.Sin(angle)) * NavalGridCellMeters);
                if (NavalSegmentDistance(Battle.EnemyPositionMeters, next, Battle.PlayerPositionMeters) < MinimumNavalDistanceMeters)
                {
                    // A head-on neighbour cannot advance or ram. Turn aside without entering
                    // the occupied cell, leaving the player a legal movement/contact decision.
                    TurnNavalShip(false, Battle.EnemyHeadingDegrees + 90f, frames);
                    break;
                }
                AdvanceNavalShip(false, NavalGridCellMeters, frames);
                Battle.SetTacticalPose(false, next, Battle.EnemyHeadingDegrees);
                frames[frames.Count - 1] = Battle.TacticalFrame;
            }
            if (arrivalHeading.HasValue) TurnNavalShip(false, arrivalHeading.Value, frames);
            FinishNavalMovement(frames, false, Battle.DistanceMeters < initialRange - 0.01f);
        }

        private void TurnNavalShip(bool player, float target, List<CoreNavalTacticalFrame> frames)
        {
            float initial = player ? Battle.PlayerHeadingDegrees : Battle.EnemyHeadingDegrees;
            float delta = NavalAngleDelta(initial, target);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / 15f));
            VoyagePoint position = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            for (int index = 1; index <= steps; index++)
            {
                Battle.SetTacticalPose(player, position, initial + delta * index / steps);
                frames.Add(Battle.TacticalFrame);
            }
        }

        private void AdvanceNavalShip(bool player, float meters, List<CoreNavalTacticalFrame> frames)
        {
            if (meters <= 0f) return;
            float heading = player ? Battle.PlayerHeadingDegrees : Battle.EnemyHeadingDegrees;
            float vx = (float)Math.Cos(heading * NavalDegreesToRadians);
            float vy = (float)Math.Sin(heading * NavalDegreesToRadians);
            VoyagePoint start = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            VoyagePoint target = player ? Battle.EnemyPositionMeters : Battle.PlayerPositionMeters;
            // Clip the full segment at the first hull-clearance contact to prevent tunnelling or overshoot.
            float ox = start.X - target.X, oy = start.Y - target.Y;
            float projection = ox * vx + oy * vy;
            float clearance = MinimumNavalDistanceMeters;
            float discriminant = projection * projection - (ox * ox + oy * oy - clearance * clearance);
            if (projection < 0f && discriminant >= 0f)
                meters = Math.Min(meters, Math.Max(0f, -projection - (float)Math.Sqrt(discriminant)));
            int steps = Math.Max(1, (int)Math.Ceiling(meters / 20f));
            for (int index = 1; index <= steps; index++)
            {
                float distance = meters * index / steps;
                Battle.SetTacticalPose(player, new VoyagePoint(start.X + vx * distance, start.Y + vy * distance), heading);
                frames.Add(Battle.TacticalFrame);
            }
        }

        private void FinishNavalMovement(List<CoreNavalTacticalFrame> frames, bool playerMoved, bool closing)
        {
            float playerBearing = NavalBearing(Battle.PlayerPositionMeters, Battle.EnemyPositionMeters);
            float enemyBearing = NavalBearing(Battle.EnemyPositionMeters, Battle.PlayerPositionMeters);
            bool playerFacesEnemy = Math.Abs(NavalAngleDelta(Battle.PlayerHeadingDegrees, playerBearing)) <= 35f;
            float enemyFacing = Math.Abs(NavalAngleDelta(Battle.EnemyHeadingDegrees, enemyBearing));
            float relativeHeading = Math.Abs(NavalAngleDelta(Battle.PlayerHeadingDegrees, Battle.EnemyHeadingDegrees));
            if (playerFacesEnemy && enemyFacing <= 35f) Battle.Alignment = NavalBattleAlignment.HeadOn;
            else if (playerFacesEnemy && enemyFacing >= 45f && enemyFacing <= 135f)
                Battle.Alignment = NavalBattleAlignment.EnemyBroadside;
            else if (relativeHeading <= 25f) Battle.Alignment = NavalBattleAlignment.Parallel;
            else Battle.Alignment = NavalBattleAlignment.Maneuvering;
            if (playerMoved) Battle.PlayerMovingTowardEnemy = closing && playerFacesEnemy;
            else
            {
                Battle.EnemyMovingTowardPlayer = closing && enemyFacing <= 35f;
                // Enemy movement can invalidate a previous player's ram line.
                Battle.PlayerMovingTowardEnemy &= playerFacesEnemy;
            }
            Battle.CommitTacticalMovement(frames);
        }
    }
}

