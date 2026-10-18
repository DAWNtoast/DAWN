using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private readonly struct NavalReachState : IEquatable<NavalReachState>
        {
            internal NavalReachState(int x, int y, int direction, int steps)
            { X = x; Y = y; Direction = direction; Steps = steps; }
            internal int X { get; }
            internal int Y { get; }
            internal int Direction { get; }
            internal int Steps { get; }
            public bool Equals(NavalReachState other) => X == other.X && Y == other.Y && Direction == other.Direction;
            public override bool Equals(object other) => other is NavalReachState state && Equals(state);
            public override int GetHashCode() => unchecked((X * 397 ^ Y) * 397 ^ Direction);
        }

        /// <summary>
        /// All cells reachable in one or more steps using the ship's current action points.
        /// Uses the actual pose, independently of an uncommitted preview. Different arrival
        /// headings share one output cell but remain distinct search states. No route is selected.
        /// The starting cell is included only if a legal positive-length loop reaches it.
        /// </summary>
        public IReadOnlyList<VoyagePoint> GetNavalReachableCells()
        {
            var result = new List<VoyagePoint>();
            if (!NavalActionMovementActive || Battle.NavalAwaitingHeadingSelection || Battle.NavalActionPoints <= 0 ||
                GuidedBossMovementRestriction(Battle.PlayerPositionMeters, 0).Length > 0)
                return result.AsReadOnly();

            VoyagePoint origin = Battle.PlayerPositionMeters;
            var pending = new Queue<NavalReachState>();
            var visited = new HashSet<NavalReachState>();
            var cells = new HashSet<long>();
            pending.Enqueue(new NavalReachState(0, 0, 0, 0));
            while (pending.Count > 0)
            {
                NavalReachState state = pending.Dequeue();
                if (state.Steps >= NavalActiveMovementLimit(true, Battle.NavalActionPoints)) continue;
                float heading = state.Steps == 0 ? Battle.PlayerHeadingDegrees : state.Direction * 90f;
                var previous = new CoreNavalMovementNode(new VoyagePoint(origin.X + state.X * NavalGridCellMeters,
                    origin.Y + state.Y * NavalGridCellMeters), heading);
                for (int choice = 0; choice < 3; choice++)
                {
                    float nextHeading = NormalizeNavalHeading(heading + (choice == 0 ? 0f : choice == 1 ? 90f : -90f));
                    float radians = nextHeading * NavalDegreesToRadians;
                    int x = state.X + (int)Math.Round(Math.Cos(radians));
                    int y = state.Y + (int)Math.Round(Math.Sin(radians));
                    var point = new VoyagePoint(origin.X + x * NavalGridCellMeters, origin.Y + y * NavalGridCellMeters);
                    if (NavalMovementEdgeUnavailableReason(previous, point, state.Steps).Length > 0) continue;
                    var next = new NavalReachState(x, y, (int)Math.Round(nextHeading / 90f) % 4, state.Steps + 1);
                    // Breadth-first discovery retains the smallest cost for each pose. Later
                    // visits to the same pose cannot unlock anything the cheaper visit cannot.
                    if (!visited.Add(next)) continue;
                    if (cells.Add(((long)x << 32) | (uint)y)) result.Add(point);
                    CoreNavalFieldCell field = NavalFieldAt(point);
                    if (field == null || field.Kind != CoreNavalFieldKind.Thunder ||
                        Battle.NavalFields.TriggerLedger.Contains(FieldTriggerKey(true, field))) pending.Enqueue(next);
                }
            }
            return result.AsReadOnly();
        }

        /// <summary>
        /// Adds only the straight, grid-aligned segment explicitly supplied by a drag sample.
        /// Every intermediate edge is validated before any preview node is added; no pathfinding.
        /// </summary>
        public bool TryAppendNavalMovementSegment(VoyagePoint targetMeters, out string reason)
        {
            if (!NavalActionMovementActive) { reason = "当前不是行动点海战"; return false; }
            if (Battle.NavalAwaitingHeadingSelection) { reason = "请先确定到达朝向"; return false; }
            EnsureNavalMovementPath();
            reason = GuidedBossMovementRestriction(targetMeters, navalMovementPath.Count - 1);
            if (reason.Length > 0) return false;
            if (float.IsNaN(targetMeters.X) || float.IsNaN(targetMeters.Y) ||
                float.IsInfinity(targetMeters.X) || float.IsInfinity(targetMeters.Y))
            { reason = "落点无效"; return false; }
            CoreNavalMovementNode previous = navalMovementPath[navalMovementPath.Count - 1];
            double dx = (double)targetMeters.X - previous.PositionMeters.X;
            double dy = (double)targetMeters.Y - previous.PositionMeters.Y;
            if (Math.Abs(dx) < .01 && Math.Abs(dy) < .01) { reason = string.Empty; return true; }
            bool horizontal = Math.Abs(dy) < .01;
            bool vertical = Math.Abs(dx) < .01;
            double distance = horizontal ? Math.Abs(dx) : Math.Abs(dy);
            double steps = Math.Round(distance / NavalGridCellMeters);
            if ((!horizontal && !vertical) || steps < 1 || Math.Abs(distance - steps * NavalGridCellMeters) >= .01)
            { reason = "请沿相邻的上下左右格子拖动路线，不能斜跳或落在格子之间"; return false; }
            if (steps > (long)NavalActiveMovementLimit(true, Battle.NavalActionPoints) - (navalMovementPath.Count - 1))
            { reason = "行动点不足，每格消耗 1 点"; return false; }
            var additions = new List<CoreNavalMovementNode>();
            VoyagePoint start = previous.PositionMeters;
            for (int index = 1; index <= (int)steps; index++)
            {
                var point = new VoyagePoint(start.X + (horizontal ? Math.Sign(dx) * index * NavalGridCellMeters : 0f),
                    start.Y + (vertical ? Math.Sign(dy) * index * NavalGridCellMeters : 0f));
                reason = NavalMovementEdgeUnavailableReason(previous, point, navalMovementPath.Count - 1 + additions.Count);
                if (reason.Length > 0) return false;
                previous = new CoreNavalMovementNode(point, NavalBearing(previous.PositionMeters, point));
                additions.Add(previous);
            }
            navalMovementPath.AddRange(additions);
            reason = string.Empty;
            return true;
        }

        /// <summary>Retraces an existing preview to its retained step index; zero keeps only the starting pose.</summary>
        public bool TryTruncateNavalMovementPath(int retainedSteps, out string reason)
        {
            if (!NavalActionMovementActive) { reason = "当前不是行动点海战"; return false; }
            if (Battle.NavalAwaitingHeadingSelection) { reason = "请先确定到达朝向"; return false; }
            EnsureNavalMovementPath();
            reason = GuidedBossMovementRestriction(Battle.PlayerPositionMeters, 0);
            if (reason.Length > 0) return false;
            if (retainedSteps < 0 || retainedSteps >= navalMovementPath.Count)
            { reason = "只能撤回到已有航线上的格子"; return false; }
            navalMovementPath.RemoveRange(retainedSteps + 1, navalMovementPath.Count - retainedSteps - 1);
            reason = string.Empty;
            return true;
        }
    }
}

