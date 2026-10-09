using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class CardReadabilityCompletionTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private int? previous;

        [SetUp] public void SetUp() => previous = GameReadability.OverrideLevel;
        [TearDown] public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameReadability.OverrideLevel = previous;
        }

        [TestCase(0, 1920)] [TestCase(1, 1920)] [TestCase(2, 1920)]
        [TestCase(0, 1280)] [TestCase(1, 1280)] [TestCase(2, 1280)]
        public void BattleCardEnergyNumbersGenerateCompleteGlyphsAtBothResolutions(int level, int width)
        {
            Create(CoreGameplayPreviewScreen.Battle, level);
            Text[] costs = host.GetComponentsInChildren<Text>(true)
                .Where(text => text.name.StartsWith("Card Energy Cost Text ")).ToArray();
            Assert.That(costs, Is.Not.Empty);
            foreach (Text cost in costs)
            {
                Assert.That(cost.fontSize, Is.EqualTo(GameReadability.FontSize(22)));
                Assert.That(cost.resizeTextForBestFit, Is.False);
                TextGenerationSettings settings = cost.GetGenerationSettings(cost.rectTransform.rect.size);
                settings.scaleFactor = width / 1920f;
                using (var visible = new TextGenerator())
                using (var complete = new TextGenerator())
                {
                    visible.Populate(cost.text, settings);
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    complete.Populate(cost.text, settings);
                    Assert.That(visible.characterCount, Is.EqualTo(complete.characterCount), cost.name + " / " + width);
                    Assert.That(visible.vertexCount, Is.EqualTo(complete.vertexCount), cost.name + " / final digit");
                    Assert.That(visible.vertexCount, Is.GreaterThanOrEqualTo(4), cost.name + " must have a visible digit mesh");
                }
                RectTransform badge = (RectTransform)cost.transform.parent;
                RectTransform card = (RectTransform)badge.parent;
                Rect bounds = Bounds(card, badge);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(card.rect.xMin));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(card.rect.yMax));
                Rect art = Bounds(card, card.Find("Card Art " + cost.name.Substring("Card Energy Cost Text ".Length)) as RectTransform);
                Assert.That(bounds.Overlaps(art), Is.False, cost.name + " leaves the card illustration clear");
            }
        }

        [TestCase(0, -850f)] [TestCase(1, 850f)] [TestCase(2, -850f)] [TestCase(2, 850f)]
        public void HoverDetailsStayInsideCanvasAfterCardLiftScaleAndRotation(int level, float x)
        {
            Create(CoreGameplayPreviewScreen.Battle, level);
            Button card = Find<Button>("Battle Card 0");
            RectTransform box = card.GetComponent<RectTransform>();
            BattleCardReadabilityScroll relay = card.GetComponent<BattleCardReadabilityScroll>();
            RectTransform bubble = (RectTransform)relay.Details.transform.parent;
            bubble.gameObject.SetActive(true);
            box.anchoredPosition = new Vector2(x, -380f);
            box.localScale = Vector3.one * 1.07f;
            box.localRotation = Quaternion.Euler(0f, 0f, -.7f);
            Canvas.ForceUpdateCanvases();
            relay.ClampDetailsToCanvas();
            RectTransform canvas = (RectTransform)card.GetComponentInParent<Canvas>().rootCanvas.transform;
            Rect bounds = Bounds(canvas, bubble);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(canvas.rect.xMin + 15.5f));
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(canvas.rect.xMax - 15.5f));
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(canvas.rect.yMin + 15.5f));
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(canvas.rect.yMax - 15.5f));
            Assert.That(bubble.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void WarehouseModalParagraphsAcceptWheelAndKeepBothEndsReachable(int level)
        {
            Create(CoreGameplayPreviewScreen.Warehouse, level);
            Text count = Find<Text>("Warehouse Collection Count");
            foreach (float pixelScale in new[] { 1f, 1280f / 1920f })
            {
                TextGenerationSettings settings = count.GetGenerationSettings(count.rectTransform.rect.size);
                settings.scaleFactor = pixelScale;
                using (var visible = new TextGenerator())
                using (var complete = new TextGenerator())
                {
                    visible.Populate(count.text, settings);
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    complete.Populate(count.text, settings);
                    Assert.That(visible.characterCount, Is.EqualTo(complete.characterCount), "Collection count retains its last page character at " + pixelScale);
                    Assert.That(visible.vertexCount, Is.EqualTo(complete.vertexCount));
                }
            }
            RectTransform ledger = Find<RectTransform>("Warehouse Ledger");
            Rect countBounds = Bounds(ledger, count.rectTransform);
            Rect tab = Bounds(ledger, Find<Button>("Warehouse Tab Valuables").GetComponent<RectTransform>());
            Assert.That(tab.yMin - countBounds.yMax, Is.GreaterThanOrEqualTo(5f));
            foreach (Button treasure in ledger.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Warehouse Treasure ") && !button.name.StartsWith("Warehouse Treasure Page ")))
                Assert.That(countBounds.yMin - Bounds(ledger, treasure.GetComponent<RectTransform>()).yMax, Is.GreaterThanOrEqualTo(10f));
            controller.ConfigureEditorPreviewWarehouseStorage(1);
            Canvas.ForceUpdateCanvases();
            RectTransform modal = Find<RectTransform>("Warehouse Storage Modal");
            ScrollRect[] paragraphs = modal.GetComponentsInChildren<ScrollRect>(true);
            Assert.That(paragraphs.Length, Is.GreaterThan(0));
            foreach (ScrollRect scroll in paragraphs)
            {
                Assert.That(scroll.viewport.GetComponent<Image>().raycastTarget, Is.True, scroll.name);
                AssertEnds(scroll);
            }
        }

        [TestCase(1)] [TestCase(2)]
        public void TavernLargePagesStackMeasuredParagraphsWithoutOverlap(int level)
        {
            Create(CoreGameplayPreviewScreen.TavernCrewDetail, level);
            foreach (string page in new[] { "Background", "Story", "Skills", "SpecialCards" })
            {
                Find<Button>("DossierPage" + page).onClick.Invoke();
                ScrollRect scroll = Find<RectTransform>("Tavern Dossier " + page + " Page").GetComponent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                AssertEnds(scroll);
                Text[] paragraphs = scroll.content.GetComponentsInChildren<Text>(true);
                Rect? previousBounds = null;
                foreach (Text paragraph in paragraphs)
                {
                    Assert.That(paragraph.resizeTextForBestFit, Is.False);
                    Assert.That(paragraph.preferredHeight, Is.LessThanOrEqualTo(paragraph.rectTransform.rect.height + .5f));
                    Rect bounds = Bounds(scroll.content, paragraph.rectTransform);
                    if (previousBounds.HasValue) Assert.That(previousBounds.Value.yMin - bounds.yMax, Is.GreaterThanOrEqualTo(11.5f), paragraph.name);
                    previousBounds = bounds;
                }
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ShipyardHeadersAndActionsRemainOutsideScrollablePages(int level)
        {
            Create(CoreGameplayPreviewScreen.ShipyardDetail, level);
            foreach (string tab in new[] { "Performance", "Background", "Equipment" })
            {
                Find<Button>("Shipyard Dossier Tab " + tab).onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                RectTransform detail = Find<RectTransform>("Shipyard Detail");
                Button action = Find<Button>("Confirm Shipyard Vessel");
                Assert.That(action.GetComponentInChildren<Text>().fontSize, Is.EqualTo(GameReadability.FontSize(23)));
                Assert.That(action.GetComponentInChildren<Text>().resizeTextForBestFit, Is.False);
                foreach (ScrollRect scroll in detail.GetComponentsInChildren<ScrollRect>(true))
                {
                    AssertEnds(scroll);
                    Assert.That(Bounds(detail, scroll.viewport).Overlaps(Bounds(detail, action.GetComponent<RectTransform>())), Is.False, scroll.name);
                }
            }
        }

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)]
        [TestCase(0, 2)] [TestCase(1, 2)] [TestCase(2, 2)]
        [TestCase(0, 5)] [TestCase(1, 5)] [TestCase(2, 5)]
        public void ShipyardVesselIllustrationLeavesTheCrewCaptionUnobstructed(int level, int vessel)
        {
            Create(CoreGameplayPreviewScreen.ShipyardDetail, level);
            controller.ConfigureEditorShipyard(vessel, true);
            Canvas.ForceUpdateCanvases();
            RectTransform detail = Find<RectTransform>("Shipyard Detail");
            Rect illustration = Bounds(detail, Find<RectTransform>(vessel == 0 ? "Shipyard Dossier Modular Ship" : "Shipyard Dossier Composite Ship " + vessel));
            Rect caption = Bounds(detail, Find<RectTransform>("Shipyard Dossier Illustration Caption Viewport"));
            Rect tabs = Bounds(detail, Find<Button>("Shipyard Dossier Tab Performance").GetComponent<RectTransform>());
            Assert.That(illustration.yMin - caption.yMax, Is.GreaterThanOrEqualTo(5.9f));
            Assert.That(tabs.yMin - illustration.yMax, Is.GreaterThanOrEqualTo(11f));
            Assert.That(Find<Text>("Shipyard Dossier Illustration Caption").text, Does.Contain("基础每日水粮各"));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DeckBuilderHeaderAndCollectionKeepPaperMarginsAtEveryScale(int level)
        {
            Create(CoreGameplayPreviewScreen.DeckBuilder, level);
            RectTransform paper = Find<RectTransform>("Deck Builder Parchment");
            Rect header = Bounds(paper, Find<RectTransform>("Deck Builder Header"));
            Rect collection = Bounds(paper, Find<RectTransform>("Deck Builder Collection"));
            Rect pager = Bounds(paper, Find<Button>("Previous Battle Card Collection Page").GetComponent<RectTransform>());
            Assert.That(paper.rect.yMax - header.yMax, Is.GreaterThanOrEqualTo(23.9f));
            Assert.That(header.yMin - collection.yMax, Is.GreaterThanOrEqualTo(11.9f));
            Assert.That(collection.yMin - pager.yMax, Is.GreaterThanOrEqualTo(9.9f));
            RectTransform rail = Find<RectTransform>("Deck Builder Deck Rail");
            Rect deckTitle = Bounds(rail, Find<RectTransform>("Deck Builder Deck Title"));
            Assert.That(deckTitle.yMax, Is.LessThanOrEqualTo(216.1f), "The heading stays below the pirate crest's painted edge.");
            foreach (Button tab in Find<RectTransform>("Deck Builder Header").GetComponentsInChildren<Button>())
            {
                Text label = tab.GetComponentInChildren<Text>();
                Assert.That(label.resizeTextForBestFit, Is.False);
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f));
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f));
            }
        }

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
        public void ShipyardEquipmentActionsStayVisibleAtEveryScrollPosition(int level, bool fromDeparture)
        {
            Create(fromDeparture ? CoreGameplayPreviewScreen.VoyageDeparture : CoreGameplayPreviewScreen.ShipyardDetail, level);
            if (fromDeparture) Find<Button>("Inspect Departure Vessel").onClick.Invoke();
            else Find<Button>("Shipyard Dossier Tab Equipment").onClick.Invoke();
            RectTransform detail = Find<RectTransform>("Shipyard Detail");
            Button equip = Find<Button>("Shipyard Equip Component");
            Button back = Find<Button>(fromDeparture ? "Back To Voyage Departure" : "Back To Shipyard Gallery");
            Button confirm = Find<Button>("Confirm Shipyard Vessel");
            ScrollRect scroll = Find<RectTransform>("Shipyard Equipment Page").GetComponent<ScrollRect>();
            Assert.That(equip.GetComponentInParent<ScrollRect>(), Is.Null, "The selected action must remain outside the clipped inventory.");
            foreach (float position in new[] { 1f, .5f, 0f })
            {
                scroll.verticalNormalizedPosition = position;
                Canvas.ForceUpdateCanvases();
                Rect equipment = Bounds(detail, equip.GetComponent<RectTransform>());
                Rect returning = Bounds(detail, back.GetComponent<RectTransform>());
                Rect flagship = Bounds(detail, confirm.GetComponent<RectTransform>());
                Assert.That(equipment.yMin, Is.GreaterThan(-365f));
                Assert.That(equipment.xMin, Is.GreaterThan(-736f));
                Assert.That(equipment.yMax, Is.LessThan(Bounds(detail, scroll.viewport).yMin - 12f));
                Assert.That(returning.xMin - equipment.xMax, Is.GreaterThan(20f));
                Assert.That(flagship.xMin - returning.xMax, Is.GreaterThan(20f));
                Assert.That(equipment.Overlaps(flagship), Is.False);
            }
            Assert.That(equip.GetComponentInChildren<Text>().fontSize, Is.EqualTo(GameReadability.FontSize(22)));
            Assert.That(equip.GetComponentInChildren<Text>().preferredHeight,
                Is.LessThanOrEqualTo(equip.GetComponentInChildren<Text>().rectTransform.rect.height + .5f));
        }

        private void Create(CoreGameplayPreviewScreen screen, int level)
        {
            GameReadability.OverrideLevel = level;
            host = new GameObject("Card Readability Completion Test"); host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true); Canvas.ForceUpdateCanvases();
            foreach (Scrollbar bar in host.GetComponentsInChildren<Scrollbar>(true))
            {
                RectTransform track = bar.GetComponent<RectTransform>();
                Rect thumb = Bounds(track, bar.handleRect);
                Assert.That(thumb.width, Is.InRange(3f, track.rect.width + .1f), bar.name + " has a visible thumb within its narrow track");
                Assert.That(thumb.xMin, Is.GreaterThanOrEqualTo(track.rect.xMin - .1f), bar.name);
                Assert.That(thumb.xMax, Is.LessThanOrEqualTo(track.rect.xMax + .1f), bar.name);
            }
        }

        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(component => component.name == name);

        private static void AssertEnds(ScrollRect scroll)
        {
            Assert.That(scroll.content.anchorMin, Is.EqualTo(new Vector2(.5f, 1f)));
            scroll.verticalNormalizedPosition = 1f; Canvas.ForceUpdateCanvases();
            Rect top = Bounds(scroll.viewport, scroll.content);
            Assert.That(top.yMax, Is.EqualTo(scroll.viewport.rect.yMax).Within(.5f), scroll.name + " starts at the first line");
            scroll.verticalNormalizedPosition = 0f; Canvas.ForceUpdateCanvases();
            Rect bottom = Bounds(scroll.viewport, scroll.content);
            Assert.That(bottom.yMin, Is.EqualTo(scroll.viewport.rect.yMin).Within(.5f), scroll.name + " reaches the final line");
        }

        private static Rect Bounds(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++) corners[i] = parent.InverseTransformPoint(corners[i]);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y), corners.Max(point => point.x), corners.Max(point => point.y));
        }
    }
}
