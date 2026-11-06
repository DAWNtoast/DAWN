using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // All three pages share one cabinet opening and summary strip.
        // Keep these measurements together so a tab cannot acquire a wider header.
        private const float WarehouseLedgerWidth = 596f;
        private static readonly Vector2 WarehouseCargoTileSize = new Vector2(188f, 168f);
        private const float WarehouseCargoGridTop = 210f;
        private const float WarehouseCargoRowGap = 12f;
        private static readonly Vector2 WarehouseSummarySize = new Vector2(WarehouseLedgerWidth, 76f);
        private static readonly Vector2 WarehouseSummaryPosition = new Vector2(0f, 254f);
        private static readonly Color WarehousePaperRuleColor = new Color(.48f, .36f, .22f, .42f);
        private WarehouseRoomTheme WarehouseRoomTheme => WarehouseRoomArtCatalog.ForPort(CurrentWarehousePort);

        private RectTransform BuildWarehouseSummary(RectTransform ledger)
        {
            RectTransform summary = WarehousePanel("Warehouse Summary Bar", ledger,
                WarehouseSummarySize, WarehouseSummaryPosition, Color.clear);
            ApplyWarehousePlain(summary.GetComponent<Image>(), new Color(.64f, .49f, .29f, .08f));
            WarehousePaperRule("Warehouse Summary Top Rule", summary,
                new Vector2(WarehouseLedgerWidth, 1f), new Vector2(0f, 38f));
            WarehousePaperRule("Warehouse Summary Bottom Rule", summary,
                new Vector2(WarehouseLedgerWidth, 1f), new Vector2(0f, -38f));
            return summary;
        }

        private void BuildWarehouseCargoLocationSelector(RectTransform ledger)
        {
            RectTransform summary = BuildWarehouseSummary(ledger);
            Button stored = WarehouseAction("Warehouse Inventory Location Stored", summary,
                new Vector2(278f, 52f), new Vector2(-151f, 0f), "本港仓库",
                () => SelectWarehouseCargoLocation(false));
            Button carried = WarehouseAction("Warehouse Inventory Location Carried", summary,
                new Vector2(278f, 52f), new Vector2(151f, 0f), "船舱",
                () => SelectWarehouseCargoLocation(true));
            WoodButtonSkin.SetSelected(stored, !warehouseViewingCarriedCargo);
            WoodButtonSkin.SetSelected(carried, warehouseViewingCarriedCargo);
            stored.interactable = carried.interactable = Application.isPlaying && !editorPreviewBuilding;
        }

        private void BuildWarehouseStorageHint(string name, RectTransform ledger, string value)
        {
            const int fontSize = 18;
            Text hint = WarehouseLabel(name, ledger, new Vector2(WarehouseLedgerWidth, 40f),
                new Vector2(0f, -350f), fontSize, value, WarehouseRoomTheme.Muted);
            hint.resizeTextForBestFit = true;
            hint.resizeTextMinSize = 14;
            hint.resizeTextMaxSize = fontSize;
        }

        private void SelectWarehouseCargoLocation(bool carried)
        {
            if (!Application.isPlaying || editorPreviewBuilding || !warehouseStorageOpen ||
                activeWarehouseCargoQuote != null || portWarehouseInventoryView == null ||
                !portWarehouseInventoryView.gameObject.activeInHierarchy || warehouseViewingCarriedCargo == carried) return;
            warehouseViewingCarriedCargo = carried;
            Render();
        }

        private RectTransform WarehouseLinedPanel(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform panel = WarehousePanel(name, parent, size, position, Color.clear);
            ApplyWarehousePlain(panel.GetComponent<Image>(), new Color(1f, .98f, .91f, .32f));
            WarehouseBrassRim(name + " Rim", panel, size, false);
            return panel;
        }

        private void WarehousePaperRule(string name, Transform parent, Vector2 size, Vector2 position)
        {
            Image rule = WarehouseImage(name, parent, size, position, WarehousePaperRuleColor);
            ApplyWarehousePlain(rule, WarehousePaperRuleColor);
        }

        private static void ApplyWarehousePlain(Image image, Color color)
        {
            image.sprite = null;
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }

        private void WarehouseBrassRim(string name, Transform parent, Vector2 size, bool selected)
        {
            Image rim = WarehouseImage(name, parent, size, Vector2.zero, Color.clear);
            ApplyWarehousePlain(rim, Color.clear);
            float thickness = selected ? 2f : 1f;
            WarehousePaperRule(name + " Top", rim.transform, new Vector2(size.x, thickness), new Vector2(0f, size.y * .5f));
            WarehousePaperRule(name + " Bottom", rim.transform, new Vector2(size.x, thickness), new Vector2(0f, -size.y * .5f));
            WarehousePaperRule(name + " Left", rim.transform, new Vector2(thickness, size.y), new Vector2(-size.x * .5f, 0f));
            WarehousePaperRule(name + " Right", rim.transform, new Vector2(thickness, size.y), new Vector2(size.x * .5f, 0f));
        }

        private void StyleWarehouseInventoryButton(Button button, bool selected = false)
        {
            MarketShopSkin.ApplyChoiceRow(button, selected);
            button.GetComponent<Image>().raycastTarget = true;
            // The light control already owns its fine outline. Avoid layering the
            // retired heavy wood rim over its item art and labels.
        }

        private Button BuildWarehouseCargoTile(RectTransform ledger, bool rare, int index, string key,
            string itemName, int stored, int carried)
        {
            string prefix = rare ? "Warehouse Rare" : "Warehouse Storage";
            float halfHeight = WarehouseCargoTileSize.y * .5f;
            Button tile = WarehouseButton(prefix + " Row " + key, ledger, WarehouseCargoTileSize,
                new Vector2(-204f + index % 3 * 204f,
                    WarehouseCargoGridTop - halfHeight - index / 3 * (WarehouseCargoTileSize.y + WarehouseCargoRowGap)),
                WarehouseRoomTheme.Lining, WarehouseRoomTheme.Ink, 19);
            tile.GetComponentInChildren<Text>().text = string.Empty;
            StyleWarehouseInventoryButton(tile);
            WarehouseCargoTileLabel(prefix + " Name " + key, tile.transform, new Vector2(172f, 22f),
                new Vector2(0f, halfHeight - 18f), 18, itemName, WarehouseRoomTheme.Ink);
            WarehouseItemIcon(prefix + " Icon " + key, tile.transform, ItemIconCatalog.ForCargo(key),
                new Vector2(0f, 4f), 96f);
            int amount = warehouseViewingCarriedCargo ? carried : stored;
            Text quantity = WarehouseCargoTileLabel(prefix + " Quantity " + key, tile.transform,
                new Vector2(152f, 24f), new Vector2(10f, -halfHeight + 20f), 22,
                amount.ToString(CultureInfo.InvariantCulture), WarehouseRoomTheme.Ink);
            quantity.alignment = TextAnchor.MiddleRight;
            return tile;
        }

        private Text WarehouseCargoTileLabel(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, string value, Color color)
        {
            Text label = WarehouseLabel(name, parent, size, position, fontSize, value, color);
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = fontSize;
            return label;
        }
    }
}
