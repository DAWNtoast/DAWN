using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>One-shot decoration for a committed upgrade or recruitment; it never owns gameplay state.</summary>
    public sealed class RewardSuccessMotion : MonoBehaviour
    {
        public const float Duration = 1.35f;
        private Behaviour owner;
        private bool hadOwner;
        private CanvasGroup group;
        private RewardSweepGraphic sweep;
        private RectTransform caption;
        private Text highlightedText;
        private string originalText, decoratedText;
        public float Elapsed { get; private set; }
        public bool IsComplete { get; private set; }

        public static RewardSuccessMotion Play(RectTransform parent, Rect bounds, Behaviour owner, Font font,
            string message, Color tint, bool contract = false)
        {
            var root = new GameObject(contract ? "Recruitment Success VFX" : "Card Upgrade Success VFX", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false); rect.sizeDelta = bounds.size; rect.anchoredPosition = bounds.center;
            var motion = root.AddComponent<RewardSuccessMotion>();
            motion.owner = owner; motion.hadOwner = owner != null;
            motion.group = root.AddComponent<CanvasGroup>();
            motion.group.blocksRaycasts = false; motion.group.interactable = false;
            motion.sweep = root.AddComponent<RewardSweepGraphic>();
            motion.sweep.Configure(tint, contract);
            var captionObject = new GameObject("Success Caption", typeof(RectTransform), typeof(Image));
            motion.caption = (RectTransform)captionObject.transform;
            motion.caption.SetParent(rect, false);
            motion.caption.sizeDelta = new Vector2(Mathf.Min(bounds.width - 24f, 300f), contract ? 48f : 38f);
            motion.caption.anchoredPosition = new Vector2(0f, bounds.height * (contract ? -.27f : .12f));
            var backing = captionObject.GetComponent<Image>();
            backing.color = contract ? new Color(.30f, .10f, .07f, .94f) : new Color(.07f, .12f, .15f, .94f);
            backing.raycastTarget = false;
            var labelRect = (RectTransform)new GameObject("Success Text", typeof(RectTransform), typeof(Text)).transform;
            labelRect.SetParent(motion.caption, false); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 0f); labelRect.offsetMax = new Vector2(-8f, 0f);
            var label = labelRect.GetComponent<Text>(); label.font = font; label.fontSize = contract ? 24 : 21;
            label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, .88f, .57f); label.text = message; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 15; label.resizeTextMaxSize = label.fontSize;
            motion.SetElapsed(0f);
            return motion;
        }

        public void HighlightChangedNumbers(Text text, string before)
        {
            if (text == null || !text.supportRichText) return;
            RestoreText();
            var oldNumbers = new List<string>();
            foreach (Match match in Regex.Matches(Regex.Replace(before ?? string.Empty, "<[^>]+>", string.Empty), @"\d+(?:\.\d+)?"))
                oldNumbers.Add(match.Value);
            int number = 0;
            originalText = text.text;
            decoratedText = Regex.Replace(originalText, @"<[^>]+>|\d+(?:\.\d+)?", match =>
            {
                if (match.Value.StartsWith("<")) return match.Value;
                bool changed = number >= oldNumbers.Count || oldNumbers[number] != match.Value;
                number++;
                return changed ? "<color=#B76B12>" + match.Value + "</color>" : match.Value;
            });
            highlightedText = text; text.text = decoratedText;
        }

        private void Update()
        {
            if (IsComplete) return;
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { Cancel(); return; }
            SetElapsed(Elapsed + Time.unscaledDeltaTime);
        }

        public void SetElapsed(float seconds)
        {
            if (IsComplete || group == null) return;
            Elapsed = Mathf.Clamp(float.IsNaN(seconds) ? Duration : seconds, 0f, Duration);
            if (Elapsed >= Duration) { Cancel(); return; }
            float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Elapsed / .16f));
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Elapsed - .96f) / .39f));
            group.alpha = enter * fade;
            sweep.SetProgress(Elapsed / Duration);
            float pop = Mathf.Sin(Mathf.Clamp01(Elapsed / .42f) * Mathf.PI);
            caption.localScale = Vector3.one * (1f + .085f * pop);
            if (Elapsed >= .78f) RestoreText();
        }

        public void Cancel()
        {
            if (IsComplete) return;
            IsComplete = true; RestoreText();
            if (group != null) group.alpha = 0f;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void RestoreText()
        {
            if (highlightedText != null && highlightedText.text == decoratedText) highlightedText.text = originalText;
            highlightedText = null; originalText = decoratedText = null;
        }
        private void OnDisable() => Cancel();
        private void OnDestroy() => RestoreText();
    }

    /// <summary>Bounded UI glints and an optional contract seal, using no additional textures.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RewardSweepGraphic : MaskableGraphic
    {
        private float progress;
        private bool contract;
        public void Configure(Color tint, bool seal = false) { color = tint; contract = seal; raycastTarget = false; }
        public void SetProgress(float value) { progress = Mathf.Clamp01(value); SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            float pulse = Mathf.Sin(progress * Mathf.PI);
            Color edge = color; edge.a *= pulse * .85f;
            float corner = Mathf.Min(36f, r.width * .14f);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? r.xMin + 2f : r.xMax - corner - 2f;
                Quad(mesh, new Rect(x, r.yMin + 2f, corner, 2f), edge);
                Quad(mesh, new Rect(x, r.yMax - 4f, corner, 2f), edge);
                float y = side == 0 ? r.yMin + 2f : r.yMax - corner - 2f;
                Quad(mesh, new Rect(r.xMin + 2f, y, 2f, corner), edge);
                Quad(mesh, new Rect(r.xMax - 4f, y, 2f, corner), edge);
            }
            float sweep = Mathf.Clamp01((progress - .04f) / .63f);
            float xCenter = Mathf.Lerp(r.xMin - 40f, r.xMax + 40f, sweep);
            for (int strip = -4; strip <= 4; strip++)
            {
                float left = Mathf.Clamp(xCenter + strip * 7f, r.xMin, r.xMax);
                float right = Mathf.Clamp(xCenter + (strip + 1) * 7f, r.xMin, r.xMax);
                Color light = color; light.a *= (1f - Mathf.Abs(strip) / 5f) * pulse * .16f;
                if (right > left) Quad(mesh, new Rect(left, r.yMin + 4f, right - left, r.height - 8f), light);
            }
            for (int i = 0; i < 10; i++)
            {
                float t = Mathf.Repeat(progress + i * .083f, 1f);
                Vector2 p = new Vector2(Mathf.Lerp(r.xMin + 12f, r.xMax - 12f, (i * 37 % 97) / 96f),
                    Mathf.Lerp(r.yMin + 10f, r.yMax - 10f, t));
                Color glint = color; glint.a *= Mathf.Sin(t * Mathf.PI) * pulse * .85f;
                Quad(mesh, new Rect(p.x - 1f, p.y - 4f, 2f, 8f), glint);
                Quad(mesh, new Rect(p.x - 4f, p.y - 1f, 8f, 2f), glint);
            }
            if (contract)
            {
                Vector2 center = new Vector2(r.xMax - 53f, r.yMin + 57f);
                float radius = 31f * (1f + .32f * (1f - Mathf.Clamp01(progress * 5f)));
                for (int i = 0; i < 40; i++)
                {
                    float a = i * Mathf.PI / 20f, b = (i + 1) * Mathf.PI / 20f;
                    Segment(mesh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                        center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 2.5f, edge);
                }
                Segment(mesh, center + new Vector2(-15f, -1f), center + new Vector2(-4f, -12f), 3.5f, edge);
                Segment(mesh, center + new Vector2(-4f, -12f), center + new Vector2(17f, 12f), 3.5f, edge);
            }
        }
        private static void Quad(VertexHelper mesh, Rect r, Color tint)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector3(r.xMin, r.yMin), tint, Vector2.zero); mesh.AddVert(new Vector3(r.xMin, r.yMax), tint, Vector2.zero);
            mesh.AddVert(new Vector3(r.xMax, r.yMax), tint, Vector2.zero); mesh.AddVert(new Vector3(r.xMax, r.yMin), tint, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
        private static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = (b - a).normalized; Vector2 s = new Vector2(-d.y, d.x) * width * .5f;
            int n = mesh.currentVertCount;
            mesh.AddVert(a - s, tint, Vector2.zero); mesh.AddVert(a + s, tint, Vector2.zero);
            mesh.AddVert(b + s, tint, Vector2.zero); mesh.AddVert(b - s, tint, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
