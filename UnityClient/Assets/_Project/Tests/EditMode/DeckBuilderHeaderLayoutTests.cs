using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DeckBuilderHeaderLayoutTests
    {
        private static readonly NavalBattleStage[] Stages =
        {
            NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding,
        };
        private GameObject host;

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void AllStageAndSpecialCardButtonsRemainEquallySizedInsideParchment(CoreGameplayPreviewPort port)
        {
            CoreGameplayDemoController controller = CreatePreview(port);
            foreach (NavalBattleStage stage in Stages)
            {
                Find<Button>("Battle Deck Stage Tab " + stage).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                RectTransform paper = Find<RectTransform>("Deck Builder Parchment");
                RectTransform header = Find<RectTransform>("Deck Builder Header");
                Assert.That(header.IsChildOf(paper), Is.True);
                string[] names = Stages.Select(value => "Battle Deck Stage Tab " + value)
                    .Concat(new[] { "Open Special Card Collection" }).ToArray();
                CollectionAssert.AreEquivalent(names, header.GetComponentsInChildren<Button>().Select(button => button.name));
                Rect previous = default;
                Rect first = default;
                foreach (string name in names)
                {
                    Button button = Find<Button>(name);
                    Rect bounds = BoundsIn(paper, button.GetComponent<RectTransform>());
                    Assert.That(button.transform.IsChildOf(header), Is.True, name);
                    Assert.That(button.interactable, Is.True, name);
                    AssertContained(paper.rect, bounds, name);
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(paper.rect.yMax - 20f), name + " leaves a visible paper edge");
                    Assert.That(bounds.height, Is.GreaterThanOrEqualTo(40f), name);
                    if (name == names[0]) first = bounds;
                    else
                    {
                        Assert.That(bounds.width, Is.EqualTo(first.width).Within(.01f), name + " shares the tab width");
                        Assert.That(bounds.height, Is.EqualTo(first.height).Within(.01f), name + " shares the tab height");
                        Assert.That(bounds.center.y, Is.EqualTo(first.center.y).Within(.1f), name);
                        Assert.That(bounds.xMin, Is.GreaterThan(previous.xMax + 8f), name + " keeps distinct hit targets");
                    }
                    Text label = button.GetComponentInChildren<Text>();
                    Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(19), name);
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f), name);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), name);
                    Assert.That(label.raycastTarget, Is.False, name);
                    previous = bounds;
                }
                Assert.That(first.xMin + previous.xMax, Is.EqualTo(0f).Within(.1f));
                Assert.That(controller.Session.GetBattleDeck(stage).Count, Is.InRange(10, 20));
                foreach (bool special in new[] { false, true })
                {
                    Text count = Find<Text>("Deck Builder " + (special ? "Special" : "Base") + " Column Count");
                    int total = special ? controller.Session.GetBonusBattleCards(stage).Count : controller.Session.GetBattleDeck(stage).Count;
                    Assert.That(count.text, Is.EqualTo((special ? "专属牌 " : "基础卡 ") + total + " 张"));
                    Assert.That(count.preferredWidth, Is.LessThanOrEqualTo(count.rectTransform.rect.width + .5f));
                    Assert.That(count.preferredHeight, Is.LessThanOrEqualTo(count.rectTransform.rect.height + .5f));
                }
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DeckRailAndCollectionCardsStayInsideTheirOwningSurfaces(CoreGameplayPreviewPort port)
        {
            CoreGameplayDemoController controller = CreatePreview(port);
            RectTransform panel = Find<RectTransform>("Deck Builder Panel");
            RectTransform paper = Find<RectTransform>("Deck Builder Parchment");
            RectTransform rail = Find<RectTransform>("Deck Builder Deck Rail");
            RectTransform collection = Find<RectTransform>("Deck Builder Collection");
            Assert.That(panel.rect.size, Is.EqualTo(new Vector2(1896f, 948f)));
            Assert.That(paper.rect.size, Is.EqualTo(new Vector2(1140f, 948f)));
            Assert.That(rail.rect.size, Is.EqualTo(new Vector2(744f, 948f)));
            AssertContained(panel.rect, BoundsIn(panel, paper), "left parchment");
            AssertContained(panel.rect, BoundsIn(panel, rail), "right parchment");
            Assert.That(BoundsIn(panel, paper).xMax, Is.LessThan(BoundsIn(panel, rail).xMin));
            Button[] cards = collection.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Add Deck Card ")).ToArray();
            Assert.That(cards, Has.Length.EqualTo(8), "The first collection page uses two rows of four cards.");
            Rect[] frames = cards.Select(card => BoundsIn(collection, card.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform)).ToArray();
            Assert.That(frames.Select(bounds => Mathf.Round(bounds.center.x)).Distinct().Count(), Is.EqualTo(4));
            Assert.That(frames.Select(bounds => Mathf.Round(bounds.center.y)).Distinct().Count(), Is.EqualTo(2));
            foreach (Button card in cards)
            {
                int index = int.Parse(card.name.Substring("Add Deck Card ".Length));
                CoreBattleCard data = controller.Session.GetBattleCard(index);
                Text inventory = Find<Text>("Workshop Card Count " + index);
                int copies = controller.Session.GetBattleDeck(NavalBattleStage.Preparation).Count(value => value == index);
                Assert.That(inventory.text, Is.EqualTo("已加入 " + copies + " / 2 张"));
                Assert.That(inventory.preferredWidth, Is.LessThanOrEqualTo(inventory.rectTransform.rect.width + .5f));
                Assert.That(inventory.preferredHeight, Is.LessThanOrEqualTo(inventory.rectTransform.rect.height + .5f));
                CardSchoolFrameGraphic frame = card.GetComponentInChildren<CardSchoolFrameGraphic>();
                Rect expected = CardSchoolArtCatalog.FrameBounds(data);
                Rect bounds = BoundsIn(collection, frame.rectTransform);
                Assert.That(frame.HasAuthoredTexture, Is.True, card.name);
                Assert.That(frame.mainTexture, Is.SameAs(CardSchoolArtCatalog.Frame(data)), card.name);
                Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(expected.size), card.name);
                Assert.That(bounds.width / bounds.height, Is.EqualTo(expected.width / expected.height).Within(.001f), card.name + " is never stretched");
                AssertContained(collection.rect, bounds, card.name + " complete outer frame");
                AssertContained(paper.rect, BoundsIn(paper, frame.rectTransform), card.name);
                foreach (Button other in cards.Where(other => other != card))
                    Assert.That(bounds.Overlaps(BoundsIn(collection, other.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform)), Is.False, card.name);
                RawImage illustration = card.GetComponentsInChildren<RawImage>(true).First(image => image.texture != null);
                Assert.That(illustration.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.IllustrationLayout(illustration).size));
                Assert.That(illustration.raycastTarget, Is.False);
                Assert.That(card.GetComponentsInChildren<ScrollRect>(true), Is.Empty, "Text and art are a complete card face.");
                Assert.That(card.GetComponent<DeckBuilderCardDrag>(), Is.Not.Null, card.name);
            }
            Rect collectionOnPaper = BoundsIn(paper, collection);
            Rect headerOnPaper = BoundsIn(paper, Find<RectTransform>("Deck Builder Header"));
            Assert.That(collectionOnPaper.yMax, Is.LessThan(headerOnPaper.yMin));
            string[] pagerNames = { "Previous Battle Card Collection Page", "Next Battle Card Collection Page" };
            Rect[] pagers = new Rect[pagerNames.Length];
            for (int side = 0; side < pagerNames.Length; side++)
            {
                Button arrow = Find<Button>(pagerNames[side]);
                Rect bounds = pagers[side] = BoundsIn(paper, arrow.GetComponent<RectTransform>());
                AssertContained(paper.rect, bounds, arrow.name);
                Assert.That(bounds.center.y, Is.EqualTo(0f).Within(.1f), "Paging arrows stay vertically centered on the parchment.");
                WoodenPageArrowGraphic wood = arrow.GetComponent<WoodenPageArrowGraphic>();
                Assert.That(wood, Is.Not.Null, "Paging controls are solid wooden arrow silhouettes.");
                Assert.That(wood.Forward, Is.EqualTo(side == 1));
                Assert.That(arrow.targetGraphic, Is.SameAs(wood));
                Assert.That(wood.mainTexture, Is.SameAs(WoodButtonSkin.Face.texture));
                Assert.That(arrow.GetComponentsInChildren<Image>(true), Is.Empty, "No rectangular backing remains behind the wooden arrow.");
                Assert.That(arrow.GetComponentsInChildren<Text>(true), Is.Empty, "The arrow is a wood silhouette rather than a text glyph.");
                Assert.That(wood.color.a, Is.EqualTo(1f));
                Assert.That(arrow.colors.normalColor.a, Is.EqualTo(1f));
                Assert.That(arrow.colors.highlightedColor.a, Is.EqualTo(1f));
                Assert.That(arrow.colors.pressedColor.a, Is.EqualTo(1f));
                Assert.That(arrow.colors.selectedColor.a, Is.EqualTo(1f));
                Assert.That(arrow.colors.disabledColor.a, Is.EqualTo(1f), "Disabled arrows dim without becoming transparent.");
                foreach (Button card in cards)
                {
                    Assert.That(bounds.Overlaps(BoundsIn(paper, card.GetComponent<RectTransform>())), Is.False, arrow.name + " leaves card interaction clear.");
                    Assert.That(bounds.Overlaps(BoundsIn(paper, card.GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform)), Is.False, arrow.name + " leaves the full card face clear.");
                }
                foreach (Text count in collection.GetComponentsInChildren<Text>(true).Where(item => item.name.StartsWith("Workshop Card Count ")))
                    Assert.That(bounds.Overlaps(BoundsIn(paper, count.rectTransform)), Is.False, arrow.name + " leaves card counts clear.");
            }
            Assert.That(pagers[0].center.x, Is.LessThan(0f));
            Assert.That(pagers[1].center.x, Is.EqualTo(-pagers[0].center.x).Within(.1f), "Paging arrows flank the card collection symmetrically.");
            Assert.That(pagers[1].width, Is.EqualTo(pagers[0].width).Within(.1f));
            Assert.That(pagers[1].height, Is.EqualTo(pagers[0].height).Within(.1f));
            foreach (string name in new[] { "Deck Builder Deck Title", "Deck Builder Base Column", "Deck Builder Special Column", "Deck Builder Status", "Back To Card Workshop Chests" })
                AssertContained(rail.rect, BoundsIn(rail, Find<RectTransform>(name)), name);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Deck Builder Hover Preview"), Is.False,
                "The left collection retains complete cards while the deck uses compact rows.");
        }

        private CoreGameplayDemoController CreatePreview(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Deck Builder Full Card Layout Test");
            host.SetActive(false);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, port, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Canvas.ForceUpdateCanvases();
            return controller;
        }

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
