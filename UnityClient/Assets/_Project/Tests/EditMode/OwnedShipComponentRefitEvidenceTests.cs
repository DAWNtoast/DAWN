using System.Linq;
using NUnit.Framework;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class OwnedShipComponentRefitEvidenceTests
    {
        [Test]
        public void AutomaticFittingsAndReinstallingThemDoNotProveARefit()
        {
            var art = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(art);
            inventory.EnsureLoadout(0);
            Assert.That(inventory.Items.Count, Is.EqualTo(ComponentArtCatalog.SlotCount));
            Assert.That(inventory.Items.All(item => !item.InstalledByPlayer), Is.True);
            var cannon = inventory.Installed(0, ComponentSlot.Cannon);
            Assert.That(inventory.TryUnequip(0, ComponentSlot.Cannon), Is.True);
            Assert.That(inventory.TryEquip(0, cannon.InstanceId), Is.True);
            Assert.That(cannon.InstalledByPlayer, Is.False);
        }

        [Test]
        public void RealInstallationSurvivesMoveAndSaveRoundTrip()
        {
            var art = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(art);
            inventory.InitializeEmptyLoadout(0);
            inventory.InitializeEmptyLoadout(1);
            var entry = art.ExpandedEntries.First(item => item.Slot == ComponentSlot.Bow && item.Rarity == ComponentRarity.Rare);
            var owned = inventory.Grant(entry);
            Assert.That(owned.InstalledByPlayer, Is.False, "Receiving or purchasing a component is not installation.");
            Assert.That(inventory.TryEquip(0, owned.InstanceId), Is.True);
            Assert.That(owned.InstalledByPlayer, Is.True);
            Assert.That(inventory.TryUnequip(0, ComponentSlot.Bow), Is.True);
            Assert.That(inventory.TryEquip(1, owned.InstanceId), Is.True);

            var restored = new OwnedShipComponentInventory(art);
            Assert.That(restored.Import(inventory.Export(), ship => ship == 0 || ship == 1), Is.True);
            Assert.That(restored.Installed(0, ComponentSlot.Bow), Is.Null);
            Assert.That(restored.Installed(1, ComponentSlot.Bow).InstanceId, Is.EqualTo(owned.InstanceId));
            Assert.That(restored.Installed(1, ComponentSlot.Bow).InstalledByPlayer, Is.True);
        }

        [Test]
        public void LegacyInventoryWithoutEvidenceKeepsItemsButDoesNotInventAPlayerAction()
        {
            var art = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(art);
            inventory.InitializeEmptyLoadout(0);
            var owned = inventory.Grant(art.ExpandedEntries.First(item => item.Rarity == ComponentRarity.Rare));
            Assert.That(inventory.TryEquip(0, owned.InstanceId), Is.True);
            string legacy = inventory.Export().Replace(",\"installedByPlayer\":true", "");
            Assert.That(legacy, Does.Not.Contain("installedByPlayer"));

            var restored = new OwnedShipComponentInventory(art);
            Assert.That(restored.Import(legacy, ship => ship == 0), Is.True);
            Assert.That(restored.Items.Single().InstanceId, Is.EqualTo(owned.InstanceId));
            Assert.That(restored.Items.Single().InstalledByPlayer, Is.False);
        }
    }
}
