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
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void AllStageAndUpgradeButtonsRemainEquallySizedInsideParchment(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Deck Builder Header Layout Test");
            host.SetActive(false);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, port, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);

            foreach (NavalBattleStage stage in Stages)
            {
                Find<Button>("Battle Deck Stage Tab " + stage).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                RectTransform paper = Find<RawImage>("Deck Builder Parchment").rectTransform;
                RectTransform header = Find<RectTransform>("Deck Builder Header");
                Assert.That(header.IsChildOf(paper), Is.True);
                Assert.That(header.GetComponentsInChildren<Button>().Length, Is.EqualTo(4));
                string[] names = Stages.Select(value => "Battle Deck Stage Tab " + value)
                    .Concat(new[] { "Open Battle Card Upgrades" }).ToArray();
                Rect previous = default;
                Rect first = default;
                foreach (string name in names)
                {
                    Button button = Find<Button>(name);
                    Rect bounds = BoundsIn(paper, button.GetComponent<RectTransform>());
                    Assert.That(button.transform.IsChildOf(header), Is.True, name);
                    Assert.That(button.interactable, Is.True, name);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(paper.rect.xMin + 48f), name + " / left margin");
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(paper.rect.xMax - 48f), name + " / right margin");
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(paper.rect.yMax - 24f), name + " / top margin");
                    Assert.That(bounds.height, Is.GreaterThanOrEqualTo(40f), name + " / usable height");
                    if (name == names[0]) first = bounds;
                    else
                    {
                        Assert.That(bounds.width, Is.EqualTo(first.width).Within(0.1f), name + " / equal width");
                        Assert.That(bounds.height, Is.EqualTo(first.height).Within(0.1f), name + " / equal height");
                        Assert.That(bounds.center.y, Is.EqualTo(first.center.y).Within(0.1f), name + " / aligned row");
                        Assert.That(bounds.xMin - previous.xMax, Is.GreaterThanOrEqualTo(11.9f), name + " / no overlap");
                    }
                    Text label = button.GetComponentInChildren<Text>();
                    Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(19), name);
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 0.5f), name + " / label width");
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.5f), name + " / label height");
                    Assert.That(label.raycastTarget, Is.False, name + " / unobstructed click");
                    previous = bounds;
                }
                Assert.That(first.xMin + previous.xMax, Is.EqualTo(0f).Within(0.1f), "The complete row must be centered on its parchment.");
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DeckRailAndCollectionCardsStayInsideTheirOwningSurfaces(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Deck Builder Bounds Test");
            host.SetActive(false);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, port, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Canvas.ForceUpdateCanvases();

            RectTransform paper = Find<RawImage>("Deck Builder Parchment").rectTransform;
            RectTransform collection = Find<RectTransform>("Deck Builder Collection");
            Assert.That(collection.GetComponent<RectMask2D>(), Is.Not.Null);
            Button[] collectionCards = host.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Add Deck Card ", System.StringComparison.Ordinal))
                .ToArray();
            Assert.That(collectionCards, Is.Not.Empty);
            foreach (Button card in collectionCards)
            {
                Rect bounds = BoundsIn(paper, card.GetComponent<RectTransform>());
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(paper.rect.xMin + 5f), card.name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(paper.rect.xMax - 5f), card.name);
                Assert.That(card.transform.IsChildOf(collection), Is.True, card.name);
                Rect cardInCollection = BoundsIn(collection, card.GetComponent<RectTransform>());
                Assert.That(cardInCollection.yMin, Is.GreaterThanOrEqualTo(collection.rect.yMin - .1f), card.name);
                Assert.That(cardInCollection.yMax, Is.LessThanOrEqualTo(collection.rect.yMax + .1f), card.name);
                string cardId = card.name.Substring("Add Deck Card ".Length);
                RawImage art = Find<RawImage>("Workshop Card Art " + cardId);
                RectTransform cardRect = card.GetComponent<RectTransform>();
                Rect artBounds = BoundsIn(cardRect, art.rectTransform);
                Rect titleBounds = BoundsIn(cardRect, Find<Text>("Workshop Card Name " + cardId).rectTransform);
                Rect effectBounds = BoundsIn(cardRect, Find<RectTransform>("Workshop Card Description " + cardId + " Viewport"));
                Rect hintBounds = BoundsIn(cardRect, Find<Text>("Workshop Card Add Hint " + cardId).rectTransform);
                Assert.That(artBounds.height, Is.GreaterThanOrEqualTo(48f), "Illustration remains identifiable while the larger body owns the reading area.");
                Assert.That(artBounds.yMin - titleBounds.yMax, Is.GreaterThanOrEqualTo(1.9f), card.name);
                Assert.That(titleBounds.yMin - effectBounds.yMax, Is.GreaterThanOrEqualTo(1.9f), card.name);
                Assert.That(effectBounds.yMin - hintBounds.yMax, Is.GreaterThanOrEqualTo(1.9f), card.name);
                Assert.That(hintBounds.yMin, Is.GreaterThanOrEqualTo(cardRect.rect.yMin), card.name);
                Assert.That(art.raycastTarget, Is.False, card.name);
            }
            Rect collectionOnPaper = BoundsIn(paper, collection);
            Rect headerOnPaper = BoundsIn(paper, Find<RectTransform>("Deck Builder Header"));
            Rect pagerOnPaper = BoundsIn(paper, Find<Button>("Previous Battle Card Collection Page").GetComponent<RectTransform>());
            Assert.That(headerOnPaper.yMin - collectionOnPaper.yMax, Is.GreaterThanOrEqualTo(10f));
            Assert.That(collectionOnPaper.yMin - pagerOnPaper.yMax, Is.GreaterThanOrEqualTo(10f));

            RectTransform rail = Find<RectTransform>("Deck Builder Deck Rail");
            Assert.That(rail.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(rail.rect.width, Is.EqualTo(386f).Within(0.1f));
            Assert.That(rail.rect.height, Is.EqualTo(554f).Within(0.1f));
            foreach (RectTransform child in rail.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == rail || child.GetComponent<RectMask2D>() != null) continue;
                Rect bounds = BoundsIn(rail, child);
                if (child.GetComponent<Button>() != null || child.name == "Deck Builder Deck Title" ||
                    child.name == "Card Workshop Summary")
                {
                    ScrollRect scroll = child.GetComponentInParent<ScrollRect>();
                    if (scroll != null)
                    {
                        Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                        Rect clip = BoundsIn(rail, scroll.viewport);
                        bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, clip.xMin), Mathf.Max(bounds.yMin, clip.yMin),
                            Mathf.Min(bounds.xMax, clip.xMax), Mathf.Min(bounds.yMax, clip.yMax));
                        if (bounds.width <= 0f || bounds.height <= 0f) continue;
                    }
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(rail.rect.xMin - 0.1f), child.name);
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(rail.rect.xMax + 0.1f), child.name);
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(rail.rect.yMin - 0.1f), child.name);
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(rail.rect.yMax + 0.1f), child.name);
                }
            }
            foreach (Button row in rail.GetComponentsInChildren<Button>(true))
            {
                RectTransform rowRect = row.GetComponent<RectTransform>();
                ScrollRect scroll = row.GetComponentInParent<ScrollRect>();
                Rect rowInViewport = BoundsIn(scroll.viewport, rowRect);
                float hidden = scroll.content.rect.height - scroll.viewport.rect.height;
                if (hidden > .1f)
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition -
                        (scroll.viewport.rect.center.y - rowInViewport.center.y) / hidden);
                Canvas.ForceUpdateCanvases();
                rowInViewport = BoundsIn(scroll.viewport, rowRect);
                Assert.That(rowInViewport.yMin, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - .1f), row.name + " scrolls fully into view");
                Assert.That(rowInViewport.yMax, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + .1f), row.name + " scrolls fully into view");
                Text name = row.GetComponentInChildren<Text>();
                Text cost = Find<Text>(row.name + " Cost");
                Text quantity = Find<Text>(row.name + " Quantity");
                Rect nameBounds = BoundsIn(rowRect, name.rectTransform);
                Rect costBounds = BoundsIn(rowRect, cost.rectTransform);
                Rect quantityBounds = BoundsIn(rowRect, quantity.rectTransform);
                Assert.That(nameBounds.xMin - costBounds.xMax, Is.GreaterThanOrEqualTo(.9f), row.name);
                Assert.That(quantityBounds.xMin - nameBounds.xMax, Is.GreaterThanOrEqualTo(3f), row.name);
                Assert.That(costBounds.xMin, Is.GreaterThanOrEqualTo(rowRect.rect.xMin), row.name);
                Assert.That(quantityBounds.xMax, Is.LessThanOrEqualTo(rowRect.rect.xMax), row.name);
                Assert.That(cost.raycastTarget || quantity.raycastTarget, Is.False, row.name + " / click reaches original row");
            }
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
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            foreach (Vector3 corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
