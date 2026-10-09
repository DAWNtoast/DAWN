using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Content;
using ShatteredSail.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        private string voyageTestDirectory;
        private object previousVoyageDirectory;
        private static readonly Type SuspensionType = typeof(MainMenuController).Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence");
        private static readonly FieldInfo DirectoryOverride = SuspensionType.GetField("directoryOverride", BindingFlags.Static | BindingFlags.NonPublic);

        private void IsolateVoyageCheckpointDirectory()
        {
            previousVoyageDirectory = DirectoryOverride.GetValue(null);
            voyageTestDirectory = Path.Combine(Path.GetTempPath(), "ShatteredSailMenuVoyageTests-" + Guid.NewGuid().ToString("N"));
            DirectoryOverride.SetValue(null, voyageTestDirectory);
            SuspensionType.GetField("PendingNotice", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        }

        private void RestoreVoyageCheckpointDirectory()
        {
            DirectoryOverride.SetValue(null, previousVoyageDirectory);
            if (Directory.Exists(voyageTestDirectory)) Directory.Delete(voyageTestDirectory, true);
            if (File.Exists(voyageTestDirectory)) File.Delete(voyageTestDirectory);
        }

        [UnityTest]
        public IEnumerator VoyageSettings_SaveReturnAndContinueRestoresCurrentVoyageWithBackupRecovery()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(1234.5m);
            SceneManager.LoadScene(MainMenuController.MainSceneName);
            yield return null; yield return null;
            CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            game.Session.StartVoyage();
            game.Session.Cargo.Gold += 27;
            game.Session.SetHeading(SailingDirection.SouthEast);
            game.Session.SetDistance(30);
            CrewVariantDefinition crew = CrewNarrativeCatalog.LegacyForProfession(0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), 1);
            ((System.Collections.Generic.List<string>)typeof(CoreGameplayDemoController)
                .GetField("selectedDepartureSpecialCrew", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)).Add(crew.Id);
            InvokeGame(game, "PersistCrewAtSea");
            InvokeGame(game, "Render");
            CoreVoyageCheckpoint expected = game.Session.CaptureVoyageCheckpoint();
            NavalBattleVisualCapture.Capture(game, "01-voyage-settings-entry.png", "航海页设置入口", "Voyage Header");
            int portGold = PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Gold"), -991);
            ActiveButton("Voyage Settings").onClick.Invoke();
            NavalBattleVisualCapture.Capture(game, "02-voyage-settings.png", "航海设置与返回菜单按钮", "Port Settings Overlay");
            ActiveButton("Return To Main Menu").onClick.Invoke();
            NavalBattleVisualCapture.Capture(game, "03-return-confirmation.png", "保存并返回确认", "Return To Main Menu Confirmation");
            ActiveButton("Cancel Return To Main Menu").onClick.Invoke();
            Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(new ProtectedCheckpointStore(voyageTestDirectory).TryLoad(out _, out _), Is.False);
            ActiveButton("Return To Main Menu").onClick.Invoke();
            ActiveButton("Confirm Return To Main Menu").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Gold"), -991), Is.EqualTo(portGold), "Parking a voyage must not bank its cargo.");
            Assert.That(GameObject.Find("Save State").GetComponent<Text>().text, Does.Contain("继续已保存的航程"));
            var store = new ProtectedCheckpointStore(voyageTestDirectory);
            File.WriteAllText(store.PrimaryPath, "interrupted or corrupted disk write");
            SceneManager.LoadScene("MainMenu");
            yield return null; yield return null;
            Assert.That(GameObject.Find("Voyage Save Notice Body").GetComponent<Text>().text, Does.Contain("备份"));
            ActiveButton("Close Voyage Save Notice").onClick.Invoke();
            ActiveButton("Continue Game").onClick.Invoke();
            yield return null; yield return null;
            game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(game.Session.Cargo.Gold, Is.EqualTo(expected.cargo.gold));
            Assert.That(game.Session.ProfileGold, Is.EqualTo(1234.5m));
            Assert.That(game.Session.Heading, Is.EqualTo(SailingDirection.SouthEast));
            Assert.That(game.Session.PlannedDistanceNm, Is.EqualTo(30));
            Assert.That(game.Session.Map.Hash, Is.EqualTo(expected.mapHash));
            Assert.That(game.Session.Position.X, Is.EqualTo(expected.x));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CrewAtSeaKey), Does.Contain(crew.Id));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id)), Is.EqualTo("__AT_SEA__"));
            Assert.That(store.TryLoad(out _, out string notice), Is.False, "A restored snapshot cannot replay rewards a second time.");
            Assert.That(notice, Is.Null);
            ActiveButton("Voyage Settings").onClick.Invoke();
            ActiveButton("Return To Main Menu").onClick.Invoke();
            ActiveButton("Confirm Return To Main Menu").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(store.TryLoad(out _, out notice), Is.True, notice);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuidedVoyage_SettingsAndResumeKeepCurrentInputStep()
        {
            SceneManager.LoadScene(MainMenuController.MainSceneName);
            yield return null; yield return null;
            CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            game.Session.ConfigureGuidedTutorialVoyage(1);
            game.Session.StartVoyage();
            InvokeGame(game, "Render");
            typeof(CoreGameplayDemoController).GetField("guidedTutorialNavigationInputStep", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(game, GuidedTutorialNavigationInputStep.Distance);
            Assert.That(ActiveButton("Voyage Settings").interactable, Is.True);
            ActiveButton("Voyage Settings").onClick.Invoke();
            ActiveButton("Return To Main Menu").onClick.Invoke();
            ActiveButton("Confirm Return To Main Menu").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            ActiveButton("Continue Game").onClick.Invoke();
            yield return null; yield return null;
            game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game.IsTutorialActive, Is.True);
            Assert.That(game.Session.GuidedTutorialLeg, Is.EqualTo(1));
            Assert.That(game.CurrentGuidedTutorialNavigationInputStep, Is.EqualTo(GuidedTutorialNavigationInputStep.Distance));
            Assert.That(ActiveButton("Voyage Settings").interactable, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InvalidVoyage_ContinuePreservesFileAndNewGameRequiresConfirmation()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(987);
            var store = new ProtectedCheckpointStore(voyageTestDirectory);
            Assert.That(store.TrySave("{\"format\":\"future-format\",\"version\":88}", out string reason), Is.True, reason);
            string original = File.ReadAllText(store.PrimaryPath);
            SceneManager.LoadScene("MainMenu");
            yield return null; yield return null;
            Assert.That(GameObject.Find("Voyage Save Notice"), Is.Not.Null);
            ActiveButton("Close Voyage Save Notice").onClick.Invoke();
            ActiveButton("Continue Game").onClick.Invoke();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(987));
            Assert.That(File.ReadAllText(store.PrimaryPath), Is.EqualTo(original));
            ActiveButton("Close Voyage Save Notice").onClick.Invoke();
            ActiveButton("New Game").onClick.Invoke();
            ActiveButton("Cancel New Game").onClick.Invoke();
            Assert.That(File.ReadAllText(store.PrimaryPath), Is.EqualTo(original));
            ActiveButton("New Game").onClick.Invoke();
            ActiveButton("Confirm New Game").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>().Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(store.TryLoad(out _, out reason), Is.False);
            Assert.That(reason, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ReturnToMenu_WhenSaveFails_KeepsVoyageAndShowsRetryableError()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene(MainMenuController.MainSceneName);
            yield return null; yield return null;
            CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            game.Session.StartVoyage();
            game.Session.Cargo.Gold += 51;
            InvokeGame(game, "Render");
            string before = JsonUtility.ToJson(game.Session.CaptureVoyageCheckpoint());
            File.WriteAllText(voyageTestDirectory, "storage unavailable");
            ActiveButton("Voyage Settings").onClick.Invoke();
            ActiveButton("Return To Main Menu").onClick.Invoke();
            ActiveButton("Confirm Return To Main Menu").onClick.Invoke();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.MainSceneName));
            Assert.That(GameObject.Find("Return To Main Menu Message").GetComponent<Text>().text, Does.Contain("未能保存"));
            Assert.That(ActiveButton("Confirm Return To Main Menu").interactable, Is.True);
            Assert.That(ActiveButton("Cancel Return To Main Menu").interactable, Is.True);
            Assert.That(JsonUtility.ToJson(game.Session.CaptureVoyageCheckpoint()), Is.EqualTo(before));
            ActiveButton("Cancel Return To Main Menu").onClick.Invoke();
            Assert.That(GameObject.Find("Return To Main Menu Confirmation"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NewGame_WhenOldVoyageCannotBeCleared_DoesNotResetProfile()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(654);
            File.WriteAllText(voyageTestDirectory, "directory is unavailable");
            SceneManager.LoadScene("MainMenu");
            yield return null; yield return null;
            ActiveButton("New Game").onClick.Invoke();
            ActiveButton("Confirm New Game").onClick.Invoke();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(654));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey), Is.EqualTo(1));
            Assert.That(GameObject.Find("Voyage Save Notice Body").GetComponent<Text>().text, Does.Contain("未重置"));
            LogAssert.NoUnexpectedReceived();
        }

        private static object InvokeGame(CoreGameplayDemoController game, string method)
            => typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
    }
}
