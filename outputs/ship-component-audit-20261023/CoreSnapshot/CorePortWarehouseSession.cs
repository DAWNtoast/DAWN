using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public CorePortWarehouse PortWarehouse { get; } = new CorePortWarehouse();

        /// <summary>Quantity carried aboard; warehouse balances remain explicitly port-scoped.</summary>
        public int GetWarehouseCargoAmount(string item) => CoreWarehouseItemCatalog.Find(item)?.Read(Cargo) ?? 0;
        public int GetPortWarehouseCargoAmount(string item) => GetWarehouseCargoAmount(item);

        public bool TryDepositPortCargo(string item, int quantity)
        {
            if (Phase != CoreDemoPhase.Port || quantity <= 0 ||
                !CorePortWarehouse.TryNormalizePort(CurrentPort, out string port) ||
                !CorePortWarehouse.TryNormalizeMaterial(item, out string key) ||
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo) || GetWarehouseCargoAmount(key) < quantity)
                return false;
            // A full warehouse cannot remove anything from the ship.
            if (!PortWarehouse.TryAdd(port, key, quantity)) return false;
            ChangeWarehouseCargo(key, -quantity);
            AddLog("存入" + port + "仓库：" + CoreWarehouseItemCatalog.Find(key).Name + " ×" + quantity + "。");
            return true;
        }

        public bool TryWithdrawPortCargo(string item, int quantity)
        {
            if (Phase != CoreDemoPhase.Port || quantity <= 0 ||
                !CorePortWarehouse.TryNormalizePort(CurrentPort, out string port) ||
                !CorePortWarehouse.TryNormalizeMaterial(item, out string key) ||
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo) || quantity > int.MaxValue - GetWarehouseCargoAmount(key) ||
                PortWarehouse.GetAmount(port, key) < quantity ||
                (double)Cargo.UsedVolume + quantity * PortWarehouseCargoVolume(key) > CargoCapacityValue + 0.001d)
                return false;
            if (!PortWarehouse.TryRemove(port, key, quantity)) return false;
            ChangeWarehouseCargo(key, quantity);
            AddLog("从" + port + "仓库取出：" + CoreWarehouseItemCatalog.Find(key).Name + " ×" + quantity + "。");
            return true;
        }

        public long GetPortInvestmentMaterialAmount(string item)
        {
            if (!CorePortWarehouse.TryNormalizeMaterial(item, out string key)) return 0L;
            return (long)Math.Max(0, GetWarehouseCargoAmount(key)) + PortWarehouse.GetAmount(CurrentPort, key);
        }

        // Trusted Core volumes cannot be reduced by a UI caller.
        private static double PortWarehouseCargoVolume(string key) => CoreWarehouseItemCatalog.Find(key).UnitVolume;

        // Call only after complete transaction validation; no public mutation bypass.
        private void ChangeWarehouseCargo(string key, int delta)
        {
            CoreWarehouseItemDefinition item = CoreWarehouseItemCatalog.Find(key);
            item.Write(Cargo, item.Read(Cargo) + delta);
        }
    }
}
