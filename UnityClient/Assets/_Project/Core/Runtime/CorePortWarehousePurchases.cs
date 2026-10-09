namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public bool TryPurchasePortWarehouseCargo(string item, int quantity, decimal cost)
        {
            if (Phase != CoreDemoPhase.Port || quantity <= 0 || cost <= 0 || !CoreEconomyBalance.IsValidGoldPayment(cost) || ProfileGold < cost ||
                !CorePortWarehouse.TryNormalizePort(CurrentPort, out string port) ||
                !CorePortWarehouse.TryNormalizeMaterial(item, out string key) || !IsMarketMaterial(key)) return false;
            // Delivery goes directly to the local warehouse and never occupies the ship.
            if (!PortWarehouse.TryAdd(port, key, quantity)) return false;
            ProfileGold -= cost;
            AddLog("港口市场采购" + MarketMaterialName(key) + " ×" + quantity +
                "，直接存入" + port + "仓库：金币 -" + cost + "。");
            return true;
        }

        public bool CanDepositAllPortCargo() => CanDepositPortCargoGroup(false);
        public bool CanDepositAllRarePortCargo() => CanDepositPortCargoGroup(true);

        private bool CanDepositPortCargoGroup(bool rareOnly)
        {
            if (Phase != CoreDemoPhase.Port || !CorePortWarehouse.TryNormalizePort(CurrentPort, out string port) ||
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo)) return false;
            foreach (CoreWarehouseItemDefinition item in rareOnly ? CoreWarehouseItemCatalog.RareItems : CoreWarehouseItemCatalog.All)
                if (GetWarehouseCargoAmount(item.Key) > int.MaxValue - PortWarehouse.GetAmount(port, item.Key)) return false;
            return true;
        }

        public bool TryDepositAllPortCargo() => TryDepositPortCargoGroup(false);
        public bool TryDepositAllRarePortCargo() => TryDepositPortCargoGroup(true);

        private bool TryDepositPortCargoGroup(bool rareOnly)
        {
            // Check every destination balance before moving the first item.
            if (!CanDepositPortCargoGroup(rareOnly)) return false;
            bool transferred = false;
            foreach (CoreWarehouseItemDefinition item in rareOnly ? CoreWarehouseItemCatalog.RareItems : CoreWarehouseItemCatalog.All)
            {
                string key = item.Key;
                int quantity = GetWarehouseCargoAmount(key);
                if (quantity == 0) continue;
                PortWarehouse.TryAdd(CurrentPort, key, quantity);
                ChangeWarehouseCargo(key, -quantity);
                transferred = true;
            }
            if (transferred) AddLog((rareOnly ? "船舱珍稀物品" : "船舱全部货物") + "已存入" + CurrentPort + "仓库。");
            return true;
        }
    }
}
