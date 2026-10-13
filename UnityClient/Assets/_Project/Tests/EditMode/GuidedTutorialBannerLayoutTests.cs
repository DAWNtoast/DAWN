using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class GuidedTutorialBannerLayoutTests
    {
        private RectTransform surface;
        private RectTransform paper;
        private GuidedTutorialBannerLayout layout;

        [SetUp]
        public void SetUp()
        {
            surface = new GameObject("Tutorial Design Surface", typeof(RectTransform)).GetComponent<RectTransform>();
            surface.sizeDelta = new Vector2(1920f, 1080f);
            paper = CreateRect("Guided Tutorial Banner", new Vector2(20f, -8f), new Vector2(480f, 160f));
            layout = paper.gameObject.AddComponent<GuidedTutorialBannerLayout>();
        }

        [TearDown]
        public void TearDown()
        {
            if (surface != null) Object.DestroyImmediate(surface.gameObject);
        }

        [Test]
        public void NoFocusConflict_KeepsTopLeftEvenWhenOtherReadoutsAreCovered()
        {
            RectTransform readout = CreateRect("Unrelated Port Readout", new Vector2(25f, -15f), new Vector2(300f, 100f));
            GuidedTutorialFocusGraphic focus = CreateFocus("Continue", new Vector2(1300f, -800f), new Vector2(200f, 60f));
            layout.Configure(surface, new[] { focus });

            layout.Apply();

            Assert.That(paper.anchoredPosition, Is.EqualTo(new Vector2(20f, -8f)));
            Assert.That(BoundsInSurface(paper).Overlaps(BoundsInSurface(readout)), Is.True,
                "Unrelated labels are allowed underneath the one-time teaching paper.");
            AssertReadableAndClear(focus);
        }

        [Test]
        public void LessonReadoutsRemainClearAndAreFollowedAfterRelayout()
        {
            RectTransform route = CreateRect("Navigation Map Title Strip", new Vector2(20f, -150f), new Vector2(1200f, 70f));
            RectTransform header = CreateRect("Voyage Header", Vector2.zero, new Vector2(1920f, 148f));
            GuidedTutorialFocusGraphic focus = CreateFocus("Execute Sail", new Vector2(1600f, -980f), new Vector2(280f, 66f));
            layout.Configure(surface, new[] { focus }, new[] { header, route });
            AssertReadableAndClear(focus);
            Assert.That(BoundsInSurface(paper).Overlaps(BoundsInSurface(route)), Is.False);
            Assert.That(BoundsInSurface(paper).Overlaps(BoundsInSurface(header)), Is.False);

            route.anchoredPosition = paper.anchoredPosition;
            layout.Apply();
            AssertReadableAndClear(focus);
            Assert.That(BoundsInSurface(paper).Overlaps(BoundsInSurface(route)), Is.False);
            Assert.That(BoundsInSurface(paper).Overlaps(BoundsInSurface(header)), Is.False);
        }

        [Test]
        public void TopLeftReturnButton_MovesPaperAndLeavesButtonOutlineAndArrowClear()
        {
            GuidedTutorialFocusGraphic focus = CreateFocus("Return To Port Hub", new Vector2(28f, -24f), new Vector2(240f, 52f));
            Rect originalPaper = BoundsInSurface(paper);
            Assert.That(originalPaper.Overlaps(BoundsInSurface(focus.Target)), Is.True,
                "The fixture must reproduce the covered return button.");
            layout.Configure(surface, new[] { focus });

            layout.Apply();

            Assert.That(paper.anchoredPosition, Is.Not.EqualTo(new Vector2(20f, -8f)));
            AssertReadableAndClear(focus);
            Vector2 settledPosition = paper.anchoredPosition;
            for (int refresh = 0; refresh < 5; refresh++) layout.Apply();
            Assert.That(paper.anchoredPosition, Is.EqualTo(settledPosition), "Repeated layout passes must not make the paper drift.");
        }

        [Test]
        public void LongPaper_MultipleTargetsAndViewportChangesRemainClearAndInsideSurface()
        {
            paper.sizeDelta = new Vector2(480f, 520f);
            GuidedTutorialFocusGraphic first = CreateFocus("Return", new Vector2(28f, -24f), new Vector2(240f, 52f));
            GuidedTutorialFocusGraphic second = CreateFocus("Alternative Action", new Vector2(360f, -170f), new Vector2(100f, 70f));
            layout.Configure(surface, new[] { first, second });

            layout.Apply();
            AssertReadableAndClear(first, second);

            surface.sizeDelta = new Vector2(1180f, 720f);
            layout.Apply();
            AssertReadableAndClear(first, second);

            // A real scrolling/relayout change moves the target into the paper's
            // old position; the next layout pass must use its new world corners.
            Rect previous = BoundsInSurface(paper);
            second.Target.position = surface.TransformPoint(new Vector2(previous.xMin + 30f, previous.yMax - 30f));
            layout.Apply();
            AssertReadableAndClear(first, second);

            paper.sizeDelta = new Vector2(480f, 300f);
            surface.sizeDelta = new Vector2(1440f, 900f);
            layout.Apply();
            AssertReadableAndClear(first, second);
        }

        [TestCase("target")]
        [TestCase("focus")]
        [TestCase("interactable")]
        public void FocusNoLongerActive_RestoresPreferredReadingPosition(string inactiveReason)
        {
            GuidedTutorialFocusGraphic focus = CreateFocus("Return", new Vector2(28f, -24f), new Vector2(240f, 52f));
            layout.Configure(surface, new[] { focus });
            layout.Apply();
            Assert.That(paper.anchoredPosition, Is.Not.EqualTo(new Vector2(20f, -8f)));

            if (inactiveReason == "target") focus.Target.gameObject.SetActive(false);
            else if (inactiveReason == "focus") focus.enabled = false;
            else focus.Target.GetComponent<Button>().interactable = false;
            layout.Apply();

            Assert.That(paper.anchoredPosition, Is.EqualTo(new Vector2(20f, -8f)));
        }

        [Test]
        public void FocusBecomesInteractableAgain_ProtectsTheRestoredAction()
        {
            GuidedTutorialFocusGraphic focus = CreateFocus("Return", new Vector2(28f, -24f), new Vector2(240f, 52f));
            layout.Configure(surface, new[] { focus });
            Vector2 protectedPosition = paper.anchoredPosition;
            focus.Target.GetComponent<Button>().interactable = false;
            layout.Apply();
            Assert.That(paper.anchoredPosition, Is.EqualTo(new Vector2(20f, -8f)));

            focus.Target.GetComponent<Button>().interactable = true;
            layout.Apply();

            Assert.That(paper.anchoredPosition, Is.EqualTo(protectedPosition));
            AssertReadableAndClear(focus);
        }

        [Test]
        public void PaperMovedAfterPlacement_RestoresClearPositionWithUnchangedTargets()
        {
            GuidedTutorialFocusGraphic focus = CreateFocus("Return", new Vector2(28f, -24f), new Vector2(240f, 52f));
            layout.Configure(surface, new[] { focus });
            Vector2 protectedPosition = paper.anchoredPosition;
            paper.anchoredPosition = new Vector2(20f, -8f);
            Assert.That(BoundsInSurface(paper).Overlaps(BoundsInSurface(focus.Target)), Is.True);

            layout.Apply();

            Assert.That(paper.anchoredPosition, Is.EqualTo(protectedPosition));
            AssertReadableAndClear(focus);
        }

        [Test]
        public void ConfigureWithNewPreferredPosition_UsesTheNewReadingPosition()
        {
            layout.Configure(surface, new GuidedTutorialFocusGraphic[0]);
            paper.anchoredPosition = new Vector2(500f, -240f);

            layout.Configure(surface, new GuidedTutorialFocusGraphic[0]);
            layout.Apply();

            Assert.That(paper.anchoredPosition, Is.EqualTo(new Vector2(500f, -240f)));
        }

        [TestCase(28f, -24f, 240f, 52f)]
        [TestCase(500f, -300f, 200f, 60f)]
        [TestCase(500f, -300f, 500f, 80f)]
        [TestCase(28f, -300f, 500f, 80f)]
        [TestCase(1660f, -24f, 230f, 60f)]
        public void ProtectedBounds_ContainActualOutlineAndArrowMesh(float x, float y, float width, float height)
        {
            GuidedTutorialFocusGraphic focus = CreateFocus("Action", new Vector2(x, y), new Vector2(width, height));
            Assert.That(focus.TryGetProtectedRect(surface, out Rect protectedBounds), Is.True);
            AssertContains(protectedBounds, BoundsInSurface(focus.Target), "The complete control must be protected.");
            MethodInfo populate = typeof(GuidedTutorialFocusGraphic).GetMethod("OnPopulateMesh",
                BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(VertexHelper) }, null);
            Assert.That(populate, Is.Not.Null);
            var mesh = new Mesh();
            try
            {
                using (var helper = new VertexHelper())
                {
                    populate.Invoke(focus, new object[] { helper });
                    helper.FillMesh(mesh);
                }
                Assert.That(mesh.vertexCount, Is.GreaterThan(32), "The real annotation must include more than its rectangular outline.");
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector2 point = surface.InverseTransformPoint(focus.rectTransform.TransformPoint(vertex));
                    Assert.That(point.x, Is.InRange(protectedBounds.xMin - .01f, protectedBounds.xMax + .01f),
                        "The protected bounds omit a visible outline/arrow vertex.");
                    Assert.That(point.y, Is.InRange(protectedBounds.yMin - .01f, protectedBounds.yMax + .01f),
                        "The protected bounds omit a visible outline/arrow vertex.");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        private RectTransform CreateRect(string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(surface, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private GuidedTutorialFocusGraphic CreateFocus(string name, Vector2 position, Vector2 size)
        {
            RectTransform target = CreateRect(name, position, size);
            target.gameObject.AddComponent<Image>();
            Button button = target.gameObject.AddComponent<Button>();
            var focus = new GameObject(name + " Red Focus", typeof(RectTransform), typeof(GuidedTutorialFocusGraphic))
                .GetComponent<GuidedTutorialFocusGraphic>();
            focus.transform.SetParent(surface, false);
            focus.rectTransform.anchorMin = Vector2.zero;
            focus.rectTransform.anchorMax = Vector2.one;
            focus.rectTransform.offsetMin = focus.rectTransform.offsetMax = Vector2.zero;
            focus.Configure(button);
            return focus;
        }

        private void AssertReadableAndClear(params GuidedTutorialFocusGraphic[] focuses)
        {
            Rect paperBounds = BoundsInSurface(paper);
            AssertContains(surface.rect, paperBounds, "The complete teaching paper must stay inside the design surface.");
            Assert.That(paper.rect.width, Is.EqualTo(480f).Within(.01f), "Avoidance must preserve the readable paper width.");
            foreach (GuidedTutorialFocusGraphic focus in focuses)
            {
                Assert.That(focus.TryGetProtectedRect(surface, out Rect protectedBounds), Is.True);
                Assert.That(paperBounds.Overlaps(BoundsInSurface(focus.Target)), Is.False, focus.Target.name);
                Assert.That(paperBounds.Overlaps(protectedBounds), Is.False, focus.Target.name + " outline and arrow");
                float horizontalGap = Mathf.Max(protectedBounds.xMin - paperBounds.xMax, paperBounds.xMin - protectedBounds.xMax);
                float verticalGap = Mathf.Max(protectedBounds.yMin - paperBounds.yMax, paperBounds.yMin - protectedBounds.yMax);
                Assert.That(Mathf.Max(horizontalGap, verticalGap), Is.GreaterThanOrEqualTo(11.99f),
                    "The paper needs a visible gap from " + focus.Target.name + " and its annotation.");
            }
        }

        private Rect BoundsInSurface(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = surface.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertContains(Rect outer, Rect inner, string message)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .01f), message);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .01f), message);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .01f), message);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .01f), message);
        }
    }
}
