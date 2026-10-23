using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Persistence;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ProfileSaveJournalTests
    {
        private const string OutsideKey = "ShatteredSail.Tests.ProfileJournal.Unrelated";
        private const string VoyageThreeEventsKey = "ShatteredSail.Achievements.Unlocked.v1.voyage_three_events";
        private const string VoyageHalfChartKey = "ShatteredSail.Achievements.Unlocked.v1.voyage_half_chart";
        private const string VoyageEventArrivalKey = "ShatteredSail.Achievements.Unlocked.v1.voyage_event_arrival";
        private static readonly string[] VersionThreeAchievementKeys = { VoyageHalfChartKey, VoyageEventArrivalKey };
        private static readonly string[] RetiredAchievementKeys =
        {
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_prepare",
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_long",
            "ShatteredSail.Achievements.Unlocked.v1.dispatch_resolved",
        };
        private readonly Dictionary<string, bool> expectedTypes = ExpectedWriterKeys();
        private readonly Dictionary<FieldInfo, object> originalStatics = new Dictionary<FieldInfo, object>();
        private Dictionary<string, SavedPreference> originalPreferences;
        private string directory;
        private ProtectedCheckpointStore store;

        [Serializable] private sealed class JournalRecord
        {
            public string format, company, product, profileScope;
            public int version;
            public JournalEntry[] entries;
        }

        [Serializable] private sealed class JournalEntry
        {
            public string key, type, stringValue;
            public bool exists;
            public int intValue;
        }

        private sealed class SavedPreference
        {
            public bool Exists, IsString, IsFloat;
            public string Text;
            public int Number;
            public float Real;
        }

        [SetUp]
        public void PreserveProfileAndUseAnIsolatedJournal()
        {
            // Every touched preference is restored, including the identity and
            // the deliberately unprotected sentinel. No DeleteAll is used.
            var preservedTypes = new Dictionary<string, bool>(expectedTypes)
            {
                [CoreGameplayDemoController.WorldEconomyScopeKey] = true,
                [OutsideKey] = true,
            };
            foreach (string key in RetiredAchievementKeys) preservedTypes.Add(key, false);
            originalPreferences = ReadPreferences(preservedTypes);
            foreach (FieldInfo field in typeof(ProfileSaveJournal).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (field.IsLiteral || field.IsInitOnly) continue;
                originalStatics.Add(field, field.GetValue(null));
                field.SetValue(null, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            }
            directory = Path.Combine(Path.GetTempPath(), "ShatteredSailProfileJournalTests-" + Guid.NewGuid().ToString("N"));
            typeof(ProfileSaveJournal).GetField("directoryOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, directory);
            store = new ProtectedCheckpointStore(directory);
            foreach (string key in expectedTypes.Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, "journal-test-" + Guid.NewGuid().ToString("N"));
            PlayerPrefs.SetString(OutsideKey, "outside-before");
        }

        [TearDown]
        public void RestoreProfileAndStaticState()
        {
            try
            {
                if (originalPreferences != null)
                {
                    WritePreferences(originalPreferences);
                    PlayerPrefs.Save();
                }
            }
            finally
            {
                foreach (KeyValuePair<FieldInfo, object> field in originalStatics) field.Key.SetValue(null, field.Value);
                originalStatics.Clear();
                // Only this fixture's unique directory is ever removed.
                if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void ProtectedManifestCoversEveryCurrentSaveProgressWriterAndNoEarlierIndependentWrites()
        {
            Assert.That(expectedTypes.Count, Is.EqualTo(106), "32 string and 74 integer keys including the two combat evidence masks.");
            Assert.That(expectedTypes.Values.Count(value => value), Is.EqualTo(32));
            Assert.That(ProfileSaveJournal.ProtectedKeys, Is.EquivalentTo(expectedTypes.Keys));
            Assert.That(ProfileSaveJournal.ProtectedKeys.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(expectedTypes.Count));
            Assert.That(ProfileSaveJournal.ProtectedKeys, Does.Not.Contain(CoreGameplayDemoController.WorldEconomyScopeKey));
            Assert.That(ProfileSaveJournal.ProtectedKeys, Does.Not.Contain(CoreGameplayDemoController.TutorialCompletedKey));
            Assert.That(ProfileSaveJournal.ProtectedKeys, Does.Not.Contain(CoreGameplayDemoController.RecordedSeaChartsKey));
        }

        [Test]
        public void NewJournalUsesVersionSixAndIncludesAllAchievementEvidence()
        {
            Begin();
            Assert.That(store.TryLoad(out string json, out string notice), Is.True, notice);
            JournalRecord record = JsonUtility.FromJson<JournalRecord>(json);
            Assert.That(record.version, Is.EqualTo(6));
            Assert.That(record.entries.Length, Is.EqualTo(106));
            foreach (string key in AchievementCatalog.ChallengeEvidenceKeys)
                Assert.That(record.entries.Single(entry => entry.key == key).type, Is.EqualTo("int"));
            Assert.That(record.entries.Single(entry => entry.key == CoreGameplayDemoController.WarehouseIncomeKey).type, Is.EqualTo("string"));
            Assert.That(record.entries.Single(entry => entry.key == CoreGameplayDemoController.ShipPortProgressKey).type, Is.EqualTo("string"));
            Assert.That(record.entries.Single(entry => entry.key == VoyageThreeEventsKey).type, Is.EqualTo("int"));
            foreach (string key in VersionThreeAchievementKeys)
                Assert.That(record.entries.Single(entry => entry.key == key).type, Is.EqualTo("int"));
        }

        [TestCase(false, VoyageThreeEventsKey)]
        [TestCase(true, VoyageThreeEventsKey)]
        [TestCase(false, VoyageHalfChartKey)]
        [TestCase(true, VoyageHalfChartKey)]
        [TestCase(false, VoyageEventArrivalKey)]
        [TestCase(true, VoyageEventArrivalKey)]
        [TestCase(false, AchievementCatalog.EditedDeckStagesKey)]
        [TestCase(true, AchievementCatalog.EditedDeckStagesKey)]
        [TestCase(false, AchievementCatalog.WonDeckStagesKey)]
        [TestCase(true, AchievementCatalog.WonDeckStagesKey)]
        public void CurrentJournalRestoresEachVoyageMilestoneBeforeImage(bool existed, string key)
        {
            if (existed) PlayerPrefs.SetInt(key, 1);
            Begin();
            PlayerPrefs.SetInt(key, 23);

            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            Assert.That(PlayerPrefs.HasKey(key), Is.EqualTo(existed));
            if (existed) Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
            AssertSlotEmpty();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CurrentJournalRestoresShipLocationsTogetherWithPortAndFlagship(bool locationsExisted)
        {
            const string locations = "{\"version\":1,\"ships\":[{\"shipIndex\":0,\"port\":\"断锚港\"},{\"shipIndex\":4,\"port\":\"白贝港\"}]}";
            if (locationsExisted) PlayerPrefs.SetString(CoreGameplayDemoController.ShipPortProgressKey, locations);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "白贝港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 4);
            Begin();
            PlayerPrefs.SetString(CoreGameplayDemoController.ShipPortProgressKey, "partially written arrival locations");
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "铜钟港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 2);
            ForgetActiveTransactionForRestartSimulation();

            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ShipPortProgressKey), Is.EqualTo(locationsExisted));
            if (locationsExisted) Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ShipPortProgressKey), Is.EqualTo(locations));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey), Is.EqualTo("白贝港"));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.SelectedShipKey), Is.EqualTo(4));
            AssertSlotEmpty();
        }

        [Test]
        public void RecoverRestoresEveryProtectedTypeAndDeletesKeysThatWereOriginallyAbsent()
        {
            SeedValues("before", 1100, true);
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            Begin();
            SeedValues("interrupted", 2200, false);
            PlayerPrefs.SetString(OutsideKey, "outside-after-begin");
            PlayerPrefs.Save();
            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            AssertPreferences(before);
            Assert.That(PlayerPrefs.GetString(OutsideKey), Is.EqualTo("outside-after-begin"));
            AssertSlotEmpty();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CurrentJournalRestoresCollectionIncomeAndGoldAsOneBeforeImage(bool incomeExisted)
        {
            const string income = "{\"version\":1,\"lastAccruedUtcTicks\":638000000000000000}";
            if (incomeExisted) PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseIncomeKey, income);
            ProfileGoldPersistence.Save(127.5m);
            Begin();
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseIncomeKey, "partially committed claim");
            ProfileGoldPersistence.Save(427.5m);
            ForgetActiveTransactionForRestartSimulation();

            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.WarehouseIncomeKey), Is.EqualTo(incomeExisted));
            if (incomeExisted) Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseIncomeKey), Is.EqualTo(income));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(127.5m));
            AssertSlotEmpty();
        }

        [Test]
        public void VersionFourJournalRestoresItsManifestAndRemovesInterruptedCollectionIncomeMigration()
        {
            SeedValues("before", 1100, true);
            foreach (string key in AchievementCatalog.ChallengeEvidenceKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseIncomeKey);
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            Begin();
            ReplaceJournalPayload(JsonUtility.ToJson(ReadVersionFourRecord()));
            SeedValues("interrupted", 2200, false);
            ForgetActiveTransactionForRestartSimulation();

            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            AssertPreferences(before);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.WarehouseIncomeKey), Is.False);
            AssertSlotEmpty();
            Begin();
            Assert.That(store.TryLoad(out string json, out notice), Is.True, notice);
            JournalRecord upgraded = JsonUtility.FromJson<JournalRecord>(json);
            Assert.That(upgraded.version, Is.EqualTo(6));
            Assert.That(upgraded.entries.Select(entry => entry.key), Is.EquivalentTo(expectedTypes.Keys));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyJournalRestoresItsKeysAndRemovesNewMilestonesWithoutRestoringRetiredAchievements(bool includesRetired)
        {
            SeedValues("before", 1100, true);
            foreach (string key in AchievementCatalog.ChallengeEvidenceKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseIncomeKey);
            PlayerPrefs.DeleteKey(VoyageThreeEventsKey);
            foreach (string key in VersionThreeAchievementKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ShipPortProgressKey);
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            Begin();
            JournalRecord legacy = ReadLegacyRecord(includesRetired);
            ReplaceJournalPayload(JsonUtility.ToJson(legacy));
            SeedValues("interrupted", 2200, false);
            foreach (string key in RetiredAchievementKeys) PlayerPrefs.DeleteKey(key);

            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            AssertPreferences(before);
            foreach (string key in RetiredAchievementKeys) Assert.That(PlayerPrefs.HasKey(key), Is.False, key);
            AssertSlotEmpty();
            Begin();
            Assert.That(store.TryLoad(out string json, out notice), Is.True, notice);
            JournalRecord upgraded = JsonUtility.FromJson<JournalRecord>(json);
            Assert.That(upgraded.version, Is.EqualTo(6));
            Assert.That(upgraded.entries.Select(entry => entry.key), Is.EquivalentTo(expectedTypes.Keys));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void VersionTwoJournalRestoresExistingMilestoneAndRemovesOnlyTheTwoLaterMilestones(bool milestoneExisted)
        {
            SeedValues("before", 1100, true);
            foreach (string key in AchievementCatalog.ChallengeEvidenceKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseIncomeKey);
            if (milestoneExisted) PlayerPrefs.SetInt(VoyageThreeEventsKey, 1);
            else PlayerPrefs.DeleteKey(VoyageThreeEventsKey);
            foreach (string key in VersionThreeAchievementKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ShipPortProgressKey);
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            Begin();
            ReplaceJournalPayload(JsonUtility.ToJson(ReadVersionTwoRecord()));
            SeedValues("interrupted", 2200, false);
            ForgetActiveTransactionForRestartSimulation();

            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            AssertPreferences(before);
            Assert.That(PlayerPrefs.HasKey(VoyageThreeEventsKey), Is.EqualTo(milestoneExisted));
            foreach (string key in VersionThreeAchievementKeys) Assert.That(PlayerPrefs.HasKey(key), Is.False, key);
            AssertSlotEmpty();
            Begin();
            Assert.That(store.TryLoad(out string json, out notice), Is.True, notice);
            Assert.That(JsonUtility.FromJson<JournalRecord>(json).version, Is.EqualTo(6));
        }

        [Test]
        public void VersionThreeJournalRestoresItsManifestAndRemovesInterruptedShipLocationMigration()
        {
            SeedValues("before", 1100, true);
            foreach (string key in AchievementCatalog.ChallengeEvidenceKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseIncomeKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ShipPortProgressKey);
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            Begin();
            ReplaceJournalPayload(JsonUtility.ToJson(ReadVersionThreeRecord()));
            SeedValues("interrupted", 2200, false);
            ForgetActiveTransactionForRestartSimulation();

            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            AssertPreferences(before);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ShipPortProgressKey), Is.False);
            AssertSlotEmpty();
            Begin();
            Assert.That(store.TryLoad(out string json, out notice), Is.True, notice);
            JournalRecord upgraded = JsonUtility.FromJson<JournalRecord>(json);
            Assert.That(upgraded.version, Is.EqualTo(6));
            Assert.That(upgraded.entries.Select(entry => entry.key), Is.EquivalentTo(expectedTypes.Keys));
        }

        [TestCase(false, "missing-current")]
        [TestCase(false, "duplicate-current")]
        [TestCase(false, "unknown")]
        [TestCase(false, "current-type")]
        [TestCase(false, "new-key")]
        [TestCase(false, "half-chart-key")]
        [TestCase(false, "event-arrival-key")]
        [TestCase(true, "missing-current")]
        [TestCase(true, "duplicate-current")]
        [TestCase(true, "duplicate-retired")]
        [TestCase(true, "unknown")]
        [TestCase(true, "retired-type")]
        [TestCase(true, "new-key")]
        [TestCase(true, "half-chart-key")]
        [TestCase(true, "event-arrival-key")]
        public void InvalidLegacyJournalIsRejectedBeforeAnyCurrentKeyIsRestored(bool includesRetired, string damage)
        {
            SeedValues("before", 1100, false);
            Begin();
            JournalRecord legacy = ReadLegacyRecord(includesRetired);
            int last = legacy.entries.Length - 1;
            switch (damage)
            {
                case "missing-current": legacy.entries = legacy.entries.Skip(1).ToArray(); break;
                case "duplicate-current": legacy.entries[1] = legacy.entries[0]; break;
                case "duplicate-retired": legacy.entries[last] = legacy.entries[last - 1]; break;
                case "unknown": legacy.entries[last].key = OutsideKey; break;
                case "current-type": legacy.entries[0].type = legacy.entries[0].type == "int" ? "string" : "int"; break;
                case "retired-type": legacy.entries[last].type = "string"; break;
                case "new-key": legacy.entries[last].key = VoyageThreeEventsKey; break;
                case "half-chart-key": legacy.entries[last].key = VoyageHalfChartKey; break;
                case "event-arrival-key": legacy.entries[last].key = VoyageEventArrivalKey; break;
            }
            ReplaceJournalPayload(JsonUtility.ToJson(legacy));
            SeedValues("interrupted", 2200, false);
            Dictionary<string, byte[]> evidence = ReadJournalFiles();
            AssertRecoveryAndBeginRejectWithoutMutation();
            AssertJournalFiles(evidence);
        }

        [TestCase("missing-new-key")]
        [TestCase("missing-half-chart-key")]
        [TestCase("missing-event-arrival-key")]
        [TestCase("missing-ship-port-key")]
        [TestCase("missing-income-key")]
        [TestCase("income-key-type")]
        [TestCase("version-four-current-keys")]
        [TestCase("version-five-version-four-keys")]
        [TestCase("version-four-version-three-keys")]
        [TestCase("version-three-current-keys")]
        [TestCase("version-three-version-two-keys")]
        [TestCase("version-three-retired-keys")]
        [TestCase("version-one-current-keys")]
        [TestCase("version-two-current-keys")]
        [TestCase("version-two-retired-keys")]
        public void JournalVersionCannotDisguiseAnIncompleteOrUnexpectedManifest(string damage)
        {
            SeedValues("before", 1100, false);
            Begin();
            Assert.That(store.TryLoad(out string json, out string notice), Is.True, notice);
            JournalRecord record = JsonUtility.FromJson<JournalRecord>(json);
            switch (damage)
            {
                case "missing-new-key": record.entries = record.entries.Where(entry => entry.key != VoyageThreeEventsKey).ToArray(); break;
                case "missing-half-chart-key": record.entries = record.entries.Where(entry => entry.key != VoyageHalfChartKey).ToArray(); break;
                case "missing-event-arrival-key": record.entries = record.entries.Where(entry => entry.key != VoyageEventArrivalKey).ToArray(); break;
                case "missing-ship-port-key": record.entries = record.entries.Where(entry => entry.key != CoreGameplayDemoController.ShipPortProgressKey).ToArray(); break;
                case "missing-income-key": record.entries = record.entries.Where(entry => entry.key != CoreGameplayDemoController.WarehouseIncomeKey).ToArray(); break;
                case "income-key-type": record.entries.Single(entry => entry.key == CoreGameplayDemoController.WarehouseIncomeKey).type = "int"; break;
                case "version-four-current-keys": record.version = 4; break;
                case "version-five-version-four-keys": record = ReadVersionFourRecord(); record.version = 5; break;
                case "version-four-version-three-keys": record = ReadVersionThreeRecord(); record.version = 4; break;
                case "version-three-current-keys": record.version = 3; break;
                case "version-three-version-two-keys": record = ReadVersionTwoRecord(); record.version = 3; break;
                case "version-three-retired-keys": record = ReadLegacyRecord(true); record.version = 3; break;
                case "version-one-current-keys": record.version = 1; break;
                case "version-two-current-keys": record.version = 2; break;
                case "version-two-retired-keys": record = ReadLegacyRecord(true); record.version = 2; break;
            }
            ReplaceJournalPayload(JsonUtility.ToJson(record));
            SeedValues("interrupted", 2200, false);
            Dictionary<string, byte[]> evidence = ReadJournalFiles();

            AssertRecoveryAndBeginRejectWithoutMutation();
            AssertJournalFiles(evidence);
        }

        [TestCase("missing-current")]
        [TestCase("duplicate-current")]
        [TestCase("new-key-type")]
        [TestCase("missing-new-key")]
        [TestCase("half-chart-key")]
        [TestCase("event-arrival-key")]
        public void InvalidVersionTwoJournalIsRejectedWithoutRestoringOrDeletingAnyMilestone(string damage)
        {
            SeedValues("before", 1100, false);
            Begin();
            JournalRecord record = ReadVersionTwoRecord();
            JournalEntry milestone = record.entries.Single(entry => entry.key == VoyageThreeEventsKey);
            switch (damage)
            {
                case "missing-current": record.entries = record.entries.Skip(1).ToArray(); break;
                case "duplicate-current": record.entries[1] = record.entries[0]; break;
                case "new-key-type": milestone.type = "string"; break;
                case "missing-new-key": record.entries = record.entries.Where(entry => entry.key != VoyageThreeEventsKey).ToArray(); break;
                case "half-chart-key": milestone.key = VoyageHalfChartKey; break;
                case "event-arrival-key": milestone.key = VoyageEventArrivalKey; break;
            }
            ReplaceJournalPayload(JsonUtility.ToJson(record));
            SeedValues("interrupted", 2200, false);
            Dictionary<string, byte[]> evidence = ReadJournalFiles();

            AssertRecoveryAndBeginRejectWithoutMutation();
            AssertJournalFiles(evidence);
        }

        [Test]
        public void BeforeImageCapturesEarlierIndependentCommitsAtBeginNotAnOlderProfile()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 117);
            PlayerPrefs.SetString(CoreGameplayDemoController.SeaChartProgressKey, "independently committed chart");
            PlayerPrefs.Save();
            Begin();
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 90);
            PlayerPrefs.SetString(CoreGameplayDemoController.SeaChartProgressKey, "pending chart");
            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water")), Is.EqualTo(117));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey), Is.EqualTo("independently committed chart"));
        }

        [Test]
        public void RecoveryPreservesMalformedComponentWarehouseTreasureAndChartStringsExactly()
        {
            string[] keys =
            {
                CoreGameplayDemoController.ComponentInventoryKey, CoreGameplayDemoController.PortWarehouseCargoKey,
                CoreGameplayDemoController.WarehouseOwnedKey, CoreGameplayDemoController.WarehouseDisplayKey,
                CoreGameplayDemoController.TreasureProgressKey, CoreGameplayDemoController.SeaChartProgressKey,
                CoreGameplayDemoController.ShipPortProgressKey,
                CoreGameplayDemoController.WarehouseIncomeKey,
            };
            const string malformed = " {invalid-json:\"旧档⚓\"}\r\n\t preserved trailing spaces  ";
            foreach (string key in keys) PlayerPrefs.SetString(key, malformed + key);
            Begin();
            foreach (string key in keys) PlayerPrefs.SetString(key, "{}");
            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            foreach (string key in keys) Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(malformed + key), key);
        }

        [Test]
        public void CompletedSaveKeepsNewValuesAndDoesNotReplayOnRepeatedRecovery()
        {
            SeedValues("before", 1100, true);
            Begin();
            SeedValues("committed", 3300, false);
            Dictionary<string, SavedPreference> committed = ReadPreferences(expectedTypes);
            PlayerPrefs.Save();
            Assert.That(ProfileSaveJournal.TryComplete(out string notice), Is.True, notice);
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.True, notice);
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.True, notice);
            AssertPreferences(committed);
            AssertSlotEmpty();
        }

        [Test]
        public void RepeatedRecoveryDoesNotUndoNewIndependentWritesAfterTheFirstRecovery()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 120);
            Begin();
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 700);
            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 143);
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.True, notice);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey), Is.EqualTo(143));
        }

        [Test]
        public void ASecondBeginCannotReplaceThePendingBeforeImage()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 99);
            Begin();
            byte[] bytes = File.ReadAllBytes(store.PrimaryPath);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 222);
            Assert.That(ProfileSaveJournal.TryBegin(out string notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.True, notice);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey), Is.EqualTo(99));
        }

        [Test]
        public void ACompleteWithoutThisProcessBeginCannotDiscardPendingRecovery()
        {
            Begin();
            byte[] bytes = File.ReadAllBytes(store.PrimaryPath);
            ForgetActiveTransactionForRestartSimulation();
            Assert.That(ProfileSaveJournal.TryComplete(out string notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(bytes));
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.True, notice);
            AssertSlotEmpty();
        }

        [Test]
        public void CompletedMarkerSuppressesResidualOldPrimaryAndBackupFiles()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 99);
            Begin();
            byte[] stale = File.ReadAllBytes(store.PrimaryPath);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 222);
            PlayerPrefs.Save();
            Assert.That(ProfileSaveJournal.TryComplete(out string notice), Is.True, notice);
            File.WriteAllBytes(store.PrimaryPath, stale);
            File.WriteAllBytes(store.PrimaryPath + ".bak1", stale);
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.True, notice);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey), Is.EqualTo(222));
            AssertSlotEmpty();
        }

        [Test]
        public void NewTransactionBackupCannotResurrectAnEarlierCommittedBeforeImage()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 99);
            Begin();
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 222);
            PlayerPrefs.Save();
            Assert.That(ProfileSaveJournal.TryComplete(out string notice), Is.True, notice);
            Begin();
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 333);
            File.WriteAllText(store.PrimaryPath, "interrupted second primary");
            ForgetActiveTransactionForRestartSimulation();
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.True, notice);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey), Is.EqualTo(222));
            Assert.That(Directory.GetFiles(directory, ProtectedCheckpointStore.FileName + ".corrupt-*").Length, Is.GreaterThan(0));
        }

        [Test]
        public void CorruptAllCopiesRejectRecoveryBeginAndCompleteWithoutChangingPreferencesOrEvidence()
        {
            Begin();
            File.WriteAllBytes(store.PrimaryPath, new byte[] { 0xff, 0x00, 0x15 });
            File.WriteAllText(store.PrimaryPath + ".bak1", "broken backup");
            Dictionary<string, byte[]> evidence = ReadJournalFiles();
            SeedValues("pending", 2200, false);
            AssertRecoveryAndBeginRejectWithoutMutation();
            Assert.That(ProfileSaveJournal.TryComplete(out string notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            AssertJournalFiles(evidence);
        }

        [Test]
        public void UnsupportedEnvelopeDoesNotFallBackToAValidOlderCopy()
        {
            Begin();
            string[] lines = File.ReadAllText(store.PrimaryPath).Split('\n');
            lines[1] = "99";
            File.WriteAllText(store.PrimaryPath, string.Join("\n", lines));
            Dictionary<string, byte[]> evidence = ReadJournalFiles();
            SeedValues("pending", 2200, false);
            AssertRecoveryAndBeginRejectWithoutMutation();
            AssertJournalFiles(evidence);
        }

        [TestCase("version")]
        [TestCase("format")]
        [TestCase("company")]
        [TestCase("product")]
        [TestCase("scope")]
        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("type")]
        [TestCase("null-entry")]
        public void SemanticallyInvalidJournalIsFullyRejectedBeforeAnyKeyIsRestored(string damage)
        {
            SeedValues("before", 1100, false);
            Begin();
            Assert.That(store.TryLoad(out string json, out string notice), Is.True, notice);
            JournalRecord record = JsonUtility.FromJson<JournalRecord>(json);
            int last = record.entries.Length - 1;
            switch (damage)
            {
                case "version": record.version += 1; break;
                case "format": record.format = "AnotherRecord"; break;
                case "company": record.company += "-different"; break;
                case "product": record.product += "-different"; break;
                case "scope": record.profileScope += "-different"; break;
                case "missing": record.entries = record.entries.Take(last).ToArray(); break;
                case "duplicate": record.entries[last] = record.entries[0]; break;
                case "unknown": record.entries[last].key = OutsideKey; break;
                case "type": record.entries[last].type = record.entries[last].type == "int" ? "string" : "int"; break;
                case "null-entry": record.entries[last] = null; break;
            }
            ReplaceJournalPayload(JsonUtility.ToJson(record));
            SeedValues("pending", 2200, false);
            Dictionary<string, byte[]> evidence = ReadJournalFiles();
            AssertRecoveryAndBeginRejectWithoutMutation();
            AssertJournalFiles(evidence);
        }

        [Test]
        public void ChangedProfileScopeRejectsReplayIntoTheNewProfile()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 99);
            Begin();
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, "different-new-profile");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 500);
            AssertRecoveryAndBeginRejectWithoutMutation();
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey), Is.EqualTo("different-new-profile"));
        }

        [Test]
        public void ExplicitResetRetiresPendingJournalBeforeANewGameChangesTheProfileScope()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 99);
            Begin();
            byte[] old = File.ReadAllBytes(store.PrimaryPath);
            Assert.That(ProfileSaveJournal.TryReset(out string notice), Is.True, notice);
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, "new-profile-after-explicit-reset");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 500);
            File.WriteAllBytes(store.PrimaryPath + ".bak1", old);
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.True, notice);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey), Is.EqualTo(500));
        }

        [Test]
        public void ExplicitResetPreservesCorruptEvidenceRatherThanSilentlyRepairingIt()
        {
            Begin();
            byte[] damaged = { 0xfa, 0x00, 0x27 };
            File.WriteAllBytes(store.PrimaryPath, damaged);
            File.WriteAllBytes(store.PrimaryPath + ".bak1", damaged);
            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.False);
            Assert.That(ProfileSaveJournal.TryReset(out notice), Is.True, notice);
            string[] preserved = Directory.GetFiles(directory, ProtectedCheckpointStore.FileName + ".corrupt-*");
            Assert.That(preserved.Any(path => File.ReadAllBytes(path).SequenceEqual(damaged)), Is.True);
            AssertSlotEmpty();
        }

        [Test]
        public void ExplicitResetArchivesAValidEnvelopeWithUnsupportedInnerSchemaByteForByte()
        {
            Begin();
            Assert.That(store.TryLoad(out string json, out string notice), Is.True, notice);
            JournalRecord record = JsonUtility.FromJson<JournalRecord>(json);
            record.version = 99;
            ReplaceJournalPayload(JsonUtility.ToJson(record));
            Dictionary<string, byte[]> evidence = ReadJournalFiles();
            Assert.That(ProfileSaveJournal.TryRecover(out notice), Is.False);
            Assert.That(ProfileSaveJournal.TryReset(out notice), Is.True, notice);
            foreach (KeyValuePair<string, byte[]> item in evidence)
            {
                string[] archived = Directory.GetFiles(directory, Path.GetFileName(item.Key), SearchOption.AllDirectories);
                Assert.That(archived.Any(path => File.ReadAllBytes(path).SequenceEqual(item.Value)), Is.True, item.Key);
            }
            AssertSlotEmpty();
        }

        [Test]
        public void FailedResetArchiveKeepsPendingJournalAndDoesNotChangeProfile()
        {
            Begin();
            byte[] pending = File.ReadAllBytes(store.PrimaryPath);
            File.WriteAllText(Path.Combine(directory, "ResetHistory"), "a file blocks the archive directory");
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            Assert.That(ProfileSaveJournal.TryReset(out string notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(pending));
            AssertPreferences(before);
        }

        [TestCase("integer-as-string")]
        [TestCase("string-as-integer")]
        [TestCase("integer-as-float")]
        public void UnexpectedNativePreferenceTypeRejectsBeginWithoutReplacingItWithADefault(string damage)
        {
            if (damage == "integer-as-string") PlayerPrefs.SetString(CoreGameplayDemoController.ProfileGoldKey, "corrupt-native-int");
            else if (damage == "string-as-integer") PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentInventoryKey, 7301);
            else PlayerPrefs.SetFloat(CoreGameplayDemoController.ProfileGoldKey, 123.75f);
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            Assert.That(ProfileSaveJournal.TryBegin(out string notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            AssertPreferences(before);
            AssertSlotEmpty();
        }

        [Test]
        public void InvalidClearMarkerRejectsRecoveryAndBeginEvenWhenAnOldBackupIsReadable()
        {
            Begin();
            File.WriteAllText(store.PrimaryPath + ".cleared", "partial clear marker");
            Dictionary<string, byte[]> evidence = ReadJournalFiles();
            AssertRecoveryAndBeginRejectWithoutMutation();
            AssertJournalFiles(evidence);
        }

        [Test]
        public void StorageLockFailureDoesNotChangeProfileAndCanBeRecoveredAfterTheLockIsReleased()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 99);
            Begin();
            PlayerPrefs.SetInt(CoreGameplayDemoController.ProfileGoldKey, 222);
            using (var held = new FileStream(store.PrimaryPath + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                AssertRecoveryAndBeginRejectWithoutMutation();
                Assert.That(ProfileSaveJournal.TryComplete(out string notice), Is.False);
                Assert.That(notice, Is.Not.Empty);
            }
            Assert.That(ProfileSaveJournal.TryRecover(out string recoveryNotice), Is.True, recoveryNotice);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ProfileGoldKey), Is.EqualTo(99));
        }

        [Test]
        public void UnknownPortRejectsBeginWithoutCreatingAFalsePartialKeyManifest()
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "unknown legacy port");
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            Assert.That(ProfileSaveJournal.TryBegin(out string notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            AssertPreferences(before);
            AssertSlotEmpty();
        }

        private void Begin()
        {
            Assert.That(ProfileSaveJournal.TryBegin(out string notice), Is.True, notice);
            Assert.That(store.TryLoad(out string payload, out notice), Is.True, notice);
            Assert.That(payload, Is.Not.Empty);
        }

        private JournalRecord ReadLegacyRecord(bool includesRetired)
        {
            JournalRecord record = ReadVersionTwoRecord();
            record.version = 1;
            record.entries = record.entries.Where(entry => entry.key != VoyageThreeEventsKey).ToArray();
            if (includesRetired)
                record.entries = record.entries.Concat(RetiredAchievementKeys.Select(key => new JournalEntry
                {
                    key = key, type = "int", stringValue = string.Empty, exists = true, intValue = 1,
                })).ToArray();
            Assert.That(record.entries.Length, Is.EqualTo(includesRetired ? 102 : 99));
            return record;
        }

        private JournalRecord ReadVersionTwoRecord()
        {
            JournalRecord record = ReadVersionThreeRecord();
            record.version = 2;
            record.entries = record.entries.Where(entry => !VersionThreeAchievementKeys.Contains(entry.key)).ToArray();
            Assert.That(record.entries.Length, Is.EqualTo(100));
            return record;
        }

        private JournalRecord ReadVersionThreeRecord()
        {
            JournalRecord record = ReadVersionFourRecord();
            record.version = 3;
            record.entries = record.entries.Where(entry => entry.key != CoreGameplayDemoController.ShipPortProgressKey).ToArray();
            Assert.That(record.entries.Length, Is.EqualTo(102));
            return record;
        }

        private JournalRecord ReadVersionFourRecord()
        {
            JournalRecord record = ReadVersionFiveRecord();
            record.version = 4;
            record.entries = record.entries.Where(entry => entry.key != CoreGameplayDemoController.WarehouseIncomeKey).ToArray();
            Assert.That(record.entries.Length, Is.EqualTo(103));
            return record;
        }

        private JournalRecord ReadVersionFiveRecord()
        {
            Assert.That(store.TryLoad(out string json, out string notice), Is.True, notice);
            JournalRecord record = JsonUtility.FromJson<JournalRecord>(json);
            record.version = 5;
            record.entries = record.entries.Where(entry => !AchievementCatalog.ChallengeEvidenceKeys.Contains(entry.key)).ToArray();
            Assert.That(record.entries.Length, Is.EqualTo(104));
            return record;
        }

        [Test]
        public void VersionFiveRecoveryRemovesInterruptedChallengeEvidenceButKeepsOldUnlocks()
        {
            const string earned = "ShatteredSail.Achievements.Unlocked.v1.battle_stages";
            PlayerPrefs.SetInt(earned, 1);
            Begin();
            ReplaceJournalPayload(JsonUtility.ToJson(ReadVersionFiveRecord()));
            foreach (string key in AchievementCatalog.ChallengeEvidenceKeys) PlayerPrefs.SetInt(key, 7);
            PlayerPrefs.DeleteKey(earned);
            ForgetActiveTransactionForRestartSimulation();
            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.True, notice);
            Assert.That(PlayerPrefs.GetInt(earned), Is.EqualTo(1));
            foreach (string key in AchievementCatalog.ChallengeEvidenceKeys) Assert.That(PlayerPrefs.HasKey(key), Is.False, key);
            AssertSlotEmpty();
        }

        private void SeedValues(string prefix, int number, bool includeAbsent)
        {
            int index = 0;
            foreach (KeyValuePair<string, bool> item in expectedTypes.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (includeAbsent && index % 3 == 0) PlayerPrefs.DeleteKey(item.Key);
                else if (item.Value)
                    PlayerPrefs.SetString(item.Key, item.Key == CoreGameplayDemoController.CurrentPortKey ? "断锚港" : prefix + ":⚓\r\n" + item.Key + "  ");
                else PlayerPrefs.SetInt(item.Key, number + index);
                index += 1;
            }
        }

        private static Dictionary<string, SavedPreference> ReadPreferences(IReadOnlyDictionary<string, bool> types)
        {
            return types.ToDictionary(item => item.Key, item => ReadPreference(item.Key, item.Value), StringComparer.Ordinal);
        }

        private static SavedPreference ReadPreference(string key, bool expectedString)
        {
            var value = new SavedPreference { Exists = PlayerPrefs.HasKey(key), IsString = expectedString };
            if (!value.Exists) return value;
            // Preserve a pre-existing wrong native type as well as valid user
            // data; teardown must not turn that value into a typed default.
            if (PlayerPrefs.GetString(key, "journal-test-a") == PlayerPrefs.GetString(key, "journal-test-b"))
            {
                value.IsString = true;
                value.Text = PlayerPrefs.GetString(key);
            }
            else if (PlayerPrefs.GetInt(key, 0) == PlayerPrefs.GetInt(key, 1))
            {
                value.IsString = false;
                value.Number = PlayerPrefs.GetInt(key);
            }
            else
            {
                Assert.That(PlayerPrefs.GetFloat(key, 0f), Is.EqualTo(PlayerPrefs.GetFloat(key, 1f)), "Unsupported native preference type: " + key);
                value.IsString = false;
                value.IsFloat = true;
                value.Real = PlayerPrefs.GetFloat(key);
            }
            return value;
        }

        private static void WritePreferences(IReadOnlyDictionary<string, SavedPreference> values)
        {
            foreach (KeyValuePair<string, SavedPreference> item in values)
            {
                if (!item.Value.Exists) PlayerPrefs.DeleteKey(item.Key);
                else if (item.Value.IsString) PlayerPrefs.SetString(item.Key, item.Value.Text);
                else if (item.Value.IsFloat) PlayerPrefs.SetFloat(item.Key, item.Value.Real);
                else PlayerPrefs.SetInt(item.Key, item.Value.Number);
            }
        }

        private static void AssertPreferences(IReadOnlyDictionary<string, SavedPreference> expected)
        {
            foreach (KeyValuePair<string, SavedPreference> item in expected)
            {
                Assert.That(PlayerPrefs.HasKey(item.Key), Is.EqualTo(item.Value.Exists), item.Key + " existence");
                if (!item.Value.Exists) continue;
                if (item.Value.IsString) Assert.That(PlayerPrefs.GetString(item.Key), Is.EqualTo(item.Value.Text), item.Key);
                else if (item.Value.IsFloat) Assert.That(PlayerPrefs.GetFloat(item.Key), Is.EqualTo(item.Value.Real), item.Key);
                else Assert.That(PlayerPrefs.GetInt(item.Key), Is.EqualTo(item.Value.Number), item.Key);
            }
        }

        private void AssertRecoveryAndBeginRejectWithoutMutation()
        {
            Dictionary<string, SavedPreference> before = ReadPreferences(expectedTypes);
            string outside = PlayerPrefs.GetString(OutsideKey);
            Assert.That(ProfileSaveJournal.TryRecover(out string notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            AssertPreferences(before);
            Assert.That(ProfileSaveJournal.TryBegin(out notice), Is.False);
            Assert.That(notice, Is.Not.Empty);
            AssertPreferences(before);
            Assert.That(PlayerPrefs.GetString(OutsideKey), Is.EqualTo(outside));
        }

        private void AssertSlotEmpty()
        {
            Assert.That(store.TryLoad(out string payload, out string notice), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(notice, Is.Null.Or.Empty);
        }

        private void ReplaceJournalPayload(string json)
        {
            Assert.That(store.TryClear(out string notice), Is.True, notice);
            Assert.That(store.TrySave(json, out notice), Is.True, notice);
        }

        private Dictionary<string, byte[]> ReadJournalFiles() => Directory.GetFiles(directory)
            .Where(path => !path.EndsWith(".lock", StringComparison.Ordinal))
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);

        private static void AssertJournalFiles(IReadOnlyDictionary<string, byte[]> files)
        {
            foreach (KeyValuePair<string, byte[]> item in files) Assert.That(File.ReadAllBytes(item.Key), Is.EqualTo(item.Value), item.Key);
        }

        private static void ForgetActiveTransactionForRestartSimulation()
        {
            // This tests the on-disk API contract without claiming a real crash.
            // The separate PlayerProfileAudit runner uses distinct processes.
            foreach (string name in new[] { "activePayload", "activeDirectory" })
                typeof(ProfileSaveJournal).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        }

        private static Dictionary<string, bool> ExpectedWriterKeys()
        {
            // Independent writer inventory: this must not be built by copying
            // ProtectedKeys, or an omitted production writer would go untested.
            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string key in new[]
            {
                "ShatteredSail.ComponentInventory.v2", "ShatteredSail.Collection.Progress.v1",
                "ShatteredSail.Warehouse.Owned.v1", "ShatteredSail.Warehouse.Display.v1", "ShatteredSail.PortWarehouse.Cargo.v1",
                "ShatteredSail.Warehouse.Income.v1",
                "ShatteredSail.TreasureProgress.v1", "ShatteredSail.SeaChart.Progress.v1", "ShatteredSail.GameClockHours.v1",
                "ShatteredSail.ProfileGold.Decimal.v2", "ShatteredSail.CurrentPort.v1", "ShatteredSail.LastVoyageExplorationCoverage.v1", "ShatteredSail.BattleDeck.v1",
                "ShatteredSail.ShipPortProgress.v1",
                "ShatteredSail.BattleCardUpgrades.v1", "ShatteredSail.SpecialCards.Experience.v1", "ShatteredSail.SpecialCards.Acquired.v1",
                "ShatteredSail.BattleDeck.v2.Preparation", "ShatteredSail.BattleDeck.v2.Naval", "ShatteredSail.BattleDeck.v2.Boarding",
            }) result.Add(key, true);
            foreach (string port in new[] { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" })
            {
                result.Add("ShatteredSail.Warehouse.OwnedByPort.v1." + port, true);
                result.Add("ShatteredSail.Warehouse.DisplayByPort.v1." + port, true);
            }
            foreach (string key in new[]
            {
                "ShatteredSail.Warehouse.DisplayRecord.v1", "ShatteredSail.Warehouse.PortMigration.v1",
                "ShatteredSail.ProfileGold.v1", "ShatteredSail.SelectedShip.v1", "ShatteredSail.BattleDeck.Edited.v1",
            }) result.Add(key, false);
            foreach (string cargo in new[] { "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame" })
                result.Add("ShatteredSail.Cargo.v1." + cargo, false);
            foreach (string key in AchievementCatalog.PersistenceKeys) result.Add(key, false);
            return result;
        }
    }
}
