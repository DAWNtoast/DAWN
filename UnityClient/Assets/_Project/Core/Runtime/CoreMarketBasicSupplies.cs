using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>Loads a complete market quote directly into the ship hold, or changes nothing.</summary>
        public bool TryPurchaseMarketBasicSupplies(IReadOnlyDictionary<string, int> quantities, decimal totalCost)
        {
            if (Phase != CoreDemoPhase.Port || quantities == null || totalCost <= 0m ||
                !CoreEconomyBalance.IsValidGoldPayment(totalCost) || ProfileGold < totalCost ||
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo)) return false;

            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> line in quantities)
            {
                string key = (line.Key ?? string.Empty).Trim().ToLowerInvariant();
                if (!IsMarketMaterial(key) || line.Value < 0 || order.ContainsKey(key)) return false;
                order.Add(key, line.Value);
            }

            double volume = Cargo.UsedVolume;
            double capacity = CargoCapacityValue;
            if (double.IsNaN(volume) || double.IsInfinity(volume) ||
                double.IsNaN(capacity) || double.IsInfinity(capacity) || capacity < 0d) return false;

            long units = 0;
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.BasicItems)
            {
                int amount = order.TryGetValue(item.Key, out int value) ? value : 0;
                int current = item.Read(Cargo);
                if (amount < 0 || amount > int.MaxValue - current || item.UnitVolume < 0d ||
                    double.IsNaN(item.UnitVolume) || double.IsInfinity(item.UnitVolume)) return false;
                volume += amount * item.UnitVolume;
                units += amount;
            }
            if (units <= 0 || double.IsNaN(volume) || double.IsInfinity(volume) ||
                volume > capacity + .001d) return false;

            // All nine rows share a single payment and a single hold-capacity check.
            // No earlier row can be loaded when a later row is invalid.
            ProfileGold -= totalCost;
            foreach (CoreWarehouseItemDefinition item in CoreWarehouseItemCatalog.BasicItems)
                if (order.TryGetValue(item.Key, out int amount))
                    item.Write(Cargo, item.Read(Cargo) + amount);
            AddLog("出航基础物资采购完成：" + units + " 单位物资，金币 -" + totalCost + "。");
            return true;
        }
    }
}
