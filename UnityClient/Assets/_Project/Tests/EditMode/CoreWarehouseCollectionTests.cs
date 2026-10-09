using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreWarehouseCollectionTests
    {
        private const string Compass = CoreWarehouseCollection.TutorialTreasureId;
        private const string Bell = CoreWarehouseCollection.BoatswainBellTreasureId;
        private const string Chart = CoreWarehouseCollection.SealedChartTreasureId;

        [Test]
        public void Catalog_ContainsTheAuthoredKeepsakesAndIsReadOnly()
        {
            Assert.That(CoreWarehouseCollection.Catalog.Count, Is.EqualTo(CoreWarehouseCollection.CatalogCount));
            Assert.That(CoreWarehouseCollection.Catalog.Count, Is.EqualTo(40));
            CoreWarehouseTreasureDefinition treasure = CoreWarehouseCollection.Catalog[0];
            Assert.That(treasure.Id, Is.EqualTo(Compass));
            Assert.That(treasure.Name, Is.EqualTo("破浪者的罗盘"));
            Assert.That(treasure.Description, Is.Not.Empty);
            Assert.That(treasure.Story, Is.Not.Empty);
            Assert.That(treasure.Source, Is.EqualTo(CoreCollectionRewardCatalog.Find(Compass).Source));
            Assert.That(treasure.ArtResourcePath, Is.EqualTo("WarehouseV2/first_victory_compass_v1"));
            CoreWarehouseTreasureDefinition bell = CoreWarehouseCollection.Catalog[1];
            Assert.That(bell.Id, Is.EqualTo(Bell));
            Assert.That(bell.Name, Is.EqualTo("航海长的铜钟"));
            Assert.That(bell.Description, Is.Not.Empty);
            Assert.That(bell.Story, Is.Not.Empty);
            Assert.That(bell.Source, Is.Not.Empty);
            Assert.That(bell.ArtResourcePath, Is.EqualTo("WarehouseV2/warehouse_boatswain_bell_v1"));
            CoreWarehouseTreasureDefinition chart = CoreWarehouseCollection.Catalog[2];
            Assert.That(chart.Id, Is.EqualTo(Chart));
            Assert.That(chart.Name, Is.EqualTo("封蜡航线图"));
            Assert.That(chart.Description, Is.Not.Empty);
            Assert.That(chart.Story, Is.Not.Empty);
            Assert.That(chart.Source, Is.Not.Empty);
            Assert.That(chart.ArtResourcePath, Is.EqualTo("WarehouseV2/warehouse_sealed_chart_v1"));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CoreWarehouseTreasureDefinition>)CoreWarehouseCollection.Catalog).Clear());
        }

        [Test]
        public void Catalog_ContainsFortyUniqueCompleteEntries()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoreWarehouseTreasureDefinition treasure in CoreWarehouseCollection.Catalog)
            {
                Assert.That(treasure.Id, Is.Not.Null.And.Not.Empty);
                Assert.That(ids.Add(treasure.Id), Is.True, "Duplicate treasure id: " + treasure.Id);
                Assert.That(treasure.Name, Is.Not.Null.And.Not.Empty);
                Assert.That(treasure.Description, Is.Not.Null.And.Not.Empty);
                Assert.That(treasure.Story, Is.Not.Null.And.Not.Empty);
                Assert.That(treasure.Source, Is.Not.Null.And.Not.Empty);
                Assert.That(treasure.ArtResourcePath, Is.Not.Null.And.Not.Empty);
            }
            Assert.That(ids.Count, Is.EqualTo(40));
        }

        [Test]
        public void MultipleKeepsakes_CanBeOwnedAndDisplayedTogether()
        {
            var collection = new CoreWarehouseCollection();
            Assert.That(collection.Grant(Compass), Is.True);
            Assert.That(collection.Grant(Bell), Is.True);
            Assert.That(collection.Grant(Chart), Is.True);
            Assert.That(collection.TryDisplay(Compass, 0), Is.True);
            Assert.That(collection.TryDisplay(Bell, 1), Is.True);
            Assert.That(collection.TryDisplay(Chart, 2), Is.True);
            CollectionAssert.AreEqual(new[] { Compass, Bell, Chart }, collection.OwnedIds);
            Assert.That(collection.DisplayedAt(0), Is.EqualTo(Compass));
            Assert.That(collection.DisplayedAt(1), Is.EqualTo(Bell));
            Assert.That(collection.DisplayedAt(2), Is.EqualTo(Chart));
            Assert.That(collection.ExportDisplay(), Is.EqualTo(Compass + "," + Bell + "," + Chart + ",,,"));
        }

        [Test]
        public void NewCollection_HasSixEmptyDisplaySlotsAndNoTreasures()
        {
            var collection = new CoreWarehouseCollection();
            Assert.That(CoreWarehouseCollection.DisplaySlotCount, Is.EqualTo(6));
            Assert.That(collection.OwnedIds, Is.Empty);
            Assert.That(collection.Owns(Compass), Is.False);
            Assert.That(collection.IsDisplayed(Compass), Is.False);
            Assert.That(collection.ExportOwned(), Is.Empty);
            Assert.That(collection.ExportDisplay(), Is.EqualTo(",,,,,"));
            for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++)
                Assert.That(collection.DisplayedAt(slot), Is.Empty);
        }

        [Test]
        public void Grant_IsIdempotentAndDoesNotPlaceTreasureAutomatically()
        {
            var collection = new CoreWarehouseCollection();
            Assert.That(collection.Grant(Compass), Is.True);
            Assert.That(collection.Grant(Compass), Is.False);
            Assert.That(collection.Owns(Compass), Is.True);
            CollectionAssert.AreEqual(new[] { Compass }, collection.OwnedIds);
            Assert.That(collection.IsDisplayed(Compass), Is.False);
            Assert.That(collection.ExportOwned(), Is.EqualTo(Compass));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)collection.OwnedIds).Clear());
            Assert.That(collection.Owns(Compass), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("unknown_treasure")]
        [TestCase("FIRST_VICTORY_COMPASS")]
        [TestCase(" first_victory_compass ")]
        [TestCase("first_victory_compass,unknown_treasure")]
        public void InvalidId_IsNeverGrantedOrDisplayed(string id)
        {
            var collection = new CoreWarehouseCollection();
            collection.Grant(Compass);
            collection.TryDisplay(Compass, 0);
            Assert.That(collection.Grant(id), Is.False);
            Assert.That(collection.Owns(id), Is.False);
            Assert.That(collection.IsDisplayed(id), Is.False);
            Assert.That(collection.TryDisplay(id, 0), Is.False);
            Assert.That(collection.DisplayedAt(0), Is.EqualTo(Compass));
            CollectionAssert.AreEqual(new[] { Compass }, collection.OwnedIds);
        }

        [TestCase(-1)]
        [TestCase(6)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidSlot_DoesNotChangeAnExistingDisplay(int slot)
        {
            var collection = new CoreWarehouseCollection();
            collection.Grant(Compass);
            collection.TryDisplay(Compass, 2);
            Assert.That(collection.DisplayedAt(slot), Is.Empty);
            Assert.That(collection.TryDisplay(Compass, slot), Is.False);
            Assert.That(collection.TryRemoveDisplay(slot), Is.False);
            Assert.That(collection.DisplayedAt(2), Is.EqualTo(Compass));
        }

        [Test]
        public void TryDisplay_RequiresOwnership()
        {
            var collection = new CoreWarehouseCollection();
            Assert.That(collection.TryDisplay(Compass, 0), Is.False);
            Assert.That(collection.Owns(Compass), Is.False);
            Assert.That(collection.DisplayedAt(0), Is.Empty);
        }

        [Test]
        public void TryDisplay_MovesExistingTreasureAndReplacingSameSlotIsIdempotent()
        {
            var collection = new CoreWarehouseCollection();
            collection.Grant(Compass);
            Assert.That(collection.TryDisplay(Compass, 0), Is.True);
            Assert.That(collection.TryDisplay(Compass, 5), Is.True);
            Assert.That(collection.DisplayedAt(0), Is.Empty);
            Assert.That(collection.DisplayedAt(5), Is.EqualTo(Compass));
            Assert.That(collection.TryDisplay(Compass, 5), Is.True);
            Assert.That(collection.ExportDisplay(), Is.EqualTo(",,,,," + Compass));
            CollectionAssert.AreEqual(new[] { Compass }, collection.OwnedIds);
        }

        [Test]
        public void TryRemoveDisplay_ReturnsTreasureToInventoryWithoutLosingOwnership()
        {
            var collection = new CoreWarehouseCollection();
            collection.Grant(Compass);
            collection.TryDisplay(Compass, 3);
            Assert.That(collection.TryRemoveDisplay(3), Is.True);
            Assert.That(collection.DisplayedAt(3), Is.Empty);
            Assert.That(collection.IsDisplayed(Compass), Is.False);
            Assert.That(collection.Owns(Compass), Is.True);
            Assert.That(collection.TryRemoveDisplay(3), Is.False);
            Assert.That(collection.TryDisplay(Compass, 1), Is.True);
        }

        [Test]
        public void ExportAndImport_RoundTripPreservesOwnershipAndEmptyDisplayPositions()
        {
            var original = new CoreWarehouseCollection();
            original.Grant(Compass);
            original.TryDisplay(Compass, 4);
            var restored = new CoreWarehouseCollection();
            restored.Import(original.ExportOwned(), original.ExportDisplay());
            CollectionAssert.AreEqual(original.OwnedIds, restored.OwnedIds);
            Assert.That(restored.ExportOwned(), Is.EqualTo(original.ExportOwned()));
            Assert.That(restored.ExportDisplay(), Is.EqualTo(original.ExportDisplay()));
            Assert.That(restored.DisplayedAt(4), Is.EqualTo(Compass));
            Assert.That(restored.DisplayedAt(0), Is.Empty);
        }

        [Test]
        public void Import_TrimsKnownIdsAndDiscardsDuplicatesUnknownIdsAndUnownedDisplays()
        {
            var collection = new CoreWarehouseCollection();
            collection.Import(" ,unknown_treasure, " + Compass + "," + Compass + ", ",
                "unknown_treasure, " + Compass + " ,," + Compass + ",,," + Compass);
            CollectionAssert.AreEqual(new[] { Compass }, collection.OwnedIds);
            Assert.That(collection.ExportOwned(), Is.EqualTo(Compass));
            Assert.That(collection.ExportDisplay(), Is.EqualTo("," + Compass + ",,,,"));

            collection.Import("unknown_treasure", Compass);
            Assert.That(collection.OwnedIds, Is.Empty);
            Assert.That(collection.ExportDisplay(), Is.EqualTo(",,,,,"));
        }

        [Test]
        public void Import_DiscardsEntriesBeyondDisplayCapacityWithoutShiftingThemIntoEmptySlots()
        {
            var collection = new CoreWarehouseCollection();
            collection.Import(Compass, ",,,,,," + Compass);
            Assert.That(collection.Owns(Compass), Is.True);
            Assert.That(collection.IsDisplayed(Compass), Is.False);
            Assert.That(collection.ExportDisplay(), Is.EqualTo(",,,,,"));
        }

        [TestCase(null, null)]
        [TestCase("", "")]
        [TestCase(",,,,", ",,,")]
        [TestCase("first_victory_compass;unknown", "\"first_victory_compass\"")]
        [TestCase("{\"owned\":true}", "[first_victory_compass]")]
        [TestCase("first_victory_compass\0", "first_victory_compass\0")]
        public void Import_MissingOrCorruptSnapshotClearsPreviousStateWithoutInventingOwnership(
            string owned, string displayed)
        {
            var collection = new CoreWarehouseCollection();
            collection.Grant(Compass);
            collection.TryDisplay(Compass, 0);
            Assert.DoesNotThrow(() => collection.Import(owned, displayed));
            Assert.That(collection.OwnedIds, Is.Empty);
            Assert.That(collection.ExportDisplay(), Is.EqualTo(",,,,,"));
        }

        [Test]
        public void Import_EmptyDisplayKeepsTreasureOwnedAndClearsPreviousPlacement()
        {
            var collection = new CoreWarehouseCollection();
            collection.Grant(Compass);
            collection.TryDisplay(Compass, 2);
            collection.Import(Compass, null);
            Assert.That(collection.Owns(Compass), Is.True);
            Assert.That(collection.IsDisplayed(Compass), Is.False);
        }
    }
}
