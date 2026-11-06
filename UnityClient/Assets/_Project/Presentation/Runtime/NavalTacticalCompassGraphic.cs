using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A brass and ivory compass rose that preserves the tactical world's north.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavalTacticalCompassGraphic : MaskableGraphic
    {
        private const int CircleSegments = 96;
        private static readonly Color BrassDark = new Color(.28f, .18f, .075f, 1f);
        private static readonly Color Brass = new Color(.76f, .52f, .22f, 1f);
        private static readonly Color BrassLight = new Color(1f, .88f, .56f, 1f);
        private static readonly Color Ivory = new Color(.97f, .92f, .77f, 1f);
        private static readonly Color IvoryShade = new Color(.77f, .68f, .49f, 1f);
        private static readonly Color TealDark = new Color(.075f, .24f, .25f, 1f);
        private static readonly Color TealLight = new Color(.29f, .56f, .55f, 1f);
        [SerializeField] private float worldRotationDegrees;
        private Rect drawingRect;
        private float diameter;

        public float WorldRotationDegrees => worldRotationDegrees;
        public Vector2 NorthDirection => Direction(90f + worldRotationDegrees);

        public void SetWorldRotation(float degrees)
        {
            float rotation = Mathf.Repeat(degrees, 360f);
            if (Mathf.Approximately(worldRotationDegrees, rotation)) return;
            worldRotationDegrees = rotation;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            drawingRect = GetPixelAdjustedRect();
            diameter = Mathf.Min(drawingRect.width, drawingRect.height);
            if (diameter <= 0f) return;

            // Concentric bevels and a soft circular shadow leave the surrounding
            // sea visible; there is no rectangular backing or pointer target.
            AddDisc(helper, new Vector2(0f, -.009f), .491f,
                new Color(.035f, .10f, .10f, .30f), new Color(.035f, .10f, .10f, 0f));
            AddDisc(helper, Vector2.zero, .478f, BrassDark, BrassDark);
            AddRing(helper, .454f, .476f, BrassLight, Brass, true);
            AddRing(helper, .447f, .454f, BrassDark, BrassDark);
            AddRing(helper, .428f, .447f, Brass, BrassLight, true);
            AddDisc(helper, Vector2.zero, .427f, Ivory, IvoryShade);
            AddRing(helper, .417f, .425f, BrassDark, Brass);
            AddDisc(helper, Vector2.zero, .414f, new Color(1f, .98f, .87f, 1f), Ivory);
            AddRing(helper, .397f, .401f,
                new Color(.23f, .36f, .31f, .65f), new Color(.23f, .36f, .31f, .65f));

            for (int index = 0; index < 8; index++)
            {
                Vector2 rivet = Direction(22.5f + index * 45f) * .464f;
                AddDisc(helper, rivet, .009f, BrassDark, BrassDark);
                AddDisc(helper, rivet + new Vector2(-.002f, .002f), .0055f, BrassLight, Brass);
            }

            // Fine bearing ticks belong to the world, as does the rose. The
            // north tick yields to the upright N drawn by the board controller.
            for (int index = 0; index < 48; index++)
            {
                if (index == 12) continue;
                bool major = index % 6 == 0;
                float angle = index * 7.5f + worldRotationDegrees;
                AddSpoke(helper, angle, major ? .361f : .380f, .395f,
                    major ? .008f : .004f, major ? TealDark : new Color(.36f, .36f, .25f, .72f));
            }

            // Small diagonal winds sit beneath the four principal points.
            for (int index = 0; index < 4; index++)
                AddRosePoint(helper, 45f + index * 90f + worldRotationDegrees,
                    .208f, .034f, BrassLight, Brass);
            for (int index = 0; index < 4; index++)
            {
                bool north = index == 1;
                AddRosePoint(helper, index * 90f + worldRotationDegrees,
                    north ? .282f : .266f, north ? .054f : .047f,
                    north ? new Color(.88f, .31f, .20f, 1f) : TealLight,
                    north ? new Color(.51f, .12f, .09f, 1f) : TealDark);
            }

            AddDisc(helper, Vector2.zero, .071f, BrassDark, BrassDark);
            AddDisc(helper, new Vector2(-.002f, .002f), .060f, BrassLight, Brass);
            AddDisc(helper, Vector2.zero, .041f, BrassDark, BrassDark);
            AddDisc(helper, Vector2.zero, .032f, TealLight, TealDark);
            AddDisc(helper, new Vector2(-.009f, .010f), .009f,
                new Color(.86f, .98f, .89f, .94f), new Color(.58f, .80f, .73f, .2f));
        }

        private void AddRosePoint(VertexHelper helper, float angle, float length, float width, Color light, Color shade)
        {
            Vector2 axis = Direction(angle);
            Vector2 side = new Vector2(-axis.y, axis.x);
            Vector2 heel = -axis * .020f;
            Vector2 shoulder = axis * .065f;
            AddQuad(helper, heel, shoulder + side * (width + .008f),
                axis * (length + .011f), shoulder - side * (width + .008f), BrassDark);
            AddTriangle(helper, heel, shoulder + side * width, axis * length, light);
            AddTriangle(helper, heel, axis * length, shoulder - side * width, shade);
        }

        private void AddSpoke(VertexHelper helper, float angle, float inner, float outer, float width, Color tint)
        {
            Vector2 axis = Direction(angle);
            Vector2 side = new Vector2(-axis.y, axis.x) * (width * .5f);
            AddQuad(helper, axis * inner - side, axis * inner + side,
                axis * outer + side, axis * outer - side, tint);
        }

        private void AddRing(VertexHelper helper, float inner, float outer, Color innerColor, Color outerColor, bool metallic = false)
        {
            for (int index = 0; index < CircleSegments; index++)
            {
                Vector2 a = Direction(index * (360f / CircleSegments));
                Vector2 b = Direction((index + 1) * (360f / CircleSegments));
                int start = helper.currentVertCount;
                AddVertex(helper, a * inner, Lit(innerColor, a, metallic));
                AddVertex(helper, b * inner, Lit(innerColor, b, metallic));
                AddVertex(helper, b * outer, Lit(outerColor, b, metallic));
                AddVertex(helper, a * outer, Lit(outerColor, a, metallic));
                helper.AddTriangle(start, start + 1, start + 2);
                helper.AddTriangle(start + 2, start + 3, start);
            }
        }

        private void AddDisc(VertexHelper helper, Vector2 center, float radius, Color innerColor, Color edgeColor)
        {
            int segments = radius > .1f ? CircleSegments : 24;
            int start = helper.currentVertCount;
            AddVertex(helper, center, innerColor);
            for (int index = 0; index <= segments; index++)
                AddVertex(helper, center + Direction(index * (360f / segments)) * radius, edgeColor);
            for (int index = 0; index < segments; index++)
                helper.AddTriangle(start, start + index + 1, start + index + 2);
        }

        private void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, a, tint);
            AddVertex(helper, b, tint);
            AddVertex(helper, c, tint);
            AddVertex(helper, d, tint);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start + 2, start + 3, start);
        }

        private void AddTriangle(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, a, tint);
            AddVertex(helper, b, tint);
            AddVertex(helper, c, tint);
            helper.AddTriangle(start, start + 1, start + 2);
        }

        private void AddVertex(VertexHelper helper, Vector2 point, Color tint)
        {
            helper.AddVert(drawingRect.center + point * diameter, (Color32)(tint * color), Vector2.zero);
        }

        private static Color Lit(Color tint, Vector2 radial, bool metallic)
        {
            if (!metallic) return tint;
            float light = Mathf.Clamp01((radial.y - radial.x) * .35f + .5f);
            return Color.Lerp(tint * new Color(.72f, .69f, .61f, 1f),
                Color.Lerp(tint, new Color(1f, .97f, .80f, 1f), .38f), light);
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
