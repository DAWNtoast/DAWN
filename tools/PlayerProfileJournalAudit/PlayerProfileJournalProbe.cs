using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShatteredSail.ProfileJournalAudit
{
    // Diagnostic build only. Run.ps1 injects a gated call into its isolated source copy.
    public sealed class PlayerProfileJournalProbe : MonoBehaviour
    {
        private const string ChartSeed = "SS2.0.0.1.0.0.2EDC24E9.0000C0DE.4DD97F4E";
        private const string TreasureSeed = "PROFILE-COMMIT-FIXTURE-V1";
        private static PlayerProfileJournalProbe instance;
        private readonly StageReport report = new StageReport();
        private string root, reportPath;
        private bool reserved, finished;
        private double startedAt;

        [Serializable] private sealed class StageReport
        {
            public string mode, status = "running", error, utc, completedUtc;
            public string company, product, persistentDataPath, checkpointDirectory, journalDirectory, recoveryNotice;
            public int pid;
            public string scope = "Windows isolated Player process interruption experiment for one SaveProgress journal. " +
                "Seeded profile and explicit reward fixture; not earned progression. Raw preferences may be mixed " +
                "before startup recovery. Recovery uses the production ProfileSaveJournal before any scene Awake.";
            public string stopSite;
            public Snapshot initialDisk, beforeCommitDisk, expectedSession, stoppedPlayerPrefsCache, afterComplete, afterRecovery;
        }

        [Serializable] private sealed class Snapshot
        {
            public string protectedValues;
            public string source, phase, gold, decimalGoldRaw, chartJson, treasureJson, chartArchiveJson, drawnChartJson;
            public bool decimalGoldExists, chartExists, treasureExists;
            public int legacyGold, water, food, wood, iron, cloth, powder, cannonballs, beer, medicine, cargoGold, fame;
            public int chartRevision = -1, savedChartRevision = -1, chartCount, chartVisits, fragments, pirateExperience, treasureMaps;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string mode = Argument("-profileJournalMode");
            if (string.IsNullOrEmpty(mode)) return;
            var host = new GameObject("Isolated Profile Commit Audit");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<PlayerProfileJournalProbe>();
            try { instance.Initialize(mode); }
            catch (Exception error) { instance.Fail(error); }
        }

        private void Initialize(string mode)
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            report.mode = mode; report.utc = DateTime.UtcNow.ToString("O");
            report.pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            report.company = Application.companyName; report.product = Application.productName;
            report.persistentDataPath = Application.persistentDataPath;
            string requestedRoot = Argument("-profileJournalRoot"), requestedReport = Argument("-profileJournalReport");
            if (string.IsNullOrEmpty(requestedRoot) || string.IsNullOrEmpty(requestedReport) ||
                !Path.IsPathRooted(requestedRoot) || !Path.IsPathRooted(requestedReport))
                throw new ArgumentException("Absolute -profileJournalRoot and -profileJournalReport are required.");
            root = Path.GetFullPath(requestedRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            reportPath = Path.GetFullPath(requestedReport);
            if (!reportPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(reportPath), ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Report must be a new JSON file inside this audit root.");
            Directory.CreateDirectory(root);
            using (new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
            reserved = true;
            if (Application.isEditor || Application.companyName != "ShatteredSailAudit" || Application.productName != "Optimization20261008")
                throw new InvalidOperationException("Refusing profile audit outside the dedicated audit Player identity.");
            if (mode != "seed" && mode != "crash" && mode != "inspect" && mode != "complete" && mode != "recover" && mode != "recover_crash")
                throw new ArgumentException("Unknown profile audit mode: " + mode);
            Environment.SetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED", null);
            Type journal = JournalType();
            FieldInfo journalDirectory = journal.GetField("directoryOverride", BindingFlags.NonPublic | BindingFlags.Static);
            if (journalDirectory == null) throw new MissingFieldException(journal.FullName, "directoryOverride");
            report.journalDirectory = Path.Combine(root, "profile-journal");
            journalDirectory.SetValue(null, report.journalDirectory);
            report.stopSite = Argument("-profileJournalStopPoint") ?? "none";
            report.initialDisk = ReadPreferences("BeforeSceneLoad: untouched process startup PlayerPrefs");
            if (mode == "recover" || mode == "recover_crash")
            {
                if (mode == "recover_crash") Environment.SetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED", "1");
                JournalCall("TryRecover");
                Environment.SetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED", null);
                if (mode == "recover_crash") throw new InvalidOperationException("Recovery stop hook was not reached.");
                report.afterRecovery = ReadPreferences("after production startup journal recovery");
                Complete();
                return;
            }
            if (mode == "inspect")
            {
                // Exit the audit-owned process directly: no scene Awake or Unity
                // OnApplicationQuit handlers may write after this read-only snapshot.
                Complete();
                return;
            }
            Type persistence = typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true);
            FieldInfo directory = persistence.GetField("directoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            if (directory == null) throw new MissingFieldException(persistence.FullName, "directoryOverride");
            report.checkpointDirectory = Path.Combine(root, "checkpoints-" + Path.GetFileNameWithoutExtension(reportPath));
            if (Directory.Exists(report.checkpointDirectory)) throw new IOException("Checkpoint fixture directory already exists.");
            directory.SetValue(null, report.checkpointDirectory);
            if (mode == "seed")
            {
                JournalCall("TryReset");
                PlayerPrefs.DeleteAll();
                PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
                PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
                PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
                PlayerPrefs.Save();
            }
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Guarded(Run()));
        }

        private IEnumerator Run()
        {
            yield return null;
            SceneManager.LoadScene("CoreGameplayDemo");
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            if (controller == null || controller.Session == null || controller.IsTutorialActive)
                throw new InvalidOperationException("Normal non-tutorial Core session failed to initialize.");
            CoreVoyageSession session = controller.Session;
            MethodInfo save = typeof(CoreGameplayDemoController).GetMethod("SaveProgress", BindingFlags.NonPublic | BindingFlags.Instance);
            if (save == null) throw new MissingMethodException("CoreGameplayDemoController.SaveProgress");
            if (report.mode == "seed")
            {
                session.RestoreProfile(1000m, "断锚港");
                session.Cargo.Water = session.Cargo.Food = 120;
                session.Cargo.Wood = 10; session.Cargo.Iron = 5; session.Cargo.Cloth = 5;
                session.Cargo.Beer = 3; session.Cargo.Medicine = 3; session.Cargo.Powder = 5; session.Cargo.Cannonballs = 10;
                session.Cargo.Gold = 0; session.Cargo.Fame = 0;
                session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = 8 });
                if (!session.RestoreSeaChartProgress(new CoreSeaChartProgress())) throw new InvalidOperationException("Empty chart fixture refused.");
                report.expectedSession = ReadSession(controller);
                save.Invoke(controller, null);
                report.afterComplete = ReadPreferences("seed: SaveProgress returned after final PlayerPrefs.Save");
                AssertExpectedGroup(report.expectedSession, report.afterComplete);
                Complete();
                yield break;
            }
            report.beforeCommitDisk = ReadPreferences("loaded Core session, immediately before fixture mutations");
            if (session.ProfileGold != 1000m || session.Cargo.Water != 120 || session.Cargo.Food != 120 ||
                session.TreasureFragments != 8 || session.TreasureMaps.Count != 0 || session.CaptureSeaChartProgress().charts.Length != 0)
                throw new InvalidOperationException("Mutation must start from the independently committed seed profile.");
            if (!session.TryCraftTreasureMap(TreasureSeed, out string reason)) throw new InvalidOperationException(reason);
            session.ClearSelectedTreasureMap();
            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            if (!session.TrySelectChartSeed(ChartSeed, out reason)) throw new InvalidOperationException(reason);
            session.StartVoyage();
            if (!session.SafeReturn()) throw new InvalidOperationException("Safe return fixture refused.");
            session.ReturnToPortAfterSettlement();
            if (session.Phase != CoreDemoPhase.Port) throw new InvalidOperationException("Return settlement did not complete.");
            session.GrantProfileGold(137, "Explicit profile commit audit reward fixture");
            report.expectedSession = ReadSession(controller);
            if (report.expectedSession.chartRevision == report.expectedSession.savedChartRevision ||
                report.expectedSession.chartVisits == report.beforeCommitDisk.chartVisits ||
                report.expectedSession.gold == report.beforeCommitDisk.gold || report.expectedSession.fragments == report.beforeCommitDisk.fragments ||
                report.expectedSession.water == report.beforeCommitDisk.water)
                throw new InvalidOperationException("Fixture must leave chart, treasure, gold and cargo changes pending together.");
            WriteReport();
            if (report.mode == "crash")
            {
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-profileJournalStopPoint") < 0)
                    throw new InvalidOperationException("Crash mode requires the explicit source-hook gate.");
                Environment.SetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED", "1");
            }
            // The runner's hook must pause crash mode during this exact call.
            save.Invoke(controller, null);
            Environment.SetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED", null);
            if (report.mode == "crash") throw new InvalidOperationException("Audit stop hook was not reached.");
            report.afterComplete = ReadPreferences("complete: SaveProgress returned after final PlayerPrefs.Save");
            AssertExpectedGroup(report.expectedSession, report.afterComplete);
            Complete();
        }

        // Reached by reflection from the command-line/environment-gated hook in
        // the isolated Presentation source, avoiding an assembly reference cycle.
        [UnityEngine.Scripting.Preserve]
        public static void RecordStopAndWait(string point)
        {
            PlayerProfileJournalProbe probe = instance;
            if (probe == null || Argument("-profileJournalStopPoint") != point ||
                Environment.GetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED") != "1") return;
            // Stop only after replay actually changed a protected value, so
            // the fourth case exercises an interrupted restoration, not an
            // unchanged prefix of the sorted key list.
            if (point == "during_recovery" && CaptureProtectedValues() == probe.report.initialDisk.protectedValues) return;
            if (probe == null || (probe.report.mode != "crash" && probe.report.mode != "recover_crash") || !probe.reserved ||
                Application.companyName != "ShatteredSailAudit" || Application.productName != "Optimization20261008" ||
                Environment.GetEnvironmentVariable("SHATTEREDSAIL_PROFILE_JOURNAL_ARMED") != "1")
                throw new InvalidOperationException("Profile stop hook is not armed for the dedicated audit process.");
            probe.report.stopSite = point;
            probe.report.stoppedPlayerPrefsCache = ReadPreferences(probe.report.stopSite + "; cache observation only, independently verify disk after kill");
            probe.report.status = "waiting_for_runner_kill";
            probe.WriteReport();
            string marker = Path.Combine(probe.root, Path.GetFileNameWithoutExtension(probe.reportPath) + ".stop");
            string pendingMarker = marker + ".pending";
            using (var stream = new FileStream(pendingMarker, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("pid=" + probe.report.pid);
                writer.WriteLine("stop=" + probe.report.stopSite);
                writer.WriteLine("utc=" + DateTime.UtcNow.ToString("O"));
                writer.Flush(); stream.Flush(true);
            }
            File.Move(pendingMarker, marker);
            // Only the runner's recorded child PID may be killed. Blocking this
            // thread prevents later saves, Render, OnDisable or orderly shutdown.
            while (true) Thread.Sleep(1000);
        }

        private static Snapshot ReadPreferences(string source)
        {
            var value = new Snapshot { source = source, phase = "raw PlayerPrefs (no Core restore)",
                gold = ProfileGoldPersistence.Load().ToString(CultureInfo.InvariantCulture),
                decimalGoldExists = PlayerPrefs.HasKey(ProfileGoldPersistence.DecimalKey),
                decimalGoldRaw = PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey, string.Empty),
                legacyGold = PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey, -1),
                chartExists = PlayerPrefs.HasKey(CoreGameplayDemoController.SeaChartProgressKey),
                chartJson = PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey, string.Empty),
                treasureExists = PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey),
                treasureJson = PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey, string.Empty),
                chartArchiveJson = PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, string.Empty),
                drawnChartJson = PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey, string.Empty),
                water = Cargo("Water"), food = Cargo("Food"), wood = Cargo("Wood"), iron = Cargo("Iron"), cloth = Cargo("Cloth"),
                powder = Cargo("Powder"), cannonballs = Cargo("Cannonballs"), beer = Cargo("Beer"), medicine = Cargo("Medicine"),
                cargoGold = Cargo("Gold"), fame = Cargo("Fame") };
            value.protectedValues = CaptureProtectedValues();
            FillDerived(value);
            return value;
        }
        private static int Cargo(string key) => PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey(key), -1);
        private static Snapshot ReadSession(CoreGameplayDemoController controller)
        {
            CoreVoyageSession session = controller.Session;
            var value = new Snapshot { source = "actual in-memory Core session after public operations", phase = session.Phase.ToString(),
                gold = session.ProfileGold.ToString(CultureInfo.InvariantCulture), legacyGold = (int)decimal.Floor(session.ProfileGold),
                chartJson = JsonUtility.ToJson(session.CaptureSeaChartProgress()), treasureJson = JsonUtility.ToJson(session.CaptureTreasureProgress()),
                chartRevision = session.SeaChartProgressRevision,
                savedChartRevision = (int)typeof(CoreGameplayDemoController).GetField("savedSeaChartRevision", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller),
                water = session.Cargo.Water, food = session.Cargo.Food, wood = session.Cargo.Wood, iron = session.Cargo.Iron, cloth = session.Cargo.Cloth,
                powder = session.Cargo.Powder, cannonballs = session.Cargo.Cannonballs, beer = session.Cargo.Beer, medicine = session.Cargo.Medicine,
                cargoGold = session.Cargo.Gold, fame = session.Cargo.Fame };
            FillDerived(value);
            return value;
        }
        private static void FillDerived(Snapshot value)
        {
            CoreSeaChartProgress charts = string.IsNullOrEmpty(value.chartJson) ? null : JsonUtility.FromJson<CoreSeaChartProgress>(value.chartJson);
            CoreTreasureProgress treasure = string.IsNullOrEmpty(value.treasureJson) ? null : JsonUtility.FromJson<CoreTreasureProgress>(value.treasureJson);
            value.chartCount = charts?.charts?.Length ?? 0;
            value.chartVisits = charts?.charts?.Sum(chart => chart.visits) ?? 0;
            value.fragments = treasure?.fragments ?? 0; value.pirateExperience = treasure?.pirateExperience ?? 0;
            value.treasureMaps = treasure?.maps?.Length ?? 0;
        }
        private static void AssertExpectedGroup(Snapshot expected, Snapshot actual)
        {
            if (expected.gold != actual.gold || expected.legacyGold != actual.legacyGold || expected.water != actual.water ||
                expected.food != actual.food || expected.cargoGold != actual.cargoGold || expected.chartJson != actual.chartJson || expected.treasureJson != actual.treasureJson)
                throw new InvalidOperationException("Complete SaveProgress did not persist the expected fixture group.");
        }
        private static Type JournalType() => typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation.ProfileSaveJournal", true);
        private static string CaptureProtectedValues()
        {
            // Reuse the production typed capture descriptors; never guess a
            // string/integer type from registry formatting or native hash names.
            Type type = JournalType();
            Array specs = (Array)type.GetField("Specs", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            MethodInfo capture = type.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static);
            var entries = new string[specs.Length];
            for (int i = 0; i < specs.Length; i++) entries[i] = JsonUtility.ToJson(capture.Invoke(null, new object[] { specs.GetValue(i) }));
            return "[" + string.Join(",", entries) + "]";
        }
        private void JournalCall(string method)
        {
            object[] parameters = { null };
            MethodInfo operation = JournalType().GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (operation == null) throw new MissingMethodException(JournalType().FullName, method);
            bool success = (bool)operation.Invoke(null, parameters);
            report.recoveryNotice = parameters[0] as string;
            if (!success) throw new IOException(method + " failed: " + report.recoveryNotice);
        }
        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, key);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
        private IEnumerator Guarded(IEnumerator routine)
        {
            while (!finished)
            {
                object next;
                try { if (!routine.MoveNext()) yield break; next = routine.Current; }
                catch (Exception error) { Fail(error); yield break; }
                yield return next;
            }
        }
        private void Update() { if (!finished && Time.realtimeSinceStartupAsDouble - startedAt > 45) Fail(new TimeoutException("Profile audit stage exceeded 45 seconds.")); }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Fail(new InvalidOperationException(message + "\n" + stack));
        }
        private void WriteReport() { if (reserved) File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); }
        private void Complete() { report.status = "passed"; Finish(0); }
        private void Fail(Exception error) { if (finished) return; report.status = "failed"; report.error = error.ToString(); Finish(1); }
        private void Finish(int exitCode)
        {
            if (finished) return;
            finished = true; Application.logMessageReceived -= OnLog;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            try { WriteReport(); }
            catch (Exception error) { Debug.LogError(error); exitCode = 1; }
            // The already-committed seed/control must remain exactly as captured,
            // and inspect must not run scene startup or Unity shutdown savers.
            while (true) System.Threading.Thread.Sleep(1000);
        }
    }
}
