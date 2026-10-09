using System;
using System.Collections.Generic;
using System.Globalization;
using ShatteredSail.Core.Random;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    public sealed class PortComponentShopStockItem
    {
        public PortComponentShopStockItem(string id, string portName, string refreshKey, ComponentArtEntry entry, float quality, int price, int refreshRevision = 0, string refreshScope = "", int investmentLevel = 0)
        {
            Id = id;
            PortName = portName;
            RefreshKey = refreshKey;
            Entry = entry;
            Quality = quality;
            Price = price;
            RefreshRevision = refreshRevision;
            RefreshScope = refreshScope ?? string.Empty;
            InvestmentLevel = Math.Max(0, investmentLevel);
        }

        public string Id { get; }
        public string PortName { get; }
        /// <summary>Game-month identity for the live shop batch.</summary>
        public string RefreshKey { get; }

        /// <summary>Legacy property name kept for save and test source compatibility.</summary>
        public string MonthKey => RefreshKey;
        public ComponentArtEntry Entry { get; }
        public float Quality { get; }
        public int Price { get; }
        public int RefreshRevision { get; }
        public string RefreshScope { get; }
        /// <summary>Investment level that unlocks this individual monthly offer.</summary>
        public int InvestmentLevel { get; }
    }

    /// <summary>
    /// Mirrors the design document's regional mystery-component market. Stock is
    /// deterministic for a port and game month. Investment adds offers to the
    /// existing batch without changing previously available items or receipts.
    /// </summary>
    public static class PortComponentShopCatalog
    {
        private sealed class PortRule
        {
            public PortRule(string canonicalName, float priceMultiplier, params ComponentSlot[] slots)
            {
                CanonicalName = canonicalName;
                PriceMultiplier = priceMultiplier;
                Slots = slots;
            }

            public string CanonicalName { get; }
            public float PriceMultiplier { get; }
            public ComponentSlot[] Slots { get; }
        }

        private static readonly PortRule BrokenAnchor = new PortRule(
            "断锚港", 0.95f, ComponentSlot.Bow, ComponentSlot.Plating, ComponentSlot.Cannon);

        private static readonly Dictionary<string, PortRule> Rules = new Dictionary<string, PortRule>(StringComparer.Ordinal)
        {
            { "断锚港", BrokenAnchor },
            { "白贝港", new PortRule("白贝港", 0.92f, ComponentSlot.Sail, ComponentSlot.Instrument, ComponentSlot.Bow) },
            { "铜钟港", new PortRule("铜钟港", 1.00f, ComponentSlot.Plating, ComponentSlot.Cannon, ComponentSlot.Bow) },
            { "盐冠港", new PortRule("盐冠港", 1.08f, ComponentSlot.Cannon, ComponentSlot.Bow, ComponentSlot.Sail) },
            { "暮潮港", new PortRule("暮潮港", 1.12f, ComponentSlot.Instrument, ComponentSlot.Sail, ComponentSlot.Bow) },

            // The integrated tutorial currently graduates at 灰鸥港. It is the
            // inherits 白贝港's supply economy, but owns a distinct inventory
            // seed and purchase IDs: buying in one harbor must not sell out the other.
            { "灰鸥港", new PortRule("灰鸥港", 0.92f, ComponentSlot.Sail, ComponentSlot.Instrument, ComponentSlot.Bow) },
        };

        public static IReadOnlyList<ComponentSlot> PreferredSlots(string portName)
        {
            return ResolveRule(portName).Slots;
        }

        public static string PreferredSlotLabel(string portName)
        {
            ComponentSlot[] slots = ResolveRule(portName).Slots;
            return ComponentArtCatalog.SlotName(slots[0]) + " / " +
                ComponentArtCatalog.SlotName(slots[1]) + " / " +
                ComponentArtCatalog.SlotName(slots[2]);
        }

        public static string MonthKey(DateTime date)
        {
            return date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        /// <summary>Legacy name retained for source compatibility; component stock is monthly.</summary>
        public static string DayKey(DateTime date)
        {
            return MonthKey(date);
        }

        public static string NextRefreshLabel(DateTime date)
        {
            return PortMarketCatalog.NextRefreshLabel(date);
        }

        public static int StockCount(int investmentLevel)
        {
            return 6 + 2 * Math.Max(0, Math.Min(PortInvestmentCatalog.MaxLevel, investmentLevel));
        }

        public static bool IsCurrentOffer(PortComponentShopStockItem offer, string portName, DateTime date)
        {
            return IsCurrentOffer(offer, portName, date, 0);
        }

        public static bool IsCurrentOffer(PortComponentShopStockItem offer, string portName, DateTime date, int refreshRevision, string refreshScope = "")
        {
            return IsCurrentOffer(offer, portName, date, refreshRevision, refreshScope, -1);
        }

        public static bool IsCurrentOffer(PortComponentShopStockItem offer, string portName, DateTime date, int refreshRevision, string refreshScope, int investmentLevel)
        {
            return offer != null && !string.IsNullOrWhiteSpace(portName) &&
                refreshRevision >= 0 && offer.RefreshRevision == refreshRevision &&
                (refreshRevision == 0 || string.Equals(offer.RefreshScope, refreshScope ?? string.Empty, StringComparison.Ordinal)) &&
                (investmentLevel < 0 || offer.InvestmentLevel <= Math.Min(PortInvestmentCatalog.MaxLevel, investmentLevel)) &&
                string.Equals(offer.PortName, portName.Trim(), StringComparison.Ordinal) &&
                string.Equals(offer.RefreshKey, MonthKey(date), StringComparison.Ordinal);
        }

        public static IReadOnlyList<PortComponentShopStockItem> BuildStock(
            ComponentArtCatalog art,
            string portName,
            DateTime date)
        {
            return BuildStockInternal(art, portName, date.Year, date.Month, 0, 0, string.Empty, 0f, 0, false);
        }

        /// <summary>Builds the live shop batch from the expanded 150-item market.</summary>
        public static IReadOnlyList<PortComponentShopStockItem> BuildExpandedStock(
            ComponentArtCatalog art, string portName, DateTime date)
        {
            return BuildStockInternal(art, portName, date.Year, date.Month, 0, 0, string.Empty, 0f, 0, true);
        }

        public static IReadOnlyList<PortComponentShopStockItem> BuildStock(
            ComponentArtCatalog art,
            string portName,
            DateTime date,
            int refreshRevision,
            string refreshScope = "")
        {
            return BuildStockInternal(art, portName, date.Year, date.Month, 0, refreshRevision, refreshScope, 0f, 0, false);
        }

        public static IReadOnlyList<PortComponentShopStockItem> BuildExpandedStock(
            ComponentArtCatalog art, string portName, DateTime date, int refreshRevision, string refreshScope = "")
        {
            return BuildStockInternal(art, portName, date.Year, date.Month, 0, refreshRevision, refreshScope, 0f, 0, true);
        }

        public static IReadOnlyList<PortComponentShopStockItem> BuildStock(
            ComponentArtCatalog art,
            string portName,
            DateTime date,
            int refreshRevision,
            string refreshScope,
            float rarityThresholdShift,
            int investmentLevel)
        {
            return BuildStockInternal(art, portName, date.Year, date.Month, 0, refreshRevision, refreshScope, rarityThresholdShift, investmentLevel, false);
        }

        public static IReadOnlyList<PortComponentShopStockItem> BuildExpandedStock(
            ComponentArtCatalog art,
            string portName,
            DateTime date,
            int refreshRevision,
            string refreshScope,
            float rarityThresholdShift,
            int investmentLevel)
        {
            return BuildStockInternal(art, portName, date.Year, date.Month, 0, refreshRevision, refreshScope, rarityThresholdShift, investmentLevel, true);
        }

        public static IReadOnlyList<PortComponentShopStockItem> BuildStock(
            ComponentArtCatalog art,
            string portName,
            int year,
            int month)
        {
            return BuildStockInternal(art, portName, year, month, 0, 0, string.Empty, 0f, 0, false);
        }

        public static IReadOnlyList<PortComponentShopStockItem> BuildStock(
            ComponentArtCatalog art,
            string portName,
            int year,
            int month,
            int refreshRevision,
            string refreshScope = "")
        {
            return BuildStockInternal(art, portName, year, month, 0, refreshRevision, refreshScope, 0f, 0, false);
        }

        public static IReadOnlyList<PortComponentShopStockItem> BuildStock(
            ComponentArtCatalog art,
            string portName,
            int year,
            int month,
            int refreshRevision,
            string refreshScope,
            float rarityThresholdShift,
            int investmentLevel)
        {
            return BuildStockInternal(art, portName, year, month, 0, refreshRevision, refreshScope, rarityThresholdShift, investmentLevel, false);
        }

        private static IReadOnlyList<PortComponentShopStockItem> BuildStockInternal(
            ComponentArtCatalog art,
            string portName,
            int year,
            int month,
            int day,
            int refreshRevision,
            string refreshScope,
            float rarityThresholdShift,
            int investmentLevel,
            bool expanded)
        {
            if (art == null)
            {
                throw new ArgumentNullException(nameof(art));
            }
            if (refreshRevision < 0) throw new ArgumentOutOfRangeException(nameof(refreshRevision));

            int safeMonth = Math.Max(1, Math.Min(12, month));
            string refreshKey = MonthKey(new DateTime(year, safeMonth, 1));
            PortRule rule = ResolveRule(portName);
            // Explicit GM batches have separate identities but never alter the clock.
            // Investment must not enter the batch seed: upgrading can only append.
            int safeInvestmentLevel = Math.Max(0, Math.Min(PortInvestmentCatalog.MaxLevel, investmentLevel));
            string batchSuffix = (expanded ? "|monthly-market-v3" : "|monthly-v3") +
                (refreshRevision == 0 ? string.Empty : "|gm-" + refreshRevision);
            Mulberry32Random random = new Mulberry32Random(
                SeedHash.Compute("MYSTERY-SHOP::" + rule.CanonicalName + "::" + refreshKey + batchSuffix));
            int count = StockCount(safeInvestmentLevel);
            uint portHash = SeedHash.Compute(rule.CanonicalName);
            List<PortComponentShopStockItem> stock = new List<PortComponentShopStockItem>(count);
            HashSet<string> selectedIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < count; index += 1)
            {
                int unlockLevel = index < StockCount(0) ? 0 : 1 + (index - StockCount(0)) / 2;
                // Each pair has the quality bonus of its unlock tier. Applying the
                // current level to the whole batch would reroll already bought items.
                float offerRarityShift = PortInvestmentCatalog.RarityThresholdShift(unlockLevel);
                ComponentSlot slot = rule.Slots[(int)Math.Floor(random.NextDouble() * rule.Slots.Length)];
                ComponentRarity rarity = RollRarity(random.NextDouble(), offerRarityShift);
                ComponentArtEntry entry = FindEntry(art, slot, rarity, random, selectedIds, expanded, rule.Slots);
                selectedIds.Add(entry.Id);
                float quality = (float)(Math.Round((0.9d + random.NextDouble() * 0.2d) * 100d) / 100d);
                int price = CalculatePrice(entry, rule.PriceMultiplier, quality);
                string scopeSuffix = refreshRevision > 0 && !string.IsNullOrEmpty(refreshScope) ? "|save-" + refreshScope : string.Empty;
                string id = "mystery|" + refreshKey + "|" + portHash + "|" + index + batchSuffix + scopeSuffix;
                stock.Add(new PortComponentShopStockItem(id, (portName ?? rule.CanonicalName).Trim(), refreshKey, entry, quality, price, refreshRevision, refreshScope, unlockLevel));
            }

            return stock;
        }

        public static int CalculatePrice(ComponentArtEntry entry, string portName, float quality)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return CalculatePrice(entry, ResolveRule(portName).PriceMultiplier, quality);
        }

        public static int CalculateSalePrice(ComponentArtEntry entry, string portName, float quality)
        {
            return CalculatePrice(entry, portName, quality) / 2;
        }

        private static int CalculatePrice(ComponentArtEntry entry, float portMultiplier, float quality)
        {
            float safeQuality = Math.Max(0.9f, Math.Min(1.1f, quality));
            float qualityMultiplier = 1f + (safeQuality - 1f) * 0.5f;
            float rawPrice = entry.Price * GameplayBalance.ComponentPriceMultiplier((int)entry.Rarity) * SlotMultiplier(entry.Slot) * portMultiplier * qualityMultiplier;
            return Math.Max(10, (int)(Math.Floor(rawPrice / 10f + 0.5f) * 10f));
        }

        private static PortRule ResolveRule(string portName)
        {
            PortRule rule;
            return !string.IsNullOrWhiteSpace(portName) && Rules.TryGetValue(portName.Trim(), out rule)
                ? rule
                : BrokenAnchor;
        }

        private static ComponentArtEntry FindEntry(
            ComponentArtCatalog art,
            ComponentSlot slot,
            ComponentRarity rarity,
            Mulberry32Random random,
            HashSet<string> selectedIds,
            bool expanded,
            ComponentSlot[] preferredSlots)
        {
            List<ComponentArtEntry> candidates = new List<ComponentArtEntry>(ComponentArtCatalog.EntriesPerRarityPerSlot);
            // The live UI opts into the expanded market explicitly.
            IReadOnlyList<ComponentArtEntry> marketEntries = expanded ? art.ExpandedEntries : art.Entries;
            for (int index = 0; index < marketEntries.Count; index += 1)
            {
                ComponentArtEntry entry = marketEntries[index];
                if (entry.Slot == slot && entry.Rarity == rarity && !selectedIds.Contains(entry.Id))
                {
                    candidates.Add(entry);
                }
            }

            if (candidates.Count == 0)
            {
                // A developed port can have sixteen distinct offers. Exhausting
                // one slot/rarity pool must not break the batch or duplicate goods.
                foreach (ComponentArtEntry entry in marketEntries)
                    if (entry.Rarity == rarity && Array.IndexOf(preferredSlots, entry.Slot) >= 0 &&
                        !selectedIds.Contains(entry.Id)) candidates.Add(entry);
            }
            if (candidates.Count == 0)
            {
                foreach (ComponentArtEntry entry in marketEntries)
                    if (Array.IndexOf(preferredSlots, entry.Slot) >= 0 &&
                        !selectedIds.Contains(entry.Id)) candidates.Add(entry);
            }

            int selectedIndex = Math.Min(
                candidates.Count - 1,
                (int)Math.Floor(random.NextDouble() * candidates.Count));
            return candidates[selectedIndex];
        }

        private static ComponentRarity RollRarity(double value, float rarityThresholdShift = 0f)
        {
            double shift = Math.Max(0, Math.Min(0.12, rarityThresholdShift));
            if (value < 0.600 - shift) return ComponentRarity.Common;
            if (value < 0.870 - shift) return ComponentRarity.Uncommon;
            if (value < 0.970 - shift) return ComponentRarity.Rare;
            if (value < 0.999 - shift * 0.25) return ComponentRarity.Epic;
            return ComponentRarity.Legendary;
        }

        private static float SlotMultiplier(ComponentSlot slot)
        {
            switch (slot)
            {
                case ComponentSlot.Sail: return 0.90f;
                case ComponentSlot.Cannon: return 1.20f;
                case ComponentSlot.Plating: return 1.15f;
                case ComponentSlot.Instrument: return 0.85f;
                default: return 1.00f;
            }
        }
    }
}
