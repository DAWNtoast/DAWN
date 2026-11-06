using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [UnityTest]
        public IEnumerator CardPackTransaction_BossVictorySavesPackAndChartWithoutBankingVoyageCargo()
        {
            using (var journal = new ProfileJournalSceneScope())
            {
                SeedCardPackTransactionProfile();
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(Application.isPlaying, Is.True);
                Assert.That(game, Is.Not.Null);
                StartCardPackTransactionVoyage(game.Session);
                InvokeGame(game, "SaveSeaChartProgress");
                Dictionary<string, string> before = SnapshotJournalScenePreferences();

                game.Session.Cargo.Water -= 17;
                game.Session.Cargo.Gold += 57;
                game.Session.GrantProfileGold(137, "Unsettled card-pack transaction fixture");
                CoreRouteBoss boss = WinCardPackTransactionBoss(game.Session);
                string expectedPacks = JsonUtility.ToJson(game.Session.CaptureCardPackProgress());
                string expectedChart = JsonUtility.ToJson(game.Session.CaptureSeaChartProgress());
                InvokeGame(game, "SaveSeaChartProgress");

                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(expectedPacks));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey), Is.EqualTo(expectedChart));
                Dictionary<string, string> after = SnapshotJournalScenePreferences();
                foreach (string key in before.Keys.Where(key =>
                    key != CoreGameplayDemoController.CardPackProgressKey && key != CoreGameplayDemoController.SeaChartProgressKey))
                    Assert.That(after[key], Is.EqualTo(before[key]), "Narrow victory save changed unrelated profile key: " + key);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Water")), Is.EqualTo(81));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(750m));
                Assert.That(journal.Store.TryLoad(out _, out string notice), Is.False, notice);

                InvokeGame(game, "SaveSeaChartProgress");
                InvokeGame(game, "Render");
                InvokeGame(game, "Render");
                Assert.That(game.Session.GetCardPackCount(boss.SchoolId), Is.EqualTo(1));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(expectedPacks));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey), Is.EqualTo(expectedChart));

                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game, Is.Not.Null);
                Assert.That(game.Session.GetCardPackCount(boss.SchoolId), Is.EqualTo(1));
                Assert.That(game.Session.CaptureSeaChartProgress().charts.Any(chart => chart.defeatedBosses.Contains(boss.Id)), Is.True);
                InvokeGame(game, "Render");
                Assert.That(game.Session.GetCardPackCount(boss.SchoolId), Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator CardPackTransaction_BeginAndCommitStorageFailuresKeepPackAndChartTogether()
        {
            foreach (bool failAtCommit in new[] { false, true })
            {
                using (var journal = new ProfileJournalSceneScope())
                {
                    SeedCardPackTransactionProfile();
                    SceneManager.LoadScene(MainMenuController.MainSceneName);
                    yield return null; yield return null;
                    CoreGameplayDemoController game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                    Assert.That(game, Is.Not.Null);
                    StartCardPackTransactionVoyage(game.Session);
                    InvokeGame(game, "SaveSeaChartProgress");
                    Dictionary<string, string> before = SnapshotJournalScenePreferences();
                    WinCardPackTransactionBoss(game.Session);

                    if (failAtCommit)
                    {
                        // Pause the exact production protocol at its commit boundary,
                        // so a real storage lock can fail TryComplete deterministically.
                        Assert.That(ProfileSaveJournal.TryBegin(out string beginNotice), Is.True, beginNotice);
                        Assert.That((bool)InvokeGame(game, "QueueSeaChartProgress"), Is.True);
                        InvokeGame(game, "SaveCardPacks");
                        PlayerPrefs.Save();
                        Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey),
                            Is.EqualTo(JsonUtility.ToJson(game.Session.CaptureCardPackProgress())));
                        Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey),
                            Is.EqualTo(JsonUtility.ToJson(game.Session.CaptureSeaChartProgress())));
                    }
                    Directory.CreateDirectory(journal.DirectoryPath);
                    using (var held = new FileStream(journal.Store.PrimaryPath + ".lock",
                        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        if (failAtCommit)
                        {
                            Assert.That(ProfileSaveJournal.TryComplete(out string commitNotice), Is.False);
                            Assert.That(commitNotice, Is.Not.Empty);
                        }
                        else
                        {
                            TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() =>
                                InvokeGame(game, "SaveSeaChartProgress"));
                            Assert.That(failure.InnerException, Is.TypeOf<ProfileSaveException>());
                            Assert.That(game.enabled, Is.False);
                            InvokeGame(game, "SaveSeaChartProgress");
                            Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(before),
                                "A failed transaction start cannot write either reward or depletion.");
                        }
                    }

                    journal.ForgetActiveForRestartSimulation();
                    Assert.That(ProfileSaveJournal.TryRecover(out string recoveryNotice), Is.True, recoveryNotice);
                    Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(before),
                        "After an interrupted commit, both the earned pack and defeated-boss marker must roll back.");
                    Assert.That(journal.Store.TryLoad(out _, out string clearedNotice), Is.False, clearedNotice);
                    LogAssert.NoUnexpectedReceived();
                }
            }
        }

        private static void SeedCardPackTransactionProfile()
        {
            SeedJournalSceneProfile();
            PlayerPrefs.SetString(CoreGameplayDemoController.CardPackProgressKey,
                JsonUtility.ToJson(new CoreCardPackProgress { randomState = 1234567u }));
            PlayerPrefs.SetString(CoreGameplayDemoController.SeaChartProgressKey, JsonUtility.ToJson(new CoreSeaChartProgress()));
            PlayerPrefs.Save();
        }

        private static void StartCardPackTransactionVoyage(CoreVoyageSession session)
        {
            // Core's direct random-voyage API does not select a persistent chart.
            // Use the same explicit chart selection as the departure flow.
            string seed = CoreVoyageSession.CreateChartSeed(0, session.CurrentPort, "白贝港");
            Assert.That(session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing), "Selected chart voyage did not start.");
            Assert.That(session.CurrentChartSeed, Is.EqualTo(seed), "Fixture requires an active persistent chart.");
            Assert.That(session.CaptureSeaChartProgress().charts.Any(chart => chart.seed == seed), Is.True,
                "Starting the selected chart must record its initial visit before the battle.");
        }

        private static CoreRouteBoss WinCardPackTransactionBoss(CoreVoyageSession session)
        {
            CoreMapNode node = session.Map.Nodes.FirstOrDefault(candidate =>
                CoreRouteBossCatalog.Find(candidate.Encounter.Id) != null);
            Assert.That(node, Is.Not.Null, "Selected chart has no authored route-boss node.");
            CoreRouteBoss boss = CoreRouteBossCatalog.Find(node.Encounter.Id);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            Assert.That(session.ResolveChoice(0), Is.True, "Cannot enter chart boss battle: " + boss.Id);
            Assert.That(session.Battle, Is.Not.Null, "Boss encounter did not start combat: " + boss.Id);
            session.Battle.EnemyHull = 0;
            Assert.That(session.EndBattleTurn(), Is.True, "Cannot resolve zero-hull boss victory: " + boss.Id);
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.GetCardPackCount(boss.SchoolId), Is.EqualTo(1));
            Assert.That(session.CaptureSeaChartProgress().charts.Any(chart => chart.defeatedBosses.Contains(boss.Id)), Is.True,
                "Victory did not record " + boss.Id + " on " + session.CurrentChartSeed + ": " +
                JsonUtility.ToJson(session.CaptureSeaChartProgress()));
            return boss;
        }
    }
}

