using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Soft, seeded edges for water and translucent chart-detail overlays. The
    /// original illustration and UV rectangle remain intact; only vertex alpha
    /// changes, so the standard UGUI material and stencil/RectMask2D paths work.
    /// Solid coastlines belong to the continuous terrain surface instead.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Voyage Map Blend Graphic")]
    public sealed class VoyageMapBlendGraphic : RawImage
    {
        // (250 + 1)^2 = 63,001 vertices, below UGUI's 65,000-vertex limit.
        private const int MaximumSubdivisions = 250;
        private const float DefaultFeatherFraction = 0.16f;

        [SerializeField, Range(0.02f, 0.35f)]
        private float featherFraction = DefaultFeatherFraction;
        [SerializeField]
        private uint seed;
        [SerializeField, Range(8, MaximumSubdivisions)]
        private int subdivisions = 24;

        /// <summary>
        /// Feather width is a fraction of the shorter side of the graphic. This
        /// keeps long water patches rectangular rather than fading into ovals.
        /// Subdivisions describe the shorter side; the longer side is refined
        /// proportionally, within the mesh limit. No global random state is used.
        /// </summary>
        public void Configure(float featherFraction, uint seed, int subdivisions = 24)
        {
            this.featherFraction = SanitizeFeather(featherFraction);
            this.seed = seed;
            this.subdivisions = Mathf.Clamp(subdivisions, 8, MaximumSubdivisions);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect bounds = GetPixelAdjustedRect();
            if (bounds.width <= 0f || bounds.height <= 0f || mainTexture == null)
                return;

            float shorterSide = Mathf.Min(bounds.width, bounds.height);
            int resolution = Mathf.Clamp(subdivisions, 8, MaximumSubdivisions);
            int columns = Mathf.Clamp(Mathf.CeilToInt(resolution * bounds.width / shorterSide),
                8, MaximumSubdivisions);
            int rows = Mathf.Clamp(Mathf.CeilToInt(resolution * bounds.height / shorterSide),
                8, MaximumSubdivisions);
            float feather = SanitizeFeather(featherFraction) * shorterSide;
            Rect sourceUv = uvRect;
            UIVertex vertex = UIVertex.simpleVert;
            Color tint = color;

            for (int row = 0; row <= rows; row += 1)
            {
                float v = (float)row / rows;
                for (int column = 0; column <= columns; column += 1)
                {
                    float u = (float)column / columns;
                    float alpha = 0f;
                    // Boundary alpha is exactly zero, independently of noise,
                    // tint, UV flips, or floating-point interpolation rounding.
                    if (row != 0 && row != rows && column != 0 && column != columns)
                    {
                        float left = Fade(u * bounds.width,
                            feather * EdgeWidth(v, seed ^ 0xA341316Cu));
                        float right = Fade((1f - u) * bounds.width,
                            feather * EdgeWidth(v, seed ^ 0xC8013EA4u));
                        float bottom = Fade(v * bounds.height,
                            feather * EdgeWidth(u, seed ^ 0xAD90777Du));
                        float top = Fade((1f - v) * bounds.height,
                            feather * EdgeWidth(u, seed ^ 0x7E95761Eu));
                        alpha = left * right * bottom * top;
                    }

                    vertex.position = new Vector3(bounds.xMin + u * bounds.width,
                        bounds.yMin + v * bounds.height, 0f);
                    // A negative UV width/height preserves RawImage-style flips.
                    vertex.uv0 = new Vector2(sourceUv.x + u * sourceUv.width,
                        sourceUv.y + v * sourceUv.height);
                    vertex.color = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
                    vertexHelper.AddVert(vertex);
                }
            }

            int stride = columns + 1;
            for (int row = 0; row < rows; row += 1)
            {
                for (int column = 0; column < columns; column += 1)
                {
                    int bottomLeft = row * stride + column;
                    int topLeft = bottomLeft + stride;
                    vertexHelper.AddTriangle(bottomLeft, topLeft, topLeft + 1);
                    vertexHelper.AddTriangle(topLeft + 1, bottomLeft + 1, bottomLeft);
                }
            }
        }

        private static float SanitizeFeather(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return DefaultFeatherFraction;
            return Mathf.Clamp(value, 0.02f, 0.35f);
        }

        private static float Fade(float distance, float width)
        {
            float t = Mathf.Clamp01(distance / width);
            // Zero slope at both ends leaves a genuinely opaque central area.
            return t * t * (3f - 2f * t);
        }

        private static float EdgeWidth(float alongEdge, uint edgeSeed)
        {
            // Two smooth, low-amplitude bands avoid straight seams without
            // imposing the conspicuous circular outline of a radial mask.
            float broad = ValueNoise(alongEdge * 3.6f, edgeSeed);
            float detail = ValueNoise(alongEdge * 8.4f, edgeSeed ^ 0x9E3779B9u);
            return 1f + (broad * 2f - 1f) * 0.15f + (detail * 2f - 1f) * 0.07f;
        }

        private static float ValueNoise(float coordinate, uint noiseSeed)
        {
            int cell = Mathf.FloorToInt(coordinate);
            float t = coordinate - cell;
            t = t * t * (3f - 2f * t);
            float start = Hash01(unchecked(noiseSeed + (uint)cell * 0x9E3779B9u));
            float end = Hash01(unchecked(noiseSeed + (uint)(cell + 1) * 0x9E3779B9u));
            return Mathf.Lerp(start, end, t);
        }

        private static float Hash01(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
            }
            return (value & 0x00FFFFFFu) / 16777215f;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            featherFraction = SanitizeFeather(featherFraction);
            subdivisions = Mathf.Clamp(subdivisions, 8, MaximumSubdivisions);
            base.OnValidate();
        }
#endif
    }
}
