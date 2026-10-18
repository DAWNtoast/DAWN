using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A faint, feathered ring around a known event, below its artwork.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageEventRiskHaloGraphic : MaskableGraphic
    {
        private const int Segments = 64;
        private static readonly float[] Radii = { .50f, .62f, .69f, .73f, .82f, 1f };
        private static readonly float[] Opacities = { 0f, .12f, .34f, .30f, .10f, 0f };

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = GetPixelAdjustedRect();
            float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
            if (radius <= 0f) return;

            // Radial vertex alpha supplies the soft glow without textures,
            // extra materials, or an animation competing with the boss pulse.
            for (int band = 0; band < Radii.Length; band++)
            {
                Color tint = color;
                tint.a *= Opacities[band];
                for (int segment = 0; segment <= Segments; segment++)
                {
                    float angle = segment * (Mathf.PI * 2f / Segments);
                    Vector2 point = bounds.center +
                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * Radii[band]);
                    mesh.AddVert(point, tint, Vector2.zero);
                    if (band == 0 || segment == 0) continue;
                    int outer = band * (Segments + 1) + segment;
                    int inner = outer - (Segments + 1);
                    mesh.AddTriangle(inner - 1, inner, outer);
                    mesh.AddTriangle(inner - 1, outer, outer - 1);
                }
            }
        }
    }
}
