using System;
using System.Collections.Generic;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    // Values deliberately match the three authored equipment rarity tiers.
    public enum CoreBoardingComponentRarity { Rare = 2, Epic = 3, Legendary = 4 }

    /// <summary>A pending physical reward. Selection never depends on UI time or log count.</summary>
    public sealed class CoreBoardingComponentDrop
    {
        public CoreBoardingComponentDrop(int sequence, int slot, CoreBoardingComponentRarity rarity,
            uint selectionSeed, string componentId, string sourceEncounterId, bool isBossEquipment)
        {
            Sequence = sequence; Slot = slot; Rarity = rarity; SelectionSeed = selectionSeed;
            ComponentId = componentId ?? string.Empty; SourceEncounterId = sourceEncounterId ?? string.Empty;
            IsBossEquipment = isBossEquipment;
        }
        public int Sequence { get; }
        public int Slot { get; }
        public CoreBoardingComponentRarity Rarity { get; }
        public uint SelectionSeed { get; }
        public string ComponentId { get; }
        public string SourceEncounterId { get; }
        public bool IsBossEquipment { get; }
    }

    public static class CoreBoardingLootBalance
    {
        public const double BossEquipmentDropChance = .5d;
        public const double RareWeight = .5d;
        public const double EpicWeight = .4d;
        public const double LegendaryWeight = .1d;

        public static CoreBoardingComponentRarity RarityForRoll(double roll) => roll < RareWeight
            ? CoreBoardingComponentRarity.Rare : roll < RareWeight + EpicWeight
                ? CoreBoardingComponentRarity.Epic : CoreBoardingComponentRarity.Legendary;

        public static int ComponentSlot(string componentId)
        {
            if (string.IsNullOrEmpty(componentId)) return -1;
            string[] prefixes = { "bow_", "sail_", "cannon_", "plating_", "instrument_" };
            for (int slot = 0; slot < prefixes.Length; slot++)
                if (componentId.StartsWith(prefixes[slot], StringComparison.Ordinal)) return slot;
            return -1;
        }

        public static IReadOnlyList<string> BossEquipment(string encounterId, out CoreBoardingComponentRarity rarity)
        {
            CoreRouteBoss route = CoreRouteBossCatalog.Find(encounterId);
            CoreLegendaryWarship legendary = CoreLegendaryWarships.Find(encounterId);
            rarity = route != null ? CoreBoardingComponentRarity.Epic : CoreBoardingComponentRarity.Legendary;
            IReadOnlyList<string> authored = route != null ? route.ComponentIds : legendary?.ComponentIds;
            var result = new List<string>();
            if (authored != null)
                foreach (string id in authored)
                    if (ComponentSlot(id) >= 0 && !result.Contains(id)) result.Add(id);
            result.Sort(StringComparer.Ordinal);
            return result.AsReadOnly();
        }
    }

    [Serializable]
    public sealed class CoreBoardingComponentDropCheckpoint
    {
        public int sequence, slot, rarity;
        public uint selectionSeed;
        public string componentId, sourceEncounterId;
        public bool isBossEquipment;
    }

    public sealed partial class CoreVoyageSession
    {
        private readonly List<CoreBoardingComponentDrop> pendingBoardingComponents = new List<CoreBoardingComponentDrop>();
        private int nextBoardingComponentSequence = 1;

        public bool TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop drop)
        {
            ImportLegacyBoardingComponentDrop();
            drop = pendingBoardingComponents.Count > 0 ? pendingBoardingComponents[0] : null;
            return drop != null;
        }

        /// <summary>Call only after the physical item has been granted successfully.</summary>
        public bool AcknowledgeBoardingComponentDrop(int sequence)
        {
            if (!TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop first) || first.Sequence != sequence) return false;
            pendingBoardingComponents.RemoveAt(0);
            SyncLegacyBoardingComponentDrop();
            return true;
        }

        // Compatibility for existing callers that consume only the old slot reward.
        public bool TryConsumeBoardingComponentDrop(out int slot)
        {
            if (!TryPeekBoardingComponentDrop(out CoreBoardingComponentDrop drop)) { slot = -1; return false; }
            slot = drop.Slot;
            return AcknowledgeBoardingComponentDrop(drop.Sequence);
        }

        private void QueueBoardingVictoryLoot(bool captured, string encounterId, double rewardMultiplier)
        {
            ImportLegacyBoardingComponentDrop();
            if (!captured || double.IsNaN(rewardMultiplier) || double.IsInfinity(rewardMultiplier) || rewardMultiplier <= 0d) return;
            double multiplier = Math.Min(1d, rewardMultiplier);
            Mulberry32Random random = voyageRandom ?? (voyageRandom = new Mulberry32Random(0u));
            IReadOnlyList<string> bossEquipment = CoreBoardingLootBalance.BossEquipment(encounterId, out CoreBoardingComponentRarity bossRarity);
            // Roll the two opportunities before selecting either item. A failed ordinary
            // drop never prevents the independent, real boss-equipment opportunity.
            bool ordinary = random.NextDouble() < BoardingComponentDropChance * multiplier;
            bool boss = bossEquipment.Count > 0 && random.NextDouble() < CoreBoardingLootBalance.BossEquipmentDropChance * multiplier;
            if (ordinary)
                EnqueueBoardingComponentDrop((int)(random.NextDouble() * ComponentSlotCountForLoot()),
                    CoreBoardingLootBalance.RarityForRoll(random.NextDouble()), random.NextUInt(), string.Empty, encounterId, false);
            if (boss)
            {
                string id = bossEquipment[(int)(random.NextDouble() * bossEquipment.Count)];
                EnqueueBoardingComponentDrop(CoreBoardingLootBalance.ComponentSlot(id), bossRarity,
                    random.NextUInt(), id, encounterId, true);
            }
        }

        private void EnqueueBoardingComponentDrop(int slot, CoreBoardingComponentRarity rarity, uint seed,
            string componentId, string encounterId, bool bossEquipment)
        {
            pendingBoardingComponents.Add(new CoreBoardingComponentDrop(nextBoardingComponentSequence++, slot,
                rarity, seed, componentId, encounterId, bossEquipment));
            SyncLegacyBoardingComponentDrop();
        }

        private void SyncLegacyBoardingComponentDrop()
        {
            pendingBoardingComponentDrop = pendingBoardingComponents.Count > 0;
            pendingBoardingComponentSlot = pendingBoardingComponentDrop ? pendingBoardingComponents[0].Slot : -1;
        }

        private void ImportLegacyBoardingComponentDrop()
        {
            if (pendingBoardingComponents.Count != 0 || !pendingBoardingComponentDrop || pendingBoardingComponentSlot < 0) return;
            // Older checkpoints had no rarity or selection seed. Preserve their rare
            // reward without drawing from (or advancing) the restored voyage stream.
            EnqueueBoardingComponentDrop(pendingBoardingComponentSlot, CoreBoardingComponentRarity.Rare,
                LegacyBoardingSelectionSeed(), string.Empty, string.Empty, false);
        }

        private uint LegacyBoardingSelectionSeed() =>
            (voyageRandom?.State ?? 0u) ^ (uint)pendingBoardingComponentSlot ^ 0xB04D1A6u;

        private CoreBoardingComponentDropCheckpoint[] CaptureBoardingComponentDrops()
        {
            // Capturing is read-only, including compatibility with an unconverted
            // legacy slot. Restore/peek can materialize it later without rerolling.
            if (pendingBoardingComponents.Count == 0 && pendingBoardingComponentDrop)
                return new[] { new CoreBoardingComponentDropCheckpoint { sequence = nextBoardingComponentSequence,
                    slot = pendingBoardingComponentSlot, rarity = (int)CoreBoardingComponentRarity.Rare,
                    selectionSeed = LegacyBoardingSelectionSeed(), componentId = string.Empty,
                    sourceEncounterId = string.Empty, isBossEquipment = false } };
            var result = new CoreBoardingComponentDropCheckpoint[pendingBoardingComponents.Count];
            for (int i = 0; i < result.Length; i++)
            {
                CoreBoardingComponentDrop drop = pendingBoardingComponents[i];
                result[i] = new CoreBoardingComponentDropCheckpoint { sequence = drop.Sequence, slot = drop.Slot,
                    rarity = (int)drop.Rarity, selectionSeed = drop.SelectionSeed, componentId = drop.ComponentId,
                    sourceEncounterId = drop.SourceEncounterId, isBossEquipment = drop.IsBossEquipment };
            }
            return result;
        }

        private bool RestoreBoardingComponentDrops(CoreVoyageCheckpoint checkpoint)
        {
            if (checkpoint.boardingLootVersion < 0 || checkpoint.boardingLootVersion > 1) return false;
            if (checkpoint.boardingLootVersion == 0)
            {
                if (checkpoint.nextBoardingComponentSequence != 0 || checkpoint.pendingBoardingComponents != null &&
                    checkpoint.pendingBoardingComponents.Length != 0) return false;
                pendingBoardingComponentDrop = checkpoint.pendingBoardingComponentDrop;
                pendingBoardingComponentSlot = checkpoint.pendingBoardingComponentSlot;
                ImportLegacyBoardingComponentDrop();
                return true;
            }
            CoreBoardingComponentDropCheckpoint[] saved = checkpoint.pendingBoardingComponents;
            if (saved == null || saved.Length > 65536 || checkpoint.nextBoardingComponentSequence < 1 ||
                checkpoint.nextBoardingComponentSequence >= int.MaxValue) return false;
            var restored = new List<CoreBoardingComponentDrop>(saved.Length);
            int previous = 0;
            foreach (CoreBoardingComponentDropCheckpoint drop in saved)
            {
                if (drop == null || drop.sequence <= previous || drop.sequence >= checkpoint.nextBoardingComponentSequence ||
                    drop.slot < 0 || drop.slot >= ComponentSlotCountForLoot() ||
                    !Enum.IsDefined(typeof(CoreBoardingComponentRarity), drop.rarity) ||
                    drop.componentId == null || drop.componentId.Length > 256 ||
                    drop.sourceEncounterId == null || drop.sourceEncounterId.Length > 256) return false;
                if (drop.isBossEquipment)
                {
                    IReadOnlyList<string> equipment = CoreBoardingLootBalance.BossEquipment(drop.sourceEncounterId, out CoreBoardingComponentRarity rarity);
                    bool known = false;
                    foreach (string id in equipment) if (id == drop.componentId) { known = true; break; }
                    if (!known || (int)rarity != drop.rarity || CoreBoardingLootBalance.ComponentSlot(drop.componentId) != drop.slot) return false;
                }
                else if (drop.componentId.Length != 0) return false;
                restored.Add(new CoreBoardingComponentDrop(drop.sequence, drop.slot, (CoreBoardingComponentRarity)drop.rarity,
                    drop.selectionSeed, drop.componentId, drop.sourceEncounterId, drop.isBossEquipment));
                previous = drop.sequence;
            }
            if (checkpoint.pendingBoardingComponentDrop != (restored.Count > 0) ||
                checkpoint.pendingBoardingComponentSlot != (restored.Count > 0 ? restored[0].Slot : -1)) return false;
            pendingBoardingComponents.Clear(); pendingBoardingComponents.AddRange(restored);
            nextBoardingComponentSequence = checkpoint.nextBoardingComponentSequence;
            SyncLegacyBoardingComponentDrop();
            return true;
        }

        private void CopyBoardingComponentDrops(CoreVoyageSession source)
        {
            pendingBoardingComponents.Clear(); pendingBoardingComponents.AddRange(source.pendingBoardingComponents);
            nextBoardingComponentSequence = source.nextBoardingComponentSequence;
            SyncLegacyBoardingComponentDrop();
        }
    }
}
