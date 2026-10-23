using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class CollectionCardKeywordHoverTests
    {
        private GameObject canvasHost;
        private RectTransform viewport, face;
        private Button card;

        [SetUp]
        public void SetUp()
        {
            canvasHost = new GameObject("Collection Hover Canvas", typeof(RectTransform), typeof(Canvas));
            canvasHost.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            viewport = canvasHost.GetComponent<RectTransform>();
            viewport.sizeDelta = FixedGameViewport.ReferenceSize;
            card = NewCard("Collection Card", viewport);
            face = (RectTransform)card.transform;
        }

        [TearDown]
        public void TearDown() { if (canvasHost != null) Object.DestroyImmediate(canvasHost); }

        [TestCase(-790f, true)]
        [TestCase(100f, false)]
        [TestCase(740f, false)]
        public void HoverUsesLeftUnlessItsFullWidthDoesNotFit(float x, bool right)
        {
            face.anchoredPosition = new Vector2(x, 0);
            face.localScale = Vector3.one * .76f;
            CollectionCardKeywordHover.Bind(card.gameObject, face, null, "额外装药：5；施加 3 层灼烧。");
            ExecuteEvents.Execute(card.gameObject, new PointerEventData(null), ExecuteEvents.pointerEnterHandler);
            CardKeywordTooltip tooltip = ActiveTooltip();
            Assert.That(tooltip, Is.Not.Null);
            Rect bubble = Bounds((RectTransform)tooltip.transform, viewport);
            Rect frame = VisualBounds(face, viewport);
            if (right) Assert.That(bubble.xMin, Is.GreaterThanOrEqualTo(frame.xMax + CardKeywordTooltip.CardGap - .1f));
            else Assert.That(bubble.xMax, Is.LessThanOrEqualTo(frame.xMin - CardKeywordTooltip.CardGap + .1f));
            Assert.That(bubble.xMin, Is.GreaterThanOrEqualTo(viewport.rect.xMin));
            Assert.That(bubble.xMax, Is.LessThanOrEqualTo(viewport.rect.xMax));
            Assert.That(bubble.width, Is.EqualTo(CardKeywordTooltip.Width).Within(.1f));
        }

        [Test]
        public void HoverShowsOnlyGlossaryAndLeavesPointerClickAndDragRoutingIntact()
        {
            CollectionCardKeywordHover hover = CollectionCardKeywordHover.Bind(card.gameObject, face, null,
                "获得 6 点格挡；抽 1 张牌；额外装药：5。");
            int clicks = 0;
            card.onClick.AddListener(() => clicks++);
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            CardKeywordTooltip tooltip = ActiveTooltip();
            CollectionAssert.AreEqual(new[] { "额外装药", "下一次炮击造成额外伤害" },
                tooltip.GetComponentsInChildren<Text>().Select(label => label.text).ToArray());
            Assert.That(tooltip.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
            Assert.That(tooltip.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(tooltip.GetComponent<GraphicRaycaster>(), Is.Null);
            Assert.That(hover is IDragHandler, Is.False);
            Assert.That(hover is IScrollHandler, Is.False);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(ActiveTooltip(), Is.Null);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(clicks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExitDisableAndPageDestructionClearTheVisibleTooltip()
        {
            var hover = CollectionCardKeywordHover.Bind(card.gameObject, face, null, "额外装药：5。");
            var pointer = new PointerEventData(null);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(ActiveTooltip(), Is.Not.Null);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Assert.That(ActiveTooltip(), Is.Null);
            hover.Show();
            card.gameObject.SetActive(false);
            Assert.That(ActiveTooltip(), Is.Null);
            card.gameObject.SetActive(true);
            hover.Show();
            Object.Destroy(card.gameObject);
            yield return null;
            Assert.That(ActiveTooltip(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ScrollAndActiveDragHideWithoutInstallingInputHandlers()
        {
            var scrollHost = new GameObject("Scrolling Collection", typeof(RectTransform), typeof(ScrollRect));
            scrollHost.transform.SetParent(viewport, false);
            card.transform.SetParent(scrollHost.transform, false);
            var hover = CollectionCardKeywordHover.Bind(card.gameObject, face, null, "额外装药：5。");
            var pointer = new PointerEventData(null);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            scrollHost.GetComponent<ScrollRect>().onValueChanged.Invoke(Vector2.zero);
            Assert.That(ActiveTooltip(), Is.Null);
            hover.Show();
            pointer.dragging = true;
            yield return null;
            Assert.That(ActiveTooltip(), Is.Null);
            ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerMoveHandler);
            Assert.That(ActiveTooltip(), Is.Null);
        }

        [Test]
        public void MovingToAnotherCardReplacesGlossaryAndPlainCardsHaveNone()
        {
            CollectionCardKeywordHover.Bind(card.gameObject, face, null, "额外装药：5。").Show();
            Button second = NewCard("Second", viewport);
            CollectionCardKeywordHover.Bind(second.gameObject, (RectTransform)second.transform, null, "施加 3 层灼烧。").Show();
            Assert.That(viewport.GetComponentsInChildren<CardKeywordTooltip>(), Has.Length.EqualTo(1));
            Assert.That(ActiveTooltip().GetComponentsInChildren<Text>().Select(label => label.text), Does.Contain("灼烧").And.Not.Contain("额外装药"));
            CollectionCardKeywordHover.Bind(second.gameObject, (RectTransform)second.transform, null, "获得 6 点格挡；抽 1 张牌。").Show();
            Assert.That(ActiveTooltip(), Is.Null);
        }

        private CardKeywordTooltip ActiveTooltip() => viewport.GetComponentsInChildren<CardKeywordTooltip>().SingleOrDefault();

        private static Button NewCard(string name, RectTransform parent)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)host.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = CardFaceLayout.CardSize;
            return host.GetComponent<Button>();
        }

        internal static Rect VisualBounds(RectTransform target, RectTransform parent)
        {
            Rect frame = CardFaceLayout.VisualBounds;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                var point = new Vector3((i & 1) == 0 ? frame.xMin : frame.xMax, (i & 2) == 0 ? frame.yMin : frame.yMax);
                Vector2 local = parent.InverseTransformPoint(target.TransformPoint(point));
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        internal static Rect Bounds(RectTransform target, RectTransform parent)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
