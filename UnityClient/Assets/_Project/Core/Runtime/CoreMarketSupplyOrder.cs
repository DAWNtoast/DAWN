namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>Commits an externally quoted market order as one transaction.</summary>
        public bool TryPurchaseMarketSupplyOrder(VoyageSupplyOrder order, decimal totalCost)
        {
            if (Phase != CoreDemoPhase.Port || order == null || totalCost <= 0 || !CoreEconomyBalance.IsValidGoldPayment(totalCost) ||
                ProfileGold < totalCost || !CoreEconomyBalance.HasNonNegativeCargo(Cargo)) return false;

            double volume = Cargo.UsedVolume;
            long units = 0;
            foreach (VoyageSupplyType type in purchasableSupplies)
            {
                int amount = order[type];
                if (amount < 0 || amount > int.MaxValue - GetSupplyAmount(type)) return false;
                volume += amount * (double)SupplyVolume(type);
                units += amount;
            }
            if (units <= 0 || volume > CargoCapacityValue + .001d) return false;

            // No mutations precede complete validation. A later row cannot leave
            // an earlier one paid for or loaded when the combined order fails.
            ProfileGold -= totalCost;
            Cargo.Water += order.Water;
            Cargo.Food += order.Food;
            Cargo.Medicine += order.Medicine;
            Cargo.Wood += order.Wood;
            Cargo.Powder += order.Powder;
            Cargo.Cannonballs += order.Cannonballs;
            AddLog("整备市场采购完成：" + units + " 单位物资，金币 -" + totalCost + "。");
            return true;
        }
    }
}
