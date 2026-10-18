using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class GameplayAudioMixTests
    {
        [UnityTest]
        public IEnumerator RuntimeMemory_AudioCacheReleasePreservesMusicAndAmbienceCrossfades()
        {
            yield return Pump(.8f);
            // Prime genuinely unused imported clips as well as the live beds.
            foreach (string key in CoreGameplayDemoController.AmbienceSoundEffectKeys) Invoke("LoadAmbience", key);
            Invoke("LoadMusic", "port_gray_gull");
            Invoke("LoadBattleSfx", "battle_card_play");
            Invoke("LoadWorldSfx", "sail_step");
            Invoke("LoadInterfaceSfx", "course_plot");
            Invoke("SetMusic", "route_golden_tide_trade", false);
            SetField("activePortFacility", 2);
            yield return Pump(.2f);
            AudioSource[] live = { Field<AudioSource>("activeMusicSource"), Field<AudioSource>("incomingMusicSource"),
                Field<AudioSource>("ambienceSource"), Field<AudioSource>("incomingAmbienceSource") };
            Assert.That(live.All(source => source.clip != null && source.isPlaying), Is.True);
            int[] clips = live.Select(source => source.clip.GetInstanceID()).ToArray();
            int[] cursors = live.Select(source => source.timeSamples).ToArray();
            Invoke("ReleaseAudioClipReferences");
            AssertAudioMemoryCachesEmpty();
            yield return Resources.UnloadUnusedAssets();
            yield return new WaitForSecondsRealtime(.05f);
            for (int i = 0; i < live.Length; i++)
            {
                Assert.That(live[i].clip, Is.Not.Null);
                Assert.That(live[i].clip.GetInstanceID(), Is.EqualTo(clips[i]));
                Assert.That(live[i].isPlaying, Is.True, "Cache retirement must not stop either side of a fade.");
                Assert.That(live[i].timeSamples, Is.Not.EqualTo(cursors[i]), "Playback must continue during the resource scan.");
            }
            yield return Pump(1.9f);
            Assert.That(live[0].clip, Is.Null);
            Assert.That(live[2].clip, Is.Null);
            Assert.That(live[1].isPlaying && live[3].isPlaying, Is.True);
            Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(live[1]));
            Assert.That(Field<AudioSource>("ambienceSource"), Is.SameAs(live[3]));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_AudioCacheReleasePreservesQueuedOutcomeAndPlayingTails()
        {
            foreach (string key in new[] { "sail_step", "sail_depart", "encounter_enter", "investment_build", "curse_purify", "milestone_unlock" })
            {
                CacheCue("worldSfxClipCache", key);
                Invoke("PlaySfx", key, .5f);
            }
            shortTestClip = AudioClip.Create("Memory regression short arrival tail", 48000 / 3, 1, 48000, false);
            Field<Dictionary<string, AudioClip>>("worldSfxClipCache")["harbor_arrive"] = shortTestClip;
            Invoke("PlaySfx", "harbor_arrive", .5f);
            CacheCue("worldSfxClipCache", "settlement_failure");
            Invoke("PlaySfx", "settlement_failure", .5f);
            // This imported cue is held only by the pending queue after cache
            // release; the test retains its ID, never an AudioClip reference.
            Invoke("PlaySfx", "settlement_treasure", .5f);
            int pendingId = Field<Dictionary<string, AudioClip>>("worldSfxClipCache")["settlement_treasure"].GetInstanceID();
            Assert.That(Field<ICollection>("pendingClosingEffects").Count, Is.EqualTo(1));
            AudioSource shortVoice = PlayingEffects().Single(source => source.clip == shortTestClip);
            AudioSource[] sustained = PlayingEffects().Where(source => source != shortVoice).ToArray();
            Assert.That(sustained.Length, Is.EqualTo(7));
            Invoke("ReleaseAudioClipReferences");
            AssertAudioMemoryCachesEmpty();
            yield return Resources.UnloadUnusedAssets();
            Assert.That(sustained.All(source => source.clip != null && source.isPlaying), Is.True,
                "Releasing cache references must not truncate any of the occupied voices.");
            yield return Pump(.45f);
            Assert.That(Field<ICollection>("pendingClosingEffects").Count, Is.Zero);
            Assert.That(shortVoice.clip, Is.Not.Null);
            Assert.That(shortVoice.clip.GetInstanceID(), Is.EqualTo(pendingId));
            Assert.That(shortVoice.isPlaying, Is.True, "The queued imported outcome must still start after the previous natural tail.");
            LogAssert.NoUnexpectedReceived();
        }

        private void AssertAudioMemoryCachesEmpty()
        {
            foreach (string name in new[] { "musicClipCache", "ambienceClipCache", "battleSfxClipCache", "worldSfxClipCache", "interfaceSfxClipCache" })
                Assert.That(Field<Dictionary<string, AudioClip>>(name), Is.Empty, name);
        }
    }
}
