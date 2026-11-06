using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class ComponentExplorationBalanceTests
    {
        [Test]
        public void ExplorationTuning_PreservesStarterStatOffsets()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry sail = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Sail);
            ComponentArtEntry instrument = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Instrument);

            // The first item in each slot is subtracted from every equipped item.
            // Moving these offsets would change every ship, including an unmodified starter.
            Assert.That(sail.Id, Is.EqualTo("sail_patched_lateen"));
            Assert.That(sail.SpeedBonus, Is.EqualTo(0.6f));
            Assert.That(sail.VisionBonus, Is.Zero);
            Assert.That(instrument.Id, Is.EqualTo("instrument_brass_compass"));
            Assert.That(instrument.SpeedBonus, Is.Zero);
            Assert.That(instrument.VisionBonus, Is.EqualTo(20f));
        }

        [TestCase(ComponentRarity.Common, 0.025f, 4f)]
        [TestCase(ComponentRarity.Uncommon, 0.045f, 12f)]
        [TestCase(ComponentRarity.Rare, 0.05f, 15f)]
        [TestCase(ComponentRarity.Epic, 0.06f, 20f)]
        [TestCase(ComponentRarity.Legendary, 0.06f, 25f)]
        public void SailSpecialists_TradeMeaningfulTravelSpeedForVision(
            ComponentRarity rarity, float minimumSpeedAdvantage, float minimumVisionAdvantage)
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry baseline = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Sail);
            ComponentArtEntry[] sails = catalog.ExpandedEntries
                .Where(entry => entry.Slot == ComponentSlot.Sail && entry.Rarity == rarity).ToArray();
            ComponentArtEntry fast = sails.OrderByDescending(entry => entry.SpeedBonus)
                .ThenBy(entry => entry.VisionBonus).First();
            ComponentArtEntry scout = sails.OrderByDescending(entry => entry.VisionBonus)
                .ThenBy(entry => entry.SpeedBonus).First();

            // Compare on the reference Gray Gull with the same full ordinary crew.
            // Terrain-dependent reveal gains are covered by the exploration audit.
            CoreVoyageSession fastShip = SessionFor(new[] { fast }, new[] { baseline });
            CoreVoyageSession scoutShip = SessionFor(new[] { scout }, new[] { baseline });
            Assert.That(fast.Id, Is.Not.EqualTo(scout.Id), rarity.ToString());
            Assert.That(fastShip.EffectiveSpeedKnots / scoutShip.EffectiveSpeedKnots - 1f,
                Is.GreaterThanOrEqualTo(minimumSpeedAdvantage), rarity + " needs a useful travel-time choice.");
            Assert.That(scoutShip.VisionBonusNm - fastShip.VisionBonusNm,
                Is.GreaterThanOrEqualTo(minimumVisionAdvantage), rarity + " needs a useful scouting choice.");
        }

        [TestCase(ComponentRarity.Rare, 30f)]
        [TestCase(ComponentRarity.Epic, 45f)]
        [TestCase(ComponentRarity.Legendary, 65f)]
        public void HighRaritySails_KeepVisionWithinTheirSlotBudget(ComponentRarity rarity, float visionBudget)
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry baseline = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Sail);
            foreach (ComponentArtEntry sail in catalog.ExpandedEntries.Where(entry =>
                entry.Slot == ComponentSlot.Sail && entry.Rarity == rarity))
            {
                Assert.That(sail.VisionBonus - baseline.VisionBonus, Is.LessThanOrEqualTo(visionBudget), sail.Id);
            }
        }

        [Test]
        public void ExtremeExplorationLoadouts_RetainBothSpeedAndScoutingChoices()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentSlot[] slots = (ComponentSlot[])Enum.GetValues(typeof(ComponentSlot));
            ComponentArtEntry[] baselines = slots.Select(slot => catalog.Entries.First(entry => entry.Slot == slot)).ToArray();
            ComponentArtEntry[] fast = slots.Select(slot => catalog.ExpandedEntries.Where(entry => entry.Slot == slot)
                .OrderByDescending(entry => entry.SpeedBonus).ThenByDescending(entry => entry.VisionBonus).First()).ToArray();
            ComponentArtEntry[] scout = slots.Select(slot => catalog.ExpandedEntries.Where(entry => entry.Slot == slot)
                .OrderByDescending(entry => entry.VisionBonus).ThenByDescending(entry => entry.SpeedBonus).First()).ToArray();

            CoreVoyageSession fastShip = SessionFor(fast, baselines);
            CoreVoyageSession scoutShip = SessionFor(scout, baselines);
            Assert.That(fastShip.EffectiveSpeedKnots / scoutShip.EffectiveSpeedKnots - 1f,
                Is.GreaterThanOrEqualTo(0.10f), "Maximum vision must have a meaningful whole-loadout speed cost.");
            Assert.That(scoutShip.VisionBonusNm - fastShip.VisionBonusNm,
                Is.GreaterThanOrEqualTo(60f), "A scouting loadout must still earn a substantial reveal-radius advantage.");
        }

        private static CoreVoyageSession SessionFor(ComponentArtEntry[] equipped, ComponentArtEntry[] baselines)
        {
            CoreVoyageSession session = new CoreVoyageSession();
            session.ConfigureComponentBonuses(
                equipped.Sum(entry => entry.HullBonus) - baselines.Sum(entry => entry.HullBonus),
                equipped.Sum(entry => entry.ArmorBonus) - baselines.Sum(entry => entry.ArmorBonus),
                equipped.Sum(entry => entry.FirepowerBonus) - baselines.Sum(entry => entry.FirepowerBonus),
                equipped.Sum(entry => entry.SpeedBonus) - baselines.Sum(entry => entry.SpeedBonus),
                equipped.Sum(entry => entry.VisionBonus) - baselines.Sum(entry => entry.VisionBonus));
            return session;
        }
    }
}
