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
        public void BareStarterHullKeepsItsUnmodifiedPerformanceUntilAPartIsEquipped()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            CoreShipDefinition starter = session.SelectedShip;
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);

            inventory.InitializeEmptyLoadout(0);
            Assert.That(inventory.Items, Is.Empty, "The new-game starter hull has no free component inventory.");
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                Assert.That(inventory.Installed(0, slot), Is.Null, slot.ToString());

            // Empty slots do not contribute a negative baseline delta. The
            // session therefore exposes the authored hull stats unchanged.
            session.ConfigureComponentBonuses(0, 0, 0, 0f, 0f);
            Assert.That(session.MaxHull, Is.EqualTo(starter.MaxHull));
            Assert.That(session.Armor, Is.EqualTo(starter.Armor));
            Assert.That(session.Firepower, Is.EqualTo(starter.Firepower));
            Assert.That(session.SpeedKnots, Is.EqualTo(starter.SpeedKnots));
            Assert.That(session.VisionBonusNm, Is.Zero);

            // Re-running the normal loadout guard must keep the intentionally
            // empty vessel empty instead of regenerating baseline fittings.
            inventory.EnsureLoadout(0);
            Assert.That(inventory.Items, Is.Empty);
            OwnedShipComponentInventory restored = new OwnedShipComponentInventory(catalog);
            Assert.That(restored.Import(inventory.Export(), index => index == 0), Is.True);
            restored.EnsureLoadout(0);
            Assert.That(restored.Items, Is.Empty, "The empty starter marker must survive a save/load cycle.");
        }

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
        public void ReplacingMovesTheOldPhysicalItemToInventoryWithoutLosingIdentity()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            ComponentArtEntry entry = catalog.Entries.First(item => item.Slot == ComponentSlot.Bow && item.Rarity == ComponentRarity.Rare);
            OwnedShipComponent first = inventory.Grant(entry);
            OwnedShipComponent second = inventory.Grant(entry);
            string firstIdentity = first.InstanceId;
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(inventory.TryEquip(0, first.InstanceId), Is.True);
            Assert.That(inventory.TryEquip(0, second.InstanceId), Is.True);
            Assert.That(first.ShipIndex, Is.EqualTo(-1));
            Assert.That(first.InstanceId, Is.EqualTo(firstIdentity));
            Assert.That(first.ComponentId, Is.EqualTo(entry.Id));
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
        public void ReloadReturnsPartsFromUnownedVesselsToInventory()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(1);
            inventory.Grant(catalog.Get(0));
            inventory.Grant(catalog.Get(0));
            OwnedShipComponentInventory restored = new OwnedShipComponentInventory(catalog);
            Assert.That(restored.Import(inventory.Export(), index => index == 0), Is.True);
            Assert.That(restored.Items.All(item => item.ShipIndex == -1), Is.True);
            Assert.That(restored.Items.Count, Is.EqualTo(7));
            Assert.That(restored.Import("not-json", index => true), Is.False);
        }

        [Test]
        public void LegacySlotFactoriesPreserveActualItems()
        {
            OwnedShipComponentInventory inventory = new OwnedShipComponentInventory(catalog);
            ComponentArtEntry rare = catalog.Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            inventory.EnsureLoadout(2, slot => slot == ComponentSlot.Cannon ? rare : null);
            Assert.That(inventory.Installed(2, ComponentSlot.Cannon).ComponentId, Is.EqualTo(rare.Id));
            Assert.That(inventory.Items.Count, Is.EqualTo(ComponentArtCatalog.SlotCount));
            Assert.That(inventory.Installed(2, ComponentSlot.Sail).ComponentId, Is.EqualTo(catalog.Get(ComponentArtCatalog.EntriesPerSlot).Id));
        }

        [Test]
        public void LegacyAppraisalsAreDiscardedWithoutLosingItemsOrEquipmentAssignments()
        {
            ComponentArtEntry entry = catalog.Entries.First(item => item.Slot == ComponentSlot.Cannon && item.Rarity == ComponentRarity.Rare);
            string legacy = "{\"version\":2,\"initializedShips\":[0],\"items\":[" +
                "{\"instanceId\":\"legacy-90\",\"componentId\":\"" + entry.Id + "\",\"quality\":0.9,\"shipIndex\":0}," +
                "{\"instanceId\":\"legacy-110\",\"componentId\":\"" + entry.Id + "\",\"quality\":1.1,\"shipIndex\":-1}]}";
            var inventory = new OwnedShipComponentInventory(catalog);

            Assert.That(inventory.Import(legacy, ship => ship == 0), Is.True);
            inventory.EnsureLoadout(0);
            Assert.That(inventory.Items.Count, Is.EqualTo(2), "Migration must not merge copies or refill empty slots.");
            Assert.That(inventory.Items.All(item => item.ComponentId == entry.Id), Is.True);
            Assert.That(inventory.Installed(0, ComponentSlot.Cannon).InstanceId, Is.EqualTo("legacy-90"));
            Assert.That(inventory.Items.Single(item => item.InstanceId == "legacy-110").ShipIndex, Is.EqualTo(-1));
            Assert.That(inventory.Installed(0, ComponentSlot.Bow), Is.Null);

            string saved = inventory.Export();
            Assert.That(saved, Does.Not.Contain("quality"), "New saves must not retain the retired appraisal field.");
            var restored = new OwnedShipComponentInventory(catalog);
            Assert.That(restored.Import(saved, ship => ship == 0), Is.True);
            Assert.That(restored.Export(), Is.EqualTo(saved));
            Assert.That(restored.TryEquip(0, "legacy-110"), Is.True);
            Assert.That(restored.Items.Single(item => item.InstanceId == "legacy-90").ShipIndex, Is.EqualTo(-1));
            Assert.That(restored.Installed(0, ComponentSlot.Cannon).InstanceId, Is.EqualTo("legacy-110"));
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
