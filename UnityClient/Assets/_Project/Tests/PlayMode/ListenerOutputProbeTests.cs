using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>Opt-in diagnostic of the actual listener filter tap relative to Master gain.</summary>
    public sealed class ListenerOutputProbeTests
    {
        private readonly Dictionary<AudioListener, bool> listeners = new Dictionary<AudioListener, bool>();
        private readonly Dictionary<AudioSource, bool> sources = new Dictionary<AudioSource, bool>();
        private GameObject listenerHost, sourceHost;
        private AudioSource source;
        private AudioClip clip;
        private ListenerOutputProbeTap tap;
        private float savedVolume, savedCaptureDelta, savedTimeScale, savedMaster;
        private bool savedPause, hadMaster, snapshot;
        private int sampleRate;

        [Serializable]
        private sealed class ProbeReport
        {
            public string unityVersion, runtimePlatform, tap, hardwareSilencing, conclusion;
            public int sampleRate;
            public double halfToFullRms, zeroToFullRms, dspStart, dspEnd, realSeconds;
            public bool usedAudioRenderer, samplesMultipliedByMaster, passed;
            public WindowReport[] windows;
        }

        [Serializable]
        private sealed class WindowReport
        {
            public float requestedMaster, observedMaster;
            public int channels, samples, callbacks, overflowSamples;
            public double rms, peak;
            public string wavPath;
        }

        [SetUp]
        public void PrepareRealtimeListenerProbe()
        {
            if (!HasArgument("-audioNativeProbe"))
                Assert.Ignore("Explicit audio API diagnostic only; pass -audioNativeProbe to run.");
            savedVolume = AudioListener.volume;
            savedPause = AudioListener.pause;
            savedCaptureDelta = Time.captureDeltaTime;
            savedTimeScale = Time.timeScale;
            hadMaster = PlayerPrefs.HasKey(MainMenuController.MasterVolumeKey);
            savedMaster = PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey);
            snapshot = true;
            foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                listeners[listener] = listener.enabled;
                listener.enabled = false;
            }
            foreach (AudioSource existing in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                sources[existing] = existing.mute;
                existing.mute = true;
            }
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            sampleRate = AudioSettings.outputSampleRate;
            listenerHost = new GameObject("Unique Listener Output Probe", typeof(AudioListener));
            tap = listenerHost.AddComponent<ListenerOutputProbeTap>();
            tap.Configure(sampleRate * 8); // Up to a second of 7.1 output, allocated on the main thread.
            sourceHost = new GameObject("Listener Probe Calibration Source");
            source = sourceHost.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.reverbZoneMix = 0f;
            source.bypassListenerEffects = false;
            source.volume = 1f;
            source.priority = 0;
            clip = AudioClip.Create("Probe only: 440 Hz calibration, amplitude 0.01", sampleRate, 1, sampleRate, false);
            var calibration = new float[sampleRate];
            for (int index = 0; index < calibration.Length; index++)
                calibration[index] = (float)(.01 * Math.Sin(2.0 * Math.PI * 440.0 * index / sampleRate));
            clip.SetData(calibration, 0);
            source.clip = clip;
        }

        [UnityTest]
        public IEnumerator ListenerFilterTap_ReportsWhetherMasterGainIsAlreadyApplied()
        {
            var windows = new WindowReport[3];
            var copies = new float[3][];
            float started = Time.realtimeSinceStartup;
            double dspStart = AudioSettings.dspTime;
            SetMaster(1f);
            // The tap is registered before the source starts. It clears the actual
            // callback buffer only AFTER copying, suppressing hardware output.
            yield return null;
            source.Play();
            yield return new WaitForSecondsRealtime(.3f);
            float[] gains = { 1f, .5f, 0f };
            for (int index = 0; index < gains.Length; index++)
            {
                SetMaster(gains[index]);
                yield return new WaitForSecondsRealtime(.2f);
                tap.BeginWindow();
                yield return new WaitForSecondsRealtime(.45f);
                copies[index] = tap.EndWindow(out int channels, out int callbacks, out int overflow);
                double sum = 0, peak = 0;
                foreach (float value in copies[index])
                {
                    sum += (double)value * value;
                    peak = Math.Max(peak, Math.Abs(value));
                }
                windows[index] = new WindowReport
                {
                    requestedMaster = gains[index], observedMaster = AudioListener.volume,
                    channels = channels, samples = copies[index].Length, callbacks = callbacks,
                    overflowSamples = overflow, rms = copies[index].Length > 0 ? Math.Sqrt(sum / copies[index].Length) : 0,
                    peak = peak,
                };
            }
            source.Stop();
            double full = windows[0].rms;
            double halfRatio = full > 1e-12 ? windows[1].rms / full : -1;
            double zeroRatio = full > 1e-12 ? windows[2].rms / full : -1;
            bool windowsValid = true;
            foreach (WindowReport window in windows)
                windowsValid &= window.channels > 0 && window.callbacks > 0 && window.overflowSamples == 0 &&
                    window.samples >= sampleRate * window.channels * .15 &&
                    Math.Abs(window.observedMaster - window.requestedMaster) < .001;
            bool passed = windowsValid && full > 1e-5 && halfRatio >= .45 && halfRatio <= .55 && zeroRatio <= .005;
            string conclusion = passed ? "Listener tap includes Master gain for the measured windows." :
                windowsValid && full > 1e-5 && halfRatio > .9 && zeroRatio > .9 ?
                "Listener tap is before Master gain; unsuitable as an unmodified final-main-mix capture." :
                "Listener tap did not establish post-Master capture; inspect actual window counts and ratios.";
            string directory = ValueAfter("-audioNativeProbePath");
            if (string.IsNullOrWhiteSpace(directory))
                directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/AudioFix20261007/native-probes"));
            Directory.CreateDirectory(directory);
            string stem = "listener-output-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            for (int index = 0; index < windows.Length; index++)
                if (windows[index].channels > 0 && copies[index].Length > 0)
                {
                    windows[index].wavPath = Path.Combine(directory, stem + "-master-" + index + ".wav");
                    WriteRawFloatWave(windows[index].wavPath, copies[index], windows[index].channels, sampleRate);
                }
            var report = new ProbeReport
            {
                unityVersion = Application.unityVersion, runtimePlatform = Application.platform.ToString(), sampleRate = sampleRate,
                tap = "OnAudioFilterRead on the sole enabled AudioListener; copied actual callback samples without Master multiplication.",
                hardwareSilencing = "Array.Clear on callback data AFTER measurement copy; this silences hardware after the measurement tap.",
                conclusion = conclusion, halfToFullRms = halfRatio, zeroToFullRms = zeroRatio,
                dspStart = dspStart, dspEnd = AudioSettings.dspTime, realSeconds = Time.realtimeSinceStartup - started,
                usedAudioRenderer = false, samplesMultipliedByMaster = false, passed = passed, windows = windows,
            };
            string reportPath = Path.Combine(directory, stem + ".json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("LISTENER_OUTPUT_PROBE " + reportPath + " half/full=" + halfRatio + " zero/full=" + zeroRatio + " " + conclusion);
            Assert.That(windowsValid, Is.True, "Missing/short callback windows, overflow, or Master changed unexpectedly. " + reportPath);
            Assert.That(full, Is.GreaterThan(1e-5), "The real listener tap did not contain the calibration source. " + reportPath);
            Assert.That(halfRatio, Is.InRange(.45, .55), "Listener tap must already contain half Master gain; no synthetic multiplication is permitted. " + reportPath);
            Assert.That(zeroRatio, Is.LessThanOrEqualTo(.005), "Listener tap must already reflect Master zero. " + reportPath);
        }

        [UnityTearDown]
        public IEnumerator RestoreListenerProbeState()
        {
            if (!snapshot) yield break;
            if (source != null) source.Stop();
            if (sourceHost != null) Object.Destroy(sourceHost);
            if (listenerHost != null) Object.Destroy(listenerHost);
            if (clip != null) Object.Destroy(clip);
            yield return null;
            foreach (var saved in sources) if (saved.Key != null) saved.Key.mute = saved.Value;
            foreach (var saved in listeners) if (saved.Key != null) saved.Key.enabled = saved.Value;
            if (hadMaster) PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, savedMaster);
            else PlayerPrefs.DeleteKey(MainMenuController.MasterVolumeKey);
            AudioListener.volume = savedVolume;
            AudioListener.pause = savedPause;
            Time.captureDeltaTime = savedCaptureDelta;
            Time.timeScale = savedTimeScale;
            sources.Clear();
            listeners.Clear();
            snapshot = false;
        }

        private static void SetMaster(float value)
        {
            // Production CoreGameplayAudioController applies Master through this same global gain.
            PlayerPrefs.SetFloat(MainMenuController.MasterVolumeKey, value);
            AudioListener.volume = value;
        }

        private static bool HasArgument(string expected) => Array.IndexOf(Environment.GetCommandLineArgs(), expected) >= 0;
        private static string ValueAfter(string expected)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, expected);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }

        private static void WriteRawFloatWave(string path, float[] samples, int channels, int rate)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 4);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((ushort)3); writer.Write((ushort)channels); writer.Write(rate);
                writer.Write(rate * channels * 4); writer.Write((ushort)(channels * 4)); writer.Write((ushort)32);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 4);
                foreach (float value in samples) writer.Write(value);
            }
        }
    }

    /// <summary>Audio-thread tap: copy incoming data first, then silence its downstream hardware buffer.</summary>
    public sealed class ListenerOutputProbeTap : MonoBehaviour
    {
        private readonly object gate = new object();
        private float[] buffer;
        private int count, channels, callbacks, overflow;
        private bool capturing;

        public void Configure(int capacity) { lock (gate) buffer = new float[capacity]; }
        public void BeginWindow()
        {
            lock (gate) { count = channels = callbacks = overflow = 0; capturing = true; }
        }
        public float[] EndWindow(out int channelCount, out int callbackCount, out int overflowCount)
        {
            lock (gate)
            {
                capturing = false;
                var copy = new float[count];
                Array.Copy(buffer, copy, count);
                channelCount = channels; callbackCount = callbacks; overflowCount = overflow;
                return copy;
            }
        }
        private void OnAudioFilterRead(float[] data, int channelCount)
        {
            lock (gate)
            {
                if (capturing && buffer != null)
                {
                    int available = Math.Min(data.Length, buffer.Length - count);
                    Array.Copy(data, 0, buffer, count, available);
                    count += available;
                    channels = channelCount;
                    callbacks++;
                    overflow += data.Length - available;
                }
            }
            // Measurement is already copied. Do not multiply, reconstruct or otherwise alter it.
            Array.Clear(data, 0, data.Length);
        }
    }
}
