using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Port-specific construction materials, accumulated in each port's own
    /// warehouse across voyages. Construction benefits remain local to the port.
    /// </summary>
    public readonly struct PortInvestmentCost
    {
        public PortInvestmentCost(int gold, int wood, int iron, PortMarketItem requiredItem, int requiredQuantity,
            IReadOnlyList<CorePortInvestmentRequirement> rareMaterials = null)
        {
            Gold = Math.Max(0, gold);
            Wood = Math.Max(0, wood);
            Iron = Math.Max(0, iron);
            RequiredItem = requiredItem;
            RequiredQuantity = Math.Max(0, requiredQuantity);
            RareMaterials = rareMaterials ?? Array.Empty<CorePortInvestmentRequirement>();
        }

        public int Gold { get; }
        public int Wood { get; }
        public int Iron { get; }
        public PortMarketItem RequiredItem { get; }
        public int RequiredQuantity { get; }
        public IReadOnlyList<CorePortInvestmentRequirement> RareMaterials { get; }
        public bool IsEmpty => Gold == 0 && Wood == 0 && Iron == 0 && RequiredQuantity == 0 &&
            (RareMaterials == null || RareMaterials.Count == 0);
    }

    public static class PortInvestmentCatalog
    {
        public const int MaxLevel = 5;
        private const float HighQualityBaseChance = 0.13f;
        private const float HighQualityChancePerLevel = 0.015f;

        private static readonly IReadOnlyDictionary<string, string> Names =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "断锚港", "联合船坞" },
                { "白贝港", "航海公会" },
                { "铜钟港", "火炮工坊" },
                { "盐冠港", "远洋仓储" },
                { "暮潮港", "月潮观测站" },
                { "灰鸥港", "灰鸥航务站" },
            };

        private static readonly int[] GoldCosts = { 0, 600, 1000, 1600, 2500, 3800 };
        // Higher tiers deliberately exceed a single hold: players can deliver
        // several loads to the local warehouse before committing construction.
        private static readonly int[] WoodCosts = { 0, 30, 50, 80, 120, 180 };
        private static readonly int[] IronCosts = { 0, 5, 12, 25, 45, 70 };
        private static readonly PortMarketItem[] RequiredItems =
        {
            PortMarketItem.Medicine, PortMarketItem.Medicine,
            PortMarketItem.Cloth, PortMarketItem.Cloth,
            PortMarketItem.Beer, PortMarketItem.Cannonballs,
        };
        private static readonly int[][] RequiredQuantities =
        {
            new[] { 0, 6, 10, 14, 19, 25 },
            new[] { 0, 8, 12, 16, 22, 28 },
            new[] { 0, 4, 7, 10, 14, 18 },
            new[] { 0, 5, 9, 13, 18, 24 },
            new[] { 0, 15, 25, 35, 45, 60 },
            new[] { 0, 5, 8, 12, 16, 20 },
        };
        private static readonly string[] FirstRareKeys =
            { "ancientmaterial", "gem", "ancientmaterial", "gem", "lantern", "ancientcompass" };
        private static readonly int[] FirstRareTierFour = { 4, 1, 4, 1, 1, 1 };
        private static readonly int[] FirstRareTierFive = { 8, 2, 8, 2, 2, 1 };
        private static readonly string[] SecondRareKeys =
            { "relic", "ancientcompass", "blackpearl", "relic", "blackpearl", "lantern" };

        public static string NameFor(string portName)
        {
            return !string.IsNullOrWhiteSpace(portName) && Names.TryGetValue(portName.Trim(), out string name)
                ? name
                : "联合船坞";
        }

        public static PortInvestmentCost CostForLevel(string portName, int level)
        {
            int safe = Math.Max(0, Math.Min(MaxLevel, level));
            int port = PortIndex(portName);
            return new PortInvestmentCost(GoldCosts[safe], WoodCosts[safe], IronCosts[safe],
                RequiredItems[port], RequiredQuantities[port][safe], RareRequirements(port, safe));
        }

        private static IReadOnlyList<CorePortInvestmentRequirement> RareRequirements(int port, int level)
        {
            if (level < 4) return Array.Empty<CorePortInvestmentRequirement>();
            if (level == 4) return Array.AsReadOnly(new[]
            {
                new CorePortInvestmentRequirement(FirstRareKeys[port], FirstRareTierFour[port]),
            });
            return Array.AsReadOnly(new[]
            {
                new CorePortInvestmentRequirement(FirstRareKeys[port], FirstRareTierFive[port]),
                new CorePortInvestmentRequirement(SecondRareKeys[port], 1),
            });
        }

        public static PortMarketItem RequiredItemFor(string portName) => RequiredItems[PortIndex(portName)];

        public static string SuggestedSourcePort(string portName)
        {
            string key = PortMarketCatalog.Key(RequiredItemFor(portName));
            string source = SupportedPorts[0];
            decimal lowest = decimal.MaxValue;
            foreach (string port in SupportedPorts)
            {
                decimal price = CorePortEconomy.LocalPriceMultiplier(port, key);
                if (price < lowest) { lowest = price; source = port; }
            }
            return source;
        }

        private static int PortIndex(string portName)
        {
            string canonical = PortMarketCatalog.CanonicalPortName(portName);
            for (int i = 0; i < SupportedPorts.Count; i++)
                if (SupportedPorts[i] == canonical) return i;
            return 0;
        }

        public static float HighQualityChance(int level)
        {
            int safe = Math.Max(0, Math.Min(MaxLevel, level));
            return HighQualityBaseChance + safe * HighQualityChancePerLevel;
        }

        public static float HighQualityChanceBonus(int level)
        {
            return HighQualityChance(Math.Max(0, Math.Min(MaxLevel, level))) - HighQualityBaseChance;
        }

        public static float RarityThresholdShift(int level)
        {
            return HighQualityChanceBonus(level);
        }

        public static IReadOnlyList<string> SupportedPorts => new[]
        {
            "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港",
        };
    }
}
