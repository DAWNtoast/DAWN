using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Owns only temporary presentation state, with exact restoration on cancellation.</summary>
    public sealed class BattleResolutionMotion : MonoBehaviour
    {
        private struct Pose
        {
            public RectTransform Rect;
            public Vector2 Position;
            public Vector3 Scale;
            public Quaternion Rotation;
            public CanvasGroup Group;
            public float Alpha;
            public bool AddedGroup, Move;
        }

        private Pose[] poses;
        private Behaviour owner;
        private bool hadOwner, restored, focusLost;
        private BattleShipIdleMotion idle;
        private bool idleWasEnabled;
        private BattleResolutionGraphic graphic;
        private Text banner;
        private Vector3 bannerScale;
        private float direction;
        public BattleResolutionKind Kind { get; private set; }
        public RectTransform Target { get; private set; }
        public float Progress { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool HasRestoredPose => restored;
        public bool IsTopDown { get; private set; }
        public float Duration => Kind == BattleResolutionKind.NavalSinking ? .95f : .72f;

        public static BattleResolutionKind DisplayedKind(RectTransform target) =>
            target != null && (target.GetComponent<BoardingPartyPresentation>() != null ||
                target.Find(target.name + " Formation Label") != null)
                ? BattleResolutionKind.BoardingDefeat : BattleResolutionKind.NavalSinking;

        public void Configure(RectTransform stage, RectTransform target, Behaviour presentationOwner,
            BattleResolutionKind kind, RectTransform waterContact = null)
        {
            RestorePose();
            owner = presentationOwner; hadOwner = owner != null;
            Target = target; Kind = kind; restored = false; Progress = 0f;
            IsPlaying = target != null && stage != null;
            if (!IsPlaying) return;
            IsTopDown = kind == BattleResolutionKind.NavalSinking && target.GetComponent<Image>() != null &&
                stage.GetComponentInChildren<NavalTacticalBoardView>() != null;
            direction = stage.InverseTransformPoint(target.position).x < 0f ? -1f : 1f;
            idle = target.GetComponent<BattleShipIdleMotion>();
            idleWasEnabled = idle != null && idle.enabled;
            if (idle != null) idle.enabled = false;
            RawImage[] figures = kind == BattleResolutionKind.BoardingDefeat ? target.GetComponentsInChildren<RawImage>(true) : null;
            int count = figures != null && figures.Length > 0 ? figures.Length : 1;
            bool includeWater = kind == BattleResolutionKind.NavalSinking && waterContact != null && waterContact != target;
            poses = new Pose[count + (includeWater ? 1 : 0)];
            for (int i = 0; i < count; i++)
                poses[i] = Capture(figures != null && figures.Length > 0 ? figures[i].rectTransform : target, true);
            if (includeWater) poses[count] = Capture(waterContact, false);
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 low = stage.InverseTransformPoint(corners[0]), high = low;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 corner = stage.InverseTransformPoint(corners[i]);
                low = Vector2.Min(low, corner); high = Vector2.Max(high, corner);
            }
            Vector2 span = high - low;
            RectTransform host = (RectTransform)transform;
            host.anchoredPosition = IsTopDown ? (Vector2)stage.InverseTransformPoint(target.position) :
                waterContact != null && kind == BattleResolutionKind.NavalSinking
                ? (Vector2)stage.InverseTransformPoint(waterContact.position)
                : new Vector2((low.x + high.x) * .5f, Mathf.Min(low.y, high.y) + span.y * .08f);
            host.sizeDelta = new Vector2(Mathf.Max(320f, span.x * 1.6f), Mathf.Max(220f, span.y));
            graphic = GetComponent<BattleResolutionGraphic>();
            if (graphic == null) graphic = gameObject.AddComponent<BattleResolutionGraphic>();
            graphic.Configure(kind, span, direction, IsTopDown);
            Apply(0f);
        }

        public void AttachBanner(Text text)
        {
            banner = text;
            bannerScale = banner == null ? Vector3.one : banner.rectTransform.localScale;
        }

        private static Pose Capture(RectTransform rect, bool move)
        {
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            bool added = group == null;
            if (added) group = rect.gameObject.AddComponent<CanvasGroup>();
            return new Pose { Rect = rect, Position = rect.anchoredPosition, Scale = rect.localScale,
                Rotation = rect.localRotation, Group = group, Alpha = group.alpha, AddedGroup = added, Move = move };
        }

        public void AdvancePresentation(float seconds)
        {
            if (!IsPlaying) return;
            if (!isActiveAndEnabled || Target == null || (hadOwner && (owner == null || !owner.isActiveAndEnabled)))
            { CancelAndRestore(); return; }
            if (focusLost) return;
            Progress = Mathf.Clamp01(Progress + Mathf.Max(0f, seconds) / Duration);
            Apply(Progress);
            if (Progress >= 1f) IsPlaying = false;
        }

        private void Apply(float progress)
        {
            if (poses == null) return;
            for (int i = 0; i < poses.Length; i++)
            {
                Pose pose = poses[i];
                if (pose.Rect == null || pose.Group == null) continue;
                if (!pose.Move)
                { pose.Group.alpha = pose.Alpha * (1f - Mathf.SmoothStep(0f, 1f, progress)); continue; }
                if (Kind == BattleResolutionKind.NavalSinking)
                {
                    float sink = progress * progress;
                    if (IsTopDown)
                    {
                        // In the top-down chart, sinking changes apparent depth, never the
                        // defeated vessel's world cell or its displayed compass heading.
                        pose.Rect.anchoredPosition = pose.Position;
                        pose.Rect.localRotation = pose.Rotation;
                        pose.Rect.localScale = pose.Scale * Mathf.Lerp(1f, .56f, sink);
                    }
                    else
                    {
                        float depth = Mathf.Clamp(pose.Rect.rect.height * .30f, 55f, 145f);
                        pose.Rect.anchoredPosition = pose.Position + Vector2.down * depth * sink;
                        pose.Rect.localRotation = pose.Rotation * Quaternion.Euler(0f, 0f, -direction * 18f * sink);
                    }
                    pose.Group.alpha = pose.Alpha * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.18f, 1f, progress)));
                }
                else
                {
                    float t = Mathf.Clamp01(progress / Mathf.Max(.7f, 1f - i * .045f));
                    float fall = t * t;
                    pose.Rect.anchoredPosition = pose.Position + new Vector2(direction * 16f * fall,
                        -Mathf.Clamp(pose.Rect.rect.height * .16f, 28f, 70f) * fall);
                    pose.Rect.localRotation = pose.Rotation * Quaternion.Euler(0f, 0f, -direction * (11f + i * 2f) * fall);
                    pose.Group.alpha = pose.Alpha * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, .96f, t)));
                }
            }
            if (graphic != null) graphic.SetProgress(progress);
            if (banner != null)
                banner.rectTransform.localScale = bannerScale * Mathf.Lerp(.9f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 2.5f)));
        }

        public void CancelAndRestore()
        {
            IsPlaying = false;
            RestorePose();
            if (graphic != null) graphic.SetProgress(1f);
            if (banner != null) banner.gameObject.SetActive(false);
        }

        private void RestorePose()
        {
            if (restored) return;
            restored = true;
            if (poses != null)
                for (int i = 0; i < poses.Length; i++)
                {
                    Pose pose = poses[i];
                    if (pose.Rect != null)
                    {
                        pose.Rect.anchoredPosition = pose.Position;
                        pose.Rect.localScale = pose.Scale;
                        pose.Rect.localRotation = pose.Rotation;
                    }
                    if (pose.Group != null)
                    {
                        pose.Group.alpha = pose.Alpha;
                        if (pose.AddedGroup)
                        {
                            if (Application.isPlaying) Destroy(pose.Group);
                            else DestroyImmediate(pose.Group);
                        }
                    }
                }
            if (idle != null) idle.enabled = idleWasEnabled;
            if (banner != null) banner.rectTransform.localScale = bannerScale;
        }

        private void LateUpdate()
        {
            if (!restored && (Target == null || (hadOwner && (owner == null || !owner.isActiveAndEnabled)))) CancelAndRestore();
        }
        private void OnApplicationFocus(bool focused) { focusLost = !focused; }
        private void OnApplicationPause(bool paused) { if (paused) CancelAndRestore(); }
        private void OnDisable() => CancelAndRestore();
        private void OnDestroy() => RestorePose();
    }
}
