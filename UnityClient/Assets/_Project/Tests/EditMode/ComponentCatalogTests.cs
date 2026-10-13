using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class ComponentCatalogTests
    {
        private static readonly string[] LegacyIds =
        {
            "bow_oak_ram", "bow_reef_cutter", "bow_gilded_mermaid", "bow_abyssal_fang", "bow_leviathan_ram",
            "sail_patched_lateen", "sail_storm_canvas", "sail_guild_silk", "sail_moonweave", "sail_tempest_wing",
            "cannon_deck_swivel", "cannon_bronze_longgun", "cannon_chain_repeater", "cannon_starfire_battery", "cannon_queens_broadside",
            "plating_braced_ribs", "plating_iron_band", "plating_gilded_bulwark", "plating_moonsilver_skin", "plating_living_hull",
            "instrument_brass_compass", "instrument_tideglass", "instrument_guild_engine", "instrument_lunar_astrolabe", "instrument_allseeing_sextant",
        };

        [Test]
        public void Catalog_HasFiveAuthoredVariantsForEverySlotAndRarity()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();

            Assert.That(Resources.Load<TextAsset>(ComponentArtCatalog.CatalogResourcePath), Is.Not.Null);
            Assert.That(catalog.Entries.Count, Is.EqualTo(125));
            Assert.That(catalog.IsAvailable, Is.True);
            Assert.That(catalog.Entries.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(125));
            Assert.That(catalog.Entries.Select(entry => entry.Name).Distinct().Count(), Is.EqualTo(125));
            Assert.That(catalog.Entries.Select(entry => entry.Effect).Distinct().Count(), Is.EqualTo(125));
            Assert.That(catalog.Entries.Select(entry => entry.Specialization).Distinct().Count(), Is.EqualTo(125));
            Assert.That(catalog.Entries.Select(entry => entry.Description).Distinct().Count(), Is.EqualTo(125));

            foreach (IGrouping<ComponentSlot, ComponentArtEntry> slot in catalog.Entries.GroupBy(entry => entry.Slot))
            {
                Assert.That(slot.Count(), Is.EqualTo(ComponentArtCatalog.EntriesPerSlot), slot.Key.ToString());
                foreach (IGrouping<ComponentRarity, ComponentArtEntry> rarity in slot.GroupBy(entry => entry.Rarity))
                {
                    Assert.That(rarity.Count(), Is.EqualTo(ComponentArtCatalog.EntriesPerRarityPerSlot),
                        slot.Key + " / " + rarity.Key);
                    Assert.That(rarity.Select(entry => entry.Price).Distinct().Count(), Is.EqualTo(5),
                        slot.Key + " / " + rarity.Key + " should have five distinct prices.");
                }
            }
        }

        [Test]
        public void Catalog_EveryEntryHasGameplayIdentityStoryAndMappedArt()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();

            foreach (ComponentArtEntry entry in catalog.ExpandedEntries)
            {
                Assert.That(entry.InstallWood, Is.GreaterThan(0), entry.Id);
                Assert.That(entry.Price, Is.GreaterThan(0), entry.Id);
                Assert.That(entry.Effect, Is.Not.Empty, entry.Id);
                Assert.That(entry.Specialization, Is.Not.Empty, entry.Id);
                Assert.That(entry.Description.Length, Is.GreaterThanOrEqualTo(24), entry.Id + " needs a short narrative paragraph.");
                Assert.That(entry.ArtSourceId, Is.Not.Empty, entry.Id);
                Assert.That(entry.Texture, Is.Not.Null, entry.Id + " icon mapping");
                Assert.That(entry.ShipLayer, Is.Not.Null, entry.Id + " ship-layer mapping");
                Assert.That(entry.StoryId, Does.StartWith("seventh_berth_"), entry.Id);
                Assert.That(entry.StoryChapter, Is.InRange(1, 15), entry.Id);
                Assert.That(entry.StoryTitle, Does.Contain("航海异闻"), entry.Id);
                Assert.That(entry.StoryBody.Length, Is.GreaterThan(50), entry.Id);
            }
            Assert.That(catalog.Entries.Select(entry => entry.StoryId).Distinct().Count(), Is.EqualTo(15));

            Assert.That(catalog.Entries.Select(entry => entry.ArtSourceId).Distinct().Count(), Is.EqualTo(125));
            Assert.That(catalog.ExpandedEntries.Select(entry => entry.ArtSourceId).Distinct().Count(), Is.EqualTo(150),
                "Each equipment item must keep its independent visual identity, including expanded rewards.");
            foreach (ComponentArtEntry entry in catalog.ExpandedEntries)
            {
                Assert.That(entry.ArtSourceId, Is.EqualTo(entry.Id), entry.Id);
                Assert.That(entry.Texture, Is.Not.Null, entry.Id + " inventory icon");
                Assert.That(entry.ShipLayer, Is.Not.Null, entry.Id + " detail image");
            }
        }

        [Test]
        public void Catalog_AdvertisedEffectsExactlyMatchAppliedNumericStats()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            foreach (ComponentArtEntry entry in catalog.ExpandedEntries)
            {
                string[] names = { "船体", "装甲", "火力", "航速", "视野" };
                float[] values = { entry.HullBonus, entry.ArmorBonus, entry.FirepowerBonus, entry.SpeedBonus, entry.VisionBonus };
                List<string> actual = new List<string>();
                for (int index = 0; index < values.Length; index += 1)
                {
                    if (values[index] == 0f) continue;
                    actual.Add(names[index] + " " + (values[index] > 0 ? "+" : string.Empty) +
                        values[index].ToString("0.##", CultureInfo.InvariantCulture));
                }
                Assert.That(entry.Effect, Is.EqualTo(string.Join("　", actual)), entry.Id);
            }
        }

        [Test]
        public void Catalog_RemovedDemoAttributesDoNotShiftRetainedRuntimeColumns()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            Assert.That(typeof(ComponentArtEntry).GetProperty("Volume"), Is.Null);
            Assert.That(typeof(ComponentArtEntry).GetProperty("InstallHours"), Is.Null);
            string[] lines = Resources.Load<TextAsset>(ComponentArtCatalog.CatalogResourcePath).text
                .Replace("\r\n", "\n").Trim().Split('\n');
            string[] header = lines[0].Split(',');
            Assert.That(header, Does.Not.Contain("volume"));
            Assert.That(header, Does.Not.Contain("install_hours"));
            Assert.That(header.Length, Is.EqualTo(20));
            Assert.That(lines.Length - 1, Is.EqualTo(125));

            foreach (string line in lines.Skip(1))
            {
                string[] cells = line.Split(',');
                Assert.That(cells.Length, Is.EqualTo(header.Length));
                Dictionary<string, string> row = header.Select((name, index) => new { name, value = cells[index].Trim() })
                    .ToDictionary(cell => cell.name, cell => cell.value);
                ComponentArtEntry entry = catalog.GetById(row["component_id"]);
                Assert.That(entry, Is.Not.Null, row["component_id"]);
                Assert.That(entry.Name, Is.EqualTo(row["name"]), entry.Id);
                Assert.That(entry.Slot.ToString().ToLowerInvariant(), Is.EqualTo(row["slot"]), entry.Id);
                Assert.That(entry.Rarity.ToString().ToLowerInvariant(), Is.EqualTo(row["rarity"]), entry.Id);
                Assert.That(entry.InstallWood, Is.EqualTo(int.Parse(row["install_wood"], CultureInfo.InvariantCulture)), entry.Id);
                Assert.That(entry.Price, Is.EqualTo(int.Parse(row["shop_rarity_price"], CultureInfo.InvariantCulture)), entry.Id);
                Assert.That(entry.HullBonus, Is.EqualTo(int.Parse(row["max_hull"], CultureInfo.InvariantCulture)), entry.Id);
                Assert.That(entry.ArmorBonus, Is.EqualTo(int.Parse(row["armor"], CultureInfo.InvariantCulture)), entry.Id);
                Assert.That(entry.FirepowerBonus, Is.EqualTo(int.Parse(row["firepower"], CultureInfo.InvariantCulture)), entry.Id);
                Assert.That(entry.SpeedBonus, Is.EqualTo(float.Parse(row["speed"], CultureInfo.InvariantCulture)).Within(0.0001f), entry.Id);
                Assert.That(entry.VisionBonus, Is.EqualTo(float.Parse(row["vision"], CultureInfo.InvariantCulture)).Within(0.0001f), entry.Id);
                Assert.That(entry.Effect, Is.EqualTo(row["effect"]), entry.Id);
                Assert.That(entry.Specialization, Is.EqualTo(row["specialization"]), entry.Id);
                Assert.That(entry.ArtSourceId, Is.EqualTo(row["art_source_id"]), entry.Id);
                Assert.That(entry.StoryId, Is.EqualTo(row["story_id"]), entry.Id);
            }
        }

        [Test]
        public void Catalog_EachRarityOffersRealSpecializationOrPriceTradeoffs()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            foreach (var group in catalog.Entries.GroupBy(entry => new { entry.Slot, entry.Rarity }))
            {
                int statFrontier = group.Count(entry => !group.Any(other =>
                    other != entry && DominatesStats(other, entry)));
                Assert.That(statFrontier, Is.GreaterThanOrEqualTo(3), group.Key.ToString() +
                    " needs several genuine stat sidegrades, not five versions of one strictly better item.");
                foreach (ComponentArtEntry entry in group)
                {
                    Assert.That(group.Any(other => other != entry && other.Price <= entry.Price && DominatesStats(other, entry)),
                        Is.False, entry.Id + " must not cost as much or more than a strictly superior same-rarity item.");
                }
            }
        }

        private static bool DominatesStats(ComponentArtEntry first, ComponentArtEntry second)
        {
            return first.HullBonus >= second.HullBonus && first.ArmorBonus >= second.ArmorBonus &&
                first.FirepowerBonus >= second.FirepowerBonus && first.SpeedBonus >= second.SpeedBonus &&
                first.VisionBonus >= second.VisionBonus &&
                (first.HullBonus > second.HullBonus || first.ArmorBonus > second.ArmorBonus ||
                 first.FirepowerBonus > second.FirepowerBonus || first.SpeedBonus > second.SpeedBonus ||
                 first.VisionBonus > second.VisionBonus);
        }

        [Test]
        public void ComponentBonuses_PreserveLightweightTradeoffsAndClampFinalStats()
        {
            CoreVoyageSession session = new CoreVoyageSession();
            int baseHull = session.MaxHull;
            int baseArmor = session.Armor;
            int baseFirepower = session.Firepower;
            float baseSpeed = session.SpeedKnots;

            session.ConfigureComponentBonuses(-3, -1, -1, 0.8f, -4f);
            Assert.That(session.MaxHull, Is.EqualTo(baseHull - 3));
            Assert.That(session.Armor, Is.EqualTo(baseArmor - 1));
            Assert.That(session.Firepower, Is.EqualTo(baseFirepower - 1));
            Assert.That(session.SpeedKnots, Is.EqualTo(baseSpeed + 0.8f).Within(0.001f));
            Assert.That(session.VisionBonusNm, Is.EqualTo(-4f));
            Assert.That(session.Hull, Is.LessThanOrEqualTo(session.MaxHull));

            session.ConfigureComponentBonuses(-10000, -10000, -10000, -10000f, 0f);
            Assert.That(session.MaxHull, Is.EqualTo(1));
            Assert.That(session.Armor, Is.Zero);
            Assert.That(session.Firepower, Is.Zero);
            Assert.That(session.SpeedKnots, Is.EqualTo(2.5f));
        }

        [Test]
        public void RegionalStock_DoesNotDuplicateItemsOrSharePurchaseIdsBetweenPorts()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            string[] ports = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
            for (int month = 1; month <= 12; month += 1)
            {
                HashSet<string> purchaseIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string port in ports)
                {
                    IReadOnlyList<PortComponentShopStockItem> stock = PortComponentShopCatalog.BuildStock(catalog, port, 2026, month);
                    Assert.That(stock.Count, Is.EqualTo(PortComponentShopCatalog.StockCount(0)), port);
                    Assert.That(stock.Select(item => item.Entry.Id).Distinct().Count(), Is.EqualTo(stock.Count), port);
                    foreach (PortComponentShopStockItem offer in stock)
                    {
                        Assert.That(purchaseIds.Add(offer.Id), Is.True, "Cross-port sale identity collision: " + offer.Id);
                        Assert.That(PortComponentShopCatalog.PreferredSlots(port), Does.Contain(offer.Entry.Slot), port);
                    }
                }
            }
        }

        [Test]
        public void QuoteValidity_RejectsOtherPortsAndExpiredMonthsIncludingYearRollover()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            PortComponentShopStockItem offer = PortComponentShopCatalog.BuildStock(
                catalog, "白贝港", new DateTime(2026, 12, 31, 23, 59, 59))[0];
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(offer, "白贝港", new DateTime(2026, 12, 31, 23, 59, 59)), Is.True);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(offer, "白贝港", new DateTime(2027, 1, 1)), Is.False,
                "A sheet held open over a game month boundary must not retain an expired quote.");
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(offer, "灰鸥港", new DateTime(2026, 12, 20)), Is.False,
                "Economically similar ports still own different stock.");
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(offer, "白贝港", new DateTime(2025, 12, 20)), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(null, "白贝港", new DateTime(2026, 12, 20)), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(offer, null, new DateTime(2026, 12, 20)), Is.False);
        }

        [Test]
        public void ComponentShop_RefreshesOnlyByGameMonthAndLabelsNextMonthsFirstDay()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            DateTime morning = new DateTime(2026, 9, 29, 8, 15, 0);
            DateTime evening = new DateTime(2026, 9, 29, 23, 59, 59);
            DateTime nextDay = morning.AddDays(1);
            IReadOnlyList<PortComponentShopStockItem> morningStock =
                PortComponentShopCatalog.BuildStock(catalog, "白贝港", morning);
            IReadOnlyList<PortComponentShopStockItem> eveningStock =
                PortComponentShopCatalog.BuildStock(catalog, "白贝港", evening);
            IReadOnlyList<PortComponentShopStockItem> nextDayStock =
                PortComponentShopCatalog.BuildStock(catalog, "白贝港", nextDay);
            IReadOnlyList<PortComponentShopStockItem> nextMonthStock =
                PortComponentShopCatalog.BuildStock(catalog, "白贝港", new DateTime(2026, 10, 1));

            CollectionAssert.AreEqual(morningStock.Select(item => item.Id), eveningStock.Select(item => item.Id),
                "Reopening the shop must keep its batch.");
            CollectionAssert.AreEqual(morningStock.Select(item => item.Id), nextDayStock.Select(item => item.Id),
                "A new day in the same game month must not replenish sold stock.");
            Assert.That(morningStock[0].RefreshKey, Is.EqualTo(PortComponentShopCatalog.MonthKey(morning)));
            Assert.That(nextMonthStock[0].RefreshKey, Is.EqualTo("2026-10"));
            Assert.That(nextMonthStock[0].Id, Is.Not.EqualTo(morningStock[0].Id));
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(morningStock[0], "白贝港", evening), Is.True);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(morningStock[0], "白贝港", nextDay), Is.True);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(morningStock[0], "白贝港", new DateTime(2026, 10, 1)), Is.False);
            Assert.That(PortComponentShopCatalog.NextRefreshLabel(evening), Is.EqualTo("2026-10-01"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ComponentShop_InvestmentAppendsTwoOffersPerTierAndPreservesExistingReceiptsAndContents(bool expanded)
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            foreach (string port in PortInvestmentCatalog.SupportedPorts)
            for (int month = 1; month <= 12; month++)
            {
                DateTime date = new DateTime(2026, month, 1);
                IReadOnlyList<PortComponentShopStockItem> previous = null;
                for (int level = 0; level <= PortInvestmentCatalog.MaxLevel; level++)
                {
                    IReadOnlyList<PortComponentShopStockItem> stock = expanded
                        ? PortComponentShopCatalog.BuildExpandedStock(catalog, port, date, 0, string.Empty,
                            PortInvestmentCatalog.RarityThresholdShift(level), level)
                        : PortComponentShopCatalog.BuildStock(catalog, port, date, 0, string.Empty,
                            PortInvestmentCatalog.RarityThresholdShift(level), level);
                    Assert.That(stock.Count, Is.EqualTo(6 + 2 * level));
                    Assert.That(stock.Select(item => item.Id).Distinct().Count(), Is.EqualTo(stock.Count));
                    Assert.That(stock.Select(item => item.Entry.Id).Distinct().Count(), Is.EqualTo(stock.Count));
                    foreach (PortComponentShopStockItem offer in stock)
                    {
                        Assert.That(PortComponentShopCatalog.PreferredSlots(port), Does.Contain(offer.Entry.Slot));
                        Assert.That(PortComponentShopCatalog.IsCurrentOffer(offer, port, date.AddDays(10), 0, string.Empty, level), Is.True);
                    }
                    if (previous != null)
                    {
                        CollectionAssert.AreEqual(previous.Select(DescribeOffer), stock.Take(previous.Count).Select(DescribeOffer),
                            port + "/" + month + "/" + level + ": investment cannot change an old offer or its purchase identity.");
                        Assert.That(stock.Skip(previous.Count).All(offer => offer.InvestmentLevel == level), Is.True);
                        Assert.That(PortComponentShopCatalog.IsCurrentOffer(stock[stock.Count - 1], port, date, 0, string.Empty, level - 1), Is.False,
                            "An extra offer remains inaccessible until its construction tier is reached.");
                    }
                    previous = stock;
                }
            }
        }

        private static string DescribeOffer(PortComponentShopStockItem offer) =>
            offer.Id + ":" + offer.Entry.Id + ":" + offer.Quality + ":" + offer.Price + ":" + offer.InvestmentLevel;

        [Test]
        public void Catalog_PreservesLegacyIdsAndStarterOrdering()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();

            foreach (string legacyId in LegacyIds)
            {
                ComponentArtEntry legacy = catalog.GetById(legacyId);
                Assert.That(legacy, Is.Not.Null, legacyId);
                Assert.That(legacy.ArtSourceId, Is.EqualTo(legacyId), legacyId);
            }

            Dictionary<ComponentSlot, string> starters = new Dictionary<ComponentSlot, string>
            {
                { ComponentSlot.Bow, "bow_oak_ram" },
                { ComponentSlot.Sail, "sail_patched_lateen" },
                { ComponentSlot.Cannon, "cannon_deck_swivel" },
                { ComponentSlot.Plating, "plating_braced_ribs" },
                { ComponentSlot.Instrument, "instrument_brass_compass" },
            };
            foreach (KeyValuePair<ComponentSlot, string> starter in starters)
            {
                Assert.That(catalog.Get((int)starter.Key * ComponentArtCatalog.EntriesPerSlot).Id,
                    Is.EqualTo(starter.Value), starter.Key.ToString());
            }
        }

        [Test]
        public void Loadout_RestoresAll125StableIdsAndAppliesTheirActualTradeoffs()
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            Dictionary<string, string> saved = new Dictionary<string, string>();
            string[] keys = Enumerable.Range(0, ComponentArtCatalog.SlotCount)
                .Select(index => CoreGameplayDemoController.ComponentLoadoutKey((ComponentSlot)index))
                .Concat(new[] { CoreGameplayDemoController.ComponentInventoryKey }).ToArray();
            foreach (string key in keys)
                if (PlayerPrefs.HasKey(key)) saved[key] = PlayerPrefs.GetString(key);
            bool hadSelectedShip = PlayerPrefs.HasKey(CoreGameplayDemoController.SelectedShipKey);
            int selectedShip = PlayerPrefs.GetInt(CoreGameplayDemoController.SelectedShipKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 0);

            GameObject host = new GameObject("Component Loadout Regression Host");
            host.SetActive(false); // Avoid scene/audio/editor-preview lifecycle; exercise only the loadout seam.
            try
            {
                CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
                CoreVoyageSession session = new CoreVoyageSession();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(CoreGameplayDemoController).GetField("componentArt", flags).SetValue(controller, catalog);
                typeof(CoreGameplayDemoController).GetField("session", flags).SetValue(controller, session);
                MethodInfo restore = typeof(CoreGameplayDemoController).GetMethod("RestoreComponentLoadout", flags);
                Assert.That(restore, Is.Not.Null);

                foreach (ComponentArtEntry entry in catalog.Entries)
                {
                    foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(entry.Slot), entry.Id);
                    restore.Invoke(controller, null);
                    ComponentArtEntry baseline = catalog.Get((int)entry.Slot * ComponentArtCatalog.EntriesPerSlot);

                    Assert.That(controller.EquippedComponent(entry.Slot).Id, Is.EqualTo(entry.Id), entry.Id);
                    Assert.That(controller.EquippedComponent(entry.Slot).ShipLayer, Is.SameAs(entry.ShipLayer), entry.Id);
                    Assert.That(session.MaxHull, Is.EqualTo(session.SelectedShip.MaxHull + entry.HullBonus - baseline.HullBonus), entry.Id);
                    Assert.That(session.Armor, Is.EqualTo(session.SelectedShip.Armor + entry.ArmorBonus - baseline.ArmorBonus), entry.Id);
                    Assert.That(session.Firepower, Is.EqualTo(session.SelectedShip.Firepower + entry.FirepowerBonus - baseline.FirepowerBonus), entry.Id);
                    Assert.That(session.SpeedKnots, Is.EqualTo(session.SelectedShip.SpeedKnots + entry.SpeedBonus - baseline.SpeedBonus).Within(0.0001f), entry.Id);
                    Assert.That(session.VisionBonusNm, Is.EqualTo(entry.VisionBonus - baseline.VisionBonus).Within(0.0001f), entry.Id);
                }

                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                PlayerPrefs.SetString(keys[(int)ComponentSlot.Bow], "removed_or_corrupt_component");
                PlayerPrefs.SetString(keys[(int)ComponentSlot.Sail], "cannon_bronze_longgun");
                restore.Invoke(controller, null);
                Assert.That(controller.EquippedComponent(ComponentSlot.Bow), Is.Null,
                    "A clean profile with only corrupt legacy fitting keys must remain a bare hull.");
                Assert.That(controller.EquippedComponent(ComponentSlot.Sail), Is.Null,
                    "A valid component ID saved to the wrong slot must not create a free fitting.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                foreach (string key in keys) PlayerPrefs.DeleteKey(key);
                foreach (KeyValuePair<string, string> pair in saved) PlayerPrefs.SetString(pair.Key, pair.Value);
                if (hadSelectedShip) PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, selectedShip);
                else PlayerPrefs.DeleteKey(CoreGameplayDemoController.SelectedShipKey);
                PlayerPrefs.Save();
            }
        }
    }
}
