using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class ExpandedComponentCatalogTests
    {
        [Test]
        public void ExpandedCatalog_AddsOneSidegradePerSlotAndRarityWithSuitabilityTags()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();

            Assert.That(catalog.ExpandedEntries.Count, Is.EqualTo(ComponentArtCatalog.ExpandedEntryCount));
            Assert.That(catalog.ExpandedIsAvailable, Is.True);
            Assert.That(catalog.ExpandedEntries.Select(entry => entry.Id).Distinct().Count(),
                Is.EqualTo(ComponentArtCatalog.ExpandedEntryCount));
            Assert.That(catalog.ExpandedEntries.Select(entry => entry.EnvironmentalEffect).Distinct().Count(),
                Is.GreaterThanOrEqualTo(2));
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
                foreach (ComponentRarity rarity in System.Enum.GetValues(typeof(ComponentRarity)))
                    Assert.That(catalog.ExpandedEntries.Count(entry => entry.Slot == slot && entry.Rarity == rarity),
                        Is.EqualTo(ComponentArtCatalog.EntriesPerRarityPerSlot + 1), slot + " / " + rarity);
        }

        [Test]
        public void EnvironmentalEffect_OnlyActivatesWhenTimeRouteAndWeatherMatch()
        {
            ComponentArtEntry entry = new ComponentArtCatalog().ExpandedEntries.Single(item => item.Id == "sail_dawnkite");
            ComponentContextEffect active = entry.Evaluate(new ComponentVoyageContext(
                entry.BestTime, entry.BestRoute, entry.BestWeather));
            ComponentContextEffect inactive = entry.Evaluate(new ComponentVoyageContext(
                ComponentTimeBand.Night, ComponentRouteType.Trade, ComponentWeatherType.Storm));

            Assert.That(active.IsActive, Is.True);
            Assert.That(active.SpeedBonus, Is.GreaterThan(0f));
            Assert.That(inactive.IsActive, Is.False);
            Assert.That(inactive.SpeedBonus, Is.EqualTo(0f));
        }

        [Test]
        public void VoyageContextEffect_IsClearedWhenRefittingInPort()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            float baseSpeed = session.SpeedKnots;
            session.ConfigureComponentContextEffect(0, 0, 0, 1.5f, 10f, 0.96f);
            Assert.That(session.SpeedKnots, Is.EqualTo(baseSpeed + 1.5f).Within(0.001f));

            session.ConfigureComponentBonuses(0, 0, 0, 0f, 0f);
            Assert.That(session.SpeedKnots, Is.EqualTo(baseSpeed).Within(0.001f));
            Assert.That(session.VisionBonusNm, Is.Zero);
        }
    }
}
