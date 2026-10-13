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
using Unity.Profiling;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [Serializable] private sealed class FlowPerformanceReport
        {
            public string unity, cpu, gpu, os, measuredAtUtc;
            public string method = "Unity Editor PlayMode, 3 warmups + 12 samples, same seeded fixtures. CPU callback plus forced Canvas layout; GC Allocated In Frame recorder includes the measured frame/test harness (not just callback). Scene loading, waits, GPU and deferred Destroy time excluded. -1 means allocation counter unavailable. Not an FPS or standalone-player benchmark.";
            public List<FlowPerformanceSample> samples = new List<FlowPerformanceSample>();
        }
        [Serializable] private sealed class FlowPerformanceSample
        {
            public string scenario, mapSeed;
            public int sample, operations, pageReplacements, mapGraphics;
            public double milliseconds;
            public long managedBytes;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator FlowPerformance_MeasureRealMarketSupplySettlementAndBattleCallbacks()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-flowPerformanceOutput");
            if (at < 0) Assert.Ignore("Opt-in: supply -flowPerformanceOutput pointing to a new JSON file.");
            Assert.That(at + 1, Is.LessThan(args.Length), "Missing performance output path.");
            string output = Path.GetFullPath(args[at + 1]);
            Assert.That(File.Exists(output), Is.False, "Keep earlier measurement evidence.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = new FlowPerformanceReport
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                os = SystemInfo.operatingSystem, measuredAtUtc = DateTime.UtcNow.ToString("O"),
            };
            const int warmups = 3, samples = 12;
            string[] scenarios = { "market_quantity_39_steps", "departure_crew_20_changes", "settlement_draw_chart", "navigation_rebuild", "navigation_distance_20_changes", "battle_rebuild", "upgraded_card_reads_10000" };
            int scenarioArg = Array.IndexOf(args, "-flowPerformanceScenarios");
            if (scenarioArg >= 0)
            {
                Assert.That(scenarioArg + 1, Is.LessThan(args.Length), "Missing scenario list.");
                string[] selected = args[scenarioArg + 1].Split(',');
                Assert.That(selected.All(scenarios.Contains), Is.True, "Unknown performance scenario.");
                scenarios = selected;
            }
            using var allocationRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            foreach (string scenario in scenarios)
            {
                yield return LoadProvisioningFixture();
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                SetProvisionClock(controller, () => new DateTime(77, 10, 8));
                MethodInfo renderMethod = typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
                Action render = (Action)Delegate.CreateDelegate(typeof(Action), controller, renderMethod);
                if (scenario.StartsWith("market")) ClickAcceptanceButton("Port Hotspot Market");
                else if (scenario.StartsWith("departure"))
                {
                    ClickAcceptanceButton("Port Hotspot Voyage");
                    yield return null;
                    ClickAcceptanceButton("Configure Voyage Crew");
                }
                else
                {
                    session.Cargo.Water = session.Cargo.Food = 100;
                    session.Cargo.Wood = 10; session.Cargo.Powder = 25; session.Cargo.Cannonballs = 40;
                    session.ConfigureCrewCapabilities(true, false, false, false, false, false);
                    // CreateChartSeed includes a random GUID salt. Keep the exact
                    // chart fixed across runs so terrain/pathfinding are comparable.
                    Assert.That(session.TrySelectChartSeed("SS2.0.0.1.0.0.2EDC24E9.0000C0DE.4DD97F4E", out string reason), Is.True, reason);
                    session.ConfigureCardUpgrades(CoreVoyageSession.CardLibrary.Where(c => !CoreVoyageSession.IsSpecialBattleCard(c.Id))
                        .ToDictionary(c => c.Id, c => 2));
                    session.StartVoyage();
                    if (scenario == "settlement_draw_chart") Assert.That(session.SafeReturn(), Is.True);
                    if (scenario == "battle_rebuild")
                    {
                        CoreMapNode boss = session.Map.Nodes.First(n => n.IsBoss);
                        typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(session, new object[] { boss.Encounter, true });
                    }
                    render();
                }
                yield return null; yield return null;
                int cardIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(i =>
                    CoreVoyageSession.CardLibrary[i].AllowedStages != NavalBattleStages.None &&
                    !CoreVoyageSession.IsSpecialBattleCard(CoreVoyageSession.CardLibrary[i].Id));
                for (int iteration = -warmups; iteration < samples; iteration++)
                {
                    if (scenario.StartsWith("market"))
                    {
                        var quantities = (Dictionary<PortMarketItem, int>)typeof(CoreGameplayDemoController)
                            .GetField("marketQuantities", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                        quantities.Clear(); render();
                        yield return OpenMarketTrade("wood");
                        MarketQuantityField("wood").value = 1;
                    }
                    else if (scenario.StartsWith("departure")) render();
                    else if (scenario == "settlement_draw_chart")
                    {
                        typeof(CoreGameplayDemoController).GetField("pendingDrawnChart", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, null);
                        render();
                    }
                    yield return null;
                    int operations = scenario.StartsWith("market") ? 39 : scenario.StartsWith("departure") || scenario == "navigation_distance_20_changes" ? 20 : 1;
                    string root = scenario.StartsWith("market") ? "Market Panel" : scenario.StartsWith("departure") ? "Voyage Setup" :
                        scenario.StartsWith("settlement") ? "Settlement Card" : scenario.StartsWith("battle") ? "Battle Card" : "Voyage Header";
                    var sample = new FlowPerformanceSample { scenario = scenario, sample = iteration, operations = operations, mapSeed = session.Map?.Seed };
                    for (int operation = 0; operation < operations; operation++)
                    {
                        Action action;
                        if (scenario.StartsWith("market"))
                        {
                            Slider slider = MarketQuantityField("wood");
                            float next = operation + 2;
                            action = () => slider.value = next;
                        }
                        else if (scenario.StartsWith("departure"))
                        {
                            Slider slider = GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>();
                            float next = operation % 2 == 0 ? slider.maxValue - 1 : slider.maxValue;
                            action = () => slider.value = next;
                        }
                        else if (scenario == "settlement_draw_chart") action = ActiveButton("Draw Sea Chart").onClick.Invoke;
                        else if (scenario == "navigation_distance_20_changes")
                        {
                            Slider slider = GameObject.Find("Distance Slider").GetComponent<Slider>();
                            float next = operation % 2 == 0 ? 20f : 170f;
                            action = () => slider.value = next;
                        }
                        else if (scenario == "upgraded_card_reads_10000") action = () =>
                        {
                            for (int i = 0; i < 10000; i++)
                                if (session.GetBattleCardAtLevel(cardIndex, 2).UpgradeLevel != 2) throw new InvalidOperationException("Wrong level.");
                        };
                        else action = render;
                        GameObject beforeRoot = GameObject.Find(root);
                        long ticks = Stopwatch.GetTimestamp();
                        action();
                        if (scenario != "upgraded_card_reads_10000") Canvas.ForceUpdateCanvases();
                        sample.milliseconds += (Stopwatch.GetTimestamp() - ticks) * 1000d / Stopwatch.Frequency;
                        if (GameObject.Find(root) != beforeRoot) sample.pageReplacements++;
                        yield return null;
                        if (allocationRecorder.Valid && sample.managedBytes >= 0) sample.managedBytes += allocationRecorder.LastValue;
                        else sample.managedBytes = -1;
                    }
                    if (scenario.StartsWith("market"))
                        Assert.That(MarketQuantityField("wood").value, Is.EqualTo(40));
                    if (scenario.StartsWith("departure"))
                        Assert.That(GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>().value,
                            Is.EqualTo(session.CrewCountValue - session.SelectedSpecialCrewCount));
                    if (scenario == "settlement_draw_chart")
                        Assert.That(ActiveButton("Draw Sea Chart").interactable, Is.False);
                    if (scenario == "navigation_distance_20_changes")
                        Assert.That(session.PlannedDistanceNm, Is.EqualTo(170));
                    GameObject map = GameObject.Find("Map Layer");
                    sample.mapGraphics = map == null ? 0 : map.GetComponentsInChildren<Graphic>().Length;
                    if (iteration >= 0) report.samples.Add(sample);
                }
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                if (scenario.StartsWith("navigation"))
                    NavalBattleVisualCapture.Capture(controller, scenario + ".png", "Measured navigation fixture", "Navigation Chart");
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
