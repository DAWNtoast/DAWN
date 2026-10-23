using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public readonly struct CorePortInvestmentRequirement
    {
        public CorePortInvestmentRequirement(string key, int quantity)
        {
            Key = (key ?? string.Empty).Trim().ToLowerInvariant();
            Quantity = quantity;
        }

        public string Key { get; }
        public int Quantity { get; }
    }

    public sealed partial class CoreVoyageSession
    {
        public bool TrySpendPortInvestment(decimal gold, int wood, int iron, string requiredItem, int requiredQuantity)
            => TrySpendPortInvestment(gold, wood, iron, requiredItem, requiredQuantity, null);

        public bool TrySpendPortInvestment(decimal gold, int wood, int iron, string requiredItem, int requiredQuantity,
            IReadOnlyList<CorePortInvestmentRequirement> rareMaterials)
        {
            string key = (requiredItem ?? string.Empty).Trim().ToLowerInvariant();
            // Validate the complete investment before changing any balance. The
            // extra cargo must be distinct from its two fixed material costs.
            if (Phase != CoreDemoPhase.Port || gold <= 0 || !CoreEconomyBalance.IsValidGoldPayment(gold) || wood <= 0 || iron <= 0 || requiredQuantity <= 0 ||
                !CorePortWarehouse.TryNormalizePort(CurrentPort, out _) ||
                !IsMarketMaterial(key) || key == "wood" || key == "iron" ||
                ProfileGold < gold || !CoreEconomyBalance.HasNonNegativeCargo(Cargo) ||
                GetPortInvestmentMaterialAmount("wood") < wood || GetPortInvestmentMaterialAmount("iron") < iron ||
                GetPortInvestmentMaterialAmount(key) < requiredQuantity)
                return false;

            // Copy validated requirements so all balances are checked before any
            // debit, including rare cargo shared with saved treasure progress.
            var donations = new List<CorePortInvestmentRequirement>();
            var distinct = new HashSet<string>();
            if (rareMaterials != null)
            {
                foreach (CorePortInvestmentRequirement donation in rareMaterials)
                {
                    CoreWarehouseItemDefinition item = CoreWarehouseItemCatalog.Find(donation.Key);
                    if (item == null || !item.IsRare || donation.Quantity <= 0 || !distinct.Add(item.Key) ||
                        GetPortInvestmentMaterialAmount(item.Key) < donation.Quantity)
                        return false;
                    donations.Add(new CorePortInvestmentRequirement(item.Key, donation.Quantity));
                }
            }

            ProfileGold -= gold;
            SpendPortInvestmentMaterial("wood", wood);
            SpendPortInvestmentMaterial("iron", iron);
            SpendPortInvestmentMaterial(key, requiredQuantity);
            string rareLog = string.Empty;
            foreach (CorePortInvestmentRequirement donation in donations)
            {
                SpendPortInvestmentMaterial(donation.Key, donation.Quantity);
                rareLog += "、" + CoreWarehouseItemCatalog.Find(donation.Key).Name + " -" + donation.Quantity;
            }
            AddLog("港口投资：金币 -" + gold + "、木材 -" + wood + "、铁矿 -" + iron +
                "、" + MarketMaterialName(key) + " -" + requiredQuantity + rareLog + "（优先使用本港仓库）。");
            return true;
        }

        private void SpendPortInvestmentMaterial(string key, int quantity)
        {
            int stored = System.Math.Min(quantity, PortWarehouse.GetAmount(CurrentPort, key));
            // All distinct basic and rare materials have been validated together.
            if (stored > 0) PortWarehouse.TryRemove(CurrentPort, key, stored);
            if (quantity > stored) ChangeWarehouseCargo(key, stored - quantity);
        }
    }
}
