using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class CardKeywordTooltipTests
    {
        private GameObject canvasHost, controllerHost;
        private RectTransform viewport, card;
        private Font font;

        [SetUp]
        public void SetUp()
        {
            canvasHost = new GameObject("Keyword Tooltip Test Canvas", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = canvasHost.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            viewport = canvasHost.GetComponent<RectTransform>();
            viewport.sizeDelta = FixedGameViewport.ReferenceSize;
            card = new GameObject("Test Card", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            card.SetParent(viewport, false);
            card.sizeDelta = CardFaceLayout.CardSize;
            font = GameTypography.BodyFont;
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasHost != null) Object.DestroyImmediate(canvasHost);
            if (controllerHost != null) Object.DestroyImmediate(controllerHost);
        }

        [Test]
        public void OrdinaryBlockAndDrawCardCreatesNoTooltip()
        {
            var entries = CoreBattleCardKeywordText.Build(null, "获得 6 点格挡；抽 1 张牌。");
            Assert.That(CardKeywordTooltip.Create(card, "Test Keywords", entries, font), Is.Null);
            Assert.That(card.childCount, Is.Zero);
        }

        [Test]
        public void EntriesShowOnlyKeywordAndMeaningInSeparateNonInteractivePanels()
        {
            var entries = CoreBattleCardKeywordText.Build(null, "获得 6 点格挡；抽 1 张牌；额外装药：5；施加 3 层灼烧。");
            RectTransform tooltip = CardKeywordTooltip.Create(card, "Test Keywords", entries, font);
            Assert.That(tooltip, Is.Not.Null);
            Assert.That(tooltip.gameObject.activeSelf, Is.False);
            tooltip.gameObject.SetActive(true);
            Text[] labels = tooltip.GetComponentsInChildren<Text>();
            CollectionAssert.AreEqual(entries.SelectMany(entry => new[] { entry.Keyword, entry.Meaning }).ToArray(),
                labels.Select(label => label.text).ToArray());
            Assert.That(labels.Select(label => label.text), Does.Contain("额外装药").And.Contain("下一次炮击造成额外伤害"));
            Assert.That(labels.All(label => !label.raycastTarget), Is.True);
            foreach (Text label in labels)
            {
                Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal), label.name + " uses the embedded face's native weight");
                Assert.That(label.font, Is.SameAs(label.name == "Keyword Bubble Title"
                    ? GameTypography.InterfaceFont : GameTypography.BodyFont), label.name);
            }
            CanvasGroup group = tooltip.GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            Image[] panels = tooltip.GetComponentsInChildren<Image>();
            Assert.That(panels.Length, Is.EqualTo(entries.Count));
            for (int index = 0; index < panels.Length; index++)
            {
                Assert.That(panels[index].raycastTarget, Is.False);
                Assert.That(panels[index].color.grayscale, Is.LessThan(.2f));
                Assert.That(panels[index].GetComponent<Outline>(), Is.Not.Null);
                if (index > 0)
                    Assert.That(BoundsIn(panels[index].rectTransform, tooltip).yMax,
                        Is.LessThan(BoundsIn(panels[index - 1].rectTransform, tooltip).yMin));
            }
        }

        [Test]
        public void TooltipRemainsLeftUprightAndReadableAcrossCardTransforms()
        {
            RectTransform tooltip = MakeTooltip();
            tooltip.gameObject.SetActive(true);
            card.anchoredPosition = new Vector2(150f, -100f);
            foreach (float scale in new[] { .55f, 1f, 1.35f })
            {
                card.localScale = Vector3.one * scale;
                card.localRotation = Quaternion.Euler(0f, 0f, -17f);
                tooltip.GetComponent<CardKeywordTooltip>().RefreshPosition();
                Rect tooltipBounds = BoundsIn(tooltip, viewport);
                Rect cardBounds = VisualCardBounds();
                Assert.That(tooltipBounds.xMax, Is.LessThanOrEqualTo(cardBounds.xMin - CardKeywordTooltip.CardGap + .1f));
                Assert.That(tooltipBounds.width, Is.EqualTo(CardKeywordTooltip.Width).Within(.1f));
                Assert.That(Quaternion.Angle(tooltip.rotation, viewport.rotation), Is.LessThan(.1f));
            }
        }

        [Test]
        public void TooltipStaysInsideViewportAtUpperAndLowerEdgesAndRefreshesEntries()
        {
            RectTransform tooltip = MakeTooltip("额外装药：5；施加 3 层灼烧；获得 2 层连锁；法力：3。");
            tooltip.gameObject.SetActive(true);
            CardKeywordTooltip component = tooltip.GetComponent<CardKeywordTooltip>();
            foreach (Vector2 position in new[] { new Vector2(650f, 450f), new Vector2(-650f, -450f) })
            {
                card.anchoredPosition = position;
                component.RefreshPosition();
                Rect bounds = BoundsIn(tooltip, viewport);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(viewport.rect.xMin));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(viewport.rect.xMax));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(viewport.rect.yMin));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(viewport.rect.yMax));
            }
            component.SetEntries(CoreBattleCardKeywordText.Build(null, "额外装药：9。"), font);
            Assert.That(tooltip.GetComponentsInChildren<Text>().Length, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "额外装药", "下一次炮击造成额外伤害" },
                tooltip.GetComponentsInChildren<Text>().Select(text => text.text).ToArray());
            component.SetEntries(CoreBattleCardKeywordText.Build(null, "抽 1 张牌。"), font);
            Assert.That(tooltip.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator PointerHoverShowsTooltipAndExitAndDisableHideIt()
        {
            RectTransform tooltip = MakeTooltip();
            Type hoverType = typeof(CoreGameplayDemoController).Assembly.GetType("ShatteredSail.Presentation.BattleCardHoverFeedback", true);
            var hover = (Behaviour)card.gameObject.AddComponent(hoverType);
            hoverType.GetMethod("Configure").Invoke(hover, new object[] { Color.cyan, tooltip });
            var pointer = new PointerEventData(null);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(tooltip.gameObject.activeInHierarchy, Is.True);
            Assert.That(tooltip.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.9f));
            Assert.That(BoundsIn(tooltip, viewport).xMax, Is.LessThan(VisualCardBounds().xMin));
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(tooltip.gameObject.activeSelf, Is.False);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            hover.enabled = false;
            Assert.That(tooltip.gameObject.activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ProductionFactoryBuildsTheSameGlossaryAndSkipsOrdinaryCards()
        {
            controllerHost = new GameObject("Inactive Tooltip Factory Controller");
            controllerHost.SetActive(false);
            var controller = controllerHost.AddComponent<CoreGameplayDemoController>();
            MethodInfo factory = typeof(CoreGameplayDemoController).GetMethod("CreateCardKeywordBubble", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(factory, Is.Not.Null);
            CoreBattleCard loadShot = CoreVoyageSession.CardLibrary.First(value => value.Id == "card_load_shot");
            var tooltip = (RectTransform)factory.Invoke(controller, new object[] { card, 0, loadShot });
            Assert.That(tooltip, Is.Not.Null);
            CollectionAssert.AreEqual(CoreBattleCardKeywordText.Build(loadShot).SelectMany(entry => new[] { entry.Keyword, entry.Meaning }).ToArray(),
                tooltip.GetComponentsInChildren<Text>(true).Select(text => text.text).ToArray());
            CoreBattleCard brace = CoreVoyageSession.CardLibrary.First(value => value.Id == "card_brace");
            Assert.That(factory.Invoke(controller, new object[] { card, 1, brace }), Is.Null);
        }

        [UnityTest]
        public IEnumerator LeftmostFanCardReservesSpaceForItsTooltipWithoutLosingOriginalPointer()
        {
            viewport.sizeDelta = new Vector2(1280f, 720f);
            var hand = new GameObject("Battle Hand", typeof(RectTransform)).GetComponent<RectTransform>();
            hand.SetParent(viewport, false);
            hand.sizeDelta = CardFaceLayout.HandSize;
            hand.anchoredPosition = new Vector2(0f, -190f);
            card.SetParent(hand, false);
            card.anchoredPosition = new Vector2(-490f, -20f);
            card.GetComponent<Image>().raycastPadding = new Vector4(-37f, -55f, -37f, -55f);
            RectTransform tooltip = MakeTooltip();
            Type hoverType = typeof(CoreGameplayDemoController).Assembly.GetType("ShatteredSail.Presentation.BattleCardHoverFeedback", true);
            var hover = (Behaviour)card.gameObject.AddComponent(hoverType);
            hoverType.GetMethod("Configure").Invoke(hover, new object[] { Color.cyan, tooltip });
            hoverType.GetMethod("SetRestingPose").Invoke(hover, new object[] { .5f, Quaternion.Euler(0f, 0f, -18f), true });
            Vector3 originalPointer = card.TransformPoint(new Vector3(0f, 120f, 0f));
            ExecuteEvents.Execute(card.gameObject, new PointerEventData(null), ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.65f);
            Rect bounds = BoundsIn(tooltip, viewport);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(viewport.rect.xMin + 7f));
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(VisualCardBounds().xMin - CardKeywordTooltip.CardGap + .5f));
            Vector4 padding = card.GetComponent<Image>().raycastPadding;
            Rect hitArea = Rect.MinMaxRect(card.rect.xMin + padding.x, card.rect.yMin + padding.y,
                card.rect.xMax - padding.z, card.rect.yMax - padding.w);
            Assert.That(hitArea.Contains(card.InverseTransformPoint(originalPointer)), Is.True,
                "Making room for the left glossary must retain the original hovered location.");
            LogAssert.NoUnexpectedReceived();
        }

        private RectTransform MakeTooltip(string description = "额外装药：5。") =>
            CardKeywordTooltip.Create(card, "Test Keywords", CoreBattleCardKeywordText.Build(null, description), font);

        private Rect VisualCardBounds()
        {
            Rect frame = CardFaceLayout.VisualBounds;
            Vector2 minimum = Vector2.positiveInfinity, maximum = Vector2.negativeInfinity;
            for (int index = 0; index < 4; index++)
            {
                var point = new Vector3((index & 1) == 0 ? frame.xMin : frame.xMax, (index & 2) == 0 ? frame.yMin : frame.yMax);
                Vector2 corner = viewport.InverseTransformPoint(card.TransformPoint(point));
                minimum = Vector2.Min(minimum, corner);
                maximum = Vector2.Max(maximum, corner);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static Rect BoundsIn(RectTransform target, RectTransform parent)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 minimum = Vector2.positiveInfinity, maximum = Vector2.negativeInfinity;
            foreach (Vector3 world in corners)
            {
                Vector2 point = parent.InverseTransformPoint(world);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }
    }
}
