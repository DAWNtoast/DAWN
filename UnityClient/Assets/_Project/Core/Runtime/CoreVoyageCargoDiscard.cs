using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>
        /// Discards physical cargo at a stable sailing/encounter boundary. Voyage
        /// gains are discarded before departure supplies; only the discarded
        /// departure portion reduces that item's settlement protection baseline.
        /// </summary>
        public bool TryDiscardVoyageCargo(string item, int quantity)
        {
            if ((Phase != CoreDemoPhase.Sailing && Phase != CoreDemoPhase.Encounter) || quantity <= 0 ||
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo) || !CoreEconomyBalance.HasNonNegativeCargo(departureCargo))
                return false;
            CoreWarehouseItemDefinition definition = CoreWarehouseItemCatalog.Find(item);
            if (definition == null) return false;
            int current = definition.Read(Cargo);
            if (quantity > current) return false;

            int baseline = definition.Read(departureCargo);
            int voyageGain = Math.Max(0, current - baseline);
            int discardedDepartureSupply = Math.Max(0, quantity - voyageGain);
            // All operands are validated nonnegative ints, quantity <= current,
            // so discardedDepartureSupply cannot exceed baseline. Do not clamp
            // baseline to remaining cargo: that would also erase the protection
            // gap caused by earlier normal consumption of water, food or ammo.
            definition.Write(Cargo, current - quantity);
            definition.Write(departureCargo, baseline - discardedDepartureSupply);
            AddLog("主动舍弃随船" + definition.Name + " ×" + quantity + "。");
            return true;
        }
    }
}
