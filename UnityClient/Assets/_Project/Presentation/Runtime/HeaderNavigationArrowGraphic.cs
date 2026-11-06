using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A direction cue belonging to the original facility navigation button.</summary>
    [DisallowMultipleComponent]
    public sealed class HeaderNavigationArrowGraphic : MaskableGraphic
    {
        [SerializeField] private bool pointsRight;
        [SerializeField] private Button owner;
        public bool PointsRight => pointsRight;

        public void Configure(bool right, Button button)
        {
            pointsRight = right;
            owner = button;
            color = new Color(.88f, .73f, .43f, 1f);
            raycastTarget = true;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            // Match hover/press/disabled tint without introducing another selectable.
            if (owner != null && owner.targetGraphic != null)
                canvasRenderer.SetColor(owner.targetGraphic.canvasRenderer.GetColor());
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width / 24f, rect.height / 40f);
            if (scale <= 0f) return;
            float direction = pointsRight ? 1f : -1f;
            Vector2 top = rect.center + new Vector2(-6f * direction, 14f) * scale;
            Vector2 tip = rect.center + new Vector2(6f * direction, 0f) * scale;
            Vector2 bottom = rect.center + new Vector2(-6f * direction, -14f) * scale;
            Color edge = new Color(.14f, .11f, .06f, color.a);
            Stroke(mesh, top, tip, 6f * scale, edge);
            Stroke(mesh, tip, bottom, 6f * scale, edge);
            Stroke(mesh, top, tip, 3.5f * scale, color);
            Stroke(mesh, tip, bottom, 3.5f * scale, color);
        }

        private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 delta = b - a;
            Vector2 side = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(a - side, tint, Vector2.zero);
            mesh.AddVert(a + side, tint, Vector2.zero);
            mesh.AddVert(b + side, tint, Vector2.zero);
            mesh.AddVert(b - side, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
