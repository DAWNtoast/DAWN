using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum BattleHudChangeKind { Added, Increased, Decreased, Removed, Changed, Gained, Spent }

    /// <summary>A finite decoration around a live HUD value; never interpolates or owns that value.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleHudChangeMotion : MonoBehaviour
    {
        public const float ChangeDuration = .72f;
        public const float RemovalDuration = .42f;
        private RectTransform decoration;
        private BattleHudChangeGraphic frame;
        private CanvasGroup group, retiredGroup;
        private Text delta;
        private Color tint;
        private Behaviour owner;
        private bool hadOwner, retiring;
        public bool IsPlaying { get; private set; }
        public int PlayCount { get; private set; }
        public string LastText { get; private set; } = string.Empty;
        public BattleHudChangeKind LastKind { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration => retiring ? RemovalDuration : ChangeDuration;

        public void SetOwner(Behaviour value) { owner = value; hadOwner = value != null; }

        public void Play(string message, Color color, BattleHudChangeKind kind, Font font, bool removed = false)
        {
            EnsureDecoration(font);
            retiring = removed;
            if (retiring)
            {
                retiredGroup = GetComponent<CanvasGroup>();
                if (retiredGroup == null) retiredGroup = gameObject.AddComponent<CanvasGroup>();
                retiredGroup.blocksRaycasts = false; retiredGroup.interactable = false;
            }
            LastText = message; LastKind = kind; tint = color;
            delta.text = message;
            IsPlaying = true; PlayCount++;
            decoration.gameObject.SetActive(true);
            SetElapsed(0f);
        }

        private void EnsureDecoration(Font font)
        {
            if (decoration != null) { delta.font = font; return; }
            decoration = NewRect("Battle HUD Change", transform, Vector2.zero, Vector2.zero);
            decoration.anchorMin = Vector2.zero; decoration.anchorMax = Vector2.one;
            decoration.offsetMin = decoration.offsetMax = Vector2.zero;
            group = decoration.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            frame = decoration.gameObject.AddComponent<BattleHudChangeGraphic>();
            frame.raycastTarget = false;
            RectTransform textRect = NewRect("Delta", decoration,
                new Vector2(Mathf.Max(50f, ((RectTransform)transform).rect.width), 23f), Vector2.zero);
            delta = textRect.gameObject.AddComponent<Text>();
            delta.font = font; delta.fontStyle = FontStyle.Normal; delta.fontSize = 16;
            delta.alignment = TextAnchor.MiddleCenter; delta.raycastTarget = false;
            delta.resizeTextForBestFit = true; delta.resizeTextMinSize = 10; delta.resizeTextMaxSize = 16;
            delta.horizontalOverflow = HorizontalWrapMode.Wrap;
            delta.verticalOverflow = VerticalWrapMode.Truncate;
            Outline outline = textRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.015f, .025f, .04f, 1f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void Update()
        {
            if (!IsPlaying) return;
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { Cancel(); return; }
            SetElapsed(Elapsed + Time.unscaledDeltaTime);
        }

        public void SetElapsed(float elapsed)
        {
            if (!IsPlaying || decoration == null) return;
            Elapsed = Mathf.Clamp(elapsed, 0f, Duration);
            float t = Elapsed / Duration;
            float appear = Mathf.Clamp01((Elapsed + .045f) / .12f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - .48f) / .52f));
            group.alpha = appear * fade;
            frame.color = new Color(tint.r, tint.g, tint.b, (.45f + .55f * Mathf.Sin(t * Mathf.PI)) * fade);
            delta.color = tint;
            delta.rectTransform.anchoredPosition = new Vector2(0f,
                ((RectTransform)transform).rect.height * .5f - 10f + t * 4f);
            if (retiring && retiredGroup != null) retiredGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            if (t >= 1f) Cancel();
        }

        public void Cancel()
        {
            IsPlaying = false;
            if (decoration != null) decoration.gameObject.SetActive(false);
            if (retiring && retiredGroup != null) retiredGroup.alpha = 0f;
        }

        private void OnDisable() => Cancel();
        private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleHudChangeGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            float thickness = Mathf.Min(1.5f, r.width * .04f), corner = Mathf.Min(10f, r.width * .18f);
            Color wash = color; wash.a *= .09f;
            Quad(mesh, new Rect(r.xMin, r.yMin, r.width, r.height), wash);
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? r.xMin : r.xMax - corner;
                Quad(mesh, new Rect(x, r.yMin, corner, thickness), color);
                Quad(mesh, new Rect(x, r.yMax - thickness, corner, thickness), color);
            }
            for (int side = 0; side < 2; side++)
            {
                float y = side == 0 ? r.yMin : r.yMax - corner;
                Quad(mesh, new Rect(r.xMin, y, thickness, corner), color);
                Quad(mesh, new Rect(r.xMax - thickness, y, thickness, corner), color);
            }
        }

        private static void Quad(VertexHelper mesh, Rect rect, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
