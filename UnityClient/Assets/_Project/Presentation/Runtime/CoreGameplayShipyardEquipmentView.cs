using System;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const int ShipyardInventoryPageSize = 4;
        private string selectedShipyardComponentInstanceId;
        private ComponentSlot selectedShipyardComponentSlot = ComponentSlot.Bow;
        private int shipyardInventoryPage;
        private bool shipyardInventoryShowAll = true;
        private ShipyardDossierTab shipyardEquipmentReturnTab;

        private void ResetShipyardEquipmentSelection()
        {
            selectedShipyardComponentInstanceId = null;
            selectedShipyardComponentSlot = ComponentSlot.Bow;
            shipyardInventoryPage = 0;
            shipyardInventoryShowAll = true;
        }

        private bool IsCurrentShipyardEquipmentView(int index)
        {
            return session != null && session.Phase == CoreDemoPhase.Port && activePortFacility == 1 &&
                shipyardDetailOpen && selectedShipPreview == index && shipyardDossierTab == ShipyardDossierTab.Equipment;
        }

        private void BuildShipyardEquipmentPage(Transform detail, int index, Color ink, Color muted)
        {
            RectTransform page = CreateRect("Shipyard Equipment Page", detail, PhaseSurfaceLayout.ContentSize, Vector2.zero);
            float pageScale = tutorialActive ? .86f : .9f;
            page.localScale = new Vector3(pageScale, pageScale, 1f);
            page.anchoredPosition = new Vector2(0f, tutorialActive ? -58f : -38f);
            bool owned = OwnsShipyardVessel(index);
            bool editable = Application.isPlaying && !editorPreviewBuilding && IsShipyardVesselAtCurrentPort(index);
            BuildShipyardEquipmentVessel(page, index);
            WorkbenchRule("Shipyard Equipment Installed Heading Surface", page, new Vector2(730f, 28f),
                new Vector2(-550f, -378f), ShipyardWorkbenchVesselSurface);
            ShipyardText("Shipyard Equipment Installed Heading", page, new Vector2(730f, 28f),
                new Vector2(-550f, -378f), 19, ShipyardWorkbenchLightMuted).text = owned ? "本船当前装配 · 点击槽位筛选配件" : "原装配置预览 · 购买后可改装";
            Vector2[] slotPositions = { new Vector2(-350f, -303f), new Vector2(-350f, 236f),
                new Vector2(-750f, -303f), new Vector2(-550f, -454f), new Vector2(-750f, 236f) };

            for (int slotIndex = 0; slotIndex < ComponentArtCatalog.SlotCount; slotIndex++)
            {
                ComponentSlot slot = (ComponentSlot)slotIndex;
                OwnedShipComponent installed = owned ? InstalledShipComponent(index, slot) : null;
                ComponentArtEntry entry = owned ? ShipComponent(index, slot) : componentArt.Get(slotIndex * ComponentArtCatalog.EntriesPerSlot);
                bool active = !shipyardInventoryShowAll && selectedShipyardComponentSlot == slot;
                Button row = CreateShipyardEquipmentButton("Shipyard Equipment Slot " + slot, page, new Vector2(300f, 108f),
                    slotPositions[slotIndex], 21, active);
                row.GetComponentInChildren<Text>().text = string.Empty;
                DrawShipyardComponentIcon("Shipyard Installed Art " + slot, row.transform, entry, new Vector2(-108f, 4f), 64f);
                ShipyardText("Shipyard Installed Slot " + slot, row.transform, new Vector2(206f, 28f),
                    new Vector2(37f, 31f), 23, active ? ShipyardWorkbenchLight : ink, TextAnchor.MiddleLeft).text = ComponentArtCatalog.SlotName(slot);
                ShipyardText("Shipyard Installed Name " + slot, row.transform, new Vector2(206f, 28f),
                    new Vector2(37f, 0f), 21, active ? ShipyardWorkbenchLight : ink, TextAnchor.MiddleLeft).text = entry?.Name ?? "未装配";
                ShipyardText("Shipyard Installed Meta " + slot, row.transform, new Vector2(130f, 24f),
                    new Vector2(-1f, -32f), 18, active ? ShipyardWorkbenchLightMuted : muted, TextAnchor.MiddleLeft).text = entry == null ? "空槽 · 待装配" :
                    ComponentArtCatalog.RarityName(entry.Rarity);
                row.onClick.AddListener(() =>
                {
                    if (row == null || !row.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index)) return;
                    selectedShipyardComponentSlot = slot;
                    selectedShipyardComponentInstanceId = null;
                    shipyardInventoryShowAll = false;
                    shipyardInventoryPage = 0;
                    RefreshShipyardDossierView();
                });
                // Keep the action beside the slot in the hierarchy so the
                // tutorial can lock slot selection without covering Unequip.
                Button remove = CreateShipyardEquipmentButton("Shipyard Unequip Component " + slot, page, new Vector2(64f, 28f),
                    slotPositions[slotIndex] + new Vector2(108f, -33f), 18, enabled: editable && installed != null);
                remove.GetComponentInChildren<Text>().text = "卸下";
                string installedId = installed?.InstanceId;
                remove.onClick.AddListener(() =>
                {
                    if (remove == null || !remove.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) ||
                        InstalledShipComponent(index, slot)?.InstanceId != installedId || !TryUnequipShipComponent(index, slot)) return;
                    selectedShipyardComponentInstanceId = null;
                    shipyardInventoryShowAll = false;
                    selectedShipyardComponentSlot = slot;
                    shipyardInventoryPage = 0;
                    RefreshShipyardDossierView();
                });

                Button category = CreateShipyardEquipmentButton("Shipyard Equipment Category " + slot, page,
                    new Vector2(200f, 68f), new Vector2(-20f + slotIndex * 210f, 366f), 23, active);
                Text categoryLabel = category.GetComponentInChildren<Text>();
                categoryLabel.text = ComponentArtCatalog.SlotName(slot);
                categoryLabel.rectTransform.anchoredPosition = new Vector2(25f, 0f);
                categoryLabel.rectTransform.sizeDelta = new Vector2(140f, 50f);
                DrawShipyardComponentIcon("Shipyard Equipment Category Art " + slot, category.transform,
                    entry ?? componentArt.Get(slotIndex * ComponentArtCatalog.EntriesPerSlot), new Vector2(-63f, 0f), 44f);
                category.onClick.AddListener(() =>
                {
                    if (category == null || !category.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index)) return;
                    selectedShipyardComponentSlot = slot;
                    selectedShipyardComponentInstanceId = null;
                    shipyardInventoryShowAll = false;
                    shipyardInventoryPage = 0;
                    RefreshShipyardDossierView();
                });
            }

            OwnedShipComponent tutorialReward = tutorialActive && GuidedTutorialEquipmentPending ? GuidedTutorialOwnedReward() : null;
            if (tutorialReward != null && tutorialReward.ShipIndex < 0) shipyardInventoryShowAll = true;
            OwnedShipComponent[] stored = OwnedComponents.Where(item => item.ShipIndex < 0 &&
                componentArt.GetById(item.ComponentId) != null &&
                (shipyardInventoryShowAll || componentArt.GetById(item.ComponentId).Slot == selectedShipyardComponentSlot))
                .OrderBy(item => (int)componentArt.GetById(item.ComponentId).Slot)
                .ThenByDescending(item => (int)componentArt.GetById(item.ComponentId).Rarity)
                .ThenBy(item => item.ComponentId, StringComparer.Ordinal).ThenBy(item => item.InstanceId, StringComparer.Ordinal).ToArray();
            int pages = Mathf.Max(1, (stored.Length + ShipyardInventoryPageSize - 1) / ShipyardInventoryPageSize);
            if (tutorialReward != null && tutorialReward.ShipIndex < 0)
            {
                int rewardPosition = Array.FindIndex(stored, item => item.InstanceId == tutorialReward.InstanceId);
                if (rewardPosition >= 0) shipyardInventoryPage = rewardPosition / ShipyardInventoryPageSize;
            }
            shipyardInventoryPage = Mathf.Clamp(shipyardInventoryPage, 0, pages - 1);
            RectTransform inventory = CreateShipyardWorkbenchPanel("Shipyard Equipment Inventory", page,
                new Vector2(300f, 730f), new Vector2(30f, -68f), ShipyardWorkbenchSurface, ShipyardWorkbenchBorder);
            ShipyardText("Shipyard Equipment Inventory Heading", inventory, new Vector2(192f, 32f),
                new Vector2(-40f, 331f), 21, ShipyardWorkbenchLight, TextAnchor.MiddleLeft).text =
                (shipyardInventoryShowAll ? "库存配件" : "可选" + ComponentArtCatalog.SlotName(selectedShipyardComponentSlot)) + " · " + stored.Length;
            Button all = CreateShipyardEquipmentButton("Shipyard Equipment All Components", inventory,
                new Vector2(72f, 32f), new Vector2(98f, 331f), 18, shipyardInventoryShowAll);
            all.GetComponentInChildren<Text>().text = "全部";
            all.onClick.AddListener(() =>
            {
                if (all == null || !all.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index)) return;
                selectedShipyardComponentInstanceId = null;
                shipyardInventoryShowAll = true;
                shipyardInventoryPage = 0;
                RefreshShipyardDossierView();
            });
            BuildShipyardInventoryPageButton(inventory, index, false, pages, new Vector2(-94f, -325f));
            BuildShipyardInventoryPageButton(inventory, index, true, pages, new Vector2(94f, -325f));
            ShipyardText("Shipyard Equipment Page Number", inventory, new Vector2(100f, 32f),
                new Vector2(0f, -325f), 20, ShipyardWorkbenchLightMuted).text = (shipyardInventoryPage + 1) + " / " + pages;

            for (int local = 0; local < ShipyardInventoryPageSize; local++)
            {
                int inventoryIndex = shipyardInventoryPage * ShipyardInventoryPageSize + local;
                if (inventoryIndex >= stored.Length) break;
                OwnedShipComponent item = stored[inventoryIndex];
                ComponentArtEntry entry = componentArt.GetById(item.ComponentId);
                bool selectedCard = selectedShipyardComponentInstanceId == item.InstanceId;
                Button card = CreateShipyardEquipmentButton("Shipyard Equipment Owned " + item.InstanceId, inventory, new Vector2(272f, 130f),
                    new Vector2(0f, 235f - local * 142f), 21, selectedCard, enabled: owned);
                card.GetComponentInChildren<Text>().text = string.Empty;
                if (selectedCard)
                    WorkbenchRule("Shipyard Inventory Selection Mark", card.transform,
                        new Vector2(4f, 112f), new Vector2(-131f, 0f), Html("#A3CEAE"));
                DrawShipyardComponentIcon("Shipyard Inventory Art " + item.InstanceId, card.transform, entry, new Vector2(-91f, 0f), 68f);
                ShipyardText("Shipyard Inventory Name " + item.InstanceId, card.transform, new Vector2(174f, 60f),
                    new Vector2(43f, 23f), 22, selectedCard ? ShipyardWorkbenchLight : ink, TextAnchor.MiddleLeft).text = entry.Name;
                ShipyardText("Shipyard Inventory Meta " + item.InstanceId, card.transform, new Vector2(174f, 48f),
                    new Vector2(43f, -31f), 19, selectedCard ? ShipyardWorkbenchLightMuted : muted, TextAnchor.MiddleLeft).text =
                    ComponentArtCatalog.SlotName(entry.Slot) + " · " + ComponentArtCatalog.RarityName(entry.Rarity);
                card.onClick.AddListener(() =>
                {
                    if (card == null || !card.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) || !OwnsShipyardVessel(index)) return;
                    selectedShipyardComponentInstanceId = item.InstanceId;
                    selectedShipyardComponentSlot = entry.Slot;
                    RefreshShipyardDossierView();
                });
            }
            if (stored.Length == 0)
                ShipyardText("Shipyard Equipment Empty Inventory", inventory, new Vector2(252f, 180f), new Vector2(0f, 40f), 23, ShipyardWorkbenchLightMuted).text =
                    "暂无" + (shipyardInventoryShowAll ? "闲置" : ComponentArtCatalog.SlotName(selectedShipyardComponentSlot)) + "配件\n\n到部件商店购买，\n或卸下现有部件。";
            OwnedShipComponent selected = stored.FirstOrDefault(item => item.InstanceId == selectedShipyardComponentInstanceId);
            ComponentArtEntry selectedEntry = selected == null ? null : componentArt.GetById(selected.ComponentId);
            ComponentSlot comparisonSlot = selectedEntry?.Slot ?? selectedShipyardComponentSlot;
            ComponentArtEntry currentEntry = owned ? ShipComponent(index, comparisonSlot) :
                componentArt.Get((int)comparisonSlot * ComponentArtCatalog.EntriesPerSlot);
            ShipyardComponentComparison comparison = ShipyardComponentComparison.Build(currentEntry, selectedEntry);
            BuildShipyardComparisonCard(page, "Current", "当前装备", currentEntry,
                currentEntry?.Name ?? "未装配", comparison.CurrentText, new Vector2(377f, -28f), ink);
            BuildShipyardComparisonCard(page, "Selected", "待替换配件", selectedEntry,
                selectedEntry?.Name ?? "未选择配件", ShipyardColoredComparison(comparison), new Vector2(747f, -28f), ink);
            Button equip = CreateShipyardEquipmentButton("Shipyard Equip Component", page, new Vector2(716f, 60f),
                new Vector2(562f, -403f), 27, primary: true, enabled: editable && selectedEntry != null);
            equip.GetComponentInChildren<Text>().text = selectedEntry == null ? "选择配件后装配" :
                (InstalledShipComponent(index, selectedEntry.Slot) == null ? "装配至" : "替换") + ComponentArtCatalog.SlotName(selectedEntry.Slot);
            string selectedId = selected?.InstanceId;
            equip.onClick.AddListener(() =>
            {
                if (equip == null || !equip.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) ||
                    selectedId != selectedShipyardComponentInstanceId || !TryEquipOwnedComponent(index, selectedId)) return;
                OnGuidedTutorialComponentEquipped();
                selectedShipyardComponentInstanceId = null;
                RefreshShipyardDossierView();
            });
            ShipyardText("Shipyard Equipment Help", page, new Vector2(1040f, 54f), new Vector2(400f, -481f), 20, muted).text =
                !owned ? "购买此船后可装配，当前为原装预览" : !IsShipyardVesselAtCurrentPort(index) ? "此船未停泊本港，暂时无法装配" :
                "装卸不消耗金币 · 替换下来的配件自动放回库存";
        }

        private static string ShipyardColoredComparison(ShipyardComponentComparison comparison)
        {
            StringBuilder text = new StringBuilder();
            foreach (ShipyardComponentComparisonRow row in comparison.Rows)
            {
                if (text.Length > 0) text.Append('\n');
                string value = row.Label + "　" + row.SelectedValue;
                if (row.HasDelta && Mathf.Abs(row.Delta) > .001f)
                    value = "<color=" + (row.Delta > 0f ? "#174D36" : "#8D3128") + ">" + value + "</color>";
                text.Append(value);
            }
            return text.ToString();
        }

        private void BuildShipyardComparisonCard(Transform parent, string side, string heading, ComponentArtEntry entry, string componentName,
            string stats, Vector2 position, Color ink)
        {
            string prefix = "Shipyard Equipment " + side + " Comparison";
            bool candidate = side == "Selected";
            RectTransform card = CreateShipyardWorkbenchPanel(prefix, parent, new Vector2(346f, 650f), position,
                candidate ? Html("#243E35") : ShipyardWorkbenchSurface,
                candidate ? Html("#70A58A") : ShipyardWorkbenchBorder);
            WorkbenchRule(prefix + " Heading Surface", card, new Vector2(344f, 66f),
                new Vector2(0f, 291f), candidate ? ShipyardWorkbenchAccent : Html("#3C4B43"));
            WorkbenchRule(prefix + " Reading Surface", card, new Vector2(344f, 264f),
                new Vector2(0f, -192f), candidate ? Html("#D8E2D7") : Html("#D9DCD2"));
            ShipyardText(prefix + " Heading", card, new Vector2(302f, 38f), new Vector2(0f, 287f), 25, ShipyardWorkbenchLight).text = heading;
            DrawShipyardComponentIcon(prefix + " Art", card, entry, new Vector2(0f, 158f), 190f);
            if (entry == null)
                ShipyardText(prefix + " Empty Art", card, new Vector2(190f, 160f), new Vector2(0f, 158f), 68, ShipyardWorkbenchLightMuted).text = "—";
            Text name = ShipyardText(prefix + " Name", card, new Vector2(302f, 64f), new Vector2(0f, 27f), 27, ShipyardWorkbenchLight);
            name.text = componentName;
            ShipyardText(prefix + " Slot", card, new Vector2(302f, 30f), new Vector2(0f, -25f), 21, ShipyardWorkbenchLightMuted).text =
                entry == null ? (side == "Selected" ? "从左侧库存选择配件" : "选择配件以填入此槽位") : ComponentArtCatalog.SlotName(entry.Slot);
            WorkbenchRule(prefix + " Divider", card, new Vector2(344f, 2f), new Vector2(0f, -59f), ShipyardWorkbenchBorder);
            CreateTavernReadingText(prefix + " Stats", card, new Vector2(22f, 408f),
                new Vector2(302f, 204f), 24, 1.3f, ink, stats);
        }

        private void BuildShipyardInventoryPageButton(Transform parent, int shipIndex, bool next, int pages, Vector2 position)
        {
            Button button = CreateShipyardEquipmentButton(next ? "Shipyard Equipment Next Page" : "Shipyard Equipment Previous Page", parent,
                new Vector2(70f, 38f), position, 23, enabled: next ? shipyardInventoryPage + 1 < pages : shipyardInventoryPage > 0);
            button.GetComponentInChildren<Text>().text = next ? "→" : "←";
            button.onClick.AddListener(() =>
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable || !IsCurrentShipyardEquipmentView(shipIndex)) return;
                shipyardInventoryPage = Mathf.Clamp(shipyardInventoryPage + (next ? 1 : -1), 0, pages - 1);
                RefreshShipyardDossierView();
            });
        }

        private void DrawShipyardComponentIcon(string name, Transform parent, ComponentArtEntry entry, Vector2 position, float size)
        {
            if (entry == null) return;
            CreateItemIcon(name, parent, ItemIconCatalog.ForComponent(entry), position, size);
        }
    }
}
