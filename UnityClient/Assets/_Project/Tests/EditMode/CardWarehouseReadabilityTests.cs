using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class CardWarehouseReadabilityTests
    {
        private int? previousLevel;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [SetUp]
        public void SetUp() => previousLevel = GameReadability.OverrideLevel;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameReadability.OverrideLevel = previousLevel;
        }

        [TestCase(0, 6)]
        [TestCase(1, 4)]
        [TestCase(2, 2)]
        public void Workshop_ChangesCapacityAndKeepsEveryCardReachable(int level, int capacity)
        {
            Create(CoreGameplayPreviewScreen.DeckBuilder, level);
            int[] expected = controller.Session.GetAvailableBattleCards(NavalBattleStage.Preparation)
                .Where(index => string.IsNullOrEmpty(CoreVoyageSession.CardLibrary[index].RequiredCrew) &&
                    string.IsNullOrEmpty(CoreVoyageSession.CardLibrary[index].RequiredComponent)).ToArray();
            var reached = new HashSet<int>();
            for (int page = 0; page < 40; page++)
            {
                Button[] cards = host.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("Add Deck Card ", StringComparison.Ordinal)).ToArray();
                Assert.That(cards.Length, Is.InRange(1, capacity));
                if (page == 0) Assert.That(cards.Length, Is.EqualTo(Math.Min(capacity, expected.Length)));
                foreach (Button card in cards)
                {
                    int id = int.Parse(card.name.Substring("Add Deck Card ".Length));
                    Assert.That(reached.Add(id), Is.True, "No duplicate or unreachable collection page.");
                    Text paragraph = Find<Text>("Workshop Card Description " + id);
                    Assert.That(paragraph.text, Is.EqualTo(controller.Session.GetBattleCard(id).Description));
                    AssertParagraph(paragraph);
                    AssertInside(card.GetComponent<RectTransform>(), paragraph.GetComponentInParent<ScrollRect>(true).viewport);
                }
                Button next = Find<Button>("Next Battle Card Collection Page");
                if (!next.interactable) break;
                next.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                Assert.That(page, Is.LessThan(39), "Collection pagination must terminate.");
            }
            CollectionAssert.AreEquivalent(expected, reached);
            ScrollRect rail = Find<RectTransform>("Deck Builder Deck Scroll Viewport").GetComponent<ScrollRect>();
            Button[] rows = rail.content.GetComponentsInChildren<Button>(true);
            foreach (Button row in rows)
            {
                Assert.That(row.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(60f * GameReadability.Scale));
                foreach (Text text in row.GetComponentsInChildren<Text>())
                {
                    Assert.That(text.font, Is.SameAs(GameTypography.BodyFont));
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(18)));
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f), text.name);
                }
            }
            AssertFirstAndLastRowsReachable(rail, rows);
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void Warehouse_LargerRowsRetainEveryCargoAndScrollToBothEnds(int level, bool rare)
        {
            Create(CoreGameplayPreviewScreen.Warehouse, level);
            if (rare) controller.ConfigureEditorPreviewWarehouseRare(0);
            else controller.ConfigureEditorPreviewWarehouseStorage(0);
            Canvas.ForceUpdateCanvases();
            string prefix = rare ? "Warehouse Rare" : "Warehouse Storage";
            ScrollRect scroll = Find<RectTransform>(prefix + " Rows Viewport").GetComponent<ScrollRect>();
            Button[] rows = scroll.content.GetComponentsInChildren<Button>(true);
            Assert.That(rows, Has.Length.EqualTo(rare ? 6 : 9));
            foreach (Button row in rows)
            {
                Assert.That(row.interactable, Is.False, "Editor layout remains read-only.");
                Assert.That(row.GetComponent<RectTransform>().rect.height, Is.EqualTo(64f * GameReadability.Scale));
                foreach (Text text in row.GetComponentsInChildren<Text>())
                {
                    if (string.IsNullOrEmpty(text.text)) continue;
                    Assert.That(text.fontSize, Is.EqualTo(GameReadability.FontSize(22)), text.name);
                    Assert.That(text.font, Is.SameAs(GameTypography.BodyFont));
                    Assert.That(text.resizeTextForBestFit, Is.False);
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f), text.name);
                }
            }
            AssertFirstAndLastRowsReachable(scroll, rows);
            AssertParagraph(Find<Text>(prefix + " Hint"), 18);
        }

        [TestCase(0, 3)]
        [TestCase(1, 2)]
        [TestCase(2, 1)]
        public void KeepsakeArchive_ReservesACompleteScrollableStory(int level, int capacity)
        {
            Create(CoreGameplayPreviewScreen.Warehouse, level);
            Button[] tiles = host.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Warehouse Treasure ", StringComparison.Ordinal) &&
                    !button.name.StartsWith("Warehouse Treasure Page ", StringComparison.Ordinal)).ToArray();
            Assert.That(tiles, Has.Length.EqualTo(capacity));
            Text story = Find<Text>("Warehouse Treasure Story");
            AssertParagraph(story);
            RectTransform viewport = story.GetComponentInParent<ScrollRect>(true).viewport;
            foreach (Button tile in tiles)
                Assert.That(Bounds(Find<RectTransform>("Warehouse Ledger"), tile.GetComponent<RectTransform>()).Overlaps(
                    Bounds(Find<RectTransform>("Warehouse Ledger"), viewport)), Is.False);
            Assert.That(story.text, Is.Not.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Battle_CompleteCardAndDetailsUseReadableScrollWithoutBlockingInput(int level)
        {
            Create(CoreGameplayPreviewScreen.Battle, level);
            Button card = Find<Button>("Battle Card 0");
            Text face = card.transform.Find("Label Viewport/Content/Label").GetComponent<Text>();
            AssertParagraph(face);
            RectTransform bubble = card.transform.Find("Card Keyword Bubble 0").GetComponent<RectTransform>();
            Text body = bubble.Find("Keyword Bubble Text Viewport/Content/Keyword Bubble Text").GetComponent<Text>();
            AssertParagraph(body);
            Assert.That(bubble.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            BattleCardReadabilityScroll relay = card.GetComponent<BattleCardReadabilityScroll>();
            Assert.That(relay, Is.Not.Null);
            Assert.That(relay.Details, Is.SameAs(body.GetComponentInParent<ScrollRect>(true)));
            foreach (Graphic graphic in relay.Details.viewport.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
            Assert.That(body.text, Does.Contain("切换阶段恢复基础费用"));
        }

        private void Create(CoreGameplayPreviewScreen screen, int level)
        {
            GameReadability.OverrideLevel = level;
            host = new GameObject("Card Warehouse Readability Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
        }

        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(component => component.name == name);

        private static void AssertParagraph(Text text, int size = 22)
        {
            Assert.That(text.font, Is.SameAs(GameTypography.BodyFont));
            Assert.That(text.fontSize, Is.EqualTo(GameReadability.FontSize(size)));
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .5f), text.name);
            ScrollRect scroll = text.GetComponentInParent<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content, Is.SameAs(text.transform.parent));
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.content.rect.height, Is.GreaterThanOrEqualTo(scroll.viewport.rect.height));
        }

        private static void AssertFirstAndLastRowsReachable(ScrollRect scroll, Button[] rows)
        {
            Button first = rows.OrderByDescending(row => row.GetComponent<RectTransform>().anchoredPosition.y).First();
            Button last = rows.OrderBy(row => row.GetComponent<RectTransform>().anchoredPosition.y).First();
            AssertInside(scroll.viewport, first.GetComponent<RectTransform>());
            if (scroll.content.rect.height > scroll.viewport.rect.height + .1f)
            {
                Assert.That(scroll.verticalScrollbar.value, Is.EqualTo(1f).Within(.01f));
                scroll.verticalScrollbar.Rebuild(CanvasUpdate.Prelayout);
                Canvas.ForceUpdateCanvases();
                AssertInside(scroll.viewport, first.GetComponent<RectTransform>());
            }
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            AssertInside(scroll.viewport, last.GetComponent<RectTransform>());
        }

        private static void AssertInside(RectTransform parent, RectTransform child)
        {
            Rect rect = Bounds(parent, child);
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - .5f), child.name);
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + .5f), child.name);
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - .5f), child.name);
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + .5f), child.name);
        }

        private static Rect Bounds(RectTransform parent, RectTransform child)
        {
            var points = new Vector3[4]; child.GetWorldCorners(points);
            for (int i = 0; i < points.Length; i++) points[i] = parent.InverseTransformPoint(points[i]);
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }
    }
}
