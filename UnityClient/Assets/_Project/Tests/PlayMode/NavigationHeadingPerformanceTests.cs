using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [Serializable] private sealed class NavigationHeadingReport
        {
            public string method = "Editor PlayMode; production heading button callbacks plus forced Canvas layout. Three warmups, then 20 measured changes. Callback allocations use the current managed thread only if a known allocation advances the counter; -1 means unavailable. Frame interval includes Editor/test harness and the normal frame cap, not standalone FPS or isolated GPU timing.";
            public string unity, cpu, gpu;
            public bool allocationCounterAvailable;
            public List<NavigationHeadingSample> heading = new List<NavigationHeadingSample>();
        }
        [Serializable] private sealed class NavigationHeadingSample
        {
            public int sample, chartReplacements, controlsReplacements, oceanReplacements;
            public double callbackMs, maxFrameIntervalMs;
            public long callbackAllocatedBytes;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NavigationHeadingPerformance_ProductionCallbacks()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-navigationHeadingPerformanceOutput");
            if (at < 0) Assert.Ignore("Opt-in: provide -navigationHeadingPerformanceOutput with a new JSON path.");
            Assert.That(at + 1, Is.LessThan(args.Length));
            string output = Path.GetFullPath(args[at + 1]);
            Assert.That(File.Exists(output), Is.False);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = new NavigationHeadingReport { unity = Application.unityVersion,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName };
            long probeBefore = GC.GetAllocatedBytesForCurrentThread();
            var allocationProbe = new byte[1024];
            report.allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() > probeBefore;
            GC.KeepAlive(allocationProbe);
            yield return LoadNavigationInterfaceRefreshFixture();
            for (int index = -3; index < 20; index++)
            {
                yield return null;
                GameObject chart = GameObject.Find("Navigation Chart"), controls = GameObject.Find("Navigation Controls"),
                    ocean = GameObject.Find("Lookout Ocean");
                Button button = ActiveButton(index % 2 == 0 ? "Heading South" : "Heading East");
                var sample = new NavigationHeadingSample { sample = index };
                long bytes = GC.GetAllocatedBytesForCurrentThread(), ticks = Stopwatch.GetTimestamp();
                button.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                sample.callbackMs = (Stopwatch.GetTimestamp() - ticks) * 1000d / Stopwatch.Frequency;
                sample.callbackAllocatedBytes = report.allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() - bytes : -1;
                sample.chartReplacements = chart == GameObject.Find("Navigation Chart") ? 0 : 1;
                sample.controlsReplacements = controls == GameObject.Find("Navigation Controls") ? 0 : 1;
                sample.oceanReplacements = ocean == GameObject.Find("Lookout Ocean") ? 0 : 1;
                yield return null;
                sample.maxFrameIntervalMs = Time.unscaledDeltaTime * 1000d;
                if (index >= 0) report.heading.Add(sample);
            }
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
