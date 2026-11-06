using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ArrangedMusic_RealSceneLoadsCarryBothDirections()
        {
            using (new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                SceneManager.LoadScene("MainMenu");
                yield return null;
                yield return null;
                yield return new WaitForSecondsRealtime(.4f);
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                var original = menu.GetComponentsInChildren<AudioSource>()
                    .Single(source => source.clip != null && source.clip.name == "captains_vow_main_menu");
                yield return VerifyLiveSceneMusicHandoff(original, 95, MainMenuController.MainSceneName, "captains_vow_broken_anchor",
                    () => typeof(MainMenuController).GetMethod("LoadGameplayWithMusic", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, null));

                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var port = game.GetComponentsInChildren<AudioSource>()
                    .Single(source => source.clip != null && source.clip.name == "captains_vow_broken_anchor");
                yield return VerifyLiveSceneMusicHandoff(port, 133, "MainMenu", "captains_vow_main_menu",
                    () => typeof(CoreGameplayDemoController).GetMethod("LoadMainMenuWithMusic", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(game, null));
            }
        }

        private static IEnumerator VerifyLiveSceneMusicHandoff(AudioSource outgoing, int seconds, string destination, string key, System.Action load)
        {
            Assert.That(outgoing.isPlaying, Is.True);
            int seek = seconds * outgoing.clip.frequency;
            outgoing.timeSamples = seek;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(outgoing.timeSamples, Is.GreaterThanOrEqualTo(seek - 2048));

            AudioSource incoming = null;
            bool liveOverlap = false;
            double driftSeconds = double.MaxValue;
            // Observe after the new scene's Awake/OnEnable but before its first Update can advance the fade.
            void OnLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name != destination) return;
                incoming = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).FirstOrDefault(source =>
                    source.gameObject.scene == scene && source.clip != null && source.clip.name == key);
                liveOverlap = incoming != null && incoming.isPlaying && outgoing != null && outgoing.isPlaying &&
                    outgoing.gameObject.scene == scene && outgoing.timeSamples >= seek - 2048;
                if (!liveOverlap) return;
                double mapped = (double)outgoing.timeSamples / outgoing.clip.samples * incoming.clip.samples;
                driftSeconds = System.Math.Abs(incoming.timeSamples - mapped) / incoming.clip.frequency;
            }

            SceneManager.sceneLoaded += OnLoaded;
            try
            {
                load();
                yield return new WaitUntil(() => SceneManager.GetActiveScene().name == destination);
                Assert.That(liveOverlap, Is.True, "Both arrangements must play under the destination scene following the real scene load.");
                Assert.That(driftSeconds, Is.LessThan(.3d), "The newly loaded scene must begin near the live outgoing fraction.");
                yield return new WaitUntil(() => outgoing == null || !outgoing.isPlaying);
                var voices = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(source =>
                    source.isPlaying && source.clip != null && source.clip.name.StartsWith("captains_vow_", System.StringComparison.Ordinal)).ToArray();
                Assert.That(voices, Is.EquivalentTo(new[] { incoming }));
                Assert.That(incoming.gameObject.scene, Is.EqualTo(SceneManager.GetActiveScene()));
            }
            finally
            {
                SceneManager.sceneLoaded -= OnLoaded;
                if (outgoing != null && outgoing.transform.parent == null)
                    Object.DestroyImmediate(outgoing.gameObject);
            }
        }
    }

    public sealed partial class GameplayAudioMixTests
    {
        [TestCase("断锚港", "broken_anchor")]
        [TestCase("白贝港", "white_shell")]
        [TestCase("铜钟港", "bronze_bell")]
        [TestCase("盐冠港", "salt_crown")]
        [TestCase("暮潮港", "twilight_tide")]
        [TestCase("灰鸥港", "gray_gull")]
        public void EveryPort_ResolvesItsImportedArrangement(string port, string arrangement)
        {
            string key = "captains_vow_" + arrangement;
            typeof(CoreVoyageSession).GetProperty("CurrentPort").SetValue(controller.Session, port);
            Assert.That(CoreGameplayDemoController.PortMusicKey(port), Is.EqualTo(key));
            Invoke("Update");
            Assert.That(controller.ActiveMusicKey, Is.EqualTo(key));
            Assert.That(Resources.Load<AudioClip>("Audio/Music/" + key), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SwitchingPorts_MapsElapsedFractionAndSamePortDoesNotRestart()
        {
            AudioSource active = Field<AudioSource>("activeMusicSource");
            yield return WaitForPlaying(active);
            yield return Pump(0.4f);
            double seekStarted = SeekThemeInsideNonWrappingInterval(active, 120);
            yield return Pump(0.1f);
            AssertThemePlaybackContinues(active, active.clip.frequency * 120, seekStarted);

            int before = active.timeSamples;
            int sourceLength = active.clip.samples;
            double switchedAt = AudioSettings.dspTime;
            typeof(CoreVoyageSession).GetProperty("CurrentPort").SetValue(controller.Session, "白贝港");
            Invoke("Update");
            AudioSource whiteShell = Field<AudioSource>("incomingMusicSource");
            Assert.That(whiteShell.clip.name, Is.EqualTo("captains_vow_white_shell"));
            yield return WaitForPlaying(whiteShell);
            Assert.That(active.isPlaying, Is.True);
            AssertNormalizedContinuation(whiteShell, before, sourceLength, switchedAt);
            yield return Pump(1.95f);
            Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(whiteShell));
            Assert.That(active.isPlaying, Is.False);

            before = whiteShell.timeSamples;
            switchedAt = AudioSettings.dspTime;
            Invoke("SetMusic", CoreGameplayDemoController.PortMusicKey("白贝港"), false);
            AssertThemePlaybackContinues(whiteShell, before, switchedAt);
            Assert.That(Field<float>("musicCrossfade"), Is.Zero);
            Assert.That(Field<AudioSource>("incomingMusicSource").isPlaying, Is.False);
        }

        [UnityTest]
        public IEnumerator RapidPortSwitch_ThirdPortKeepsDominantVoiceAndCombinedGain()
        {
            yield return Pump(.4f);
            var port = typeof(CoreVoyageSession).GetProperty("CurrentPort");
            port.SetValue(controller.Session, "白贝港");
            Invoke("Update");
            AudioSource whiteShell = Field<AudioSource>("incomingMusicSource");
            yield return WaitForPlaying(whiteShell);
            SetField("musicCrossfade", .18f);
            Invoke("ApplyMusicSourceVolumes");
            AudioSource previous = Field<AudioSource>("activeMusicSource");
            float gain = whiteShell.volume + previous.volume;
            Assert.That(whiteShell.volume, Is.GreaterThan(gain * .9f));

            port.SetValue(controller.Session, "铜钟港");
            Invoke("Update");
            Assert.That(controller.ActiveMusicKey, Is.EqualTo("captains_vow_bronze_bell"));
            Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(whiteShell));
            Assert.That(whiteShell.clip.name, Is.EqualTo("captains_vow_white_shell"));
            Assert.That(previous.clip.name, Is.EqualTo(controller.ActiveMusicKey));
            Assert.That(whiteShell.volume + previous.volume, Is.EqualTo(gain).Within(.0001f));
            yield return Pump(1.95f);
            Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(previous));
            Assert.That(previous.isPlaying, Is.True);
            Assert.That(whiteShell.clip, Is.Null);
        }

        [UnityTest]
        public IEnumerator RapidPortSwitch_ReturnToOriginalPortReversesBothLiveVoices()
        {
            yield return Pump(.4f);
            AudioSource returning = Field<AudioSource>("activeMusicSource");
            var port = typeof(CoreVoyageSession).GetProperty("CurrentPort");
            port.SetValue(controller.Session, "白贝港");
            Invoke("Update");
            AudioSource outgoing = Field<AudioSource>("incomingMusicSource");
            yield return WaitForPlaying(outgoing);
            double seekStarted = SeekThemeInsideNonWrappingInterval(returning, 55);
            yield return Pump(.1f);
            SetField("musicCrossfade", .45f);
            Invoke("ApplyMusicSourceVolumes");
            float gain = returning.volume + outgoing.volume;
            float returningBefore = returning.volume;

            port.SetValue(controller.Session, "断锚港");
            Invoke("Update");
            Assert.That(controller.ActiveMusicKey, Is.EqualTo("captains_vow_broken_anchor"));
            Assert.That(Field<AudioSource>("incomingMusicSource"), Is.SameAs(returning));
            Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(outgoing));
            Assert.That(returning.volume, Is.GreaterThanOrEqualTo(returningBefore - .0001f));
            Assert.That(returning.volume + outgoing.volume, Is.EqualTo(gain).Within(.0001f));
            AssertThemePlaybackContinues(returning, returning.clip.frequency * 55, seekStarted);
            yield return Pump(1.95f);
            Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(returning));
            Assert.That(outgoing.clip, Is.Null);
        }

        [UnityTest]
        public IEnumerator BattleStageChange_TracksNavalAndBoardingWithoutRestartingTheTheme()
        {
            var battle = new CoreBattleState { Stage = NavalBattleStage.Naval };
            typeof(CoreVoyageSession).GetProperty("Battle").SetValue(controller.Session, battle);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(controller.Session, CoreDemoPhase.Battle);
            Invoke("Update");
            Assert.That(controller.ActiveMusicKey, Is.EqualTo("captains_vow_naval"));
            yield return Pump(1.95f);
            AudioSource naval = Field<AudioSource>("activeMusicSource");
            yield return WaitForPlaying(naval);
            double seekStarted = SeekThemeInsideNonWrappingInterval(naval, 60);
            yield return Pump(.1f);
            AssertThemePlaybackContinues(naval, naval.clip.frequency * 60, seekStarted);
            int before = naval.timeSamples;
            double switchedAt = AudioSettings.dspTime;
            battle.Stage = NavalBattleStage.Boarding;
            Invoke("Update");
            Assert.That(controller.ActiveMusicKey, Is.EqualTo("captains_vow_boarding"));
            AudioSource boarding = Field<AudioSource>("incomingMusicSource");
            yield return WaitForPlaying(boarding);
            Assert.That(boarding.clip.name, Is.EqualTo(controller.ActiveMusicKey));
            AssertNormalizedContinuation(boarding, before, naval.clip.samples, switchedAt);
        }

        [UnityTest]
        public IEnumerator PortArrangement_HierarchyReactivationKeepsItsPlaybackPosition()
        {
            AudioSource active = Field<AudioSource>("activeMusicSource");
            yield return WaitForPlaying(active);
            yield return Pump(0.4f);
            double seekStarted = SeekThemeInsideNonWrappingInterval(active, 15);
            yield return Pump(0.1f);
            int before = active.timeSamples;
            AssertThemePlaybackContinues(active, active.clip.frequency * 15, seekStarted);
            Debug.Log("PORT_THEME_DISABLE_BEFORE: time=" + active.time + ", samples=" + before);
            Assert.That(before, Is.GreaterThan(active.clip.frequency * 14), "The streamed seek must settle before testing hierarchy deactivation.");
            Invoke("RememberMenuPortMusic");
            int remembered = (int)MenuThemeCursorField("rememberedSample").GetValue(null);
            Assert.That(remembered, Is.InRange(before - 2048, before + 2048));
            double suspendedAt = AudioSettings.dspTime;
            audioRoot.SetActive(false);
            yield return null;
            AssertExternalMenuThemesPaused();
            Assert.That(active.isPlaying, Is.False);
            Invoke("RememberMenuPortMusic"); // A stopped source must not replace the last live cursor with zero.
            Assert.That((int)MenuThemeCursorField("rememberedSample").GetValue(null), Is.EqualTo(remembered));
            audioRoot.SetActive(true);
            Invoke("ResumeAudioPresentation");
            yield return WaitForPlaying(active);
            Debug.Log("PORT_THEME_ENABLE_AFTER: time=" + active.time + ", samples=" + active.timeSamples);
            AssertThemePlaybackContinues(active, before, suspendedAt);
            int resumed = active.timeSamples;
            double resumedAt = AudioSettings.dspTime;
            yield return Pump(0.1f);
            Assert.That(active.timeSamples, Is.GreaterThan(resumed));
            AssertThemePlaybackContinues(active, resumed, resumedAt);
        }

        [UnityTest]
        public IEnumerator MenuAndPort_HandOffLiveMusicAndElapsedFractionInBothDirections()
        {
            GameObject menuHost = new GameObject("Inactive Menu Audio Test Controller");
            GameObject returnHost = new GameObject("Returning Menu Audio Test Controller");
            menuHost.SetActive(false);
            returnHost.SetActive(false);
            var menu = menuHost.AddComponent<MainMenuController>();
            var returningMenu = returnHost.AddComponent<MainMenuController>();
            GameObject menuRoot = new GameObject("Isolated Menu Audio");
            GameObject returnRoot = new GameObject("Returning Menu Audio");
            Field<AudioSource>("activeMusicSource").Stop();
            AudioSource menuSource = null;
            AudioSource portSource = null;
            try
            {
                InvokeMenu(menu, "InitializeMenuAudio", menuRoot.transform);
                menuSource = (AudioSource)typeof(MainMenuController)
                    .GetField("menuMusicSource", PrivateInstance).GetValue(menu);
                yield return WaitForPlaying(menuSource);
                Assert.That(menuSource.loop, Is.True);
                Assert.That(menuSource.clip.name, Is.EqualTo("captains_vow_main_menu"));
                yield return PumpMenu(menu, .4f);
                PlayerPrefs.SetFloat(MainMenuController.MusicVolumeKey, 0.4f);
                PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, 0.35f);
                InvokeMenu(menu, "UpdateMenuAudio");
                Assert.That(menuSource.volume, Is.EqualTo(0.4f * GameplayAudioMix.MusicGain).Within(0.0001f));
                Assert.That(AudioListener.volume, Is.EqualTo(0.35f).Within(0.0001f));
                double seekStarted = SeekThemeInsideNonWrappingInterval(menuSource, 95);
                yield return PumpMenu(menu, .1f);
                AssertThemePlaybackContinues(menuSource, menuSource.clip.frequency * 95, seekStarted);
                int menuSample = menuSource.timeSamples;
                int menuLength = menuSource.clip.samples;
                double handedOffAt = AudioSettings.dspTime;
                InvokeMenu(menu, "CarryMusicToGame");
                menuRoot.SetActive(false);
                yield return null;
                InvokeMenu(menu, "StopMenuAudio");
                Invoke("InitializeAudioSources", audioRoot.transform);
                AudioSource oldMenu = Field<AudioSource>("activeMusicSource");
                Assert.That(oldMenu.isPlaying, Is.True);
                Assert.That(oldMenu.clip.name, Is.EqualTo("captains_vow_main_menu"));
                portSource = Field<AudioSource>("incomingMusicSource");
                yield return WaitForPlaying(portSource);
                Assert.That(portSource.clip.name, Is.EqualTo("captains_vow_broken_anchor"));
                AssertNormalizedContinuation(portSource, menuSample, menuLength, handedOffAt);
                yield return Pump(2f);
                Assert.That(Field<AudioSource>("activeMusicSource"), Is.SameAs(portSource));
                Assert.That(oldMenu == null || !oldMenu.isPlaying, Is.True);

                seekStarted = SeekThemeInsideNonWrappingInterval(portSource, 133);
                yield return Pump(.1f);
                AssertThemePlaybackContinues(portSource, portSource.clip.frequency * 133, seekStarted);
                int portSample = portSource.timeSamples;
                int portLength = portSource.clip.samples;
                handedOffAt = AudioSettings.dspTime;
                Invoke("CarryMusicToMenu");
                audioRoot.SetActive(false);
                yield return null;
                InvokeMenu(returningMenu, "InitializeMenuAudio", returnRoot.transform);
                AudioSource returned = (AudioSource)typeof(MainMenuController)
                    .GetField("menuMusicSource", PrivateInstance).GetValue(returningMenu);
                yield return WaitForPlaying(returned);
                Assert.That(returned.clip.name, Is.EqualTo("captains_vow_main_menu"));
                AssertNormalizedContinuation(returned, portSample, portLength, handedOffAt);
                yield return PumpMenu(returningMenu, .2f);
                var livePort = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Single(source =>
                    source.isPlaying && source.clip != null && source.clip.name == "captains_vow_broken_anchor");
                Assert.That(livePort.volume, Is.GreaterThan(0f));
                Assert.That(returned.volume, Is.InRange(.0001f, .4f * GameplayAudioMix.MusicGain - .0001f));
                yield return PumpMenu(returningMenu, 2f);
                Assert.That(livePort == null || !livePort.isPlaying, Is.True);
                Assert.That(returned.isPlaying, Is.True);
            }
            finally
            {
                // A failed assertion between departure and arrival may leave an owned voice detached.
                if (menuSource != null && menuSource.transform.parent == null) Object.Destroy(menuSource.gameObject);
                if (portSource != null && portSource.transform.parent == null) Object.Destroy(portSource.gameObject);
                Object.Destroy(menuRoot);
                Object.Destroy(menuHost);
                Object.Destroy(returnRoot);
                Object.Destroy(returnHost);
                typeof(MenuPortMusic).GetMethod("ResetPlayback", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            }
        }

        private static void InvokeMenu(MainMenuController menu, string method, params object[] args) =>
            typeof(MainMenuController).GetMethod(method, PrivateInstance).Invoke(menu, args);

        private IEnumerator PumpMenu(MainMenuController menu, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                AssertExternalMenuThemesPaused();
                InvokeMenu(menu, "UpdateMenuAudio");
            }
        }

        private static void AssertNormalizedContinuation(AudioSource target, int sourceSample, int sourceLength, double observedAt)
        {
            int mappedSample = (int)((long)sourceSample * target.clip.samples / sourceLength);
            AssertThemePlaybackContinues(target, mappedSample, observedAt);
        }

        private static double SeekThemeInsideNonWrappingInterval(AudioSource source, int seconds)
        {
            Assert.That(source.clip.name, Does.StartWith("captains_vow_"));
            Assert.That(source.loop, Is.True);
            Assert.That(source.pitch, Is.EqualTo(1f));
            Assert.That(source.clip.samples, Is.GreaterThan(source.clip.frequency * (seconds + 5)),
                "The actual imported theme must leave ample room after the probe; a clip loop cannot stand in for a restart.");
            source.timeSamples = source.clip.frequency * seconds;
            return AudioSettings.dspTime;
        }

        private static void AssertThemePlaybackContinues(AudioSource source, int before, double observedAt)
        {
            Assert.That(source.isPlaying, Is.True);
            double elapsed = AudioSettings.dspTime - observedAt;
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(0d));
            const int bufferTolerance = 2048;
            int minimum = before - bufferTolerance;
            int maximum = before + (int)System.Math.Ceiling(elapsed * source.clip.frequency) + bufferTolerance;
            Assert.That(minimum, Is.GreaterThan(0), "A zero/restarted cursor must remain outside the accepted interval.");
            Assert.That(maximum, Is.LessThan(source.clip.samples), "This continuity probe must stay clear of the natural loop boundary.");
            Debug.Log("MENU_THEME_CONTINUITY: before=" + before + ", after=" + source.timeSamples +
                ", elapsedDsp=" + elapsed + ", allowed=" + minimum + ".." + maximum +
                ", clipSamples=" + source.clip.samples);
            Assert.That(source.timeSamples, Is.InRange(minimum, maximum),
                "Playback must continue from this owner's cursor, without a restart or a jump to another scene's position.");
        }
    }
}
