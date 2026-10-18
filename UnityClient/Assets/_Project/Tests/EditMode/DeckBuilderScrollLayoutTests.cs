using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DeckBuilderScrollLayoutTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [TestCase("neutral")]
        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void EverySchoolHasAuthoredDeckListArtWithUsableUvAndEnergyPlacement(string school)
        {
            Texture2D texture = DeckListArtCatalog.Texture(school);
            DeckListArtCatalog.Layout layout = DeckListArtCatalog.Bounds(school);
            Assert.That(texture, Is.Not.Null, school + " row texture must be packaged in Resources.");
            Assert.That(layout, Is.Not.Null, school + " must have its own trim and ornament bounds.");
            Assert.That(layout.id, Is.EqualTo(school));
            Assert.That(layout.Uv.width, Is.GreaterThan(0f));
            Assert.That(layout.Uv.height, Is.GreaterThan(0f));
            Assert.That(layout.Uv.xMin, Is.InRange(0f, 1f));
            Assert.That(layout.Uv.yMin, Is.InRange(0f, 1f));
            Assert.That(layout.Uv.xMax, Is.InRange(0f, 1f));
            Assert.That(layout.Uv.yMax, Is.InRange(0f, 1f));
            Assert.That(layout.left, Is.GreaterThan(0f));
            Assert.That(layout.right, Is.GreaterThan(0f));
            Assert.That(layout.left + layout.right, Is.LessThan(1f), "The card name and art retain a nonempty middle section.");
            Assert.That(layout.energyX, Is.InRange(0f, layout.left), "The single energy numeral sits in the left icon.");
            Assert.That(layout.energyY, Is.InRange(0f, 1f));
            Assert.That(DeckListArtCatalog.MapRowX(layout.energyX, layout, 306f), Is.InRange(0f, 54f));
            host = new GameObject("School Deck List Resource Test", typeof(RectTransform));
            DeckListRowGraphic frame = host.AddComponent<DeckListRowGraphic>();
            frame.Configure(school);
            Assert.That(frame.HasAuthoredTexture, Is.True);
            Assert.That(frame.SchoolId, Is.EqualTo(school));
            Assert.That(frame.mainTexture, Is.SameAs(texture));
            Assert.That(frame.raycastTarget, Is.False);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DeckColumnsAggregateDuplicatesAndCountCopiesIndependently(CoreGameplayPreviewPort port)
        {
            CreatePreview(port);
            int[] unique = BasePool().Take(10).ToArray();
            int[] deck = new[] { unique[0], unique[1], unique[0] }.Concat(unique.Skip(2)).ToArray();
            Assert.That(controller.Session.SetBattleDeck(NavalBattleStage.Naval, deck), Is.True);
            RebuildColumns();
            ScrollRect basic = DeckScroll(false);
            ScrollRect special = DeckScroll(true);
            RectTransform rail = Find<RectTransform>("Deck Builder Deck Rail");
            Button[] rows = Cards(false);
            Assert.That(rows, Has.Length.EqualTo(10), "Two copies of a card share one row.");
            CollectionAssert.AreEqual(CatalogOrder(deck), rows.Select(CardIndex),
                "Grouped rows follow the visible card numbers independently of saved deck order.");
            CollectionAssert.AreEqual(deck, controller.Session.GetBattleDeck(NavalBattleStage.Naval),
                "Sorting the display must preserve the deck's copy order.");
            AssertColumnTotal(false, deck.Length);
            int[] sourceCards = controller.Session.GetBonusBattleCards(NavalBattleStage.Naval).ToArray();
            CollectionAssert.AreEqual(CatalogOrder(sourceCards), Cards(true).Select(CardIndex),
                "Source-card rows use the same catalogue order as the base-card column.");
            AssertColumnTotal(true, sourceCards.Length);
            foreach (bool isSpecial in new[] { false, true })
            {
                ScrollRect scroll = DeckScroll(isSpecial);
                RectTransform column = Column(isSpecial);
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(scroll.content.IsChildOf(scroll.viewport), Is.True);
                Assert.That(scroll.viewport.IsChildOf(column), Is.True);
                AssertContained(rail.rect, BoundsIn(rail, column), column.name);
                AssertContained(column.rect, BoundsIn(column, scroll.viewport), scroll.name);
                Assert.That(scroll.vertical, Is.True);
                Assert.That(scroll.horizontal, Is.False);
                Assert.That(scroll.content.rect.width, Is.EqualTo(scroll.viewport.rect.width).Within(.01f));
                foreach (Button row in Cards(isSpecial))
                {
                    int copies = (isSpecial ? sourceCards : deck).Count(index => index == CardIndex(row));
                    AssertCompactRow(row, scroll, copies);
                    Assert.That(row.GetComponent<DeckBuilderCardDrag>() != null, Is.EqualTo(!isSpecial));
                }
            }
            Assert.That(BoundsIn(rail, Column(false)).xMax, Is.LessThan(BoundsIn(rail, Column(true)).xMin));
            Assert.That(basic.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f));
            Assert.That(special.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f));
            foreach (string obsolete in new[] { "Deck Builder Base Filter", "Deck Builder Special Filter",
                "Deck Builder Deck Scroll Viewport", "Previous Stage Deck Page", "Next Stage Deck Page" })
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == obsolete), Is.False, obsolete);
        }

        [TestCase(10, false)]
        [TestCase(11, false)]
        [TestCase(12, false)]
        [TestCase(10, true)]
        [TestCase(11, true)]
        public void TenDifferentCardsFillTheViewportAndOnlyAdditionalKindsScroll(int differentCards, bool special)
        {
            CreatePreview(CoreGameplayPreviewPort.GrayGull);
            if (special)
            {
                CoreSpecialCardDefinition[] sources = CoreSpecialCardCatalog.All.Where(card => card.Card.IsAllowedInStage(NavalBattleStage.Naval))
                    .Take(differentCards).ToArray();
                controller.Session.ConfigureSpecialCardSources(
                    sources.Where(card => card.SourceKind == CoreSpecialCardSourceKind.Crew).Select(card => card.SourceId).ToArray(),
                    sources.Where(card => card.SourceKind == CoreSpecialCardSourceKind.Component).Select(card => card.SourceId).ToArray());
                Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Naval).Distinct().Count(), Is.EqualTo(differentCards));
            }
            else
            {
                int[] deck = BasePool().Take(differentCards).ToArray();
                Assert.That(deck, Has.Length.EqualTo(differentCards), "The fixture uses real unlocked card kinds.");
                Assert.That(controller.Session.SetBattleDeck(NavalBattleStage.Naval, deck), Is.True);
            }
            RebuildColumns();
            ScrollRect scroll = DeckScroll(special);
            Button[] rows = Cards(special);
            DeckListDiamondScrollbar scrollbar = Column(special).GetComponentInChildren<DeckListDiamondScrollbar>(true);
            Assert.That(scrollbar, Is.Not.Null);
            Assert.That(scrollbar.Scroll, Is.SameAs(scroll));
            scrollbar.Refresh();
            Assert.That(scrollbar.HasOverflow, Is.EqualTo(differentCards > 10));
            Assert.That(scrollbar.Handle.gameObject.activeSelf, Is.EqualTo(differentCards > 10));
            Assert.That(scrollbar.Handle.rect.size, Is.EqualTo(new Vector2(18f, 30f)), "The border diamond never stretches into a conventional scrollbar.");
            Assert.That(scroll.content.rect.height, Is.EqualTo(scroll.viewport.rect.height * differentCards / 10f).Within(.01f));
            Rect[] bounds = rows.Select(row => BoundsIn(scroll.content, row.GetComponent<RectTransform>())).ToArray();
            Assert.That(bounds[0].yMax, Is.EqualTo(scroll.content.rect.yMax).Within(.01f), "No unused space above the first row.");
            Assert.That(bounds.Last().yMin, Is.EqualTo(scroll.content.rect.yMin).Within(.01f), "No unused space below the last row.");
            for (int row = 0; row < bounds.Length; row++)
            {
                Assert.That(bounds[row].xMin, Is.EqualTo(scroll.content.rect.xMin).Within(.01f), "Rows meet the left list edge.");
                Assert.That(bounds[row].xMax, Is.EqualTo(scroll.content.rect.xMax).Within(.01f), "Rows meet the right list edge.");
                Assert.That(bounds[row].height, Is.EqualTo(scroll.viewport.rect.height / 10f).Within(.01f));
                if (row > 0) Assert.That(bounds[row].yMax, Is.EqualTo(bounds[row - 1].yMin).Within(.01f), "Rows meet without gaps or overlap.");
            }
            if (differentCards == 10)
                foreach (Button row in rows) AssertContained(scroll.viewport.rect, BoundsIn(scroll.viewport, row.GetComponent<RectTransform>()), row.name);
            else
            {
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                scrollbar.Refresh();
                AssertContained(scroll.viewport.rect, BoundsIn(scroll.viewport, rows.Last().GetComponent<RectTransform>()), "Last row is reachable.");
                Rect thumb = BoundsIn(scrollbar.transform, scrollbar.Handle);
                Rect track = ((RectTransform)scrollbar.transform).rect;
                Assert.That(thumb.yMin, Is.EqualTo(track.yMin).Within(.1f));
            }
        }

        [Test]
        public void BothColumnsPreserveIndependentScrollOffsetsAndSpecialRowsRemainReadOnly()
        {
            CreatePreview(CoreGameplayPreviewPort.GrayGull);
            int[] deck = BasePool().Take(12).ToArray();
            Assert.That(controller.Session.SetBattleDeck(NavalBattleStage.Naval, deck), Is.True);
            controller.Session.ConfigureSpecialCardSources(
                CoreSpecialCardCatalog.All.Where(card => card.SourceKind == CoreSpecialCardSourceKind.Crew).Select(card => card.SourceId).ToArray(),
                CoreSpecialCardCatalog.All.Where(card => card.SourceKind == CoreSpecialCardSourceKind.Component).Select(card => card.SourceId).ToArray());
            RebuildColumns();
            int[] sources = controller.Session.GetBonusBattleCards(NavalBattleStage.Naval).ToArray();
            decimal gold = controller.Session.ProfileGold;
            Assert.That(sources.Distinct().Count(), Is.GreaterThan(10), "Use real source cards to exercise a second overflowing column.");
            DeckScroll(false).verticalNormalizedPosition = .31f;
            DeckScroll(true).verticalNormalizedPosition = .64f;
            Canvas.ForceUpdateCanvases();
            Vector2 baseOffset = DeckScroll(false).content.anchoredPosition;
            Vector2 specialOffset = DeckScroll(true).content.anchoredPosition;
            Button next = Find<Button>("Next Battle Card Collection Page");
            Assert.That(next.interactable, Is.True);
            next.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(DeckScroll(false).content.anchoredPosition.y, Is.EqualTo(baseOffset.y).Within(.1f));
            Assert.That(DeckScroll(true).content.anchoredPosition.y, Is.EqualTo(specialOffset.y).Within(.1f));
            foreach (Button row in Cards(true))
            {
                Assert.That(row.interactable, Is.True);
                Assert.That(row.GetComponent<DeckBuilderCardDrag>(), Is.Null);
                row.onClick.Invoke();
                Assert.That(Find<Text>("Deck Builder Status").text, Does.Contain("不能手动移除"));
            }
            AssertColumnTotal(false, deck.Length);
            AssertColumnTotal(true, sources.Length);
            CollectionAssert.AreEqual(deck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            CollectionAssert.AreEqual(sources, controller.Session.GetBonusBattleCards(NavalBattleStage.Naval));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
        }

        private void AssertCompactRow(Button row, ScrollRect scroll, int copies)
        {
            CoreBattleCard data = controller.Session.GetBattleCard(CardIndex(row));
            Assert.That(row.GetComponentInChildren<CardSchoolFrameGraphic>(), Is.Null, "The deck uses a compact summary, not a shrunken full card.");
            DeckListRowGraphic frame = row.GetComponentInChildren<DeckListRowGraphic>();
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.HasAuthoredTexture, Is.True);
            Assert.That(frame.SchoolId, Is.EqualTo(CardSchoolArtCatalog.NormalizeSchool(data.SchoolId)));
            Assert.That(frame.mainTexture, Is.SameAs(DeckListArtCatalog.Texture(frame.SchoolId)));
            Assert.That(row.GetComponentsInChildren<CardOrnamentGraphic>(true), Is.Empty, "Compact rows do not repeat the full card's resource ornaments.");
            Text[] labels = row.GetComponentsInChildren<Text>(true).Where(label => !string.IsNullOrEmpty(label.text)).ToArray();
            Assert.That(labels, Has.Length.EqualTo(3), "Energy is shown once inside its icon; only the live name and quantity accompany it.");
            Assert.That(labels.Single(label => label.name == row.name + " Energy").text, Is.EqualTo(data.Energy.ToString()));
            Text quantity = labels.Single(label => label.name == row.name + " Quantity");
            Assert.That(quantity.text, Is.EqualTo(copies.ToString()));
            Assert.That(quantity.preferredWidth, Is.LessThanOrEqualTo(quantity.rectTransform.rect.width + .5f), "The complete quantity fits on one line.");
            Assert.That(quantity.preferredHeight, Is.LessThanOrEqualTo(quantity.rectTransform.rect.height + .5f), "The numeral is not clipped on a wrapped second line.");
            Text title = labels.Single(label => label.name == row.name + " Name");
            string fullName = CardFaceLayout.DisplayName(data);
            if (title.text != fullName)
            {
                Assert.That(title.text, Does.EndWith("…"));
                Assert.That(title.text.Length, Is.GreaterThan(1));
                Assert.That(fullName, Does.StartWith(title.text.Substring(0, title.text.Length - 1)));
            }
            Assert.That(title.fontSize, Is.GreaterThanOrEqualTo(12));
            Assert.That(title.preferredWidth, Is.LessThanOrEqualTo(title.rectTransform.rect.width + .5f), "Final text width fits: " + fullName);
            Assert.That(title.preferredHeight, Is.LessThanOrEqualTo(title.rectTransform.rect.height + .5f));
            RawImage art = row.GetComponentsInChildren<RawImage>(true).Single(image => image.name == row.name + " Art");
            Assert.That(art.texture, Is.Not.Null, "Each summary retains card art.");
            Assert.That(art.uvRect.width, Is.GreaterThan(0f));
            Assert.That(art.uvRect.height, Is.GreaterThan(0f));
            Assert.That(art.uvRect.xMin, Is.InRange(0f, 1f));
            Assert.That(art.uvRect.yMin, Is.InRange(0f, 1f));
            Assert.That(art.uvRect.xMax, Is.InRange(0f, 1f));
            Assert.That(art.uvRect.yMax, Is.InRange(0f, 1f));
            AssertContained(scroll.content.rect, BoundsIn(scroll.content, row.GetComponent<RectTransform>()), row.name);
            foreach (Graphic child in row.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic.gameObject != row.gameObject))
                Assert.That(child.raycastTarget, Is.False, "Decorations cannot intercept clicking, scrolling or dragging: " + child.name);
        }

        private void AssertColumnTotal(bool special, int total)
        {
            string expected = (special ? "专属牌 " : "基础卡 ") + total + " 张";
            Assert.That(Find<Text>("Deck Builder " + (special ? "Special" : "Base") + " Column Count").text, Is.EqualTo(expected));
        }

        private static IEnumerable<int> CatalogOrder(IEnumerable<int> cards) => cards.Distinct()
            .OrderBy(index => CoreBattleCardNumbering.Number(CoreVoyageSession.CardLibrary[index]))
            .ThenBy(index => index);

        private int[] BasePool() => controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval)
            .Where(index => !CoreVoyageSession.IsSpecialBattleCard(controller.Session.GetBattleCard(index).Id) &&
                string.IsNullOrEmpty(controller.Session.GetBattleCard(index).RequiredCrew) &&
                string.IsNullOrEmpty(controller.Session.GetBattleCard(index).RequiredComponent)).Distinct().ToArray();

        private void CreatePreview(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Deck Builder Compact Columns Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, port, 1);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Canvas.ForceUpdateCanvases();
        }

        private void RebuildColumns()
        {
            Find<Button>("Battle Deck Stage Tab Naval").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
        }

        private ScrollRect DeckScroll(bool special) => Find<ScrollRect>("Deck Builder " + (special ? "Special" : "Base") + " Scroll Viewport");
        private RectTransform Column(bool special) => Find<RectTransform>("Deck Builder " + (special ? "Special" : "Base") + " Column");
        private Button[] Cards(bool special) => DeckScroll(special).content.GetComponentsInChildren<Button>()
            .Where(button => button.name.StartsWith(special ? "Crew Bonus Deck Card " : "Remove Deck Card ")).ToArray();
        private static int CardIndex(Button button) => int.Parse(button.name.Substring(
            button.name.StartsWith("Remove Deck Card ") ? "Remove Deck Card ".Length : "Crew Bonus Deck Card ".Length).Split(' ')[0]);

        private T Find<T>(string name) where T : Component
        {
            T result = host.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y), local.Max(point => point.x), local.Max(point => point.y));
        }
        private static void AssertContained(Rect outer, Rect inner, string context)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .5f), context + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .5f), context + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .5f), context + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .5f), context + " top");
        }
    }
}
