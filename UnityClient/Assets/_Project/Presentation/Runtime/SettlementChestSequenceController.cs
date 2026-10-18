using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum SettlementChestSequencePhase { Opening, Rewards, Anticipation, Jackpot, Closing, Complete }

    /// <summary>
    /// Unscaled presentation of an immutable reward snapshot. Each reward has one visual that flies
    /// out, counts up and lands in the manifest; this controller never grants or confirms rewards.
    /// </summary>
    public sealed class SettlementChestSequenceController : MonoBehaviour
    {
        public static readonly Vector2 MouthPosition = new Vector2(-340f, 80f);
        public static readonly Vector2 ShowcasePosition = new Vector2(320f, 95f);
        public const float LandedScale = .43f;
        public const float OpeningDuration = .95f;
        public const float JackpotChargeDuration = 1.25f;
        public const float JackpotFlightDuration = .80f;
        public const float JackpotCountDuration = 1.70f;
        public const float JackpotHoldDuration = .35f;
        public const float JackpotLandDuration = .50f;
        public const float FadeDuration = .55f;
        private const float JackpotDuration = JackpotChargeDuration + JackpotFlightDuration +
            JackpotCountDuration + JackpotHoldDuration + JackpotLandDuration;

        private SettlementChestRewardPlan plan;
        private CanvasGroup intro, overview;
        private RectTransform chest;
        private RectTransform[] rewards;
        private CanvasGroup[] rewardGroups;
        private Text[] amounts;
        private Vector2[] landings;
        private Text title, status;
        private SettlementFireworksGraphic fireworks;
        private Button skip;
        private Action<string, float> playSound;
        private Action completed;
        private Vector2 chestOrigin;
        private Vector3 chestScale;
        private Quaternion chestRotation;
        private float ordinaryDuration, rewardsEnd, fadeStart;
        private int ordinaryCount;
        private bool animate, allowCompletion, completionDelivered, configured;
        private readonly List<float> soundTimes = new List<float>();
        private readonly List<string> soundCues = new List<string>();
        private readonly List<float> soundVolumes = new List<float>();
        private int soundCursor;

        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public float JackpotStart { get; private set; } = -1f;
        public float FireworksStart { get; private set; } = -1f;
        public bool IsComplete => configured && Elapsed >= Duration;
        public SettlementChestSequencePhase Phase { get; private set; }

        public void Configure(SettlementChestRewardPlan rewardPlan, CanvasGroup introGroup, CanvasGroup overviewGroup,
            RectTransform chestTransform, RectTransform[] rewardTransforms, CanvasGroup[] groups, Text[] rewardAmounts,
            Vector2[] landedPositions, Text titleLabel, Text statusLabel, SettlementFireworksGraphic effect,
            Button skipButton, Action<string, float> audio, Action onCompleted, bool play)
        {
            if (skip != null) skip.onClick.RemoveListener(CompleteImmediately);
            plan = rewardPlan ?? SettlementChestRewardPlan.Create(null);
            intro = introGroup;
            overview = overviewGroup;
            chest = chestTransform;
            rewards = rewardTransforms ?? Array.Empty<RectTransform>();
            rewardGroups = groups ?? Array.Empty<CanvasGroup>();
            amounts = rewardAmounts ?? Array.Empty<Text>();
            landings = landedPositions ?? Array.Empty<Vector2>();
            title = titleLabel;
            status = statusLabel;
            fireworks = effect;
            skip = skipButton;
            playSound = audio;
            completed = onCompleted;
            chestOrigin = chest != null ? chest.anchoredPosition : Vector2.zero;
            chestScale = chest != null ? chest.localScale : Vector3.one;
            chestRotation = chest != null ? chest.localRotation : Quaternion.identity;
            ordinaryCount = plan.Items.Count - (plan.IsJackpot ? 1 : 0);
            ordinaryDuration = plan.IsJackpot
                ? Mathf.Clamp(4.2f / Mathf.Max(1, ordinaryCount), .58f, 1.05f)
                : Mathf.Clamp(4.8f / Mathf.Max(1, ordinaryCount), .62f, 1.35f);
            JackpotStart = plan.IsJackpot ? OpeningDuration + ordinaryCount * ordinaryDuration : -1f;
            FireworksStart = plan.IsJackpot ? JackpotStart + JackpotChargeDuration + JackpotFlightDuration : -1f;
            rewardsEnd = plan.IsJackpot ? JackpotStart + JackpotDuration : OpeningDuration + ordinaryCount * ordinaryDuration;
            Duration = plan.HasRewards ? Mathf.Max(plan.IsJackpot ? 8.2f : 5.2f, rewardsEnd + .75f + FadeDuration) : 0f;
            fadeStart = Duration - FadeDuration;
            completionDelivered = false;
            animate = play;
            allowCompletion = false;
            configured = true;
            BuildSoundTimeline();
            if (skip != null) skip.onClick.AddListener(CompleteImmediately);
            // Static previews start on the completed overview and never run gameplay callbacks.
            SetElapsed(play ? 0f : Duration);
            allowCompletion = play;
            if (play && IsComplete) DeliverCompletion();
        }

        private void Update()
        {
            if (animate && Application.isPlaying && !IsComplete)
                SetElapsed(Elapsed + Time.unscaledDeltaTime, true);
        }

        public void CompleteImmediately()
        {
            if (!configured) return;
            animate = false;
            SetElapsed(Duration);
        }

        /// <summary>
        /// Deterministic scrubbing for previews and acceptance captures, including rewinding from
        /// the overview. Silent by default; seeking consumes past cues instead of queuing them.
        /// </summary>
        public void SetElapsed(float seconds, bool emitSound = false)
        {
            if (!configured) return;
            float previous = Elapsed;
            Elapsed = float.IsNaN(seconds) ? 0f : Mathf.Clamp(seconds, 0f, Duration);
            float fade = Duration > 0 ? Smooth((Elapsed - fadeStart) / FadeDuration) : 1f;
            if (intro != null)
            {
                if (!IsComplete && !intro.gameObject.activeSelf) intro.gameObject.SetActive(true);
                intro.alpha = 1f - fade;
                intro.interactable = !IsComplete;
                intro.blocksRaycasts = !IsComplete;
            }
            if (overview != null)
            {
                overview.alpha = fade;
                overview.interactable = IsComplete;
                overview.blocksRaycasts = IsComplete;
            }
            AnimateChest();
            for (int i = 0; i < plan.Items.Count; i++)
            {
                if (plan.Items[i].IsJackpot) AnimateJackpot(i);
                else AnimateOrdinary(i);
            }
            if (fireworks != null)
                fireworks.SetTime(plan.IsJackpot && !IsComplete ? Elapsed - FireworksStart : -1f);
            UpdateCopy();
            if (skip != null)
            {
                skip.interactable = !IsComplete;
                Text label = skip.GetComponentInChildren<Text>();
                if (label != null) label.text = IsComplete ? "展示完成" : "跳过演出";
            }
            AdvanceAudio(previous, emitSound);
            if (IsComplete)
            {
                // Deactivate the overlay only after all visuals have reached their exact final state.
                EventSystem events = EventSystem.current;
                if (events != null && skip != null && events.currentSelectedGameObject == skip.gameObject)
                    events.SetSelectedGameObject(null);
                if (intro != null) intro.gameObject.SetActive(false);
                DeliverCompletion();
            }
        }

        private void AnimateChest()
        {
            if (chest == null) return;
            float entry = Mathf.Clamp01(Elapsed / .72f);
            float bounce = Mathf.Sin(entry * Mathf.PI) * (plan.IsFailure ? 10f : 28f);
            float tilt = 0f;
            float stretch = 1f + Mathf.Sin(entry * Mathf.PI) * .045f;
            if (!plan.IsFailure)
            {
                for (int i = 0; i < ordinaryCount; i++)
                {
                    float pulse = Mathf.Clamp01((Elapsed - OpeningDuration - i * ordinaryDuration) / .36f);
                    bounce += Mathf.Sin(pulse * Mathf.PI) * 17f;
                    tilt += Mathf.Sin(pulse * Mathf.PI * 2f) * (i % 2 == 0 ? 3.2f : -3.2f);
                }
                if (plan.IsJackpot)
                {
                    float charge = Mathf.Clamp01((Elapsed - JackpotStart) / JackpotChargeDuration);
                    if (Elapsed >= JackpotStart && Elapsed < JackpotStart + JackpotChargeDuration)
                    {
                        float tension = charge * charge;
                        tilt += Mathf.Sin(charge * 62f) * (1f + 6f * tension);
                        bounce += Mathf.Abs(Mathf.Sin(charge * 37f)) * 12f * tension;
                        stretch += .055f * tension;
                    }
                    float release = Mathf.Clamp01((Elapsed - JackpotStart - JackpotChargeDuration) / .55f);
                    bounce += Mathf.Sin(release * Mathf.PI) * 36f;
                    stretch += Mathf.Sin(release * Mathf.PI) * .075f;
                }
            }
            chest.anchoredPosition = chestOrigin + new Vector2(0f, Mathf.Lerp(-44f, 0f, Smooth(entry)) + bounce);
            chest.localScale = chestScale * Mathf.Lerp(.82f, stretch, Smooth(entry));
            chest.localRotation = chestRotation * Quaternion.Euler(0f, 0f, tilt);
        }

        private void AnimateOrdinary(int index)
        {
            float local = Elapsed - OpeningDuration - index * ordinaryDuration;
            float flight = Mathf.Min(.36f, ordinaryDuration * .31f);
            float landing = Mathf.Min(.34f, ordinaryDuration * .31f);
            float landStart = ordinaryDuration - landing;
            if (local < 0f) { SetReward(index, MouthPosition, .18f, 0f, 0d); return; }
            if (local < flight)
            {
                float p = Mathf.Clamp01(local / flight);
                Vector2 position = Vector2.Lerp(MouthPosition, ShowcasePosition, OutCubic(p));
                position.y += Mathf.Sin(p * Mathf.PI) * (plan.IsFailure ? 55f : 110f);
                SetReward(index, position, Mathf.Lerp(.18f, 1f, OutCubic(p)), Mathf.Clamp01(p * 3f), 0d);
                return;
            }
            if (local < landStart)
            {
                float count = Mathf.Clamp01((local - flight) / (landStart - flight));
                float scale = 1f + Mathf.Sin(count * Mathf.PI) * (plan.IsFailure ? .015f : .055f);
                SetReward(index, ShowcasePosition, scale, 1f, OutCubic(count));
                return;
            }
            LandReward(index, Mathf.Clamp01((local - landStart) / landing), 1f);
        }

        private void AnimateJackpot(int index)
        {
            float local = Elapsed - JackpotStart - JackpotChargeDuration;
            if (local < 0f) { SetReward(index, MouthPosition, .18f, 0f, 0d); return; }
            if (local < JackpotFlightDuration)
            {
                float p = Mathf.Clamp01(local / JackpotFlightDuration);
                Vector2 position = Vector2.Lerp(MouthPosition, ShowcasePosition, Smooth(p));
                position.y += Mathf.Sin(p * Mathf.PI) * 150f;
                SetReward(index, position, Mathf.Lerp(.18f, 1.10f, Smooth(p)), Mathf.Clamp01(p * 2.5f), 0d);
                return;
            }
            float count = Mathf.Clamp01((local - JackpotFlightDuration) / JackpotCountDuration);
            float landStart = JackpotFlightDuration + JackpotCountDuration + JackpotHoldDuration;
            if (local < landStart)
            {
                // A slow start, then a rising count, gives the biggest prize room to land emotionally.
                double valueProgress = count >= 1f ? 1d : Math.Pow(count, 1.45d);
                float celebrationPop = Mathf.Sin(Mathf.Clamp01((local - JackpotFlightDuration - JackpotCountDuration) /
                    JackpotHoldDuration) * Mathf.PI) * .055f;
                SetReward(index, ShowcasePosition + Vector2.up * Mathf.Sin(count * Mathf.PI) * 6f,
                    1.10f + celebrationPop, 1f, valueProgress);
                return;
            }
            LandReward(index, Mathf.Clamp01((local - landStart) / JackpotLandDuration), 1.10f);
        }

        private void LandReward(int index, float progress, float startScale)
        {
            Vector2 target = index < landings.Length ? landings[index] :
                new Vector2((index - (plan.Items.Count - 1) * .5f) * 164f, -335f);
            float p = Smooth(progress);
            Vector2 position = Vector2.Lerp(ShowcasePosition, target, p);
            position.y += Mathf.Sin(progress * Mathf.PI) * 42f;
            SetReward(index, position, Mathf.Lerp(startScale, LandedScale, p), 1f, 1d);
        }

        private void SetReward(int index, Vector2 position, float scale, float alpha, double progress)
        {
            if (index < rewards.Length && rewards[index] != null)
            {
                rewards[index].anchoredPosition = position;
                rewards[index].localScale = Vector3.one * scale;
                rewards[index].localRotation = Quaternion.identity;
            }
            if (index < rewardGroups.Length && rewardGroups[index] != null)
            {
                rewardGroups[index].alpha = alpha;
                rewardGroups[index].interactable = false;
                rewardGroups[index].blocksRaycasts = false;
            }
            if (index < amounts.Length && amounts[index] != null)
            {
                int target = plan.Items[index].Amount;
                // Multiplication stays in double, and the exact final int is assigned explicitly.
                // This handles int.MaxValue without float rounding to 2,147,483,648.
                int amount = progress >= 1d ? target : (int)Math.Floor(target * Math.Max(0d, Math.Min(1d, progress)));
                amounts[index].text = "+" + amount.ToString("N0");
            }
        }

        private void UpdateCopy()
        {
            if (IsComplete)
            {
                Phase = SettlementChestSequencePhase.Complete;
                SetCopy(plan.IsFailure ? "物资已清点" : "收获已清点", "查看本次航海一览");
            }
            else if (Elapsed >= rewardsEnd)
            {
                Phase = SettlementChestSequencePhase.Closing;
                SetCopy(plan.IsFailure ? "带回希望，再次启航" : plan.IsJackpot ? "满载而归！" : "收获已清点", "这一程的收获，已为你记下");
            }
            else if (plan.IsJackpot && Elapsed >= JackpotStart)
            {
                bool charging = Elapsed < JackpotStart + JackpotChargeDuration;
                Phase = charging ? SettlementChestSequencePhase.Anticipation : SettlementChestSequencePhase.Jackpot;
                SetCopy(charging ? "箱底，还有一份重礼……" : "满载而归！", charging ? "压轴收获，即将揭晓" : "本航程最大收获 · " + plan.Items[plan.Items.Count - 1].Name);
            }
            else if (Elapsed < OpeningDuration)
            {
                Phase = SettlementChestSequencePhase.Opening;
                SetCopy(plan.IsFailure ? "清点归航物资" : "归航，开箱！", plan.IsFailure ? "清点这一程保住的物资" : "打开宝箱，看看这一程的收获");
            }
            else
            {
                Phase = SettlementChestSequencePhase.Rewards;
                int current = Mathf.Min(ordinaryCount - 1, Mathf.FloorToInt((Elapsed - OpeningDuration) / ordinaryDuration));
                SetCopy(plan.IsFailure ? "保住的物资" : "收获逐项揭晓", current >= 0 ?
                    "第 " + (current + 1) + " / " + plan.Items.Count + " 项 · " + plan.Items[current].Name : string.Empty);
            }
        }

        private void SetCopy(string heading, string detail)
        {
            if (title != null) title.text = heading;
            if (status != null) status.text = detail;
        }

        private void BuildSoundTimeline()
        {
            soundTimes.Clear(); soundCues.Clear(); soundVolumes.Clear(); soundCursor = 0;
            if (!plan.HasRewards) return;
            AddCue(.16f, plan.IsFailure ? "settlement_failure" : "harbor_arrive", .45f);
            if (plan.IsFailure) return;
            for (int i = 0; i < ordinaryCount; i++)
                AddCue(OpeningDuration + i * ordinaryDuration + .08f, "settlement_reward", .34f);
            if (plan.IsJackpot)
            {
                AddCue(JackpotStart + JackpotChargeDuration + .10f, "settlement_reward", .42f);
                AddCue(FireworksStart, "settlement_treasure", .60f);
                AddCue(FireworksStart + 1.2f, "settlement_reward", .38f);
            }
        }

        private void AddCue(float when, string cue, float volume)
        {
            soundTimes.Add(when); soundCues.Add(cue); soundVolumes.Add(volume);
        }

        private void AdvanceAudio(float previous, bool emitSound)
        {
            if (Elapsed < previous) soundCursor = 0;
            int last = -1;
            while (soundCursor < soundTimes.Count && soundTimes[soundCursor] <= Elapsed) last = soundCursor++;
            // Scrubbing, skipping and long frames consume cues without playing a backlog.
            if (emitSound && !IsComplete && Elapsed > previous && last >= 0 &&
                Elapsed - soundTimes[last] <= .18f && soundTimes[last] >= previous)
                playSound?.Invoke(soundCues[last], soundVolumes[last]);
        }

        private void DeliverCompletion()
        {
            if (!allowCompletion || completionDelivered) return;
            completionDelivered = true;
            completed?.Invoke();
        }

        private static float Smooth(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static float OutCubic(float value)
        {
            float t = 1f - Mathf.Clamp01(value);
            return 1f - t * t * t;
        }

        private void OnDestroy()
        {
            if (skip != null) skip.onClick.RemoveListener(CompleteImmediately);
        }
    }
}
