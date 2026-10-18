using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>One finite, interruptible presentation of an already committed treasure result.</summary>
    public sealed class TreasureResultMotion : MonoBehaviour
    {
        public const float IdentificationDuration = 1.2f;
        public const float CraftDuration = 1.85f;
        private RectTransform[] pieces;
        private CanvasGroup[] pieceGroups;
        private Vector2[] starts, ends;
        private float[] angles;
        private CanvasGroup targetGroup, captionGroup, seamGroup, chartGroup;
        private RectTransform chartMask, leftRoll, rightRoll;
        private Vector2 chartSize;
        private float targetAlpha;
        private bool ownsTargetGroup, restored, craft, hadOwner;
        private Behaviour owner;
        public float Elapsed { get; private set; }
        public float Duration => craft ? CraftDuration : IdentificationDuration;
        public bool IsComplete { get; private set; }
        public bool IsCrafting => craft;

        public static TreasureResultMotion PlayIdentification(RectTransform parent, RawImage target,
            TreasureChartDisplayLayout layout, int quadrant, Vector2 source, string rankName, bool hideTarget,
            Behaviour owner = null)
        {
            RectTransform root = Rect("Treasure Identification Motion", parent, parent.rect.size, Vector2.zero);
            var motion = root.gameObject.AddComponent<TreasureResultMotion>();
            motion.BindOwner(owner);
            if (hideTarget) motion.HideTarget(target);
            RawImage piece = Art("Identified Fragment", root, target.texture, layout.Size - Vector2.one * 5f,
                layout.Position, target.uvRect);
            layout.ProjectFragment(piece, quadrant);
            motion.pieces = new[] { piece.rectTransform };
            motion.pieceGroups = new[] { piece.gameObject.AddComponent<CanvasGroup>() };
            motion.ends = new[] { layout.Position };
            // Account for the actual four-corner paper projection when finding the drop point.
            motion.starts = new[] { source - layout.FragmentCenter(quadrant) };
            motion.angles = new[] { quadrant % 2 == 0 ? -5f : 5f };
            RectTransform caption = Rect("Identification Result", root, new Vector2(310f, 40f), source + new Vector2(0f, -67f));
            Image paper = caption.gameObject.AddComponent<Image>();
            paper.color = new Color(.14f, .10f, .055f, .91f); paper.raycastTarget = false;
            Label("Result", caption, new Vector2(242f, 38f), new Vector2(-28f, 0f), rankName + "碎片 +1", 20);
            motion.captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
            motion.AddSkip(caption, new Vector2(119f, 0f), new Vector2(65f, 38f));
            motion.SetElapsed(0f);
            return motion;
        }

        public static TreasureResultMotion PlayCraft(RawImage target, Behaviour owner = null)
        {
            RectTransform root = Rect("Treasure Craft Motion", target.transform.parent,
                target.rectTransform.rect.size, target.rectTransform.anchoredPosition);
            root.gameObject.AddComponent<RectMask2D>();
            var motion = root.gameObject.AddComponent<TreasureResultMotion>();
            motion.craft = true;
            motion.BindOwner(owner);
            motion.HideTarget(target);
            motion.chartSize = root.sizeDelta;
            motion.pieces = new RectTransform[4]; motion.pieceGroups = new CanvasGroup[4];
            motion.starts = new Vector2[4]; motion.ends = new Vector2[4]; motion.angles = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sy = i < 2 ? 1f : -1f;
                Vector2 end = Vector2.Scale(motion.chartSize, new Vector2(sx * .25f, sy * .25f));
                RawImage piece = Art("Chart Fragment " + i, root, target.texture, motion.chartSize * .5f,
                    end, new Rect(i % 2 * .5f, i < 2 ? .5f : 0f, .5f, .5f));
                motion.pieces[i] = piece.rectTransform;
                motion.pieceGroups[i] = piece.gameObject.AddComponent<CanvasGroup>();
                motion.ends[i] = end;
                motion.starts[i] = end + new Vector2(sx * 42f, sy * 24f);
                motion.angles[i] = sx * sy * 6f;
            }
            RectTransform seams = Rect("Brass Joining Light", root, motion.chartSize, Vector2.zero);
            motion.seamGroup = seams.gameObject.AddComponent<CanvasGroup>();
            Solid("Vertical Seam", seams, new Vector2(2f, motion.chartSize.y - 12f), Vector2.zero, new Color(1f, .81f, .42f, .7f));
            Solid("Horizontal Seam", seams, new Vector2(motion.chartSize.x - 12f, 2f), Vector2.zero, new Color(1f, .81f, .42f, .7f));
            motion.chartMask = Rect("Unfolding Chart", root, motion.chartSize, Vector2.zero);
            motion.chartMask.gameObject.AddComponent<RectMask2D>();
            motion.chartGroup = motion.chartMask.gameObject.AddComponent<CanvasGroup>();
            Art("Revealed Chart", motion.chartMask, target.texture, motion.chartSize, Vector2.zero, target.uvRect);
            motion.leftRoll = Solid("Left Paper Fold", root, new Vector2(3f, motion.chartSize.y), Vector2.zero,
                new Color(1f, .89f, .65f, .5f)).rectTransform;
            motion.rightRoll = Solid("Right Paper Fold", root, new Vector2(3f, motion.chartSize.y), Vector2.zero,
                new Color(1f, .89f, .65f, .5f)).rectTransform;
            RectTransform caption = Rect("Craft Result", root, new Vector2(188f, 34f),
                new Vector2(0f, -motion.chartSize.y * .5f + 23f));
            Solid("Caption Backing", caption, caption.sizeDelta, Vector2.zero, new Color(.14f, .10f, .055f, .88f));
            Label("Result", caption, new Vector2(130f, 32f), new Vector2(-25f, 0f), "拼合海图", 18);
            motion.captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
            motion.AddSkip(caption, new Vector2(62f, 0f), new Vector2(62f, 34f));
            motion.SetElapsed(0f);
            return motion;
        }

        private void BindOwner(Behaviour value) { owner = value; hadOwner = value != null; }

        private void HideTarget(RawImage target)
        {
            targetGroup = target.GetComponent<CanvasGroup>();
            ownsTargetGroup = targetGroup == null;
            if (ownsTargetGroup) targetGroup = target.gameObject.AddComponent<CanvasGroup>();
            targetAlpha = targetGroup.alpha;
            targetGroup.alpha = 0f;
        }

        private void AddSkip(Transform parent, Vector2 position, Vector2 size)
        {
            Image image = Solid("Skip Treasure Result", parent, size, position, new Color(.62f, .43f, .19f, .38f));
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label("Label", button.transform, size, Vector2.zero, "跳过", 17);
            button.onClick.AddListener(CompleteImmediately);
        }

        private void Update()
        {
            if (IsComplete) return;
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { CompleteImmediately(); return; }
            SetElapsed(Elapsed + Time.unscaledDeltaTime);
        }

        public void SetElapsed(float seconds)
        {
            if (IsComplete || pieces == null) return;
            Elapsed = Mathf.Clamp(seconds, 0f, Duration);
            if (Elapsed >= Duration) { CompleteImmediately(); return; }
            if (craft) ApplyCraft(); else ApplyIdentification();
        }

        private void ApplyIdentification()
        {
            float reveal = Ease((Elapsed - .02f) / .22f);
            float travel = Ease((Elapsed - .3f) / .64f);
            RectTransform piece = pieces[0];
            piece.anchoredPosition = Vector2.Lerp(starts[0], ends[0], travel) + Vector2.up * (Mathf.Sin(travel * Mathf.PI) * 28f);
            piece.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, reveal);
            piece.localRotation = Quaternion.Euler(0f, 0f, angles[0] * (1f - travel));
            pieceGroups[0].alpha = reveal;
            captionGroup.alpha = reveal * (1f - Ease((Elapsed - .94f) / .2f));
        }

        private void ApplyCraft()
        {
            float unfold = Ease((Elapsed - 1.02f) / .56f);
            for (int i = 0; i < pieces.Length; i++)
            {
                float arrive = Ease((Elapsed - .09f - i * .065f) / .63f);
                pieces[i].anchoredPosition = Vector2.Lerp(starts[i], ends[i], arrive);
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, angles[i] * (1f - arrive));
                pieces[i].localScale = Vector3.one * Mathf.Lerp(.96f, 1f, arrive);
                pieceGroups[i].alpha = Ease((Elapsed - i * .065f) / .18f) * (1f - Ease((Elapsed - 1.04f) / .18f));
            }
            seamGroup.alpha = Mathf.Sin(Mathf.Clamp01((Elapsed - .82f) / .35f) * Mathf.PI);
            float width = Mathf.Lerp(chartSize.x * .04f, chartSize.x, unfold);
            chartMask.sizeDelta = new Vector2(width, chartSize.y);
            chartGroup.alpha = Ease((Elapsed - 1.02f) / .1f);
            leftRoll.anchoredPosition = new Vector2(-width * .5f, 0f);
            rightRoll.anchoredPosition = new Vector2(width * .5f, 0f);
            float foldAlpha = chartGroup.alpha * (1f - unfold) * .55f;
            leftRoll.GetComponent<Image>().color = rightRoll.GetComponent<Image>().color = new Color(1f, .89f, .65f, foldAlpha);
            captionGroup.alpha = Ease(Elapsed / .18f) * (1f - Ease((Elapsed - 1.55f) / .25f));
        }

        public void CompleteImmediately()
        {
            if (IsComplete) return;
            IsComplete = true;
            Elapsed = Duration;
            RestoreTarget();
            gameObject.SetActive(false);
        }

        private void RestoreTarget()
        {
            if (restored) return;
            restored = true;
            if (targetGroup != null)
            {
                targetGroup.alpha = targetAlpha;
                if (ownsTargetGroup)
                {
                    if (Application.isPlaying) Destroy(targetGroup); else DestroyImmediate(targetGroup);
                }
            }
        }

        private void OnDisable()
        {
            IsComplete = true;
            RestoreTarget();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
        private void OnDestroy() => RestoreTarget();
        private static float Ease(float value) { float t = Mathf.Clamp01(value); return t * t * (3f - 2f * t); }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static RawImage Art(string name, Transform parent, Texture texture, Vector2 size, Vector2 position, Rect uv)
        {
            RawImage image = Rect(name, parent, size, position).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.uvRect = uv; image.raycastTarget = false;
            return image;
        }

        private static Image Solid(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            Image image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        private static void Label(string name, Transform parent, Vector2 size, Vector2 position, string value, int fontSize)
        {
            Text text = Rect(name, parent, size, position).gameObject.AddComponent<Text>();
            GameTypography.Apply(text, fontSize, FontStyle.Normal);
            text.color = new Color(1f, .9f, .69f, 1f); text.text = value;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        }
    }
}
