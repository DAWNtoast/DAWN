using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private string selectedWarehouseTreasureId = string.Empty;
        private int selectedWarehouseDisplaySlot;
        private int warehouseTreasurePage;
        private bool warehouseCatalogOpen;
        private int warehouseViewRevision;
        private string warehouseViewedPort = string.Empty;
        private CoreVoyageSession warehouseViewedSession;
        // One row leaves the lower half of the ledger for the selected keepsake's
        // story and display actions. The cabinet still has six independent slots.
        private static int WarehouseTreasuresPerPage => GameReadability.Level == 2 ? 1 : GameReadability.Level == 1 ? 2 : 3;
        private static readonly Color WarehouseInk = new Color(0.22f, 0.13f, 0.06f);

        private void BuildWarehouseFacility()
        {
            warehouseViewRevision++;
            InvalidateWarehouseCargoDialog();
            if (!ReferenceEquals(warehouseViewedSession, session) || warehouseViewedPort != CurrentWarehousePort)
            {
                bool changedPortInSession = ReferenceEquals(warehouseViewedSession, session);
                warehouseViewedSession = session;
                warehouseViewedPort = CurrentWarehousePort;
                warehouseStorageOpen = false;
                warehouseTreasurePage = 0;
                warehouseCatalogOpen = false;
                // A preview initializer can deliberately select a sample item in
                // its fresh session. Only a real port switch clears that choice.
                if (changedPortInSession)
                {
                    selectedWarehouseTreasureId = string.Empty;
                    selectedWarehouseDisplaySlot = 0;
                }
                portWarehouseNotice = string.Empty;
            }
            RectTransform inventory = CreateFixedPanel("Warehouse Panel", contentRoot,
                new Vector2(1820f, 790f), new Vector2(0f, -45f), Color.clear);
            // Keep the authored cabinet texture available as a fallback art layer.
            // Warehouse Background now owns the expanded scene composition, so put
            // this full-surface cabinet art behind it instead of drawing two opaque
            // warehouse scenes on top of each other.
            Texture2D cabinetTexture = Resources.Load<Texture2D>("WarehouseV2/trophy_cabinet_v2_expanded");
            RawImage cabinetArt = CreateRawImage("Warehouse Cabinet Art", contentRoot,
                PhaseSurfaceLayout.BackgroundSize, PhaseSurfaceLayout.BackgroundPosition,
                cabinetTexture, PhaseSurfaceLayout.BackgroundUv(cabinetTexture));
            cabinetArt.raycastTarget = false;
            cabinetArt.transform.SetSiblingIndex(0);
            BuildWarehouseCabinet(inventory);
            BuildWarehouseCurseService(inventory);
            RectTransform ledger = CreateFixedPanel("Warehouse Ledger", inventory,
                new Vector2(690f, 740f), new Vector2(608f, -11f), new Color(0.9f, 0.8f, 0.62f));
            Button valuables = WarehouseAction("Warehouse Tab Valuables", ledger, new Vector2(196f, 52f),
                new Vector2(-208f, 320f), "藏品陈列", () => { warehouseStorageOpen = false; warehouseCatalogOpen = false; warehouseTreasurePage = 0; Render(); });
            Button storage = WarehouseAction("Warehouse Tab Storage", ledger, new Vector2(196f, 52f),
                new Vector2(0f, 320f), "基础货物", () => { warehouseStorageOpen = true; Render(); });
            Button rare = WarehouseAction("Warehouse Tab Rare", ledger, new Vector2(196f, 52f),
                new Vector2(208f, 320f), "珍稀物品", () =>
                {
                    if (!WarehouseCargoActionsAllowed) return;
                    warehouseStorageOpen = true;
                    warehouseRareStorageOpen = true;
                    Render();
                });
            rare.interactable = WarehouseCargoActionsAllowed;
            WoodButtonSkin.SetSelected(valuables, !warehouseStorageOpen);
            WoodButtonSkin.SetSelected(storage, warehouseStorageOpen && !warehouseRareStorageOpen);
            WoodButtonSkin.SetSelected(rare, warehouseStorageOpen && warehouseRareStorageOpen);
            if (warehouseStorageOpen && warehouseRareStorageOpen) BuildPortWarehouseRareStorage(ledger);
            else if (warehouseStorageOpen) BuildPortWarehouseStorage(ledger);
            else BuildWarehouseValuables(ledger);
        }

        private void BuildWarehouseCabinet(RectTransform parent)
        {
            RectTransform cabinet = CreateRect("Warehouse Display Cabinet", parent,
                new Vector2(1060f, 706f), new Vector2(-374f, -38f));

            Vector2[] slotPositions =
            {
                new Vector2(-280f, 176f),
                new Vector2(50f, 176f),
                new Vector2(380f, 176f),
                new Vector2(-280f, -52f),
                new Vector2(50f, -52f),
                new Vector2(380f, -52f),
            };
            Vector2[] labelPositions =
            {
                new Vector2(-280f, 70f),
                new Vector2(50f, 70f),
                new Vector2(380f, 70f),
                new Vector2(-280f, -155f),
                new Vector2(50f, -155f),
                new Vector2(380f, -155f),
            };

            for (int index = 0; index < CoreWarehouseCollection.DisplaySlotCount; index++)
            {
                int slot = index;
                Vector2 position = slotPositions[index];
                Button target = CreateButton("Warehouse Display Slot " + slot, cabinet,
                    new Vector2(226f, 176f), position, Color.clear, Gold, 16);
                target.GetComponentInChildren<Text>().text = string.Empty;
                // The cabinet recess is already painted into the cabinet art. Keep the
                // compatibility rim object as an invisible hit-safe layer so exhibits
                // sit directly on the shelf without an extra card-like border.
                Image rim = CreateImage("Warehouse Slot Rim " + slot, target.transform,
                    new Vector2(232f, 184f), Vector2.zero,
                    Color.clear);
                rim.type = Image.Type.Sliced;
                rim.fillCenter = false;
                rim.pixelsPerUnitMultiplier = 22f;
                rim.raycastTarget = false;
                string displayed = warehouseCollection.DisplayedAt(slot);
                CoreWarehouseTreasureDefinition definition = FindWarehouseTreasure(displayed);
                if (definition != null)
                {
                    RawImage item = CreateItemIcon("Warehouse Displayed Art " + slot, target.transform,
                        ItemIconCatalog.ForTreasure(definition), new Vector2(0f, -1f), 176f);
                    item.rectTransform.anchoredPosition = new Vector2(0f, -89f + item.rectTransform.rect.height * 0.5f);
                }
                WarehouseLabel("Warehouse Display Label " + slot, cabinet, new Vector2(286f, 64f),
                    labelPositions[index], GameReadability.FontSize(18),
                    definition == null ? "展位 " + (slot + 1) + " · 待陈列" : definition.Name, definition == null ? MutedInk : Gold);
                ColorBlock colors = target.colors;
                Image targetFace = target.GetComponent<Image>();
                targetFace.color = Color.clear;
                targetFace.sprite = null;
                targetFace.type = Image.Type.Simple;
                targetFace.fillCenter = true;
                targetFace.pixelsPerUnitMultiplier = 1f;
                colors.normalColor = Color.clear;
                colors.highlightedColor = colors.selectedColor = Color.clear;
                colors.pressedColor = Color.clear;
                target.colors = colors;
                AddWarehouseAction(target, () =>
                {
                    selectedWarehouseDisplaySlot = slot;
                    warehouseStorageOpen = false;
                    warehouseCatalogOpen = false;
                    if (!string.IsNullOrEmpty(displayed)) selectedWarehouseTreasureId = displayed;
                    Render();
                });
            }
        }

        private void BuildWarehouseValuables(RectTransform ledger)
        {
            IReadOnlyList<string> ownedIds = warehouseCatalogOpen
                ? CoreWarehouseCollection.Catalog.Select(item => item.Id).ToArray() : warehouseCollection.OwnedIds;
            int owned = ownedIds.Count;
            int displayed = 0;
            for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++)
                if (!string.IsNullOrEmpty(warehouseCollection.DisplayedAt(slot))) displayed++;
            int pageCount = Math.Max(1, (owned + WarehouseTreasuresPerPage - 1) / WarehouseTreasuresPerPage);
            warehouseTreasurePage = Math.Max(0, Math.Min(warehouseTreasurePage, pageCount - 1));
            int pageStart = warehouseTreasurePage * WarehouseTreasuresPerPage;
            int pageEnd = Math.Min(owned, pageStart + WarehouseTreasuresPerPage);
            bool extraLarge = GameReadability.Level == 2;
            Text countLabel = WarehouseLabel("Warehouse Collection Count", ledger, new Vector2(610f, extraLarge ? 64f : 32f),
                new Vector2(0f, extraLarge ? 250f : 264f), GameReadability.FontSize(18), warehouseCatalogOpen
                    ? "全存档藏品图鉴 " + GlobalWarehouseTreasureCount + " / 40　·　第 " + (warehouseTreasurePage + 1) + " / " + pageCount + " 页"
                    : CurrentWarehousePort + "珍藏 " + owned + " 件　·　已陈列 " + displayed + " / 6　·　第 " + (warehouseTreasurePage + 1) + " / " + pageCount + " 页", WarehouseInk);
            if (extraLarge) countLabel.text = countLabel.text.Replace("　·　第 ", "\n第 ");

            for (int itemIndex = pageStart; itemIndex < pageEnd; itemIndex++)
            {
                CoreWarehouseTreasureDefinition treasure = FindWarehouseTreasure(ownedIds[itemIndex]);
                if (treasure == null) continue;
                string id = treasure.Id;
                int visibleIndex = itemIndex - pageStart;
                int column = visibleIndex;
                float tileWidth = (614f - (WarehouseTreasuresPerPage - 1) * 12f) / WarehouseTreasuresPerPage;
                Button tile = CreateButton("Warehouse Treasure " + id, ledger,
                    new Vector2(tileWidth, extraLarge ? 166f : 198f), new Vector2(-307f + tileWidth * .5f + column * (tileWidth + 12f), extraLarge ? 121f : 137f), PanelRaised, Ink, 19);
                tile.GetComponentInChildren<Text>().text = string.Empty;
                RawImage icon = CreateItemIcon("Warehouse Inventory Art " + id, tile.transform,
                    ItemIconCatalog.ForTreasure(treasure), new Vector2(0f, extraLarge ? 54f : 57f), extraLarge ? 52f : 64f);
                string ownerPort = WarehouseTreasureOwnerPort(id);
                if (warehouseCatalogOpen && string.IsNullOrEmpty(ownerPort)) icon.color = new Color(.65f, .65f, .65f, .7f);
                WarehouseLabel("Warehouse Inventory Name " + id, tile.transform, new Vector2(tileWidth - 14f, extraLarge ? 44f : 48f),
                    new Vector2(0f, extraLarge ? -10f : -5f), GameReadability.FontSize(22), treasure.Name, Ink);
                // Leave a small baseline gap below the item name.  At -82 with a
                // 28px name and 24px state label the glyph boxes overlapped by 3px,
                // producing a dark/blurred line in the collection tiles.
                WarehouseLabel("Warehouse Inventory State " + id, tile.transform, new Vector2(tileWidth - 14f, extraLarge ? 42f : 48f),
                    new Vector2(0f, extraLarge ? -55f : -65f), GameReadability.FontSize(18), string.IsNullOrEmpty(ownerPort) ? "未获得" :
                        ownerPort != CurrentWarehousePort ? "已收藏 · " + ownerPort :
                        warehouseCollection.IsDisplayed(id) ? "已陈列 · 唯一藏品" : "已入库 · 可陈列", Gold);
                WoodButtonSkin.SetSelected(tile, selectedWarehouseTreasureId == id);
                AddWarehouseAction(tile, () => { selectedWarehouseTreasureId = id; Render(); });
            }

            if (owned == 0)
            {
                WarehouseLabel("Warehouse Empty Collection", ledger, new Vector2(578f, 158f),
                    new Vector2(0f, 139f), 24, CurrentWarehousePort + "尚无藏品\n\n藏品存放在获得它的港口。", WarehouseInk);
            }
            Button previousPage = WarehouseAction("Warehouse Treasure Page Previous", ledger, new Vector2(120f, 52f),
                new Vector2(-250f, -331f), "上一页", () =>
                {
                    warehouseTreasurePage = Math.Max(0, warehouseTreasurePage - 1);
                    Render();
                });
            previousPage.interactable = warehouseTreasurePage > 0;
            WarehouseLabel("Warehouse Treasure Page", ledger, new Vector2(100f, 42f),
                new Vector2(-125f, -331f), GameReadability.FontSize(18), (warehouseTreasurePage + 1) + " / " + pageCount, WarehouseInk);
            Button nextPage = WarehouseAction("Warehouse Treasure Page Next", ledger, new Vector2(120f, 52f),
                new Vector2(0f, -331f), "下一页", () =>
                {
                    warehouseTreasurePage = Math.Min(pageCount - 1, warehouseTreasurePage + 1);
                    Render();
                });
            nextPage.interactable = warehouseTreasurePage < pageCount - 1;
            WarehouseAction("Warehouse Collection Catalog", ledger, new Vector2(218f, 52f),
                new Vector2(198f, -331f), warehouseCatalogOpen ? "本港藏品" : "藏品图鉴", () =>
                {
                    warehouseCatalogOpen = !warehouseCatalogOpen;
                    warehouseTreasurePage = 0;
                    selectedWarehouseTreasureId = string.Empty;
                    Render();
                });
            CoreWarehouseTreasureDefinition selected = warehouseCatalogOpen || warehouseCollection.Owns(selectedWarehouseTreasureId)
                ? FindWarehouseTreasure(selectedWarehouseTreasureId) : null;
            WarehouseLabel("Warehouse Treasure Title", ledger, new Vector2(612f, 46f * GameReadability.Scale),
                new Vector2(0f, 0f), GameReadability.FontSize(32), selected == null ? "藏品档案" : selected.Name, WarehouseInk);
            Text source = WarehouseLabel("Warehouse Treasure Source", ledger, new Vector2(606f, 56f),
                new Vector2(0f, -63f), 18, selected == null ? "图鉴可查询未获得的藏品。" : selected.Source, WarehouseInk);
            ReadableParagraph(source, ledger, new Vector2(606f, 48f), new Vector2(0f, -64f), 18, true);
            Text story = WarehouseLabel("Warehouse Treasure Story", ledger, new Vector2(596f, 132f),
                new Vector2(0f, -163f), 22,
                selected == null ? "藏品分港保管，不占船舱，返航不损失。\n\n陈列或收回均不消耗藏品。" : selected.Story, WarehouseInk);
            story.lineSpacing = 1.22f;
            ReadableParagraph(story, ledger, new Vector2(606f, 118f), new Vector2(0f, -155f), 22, true);
            Button display = WarehouseAction("Warehouse Display Selected", ledger, new Vector2(300f, 52f),
                new Vector2(-154f, -253f), "陈列至展位 " + (selectedWarehouseDisplaySlot + 1), () =>
                {
                    if (!warehouseCollection.TryDisplay(selectedWarehouseTreasureId, selectedWarehouseDisplaySlot)) return;
                    OnWarehouseTreasureDisplayed();
                    SaveProgress();
                    Render();
                });
            display.interactable = selected != null && warehouseCollection.Owns(selected.Id);
            Button remove = WarehouseAction("Warehouse Remove Selected", ledger, new Vector2(278f, 52f),
                new Vector2(157f, -253f), "收回仓库", () =>
                {
                    for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++)
                        if (warehouseCollection.DisplayedAt(slot) == selectedWarehouseTreasureId)
                            warehouseCollection.TryRemoveDisplay(slot);
                    SaveProgress();
                    Render();
                });
            remove.interactable = selected != null && warehouseCollection.IsDisplayed(selected.Id);
        }

        private CoreWarehouseTreasureDefinition FindWarehouseTreasure(string id)
        {
            foreach (CoreWarehouseTreasureDefinition definition in CoreWarehouseCollection.Catalog)
                if (definition.Id == id) return definition;
            return null;
        }

        private Text WarehouseLabel(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, string value, Color color)
        {
            Text text = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter, color);
            text.text = value;
            text.font = GameTypography.BodyFont;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Button WarehouseAction(string name, Transform parent, Vector2 size, Vector2 position, string label, Action action)
        {
            size.y = Mathf.Max(size.y, 42f * GameReadability.Scale);
            Button button = CreateButton(name, parent, size, position, Gold, Ink, GameReadability.FontSize(22));
            button.GetComponentInChildren<Text>().text = label;
            button.GetComponentInChildren<Text>().font = GameTypography.BodyFont;
            GameReadability.Body(button.GetComponentInChildren<Text>(), 22);
            button.GetComponentInChildren<Text>().rectTransform.sizeDelta = size - new Vector2(16f, 8f);
            AddWarehouseAction(button, action);
            return button;
        }

        private void AddWarehouseAction(Button button, Action action)
        {
            string quotedPort = CurrentWarehousePort;
            CoreVoyageSession quotedSession = session;
            int quotedRevision = warehouseViewRevision;
            button.onClick.AddListener(() =>
            {
                if (!ReferenceEquals(session, quotedSession) || CurrentWarehousePort != quotedPort ||
                    warehouseViewRevision != quotedRevision || session == null ||
                    session.Phase != CoreDemoPhase.Port || activePortFacility != 0) return;
                action();
            });
        }
    }
}




