using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Reviewed source-image regions, projected through the exact cached backdrop crop.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PortAmbientGraphic : MaskableGraphic
    {
        private RawImage backdrop;
        private Behaviour owner;
        private bool hadOwner;
        private Rect[] water = System.Array.Empty<Rect>();
        private Vector2[] lamps = System.Array.Empty<Vector2>();
        private Vector2 chimney;
        private float nextFrame;
        public float MotionTime { get; private set; }
        public string BoundArtwork { get; private set; } = string.Empty;
        public int WaterRegionCount => water.Length;
        public int LampCount => lamps.Length;
        public bool HasSmoke => chimney != Vector2.zero;

        public void Bind(RawImage source, bool hub, Behaviour lifetimeOwner)
        {
            backdrop = source; owner = lifetimeOwner; hadOwner = owner != null;
            raycastTarget = false;
            string artwork = source != null && source.texture != null ? source.texture.name : string.Empty;
            bool known = ConfigureRegions(artwork);
            BoundArtwork = known ? artwork : string.Empty;
            gameObject.SetActive(hub && known);
            SetVerticesDirty();
        }

        private bool ConfigureRegions(string artwork)
        {
            // These top-origin UVs were checked against all six production v3 paintings.
            // Open water excludes hulls, quays, buildings and the permanent facility signs.
            lamps = System.Array.Empty<Vector2>(); chimney = Vector2.zero;
            switch (artwork)
            {
                case "port_hub_broken_anchor_v3":
                    water = new[] { new Rect(.497f, .380f, .030f, .050f), new Rect(.542f, .255f, .030f, .030f) };
                    break;
                case "port_hub_white_shell_v3":
                    water = new[] { new Rect(.325f, .487f, .070f, .045f), new Rect(.805f, .885f, .095f, .060f) };
                    break;
                case "port_hub_bronze_bell_v3":
                    water = new[] { new Rect(.403f, .596f, .045f, .040f), new Rect(.025f, .544f, .065f, .029f) };
                    // The small factory stack above the wheel facade, not the decorative roof vents.
                    chimney = new Vector2(.539f, .108f);
                    break;
                case "port_hub_salt_crown_v3":
                    water = new[] { new Rect(.445f, .485f, .067f, .070f), new Rect(.425f, .560f, .045f, .035f) };
                    break;
                case "port_hub_twilight_tide_v3":
                    water = new[] { new Rect(.582f, .505f, .078f, .052f), new Rect(.323f, .588f, .100f, .032f) };
                    // Existing blue braziers on the departure gate; the painted light remains intact.
                    lamps = new[] { new Vector2(.347f, .663f), new Vector2(.446f, .674f) };
                    break;
                case "port_hub_gray_gull_v3":
                    water = new[] { new Rect(.470f, .425f, .035f, .025f), new Rect(.512f, .393f, .037f, .035f) };
                    break;
                default:
                    water = System.Array.Empty<Rect>();
                    return false;
            }
            return true;
        }

        private void Update()
        {
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { gameObject.SetActive(false); return; }
            if (!Application.isPlaying || backdrop == null || backdrop.texture == null) return;
            MotionTime += Mathf.Min(.05f, Time.unscaledDeltaTime);
            if (MotionTime < nextFrame) return;
            nextFrame = MotionTime + .05f;
            SetVerticesDirty();
        }

        public void SetMotionTime(float time)
        {
            MotionTime = Mathf.Max(0f, time); nextFrame = MotionTime; SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            MotionTime = nextFrame = 0f;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (backdrop == null || backdrop.texture == null) return;
            for (int region = 0; region < water.Length; region++)
            {
                Rect area = water[region];
                for (int i = 0; i < 12; i++)
                {
                    float phase = i * 2.39996f + region * 1.73f;
                    float x = area.x + area.width * (.12f + Mathf.Repeat(i * .618034f, 1f) * .76f);
                    float y = area.y + area.height * (.1f + Mathf.Repeat(i * .414214f, 1f) * .8f);
                    x += Mathf.Sin(MotionTime * .42f + phase) * area.width * .025f;
                    y += Mathf.Sin(MotionTime * .7f + phase) * .0007f;
                    float length = Mathf.Min(.013f, area.width * .19f) * (.55f + .45f * Mathf.Sin(MotionTime * .8f + phase));
                    float opacity = .045f + .105f * Mathf.Pow(.5f + .5f * Mathf.Sin(MotionTime * 1.2f + phase), 2f);
                    Color light = new Color(.76f, .94f, 1f, opacity);
                    Stroke(vh, Project(new Vector2(x - length * .5f, y)), Project(new Vector2(x + length * .5f, y)), 1.1f, light);
                }
            }
            foreach (Vector2 lamp in lamps)
            {
                float flicker = .8f + .12f * Mathf.Sin(MotionTime * 2.1f) + .08f * Mathf.Sin(MotionTime * 3.7f + lamp.x * 17f);
                SoftEllipse(vh, Project(lamp), new Vector2(10f, 16f) * flicker, new Color(.22f, .59f, 1f, .16f));
                SoftEllipse(vh, Project(lamp), new Vector2(3.5f, 7f) * flicker, new Color(.7f, .9f, 1f, .23f));
            }
            if (chimney != Vector2.zero)
                for (int i = 0; i < 6; i++)
                {
                    float age = Mathf.Repeat(MotionTime * .14f + i / 6f, 1f);
                    Vector2 uv = chimney + new Vector2(age * .006f + Mathf.Sin(age * 4f + i) * .0017f, -age * .046f);
                    float alpha = Mathf.Sin(age * Mathf.PI) * .052f;
                    SoftEllipse(vh, Project(uv), Vector2.Lerp(new Vector2(3f, 4f), new Vector2(13f, 9f), age),
                        new Color(.36f, .38f, .40f, alpha));
                }
        }

        private Vector2 Project(Vector2 sourceUv)
        {
            Rect crop = backdrop.uvRect;
            Rect r = rectTransform.rect;
            return new Vector2(r.xMin + (sourceUv.x - crop.x) / crop.width * r.width,
                r.yMin + (1f - sourceUv.y - crop.y) / crop.height * r.height);
        }

        private static void SoftEllipse(VertexHelper vh, Vector2 center, Vector2 radius, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            Color edge = color; edge.a = 0f;
            for (int i = 0; i <= 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                vh.AddVert(center + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), radius), edge, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            if ((b - a).sqrMagnitude < .001f) return;
            Vector2 normal = new Vector2(a.y - b.y, b.x - a.x).normalized * width * .5f;
            int n = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero); vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero); vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
