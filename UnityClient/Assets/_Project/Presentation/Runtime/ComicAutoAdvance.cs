using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Advances the same full-screen button used by a manual comic click.</summary>
    [DisallowMultipleComponent]
    public sealed class ComicAutoAdvance : MonoBehaviour
    {
        private static bool applicationPaused;
        private static bool applicationLostFocus;
        private Button advance;
        private Func<bool> canAdvance;
        private float secondsPerPage;
        private float elapsed;
        private float lastSampleTime;
        private int resetFrame = -1;

        public float RemainingSeconds
        {
            get
            {
                SampleElapsedTime();
                return Mathf.Max(0f, secondsPerPage - elapsed);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetApplicationState()
        {
            // Do not sample isFocused: batch players can start unfocused without
            // ever receiving a focus-loss event. Actual lifecycle events govern playback.
            applicationPaused = false;
            applicationLostFocus = false;
        }

        public void Configure(Button advanceButton, float seconds, Func<bool> canAdvance = null)
        {
            if (advance != null) advance.onClick.RemoveListener(ResetTimer);
            advance = advanceButton;
            this.canAdvance = canAdvance;
            secondsPerPage = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 6f : Mathf.Max(.05f, seconds);
            ResetTimer();
            if (advance != null) advance.onClick.AddListener(ResetTimer);
        }

        public void ResetTimer()
        {
            elapsed = 0f;
            RestartSampling();
        }

        /// <summary>
        /// Resumes a previously displayed page after its UI is rebuilt. Zero or a
        /// negative duration means the page is already due on the next LateUpdate.
        /// </summary>
        public void RestoreRemainingSeconds(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            elapsed = secondsPerPage - Mathf.Clamp(seconds, 0f, secondsPerPage);
            lastSampleTime = Time.unscaledTime;
            // Rebuilding the same page is not a new click or page. In particular,
            // restoring an expired timer must still fire if Render runs every frame.
            resetFrame = -1;
        }

        private void OnEnable() => RestartSampling();

        private void RestartSampling()
        {
            lastSampleTime = Time.unscaledTime;
            resetFrame = Time.frameCount;
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            RestartSampling();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationLostFocus = !focused;
            RestartSampling();
        }

        private bool CanCountDown() => Application.isPlaying && isActiveAndEnabled && !applicationPaused &&
            !applicationLostFocus && advance != null && advance.isActiveAndEnabled && advance.IsInteractable() &&
            (canAdvance == null || canAdvance());

        private void SampleElapsedTime()
        {
            float now = Time.unscaledTime;
            if (resetFrame != Time.frameCount && CanCountDown())
                elapsed += Mathf.Max(0f, now - lastSampleTime);
            // Sampling on capture also counts a frame that rebuilds the UI before
            // LateUpdate, so frequent same-page renders cannot indefinitely delay it.
            lastSampleTime = now;
        }

        private void LateUpdate()
        {
            // Pointer events are processed during Update. Waiting until LateUpdate
            // gives a click priority over the timeout when both happen in one frame.
            SampleElapsedTime();
            if (resetFrame == Time.frameCount || !CanCountDown()) return;
            if (elapsed < secondsPerPage) return;
            ResetTimer();
            advance.onClick.Invoke();
        }

        private void OnDestroy()
        {
            if (advance != null) advance.onClick.RemoveListener(ResetTimer);
            advance = null;
            canAdvance = null;
        }
    }
}
