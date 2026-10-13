using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A compass wedge whose mesh and pointer hit area share the same annulus.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class NavigationCompassSectorGraphic : Image
    {
        public const float InnerRadiusRatio = .23f;
        public const float OuterRadiusRatio = .45f;
        private const float HalfSectorDegrees = 22.5f;
        [SerializeField] private float directionDegrees;
        [SerializeField] private bool headingSelected;

        // Keep an Image target so the existing wood-button selection API still
        // owns the selected state. The custom mesh uses plain vertex colours.
        public override Texture mainTexture => Texture2D.whiteTexture;
        public float DirectionDegrees => directionDegrees;
        public bool HeadingSelected => headingSelected;

        public void Configure(float degrees, bool selected)
        {
            float normalized = Mathf.Repeat(degrees, 360f);
            if (directionDegrees == normalized && headingSelected == selected) return;
            directionDegrees = normalized;
            headingSelected = selected;
            SetVerticesDirty();
        }

        public bool ContainsLocalPoint(Vector2 localPoint)
        {
            Vector2 delta = localPoint - rectTransform.rect.center;
            float diameter = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            float inner = diameter * InnerRadiusRatio, outer = diameter * OuterRadiusRatio;
            if (diameter <= 0f || delta.sqrMagnitude < inner * inner || delta.sqrMagnitude > outer * outer)
                return false;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            // Quantize once in a common coordinate system. Subtracting each
            // wedge's angle separately can round the same seam to two owners.
            int pointSector = Mathf.FloorToInt(Mathf.Repeat(angle + HalfSectorDegrees, 360f) / 45f) % 8;
            int ownSector = Mathf.RoundToInt(directionDegrees / 45f) % 8;
            return pointSector == ownSector;
        }

        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint,
                eventCamera, out Vector2 localPoint) && ContainsLocalPoint(localPoint);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            float diameter = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            if (diameter <= 0f) return;
            float inner = diameter * InnerRadiusRatio, outer = diameter * OuterRadiusRatio;
            float start = directionDegrees - HalfSectorDegrees + 1.5f;
            float end = directionDegrees + HalfSectorDegrees - 1.5f;
            Color fill = headingSelected ? new Color(1f, .61f, .16f, .48f) : new Color(.03f, .025f, .02f, .12f);
            Color edge = headingSelected ? new Color(1f, .82f, .36f, 1f) : new Color(.68f, .47f, .23f, .62f);
            float rim = Mathf.Max(1.2f, diameter * .005f);
            AddBand(helper, inner, outer, start, end, fill);
            AddBand(helper, inner, inner + rim, start, end, edge);
            AddBand(helper, outer - rim, outer, start, end, edge);
            AddSpoke(helper, inner, outer, start, rim, edge);
            AddSpoke(helper, inner, outer, end, rim, edge);
        }

        private void AddBand(VertexHelper helper, float inner, float outer, float start, float end, Color tint)
        {
            const int segments = 16;
            for (int index = 0; index < segments; index++)
            {
                float a = Mathf.Lerp(start, end, index / (float)segments) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(start, end, (index + 1f) / segments) * Mathf.Deg2Rad;
                Vector2 first = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 next = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                AddQuad(helper, first * inner, next * inner, next * outer, first * outer, tint);
            }
        }

        private void AddSpoke(VertexHelper helper, float inner, float outer, float angle, float width, Color tint)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector2 axis = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 side = new Vector2(-axis.y, axis.x) * width * .5f;
            AddQuad(helper, axis * inner - side, axis * inner + side,
                axis * outer + side, axis * outer - side, tint);
        }

        private void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = helper.currentVertCount;
            Color32 vertexColor = tint * color;
            Vector2 center = rectTransform.rect.center;
            helper.AddVert(a + center, vertexColor, Vector2.zero);
            helper.AddVert(b + center, vertexColor, Vector2.zero);
            helper.AddVert(c + center, vertexColor, Vector2.zero);
            helper.AddVert(d + center, vertexColor, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start + 2, start + 3, start);
        }
    }
}
