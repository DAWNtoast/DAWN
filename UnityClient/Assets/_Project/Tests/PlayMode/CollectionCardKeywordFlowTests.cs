using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator CollectionKeyword_UpgradeComparisonHoverKeepsBothCardsUnobscured()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(value => CoreVoyageSession.CardLibrary[value].Id == "card_load_shot");
            Button choice = null;
            for (int page = 0; page < 20; page++)
            {
                choice = GameObject.Find("Select Upgrade Card " + index)?.GetComponent<Button>();
                if (choice != null) break;
                Button next = ActiveButton("Next Upgrade Cards");
                Assert.That(next.interactable, Is.True, "The real collection must contain the load-shot card.");
                next.onClick.Invoke();
                yield return null;
            }
            Assert.That(choice, Is.Not.Null);
            choice.onClick.Invoke();
            yield return null;
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            RectTransform current = GameObject.Find("Upgrade Comparison Current Face").GetComponent<RectTransform>();
            RectTransform upgraded = GameObject.Find("Upgrade Comparison Next Face").GetComponent<RectTransform>();
            RectTransform viewport = FixedGameViewport.Bounds(current.GetComponentInParent<Canvas>());
            Rect currentBounds = CollectionCardKeywordHoverTests.VisualBounds(current, viewport);
            Rect upgradedBounds = CollectionCardKeywordHoverTests.VisualBounds(upgraded, viewport);
            foreach (RectTransform inspected in new[] { current, upgraded })
            {
                var pointer = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(inspected.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return null;
                Canvas.ForceUpdateCanvases();
                CardKeywordTooltip[] tooltips = VisibleCollectionKeywords();
                Assert.That(tooltips, Has.Length.EqualTo(1), inspected.name);
                Rect bubble = CollectionCardKeywordHoverTests.Bounds((RectTransform)tooltips[0].transform, viewport);
                Assert.That(bubble.Overlaps(currentBounds), Is.False, inspected.name + " must leave the current rank readable");
                Assert.That(bubble.Overlaps(upgradedBounds), Is.False, inspected.name + " must leave the upgraded rank readable");
                Assert.That(bubble.xMax, Is.LessThanOrEqualTo(Mathf.Min(currentBounds.xMin, upgradedBounds.xMin)
                    - CardKeywordTooltip.CardGap + .2f), "The glossary belongs beside the complete comparison.");
                Assert.That(bubble.xMin, Is.GreaterThanOrEqualTo(viewport.rect.xMin));
                Assert.That(bubble.xMax, Is.LessThanOrEqualTo(viewport.rect.xMax));
                Assert.That(bubble.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin));
                Assert.That(bubble.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax));
                ExecuteEvents.Execute(inspected.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                Assert.That(VisibleCollectionKeywords(), Is.Empty);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CollectionKeyword_RegularGridFlipsSidesAndSurvivesPageUpgradeAndDeckNavigation()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            ActiveButton("Workshop Preview Stage Naval").onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            bool sawLeft = false, sawRight = false;
            for (int page = 0; page < 12; page++)
            {
                Button[] choices = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                    .Where(button => button.name.StartsWith("Select Upgrade Card ")).ToArray();
                foreach (Button choice in choices)
                {
                    int index = int.Parse(choice.name.Substring("Select Upgrade Card ".Length));
                    CardKeywordTooltip tooltip = AssertCollectionKeywordHover(choice, controller.Session.GetBattleCard(index));
                    if (tooltip == null) continue;
                    RectTransform viewport = FixedGameViewport.Bounds(choice.GetComponentInParent<Canvas>());
                    RectTransform face = CollectionKeywordFace(choice);
                    bool right = CollectionCardKeywordHoverTests.Bounds((RectTransform)tooltip.transform, viewport).xMin >
                        CollectionCardKeywordHoverTests.VisualBounds(face, viewport).xMax;
                    if (right && !sawRight || !right && !sawLeft)
                        NavalBattleVisualCapture.Capture(controller, right ? "collection_left_edge_right_tooltip.png" : "collection_left_tooltip.png",
                            right ? "左侧收藏卡牌没有空间时，关键词浮窗自动显示在卡牌右侧" : "收藏卡牌默认在左侧显示关键词与释义", "Card Workshop Preview Panel");
                    sawRight |= right; sawLeft |= !right;
                }
                if (sawLeft && sawRight) break;
                Button next = ActiveButton("Next Upgrade Cards");
                Assert.That(next.interactable, Is.True, "The real catalog must exercise both placement branches.");
                next.onClick.Invoke();
                yield return null;
                Assert.That(VisibleCollectionKeywords(), Is.Empty, "Paging destroys the old card and tooltip.");
            }
            Assert.That(sawLeft && sawRight, Is.True);
            ActiveButton("Next Upgrade Cards").onClick.Invoke();
            yield return null;
            Assert.That(VisibleCollectionKeywords(), Is.Empty);
            ActiveButton("Open Battle Card Upgrades").onClick.Invoke();
            yield return null;
            foreach (string side in new[] { "Current", "Next" })
            {
                var comparison = GameObject.Find("Upgrade Comparison " + side + " Face");
                Assert.That(comparison.GetComponent<CollectionCardKeywordHover>(), Is.Not.Null, side + " upgrade card hover");
            }
            ActiveButton("Back From Card Upgrades").onClick.Invoke();
            yield return null;
            ActiveButton("Edit Stage Deck Naval").onClick.Invoke();
            yield return null;
            Button[] deckChoices = GameObject.Find("Deck Builder Collection").GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Add Deck Card ")).ToArray();
            Assert.That(deckChoices, Is.Not.Empty);
            foreach (Button choice in deckChoices)
            {
                int index = int.Parse(choice.name.Substring("Add Deck Card ".Length));
                AssertCollectionKeywordHover(choice, controller.Session.GetBattleCard(index, NavalBattleStage.Naval));
                Assert.That(choice.GetComponent<DeckBuilderCardDrag>(), Is.Not.Null);
            }
            ActiveButton("Back To Card Workshop Chests").onClick.Invoke();
            yield return null;
            Assert.That(VisibleCollectionKeywords(), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CollectionKeyword_SpecialGridExplainsOnlyVisibleKeywordsAndClickStillSelects()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.AcquireSpecialCards(CoreSpecialCardCatalog.All.Select(item => item.Card.Id).ToArray());
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            ActiveButton("Open Special Card Collection").onClick.Invoke();
            yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            Button[] choices = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Select Special Card ")).ToArray();
            Assert.That(choices, Has.Length.EqualTo(8));
            Button inspected = null;
            foreach (Button choice in choices)
            {
                int index = int.Parse(choice.name.Substring("Select Special Card ".Length));
                if (AssertCollectionKeywordHover(choice, controller.Session.GetBattleCard(index)) == null) continue;
                inspected = choice;
            }
            Assert.That(inspected, Is.Not.Null, "Real special cards must have authored keywords.");
            inspected.GetComponent<CollectionCardKeywordHover>().Show();
            NavalBattleVisualCapture.Capture(controller, "special_collection_keywords.png",
                "特殊卡收藏采用相同关键词浮窗，内容与当前可见卡面一致", "Card Workshop Preview Panel");
            string selectedName = inspected.name;
            ExecuteEvents.Execute(inspected.gameObject, new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(VisibleCollectionKeywords(), Is.Empty, "Click still selects and replaces the collection page.");
            Assert.That(ActiveButton(selectedName).GetComponentInChildren<CardSchoolFrameGraphic>().GetComponent<Outline>(), Is.Not.Null);
            ActiveButton("Next Upgrade Cards").onClick.Invoke();
            yield return null;
            Assert.That(VisibleCollectionKeywords(), Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        private static CardKeywordTooltip AssertCollectionKeywordHover(Button button, CoreBattleCard card)
        {
            CollectionCardKeywordHover hover = button.GetComponent<CollectionCardKeywordHover>();
            Assert.That(hover, Is.Not.Null, button.name);
            RectTransform face = CollectionKeywordFace(button);
            Text effect = face.GetComponentsInChildren<Text>().Single(label =>
                label.name.EndsWith(" Effect") || label.name.StartsWith("Workshop Card Description "));
            string description = (string)typeof(CardDescriptionText).GetMethod("SourceFor",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { effect });
            var expected = CoreBattleCardKeywordText.Build(card, description);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            var tooltips = VisibleCollectionKeywords();
            if (expected.Count == 0) { Assert.That(tooltips, Is.Empty, button.name); return null; }
            Assert.That(tooltips, Has.Length.EqualTo(1), button.name);
            CardKeywordTooltip tooltip = tooltips[0];
            CollectionAssert.AreEqual(expected.SelectMany(entry => new[] { entry.Keyword, entry.Meaning }).ToArray(),
                tooltip.GetComponentsInChildren<Text>().Select(label => label.text).ToArray(), button.name);
            RectTransform viewport = FixedGameViewport.Bounds(button.GetComponentInParent<Canvas>());
            Rect bubble = CollectionCardKeywordHoverTests.Bounds((RectTransform)tooltip.transform, viewport);
            Rect frame = CollectionCardKeywordHoverTests.VisualBounds(face, viewport);
            bool shouldRight = frame.xMin - CardKeywordTooltip.CardGap - bubble.width < viewport.rect.xMin + 8f;
            if (shouldRight) Assert.That(bubble.xMin, Is.GreaterThanOrEqualTo(frame.xMax + CardKeywordTooltip.CardGap - .2f), button.name);
            else Assert.That(bubble.xMax, Is.LessThanOrEqualTo(frame.xMin - CardKeywordTooltip.CardGap + .2f), button.name);
            Assert.That(bubble.xMin, Is.GreaterThanOrEqualTo(viewport.rect.xMin), button.name);
            Assert.That(bubble.xMax, Is.LessThanOrEqualTo(viewport.rect.xMax), button.name);
            Assert.That(tooltip.GetComponent<Canvas>().overrideSorting, Is.True);
            Assert.That(tooltip.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
            Canvas.ForceUpdateCanvases();
            foreach (Text label in tooltip.GetComponentsInChildren<Text>())
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length),
                    button.name + " renders the entire keyword explanation, including its last line: " + label.text);
            return tooltip;
        }

        private static RectTransform CollectionKeywordFace(Button button) => button.GetComponentsInChildren<RectTransform>()
            .First(rect => rect.name.EndsWith(" Face"));

        private static CardKeywordTooltip[] VisibleCollectionKeywords() => Object.FindObjectsByType<CardKeywordTooltip>(FindObjectsSortMode.None)
            .Where(tooltip => tooltip.name == "Collection Card Keyword Tooltip").ToArray();
    }
}
