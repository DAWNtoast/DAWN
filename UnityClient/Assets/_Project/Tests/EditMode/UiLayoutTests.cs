using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class UiLayoutTests
    {
        private RectTransform host;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Layout test viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            host.sizeDelta = new Vector2(1920f, 1080f);
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(host.gameObject); }

        [Test]
        public void StretchPreservesParentCoverageAndUsesInsetsWithoutChangingPivot()
        {
            RectTransform child = UiKit.CreateRect("Dialog blocker", host, new Vector2(40f, 20f), Vector2.one);
            child.pivot = new Vector2(.2f, .8f);
            UiLayout.Stretch(child);
            Assert.That(child.rect.size, Is.EqualTo(host.rect.size));
            Assert.That(child.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(child.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(child.pivot, Is.EqualTo(new Vector2(.2f, .8f)));
            UiLayout.Stretch(child, new RectOffset(10, 20, 30, 40));
            Rect bounds = Bounds(child, host);
            Assert.That(bounds.xMin, Is.EqualTo(host.rect.xMin + 10f).Within(.01f));
            Assert.That(bounds.xMax, Is.EqualTo(host.rect.xMax - 20f).Within(.01f));
            Assert.That(bounds.yMin, Is.EqualTo(host.rect.yMin + 40f).Within(.01f));
            Assert.That(bounds.yMax, Is.EqualTo(host.rect.yMax - 30f).Within(.01f));
        }

        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(3440, 1440)]
        [TestCase(1280, 800)]
        public void SafeAreaAndAspectCapStayInsideTheActualViewport(int width, int height)
        {
            // Offset viewports also model a camera that renders to only part of the screen.
            var viewport = new Rect(80f, 40f, width, height);
            var safe = new Rect(200f, 70f, width - 220f, height - 90f);
            Rect anchors = UiLayout.CalculateAnchors(viewport, safe, true, 16f / 9f);
            Rect pixels = ToViewport(viewport, anchors);
            Assert.That(pixels.xMin, Is.GreaterThanOrEqualTo(safe.xMin - .01f));
            Assert.That(pixels.xMax, Is.LessThanOrEqualTo(safe.xMax + .01f));
            Assert.That(pixels.yMin, Is.EqualTo(safe.yMin).Within(.01f));
            Assert.That(pixels.yMax, Is.EqualTo(safe.yMax).Within(.01f));
            Assert.That(pixels.width, Is.EqualTo(Mathf.Min(safe.width, safe.height * 16f / 9f)).Within(.01f));
            Assert.That(pixels.center.x, Is.EqualTo(safe.center.x).Within(.01f));
            Assert.That(UiLayout.CalculateAnchors(viewport, safe, false), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }

        [Test]
        public void InvalidViewportWaitsForResizeAndSafeAreaClampsToParentIntersection()
        {
            Assert.That(UiLayout.CalculateAnchors(new Rect(0f, 0f, 0f, 720f), Rect.zero),
                Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(UiLayout.CalculateAnchors(new Rect(0f, 0f, float.NaN, 720f), Rect.zero),
                Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            var viewport = new Rect(100f, 100f, 800f, 600f);
            Assert.That(UiLayout.CalculateAnchors(viewport, new Rect(0f, 0f, 1000f, 1000f)),
                Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Rect disjoint = UiLayout.CalculateAnchors(viewport, new Rect(1000f, 0f, 100f, 1000f));
            Assert.That(disjoint.width, Is.Zero);
            Assert.That(disjoint.xMax, Is.EqualTo(1f));
        }

        [Test]
        public void EditorPreviewAndWorldSpaceRootsRespondToParentResizeWithoutScreenCoordinates()
        {
            RectTransform content = UiLayout.CreateFullScreen("Content", host, true, 16f / 9f);
            UiLayout layout = content.GetComponent<UiLayout>();
            Assert.That(content.rect.size, Is.EqualTo(host.rect.size));
            host.sizeDelta = new Vector2(3440f, 1440f);
            layout.Refresh();
            Assert.That(content.rect.width, Is.EqualTo(2560f).Within(.01f));
            Assert.That(Bounds(content, host).center, Is.EqualTo(host.rect.center));
            Canvas canvas = host.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            host.sizeDelta = new Vector2(1280f, 800f);
            layout.Refresh();
            Assert.That(content.rect.size, Is.EqualTo(host.rect.size));
            host.sizeDelta = Vector2.zero;
            layout.Refresh();
            host.sizeDelta = new Vector2(1920f, 1080f);
            layout.Refresh();
            Assert.That(content.rect.size, Is.EqualTo(host.rect.size));
        }

        [Test]
        public void CameraCanvasUsesCameraCoordinatesAndMissingCameraFallsBackToParent()
        {
            Canvas canvas = host.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            RectTransform content = UiLayout.CreateFullScreen("Camera content", host, true);
            UiLayout layout = content.GetComponent<UiLayout>();
            Assert.That(content.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(content.anchorMax, Is.EqualTo(Vector2.one));
            var cameraObject = new GameObject("Layout test camera", typeof(Camera));
            RenderTexture captureTarget = null;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.pixelRect = new Rect(20f, 30f, 800f, 500f);
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                layout.Configure(false);
                AssertFullAnchors(content);
                camera.pixelRect = new Rect(60f, 80f, 1000f, 600f);
                Canvas.ForceUpdateCanvases();
                layout.Refresh();
                AssertFullAnchors(content);
                captureTarget = new RenderTexture(768, 3072, 0);
                captureTarget.Create();
                camera.targetTexture = captureTarget;
                // Keep the previous offset screen viewport: it is ignored for a
                // render texture and must not become an artificial safe-area inset.
                Canvas.ForceUpdateCanvases();
                layout.Configure(true);
                AssertFullAnchors(content);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                if (captureTarget != null)
                {
                    captureTarget.Release();
                    Object.DestroyImmediate(captureTarget);
                }
            }
        }

        [TestCase(1280, 720, "two")]
        [TestCase(1920, 1080, "two")]
        [TestCase(3440, 1440, "two")]
        [TestCase(1280, 800, "two")]
        [TestCase(1280, 720, "three")]
        [TestCase(1920, 1080, "three")]
        [TestCase(3440, 1440, "three")]
        [TestCase(1280, 800, "three")]
        [TestCase(1280, 720, "sidebar")]
        [TestCase(1920, 1080, "sidebar")]
        [TestCase(3440, 1440, "sidebar")]
        [TestCase(1280, 800, "sidebar")]
        public void ColumnsFillAvailableSpaceWithoutOverlapAcrossViewportMatrix(int width, int height, string template)
        {
            host.sizeDelta = new Vector2(width, height);
            RectTransform content = UiLayout.CreateFullScreen("Content", host, false, 16f / 9f);
            var padding = new RectOffset(24, 24, 16, 16);
            UiLayout.Columns columns = template == "three"
                ? UiLayout.CreateThreeColumn("Columns", content, 1f, 2f, 1f, 24f, padding)
                : template == "sidebar" ? UiLayout.CreateSidebarDetail("Columns", content, 320f, 24f, padding)
                : UiLayout.CreateTwoColumn("Columns", content, 1f, 2f, 24f, padding);
            LayoutRebuilder.ForceRebuildLayoutImmediate(columns.Root);
            float previousRight = columns.Root.rect.xMin;
            for (int index = 0; index < columns.Regions.Count; index++)
            {
                RectTransform region = columns.Regions[index];
                Rect bounds = Bounds(region, columns.Root);
                Assert.That(bounds.xMin, Is.EqualTo(previousRight + 24f).Within(.1f));
                Assert.That(bounds.yMin, Is.EqualTo(columns.Root.rect.yMin + 16f).Within(.1f));
                Assert.That(bounds.yMax, Is.EqualTo(columns.Root.rect.yMax - 16f).Within(.1f));
                Assert.That(bounds.width, Is.GreaterThan(0f));
                Assert.That(UiId.Find(columns.Root, region.name), Is.SameAs(region));
                previousRight = bounds.xMax;
            }
            Assert.That(previousRight, Is.EqualTo(columns.Root.rect.xMax - 24f).Within(.1f));
            if (template == "sidebar") Assert.That(columns.Regions[0].rect.width, Is.EqualTo(320f).Within(.1f));
            else Assert.That(columns.Regions[1].rect.width, Is.EqualTo(columns.Regions[0].rect.width * 2f).Within(.1f));
        }

        [Test]
        public void InvalidColumnInputsDoNotCreatePartialViews()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UiLayout.CreateTwoColumn("Invalid", host, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiLayout.CreateThreeColumn("Invalid", host, spacing: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiLayout.CreateSidebarDetail("Invalid", host, float.NaN));
            Assert.That(host.childCount, Is.Zero);
        }

        private static Rect ToViewport(Rect viewport, Rect anchors) => new Rect(
            viewport.min + Vector2.Scale(viewport.size, anchors.min), Vector2.Scale(viewport.size, anchors.size));

        private static void AssertFullAnchors(RectTransform rect)
        {
            Assert.That(rect.anchorMin.x, Is.EqualTo(0f).Within(.001f));
            Assert.That(rect.anchorMin.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(rect.anchorMax.x, Is.EqualTo(1f).Within(.001f));
            Assert.That(rect.anchorMax.y, Is.EqualTo(1f).Within(.001f));
        }

        private static Rect Bounds(RectTransform rect, Transform relative)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = relative.InverseTransformPoint(corners[0]);
            Vector3 max = relative.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
