using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Tests the real suspension boundary without activating gameplay Awake, changing
    /// scenes, or writing the player's port profile. All checkpoint files are isolated.
    /// </summary>
    public sealed class VoyageSuspensionFlowTests
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<GameObject> hosts = new List<GameObject>();
        private string directory;
        private Type persistenceType;
        private FieldInfo directoryOverride;
        private object previousDirectory;
        private PropertyInfo pendingNotice;
        private object previousNotice;
        private PortProfileSnapshot profile;

        [SetUp]
        public void CreateIsolatedStorage()
        {
            Assert.That(Application.isPlaying, Is.True);
            profile = PortProfileSnapshot.Read();
            directory = Path.Combine(Path.GetTempPath(), "ShatteredSailSuspensionFlow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            persistenceType = typeof(CoreGameplayDemoController).Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true);
            directoryOverride = persistenceType.GetField("directoryOverride", StaticFlags);
            Assert.That(directoryOverride, Is.Not.Null, "Persistence must support an isolated directory for this test.");
            previousDirectory = directoryOverride.GetValue(null);
            directoryOverride.SetValue(null, directory);
            pendingNotice = persistenceType.GetProperty("PendingNotice", StaticFlags);
            if (pendingNotice != null)
            {
                previousNotice = pendingNotice.GetValue(null);
                if (pendingNotice.GetSetMethod(true) != null) pendingNotice.SetValue(null, null);
            }
        }

        [UnityTearDown]
        public IEnumerator DisposeIsolatedStorage()
        {
            foreach (GameObject host in hosts) if (host != null) Object.Destroy(host);
            hosts.Clear();
            yield return null;
            if (directoryOverride != null) directoryOverride.SetValue(null, previousDirectory);
            if (pendingNotice != null && pendingNotice.GetSetMethod(true) != null) pendingNotice.SetValue(null, previousNotice);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                string resolved = Path.GetFullPath(directory);
                string allowedPrefix = Path.Combine(Path.GetTempPath(), "ShatteredSailSuspensionFlow-");
                Assert.That(resolved.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(resolved, true);
            }
            if (profile != null) profile.AssertUnchanged();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavingVoyageDoesNotSettleOrWritePortGoldAndCargo()
        {
            CoreGameplayDemoController controller = CreateInactiveController(CreateVoyage(false));
            CoreVoyageSession session = controller.Session;
            string before = JsonUtility.ToJson(session.CaptureVoyageCheckpoint());

            Assert.That(Save(controller, out string reason), Is.True, reason);

            Assert.That(controller.Session, Is.SameAs(session));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Settlement, Is.Null);
            Assert.That(JsonUtility.ToJson(session.CaptureVoyageCheckpoint()), Is.EqualTo(before));
            Assert.That(TryRead(out object record, out string notice), Is.True, notice);
            Assert.That(record, Is.Not.Null);
            Assert.That(ReadRecord<CoreVoyageCheckpoint>(record, "voyage").cargo.gold, Is.EqualTo(session.Cargo.Gold));
            Assert.That(HasCheckpoint(), Is.True);
            profile.AssertUnchanged();
        }

        [Test]
        public void SuccessfulRestoreConsumesCheckpointAndCannotReplayStaleBackup()
        {
            CoreGameplayDemoController source = CreateInactiveController(CreateVoyage(false));
            string expected = JsonUtility.ToJson(source.Session.CaptureVoyageCheckpoint());
            Assert.That(Save(source, out string reason), Is.True, reason);
            byte[] oldCheckpoint = File.ReadAllBytes(PrimaryPath);
            Assert.That(TryRead(out object record, out string notice), Is.True, notice);
            Assert.That(record, Is.Not.Null);
            CoreGameplayDemoController restored = CreateInactiveController(new CoreVoyageSession());

            Assert.That(Restore(restored, record, out reason), Is.True, reason);

            Assert.That(JsonUtility.ToJson(restored.Session.CaptureVoyageCheckpoint()), Is.EqualTo(expected));
            Assert.That(TryRead(out object consumed, out notice), Is.True, notice);
            Assert.That(consumed, Is.Null, "A resumed voyage must not remain claimable from the menu.");
            Assert.That(HasCheckpoint(), Is.False);
            // Even interrupted cleanup or a reappearing old copy must not undo consumption.
            File.WriteAllBytes(PrimaryPath + ".bak1", oldCheckpoint);
            Assert.That(TryRead(out consumed, out notice), Is.True, notice);
            Assert.That(consumed, Is.Null);
            Assert.That(HasCheckpoint(), Is.False);
            profile.AssertUnchanged();
        }

        [TestCase(GuidedTutorialNavigationInputStep.Heading)]
        [TestCase(GuidedTutorialNavigationInputStep.Distance)]
        [TestCase(GuidedTutorialNavigationInputStep.Sail)]
        public void TutorialResumeMigratesRetiredDistanceStepAndPreservesCrewSelection(GuidedTutorialNavigationInputStep step)
        {
            CoreGameplayDemoController source = CreateInactiveController(CreateVoyage(true));
            WriteField(source, "tutorialActive", true);
            WriteField(source, "guidedTutorialCheckpoint", 0);
            WriteField(source, "guidedTutorialNavigationInputStep", step);
            ReadField<List<string>>(source, "selectedDepartureSpecialCrew").Add("crew_broken_anchor_navigator_lorne");
            ReadField<HashSet<string>>(source, "departureStandbyCrew").Add("crew_broken_anchor_carpenter_kia");
            Assert.That(Save(source, out string reason), Is.True, reason);
            Assert.That(TryRead(out object record, out string notice), Is.True, notice);
            Assert.That(ReadRecord<int>(record, "navigationInputStep"), Is.EqualTo((int)step));
            CoreGameplayDemoController restored = CreateInactiveController(new CoreVoyageSession());

            Assert.That(Restore(restored, record, out reason), Is.True, reason);

            Assert.That(restored.IsTutorialActive, Is.True);
            GuidedTutorialNavigationInputStep expected = step == GuidedTutorialNavigationInputStep.Distance
                ? GuidedTutorialNavigationInputStep.Sail : step;
            Assert.That(restored.CurrentGuidedTutorialNavigationInputStep, Is.EqualTo(expected));
            Assert.That(restored.Session.PlannedDistanceNm, Is.EqualTo(CoreGuidedTutorialContent.PlannedDistanceNm),
                "A suspended legacy slider value must normalize to the fixed teaching route before forecasting.");
            Assert.That(restored.Session.TotalDistanceNm, Is.EqualTo(source.Session.TotalDistanceNm),
                "Migrating the retired distance lesson must not execute a voyage action.");
            Assert.That(restored.Session.Cargo.Water, Is.EqualTo(source.Session.Cargo.Water));
            Assert.That(restored.Session.Cargo.Food, Is.EqualTo(source.Session.Cargo.Food));
            Assert.That(restored.Session.GuidedTutorialLeg, Is.EqualTo(source.Session.GuidedTutorialLeg));
            Assert.That(ReadField<int>(restored, "guidedTutorialCheckpoint"), Is.Zero);
            Assert.That(ReadField<int>(restored, "selectedDepartureOrdinaryCrew"), Is.EqualTo(source.Session.SelectedOrdinaryCrew));
            Assert.That(ReadField<int>(restored, "departureCrewShipIndex"), Is.EqualTo(source.Session.SelectedShipIndex));
            Assert.That(ReadField<string>(restored, "departureCrewPort"), Is.EqualTo(source.Session.Map.DepartureName));
            Assert.That(ReadField<List<string>>(restored, "selectedDepartureSpecialCrew"), Is.EqualTo(new[] { "crew_broken_anchor_navigator_lorne" }));
            Assert.That(ReadField<HashSet<string>>(restored, "departureStandbyCrew"), Is.EquivalentTo(new[] { "crew_broken_anchor_carpenter_kia" }));
            profile.AssertUnchanged();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyAppraisalSnapshotRestoresRarityStrengthFromPhysicalLoadout(bool emptyLoadout)
        {
            CoreGameplayDemoController source = CreateInactiveController(CreateVoyage(false, 2.7f, 3.3f));
            CoreVoyageCheckpoint expected = source.Session.CaptureVoyageCheckpoint();
            Assert.That(Save(source, out string reason), Is.True, reason);
            Assert.That(TryRead(out object record, out string notice), Is.True, notice);
            Assert.That(ReadRecord<CoreVoyageCheckpoint>(record, "voyage").battleBowQuality, Is.EqualTo(2.7f));
            Assert.That(ReadRecord<CoreVoyageCheckpoint>(record, "voyage").battleHullQuality, Is.EqualTo(3.3f));
            CoreGameplayDemoController restored = CreateInactiveController(new CoreVoyageSession());
            var catalog = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(catalog);
            if (emptyLoadout) inventory.InitializeEmptyLoadout(0);
            else
            {
                ComponentArtEntry bow = catalog.Entries.First(item => item.Slot == ComponentSlot.Bow && item.Rarity == ComponentRarity.Rare);
                ComponentArtEntry plating = catalog.Entries.First(item => item.Slot == ComponentSlot.Plating && item.Rarity == ComponentRarity.Rare);
                string legacy = "{\"version\":2,\"initializedShips\":[0],\"items\":[" +
                    "{\"instanceId\":\"old-bow\",\"componentId\":\"" + bow.Id + "\",\"quality\":0.9,\"shipIndex\":0}," +
                    "{\"instanceId\":\"old-plating\",\"componentId\":\"" + plating.Id + "\",\"quality\":1.1,\"shipIndex\":0}]}";
                Assert.That(inventory.Import(legacy, ship => ship == 0), Is.True);
            }
            WriteField(restored, "componentArt", catalog);
            WriteField(restored, "componentInventory", inventory);

            Assert.That(Restore(restored, record, out reason), Is.True, reason);

            expected.battleBowQuality = expected.battleHullQuality = emptyLoadout ? 1f : 3f;
            Assert.That(JsonUtility.ToJson(restored.Session.CaptureVoyageCheckpoint()), Is.EqualTo(JsonUtility.ToJson(expected)),
                "Resume changes only retired appraisal strengths; the voyage, cargo, hull and random state must survive intact.");
            Assert.That(inventory.Items.Count, Is.EqualTo(emptyLoadout ? 0 : 2));
            Assert.That(inventory.Export(), Does.Not.Contain("quality"));
            Assert.That(HasCheckpoint(), Is.False);
            profile.AssertUnchanged();
        }

        [Test]
        public void CorruptPrimaryUsesRedundantSnapshotAndReportsRecovery()
        {
            CoreGameplayDemoController source = CreateInactiveController(CreateVoyage(false));
            Assert.That(Save(source, out string reason), Is.True, reason);
            File.WriteAllText(PrimaryPath, "damaged primary checkpoint");

            Assert.That(TryRead(out object record, out string notice), Is.True, notice);

            Assert.That(record, Is.Not.Null);
            Assert.That(notice, Does.Contain("备份"));
            Assert.That(ReadRecord<CoreVoyageCheckpoint>(record, "voyage").mapHash, Is.EqualTo(source.Session.Map.Hash));
            Assert.That(File.ReadAllText(PrimaryPath), Is.EqualTo("damaged primary checkpoint"));
            profile.AssertUnchanged();
        }

        [Test]
        public void AllDamagedFilesRemainIntactAndRejectFurtherSaving()
        {
            CoreGameplayDemoController controller = CreateInactiveController(CreateVoyage(false));
            Assert.That(Save(controller, out string reason), Is.True, reason);
            string before = JsonUtility.ToJson(controller.Session.CaptureVoyageCheckpoint());
            var damagedFiles = new Dictionary<string, byte[]>();
            foreach (string path in Directory.GetFiles(directory, "voyage.checkpoint*"))
            {
                string name = Path.GetFileName(path);
                if (name != "voyage.checkpoint" && !name.StartsWith("voyage.checkpoint.bak", StringComparison.Ordinal)) continue;
                byte[] bytes = { 0xff, 0x01, 0x00, 0xfe };
                File.WriteAllBytes(path, bytes);
                damagedFiles.Add(path, bytes);
            }
            Assert.That(damagedFiles.Count, Is.GreaterThanOrEqualTo(2));

            Assert.That(TryRead(out object record, out string notice), Is.False);
            Assert.That(record, Is.Null);
            Assert.That(notice, Is.Not.Null.And.Not.Empty);
            Assert.That(Save(controller, out reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);

            foreach (KeyValuePair<string, byte[]> damaged in damagedFiles)
                Assert.That(File.ReadAllBytes(damaged.Key), Is.EqualTo(damaged.Value));
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureVoyageCheckpoint()), Is.EqualTo(before));
            profile.AssertUnchanged();
        }

        [Test]
        public void UnwritableStoragePathKeepsSessionAndPortProfileUnchanged()
        {
            CoreGameplayDemoController controller = CreateInactiveController(CreateVoyage(false));
            CoreVoyageSession session = controller.Session;
            string before = JsonUtility.ToJson(session.CaptureVoyageCheckpoint());
            string blocker = Path.Combine(directory, "directory-is-a-file");
            File.WriteAllText(blocker, "must remain intact");
            directoryOverride.SetValue(null, blocker);

            Assert.That(Save(controller, out string reason), Is.False);

            Assert.That(reason, Is.Not.Null.And.Not.Empty);
            Assert.That(controller.Session, Is.SameAs(session));
            Assert.That(JsonUtility.ToJson(session.CaptureVoyageCheckpoint()), Is.EqualTo(before));
            Assert.That(File.ReadAllText(blocker), Is.EqualTo("must remain intact"));
            profile.AssertUnchanged();
        }

        private string PrimaryPath { get { return Path.Combine(directory, "voyage.checkpoint"); } }

        private CoreGameplayDemoController CreateInactiveController(CoreVoyageSession session)
        {
            var host = new GameObject("Inactive Voyage Suspension Test Controller");
            host.SetActive(false);
            hosts.Add(host);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null, "Gameplay Awake must not run in this isolated test.");
            WriteField(controller, "session", session);
            WriteField(controller, "tutorialActive", false);
            WriteField(controller, "selectedDepartureOrdinaryCrew", session.SelectedOrdinaryCrew);
            WriteField(controller, "departureCrewShipIndex", session.SelectedShipIndex);
            WriteField(controller, "departureCrewPort", session.Map == null ? session.CurrentPort : session.Map.DepartureName);
            return controller;
        }

        private static CoreVoyageSession CreateVoyage(bool tutorial, float bowStrength = 1f, float platingStrength = 1f)
        {
            var session = new CoreVoyageSession();
            session.ConfigureBattleEquipment(bowStrength, platingStrength);
            session.RestoreProfile(731.2468m, "断锚港");
            session.SetSeed("SUSPENSION-FLOW-ISOLATED");
            session.Cargo.Water = 73;
            session.Cargo.Food = 71;
            session.Cargo.Gold = 17;
            if (tutorial)
            {
                session.ConfigureCrewCapabilities(true, false, false, false, false, false);
                session.ConfigureSpecialCardSources(new[] { "crew_broken_anchor_navigator_lorne" }, Array.Empty<string>());
                Assert.That(session.ConfigureGuidedTutorialVoyage(1), Is.True);
            }
            session.StartVoyage();
            session.Cargo.Water -= 3;
            session.Cargo.Food -= 2;
            session.Cargo.Gold += 19;
            session.SetHeading(SailingDirection.East);
            session.SetDistance(160);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.CanCaptureVoyageCheckpoint, Is.True);
            return session;
        }

        private bool TryRead(out object record, out string notice)
        {
            object[] args = { null, null };
            bool success = (bool)StaticMethod("TryRead").Invoke(null, args);
            record = args[0];
            notice = (string)args[1];
            return success;
        }

        private bool HasCheckpoint()
        {
            PropertyInfo property = persistenceType.GetProperty("HasCheckpoint", StaticFlags);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(null);
        }

        private MethodInfo StaticMethod(string name)
        {
            MethodInfo method = persistenceType.GetMethod(name, StaticFlags);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }

        private static bool Save(CoreGameplayDemoController controller, out string reason)
        {
            object[] args = { null };
            bool result = (bool)ControllerMethod("TrySaveVoyageSuspension").Invoke(controller, args);
            reason = (string)args[0];
            return result;
        }

        private static bool Restore(CoreGameplayDemoController controller, object record, out string reason)
        {
            object[] args = { record, null };
            bool result = (bool)ControllerMethod("TryRestoreSuspendedVoyage").Invoke(controller, args);
            reason = (string)args[1];
            return result;
        }

        private static MethodInfo ControllerMethod(string name)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, InstanceFlags);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }

        private static T ReadRecord<T>(object record, string name)
        {
            Assert.That(record, Is.Not.Null);
            return (T)record.GetType().GetField(name, InstanceFlags).GetValue(record);
        }

        private static T ReadField<T>(CoreGameplayDemoController controller, string name)
        {
            return (T)typeof(CoreGameplayDemoController).GetField(name, InstanceFlags).GetValue(controller);
        }

        private static void WriteField(CoreGameplayDemoController controller, string name, object value)
        {
            FieldInfo field = typeof(CoreGameplayDemoController).GetField(name, InstanceFlags);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(controller, value);
        }

        private sealed class PortProfileSnapshot
        {
            private static readonly string[] IntegerKeys = new[]
            {
                "Water", "Food", "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs", "Gold", "Fame",
            }.Select(CoreGameplayDemoController.CargoKey).Concat(new[]
            {
                CoreGameplayDemoController.ProfileGoldKey,
                CoreGameplayDemoController.TutorialCompletedKey,
                CoreGameplayDemoController.TutorialRewardGrantedKey,
                CoreGameplayDemoController.GuidedTutorialCheckpointKey,
            }).ToArray();
            private static readonly string[] StringKeys = { ProfileGoldPersistence.DecimalKey, CoreGameplayDemoController.CurrentPortKey, CoreGameplayDemoController.WarehouseIncomeKey };
            private bool[] integerExists;
            private int[] integers;
            private bool[] stringExists;
            private string[] strings;

            public static PortProfileSnapshot Read()
            {
                return new PortProfileSnapshot
                {
                    integerExists = IntegerKeys.Select(PlayerPrefs.HasKey).ToArray(),
                    integers = IntegerKeys.Select(key => PlayerPrefs.GetInt(key, -876543)).ToArray(),
                    stringExists = StringKeys.Select(PlayerPrefs.HasKey).ToArray(),
                    strings = StringKeys.Select(key => PlayerPrefs.GetString(key, "<missing>" )).ToArray(),
                };
            }

            public void AssertUnchanged()
            {
                PortProfileSnapshot after = Read();
                Assert.That(after.integerExists, Is.EqualTo(integerExists), "Suspension must not create port profile keys.");
                Assert.That(after.integers, Is.EqualTo(integers), "Voyage cargo and rewards must not be committed as port resources.");
                Assert.That(after.stringExists, Is.EqualTo(stringExists));
                Assert.That(after.strings, Is.EqualTo(strings), "Suspension must not change the port purse or location.");
            }
        }
    }
}
