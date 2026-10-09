using System;
using System.Linq;
using NUnit.Framework;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class ComponentResaleTests
    {
        [Test]
        public void RemoveUninstalled_RemovesOnlyTheSelectedPhysicalCopyOnce()
        {
            var catalog = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            OwnedShipComponent first = inventory.Grant(catalog.Get(0), 0.93f);
            OwnedShipComponent second = inventory.Grant(catalog.Get(0), 1.08f);
            string equippedId = inventory.Installed(0, ComponentSlot.Bow).InstanceId;

            Assert.That(inventory.TryRemoveUninstalled(first.InstanceId), Is.True);
            Assert.That(inventory.Items.Any(item => item.InstanceId == first.InstanceId), Is.False);
            Assert.That(inventory.Items.Single(item => item.InstanceId == second.InstanceId), Is.SameAs(second));
            Assert.That(second.Quality, Is.EqualTo(1.08f));
            Assert.That(inventory.Installed(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(equippedId));
            Assert.That(inventory.Items.Count, Is.EqualTo(6));
            string unchanged = inventory.Export();
            foreach (string invalid in new[] { first.InstanceId, "missing", string.Empty, null })
            {
                Assert.That(inventory.TryRemoveUninstalled(invalid), Is.False);
                Assert.That(inventory.Export(), Is.EqualTo(unchanged));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RemoveUninstalled_RejectsEveryInstalledSlotOnEveryVessel(int ship)
        {
            var inventory = new OwnedShipComponentInventory(new ComponentArtCatalog());
            inventory.EnsureLoadout(ship);
            string unchanged = inventory.Export();
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
            {
                OwnedShipComponent installed = inventory.Installed(ship, slot);
                Assert.That(inventory.TryRemoveUninstalled(installed.InstanceId), Is.False);
                Assert.That(inventory.Installed(ship, slot), Is.SameAs(installed));
                Assert.That(inventory.Export(), Is.EqualTo(unchanged));
            }
        }

        [Test]
        public void SoldStarterSlot_DoesNotRegenerateFreePartsWhenInventoryIsReloaded()
        {
            var catalog = new ComponentArtCatalog();
            var inventory = new OwnedShipComponentInventory(catalog);
            inventory.EnsureLoadout(0);
            inventory.EnsureLoadout(1);
            string soldId = inventory.Installed(0, ComponentSlot.Plating).InstanceId;
            Assert.That(inventory.TryUnequip(0, ComponentSlot.Plating), Is.True);
            Assert.That(inventory.TryRemoveUninstalled(soldId), Is.True);

            var restored = new OwnedShipComponentInventory(catalog);
            Assert.That(restored.Import(inventory.Export(), ship => ship == 0 || ship == 1), Is.True);
            restored.EnsureLoadout(0);
            restored.EnsureLoadout(1);
            Assert.That(restored.Items.Count, Is.EqualTo(9));
            Assert.That(restored.Items.Any(item => item.InstanceId == soldId), Is.False);
            Assert.That(restored.Installed(0, ComponentSlot.Plating), Is.Null);
            Assert.That(restored.Installed(1, ComponentSlot.Plating), Is.Not.Null);
            Assert.That(restored.TryRemoveUninstalled(soldId), Is.False);
        }

        [Test]
        public void EveryAppraisalAndPort_PaysHalfRetailAndNeverAllowsCrossPortPurchaseResaleProfit()
        {
            var catalog = new ComponentArtCatalog();
            string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            foreach (ComponentArtEntry entry in catalog.ExpandedEntries)
            {
                // Shop appraisals have 1% steps, from 90% through 110%.
                for (int appraisal = 90; appraisal <= 110; appraisal++)
                {
                    float quality = appraisal / 100f;
                    int cheapestPurchase = ports.Min(port => PortComponentShopCatalog.CalculatePrice(entry, port, quality));
                    foreach (string port in ports)
                    {
                        int retail = PortComponentShopCatalog.CalculatePrice(entry, port, quality);
                        int resale = PortComponentShopCatalog.CalculateSalePrice(entry, port, quality);
                        string context = entry.Id + " / " + port + " / " + appraisal;
                        Assert.That(resale, Is.EqualTo(retail / 2), context);
                        Assert.That(resale, Is.GreaterThan(0), context);
                        Assert.That(resale, Is.LessThan(cheapestPurchase),
                            context + ": a bought fitting cannot generate profit by immediate or cross-port resale.");
                    }
                }
            }
        }
    }
}
