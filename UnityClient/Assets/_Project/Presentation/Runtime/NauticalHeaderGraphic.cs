using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared harbour timber backing with narrow aged-brass edges.</summary>
    [DisallowMultipleComponent]
    public sealed class NauticalHeaderGraphic : MaskableGraphic
    {
        public const string WoodResourcePath = "UI/PortHeaderArt/harbour_walnut_header_v1";
        [SerializeField] private bool inset;
        [SerializeField] private string portName = "断锚港";
        private Texture2D wood;

        public string PortName => portName;

        public static string ResourcePathForPort(string port)
        {
            switch (port)
            {
                case "断锚港": return WoodResourcePath;
                case "白贝港": return "UI/PortHeaderArt/white_shell_wood_header_v1";
                case "铜钟港": return "UI/PortHeaderArt/bronze_bell_wood_header_v1";
                case "盐冠港": return "UI/PortHeaderArt/salt_crown_wood_header_v1";
                case "暮潮港": return "UI/PortHeaderArt/twilight_tide_wood_header_v1";
                default: return "UI/PortHeaderArt/gray_gull_wood_header_v1";
            }
        }

        public override Texture mainTexture
        {
            get
            {
                if (!inset && wood == null) wood = Resources.Load<Texture2D>(ResourcePathForPort(portName));
                return wood != null && !inset ? wood : base.mainTexture;
            }
        }

        public void Configure(Color value, bool panel = false, string portName = null)
        {
            // Layout-only refreshes retain the selected port; controller bindings
            // explicitly update it when this header is reused for another voyage.
            if (!string.IsNullOrEmpty(portName) && this.portName != portName)
            {
                this.portName = portName;
                wood = null;
            }
            inset = panel;
            raycastTarget = false;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // Parked views must not keep the previous harbour's texture alive.
            wood = null;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            // Retain the existing prefab nodes/bindings without drawing inset frames.
            if (inset) return;
            Rect bounds = rectTransform.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            Texture texture = mainTexture;
            if (wood == null)
            {
                Quad(mesh, bounds, new Rect(0, 0, 1, 1),
                    new Color(.20f, .10f, .045f, 1f), new Color(.34f, .20f, .10f, 1f));
                return;
            }

            const float sourceRail = .068f;
            float railHeight = Mathf.Min(6f, bounds.height * .08f);
            Rect body = new Rect(bounds.xMin, bounds.yMin + railHeight, bounds.width, bounds.height - railHeight * 2f);
            // Sample a natural-scale band through the timber, rather than flattening
            // the tall source image (and its knots) into a thin, wide header.
            float uvHeight = Mathf.Min(1f - sourceRail * 2f,
                body.height / body.width * texture.width / texture.height);
            // Neutral modulation preserves each port's authored timber colour.
            float shade = portName == "白贝港" ? .66f : portName == "灰鸥港" ? .64f : .78f;
            Quad(mesh, body, new Rect(0f, .5f - uvHeight * .5f, 1f, uvHeight),
                new Color(shade * .80f, shade * .80f, shade * .80f, 1f), new Color(shade, shade, shade, 1f));

            // Fixed-size end caps keep brass fasteners round at every viewport width.
            Rail(mesh, new Rect(bounds.xMin, bounds.yMin, bounds.width, railHeight),
                new Rect(0f, 0f, 1f, sourceRail), texture);
            Rail(mesh, new Rect(bounds.xMin, bounds.yMax - railHeight, bounds.width, railHeight),
                new Rect(0f, 1f - sourceRail, 1f, sourceRail), texture);
        }

        private static void Rail(VertexHelper mesh, Rect r, Rect uv, Texture texture)
        {
            const float capUv = .032f;
            float cap = Mathf.Min(r.width * .5f, r.height * (capUv * texture.width) / (uv.height * texture.height));
            Color brass = new Color(.75f, .75f, .75f, 1f);
            Quad(mesh, new Rect(r.xMin, r.yMin, cap, r.height),
                new Rect(0f, uv.yMin, capUv, uv.height), brass, brass);
            Quad(mesh, new Rect(r.xMin + cap, r.yMin, r.width - cap * 2f, r.height),
                new Rect(capUv, uv.yMin, 1f - capUv * 2f, uv.height), brass, brass);
            Quad(mesh, new Rect(r.xMax - cap, r.yMin, cap, r.height),
                new Rect(1f - capUv, uv.yMin, capUv, uv.height), brass, brass);
        }

        private static void Quad(VertexHelper mesh, Rect r, Rect uv, Color bottom, Color top)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(new Vector3(r.xMin, r.yMin), bottom, new Vector2(uv.xMin, uv.yMin));
            mesh.AddVert(new Vector3(r.xMin, r.yMax), top, new Vector2(uv.xMin, uv.yMax));
            mesh.AddVert(new Vector3(r.xMax, r.yMax), top, new Vector2(uv.xMax, uv.yMax));
            mesh.AddVert(new Vector3(r.xMax, r.yMin), bottom, new Vector2(uv.xMax, uv.yMin));
            mesh.AddTriangle(i, i + 1, i + 2);
            mesh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
