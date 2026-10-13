using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    // Audit-only: copy into the isolated project's existing PlayMode test assembly.
    // Uses only pre-optimization APIs so the identical probe can measure both revisions.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [Serializable] private sealed class PortUiPerformanceReport
        {
            public string unity, cpu, gpu, os, measuredAtUtc;
            public bool allocationCounterAvailable;
            public string method = "Unity Editor PlayMode; same fixed date and profile fixtures; 3 warmups + 12 samples per scenario. CPU and current-thread managed bytes cover only real button callbacks + Canvas.ForceUpdateCanvases, excluding fixture reset, UI lookup, assertions, snapshots, waits, deferred destruction and GPU. One frame separates operations. -1 means unavailable/not applicable. Market has simultaneous buy/sell controls, so its mode scenario cycles the actual delivery preference. Not an FPS or standalone-player benchmark.";
            public List<PortUiPerformanceSample> samples = new List<PortUiPerformanceSample>();
        }

        [Serializable] private sealed class PortUiPerformanceSample
        {
            public string scenario;
            public int sample, operations, pageReplacements, headerReplacements, backdropReplacements;
            public int activeObjectsBefore, activeObjectsAfter;
            public double milliseconds;
            public long managedBytes;
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator PortUiPerformance_MeasureMarketWarehouseAndFacilityCallbacks()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-portUiPerformanceOutput");
            if (at < 0) Assert.Ignore("Opt-in: supply -portUiPerformanceOutput with a new JSON path.");
            Assert.That(at + 1, Is.LessThan(args.Length));
            string output = Path.GetFullPath(args[at + 1]);
            Assert.That(File.Exists(output), Is.False, "Preserve earlier benchmark evidence.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = new PortUiPerformanceReport
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, os = SystemInfo.operatingSystem,
                measuredAtUtc = DateTime.UtcNow.ToString("O")
            };
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var allocationProbe = new byte[16384];
                report.allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() - before >= allocationProbe.Length;
                GC.KeepAlive(allocationProbe);
            }
            catch (NotSupportedException) { report.allocationCounterAvailable = false; }

            DateTime now = new DateTime(77, 10, 8, 12, 0, 0);
            Dictionary<string, int?> ledger = PreserveTradeMarketLedger(now);
            string[] scenarios = { "warehouse_catalog_6_pages", "market_storage_mode_3_changes", "market_buy_sell_wood", "facility_cycle_10_changes" };
            int scenarioAt = Array.IndexOf(args, "-portUiPerformanceScenarios");
            if (scenarioAt >= 0)
            {
                Assert.That(scenarioAt + 1, Is.LessThan(args.Length));
                string[] selected = args[scenarioAt + 1].Split(',');
                Assert.That(selected.All(scenarios.Contains), Is.True, "Unknown port UI scenario.");
                scenarios = selected;
            }
            try
            {
                foreach (string scenario in scenarios)
                {
                    PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseStorageModeKey, (int)MarketPurchaseStorageMode.Ship);
                    yield return LoadProvisioningFixture();
                    var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                    CoreVoyageSession session = controller.Session;
                    SetProvisionClock(controller, () => now);
                    MethodInfo renderMethod = typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
                    Action render = (Action)Delegate.CreateDelegate(typeof(Action), controller, renderMethod);
                    bool warehouse = scenario.StartsWith("warehouse", StringComparison.Ordinal);
                    bool facility = scenario.StartsWith("facility", StringComparison.Ordinal);
                    ClickAcceptanceButton(warehouse ? "Port Hotspot Warehouse" : "Port Hotspot Market");
                    yield return null;
                    if (warehouse) { ClickAcceptanceButton("Warehouse Collection Catalog"); yield return null; }
                    yield return null;
                    string[] ownedBefore = controller.WarehouseCollection.OwnedIds.ToArray();
                    string pageName = warehouse ? "Warehouse Panel" : "Market Panel";
                    for (int iteration = -3; iteration < 12; iteration++)
                    {
                        decimal expectedGold = session.ProfileGold;
                        if (scenario == "market_buy_sell_wood")
                        {
                            // Keep every sample at the same quote, stock and asset state.
                            session.RestoreProfile(2000, "断锚港");
                            session.Cargo.Wood = 0;
                            PlayerPrefs.DeleteKey(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood));
                            render();
                            expectedGold = 2000 - PortMarketCatalog.CalculateBuyCost(session.CurrentPort, PortMarketItem.Wood, now, 1)
                                + PortMarketCatalog.CalculateSellRevenue(session.CurrentPort, PortMarketItem.Wood, now, 1);
                        }
                        yield return null;
                        var sample = new PortUiPerformanceSample
                        {
                            scenario = scenario, sample = iteration, operations = facility ? 10 : warehouse ? 6 : scenario == "market_buy_sell_wood" ? 2 : 3,
                            pageReplacements = facility ? -1 : 0,
                            managedBytes = report.allocationCounterAvailable ? 0 : -1,
                            activeObjectsBefore = controller.GetComponentsInChildren<Transform>().Length
                        };
                        for (int operation = 0; operation < sample.operations; operation++)
                        {
                            string buttonName = facility ? "Next Port Facility" : warehouse
                                ? operation < 3 ? "Warehouse Treasure Page Next" : "Warehouse Treasure Page Previous"
                                : scenario == "market_buy_sell_wood" ? operation == 0 ? "Market Buy wood" : "Market Sell wood" : "Market Storage Mode";
                            Button button = ActiveButton(buttonName);
                            Assert.That(button, Is.Not.Null, buttonName);
                            Assert.That(button.IsInteractable(), Is.True, buttonName);
                            GameObject beforePage = facility ? null : GameObject.Find(pageName);
                            GameObject beforeHeader = GameObject.Find("Port Facility Header");
                            RawImage beforeBackdrop = PortUiAuditBackdrop(controller);
                            Assert.That(beforeHeader, Is.Not.Null);
                            Assert.That(beforeBackdrop, Is.Not.Null);
                            long allocatedBefore = report.allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
                            long ticks = Stopwatch.GetTimestamp();
                            button.onClick.Invoke();
                            Canvas.ForceUpdateCanvases();
                            sample.milliseconds += (Stopwatch.GetTimestamp() - ticks) * 1000d / Stopwatch.Frequency;
                            if (report.allocationCounterAvailable) sample.managedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                            if (!facility && GameObject.Find(pageName) != beforePage) sample.pageReplacements++;
                            if (GameObject.Find("Port Facility Header") != beforeHeader) sample.headerReplacements++;
                            if (PortUiAuditBackdrop(controller) != beforeBackdrop) sample.backdropReplacements++;
                            yield return null;
                        }
                        sample.activeObjectsAfter = controller.GetComponentsInChildren<Transform>().Length;
                        if (scenario == "market_buy_sell_wood")
                        {
                            Assert.That(session.Cargo.Wood, Is.Zero);
                            Assert.That(session.ProfileGold, Is.EqualTo(expectedGold));
                            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.MarketPurchaseKey(session.CurrentPort, now, PortMarketItem.Wood)), Is.EqualTo(1));
                        }
                        else if (warehouse)
                        {
                            Assert.That(GameObject.Find("Warehouse Treasure Page").GetComponent<Text>().text, Is.EqualTo("1 / 14"));
                            CollectionAssert.AreEquivalent(ownedBefore, controller.WarehouseCollection.OwnedIds,
                                "Browsing the catalog must preserve any legitimate fixture collection rewards.");
                        }
                        else if (facility) Assert.That(ActiveButton("Market Buy wood"), Is.Not.Null, "Ten next-facility callbacks must return to the market.");
                        else Assert.That(controller.MarketStorageMode, Is.EqualTo(MarketPurchaseStorageMode.Ship));
                        if (iteration >= 0) report.samples.Add(sample);
                    }
                    File.WriteAllText(output, JsonUtility.ToJson(report, true));
                    // Capture the settled presentation after its 0.24-second
                    // screen fade. This wait is outside every measured sample.
                    yield return new WaitForSecondsRealtime(0.35f);
                    NavalBattleVisualCapture.Capture(controller, "PortUi_" + scenario + ".png",
                        "固定夹具 · 真实按钮回调性能采样后的界面（截图不计入测量）", pageName);
                    LogAssert.NoUnexpectedReceived();
                }
            }
            finally { RestoreProvisionLedger(ledger); }
            TestContext.WriteLine("Port UI performance report: " + output);
        }

        private static RawImage PortUiAuditBackdrop(CoreGameplayDemoController controller)
        {
            return controller.GetComponentsInChildren<RawImage>().SingleOrDefault(image =>
                image.name.EndsWith(" Background", StringComparison.Ordinal) && image.transform.parent != null &&
                image.transform.parent.name == "Phase Content");
        }
    }
}
