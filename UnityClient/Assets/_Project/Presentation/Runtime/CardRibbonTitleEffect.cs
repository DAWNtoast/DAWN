using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Visible glyph center line, measured on the authored ribbon in card-local units.</summary>
    [Serializable]
    public sealed class CardRibbonTitleLayout
    {
        public float centerY, slope, edgeRise, halfWidth, halfThickness;
        public static CardRibbonTitleLayout Default => new CardRibbonTitleLayout
        { centerY = -2.3f, slope = .004f, edgeRise = 13f, halfWidth = 87f, halfThickness = 11f };
        public float HeightAt(float x) => centerY + slope * x + edgeRise * x * x / (halfWidth * halfWidth);
        public float AngleAt(float x) => Mathf.Clamp(Mathf.Atan(slope + 2f * edgeRise * x / (halfWidth * halfWidth)),
            -15f * Mathf.Deg2Rad, 15f * Mathf.Deg2Rad);
        public bool Contains(Vector2 point, float tolerance = .25f) => Mathf.Abs(point.x) <= halfWidth + tolerance &&
            Mathf.Abs(point.y - HeightAt(point.x)) <= halfThickness + tolerance;
    }

    /// <summary>One dynamic Text; each glyph follows the paper without shearing its authored strokes.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed class CardRibbonTitleEffect : BaseMeshEffect
    {
        private readonly List<UIVertex> vertices = new List<UIVertex>();
        private readonly List<UIVertex> measuredVertices = new List<UIVertex>();
        private readonly List<UIVertex> rasterVertices = new List<UIVertex>();
        private readonly TextGenerator measurement = new TextGenerator();
        private readonly TextGenerator rendering = new TextGenerator();
        private readonly UIVertex[] glyphQuad = new UIVertex[4];
        private bool buildingMesh;
        [SerializeField] private string frameId;
        public string FrameId => frameId;
        public CardRibbonTitleLayout Layout { get; private set; } = CardRibbonTitleLayout.Default;
        public Rect RenderedBounds { get; private set; }
        public bool FitsAuthoredRibbon { get; private set; }
        public int RenderedGlyphCount { get; private set; }
        public float RenderedPixelsPerUnit { get; private set; } = 1f;

        private float ScreenPixelsPerUnit(Text text)
        {
            Canvas canvas = text.canvas;
            if (canvas == null || text.font == null || !text.font.dynamic) return 1f;
            Canvas root = canvas.rootCanvas;
            Matrix4x4 relative = root.transform.worldToLocalMatrix * text.rectTransform.localToWorldMatrix;
            float scale = Mathf.Max(relative.MultiplyVector(Vector3.right).magnitude,
                relative.MultiplyVector(Vector3.up).magnitude) * root.scaleFactor;
            // Quantize upward so animated hover never undersamples or requests a
            // different dynamic-font atlas size on every animation frame.
            return Mathf.Max(1f, Mathf.Ceil(scale - .001f));
        }

        private void LateUpdate()
        {
            // Transform scaling does not dirty a Graphic's local mesh. A card
            // becoming larger on hover must request its denser glyphs explicitly.
            if (!buildingMesh && graphic != null &&
                ScreenPixelsPerUnit(GetComponent<Text>()) != RenderedPixelsPerUnit)
                graphic.SetVerticesDirty();
        }

        public void ConfigureForFrame(string frameId)
        {
            this.frameId = frameId;
            Layout = CardSchoolArtCatalog.TitleLayoutForFrame(frameId);
            if (graphic != null) graphic.SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Font.textureRebuilt += FontTextureRebuilt;
            CardSchoolFrameGraphic frame = transform.parent?.parent?.GetComponentInChildren<CardSchoolFrameGraphic>();
            ConfigureForFrame(frame != null && frame.HasAuthoredTexture ? frame.mainTexture.name : frameId);
        }

        protected override void OnDisable()
        {
            Font.textureRebuilt -= FontTextureRebuilt;
            base.OnDisable();
        }

        private void FontTextureRebuilt(Font changedFont)
        {
            if (GetComponent<Text>().font != changedFont) return;
            // Text invalidates only its own generators. Our canonical streams
            // must also discard atlas UVs when another label grows the atlas.
            measurement.Invalidate();
            rendering.Invalidate();
        }

        protected override void OnDestroy()
        {
            ((IDisposable)measurement).Dispose();
            ((IDisposable)rendering).Dispose();
            base.OnDestroy();
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || buildingMesh) return;
            buildingMesh = true;
            try
            {
            Text text = GetComponent<Text>();
            RenderedPixelsPerUnit = ScreenPixelsPerUnit(text);
            // The canonical stream owns fitting, glyph centers and the ribbon
            // curve. Only the glyph raster density follows the displayed card.
            PopulateCanonical(text, measurement, vertices);
            float canonicalCenterY = float.NaN;
            if (RenderedPixelsPerUnit > 1f)
            {
                PopulateCanonical(text, rendering, rasterVertices, scale: RenderedPixelsPerUnit);
                if (rasterVertices.Count == vertices.Count)
                    canonicalCenterY = ApplyRasterGlyphs(vertices, rasterVertices, RenderedPixelsPerUnit);
                else
                {
                    // A font-atlas rebuild can invalidate the earlier UVs.
                    // Regenerate the fallback after the final atlas request.
                    PopulateCanonical(text, measurement, vertices);
                    RenderedPixelsPerUnit = 1f;
                }
            }
            TransformGlyphs(vertices, Layout, out Rect bounds, out int glyphs, out bool fits, out _, canonicalCenterY);
            RenderedBounds = bounds;
            RenderedGlyphCount = glyphs;
            FitsAuthoredRibbon = glyphs > 0 && fits;
            helper.Clear();
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                for (int j = 0; j < 4; j++) glyphQuad[j] = vertices[i + j];
                helper.AddUIVertexQuad(glyphQuad);
            }
            }
            finally { buildingMesh = false; }
        }

        public bool GeneratedTextFits(Text text, string content = null, int fontSize = 0)
        {
            if (text == null || text.font == null || string.IsNullOrEmpty(text.text)) return false;
            PopulateCanonical(text, measurement, measuredVertices, content, fontSize);
            // Match this project's UGUI Text.OnPopulateMesh: it consumes the
            // complete vertex stream. Empty quads are ignored below; a final
            // real glyph (including the +3 suffix) must never escape fitting.
            TransformGlyphs(measuredVertices, Layout, out _, out int glyphs, out bool fits, out _);
            // Font quads include transparent atlas padding. The measured paper
            // band already constrains every rotated corner; a second raw quad
            // height cap would make this display face unnecessarily small.
            return glyphs > 0 && fits;
        }

        private static void PopulateCanonical(Text text, TextGenerator generator,
            List<UIVertex> target, string content = null, int fontSize = 0, float scale = 1f)
        {
            TextGenerationSettings settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.scaleFactor = scale;
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            if (fontSize > 0) settings.fontSize = fontSize;
            // FontUpdateTracker can immediately rebuild Text before our later
            // textureRebuilt callback runs. Never reuse cached UVs in that path.
            generator.Invalidate();
            generator.Populate(content ?? text.text, settings);
            target.Clear();
            target.AddRange(generator.verts);
        }

        private static float ApplyRasterGlyphs(List<UIVertex> canonical, List<UIVertex> raster, float density)
        {
            // Unity's dynamic-font quads include a fixed number of transparent
            // atlas pixels. Normalize the dense quad, including that padding,
            // instead of stretching its UVs over the larger 1x padded quad.
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i + 3 < canonical.Count; i += 4)
            {
                Rect placement = QuadBounds(canonical, i);
                Rect sampled = QuadBounds(raster, i);
                if (placement.width < .001f || placement.height < .001f ||
                    sampled.width < .001f || sampled.height < .001f) continue;
                minY = Mathf.Min(minY, placement.yMin);
                maxY = Mathf.Max(maxY, placement.yMax);
                Vector2 size = sampled.size / density;
                // Raster hinting may round an edge outward. Never enlarge the
                // fitted paper bounds because of that subpixel rounding.
                size = Vector2.Min(size, placement.size);
                for (int j = 0; j < 4; j++)
                {
                    UIVertex vertex = raster[i + j];
                    Vector2 relative = (Vector2)vertex.position - sampled.center;
                    Vector2 point = placement.center + Vector2.Scale(relative,
                        new Vector2(size.x / sampled.width, size.y / sampled.height));
                    vertex.position = new Vector3(point.x, point.y, canonical[i + j].position.z);
                    canonical[i + j] = vertex;
                }
            }
            return float.IsInfinity(minY) ? float.NaN : (minY + maxY) * .5f;
        }

        private static void TransformGlyphs(List<UIVertex> mesh, CardRibbonTitleLayout layout,
            out Rect bounds, out int glyphs, out bool fits, out float maximumGlyphHeight,
            float canonicalCenterY = float.NaN)
        {
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            maximumGlyphHeight = 0f;
            glyphs = 0;
            for (int i = 0; i + 3 < mesh.Count; i += 4)
            {
                Rect quad = QuadBounds(mesh, i);
                if (quad.width < .001f || quad.height < .001f) continue;
                minY = Mathf.Min(minY, quad.yMin); maxY = Mathf.Max(maxY, quad.yMax);
                maximumGlyphHeight = Mathf.Max(maximumGlyphHeight, quad.height);
                glyphs++;
            }
            bounds = Rect.zero; fits = true;
            if (glyphs == 0) return;
            // One vertical reference preserves baseline relationships in Latin +3 suffixes.
            float midY = float.IsNaN(canonicalCenterY) ? (minY + maxY) * .5f : canonicalCenterY;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i + 3 < mesh.Count; i += 4)
            {
                Rect quad = QuadBounds(mesh, i);
                if (quad.width < .001f || quad.height < .001f) continue;
                float x = quad.center.x, angle = layout.AngleAt(x);
                float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
                Vector2 pivot = new Vector2(x, layout.HeightAt(x));
                for (int j = 0; j < 4; j++)
                {
                    UIVertex vertex = mesh[i + j];
                    Vector2 relative = new Vector2(vertex.position.x - x, vertex.position.y - midY);
                    Vector2 point = pivot + new Vector2(cos * relative.x - sin * relative.y, sin * relative.x + cos * relative.y);
                    vertex.position = new Vector3(point.x, point.y, vertex.position.z);
                    mesh[i + j] = vertex;
                    minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
                    fits &= layout.Contains(point);
                }
            }
            bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static Rect QuadBounds(List<UIVertex> mesh, int offset)
        {
            Vector2 minimum = mesh[offset].position, maximum = minimum;
            for (int i = 1; i < 4; i++)
            { minimum = Vector2.Min(minimum, mesh[offset + i].position); maximum = Vector2.Max(maximum, mesh[offset + i].position); }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }
    }
}
