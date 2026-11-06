using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class WorkshopPageNavigationTests
    {
        private static readonly string[] Pages = { "Collection", "DeckBuilder", "Packs", "Upgrade" };
        private static readonly string[] Labels = { "查看已有卡牌", "组建牌组", "打开卡包", "卡牌强化" };
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void FourPagesHaveSeparateContentAndBrowsingNeverChangesProgress()
        {
            CreatePreview();
            decimal gold = controller.Session.ProfileGold;
            Dictionary<NavalBattleStage, int[]> decks = Enum.GetValues(typeof(NavalBattleStage)).Cast<NavalBattleStage>()
                .ToDictionary(stage => stage, stage => controller.Session.GetBattleDeck(stage).ToArray());
            string upgrades = PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Empty);
            Dictionary<NavalBattleStage, string> saves = decks.Keys.ToDictionary(stage => stage,
                stage => PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage), string.Empty));
            AssertHomeContents();
            foreach (string page in Pages)
            {
                Click("Workshop Entrance " + page);
                AssertNavigationAndContentBounds(page);
                Assert.That(Exists("Workshop Collection Detail"), Is.EqualTo(page == "Collection"));
                Assert.That(Exists("Deck Builder Panel"), Is.EqualTo(page == "DeckBuilder"));
                Assert.That(Exists("Workshop Packs Panel"), Is.EqualTo(page == "Packs"));
                Assert.That(Exists("Battle Card Upgrade Panel"), Is.EqualTo(page == "Upgrade"));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Empty), Is.EqualTo(upgrades));
                foreach (NavalBattleStage stage in decks.Keys)
                {
                    CollectionAssert.AreEqual(decks[stage], controller.Session.GetBattleDeck(stage));
                    Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage), string.Empty), Is.EqualTo(saves[stage]));
                }
                Click("Return To Port Hub");
                AssertHomeContents();
            }
            Click("Workshop Entrance Packs");
            Assert.That(Exists("Workshop Packs Owned Count"), Is.False);
            Assert.That(Find<Text>("Workshop Packs Empty Title").text, Does.Contain("暂无"));
            Assert.That(Find<RectTransform>("Workshop Packs Panel").GetComponentsInChildren<Button>()
                .Select(button => button.name), Is.EquivalentTo(new[] {
                    "Workshop Pack Select neutral", "Workshop Pack Select fire", "Workshop Pack Select arcane",
                    "Workshop Pack Select curse", "Workshop Pack Select tide", "Workshop Pack Select thunder",
                    "Workshop Pack Select blood", "Workshop Packs Open Selected", "Workshop Packs View Collection"
                }), "All seven pack categories remain browsable even when inventory is empty.");
            Button emptyOpen = Find<Button>("Workshop Packs Open Selected");
            Assert.That(emptyOpen.interactable, Is.False, "An empty inventory cannot open or claim a pack.");
            string emptyInventory = JsonUtility.ToJson(controller.Session.CaptureCardPackProgress());
            emptyOpen.onClick.Invoke();
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()), Is.EqualTo(emptyInventory),
                "Invoking a disabled empty-pack action must preserve inventory, owned copies and random state.");
            Click("Workshop Packs View Collection");
            Find<RectTransform>("Workshop Collection Detail");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
        }

        [Test]
        public void OpeningTwoPacksThroughRealButtons_GrantsExactlyThreeCopiesEachAndRejectsStaleActions()
        {
            CreatePreview();
            string savedPacks = PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey, string.Empty);
            bool hadSavedPacks = PlayerPrefs.HasKey(CoreGameplayDemoController.CardPackProgressKey);
            decimal gold = controller.Session.ProfileGold;
            Assert.That(controller.Session.GrantCardPacks("fire", 2), Is.True);
            Click("Workshop Entrance Packs");
            Click("Workshop Pack Select fire");
            Button.ButtonClickedEvent firstAction = Find<Button>("Workshop Packs Open Selected").onClick;
            Click("Workshop Packs Open Selected");
            Canvas.ForceUpdateCanvases();

            CoreCardPackProgress first = controller.Session.CaptureCardPackProgress();
            Assert.That(controller.Session.GetCardPackCount("fire"), Is.EqualTo(1));
            Assert.That(first.cards.Sum(entry => entry.count), Is.EqualTo(3));
            Assert.That(Find<Text>("Workshop Packs Results Heading").text, Is.EqualTo("获得 3 张卡牌"));
            for (int slot = 0; slot < 3; slot++) Find<RectTransform>("Workshop Pack Result " + slot + " Slot");
            string firstState = JsonUtility.ToJson(first);
            firstAction.Invoke();
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()), Is.EqualTo(firstState),
                "A callback retained from the spent opening button cannot consume the next pack.");

            Button.ButtonClickedEvent secondAction = Find<Button>("Workshop Packs Open Again").onClick;
            Click("Workshop Packs Open Again");
            Assert.That(controller.Session.GetCardPackCount("fire"), Is.Zero);
            Assert.That(controller.Session.CaptureCardPackProgress().cards.Sum(entry => entry.count), Is.EqualTo(6));
            Button exhausted = Find<Button>("Workshop Packs Open Again");
            Assert.That(exhausted.interactable, Is.False);
            string finalState = JsonUtility.ToJson(controller.Session.CaptureCardPackProgress());
            firstAction.Invoke();
            secondAction.Invoke();
            exhausted.onClick.Invoke();
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()), Is.EqualTo(finalState),
                "Stale result callbacks and the exhausted action cannot duplicate cards or advance random state.");

            Click("Workshop Packs View Collection");
            Find<RectTransform>("Workshop Collection Detail");
            foreach (CoreCardCopySave copy in controller.Session.CaptureCardPackProgress().cards)
            {
                int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .First(candidate => CoreVoyageSession.CardLibrary[candidate].Id == copy.id);
                Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(index), Is.GreaterThanOrEqualTo(copy.count),
                    "Every awarded copy must be available to the collection and deck builder.");
            }
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()), Is.EqualTo(finalState));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(hadSavedPacks));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey, string.Empty), Is.EqualTo(savedPacks),
                "An authoring preview must not persist this test inventory into the player's real profile.");
        }

        [Test]
        public void CollectionDetailActionsOpenDeckBuilderAndUpgradeTheSelectedCard()
        {
            CreatePreview();
            Click("Workshop Entrance Collection");
            Button choice = host.GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Upgrade Card ")).Skip(1).First();
            int index = int.Parse(choice.name.Substring("Select Upgrade Card ".Length));
            Click(choice.name);
            decimal gold = controller.Session.ProfileGold;
            string upgrades = PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Empty);
            Click("Workshop Collection Card Action");
            Find<RectTransform>("Workshop Upgrade Library");
            Assert.That((int)Field("selectedBattleCardUpgrade").GetValue(controller), Is.EqualTo(index));
            NavalBattleStage stage = (NavalBattleStage)Field("selectedBattleDeckStage").GetValue(controller);
            Assert.That(Find<Text>("Upgrade Comparison Name Current").text,
                Is.EqualTo(CardFaceLayout.DisplayName(controller.Session.GetBattleCard(index, stage))));
            Click("Workshop Page Collection");
            Assert.That((int)Field("selectedBattleCardUpgrade").GetValue(controller), Is.EqualTo(index));
            Click("Workshop Collection Build Deck");
            Find<RectTransform>("Deck Builder Panel");
            Assert.That((NavalBattleStage)Field("selectedBattleDeckStage").GetValue(controller), Is.EqualTo(stage));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.BattleCardUpgradeKey, string.Empty), Is.EqualTo(upgrades));
        }

        [Test]
        public void IncompleteDraftBlocksAllFourPageCallbacksAndKeepsLastValidDeck()
        {
            CreatePreview();
            Click("Workshop Entrance DeckBuilder");
            string[] blocked = Pages.Select(page => "Workshop Page " + page).Concat(new[] { "Return To Port Hub" }).ToArray();
            Button.ButtonClickedEvent[] previousCallbacks = blocked.Select(name => Find<Button>(name).onClick).ToArray();
            NavalBattleStage stage = (NavalBattleStage)Field("selectedBattleDeckStage").GetValue(controller);
            int[] valid = controller.Session.GetBattleDeck(stage).ToArray();
            List<int> draft = valid.Take(CoreVoyageSession.MinimumBaseBattleDeckSize - 1).ToList();
            Field("deckBuilderDraft").SetValue(controller, draft);
            Field("deckBuilderDraftSession").SetValue(controller, controller.Session);
            Field("deckBuilderDraftStage").SetValue(controller, stage);
            Field("deckBuilderDraftPort").SetValue(controller, controller.Session.CurrentPort);
            typeof(CoreGameplayDemoController).GetMethod("Render", PrivateInstance).Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
            foreach (string name in blocked)
            {
                Button tab = Find<Button>(name);
                Assert.That(tab.interactable, Is.False, name);
                tab.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                AssertDraftRetained(draft, valid, stage);
            }
            foreach (Button.ButtonClickedEvent callback in previousCallbacks)
            {
                callback.Invoke();
                Canvas.ForceUpdateCanvases();
                AssertDraftRetained(draft, valid, stage);
            }
            Type pageType = typeof(CoreGameplayDemoController).GetNestedType("WorkshopPage", BindingFlags.NonPublic);
            MethodInfo select = typeof(CoreGameplayDemoController).GetMethod("SelectWorkshopPage", PrivateInstance);
            foreach (string page in Pages.Concat(new[] { "Home" }))
            {
                select.Invoke(controller, new[] { Enum.Parse(pageType, page) });
                AssertDraftRetained(draft, valid, stage);
            }
            Assert.That(Find<Button>("Workshop School Filter neutral").interactable, Is.True);
        }

        [Test]
        public void UpgradePageRejectsSpecialCollectionEvenWhenItsDisabledCallbackIsInvoked()
        {
            CreatePreview();
            Click("Workshop Entrance Collection");
            Button.ButtonClickedEvent special = Find<Button>("Open Special Card Collection").onClick;
            Click("Open Special Card Collection");
            Click("Workshop Page Upgrade");
            Assert.That(Exists("Open Special Card Collection"), Is.False, "The upgrade picker contains ordinary cards only.");
            special.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That((bool)Field("specialCardCollectionOpen").GetValue(controller), Is.False);
            Find<RectTransform>("Battle Card Upgrade Panel");
            Assert.That(host.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Select Special Card ")), Is.False);
            Click("Workshop School Filter fire");
            Assert.That(Exists("Confirm Battle Card Upgrade"), Is.False,
                "A locked school has no selected card and cannot retain a stale upgrade action.");
        }

        private void AssertHomeContents()
        {
            Find<RectTransform>("Workshop Home Panel");
            Assert.That(Find<RawImage>("Workshop Home Background").texture, Is.Not.Null, "The home uses the workshop scene artwork.");
            Assert.That(Exists("Workshop Page Navigation"), Is.False, "The entrance is a scene rather than the content tab strip.");
            Assert.That(Exists("Workshop Preview Collection"), Is.False);
            Assert.That(Exists("Deck Builder Collection"), Is.False);
            Assert.That(host.GetComponentsInChildren<CardSchoolFrameGraphic>(), Is.Empty, "Card browsing starts only after choosing its scene entrance.");
            Button[] entrances = host.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Workshop Entrance ")).ToArray();
            CollectionAssert.AreEqual(Pages.Select(page => "Workshop Entrance " + page), entrances.Select(button => button.name));
            CollectionAssert.AreEqual(Labels, entrances.Select(button => button.GetComponentInChildren<Text>().text));
            Assert.That(entrances.All(button => button.IsInteractable()), Is.True);
        }

        private void AssertDraftRetained(List<int> draft, int[] valid, NavalBattleStage stage)
        {
            Find<RectTransform>("Deck Builder Panel");
            Assert.That(Field("deckBuilderDraft").GetValue(controller), Is.SameAs(draft));
            CollectionAssert.AreEqual(valid, controller.Session.GetBattleDeck(stage));
            Assert.That(Find<Text>("Deck Builder Status").text, Does.Contain("补足"));
        }

        private void AssertNavigationAndContentBounds(string page)
        {
            RectTransform shell = Find<RectTransform>("Workshop Page Shell");
            RectTransform navigation = Find<RectTransform>("Workshop Page Navigation");
            Button[] tabs = navigation.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Workshop Page ")).ToArray();
            CollectionAssert.AreEqual(Pages.Select(value => "Workshop Page " + value), tabs.Select(button => button.name));
            CollectionAssert.AreEqual(Labels, tabs.Select(button => button.GetComponentInChildren<Text>().text));
            Assert.That(Exists("Workshop Return Home"), Is.False);
            Assert.That(Exists("Workshop Current Page Title"), Is.False);
            Assert.That(navigation.GetComponentsInChildren<Button>(), Has.Length.EqualTo(4));
            Find<Button>("Return To Port Hub");
            Rect? previous = null;
            foreach (Button tab in tabs)
            {
                Rect bounds = BoundsIn(shell, tab.GetComponent<RectTransform>());
                AssertInside(shell.rect, bounds, tab.name);
                if (previous.HasValue) Assert.That(bounds.xMin, Is.GreaterThan(previous.Value.xMax));
                previous = bounds;
                Text label = tab.GetComponentInChildren<Text>();
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f));
            }
            string contentName = page == "Packs" ? "Workshop Packs Panel" : page == "DeckBuilder" ? "Deck Builder Panel" : page == "Upgrade" ? "Card Workshop Upgrade Workspace" : "Card Workshop Preview Panel";
            Rect content = BoundsIn(shell, Find<RectTransform>(contentName));
            AssertInside(shell.rect, content, contentName);
            foreach (Button tab in tabs)
                Assert.That(content.Overlaps(BoundsIn(shell, tab.GetComponent<RectTransform>())), Is.False,
                    "Page navigation must remain above the active content.");
        }

        private void CreatePreview()
        {
            host = new GameObject("Workshop Page Navigation Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        private void Click(string name)
        {
            Button button = Find<Button>(name);
            Assert.That(button.interactable, Is.True, name);
            button.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
        }
        private static FieldInfo Field(string name) => typeof(CoreGameplayDemoController).GetField(name, PrivateInstance);
        private bool Exists(string name) => host.GetComponentsInChildren<Transform>().Any(item => item.name == name);
        private T Find<T>(string name) where T : Component
        {
            T result = host.GetComponentsInChildren<T>().FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }
        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y), local.Max(point => point.x), local.Max(point => point.y));
        }
        private static void AssertInside(Rect outer, Rect inner, string label)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .5f), label);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .5f), label);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .5f), label);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .5f), label);
        }
    }
}
