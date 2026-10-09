using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BattleCardSurfaceMaterial { Copper, Sailcloth, Leather, Timber, Steel }

    public readonly struct BattleCardPalette
    {
        public readonly BattleCardSurfaceMaterial Material;
        public readonly Color Surface;
        public readonly Color Accent;
        public readonly Color Parchment;
        public readonly Color Ink;
        public readonly Color TitleInk;

        public BattleCardPalette(BattleCardSurfaceMaterial material, Color surface, Color accent,
            Color parchment, Color ink, Color titleInk)
        {
            Material = material;
            Surface = surface;
            Accent = accent;
            Parchment = parchment;
            Ink = ink;
            TitleInk = titleInk;
        }
    }

    /// <summary>Semantic category palettes, not inferred from damage/block effects.</summary>
    public static class BattleCardStylePalette
    {
        public static BattleCardPalette ForType(string type)
        {
            switch (type)
            {
                case "炮击": return Make(BattleCardSurfaceMaterial.Copper, "#8A4B2C", "#E3AA61", "#EDDCB2", "#321D15", "#6C2B13");
                case "机动": return Make(BattleCardSurfaceMaterial.Sailcloth, "#297C80", "#A2D9C8", "#DCE6CE", "#172D2C", "#175A57");
                case "登船": return Make(BattleCardSurfaceMaterial.Leather, "#823D50", "#DEA291", "#ECD5BE", "#342129", "#732B3A");
                case "维修": return Make(BattleCardSurfaceMaterial.Timber, "#737D42", "#C8C985", "#E4E3BC", "#2D321C", "#4C5424");
                case "防御": return Make(BattleCardSurfaceMaterial.Steel, "#536E90", "#B3CDE0", "#DEE3DF", "#253143", "#344D75");
                default: return Make(BattleCardSurfaceMaterial.Sailcloth, "#537F7B", "#C7D7BD", "#E7E0C6", "#253631", "#375B53");
            }
        }

        private static BattleCardPalette Make(BattleCardSurfaceMaterial material, string surface, string accent,
            string parchment, string ink, string titleInk)
        {
            return new BattleCardPalette(material, Parse(surface), Parse(accent), Parse(parchment), Parse(ink), Parse(titleInk));
        }

        private static Color Parse(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color result);
            return result;
        }
    }

    /// <summary>
    /// Resolution-independent card furniture: engraved metal, sail seams,
    /// leather stitching or timber grain surrounding a light writing panel.
    /// Only decorative geometry is emitted; the existing square art, live text,
    /// energy badge and tooltip stay above it in the regular UGUI hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Shattered Sail/Battle Card Surface")]
    public sealed class BattleCardSurfaceGraphic : MaskableGraphic
    {
        [SerializeField] private string cardType = "炮击";
        [SerializeField] private bool available = true;
        public string CardType => cardType;

        public void Configure(string type, bool canPlay)
        {
            cardType = type;
            available = canPlay;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            BattleCardPalette palette = BattleCardStylePalette.ForType(cardType);
            Color light = Color.Lerp(palette.Accent, palette.Surface, 0.24f);
            Color shadow = Color.Lerp(palette.Surface, palette.Ink, 0.36f);
            Quad(helper, rect, 0f, 0f, 1f, 1f, shadow);
            Quad(helper, rect, 0.012f, 0.012f, 0.976f, 0.976f, palette.Surface);
            Quad(helper, rect, 0.012f, 0.984f, 0.976f, 0.006f, light);
            Quad(helper, rect, 0.012f, 0.012f, 0.009f, 0.972f, palette.Accent);
            Quad(helper, rect, 0.975f, 0.012f, 0.009f, 0.972f, shadow);

            DrawMaterial(helper, rect, palette, light, shadow);

            // A recessed picture well also removes black gaps around the
            // square illustration if it has rounded or transparent corners.
            Quad(helper, rect, 0.052f, 0.358f, 0.896f, 0.595f, Color.Lerp(palette.Accent, palette.Surface, .5f));
            Quad(helper, rect, 0.065f, 0.366f, 0.87f, 0.579f, shadow);
            Quad(helper, rect, 0.045f, 0.024f, 0.91f, 0.326f, shadow);
            DrawPaper(helper, rect, palette.Parchment);
            Quad(helper, rect, 0.07f, 0.342f, 0.86f, 0.006f, light);
            Quad(helper, rect, 0.07f, 0.031f, 0.86f, 0.004f, palette.Accent);

            // Small brass corner studs are part of the border, never hit areas.
            Color stud = Color.Lerp(palette.Accent, Color.white, 0.18f);
            foreach (float x in new[] { 0.037f, 0.963f })
                foreach (float y in new[] { 0.024f, 0.975f, 0.354f })
                    Diamond(helper, rect, x, y, 0.016f, 0.011f, stud);
        }

        private void DrawPaper(VertexHelper helper, Rect rect, Color paper)
        {
            const int columns = 14;
            const int rows = 10;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float grain = Mathf.Sin(x * 7.17f + y * 19.31f) * 0.012f;
                    float edge = Mathf.Min(Mathf.Min(x, columns - 1 - x), Mathf.Min(y, rows - 1 - y));
                    Color tone = Color.Lerp(paper, new Color(0.51f, 0.35f, 0.19f, 1f), edge < 1f ? 0.09f : 0f);
                    tone += new Color(grain, grain, grain, 0f);
                    Quad(helper, rect, 0.062f + x * 0.876f / columns, 0.038f + y * 0.299f / rows,
                        0.876f / columns, 0.299f / rows, tone);
                }
            }
        }

        private void DrawMaterial(VertexHelper helper, Rect rect, BattleCardPalette palette, Color light, Color shadow)
        {
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? 0.028f : 0.96f;
                switch (palette.Material)
                {
                    case BattleCardSurfaceMaterial.Copper:
                        for (int n = 0; n < 11; n++)
                        {
                            float y = 0.07f + n * 0.08f;
                            Diamond(helper, rect, x + 0.004f, y, 0.008f, 0.006f, light);
                            Quad(helper, rect, x - 0.004f, y + 0.02f, 0.016f, 0.003f, shadow);
                        }
                        break;
                    case BattleCardSurfaceMaterial.Sailcloth:
                    case BattleCardSurfaceMaterial.Leather:
                        for (int n = 0; n < 36; n++)
                        {
                            float y = 0.05f + n * 0.025f;
                            Diamond(helper, rect, x + 0.004f, y, 0.011f,
                                palette.Material == BattleCardSurfaceMaterial.Sailcloth ? 0.010f : 0.004f, light);
                        }
                        break;
                    case BattleCardSurfaceMaterial.Timber:
                        for (int n = 0; n < 3; n++)
                            Quad(helper, rect, x - 0.006f + n * 0.006f, 0.04f + n * 0.021f, 0.002f, 0.88f - n * 0.04f,
                                n == 1 ? light : shadow);
                        for (int n = 0; n < 5; n++)
                            Diamond(helper, rect, x + 0.001f, 0.10f + n * 0.18f, 0.007f, 0.024f, shadow);
                        break;
                    case BattleCardSurfaceMaterial.Steel:
                        for (int n = 0; n < 8; n++)
                        {
                            float y = 0.07f + n * 0.12f;
                            Quad(helper, rect, x - 0.011f, y, 0.028f, 0.004f, shadow);
                            Quad(helper, rect, x - 0.011f, y + 0.005f, 0.028f, 0.003f, light);
                            Diamond(helper, rect, x + 0.003f, y + 0.057f, 0.009f, 0.006f, light);
                        }
                        break;
                }
            }
        }

        private void Quad(VertexHelper helper, Rect rect, float x, float y, float width, float height, Color tint)
        {
            int first = helper.currentVertCount;
            AddVertex(helper, rect, x, y, tint);
            AddVertex(helper, rect, x, y + height, tint);
            AddVertex(helper, rect, x + width, y + height, tint);
            AddVertex(helper, rect, x + width, y, tint);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }

        private void Diamond(VertexHelper helper, Rect rect, float x, float y, float halfWidth, float halfHeight, Color tint)
        {
            int first = helper.currentVertCount;
            AddVertex(helper, rect, x - halfWidth, y, tint);
            AddVertex(helper, rect, x, y + halfHeight, tint);
            AddVertex(helper, rect, x + halfWidth, y, tint);
            AddVertex(helper, rect, x, y - halfHeight, tint);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first, first + 2, first + 3);
        }

        private void AddVertex(VertexHelper helper, Rect rect, float x, float y, Color tint)
        {
            if (!available) tint = Color.Lerp(tint, new Color(tint.grayscale, tint.grayscale, tint.grayscale, 1f), 0.30f) * 0.88f;
            tint.a = 1f;
            tint *= color;
            helper.AddVert(new Vector3(rect.xMin + x * rect.width, rect.yMin + y * rect.height, 0f), tint, Vector2.zero);
        }
    }
}
