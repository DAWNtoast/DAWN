using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>Two-second API probes. No gameplay controller, synthetic samples or fallback data.</summary>
    public sealed class NativeRendererProbeTests
    {
        private const int Rate = 48000, Channels = 2, FramesPerCall = 800, Calls = 120;
        private string outputDirectory;
        private GameObject host;
        private AudioSource source;
        private AudioClip clip;
        private AudioConfiguration savedConfiguration;
        private float savedVolume, savedCaptureDelta, savedTimeScale;
        private bool savedPause, configurationChanged, recording;
        private readonly Dictionary<AudioListener, bool> listeners = new Dictionary<AudioListener, bool>();
        private readonly Dictionary<AudioSource, bool> sourceMutes = new Dictionary<AudioSource, bool>();

        [Serializable]
        private sealed class Observation
        {
            public int playerFrame, reportedFrames, requestedFrames, sourceSamplesBefore, sourceSamplesAfter;
            public float unscaledDelta;
            public double dspBefore, dspAfter;
            public bool renderCalled, renderSucceeded;
        }

        [Serializable]
        private sealed class Report
        {
            public string name, mode, clipName, unityVersion;
            public bool pausedAtStart, completed;
            public int sampleRate = Rate, channels = Channels, clipFrequency, sourceStart, sourceEnd, zeroQueries, successfulRenders;
            public long sampleFrames, nonzeroSamples, clippedSamples, nonFiniteSamples;
            public double dspStart, dspEnd, sourceElapsedSeconds, sampleDurationSeconds, peak, rms, realtimeSeconds, unscaledSeconds;
            public List<Observation> observations = new List<Observation>();
        }

        [OneTimeSetUp]
        public void SetOutputDirectory()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-audioNativeProbe") < 0)
                Assert.Ignore("Opt-in API diagnostic: pass -audioNativeProbe and a fresh -audioCapturePath to run these probes.");
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-audioCapturePath") outputDirectory = Path.GetFullPath(args[i + 1]);
            if (outputDirectory == null)
                outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/AudioFix20261007",
                    "native-probe-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            Directory.CreateDirectory(outputDirectory);
        }

        [UnitySetUp]
        public IEnumerator PrepareActualSource()
        {
            savedConfiguration = AudioSettings.GetConfiguration();
            savedVolume = AudioListener.volume;
            savedPause = AudioListener.pause;
            savedCaptureDelta = Time.captureDeltaTime;
            savedTimeScale = Time.timeScale;
            listeners.Clear(); sourceMutes.Clear(); recording = false; configurationChanged = false;
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                listeners[listener] = listener.enabled;
                listener.enabled = false;
            }
            foreach (AudioSource other in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                sourceMutes[other] = other.mute;
                other.mute = true;
            }
            AudioConfiguration configuration = savedConfiguration;
            configuration.sampleRate = Rate;
            configuration.speakerMode = AudioSpeakerMode.Stereo;
            if (configuration.sampleRate != savedConfiguration.sampleRate || configuration.speakerMode != savedConfiguration.speakerMode)
            {
                configurationChanged = true;
                Assert.That(AudioSettings.Reset(configuration), Is.True);
            }
            Assert.That(AudioSettings.GetConfiguration().sampleRate, Is.EqualTo(Rate));
            Assert.That(AudioSettings.GetConfiguration().speakerMode, Is.EqualTo(AudioSpeakerMode.Stereo));
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            clip = Resources.Load<AudioClip>("Audio/Music/port_broken_anchor");
            Assert.That(clip, Is.Not.Null);
            if (clip.loadState == AudioDataLoadState.Unloaded) Assert.That(clip.LoadAudioData(), Is.True);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (clip.loadState != AudioDataLoadState.Loaded)
            {
                Assert.That(clip.loadState, Is.Not.EqualTo(AudioDataLoadState.Failed));
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                yield return null;
            }
            host = new GameObject("Native renderer probe", typeof(AudioListener), typeof(AudioSource));
            source = host.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f; // Avoid hardware output before recording; no listener pause needed.
            source.clip = clip;
            source.Play();
            yield return null; // Register the complete listener/source graph.
        }

        [UnityTearDown]
        public IEnumerator RestoreState()
        {
            if (source != null) source.Stop();
            if (recording) { AudioRenderer.Stop(); recording = false; }
            if (host != null) Object.Destroy(host);
            yield return null;
            bool restored = !configurationChanged || AudioSettings.Reset(savedConfiguration);
            AudioListener.volume = savedVolume;
            AudioListener.pause = savedPause;
            Time.captureDeltaTime = savedCaptureDelta;
            Time.timeScale = savedTimeScale;
            foreach (var entry in listeners) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in sourceMutes) if (entry.Key != null) entry.Key.mute = entry.Value;
            Assert.That(restored, Is.True, "Original audio configuration could not be restored.");
        }

        [UnityTest]
        public IEnumerator UnpausedStart_QuerySuggestedFrames() => Probe("unpaused-query", false, false);

        [UnityTest]
        public IEnumerator UnpausedStart_DirectNative800Frames() => Probe("unpaused-direct", false, true);

        [UnityTest]
        public IEnumerator PausedStartThenResume_DirectNative800Frames() => Probe("paused-direct", true, true);

        private IEnumerator Probe(string name, bool pausedAtStart, bool directNativeRender)
        {
            string wavPath = Path.Combine(outputDirectory, name + ".wav");
            string jsonPath = Path.Combine(outputDirectory, name + ".json");
            Assert.That(File.Exists(wavPath) || File.Exists(jsonPath), Is.False, "Choose a new -audioCapturePath; prior probe evidence is preserved.");
            AudioListener.pause = pausedAtStart;
            recording = AudioRenderer.Start();
            Assert.That(recording, Is.True, "Another recorder may own AudioRenderer.");
            AudioListener.pause = false;
            source.volume = 0.4f;
            var report = new Report
            {
                name = name, pausedAtStart = pausedAtStart, clipName = clip.name, clipFrequency = clip.frequency,
                unityVersion = Application.unityVersion,
                mode = directNativeRender ? "Explicit native Render request: 800 frames, independent of untouched query result" : "Render only the API-reported suggested frame count",
                dspStart = AudioSettings.dspTime, sourceStart = source.timeSamples,
            };
            var samples = new List<float>(Rate * Channels * 2);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            bool written = false;
            try
            {
                for (int iteration = 0; iteration < Calls; iteration++)
                {
                    double wait = (iteration + 1) / 60d - clock.Elapsed.TotalSeconds;
                    if (wait > 0.001d) System.Threading.Thread.Sleep((int)(wait * 1000d));
                    yield return null;
                    int reported = AudioRenderer.GetSampleCountForCaptureFrame();
                    Assert.That(reported, Is.GreaterThanOrEqualTo(0));
                    var observation = new Observation
                    {
                        playerFrame = Time.frameCount, reportedFrames = reported,
                        requestedFrames = directNativeRender ? FramesPerCall : reported,
                        dspBefore = AudioSettings.dspTime, sourceSamplesBefore = source.timeSamples,
                        unscaledDelta = Time.unscaledDeltaTime,
                    };
                    report.observations.Add(observation);
                    report.unscaledSeconds += Time.unscaledDeltaTime;
                    if (reported == 0) report.zeroQueries++;
                    if (observation.requestedFrames > 0)
                    {
                        // This is an explicit experiment with the public native Render
                        // buffer argument. It does not replace/alter the reported count,
                        // copy asset samples, synthesize audio, or accept a zero output.
                        using (var buffer = new NativeArray<float>(observation.requestedFrames * Channels, Allocator.Temp))
                        {
                            observation.renderCalled = true;
                            observation.renderSucceeded = AudioRenderer.Render(buffer);
                            Assert.That(observation.renderSucceeded, Is.True, "Native Render rejected the explicit buffer request.");
                            report.successfulRenders++;
                            report.sampleFrames += observation.requestedFrames;
                            for (int index = 0; index < buffer.Length; index++) samples.Add(buffer[index]);
                        }
                    }
                    observation.dspAfter = AudioSettings.dspTime;
                    observation.sourceSamplesAfter = source.timeSamples;
                    Assert.That(observation.dspAfter, Is.GreaterThanOrEqualTo(observation.dspBefore), "DSP clock moved backwards.");
                }
                report.completed = true;
                written = true;
                SaveReport(report, samples, clock, wavPath, jsonPath);
                AudioSettings.GetDSPBufferSize(out int block, out int buffers);
                double tolerance = 2d * block / Rate + 0.05d;
                Assert.That(report.sampleDurationSeconds, Is.EqualTo(2d).Within(tolerance), "API did not provide two seconds of real native output.");
                Assert.That(report.dspEnd - report.dspStart, Is.EqualTo(report.sampleDurationSeconds).Within(tolerance), "Rendered output did not advance the actual DSP clock correspondingly.");
                Assert.That(report.sourceElapsedSeconds, Is.EqualTo(report.sampleDurationSeconds).Within(tolerance), "The imported AudioSource playback cursor did not advance with the rendered mix.");
                Assert.That(report.dspEnd, Is.GreaterThan(report.dspStart + 1.8d));
                Assert.That(report.sourceElapsedSeconds, Is.GreaterThan(1.8d));
                Assert.That(report.nonFiniteSamples, Is.Zero);
                Assert.That(report.nonzeroSamples, Is.GreaterThan(samples.Count / 10), "Native output must contain substantial actual sound, not silence.");
                Assert.That(report.rms, Is.GreaterThan(0.001d));
                Assert.That(report.clippedSamples, Is.Zero);
                Assert.That(report.peak, Is.LessThan(0.98d));
            }
            finally
            {
                if (!written) SaveReport(report, samples, clock, wavPath, jsonPath);
            }
        }

        private void SaveReport(Report report, List<float> samples, System.Diagnostics.Stopwatch clock, string wav, string json)
        {
            report.dspEnd = AudioSettings.dspTime;
            report.sourceEnd = source.timeSamples;
            int elapsedSamples = report.sourceEnd - report.sourceStart;
            if (elapsedSamples < 0) elapsedSamples += clip.samples;
            report.sourceElapsedSeconds = elapsedSamples / (double)clip.frequency;
            report.sampleDurationSeconds = report.sampleFrames / (double)Rate;
            report.realtimeSeconds = clock.Elapsed.TotalSeconds;
            double squares = 0d;
            foreach (float sample in samples)
            {
                if (float.IsNaN(sample) || float.IsInfinity(sample)) { report.nonFiniteSamples++; continue; }
                double magnitude = Math.Abs(sample);
                report.peak = Math.Max(report.peak, magnitude);
                squares += sample * (double)sample;
                if (magnitude > 0.0000001d) report.nonzeroSamples++;
                if (magnitude >= 1d) report.clippedSamples++;
            }
            report.rms = samples.Count == 0 ? 0d : Math.Sqrt(squares / samples.Count);
            using (var writer = new BinaryWriter(new FileStream(wav, FileMode.CreateNew, FileAccess.Write), Encoding.ASCII))
            {
                int bytes = checked(samples.Count * sizeof(float));
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(48 + bytes); writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt ")); writer.Write(16); writer.Write((ushort)3); writer.Write((ushort)Channels);
                writer.Write(Rate); writer.Write(Rate * Channels * sizeof(float)); writer.Write((ushort)(Channels * sizeof(float))); writer.Write((ushort)32);
                writer.Write(Encoding.ASCII.GetBytes("fact")); writer.Write(4); writer.Write(samples.Count / Channels);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                foreach (float sample in samples) writer.Write(sample);
            }
            File.WriteAllText(json, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            Debug.Log("Native renderer probe " + report.name + ": frames=" + report.sampleFrames + " zeroQueries=" + report.zeroQueries +
                " DSP=" + (report.dspEnd - report.dspStart) + " source=" + report.sourceElapsedSeconds + " peak=" + report.peak + " rms=" + report.rms);
        }
    }
}
