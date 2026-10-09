using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [UnityTest]
        public IEnumerator ProfileJournal_BadJournalBlocksMenuContinueAndProgressCapture()
        {
            using (var journal = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                byte[] damaged = journal.WriteDamagedJournal();
                Dictionary<string, string> before = SnapshotJournalScenePreferences();
                SceneManager.LoadScene("MainMenu");
                yield return null; yield return null;

                Assert.That(Object.FindFirstObjectByType<MainMenuController>().HasSaveData, Is.False);
                Assert.That(GameObject.Find("Voyage Save Notice"), Is.Not.Null);
                Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GameClockKey), Is.False);
                yield return ClickAchievementIconControl("Close Voyage Save Notice");
                yield return ClickAchievementIconControl("Continue Game");
                yield return null;

                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>(), Is.Null);
                Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(before),
                    "A blocked menu must not capture achievements or rewrite the partial profile.");
                Assert.That(File.ReadAllBytes(journal.Store.PrimaryPath), Is.EqualTo(damaged));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator ProfileJournal_DirectGameStartupRejectsJournalBeforeProfileMigration()
        {
            using (var journal = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, "{unread-partial-inventory");
                byte[] damaged = journal.WriteDamagedJournal();
                Dictionary<string, string> before = SnapshotJournalScenePreferences();
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null; yield return null;

                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>(), Is.Null);
                Assert.That(GameObject.Find("Voyage Save Notice"), Is.Not.Null);
                Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GameClockKey), Is.False,
                    "Core Awake must recover before the calendar's first migration write.");
                Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.WarehousePortMigrationKey), Is.False);
                Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(before));
                Assert.That(File.ReadAllBytes(journal.Store.PrimaryPath), Is.EqualTo(damaged));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator ProfileJournal_MenuAwakeRecoversPendingGoldAndCargoBeforeContinue()
        {
            using (var journal = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                Assert.That(ProfileSaveJournal.TryBegin(out string notice), Is.True, notice);
                ProfileGoldPersistence.Save(9999);
                PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 2);
                PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 3);
                PlayerPrefs.Save();
                journal.ForgetActiveForRestartSimulation();
                SceneManager.LoadScene("MainMenu");
                yield return null; yield return null;

                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(750m));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water")), Is.EqualTo(81));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Food")), Is.EqualTo(91));
                Assert.That(journal.Store.TryLoad(out _, out notice), Is.False, notice);
                Assert.That(notice, Is.Null);
                Assert.That(Object.FindFirstObjectByType<MainMenuController>().ContinueButton.IsInteractable(), Is.True);
                yield return ClickAchievementIconControl("Close Voyage Save Notice");
                yield return ClickAchievementIconControl("Continue Game");
                yield return null; yield return null;

                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game, Is.Not.Null);
                Assert.That(game.Session.ProfileGold, Is.EqualTo(750m));
                Assert.That(game.Session.Cargo.Water, Is.EqualTo(81));
                Assert.That(game.Session.Cargo.Food, Is.EqualTo(91));
                Assert.That(game.IsTutorialActive, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator ProfileJournal_BeginFailureKeepsSceneBlocksWritersAndOffersRealRecovery()
        {
            using (var journal = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                yield return new WaitForSecondsRealtime(0.35f);
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game, Is.Not.Null);
                yield return ClickAchievementIconControl("Port Hub Settings");
                yield return ClickAchievementIconControl("Return To Main Menu");
                Dictionary<string, string> before = SnapshotJournalScenePreferences();
                game.Session.GrantProfileGold(137, "Profile journal failure integration fixture");
                game.Session.Cargo.Water -= 17;
                Assert.That(game.Session.RestoreSeaChartProgress(new CoreSeaChartProgress()), Is.True);
                game.Session.RecordCollectionPortVisit("白贝港");
                Directory.CreateDirectory(journal.DirectoryPath);

                using (var held = new FileStream(journal.Store.PrimaryPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    // This is the production save-and-exit callback. Its catch
                    // must see the failed SaveProgress and must not leave.
                    yield return ClickAchievementIconControl("Confirm Return To Main Menu");
                    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.MainSceneName));
                    Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>(), Is.SameAs(game));
                    Assert.That(game.enabled, Is.False);
                    Assert.That(GameObject.Find("Profile Save Failure"), Is.Not.Null);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(ActiveButton("Recover Profile Save").gameObject),
                        "The failed save-and-exit callback must not return keyboard focus to its underlying Cancel button.");

                    bool reachedFollowingAction = false;
                    TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() =>
                    {
                        InvokeGame(game, "SaveProgress");
                        reachedFollowingAction = true;
                    });
                    Assert.That(failure.InnerException, Is.TypeOf<ProfileSaveException>());
                    Assert.That(reachedFollowingAction, Is.False);
                    InvokeGame(game, "Render");
                    InvokeGame(game, "SaveSeaChartProgress");
                    InvokeGame(game, "RefreshCollectionRewards");
                    Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(before),
                        "Neither failed SaveProgress nor later rendering may write the changed in-memory session.");
                    yield return ClickAchievementIconControl("Recover Profile Save");
                    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.MainSceneName));
                    Assert.That(GameObject.Find("Profile Save Failure Detail").GetComponent<Text>().text, Does.Contain("尚未恢复"));
                }

                yield return ClickAchievementIconControl("Recover Profile Save");
                yield return null; yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(750m));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water")), Is.EqualTo(81));
                Assert.That(GameObject.Find("Profile Save Failure"), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static void SeedJournalSceneProfile()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, "profile-journal-scene-" + Guid.NewGuid().ToString("N"));
            ProfileGoldPersistence.Save(750);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 81);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 91);
            PlayerPrefs.Save();
        }

        private static Dictionary<string, string> SnapshotJournalScenePreferences() => ProfileSaveJournal.ProtectedKeys.ToDictionary(key => key,
            key => !PlayerPrefs.HasKey(key) ? "missing" :
                PlayerPrefs.GetString(key, "profile-scene-a") == PlayerPrefs.GetString(key, "profile-scene-b") ? "string:" + PlayerPrefs.GetString(key) :
                "int:" + PlayerPrefs.GetInt(key), StringComparer.Ordinal);

        private sealed class ProfileJournalSceneScope : IDisposable
        {
            private readonly Dictionary<FieldInfo, object> previousStatics = new Dictionary<FieldInfo, object>();
            private readonly FieldInfo menuBlocked = typeof(MainMenuController).GetField("profileRecoveryBlocked", BindingFlags.Static | BindingFlags.NonPublic);
            private readonly object previousMenuBlocked;
            private readonly bool hadCollection, hadDisplayRecord;
            private readonly string previousCollection;
            private readonly int previousDisplayRecord;
            public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "ShatteredSailProfileJournalScene-" + Guid.NewGuid().ToString("N"));
            public ProtectedCheckpointStore Store => new ProtectedCheckpointStore(DirectoryPath);

            public ProfileJournalSceneScope()
            {
                // The existing MainMenu fixture preserves all other profile and
                // voyage keys; these two collection fields are additional here.
                hadCollection = PlayerPrefs.HasKey(CoreGameplayDemoController.CollectionProgressKey);
                previousCollection = PlayerPrefs.GetString(CoreGameplayDemoController.CollectionProgressKey, string.Empty);
                hadDisplayRecord = PlayerPrefs.HasKey(CoreGameplayDemoController.WarehouseDisplayRecordKey);
                previousDisplayRecord = PlayerPrefs.GetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey);
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.CollectionProgressKey);
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseDisplayRecordKey);
                previousMenuBlocked = menuBlocked.GetValue(null);
                foreach (FieldInfo field in typeof(ProfileSaveJournal).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (field.IsLiteral || field.IsInitOnly) continue;
                    previousStatics.Add(field, field.GetValue(null));
                    field.SetValue(null, null);
                }
                typeof(ProfileSaveJournal).GetField("directoryOverride", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, DirectoryPath);
            }

            public byte[] WriteDamagedJournal()
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(Store.PrimaryPath, "interrupted profile journal bytes - preserve verbatim");
                return File.ReadAllBytes(Store.PrimaryPath);
            }

            public void ForgetActiveForRestartSimulation()
            {
                // Scene integration only; the separate Player audit proves real restart behavior.
                foreach (string field in new[] { "activePayload", "activeDirectory" })
                    typeof(ProfileSaveJournal).GetField(field, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            }

            public void Dispose()
            {
                // Remove these scene controllers before restoring shared state;
                // no subsequent Update may run with the caller's journal path.
                foreach (CoreGameplayDemoController game in Object.FindObjectsByType<CoreGameplayDemoController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Object.DestroyImmediate(game.gameObject);
                foreach (MainMenuController menu in Object.FindObjectsByType<MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Object.DestroyImmediate(menu.gameObject);
                foreach (KeyValuePair<FieldInfo, object> pair in previousStatics) pair.Key.SetValue(null, pair.Value);
                menuBlocked.SetValue(null, previousMenuBlocked);
                if (hadCollection) PlayerPrefs.SetString(CoreGameplayDemoController.CollectionProgressKey, previousCollection);
                else PlayerPrefs.DeleteKey(CoreGameplayDemoController.CollectionProgressKey);
                if (hadDisplayRecord) PlayerPrefs.SetInt(CoreGameplayDemoController.WarehouseDisplayRecordKey, previousDisplayRecord);
                else PlayerPrefs.DeleteKey(CoreGameplayDemoController.WarehouseDisplayRecordKey);
                PlayerPrefs.Save();
                string resolved = Path.GetFullPath(DirectoryPath);
                Assert.That(resolved.StartsWith(Path.Combine(Path.GetTempPath(), "ShatteredSailProfileJournalScene-"), StringComparison.OrdinalIgnoreCase), Is.True);
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}
