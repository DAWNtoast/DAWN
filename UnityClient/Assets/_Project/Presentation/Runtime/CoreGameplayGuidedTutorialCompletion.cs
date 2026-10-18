using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private GameObject guidedTutorialCompletionNotice;

        // Only the successful final equipment action calls this. Loading an
        // already completed save must never replay the graduation message.
        private void ShowGuidedTutorialCompletionNotice()
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked ||
                legacyCanvasRoot == null || guidedTutorialCompletionNotice != null) return;

            // Live outside Phase Content so refreshing the equipment page or
            // returning to port cannot remove the message before it fades.
            RectTransform notice = CreateRect("Guided Tutorial Completion Notice", legacyCanvasRoot,
                new Vector2(1320f, 236f), Vector2.zero);
            guidedTutorialCompletionNotice = notice.gameObject;
            Canvas canvas = notice.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1600;
            CanvasGroup group = notice.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            Text message = CreateText("Guided Tutorial Completion Text", notice,
                new Vector2(1224f, 180f), Vector2.zero, 56, FontStyle.Bold,
                TextAnchor.MiddleCenter, Color.white);
            message.text = "恭喜完成引导，\n接下来开始自由航行吧";
            message.color = Html("#F23838");
            message.resizeTextForBestFit = false;
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Truncate;
            Shadow shadow = message.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .8f);
            shadow.effectDistance = new Vector2(2f, -2f);

            var presenter = notice.gameObject.AddComponent<GuidedTutorialCompletionPresenter>();
            presenter.Owner = this;
            presenter.Group = group;
            presenter.ShownAt = Time.unscaledTime;
        }

        private sealed class GuidedTutorialCompletionPresenter : MonoBehaviour
        {
            internal CoreGameplayDemoController Owner;
            internal CanvasGroup Group;
            internal float ShownAt;
            private const float HoldSeconds = 3f;
            private const float FadeSeconds = 3f;

            private void Update()
            {
                float elapsed = Time.unscaledTime - ShownAt;
                if (Owner == null || !Owner.isActiveAndEnabled || Owner.profileSaveBlocked ||
                    elapsed >= HoldSeconds + FadeSeconds)
                {
                    Destroy(gameObject);
                    return;
                }

                Group.alpha = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((elapsed - HoldSeconds) / FadeSeconds));
            }
        }
    }
}
