using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>A finite, input-transparent receipt. It never changes the live readouts or transaction.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class PortTransactionMotion : MonoBehaviour
    {
        public const float Duration = 1.15f;
        public const float FlightDuration = .44f;
        private CanvasGroup group;
        private RawImage item;
        private Text receipt, secondary;
        private RectTransform receiptPanel, secondaryPanel;
        private PortTransactionGraphic decoration;
        private Vector2 source, destination, receiptPosition;
        private RectTransform coordinateSpace, destinationAnchor, receiptSpace, goldAnchor;
        private Vector2 receiptNormalized;
        private bool followsDestination, positionedReceipt;
        private Behaviour owner;
        private bool hadOwner;
        public float Elapsed { get; private set; }
        public bool IsPlaying { get; private set; }
        public string ReceiptText => receipt == null ? string.Empty : receipt.text;
        public string SecondaryText => secondary == null ? string.Empty : secondary.text;
        public Vector2 Source => source;
        public Vector2 Destination => destinationAnchor == null ? destination : Anchor(coordinateSpace, destinationAnchor);
        public RectTransform ReceiptRect => receiptPanel;
        public RectTransform DestinationAnchor => destinationAnchor;

        public static PortTransactionMotion Play(RectTransform parent, Vector2 from, Vector2 to,
            ItemIconArt art, string text, Font font, Behaviour owner, bool metallic = false,
            RectTransform accent = null, RectTransform secondaryAnchor = null, string secondaryText = "",
            RectTransform destinationAnchor = null, RectTransform receiptSpace = null, Vector2? receiptNormalized = null)
        {
            if (parent == null || !parent.gameObject.activeInHierarchy || !art.IsAvailable) return null;
            // Repeated legal purchases stay responsive and use at most one short receipt per view.
            Transform previous = parent.Find("Port Transaction Feedback");
            if (previous != null)
            {
                previous.name = "Retired Port Transaction Feedback";
                previous.gameObject.SetActive(false);
                Destroy(previous.gameObject);
            }
            RectTransform root = Rect("Port Transaction Feedback", parent, parent.rect.size, Vector2.zero);
            var motion = root.gameObject.AddComponent<PortTransactionMotion>();
            motion.owner = owner; motion.hadOwner = owner != null;
            motion.source = from; motion.destination = to;
            motion.coordinateSpace = parent; motion.destinationAnchor = destinationAnchor;
            motion.followsDestination = destinationAnchor != null;
            motion.receiptSpace = receiptSpace; motion.positionedReceipt = receiptSpace != null && receiptNormalized.HasValue;
            motion.receiptNormalized = receiptNormalized ?? Vector2.one * .5f;
            motion.goldAnchor = secondaryAnchor;
            motion.group = root.gameObject.AddComponent<CanvasGroup>();
            motion.group.blocksRaycasts = false; motion.group.interactable = false;
            motion.decoration = root.gameObject.AddComponent<PortTransactionGraphic>();
            motion.decoration.raycastTarget = false;
            motion.decoration.Source = from; motion.decoration.Destination = to;
            motion.decoration.Metallic = metallic;
            motion.decoration.CoordinateSpace = parent; motion.decoration.DestinationAnchor = destinationAnchor;
            motion.decoration.AccentAnchor = accent;
            if (accent != null)
            {
                Vector2 center = Anchor(parent, accent);
                Vector3 scale = parent.InverseTransformVector(accent.TransformVector(accent.rect.size));
                motion.decoration.Accent = new Rect(center - new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) * .5f,
                    new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            }
            RectTransform iconRect = Rect("Transferred Item", root, new Vector2(68f, 68f), from);
            motion.item = iconRect.gameObject.AddComponent<RawImage>();
            motion.item.texture = art.Texture; motion.item.uvRect = art.Uv; motion.item.raycastTarget = false;
            float aspect = art.Texture.width * art.Uv.width / Mathf.Max(1f, art.Texture.height * art.Uv.height);
            iconRect.sizeDelta = aspect >= 1f ? new Vector2(68f, 68f / aspect) : new Vector2(68f * aspect, 68f);
            Vector2 receiptSize = metallic ? new Vector2(620f, 54f) : new Vector2(430f, 64f);
            motion.receiptPosition = ClampLabel(parent.rect, to + new Vector2(0f, 75f), receiptSize);
            motion.receiptPanel = Label("Transaction Receipt", root, receiptSize, motion.receiptPosition, font, text,
                metallic ? 20 : 24, out motion.receipt);
            if (secondaryAnchor != null && !string.IsNullOrEmpty(secondaryText))
            {
                Vector2 goldPosition = Anchor(parent, secondaryAnchor) + new Vector2(0f, -57f);
                motion.secondaryPanel = Label("Gold Receipt", root, new Vector2(260f, 44f),
                    ClampLabel(parent.rect, goldPosition, new Vector2(260f, 44f)), font, secondaryText, 24, out motion.secondary);
            }
            motion.IsPlaying = true;
            motion.SetElapsed(0f);
            return motion;
        }

        public static Vector2 Anchor(RectTransform parent, RectTransform target) =>
            parent == null || target == null ? Vector2.zero : (Vector2)parent.InverseTransformPoint(target.TransformPoint(target.rect.center));

        private void Update()
        {
            if (!IsPlaying) return;
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { Finish(); return; }
            SetElapsed(Elapsed + Mathf.Min(.05f, Time.unscaledDeltaTime));
        }

        private void OnEnable() => Canvas.willRenderCanvases += RefreshBeforeRender;
        private void LateUpdate() => RefreshBeforeRender();
        private void RefreshBeforeRender()
        {
            if (IsPlaying && coordinateSpace != null) SetElapsed(Elapsed);
        }

        public void SetElapsed(float elapsed)
        {
            if (!IsPlaying) return;
            if (followsDestination && (destinationAnchor == null || !destinationAnchor.gameObject.activeInHierarchy)) { Finish(); return; }
            if (destinationAnchor != null) destination = Anchor(coordinateSpace, destinationAnchor);
            if (positionedReceipt && receiptSpace != null)
            {
                Rect region = receiptSpace.rect;
                Vector2 point = region.min + Vector2.Scale(region.size, receiptNormalized);
                receiptPosition = ClampLabel(coordinateSpace.rect,
                    coordinateSpace.InverseTransformPoint(receiptSpace.TransformPoint(point)), receiptPanel.sizeDelta);
            }
            if (secondaryPanel != null && goldAnchor != null)
                secondaryPanel.anchoredPosition = ClampLabel(coordinateSpace.rect,
                    Anchor(coordinateSpace, goldAnchor) + new Vector2(0f, -57f), secondaryPanel.sizeDelta);
            Elapsed = Mathf.Clamp(elapsed, 0f, Duration);
            float travel = Mathf.Clamp01(Elapsed / FlightDuration);
            float eased = 1f - Mathf.Pow(1f - travel, 3f);
            Vector2 position = Vector2.Lerp(source, destination, eased);
            position.y += Mathf.Sin(travel * Mathf.PI) * 38f;
            item.rectTransform.anchoredPosition = position;
            item.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(travel * Mathf.PI) * .14f);
            item.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((Elapsed - FlightDuration) / .14f));
            float appear = Mathf.Clamp01((Elapsed - .24f) / .18f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Elapsed - .86f) / (Duration - .86f)));
            group.alpha = fade;
            receiptPanel.GetComponent<CanvasGroup>().alpha = appear;
            receiptPanel.anchoredPosition = receiptPosition + new Vector2(0f, Mathf.SmoothStep(0f, 1f, travel) * 5f);
            if (secondaryPanel != null) secondaryPanel.GetComponent<CanvasGroup>().alpha = appear;
            decoration.Elapsed = Elapsed; decoration.SetVerticesDirty();
            if (Elapsed >= Duration) Finish();
        }

        private void Finish()
        {
            IsPlaying = false;
            if (item != null) item.texture = null;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= RefreshBeforeRender;
            IsPlaying = false;
            if (group != null) group.alpha = 0f;
            if (item != null) item.texture = null;
        }

        private static Vector2 ClampLabel(Rect bounds, Vector2 position, Vector2 size) => new Vector2(
            Mathf.Clamp(position.x, bounds.xMin + size.x * .5f + 16f, bounds.xMax - size.x * .5f - 16f),
            Mathf.Clamp(position.y, bounds.yMin + size.y * .5f + 16f,
                bounds.yMax - PhaseSurfaceLayout.HeaderHeight - size.y * .5f - 16f));

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false); root.sizeDelta = size; root.anchoredPosition = position;
            return root;
        }

        private static RectTransform Label(string name, Transform parent, Vector2 size, Vector2 position,
            Font font, string text, int fontSize, out Text label)
        {
            RectTransform panel = Rect(name, parent, size, position);
            Image back = panel.gameObject.AddComponent<Image>();
            back.color = new Color(.035f, .075f, .08f, .92f); back.raycastTarget = false;
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            label = Rect("Text", panel, size - new Vector2(20f, 10f), Vector2.zero).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
            label.text = text; label.color = new Color(1f, .89f, .59f, 1f); label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 17; label.resizeTextMaxSize = fontSize;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return panel;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PortTransactionGraphic : MaskableGraphic
    {
        public Vector2 Source, Destination;
        public Rect Accent;
        public float Elapsed;
        public bool Metallic;
        public RectTransform CoordinateSpace, DestinationAnchor, AccentAnchor;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (CoordinateSpace != null && DestinationAnchor != null)
                Destination = PortTransactionMotion.Anchor(CoordinateSpace, DestinationAnchor);
            if (CoordinateSpace != null && AccentAnchor != null)
            {
                Vector2 center = PortTransactionMotion.Anchor(CoordinateSpace, AccentAnchor);
                Vector3 extent = CoordinateSpace.InverseTransformVector(AccentAnchor.TransformVector(AccentAnchor.rect.size));
                Vector2 size = new Vector2(Mathf.Abs(extent.x), Mathf.Abs(extent.y));
                Accent = new Rect(center - size * .5f, size);
            }
            float contact = Mathf.Clamp01((Elapsed - PortTransactionMotion.FlightDuration) / .48f);
            if (Elapsed < PortTransactionMotion.FlightDuration || contact >= 1f) return;
            Color tint = Metallic ? new Color(1f, .75f, .3f, .65f * (1f - contact)) : new Color(.48f, .94f, .8f, .62f * (1f - contact));
            float radius = Mathf.Lerp(20f, 52f, contact);
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f, b = (i + 1) * Mathf.PI * 2f / 28f;
                Stroke(vh, Destination + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    Destination + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 1.6f * (1f - contact), tint);
            }
            if (Metallic)
                for (int i = 0; i < 7; i++)
                {
                    float angle = i * 2.39996f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Stroke(vh, Destination + direction * (18f + contact * 30f),
                        Destination + direction * (25f + contact * 41f), 2f * (1f - contact), tint);
                }
            if (Accent.width > 0f && Accent.height > 0f)
            {
                Color wash = tint; wash.a *= .15f;
                Quad(vh, Accent, wash);
                Stroke(vh, new Vector2(Accent.xMin, Accent.yMin), new Vector2(Accent.xMax, Accent.yMin), 2f, tint);
            }
        }

        private static void Quad(VertexHelper vh, Rect r, Color color)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }

        private static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < .001f || width <= 0f) return;
            Vector2 normal = new Vector2(-d.y, d.x).normalized * width * .5f;
            int n = vh.currentVertCount;
            vh.AddVert(a - normal, color, Vector2.zero); vh.AddVert(a + normal, color, Vector2.zero);
            vh.AddVert(b + normal, color, Vector2.zero); vh.AddVert(b - normal, color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
