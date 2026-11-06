using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class ArrivalComicCatalogTests
    {
        [Test]
        public void FivePorts_MapToTenUniquePagesAlongsideFourOpeningPages()
        {
            Assert.That(ArrivalComicCatalog.All.Select(story => story.PortName),
                Is.EquivalentTo(new[] { "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" }));
            Assert.That(ArrivalComicCatalog.All.Select(story => story.Id), Is.Unique);
            Assert.That(ArrivalComicCatalog.All.SelectMany(story => story.Pages), Is.Unique);
            Assert.That(ArrivalComicCatalog.All.Sum(story => story.Pages.Count), Is.EqualTo(10));
            Assert.That(OpeningComicCatalog.Pages.Count, Is.EqualTo(4));
            Assert.That(OpeningComicCatalog.SecondsPerPage, Is.EqualTo(12f));
            Assert.That(OpeningComicCatalog.Pages.All(page => string.IsNullOrEmpty(page.Narration)), Is.True);
            foreach (ArrivalComicStory story in ArrivalComicCatalog.All)
            {
                Assert.That(ArrivalComicCatalog.Find(story.PortName), Is.SameAs(story));
                Assert.That(ArrivalComicCatalog.FindById(story.Id), Is.SameAs(story));
                Assert.That(story.Pages, Is.EqualTo(new[] { "ArrivalComics/" + story.Id + "_page01", "ArrivalComics/" + story.Id + "_page02" }));
            }
        }

        [TestCase(null)] [TestCase("")] [TestCase("断锚港")] [TestCase("未知港")]
        public void OtherPorts_HaveNoInventedArrivalStory(string port)
        {
            Assert.That(ArrivalComicCatalog.Find(port), Is.Null);
            Assert.That(ArrivalComicCatalog.FindById(port), Is.Null);
        }
    }
}
