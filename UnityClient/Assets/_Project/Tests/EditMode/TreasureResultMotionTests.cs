using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class TreasureResultMotionTests
    {
        [Test]
        public void SkipRestoresExistingTransparency_AndDoesNotAlterTheAuthoritativeMap()
        {
            var parent = new GameObject("Treasure Result Test", typeof(RectTransform));
            try
            {
                RawImage target = Target(parent.transform);
                target.color = new Color(.8f, .9f, 1f, .7f);
                CanvasGroup existing = target.gameObject.AddComponent<CanvasGroup>(); existing.alpha = .65f;
                TreasureResultMotion motion = TreasureResultMotion.PlayCraft(target);
                motion.SetElapsed(.6f);
                Assert.That(existing.alpha, Is.Zero);
                motion.CompleteImmediately(); motion.CompleteImmediately();
                Assert.That(motion.IsComplete, Is.True);
                Assert.That(motion.gameObject.activeSelf, Is.False);
                Assert.That(existing.alpha, Is.EqualTo(.65f));
                Assert.That(target.GetComponent<CanvasGroup>(), Is.SameAs(existing));
                Assert.That(target.color, Is.EqualTo(new Color(.8f, .9f, 1f, .7f)));
                Assert.That(target.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void IdentificationDoesNotHideAnExistingFullBoard_WhenAnotherFragmentIsStored()
        {
            var parent = new GameObject("Treasure Result Test", typeof(RectTransform));
            try
            {
                RawImage target = Target(parent.transform);
                var motion = TreasureResultMotion.PlayIdentification((RectTransform)parent.transform, target,
                    TreasureChartDisplayLayout.For(0), 3, new Vector2(-400f, -220f), "残旧", false);
                Assert.That(target.GetComponent<CanvasGroup>(), Is.Null);
                motion.CompleteImmediately();
                Assert.That(target.color.a, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [TestCase(1280, 720)]
        [TestCase(3840, 2160)]
        public void ProjectedFragmentLandingsRemainWithinTheirCharts_AtSupportedResolutions(int width, int height)
        {
            float scale = Mathf.Min(width / 1920f, height / 1080f);
            for (int board = 0; board < 3; board++)
            {
                TreasureChartDisplayLayout layout = TreasureChartDisplayLayout.For(board);
                for (int piece = 0; piece < 4; piece++)
                {
                    Vector2 pixelPoint = layout.FragmentCenter(piece) * scale;
                    Assert.That(Mathf.Abs(pixelPoint.x), Is.LessThan(layout.Size.x * scale * .5f));
                    Assert.That(Mathf.Abs(pixelPoint.y), Is.LessThan(layout.Size.y * scale * .5f));
                    Assert.That(Mathf.Sign(pixelPoint.y), Is.EqualTo(piece < 2 ? 1f : -1f));
                }
            }
        }

        private static RawImage Target(Transform parent)
        {
            var root = new GameObject("Committed Treasure Chart", typeof(RectTransform), typeof(RawImage));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<RawImage>(); image.rectTransform.sizeDelta = new Vector2(636f, 357.75f);
            return image;
        }
    }
}
