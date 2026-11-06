using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>One textured mesh per field kind, shared by all its world-anchored cells.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalFieldPatternGraphic : MaskableGraphic
    {
        private NavalTacticalBoardView board;
        private IReadOnlyList<NavalFieldVisualCell> cells;
        private Texture2D painting;
        private float motionTime;
        public CoreNavalFieldKind Kind { get; private set; }
        public int PatternCellCount { get; private set; }
        public float PatternOpacity { get; private set; }
        public override Texture mainTexture => painting != null ? painting : Texture2D.whiteTexture;

        public void Configure(NavalTacticalBoardView view, IReadOnlyList<NavalFieldVisualCell> source,
            CoreNavalFieldKind kind, Texture2D texture)
        {
            board = view; cells = source; Kind = kind;
            if (painting != texture) { painting = texture; SetMaterialDirty(); }
            raycastTarget = false;
            PatternCellCount = 0;
            if (cells != null)
                for (int i = 0; i < cells.Count; i++)
                    if (cells[i].Pattern && cells[i].Kind == Kind) PatternCellCount++;
            SetVerticesDirty();
        }

        public void SetMotion(float seconds, float opacity)
        {
            motionTime = seconds; PatternOpacity = opacity;
            if (PatternCellCount > 0) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (board == null || painting == null || cells == null || PatternOpacity <= 0f) return;
            for (int i = 0; i < cells.Count; i++)
            {
                NavalFieldVisualCell cell = cells[i];
                if (!cell.Pattern || cell.Kind != Kind) continue;
                float phase = cell.Position.X * .013f + cell.Position.Y * .019f;
                float speed = Kind == CoreNavalFieldKind.Thunder ? 3.7f : Kind == CoreNavalFieldKind.Blood ? 2.4f : 1.6f;
                float pulse = .5f + .5f * Mathf.Sin(motionTime * speed + phase);
                float radius = .44f + .012f * pulse;
                // Tide's painting flows east. Rotate the complete square to the actual
                // world direction before board projection; never rotate the direction arrow.
                Vector2 right = Kind == CoreNavalFieldKind.Tide && (cell.DirectionX != 0 || cell.DirectionY != 0)
                    ? new Vector2(cell.DirectionX, cell.DirectionY).normalized : Vector2.right;
                Vector2 up = new Vector2(-right.y, right.x);
                Color tint = new Color(1f, 1f, 1f, PatternOpacity * (.73f + .09f * pulse));
                int start = helper.currentVertCount;
                helper.AddVert(Project(cell.Position, (-right - up) * radius), tint, new Vector2(0f, 0f));
                helper.AddVert(Project(cell.Position, (-right + up) * radius), tint, new Vector2(0f, 1f));
                helper.AddVert(Project(cell.Position, (right + up) * radius), tint, new Vector2(1f, 1f));
                helper.AddVert(Project(cell.Position, (right - up) * radius), tint, new Vector2(1f, 0f));
                helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
            }
        }

        private Vector2 Project(VoyagePoint center, Vector2 offset) => board.WorldToBoard(new VoyagePoint(
            center.X + offset.x * CoreVoyageSession.NavalGridCellMeters,
            center.Y + offset.y * CoreVoyageSession.NavalGridCellMeters));
    }
}
