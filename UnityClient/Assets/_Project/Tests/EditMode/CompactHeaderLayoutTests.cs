using NUnit.Framework;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class CompactHeaderLayoutTests
    {
        private const float GeometryTolerance = 0.002f;
        private const float UvTolerance = 0.000002f;

        [Test]
        public void CompactHeader_ReclaimsFortyPixelsWhileTutorialKeepsItsReservedHeaderHeight()
        {
            Assert.That(PhaseSurfaceLayout.HeaderHeight, Is.EqualTo(108f));
            Assert.That(PhaseSurfaceLayout.TutorialHeaderHeight, Is.EqualTo(148f));
            Assert.That(PhaseSurfaceLayout.GetHeaderHeight(false), Is.EqualTo(108f));
            Assert.That(PhaseSurfaceLayout.GetHeaderHeight(true), Is.EqualTo(148f));
            AssertVector(PhaseSurfaceLayout.ContentSize, new Vector2(1920f, 1080f), "reference surface");
            AssertVector(PhaseSurfaceLayout.BackgroundSize, new Vector2(1920f, 972f), "extended background");
            AssertVector(PhaseSurfaceLayout.BackgroundPosition, new Vector2(0f, -54f), "background center");
            Assert.That(PhaseSurfaceLayout.StagePositionY, Is.EqualTo(-55f), "Authored stage positions must not follow the header resize.");

            float backgroundTop = PhaseSurfaceLayout.BackgroundPosition.y + PhaseSurfaceLayout.BackgroundSize.y * .5f;
            float backgroundBottom = PhaseSurfaceLayout.BackgroundPosition.y - PhaseSurfaceLayout.BackgroundSize.y * .5f;
            Assert.That(backgroundBottom, Is.EqualTo(-540f).Within(GeometryTolerance));
            Assert.That(backgroundTop, Is.EqualTo(540f - PhaseSurfaceLayout.GetHeaderHeight(false)).Within(GeometryTolerance));
            float tutorialBottom = 540f - PhaseSurfaceLayout.GetHeaderHeight(true);
            Assert.That(backgroundTop - tutorialBottom, Is.EqualTo(40f).Within(GeometryTolerance),
                "The taller teaching header covers the newly exposed band without moving the stage.");
        }

        [TestCase(1672f, 940f)]
        [TestCase(1672f, 941f)]
        [TestCase(1536f, 1024f)]
        public void BackgroundExtension_PreservesExistingPaintedLandmarksAndTheOldBottomCrop(float width, float height)
        {
            var source = new Vector2(width, height);
            Rect crop = PhaseSurfaceLayout.BackgroundUv(source);
            // Frozen pre-change rendering: centered cover of 1920 x 932 at y=-74.
            // These independent constants are the old painted hotspot contract.
            float oldPixelsPerSourcePixel = 1920f / width;
            float oldVisibleHeight = 932f / (height * oldPixelsPerSourcePixel);
            float oldBottomUv = (1f - oldVisibleHeight) * .5f;
            Assert.That(crop.x, Is.EqualTo(0f).Within(UvTolerance));
            Assert.That(crop.width, Is.EqualTo(1f).Within(UvTolerance));
            Assert.That(crop.yMin, Is.EqualTo(oldBottomUv).Within(UvTolerance), "Expose additional sky above, not a shifted whole painting.");
            Assert.That((crop.height - oldVisibleHeight) * height * oldPixelsPerSourcePixel,
                Is.EqualTo(40f).Within(GeometryTolerance));

            foreach (Vector2 sourceUv in new[] { new Vector2(.17f, .2f), new Vector2(.5f, .5f), new Vector2(.82f, .8f) })
            {
                Vector2 oldContentPoint = new Vector2((sourceUv.x - .5f) * 1920f,
                    (.5f - sourceUv.y) * height * oldPixelsPerSourcePixel - 74f);
                AssertVector(PhaseSurfaceLayout.SourceUvToContent(sourceUv, source), oldContentPoint, "existing painted landmark " + sourceUv);
                AssertVector(PhaseSurfaceLayout.SourcePixelToContent(Vector2.Scale(sourceUv, source), source),
                    oldContentPoint, "pixel-defined painted landmark " + sourceUv);
            }
        }

        [TestCase(1672f, 940f)]
        [TestCase(1672f, 941f)]
        [TestCase(1536f, 1024f)]
        [TestCase(1920f, 992f)]
        [TestCase(4096f, 1024f)]
        public void SourceCoordinates_AgreeWithRenderedCropAndRoundTripOnBothAxes(float width, float height)
        {
            var source = new Vector2(width, height);
            Rect crop = PhaseSurfaceLayout.BackgroundUv(source);
            Vector2 size = PhaseSurfaceLayout.BackgroundSize;
            Vector2 center = PhaseSurfaceLayout.BackgroundPosition;
            foreach (float x in new[] { 0f, .23f, .5f, 1f })
            foreach (float y in new[] { 0f, .31f, .5f, 1f })
            {
                // RawImage samples UV from bottom-left; the layout APIs accept source top-left.
                var sourceTopUv = new Vector2(crop.xMin + x * crop.width, 1f - (crop.yMin + y * crop.height));
                Vector2 expected = center + new Vector2((x - .5f) * size.x, (y - .5f) * size.y);
                AssertVector(PhaseSurfaceLayout.SourceUvToContent(sourceTopUv, source), expected, "rendered UV point");
                AssertVector(PhaseSurfaceLayout.SourcePixelToContent(Vector2.Scale(sourceTopUv, source), source), expected, "rendered source pixel");
                Vector2 restored = PhaseSurfaceLayout.ContentToSourceUv(expected, source);
                Assert.That(restored.x, Is.EqualTo(sourceTopUv.x).Within(UvTolerance), "inverse x");
                Assert.That(restored.y, Is.EqualTo(sourceTopUv.y).Within(UvTolerance), "inverse top-origin y");
            }

            var pixelExtent = new Vector2(113f, 67f);
            var expectedExtent = new Vector2(pixelExtent.x * size.x / (width * crop.width),
                pixelExtent.y * size.y / (height * crop.height));
            AssertVector(PhaseSurfaceLayout.SourcePixelSizeToContent(pixelExtent, source), expectedExtent, "source extent with horizontal and vertical cropping");
            Vector2 origin = new Vector2(.45f * width, .45f * height);
            Vector2 delta = PhaseSurfaceLayout.SourcePixelToContent(origin + pixelExtent, source) -
                PhaseSurfaceLayout.SourcePixelToContent(origin, source);
            AssertVector(delta, new Vector2(expectedExtent.x, -expectedExtent.y), "pixel extent and point mappings agree");
        }

        [TestCase(1672f, 940f)]
        [TestCase(1672f, 941f)]
        [TestCase(1536f, 1024f)]
        [TestCase(1920f, 992f)]
        [TestCase(4096f, 1024f)]
        public void BackgroundCrop_RemainsInsideSourceAndDoesNotDistortAspectWhenExtraSkyIsUnavailable(float width, float height)
        {
            Rect crop = PhaseSurfaceLayout.BackgroundUv(new Vector2(width, height));
            Assert.That(crop.width, Is.GreaterThan(0f));
            Assert.That(crop.height, Is.GreaterThan(0f));
            Assert.That(crop.xMin, Is.GreaterThanOrEqualTo(-UvTolerance));
            Assert.That(crop.yMin, Is.GreaterThanOrEqualTo(-UvTolerance));
            Assert.That(crop.xMax, Is.LessThanOrEqualTo(1f + UvTolerance));
            Assert.That(crop.yMax, Is.LessThanOrEqualTo(1f + UvTolerance));
            float horizontalScale = PhaseSurfaceLayout.BackgroundSize.x / (width * crop.width);
            float verticalScale = PhaseSurfaceLayout.BackgroundSize.y / (height * crop.height);
            Assert.That(horizontalScale, Is.EqualTo(verticalScale).Within(0.00001f), "A source pixel must have the same size in both axes.");
            Assert.That(Mathf.Abs(crop.width - 1f) < UvTolerance || Mathf.Abs(crop.height - 1f) < UvTolerance,
                Is.True, "Cover uses one full source dimension instead of unnecessary zoom.");
        }

        private static void AssertVector(Vector2 actual, Vector2 expected, string context)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThanOrEqualTo(GeometryTolerance), context);
        }
    }
}
