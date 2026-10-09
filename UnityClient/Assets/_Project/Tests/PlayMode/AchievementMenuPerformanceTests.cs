using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [Serializable]
        private sealed class AchievementMenuPerformanceReport
        {
            public string unity, cpu, gpu;
            public bool allocationCounterAvailable;
            public string method = "Unity Editor PlayMode; clean progress fixture; 3 warmups and 7 samples. Synchronous controller Awake + Canvas layout, first open + layout, and six forward/six backward page callbacks + layout. Current-thread managed allocation counter checked against a known allocation; -1 means unavailable. Excludes GPU rendering, scene loading, deferred destruction and native memory. Not an FPS or standalone-player benchmark.";
            public List<AchievementMenuPerformanceSample> samples = new List<AchievementMenuPerformanceSample>();
        }

        [Serializable]
        private sealed class AchievementMenuPerformanceSample
        {
            public int sample, startupObjects, startupRows, openedObjects, openedRows;
            public double startupMilliseconds, openMilliseconds, pagingMilliseconds;
            public long startupBytes, openBytes, pagingBytes;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AchievementMenu_PerformanceMeasureStartupOpenAndPaging()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-achievementMenuBenchmarkPath");
            if (at < 0) Assert.Ignore("Opt-in: supply -achievementMenuBenchmarkPath with a new JSON output path.");
            Assert.That(at + 1, Is.LessThan(args.Length));
            string output = Path.GetFullPath(args[at + 1]);
            Assert.That(File.Exists(output), Is.False, "Preserve prior benchmark evidence.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = new AchievementMenuPerformanceReport
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName
            };
            long probeBefore = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[16384];
            report.allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() - probeBefore >= probe.Length;
            GC.KeepAlive(probe);
            var watch = new Stopwatch();
            for (int sample = -3; sample < 7; sample++)
            {
                var host = new GameObject("Achievement Menu Benchmark Fixture");
                try
                {
                    yield return null;
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    watch.Restart();
                    var controller = host.AddComponent<MainMenuController>();
                    Canvas.ForceUpdateCanvases();
                    watch.Stop();
                    long startupBytes = report.allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() - before : -1;
                    var record = new AchievementMenuPerformanceSample
                    {
                        sample = sample, startupBytes = startupBytes, startupMilliseconds = watch.Elapsed.TotalMilliseconds,
                        startupObjects = host.GetComponentsInChildren<Transform>(true).Length,
                        startupRows = host.GetComponentsInChildren<RectTransform>(true).Count(row => row.name.StartsWith("Achievement Row "))
                    };
                    Button open = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements");
                    before = GC.GetAllocatedBytesForCurrentThread();
                    watch.Restart();
                    open.onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    watch.Stop();
                    record.openBytes = report.allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() - before : -1;
                    record.openMilliseconds = watch.Elapsed.TotalMilliseconds;
                    record.openedObjects = host.GetComponentsInChildren<Transform>(true).Length;
                    record.openedRows = host.GetComponentsInChildren<RectTransform>(true).Count(row => row.name.StartsWith("Achievement Row "));
                    Button next = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements Next Page");
                    Button previous = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements Previous Page");
                    before = GC.GetAllocatedBytesForCurrentThread();
                    watch.Restart();
                    for (int page = 0; page < 6; page++) { next.onClick.Invoke(); Canvas.ForceUpdateCanvases(); }
                    for (int page = 0; page < 6; page++) { previous.onClick.Invoke(); Canvas.ForceUpdateCanvases(); }
                    watch.Stop();
                    record.pagingBytes = report.allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() - before : -1;
                    record.pagingMilliseconds = watch.Elapsed.TotalMilliseconds;
                    Assert.That(next.interactable, Is.True);
                    Assert.That(previous.interactable, Is.False);
                    if (sample >= 0) report.samples.Add(record);
                }
                finally
                {
                    host.SetActive(false);
                    Object.Destroy(host);
                }
                yield return null;
            }
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            TestContext.WriteLine("Achievement menu measurement: " + output);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
