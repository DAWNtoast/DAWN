using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Scalable card furniture; labels remain ordinary, accessible UGUI text.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CardOrnamentGraphic : MaskableGraphic
    {
        [SerializeField] private CardOrnamentKind kind;
        [SerializeField] private string cardType;
        [SerializeField] private bool useSchoolPalette;
        [SerializeField] private string schoolId;
        [SerializeField] private bool isMarkerOnly;
        public CardOrnamentKind Kind => kind;
        public bool IsMarkerOnly => isMarkerOnly;

        public void Configure(CardOrnamentKind value, string type, string school = null)
        {
            kind = value; cardType = type;
            isMarkerOnly = false;
            useSchoolPalette = school != null;
            schoolId = school;
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void ConfigureCostLabel(CardOrnamentKind value, string type)
        {
            Configure(value, type);
            // The symbol is painted into the single frame texture. Retain a
            // semantic, non-raycasting anchor for its changing numeric value.
            isMarkerOnly = value != CardOrnamentKind.Wood;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (isMarkerOnly) return;
            // The integrated frame now includes its authored name paper.
            // Retain this transform solely as the live title's layout anchor.
            if (useSchoolPalette && kind == CardOrnamentKind.NameRibbon) return;
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            var pen = new OrnamentPen(mesh, rect, color);
            BattleCardPalette palette = useSchoolPalette ? CardSchoolArtCatalog.Palette(schoolId) : BattleCardStylePalette.ForType(cardType);
            switch (kind)
            {
                case CardOrnamentKind.Energy: DrawEnergy(pen); break;
                case CardOrnamentKind.Gunpowder: DrawGunpowder(pen); break;
                case CardOrnamentKind.Cannonball: DrawCannonball(pen); break;
                case CardOrnamentKind.Wood: DrawWood(pen); break;
                case CardOrnamentKind.Tide: DrawTide(pen); break;
                case CardOrnamentKind.NameRibbon: DrawRibbon(pen, palette); break;
                case CardOrnamentKind.TypeTag: DrawTypeTag(pen, palette); break;
                case CardOrnamentKind.UpgradeArrow: DrawUpgradeArrow(pen); break;
            }
        }

        private static void DrawEnergy(OrnamentPen p)
        {
            DrawCrystal(p, Hex("#172D47"), Hex("#246B9B"), Hex("#A8E3EF"),
                Hex("#559CC5"), Hex("#32749E"), Hex("#163E65"));
        }

        private static void DrawGunpowder(OrnamentPen p)
        {
            DrawCrystal(p, Hex("#481C29"), Hex("#A43538"), Hex("#F6B3A5"),
                Hex("#D96C69"), Hex("#B54A51"), Hex("#6D2535"));
        }

        private static void DrawCannonball(OrnamentPen p)
        {
            // A shaded iron ball stays distinct from the red gunpowder crystal.
            DrawDisc(p, .5f, .5f, .5f, Hex("#141B23"));
            DrawDisc(p, .48f, .53f, .44f, Hex("#667786"));
            DrawDisc(p, .51f, .49f, .39f, Hex("#394753"));
            p.Line(.21f, .72f, .29f, .8f, .04f, Hex("#AABBC6"));
            p.Line(.29f, .8f, .41f, .85f, .035f, Hex("#AABBC6"));
        }

        private static void DrawDisc(OrnamentPen p, float x, float y, float radius, Color color)
        {
            const int segments = 32;
            var points = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points[i] = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
            }
            p.Polygon(points, color);
        }

        private static void DrawWood(OrnamentPen p)
        {
            DrawCrystal(p, Hex("#382419"), Hex("#88623D"), Hex("#E7C599"),
                Hex("#BA9367"), Hex("#88613F"), Hex("#4C3323"));
            p.Line(.13f, .15f, .13f, .85f, .025f, Hex("#ECD1A6"));
            p.Line(.87f, .15f, .87f, .85f, .025f, Hex("#4C3323"));
        }

        private static void DrawTide(OrnamentPen p)
        {
            DrawCrystal(p, Hex("#123B46"), Hex("#246A77"), Hex("#B3F0E5"),
                Hex("#67BAAD"), Hex("#328D93"), Hex("#174D5B"));
            p.Line(.12f, .18f, .35f, .14f, .035f, Hex("#B3F0E5"));
            p.Line(.35f, .14f, .65f, .18f, .035f, Hex("#B3F0E5"));
            p.Line(.65f, .18f, .88f, .14f, .035f, Hex("#B3F0E5"));
        }

        private static void DrawCrystal(OrnamentPen p, Color rim, Color centre,
            Color top, Color left, Color right, Color bottom)
        {
            // Fill the entire square so its outside edges meet the card corner.
            // The same four facets identify resources in the card and battle HUD.
            p.Quad(0f, 0f, 1f, 1f, rim);
            p.Four(new Vector2(.04f, .96f), new Vector2(.96f, .96f),
                new Vector2(.8f, .8f), new Vector2(.2f, .8f), top);
            p.Four(new Vector2(.04f, .04f), new Vector2(.04f, .96f),
                new Vector2(.2f, .8f), new Vector2(.2f, .2f), left);
            p.Four(new Vector2(.96f, .96f), new Vector2(.96f, .04f),
                new Vector2(.8f, .2f), new Vector2(.8f, .8f), right);
            p.Four(new Vector2(.96f, .04f), new Vector2(.04f, .04f),
                new Vector2(.2f, .2f), new Vector2(.8f, .2f), bottom);
            p.Quad(.2f, .2f, .6f, .6f, centre);
            p.Line(.075f, .925f, .47f, .925f, .022f, Color.Lerp(top, Color.white, .65f));
            p.Line(.075f, .61f, .075f, .925f, .018f, top);
        }

        private static void DrawRibbon(OrnamentPen p, BattleCardPalette palette)
        {
            Color rim = Color.Lerp(palette.Ink, palette.Surface, .2f);
            Color gold = Color.Lerp(palette.Accent, Hex("#BE985C"), .5f);
            p.Polygon(new[] { new Vector2(0, .08f), new Vector2(.08f, .02f), new Vector2(.15f, .23f),
                new Vector2(.15f, .76f), new Vector2(.02f, .72f), new Vector2(.045f, .39f) }, rim);
            p.Polygon(new[] { new Vector2(1, .08f), new Vector2(.92f, .02f), new Vector2(.85f, .23f),
                new Vector2(.85f, .76f), new Vector2(.98f, .72f), new Vector2(.955f, .39f) }, rim);
            // A gently arched banner with a bevel, not a flat rectangle.
            const int segments = 18;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments, b = (i + 1) / (float)segments;
                float xa = .04f + a * .92f, xb = .04f + b * .92f;
                float arcA = Mathf.Sin(a * Mathf.PI) * .075f, arcB = Mathf.Sin(b * Mathf.PI) * .075f;
                p.Four(new Vector2(xa, .08f + arcA), new Vector2(xa, .9f + arcA),
                    new Vector2(xb, .9f + arcB), new Vector2(xb, .08f + arcB), rim);
                p.Four(new Vector2(xa, .14f + arcA), new Vector2(xa, .85f + arcA),
                    new Vector2(xb, .85f + arcB), new Vector2(xb, .14f + arcB), gold);
                p.Four(new Vector2(xa, .22f + arcA), new Vector2(xa, .78f + arcA),
                    new Vector2(xb, .78f + arcB), new Vector2(xb, .22f + arcB), palette.Parchment);
                p.Four(new Vector2(xa, .78f + arcA), new Vector2(xa, .815f + arcA),
                    new Vector2(xb, .815f + arcB), new Vector2(xb, .78f + arcB), Hex("#F9EBC3"));
            }
        }

        private static void DrawTypeTag(OrnamentPen p, BattleCardPalette palette)
        {
            p.Polygon(new[] { new Vector2(0, .1f), new Vector2(.09f, .87f), new Vector2(.91f, .87f),
                new Vector2(1, .1f) }, palette.Ink);
            p.Polygon(new[] { new Vector2(.045f, .18f), new Vector2(.12f, .79f), new Vector2(.88f, .79f),
                new Vector2(.955f, .18f) }, palette.Accent);
            p.Quad(.15f, .22f, .7f, .47f, palette.Surface);
        }

        private static void DrawUpgradeArrow(OrnamentPen p)
        {
            Color rim = Hex("#694524"), gold = Hex("#BF8E40");
            p.Quad(0f, .36f, .6f, .28f, rim);
            p.Polygon(new[] { new Vector2(.53f, .1f), new Vector2(1f, .5f), new Vector2(.53f, .9f) }, rim);
            p.Quad(.03f, .43f, .58f, .14f, gold);
            p.Polygon(new[] { new Vector2(.58f, .23f), new Vector2(.92f, .5f), new Vector2(.58f, .77f) }, gold);
            p.Line(.05f, .565f, .58f, .565f, .025f, Hex("#F4D994"));
        }

        private static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out Color result); return result; }

        private readonly struct OrnamentPen
        {
            private readonly VertexHelper mesh;
            private readonly Rect rect;
            private readonly Color tint;
            public OrnamentPen(VertexHelper mesh, Rect rect, Color tint) { this.mesh = mesh; this.rect = rect; this.tint = tint; }
            public void Quad(float x, float y, float w, float h, Color c) => Four(new Vector2(x, y), new Vector2(x, y + h), new Vector2(x + w, y + h), new Vector2(x + w, y), c);
            public void Diamond(float x, float y, float w, float h, Color c) => Four(new Vector2(x - w, y), new Vector2(x, y + h), new Vector2(x + w, y), new Vector2(x, y - h), c);
            public void Line(float x1, float y1, float x2, float y2, float width, Color color)
            {
                Vector2 a = new Vector2(x1, y1), b = new Vector2(x2, y2), delta = b - a;
                Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * .5f);
                Four(a - normal, a + normal, b + normal, b - normal, color);
            }
            public void Four(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
            {
                int first = mesh.currentVertCount;
                Vertex(a, color); Vertex(b, color); Vertex(c, color); Vertex(d, color);
                mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
            }
            public void Polygon(Vector2[] points, Color color)
            {
                int first = mesh.currentVertCount;
                Vector2 centre = Vector2.zero;
                foreach (Vector2 point in points) centre += point;
                Vertex(centre / points.Length, color);
                foreach (Vector2 point in points) Vertex(point, color);
                for (int i = 0; i < points.Length; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % points.Length);
            }
            private void Vertex(Vector2 p, Color color) => mesh.AddVert(
                new Vector3(rect.xMin + p.x * rect.width, rect.yMin + p.y * rect.height, 0), color * tint, Vector2.zero);
        }
    }
}
