using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // All three pages share one cabinet opening, summary strip and table grid.
        // Keep these measurements together so a tab cannot acquire a wider header.
        private const float WarehouseLedgerWidth = 596f;
        private static readonly Vector2 WarehouseSummarySize = new Vector2(WarehouseLedgerWidth, 76f);
        private static readonly Vector2 WarehouseSummaryPosition = new Vector2(0f, 254f);
        private static readonly float[] WarehouseCargoColumnX = { -195f, -30f, 98f, 231f };
        private static readonly float[] WarehouseCargoColumnWidths = { 206f, 124f, 132f, 134f };
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

        private void BuildWarehouseCargoSummary(RectTransform ledger, string name, string title)
        {
            RectTransform summary = BuildWarehouseSummary(ledger);
            Text heading = WarehouseLabel(name, summary, new Vector2(264f, 52f),
                new Vector2(-149f, 0f), 20, title, WarehouseRoomTheme.Brass);
            heading.alignment = TextAnchor.MiddleLeft;
            heading.resizeTextForBestFit = true;
            heading.resizeTextMinSize = 16;
            heading.resizeTextMaxSize = 20;
            Text capacity = WarehouseLabel("Warehouse Cargo Capacity", summary, new Vector2(292f, 30f),
                new Vector2(136f, 15f), 18, "随船货舱 " + WarehouseVolume(session.Cargo.UsedVolume) +
                " / " + WarehouseVolume(session.CargoCapacityValue), WarehouseRoomTheme.Ink);
            capacity.alignment = TextAnchor.MiddleRight;
            capacity.resizeTextForBestFit = true;
            capacity.resizeTextMinSize = 14;
            capacity.resizeTextMaxSize = 18;
            const float trackWidth = 286f;
            Image track = WarehouseImage("Warehouse Cargo Capacity Track", summary,
                new Vector2(trackWidth, 10f), new Vector2(136f, -15f), new Color(.76f, .64f, .43f));
            ApplyWarehousePlain(track, new Color(.76f, .64f, .43f));
            float ratio = session.CargoCapacityValue > 0f
                ? Mathf.Clamp01(session.Cargo.UsedVolume / session.CargoCapacityValue) : 0f;
            float fillWidth = trackWidth * ratio;
            Image fill = WarehouseImage("Warehouse Cargo Capacity Fill", track.transform,
                new Vector2(fillWidth, 10f), new Vector2(-(trackWidth - fillWidth) * .5f, 0f),
                ratio >= .95f ? new Color(.59f, .30f, .13f) : new Color(.35f, .25f, .16f));
            ApplyWarehousePlain(fill, fill.color);
            fill.enabled = ratio > 0f;
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
            MarketShopSkin.ApplyLightButton(button, selected);
            button.GetComponent<Image>().raycastTarget = true;
            // The light control already owns its fine outline. Avoid layering the
            // retired heavy wood rim over its item art and labels.
        }

        private void BuildWarehouseCargoGrid(RectTransform ledger, bool rare)
        {
            float height = rare ? 470f : 508f;
            WarehouseLinedPanel("Warehouse Cargo Table", ledger, new Vector2(WarehouseLedgerWidth, height),
                new Vector2(0f, 210f - height * .5f));
            // Dividers share the exact column boundaries used by both inventories.
            float[] boundaries = { -92f, 32f, 164f };
            foreach (float boundary in boundaries)
            {
                Image divider = WarehouseImage("Warehouse Cargo Divider " + boundary, ledger,
                    new Vector2(1f, height), new Vector2(boundary, 210f - height * .5f),
                    WarehousePaperRuleColor);
                ApplyWarehousePlain(divider, divider.color);
            }
        }
    }
}
