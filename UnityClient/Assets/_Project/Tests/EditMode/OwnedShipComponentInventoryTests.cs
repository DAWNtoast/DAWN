using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class OwnedShipComponentInventoryTests
    {
        private ComponentArtCatalog catalog;
        [SetUp] public void SetUp() => catalog = new ComponentArtCatalog();

        [Test]
        public void EachVesselStartsWithFiveIndependentRemovableParts()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            inventory.EnsureLoadout(1);
            inventory.EnsureLoadout(1);
            Assert.That(inventory.Items.Count, Is.EqualTo(10));
            Assert.That(inventory.Items.Select(item => item.InstanceId).Distinct().Count(), Is.EqualTo(10));
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
            {
                Assert.That(inventory.Installed(0, slot), Is.Not.SameAs(inventory.Installed(1, slot)));
                Assert.That(inventory.TryUnequip(0, slot), Is.True);
                Assert.That(inventory.Installed(0, slot), Is.Null);
                Assert.That(inventory.Installed(1, slot), Is.Not.Null);
            }
            Assert.That(inventory.Items.Count(item => item.ShipIndex == -1), Is.EqualTo(5));
        }

        [Test]
        public void ReplacingMovesTheOldPhysicalItemToInventoryWithoutLosingQuality()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            ComponentArtEntry entry = catalog.Entries.First(item => item.Slot == ComponentSlot.Bow && item.Rarity == ComponentRarity.Rare);
            OwnedShipComponent first = inventory.Grant(entry, 0.93f);
            OwnedShipComponent second = inventory.Grant(entry, 1.08f);
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(inventory.TryEquip(0, first.InstanceId), Is.True);
            Assert.That(inventory.TryEquip(0, second.InstanceId), Is.True);
            Assert.That(first.ShipIndex, Is.EqualTo(-1));
            Assert.That(first.Quality, Is.EqualTo(0.93f));
            Assert.That(inventory.Installed(0, ComponentSlot.Bow), Is.SameAs(second));
            Assert.That(inventory.TryEquip(0, second.InstanceId), Is.False, "Repeated callbacks cannot duplicate or re-equip the same item.");
            Assert.That(inventory.Items.Count, Is.EqualTo(7));
        }

        [Test]
        public void AnInstalledInstanceCannotBeSharedByTwoVessels()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            inventory.EnsureLoadout(1);
            OwnedShipComponent item = inventory.Installed(0, ComponentSlot.Cannon);
            Assert.That(inventory.TryEquip(1, item.InstanceId), Is.False);
            Assert.That(inventory.TryUnequip(0, ComponentSlot.Cannon), Is.True);
            Assert.That(inventory.TryEquip(1, item.InstanceId), Is.True);
            Assert.That(inventory.Installed(0, ComponentSlot.Cannon), Is.Null);
            Assert.That(inventory.Installed(1, ComponentSlot.Cannon), Is.SameAs(item));
            Assert.That(inventory.TryEquip(2, inventory.Items.First(value => value.ShipIndex == -1).InstanceId), Is.False);
        }

        [Test]
        public void EmptySlotsAndIdentitySurviveReloadWithoutRegeneratingFreeParts()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            OwnedShipComponent removed = inventory.Installed(0, ComponentSlot.Plating);
            inventory.TryUnequip(0, ComponentSlot.Plating);
            string saved = inventory.Export();
            OwnedShipComponentInventory restored = new OwnedShipComponentInventory(catalog);
            Assert.That(restored.Import(saved, index => index == 0), Is.True);
            restored.EnsureLoadout(0);
            Assert.That(restored.Installed(0, ComponentSlot.Plating), Is.Null);
            Assert.That(restored.Items.Count, Is.EqualTo(5));
            Assert.That(restored.Items.Single(item => item.InstanceId == removed.InstanceId).ShipIndex, Is.EqualTo(-1));
            Assert.That(restored.TryUnequip(0, ComponentSlot.Plating), Is.False);
        }

        [Test]
        public void ReloadReturnsPartsFromUnownedVesselsToInventoryAndClampsAppraisal()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(1);
            OwnedShipComponent high = inventory.Grant(catalog.Get(0), float.PositiveInfinity);
            OwnedShipComponent low = inventory.Grant(catalog.Get(0), -5f);
            Assert.That(high.Quality, Is.EqualTo(1f));
            Assert.That(low.Quality, Is.EqualTo(0.9f));
            OwnedShipComponentInventory restored = new OwnedShipComponentInventory(catalog);
            Assert.That(restored.Import(inventory.Export(), index => index == 0), Is.True);
            Assert.That(restored.Items.All(item => item.ShipIndex == -1), Is.True);
            Assert.That(restored.Items.Count, Is.EqualTo(7));
            Assert.That(restored.Import("not-json", index => true), Is.False);
        }

        [Test]
        public void LegacySlotFactoriesPreserveActualItemsAndAppraisedQuality()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            ComponentArtEntry rare = catalog.Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            inventory.EnsureLoadout(2, slot => slot == ComponentSlot.Cannon ? rare : null, slot => 0.94f);
            Assert.That(inventory.Installed(2, ComponentSlot.Cannon).ComponentId, Is.EqualTo(rare.Id));
            Assert.That(inventory.Items.All(item => item.Quality == 0.94f), Is.True);
            Assert.That(inventory.Installed(2, ComponentSlot.Sail).ComponentId, Is.EqualTo(catalog.Get(ComponentArtCatalog.EntriesPerSlot).Id));
        }

        [TestCase("not-json")]
        [TestCase("{\"version\":3,\"items\":[]}")]
        [TestCase("{\"version\":2,\"items\":[null]}")]
        public void FailedImportPreservesExistingItemsAndInitializedShips(string invalid)
        {
            var inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            inventory.TryUnequip(0, ComponentSlot.Cannon);
            string original = inventory.Export();
            Assert.That(inventory.Import(invalid, index => true), Is.False);
            inventory.EnsureLoadout(0);
            Assert.That(inventory.Export(), Is.EqualTo(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnknownOrDuplicateItemRejectsWholeImportWithoutLosingOtherItems(bool duplicate)
        {
            var inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            string original = inventory.Export();
            string invalid = duplicate
                ? original.Replace(inventory.Items[1].InstanceId, inventory.Items[0].InstanceId)
                : original.Replace(inventory.Items[1].ComponentId, "unknown-future-component");
            Assert.That(inventory.Import(invalid, index => true), Is.False);
            Assert.That(inventory.Export(), Is.EqualTo(original));
        }

        [Test]
        public void EquipmentSwapCannotRepairHullByRepeatedlyChangingMaximumHull()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            int originalHull = session.Hull;
            for (int repeat = 0; repeat < 5; repeat++)
            {
                session.ConfigureComponentBonuses(-8, 0, 0, 0f, 0f, true);
                session.ConfigureComponentBonuses(20, 0, 0, 0f, 0f, true);
                Assert.That(session.Hull, Is.LessThanOrEqualTo(originalHull));
            }
        }
    }
}
