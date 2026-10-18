using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalTacticalGridGraphic : MaskableGraphic
    {
        private float cellSize = 75f;
        private Vector2 origin;
        public Vector2 GridOriginPixels => origin;

        public void SetGrid(float pixelsPerCell, Vector2 worldOffset)
        {
            raycastTarget = false;
            if (Mathf.Approximately(cellSize, pixelsPerCell) && origin == worldOffset) return;
            cellSize = Mathf.Max(2f, pixelsPerCell);
            origin = worldOffset;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect area = rectTransform.rect;
            float firstX = Mathf.Floor((area.xMin - origin.x) / cellSize) * cellSize + origin.x;
            float firstY = Mathf.Floor((area.yMin - origin.y) / cellSize) * cellSize + origin.y;
            for (float x = firstX; x <= area.xMax; x += cellSize)
                NavalTacticalTrackGraphic.AddLine(helper, new Vector2(x, area.yMin), new Vector2(x, area.yMax), 1.25f, color);
            for (float y = firstY; y <= area.yMax; y += cellSize)
                NavalTacticalTrackGraphic.AddLine(helper, new Vector2(area.xMin, y), new Vector2(area.xMax, y), 1.25f, color);
        }
    }
}
