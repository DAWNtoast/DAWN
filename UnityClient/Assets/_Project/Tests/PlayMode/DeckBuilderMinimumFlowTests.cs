using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private static readonly string[] DeckBuilderBlockedNavigation =
        {
            "Return To Port Hub", "Previous Port Facility", "Next Port Facility", "Port Facility Settings",
            "Battle Deck Stage Tab Preparation", "Battle Deck Stage Tab Naval", "Battle Deck Stage Tab Boarding",
            "Open Special Card Collection", "Back To Card Workshop Chests",
        };

        [UnityTest, Timeout(120000)]
        public IEnumerator DeckBuilderMinimum_EmptyDraftRefillsAcrossPagesAndRestoresCachedNavigation()
        {
            yield return LoadDeckBuilderCardFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            GameObject cachedHeader = GameObject.Find("Port Facility Header");
            Button cachedSettings = ActiveButton("Port Facility Settings");
            int[] preparation = controller.Session.GetBattleDeck(NavalBattleStage.Preparation).ToArray();
            int[] boarding = controller.Session.GetBattleDeck(NavalBattleStage.Boarding).ToArray();
            int[] bonus = controller.Session.GetBonusBattleCards(NavalBattleStage.Naval).ToArray();
            decimal gold = controller.Session.ProfileGold;
            AssertDeckBuilderMinimumLock(false);
            AssertDeckBuilderMinimumHint(12);

            while (controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count > 10)
            {
                Button row = DeckBuilderBaseCards().First();
                yield return RevealDeckBuilderCard(row);
                ClickDeckBuilderHeader(row.name);
                yield return null;
            }
            int[] committed = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            Dictionary<string, string> saved = DeckBuilderSavedProgress();
            // Keep events from the legal state too: cached controls must not offer a
            // bypass after their original listeners have been wrapped or replaced.
            Button.ButtonClickedEvent[] legalEvents = DeckBuilderBlockedNavigation
                .Select(name => ActiveButton(name).onClick).ToArray();

            for (int expected = 9; expected >= 0; expected--)
            {
                Button row = DeckBuilderBaseCards().First();
                yield return RevealDeckBuilderCard(row);
                if (expected == 0)
                    yield return DragDeckBuilderCard(row, DeckBuilderNamedPoint("Deck Builder Parchment"), false);
                else
                {
                    ClickDeckBuilderHeader(row.name);
                    yield return null;
                }
                AssertDeckBuilderDraftCount(expected);
                AssertDeckBuilderMinimumHint(expected);
                AssertDeckBuilderMinimumLock(true);
                CollectionAssert.AreEqual(committed, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
                CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress(),
                    "Incomplete edits must not persist an invalid deck or modify unrelated progress.");
            }
            Assert.That(DeckBuilderBaseCards(), Is.Empty, "All base cards can be removed, including the last one.");
            CollectionAssert.AreEqual(bonus, controller.Session.GetBonusBattleCards(NavalBattleStage.Naval),
                "Source-provided special cards cannot satisfy or change the base-card minimum.");
            Assert.That(DeckBuilderSpecialCards(), Is.Not.Empty);
            foreach (Button.ButtonClickedEvent click in legalEvents)
            {
                click.Invoke();
                yield return null;
                AssertDeckBuilderDraftCount(0);
                AssertDeckBuilderMinimumLock(true);
                Assert.That(GameObject.Find("Port Settings Overlay"), Is.Null);
            }
            yield return AssertDeckBuilderBlockedCallbacksPreserveDraft(controller, 0, committed, saved);
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderMinimum_01_EmptyLocked.png",
                "基础卡可清空为0张；阶段、特殊牌、设施切换、返回和设置均禁用，只能继续构筑", "Deck Builder Panel");

            string firstPage = GameObject.Find("Battle Card Collection Page").GetComponent<Text>().text;
            ClickDeckBuilderHeader("Next Battle Card Collection Page");
            yield return null;
            Assert.That(GameObject.Find("Battle Card Collection Page").GetComponent<Text>().text, Is.Not.EqualTo(firstPage));
            AssertDeckBuilderDraftCount(0);
            AssertDeckBuilderMinimumLock(true);
            int secondPageCard = DeckBuilderLibraryChoices().First();
            ClickDeckBuilderHeader("Add Deck Card " + secondPageCard);
            yield return null;
            yield return DragDeckBuilderCard(ActiveButton("Add Deck Card " + secondPageCard),
                DeckBuilderNamedPoint("Deck Builder Deck Rail"), false);
            AssertDeckBuilderDraftCount(2);
            CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress());
            ClickDeckBuilderHeader("Previous Battle Card Collection Page");
            yield return null;
            Assert.That(GameObject.Find("Battle Card Collection Page").GetComponent<Text>().text, Is.EqualTo(firstPage));
            AssertDeckBuilderDraftCount(2);

            int[] fill = DeckBuilderLibraryChoices().Take(4).SelectMany(index => new[] { index, index }).ToArray();
            Assert.That(fill, Has.Length.EqualTo(8), "The real first collection page supplies enough owned base cards.");
            for (int index = 0; index < fill.Length; index++)
            {
                ClickDeckBuilderHeader("Add Deck Card " + fill[index]);
                yield return null;
                int total = 3 + index;
                AssertDeckBuilderDraftCount(total);
                AssertDeckBuilderMinimumHint(total);
                Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(cachedHeader));
                Assert.That(ActiveButton("Port Facility Settings"), Is.SameAs(cachedSettings));
                if (total < 10)
                {
                    AssertDeckBuilderMinimumLock(true);
                    CollectionAssert.AreEqual(committed, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
                    CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress());
                }
                if (total == 9)
                {
                    yield return AssertDeckBuilderBlockedCallbacksPreserveDraft(controller, 9, committed, saved);
                    yield return new WaitForSecondsRealtime(.7f);
                    NavalBattleVisualCapture.Capture(controller, "DeckBuilderMinimum_02_NineLocked.png",
                        "当前9张，提示补足至少10张后离开，全部导航仍保持禁用", "Deck Builder Panel");
                }
            }
            int[] finalDeck = new[] { secondPageCard, secondPageCard }.Concat(fill).ToArray();
            CollectionAssert.AreEqual(finalDeck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            AssertDeckBuilderSaved(controller, 10);
            AssertDeckBuilderMinimumLock(false);
            CollectionAssert.AreEqual(preparation, controller.Session.GetBattleDeck(NavalBattleStage.Preparation));
            CollectionAssert.AreEqual(boarding, controller.Session.GetBattleDeck(NavalBattleStage.Boarding));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderMinimum_03_TenUnlocked.png",
                "补足10张后自动保存，恢复全部按钮，仅保留基础卡至少10张提示", "Deck Builder Panel");

            ClickDeckBuilderHeader("Port Facility Settings");
            yield return null;
            Assert.That(GameObject.Find("Port Settings Overlay"), Is.Not.Null,
                "The retained settings button regains its original action after committing a legal draft.");
            ClickDeckBuilderHeader("Close Port Settings");
            yield return null;
            ClickDeckBuilderHeader("Battle Deck Stage Tab Preparation");
            yield return null;
            Assert.That(GameObject.Find("Deck Builder Deck Title").GetComponent<Text>().text, Does.Contain("备战策略"));
            ClickDeckBuilderHeader("Battle Deck Stage Tab Naval");
            yield return null;
            AssertDeckBuilderSaved(controller, 10);
            ClickDeckBuilderHeader("Open Special Card Collection");
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            ClickDeckBuilderHeader("Workshop Preview Stage Naval");
            yield return null;
            ClickDeckBuilderHeader("Back To Card Workshop Chests");
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            ClickDeckBuilderHeader("Previous Port Facility");
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Null);
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(cachedHeader));
            Assert.That(ActiveButton("Port Facility Settings"), Is.SameAs(cachedSettings));
            ClickDeckBuilderHeader("Next Port Facility");
            yield return null;
            Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
            ClickDeckBuilderHeader("Return To Port Hub");
            yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CollectionAssert.AreEqual(finalDeck, controller.Session.GetBattleDeck(NavalBattleStage.Naval),
                "Only the final complete draft is restored after reloading.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DeckBuilderMinimum_ReloadDuringEmptyDraftPreservesLastValidSavedDeck()
        {
            yield return LoadDeckBuilderCardFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            int[] committed = null;
            Dictionary<string, string> saved = null;
            for (int expected = 11; expected >= 0; expected--)
            {
                Button row = DeckBuilderBaseCards().First();
                yield return RevealDeckBuilderCard(row);
                ClickDeckBuilderHeader(row.name);
                yield return null;
                AssertDeckBuilderDraftCount(expected);
                if (expected == 10)
                {
                    committed = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
                    saved = DeckBuilderSavedProgress();
                }
            }
            AssertDeckBuilderMinimumLock(true);
            CollectionAssert.AreEqual(committed, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress());
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CollectionAssert.AreEqual(committed, controller.Session.GetBattleDeck(NavalBattleStage.Naval),
                "An interrupted incomplete edit must never replace the last valid persisted deck with zero cards.");
            ClickDeckBuilderHeader("Port Hotspot CardWorkshop");
            yield return null;
            ClickDeckBuilderHeader("Edit Stage Deck Naval");
            yield return null;
            AssertDeckBuilderSaved(controller, 10);
            AssertDeckBuilderMinimumLock(false);
            AssertDeckBuilderMinimumHint(10);
            LogAssert.NoUnexpectedReceived();
        }

        private static int[] DeckBuilderLibraryChoices() => GameObject.Find("Deck Builder Collection")
            .GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Add Deck Card "))
            .Select(DeckBuilderCardIndex).ToArray();

        private static void AssertDeckBuilderDraftCount(int count)
        {
            Assert.That(GameObject.Find("Deck Builder Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Deck Builder Deck Title").GetComponent<Text>().text, Does.Contain("炮击机动"));
            Assert.That(GameObject.Find("Deck Builder Base Column Count").GetComponent<Text>().text,
                Is.EqualTo("基础卡 " + count + " 张"));
            Assert.That(DeckBuilderBaseCards().Sum(row => int.Parse(row.GetComponentsInChildren<Text>()
                .Single(label => label.name == row.name + " Quantity").text)), Is.EqualTo(count),
                "The visible compact rows and their copy counts must match the draft count, including an empty draft.");
        }

        private static void AssertDeckBuilderMinimumHint(int count)
        {
            string hint = GameObject.Find("Deck Builder Status").GetComponent<Text>().text;
            Assert.That(hint, Does.Contain("至少").And.Contain("10"));
            Assert.That(hint, Does.Not.Contain("已自动保存"));
            if (count < 10) Assert.That(hint, Does.Contain("当前 " + count + " 张").And.Contain("补足"));
            else Assert.That(hint, Does.Not.Contain("补足"));
        }

        private static void AssertDeckBuilderMinimumLock(bool locked)
        {
            foreach (string name in DeckBuilderBlockedNavigation)
            {
                Button button = ActiveButton(name);
                Assert.That(button, Is.Not.Null, name);
                Assert.That(button.interactable, Is.EqualTo(!locked), name);
            }
            if (!locked) return;
            foreach (Button button in GameObject.Find("Phase Content").GetComponentsInChildren<Button>())
            {
                bool deckEdit = button.name.StartsWith("Add Deck Card ") || button.name.StartsWith("Remove Deck Card ");
                bool pager = button.name == "Previous Battle Card Collection Page" || button.name == "Next Battle Card Collection Page";
                if (deckEdit) Assert.That(button.interactable, Is.True, button.name + " must remain available to finish the draft.");
                else if (!pager) Assert.That(button.interactable, Is.False, button.name + " must be locked while the draft is incomplete.");
            }
        }

        private static IEnumerator AssertDeckBuilderBlockedCallbacksPreserveDraft(CoreGameplayDemoController controller,
            int count, int[] committed, Dictionary<string, string> saved)
        {
            GameObject panel = GameObject.Find("Deck Builder Panel");
            foreach (string name in DeckBuilderBlockedNavigation)
            {
                Button button = ActiveButton(name);
                Assert.That(button.interactable, Is.False, name);
                button.onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.SameAs(panel),
                    name + " cannot bypass the incomplete-deck guard by invoking its event directly.");
                AssertDeckBuilderDraftCount(count);
                AssertDeckBuilderMinimumHint(count);
                AssertDeckBuilderMinimumLock(true);
                Assert.That(GameObject.Find("Port Settings Overlay"), Is.Null);
                CollectionAssert.AreEqual(committed, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
                CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress());
            }
        }
    }
}
