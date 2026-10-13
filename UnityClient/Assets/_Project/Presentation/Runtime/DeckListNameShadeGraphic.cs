using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Softens the picture under a long card name without changing the source illustration.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DeckListNameShadeGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            Color clear = new Color(color.r, color.g, color.b, 0f);
            mesh.AddVert(new Vector3(r.xMin,r.yMin),color,Vector2.zero);
            mesh.AddVert(new Vector3(r.xMin,r.yMax),color,Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax,r.yMax),clear,Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax,r.yMin),clear,Vector2.zero);
            mesh.AddTriangle(0,1,2); mesh.AddTriangle(0,2,3);
        }
    }
}
