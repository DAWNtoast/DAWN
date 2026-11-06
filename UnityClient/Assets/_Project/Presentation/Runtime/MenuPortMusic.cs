using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Transfers the playing music and its fractional loop position between arrangements.</summary>
    public static class MenuPortMusic
    {
        public const string Key = "captains_vow_main_menu";
        public const string ResourcePath = "Audio/Music/" + Key;
        internal const float CrossfadeSeconds = 1.8f;
        private static int rememberedSample;
        private static int rememberedLength;
        private static AudioSource carriedSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlayback()
        {
            rememberedSample = rememberedLength = 0;
            carriedSource = null;
        }

        public static void RememberPlayback(AudioSource source)
        {
            if (source == null || !source.isPlaying || source.clip == null || source.clip.samples <= 0) return;
            rememberedSample = source.timeSamples;
            rememberedLength = source.clip.samples;
        }

        public static void RestorePlayback(AudioSource source)
        {
            if (source == null || source.clip == null || source.clip.samples <= 0 || rememberedLength <= 0) return;
            int target = (int)((long)rememberedSample * source.clip.samples / rememberedLength);
            source.timeSamples = Mathf.Clamp(target, 0, source.clip.samples - 1);
        }

        internal static AudioSource LouderPlayingSource(AudioSource first, AudioSource second)
        {
            if (first == null || !first.isPlaying) return second != null && second.isPlaying ? second : null;
            return second != null && second.isPlaying && second.volume > first.volume ? second : first;
        }

        internal static void CarryAcrossScene(AudioSource source)
        {
            if (source == null || !source.isPlaying) return;
            if (carriedSource != null && carriedSource != source)
                Object.Destroy(carriedSource.gameObject);
            RememberPlayback(source);
            source.transform.SetParent(null, false);
            Object.DontDestroyOnLoad(source.gameObject);
            carriedSource = source;
        }

        internal static AudioSource TakeOutgoing(Transform parent)
        {
            AudioSource source = carriedSource;
            carriedSource = null;
            if (source == null) return null;
            // Parenting adopts the voice even during the destination's Awake/OnEnable,
            // before Unity marks that scene as loaded.
            source.transform.SetParent(parent, false);
            return source;
        }
    }
}
