using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator AchievementChallenge_LegacyCheckpointReturnRestoresEditedStagesForNextVoyage()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                AchievementCatalog.RecordDeckEdited(NavalBattleStage.Preparation);
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.RestoreCollectionProgress(new CoreCollectionProgress
                    { defeatedBossIds = new[] { CoreGuidedTutorialContent.BossEventId } }), Is.True);
                game.Session.StartVoyage();
                CoreVoyageCheckpoint checkpoint = game.Session.CaptureVoyageCheckpoint();
                checkpoint.achievementProgressVersion = 0;
                checkpoint.achievementEditedDeckStages = 0;
                checkpoint.achievementVoyageSupplyEvidenceAvailable = false;
                Assert.That(game.Session.SafeReturn(), Is.True);
                game.Session.ReturnToPortAfterSettlement();
                Assert.That(game.Session.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
                Assert.That(game.Session.CaptureVoyageCheckpoint().achievementEditedDeckStages, Is.Zero);
                Assert.That(game.Session.SafeReturn(), Is.True);
                InvokeGame(game, "ConfirmVoyageSettlement");
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey), Does.Contain("first_victory_compass"),
                    "The old boss qualification is still reconciled into the warehouse.");
                Assert.That(VoyageAchievementUnlocked("warehouse_one"), Is.False, "A legacy checkpoint cannot turn old unclaimed collection eligibility into a new voyage reward.");
                InvokeGame(game, "SaveStageBattleDecks");
                game.Session.StartVoyage();
                Assert.That(game.Session.CaptureVoyageCheckpoint().achievementEditedDeckStages, Is.EqualTo(1),
                    "The next departure uses the profile's authored stages, not the legacy checkpoint's empty mask.");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AchievementChallenge_RealBattleAndDamagedReturnCommitOnlyAfterConfirmation()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                AchievementCatalog.RecordDeckEdited(NavalBattleStage.Preparation);
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                PrepareVoyageAchievementSettlement(game, 0, beforeSettlement: session =>
                {
                    typeof(CoreVoyageSession).GetMethod("StartBattle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(session, new object[] { new CoreEncounter("achievement_flow_fixture", "验证敌舰", "海战", "测试", true, false, 1, 70), false });
                    int guard = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(index => CoreVoyageSession.CardLibrary[index].Id == "card_brace");
                    session.Battle.Hand.Clear();
                    session.Battle.Hand.Add(guard);
                    session.Battle.Energy = 20;
                    Assert.That(session.PlayCard(0), Is.True);
                    session.Battle.EnemyHull = 0;
                    typeof(CoreVoyageSession).GetMethod("ResolveBattleOutcome", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                    Assert.That(session.AchievementVoyageBattleWins, Is.EqualTo(1));
                    Assert.That(session.AchievementWonEditedDeckStages, Is.EqualTo(1));
                    typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull / 4);
                });
                Assert.That(VoyageAchievementUnlocked("tutorial_recovery"), Is.False);
                Assert.That(VoyageAchievementUnlocked("battle_deck"), Is.False);
                yield return ConfirmAchievementSettlement();
                Assert.That(VoyageAchievementUnlocked("tutorial_recovery"), Is.True);
                Assert.That(VoyageAchievementUnlocked("battle_deck"), Is.True);
                Assert.That(VoyageAchievementUnlocked("battle_stages"), Is.False);
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                Assert.That(VoyageAchievementUnlocked("tutorial_recovery"), Is.True);
                Assert.That(VoyageAchievementUnlocked("battle_deck"), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AchievementChallenge_LongVoyageRequiresUnbrokenSuppliesAndNewTreasureRequiresArrival()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                foreach (bool depleted in new[] { true, false })
                {
                    PrepareVoyageAchievementSettlement(game, 0, 0f, true, session =>
                    {
                        if (depleted) session.Cargo.Water = 0;
                        session.Cargo.Water = session.Cargo.Food = 500;
                        typeof(CoreVoyageSession).GetProperty("TotalHours").SetValue(session, 240d);
                    });
                    Assert.That(VoyageAchievementUnlocked("cargo_all"), Is.False);
                    if (depleted) Assert.That(VoyageAchievementUnlocked("warehouse_one"), Is.False);
                    yield return ConfirmAchievementSettlement();
                    Assert.That(VoyageAchievementUnlocked("cargo_all"), Is.EqualTo(!depleted));
                    Assert.That(VoyageAchievementUnlocked("warehouse_one"), Is.True, "A newly completed ordinary chart grants a new collection reward at confirmed arrival.");
                }
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                Assert.That(VoyageAchievementUnlocked("cargo_all"), Is.True);
                Assert.That(VoyageAchievementUnlocked("warehouse_one"), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AchievementChallenge_ReplayedTutorialChartAfterGraduationCannotGrantOrdinaryVoyageChallenges()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                var tutorial = new CoreVoyageSession();
                Assert.That(tutorial.ConfigureGuidedTutorialVoyage(1), Is.True);
                tutorial.StartVoyage();
                string seed = tutorial.CurrentChartSeed;
                Assert.That(CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition definition), Is.True);
                Assert.That(definition.TutorialWaters, Is.True);
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
                PrepareVoyageAchievementSettlement(game, 0, 100f);
                game.Session.Settlement.CompletedEvents = 6;
                yield return ConfirmAchievementSettlement();
                Assert.That(VoyageAchievementUnlocked(), Is.False);
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.False);
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageEventArrivalId), Is.False);
                Assert.That(VoyageAchievementUnlocked("warehouse_one"), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageThreeEvents_OnlyConfirmedSingleVoyageUnlocksAndSurvivesReload()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                foreach (int completed in new[] { 2, 2, 3 })
                {
                    PrepareVoyageAchievementSettlement(game, completed);
                    yield return null;
                    Assert.That(VoyageAchievementUnlocked(), Is.False, "Viewing the settlement is not confirmation.");
                    if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
                        yield return ClickAchievementIconControl("Skip Settlement Reveal");
                    yield return ClickAchievementIconControl("Return To Port");
                    yield return SchoolUnlockNoticeTestActions.ConfirmAll();
                    Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                    Assert.That(VoyageAchievementUnlocked(), Is.EqualTo(completed == 3),
                        "Completions from separate shorter voyages must not accumulate.");
                    Assert.That(scope.Store.TryLoad(out _, out string notice), Is.False, notice);
                    Assert.That(notice, Is.Null, "Confirmed progress must have retired its transaction journal.");
                }
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                Assert.That(VoyageAchievementUnlocked(), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageHalfChart_OnlyConfirmedSingleVoyageCoverageUnlocksAndSurvivesReload()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                foreach (float coverage in new[] { 40f, 40f, 50f })
                {
                    PrepareVoyageAchievementSettlement(game, 0, coverage);
                    yield return null;
                    Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.False,
                        "Neither viewing settlement nor coverage from previous voyages grants this task.");
                    if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
                        yield return ClickAchievementIconControl("Skip Settlement Reveal");
                    yield return ClickAchievementIconControl("Return To Port");
                    Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                    Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.EqualTo(coverage >= 50f));
                    Assert.That(VoyageAchievementUnlocked(), Is.False, "Exploring without events cannot grant the event task.");
                    Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageEventArrivalId), Is.False);
                    Assert.That(scope.Store.TryLoad(out _, out string notice), Is.False, notice);
                    Assert.That(notice, Is.Null);
                }
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageHalfChart_ImportedHistoricalChartCannotUnlockOnImmediateReturn()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
                Assert.That(CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition definition), Is.True);
                var chartGeometry = new CoreVoyageMap(definition.MapGeometryVersion);
                var historicalChart = new VoyageAchievementChartRecord
                {
                    seed = seed, routeIndex = definition.RouteIndex,
                    mapGeometryVersion = definition.MapGeometryVersion,
                    departurePort = definition.ForwardDeparturePort, destinationPort = definition.ForwardDestinationPort,
                    tutorialWaters = definition.TutorialWaters,
                    exploredCells = Enumerable.Repeat(true, chartGeometry.Columns * chartGeometry.Rows).ToArray(),
                    revealedLocationIds = new int[0],
                };
                PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey,
                    JsonUtility.ToJson(new VoyageAchievementChartArchive { charts = new[] { historicalChart } }));
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
                game.Session.StartVoyage();
                Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                InvokeGame(game, "ApplyRecordedSeaChartKnowledge");
                Assert.That(game.Session.ExplorationCoverage(), Is.EqualTo(100f));
                Assert.That(game.Session.VoyageExplorationCoverage(), Is.GreaterThan(0f).And.LessThan(50f));
                Assert.That(game.Session.SafeReturn(), Is.True);
                Assert.That(game.Session.Settlement.Coverage, Is.EqualTo(100f));
                Assert.That(game.Session.Settlement.VoyageCoverage, Is.LessThan(50f));
                InvokeGame(game, "Render");
                yield return null;
                if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
                    yield return ClickAchievementIconControl("Skip Settlement Reveal");
                yield return ClickAchievementIconControl("Return To Port");
                Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.False,
                    "An imported complete chart is knowledge, not fifty percent observed during this voyage.");
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageEventArrival_SixEventsMustReachAnotherPortAndSurviveReload()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                int[] completedEvents = { 3, 5, 6, 6 };
                for (int voyage = 0; voyage < completedEvents.Length; voyage++)
                {
                    int completed = completedEvents[voyage];
                    // The first six-event voyage returns home; only the final
                    // voyage reaches its destination and can earn the harder task.
                    bool arrive = voyage != 2;
                    PrepareVoyageAchievementSettlement(game, completed, 0f, arrive);
                    yield return null;
                    Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageEventArrivalId), Is.False);
                    string destination = game.Session.Settlement.Destination;
                    if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
                        yield return ClickAchievementIconControl("Skip Settlement Reveal");
                    yield return ClickAchievementIconControl("Return To Port");
                    Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                    Assert.That(game.Session.CurrentPort, Is.EqualTo(destination));
                    yield return SchoolUnlockNoticeTestActions.ConfirmAll();
                    Assert.That(VoyageAchievementUnlocked(), Is.True);
                    Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageEventArrivalId), Is.EqualTo(completed >= 6 && arrive));
                    Assert.That(scope.Store.TryLoad(out _, out string notice), Is.False, notice);
                    Assert.That(notice, Is.Null);
                }
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageEventArrivalId), Is.True);
                Assert.That(VoyageAchievementUnlocked(), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageThreeEvents_FailedConfirmationDoesNotUnlockOrLeakIntoRecovery()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedJournalSceneProfile();
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                PrepareVoyageAchievementSettlement(game, 6, 60f, true);
                game.Session.Settlement.Hours = 240d;
                AchievementCatalog.RecordDeckEdited(NavalBattleStage.Preparation);
                typeof(CoreVoyageSession).GetProperty("AchievementWonEditedDeckStages").SetValue(game.Session, 1);
                yield return null;
                if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
                    yield return ClickAchievementIconControl("Skip Settlement Reveal");
                Directory.CreateDirectory(scope.DirectoryPath);
                using (var held = new FileStream(scope.Store.PrimaryPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    LogAssert.Expect(LogType.Exception,
                        "ProfileSaveException: 无法读取航程存档，原文件已保留：Sharing violation on path " + scope.Store.PrimaryPath + ".lock");
                    yield return ClickAchievementIconControl("Return To Port");
                    Assert.That(GameObject.Find("Profile Save Failure"), Is.Not.Null);
                    Assert.That(game.enabled, Is.False);
                    Assert.That(VoyageAchievementUnlocked(), Is.False);
                    Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.False);
                    Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageEventArrivalId), Is.False);
                    Assert.That(VoyageAchievementUnlocked("cargo_all"), Is.False);
                    Assert.That(VoyageAchievementUnlocked("battle_deck"), Is.False);
                    Assert.That(VoyageAchievementUnlocked("warehouse_one"), Is.False);
                    Assert.That(GameObject.Find("Achievement Notification"), Is.Null);
                }
                yield return ClickAchievementIconControl("Recover Profile Save");
                yield return null; yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(VoyageAchievementUnlocked(), Is.False);
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.False);
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageEventArrivalId), Is.False);
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                InvokeGame(Object.FindFirstObjectByType<CoreGameplayDemoController>(), "SaveProgress");
                Assert.That(VoyageAchievementUnlocked(), Is.False, "An unrelated later save must not inherit the failed confirmation.");
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageHalfChartId), Is.False);
                Assert.That(VoyageAchievementUnlocked(CoreAchievementCatalog.VoyageEventArrivalId), Is.False);
                Assert.That(VoyageAchievementUnlocked("cargo_all"), Is.False);
                Assert.That(VoyageAchievementUnlocked("battle_deck"), Is.False);
                Assert.That(VoyageAchievementUnlocked("warehouse_one"), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator VoyageThreeEvents_MenuShowsTaskAndPreservesBothNeighborPages()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SceneManager.LoadScene("MainMenu");
                yield return null; yield return null;
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                var before = SnapshotJournalScenePreferences();
                yield return ClickAchievementIconControl("Achievements");
                int taskIndex = AchievementCatalog.All.ToList().FindIndex(item => item.Id == CoreAchievementCatalog.VoyageThreeEventsId);
                Assert.That(taskIndex, Is.GreaterThanOrEqualTo(0));
                int taskPage = taskIndex / 8;
                for (int page = 0; page < taskPage; page++) yield return ClickAchievementIconControl("Achievements Next Page");
                string[] taskIds = { CoreAchievementCatalog.VoyageThreeEventsId,
                    CoreAchievementCatalog.VoyageHalfChartId, CoreAchievementCatalog.VoyageEventArrivalId };
                foreach (string id in taskIds)
                {
                    var definition = CoreAchievementCatalog.Find(id);
                    Transform row = GameObject.Find("Achievement Row " + definition.Id).transform;
                    Assert.That(row.Find("Title").GetComponent<Text>().text, Is.EqualTo(definition.Title));
                    Assert.That(row.Find("Description").GetComponent<Text>().text, Is.EqualTo(definition.Description));
                    Assert.That(row.Find("State").GetComponent<Text>().text, Is.EqualTo("未解锁"));
                }
                CollectionAssert.AreEqual(AchievementCatalog.All.Skip(taskPage * 8).Take(8).Select(item => item.Id), VisibleExperienceIds(menu));
                if (taskPage > 0)
                {
                    yield return ClickAchievementIconControl("Achievements Previous Page");
                    CollectionAssert.AreEqual(AchievementCatalog.All.Skip((taskPage - 1) * 8).Take(8).Select(item => item.Id), VisibleExperienceIds(menu));
                    yield return ClickAchievementIconControl("Achievements Next Page");
                }
                if ((taskPage + 1) * 8 < AchievementCatalog.All.Count)
                {
                    yield return ClickAchievementIconControl("Achievements Next Page");
                    CollectionAssert.AreEqual(AchievementCatalog.All.Skip((taskPage + 1) * 8).Take(8).Select(item => item.Id), VisibleExperienceIds(menu));
                }
                yield return SelectExperienceCategory("航程进度");
                var missingInVoyageCategory = taskIds.ToList();
                for (int page = 0; page <= AchievementCatalog.All.Count / 8; page++)
                {
                    foreach (string id in VisibleExperienceIds(menu)) missingInVoyageCategory.Remove(id);
                    if (missingInVoyageCategory.Count == 0) break;
                    if (!ActiveButton("Achievements Next Page").interactable) break;
                    yield return ClickAchievementIconControl("Achievements Next Page");
                }
                Assert.That(missingInVoyageCategory, Is.Empty, "All three replacements are reachable through the voyage category and all achievements.");
                Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(before), "Menu inspection cannot award or write the task.");
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static bool VoyageAchievementUnlocked(string id = CoreAchievementCatalog.VoyageThreeEventsId) =>
            AchievementCatalog.IsUnlocked(CoreAchievementCatalog.Find(id));

        [System.Serializable]
        private sealed class VoyageAchievementChartArchive
        {
            public int version = 1;
            public VoyageAchievementChartRecord[] charts;
        }

        [System.Serializable]
        private sealed class VoyageAchievementChartRecord
        {
            public string seed, departurePort, destinationPort;
            public int routeIndex;
            public int mapGeometryVersion;
            public bool tutorialWaters;
            public bool[] exploredCells;
            public int[] revealedLocationIds;
        }

        private static void PrepareVoyageAchievementSettlement(CoreGameplayDemoController game, int completed,
            float coverage = 0f, bool arrive = false, System.Action<CoreVoyageSession> beforeSettlement = null)
        {
            CoreVoyageSession session = game.Session;
            session.Cargo.Water = 20;
            session.Cargo.Food = 20;
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(game.IsTutorialActive, Is.False);
            Assert.That(session.GuidedTutorialLeg, Is.Zero);
            Assert.That(session.IsTreasureVoyage, Is.False);
            beforeSettlement?.Invoke(session);
            Assert.That(session.Map.Nodes.Count, Is.GreaterThanOrEqualTo(completed));
            // Arrange completed map events and revealed cells. Production
            // SafeReturn/Sail builds settlement and the real button commits it.
            foreach (CoreMapNode node in session.Map.Nodes) { node.Visited = false; node.Skipped = false; }
            foreach (CoreMapNode node in session.Map.Nodes.Take(completed)) node.Visited = true;
            for (int index = 0; index < session.Map.ExploredCells.Length; index++)
            {
                session.Map.ExploredCells[index] = index < session.Map.ExploredCells.Length * coverage / 100f;
                session.Map.VoyageExploredCells[index] = session.Map.ExploredCells[index];
            }
            if (arrive)
            {
                System.Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
                typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Position)).SetValue(session, session.Map.Destination);
                typeof(CoreVoyageSession).GetField("bossDefeated", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(session, true);
                typeof(CoreVoyageSession).GetField("randomAmbushOccurred", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(session, true);
                session.SetHeading(SailingDirection.East);
                Assert.That(session.Sail(), Is.True);
            }
            else Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(arrive ? VoyageOutcome.ArrivedAtPort : VoyageOutcome.SafeReturn));
            Assert.That(session.Settlement.CompletedEvents, Is.EqualTo(completed));
            InvokeGame(game, "Render");
        }

        private static IEnumerator ConfirmAchievementSettlement()
        {
            yield return null;
            if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
                yield return ClickAchievementIconControl("Skip Settlement Reveal");
            yield return ClickAchievementIconControl("Return To Port");
            yield return SchoolUnlockNoticeTestActions.ConfirmAll();
        }
    }
}
