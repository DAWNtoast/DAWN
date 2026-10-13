using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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
        public IEnumerator ReleaseSmoke_MenuLeavesPendingRecoveryAchievementsAndVoyageBytesUntouched()
        {
            FieldInfo arguments = typeof(ReleasePlayerSmokeCheck).GetField("commandLineArguments", BindingFlags.Static | BindingFlags.NonPublic);
            object previousArguments = arguments.GetValue(null);
            string[] volumeKeys = { MainMenuController.MasterVolumeKey, MainMenuController.MusicVolumeKey, MainMenuController.EffectsVolumeKey };
            var volumeExists = volumeKeys.ToDictionary(key => key, PlayerPrefs.HasKey);
            var volumes = volumeKeys.ToDictionary(key => key, key => PlayerPrefs.GetFloat(key, .5f));
            bool hadFullscreen = PlayerPrefs.HasKey(MainMenuController.FullScreenKey);
            int fullscreen = PlayerPrefs.GetInt(MainMenuController.FullScreenKey);
            using (var journal = new ProfileJournalSceneScope())
            {
                try
                {
                    SeedJournalSceneProfile();
                    foreach (string key in AchievementCatalog.PersistenceKeys) PlayerPrefs.DeleteKey(key);
                    Assert.That(ProfileSaveJournal.TryBegin(out string notice), Is.True, notice);
                    ProfileGoldPersistence.Save(9999);
                    PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 2);
                    PlayerPrefs.Save();
                    journal.ForgetActiveForRestartSimulation();
                    Directory.CreateDirectory(voyageTestDirectory);
                    File.WriteAllText(new ProtectedCheckpointStore(voyageTestDirectory).PrimaryPath, "unreadable voyage - preserve exactly");
                    var beforePreferences = SnapshotJournalScenePreferences();
                    var beforeJournal = SmokeFiles(journal.DirectoryPath);
                    var beforeVoyage = SmokeFiles(voyageTestDirectory);
                    arguments.SetValue(null, new[] { "game.exe", "-shatteredSailSmokeTest", Path.Combine(journal.DirectoryPath, "capture") });

                    SceneManager.LoadScene("MainMenu");
                    yield return null; yield return null;
                    MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
                    Assert.That(menu, Is.Not.Null);
                    Assert.That(menu.HasSaveData, Is.False, "Smoke must not probe the suspended-voyage file store for labels.");
                    Assert.That(GameObject.Find("Voyage Save Notice"), Is.Null, "Pending recovery is left for the next normal launch.");
                    Assert.That(GameClientInfo.DisplayName, Is.EqualTo("第七港口"));
                    RawImage titleArtwork = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None)
                        .SingleOrDefault(image => image.name == "Game Title Artwork");
                    Assert.That(titleArtwork, Is.Not.Null);
                    Assert.That(titleArtwork.texture, Is.Not.Null);
                    Assert.That(titleArtwork.texture, Is.SameAs(Resources.Load<Texture2D>("MainMenu/main_menu_title_bilingual")));
                    Assert.That(ActiveButton("New Game").interactable, Is.False);
                    Assert.That(menu.ContinueButton.interactable, Is.False);

                    yield return ClickAchievementIconControl("Settings");
                    Slider master = GameObject.Find("Master Volume").GetComponent<Slider>();
                    master.value = master.value > .5f ? .2f : .8f;
                    Assert.That(ActiveButton("Apply Settings").interactable, Is.False);
                    ActiveButton("Apply Settings").onClick.Invoke();
                    yield return ClickAchievementIconControl("Back From Settings");
                    // Disabled UI alone cannot protect against a retained callback.
                    menu.ContinueButton.onClick.Invoke();
                    ActiveButton("New Game").onClick.Invoke();
                    typeof(MainMenuController).GetMethod("StartNewGame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    yield return ClickAchievementIconControl("Achievements");
                    yield return ClickAchievementIconControl("Back From Achievements");

                    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                    Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>(), Is.Null);
                    Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(beforePreferences));
                    Assert.That(SmokeFiles(journal.DirectoryPath), Is.EquivalentTo(beforeJournal), "No recovery, clear marker or new journal file.");
                    Assert.That(SmokeFiles(voyageTestDirectory), Is.EquivalentTo(beforeVoyage), "No probe-created lock file or mutation of unreadable voyage bytes.");
                    foreach (string key in volumeKeys)
                    {
                        Assert.That(PlayerPrefs.HasKey(key), Is.EqualTo(volumeExists[key]), key);
                        Assert.That(PlayerPrefs.GetFloat(key, .5f), Is.EqualTo(volumes[key]), key);
                    }
                    Assert.That(PlayerPrefs.HasKey(MainMenuController.FullScreenKey), Is.EqualTo(hadFullscreen));
                    Assert.That(PlayerPrefs.GetInt(MainMenuController.FullScreenKey), Is.EqualTo(fullscreen));
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    arguments.SetValue(null, previousArguments);
                    foreach (string key in volumeKeys)
                        if (volumeExists[key]) PlayerPrefs.SetFloat(key, volumes[key]); else PlayerPrefs.DeleteKey(key);
                    if (hadFullscreen) PlayerPrefs.SetInt(MainMenuController.FullScreenKey, fullscreen);
                    else PlayerPrefs.DeleteKey(MainMenuController.FullScreenKey);
                }
            }
        }

        private static Dictionary<string, string> SmokeFiles(string directory) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path.Substring(directory.Length), path => Convert.ToBase64String(File.ReadAllBytes(path)), StringComparer.Ordinal);
    }
}
