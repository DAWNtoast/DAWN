using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public static class SettlementRevealAudio
    {
        public static string OutcomeCue(VoyageSettlement result) => result != null && result.Outcome == VoyageOutcome.Failure
            ? "settlement_failure" : result != null && result.TreasureCompleted ? "settlement_treasure" :
            result != null && result.Outcome == VoyageOutcome.ArrivedAtPort ? "harbor_arrive" : "settlement_reward";
    }

    /// <summary>Finite, unscaled, skippable presentation only. Never mutates voyage rewards or saves.</summary>
    public sealed class SettlementRevealController : MonoBehaviour
    {
        public const float Duration = 2.8f;
        private RectTransform[] tracks;
        private Vector2[] origins;
        private CanvasGroup[] groups;
        private float[] starts, countStarts;
        private Text[] values;
        private int[] targets;
        private SettlementCelebrationGraphic sparks;
        private Button skip;
        private bool animate, celebrate;
        private bool treasureCompleted;
        private string firstCue = "settlement_reward";
        private Action<string, float> playSound;
        private int soundStep;
        public float Elapsed { get; private set; }
        public bool IsComplete => Elapsed >= Duration;

        public void Configure(RectTransform[] elements, float[] revealStarts, Text[] numberLabels, int[] amounts, float[] numberStarts,
            SettlementCelebrationGraphic effect, Button skipButton, bool celebration, Action<string, float> audio, bool play)
        {
            Configure(elements, revealStarts, numberLabels, amounts, numberStarts, effect, skipButton, celebration, audio, play,
                celebration ? "settlement_reward" : "settlement_failure", false);
        }

        public void Configure(RectTransform[] elements, float[] revealStarts, Text[] numberLabels, int[] amounts, float[] numberStarts,
            SettlementCelebrationGraphic effect, Button skipButton, bool celebration, Action<string, float> audio, bool play,
            string outcomeCue, bool treasure)
        {
            tracks = elements; starts = revealStarts; values = numberLabels; targets = (int[])amounts.Clone(); countStarts = numberStarts;
            sparks = effect; skip = skipButton; celebrate = celebration; playSound = audio; animate = play;
            firstCue = string.IsNullOrEmpty(outcomeCue) ? "settlement_reward" : outcomeCue;
            treasureCompleted = treasure;
            origins = new Vector2[tracks.Length]; groups = new CanvasGroup[tracks.Length];
            for (int i = 0; i < tracks.Length; i++)
            {
                origins[i] = tracks[i].anchoredPosition;
                groups[i] = tracks[i].gameObject.AddComponent<CanvasGroup>();
            }
            SetElapsed(play ? 0f : Duration);
        }

        private void Update()
        {
            if (animate && Application.isPlaying && !IsComplete) SetElapsed(Elapsed + Time.unscaledDeltaTime, true);
        }

        public void CompleteImmediately() { SetElapsed(Duration); soundStep = 5; }

        public void SetElapsed(float seconds, bool emitSound = false)
        {
            if (tracks == null) return;
            Elapsed = Mathf.Clamp(seconds, 0, Duration);
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i] == null) continue;
                float t = Mathf.Clamp01((Elapsed - starts[i]) / 0.48f);
                float ease = 1f - Mathf.Pow(1f - t, 3);
                // Short local overshoot, no screen flash or camera shake.
                float pop = 1f + Mathf.Sin(t * Mathf.PI) * (celebrate ? 0.055f : 0.012f);
                groups[i].alpha = ease;
                tracks[i].localScale = Vector3.one * Mathf.Lerp(0.90f, pop, ease);
                tracks[i].anchoredPosition = origins[i] + new Vector2(0, (1f - ease) * -24f);
            }
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) continue;
                double t = Mathf.Clamp01((Elapsed - countStarts[i]) / 1.02f);
                double eased = 1d - Math.Pow(1d - t, 3d);
                int amount = t >= 1d ? targets[i] : (int)Math.Floor(targets[i] * eased);
                values[i].text = (amount > 0 ? "+" : "") + amount.ToString("N0");
                float landed = Mathf.Clamp01((Elapsed - countStarts[i] - 1.02f) / 0.34f);
                values[i].rectTransform.localScale = Vector3.one * (1f + (celebrate && targets[i] > 0 ? Mathf.Sin(landed * Mathf.PI) * 0.06f : 0f));
            }
            if (sparks != null) sparks.SetTime(Elapsed);
            if (skip != null)
            {
                skip.interactable = !IsComplete;
                skip.GetComponentInChildren<Text>().text = IsComplete ? "展示完成" : "跳过演出";
            }
            if (emitSound)
            {
                float[] cueTimes = { 0.22f, 0.66f, 1.10f, 1.42f, 2.13f };
                while (soundStep < cueTimes.Length && Elapsed >= cueTimes[soundStep])
                {
                    // A long frame should not stack every missed coin sound at once.
                    bool latest = soundStep + 1 == cueTimes.Length || Elapsed < cueTimes[soundStep + 1];
                    if (latest)
                    {
                        string cue = soundStep == 0 ? firstCue :
                            soundStep == 4 && treasureCompleted && firstCue != "settlement_treasure" ? "settlement_treasure" :
                            celebrate ? "settlement_reward" : string.Empty;
                        if (!string.IsNullOrEmpty(cue)) playSound?.Invoke(cue, soundStep == 0 ? 0.5f : 0.42f);
                    }
                    soundStep++;
                }
            }
        }
    }
}
