using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ItemIconCatalogTests
    {
        private static readonly HashSet<string> SharedSupplyKeys = new HashSet<string>
        {
            "wood", "medicine", "powder", "cannonballs",
        };

        [Test]
        public void EveryPhysicalCargoAndSpecialItem_HasUniqueCellInItsSharedAtlas()
        {
            Assert.That(CoreWarehouseItemCatalog.All.Count, Is.EqualTo(15));
            Texture2D itemTexture = Resources.Load<Texture2D>(ItemIconCatalog.AtlasResourcePath);
            Texture2D battleTexture = BattleIconArtCatalog.Texture;
            Assert.That(itemTexture, Is.Not.Null, ItemIconCatalog.AtlasResourcePath);
            Assert.That(battleTexture, Is.Not.Null, BattleIconArtCatalog.ResourcePath);
            Assert.That(battleTexture, Is.Not.SameAs(itemTexture));
            var itemCells = new HashSet<Rect>();
            var battleCells = new HashSet<Rect>();
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.All)
            {
                bool sharedSupply = SharedSupplyKeys.Contains(item.Key);
                AssertAtlasItem(ItemIconCatalog.ForCargo(item.Key), sharedSupply ? battleTexture : itemTexture,
                    sharedSupply ? battleCells : itemCells, item.Key);
            }
            foreach (string key in new[] { "gold", "treasurefragment", "treasuremap_weathered", "treasuremap_gilded", "treasuremap_moonlit" })
                AssertAtlasItem(ItemIconCatalog.ForSpecial(key), itemTexture, itemCells, key);
            Assert.That(battleCells.Count, Is.EqualTo(4));
            Assert.That(itemCells.Count, Is.EqualTo(16));
            Assert.That(itemCells.Count + battleCells.Count, Is.EqualTo(20));
        }

        [TestCase("wood", BattleCommandIconKind.Planks)]
        [TestCase("medicine", BattleCommandIconKind.Medicine)]
        [TestCase("powder", BattleCommandIconKind.Powder)]
        [TestCase("cannonballs", BattleCommandIconKind.Cannonball)]
        public void SharedSupplies_UseTheSameTextureAndCellAsTheirBattleCommand(string key, BattleCommandIconKind command)
        {
            ItemIconArt art = ItemIconCatalog.ForCargo(key);
            Assert.That(art.IsAvailable, Is.True, key);
            Assert.That(art.Texture, Is.SameAs(BattleIconArtCatalog.Texture), key);
            Assert.That(BattleIconArtCatalog.TryGetUv(BattleIconArtCatalog.CommandAssetId(command), out Rect expected), Is.True);
            Assert.That(art.Uv, Is.EqualTo(expected), key);
            ItemIconArt normalized = ItemIconCatalog.ForCargo(" " + key.ToUpperInvariant() + " ");
            Assert.That(normalized.Texture, Is.SameAs(art.Texture), key);
            Assert.That(normalized.Uv, Is.EqualTo(art.Uv), key);
        }

        [Test]
        public void UnchangedAtlasLayout_PreservesMeasuredPaddedBoundsWithoutOverlappingOtherSubjects()
        {
            string[] keys =
            {
                "water", "food", "iron", "cloth", "beer", "gold",
                "gem", "ancientmaterial", "relic", "blackpearl", "ancientcompass",
                "lantern", "treasurefragment", "treasuremap_weathered", "treasuremap_gilded", "treasuremap_moonlit",
            };
            Rect[] expectedBounds =
            {
                new Rect(51, 30, 201, 254), new Rect(297, 62, 256, 223),
                new Rect(839, 64, 277, 213), new Rect(1136, 58, 248, 223),
                new Rect(65, 284, 163, 279), new Rect(1146, 327, 227, 222),
                new Rect(47, 583, 204, 218), new Rect(295, 570, 243, 244),
                new Rect(574, 566, 240, 244), new Rect(846, 577, 258, 229), new Rect(1142, 563, 228, 248),
                new Rect(59, 802, 177, 281), new Rect(322, 827, 207, 254),
                new Rect(546, 813, 274, 260), new Rect(831, 816, 278, 264), new Rect(1119, 818, 266, 265),
            };
            Texture2D texture = Resources.Load<Texture2D>(ItemIconCatalog.AtlasResourcePath);
            Assert.That(texture, Is.Not.Null);
            var actualBounds = new List<Rect>();
            for (int cell = 0; cell < keys.Length; cell++)
            {
                ItemIconArt art = ItemIconCatalog.ForCargo(keys[cell]);
                if (!art.IsAvailable) art = ItemIconCatalog.ForSpecial(keys[cell]);
                Assert.That(art.IsAvailable, Is.True, keys[cell]);
                Assert.That(art.Texture, Is.SameAs(texture), keys[cell]);
                // Source-pixel bounds are independent of platform import scaling.
                Rect expected = expectedBounds[cell];
                Assert.That(art.Uv.x * 1402f, Is.EqualTo(expected.x).Within(0.001f), keys[cell]);
                Assert.That((1f - art.Uv.yMax) * 1122f, Is.EqualTo(expected.y).Within(0.001f), keys[cell]);
                Assert.That(art.Uv.width * 1402f, Is.EqualTo(expected.width).Within(0.001f), keys[cell]);
                Assert.That(art.Uv.height * 1122f, Is.EqualTo(expected.height).Within(0.001f), keys[cell]);
                foreach (Rect previous in actualBounds)
                    Assert.That(art.Uv.Overlaps(previous), Is.False, keys[cell] + " overlaps another icon.");
                actualBounds.Add(art.Uv);
            }
            Assert.That(actualBounds.Count, Is.EqualTo(16));
        }

        [Test]
        public void AllExpandedComponents_ReuseTheirExistingArtWithoutNewTextures()
        {
            var catalog = new ComponentArtCatalog();
            Assert.That(catalog.ExpandedEntries.Count, Is.EqualTo(150));
            var textures = new HashSet<Texture2D>();
            foreach (ComponentArtEntry entry in catalog.ExpandedEntries)
            {
                ItemIconArt art = ItemIconCatalog.ForComponent(entry);
                Assert.That(art.IsAvailable, Is.True, entry.Id);
                Assert.That(art.Texture, Is.SameAs(entry.Texture), entry.Id);
                Assert.That(art.Uv, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), entry.Id);
                textures.Add(art.Texture);
            }
            Assert.That(textures.Count, Is.EqualTo(catalog.ExpandedEntries.Select(entry => entry.ArtSourceId).Distinct().Count()));
        }

        [Test]
        public void AllPermanentTreasures_HaveFortyDistinctCellsInTwoSharedAtlases()
        {
            Assert.That(CoreWarehouseCollection.Catalog.Count, Is.EqualTo(40));
            Texture2D first = Resources.Load<Texture2D>(ItemIconCatalog.KeepsakeAtlasAResourcePath);
            Texture2D second = Resources.Load<Texture2D>(ItemIconCatalog.KeepsakeAtlasBResourcePath);
            Assert.That(first, Is.Not.Null, ItemIconCatalog.KeepsakeAtlasAResourcePath);
            Assert.That(second, Is.Not.Null, ItemIconCatalog.KeepsakeAtlasBResourcePath);
            Assert.That(second, Is.Not.SameAs(first));
            var firstCells = new HashSet<Rect>();
            var secondCells = new HashSet<Rect>();
            for (int index = 0; index < CoreWarehouseCollection.Catalog.Count; index++)
            {
                CoreWarehouseTreasureDefinition definition = CoreWarehouseCollection.Catalog[index];
                ItemIconArt art = ItemIconCatalog.ForTreasure(definition);
                HashSet<Rect> cells = index < 20 ? firstCells : secondCells;
                foreach (Rect previous in cells)
                    Assert.That(art.Uv.Overlaps(previous), Is.False, definition.Id + " overlaps another keepsake.");
                AssertAtlasItem(art, index < 20 ? first : second, cells, definition.Id);
                ItemIconArt repeated = ItemIconCatalog.ForTreasure(definition);
                Assert.That(repeated.Texture, Is.SameAs(art.Texture), definition.Id);
                Assert.That(repeated.Uv, Is.EqualTo(art.Uv), definition.Id);
            }
            Assert.That(firstCells.Count, Is.EqualTo(20));
            Assert.That(secondCells.Count, Is.EqualTo(20));
        }

        [Test]
        public void KeepsakeAtlases_LoadOnlyWhenTheirGroupIsRequested()
        {
            Type catalog = typeof(ItemIconCatalog);
            catalog.GetMethod("ResetCache", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            FieldInfo first = catalog.GetField("treasureAtlasA", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo second = catalog.GetField("treasureAtlasB", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(first.GetValue(null), Is.Null);
            Assert.That(second.GetValue(null), Is.Null);
            Assert.That(ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[0]).IsAvailable, Is.True);
            Assert.That(first.GetValue(null), Is.Not.Null);
            Assert.That(second.GetValue(null), Is.Null);
            Assert.That(ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[20]).IsAvailable, Is.True);
            Assert.That(second.GetValue(null), Is.Not.Null);
        }

        [Test]
        public void ReleaseLoadedTextures_PreservesBorrowedAtlasesAndRestoresLazyLoading()
        {
            ItemIconCatalog.ReleaseLoadedTextures();
            Texture2D cargo = ItemIconCatalog.ForCargo("water").Texture;
            Texture2D first = ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[0]).Texture;
            Texture2D second = ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[20]).Texture;
            Assert.That(new[] { cargo, first, second }.All(texture => texture != null), Is.True);
            try
            {
                ItemIconCatalog.ReleaseLoadedTextures();
                ItemIconCatalog.ReleaseLoadedTextures();
                Assert.That(new[] { cargo, first, second }.All(texture => texture != null), Is.True,
                    "Clearing borrowed cache references must not destroy textures retained by a view.");
                foreach (string field in new[] { "atlas", "treasureAtlasA", "treasureAtlasB" })
                {
                    Assert.That(typeof(ItemIconCatalog).GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
                    Assert.That(typeof(ItemIconCatalog).GetField(field + "Loaded", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.False);
                }
                Assert.That(ItemIconCatalog.ForCargo("water").Texture, Is.SameAs(cargo));
                Assert.That(typeof(ItemIconCatalog).GetField("treasureAtlasA", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
                Assert.That(typeof(ItemIconCatalog).GetField("treasureAtlasB", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
                Assert.That(ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[20]).Texture, Is.SameAs(second));
                Assert.That(typeof(ItemIconCatalog).GetField("treasureAtlasA", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
                Assert.That(ItemIconCatalog.ForTreasure(CoreWarehouseCollection.Catalog[0]).Texture, Is.SameAs(first));
            }
            finally { ItemIconCatalog.ReleaseLoadedTextures(); }
        }

        [Test]
        public void AllTreasureRanks_UseTheirOwnSpecialCell()
        {
            Assert.That(CoreTreasureHuntCatalog.Ranks.Count, Is.EqualTo(3));
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
            {
                ItemIconArt art = ItemIconCatalog.ForTreasureMap(rank.Id);
                ItemIconArt special = ItemIconCatalog.ForSpecial("treasuremap_" + rank.Id);
                Assert.That(art.IsAvailable, Is.True, rank.Id);
                Assert.That(art.Texture, Is.SameAs(special.Texture), rank.Id);
                Assert.That(art.Uv, Is.EqualTo(special.Uv), rank.Id);
            }
        }

        [Test]
        public void UnknownAndMissingItems_ReturnUnavailableWithoutBorrowingAnotherIcon()
        {
            foreach (string key in new[] { null, "", "unknown", "gem_typo" })
            {
                Assert.That(ItemIconCatalog.ForCargo(key).IsAvailable, Is.False);
                Assert.That(ItemIconCatalog.ForSpecial(key).IsAvailable, Is.False);
                Assert.That(ItemIconCatalog.ForTreasureMap(key).IsAvailable, Is.False);
            }
            Assert.That(ItemIconCatalog.ForComponent(null).IsAvailable, Is.False);
            Assert.That(ItemIconCatalog.ForTreasure(null).IsAvailable, Is.False);
            Assert.That(default(ItemIconArt).IsAvailable, Is.False);
            Assert.That(new ItemIconArt(null, new Rect(0f, 0f, 1f, 1f)).IsAvailable, Is.False);

            // An unknown stable ID must stay unavailable even if its old large
            // illustration points to a valid resource used by another keepsake.
            var missing = (CoreWarehouseTreasureDefinition)Activator.CreateInstance(
                typeof(CoreWarehouseTreasureDefinition), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { "missing_icon_test", "Missing", "", "", "", CoreWarehouseCollection.Catalog[0].ArtResourcePath }, null);
            Assert.That(ItemIconCatalog.ForTreasure(missing).IsAvailable, Is.False);
            Assert.That(ItemIconCatalog.ForTreasure(missing).IsAvailable, Is.False);
        }

        private static void AssertAtlasItem(ItemIconArt art, Texture2D texture, HashSet<Rect> cells, string key)
        {
            Assert.That(art.IsAvailable, Is.True, key);
            Assert.That(art.Texture, Is.SameAs(texture), key);
            Assert.That(art.Uv.xMin, Is.GreaterThanOrEqualTo(0f), key);
            Assert.That(art.Uv.yMin, Is.GreaterThanOrEqualTo(0f), key);
            Assert.That(art.Uv.xMax, Is.LessThanOrEqualTo(1f), key);
            Assert.That(art.Uv.yMax, Is.LessThanOrEqualTo(1f), key);
            Assert.That(art.Uv.width, Is.GreaterThan(0f), key);
            Assert.That(art.Uv.height, Is.GreaterThan(0f), key);
            Assert.That(cells.Add(art.Uv), Is.True, key + " aliases another item cell.");
        }
    }
}
