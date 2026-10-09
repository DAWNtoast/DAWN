using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class ComponentEnvironmentalEffectsTests
    {
        [Test]
        public void ExpandedCatalog_AddsFiveContextAwareSidegradesPerSlot()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();

            Assert.That(catalog.Entries.Count, Is.EqualTo(ComponentArtCatalog.ExpectedEntryCount));
            Assert.That(catalog.ExpandedEntries.Count, Is.EqualTo(ComponentArtCatalog.ExpandedEntryCount));
            Assert.That(catalog.ExpandedEntries.Select(entry => entry.Id).Distinct().Count(),
                Is.EqualTo(ComponentArtCatalog.ExpandedEntryCount));
            Assert.That(catalog.ExpandedIsAvailable, Is.True);
            Assert.That(catalog.ExpandedEntries.Skip(ComponentArtCatalog.ExpectedEntryCount)
                .Select(entry => entry.Slot).Distinct().Count(), Is.EqualTo(ComponentArtCatalog.SlotCount));
        }

        [Test]
        public void MatchingTimeRouteWeather_ProducesAConditionalSidegrade()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry entry = catalog.GetById("bow_freewake_lance");
            ComponentContextEffect none = entry.Evaluate(new ComponentVoyageContext(
                ComponentTimeBand.Night, ComponentRouteType.DeepSea, ComponentWeatherType.Fog));
            ComponentContextEffect match = entry.Evaluate(new ComponentVoyageContext(
                ComponentTimeBand.Dusk, ComponentRouteType.OpenSea, ComponentWeatherType.Storm));

            Assert.That(none.IsActive, Is.False);
            Assert.That(match.IsActive, Is.True);
            Assert.That(match.SpeedBonus, Is.GreaterThan(0f));
            Assert.That(match.ArmorBonus, Is.GreaterThan(0));
        }
    }
}
