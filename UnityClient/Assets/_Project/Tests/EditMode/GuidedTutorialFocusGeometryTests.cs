using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class GuidedTutorialFocusGeometryTests
    {
        private RectTransform surface;

        [SetUp]
        public void SetUp()
        {
            surface = NewRect("Teaching Surface", null, Vector2.zero, new Vector2(1920f, 1080f));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(surface.gameObject);

        [TestCase(.7083333f, 0f)]
        [TestCase(1f, -.7f)]
        public void CardFocus_FollowsCompleteFrameWhileRetainingRealButton(float scale, float rotation)
        {
            Button button = NewButton(surface, Vector2.zero, new Vector2(206f, 370f));
            RectTransform frame = NewRect("Card School Frame", button.transform, new Vector2(-1f, -7f), new Vector2(272f, 459f));
            frame.gameObject.AddComponent<CardSchoolFrameGraphic>();
            button.transform.localScale = Vector3.one * scale;
            button.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            GuidedTutorialFocusGraphic focus = NewFocus(button);

            Assert.That(focus.Target, Is.SameAs(button.transform));
            Assert.That(focus.VisualTarget, Is.SameAs(frame));
            Assert.That(focus.TryGetProtectedRect(surface, out Rect protectedBounds), Is.True);
            AssertContains(protectedBounds, Bounds(frame));
            Assert.That(focus.raycastTarget, Is.False);

            frame.sizeDelta += new Vector2(18f, 30f);
            frame.anchoredPosition += new Vector2(30f, 20f);
            Assert.That(focus.TryGetProtectedRect(surface, out protectedBounds), Is.True);
            AssertContains(protectedBounds, Bounds(frame));
        }

        [Test]
        public void RaisedCardFocus_RendersAboveInspectionAndRestoresOrdinaryOrdering()
        {
            surface.gameObject.AddComponent<Canvas>();
            Button button = NewButton(surface, Vector2.zero, new Vector2(206f, 370f));
            Canvas cardCanvas = button.gameObject.AddComponent<Canvas>();
            GuidedTutorialFocusGraphic focus = NewFocus(button);
            cardCanvas.overrideSorting = true;
            cardCanvas.sortingOrder = 30;

            Refresh(focus);

            Canvas focusCanvas = focus.GetComponent<Canvas>();
            Assert.That(focusCanvas, Is.Not.Null);
            Assert.That(focusCanvas.overrideSorting, Is.True);
            Assert.That(focusCanvas.sortingOrder, Is.GreaterThan(cardCanvas.sortingOrder));
            Assert.That(focusCanvas.GetComponent<GraphicRaycaster>(), Is.Null);
            Assert.That(focus.raycastTarget, Is.False);

            cardCanvas.overrideSorting = false;
            Refresh(focus);
            Assert.That(focusCanvas.overrideSorting, Is.False);
        }

        [TestCase(-210f, -1f)]
        [TestCase(210f, 1f)]
        public void CardArrow_PrefersClearOuterSideInsteadOfHullReadout(float x, float directionX)
        {
            RectTransform hand = NewRect("Battle Hand", surface, Vector2.zero, new Vector2(1120f, 340f));
            Button button = NewFramedCard(hand, new Vector2(x, 0f));
            NewFramedCard(hand, Vector2.zero);
            NewRect("Player Battle Hull Bar Track", surface, new Vector2(x, 205f), new Vector2(300f, 30f));
            GuidedTutorialFocusGraphic focus = NewFocus(button);

            Assert.That(FocusGeometry(focus, out _, out Vector2 direction), Is.True);
            Assert.That(direction, Is.EqualTo(new Vector2(directionX, 0f)));
        }

        [Test]
        public void MiddleCardArrow_DoesNotPointAcrossAdjacentFrames()
        {
            RectTransform hand = NewRect("Battle Hand", surface, Vector2.zero, new Vector2(1120f, 340f));
            Button button = NewFramedCard(hand, Vector2.zero);
            NewFramedCard(hand, new Vector2(-206f, 0f));
            NewFramedCard(hand, new Vector2(206f, 0f));
            GuidedTutorialFocusGraphic focus = NewFocus(button);

            Assert.That(FocusGeometry(focus, out _, out Vector2 direction), Is.True);
            Assert.That(direction, Is.EqualTo(Vector2.up));
        }

        [Test]
        public void FullyClippedChoice_HasNoVisibleFocusOrProtectedBounds()
        {
            RectTransform viewport = NewViewport();
            Button button = NewButton(viewport, new Vector2(0f, -140f), new Vector2(300f, 60f));
            GuidedTutorialFocusGraphic focus = NewFocus(button);

            Assert.That(focus.HasVisibleTarget, Is.False);
            Assert.That(focus.TryGetProtectedRect(surface, out _), Is.False);
            Assert.That(FocusBox(focus, out _), Is.False);

            button.transform.localPosition = Vector3.zero;
            Assert.That(focus.HasVisibleTarget, Is.True, "Scrolling a row into view restores its annotation.");
            Assert.That(focus.TryGetProtectedRect(surface, out _), Is.True);
        }

        [Test]
        public void TallerCanvas_ProtectsVisibleCardBelowFixedDesignSurface()
        {
            surface.sizeDelta = new Vector2(1920f, 1200f);
            surface.gameObject.AddComponent<Canvas>();
            Button button = NewButton(surface, new Vector2(0f, -555f), new Vector2(100f, 60f));
            GuidedTutorialFocusGraphic focus = NewFocus(button);
            focus.rectTransform.anchorMin = focus.rectTransform.anchorMax = new Vector2(.5f, .5f);
            focus.rectTransform.sizeDelta = new Vector2(1920f, 1080f);

            Assert.That(focus.HasVisibleTarget, Is.True);
            Assert.That(focus.TryGetProtectedRect(surface, out Rect protectedBounds), Is.True);
            AssertContains(protectedBounds, Bounds(button.transform as RectTransform));
            AssertContains(surface.rect, protectedBounds);
        }

        [Test]
        public void PartlyClippedChoice_FramesOnlyVisiblePortionOfPaddedScaledViewport()
        {
            RectTransform viewport = NewViewport();
            viewport.localScale = new Vector3(.8f, .8f, 1f);
            viewport.GetComponent<RectMask2D>().padding = new Vector4(8f, 6f, 8f, 6f);
            Button button = NewButton(viewport, new Vector2(0f, -70f), new Vector2(300f, 60f));
            GuidedTutorialFocusGraphic focus = NewFocus(button);

            Assert.That(focus.HasVisibleTarget, Is.True);
            Assert.That(FocusBox(focus, out Rect box), Is.True);
            Assert.That(box.yMin, Is.GreaterThan(Bounds(button.transform as RectTransform).yMin));
            AssertContains(Bounds(viewport), box);
            Assert.That(box.yMin, Is.GreaterThanOrEqualTo(-54f * .8f));

            viewport.GetComponent<RectMask2D>().enabled = false;
            Assert.That(FocusBox(focus, out Rect unmasked), Is.True);
            Assert.That(unmasked.yMin, Is.LessThan(box.yMin));
        }

        [Test]
        public void NestedScrollMasks_UseTheirVisibleIntersection()
        {
            RectTransform viewport = NewViewport();
            RectTransform inner = NewRect("Nested Viewport", viewport, Vector2.zero, new Vector2(320f, 240f));
            inner.gameObject.AddComponent<RectMask2D>();
            Button button = NewButton(inner, new Vector2(0f, -100f), new Vector2(300f, 60f));
            GuidedTutorialFocusGraphic focus = NewFocus(button);

            Assert.That(focus.HasVisibleTarget, Is.False, "A larger inner mask does not bypass the outer scroll viewport.");
        }

        private RectTransform NewViewport()
        {
            RectTransform viewport = NewRect("Encounter Decision Viewport", surface, Vector2.zero, new Vector2(320f, 120f));
            viewport.gameObject.AddComponent<RectMask2D>();
            return viewport;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Button NewButton(Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = NewRect("Action", parent, position, size);
            rect.gameObject.AddComponent<Image>();
            return rect.gameObject.AddComponent<Button>();
        }

        private static Button NewFramedCard(Transform parent, Vector2 position)
        {
            Button button = NewButton(parent, position, new Vector2(206f, 370f));
            button.transform.localScale = Vector3.one * .7083333f;
            RectTransform frame = NewRect("Card School Frame", button.transform, Vector2.zero, new Vector2(272f, 459f));
            frame.gameObject.AddComponent<CardSchoolFrameGraphic>();
            return button;
        }

        private GuidedTutorialFocusGraphic NewFocus(Button button)
        {
            RectTransform rect = NewRect("Red Focus", surface, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            GuidedTutorialFocusGraphic focus = rect.gameObject.AddComponent<GuidedTutorialFocusGraphic>();
            focus.Configure(button);
            return focus;
        }

        private Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = surface.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = surface.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void Refresh(GuidedTutorialFocusGraphic focus) => typeof(GuidedTutorialFocusGraphic)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(focus, null);

        private static bool FocusBox(GuidedTutorialFocusGraphic focus, out Rect box)
            => FocusGeometry(focus, out box, out _);

        private static bool FocusGeometry(GuidedTutorialFocusGraphic focus, out Rect box, out Vector2 direction)
        {
            object[] arguments = { default(Rect), default(Vector2) };
            bool visible = (bool)typeof(GuidedTutorialFocusGraphic).GetMethod("TryGetFocusGeometry",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(focus, arguments);
            box = (Rect)arguments[0];
            direction = (Vector2)arguments[1];
            return visible;
        }

        private static void AssertContains(Rect outer, Rect inner)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .01f));
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .01f));
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .01f));
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .01f));
        }
    }
}
