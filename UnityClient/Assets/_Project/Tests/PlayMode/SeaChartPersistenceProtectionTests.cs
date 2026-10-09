using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class SeaChartPersistenceProtectionTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
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
        private string checkpointDirectory;
        private FieldInfo checkpointDirectoryOverride;
        private object previousCheckpointDirectory;

        [SetUp]
        public void IsolateSeaChartRecords()
        {
            Assert.That(Application.isPlaying, Is.True);
            foreach (string key in Keys)
            {
                if (PlayerPrefs.HasKey(key)) previous.Add(key, PlayerPrefs.GetString(key));
                PlayerPrefs.DeleteKey(key);
            }
            host = new GameObject("Inactive Sea Chart Persistence Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null);
            session = new CoreVoyageSession();
            typeof(CoreGameplayDemoController).GetField("session", InstanceFlags).SetValue(controller, session);
        }

        [UnityTearDown]
        public IEnumerator RestoreSeaChartRecords()
        {
            if (host != null) Object.Destroy(host);
            yield return null;
            if (checkpointDirectoryOverride != null)
                checkpointDirectoryOverride.SetValue(null, previousCheckpointDirectory);
            if (!string.IsNullOrEmpty(checkpointDirectory) && Directory.Exists(checkpointDirectory))
            {
                string resolved = Path.GetFullPath(checkpointDirectory);
                Assert.That(resolved.StartsWith(Path.Combine(Path.GetTempPath(), "ShatteredSailChartProtection-"), StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(resolved, true);
            }
            checkpointDirectory = null;
            checkpointDirectoryOverride = null;
            foreach (string key in Keys)
            {
                if (previous.TryGetValue(key, out string value)) PlayerPrefs.SetString(key, value);
                else PlayerPrefs.DeleteKey(key);
            }
            previous.Clear();
            PlayerPrefs.Save();
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase("")]
        [TestCase("{broken")]
        [TestCase("{\"version\":2,\"charts\":[],\"aliases\":[]}")]
        [TestCase("{\"version\":1,\"charts\":[null],\"aliases\":[]}")]
        public void UnreadableProgressSurvivesAChangedVoyageRevision(string original)
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.SeaChartProgressKey, original);
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, "{\"version\":1,\"charts\":[]}");
            Invoke("RestoreSeaChartProgress");
            AssertProtected(true);
            int before = session.SeaChartProgressRevision;

            StartChartVoyage();
            Assert.That(session.SeaChartProgressRevision, Is.GreaterThan(before));
            Assert.That((bool)Invoke("QueueSeaChartProgress"), Is.False,
                "The profile's deferred write must preserve unreadable chart data too.");
            Invoke("SaveSeaChartProgress");

            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey), Is.EqualTo(original),
                "A later voyage must not replace unreadable depletion and boss records with a fresh chart.");
        }

        [Test]
        public void QueuedVoyageProgressRoundTripsAndAnUnchangedRevisionDoesNotRequeue()
        {
            Invoke("RestoreSeaChartProgress");
            StartChartVoyage();

            Assert.That((bool)Invoke("QueueSeaChartProgress"), Is.True);
            string queued = PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey);
            var restored = new CoreVoyageSession();
            Assert.That(restored.RestoreSeaChartProgress(JsonUtility.FromJson<CoreSeaChartProgress>(queued)), Is.True);
            CoreSeaChartProgress progress = restored.CaptureSeaChartProgress();
            Assert.That(progress.charts.Length, Is.EqualTo(1));
            Assert.That(progress.charts[0].seed, Is.EqualTo(session.CurrentChartSeed));
            Assert.That(progress.charts[0].visits, Is.EqualTo(1));
            Assert.That((bool)Invoke("QueueSeaChartProgress"), Is.False);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey), Is.EqualTo(queued));
            // This is an in-process cache/round-trip check. The standalone
            // process-kill audit verifies the actual durable commit boundary.
        }

        [TestCase("")]
        [TestCase("{broken")]
        [TestCase("{\"version\":2,\"charts\":[]}")]
        [TestCase("{\"version\":1,\"charts\":[null]}")]
        public void UnreadableArchiveCannotBeOverwrittenByTheNextSave(string original)
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, original);
            Invoke("RestoreSeaChartProgress");
            AssertProtected(true);

            Invoke("SaveRecordedSeaCharts");

            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey), Is.EqualTo(original));
        }

        [Test]
        public void OneInvalidArchiveEntryRejectsTheWholeArchiveWithoutExposingAPartialList()
        {
            string original = JsonUtility.ToJson(new Archive
            {
                // The valid final record was loaded first by the old reverse loop,
                // causing an invalid first record to disappear on the next save.
                charts = new[] { new Chart { seed = "invalid-chart" }, ValidChart() },
            });
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, original);

            Invoke("RestoreSeaChartProgress");

            AssertProtected(true);
            IList loaded = (IList)typeof(CoreGameplayDemoController).GetField("recordedDepartureCharts", InstanceFlags).GetValue(controller);
            Assert.That(loaded.Count, Is.Zero);
            Invoke("SaveRecordedSeaCharts");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey), Is.EqualTo(original));
        }

        [Test]
        public void ExplicitNewGameResetRemovesProtectionAndAllowsNewProgress()
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.SeaChartProgressKey, "{broken-progress");
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, "{broken-archive");
            Invoke("RestoreSeaChartProgress");
            AssertProtected(true);

            // MainMenu's explicit new-game path removes these keys before loading gameplay.
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            Invoke("RestoreSeaChartProgress");
            AssertProtected(false);
            StartChartVoyage();
            Invoke("SaveSeaChartProgress");

            CoreSeaChartProgress saved = JsonUtility.FromJson<CoreSeaChartProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey));
            Assert.That(saved.charts.Length, Is.EqualTo(1));
            Assert.That(saved.charts[0].visits, Is.EqualTo(1));
            Archive archive = JsonUtility.FromJson<Archive>(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey));
            Assert.That(archive.version, Is.EqualTo(1));
            Assert.That(archive.charts, Is.Empty);
        }

        [Test]
        public void ReloadingARepairedArchiveRestoresItsEntriesAndClearsProtection()
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, "{broken");
            Invoke("RestoreSeaChartProgress");
            AssertProtected(true);
            Chart chart = ValidChart();
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, JsonUtility.ToJson(new Archive { charts = new[] { chart } }));

            Invoke("RestoreSeaChartProgress");

            AssertProtected(false);
            Invoke("SaveRecordedSeaCharts");
            Archive saved = JsonUtility.FromJson<Archive>(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey));
            Assert.That(saved.charts.Length, Is.EqualTo(1));
            Assert.That(saved.charts[0].seed, Is.EqualTo(chart.seed));
            Assert.That(saved.charts[0].exploredCells[0], Is.True);
            Assert.That(saved.charts[0].revealedLocationIds, Is.EqualTo(new[] { 0, 2 }));
        }

        [TestCase(CoreGameplayDemoController.SeaChartProgressKey)]
        [TestCase(CoreGameplayDemoController.RecordedSeaChartsKey)]
        public void UnreadableChartDataBlocksSuspensionAndResumeWithoutTouchingTheParkedSnapshot(string damagedKey)
        {
            Type persistence = typeof(CoreGameplayDemoController).Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true);
            checkpointDirectoryOverride = persistence.GetField("directoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            previousCheckpointDirectory = checkpointDirectoryOverride.GetValue(null);
            checkpointDirectory = Path.Combine(Path.GetTempPath(), "ShatteredSailChartProtection-" + Guid.NewGuid().ToString("N"));
            checkpointDirectoryOverride.SetValue(null, checkpointDirectory);
            Invoke("RestoreSeaChartProgress");
            StartChartVoyage();
            CoreVoyageSession parked = session;
            object[] saveArgs = { null };
            Assert.That((bool)Invoke("TrySaveVoyageSuspension", saveArgs), Is.True, (string)saveArgs[0]);
            string primary = Path.Combine(checkpointDirectory, "voyage.checkpoint");
            byte[] before = File.ReadAllBytes(primary), backup = File.ReadAllBytes(primary + ".bak1");
            string voyageBefore = JsonUtility.ToJson(parked.CaptureVoyageCheckpoint());
            object[] readArgs = { null, null };
            Assert.That((bool)persistence.GetMethod("TryRead", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, readArgs), Is.True);
            Assert.That(readArgs[0], Is.Not.Null);

            PlayerPrefs.SetString(damagedKey, "{broken-chart-record");
            session = new CoreVoyageSession();
            typeof(CoreGameplayDemoController).GetField("session", InstanceFlags).SetValue(controller, session);
            Invoke("RestoreSeaChartProgress");
            AssertProtected(true);
            object[] restoreArgs = { readArgs[0], null };
            Assert.That((bool)Invoke("TryRestoreSuspendedVoyage", restoreArgs), Is.False);
            Assert.That((string)restoreArgs[1], Does.Contain("原档案已保留"));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.Map, Is.Null);

            session = parked;
            typeof(CoreGameplayDemoController).GetField("session", InstanceFlags).SetValue(controller, parked);
            foreach (string method in new[] { "TrySaveVoyageSuspension", "TrySaveAndReturnToMainMenu" })
            {
                object[] args = { null };
                Assert.That((bool)Invoke(method, args), Is.False, method);
                Assert.That((string)args[0], Does.Contain("原档案已保留"));
            }
            Assert.That(File.ReadAllBytes(primary), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(primary + ".bak1"), Is.EqualTo(backup));
            Assert.That(File.Exists(primary + ".cleared"), Is.False, "Rejected resume must not consume the parked voyage.");
            Assert.That(JsonUtility.ToJson(parked.CaptureVoyageCheckpoint()), Is.EqualTo(voyageBefore));
            Assert.That(PlayerPrefs.GetString(damagedKey), Is.EqualTo("{broken-chart-record"));
        }

        private void StartChartVoyage()
        {
            Assert.That(session.TrySelectChartSeed(CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港"), out string reason), Is.True, reason);
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
        }

        private static Chart ValidChart()
        {
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            Assert.That(CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition definition), Is.True);
            var explored = new bool[CoreVoyageMap.GridColumns * CoreVoyageMap.GridRows];
            explored[0] = true;
            return new Chart { seed = seed, routeIndex = definition.RouteIndex, routeName = "test",
                departurePort = definition.ForwardDeparturePort, destinationPort = definition.ForwardDestinationPort,
                tutorialWaters = definition.TutorialWaters, exploredCells = explored, revealedLocationIds = new[] { 0, 2 } };
        }

        private void AssertProtected(bool expected)
        {
            object[] args = { null };
            bool blocked = (bool)Invoke("TryGetSeaChartSaveProtectionReason", args);
            Assert.That(blocked, Is.EqualTo(expected));
            if (expected) Assert.That((string)args[0], Does.Contain("原档案已保留"));
            else Assert.That(args[0], Is.Null);
        }

        private object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, InstanceFlags);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(controller, arguments);
        }

        [Serializable] private sealed class Archive { public int version = 1; public Chart[] charts; }
        [Serializable] private sealed class Chart
        {
            public string seed, routeName, departurePort, destinationPort;
            public int routeIndex;
            public bool tutorialWaters;
            public bool[] exploredCells;
            public int[] revealedLocationIds;
        }
    }

    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator SeaChartProtection_DepartureButtonRejectsUnreadableProgressBeforeSpending()
        {
            foreach (string damagedKey in new[] { CoreGameplayDemoController.SeaChartProgressKey, CoreGameplayDemoController.RecordedSeaChartsKey })
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.SeaChartProgressKey);
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.RecordedSeaChartsKey);
                PlayerPrefs.SetString(damagedKey, "{broken-chart-record");
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                session.Cargo.Water = session.Cargo.Food = 80;
                ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
                InvestmentResourceSnapshot before = SnapshotInvestmentResources(session);
                int carriedGold = session.Cargo.Gold;
                int revision = session.SeaChartProgressRevision;

                ClickAcceptanceButton("Start Voyage"); yield return null;

                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(session.Map, Is.Null);
                AssertInvestmentResourcesEqual(session, before);
                Assert.That(session.Cargo.Gold, Is.EqualTo(carriedGold));
                Assert.That(session.SeaChartProgressRevision, Is.EqualTo(revision));
                Assert.That(PlayerPrefs.GetString(damagedKey), Is.EqualTo("{broken-chart-record"));
                Assert.That((string)typeof(CoreGameplayDemoController).GetField("portNotice", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller),
                    Does.Contain("原档案已保留"));
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
