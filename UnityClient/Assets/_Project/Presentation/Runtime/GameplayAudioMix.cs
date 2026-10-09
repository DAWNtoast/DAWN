using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Gain and polyphony limits shared by the runtime mixer and its tests.</summary>
    public static class GameplayAudioMix
    {
        public const int EffectVoiceCount = 8;
        public const int RegularEffectVoiceCount = 6;
        public const float MusicGain = 0.85f;
        public const float AmbienceGain = 0.20f;
        public const float EffectsGain = 0.8f;
        public const float EffectsAttenuationSeconds = 0.025f;
        public const float EffectsRecoverySeconds = 0.20f;

        public static bool IsClosingCue(string key) => key == "battle_victory" || key == "battle_sunk" ||
            key == "settlement_failure" || key == "settlement_treasure" || key == "harbor_arrive";

        public static float RetriggerSeconds(string key)
        {
            // Navigation completion and the settlement reveal describe the same arrival.
            if (key == "harbor_arrive") return 1.8f;
            if (key == "settlement_reward") return 0.5f;
            if (IsClosingCue(key)) return 0.5f;
            return key == "course_plot" ? 0.14f : 0.1f;
        }

        public static int SimultaneousCueLimit(string key) => IsClosingCue(key) ? 1 : 2;

        // Authored clips retain their dynamics. Extra voices share energy instead of
        // pushing the mix louder with every overlapping PlayOneShot invocation.
        public static float EffectMixScale(int activeVoiceCount) => activeVoiceCount <= 0 ? 0f :
            EffectsGain / Mathf.Sqrt(activeVoiceCount);
    }
}
