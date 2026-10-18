using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ChartDetailPersistenceTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] Keys =
        {
            CoreGameplayDemoController.SeaChartProgressKey,
            CoreGameplayDemoController.RecordedSeaChartsKey,
            CoreGameplayDemoController.DrawnSeaChartKey,
        };
        private readonly Dictionary<string, string> previous = new Dictionary<string, string>();
        private GameObject host;
        private CoreGameplayDemoController controller;
        private CoreVoyageSession session;

        [SetUp]
        public void IsolateRecords()
        {
            Assert.That(Application.isPlaying, Is.True);
            foreach (string key in Keys)
            {
                if (PlayerPrefs.HasKey(key)) previous.Add(key, PlayerPrefs.GetString(key));
                PlayerPrefs.DeleteKey(key);
            }
            host = new GameObject("Inactive Detailed Chart Persistence Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            session = new CoreVoyageSession();
            typeof(CoreGameplayDemoController).GetField("session", PrivateInstance).SetValue(controller, session);
        }

        [UnityTearDown]
        public IEnumerator RestoreRecords()
        {
            if (host != null) Object.Destroy(host);
            yield return null;
            foreach (string key in Keys)
            {
                if (previous.TryGetValue(key, out string value)) PlayerPrefs.SetString(key, value);
                else PlayerPrefs.DeleteKey(key);
            }
            previous.Clear();
            PlayerPrefs.Save();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DetailedArchiveMergesAndReloadsExactSubcellsBeforeTheNextVoyage()
        {
            Chart first = MakeChart();
            Chart second = JsonUtility.FromJson<Chart>(JsonUtility.ToJson(first));
            var geometry = new CoreVoyageMap(first.mapGeometryVersion);
            first.chartExploredCells = new bool[geometry.FineColumns * geometry.FineRows];
            second.chartExploredCells = new bool[first.chartExploredCells.Length];
            first.chartExploredCells[0] = true;
            int addedRowCell = (geometry.FineRows - 1) * geometry.FineColumns + 1;
            second.chartExploredCells[addedRowCell] = true;
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey,
                JsonUtility.ToJson(new Archive { charts = new[] { first, second } }));
            Invoke("RestoreSeaChartProgress");
            Invoke("SaveRecordedSeaCharts");
            Archive merged = JsonUtility.FromJson<Archive>(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey));
            Assert.That(merged.charts.Length, Is.EqualTo(1));
            Assert.That(merged.charts[0].chartExploredCells.Count(value => value), Is.EqualTo(2));
            Assert.That(merged.charts[0].chartExploredCells[0], Is.True);
            Assert.That(merged.charts[0].chartExploredCells[addedRowCell], Is.True);
            Assert.That(merged.charts[0].mapGeometryVersion, Is.EqualTo(1));
            Invoke("RestoreSeaChartProgress");

            Assert.That(session.TrySelectChartSeed(first.seed, out string reason), Is.True, reason);
            session.StartVoyage();
            Array.Clear(session.Map.ExploredCells, 0, session.Map.ExploredCells.Length);
            session.Map.RestoreChartExploration(new bool[first.chartExploredCells.Length]);
            Invoke("ApplyRecordedSeaChartKnowledge");
            Assert.That(session.Map.ChartExploredCells, Is.EqualTo(merged.charts[0].chartExploredCells));
            Assert.That(session.Map.ExploredCells[0], Is.True);
        }

        [Test]
        public void LegacyArchiveExpandsItsCoarseCellsWithoutLosingKnowledge()
        {
            Chart legacy = MakeChart(0);
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey,
                JsonUtility.ToJson(new Archive { charts = new[] { legacy } }).Replace("\"mapGeometryVersion\":0,", string.Empty));
            Invoke("RestoreSeaChartProgress");
            Invoke("SaveRecordedSeaCharts");
            Archive saved = JsonUtility.FromJson<Archive>(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey));
            Assert.That(saved.charts[0].chartExploredCells,
                Is.EqualTo(CoreVoyageMap.ExpandExplorationForChart(legacy.exploredCells)));
            Assert.That(saved.charts[0].chartExploredCells.Count(value => value), Is.EqualTo(4));
            Assert.That(saved.charts[0].mapGeometryVersion, Is.Zero);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ArchivedKnownLocationsAreImportedBeforeNavigatorDepartureBonus(int navigators)
        {
            Chart chart = MakeChart();
            var previousVoyage = new CoreVoyageSession();
            previousVoyage.ConfigureCrewCapabilities(1, false, false, false, false, false);
            Assert.That(previousVoyage.TrySelectChartSeed(chart.seed, out string reason), Is.True, reason);
            previousVoyage.StartVoyage();
            chart.revealedLocationIds = previousVoyage.Map.Nodes.Where(node => node.IsChartVisible).Select(node => node.Id).ToArray();
            chart.exploredCells = (bool[])previousVoyage.Map.ExploredCells.Clone();
            chart.chartExploredCells = previousVoyage.Map.CaptureChartExploration();
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey,
                JsonUtility.ToJson(new Archive { charts = new[] { chart } }));
            Invoke("RestoreSeaChartProgress");
            session.ConfigureCrewCapabilities(navigators, false, false, false, false, false);
            Assert.That(session.TrySelectChartSeed(chart.seed, out reason), Is.True, reason);
            var known = (IReadOnlyList<int>)Invoke("RecordedDepartureChartLocationIds");
            Assert.That(known, Is.EquivalentTo(chart.revealedLocationIds));
            session.StartVoyage(session.SelectedDestinationPort, known);
            Invoke("ApplyRecordedSeaChartKnowledge");
            Assert.That(session.Map.Nodes.Count(node => node.IsChartVisible && !chart.revealedLocationIds.Contains(node.Id)),
                Is.EqualTo(navigators * 2));
            Assert.That(session.Map.Nodes.Any(node => node.Visited), Is.False);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(session.CaptureVoyageCheckpoint(), out reason), Is.True, reason);
            Assert.That(restored.Map.Nodes.Select(node => node.IsChartVisible), Is.EqualTo(session.Map.Nodes.Select(node => node.IsChartVisible)));
        }

        [Test]
        public void MalformedDetailedArchiveRemainsProtectedFromOverwrite()
        {
            Chart invalid = MakeChart();
            invalid.chartExploredCells = new bool[3];
            string original = JsonUtility.ToJson(new Archive { charts = new[] { invalid } });
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, original);
            Invoke("RestoreSeaChartProgress");
            object[] arguments = { null };
            Assert.That((bool)Invoke("TryGetSeaChartSaveProtectionReason", arguments), Is.True);
            Invoke("SaveRecordedSeaCharts");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey), Is.EqualTo(original));
        }

        [Test]
        public void ConfirmedDrawingCarriesItsDetailedCellsIntoTheArchive()
        {
            Chart drawing = MakeChart();
            var geometry = new CoreVoyageMap(drawing.mapGeometryVersion);
            drawing.chartExploredCells = new bool[geometry.FineColumns * geometry.FineRows];
            drawing.chartExploredCells[1] = true;
            Type drawnType = typeof(CoreGameplayDemoController).GetNestedType("DrawnSeaChartRecord", BindingFlags.NonPublic);
            object drawn = JsonUtility.FromJson(JsonUtility.ToJson(drawing), drawnType);
            Invoke("RecordConfirmedSeaChart", drawn);
            Archive saved = JsonUtility.FromJson<Archive>(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey));
            Assert.That(saved.charts[0].chartExploredCells, Is.EqualTo(drawing.chartExploredCells));
            Assert.That(saved.charts[0].chartExploredCells.Count(value => value), Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GeometryMismatchPreservesTheEntireArchive(bool mismatchedVersion)
        {
            Chart invalid = MakeChart();
            if (mismatchedVersion) invalid.mapGeometryVersion = 0;
            else invalid.exploredCells = new bool[CoreVoyageMap.GridColumns * CoreVoyageMap.GridRows];
            string original = JsonUtility.ToJson(new Archive { charts = new[] { MakeChart(0), invalid } });
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, original);
            Invoke("RestoreSeaChartProgress");
            object[] arguments = { null };
            Assert.That((bool)Invoke("TryGetSeaChartSaveProtectionReason", arguments), Is.True);
            Invoke("SaveRecordedSeaCharts");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey), Is.EqualTo(original));
        }

        private static Chart MakeChart(int geometryVersion = 1)
        {
            string seed = geometryVersion == 0 ? "SS2.0.0.1.0.0.2EDC24E9.0000C0DE.4DD97F4E" :
                "SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6";
            Assert.That(CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition definition), Is.True);
            var geometry = new CoreVoyageMap(definition.MapGeometryVersion);
            var coarse = new bool[geometry.Columns * geometry.Rows];
            coarse[0] = true;
            return new Chart { seed = seed, routeIndex = definition.RouteIndex, routeName = "test",
                mapGeometryVersion = definition.MapGeometryVersion,
                departurePort = definition.ForwardDeparturePort, destinationPort = definition.ForwardDestinationPort,
                tutorialWaters = definition.TutorialWaters, exploredCells = coarse, revealedLocationIds = Array.Empty<int>() };
        }

        private object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, PrivateInstance);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(controller, arguments);
        }

        [Serializable] private sealed class Archive { public int version = 1; public Chart[] charts; }
        [Serializable] private sealed class Chart
        {
            public int version = 1;
            public string seed, routeName, departurePort, destinationPort;
            public int routeIndex;
            public int mapGeometryVersion;
            public bool tutorialWaters;
            public bool[] exploredCells, chartExploredCells;
            public int[] revealedLocationIds;
        }
    }
}
