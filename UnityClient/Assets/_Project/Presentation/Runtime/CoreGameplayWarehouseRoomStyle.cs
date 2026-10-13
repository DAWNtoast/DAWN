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
        private WarehouseRoomTheme WarehouseRoomTheme => WarehouseRoomArtCatalog.ForPort(CurrentWarehousePort);

        private RectTransform BuildWarehouseSummary(RectTransform ledger)
        {
            RectTransform summary = WarehousePanel("Warehouse Summary Bar", ledger,
                WarehouseSummarySize, WarehouseSummaryPosition, Color.clear);
            ApplyWarehouseWood(summary.GetComponent<Image>());
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
                new Vector2(trackWidth, 20f), new Vector2(136f, -15f), new Color(.025f, .038f, .042f));
            ApplyWarehousePlain(track, new Color(.025f, .038f, .042f));
            float ratio = session.CargoCapacityValue > 0f
                ? Mathf.Clamp01(session.Cargo.UsedVolume / session.CargoCapacityValue) : 0f;
            float fillWidth = (trackWidth - 6f) * ratio;
            Image fill = WarehouseImage("Warehouse Cargo Capacity Fill", track.transform,
                new Vector2(fillWidth, 14f), new Vector2(-(trackWidth - 6f - fillWidth) * .5f, 0f),
                ratio >= .95f ? new Color(.73f, .44f, .18f) : new Color(.06f, .51f, .55f));
            ApplyWarehousePlain(fill, fill.color);
            fill.enabled = ratio > 0f;
            WarehouseBrassRim("Warehouse Cargo Capacity Rim", track.transform, track.rectTransform.sizeDelta, false);
        }

        private RectTransform WarehouseLinedPanel(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform panel = WarehousePanel(name, parent, size, position, Color.clear);
            ApplyWarehouseLining(panel.GetComponent<Image>(), WarehouseRoomTheme.Lining);
            WarehouseBrassRim(name + " Rim", panel, size, false);
            return panel;
        }

        private void ApplyWarehouseWood(Image image)
        {
            image.sprite = WoodButtonSkin.Face;
            image.overrideSprite = null;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 20f;
            image.color = new Color(.87f, .85f, .79f);
            image.raycastTarget = false;
        }

        private static void ApplyWarehousePlain(Image image, Color color)
        {
            image.sprite = null;
            image.overrideSprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void ApplyWarehouseLining(Image image, Color color)
        {
            // The room already contains the detailed velvet weave. A translucent
            // fill protects text contrast while retaining that authored material.
            color.a = .52f;
            ApplyWarehousePlain(image, color);
        }

        private void WarehouseBrassRim(string name, Transform parent, Vector2 size, bool selected)
        {
            Image rim = WarehouseImage(name, parent, size, Vector2.zero, WarehouseRoomTheme.Brass);
            rim.sprite = WoodButtonSkin.Face;
            rim.overrideSprite = null;
            rim.type = Image.Type.Sliced;
            rim.fillCenter = false;
            rim.pixelsPerUnitMultiplier = selected ? 24f : 32f;
            rim.color = selected ? WarehouseRoomTheme.Brass * 1.35f : WarehouseRoomTheme.Brass;
            rim.raycastTarget = false;
        }

        private void StyleWarehouseInventoryButton(Button button, bool selected = false)
        {
            Image face = button.GetComponent<Image>();
            ApplyWarehouseLining(face, Color.white);
            face.raycastTarget = true;
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? Color.Lerp(WarehouseRoomTheme.Lining, new Color(.23f, .24f, .15f), .45f) : WarehouseRoomTheme.Lining;
            colors.highlightedColor = Color.Lerp(WarehouseRoomTheme.Lining, new Color(.27f, .36f, .33f), .5f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(WarehouseRoomTheme.Lining, Color.black, .2f);
            // The preview is intentionally not actionable; retain readable materials.
            colors.disabledColor = WarehouseRoomTheme.Lining;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            WoodButtonRoleState woodRole = button.GetComponent<WoodButtonRoleState>();
            if (woodRole != null) woodRole.enabled = false;
            WarehouseBrassRim("Warehouse Inventory Rim", button.transform,
                ((RectTransform)button.transform).sizeDelta, selected);
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
                    new Color(WarehouseRoomTheme.Brass.r, WarehouseRoomTheme.Brass.g, WarehouseRoomTheme.Brass.b, .3f));
                ApplyWarehousePlain(divider, divider.color);
            }
        }
    }
}
