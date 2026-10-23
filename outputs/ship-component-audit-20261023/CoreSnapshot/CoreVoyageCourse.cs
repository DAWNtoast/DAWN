using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private bool HasNavigableGridLine(VoyagePoint start, VoyagePoint end)
        {
            if (!FiniteCoursePoint(start) || !FiniteCoursePoint(end)) return false;
            PointToCell(start, out int column, out int row);
            PointToCell(end, out int endColumn, out int endRow);
            if (!Map.IsPassable(column, row) || !Map.IsPassable(endColumn, endRow)) return false;
            double dx = (double)end.X - start.X, dy = (double)end.Y - start.Y;
            int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
            double cellWidth = Map.WorldWidth / (double)Map.Columns, cellHeight = Map.WorldHeight / (double)Map.Rows;
            double nextX = stepX == 0 ? double.PositiveInfinity :
                ((stepX > 0 ? column + 1 : column) * cellWidth - start.X) / dx;
            double nextY = stepY == 0 ? double.PositiveInfinity :
                ((stepY > 0 ? row + 1 : row) * cellHeight - start.Y) / dy;
            double deltaX = stepX == 0 ? double.PositiveInfinity : cellWidth / Math.Abs(dx);
            double deltaY = stepY == 0 ? double.PositiveInfinity : cellHeight / Math.Abs(dy);
            // Visit every crossed grid cell. Distance sampling can miss the
            // narrow corner of an island after a voyage is split at a new point.
            for (int crossings = 0; crossings <= Map.Columns + Map.Rows; crossings++)
            {
                if (column == endColumn && row == endRow) return true;
                if (Math.Abs(nextX - nextY) <= 1e-10d)
                {
                    // Match A*'s rule: a diagonal cannot squeeze between land.
                    if (!Map.IsPassable(column + stepX, row) || !Map.IsPassable(column, row + stepY)) return false;
                    column += stepX; row += stepY; nextX += deltaX; nextY += deltaY;
                }
                else if (nextX < nextY) { column += stepX; nextX += deltaX; }
                else { row += stepY; nextY += deltaY; }
                if (!Map.IsPassable(column, row)) return false;
            }
            return false;
        }

        private static bool FiniteCoursePoint(VoyagePoint point) =>
            !float.IsNaN(point.X) && !float.IsInfinity(point.X) && !float.IsNaN(point.Y) && !float.IsInfinity(point.Y);

        private VoyagePoint NormalizeDirectCourseTarget(VoyagePoint target) => SnapToNearestPassable(new VoyagePoint(
            Clamp(target.X, 2f, Map.WorldWidth - 2f), Clamp(target.Y, 2f, Map.WorldHeight - 2f)));

        private static List<VoyagePoint> TruncateCoursePath(List<VoyagePoint> path, float maximumMapDistance)
        {
            if (PathDistance(path) <= maximumMapDistance) return path;
            var leg = new List<VoyagePoint> { path[0] };
            float remaining = Math.Max(0f, maximumMapDistance);
            for (int index = 1; index < path.Count; index++)
            {
                VoyagePoint start = path[index - 1], end = path[index];
                float distance = Distance(start, end);
                if (distance <= remaining)
                {
                    leg.Add(end);
                    remaining -= distance;
                    continue;
                }
                float t = remaining / distance;
                leg.Add(new VoyagePoint(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t));
                break;
            }
            return leg;
        }

        private void UpdateCourseHeading(IReadOnlyList<VoyagePoint> path)
        {
            if (path == null) return;
            for (int index = 1; index < path.Count; index++)
            {
                float dx = path[index].X - path[index - 1].X, dy = path[index].Y - path[index - 1].Y;
                if (Math.Abs(dx) + Math.Abs(dy) < 0.0001f) continue;
                Heading = ClosestDirection(Math.Atan2(dy, dx));
                return;
            }
        }

        private void RefreshDirectCourse()
        {
            if (!HasDirectCourseTarget || Map == null) return;
            VoyagePoint target = NormalizeDirectCourseTarget(DirectCourseTarget);
            if (Distance(Position, target) < 0.0001f)
            {
                HasDirectCourseTarget = false;
                return;
            }
            UpdateCourseHeading(SnapPathToDestination(FindNavigablePath(Position, target)));
        }
    }
}
