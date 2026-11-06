using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A faint broad reflection beneath the tactical grid, ships and HUD.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalSeaLightGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (color.a <= 0f) return;
            const int segments = 48;
            Rect area = rectTransform.rect;
            Vector2 center = new Vector2(area.xMin + area.width * .44f, area.yMin + area.height * .57f);
            Vector2 radius = new Vector2(area.width * .37f, area.height * .42f);
            mesh.AddVert(center, color, Vector2.zero);
            Color clear = new Color(color.r, color.g, color.b, 0f);
            for (int index = 0; index <= segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y),
                    clear, Vector2.zero);
                if (index > 0) mesh.AddTriangle(0, index, index + 1);
            }
        }
    }
}
