using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalTacticalAttackRangeGraphic : MaskableGraphic
    {
        private static readonly Color FillTint = new Color(1f, .16f, .14f, .24f);
        private static readonly Color BorderTint = new Color(1f, .32f, .26f, .88f);
        private const int MaxMeshVertices = 64000;
        private readonly List<VoyagePoint> cells = new List<VoyagePoint>();
        private readonly HashSet<Vector2Int> movementGrid = new HashSet<Vector2Int>();
        private readonly List<Rect> movementAreas = new List<Rect>();
        private readonly List<Rect> rectangleParts = new List<Rect>();
        private readonly List<Vector2> lineIntervals = new List<Vector2>();
        private readonly Vector2[] corners = new Vector2[4];
        private ReadOnlyCollection<VoyagePoint> readOnlyCells;
        private NavalTacticalBoardView board;
        private VoyagePoint unlimitedOrigin;

        public bool IsUnlimitedPreview { get; private set; }
        public IReadOnlyList<VoyagePoint> Cells => readOnlyCells ?? (readOnlyCells = cells.AsReadOnly());

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        public void Configure(NavalTacticalBoardView view, IReadOnlyList<VoyagePoint> attackCells,
            IReadOnlyList<VoyagePoint> movementCells = null)
        {
            board = view;
            IsUnlimitedPreview = false;
            // Copy the source so changing a card or rebuilding a movement route cannot mutate this overlay.
            var copy = attackCells == null ? null : new List<VoyagePoint>(attackCells.Count);
            if (copy != null)
                for (int index = 0; index < attackCells.Count; index++) copy.Add(attackCells[index]);
            CopyMovementCells(view == null ? null : movementCells);
            cells.Clear();
            if (copy != null) cells.AddRange(copy);
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void ConfigureUnlimited(NavalTacticalBoardView view, VoyagePoint origin,
            IReadOnlyList<VoyagePoint> movementCells = null)
        {
            board = view;
            unlimitedOrigin = origin;
            IsUnlimitedPreview = true;
            CopyMovementCells(view == null ? null : movementCells);
            cells.Clear();
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (board == null) return;
            if (IsUnlimitedPreview) { DrawUnlimited(helper); return; }
            foreach (VoyagePoint cell in cells)
                if (!movementGrid.Contains(GridCell(cell))) DrawCell(helper, cell);
        }

        private static Vector2Int GridCell(VoyagePoint point) => new Vector2Int(
            Mathf.RoundToInt(point.X / CoreVoyageSession.NavalGridCellMeters),
            Mathf.RoundToInt(point.Y / CoreVoyageSession.NavalGridCellMeters));

        private void CopyMovementCells(IReadOnlyList<VoyagePoint> source)
        {
            movementGrid.Clear();
            movementAreas.Clear();
            if (source == null) return;
            for (int index = 0; index < source.Count; index++)
            {
                VoyagePoint point = source[index];
                if (!float.IsNaN(point.X) && !float.IsNaN(point.Y) &&
                    !float.IsInfinity(point.X) && !float.IsInfinity(point.Y)) movementGrid.Add(GridCell(point));
            }
            // Store values rather than the caller's mutable route list. Merging
            // each row keeps unlimited preview clipping proportional to its edges.
            var ordered = new List<Vector2Int>(movementGrid);
            ordered.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            const float size = CoreVoyageSession.NavalGridCellMeters;
            for (int index = 0; index < ordered.Count;)
            {
                Vector2Int first = ordered[index++], last = first;
                while (index < ordered.Count && ordered[index].y == first.y && ordered[index].x == last.x + 1)
                    last = ordered[index++];
                movementAreas.Add(Rect.MinMaxRect(first.x * size - size * .5f, first.y * size - size * .5f,
                    last.x * size + size * .5f, first.y * size + size * .5f));
            }
        }

        private void DrawUnlimited(VertexHelper helper)
        {
            Rect visible = ((RectTransform)board.transform).rect;
            VoyagePoint bottomLeft = board.BoardToWorld(new Vector2(visible.xMin, visible.yMin));
            VoyagePoint topLeft = board.BoardToWorld(new Vector2(visible.xMin, visible.yMax));
            VoyagePoint topRight = board.BoardToWorld(new Vector2(visible.xMax, visible.yMax));
            VoyagePoint bottomRight = board.BoardToWorld(new Vector2(visible.xMax, visible.yMin));
            float minX = Mathf.Min(bottomLeft.X, topLeft.X, topRight.X, bottomRight.X);
            float maxX = Mathf.Max(bottomLeft.X, topLeft.X, topRight.X, bottomRight.X);
            float minY = Mathf.Min(bottomLeft.Y, topLeft.Y, topRight.Y, bottomRight.Y);
            float maxY = Mathf.Max(bottomLeft.Y, topLeft.Y, topRight.Y, bottomRight.Y);
            const float cellSize = CoreVoyageSession.NavalGridCellMeters;
            float originX = Mathf.Round(unlimitedOrigin.X / cellSize) * cellSize;
            float originY = Mathf.Round(unlimitedOrigin.Y / cellSize) * cellSize;
            float left = Mathf.Clamp(originX - cellSize * .5f, minX, maxX);
            float right = Mathf.Clamp(originX + cellSize * .5f, minX, maxX);
            float bottom = Mathf.Clamp(originY - cellSize * .5f, minY, maxY);
            float top = Mathf.Clamp(originY + cellSize * .5f, minY, maxY);
            // Four large quads leave the player's cell clear without tessellating thousands of cells.
            DrawUncoveredRectangle(helper, minX, minY, left, maxY);
            DrawUncoveredRectangle(helper, right, minY, maxX, maxY);
            DrawUncoveredRectangle(helper, left, minY, right, bottom);
            DrawUncoveredRectangle(helper, left, top, right, maxY);

            // Preserve grid boundaries at any bearing while keeping far-away cells and meshes readable.
            float strideCells = Mathf.Max(1f, Mathf.Ceil(3f / Mathf.Max(.0001f, board.CellSizePixels)));
            strideCells = Mathf.Max(strideCells, Mathf.Ceil(Mathf.Max(maxX - minX, maxY - minY) / cellSize / 2047f));
            float stride = strideCells * cellSize;
            float firstX = Mathf.Ceil((minX - cellSize * .5f) / stride) * stride + cellSize * .5f;
            float firstY = Mathf.Ceil((minY - cellSize * .5f) / stride) * stride + cellSize * .5f;
            int columns = Mathf.Min(2048, Mathf.Max(0, Mathf.FloorToInt((maxX - firstX) / stride) + 1));
            int rows = Mathf.Min(2048, Mathf.Max(0, Mathf.FloorToInt((maxY - firstY) / stride) + 1));
            for (int index = 0; index < columns && helper.currentVertCount + 4 <= MaxMeshVertices; index++)
            {
                float x = firstX + index * stride;
                DrawUncoveredGridLine(helper, true, x, minY, maxY);
            }
            for (int index = 0; index < rows && helper.currentVertCount + 4 <= MaxMeshVertices; index++)
            {
                float y = firstY + index * stride;
                DrawUncoveredGridLine(helper, false, y, minX, maxX);
            }
        }

        private void DrawUncoveredRectangle(VertexHelper helper, float minX, float minY, float maxX, float maxY)
        {
            if (maxX <= minX || maxY <= minY) return;
            rectangleParts.Clear();
            rectangleParts.Add(Rect.MinMaxRect(minX, minY, maxX, maxY));
            foreach (Rect exclusion in movementAreas)
            {
                for (int index = rectangleParts.Count - 1; index >= 0; index--)
                {
                    Rect part = rectangleParts[index];
                    float left = Mathf.Max(part.xMin, exclusion.xMin), right = Mathf.Min(part.xMax, exclusion.xMax);
                    float bottom = Mathf.Max(part.yMin, exclusion.yMin), top = Mathf.Min(part.yMax, exclusion.yMax);
                    if (right <= left || top <= bottom) continue;
                    rectangleParts.RemoveAt(index);
                    AddRectanglePart(part.xMin, part.yMin, left, part.yMax);
                    AddRectanglePart(right, part.yMin, part.xMax, part.yMax);
                    AddRectanglePart(left, part.yMin, right, bottom);
                    AddRectanglePart(left, top, right, part.yMax);
                }
            }
            foreach (Rect part in rectangleParts)
                DrawWorldRectangle(helper, part.xMin, part.yMin, part.xMax, part.yMax);
        }

        private void AddRectanglePart(float minX, float minY, float maxX, float maxY)
        {
            if (maxX > minX && maxY > minY) rectangleParts.Add(Rect.MinMaxRect(minX, minY, maxX, maxY));
        }

        private void DrawUncoveredGridLine(VertexHelper helper, bool vertical, float axis, float from, float to, float width = 1.5f)
        {
            lineIntervals.Clear();
            // Clip the line's full pixel width too, including lines lying on a
            // movement cell boundary. No red border should bleed into that cell.
            float padding = width * .5f * CoreVoyageSession.NavalGridCellMeters / Mathf.Max(.0001f, board.CellSizePixels);
            foreach (Rect exclusion in movementAreas)
            {
                float near = vertical ? exclusion.xMin : exclusion.yMin;
                float far = vertical ? exclusion.xMax : exclusion.yMax;
                if (axis + padding < near || axis - padding > far) continue;
                float start = Mathf.Max(from, vertical ? exclusion.yMin : exclusion.xMin);
                float end = Mathf.Min(to, vertical ? exclusion.yMax : exclusion.xMax);
                if (end > start) lineIntervals.Add(new Vector2(start, end));
            }
            lineIntervals.Sort((a, b) => a.x.CompareTo(b.x));
            float cursor = from;
            foreach (Vector2 interval in lineIntervals)
            {
                if (interval.x > cursor) DrawGridLine(helper, vertical, axis, cursor, interval.x, width);
                cursor = Mathf.Max(cursor, interval.y);
                if (cursor >= to) return;
            }
            DrawGridLine(helper, vertical, axis, cursor, to, width);
        }

        private void DrawGridLine(VertexHelper helper, bool vertical, float axis, float from, float to, float width)
        {
            if (to <= from || helper.currentVertCount + 4 > MaxMeshVertices) return;
            VoyagePoint start = vertical ? new VoyagePoint(axis, from) : new VoyagePoint(from, axis);
            VoyagePoint end = vertical ? new VoyagePoint(axis, to) : new VoyagePoint(to, axis);
            NavalTacticalTrackGraphic.AddLine(helper, board.WorldToBoard(start), board.WorldToBoard(end), width, BorderTint);
        }

        private void DrawWorldRectangle(VertexHelper helper, float minX, float minY, float maxX, float maxY)
        {
            if (maxX <= minX || maxY <= minY || helper.currentVertCount + 4 > MaxMeshVertices) return;
            int start = helper.currentVertCount;
            helper.AddVert(board.WorldToBoard(new VoyagePoint(minX, minY)), FillTint, Vector2.zero);
            helper.AddVert(board.WorldToBoard(new VoyagePoint(minX, maxY)), FillTint, Vector2.zero);
            helper.AddVert(board.WorldToBoard(new VoyagePoint(maxX, maxY)), FillTint, Vector2.zero);
            helper.AddVert(board.WorldToBoard(new VoyagePoint(maxX, minY)), FillTint, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }

        private void DrawCell(VertexHelper helper, VoyagePoint center)
        {
            if (helper.currentVertCount + 20 > MaxMeshVertices) return;
            // Leave a narrow gap between adjacent cells so their grid boundaries remain readable.
            const float half = CoreVoyageSession.NavalGridCellMeters * .46f;
            corners[0] = board.WorldToBoard(new VoyagePoint(center.X - half, center.Y - half));
            corners[1] = board.WorldToBoard(new VoyagePoint(center.X - half, center.Y + half));
            corners[2] = board.WorldToBoard(new VoyagePoint(center.X + half, center.Y + half));
            corners[3] = board.WorldToBoard(new VoyagePoint(center.X + half, center.Y - half));
            int start = helper.currentVertCount;
            for (int index = 0; index < corners.Length; index++) helper.AddVert(corners[index], FillTint, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
            DrawUncoveredGridLine(helper, true, center.X - half, center.Y - half, center.Y + half, 2f);
            DrawUncoveredGridLine(helper, true, center.X + half, center.Y - half, center.Y + half, 2f);
            DrawUncoveredGridLine(helper, false, center.Y - half, center.X - half, center.X + half, 2f);
            DrawUncoveredGridLine(helper, false, center.Y + half, center.X - half, center.X + half, 2f);
        }
    }
}
