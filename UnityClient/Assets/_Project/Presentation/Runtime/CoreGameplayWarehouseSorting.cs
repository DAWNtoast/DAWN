using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Sorting is view state only. The first column restores catalog order.
        private int warehouseCargoSortColumn;
        private bool warehouseCargoSortAscending;

        private int[] WarehouseCargoOrder(int count, Func<int, double> value)
        {
            var order = new int[count];
            for (int index = 0; index < count; index++) order[index] = index;
            if (warehouseCargoSortColumn == 0) return order;
            Array.Sort(order, (left, right) =>
            {
                int comparison = warehouseCargoSortAscending
                    ? value(left).CompareTo(value(right)) : value(right).CompareTo(value(left));
                return comparison != 0 ? comparison : left.CompareTo(right);
            });
            return order;
        }

        private void BuildWarehouseSortHeader(string name, RectTransform parent,
            Vector2 size, Vector2 position, string title, int column)
        {
            bool selected = warehouseCargoSortColumn == column;
            string suffix = column == 0 ? " ↺" : selected ? warehouseCargoSortAscending ? " ↑" : " ↓" : string.Empty;
            Button button = WarehouseButton(name, parent, size, position,
                selected ? new Color(.65f, .44f, .20f) : new Color(.32f, .22f, .12f), WarehouseRoomTheme.Ink, 18);
            MarketShopSkin.ApplyLightButton(button, selected);
            Text label = button.GetComponentInChildren<Text>();
            label.text = title + suffix;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 18;
            label.raycastTarget = false;
            button.interactable = Application.isPlaying && !editorPreviewBuilding;
            AddWarehouseAction(button, () =>
            {
                if (!Application.isPlaying || editorPreviewBuilding || activeWarehouseCargoQuote != null || portWarehouseInventoryView == null ||
                    !portWarehouseInventoryView.gameObject.activeInHierarchy) return;
                warehouseCargoSortAscending = column != 0 && selected && !warehouseCargoSortAscending;
                warehouseCargoSortColumn = column;
                Render();
            });
        }
    }
}
