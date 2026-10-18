using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_ConfirmedReturnShowsRealCardsAndDoesNotReplay()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                AssertAllSchoolPreviewSources();
                PrepareSchoolUnlockSettlement(game, 99);
                Assert.That(game.Session.PirateLevel, Is.EqualTo(1));
                Assert.That(SchoolUnlockRoot(game), Is.Null, "Viewing pending settlement must not announce an uncommitted unlock.");
                yield return ConfirmSchoolUnlockSettlement();
                Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(game.Session.PirateLevel, Is.EqualTo(2));
                AssertSchoolUnlockPage(game, "fire", 1, 1);
                Assert.That(scope.Store.TryLoad(out _, out string notice), Is.False, notice);
                Assert.That(notice, Is.Null, "The notification follows the committed return transaction.");
                GameObject first = SchoolUnlockRoot(game);
                InvokeGame(game, "Render");
                yield return null;
                Assert.That(SchoolUnlockRoot(game), Is.SameAs(first), "Port refresh must retain the notification.");
                AssertSchoolUnlockPage(game, "fire", 1, 1);
                NavalBattleVisualCapture.Capture(game, "01_Fire_Unlock.png", "真实回港确认后升至海盗等级2，展示火焰基础体系的船战与接舷卡牌", "School Unlock Title");
                Button.ButtonClickedEvent retiredConfirm = ActiveButton("Confirm School Unlock").onClick;
                yield return ClickAchievementIconControl("Confirm School Unlock");
                Assert.That(SchoolUnlockRoot(game), Is.Null);
                retiredConfirm.Invoke();
                InvokeGame(game, "Render");
                yield return null;
                Assert.That(SchoolUnlockRoot(game), Is.Null, "Closing and rebuilding port must not replay the unlock.");
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(2));
                Assert.That(SchoolUnlockRoot(game), Is.Null, "Loading an already unlocked profile must not replay it.");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_MultiLevelReturnPagesEveryNewSchoolAndBlocksUnderlyingPort()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                PrepareSchoolUnlockSettlement(game, 120, 6503f, 5);
                Assert.That(game.Session.Settlement.BankedPirateExperience, Is.EqualTo(426));
                yield return ConfirmSchoolUnlockSettlement();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(4));
                AssertSchoolUnlockPage(game, "arcane", 1, 2);
                Button underneath = ActiveButton("Port Hotspot Voyage");
                Assert.That(underneath, Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null,
                    underneath.GetComponent<RectTransform>().TransformPoint(underneath.GetComponent<RectTransform>().rect.center)) };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty);
                Assert.That(hits[0].gameObject.transform.IsChildOf(SchoolUnlockRoot(game).transform), Is.True,
                    "The notification must intercept clicks before the port controls.");
                NavalBattleVisualCapture.Capture(game, "02_Arcane_First_Page.png", "一次回港从等级2升至等级4，先显示秘术体系并标记第1/2页", "School Unlock Title");
                yield return ClickAchievementIconControl("Next School Unlock");
                AssertSchoolUnlockPage(game, "curse", 2, 2);
                InvokeGame(game, "Render");
                yield return null;
                AssertSchoolUnlockPage(game, "curse", 2, 2);
                NavalBattleVisualCapture.Capture(game, "03_Curse_Second_Page.png", "下一页展示本次同时解锁的诅咒体系，刷新港口仍保留当前页", "School Unlock Title");
                yield return ClickAchievementIconControl("Confirm School Unlock");
                Assert.That(SchoolUnlockRoot(game), Is.Null);
                Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_EscapeAdvancesOnlyOneNewSchoolAtATime()
        {
            using (var scope = new ProfileJournalSceneScope())
            using (var input = new SchoolUnlockKeyboard())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                PrepareSchoolUnlockSettlement(game, 120, 6503f, 5);
                yield return ConfirmSchoolUnlockSettlement();
                AssertSchoolUnlockPage(game, "arcane", 1, 2);
                yield return input.Press();
                AssertSchoolUnlockPage(game, "curse", 2, 2);
                yield return input.Press();
                Assert.That(SchoolUnlockRoot(game), Is.Null);
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_NoUpgradeOrLevelWithoutSchoolDoesNotDisplay()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                foreach (int experience in new[] { 0, 100, 1399, 2999, 4099, 6000 })
                {
                    PrepareSchoolUnlockSettlement(game, experience);
                    int before = game.Session.PirateLevel;
                    yield return ConfirmSchoolUnlockSettlement();
                    Assert.That(SchoolUnlockRoot(game), Is.Null, "Unexpected notification for XP " + experience + " / level " + before + " -> " + game.Session.PirateLevel);
                }
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolUnlockNotice_FailedReturnSaveDoesNotAnnounceAnUnlock()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                yield return LoadSchoolUnlockFixture();
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                game.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 99 });
                InvokeGame(game, "SaveProgress");
                PrepareSchoolUnlockSettlement(game, 99);
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
                    Assert.That(SchoolUnlockRoot(game), Is.Null);
                    Assert.That(GameObject.Find("School Unlock Title"), Is.Null);
                }
                yield return ClickAchievementIconControl("Recover Profile Save");
                yield return null; yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                SceneManager.LoadScene(MainMenuController.MainSceneName);
                yield return null; yield return null;
                game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.PirateLevel, Is.EqualTo(1));
                Assert.That(SchoolUnlockRoot(game), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static IEnumerator LoadSchoolUnlockFixture()
        {
            SeedJournalSceneProfile();
            SceneManager.LoadScene(MainMenuController.MainSceneName);
            yield return null; yield return null;
            Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>().IsTutorialActive, Is.False);
        }

        private static void PrepareSchoolUnlockSettlement(CoreGameplayDemoController game, int experience,
            float distance = 540f, int completed = 1)
        {
            CoreVoyageSession session = game.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = experience });
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
            if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
                yield return ClickAchievementIconControl("Skip Settlement Reveal");
            yield return ClickAchievementIconControl("Return To Port");
            yield return null;
        }

        private static GameObject SchoolUnlockRoot(CoreGameplayDemoController game) =>
            (GameObject)typeof(CoreGameplayDemoController).GetField("schoolUnlockNotice",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(game);

        private sealed class SchoolUnlockKeyboard : System.IDisposable
        {
            private readonly InputSettings original = InputSystem.settings;
            private readonly HideFlags originalFlags;
            private readonly InputSettings temporary;
            private readonly Keyboard keyboard;

            public SchoolUnlockKeyboard()
            {
                originalFlags = original.hideFlags;
                if (originalFlags == HideFlags.HideAndDontSave) original.hideFlags = HideFlags.DontSave;
                temporary = Object.Instantiate(original);
                temporary.hideFlags = HideFlags.DontSave;
                temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                temporary.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings = temporary;
                keyboard = InputSystem.AddDevice<Keyboard>();
            }

            public IEnumerator Press()
            {
                keyboard.MakeCurrent();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
            }

            public void Dispose()
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = original;
                original.hideFlags = originalFlags;
                Object.Destroy(temporary);
            }
        }

        private static void AssertSchoolUnlockPage(CoreGameplayDemoController game, string schoolId, int page, int total)
        {
            Assert.That(SchoolUnlockRoot(game), Is.Not.Null);
            CoreSchoolUnlockDefinition school = CoreSchoolUnlockCatalog.Find(schoolId);
            Assert.That(GameObject.Find("School Unlock Title").GetComponent<Text>().text, Does.Contain(school.Name));
            Assert.That(GameObject.Find("School Unlock Level").GetComponent<Text>().text, Does.Contain(game.Session.PirateLevel.ToString()));
            string progress = GameObject.Find("School Unlock Progress").GetComponent<Text>().text.Replace(" ", string.Empty).Replace("　", string.Empty);
            Assert.That(progress, Does.Contain(page + "/" + total));
            Assert.That(GameObject.Find("School Unlock Description").GetComponent<Text>().text, Is.Not.Empty);
            CoreBattleCard[] previewCards = SchoolPreviewCards(schoolId);
            Assert.That(previewCards, Has.Length.EqualTo(3));
            for (int slot = 0; slot < 3; slot++)
            {
                GameObject preview = GameObject.Find("School Unlock Preview " + slot);
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.GetComponentInChildren<CardSchoolFrameGraphic>().SchoolId, Is.EqualTo(schoolId));
                string[] text = preview.GetComponentsInChildren<Text>().Select(label => label.text).ToArray();
                // Historic aliases share display names with canonical cards;
                // validate the exact chosen definition, then its displayed face.
                CoreBattleCard card = previewCards[slot];
                Assert.That(text, Does.Contain(CardFaceLayout.DisplayName(card)), card.Id);
                Assert.That(card.RequiredCrew, Is.Empty);
                Assert.That(card.RequiredComponent, Is.Empty);
                Assert.That(CoreBoardingCardCatalog.IsLegacyAlias(card), Is.False);
                Assert.That(game.Session.IsBattleCardUnlocked(card), Is.True);
                Assert.That(preview.GetComponentsInChildren<RawImage>().Any(image => image.texture != null), Is.True);
            }
            Assert.That(previewCards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(previewCards.Count(card => card.AllowedStages == NavalBattleStages.Boarding), Is.EqualTo(2));
            Assert.That(previewCards.Count(card => card.AllowedStages != NavalBattleStages.Boarding), Is.EqualTo(1));
        }

        private static CoreBattleCard[] SchoolPreviewCards(string schoolId) =>
            ((System.Collections.Generic.IEnumerable<CoreBattleCard>)typeof(CoreGameplayDemoController)
                .GetMethod("SchoolUnlockPreviewCards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { schoolId })).ToArray();

        private static void AssertAllSchoolPreviewSources()
        {
            var unlocked = new CoreVoyageSession();
            unlocked.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            foreach (CoreSchoolUnlockDefinition school in CoreSchoolUnlockCatalog.All)
            {
                CoreBattleCard[] cards = SchoolPreviewCards(school.Id);
                Assert.That(cards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(3), school.Id);
                Assert.That(cards.Count(card => card.AllowedStages == NavalBattleStages.Boarding), Is.EqualTo(2), school.Id);
                foreach (CoreBattleCard card in cards)
                {
                    Assert.That(CoreVoyageSession.CardLibrary, Does.Contain(card));
                    Assert.That(card.SchoolId, Is.EqualTo(school.Id));
                    Assert.That(card.RequiredCrew, Is.Empty, card.Id);
                    Assert.That(card.RequiredComponent, Is.Empty, card.Id);
                    Assert.That(CoreBoardingCardCatalog.IsLegacyAlias(card), Is.False, card.Id);
                    Assert.That(unlocked.IsBattleCardUnlocked(card), Is.True, card.Id);
                }
            }
        }
    }
}
