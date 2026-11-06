using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A wood and brass ship's wheel with open spokes, sized for a slider handle.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavigationHelmGraphic : Image
    {
        private const int RingSegments = 64;
        private static readonly Color Outline = new Color(.20f, .12f, .065f, 1f);
        private static readonly Color Brass = new Color(1f, .85f, .49f, 1f);
        private static readonly Color BrassLight = new Color(1f, .98f, .76f, 1f);
        private static readonly Color BrassShade = new Color(.60f, .40f, .17f, 1f);
        private Rect drawingRect;
        private Vector4 spriteUv;
        private float diameter;

        protected override void OnEnable()
        {
            if (sprite == null) sprite = WoodButtonSkin.Face;
            base.OnEnable();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            drawingRect = GetPixelAdjustedRect();
            diameter = Mathf.Min(drawingRect.width, drawingRect.height);
            if (diameter <= 0f) return;
            spriteUv = sprite == null ? new Vector4(0f, 0f, 1f, 1f) : DataUtility.GetOuterUV(sprite);

            // Individual spokes leave the parchment visible through the wheel.
            // The rectangular Image hit target remains easy to grab at this size.
            for (int spoke = 0; spoke < 8; spoke++)
            {
                float angle = spoke * 45f;
                AddSpoke(helper, angle, .09f, .446f, .085f, Outline, false);
                AddDisc(helper, Direction(angle) * .446f, .051f, Outline, false);
                AddSpoke(helper, angle, .10f, .446f, .052f, Color.white, true);
                AddDisc(helper, Direction(angle) * .446f, .034f, Color.white, true);
                AddSpoke(helper, angle, .105f, .425f, .014f, Brass, false);
                AddSpoke(helper, angle, .384f, .403f, .067f, Brass, false);
                AddSpoke(helper, angle, .384f, .391f, .067f, BrassLight, false);
            }

            AddRing(helper, .231f, .374f, Outline, false);
            AddRing(helper, .246f, .363f, BrassShade, false);
            AddRing(helper, .252f, .355f, Brass, false);
            AddRing(helper, .274f, .337f, Color.white, true);
            AddRing(helper, .252f, .263f, BrassLight, false);
            AddRing(helper, .345f, .355f, BrassLight, false);
            AddRing(helper, .274f, .282f, BrassShade, false);

            // A raised, concentric brass axle stays readable at roughly 44 px.
            AddDisc(helper, Vector2.zero, .143f, Outline, false);
            AddDisc(helper, Vector2.zero, .128f, BrassLight, false);
            AddDisc(helper, Vector2.zero, .109f, BrassShade, false);
            AddDisc(helper, Vector2.zero, .092f, Brass, false);
            AddDisc(helper, new Vector2(-.012f, .014f), .061f, BrassLight, false);
            AddRing(helper, .034f, .044f, Brass, false);
        }

        private static Vector2 Direction(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private void AddSpoke(VertexHelper helper, float angle, float inner, float outer,
            float width, Color tint, bool wood)
        {
            Vector2 axis = Direction(angle);
            Vector2 side = new Vector2(-axis.y, axis.x) * (width * .5f);
            AddQuad(helper, axis * inner - side, axis * inner + side,
                axis * outer + side, axis * outer - side, tint, wood);
        }

        private void AddRing(VertexHelper helper, float inner, float outer, Color tint, bool wood)
        {
            for (int segment = 0; segment < RingSegments; segment++)
            {
                Vector2 a = Direction(segment * (360f / RingSegments));
                Vector2 b = Direction((segment + 1) * (360f / RingSegments));
                AddQuad(helper, a * inner, b * inner, b * outer, a * outer, tint, wood);
            }
        }

        private void AddDisc(VertexHelper helper, Vector2 center, float radius, Color tint, bool wood)
        {
            const int segments = 24;
            int start = helper.currentVertCount;
            AddVertex(helper, center, tint, wood);
            for (int segment = 0; segment <= segments; segment++)
                AddVertex(helper, center + Direction(segment * (360f / segments)) * radius, tint, wood);
            for (int segment = 0; segment < segments; segment++)
                helper.AddTriangle(start, start + segment + 1, start + segment + 2);
        }

        private void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color tint, bool wood)
        {
            int start = helper.currentVertCount;
            AddVertex(helper, a, tint, wood);
            AddVertex(helper, b, tint, wood);
            AddVertex(helper, c, tint, wood);
            AddVertex(helper, d, tint, wood);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start + 2, start + 3, start);
        }

        private void AddVertex(VertexHelper helper, Vector2 point, Color tint, bool wood)
        {
            // Wood samples the face interior; metal samples its existing bright
            // frame, so no extra texture, generated sprite or material is needed.
            Vector2 uv = wood ? new Vector2(.5f + point.x * .6f, .5f + point.y * .6f)
                : new Vector2(.5f, .955f);
            uv = new Vector2(Mathf.Lerp(spriteUv.x, spriteUv.z, uv.x),
                Mathf.Lerp(spriteUv.y, spriteUv.w, uv.y));
            helper.AddVert(drawingRect.center + point * diameter, (Color32)(tint * color), uv);
        }
    }
}
