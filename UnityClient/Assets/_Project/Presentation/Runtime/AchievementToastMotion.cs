using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Non-blocking, finite movement of the existing committed-achievement notification.</summary>
    public sealed class AchievementToastMotion : MonoBehaviour
    {
        public const float Duration = 4f;
        private RectTransform card;
        private CanvasGroup group;
        private RewardSweepGraphic badgeGlint;
        private Behaviour owner;
        private bool hadOwner;
        private Vector2 restingPosition;
        public float Elapsed { get; private set; }
        public bool IsComplete { get; private set; } = true;

        public void Configure(Behaviour owner, RectTransform badge)
        {
            this.owner = owner; hadOwner = owner != null;
            card = (RectTransform)transform; restingPosition = card.anchoredPosition;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            var glintRect = (RectTransform)new GameObject("Achievement Badge Glint", typeof(RectTransform)).transform;
            glintRect.SetParent(card, false); glintRect.sizeDelta = new Vector2(52f, 52f);
            glintRect.anchoredPosition = badge.anchoredPosition;
            badgeGlint = glintRect.gameObject.AddComponent<RewardSweepGraphic>();
            badgeGlint.Configure(new Color(1f, .83f, .45f));
        }

        public void Play()
        {
            if (card == null) return;
            IsComplete = false; Elapsed = 0f;
            gameObject.SetActive(true); SetElapsed(0f);
        }

        private void Update()
        {
            if (IsComplete) return;
            if (hadOwner && (owner == null || !owner.isActiveAndEnabled)) { Cancel(); return; }
            SetElapsed(Elapsed + Time.unscaledDeltaTime);
        }

        public void SetElapsed(float seconds)
        {
            if (IsComplete || card == null) return;
            Elapsed = Mathf.Clamp(float.IsNaN(seconds) ? Duration : seconds, 0f, Duration);
            if (Elapsed >= Duration) { Cancel(); return; }
            float enter = Mathf.Clamp01(Elapsed / .32f);
            float exit = Mathf.Clamp01((Elapsed - 3.58f) / .42f);
            float ease = 1f - Mathf.Pow(1f - enter, 3f);
            card.anchoredPosition = restingPosition + Vector2.right * ((1f - ease) * (card.rect.width + 42f) + exit * 34f);
            group.alpha = Mathf.Clamp01(Elapsed / .14f) * (1f - Mathf.SmoothStep(0f, 1f, exit));
            badgeGlint.SetProgress(Mathf.Clamp01((Elapsed - .20f) / 1.05f));
        }

        public void Cancel()
        {
            IsComplete = true;
            if (card != null) card.anchoredPosition = restingPosition;
            if (group != null) group.alpha = 0f;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
        private void OnDisable()
        {
            IsComplete = true;
            if (card != null) card.anchoredPosition = restingPosition;
            if (group != null) group.alpha = 0f;
        }
    }
}
