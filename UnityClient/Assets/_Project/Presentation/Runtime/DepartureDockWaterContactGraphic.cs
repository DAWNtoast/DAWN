using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Soft immersion follows the painted keel; the berth and shore remain fixed.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class DepartureDockWaterContactGraphic : MaskableGraphic
    {
        // Current transparent PNG cells, alpha >= 96 with a 37-pixel median.
        // Rows: xMin, xMax, seventeen normalized bottom-up y samples. The live
        // contact anchor calibrates height independently of atlas padding.
        private static readonly float[][] Profiles =
        {
            new[] { .16852f,.82236f,.22168f,.20752f,.19409f,.18091f,.16797f,.15625f,.14697f,.13965f,.13306f,.12671f,.12036f,.11816f,.13940f,.20166f,.28271f,.35010f,.40527f },
            new[] { .12121f,.88204f,.34668f,.23950f,.19580f,.18481f,.17480f,.16504f,.15527f,.14600f,.13770f,.12964f,.12158f,.11768f,.13989f,.20630f,.29004f,.36084f,.42285f },
            new[] { .21816f,.84499f,.23926f,.22437f,.20874f,.19263f,.17749f,.16455f,.15381f,.14453f,.13574f,.12671f,.11792f,.11694f,.14331f,.19849f,.26318f,.32764f,.38477f },
            new[] { .21838f,.84412f,.23828f,.22168f,.20532f,.18994f,.17627f,.16431f,.15356f,.14355f,.13452f,.12720f,.12231f,.12549f,.14917f,.19849f,.25293f,.31250f,.40527f },
            new[] { .17762f,.92980f,.24609f,.23144f,.21509f,.19873f,.18384f,.17065f,.15942f,.14941f,.14111f,.13525f,.13013f,.12866f,.15430f,.23071f,.33496f,.42053f,.47607f },
            new[] { .20693f,.86077f,.26465f,.24390f,.22510f,.20850f,.19385f,.18115f,.17041f,.16113f,.15259f,.14478f,.13843f,.13940f,.16553f,.21582f,.25830f,.28223f,.30078f },
            new[] { .14404f,.98047f,.30371f,.26123f,.23706f,.22192f,.20752f,.19409f,.18091f,.16846f,.15723f,.14697f,.13818f,.13184f,.13916f,.18359f,.28809f,.34839f,.22852f },
            new[] { .15256f,.96202f,.25000f,.23120f,.21436f,.19702f,.18018f,.16602f,.15503f,.14673f,.14014f,.13525f,.13135f,.12817f,.14722f,.20996f,.32837f,.38428f,.21680f },
            new[] { .26681f,.98047f,.41406f,.39014f,.36572f,.34229f,.32031f,.30029f,.28320f,.26807f,.25513f,.24487f,.23999f,.24512f,.26318f,.30762f,.32910f,.28638f,.25391f },
            new[] { .21085f,.87118f,.24023f,.22925f,.21729f,.20361f,.18921f,.17603f,.16504f,.15649f,.14941f,.14331f,.13867f,.13672f,.15771f,.22314f,.31372f,.39062f,.43945f },
            new[] { .28042f,.98047f,.34668f,.32568f,.30347f,.27881f,.25317f,.23315f,.21875f,.20288f,.18530f,.17554f,.19287f,.24707f,.28931f,.32202f,.34546f,.30078f,.25879f },
            new[] { .26906f,.84617f,.29785f,.28101f,.26416f,.24805f,.23267f,.21802f,.20435f,.19189f,.18042f,.16919f,.16016f,.17090f,.20850f,.24780f,.27881f,.30908f,.33887f },
        };
        private static readonly int[] Cells = { 0,1,2,3,2,0,1,5,6,3,4,7 };
        private static readonly float[] Centers = { .495443f,.501628f,.531576f,.531250f,.553711f,.533854f,.588216f,.557292f,.642904f,.541016f,.640299f,.557617f };
        [SerializeField] private Texture background;
        [SerializeField] private bool ripples;
        [SerializeField] private Vector2[] edge = new Vector2[17];
        [SerializeField] private Vector2 hullContact;
        [SerializeField] private float hullWidth;
        [SerializeField] private Color waterTint = Color.white;
        public Vector2 HullContact => hullContact;
        public float HullWidth => hullWidth;
        public bool IsRippleLayer => ripples;
        public override Texture mainTexture => !ripples && background != null ? background : Texture2D.whiteTexture;

        public void Configure(Texture backdrop, RawImage hull, int shipIndex, bool rippleLayer, string port)
        {
            if (hull == null || hull.texture == null) return;
            background = backdrop;
            ripples = rippleLayer;
            raycastTarget = false;
            waterTint = DepartureDockShipLighting.TintForPort(port);
            Vector2 contactLocal = ShipWaterlineLayout.HullContactLocal(hull);
            hullContact = rectTransform.InverseTransformPoint(hull.rectTransform.TransformPoint(contactLocal));
            Vector3 widthEnd = rectTransform.InverseTransformPoint(hull.rectTransform.TransformPoint(
                contactLocal + new Vector2(ShipWaterlineLayout.HullWidthLocal(hull), 0f)));
            hullWidth = Mathf.Abs(widthEnd.x - hullContact.x);
            int index = Mathf.Clamp(shipIndex, 0, Profiles.Length - 1);
            float[] profile = Profiles[index];
            int columns = index == 0 ? 1 : index < 4 ? 2 : 4;
            int rows = index == 0 ? 1 : 2;
            int column = Cells[index] % columns;
            int row = rows - 1 - Cells[index] / columns;
            Rect uv = hull.uvRect;
            Rect bounds = hull.rectTransform.rect;
            float anchorY = ((contactLocal.y - bounds.yMin) / bounds.height * uv.height + uv.y) * rows - row;
            float anchorSample = Mathf.InverseLerp(profile[0], profile[1], Centers[index]) * (edge.Length - 1);
            int left = Mathf.Clamp(Mathf.FloorToInt(anchorSample), 0, edge.Length - 2);
            float sampledAnchorY = Mathf.Lerp(profile[left + 2], profile[left + 3], anchorSample - left);
            float calibration = anchorY - sampledAnchorY;
            for (int n = 0; n < edge.Length; n++)
            {
                float x = Mathf.Lerp(profile[0], profile[1], n / (float)(edge.Length - 1));
                float y = profile[n + 2] + calibration;
                // Water does not climb a raised stem or bowsprit silhouette.
                if (x > Centers[index]) y = Mathf.Min(y, anchorY + .025f);
                Vector2 source = new Vector2((column + x) / columns, (row + y) / rows);
                Vector2 local = bounds.min + Vector2.Scale(bounds.size,
                    new Vector2((source.x - uv.x) / uv.width, (source.y - uv.y) / uv.height));
                edge[n] = rectTransform.InverseTransformPoint(hull.rectTransform.TransformPoint(local));
            }
            SetMaterialDirty();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (background == null || edge == null || edge.Length < 2 || hullWidth <= 0f) return;
            if (ripples) DrawRipples(mesh); else DrawImmersion(mesh);
        }

        private void DrawImmersion(VertexHelper mesh)
        {
            float depth = Mathf.Clamp(hullWidth * .28f, 80f, 180f);
            // Same screen UVs as the fixed background. A curved 10-pixel feather
            // replaces the exposed keel, without a rectangle or moving backdrop.
            for (int n = 0; n < edge.Length - 1; n++)
            {
                float a = EdgeOpacity(n), b = EdgeOpacity(n + 1);
                Band(mesh, edge[n], edge[n + 1], 4f, -2f, 0f, .65f, a, b, Color.white);
                Band(mesh, edge[n], edge[n + 1], -2f, -6f, .65f, 1f, a, b, Color.white);
                Band(mesh, edge[n], edge[n + 1], -6f, -depth, 1f, 1f, a, b, Color.white);
                Band(mesh, edge[n], edge[n + 1], -depth, -depth - 12f, 1f, 0f, a, b, Color.white);
            }
        }

        private void DrawRipples(VertexHelper mesh)
        {
            Color shadow = new Color(.015f, .075f, .095f, 1f);
            Color foam = new Color(.60f * waterTint.r, .79f * waterTint.g, .80f * waterTint.b, .12f);
            for (int n = 0; n < edge.Length - 1; n++)
            {
                float a = EdgeOpacity(n), b = EdgeOpacity(n + 1);
                Band(mesh, edge[n], edge[n + 1], 1f, -4f, 0f, .13f, a, b, shadow);
                Band(mesh, edge[n], edge[n + 1], -4f, -11f, .13f, 0f, a, b, shadow);
                // Short broken strokes: no moving wake or broad glow.
                if (n % 4 == 1) continue;
                Vector2 from = Vector2.Lerp(edge[n], edge[n + 1], .12f) + Vector2.down * 3.5f;
                Vector2 to = Vector2.Lerp(edge[n], edge[n + 1], .75f) + Vector2.down * 3.5f;
                Stroke(mesh, from, to, .65f, foam * Mathf.Min(a, b));
            }
        }

        private float EdgeOpacity(int sample)
        {
            float t = sample / (float)(edge.Length - 1);
            return Mathf.SmoothStep(0f, 1f, t / .14f) * Mathf.SmoothStep(0f, 1f, (1f - t) / .20f);
        }

        private void Band(VertexHelper mesh, Vector2 a, Vector2 b, float top, float bottom,
            float topAlpha, float bottomAlpha, float aAlpha, float bAlpha, Color tint)
        {
            int first = mesh.currentVertCount;
            Add(mesh, a + Vector2.up * top, tint, topAlpha * aAlpha);
            Add(mesh, b + Vector2.up * top, tint, topAlpha * bAlpha);
            Add(mesh, b + Vector2.up * bottom, tint, bottomAlpha * bAlpha);
            Add(mesh, a + Vector2.up * bottom, tint, bottomAlpha * aAlpha);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float halfWidth, Color tint)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * halfWidth;
            int first = mesh.currentVertCount;
            Add(mesh, a - normal, tint, 1f); Add(mesh, a + normal, tint, 1f);
            Add(mesh, b + normal, tint, 1f); Add(mesh, b - normal, tint, 1f);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }

        private void Add(VertexHelper mesh, Vector2 point, Color tint, float alpha)
        {
            Rect rect = rectTransform.rect;
            Vector2 uv = ripples ? Vector2.zero : new Vector2(
                (point.x - rect.xMin) / rect.width, (point.y - rect.yMin) / rect.height);
            tint.a *= alpha;
            mesh.AddVert(point, tint * color, uv);
        }
    }
}
