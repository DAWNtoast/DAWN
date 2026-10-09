using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class PresentationAudioListenerAuditTests
    {
        [Test]
        public void AudioListener_SteadyStateCacheMeasuresSearchCostWithoutAllocating()
        {
            WithIsolatedListeners((controller, camera) =>
            {
                var ensure = (System.Action)System.Delegate.CreateDelegate(typeof(System.Action), controller,
                    typeof(CoreGameplayDemoController).GetMethod("EnsureAudioListener", BindingFlags.Instance | BindingFlags.NonPublic));
                ensure();
                for (int i = 0; i < 10; i++) { ensure(); Object.FindFirstObjectByType<AudioListener>(); }
                const int frames = 1000;
                var timer = new System.Diagnostics.Stopwatch();
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                timer.Start();
                for (int i = 0; i < frames; i++) Object.FindFirstObjectByType<AudioListener>();
                timer.Stop();
                long searchBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
                double searchMs = timer.Elapsed.TotalMilliseconds;
                timer.Reset();
                before = System.GC.GetAllocatedBytesForCurrentThread();
                timer.Start();
                for (int i = 0; i < frames; i++) ensure();
                timer.Stop();
                long cachedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
                Debug.Log("Audio listener audit, " + frames + " calls: scene search " + searchBytes + " B / " +
                    searchMs.ToString("F3") + " ms; cached " + cachedBytes + " B / " + timer.Elapsed.TotalMilliseconds.ToString("F3") + " ms.");
                Assert.That(cachedBytes, Is.Zero, "A valid cached listener must not allocate during idle frame updates.");
            });
        }

        [Test]
        public void AudioListener_ReusesCachedListenerAndRecoversAfterItIsDestroyed()
        {
            WithIsolatedListeners((controller, camera) =>
            {
                EnsureListener(controller);
                AudioListener first = camera.GetComponent<AudioListener>();
                Assert.That(first, Is.Not.Null);
                Assert.That(CachedListener(controller), Is.SameAs(first));
                for (int i = 0; i < 100; i++) EnsureListener(controller);
                Assert.That(camera.GetComponents<AudioListener>().Length, Is.EqualTo(1));
                Assert.That(CachedListener(controller), Is.SameAs(first));

                Object.DestroyImmediate(first);
                EnsureListener(controller);
                AudioListener replacement = camera.GetComponent<AudioListener>();
                Assert.That(replacement, Is.Not.Null);
                Assert.That(CachedListener(controller), Is.SameAs(replacement));
                Assert.That(replacement.isActiveAndEnabled, Is.True);
            });
        }

        [Test]
        public void AudioListener_DisabledCameraListenerIsReenabledWithoutAddingADuplicate()
        {
            WithIsolatedListeners((controller, camera) =>
            {
                AudioListener listener = camera.AddComponent<AudioListener>();
                listener.enabled = false;
                EnsureListener(controller);
                Assert.That(listener.enabled, Is.True, "A disabled listener does not provide audio output.");
                Assert.That(camera.GetComponents<AudioListener>().Length, Is.EqualTo(1));
                Assert.That(CachedListener(controller), Is.SameAs(listener));
            });
        }

        private static void WithIsolatedListeners(System.Action<CoreGameplayDemoController, GameObject> test)
        {
            AudioListener[] previous = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var enabledListeners = new List<AudioListener>();
            foreach (AudioListener listener in previous)
                if (listener.enabled) { enabledListeners.Add(listener); listener.enabled = false; }
            // Temporarily remove MainCamera tags so selection is independent of
            // whichever gameplay scene the preceding fixture left loaded.
            GameObject[] previousMainCameras = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (GameObject camera in previousMainCameras) camera.tag = "Untagged";
            var cameraHost = new GameObject("Audio Audit Main Camera", typeof(Camera));
            cameraHost.tag = "MainCamera";
            var host = new GameObject("Inactive Audio Audit Controller");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                Assert.That(controller.Session, Is.Null, "This test must not initialize or save a player profile.");
                test(controller, cameraHost);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(cameraHost);
                foreach (GameObject camera in previousMainCameras) if (camera != null) camera.tag = "MainCamera";
                foreach (AudioListener listener in enabledListeners) if (listener != null) listener.enabled = true;
            }
        }

        private static void EnsureListener(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("EnsureAudioListener", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);

        private static AudioListener CachedListener(CoreGameplayDemoController controller) =>
            (AudioListener)typeof(CoreGameplayDemoController).GetField("cachedAudioListener", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller);
    }
}
