using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Exercises the actual login, collection-save, suspension and receipt methods
    /// without activating gameplay Awake or writing the player's checkpoint files.
    /// All touched preferences, static state and selection are restored afterwards.
    /// </summary>
    public sealed class CollectionIncomeFlowTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly DateTime LoginTime = new DateTime(2040, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        private readonly List<GameObject> hosts = new List<GameObject>();
        private readonly Dictionary<FieldInfo, object> originalStatics = new Dictionary<FieldInfo, object>();
        private Dictionary<string, SavedPreference> originalPreferences;
        private string[] originalLoginScopes;
        private GameObject originalSelection;
        private string directory;
        private Type suspensionType;

        private sealed class SavedPreference
        {
            public bool Exists;
            public string Type, Text;
            public int Integer;
            public float Real;
        }

        [SetUp]
        public void PreservePlayerStateAndUseIsolatedStorage()
        {
            Assert.That(Application.isPlaying, Is.True);
            originalPreferences = ProfileSaveJournal.ProtectedKeys
                .Concat(new[] { CoreGameplayDemoController.WorldEconomyScopeKey })
                .Distinct().ToDictionary(key => key, CapturePreference);
            originalLoginScopes = LoginScopes.ToArray();
            originalSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            LoginScopes.Clear();
            suspensionType = typeof(CoreGameplayDemoController).Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true);
            foreach (Type type in new[] { typeof(ProfileSaveJournal), suspensionType })
                foreach (FieldInfo field in type.GetFields(StaticFlags))
                {
                    if (field.IsLiteral || field.IsInitOnly) continue;
                    originalStatics.Add(field, field.GetValue(null));
                    field.SetValue(null, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
                }
            directory = Path.Combine(Path.GetTempPath(), "ShatteredSailCollectionIncomeFlow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            typeof(ProfileSaveJournal).GetField("directoryOverride", StaticFlags).SetValue(null, Path.Combine(directory, "profile"));
            suspensionType.GetField("directoryOverride", StaticFlags).SetValue(null, Path.Combine(directory, "voyage"));
            foreach (string key in originalPreferences.Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, "collection-income-test-" + Guid.NewGuid().ToString("N"));
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.WarehousePortMigrationKey, 1);
            ProfileGoldPersistence.Save(100m);
        }

        [UnityTearDown]
        public IEnumerator RestorePlayerState()
        {
            foreach (GameObject host in hosts) if (host != null) Object.Destroy(host);
            hosts.Clear();
            yield return null;
            foreach (KeyValuePair<string, SavedPreference> pair in originalPreferences)
            {
                PlayerPrefs.DeleteKey(pair.Key);
                SavedPreference value = pair.Value;
                if (!value.Exists) continue;
                if (value.Type == "string") PlayerPrefs.SetString(pair.Key, value.Text);
                else if (value.Type == "int") PlayerPrefs.SetInt(pair.Key, value.Integer);
                else PlayerPrefs.SetFloat(pair.Key, value.Real);
            }
            PlayerPrefs.Save();
            foreach (KeyValuePair<FieldInfo, object> pair in originalStatics) pair.Key.SetValue(null, pair.Value);
            originalStatics.Clear();
            LoginScopes.Clear();
            foreach (string scope in originalLoginScopes) LoginScopes.Add(scope);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(originalSelection);
            if (directory != null && Directory.Exists(directory))
            {
                string resolved = Path.GetFullPath(directory);
                string prefix = Path.Combine(Path.GetTempPath(), "ShatteredSailCollectionIncomeFlow-");
                Assert.That(resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(resolved, true);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EveryPortContributesAndTheCombinedLoginRewardIsCappedAtThreeHundred()
        {
            for (int port = 0; port < CoreVoyageSession.DestinationPorts.Count; port++)
                SeedDisplays(CoreVoyageSession.DestinationPorts[port], port + 1);
            CoreGameplayDemoController first = CreateController();
            Initialize(first, LoginTime);
            Assert.That(first.Session.ProfileGold, Is.EqualTo(100m), "An existing collection starts its clock without invented back pay.");
            Assert.That(SavedIncome().CurrentDisplayedCount, Is.EqualTo(21));

            SimulateNewProcess();
            CoreGameplayDemoController second = CreateController();
            Initialize(second, LoginTime.AddHours(1));
            Assert.That(second.Session.ProfileGold, Is.EqualTo(310m));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(310m));

            SimulateNewProcess();
            CoreGameplayDemoController third = CreateController();
            Initialize(third, LoginTime.AddHours(10));
            Assert.That(third.Session.ProfileGold, Is.EqualTo(610m));
            Assert.That(ReadField<decimal>(third, "warehouseIncomeReceipt"), Is.EqualTo(300m));
            Assert.That(SavedIncome().PendingGold, Is.Zero, "Capped overflow cannot be reclaimed by reopening.");
        }

        [Test]
        public void ReinitializingScenesInOneProcessDoesNotPayAgainButTheNextProcessCanClaim()
        {
            SeedDisplays("断锚港", 1);
            SeedDisplays("白贝港", 1);
            SeedIncome(LoginTime.AddHours(-1), 2);
            CoreGameplayDemoController first = CreateController();
            Initialize(first, LoginTime);
            Assert.That(first.Session.ProfileGold, Is.EqualTo(120m));

            CoreGameplayDemoController reentered = CreateController();
            Initialize(reentered, LoginTime.AddHours(4));
            Assert.That(reentered.Session.ProfileGold, Is.EqualTo(120m));
            Assert.That(ReadField<decimal>(reentered, "warehouseIncomeReceipt"), Is.Zero);
            Assert.That(SavedIncome().PendingGold, Is.EqualTo(80m));

            SimulateNewProcess();
            CoreGameplayDemoController nextLogin = CreateController();
            Initialize(nextLogin, LoginTime.AddHours(4));
            Assert.That(nextLogin.Session.ProfileGold, Is.EqualTo(200m));
            Assert.That(SavedIncome().PendingGold, Is.Zero);
        }

        [Test]
        public void CollectionSavesTrackPlacementAndRemovalAcrossPortsWithoutGrantingImmediately()
        {
            SeedDisplays("断锚港", 1);
            CoreGameplayDemoController controller = CreateController();
            Initialize(controller, LoginTime);
            var collections = ReadField<Dictionary<string, CoreWarehouseCollection>>(controller, "warehouseCollections");
            CoreWarehouseCollection remote = collections["灰鸥港"];
            string remoteTreasure = CoreWarehouseCollection.Catalog[1].Id;
            Assert.That(remote.Grant(remoteTreasure), Is.True);
            Assert.That(remote.TryDisplay(remoteTreasure, 0), Is.True);
            Invoke(controller, "SaveWarehouseCollection");
            Assert.That(SavedIncome().CurrentDisplayedCount, Is.EqualTo(2));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseDisplayPortKey("灰鸥港")), Does.Contain(remoteTreasure));

            Assert.That(collections["断锚港"].TryRemoveDisplay(0), Is.True);
            Invoke(controller, "SaveWarehouseCollection");
            Assert.That(SavedIncome().CurrentDisplayedCount, Is.EqualTo(1));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(100m));

            SimulateNewProcess();
            CoreGameplayDemoController nextLogin = CreateController();
            Initialize(nextLogin, LoginTime.AddHours(1));
            Assert.That(nextLogin.Session.ProfileGold, Is.EqualTo(110m), "Only the remaining remote display earns the following hour.");
        }

        [Test]
        public void IncomeOnAResumedVoyagePreservesCargoAndIsIncludedInTheNextSuspensionSnapshot()
        {
            SeedDisplays("断锚港", 2);
            SeedIncome(LoginTime.AddHours(-1), 2);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Gold"), 53);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 91);
            var voyage = new CoreVoyageSession();
            voyage.RestoreProfile(100m, "断锚港");
            voyage.StartVoyage();
            voyage.Cargo.Gold = 17;
            voyage.Cargo.Water = 73;
            float volume = voyage.Cargo.UsedVolume;
            CoreGameplayDemoController controller = CreateController(voyage);
            Initialize(controller, LoginTime);

            Assert.That(voyage.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(voyage.ProfileGold, Is.EqualTo(120m));
            Assert.That(voyage.Cargo.Gold, Is.EqualTo(17));
            Assert.That(voyage.Cargo.Water, Is.EqualTo(73));
            Assert.That(voyage.Cargo.UsedVolume, Is.EqualTo(volume));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Gold")), Is.EqualTo(53));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water")), Is.EqualTo(91));
            object[] saveArgs = { null };
            Assert.That((bool)Invoke(controller, "TrySaveVoyageSuspension", saveArgs), Is.True, (string)saveArgs[0]);
            object[] readArgs = { null, null };
            Assert.That((bool)suspensionType.GetMethod("TryRead", StaticFlags).Invoke(null, readArgs), Is.True, (string)readArgs[1]);
            object record = readArgs[0];
            Assert.That(record, Is.Not.Null);
            CoreVoyageCheckpoint checkpoint = (CoreVoyageCheckpoint)record.GetType().GetField("voyage").GetValue(record);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.ProfileGold, Is.EqualTo(120m));
            Assert.That(restored.Cargo.Gold, Is.EqualTo(17));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(120m));
        }

        [UnityTest]
        public IEnumerator ClosingTheReceiptCannotGrantOrDisplayTheSameIncomeAgain()
        {
            SeedDisplays("断锚港", 1);
            SeedIncome(LoginTime.AddHours(-2), 1);
            CoreGameplayDemoController controller = CreateController();
            Initialize(controller, LoginTime);
            string saved = PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey);
            Invoke(controller, "ShowWarehouseIncomeReceipt");
            Canvas canvas = controller.GetComponentsInChildren<Canvas>(true).Single(value => value.name == "Warehouse Income Canvas");
            // Activate only the completed receipt; the gameplay controller stays
            // inactive so no scene or unrelated profile initialization can run.
            canvas.transform.SetParent(null, false);
            hosts.Add(canvas.gameObject);
            Button close = canvas.GetComponentsInChildren<Button>(true).Single(value => value.name == "Close Warehouse Income");
            Assert.That(canvas.GetComponentsInChildren<Text>(true).Single(value => value.name == "Warehouse Income Amount").text,
                Is.EqualTo("+20 金币"));
            yield return null;
            yield return new WaitForSecondsRealtime(.15f);
            NavalBattleVisualCapture.Capture(controller, "01_Collection_Income_Receipt.png", "所有港口藏品收益已存入账户；确认仅关闭弹窗", "Warehouse Income Receipt");

            close.onClick.Invoke();
            close.onClick.Invoke();
            Invoke(controller, "ShowWarehouseIncomeReceipt");
            Assert.That(ReadField<decimal>(controller, "warehouseIncomeReceipt"), Is.Zero);
            Assert.That(ReadField<RectTransform>(controller, "warehouseIncomeOverlay"), Is.Null);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(120m));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(120m));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey), Is.EqualTo(saved));
            Assert.That(canvas.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void FailedIncomeCommitDoesNotConsumeRewardOrLoginEligibility()
        {
            SeedDisplays("断锚港", 1);
            SeedIncome(LoginTime.AddHours(-2), 1);
            string before = PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey);
            string blocker = Path.Combine(directory, "blocked-profile-directory");
            File.WriteAllText(blocker, "this file must survive the failed save");
            FieldInfo journalDirectory = typeof(ProfileSaveJournal).GetField("directoryOverride", StaticFlags);
            journalDirectory.SetValue(null, blocker);
            CoreGameplayDemoController failed = CreateController();
            object[] args = { LoginTime, null };

            Assert.That((bool)Invoke(failed, "TryInitializeWarehouseIncome", args), Is.False);
            Assert.That((string)args[1], Is.Not.Empty);
            Assert.That(failed.Session.ProfileGold, Is.EqualTo(100m));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(100m));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey), Is.EqualTo(before));
            Assert.That(LoginScopes, Is.Empty);
            Assert.That(File.ReadAllText(blocker), Is.EqualTo("this file must survive the failed save"));

            journalDirectory.SetValue(null, Path.Combine(directory, "profile"));
            CoreGameplayDemoController retry = CreateController();
            Initialize(retry, LoginTime);
            Assert.That(retry.Session.ProfileGold, Is.EqualTo(120m));
            Assert.That(SavedIncome().PendingGold, Is.Zero);
        }

        [Test]
        public void MalformedIncomeIsPreservedWithoutChangingEitherWallet()
        {
            const string malformed = "unrecognized-income-v99";
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseIncomeKey, malformed);
            CoreGameplayDemoController controller = CreateController();
            object[] args = { LoginTime, null };

            Assert.That((bool)Invoke(controller, "TryInitializeWarehouseIncome", args), Is.False);
            Assert.That((string)args[1], Is.Not.Empty);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(100m));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(100m));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey), Is.EqualTo(malformed));
            Assert.That(LoginScopes, Is.Empty);
        }

        private CoreGameplayDemoController CreateController(CoreVoyageSession session = null)
        {
            if (session == null)
            {
                session = new CoreVoyageSession();
                session.RestoreProfile(ProfileGoldPersistence.Load(), "断锚港");
            }
            var host = new GameObject("Inactive Collection Income Test Controller");
            host.SetActive(false);
            hosts.Add(host);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null, "This fixture must never trigger gameplay Awake.");
            WriteField(controller, "session", session);
            WriteField(controller, "tutorialActive", false);
            WriteField(controller, "selectedDepartureOrdinaryCrew", session.SelectedOrdinaryCrew);
            WriteField(controller, "departureCrewShipIndex", session.SelectedShipIndex);
            WriteField(controller, "departureCrewPort", session.Map == null ? session.CurrentPort : session.Map.DepartureName);
            Invoke(controller, "RestoreWarehouseCollection");
            return controller;
        }

        private static void SeedDisplays(string port, int count)
        {
            var collection = new CoreWarehouseCollection();
            for (int index = 0; index < count; index++)
            {
                string id = CoreWarehouseCollection.Catalog[index].Id;
                Assert.That(collection.Grant(id), Is.True);
                Assert.That(collection.TryDisplay(id, index), Is.True);
            }
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedPortKey(port), collection.ExportOwned());
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseDisplayPortKey(port), collection.ExportDisplay());
        }

        private static void SeedIncome(DateTime startedAt, int count)
        {
            var income = new CoreWarehouseIncome();
            income.Observe(startedAt, count);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseIncomeKey, income.Serialize());
        }

        private static CoreWarehouseIncome SavedIncome()
        {
            var result = new CoreWarehouseIncome();
            Assert.That(result.Restore(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey)), Is.True);
            return result;
        }

        private static void Initialize(CoreGameplayDemoController controller, DateTime now)
        {
            object[] args = { now, null };
            Assert.That((bool)Invoke(controller, "TryInitializeWarehouseIncome", args), Is.True, (string)args[1]);
        }

        private static void SimulateNewProcess() => typeof(CoreGameplayDemoController)
            .GetMethod("ResetWarehouseIncomeLoginScopes", StaticFlags).Invoke(null, null);

        private static HashSet<string> LoginScopes => (HashSet<string>)typeof(CoreGameplayDemoController)
            .GetField("WarehouseIncomeLoginScopes", StaticFlags).GetValue(null);

        private static object Invoke(CoreGameplayDemoController controller, string method, params object[] args) =>
            typeof(CoreGameplayDemoController).GetMethod(method, InstanceFlags).Invoke(controller, args);

        private static T ReadField<T>(CoreGameplayDemoController controller, string field) =>
            (T)typeof(CoreGameplayDemoController).GetField(field, InstanceFlags).GetValue(controller);

        private static void WriteField(CoreGameplayDemoController controller, string field, object value) =>
            typeof(CoreGameplayDemoController).GetField(field, InstanceFlags).SetValue(controller, value);

        private static SavedPreference CapturePreference(string key)
        {
            var value = new SavedPreference { Exists = PlayerPrefs.HasKey(key) };
            if (!value.Exists) return value;
            if (PlayerPrefs.GetString(key, "income-type-a") == PlayerPrefs.GetString(key, "income-type-b"))
            {
                value.Type = "string";
                value.Text = PlayerPrefs.GetString(key);
            }
            else if (PlayerPrefs.GetInt(key, 0) == PlayerPrefs.GetInt(key, 1))
            {
                value.Type = "int";
                value.Integer = PlayerPrefs.GetInt(key);
            }
            else
            {
                value.Type = "float";
                value.Real = PlayerPrefs.GetFloat(key);
            }
            return value;
        }
    }
}
