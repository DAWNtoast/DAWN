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
            BuildWarehouseCargoSummary(ledger, "Warehouse Storage Summary", port + " · 独立仓储");
            BuildWarehouseCargoGrid(ledger, false);

            string[] ids = { "Name", "Stored", "Carried", "Volume" };
            string[] titles = { "物资", "本港库存", "船舱数量", "船舱占用" };
            float[] positions = WarehouseCargoColumnX;
            float[] widths = WarehouseCargoColumnWidths;
            for (int column = 0; column < ids.Length; column++)
                BuildWarehouseSortHeader("Warehouse Storage Header " + ids[column], ledger,
                    new Vector2(widths[column] - 2f, 40f), new Vector2(positions[column], 190f), titles[column], column);

            int[] order = WarehouseCargoOrder(PortMarketCatalog.AllItems.Count, index =>
            {
                PortMarketItem cargo = PortMarketCatalog.AllItems[index];
                string cargoKey = PortMarketCatalog.Key(cargo);
                if (warehouseCargoSortColumn == 1) return session.PortWarehouse.GetAmount(port, cargoKey);
                int amount = session.GetMarketCargoAmount(cargoKey);
                return warehouseCargoSortColumn == 2 ? amount : amount * (double)PortMarketCatalog.CargoVolume(cargo);
            });
            for (int rowIndex = 0; rowIndex < order.Length; rowIndex++)
            {
                PortMarketItem item = PortMarketCatalog.AllItems[order[rowIndex]];
                string key = PortMarketCatalog.Key(item);
                int carried = session.GetMarketCargoAmount(key);
                int stored = session.PortWarehouse.GetAmount(port, key);
                Button row = WarehouseButton("Warehouse Storage Row " + key, ledger,
                    new Vector2(WarehouseLedgerWidth, 50f), new Vector2(0f, 144f - rowIndex * 52f),
                    WarehouseRoomTheme.Lining, WarehouseRoomTheme.Ink, 19);
                row.GetComponentInChildren<Text>().text = string.Empty;
                StyleWarehouseInventoryButton(row);
                WarehouseItemIcon("Warehouse Storage Icon " + key, row.transform, ItemIconCatalog.ForCargo(key),
                    new Vector2(-265f, 0f), 42f);
                string[] values = { PortMarketCatalog.Name(item), stored.ToString(CultureInfo.InvariantCulture),
                    carried.ToString(CultureInfo.InvariantCulture), WarehouseVolume(carried * (double)PortMarketCatalog.CargoVolume(item)) };
                for (int column = 0; column < ids.Length; column++)
                {
                    Text label = WarehouseLabel("Warehouse Storage " + ids[column] + " " + key, row.transform,
                        new Vector2(column == 0 ? 130f : widths[column] - 8f, 40f),
                        new Vector2(column == 0 ? -169f : positions[column], 0f), 19,
                        values[column], WarehouseRoomTheme.Ink);
                    label.raycastTarget = false;
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 12;
                    label.resizeTextMaxSize = 19;
                }
                row.interactable = Application.isPlaying && !editorPreviewBuilding && WarehouseCargoActionsAllowed;
                AddWarehouseAction(row, () => OpenWarehouseCargoActions(item, carried, stored));
            }
            Text hint = WarehouseLabel("Warehouse Storage Hint", ledger, new Vector2(WarehouseLedgerWidth, 52f),
                new Vector2(0f, -330f), 18, !WarehouseCargoActionsAllowed
                    ? "教学期间仅可查看库存；结束后可存取或出售。"
                    : portWarehouseNotice, WarehouseRoomTheme.Muted);
            hint.resizeTextForBestFit = true;
            hint.resizeTextMinSize = 14;
            hint.resizeTextMaxSize = 18;
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
