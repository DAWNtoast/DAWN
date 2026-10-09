using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private CoreDemoPhase transitionPhase = (CoreDemoPhase)(-1);
        private int transitionFacility = int.MinValue;
        private CanvasGroup transitionCanvasGroup;
        private Coroutine transitionRoutine;

        private void SuspendScreenTransition()
        {
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = null;
            // A disabled GameObject cancels the coroutine before its final
            // assignment; never leave the retained screen partly transparent.
            if (transitionCanvasGroup != null) transitionCanvasGroup.alpha = 1f;
        }

        private void PlayScreenTransitionIfNeeded()
        {
            if (!Application.isPlaying || session == null || contentRoot == null) return;
            int facility = session.Phase == CoreDemoPhase.Port ? activePortFacility : -1;
            bool changed = session.Phase != transitionPhase || facility != transitionFacility;
            transitionPhase = session.Phase;
            transitionFacility = facility;
            if (!changed) return;

            if (transitionCanvasGroup == null)
                transitionCanvasGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(RevealScreen(0.24f));
        }

        private IEnumerator RevealScreen(float duration)
        {
            if (transitionCanvasGroup == null) yield break;
            transitionCanvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < duration && transitionCanvasGroup != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transitionCanvasGroup.alpha = 1f - Mathf.Pow(1f - t, 3f);
                yield return null;
            }
            if (transitionCanvasGroup != null) transitionCanvasGroup.alpha = 1f;
            transitionRoutine = null;
        }
    }
}
