using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>Exercises real AudioSources without activating gameplay or writing a player profile.</summary>
    public sealed partial class GameplayAudioMixTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly string[] volumeKeys =
        {
            MainMenuController.MasterVolumeKey, MainMenuController.MusicVolumeKey, MainMenuController.EffectsVolumeKey,
        };
        private readonly Dictionary<string, float> savedVolumes = new Dictionary<string, float>();
        private GameObject host, audioRoot, listenerHost;
        private CoreGameplayDemoController controller;
        private AudioClip sustainedTestClip;
        private AudioClip shortTestClip;
        private float savedListenerVolume;
        private bool savedListenerPause;
        private readonly List<AudioSource> pausedExternalMenuThemes = new List<AudioSource>();
        private int savedMenuThemeSample, savedMenuThemeLength;
        private bool isolatedMenuThemePlayback;

        [SetUp]
        public void CreateIsolatedAudio()
        {
            savedVolumes.Clear();
            foreach (string key in volumeKeys)
            {
                if (PlayerPrefs.HasKey(key)) savedVolumes[key] = PlayerPrefs.GetFloat(key);
                PlayerPrefs.SetFloat(key, 1f);
            }
            savedListenerVolume = AudioListener.volume;
            savedListenerPause = AudioListener.pause;
            try
            {
                IsolateMenuThemePlayback();
                AudioListener.pause = false;
                host = new GameObject("Inactive Audio Test Controller");
                host.SetActive(false);
                controller = host.AddComponent<CoreGameplayDemoController>();
                Assert.That(controller.Session, Is.Null, "The fixture must never activate gameplay Awake or profile persistence.");
                SetField("session", new CoreVoyageSession());
                audioRoot = new GameObject("Isolated Runtime Audio Sources");
                if (Object.FindFirstObjectByType<AudioListener>() == null)
                    listenerHost = new GameObject("Isolated Test Audio Listener", typeof(AudioListener));
                Invoke("InitializeAudioSources", audioRoot.transform);
                // A quiet, long cue makes voice occupancy independent of asset duration changes.
                sustainedTestClip = AudioClip.Create("Audio test sustained cue", 48000 * 4, 1, 48000, false);
                float[] samples = new float[sustainedTestClip.samples];
                for (int i = 0; i < samples.Length; i++) samples[i] = 0.002f * Mathf.Sin(i * (2f * Mathf.PI * 220f / 48000f));
                sustainedTestClip.SetData(samples, 0);
            }
            catch
            {
                try { DestroyIsolatedAudioObjects(); }
                finally
                {
                    try { RestoreAudioSettings(); }
                    finally { RestoreMenuThemePlayback(); }
                }
                throw;
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreAudioAndSettings()
        {
            try
            {
                DestroyIsolatedAudioObjects();
                yield return null;
            }
            finally
            {
                try { RestoreAudioSettings(); }
                finally { RestoreMenuThemePlayback(); }
            }
        }

        private void DestroyIsolatedAudioObjects()
        {
            if (audioRoot != null) Object.Destroy(audioRoot);
            if (host != null) Object.Destroy(host);
            if (listenerHost != null) Object.Destroy(listenerHost);
            if (sustainedTestClip != null) Object.Destroy(sustainedTestClip);
            if (shortTestClip != null) Object.Destroy(shortTestClip);
        }

        private void RestoreAudioSettings()
        {
            foreach (string key in volumeKeys)
            {
                if (savedVolumes.TryGetValue(key, out float volume)) PlayerPrefs.SetFloat(key, volume);
                else PlayerPrefs.DeleteKey(key);
            }
            AudioListener.volume = savedListenerVolume;
            AudioListener.pause = savedListenerPause;
        }

        private static FieldInfo MenuThemeCursorField(string name) => typeof(MenuPortMusic)
            .GetField(name, BindingFlags.Static | BindingFlags.NonPublic);

        private void IsolateMenuThemePlayback()
        {
            // Other fixtures leave their scene loaded. Its real controller still
            // updates this shared cursor, unlike a production single-scene handoff.
            // Pause only its live theme voices; do not deactivate a controller or
            // hierarchy and trigger unrelated lifecycle/profile callbacks.
            pausedExternalMenuThemes.Clear();
            savedMenuThemeSample = (int)MenuThemeCursorField("rememberedSample").GetValue(null);
            savedMenuThemeLength = (int)MenuThemeCursorField("rememberedLength").GetValue(null);
            isolatedMenuThemePlayback = true;
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (!source.isPlaying || source.clip == null ||
                    !source.clip.name.StartsWith("captains_vow_", System.StringComparison.Ordinal)) continue;
                pausedExternalMenuThemes.Add(source);
                Debug.Log("AUDIO_FIXTURE_PAUSED_EXTERNAL_THEME: owner=" + source.name +
                    ", samples=" + source.timeSamples + ", clipSamples=" + source.clip.samples);
                source.Pause();
            }
            AssertExternalMenuThemesPaused();
            typeof(MenuPortMusic).GetMethod("ResetPlayback", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }

        private void AssertExternalMenuThemesPaused()
        {
            foreach (AudioSource source in pausedExternalMenuThemes)
                if (source != null)
                    Assert.That(source.isPlaying, Is.False,
                        "A previous scene's music owner must stay paused while this fixture tests its own real sources.");
        }

        private void RestoreMenuThemePlayback()
        {
            if (!isolatedMenuThemePlayback) return;
            try
            {
                MenuThemeCursorField("rememberedSample").SetValue(null, savedMenuThemeSample);
                MenuThemeCursorField("rememberedLength").SetValue(null, savedMenuThemeLength);
            }
            finally
            {
                // UnPause preserves each source's own retained cursor; Play would
                // restart unrelated scene music and defeat fixture restoration.
                foreach (AudioSource source in pausedExternalMenuThemes)
                    if (source != null) source.UnPause();
                pausedExternalMenuThemes.Clear();
                isolatedMenuThemePlayback = false;
            }
        }

        [UnityTest]
        public IEnumerator InitialMusic_ActuallyPlaysAndAdvancesAfterGentleFadeIn()
        {
            AudioSource active = Field<AudioSource>("activeMusicSource");
            Assert.That(active.clip, Is.EqualTo(Resources.Load<AudioClip>("Audio/Music/" + controller.ActiveMusicKey)));
            Assert.That(active.volume, Is.Zero, "Initialization must begin at zero before raising the music bed.");
            yield return WaitForPlaying(active);
            int firstSample = active.timeSamples;
            yield return Pump(0.4f);
            Assert.That(active.timeSamples, Is.GreaterThan(firstSample), "The playback cursor must advance, not merely retain a music key.");
            Assert.That(active.volume, Is.EqualTo(GameplayAudioMix.MusicGain).Within(0.0001f));
            Assert.That(Field<AudioSource>("incomingMusicSource").isPlaying, Is.False);
            active.Stop();
            Invoke("SetMusic", controller.ActiveMusicKey, true);
            yield return WaitForPlaying(active);
            Assert.That(active.isPlaying, Is.True, "Requesting a stopped current track should recover playback.");
        }

        [UnityTest]
        public IEnumerator MusicLifecycle_ReactivationRestartsBothSidesOfAnInterruptedCrossfade()
        {
            yield return Pump(0.4f);
            AudioSource outgoing = Field<AudioSource>("activeMusicSource");
            Invoke("SetMusic", "captains_vow_sailing", false);
            AudioSource incoming = Field<AudioSource>("incomingMusicSource");
            yield return WaitForPlaying(incoming);
            yield return Pump(0.25f);
            audioRoot.SetActive(false);
            yield return null;
            Assert.That(outgoing.isPlaying || incoming.isPlaying, Is.False);
            audioRoot.SetActive(true);
            Invoke("ResumeAudioPresentation");
            yield return WaitForPlaying(outgoing);
            yield return WaitForPlaying(incoming);
            Assert.That(outgoing.isPlaying && incoming.isPlaying, Is.True,
                "A retained music key must not suppress playback recovery after hierarchy suspension.");
            yield return Pump(1.8f);
            Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(incoming));
            Assert.That(incoming.isPlaying, Is.True);
            Assert.That(outgoing.isPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator MusicTransition_HasComplementaryGainsAndReleasesOutgoingSource()
        {
            yield return Pump(0.4f);
            AudioSource outgoing = Field<AudioSource>("activeMusicSource");
            Invoke("SetMusic", "captains_vow_sailing", false);
            AudioSource incoming = Field<AudioSource>("incomingMusicSource");
            Assert.That(incoming.volume, Is.Zero);
            yield return WaitForPlaying(incoming);
            yield return Pump(0.35f);
            Assert.That(outgoing.isPlaying && incoming.isPlaying, Is.True);
            Assert.That(outgoing.volume, Is.InRange(0.001f, GameplayAudioMix.MusicGain - 0.001f));
            Assert.That(incoming.volume, Is.InRange(0.001f, GameplayAudioMix.MusicGain - 0.001f));
            Assert.That(outgoing.volume + incoming.volume, Is.EqualTo(GameplayAudioMix.MusicGain).Within(0.0001f));
            yield return Pump(1.6f);
            Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(incoming));
            Assert.That(incoming.isPlaying, Is.True);
            Assert.That(outgoing.isPlaying, Is.False);
            Assert.That(outgoing.clip, Is.Null);
        }

        [UnityTest]
        public IEnumerator ImportedBattleCue_ActuallyPlaysAndAdvancesOnTheDedicatedPoolMember()
        {
            SetField("battleAnimationPlaying", true);
            Invoke("PlaySfx", "battle_card_play", 0.5f);
            AudioSource source = Field<AudioSource>("battleSfxSource");
            Assert.That(source.clip, Is.SameAs(Resources.Load<AudioClip>("Audio/BattleSfx/battle_card_play")));
            yield return WaitForPlaying(source);
            int firstSample = source.timeSamples;
            yield return Pump(0.06f);
            Assert.That(source.isPlaying, Is.True);
            Assert.That(source.timeSamples, Is.GreaterThan(firstSample));
            Assert.That(source.volume, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator VolumeSettings_MasterIsAppliedOnceAndEffectsMuteExistingTails()
        {
            yield return Pump(0.8f);
            CacheCue("worldSfxClipCache", "sail_step");
            Invoke("PlaySfx", "sail_step", 0.5f);
            AudioSource effect = Field<AudioSource>("worldSfxSource");
            yield return WaitForPlaying(effect);
            AudioSource music = Field<AudioSource>("activeMusicSource");
            AudioSource ambience = Field<AudioSource>("ambienceSource");
            float originalMusic = music.volume, originalEffect = effect.volume, originalAmbience = ambience.volume;
            PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, 0.5f);
            Invoke("RefreshMusicVolume");
            Assert.That(AudioListener.volume, Is.EqualTo(0.5f));
            Assert.That(music.volume, Is.EqualTo(originalMusic).Within(0.0001f));
            Assert.That(effect.volume, Is.EqualTo(originalEffect).Within(0.0001f));
            Assert.That(ambience.volume, Is.EqualTo(originalAmbience).Within(0.0001f), "Master must not be multiplied into category sources again.");
            PlayerPrefs.SetFloat(MainMenuController.MusicVolumeKey, 0.5f);
            PlayerPrefs.SetFloat(MainMenuController.EffectsVolumeKey, 0.5f);
            Invoke("RefreshMusicVolume");
            Assert.That(music.volume, Is.EqualTo(originalMusic * 0.5f).Within(0.0001f));
            Assert.That(effect.volume, Is.EqualTo(originalEffect * 0.5f).Within(0.0001f));
            Assert.That(ambience.volume, Is.EqualTo(originalAmbience * 0.5f).Within(0.0001f));
            PlayerPrefs.SetFloat(MainMenuController.EffectsVolumeKey, 0f);
            Invoke("RefreshMusicVolume");
            Assert.That(effect.isPlaying, Is.True, "The tail should be retained while its category gain changes.");
            Assert.That(effect.volume, Is.Zero);
            Assert.That(ambience.volume, Is.Zero);
            Assert.That(music.volume, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator RepeatedCues_AreDeduplicatedWithoutRestartingOrTruncatingTheFirstVoice()
        {
            CacheCue("interfaceSfxClipCache", "course_plot");
            for (int i = 0; i < 20; i++) Invoke("PlaySfx", "course_plot", 0.4f);
            AudioSource first = Field<AudioSource>("interfaceSfxSource");
            yield return WaitForPlaying(first);
            Assert.That(PlayingEffects().Length, Is.EqualTo(1));
            int firstSample = first.timeSamples;
            yield return Pump(0.18f);
            Invoke("PlaySfx", "course_plot", 0.4f);
            Assert.That(PlayingEffects().Length, Is.EqualTo(2), "A later deliberate click remains audible.");
            Assert.That(first.timeSamples, Is.GreaterThan(firstSample));
            yield return Pump(0.18f);
            Invoke("PlaySfx", "course_plot", 0.4f);
            Assert.That(PlayingEffects().Length, Is.EqualTo(2), "One repeatedly clicked cue may not occupy the whole pool.");
            CacheCue("worldSfxClipCache", "harbor_arrive");
            Invoke("PlaySfx", "harbor_arrive", 0.5f);
            yield return Pump(0.25f);
            Invoke("PlaySfx", "harbor_arrive", 0.5f);
            Assert.That(PlayingEffects().Length, Is.EqualTo(3), "Navigation and reveal arrival cues must share one audible voice.");
        }

        [UnityTest]
        public IEnumerator VoiceBudget_ReservesOutcomesAndKeepsBattleTailsAfterThePhaseChanges()
        {
            SetField("battleAnimationPlaying", true);
            string[] regular = { "battle_card_play", "battle_enemy_intent", "battle_maneuver", "battle_cannon_fire", "battle_impact", "battle_guard" };
            foreach (string key in regular)
            {
                CacheCue("battleSfxClipCache", key);
                Invoke("PlaySfx", key, 0.6f);
            }
            AudioSource[] original = PlayingEffects();
            Assert.That(original.Length, Is.EqualTo(6));
            CacheCue("battleSfxClipCache", "battle_repair");
            Invoke("PlaySfx", "battle_repair", 0.6f);
            Assert.That(PlayingEffects().Length, Is.EqualTo(6), "Regular cues may not consume reserved outcome voices.");
            foreach (string key in new[] { "battle_victory", "battle_sunk" })
            {
                CacheCue("battleSfxClipCache", key);
                Invoke("PlaySfx", key, 0.6f);
            }
            Assert.That(PlayingEffects().Length, Is.EqualTo(GameplayAudioMix.EffectVoiceCount));
            Assert.That(original.All(source => source.isPlaying), Is.True, "Critical cues must not steal or cut earlier impacts.");
            yield return Pump(0.06f);
            Assert.That(PlayingEffects().Sum(source => source.volume * source.volume),
                Is.EqualTo(0.6f * 0.6f * GameplayAudioMix.EffectsGain * GameplayAudioMix.EffectsGain).Within(0.0001f),
                "The same gain across many cues must share energy rather than multiply it.");
            SetField("battleAnimationPlaying", false);
            yield return Pump(0.1f);
            Assert.That(PlayingEffects().Length, Is.EqualTo(8), "Leaving battle presentation must retain scheduled tails.");
            Assert.That(audioRoot.GetComponentsInChildren<AudioSource>().Count(source => !source.loop), Is.EqualTo(8));
        }

        [UnityTest]
        public IEnumerator SharedGainEnvelope_SoftensTailChangesAndKeepsMuteAndRestoreImmediate()
        {
            SetField("battleAnimationPlaying", true);
            CacheCue("battleSfxClipCache", "battle_impact");
            Invoke("PlaySfx", "battle_impact", 0.5f);
            AudioSource tail = Field<AudioSource>("battleSfxSource");
            yield return WaitForPlaying(tail);
            float singleVoiceVolume = tail.volume;
            shortTestClip = AudioClip.Create("Short overlap for gain envelope", 48000 / 5, 1, 48000, false);
            Field<Dictionary<string, AudioClip>>("battleSfxClipCache")["battle_card_play"] = shortTestClip;
            Field<Dictionary<string, AudioClip>>("battleSfxClipCache")["battle_guard"] = shortTestClip;
            Invoke("PlaySfx", "battle_card_play", 0.5f);
            Invoke("PlaySfx", "battle_guard", 0.5f);
            Assert.That(tail.volume, Is.EqualTo(singleVoiceVolume).Within(0.0001f),
                "Multiple Play calls in one frame must not advance or reset the shared gain envelope.");

            // Drive the same update subroutine with known steps so this assertion is
            // independent of a batch runner's variable frame rate. Playback remains real.
            Invoke("AdvanceEffectMixEnvelope", 0.01f);
            Invoke("ApplyEffectSourceVolumes");
            float settledCrowdedVolume = 0.5f * GameplayAudioMix.EffectsGain / Mathf.Sqrt(3f);
            Assert.That(tail.volume, Is.GreaterThan(settledCrowdedVolume).And.LessThan(singleVoiceVolume),
                "An existing tail must attenuate through intermediate gains, not jump to the new voice-count gain.");
            Invoke("AdvanceEffectMixEnvelope", 0.015f);
            Invoke("ApplyEffectSourceVolumes");
            Assert.That(tail.volume, Is.EqualTo(settledCrowdedVolume).Within(0.0001f));

            int tailSample = tail.timeSamples;
            yield return new WaitForSecondsRealtime(0.25f); // Short overlaps finish naturally; do not stop any voice.
            Invoke("AdvanceEffectMixEnvelope", 0.05f);
            Invoke("ApplyEffectSourceVolumes");
            Assert.That(tail.volume, Is.GreaterThan(settledCrowdedVolume).And.LessThan(singleVoiceVolume),
                "The remaining tail must recover gradually after other cues finish.");
            float recoveringVolume = tail.volume;
            PlayerPrefs.SetFloat(MainMenuController.EffectsVolumeKey, 0f);
            PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, 0f);
            Invoke("RefreshMusicVolume");
            Assert.That(tail.volume, Is.Zero);
            Assert.That(AudioListener.volume, Is.Zero);
            Invoke("AdvanceEffectMixEnvelope", 0.05f);
            PlayerPrefs.SetFloat(MainMenuController.EffectsVolumeKey, 1f);
            PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, 1f);
            Invoke("RefreshMusicVolume");
            Assert.That(tail.volume, Is.GreaterThan(recoveringVolume).And.LessThan(singleVoiceVolume),
                "Unmuting must use the continuing envelope instead of resetting or leaving the cue silent.");
            Assert.That(AudioListener.volume, Is.EqualTo(1f));
            Invoke("AdvanceEffectMixEnvelope", 0.1f);
            Invoke("ApplyEffectSourceVolumes");
            Assert.That(tail.volume, Is.EqualTo(singleVoiceVolume).Within(0.0001f), "Recovery should settle after 200 ms.");
            Assert.That(tail.isPlaying, Is.True);
            Assert.That(tail.timeSamples, Is.GreaterThan(tailSample), "The gain envelope must not restart the underlying tail.");
        }

        [UnityTest]
        public IEnumerator AmbienceTransition_FadesBothSourcesAndFadesOutBeforeStopping()
        {
            yield return Pump(0.8f);
            AudioSource outgoing = Field<AudioSource>("ambienceSource");
            Assert.That(outgoing.isPlaying, Is.True);
            SetField("activePortFacility", 2);
            yield return Pump(0.2f);
            AudioSource incoming = Field<AudioSource>("incomingAmbienceSource");
            Assert.That(outgoing.isPlaying && incoming.isPlaying, Is.True);
            Assert.That(outgoing.volume, Is.InRange(0.001f, GameplayAudioMix.AmbienceGain - 0.001f));
            Assert.That(incoming.volume, Is.InRange(0.001f, GameplayAudioMix.AmbienceGain - 0.001f));
            Assert.That(outgoing.volume + incoming.volume, Is.EqualTo(GameplayAudioMix.AmbienceGain).Within(0.0001f));
            yield return Pump(0.6f);
            Assert.That(outgoing.isPlaying, Is.False);
            Assert.That(incoming.isPlaying, Is.True);
            SetField("session", null); // No active world requires a fade to silence.
            yield return Pump(0.2f);
            Assert.That(incoming.isPlaying, Is.True);
            Assert.That(incoming.volume, Is.InRange(0.001f, GameplayAudioMix.AmbienceGain - 0.001f));
            yield return Pump(0.6f);
            Assert.That(incoming.isPlaying, Is.False);
            Assert.That(Field<AudioSource>("ambienceSource").isPlaying, Is.False);
            Assert.That(Field<AudioSource>("incomingAmbienceSource").isPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator QueuedOutcome_WaitsForANaturalTailWithoutExceedingTheBudget()
        {
            string[] regular = { "sail_step", "sail_depart", "encounter_enter", "investment_build", "curse_purify", "milestone_unlock" };
            foreach (string key in regular)
            {
                CacheCue("worldSfxClipCache", key);
                Invoke("PlaySfx", key, 0.5f);
            }
            shortTestClip = AudioClip.Create("Short arrival tail", 48000 / 3, 1, 48000, false);
            Field<Dictionary<string, AudioClip>>("worldSfxClipCache")["harbor_arrive"] = shortTestClip;
            Invoke("PlaySfx", "harbor_arrive", 0.5f);
            CacheCue("worldSfxClipCache", "settlement_failure");
            Invoke("PlaySfx", "settlement_failure", 0.5f);
            CacheCue("worldSfxClipCache", "settlement_treasure");
            Invoke("PlaySfx", "settlement_treasure", 0.5f);
            Assert.That(PlayingEffects().Length, Is.EqualTo(8));
            Assert.That(Field<ICollection>("pendingClosingEffects").Count, Is.EqualTo(1));
            AudioSource shortVoice = PlayingEffects().Single(source => source.clip == shortTestClip);
            yield return Pump(0.45f);
            Assert.That(Field<ICollection>("pendingClosingEffects").Count, Is.Zero);
            Assert.That(shortVoice.isPlaying, Is.True);
            Assert.That(shortVoice.clip, Is.SameAs(sustainedTestClip), "The next outcome may reuse the slot only after the prior tail ends.");
            Assert.That(PlayingEffects().Length, Is.EqualTo(8));
        }

        [Test]
        public void SettlementPhaseObserver_DoesNotInventAnArrivalBeforeTheOutcomeReveal()
        {
            Invoke("UpdateWorldPhaseCue");
            typeof(CoreVoyageSession).GetProperty("Phase").GetSetMethod(true).Invoke(controller.Session,
                new object[] { CoreDemoPhase.Settlement });
            Invoke("UpdateWorldPhaseCue");
            Assert.That(Field<Dictionary<string, AudioClip>>("worldSfxClipCache").ContainsKey("harbor_arrive"), Is.False,
                "Failure and non-arrival settlements must not receive a generic harbor cue.");
            Assert.That(PlayingEffects(), Is.Empty);
        }

        private IEnumerator Pump(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                AssertExternalMenuThemesPaused();
                Invoke("UpdateMusicCrossfade");
                Invoke("UpdateAmbience");
                Invoke("UpdateEffectVoices");
            }
        }

        private IEnumerator WaitForPlaying(AudioSource source)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!source.isPlaying && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                AssertExternalMenuThemesPaused();
            }
            AssertExternalMenuThemesPaused();
            Assert.That(source.isPlaying, Is.True, "AudioSource must be playing the actual assigned clip.");
        }

        private AudioSource[] PlayingEffects() => audioRoot.GetComponentsInChildren<AudioSource>()
            .Where(source => !source.loop && source.isPlaying).ToArray();
        private void CacheCue(string cache, string key) => Field<Dictionary<string, AudioClip>>(cache)[key] = sustainedTestClip;
        private T Field<T>(string name) => (T)typeof(CoreGameplayDemoController).GetField(name, PrivateInstance).GetValue(controller);
        private void SetField(string name, object value) => typeof(CoreGameplayDemoController).GetField(name, PrivateInstance).SetValue(controller, value);
        private void Invoke(string name, params object[] args) => typeof(CoreGameplayDemoController).GetMethod(name, PrivateInstance).Invoke(controller, args);
    }

    public sealed class GameplayFacilityAmbienceTests
    {
        [TestCase(PortFacilityArtCatalog.TreasureFacilityIndex, "treasure_workbench_ambience")]
        [TestCase(PortFacilityArtCatalog.MaintenanceFacilityIndex, "market_ambience")]
        [TestCase(PortFacilityArtCatalog.VoyageFacilityIndex, "harbor_ambience")]
        [TestCase(PortFacilityArtCatalog.MarketFacilityIndex, "market_ambience")]
        public void PortFacility_ResolvesItsExpectedAmbienceWithoutInitializingAudio(int facilityIndex, string expectedKey)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var host = new GameObject("Inactive Facility Ambience Test Controller");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                Assert.That(controller.Session, Is.Null);
                typeof(CoreGameplayDemoController).GetField("session", flags).SetValue(controller, new CoreVoyageSession());
                typeof(CoreGameplayDemoController).GetField("activePortFacility", flags).SetValue(controller, facilityIndex);

                string ambience = (string)typeof(CoreGameplayDemoController)
                    .GetMethod("ResolveDesiredAmbienceKey", flags).Invoke(controller, null);

                Assert.That(ambience, Is.EqualTo(expectedKey));
                Assert.That(host.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
