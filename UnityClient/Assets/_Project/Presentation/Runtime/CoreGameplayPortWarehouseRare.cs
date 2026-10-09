using System;
using System.Globalization;
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
            WarehouseLabel("Warehouse Rare Summary", ledger, new Vector2(614f, 66f),
                new Vector2(0f, 258f), GameReadability.FontSize(18), port + " · 珍稀物品仓储\n随船货舱 " +
                WarehouseVolume(session.Cargo.UsedVolume) + " / " + WarehouseVolume(session.CargoCapacityValue), WarehouseInk);
            string[] ids = { "Name", "Stored", "Carried", "Volume" };
            string[] headers = { "珍稀物品", "本港库存", "船舱数量", "船舱占用" };
            float[] x = { -175f, -28f, 84f, 220f };
            float[] widths = { 182f, 104f, 104f, 152f };
            for (int column = 0; column < ids.Length; column++)
                BuildWarehouseSortHeader("Warehouse Rare Header " + ids[column], ledger, new Vector2(widths[column], 60f),
                    new Vector2(x[column], 190f), headers[column], column);
            var snapshots = new WarehouseCargoQuote[CoreWarehouseItemCatalog.RareItems.Count];
            bool hasCarriedRare = false;
            int[] order = WarehouseCargoOrder(CoreWarehouseItemCatalog.RareItems.Count, index =>
            {
                CoreWarehouseItemDefinition cargo = CoreWarehouseItemCatalog.RareItems[index];
                if (warehouseCargoSortColumn == 1) return session.PortWarehouse.GetAmount(port, cargo.Key);
                int amount = session.GetWarehouseCargoAmount(cargo.Key);
                return warehouseCargoSortColumn == 2 ? amount : amount * cargo.UnitVolume;
            });
            RectTransform rows = CreateWarehouseReadableRows(ledger, "Warehouse Rare", order.Length, true);
            for (int rowIndex = 0; rowIndex < order.Length; rowIndex++)
            {
                int index = order[rowIndex];
                CoreWarehouseItemDefinition item = CoreWarehouseItemCatalog.RareItems[index];
                string key = item.Key;
                int carried = session.GetWarehouseCargoAmount(key);
                hasCarriedRare |= carried > 0;
                int stored = session.PortWarehouse.GetAmount(port, key);
                snapshots[index] = CreateWarehouseCargoQuote(key, editorPreviewBuilding || !Application.isPlaying);
                Button row = CreateButton("Warehouse Rare Row " + key, rows,
                    new Vector2(614f, WarehouseReadableRowHeight), Vector2.zero,
                    rowIndex % 2 == 0 ? new Color(.20f, .12f, .06f) : new Color(.26f, .16f, .08f), Ink, 17);
                AnchorWarehouseReadableRow(row, rowIndex);
                row.GetComponentInChildren<Text>().text = string.Empty;
                CreateItemIcon("Warehouse Rare Icon " + key, row.transform, ItemIconCatalog.ForCargo(key),
                    new Vector2(-286f, 0f), 32f);
                string[] values = { item.Name, stored.ToString(CultureInfo.InvariantCulture),
                    carried.ToString(CultureInfo.InvariantCulture), WarehouseVolume(carried * item.UnitVolume) };
                for (int column = 0; column < ids.Length; column++)
                {
                    Text label = WarehouseLabel("Warehouse Rare " + ids[column] + " " + key, row.transform,
                        new Vector2(widths[column], WarehouseReadableRowHeight - 8f), new Vector2(x[column], 0f), GameReadability.FontSize(22),
                        values[column], column == 0 ? Gold : Ink);
                    label.raycastTarget = false;
                }
                row.interactable = Application.isPlaying && !editorPreviewBuilding && WarehouseCargoActionsAllowed;
                AddWarehouseAction(row, () => OpenWarehouseRareActions(key, carried, stored));
            }
            bool consumed = false;
            Button all = WarehouseAction("Warehouse Store All Rare", ledger, new Vector2(600f, 60f),
                new Vector2(0f, -224f), "珍稀物品全部存入本港", () =>
                {
                    if (consumed || !CanUseWarehouseRareInventory() || activeWarehouseCargoQuote != null) return;
                    foreach (WarehouseCargoQuote quote in snapshots)
                    {
                        if (!ReferenceEquals(quote.View, portWarehouseInventoryView) || !WarehouseQuoteInventoryUnchanged(quote))
                        {
                            portWarehouseNotice = "库存或货舱状态已变化，请重新确认存仓。";
                            Render();
                            return;
                        }
                    }
                    consumed = true;
                    if (!session.TryDepositAllRarePortCargo())
                        portWarehouseNotice = "存仓未完成；请检查随船珍稀物品及本港库存上限。";
                    else
                    {
                        // SaveProgress queues both port inventory and carried
                        // treasure cargo before its final PlayerPrefs.Save.
                        SaveProgress();
                        portWarehouseNotice = "六类随船珍稀物品已全部存入" + port + "，船舱已腾出空间。";
                    }
                    Render();
                });
            if (!hasCarriedRare) all.GetComponentInChildren<Text>().text = "没有随船珍稀物品";
            all.interactable = hasCarriedRare && Application.isPlaying && !editorPreviewBuilding && WarehouseCargoActionsAllowed &&
                session.CanDepositAllRarePortCargo();
            Text hint = WarehouseLabel("Warehouse Rare Hint", ledger, new Vector2(614f, 82f),
                new Vector2(0f, -308f), 17, (string.IsNullOrEmpty(portWarehouseNotice)
                    ? "选择物品存取或出售；各港回收价相同。\n高阶捐献和海上事件也需珍稀物品，请酌情保留。"
                    : portWarehouseNotice) + "\n航海灯随船增加藏宝侦测，最多4盏生效。", WarehouseInk);
            ReadableParagraph(hint, ledger, new Vector2(614f, 92f), new Vector2(0f, -313f), 18, true);
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
