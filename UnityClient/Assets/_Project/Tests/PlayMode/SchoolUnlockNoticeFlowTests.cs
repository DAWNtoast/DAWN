using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
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
        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_ConfirmedLevelGainKeepsTreasureUnlockWithoutGrantingCards()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                game.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 99, fragments = 1 });
                Assert.That(game.Session.CanIdentifyTreasureFragment(out string locked), Is.False);
                Assert.That(locked, Does.Contain("等级"));
                int[] copies = OrdinaryPackCardCopies(game.Session);
                string progress = PackProgressJson(game.Session);
                PrepareSchoolUnlockSettlement(game, 99);
                int earnedExperience = game.Session.Settlement.BankedPirateExperience;
                Assert.That(game.Session.PirateLevel, Is.EqualTo(1));
                AssertNoSchoolRewardNotice(game);

                yield return ConfirmSchoolUnlockSettlement();
                Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(game.Session.PirateExperience, Is.EqualTo(99 + earnedExperience));
                Assert.That(game.Session.PirateLevel, Is.EqualTo(2));
                Assert.That(game.Session.CanIdentifyTreasureFragment(out string reason), Is.True, reason);
                AssertPackCollectionUnchanged(game, copies, progress);
                Assert.That(scope.Store.TryLoad(out _, out string notice), Is.False, notice);
                Assert.That(notice, Is.Null, "The normal return transaction must finish even though the old card notice is retired.");

                InvokeGame(game, "Render");
                yield return null;
                AssertPackCollectionUnchanged(game, copies, progress);
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(2));
                Assert.That(game.Session.CanIdentifyTreasureFragment(out reason), Is.True, reason);
                AssertPackCollectionUnchanged(game, copies, progress);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_MultiLevelReturnKeepsRouteUnlockAndPortInputAvailable()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                game.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 120 });
                Assert.That(game.Session.CanSelectRoute(1, out _), Is.False);
                int[] copies = OrdinaryPackCardCopies(game.Session);
                string progress = PackProgressJson(game.Session);
                PrepareSchoolUnlockSettlement(game, 120, 28200f, 5);
                Assert.That(game.Session.Settlement.BankedPirateExperience, Is.EqualTo(408));
                yield return ConfirmSchoolUnlockSettlement();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(4));
                Assert.That(game.Session.PirateExperience, Is.EqualTo(528));
                Assert.That(game.Session.CanSelectRoute(1, out string reason), Is.True, reason);
                Assert.That(game.Session.CanSelectRoute(2, out _), Is.False, "Higher route level gates remain active.");
                AssertPackCollectionUnchanged(game, copies, progress);

                Button voyage = ActiveButton("Port Hotspot Voyage");
                Assert.That(voyage, Is.Not.Null);
                Assert.That(voyage.IsInteractable(), Is.True);
                Canvas.ForceUpdateCanvases();
                var rect = voyage.GetComponent<RectTransform>();
                var pointer = new PointerEventData(EventSystem.current) {
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
                };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty);
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(voyage),
                    "No retired level-card notice may intercept the port controls.");
                InvokeGame(game, "Render");
                yield return null;
                AssertPackCollectionUnchanged(game, copies, progress);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_PackRewardsSurviveLaterLevelGainAndReloadWithoutExtraCopies()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.GrantCardPacks("fire", 2), Is.True);
                Assert.That(game.Session.TryOpenCardPack("fire", out int[] rewards, out string reason), Is.True, reason);
                Assert.That(rewards, Has.Length.EqualTo(3));
                Assert.That(game.Session.CaptureCardPackProgress().cards.Sum(item => item.count), Is.EqualTo(3));
                int[] copies = OrdinaryPackCardCopies(game.Session);
                string progress = PackProgressJson(game.Session);
                PrepareSchoolUnlockSettlement(game, CoreVoyageSession.PirateExperienceForLevel(6) - 1);
                yield return ConfirmSchoolUnlockSettlement();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(6));
                AssertPackCollectionUnchanged(game, copies, progress);
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(6));
                Assert.That(game.Session.GetCardPackCount("fire"), Is.EqualTo(1));
                AssertPackCollectionUnchanged(game, copies, progress);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_AllFormerSchoolThresholdsLeaveTheCollectionUnchanged()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                int[] copies = OrdinaryPackCardCopies(game.Session);
                string progress = PackProgressJson(game.Session);
                foreach (int level in CoreSchoolUnlockCatalog.All.Select(school => school.RequiredLevel).Distinct())
                {
                    int experience = CoreVoyageSession.PirateExperienceForLevel(level) - 1;
                    PrepareSchoolUnlockSettlement(game, experience);
                    Assert.That(game.Session.PirateLevel, Is.EqualTo(level - 1));
                    yield return ConfirmSchoolUnlockSettlement();
                    Assert.That(game.Session.PirateLevel, Is.EqualTo(level));
                    AssertPackCollectionUnchanged(game, copies, progress);
                }
                Assert.That(game.Session.CaptureCardPackProgress().cards, Is.Empty);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_ReturnWithoutPromotionStillCommitsExperienceWithoutCards()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                int[] copies = OrdinaryPackCardCopies(game.Session);
                string progress = PackProgressJson(game.Session);
                PrepareSchoolUnlockSettlement(game, 0);
                int earnedExperience = game.Session.Settlement.BankedPirateExperience;
                yield return ConfirmSchoolUnlockSettlement();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(1));
                Assert.That(game.Session.PirateExperience, Is.EqualTo(earnedExperience));
                AssertPackCollectionUnchanged(game, copies, progress);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_FailedReturnSaveDoesNotGrantCardsOrAnnounceAnUnlock()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                game.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 99 });
                InvokeGame(game, "SaveProgress");
                int[] copies = OrdinaryPackCardCopies(game.Session);
                string progress = PackProgressJson(game.Session);
                PrepareSchoolUnlockSettlement(game, 99);
                yield return null;
                yield return SettlementChestTestActions.ShowOverview();
                Directory.CreateDirectory(scope.DirectoryPath);
                using (var held = new FileStream(scope.Store.PrimaryPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    LogAssert.Expect(LogType.Exception,
                        "ProfileSaveException: 无法读取航程存档，原文件已保留：Sharing violation on path " + scope.Store.PrimaryPath + ".lock");
                    yield return ClickAchievementIconControl("Return To Port");
                    Assert.That(GameObject.Find("Profile Save Failure"), Is.Not.Null);
                    AssertPackCollectionUnchanged(game, copies, progress);
                }
                yield return ClickAchievementIconControl("Recover Profile Save");
                yield return null; yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(1));
                AssertPackCollectionUnchanged(game, copies, progress);
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static IEnumerator LoadSchoolUnlockFixture()
        {
            SeedJournalSceneProfile();
            // This fixture models a new-format collection, not a legacy profile
            // whose preexisting school copies need one-time migration.
            PlayerPrefs.SetString(CoreGameplayDemoController.CardPackProgressKey,
                JsonUtility.ToJson(new CoreVoyageSession().CaptureCardPackProgress()));
            PlayerPrefs.Save();
            SceneManager.LoadScene(MainMenuController.MainSceneName);
            yield return null; yield return null;
            Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>().IsTutorialActive, Is.False);
        }

        private static void PrepareSchoolUnlockSettlement(CoreGameplayDemoController game, int experience,
            float distance = 540f, int completed = 1)
        {
            CoreVoyageSession session = game.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = experience, fragments = 1 });
            session.Cargo.Water = session.Cargo.Food = 100;
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.TotalDistanceNm)).SetValue(session, distance);
            foreach (CoreMapNode node in session.Map.Nodes) { node.Visited = false; node.Skipped = false; }
            foreach (CoreMapNode node in session.Map.Nodes.Take(completed)) node.Visited = true;
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.PirateExperience, Is.EqualTo(experience));
            Assert.That(session.Settlement.BankedPirateExperience, Is.GreaterThan(0));
            InvokeGame(game, "Render");
        }

        private static IEnumerator ConfirmSchoolUnlockSettlement()
        {
            yield return null;
            yield return SettlementChestTestActions.ShowOverview();
            yield return ClickAchievementIconControl("Return To Port");
            yield return null;
        }

        private static GameObject SchoolUnlockRoot(CoreGameplayDemoController game) =>
            (GameObject)typeof(CoreGameplayDemoController).GetField("schoolUnlockNotice",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);

        private static int[] OrdinaryPackCardCopies(CoreVoyageSession session) => session.GetCardPackPool("neutral")
            .Select(index => session.GetBaseBattleCardOwnedCopies(index)).ToArray();

        private static string PackProgressJson(CoreVoyageSession session) => JsonUtility.ToJson(session.CaptureCardPackProgress());

        private static void AssertNoSchoolRewardNotice(CoreGameplayDemoController game)
        {
            Assert.That(SchoolUnlockRoot(game), Is.Null);
            Assert.That(GameObject.Find("School Unlock Notice"), Is.Null);
            Assert.That(GameObject.Find("School Unlock Title"), Is.Null);
            Assert.That(ActiveButton("Next School Unlock"), Is.Null);
            Assert.That(ActiveButton("Confirm School Unlock"), Is.Null);
        }

        private static void AssertPackCollectionUnchanged(CoreGameplayDemoController game, int[] copies, string progress)
        {
            Assert.That(OrdinaryPackCardCopies(game.Session), Is.EqualTo(copies), "Level gains must not grant any ordinary card copies.");
            Assert.That(PackProgressJson(game.Session), Is.EqualTo(progress), "Level gains must not mutate pack inventory, acquired cards or draw state.");
            AssertNoSchoolRewardNotice(game);
        }
    }
}
