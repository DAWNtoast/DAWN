using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalTacticalRouteGraphic : MaskableGraphic
    {
        private NavalTacticalBoardView board;
        private readonly List<VoyagePoint> path = new List<VoyagePoint>();
        private readonly List<VoyagePoint> next = new List<VoyagePoint>();
        private readonly List<VoyagePoint> reachable = new List<VoyagePoint>();

        public IReadOnlyList<VoyagePoint> ReachableCells => reachable.AsReadOnly();
        public IReadOnlyList<VoyagePoint> PathPoints => path.AsReadOnly();
        public bool EndsInRam { get; private set; }

        public void Configure(NavalTacticalBoardView view, IReadOnlyList<CoreNavalMovementNode> nodes, IReadOnlyList<VoyagePoint> options,
            IReadOnlyList<VoyagePoint> reachableCells = null, bool endsInRam = false)
        {
            board = view;
            EndsInRam = endsInRam;
            path.Clear(); next.Clear(); reachable.Clear();
            foreach (CoreNavalMovementNode node in nodes) path.Add(node.PositionMeters);
            foreach (VoyagePoint point in options) next.Add(point);
            if (reachableCells != null) foreach (VoyagePoint point in reachableCells) reachable.Add(point);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (board == null) return;
            // Movement owns overlapping cells; the attack layer leaves these
            // cells clear so the turquoise fill never blends with attack red.
            Rect visible = ((RectTransform)board.transform).rect;
            float margin = board.CellSizePixels;
            visible = Rect.MinMaxRect(visible.xMin - margin, visible.yMin - margin, visible.xMax + margin, visible.yMax + margin);
            foreach (VoyagePoint point in reachable)
                if (visible.Contains(board.WorldToBoard(point)))
                    DrawCell(helper, point, new Color(.12f, .8f, .74f, .24f), new Color(.18f, .9f, .89f, .8f));
            foreach (VoyagePoint point in next) DrawCell(helper, point, new Color(.12f, .8f, .74f, .12f), new Color(.35f, 1f, .9f, 1f));
            for (int i = 1; i < path.Count; i++)
            {
                bool ramTarget = EndsInRam && i == path.Count - 1;
                Color routeColor = ramTarget ? new Color(1f, .35f, .24f) : new Color(1f, .85f, .4f);
                DrawCell(helper, path[i], ramTarget ? new Color(1f, .24f, .16f, .30f) : new Color(1f, .77f, .23f, .20f),
                    ramTarget ? routeColor : new Color(1f, .8f, .3f, .9f));
                Vector2 from = board.WorldToBoard(path[i - 1]), to = board.WorldToBoard(path[i]);
                Vector2 direction = (to - from).normalized, side = new Vector2(-direction.y, direction.x);
                NavalTacticalTrackGraphic.AddLine(helper, from, to, 3f, routeColor);
                NavalTacticalTrackGraphic.AddLine(helper, to, to - direction * 11f + side * 7f, 3f, routeColor);
                NavalTacticalTrackGraphic.AddLine(helper, to, to - direction * 11f - side * 7f, 3f, routeColor);
            }
        }

        private void DrawCell(VertexHelper helper, VoyagePoint center, Color fill, Color border)
        {
            const float half = CoreVoyageSession.NavalGridCellMeters * .46f;
            var corners = new[] {
                board.WorldToBoard(new VoyagePoint(center.X - half, center.Y - half)),
                board.WorldToBoard(new VoyagePoint(center.X - half, center.Y + half)),
                board.WorldToBoard(new VoyagePoint(center.X + half, center.Y + half)),
                board.WorldToBoard(new VoyagePoint(center.X + half, center.Y - half)) };
            int start = helper.currentVertCount;
            foreach (Vector2 corner in corners) helper.AddVert(corner, fill, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
            for (int i = 0; i < 4; i++) NavalTacticalTrackGraphic.AddLine(helper, corners[i], corners[(i + 1) % 4], 2f, border);
        }
    }
}
