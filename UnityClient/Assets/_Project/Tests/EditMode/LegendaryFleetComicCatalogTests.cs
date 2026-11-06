using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class LegendaryFleetComicCatalogTests
    {
        [Test]
        public void EveryLegendaryEncounterHasExactlyOneMatchingPersonalStory()
        {
            Assert.That(LegendaryFleetComicCatalog.All.Count, Is.EqualTo(6));
            CollectionAssert.AreEquivalent(CoreLegendaryWarships.All.Select(ship => ship.Id),
                LegendaryFleetComicCatalog.All.Select(story => story.EncounterId));
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
            {
                LegendaryFleetComicStory story = LegendaryFleetComicCatalog.Find(ship.Id);
                Assert.That(story, Is.Not.Null, ship.Id);
                Assert.That(story.ShipName, Is.EqualTo(ship.Name), ship.Id);
                Assert.That(LegendaryFleetComicCatalog.Find(story.EncounterId), Is.SameAs(story));
            }
        }

        [Test]
        public void EachShipHasSeparateBeforeAndAfterPagesWithoutSharingAnyResource()
        {
            var resources = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegendaryFleetComicStory story in LegendaryFleetComicCatalog.All)
            {
                Assert.That(story.Before, Is.Not.Null, story.EncounterId);
                Assert.That(story.After, Is.Not.Null, story.EncounterId);
                Assert.That(story.Before, Is.Not.SameAs(story.After), story.EncounterId);
                string key = story.EncounterId.Substring("legendary_pirate_".Length);
                Assert.That(story.Before.ImageResource,
                    Is.EqualTo("LegendaryFleetComics/" + key + "_before"), story.EncounterId);
                Assert.That(story.After.ImageResource,
                    Is.EqualTo("LegendaryFleetComics/" + key + "_after"), story.EncounterId);
                Assert.That(resources.Add(story.Before.ImageResource), Is.True, story.Before.ImageResource);
                Assert.That(resources.Add(story.After.ImageResource), Is.True, story.After.ImageResource);
            }
            Assert.That(resources.Count, Is.EqualTo(12));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("legendary_pirate_unknown")]
        [TestCase("legendary_pirate_iron_oath_extra")]
        [TestCase("treasure_ghost_fleet")]
        [TestCase("boss_route_moon_tide_ruins_arcane")]
        public void UnrelatedEncountersCannotBorrowALegendaryComic(string encounterId)
        {
            Assert.That(LegendaryFleetComicCatalog.Find(encounterId), Is.Null);
        }

        [Test]
        public void WordlessPageExposesOnlyItsImageRatherThanTextToRender()
        {
            CollectionAssert.AreEquivalent(new[] { "ImageResource" },
                typeof(LegendaryFleetComicPage).GetProperties().Select(property => property.Name));
        }
    }
}
