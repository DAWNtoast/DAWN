namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Fixed recovery prices shared by all ports. Rare goods are never market purchase stock.</summary>
    public static class CoreRareItemEconomy
    {
        public static decimal SaleUnitPrice(string item)
        {
            CoreWarehouseItemDefinition definition = CoreWarehouseItemCatalog.Find(item);
            if (definition == null || !definition.IsRare) return 0m;
            switch (definition.Key)
            {
                case "gem": return 60m;
                case "ancientmaterial": return 8m;
                case "relic": return 100m;
                case "blackpearl": return 120m;
                case "ancientcompass": return 75m;
                case "lantern": return 20m;
                default: return 0m;
            }
        }
    }
}
