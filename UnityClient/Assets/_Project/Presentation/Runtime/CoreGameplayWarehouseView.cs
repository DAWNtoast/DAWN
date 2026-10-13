using System;
using System.Collections.Generic;
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
        private const int WarehouseTreasuresPerPage = 3;
        private const int WarehouseCatalogEntriesPerPage = 8;
        private static readonly Color WarehouseInk = new Color(0.22f, 0.13f, 0.06f);

        private void BuildWarehouseFacility()
        {
            warehouseViewRevision++;
            BeginWarehouseRetainedView();
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
            RectTransform inventory = WarehousePanel("Warehouse Panel", contentRoot,
                new Vector2(1820f, 790f), new Vector2(0f, -45f), Color.clear);
            // Preserve the compatibility node without loading or painting a second
            // room. Each port's full-frame Warehouse Background owns the cabinet.
            RawImage cabinetArt = WarehouseRawImage("Warehouse Cabinet Art", contentRoot,
                new Vector2(1920f, 1080f), Vector2.zero, null, new Rect(0f, 0f, 1f, 1f));
            cabinetArt.color = Color.clear;
            cabinetArt.raycastTarget = false;
            cabinetArt.transform.SetSiblingIndex(0);
            if (warehouseCatalogOpen)
            {
                BuildWarehouseCollectionCatalog(inventory);
                EndWarehouseRetainedView(inventory);
                return;
            }
            BuildWarehouseCabinet(inventory);
            BuildWarehouseCurseService(inventory);
            // The painted cabinet supplies the outer timber frame. All pages use
            // the same transparent opening and shared summary measurements.
            RectTransform ledger = WarehousePanel("Warehouse Ledger", inventory,
                new Vector2(640f, 832f), new Vector2(538f, -2f), Color.clear);
            ApplyWarehousePlain(ledger.GetComponent<Image>(), Color.clear);
            Button valuables = WarehouseAction("Warehouse Tab Valuables", ledger, new Vector2(188f, 60f),
                new Vector2(-204f, 336f), "藏品陈列", () => { warehouseStorageOpen = false; warehouseCatalogOpen = false; warehouseTreasurePage = 0; Render(); });
            Button storage = WarehouseAction("Warehouse Tab Storage", ledger, new Vector2(188f, 60f),
                new Vector2(0f, 336f), "基础货物", () => { warehouseStorageOpen = true; Render(); });
            Button rare = WarehouseAction("Warehouse Tab Rare", ledger, new Vector2(188f, 60f),
                new Vector2(204f, 336f), "珍稀物品", () =>
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
            EndWarehouseRetainedView(inventory);
        }

        private void BuildWarehouseCabinet(RectTransform parent)
        {
            RectTransform cabinet = WarehouseRect("Warehouse Display Cabinet", parent,
                new Vector2(1010f, 500f), new Vector2(-314f, 22f));

            Vector2[] slotPositions =
            {
                new Vector2(-316f, 137f),
                new Vector2(-8f, 137f),
                new Vector2(302f, 137f),
                new Vector2(-316f, -112f),
                new Vector2(-8f, -112f),
                new Vector2(302f, -112f),
            };
            Vector2[] labelPositions =
            {
                new Vector2(-316f, 14f),
                new Vector2(-8f, 14f),
                new Vector2(302f, 14f),
                new Vector2(-316f, -224f),
                new Vector2(-8f, -224f),
                new Vector2(302f, -224f),
            };

            for (int index = 0; index < CoreWarehouseCollection.DisplaySlotCount; index++)
            {
                int slot = index;
                Vector2 position = slotPositions[index];
                Button target = WarehouseButton("Warehouse Display Slot " + slot, cabinet,
                    new Vector2(280f, 186f), position, Color.clear, Gold, 16);
                target.GetComponentInChildren<Text>().text = string.Empty;
                // The cabinet recess is already painted into the cabinet art. Keep the
                // compatibility rim object as an invisible hit-safe layer so exhibits
                // retain clear space above the shelf without an extra card-like border.
                Image rim = WarehouseImage("Warehouse Slot Rim " + slot, target.transform,
                    new Vector2(280f, 186f), Vector2.zero,
                    Color.clear);
                rim.type = Image.Type.Sliced;
                rim.fillCenter = false;
                rim.pixelsPerUnitMultiplier = 22f;
                rim.raycastTarget = false;
                string displayed = warehouseCollection.DisplayedAt(slot);
                CoreWarehouseTreasureDefinition definition = FindWarehouseTreasure(displayed);
                if (definition != null)
                {
                    ItemIconArt art = ItemIconCatalog.ForTreasure(definition);
                    Vector2 fittedSize = UiImageAspect.FitSize(art.Texture, art.Uv, new Vector2(186f, 186f));
                    WarehouseItemIcon("Warehouse Displayed Art " + slot, target.transform,
                        art, new Vector2(0f, -93f + fittedSize.y * 0.5f), 186f);
                }
                // Center names on the shelf's front board, below the raised exhibit.
                Text slotLabel = WarehouseLabel("Warehouse Display Label " + slot, cabinet,
                    new Vector2(156f, 26f), labelPositions[index], 17,
                    definition == null ? "展位 " + (slot + 1) + " · 待陈列" : definition.Name, WarehouseInk);
                GameTypography.Apply(slotLabel, 17, selectedWarehouseDisplaySlot == slot ? FontStyle.Bold : FontStyle.Normal);
                slotLabel.resizeTextForBestFit = true;
                slotLabel.resizeTextMinSize = 12;
                slotLabel.resizeTextMaxSize = 17;
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
            IReadOnlyList<string> ownedIds = warehouseCollection.OwnedIds;
            int owned = ownedIds.Count;
            int displayed = 0;
            for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++)
                if (!string.IsNullOrEmpty(warehouseCollection.DisplayedAt(slot))) displayed++;
            int pageCount = Math.Max(1, (owned + WarehouseTreasuresPerPage - 1) / WarehouseTreasuresPerPage);
            warehouseTreasurePage = Math.Max(0, Math.Min(warehouseTreasurePage, pageCount - 1));
            int pageStart = warehouseTreasurePage * WarehouseTreasuresPerPage;
            int pageEnd = Math.Min(owned, pageStart + WarehouseTreasuresPerPage);
            RectTransform summary = BuildWarehouseSummary(ledger);
            Text count = WarehouseLabel("Warehouse Collection Count", summary, new Vector2(WarehouseLedgerWidth - 28f, 50f),
                Vector2.zero, 20, CurrentWarehousePort + "珍藏 " + owned + " 件　·　已陈列 " + displayed + " / 6　·　第 " + (warehouseTreasurePage + 1) + " / " + pageCount + " 页", WarehouseRoomTheme.Ink);
            count.resizeTextForBestFit = true;
            count.resizeTextMinSize = 16;
            count.resizeTextMaxSize = 20;
            // Three fixed card frames retain the same rhythm even on a sparse page.
            for (int column = 0; column < WarehouseTreasuresPerPage; column++)
                WarehouseLinedPanel("Warehouse Collection Card Back " + column, ledger,
                    new Vector2(188f, 206f), new Vector2(-204f + column * 204f, 106f));

            for (int itemIndex = pageStart; itemIndex < pageEnd; itemIndex++)
            {
                CoreWarehouseTreasureDefinition treasure = FindWarehouseTreasure(ownedIds[itemIndex]);
                if (treasure == null) continue;
                string id = treasure.Id;
                int visibleIndex = itemIndex - pageStart;
                int column = visibleIndex % 3;
                Button tile = WarehouseButton("Warehouse Treasure " + id, ledger,
                    new Vector2(188f, 206f), new Vector2(-204f + column * 204f, 106f), PanelRaised, Ink, 19);
                tile.GetComponentInChildren<Text>().text = string.Empty;
                StyleWarehouseInventoryButton(tile, selectedWarehouseTreasureId == id);
                WarehouseItemIcon("Warehouse Inventory Art " + id, tile.transform,
                    ItemIconCatalog.ForTreasure(treasure), new Vector2(0f, 19f), 130f);
                string ownerPort = WarehouseTreasureOwnerPort(id);
                Text inventoryName = WarehouseLabel("Warehouse Inventory Name " + id, tile.transform, new Vector2(174f, 28f),
                    new Vector2(0f, -63f), 18, treasure.Name, WarehouseRoomTheme.Ink);
                inventoryName.resizeTextForBestFit = true;
                inventoryName.resizeTextMinSize = 13;
                inventoryName.resizeTextMaxSize = 18;
                // Leave a small baseline gap below the item name.  At -82 with a
                // 28px name and 24px state label the glyph boxes overlapped by 3px,
                // producing a dark/blurred line in the collection tiles.
                WarehouseLabel("Warehouse Inventory State " + id, tile.transform, new Vector2(166f, 24f),
                    new Vector2(0f, -88f), 15, string.IsNullOrEmpty(ownerPort) ? "未获得" :
                        ownerPort != CurrentWarehousePort ? "已收藏 · " + ownerPort :
                        warehouseCollection.IsDisplayed(id) ? "已陈列 · 唯一藏品" : "已入库 · 可陈列", WarehouseRoomTheme.Brass);
                AddWarehouseAction(tile, () => { selectedWarehouseTreasureId = id; Render(); });
            }

            if (owned == 0)
            {
                WarehouseLabel("Warehouse Empty Collection", ledger, new Vector2(578f, 158f),
                    new Vector2(0f, 106f), 23, CurrentWarehousePort + "尚无藏品\n\n藏品存放在获得它的港口。", WarehouseRoomTheme.Ink);
            }
            else if (pageEnd - pageStart == 1)
            {
                CoreWarehouseTreasureDefinition inspected = warehouseCollection.Owns(selectedWarehouseTreasureId)
                    ? FindWarehouseTreasure(selectedWarehouseTreasureId) : null;
                Text hint = WarehouseLabel("Warehouse Collection Hint", ledger, new Vector2(376f, 164f),
                    new Vector2(105f, 106f), 18,
                    inspected == null ? "选择藏品查看来历。" : inspected.Description, WarehouseRoomTheme.Ink);
                hint.resizeTextForBestFit = true;
                hint.resizeTextMinSize = 15;
                hint.resizeTextMaxSize = 18;
            }
            Button previousPage = WarehouseAction("Warehouse Treasure Page Previous", ledger, new Vector2(124f, 44f),
                new Vector2(-236f, -338f), "上一页", () =>
                {
                    warehouseTreasurePage = Math.Max(0, warehouseTreasurePage - 1);
                    Render();
                });
            previousPage.interactable = warehouseTreasurePage > 0;
            WarehouseLabel("Warehouse Treasure Page", ledger, new Vector2(80f, 30f),
                new Vector2(-128f, -338f), 19, (warehouseTreasurePage + 1) + " / " + pageCount, WarehouseRoomTheme.Ink);
            Button nextPage = WarehouseAction("Warehouse Treasure Page Next", ledger, new Vector2(124f, 44f),
                new Vector2(-20f, -338f), "下一页", () =>
                {
                    warehouseTreasurePage = Math.Min(pageCount - 1, warehouseTreasurePage + 1);
                    Render();
                });
            nextPage.interactable = warehouseTreasurePage < pageCount - 1;
            WarehouseAction("Warehouse Collection Catalog", ledger, new Vector2(218f, 44f),
                new Vector2(189f, -338f), "藏品图鉴 " + GlobalWarehouseTreasureCount + " / " + CoreWarehouseCollection.CatalogCount, () =>
                {
                    warehouseCatalogOpen = true;
                    warehouseTreasurePage = 0;
                    selectedWarehouseTreasureId = string.Empty;
                    Render();
                });
            CoreWarehouseTreasureDefinition selected = warehouseCollection.Owns(selectedWarehouseTreasureId)
                ? FindWarehouseTreasure(selectedWarehouseTreasureId) : null;
            WarehouseLinedPanel("Warehouse Treasure Details", ledger,
                new Vector2(WarehouseLedgerWidth, 238f), new Vector2(0f, -128f));
            Text title = WarehouseLabel("Warehouse Treasure Title", ledger, new Vector2(548f, 42f),
                new Vector2(0f, -41f), 27, selected == null ? "选择藏品查看来历" : selected.Name, WarehouseRoomTheme.Brass);
            GameTypography.Apply(title, 27, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleLeft;
            Text source = WarehouseLabel("Warehouse Treasure Source", ledger, new Vector2(548f, 42f),
                new Vector2(0f, -86f), 18, selected == null ? string.Empty : "来源：" + selected.Source, WarehouseRoomTheme.Ink);
            source.alignment = TextAnchor.MiddleLeft;
            source.resizeTextForBestFit = true;
            source.resizeTextMinSize = 14;
            source.resizeTextMaxSize = 18;
            Text story = WarehouseLabel("Warehouse Treasure Story", ledger, new Vector2(548f, 126f),
                new Vector2(0f, -176f), 21,
                selected == null ? string.Empty : selected.Story, WarehouseRoomTheme.Ink);
            story.alignment = TextAnchor.UpperLeft;
            story.lineSpacing = 1.22f;
            story.resizeTextForBestFit = true;
            story.resizeTextMinSize = 18;
            story.resizeTextMaxSize = 21;
            Button display = WarehouseAction("Warehouse Display Selected", ledger, new Vector2(292f, 56f),
                new Vector2(-152f, -281f), "陈列到展位 " + (selectedWarehouseDisplaySlot + 1), () =>
                {
                    if (!warehouseCollection.TryDisplay(selectedWarehouseTreasureId, selectedWarehouseDisplaySlot)) return;
                    OnWarehouseTreasureDisplayed();
                    SaveProgress();
                    Render();
                });
            display.interactable = selected != null && warehouseCollection.Owns(selected.Id);
            Button remove = WarehouseAction("Warehouse Remove Selected", ledger, new Vector2(280f, 56f),
                new Vector2(158f, -281f), "收回仓库", () =>
                {
                    for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++)
                        if (warehouseCollection.DisplayedAt(slot) == selectedWarehouseTreasureId)
                            warehouseCollection.TryRemoveDisplay(slot);
                    SaveProgress();
                    Render();
                });
            remove.interactable = selected != null && warehouseCollection.IsDisplayed(selected.Id);
        }

        private void BuildWarehouseCollectionCatalog(RectTransform parent)
        {
            IReadOnlyList<CoreWarehouseTreasureDefinition> catalog = CoreWarehouseCollection.Catalog;
            int pageCount = Math.Max(1, (catalog.Count + WarehouseCatalogEntriesPerPage - 1) / WarehouseCatalogEntriesPerPage);
            warehouseTreasurePage = Math.Max(0, Math.Min(warehouseTreasurePage, pageCount - 1));
            RectTransform paper = WarehousePanel("Warehouse Collection Catalog Panel", parent,
                new Vector2(1180f, 900f), Vector2.zero, new Color(0.9f, 0.8f, 0.62f));
            NauticalSurfaceSkin.ApplyReadingPanel(paper.GetComponent<Image>());
            Text heading = WarehouseLabel("Warehouse Catalog Heading", paper, new Vector2(1040f, 42f),
                new Vector2(0f, 360f), 32, "藏品图鉴", WarehouseInk);
            GameTypography.Apply(heading, 32, FontStyle.Bold);
            WarehouseLabel("Warehouse Catalog Count", paper, new Vector2(1040f, 32f),
                new Vector2(0f, 314f), 20, "全存档藏品 " + GlobalWarehouseTreasureCount + " / " + catalog.Count, WarehouseInk);
            Image divider = WarehouseImage("Warehouse Catalog Divider", paper, new Vector2(1040f, 2f),
                new Vector2(0f, 286f), new Color(WarehouseInk.r, WarehouseInk.g, WarehouseInk.b, 0.3f));
            divider.raycastTarget = false;
            Text itemColumn = WarehouseLabel("Warehouse Catalog Item Column", paper, new Vector2(420f, 28f),
                new Vector2(-292f, 264f), 20, "藏品", WarehouseInk);
            itemColumn.alignment = TextAnchor.MiddleLeft;
            // The parchment's centre fold separates the two reading columns.
            // Keep the entire requirement box on the right with a 28px gutter.
            Text sourceColumn = WarehouseLabel("Warehouse Catalog Source Column", paper, new Vector2(480f, 28f),
                new Vector2(268f, 264f), 20, "解锁方式", WarehouseInk);
            sourceColumn.alignment = TextAnchor.MiddleLeft;

            int first = warehouseTreasurePage * WarehouseCatalogEntriesPerPage;
            int last = Math.Min(catalog.Count, first + WarehouseCatalogEntriesPerPage);
            for (int index = first; index < last; index++)
            {
                CoreWarehouseTreasureDefinition treasure = catalog[index];
                string ownerPort = WarehouseTreasureOwnerPort(treasure.Id);
                bool owned = !string.IsNullOrEmpty(ownerPort);
                RectTransform row = WarehouseRect("Warehouse Catalog Row " + treasure.Id, paper,
                    new Vector2(1040f, 70f), new Vector2(0f, 212f - (index - first) * 74f));
                RawImage icon = WarehouseItemIcon("Warehouse Catalog Icon " + treasure.Id, row,
                    ItemIconCatalog.ForTreasure(treasure), new Vector2(-478f, 0f), 48f);
                icon.color = owned ? Color.white : new Color(0.78f, 0.78f, 0.78f, 0.9f);
                Text name = WarehouseLabel("Warehouse Catalog Name " + treasure.Id, row,
                    new Vector2(330f, 30f), new Vector2(-277f, 10f), 22, treasure.Name, WarehouseInk);
                GameTypography.Apply(name, 22, FontStyle.Bold);
                name.alignment = TextAnchor.MiddleLeft;
                Text state = WarehouseLabel("Warehouse Catalog State " + treasure.Id, row,
                    new Vector2(330f, 22f), new Vector2(-277f, -20f), 16,
                    owned ? "已收藏 · " + ownerPort : "未获得", WarehouseInk);
                state.alignment = TextAnchor.MiddleLeft;
                Text source = WarehouseLabel("Warehouse Catalog Source " + treasure.Id, row,
                    new Vector2(480f, 70f), new Vector2(268f, 0f), 18,
                    treasure.Source.Replace("（非教学", "\n（非教学"), WarehouseInk);
                source.alignment = TextAnchor.MiddleLeft;
                source.lineSpacing = 1f;
            }

            Button previous = WarehouseAction("Warehouse Catalog Page Previous", paper,
                new Vector2(150f, 38f), new Vector2(-417f, -367f), "上一页", () =>
                {
                    warehouseTreasurePage = Math.Max(0, warehouseTreasurePage - 1);
                    Render();
                });
            previous.interactable = warehouseTreasurePage > 0;
            WarehouseLabel("Warehouse Catalog Page", paper, new Vector2(96f, 30f),
                new Vector2(-260f, -367f), 20, (warehouseTreasurePage + 1) + " / " + pageCount, WarehouseInk);
            Button next = WarehouseAction("Warehouse Catalog Page Next", paper,
                new Vector2(150f, 38f), new Vector2(-103f, -367f), "下一页", () =>
                {
                    warehouseTreasurePage = Math.Min(pageCount - 1, warehouseTreasurePage + 1);
                    Render();
                });
            next.interactable = warehouseTreasurePage < pageCount - 1;
            WarehouseAction("Warehouse Collection Catalog", paper, new Vector2(300f, 38f),
                new Vector2(268f, -367f), "返回本港藏品", () =>
                {
                    warehouseCatalogOpen = false;
                    warehouseTreasurePage = 0;
                    selectedWarehouseTreasureId = string.Empty;
                    Render();
                });
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
            Text text = WarehouseText(name, parent, size, position, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter, color);
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Button WarehouseAction(string name, Transform parent, Vector2 size, Vector2 position, string label, Action action)
        {
            Button button = WarehouseButton(name, parent, size, position, Gold, Ink, 21);
            button.GetComponentInChildren<Text>().text = label;
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




