using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Records the listener's real post-master mix in OnAudioFilterRead, never asset samples.
    /// Run with -audioCapturePath pointing to a fresh output directory. The listener tap
    /// copies the unmodified mix before clearing only the downstream hardware buffer.
    /// </summary>
    public sealed class AudioOutputCaptureTests
    {
        private const int PacingRate = 60;
        private const int RequestedSampleRate = 48000;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly string[] volumeKeys =
        {
            MainMenuController.MasterVolumeKey, MainMenuController.MusicVolumeKey, MainMenuController.EffectsVolumeKey,
        };
        private readonly Dictionary<string, float> savedVolumes = new Dictionary<string, float>();
        private readonly Dictionary<AudioListener, bool> savedListeners = new Dictionary<AudioListener, bool>();
        private readonly Dictionary<AudioSource, bool> savedSourceMutes = new Dictionary<AudioSource, bool>();
        private readonly List<AudioClip> loadedClips = new List<AudioClip>();
        private string outputDirectory;
        private GameObject host, audioRoot, listenerHost;
        private AudioOutputCaptureTap outputTap;
        private CoreGameplayDemoController controller;
        private AudioConfiguration savedConfiguration;
        private bool configurationChanged, snapshotTaken;
        private float savedListenerVolume, savedCaptureDeltaTime, savedTimeScale;
        private bool savedListenerPause;
        private int sampleRate, channels;

        [Serializable]
        private sealed class CaptureReport
        {
            public string scenario, wavPath, unityVersion, speakerMode, failureDiagnostic, clockMode;
            public string captureMethod = "Listener OnAudioFilterRead after master, output cleared after copying";
            public bool completed;
            public int sampleRate, channels, captureRate, pacingRate, captureFrames, zeroSampleFrames, minSampleFrames = int.MaxValue, maxSampleFrames;
            public long renderedSampleFrames, trimmedTrailingSampleFrames;
            public long samples, nonzeroSamples, clippedCount, nonFiniteCount, nativeClippedCount, nativeNonFiniteCount;
            public double durationSeconds, peak, rms, nativePeak, dspStart, dspEnd, summedUnscaledDeltaTime;
            public float minUnscaledDeltaTime = float.MaxValue, maxUnscaledDeltaTime;
            public int maxActiveEffectVoices, maxActiveMusicSources, cueRequests;
            public int callbackChannels, channelMismatchCount, overflowSamples, playerLoopFrames;
            public bool victoryVoiceObserved, victoryTailObservedAfterPhaseChange, includesArtificialEightVoiceStress;
            public string finalMusicKey;
            public List<string> events = new List<string>();
            public List<WindowReport> windows = new List<WindowReport>();
        }

        [Serializable]
        private sealed class WindowReport
        {
            public string name;
            public double startSeconds, endSeconds, peak, rms;
            public long samples, nonzeroSamples;
        }

        private sealed class ScheduledAction
        {
            public float At;
            public string Description;
            public Action Action;
        }

        [OneTimeSetUp]
        public void ChooseOutputDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
                if (arguments[index] == "-audioCapturePath") outputDirectory = Path.GetFullPath(arguments[index + 1]);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/AudioFix20261007",
                    "runtime-output-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            Directory.CreateDirectory(outputDirectory);
            foreach (string scenario in new[] { "port", "battle", "settings" })
            {
                Assert.That(File.Exists(Path.Combine(outputDirectory, scenario + ".wav")), Is.False,
                    "Use a new -audioCapturePath; existing evidence must not be overwritten.");
                Assert.That(File.Exists(Path.Combine(outputDirectory, scenario + ".json")), Is.False,
                    "Use a new -audioCapturePath; existing evidence must not be overwritten.");
            }
        }

        [UnitySetUp]
        public IEnumerator StartIsolatedRecording()
        {
            savedConfiguration = AudioSettings.GetConfiguration();
            savedListenerVolume = AudioListener.volume;
            savedListenerPause = AudioListener.pause;
            savedCaptureDeltaTime = Time.captureDeltaTime;
            savedTimeScale = Time.timeScale;
            savedVolumes.Clear();
            savedListeners.Clear();
            savedSourceMutes.Clear();
            loadedClips.Clear();
            configurationChanged = false;
            snapshotTaken = true;
            foreach (string key in volumeKeys)
            {
                if (PlayerPrefs.HasKey(key)) savedVolumes[key] = PlayerPrefs.GetFloat(key);
                PlayerPrefs.SetFloat(key, 1f);
            }
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                savedListeners[listener] = listener.enabled;
                listener.enabled = false;
            }
            MuteUnownedSources();
            AudioConfiguration configuration = savedConfiguration;
            configuration.sampleRate = RequestedSampleRate;
            configuration.speakerMode = AudioSpeakerMode.Stereo;
            if (configuration.sampleRate != savedConfiguration.sampleRate || configuration.speakerMode != savedConfiguration.speakerMode)
            {
                configurationChanged = true;
                Assert.That(AudioSettings.Reset(configuration), Is.True, "Audio configuration reset failed; cannot assume a sample rate or channel count.");
            }
            AudioConfiguration actual = AudioSettings.GetConfiguration();
            sampleRate = actual.sampleRate;
            channels = actual.speakerMode == AudioSpeakerMode.Stereo ? 2 : 0;
            Assert.That(sampleRate, Is.EqualTo(RequestedSampleRate), "Capture format must describe the actual renderer, not a requested but rejected rate.");
            Assert.That(channels, Is.EqualTo(2), "This capture fixture explicitly requires stereo. Actual mode: " + actual.speakerMode);
            Time.timeScale = 1f;
            // The real DSP and production fades both advance in real time. Capture
            // duration and events follow the samples actually received by the tap.
            Time.captureFramerate = 0;
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            listenerHost = new GameObject("Output Capture Listener", typeof(AudioListener));
            outputTap = listenerHost.AddComponent<AudioOutputCaptureTap>();

            // Preload the real imported clips; no samples are read or replaced by the fixture.
            LoadClip("Audio/Music/port_broken_anchor");
            LoadClip("Audio/Music/route_golden_tide_trade");
            LoadClip("Audio/Music/battle_nonboss");
            LoadClip("Audio/Ambience/harbor_ambience");
            LoadClip("Audio/Ambience/tavern_ambience");
            foreach (string key in new[] { "battle_start", "battle_cannon_fire", "battle_impact", "battle_enemy_intent",
                "battle_card_play", "battle_maneuver", "battle_guard", "battle_victory", "battle_sunk" }) LoadClip("Audio/BattleSfx/" + key);
            foreach (string key in new[] { "course_plot", "trade_confirm", "shop_refresh", "chart_draw" }) LoadClip("Audio/InterfaceSfx/" + key);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (loadedClips.Any(clip => clip.loadState != AudioDataLoadState.Loaded))
            {
                Assert.That(loadedClips.Any(clip => clip.loadState == AudioDataLoadState.Failed), Is.False, LoadDiagnostic());
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Imported audio did not load: " + LoadDiagnostic());
                yield return null;
            }

            // The tap already clears downstream playback, even outside a capture
            // window, so the actual sources can run without pausing the DSP clock.
            host = new GameObject("Inactive Output Capture Controller");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null, "Game Awake must not run or write the player's profile.");
            SetField("session", new CoreVoyageSession());
            audioRoot = new GameObject("Isolated Output Capture Sources");
            Invoke("InitializeAudioSources", audioRoot.transform);
            outputTap.BeginCapture(sampleRate * channels * 5);
            yield return null;

            // Prime for bounded real time, not eight batch-editor frames (which can
            // elapse in less than a millisecond). Do not manufacture sample counts.
            int primedSampleFrames = 0;
            var primingDiagnostics = new List<string>();
            var primeClock = System.Diagnostics.Stopwatch.StartNew();
            double nextPrimeFrame = 0d;
            int attempts = 0;
            while (primedSampleFrames <= 0 && primeClock.Elapsed.TotalSeconds < 4d)
            {
                PaceNextPlayerLoopFrame(primeClock, ref nextPrimeFrame);
                yield return null;
                InvokeAudioUpdates();
                primedSampleFrames = outputTap.ReadProgress().sampleCount / channels;
                if (attempts++ < 8 || attempts % PacingRate == 0 || primedSampleFrames > 0)
                    primingDiagnostics.Add("frame=" + Time.frameCount + ", delta=" + Time.deltaTime +
                        ", sampleFrames=" + primedSampleFrames + ", " + ClockDiagnostic());
            }
            Debug.Log("Listener output priming: " + string.Join(" | ", primingDiagnostics));
            Assert.That(primedSampleFrames, Is.GreaterThan(0), "The listener produced no sample frames after bounded real-time priming. " +
                string.Join(" | ", primingDiagnostics));
            Assert.That(outputTap.ReadProgress().channels, Is.EqualTo(channels), "Actual listener channel layout differs from the configured stereo format.");
            outputTap.StopCapture();

            // Include a fresh initial music fade in the scenario, after native priming.
            Field<AudioSource>("activeMusicSource").Stop();
            Invoke("SetMusic", controller.ActiveMusicKey, true);
            Assert.That(controller.ActiveMusicKey, Is.EqualTo("port_broken_anchor"));
        }

        [UnityTearDown]
        public IEnumerator RestoreRecordingState()
        {
            if (audioRoot != null)
                foreach (AudioSource source in audioRoot.GetComponentsInChildren<AudioSource>()) source.Stop();
            if (outputTap != null) outputTap.StopCapture();
            if (audioRoot != null) Object.Destroy(audioRoot);
            if (host != null) Object.Destroy(host);
            if (listenerHost != null) Object.Destroy(listenerHost);
            yield return null;
            if (!snapshotTaken) yield break;
            bool configurationRestored = !configurationChanged || AudioSettings.Reset(savedConfiguration);
            foreach (KeyValuePair<string, float> entry in savedVolumes) PlayerPrefs.SetFloat(entry.Key, entry.Value);
            foreach (string key in volumeKeys) if (!savedVolumes.ContainsKey(key)) PlayerPrefs.DeleteKey(key);
            AudioListener.volume = savedListenerVolume;
            AudioListener.pause = savedListenerPause;
            Time.captureDeltaTime = savedCaptureDeltaTime;
            Time.timeScale = savedTimeScale;
            foreach (KeyValuePair<AudioListener, bool> entry in savedListeners) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (KeyValuePair<AudioSource, bool> entry in savedSourceMutes) if (entry.Key != null) entry.Key.mute = entry.Value;
            snapshotTaken = false;
            Assert.That(configurationRestored, Is.True, "Could not restore the original audio configuration; other saved state was restored.");
        }

        [UnityTest]
        public IEnumerator PortMusicAmbienceAndClicks_RecordTheActualMainMix()
        {
            var actions = new List<ScheduledAction>();
            for (int i = 0; i < 20; i++) Cue(actions, 2f + i * 0.15f, "course_plot", 0.44f);
            At(actions, 6f, "Open tavern ambience", () => SetField("activePortFacility", 2));
            At(actions, 8f, "Return to harbor ambience", () => SetField("activePortFacility", -1));
            Cue(actions, 9f, "trade_confirm", 0.5f);
            Cue(actions, 9.3f, "shop_refresh", 0.44f);
            yield return CaptureScenario("port", 12, actions, null);
        }

        [UnityTest]
        public IEnumerator DenseBattleAndVictory_RecordTheActualMainMix()
        {
            SetPhase(CoreDemoPhase.Battle);
            SetField("battleAnimationPlaying", true);
            Invoke("SetMusic", "battle_nonboss", false);
            var actions = new List<ScheduledAction>();
            Cue(actions, 0f, "battle_start", 0.86f);
            string[] keys = { "battle_cannon_fire", "battle_impact", "battle_enemy_intent", "battle_card_play", "battle_maneuver", "battle_guard" };
            float[] gains = { 0.92f, 0.88f, 0.52f, 0.78f, 0.72f, 0.78f };
            for (int i = 0; i < 21; i++) Cue(actions, 0.7f + i * 0.16f, keys[i % keys.Length], gains[i % gains.Length]);
            // Deliberately artificial worst case, not a normal story sequence: leave
            // enough time for earlier tails, then fill six regular and two reserved
            // voices in one frame, including the shared envelope's 25 ms attenuation.
            At(actions, 6f, "Artificial maximum-overlap stress: six regular cues plus victory and sunk simultaneously", () => { });
            for (int i = 0; i < keys.Length; i++) Cue(actions, 6f, keys[i], gains[i]);
            Cue(actions, 6f, "battle_victory", 0.9f);
            Cue(actions, 6f, "battle_sunk", 0.9f);
            Cue(actions, 8f, "battle_victory", 0.9f);
            At(actions, 8.2f, "Core leaves battle; victory tail must continue", () =>
            {
                SetField("battleAnimationPlaying", false);
                SetPhase(CoreDemoPhase.Settlement);
                Invoke("SetMusic", "port_broken_anchor", false);
            });
            yield return CaptureScenario("battle", 12, actions, null);
        }

        [UnityTest]
        public IEnumerator VolumeSlidersAndMusicReturn_RecordTheActualMainMix()
        {
            var actions = new List<ScheduledAction>();
            for (int i = 0; i < 36; i++) Cue(actions, 0.5f + i * 0.4f, "course_plot", 0.44f);
            At(actions, 4f, "Restore master; crossfade to route", () => Invoke("SetMusic", "route_golden_tide_trade", false));
            At(actions, 10f, "Restore categories; crossfade back to original port track", () => Invoke("SetMusic", "port_broken_anchor", false));
            Cue(actions, 14.15f, "chart_draw", 0.5f);
            yield return CaptureScenario("settings", 15, actions, seconds =>
            {
                float master = seconds < 2f ? 1f : seconds < 3f ? 3f - seconds : seconds < 4f ? 0f : 1f;
                float music = seconds < 5f ? 1f : seconds < 6f ? 6f - seconds : seconds < 8f ? 0f : 1f;
                float effects = seconds < 8f ? 1f : seconds < 9f ? 9f - seconds : seconds < 10f ? 0f : 1f;
                if (seconds >= 12f && seconds < 14f)
                    master = music = effects = seconds < 13f ? 13f - seconds : 0f;
                SetVolumes(master, music, effects);
            });
        }

        private IEnumerator CaptureScenario(string scenario, int durationSeconds, List<ScheduledAction> actions, Action<float> settings)
        {
            actions = actions.OrderBy(action => action.At).ToList();
            var samples = new List<float>(durationSeconds * sampleRate * channels);
            var report = new CaptureReport
            {
                scenario = scenario, wavPath = Path.Combine(outputDirectory, scenario + ".wav"),
                sampleRate = sampleRate, channels = channels, captureRate = Time.captureFramerate, pacingRate = PacingRate,
                clockMode = "Realtime PlayerLoop and DSP with captureFramerate=0; events scheduled by received listener sample frames",
                unityVersion = Application.unityVersion, speakerMode = AudioSettings.GetConfiguration().speakerMode.ToString(),
                includesArtificialEightVoiceStress = scenario == "battle",
                dspStart = AudioSettings.dspTime,
            };
            bool written = false;
            int nextAction = 0;
            int targetSamples = checked(durationSeconds * sampleRate * channels);
            var captureClock = System.Diagnostics.Stopwatch.StartNew();
            double nextPlayerLoopFrame = 0d;
            double lastSampleTime = 0d;
            int previousSampleCount = 0;
            bool captureEnded = false;
            outputTap.BeginCapture(checked((durationSeconds + 5) * sampleRate * channels));
            report.dspStart = AudioSettings.dspTime;
            try
            {
                while (outputTap.ReadProgress().sampleCount < targetSamples)
                {
                    Assert.That(captureClock.Elapsed.TotalSeconds, Is.LessThan(durationSeconds * 2d + 10d),
                        "Native sample clock failed to reach the requested duration within bounded real time. " + ClockDiagnostic());
                    PaceNextPlayerLoopFrame(captureClock, ref nextPlayerLoopFrame);
                    yield return null;
                    MuteUnownedSources();
                    AudioOutputCaptureTap.Progress progress = outputTap.ReadProgress();
                    float seconds = progress.sampleCount / (float)(sampleRate * channels);
                    settings?.Invoke(seconds);
                    while (nextAction < actions.Count && actions[nextAction].At <= seconds + 0.0001f)
                    {
                        ScheduledAction action = actions[nextAction++];
                        action.Action();
                        report.events.Add(action.At.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + action.Description);
                        if (action.Description.StartsWith("Cue ", StringComparison.Ordinal)) report.cueRequests++;
                    }
                    InvokeAudioUpdates();
                    report.minUnscaledDeltaTime = Mathf.Min(report.minUnscaledDeltaTime, Time.unscaledDeltaTime);
                    report.maxUnscaledDeltaTime = Mathf.Max(report.maxUnscaledDeltaTime, Time.unscaledDeltaTime);
                    report.summedUnscaledDeltaTime += Time.unscaledDeltaTime;
                    report.playerLoopFrames++;
                    if (progress.sampleCount == previousSampleCount)
                    {
                        report.zeroSampleFrames++;
                        Assert.That(captureClock.Elapsed.TotalSeconds - lastSampleTime, Is.LessThan(2d),
                            "The listener stopped delivering samples for two real seconds. " + ClockDiagnostic());
                    }
                    else
                    {
                        previousSampleCount = progress.sampleCount;
                        lastSampleTime = captureClock.Elapsed.TotalSeconds;
                    }
                    AudioSource[] sources = audioRoot.GetComponentsInChildren<AudioSource>();
                    report.maxActiveEffectVoices = Math.Max(report.maxActiveEffectVoices, sources.Count(source => !source.loop && source.isPlaying));
                    report.maxActiveMusicSources = Math.Max(report.maxActiveMusicSources,
                        new[] { Field<AudioSource>("activeMusicSource"), Field<AudioSource>("incomingMusicSource") }.Count(source => source.isPlaying));
                    bool victoryPlaying = sources.Any(source => !source.loop && source.isPlaying && source.clip != null && source.clip.name == "battle_victory");
                    report.victoryVoiceObserved |= victoryPlaying;
                    report.victoryTailObservedAfterPhaseChange |= victoryPlaying && scenario == "battle" && seconds >= 8.2f;
                }
                CollectListenerOutput(report, samples, targetSamples);
                captureEnded = true;
                report.completed = true;
                written = true;
                FinishReport(report, samples);
                Assert.That(report.nonFiniteCount, Is.Zero, "The real output contains NaN or infinity. See " + scenario + ".json");
                Assert.That(report.nativeNonFiniteCount, Is.Zero);
                Assert.That(report.callbackChannels, Is.EqualTo(channels), "Capture must use the actual callback channel count.");
                Assert.That(report.channelMismatchCount, Is.Zero, "Listener output format changed during recording.");
                Assert.That(report.overflowSamples, Is.Zero, "The capture buffer overflowed; omitted samples cannot be accepted.");
                Assert.That(report.captureFrames, Is.GreaterThan(0), "No actual audio-thread callbacks were captured.");
                Assert.That(report.nonzeroSamples, Is.GreaterThan(report.samples / 100), "The main output is silent or nearly empty; no silent capture is accepted. " + ClockDiagnostic());
                Assert.That(report.rms, Is.GreaterThan(0.001), "The complete main mix is implausibly quiet; recording must contain actual playback.");
                Assert.That(report.clippedCount, Is.Zero, "The real main output clips. Inspect the unmodified float WAV.");
                Assert.That(report.nativeClippedCount, Is.Zero, "A native buffer clipped, including its final unused tail.");
                Assert.That(report.peak, Is.LessThan(0.98), "The real main output lacks peak headroom. Inspect the unmodified float WAV.");
                Assert.That(report.nativePeak, Is.LessThan(0.98), "A native buffer lacks headroom, including its final unused tail.");
                Assert.That(report.durationSeconds, Is.EqualTo(durationSeconds).Within(0.05), "Unexpected sample-frame/channel or capture-clock convention. " + ClockDiagnostic());
                AudioSettings.GetDSPBufferSize(out int dspBufferFrames, out int dspBuffers);
                double nativeDuration = report.renderedSampleFrames / (double)sampleRate;
                double clockTolerance = 2d * dspBufferFrames / sampleRate + 0.05d;
                Assert.That(report.dspEnd - report.dspStart, Is.GreaterThan(0d), "Captured samples must advance the real DSP clock.");
                Assert.That(report.dspEnd - report.dspStart, Is.EqualTo(nativeDuration).Within(clockTolerance),
                    "Received listener samples and DSP elapsed time disagree. " + ClockDiagnostic());
                Assert.That(report.summedUnscaledDeltaTime, Is.EqualTo(nativeDuration).Within(clockTolerance),
                    "Production fades and native audio did not run on matching real-time clocks. " + ClockDiagnostic());
                Assert.That(report.maxActiveEffectVoices, Is.LessThanOrEqualTo(GameplayAudioMix.EffectVoiceCount));
                Assert.That(report.windows.Single(window => window.name == "audible_intro").rms, Is.GreaterThan(0.001), "The beginning of the scenario must contain actual audible output.");
                if (scenario == "battle")
                {
                    Assert.That(report.maxActiveEffectVoices, Is.EqualTo(GameplayAudioMix.EffectVoiceCount),
                        "The artificial worst-case burst must actually fill all eight voices, including the attenuation transition.");
                    Assert.That(report.victoryVoiceObserved, Is.True, "The victory cue must actually start, not merely be requested.");
                    Assert.That(report.victoryTailObservedAfterPhaseChange, Is.True, "The real victory source must retain its tail after leaving battle.");
                }
                if (scenario == "settings")
                {
                    Assert.That(report.windows.Single(window => window.name == "master_zero").peak, Is.LessThan(0.00001), "Master zero failed to silence the actual mix.");
                    Assert.That(report.windows.Single(window => window.name == "all_zero").peak, Is.LessThan(0.00001), "All sliders at zero failed to silence the actual mix.");
                    Assert.That(report.windows.Single(window => window.name == "music_zero_effects_live").rms, Is.GreaterThan(0.0001), "Muting music must preserve audible effects/ambience.");
                    Assert.That(report.windows.Single(window => window.name == "effects_zero_music_live").rms, Is.GreaterThan(0.001), "Muting effects must preserve audible music.");
                    Assert.That(report.windows.Single(window => window.name == "restored").rms, Is.GreaterThan(0.001), "Restoring volume must restore actual output.");
                    Assert.That(report.finalMusicKey, Is.EqualTo("port_broken_anchor"));
                    Assert.That(Field<float>("musicCrossfade"), Is.LessThanOrEqualTo(0f), "Music fade did not finish on the capture clock. " + ClockDiagnostic());
                }
            }
            finally
            {
                if (!written)
                {
                    if (!captureEnded) CollectListenerOutput(report, samples, targetSamples);
                    report.failureDiagnostic = "Capture interrupted before validation: " + ClockDiagnostic();
                    FinishReport(report, samples);
                }
            }
        }

        private void CollectListenerOutput(CaptureReport report, List<float> samples, int targetSamples)
        {
            float[] captured = outputTap.EndCapture(out AudioOutputCaptureTap.Progress progress);
            report.dspEnd = AudioSettings.dspTime;
            report.captureFrames = progress.callbacks;
            report.callbackChannels = progress.channels;
            report.channelMismatchCount = progress.channelMismatches;
            report.overflowSamples = progress.overflowSamples;
            report.minSampleFrames = progress.minSampleFrames;
            report.maxSampleFrames = progress.maxSampleFrames;
            report.renderedSampleFrames = captured.Length / channels;
            int retained = Math.Min(captured.Length, targetSamples);
            report.trimmedTrailingSampleFrames = (captured.Length - retained) / channels;
            // Keep an unmodified prefix for the requested exact duration. Also
            // measure the full last callback, so trimming cannot hide an overload.
            for (int index = 0; index < captured.Length; index++)
            {
                float value = captured[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) report.nativeNonFiniteCount++;
                else
                {
                    report.nativePeak = Math.Max(report.nativePeak, Math.Abs(value));
                    if (Math.Abs(value) >= 1f) report.nativeClippedCount++;
                }
                if (index < retained) samples.Add(value);
            }
        }

        private void FinishReport(CaptureReport report, List<float> data)
        {
            report.samples = data.Count;
            report.durationSeconds = data.Count / (double)(sampleRate * channels);
            report.finalMusicKey = controller == null ? "" : controller.ActiveMusicKey;
            double squares = 0d;
            foreach (float value in data)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) { report.nonFiniteCount++; continue; }
                double absolute = Math.Abs(value);
                report.peak = Math.Max(report.peak, absolute);
                squares += value * (double)value;
                if (absolute > 0.0000001) report.nonzeroSamples++;
                if (absolute >= 1d) report.clippedCount++;
            }
            report.rms = data.Count == 0 ? 0d : Math.Sqrt(squares / data.Count);
            report.windows.Add(MeasureWindow("audible_intro", data, 0.5, 1.5));
            if (report.scenario == "settings")
            {
                report.windows.Add(MeasureWindow("master_zero", data, 3.25, 3.75));
                report.windows.Add(MeasureWindow("music_zero_effects_live", data, 6.25, 7.75));
                report.windows.Add(MeasureWindow("effects_zero_music_live", data, 9.25, 9.75));
                report.windows.Add(MeasureWindow("all_zero", data, 13.25, 13.75));
                report.windows.Add(MeasureWindow("restored", data, 14.25, 14.9));
            }
            WriteFloatWave(report.wavPath, data);
            File.WriteAllText(Path.Combine(outputDirectory, report.scenario + ".json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("Audio main-output capture: " + report.wavPath + " | samples=" + report.samples + " peak=" + report.peak + " rms=" + report.rms + " clipped=" + report.clippedCount);
        }

        private WindowReport MeasureWindow(string name, List<float> data, double start, double end)
        {
            var result = new WindowReport { name = name, startSeconds = start, endSeconds = end };
            int first = Math.Min(data.Count, (int)(start * sampleRate * channels));
            int last = Math.Min(data.Count, (int)(end * sampleRate * channels));
            double squares = 0d;
            for (int index = first; index < last; index++)
            {
                float value = data[index];
                if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                result.peak = Math.Max(result.peak, Math.Abs(value));
                squares += value * (double)value;
                if (Math.Abs(value) > 0.0000001) result.nonzeroSamples++;
                result.samples++;
            }
            result.rms = result.samples == 0 ? 0d : Math.Sqrt(squares / result.samples);
            return result;
        }

        private void WriteFloatWave(string path, List<float> data)
        {
            int dataBytes = checked(data.Count * sizeof(float));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(48 + dataBytes); writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt ")); writer.Write(16); writer.Write((ushort)3); // IEEE float, no clipping/normalization.
                writer.Write((ushort)channels); writer.Write(sampleRate); writer.Write(sampleRate * channels * sizeof(float));
                writer.Write((ushort)(channels * sizeof(float))); writer.Write((ushort)32);
                writer.Write(Encoding.ASCII.GetBytes("fact")); writer.Write(4); writer.Write(data.Count / channels);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(dataBytes);
                foreach (float value in data) writer.Write(value);
            }
        }

        private void InvokeAudioUpdates()
        {
            Invoke("UpdateMusicCrossfade");
            Invoke("UpdateAmbience");
            Invoke("UpdateEffectVoices");
        }

        private static void PaceNextPlayerLoopFrame(System.Diagnostics.Stopwatch clock, ref double nextFrame)
        {
            // Batch Editor can ignore Application.targetFrameRate. A short sleep
            // before yielding lets the next genuine player-loop delta include the
            // elapsed real time; no Unity clock or production fade is overwritten.
            nextFrame += 1d / PacingRate;
            double remaining = nextFrame - clock.Elapsed.TotalSeconds;
            if (remaining > 0.001d) System.Threading.Thread.Sleep((int)(remaining * 1000d));
        }

        private void LoadClip(string path)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            Assert.That(clip, Is.Not.Null, path);
            if (clip.loadState == AudioDataLoadState.Unloaded) Assert.That(clip.LoadAudioData(), Is.True, "Could not request audio data: " + path);
            loadedClips.Add(clip);
        }

        private void MuteUnownedSources()
        {
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (audioRoot != null && source.transform.IsChildOf(audioRoot.transform)) continue;
                if (!savedSourceMutes.ContainsKey(source)) savedSourceMutes[source] = source.mute;
                source.mute = true;
            }
        }

        private string LoadDiagnostic() => string.Join(", ", loadedClips.Where(clip => clip.loadState != AudioDataLoadState.Loaded)
            .Select(clip => clip.name + "=" + clip.loadState));
        private string ClockDiagnostic() => "sr=" + AudioSettings.outputSampleRate + ", mode=" + AudioSettings.GetConfiguration().speakerMode +
            ", captureFramerate=" + Time.captureFramerate + ", unscaledDelta=" + Time.unscaledDeltaTime + ", dsp=" + AudioSettings.dspTime +
            ", listenerVolume=" + AudioListener.volume + ", paused=" + AudioListener.pause;
        private void SetVolumes(float master, float music, float effects)
        {
            PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, master);
            PlayerPrefs.SetFloat(MainMenuController.MusicVolumeKey, music);
            PlayerPrefs.SetFloat(MainMenuController.EffectsVolumeKey, effects);
            Invoke("RefreshMusicVolume");
        }
        private void SetPhase(CoreDemoPhase phase) => typeof(CoreVoyageSession).GetProperty("Phase").GetSetMethod(true)
            .Invoke(controller.Session, new object[] { phase });
        private void At(List<ScheduledAction> actions, float seconds, string description, Action action) =>
            actions.Add(new ScheduledAction { At = seconds, Description = description, Action = action });
        private void Cue(List<ScheduledAction> actions, float seconds, string key, float gain) =>
            At(actions, seconds, "Cue " + key + " @ " + gain, () => Invoke("PlaySfx", key, gain));
        private T Field<T>(string name) => (T)typeof(CoreGameplayDemoController).GetField(name, PrivateInstance).GetValue(controller);
        private void SetField(string name, object value) => typeof(CoreGameplayDemoController).GetField(name, PrivateInstance).SetValue(controller, value);
        private void Invoke(string name, params object[] args) => typeof(CoreGameplayDemoController).GetMethod(name, PrivateInstance).Invoke(controller, args);
    }

    /// <summary>
    /// Audio-thread tap on the same GameObject as the sole enabled AudioListener.
    /// This callback receives the post-master mix; multiplying by Master again
    /// would invalidate both the recording and the volume-slider assertions.
    /// </summary>
    public sealed class AudioOutputCaptureTap : MonoBehaviour
    {
        public struct Progress
        {
            public int sampleCount, channels, callbacks, channelMismatches, overflowSamples;
            public int minSampleFrames, maxSampleFrames;
        }

        private readonly object gate = new object();
        private float[] buffer;
        private Progress progress;
        private bool capturing;

        public void BeginCapture(int capacity)
        {
            // Allocate on the main thread, never in an audio callback.
            var allocated = new float[capacity];
            lock (gate)
            {
                buffer = allocated;
                progress = new Progress { minSampleFrames = int.MaxValue };
                capturing = true;
            }
        }

        public Progress ReadProgress()
        {
            lock (gate) return progress;
        }

        public void StopCapture()
        {
            lock (gate) capturing = false;
        }

        public float[] EndCapture(out Progress capturedProgress)
        {
            float[] completedBuffer;
            lock (gate)
            {
                capturing = false;
                capturedProgress = progress;
                completedBuffer = buffer;
            }
            // The stopped buffer cannot change. Copy outside the lock so the
            // audio thread can immediately resume clearing downstream output.
            var copy = new float[capturedProgress.sampleCount];
            if (copy.Length > 0) Array.Copy(completedBuffer, copy, copy.Length);
            return copy;
        }

        private void OnAudioFilterRead(float[] data, int channelCount)
        {
            lock (gate)
            {
                if (capturing && buffer != null)
                {
                    if (progress.channels != 0 && progress.channels != channelCount) progress.channelMismatches++;
                    progress.channels = channelCount;
                    progress.callbacks++;
                    int frames = channelCount > 0 ? data.Length / channelCount : 0;
                    progress.minSampleFrames = Math.Min(progress.minSampleFrames, frames);
                    progress.maxSampleFrames = Math.Max(progress.maxSampleFrames, frames);
                    int copied = Math.Min(data.Length, buffer.Length - progress.sampleCount);
                    Array.Copy(data, 0, buffer, progress.sampleCount, copied);
                    progress.sampleCount += copied;
                    progress.overflowSamples += data.Length - copied;
                }
            }
            // Copy first, then suppress only the downstream hardware playback.
            // No source, listener gain, sample, or DSP clock is modified for capture.
            Array.Clear(data, 0, data.Length);
        }
    }
}
