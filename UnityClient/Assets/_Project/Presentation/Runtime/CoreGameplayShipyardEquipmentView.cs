using System;
using System.Linq;
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
            RectTransform page = CreateRect("Shipyard Equipment Page", detail, new Vector2(1440f, 680f), Vector2.zero);
            bool owned = OwnsShipyardVessel(index);
            bool editable = Application.isPlaying && !editorPreviewBuilding && owned;
            ShipyardText("Shipyard Equipment Installed Heading", page, new Vector2(550f, 30f),
                new Vector2(-432f, 145f), 25, ink, TextAnchor.MiddleLeft).text = owned ? "本船当前装配" : "船厂原装配置 · 购买后可改装";

            for (int slotIndex = 0; slotIndex < ComponentArtCatalog.SlotCount; slotIndex++)
            {
                ComponentSlot slot = (ComponentSlot)slotIndex;
                OwnedShipComponent installed = owned ? InstalledShipComponent(index, slot) : null;
                ComponentArtEntry entry = owned ? ShipComponent(index, slot) : componentArt.Get(slotIndex * ComponentArtCatalog.EntriesPerSlot);
                float y = 86f - slotIndex * 96f;
                Button row = CreateButton("Shipyard Equipment Slot " + slot, page, new Vector2(568f, 84f),
                    new Vector2(-432f, y), Gold, Ink, 23);
                row.GetComponentInChildren<Text>().text = string.Empty;
                WoodButtonSkin.SetSelected(row, !shipyardInventoryShowAll && selectedShipyardComponentSlot == slot);
                DrawShipyardComponentIcon("Shipyard Installed Art " + slot, row.transform, entry, new Vector2(-241f, 0f), 64f);
                ShipyardText("Shipyard Installed Name " + slot, row.transform, new Vector2(326f, 30f),
                    new Vector2(-32f, 18f), 23, Ink, TextAnchor.MiddleLeft).text = ComponentArtCatalog.SlotName(slot) + " · " + (entry?.Name ?? "未装配");
                ShipyardText("Shipyard Installed Quality " + slot, row.transform, new Vector2(326f, 28f),
                    new Vector2(-32f, -18f), 19, MutedInk, TextAnchor.MiddleLeft).text = entry == null ? "空槽 · 可从库存选择配件" :
                    ComponentArtCatalog.RarityName(entry.Rarity) + "　品质 " + Mathf.RoundToInt((installed?.Quality ?? 1f) * 100f) + "%";
                row.onClick.AddListener(() =>
                {
                    if (row == null || !row.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index)) return;
                    selectedShipyardComponentSlot = slot;
                    selectedShipyardComponentInstanceId = null;
                    shipyardInventoryShowAll = false;
                    shipyardInventoryPage = 0;
                    RefreshShipyardDossierView();
                });
                Button remove = CreateButton("Shipyard Unequip Component " + slot, page, new Vector2(102f, 44f),
                    new Vector2(-218f, y), muted, Ink, 22);
                remove.GetComponentInChildren<Text>().text = "卸下";
                remove.interactable = editable && installed != null;
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
            ShipyardText("Shipyard Equipment Inventory Heading", page, new Vector2(425f, 30f),
                new Vector2(188f, 145f), 25, ink, TextAnchor.MiddleLeft).text = "库存配件 · " +
                (shipyardInventoryShowAll ? "全部" : ComponentArtCatalog.SlotName(selectedShipyardComponentSlot)) + "（" + stored.Length + "）";
            Button all = CreateButton("Shipyard Equipment All Components", page, new Vector2(116f, 34f), new Vector2(470f, 146f), Gold, Ink, 20);
            all.GetComponentInChildren<Text>().text = "全部类型";
            all.onClick.AddListener(() =>
            {
                if (all == null || !all.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index)) return;
                shipyardInventoryShowAll = true;
                shipyardInventoryPage = 0;
                RefreshShipyardDossierView();
            });
            BuildShipyardInventoryPageButton(page, index, false, pages, new Vector2(570f, 146f));
            BuildShipyardInventoryPageButton(page, index, true, pages, new Vector2(662f, 146f));

            for (int local = 0; local < ShipyardInventoryPageSize; local++)
            {
                int inventoryIndex = shipyardInventoryPage * ShipyardInventoryPageSize + local;
                if (inventoryIndex >= stored.Length) break;
                OwnedShipComponent item = stored[inventoryIndex];
                ComponentArtEntry entry = componentArt.GetById(item.ComponentId);
                Button card = CreateButton("Shipyard Equipment Owned " + item.InstanceId, page, new Vector2(335f, 76f),
                    new Vector2(170f + (local % 2) * 354f, 88f - (local / 2) * 86f), Gold, Ink, 22);
                card.GetComponentInChildren<Text>().text = string.Empty;
                card.interactable = owned;
                WoodButtonSkin.SetSelected(card, selectedShipyardComponentInstanceId == item.InstanceId);
                DrawShipyardComponentIcon("Shipyard Inventory Art " + item.InstanceId, card.transform, entry, new Vector2(-130f, 0f), 48f);
                ShipyardText("Shipyard Inventory Name " + item.InstanceId, card.transform, new Vector2(235f, 30f),
                    new Vector2(32f, 14f), 22, Ink, TextAnchor.MiddleLeft).text = entry.Name;
                ShipyardText("Shipyard Inventory Meta " + item.InstanceId, card.transform, new Vector2(235f, 28f),
                    new Vector2(32f, -17f), 19, MutedInk, TextAnchor.MiddleLeft).text =
                    ComponentArtCatalog.SlotName(entry.Slot) + " · " + ComponentArtCatalog.RarityName(entry.Rarity) + " · " + Mathf.RoundToInt(item.Quality * 100f) + "%";
                card.onClick.AddListener(() =>
                {
                    if (card == null || !card.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) || !OwnsShipyardVessel(index)) return;
                    selectedShipyardComponentInstanceId = item.InstanceId;
                    selectedShipyardComponentSlot = entry.Slot;
                    RefreshShipyardDossierView();
                });
            }
            if (stored.Length == 0)
                ShipyardText("Shipyard Equipment Empty Inventory", page, new Vector2(680f, 115f), new Vector2(347f, 48f), 25, muted).text =
                    "暂无" + (shipyardInventoryShowAll ? "闲置" : ComponentArtCatalog.SlotName(selectedShipyardComponentSlot)) + "配件\n到部件商店购买，或卸下现有部件。";
            OwnedShipComponent selected = OwnedComponents.FirstOrDefault(item => item.InstanceId == selectedShipyardComponentInstanceId && item.ShipIndex < 0);
            ComponentArtEntry selectedEntry = selected == null ? null : componentArt.GetById(selected.ComponentId);
            ComponentSlot comparisonSlot = selectedEntry?.Slot ?? selectedShipyardComponentSlot;
            OwnedShipComponent current = owned ? InstalledShipComponent(index, comparisonSlot) : null;
            ComponentArtEntry currentEntry = owned ? ShipComponent(index, comparisonSlot) :
                componentArt.Get((int)comparisonSlot * ComponentArtCatalog.EntriesPerSlot);
            ShipyardComponentComparison comparison = ShipyardComponentComparison.Build(currentEntry, current?.Quality ?? 1f,
                selectedEntry, selected?.Quality ?? 1f);
            BuildShipyardComparisonCard(page, "Current", "当前装备 · " + ComponentArtCatalog.SlotName(comparisonSlot),
                currentEntry?.Name ?? "未装配", comparison.CurrentText, new Vector2(170f, -166f), ink, muted);
            BuildShipyardComparisonCard(page, "Selected", "所选配件" + (selectedEntry == null ? string.Empty : " · " + ComponentArtCatalog.SlotName(comparisonSlot)),
                selectedEntry?.Name ?? "未选择配件", comparison.SelectedText, new Vector2(524f, -166f), ink, muted);
            Button equip = CreateButton("Shipyard Equip Component", page, new Vector2(220f, 48f), new Vector2(347f, -316f), Gold, Ink, 24);
            equip.GetComponentInChildren<Text>().text = selectedEntry == null ? "替换部件" :
                (InstalledShipComponent(index, selectedEntry.Slot) == null ? "装配至" : "替换") + ComponentArtCatalog.SlotName(selectedEntry.Slot);
            equip.interactable = editable && selectedEntry != null;
            string selectedId = selected?.InstanceId;
            equip.onClick.AddListener(() =>
            {
                if (equip == null || !equip.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) ||
                    selectedId != selectedShipyardComponentInstanceId || !TryEquipOwnedComponent(index, selectedId)) return;
                OnGuidedTutorialComponentEquipped();
                selectedShipyardComponentInstanceId = null;
                RefreshShipyardDossierView();
            });
        }

        private void BuildShipyardComparisonCard(Transform parent, string side, string heading, string componentName,
            string stats, Vector2 position, Color ink, Color muted)
        {
            string prefix = "Shipyard Equipment " + side + " Comparison";
            RectTransform card = CreateFixedPanel(prefix, parent, new Vector2(335f, 232f), position,
                new Color(0.38f, 0.22f, 0.08f, 0.09f));
            card.GetComponent<Image>().raycastTarget = false;
            Color border = new Color(muted.r, muted.g, muted.b, 0.65f);
            CreateImage(prefix + " Top", card, new Vector2(335f, 2f), new Vector2(0f, 115f), border).raycastTarget = false;
            CreateImage(prefix + " Bottom", card, new Vector2(335f, 2f), new Vector2(0f, -115f), border).raycastTarget = false;
            CreateImage(prefix + " Left", card, new Vector2(2f, 232f), new Vector2(-166.5f, 0f), border).raycastTarget = false;
            CreateImage(prefix + " Right", card, new Vector2(2f, 232f), new Vector2(166.5f, 0f), border).raycastTarget = false;
            ShipyardText(prefix + " Heading", card, new Vector2(305f, 26f), new Vector2(0f, 96f), 20, muted, TextAnchor.MiddleLeft).text = heading;
            Text name = ShipyardText(prefix + " Name", card, new Vector2(305f, 28f), new Vector2(0f, 66f), 22, ink, TextAnchor.MiddleLeft);
            name.text = componentName;
            CreateImage(prefix + " Divider", card, new Vector2(305f, 1f), new Vector2(0f, 48f), border).raycastTarget = false;
            Text values = ShipyardText(prefix + " Stats", card, new Vector2(305f, 150f), new Vector2(0f, -29f), 20, ink, TextAnchor.UpperLeft);
            values.lineSpacing = 1f;
            values.text = stats;
        }

        private void BuildShipyardInventoryPageButton(Transform parent, int shipIndex, bool next, int pages, Vector2 position)
        {
            Button button = CreateButton(next ? "Shipyard Equipment Next Page" : "Shipyard Equipment Previous Page", parent,
                new Vector2(70f, 34f), position, Gold, Ink, 23);
            button.GetComponentInChildren<Text>().text = next ? "→" : "←";
            button.interactable = next ? shipyardInventoryPage + 1 < pages : shipyardInventoryPage > 0;
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
