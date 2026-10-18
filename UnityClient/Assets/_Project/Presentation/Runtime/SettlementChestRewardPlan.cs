using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    public enum SettlementChestRewardKind
    {
        Gold, Wood, Iron, Fame, Fragments, PirateExperience, Gem, AncientMaterial, Relic, BlackPearl,
    }

    public sealed class SettlementChestRewardItem
    {
        public SettlementChestRewardKind Kind { get; }
        public string Id => Kind.ToString();
        public string Name { get; }
        public int Amount { get; }
        public long ValueWeight { get; }
        public bool IsJackpot { get; internal set; }

        internal SettlementChestRewardItem(SettlementChestRewardKind kind, string name, int amount, int unitWeight)
        {
            Kind = kind;
            Name = name;
            Amount = amount;
            ValueWeight = (long)amount * unitWeight;
        }
    }

    /// <summary>
    /// A snapshot of the actual banked rewards, for presentation only. Weights decide reveal emphasis;
    /// they are deliberately independent from trading prices and never change cargo, balances or saves.
    /// </summary>
    public sealed class SettlementChestRewardPlan
    {
        public const long JackpotTotalValueThreshold = 5000L;
        public const long JackpotSingleValueThreshold = 3500L;
        public const int GoldWeight = 1;
        public const int WoodWeight = 10;
        public const int IronWeight = 18;
        public const int FameWeight = 12;
        public const int FragmentsWeight = 120;
        public const int PirateExperienceWeight = 2;
        public const int GemWeight = 350;
        public const int AncientMaterialWeight = 160;
        public const int RelicWeight = 700;
        public const int BlackPearlWeight = 500;

        public IReadOnlyList<SettlementChestRewardItem> Items { get; }
        public bool IsFailure { get; }
        public bool IsJackpot { get; }
        public bool HasRewards => Items.Count > 0;
        public long TotalValue { get; }

        private SettlementChestRewardPlan(List<SettlementChestRewardItem> items, bool failed)
        {
            IsFailure = failed;
            long total = 0;
            int largest = -1;
            for (int i = 0; i < items.Count; i++)
            {
                total += items[i].ValueWeight;
                if (largest < 0 || items[i].ValueWeight > items[largest].ValueWeight) largest = i;
            }
            TotalValue = total;
            IsJackpot = !failed && largest >= 0 &&
                (total >= JackpotTotalValueThreshold || items[largest].ValueWeight >= JackpotSingleValueThreshold);
            if (IsJackpot)
            {
                // Stable order for everyday rewards; only the biggest value is saved for the finale.
                SettlementChestRewardItem finale = items[largest];
                finale.IsJackpot = true;
                items.RemoveAt(largest);
                items.Add(finale);
            }
            Items = items.AsReadOnly();
        }

        public static SettlementChestRewardPlan Create(VoyageSettlement settlement)
        {
            var items = new List<SettlementChestRewardItem>(10);
            if (settlement == null) return new SettlementChestRewardPlan(items, false);
            Add(items, SettlementChestRewardKind.Gold, "金币", settlement.BankedGold, GoldWeight);
            Add(items, SettlementChestRewardKind.Wood, "木材", settlement.BankedWood, WoodWeight);
            Add(items, SettlementChestRewardKind.Iron, "铁矿", settlement.BankedIron, IronWeight);
            Add(items, SettlementChestRewardKind.Fame, "声望", settlement.BankedFame, FameWeight);
            Add(items, SettlementChestRewardKind.Fragments, "藏宝图碎片", settlement.BankedFragments, FragmentsWeight);
            Add(items, SettlementChestRewardKind.PirateExperience, "海盗经验", settlement.BankedPirateExperience, PirateExperienceWeight);
            Add(items, SettlementChestRewardKind.Gem, "宝石", settlement.BankedGem, GemWeight);
            Add(items, SettlementChestRewardKind.AncientMaterial, "古代材料", settlement.BankedAncientMaterial, AncientMaterialWeight);
            Add(items, SettlementChestRewardKind.Relic, "稀有遗物", settlement.BankedRelic, RelicWeight);
            Add(items, SettlementChestRewardKind.BlackPearl, "黑珍珠", settlement.BankedBlackPearl, BlackPearlWeight);
            return new SettlementChestRewardPlan(items, settlement.Outcome == VoyageOutcome.Failure);
        }

        private static void Add(List<SettlementChestRewardItem> items, SettlementChestRewardKind kind,
            string name, int amount, int unitWeight)
        {
            if (amount > 0) items.Add(new SettlementChestRewardItem(kind, name, amount, unitWeight));
        }
    }
}
