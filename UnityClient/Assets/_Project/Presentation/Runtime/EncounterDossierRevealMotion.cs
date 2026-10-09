using System.Collections;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Short, interruptible entrance motion for the event dossier.</summary>
    public sealed class EncounterDossierRevealMotion : MonoBehaviour
    {
        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 targetPosition;
        private Vector3 targetScale;

        public void Play(RectTransform target, float duration = 0.32f, bool animate = true)
        {
            StopAllCoroutines();
            // An interrupted entrance must not make its temporary offset/scale
            // the destination of the next entrance.
            ApplyFinalState();
            rect = target == null ? GetComponent<RectTransform>() : target;
            if (rect == null) return;
            // Unity components can be fake-null, which the CLR ?? operator does
            // not recognize. Reacquire/add the native component explicitly.
            group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            targetPosition = rect.anchoredPosition;
            targetScale = rect.localScale;
            if (!Application.isPlaying || !animate || !isActiveAndEnabled)
            {
                ApplyFinalState();
                return;
            }
            StartCoroutine(Reveal(Mathf.Clamp(duration, 0.18f, 0.55f)));
        }

        private IEnumerator Reveal(float duration)
        {
            if (rect == null || group == null) yield break;
            Vector2 startPosition = targetPosition + Vector2.down * 34f;
            Vector3 startScale = targetScale * 0.985f;
            rect.anchoredPosition = startPosition;
            rect.localScale = startScale;
            group.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < duration && rect != null && group != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
                rect.localScale = Vector3.LerpUnclamped(startScale, targetScale, eased);
                group.alpha = Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }
            ApplyFinalState();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ApplyFinalState();
        }

        private void ApplyFinalState()
        {
            if (rect == null) return;
            rect.anchoredPosition = targetPosition;
            rect.localScale = targetScale;
            if (group != null) group.alpha = 1f;
        }
    }
}
