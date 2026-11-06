using System;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool warehouseCargoStorageOpen;
        private bool warehouseRareStorageOpen;
        private bool warehouseViewingCarriedCargo;
        // Existing investment/tutorial entry points select basic cargo. A rare
        // page is selected explicitly only after opening the warehouse section.
        private bool warehouseStorageOpen
        {
            get => warehouseCargoStorageOpen;
            set { warehouseCargoStorageOpen = value; warehouseRareStorageOpen = false; }
        }
        private bool WarehouseCargoActionsAllowed => !tutorialActive || GuidedTutorialRecoveryActive;
        private string portWarehouseNotice = string.Empty;
        private RectTransform portWarehouseInventoryView;

        private void BuildPortWarehouseStorage(RectTransform ledger)
        {
            InvalidateWarehouseCargoDialog();
            portWarehouseInventoryView = ledger;
            string port = CurrentWarehousePort;
            BuildWarehouseCargoLocationSelector(ledger);
            for (int rowIndex = 0; rowIndex < PortMarketCatalog.AllItems.Count; rowIndex++)
            {
                PortMarketItem item = PortMarketCatalog.AllItems[rowIndex];
                string key = PortMarketCatalog.Key(item);
                int carried = session.GetMarketCargoAmount(key);
                int stored = session.PortWarehouse.GetAmount(port, key);
                Button row = BuildWarehouseCargoTile(ledger, false, rowIndex, key, PortMarketCatalog.Name(item),
                    stored, carried);
                row.interactable = Application.isPlaying && !editorPreviewBuilding && WarehouseCargoActionsAllowed;
                AddWarehouseAction(row, () => OpenWarehouseCargoActions(item, carried, stored));
            }
            BuildWarehouseStorageHint("Warehouse Storage Hint", ledger, !WarehouseCargoActionsAllowed
                    ? "教学期间仅可查看库存；结束后可存取或出售。"
                    : portWarehouseNotice);
        }

        private static string WarehouseVolume(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        // The editor sync builds sample pages from a staging controller. This
        // entry point never reads or writes player preferences or live sessions.
        public void ConfigureEditorPreviewWarehouseStorage(int state)
        {
            if (Application.isPlaying || session == null) return;
            session.Cargo.Wood = 18;
            string port = CurrentWarehousePort;
            session.PortWarehouse.Restore("v1;" + port + ",water,600;" + port + ",food,500;" + port +
                ",beer,40;" + port + ",medicine,24;" + port + ",wood,120;" + port + ",iron,35;" + port +
                ",cloth,42;" + port + ",powder,30;" + port + ",cannonballs,50");
            activePortFacility = 0;
            warehouseViewedSession = session;
            warehouseViewedPort = port;
            warehouseStorageOpen = true;
            portWarehouseNotice = string.Empty;
            Render();
            if (state == 1 || state == 2)
            {
                WarehouseCargoQuote quote = CreateWarehouseCargoQuote(PortMarketItem.Wood, true);
                activeWarehouseCargoQuote = quote;
                if (state == 1) BuildWarehouseCargoAdjustment(quote, Math.Min(6, quote.MaxWithdrawal));
                else BuildWarehouseCargoSale(quote, Math.Min(12, quote.Stored), Math.Min(4, quote.Carried));
            }
        }
    }
}
