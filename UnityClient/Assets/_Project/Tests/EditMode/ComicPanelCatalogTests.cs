using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ComicPanelCatalogTests
    {
        [TestCase(0, 0f, .5f)]
        [TestCase(1, .5f, .5f)]
        [TestCase(2, 0f, 0f)]
        [TestCase(3, .5f, 0f)]
        public void Quadrants_ReadLeftToRightThenTopToBottom(int index, float x, float y)
        {
            Assert.That(ComicPanelCatalog.QuadPanel(index), Is.EqualTo(new Rect(x, y, .5f, .5f)));
        }

        [TestCase(-1)] [TestCase(4)]
        public void InvalidQuadrant_CannotRepeatOrInventAPage(int index)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ComicPanelCatalog.QuadPanel(index));
        }

        private static IEnumerable<TestCaseData> ComicResources()
        {
            foreach (OpeningComicPage page in OpeningComicCatalog.Pages)
                yield return new TestCaseData(page.ImageResource, 4);
            foreach (ArrivalComicStory story in ArrivalComicCatalog.All)
                foreach (string resource in story.Pages)
                    yield return new TestCaseData(resource, 4);
            foreach (LegendaryFleetComicStory story in LegendaryFleetComicCatalog.All)
            {
                yield return new TestCaseData(story.Before.ImageResource, 4);
                yield return new TestCaseData(story.After.ImageResource, 4);
            }
            for (int sequence = 0; sequence < RamComicCatalog.SequenceCount; sequence++)
                for (int page = 0; page < RamComicCatalog.PagesPerSequence; page++)
                    yield return new TestCaseData(RamComicCatalog.ImageResource(sequence, page), 1);
        }

        [TestCaseSource(nameof(ComicResources))]
        public void EveryShippedComic_HasNonOverlappingReadablePanelBounds(string resource, int expectedCount)
        {
            Texture2D texture = Resources.Load<Texture2D>(resource);
            Assert.That(texture, Is.Not.Null, "Use the actual shipped artwork: " + resource);
            IReadOnlyList<Rect> panels = ComicPanelCatalog.PanelsFor(resource);
            Assert.That(panels.Count, Is.EqualTo(expectedCount), resource);
            for (int index = 0; index < panels.Count; index++)
            {
                Rect panel = panels[index];
                string context = resource + " panel " + index;
                Assert.That(panel.xMin, Is.InRange(0f, 1f), context);
                Assert.That(panel.yMin, Is.InRange(0f, 1f), context);
                Assert.That(panel.xMax, Is.InRange(0f, 1.0001f), context);
                Assert.That(panel.yMax, Is.InRange(0f, 1.0001f), context);
                Assert.That(panel.width * texture.width, Is.GreaterThan(1f), context);
                Assert.That(panel.height * texture.height, Is.GreaterThan(1f), context);
                if (panels.Count > 1) Assert.That(panel.width * panel.height, Is.LessThan(1f), context);
                for (int other = 0; other < index; other++)
                {
                    float width = Mathf.Min(panel.xMax, panels[other].xMax) - Mathf.Max(panel.xMin, panels[other].xMin);
                    float height = Mathf.Min(panel.yMax, panels[other].yMax) - Mathf.Max(panel.yMin, panels[other].yMin);
                    Assert.That(width <= .0001f || height <= .0001f, Is.True,
                        context + " must not reveal content from panel " + other);
                }
            }
            if (expectedCount == 4)
                for (int index = 0; index < panels.Count; index++)
                    Assert.That(panels[index], Is.EqualTo(ComicPanelCatalog.QuadPanel(index)), resource);
            if (expectedCount == 1) Assert.That(panels[0], Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), resource);
        }

    }
}
