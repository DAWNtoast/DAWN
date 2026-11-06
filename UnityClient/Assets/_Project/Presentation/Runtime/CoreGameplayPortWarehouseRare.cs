using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildPortWarehouseRareStorage(RectTransform ledger)
        {
            InvalidateWarehouseCargoDialog();
            portWarehouseInventoryView = ledger;
            string port = CurrentWarehousePort;
            BuildWarehouseCargoLocationSelector(ledger);
            for (int rowIndex = 0; rowIndex < CoreWarehouseItemCatalog.RareItems.Count; rowIndex++)
            {
                CoreWarehouseItemDefinition item = CoreWarehouseItemCatalog.RareItems[rowIndex];
                string key = item.Key;
                int carried = session.GetWarehouseCargoAmount(key);
                int stored = session.PortWarehouse.GetAmount(port, key);
                Button row = BuildWarehouseCargoTile(ledger, true, rowIndex, key, item.Name,
                    stored, carried);
                row.interactable = Application.isPlaying && !editorPreviewBuilding && WarehouseCargoActionsAllowed;
                AddWarehouseAction(row, () => OpenWarehouseRareActions(key, carried, stored));
            }
            BuildWarehouseStorageHint("Warehouse Rare Hint", ledger, portWarehouseNotice);
        }

        private bool CanUseWarehouseRareInventory() => Application.isPlaying && !editorPreviewBuilding &&
            WarehouseCargoActionsAllowed && session != null && session.Phase == CoreDemoPhase.Port &&
            activePortFacility == 0 && warehouseStorageOpen && warehouseRareStorageOpen &&
            portWarehouseInventoryView != null && portWarehouseInventoryView.gameObject.activeInHierarchy;

        private void OpenWarehouseRareActions(string key, int carried, int stored)
        {
            if (!CanUseWarehouseRareInventory() || activeWarehouseCargoQuote != null) return;
            CoreWarehouseItemDefinition item = CoreWarehouseItemCatalog.Find(key);
            if (item == null || !item.IsRare) return;
            if (session.GetWarehouseCargoAmount(key) != carried || session.PortWarehouse.GetAmount(CurrentWarehousePort, key) != stored)
            {
                portWarehouseNotice = "库存已变化，请重新选择珍稀物品。";
                Render();
                return;
            }
            WarehouseCargoQuote quote = CreateWarehouseCargoQuote(key);
            activeWarehouseCargoQuote = quote;
            BuildWarehouseCargoActions(quote);
        }

        // Explicit art/layout fixtures only; never restore or write player prefs.
        public void ConfigureEditorPreviewWarehouseRare(int state)
        {
            if (Application.isPlaying || session == null) return;
            // This isolated layout fixture represents an already acquired merchant
            // vessel; it never changes player ownership or saves player preferences.
            // Its real 150-unit hold accommodates all six rare sample resources.
            if (!session.SelectShip(1)) throw new InvalidOperationException("Rare warehouse preview requires its merchant fixture.");
            session.Cargo.Gem = 2;
            session.Cargo.AncientMaterial = 5;
            session.Cargo.Relic = 1;
            session.Cargo.BlackPearl = 1;
            session.Cargo.AncientCompass = 1;
            session.Cargo.Lantern = 1;
            string port = CurrentWarehousePort;
            session.PortWarehouse.Restore("v2;" + port + ",gem,8;" + port + ",ancientmaterial,24;" + port +
                ",relic,3;" + port + ",blackpearl,4;" + port + ",ancientcompass,2;" + port + ",lantern,6");
            activePortFacility = 0;
            warehouseViewedSession = session;
            warehouseViewedPort = port;
            warehouseStorageOpen = true;
            warehouseRareStorageOpen = true;
            portWarehouseNotice = string.Empty;
            Render();
            if (state == 1)
            {
                WarehouseCargoQuote quote = CreateWarehouseCargoQuote("gem", true);
                activeWarehouseCargoQuote = quote;
                BuildWarehouseCargoAdjustment(quote, -quote.Carried);
            }
            else if (state == 2)
            {
                WarehouseCargoQuote quote = CreateWarehouseCargoQuote("gem", true);
                activeWarehouseCargoQuote = quote;
                BuildWarehouseCargoSale(quote, 2, 1);
            }
        }
    }
}
