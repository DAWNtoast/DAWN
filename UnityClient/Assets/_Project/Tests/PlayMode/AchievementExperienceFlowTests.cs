using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
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
        public IEnumerator AchievementExperience_RealFiltersReuseRowsRefreshAndHandleEmptyResults()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                ProfileGoldPersistence.Save(1500m);
                SceneManager.LoadScene("MainMenu"); yield return null; yield return null;
                var menu = Object.FindFirstObjectByType<MainMenuController>();
                Assert.That(menu.GetComponentsInChildren<RawImage>(true).Any(image => image.name.StartsWith("Achievement Icon ")), Is.False);
                yield return ClickAchievementIconControl("Achievements");
                var icons = menu.GetComponentsInChildren<RawImage>(true).Where(image => image.name.StartsWith("Achievement Icon ")).ToArray();
                Assert.That(icons.Length, Is.EqualTo(8));
                var before = SnapshotJournalScenePreferences();
                NavalBattleVisualCapture.Capture(null, "AchievementExperience_01_All.png", "真实成就菜单：分类、状态筛选与八行复用", "Achievements Card");
                yield return SelectExperienceCategory("财富");
                yield return ClickAchievementIconControl("Achievements Filter Locked");
                CollectionAssert.AreEquivalent(new[] { "gold_900", "gold_1200", "gold_2000" }, VisibleExperienceIds(menu));
                Assert.That(GameObject.Find("Achievement Row gold_900").transform.Find("Progress").GetComponent<Text>().text, Is.EqualTo("1500 / 2500"));
                Assert.That(GameObject.Find("Achievements Page Indicator").GetComponent<Text>().text, Is.EqualTo("1 / 1"));
                AssertExperienceRowsClear(menu);
                NavalBattleVisualCapture.Capture(null, "AchievementExperience_02_Wealth.png", "财富未解锁筛选：显示当前资金与真实目标，查询不写存档", "Achievements Card");
                Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(before));
                yield return ClickAchievementIconControl("Back From Achievements");
                ProfileGoldPersistence.Save(3000m); AchievementCatalog.CaptureProgress();
                before = SnapshotJournalScenePreferences();
                yield return ClickAchievementIconControl("Achievements");
                CollectionAssert.AreEquivalent(new[] { "gold_1200", "gold_2000" }, VisibleExperienceIds(menu));
                yield return ClickAchievementIconControl("Achievements Filter Unlocked");
                CollectionAssert.AreEquivalent(new[] { "gold_600", "gold_900" }, VisibleExperienceIds(menu));
                Assert.That(GameObject.Find("Achievement Row gold_900").transform.Find("Progress").GetComponent<Text>().text, Is.Empty);
                yield return SelectExperienceCategory("探索");
                Assert.That(VisibleExperienceIds(menu), Is.Empty);
                Assert.That(GameObject.Find("Achievements Empty"), Is.Not.Null);
                Assert.That(ActiveButton("Achievements Previous Page").interactable, Is.False);
                Assert.That(ActiveButton("Achievements Next Page").interactable, Is.False);
                yield return SelectExperienceCategory("全部分类");
                yield return ClickAchievementIconControl("Achievements Filter All");
                Assert.That(VisibleExperienceIds(menu).Length, Is.EqualTo(8));
                yield return ClickAchievementIconControl("Achievements Next Page");
                Assert.That(GameObject.Find("Achievements Page Indicator").GetComponent<Text>().text, Is.EqualTo("2 / 7"));
                CollectionAssert.AreEquivalent(icons, menu.GetComponentsInChildren<RawImage>(true).Where(image => image.name.StartsWith("Achievement Icon ")).ToArray());
                Assert.That(SnapshotJournalScenePreferences(), Is.EquivalentTo(before));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AchievementExperience_CommittedMarketMilestoneShowsOnceWithoutBlockingInput()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedExperienceToastProfile();
                // Leave exactly 73/80 hold units in the historical profile so
                // buying 100 water legitimately reaches the milestone and fills the hold.
                foreach (string cargo in new[] { "Beer", "Medicine", "Wood", "Iron", "Cloth", "Powder", "Cannonballs" })
                    PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey(cargo), 0);
                PlayerPrefs.SetInt(CoreGameplayDemoController.MarketPurchaseStorageModeKey, (int)MarketPurchaseStorageMode.Ship);
                PlayerPrefs.Save();
                SceneManager.LoadScene(MainMenuController.MainSceneName); yield return null; yield return null;
                yield return new WaitForSecondsRealtime(.35f);
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(game.Session.Cargo.UsedVolume, Is.EqualTo(73f).Within(.001f));
                Assert.That(game.Session.CargoCapacityValue, Is.EqualTo(80f));
                Assert.That(GameObject.Find("Achievement Notification Canvas"), Is.Null, "Loading an old profile must not replay its milestones.");
                string ledger = CoreGameplayDemoController.MarketPurchaseKey(game.Session.CurrentPort, game.Session.GameDateTime, PortMarketItem.Water);
                try
                {
                    yield return ClickAchievementIconControl("Port Hotspot Market");
                    yield return new WaitForSecondsRealtime(.35f);
                    yield return ClickAchievementIconControl("Market Item Icon water");
                    yield return ClickAchievementIconControl("Market Buy water");
                    yield return ClickAchievementIconControl("Confirm Market Buy water");
                    Assert.That(game.Session.Cargo.Water, Is.EqualTo(500));
                    Assert.That(game.Session.Cargo.UsedVolume, Is.EqualTo(game.Session.CargoCapacityValue).Within(.001f));
                    Assert.That(PlayerPrefs.GetInt("ShatteredSail.Achievements.Unlocked.v1.cargo_ready"), Is.EqualTo(1));
                    Assert.That(scope.Store.TryLoad(out _, out string notice), Is.False, "The notification follows completed journal retirement.");
                    Assert.That(notice, Is.Null);
                    GameObject card = GameObject.Find("Achievement Notification");
                    Assert.That(card, Is.Not.Null);
                    Assert.That(card.transform.Find("Achievement Notification Title").GetComponent<Text>().text, Does.Contain("补给充足"));
                    Assert.That(card.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
                    Assert.That(card.GetComponentsInChildren<Selectable>(), Is.Empty);
                    Assert.That(card.GetComponentInParent<CanvasGroup>().blocksRaycasts, Is.False);
                    NavalBattleVisualCapture.Capture(game, "AchievementExperience_03_Toast.png", "真实市场补水跨越成就条件，整组保存完成后提示，不阻挡操作", "Market Panel");
                    int water = game.Session.Cargo.Water;
                    if (ActiveButton("Cancel Market Trade") != null)
                        yield return ClickAchievementIconControl("Cancel Market Trade");
                    if (ActiveButton("Market Detail Back") != null)
                        yield return ClickAchievementIconControl("Market Detail Back");
                    yield return ClickAchievementIconControl("Market Item Icon water");
                    yield return ClickAchievementIconControl("Market Sell water");
                    Slider quantity = GameObject.Find("Market Quantity Slider water").GetComponent<Slider>();
                    Assert.That(quantity.IsInteractable(), Is.True);
                    quantity.value = 1f; yield return null;
                    Assert.That(GameObject.Find("Market Quantity water").GetComponent<Text>().text, Is.EqualTo("1"));
                    decimal gold = game.Session.ProfileGold;
                    decimal revenue = PortMarketCatalog.CalculateSellRevenue(game.Session.CurrentPort,
                        PortMarketItem.Water, game.Session.GameDateTime, 1);
                    Assert.That(card.activeSelf, Is.True, "Exercise actual market input while its non-blocking toast is visible.");
                    yield return ClickAchievementIconControl("Confirm Market Sell water");
                    Assert.That(game.Session.Cargo.Water, Is.EqualTo(water - 1), "The actual market remains clickable while the toast is visible.");
                    Assert.That(game.Session.ProfileGold, Is.EqualTo(gold + revenue));
                    yield return new WaitForSecondsRealtime(4.1f);
                    Assert.That(card.activeSelf, Is.False);
                    InvokeGame(game, "SaveProgress"); yield return null;
                    Assert.That(card.activeSelf, Is.False, "Repeated successful saves must not replay already announced milestones.");
                    SceneManager.LoadScene(MainMenuController.MainSceneName); yield return null; yield return null;
                    Assert.That(GameObject.Find("Achievement Notification Canvas"), Is.Null, "A process/scene baseline includes existing permanent records.");
                    LogAssert.NoUnexpectedReceived();
                }
                finally { PlayerPrefs.DeleteKey(ledger); }
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AchievementExperience_FailedSaveSuppressesToastAndPreservesRecoveryFocus()
        {
            using (var scope = new ProfileJournalSceneScope())
            {
                SeedExperienceToastProfile();
                PlayerPrefs.DeleteKey("ShatteredSail.Achievements.Unlocked.v1.gold_900");
                SceneManager.LoadScene(MainMenuController.MainSceneName); yield return null; yield return null;
                yield return new WaitForSecondsRealtime(.35f);
                var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                game.Session.Cargo.Water = 500;
                InvokeGame(game, "SaveProgress"); yield return null;
                Assert.That(GameObject.Find("Achievement Notification"), Is.Not.Null, "Arrange an already visible legitimate toast before the failure.");
                game.Session.GrantProfileGold(300, "Achievement notification failed-save fixture");
                yield return ClickAchievementIconControl("Port Hub Settings");
                yield return ClickAchievementIconControl("Return To Main Menu");
                Directory.CreateDirectory(scope.DirectoryPath);
                using (var held = new FileStream(scope.Store.PrimaryPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    yield return ClickAchievementIconControl("Confirm Return To Main Menu");
                    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.MainSceneName));
                    Assert.That(GameObject.Find("Achievement Notification"), Is.Null);
                    Assert.That(PlayerPrefs.HasKey("ShatteredSail.Achievements.Unlocked.v1.gold_900"), Is.False);
                    Assert.That(GameObject.Find("Profile Save Failure"), Is.Not.Null);
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(ActiveButton("Recover Profile Save").gameObject));
                    yield return ClickAchievementIconControl("Recover Profile Save");
                    Assert.That(GameObject.Find("Achievement Notification"), Is.Null);
                }
                yield return ClickAchievementIconControl("Recover Profile Save"); yield return null; yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static void SeedExperienceToastProfile()
        {
            SeedJournalSceneProfile();
            // Explicit historical profile fixture: old permanent milestones
            // must not replay when one new gameplay condition becomes true.
            foreach (var definition in AchievementCatalog.All)
                if (definition.Id != "cargo_ready") PlayerPrefs.SetInt("ShatteredSail.Achievements.Unlocked.v1." + definition.Id, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 400);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Food"), 500);
            PlayerPrefs.Save();
        }

        private static string[] VisibleExperienceIds(MainMenuController menu) => menu.GetComponentsInChildren<RawImage>()
            .Where(image => image.name.StartsWith("Achievement Icon ")).Select(image => image.name.Substring("Achievement Icon ".Length)).ToArray();

        private static void AssertExperienceRowsClear(MainMenuController menu)
        {
            Canvas.ForceUpdateCanvases();
            foreach (RawImage icon in menu.GetComponentsInChildren<RawImage>().Where(image => image.name.StartsWith("Achievement Icon ")))
            {
                Transform row = icon.transform.parent;
                Rect progress = AchievementIconBounds(row, row.Find("Progress").GetComponent<RectTransform>());
                foreach (string name in new[] { "Title", "Description", "State" })
                    Assert.That(progress.Overlaps(AchievementIconBounds(row, row.Find(name).GetComponent<RectTransform>())), Is.False, row.name + "/" + name);
            }
        }

        private static IEnumerator SelectExperienceCategory(string label)
        {
            Dropdown dropdown = GameObject.Find("Achievements Category").GetComponent<Dropdown>();
            yield return ClickExperienceSelectable(dropdown);
            var list = GameObject.Find("Dropdown List"); Assert.That(list, Is.Not.Null);
            ScrollRect scroll = list.GetComponent<ScrollRect>();
            // Start at the top of the actual scrollable list; all categories
            // exercised here are visible without changing the selected value.
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            yield return null; Canvas.ForceUpdateCanvases();
            Toggle option = list.GetComponentsInChildren<Toggle>().Single(item => item.GetComponentInChildren<Text>().text == label);
            yield return ClickExperienceSelectable(option);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(dropdown.captionText.text, Is.EqualTo(label));
        }

        private static IEnumerator ClickExperienceSelectable(Selectable control)
        {
            Canvas.ForceUpdateCanvases();
            Canvas canvas = control.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)control.transform;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, control.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(), Is.SameAs(control), control.name + " is blocked by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
