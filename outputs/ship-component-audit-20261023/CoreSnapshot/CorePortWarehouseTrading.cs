using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public int GetMaxPortWarehouseWithdrawal(string item)
        {
            if (Phase != CoreDemoPhase.Port || !CorePortWarehouse.TryNormalizePort(CurrentPort, out string port) ||
                !CorePortWarehouse.TryNormalizeMaterial(item, out string key) ||
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo)) return 0;
            double freeVolume = (double)CargoCapacityValue - Cargo.UsedVolume + 0.001d;
            if (freeVolume <= 0d) return 0;
            double fits = Math.Floor(freeVolume / PortWarehouseCargoVolume(key));
            return (int)Math.Min(fits, Math.Min(PortWarehouse.GetAmount(port, key),
                int.MaxValue - GetWarehouseCargoAmount(key)));
        }

        public bool TryAdjustPortWarehouseCargo(string item, int signedDelta)
        {
            if (signedDelta == 0 || signedDelta == int.MinValue) return false;
            return signedDelta > 0 ? TryWithdrawPortCargo(item, signedDelta) : TryDepositPortCargo(item, -signedDelta);
        }

        public bool TrySellPortWarehouseCargo(string item, int storedQty, int shipQty, decimal revenue)
        {
            if (Phase != CoreDemoPhase.Port || storedQty < 0 || shipQty < 0 ||
                (storedQty == 0 && shipQty == 0) || revenue <= 0 || !CoreEconomyBalance.IsValidGoldAmount(revenue) ||
                !CorePortWarehouse.TryNormalizePort(CurrentPort, out string port) ||
                !CorePortWarehouse.TryNormalizeMaterial(item, out string key) ||
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo) ||
                ProfileGold + revenue > CoreEconomyBalance.MaximumProfileGold ||
                PortWarehouse.GetAmount(port, key) < storedQty || GetWarehouseCargoAmount(key) < shipQty)
                return false;
            CoreWarehouseItemDefinition definition = CoreWarehouseItemCatalog.Find(key);
            if (definition.IsRare)
            {
                // The caller cannot invent a rare recovery quote. Sum quantities in
                // wide arithmetic before pricing; both sources must use this fixed rate.
                decimal unitPrice = CoreRareItemEconomy.SaleUnitPrice(key);
                if (unitPrice <= 0m || revenue != unitPrice * ((long)storedQty + shipQty)) return false;
            }
            else if (!IsMarketMaterial(key)) return false;
            // Both sources and the account have been checked before either source is debited.
            if (storedQty > 0 && !PortWarehouse.TryRemove(port, key, storedQty)) return false;
            if (shipQty > 0) definition.Write(Cargo, definition.Read(Cargo) - shipQty);
            ProfileGold += revenue;
            AddLog((definition.IsRare ? "港口回收" : "港口市场出售") + definition.Name + "：本港仓库 -" + storedQty +
                "、船舱 -" + shipQty + "，金币 +" + revenue + "。");
            return true;
        }
    }
}
