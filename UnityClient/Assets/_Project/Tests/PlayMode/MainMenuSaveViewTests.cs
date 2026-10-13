using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
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
        [UnityTest]
        public IEnumerator MainMenu_VoyageChangedAfterBuild_ContinueRevalidatesAndCanResumeRepairedSave()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(1234.5m);
            // A real gameplay session creates this identity before any voyage
            // can be parked. The inactive fixture must supply it as well.
            PlayerPrefs.SetString(CoreGameplayDemoController.WorldEconomyScopeKey, "menu-save-view-" + System.Guid.NewGuid().ToString("N"));
            CoreVoyageCheckpoint originalVoyage = SaveMenuVoyage(17);
            SceneManager.LoadScene("MainMenu");
            yield return null; yield return null;

            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.HasSaveData, Is.True);
            Assert.That(menu.ContinueButton.interactable, Is.True);
            Assert.That(menu.ContinueButton.transform.Find("Detail"), Is.Null);
            Assert.That(GameObject.Find("Save State"), Is.Null);

            // The button was enabled from the opening snapshot, while the click
            // must see a checkpoint replaced after the menu became visible.
            var store = new ProtectedCheckpointStore(voyageTestDirectory);
            Assert.That(store.TryClear(out string reason), Is.True, reason);
            Assert.That(store.TrySave("{\"format\":\"future-format\",\"version\":88}", out reason), Is.True, reason);
            byte[] invalidSave = File.ReadAllBytes(store.PrimaryPath);
            menu.ContinueButton.onClick.Invoke();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(GameObject.Find("Voyage Save Notice"), Is.Not.Null);
            Assert.That(File.ReadAllBytes(store.PrimaryPath), Is.EqualTo(invalidSave));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(1234.5m));
            ActiveButton("Close Voyage Save Notice").onClick.Invoke();

            Assert.That(store.TryClear(out reason), Is.True, reason);
            CoreVoyageCheckpoint repairedVoyage = SaveMenuVoyage(83);
            menu.ContinueButton.onClick.Invoke();
            yield return null; yield return null;
            CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(game.Session.Cargo.Gold, Is.EqualTo(repairedVoyage.cargo.gold));
            Assert.That(game.Session.Cargo.Gold, Is.Not.EqualTo(originalVoyage.cargo.gold));
            Assert.That(game.Session.Map.Hash, Is.EqualTo(repairedVoyage.mapHash));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MainMenu_ProfileAddedAfterBuild_NewGameChecksCurrentSaveBeforeReset()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null; yield return null;
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu.ContinueButton.interactable, Is.False);
            Assert.That(menu.HasSaveData, Is.False);

            ProfileGoldPersistence.Save(987.5m);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "灰鸥港");
            PlayerPrefs.Save();
            Assert.That(menu.HasSaveData, Is.True);
            ActiveButton("New Game").onClick.Invoke();
            Assert.That(GameObject.Find("New Game Confirmation"), Is.Not.Null);
            ActiveButton("Cancel New Game").onClick.Invoke();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(987.5m));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey), Is.EqualTo("灰鸥港"));
            LogAssert.NoUnexpectedReceived();
        }

        private static CoreVoyageCheckpoint SaveMenuVoyage(int cargoGold)
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(1234.5m, "断锚港");
            session.SetSeed("MENU-SAVE-VIEW-ISOLATED");
            session.Cargo.Water = 100;
            session.Cargo.Food = 100;
            session.StartVoyage();
            session.Cargo.Gold = cargoGold;
            Assert.That(session.CanCaptureVoyageCheckpoint, Is.True);
            CoreVoyageCheckpoint checkpoint = session.CaptureVoyageCheckpoint();

            var host = new GameObject("Inactive Menu Save View Fixture");
            host.SetActive(false);
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(CoreGameplayDemoController).GetField("session", flags).SetValue(controller, session);
                typeof(CoreGameplayDemoController).GetField("selectedDepartureOrdinaryCrew", flags).SetValue(controller, session.SelectedOrdinaryCrew);
                typeof(CoreGameplayDemoController).GetField("departureCrewShipIndex", flags).SetValue(controller, session.SelectedShipIndex);
                typeof(CoreGameplayDemoController).GetField("departureCrewPort", flags).SetValue(controller, session.Map.DepartureName);
                object[] saveArgs = { null };
                bool saved = (bool)typeof(CoreGameplayDemoController).GetMethod("TrySaveVoyageSuspension", flags).Invoke(controller, saveArgs);
                Assert.That(saved, Is.True, (string)saveArgs[0]);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
            return checkpoint;
        }
    }
}
