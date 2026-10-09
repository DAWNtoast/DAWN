using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Regression coverage for the strategy-deck rail. The rail is a vertical scroll list;
    /// rows stay inside the content grid and the old pager controls stay removed.
    /// </summary>
    public sealed class DeckBuilderScrollLayoutTests
    {
        private GameObject host;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void StrategyDeckUsesBoundedVerticalScrollWithoutPager(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Deck Builder Scroll Layout Test");
            host.SetActive(false);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, port, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Canvas.ForceUpdateCanvases();

            RectTransform rail = Find<RectTransform>("Deck Builder Deck Rail");
            Image deckBacking = Find<Image>("Deck Builder Deck Backing");
            RectTransform pirateFrame = Find<RectTransform>("Deck Builder Pirate Frame");
            RectTransform viewport = Find<RectTransform>("Deck Builder Deck Scroll Viewport");
            RectTransform content = Find<RectTransform>("Deck Builder Deck Scroll Content");
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();

            Assert.That(rail.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(deckBacking.sprite, Is.Not.Null);
            Assert.That(deckBacking.sprite.texture.name, Is.EqualTo("deck_builder_parchment_v2"));
            Assert.That(deckBacking.raycastTarget, Is.False);
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.content, Is.SameAs(content));
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(viewport.transform.IsChildOf(rail), Is.True);
            Assert.That(content.transform.IsChildOf(viewport), Is.True);
            Assert.That(viewport.rect.width, Is.LessThanOrEqualTo(rail.rect.width - 20f));
            Assert.That(viewport.rect.height, Is.LessThan(rail.rect.height - 120f));
            Assert.That(pirateFrame.transform.GetSiblingIndex(), Is.GreaterThan(rail.transform.GetSiblingIndex()),
                "Pirate frame must render above the deck scroll rail/content");

            string[] obsoletePagerNames =
            {
                "Previous Stage Deck Page", "Stage Deck Page", "Next Stage Deck Page",
            };
            foreach (string obsoleteName in obsoletePagerNames)
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == obsoleteName), Is.False, obsoleteName);

            Button[] rows = content.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Remove Deck Card ", StringComparison.Ordinal) ||
                                 button.name.StartsWith("Crew Bonus Deck Card ", StringComparison.Ordinal))
                .ToArray();
            Assert.That(rows.Length, Is.GreaterThan(0));
            foreach (Button row in rows)
            {
                Assert.That(row.transform.IsChildOf(content), Is.True, row.name);
                if (row.name.StartsWith("Remove Deck Card ", StringComparison.Ordinal))
                {
                    RectTransform rowTransform = row.GetComponent<RectTransform>();
                    Assert.That(rowTransform.rect.width, Is.EqualTo(296f).Within(0.01f), row.name + " / width");
                    Assert.That(rowTransform.rect.height, Is.EqualTo(68f * GameReadability.Scale - 8f).Within(0.01f), row.name + " / height");
                }
                Rect bounds = BoundsIn(content, row.GetComponent<RectTransform>());
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(content.rect.xMin - 0.5f), row.name + " / left");
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(content.rect.xMax + 0.5f), row.name + " / right");
                Assert.That(bounds.width, Is.LessThanOrEqualTo(viewport.rect.width + 0.5f), row.name + " / viewport width");
                Assert.That(bounds.height, Is.GreaterThanOrEqualTo(24f), row.name + " / usable height");
            }

            Rect[] orderedRows = rows.Select(row => BoundsIn(content, row.GetComponent<RectTransform>()))
                .OrderByDescending(bounds => bounds.center.y).ToArray();
            for (int index = 0; index < orderedRows.Length; index++)
            {
                Assert.That(orderedRows[index].yMax, Is.LessThanOrEqualTo(content.rect.yMax + 0.5f), "row top / " + index);
                Assert.That(orderedRows[index].yMin, Is.GreaterThanOrEqualTo(content.rect.yMin - 0.5f), "row bottom / " + index);
                if (index > 0)
                    Assert.That(orderedRows[index - 1].yMin - orderedRows[index].yMax, Is.GreaterThanOrEqualTo(4.5f), "row gap / " + index);
            }

            Rect viewportInRail = BoundsIn(rail, viewport);
            Assert.That(viewportInRail.xMin, Is.GreaterThanOrEqualTo(rail.rect.xMin + 8f));
            Assert.That(viewportInRail.xMax, Is.LessThanOrEqualTo(rail.rect.xMax - 8f));
            Assert.That(viewportInRail.yMin, Is.GreaterThanOrEqualTo(rail.rect.yMin + 8f));
            Assert.That(viewportInRail.yMax, Is.LessThanOrEqualTo(rail.rect.yMax - 112f));
            AssertInsidePaintedOpening(viewportInRail, "viewport");
            AssertInsidePaintedOpening(BoundsIn(rail, Find<RectTransform>("Deck Builder Scrollbar")), "scrollbar");
            // With no overflow Unity reports zero for either end of the same view.
            if (content.rect.height > viewport.rect.height + .1f)
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f), "The overflowing initial view starts at the first row.");
            Button firstRow = rows.OrderByDescending(row => BoundsIn(content, row.GetComponent<RectTransform>()).yMax).First();
            AssertInsidePaintedOpening(BoundsIn(rail, firstRow.GetComponent<RectTransform>()), "initial first row");
            AssertInsideViewport(scroll, firstRow.GetComponent<RectTransform>(), "initial first row");
            Assert.That(content.anchoredPosition.y, Is.EqualTo(0f).Within(.1f), "Fresh content remains anchored to the actual viewport top.");
            AssertRowTextColumns(rail, rows);
        }

        [Test]
        public void StrategyDeckRowsRemainAvailableAfterScrollingToTheEnd()
        {
            host = new GameObject("Deck Builder Scroll Interaction Test");
            host.SetActive(false);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            // Use a legal ten-card deck with distinct base cards, plus the preview's
            // actual crew sources, so reaching the final row requires real scrolling.
            int[] distinctCards = controller.Session.GetAvailableBattleCards(NavalBattleStage.Preparation)
                .Distinct().Take(10).ToArray();
            Assert.That(distinctCards.Length, Is.EqualTo(10));
            Assert.That(controller.Session.SetBattleDeck(NavalBattleStage.Preparation, distinctCards), Is.True);
            int crewRows = controller.Session.GetBonusBattleCards(NavalBattleStage.Preparation).Count;
            Assert.That(crewRows, Is.GreaterThan(0), "The fixture includes real preview crew/component source rows.");
            Find<Button>("Battle Deck Stage Tab " + NavalBattleStage.Preparation).onClick.Invoke();
            Canvas.ForceUpdateCanvases();

            ScrollRect scroll = Find<RectTransform>("Deck Builder Deck Scroll Viewport").GetComponent<ScrollRect>();
            RectTransform rail = Find<RectTransform>("Deck Builder Deck Rail");
            RectTransform content = Find<RectTransform>("Deck Builder Deck Scroll Content");
            Button[] rows = content.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Remove Deck Card ", StringComparison.Ordinal) ||
                                 button.name.StartsWith("Crew Bonus Deck Card ", StringComparison.Ordinal))
                .ToArray();
            Assert.That(rows.Length, Is.EqualTo(distinctCards.Length + crewRows), "Every base and source card remains available.");
            Assert.That(content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "This fixture must overflow.");
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f));
            Assert.That(scroll.verticalScrollbar.value, Is.EqualTo(1f).Within(.001f));
            // Match the native Editor prelayout callback which previously emitted
            // the scrollbar's default zero and hid the first row behind the mask.
            scroll.verticalScrollbar.Rebuild(CanvasUpdate.Prelayout);
            Canvas.ForceUpdateCanvases();
            Assert.That(content.anchoredPosition.y, Is.EqualTo(0f).Within(.1f));
            Button firstRow = rows.OrderByDescending(row => BoundsIn(content, row.GetComponent<RectTransform>()).yMax).First();
            Button lastRow = rows.OrderBy(row => BoundsIn(content, row.GetComponent<RectTransform>()).yMin).First();
            AssertInsidePaintedOpening(BoundsIn(rail, firstRow.GetComponent<RectTransform>()), "overflowing initial first row");
            AssertInsideViewport(scroll, firstRow.GetComponent<RectTransform>(), "overflowing initial first row");
            foreach (Text text in firstRow.GetComponentsInChildren<Text>())
                AssertInsideViewport(scroll, text.rectTransform, "initial first row / " + text.name);
            Assert.That(BoundsIn(scroll.viewport, lastRow.GetComponent<RectTransform>()).yMin,
                Is.LessThan(scroll.viewport.rect.yMin), "The last row starts below the visible area.");
            Vector2 beforeScroll = content.anchoredPosition;

            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector2.Distance(content.anchoredPosition, beforeScroll), Is.GreaterThan(1f), "Scrolling must actually move the list.");
            foreach (Button row in rows)
                Assert.That(row.transform.IsChildOf(content), Is.True, row.name);
            AssertInsidePaintedOpening(BoundsIn(rail, lastRow.GetComponent<RectTransform>()), "last row after scrolling");
            AssertInsideViewport(scroll, lastRow.GetComponent<RectTransform>(), "last row after scrolling");
            foreach (Text text in lastRow.GetComponentsInChildren<Text>())
            {
                AssertInsidePaintedOpening(BoundsIn(rail, text.rectTransform), "last row / " + text.name);
                AssertInsideViewport(scroll, text.rectTransform, "last row / " + text.name);
            }
        }

        private static void AssertInsideViewport(ScrollRect scroll, RectTransform child, string context)
        {
            Rect bounds = BoundsIn(scroll.viewport, child);
            Rect visible = scroll.viewport.rect;
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(visible.xMin - .1f), context + " / mask left");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(visible.xMax + .1f), context + " / mask right");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(visible.yMin - .1f), context + " / mask bottom");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(visible.yMax + .1f), context + " / mask top");
        }

        private static void AssertInsidePaintedOpening(Rect bounds, string context)
        {
            // Measured usable paper inside deck_builder_pirate_frame_v1, in rail
            // coordinates. The opaque header/carvings are not usable UI surface.
            Rect paper = Rect.MinMaxRect(-150f, -220f, 150f, 125f);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(paper.xMin - .1f), context + " / painted left");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(paper.xMax + .1f), context + " / painted right");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(paper.yMin - .1f), context + " / painted bottom");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(paper.yMax + .1f), context + " / painted header");
        }

        private static void AssertRowTextColumns(RectTransform rail, Button[] rows)
        {
            Rect scrollbar = BoundsIn(rail, rail.GetComponentsInChildren<RectTransform>(true)
                .Single(rect => rect.name == "Deck Builder Scrollbar"));
            foreach (Button row in rows)
            foreach (Text label in row.GetComponentsInChildren<Text>())
            {
                Rect bounds = BoundsIn(rail, label.rectTransform);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-150f), row.name + " / visible left text edge");
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(scrollbar.xMin - 4f), row.name + " / text clear of frame and scrollbar");
                if (!label.name.EndsWith(" Quantity", StringComparison.Ordinal)) continue;
                Assert.That(label.text, Is.Not.Empty, row.name + " / count or source");
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f), row.name + " / complete count or source");
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), row.name + " / complete count or source");
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
