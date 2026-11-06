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
            bool owned = OwnsShipyardVessel(index);
            bool editable = Application.isPlaying && !editorPreviewBuilding && IsShipyardVesselAtCurrentPort(index);
            BuildShipyardEquipmentVessel(page, index);
            Vector2[] slotPositions = { new Vector2(-350f, -303f), new Vector2(-350f, 236f),
                new Vector2(-750f, -303f), new Vector2(-550f, -454f), new Vector2(-750f, 236f) };

            for (int slotIndex = 0; slotIndex < ComponentArtCatalog.SlotCount; slotIndex++)
            {
                ComponentSlot slot = (ComponentSlot)slotIndex;
                ComponentArtEntry entry = owned ? ShipComponent(index, slot) : componentArt.Get(slotIndex * ComponentArtCatalog.EntriesPerSlot);
                bool active = !shipyardInventoryShowAll && selectedShipyardComponentSlot == slot;
                Button row = CreateShipyardComponentCard("Shipyard Equipment Slot " + slot, page, new Vector2(360f, 128f),
                    slotPositions[slotIndex], entry, active);
                DrawShipyardComponentIcon("Shipyard Installed Art " + slot, row.transform,
                    entry ?? componentArt.Get(slotIndex * ComponentArtCatalog.EntriesPerSlot), new Vector2(-114f, 0f), 108f);
                Text installedName = ShipyardText("Shipyard Installed Name " + slot, row.transform, new Vector2(200f, 72f),
                    new Vector2(52f, 0f), 22, WoodButtonSkin.Ink, TextAnchor.MiddleLeft);
                installedName.text = entry?.Name ?? "未装配";
                installedName.color = WoodButtonSkin.Ink;
                row.onClick.AddListener(() =>
                {
                    if (row == null || !row.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index)) return;
                    selectedShipyardComponentSlot = slot;
                    selectedShipyardComponentInstanceId = null;
                    shipyardInventoryShowAll = false;
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
                new Vector2(-40f, 331f), 21, ink, TextAnchor.MiddleLeft).text =
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
                new Vector2(0f, -325f), 20, muted).text = (shipyardInventoryPage + 1) + " / " + pages;

            for (int local = 0; local < ShipyardInventoryPageSize; local++)
            {
                int inventoryIndex = shipyardInventoryPage * ShipyardInventoryPageSize + local;
                if (inventoryIndex >= stored.Length) break;
                OwnedShipComponent item = stored[inventoryIndex];
                ComponentArtEntry entry = componentArt.GetById(item.ComponentId);
                bool selectedCard = selectedShipyardComponentInstanceId == item.InstanceId;
                Button card = CreateShipyardComponentCard("Shipyard Equipment Owned " + item.InstanceId, inventory, new Vector2(272f, 130f),
                    new Vector2(0f, 235f - local * 142f), entry, selectedCard, enabled: owned);
                DrawShipyardComponentIcon("Shipyard Inventory Art " + item.InstanceId, card.transform, entry, new Vector2(-91f, 0f), 68f);
                Text inventoryName = ShipyardText("Shipyard Inventory Name " + item.InstanceId, card.transform, new Vector2(174f, 60f),
                    new Vector2(43f, 0f), 22, WoodButtonSkin.Ink, TextAnchor.MiddleLeft);
                inventoryName.text = entry.Name;
                inventoryName.color = WoodButtonSkin.Ink;
                card.onClick.AddListener(() =>
                {
                    if (card == null || !card.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) || !OwnsShipyardVessel(index)) return;
                    selectedShipyardComponentInstanceId = item.InstanceId;
                    selectedShipyardComponentSlot = entry.Slot;
                    RefreshShipyardDossierView();
                });
            }
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
            OwnedShipComponent installed = owned ? InstalledShipComponent(index, comparisonSlot) : null;
            Button remove = CreateShipyardEquipmentButton("Shipyard Unequip Component " + comparisonSlot, page, new Vector2(346f, 60f),
                new Vector2(377f, -403f), 27, primary: true, enabled: editable && installed != null);
            remove.GetComponentInChildren<Text>().text = "卸下" + ComponentArtCatalog.SlotName(comparisonSlot);
            string installedId = installed?.InstanceId;
            remove.onClick.AddListener(() =>
            {
                Vector2 visualOrigin = CapturePortActionOrigin("Shipyard Installed Art " + comparisonSlot);
                if (remove == null || !remove.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) ||
                    InstalledShipComponent(index, comparisonSlot)?.InstanceId != installedId || !TryUnequipShipComponent(index, comparisonSlot)) return;
                selectedShipyardComponentInstanceId = null;
                shipyardInventoryShowAll = false;
                selectedShipyardComponentSlot = comparisonSlot;
                shipyardInventoryPage = 0;
                RefreshShipyardDossierView();
                PlayShipyardRefitFeedback(index, comparisonSlot, currentEntry, null, installedId, visualOrigin);
            });
            Button equip = CreateShipyardEquipmentButton("Shipyard Equip Component", page, new Vector2(346f, 60f),
                new Vector2(747f, -403f), 27, primary: true, enabled: editable && selectedEntry != null);
            equip.GetComponentInChildren<Text>().text = selectedEntry == null ? "选择配件后装配" :
                (InstalledShipComponent(index, selectedEntry.Slot) == null ? "装配至" : "替换") + ComponentArtCatalog.SlotName(selectedEntry.Slot);
            string selectedId = selected?.InstanceId;
            equip.onClick.AddListener(() =>
            {
                Vector2 visualOrigin = CapturePortActionOrigin("Shipyard Equipment Selected Comparison Art");
                if (equip == null || !equip.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) ||
                    selectedId != selectedShipyardComponentInstanceId || !TryEquipOwnedComponent(index, selectedId)) return;
                OnGuidedTutorialComponentEquipped();
                selectedShipyardComponentInstanceId = null;
                RefreshShipyardDossierView();
                PlayShipyardRefitFeedback(index, comparisonSlot, currentEntry, ShipComponent(index, comparisonSlot), selectedId, visualOrigin);
            });
            if (!owned || !IsShipyardVesselAtCurrentPort(index))
                ShipyardText("Shipyard Equipment Help", page, new Vector2(1040f, 54f), new Vector2(400f, -481f), 20, muted).text =
                    !owned ? "购买此船后可装配，当前为原装预览" : "此船未停泊本港，暂时无法装配";
        }

        private Button CreateShipyardComponentCard(string name, Transform parent, Vector2 size, Vector2 position,
            ComponentArtEntry entry, bool selected, bool enabled = true)
        {
            Button button = CreateButton(name, parent, size, position, Color.white, WoodButtonSkin.Ink, 22);
            button.interactable = enabled;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.GetComponentInChildren<Text>().text = string.Empty;
            ComponentRarityWoodSkin.Apply(button, entry?.Rarity ?? ComponentRarity.Common);
            ComponentRarityWoodSkin.SetSelected(button, selected);
            return button;
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
                candidate ? Html("#FFF4D8") : ShipyardWorkbenchSurface,
                candidate ? Html("#A06D28") : ShipyardWorkbenchBorder);
            WorkbenchRule(prefix + " Heading Surface", card, new Vector2(344f, 66f),
                new Vector2(0f, 291f), candidate ? Html("#EED39D") : Html("#F6E6C2"));
            WorkbenchRule(prefix + " Reading Surface", card, new Vector2(344f, 264f),
                new Vector2(0f, -192f), Color.white);
            ShipyardText(prefix + " Heading", card, new Vector2(302f, 38f), new Vector2(0f, 287f), 25, ink).text = heading;
            Image board = CreateImage(prefix + " Component Board", card, new Vector2(314f, 124f), Vector2.zero,
                Color.white, ComponentRarityWoodSkin.FaceFor(entry?.Rarity ?? ComponentRarity.Common) ?? WoodButtonSkin.Face);
            board.type = Image.Type.Sliced;
            if (board.sprite != null) board.pixelsPerUnitMultiplier = board.sprite.border.x / 14f;
            board.raycastTarget = false;
            DrawShipyardComponentIcon(prefix + " Art", board.transform, entry, Vector2.zero, 106f);
            if (entry == null)
                ShipyardText(prefix + " Empty Art", board.transform, new Vector2(290f, 80f), Vector2.zero,
                    24, WoodButtonSkin.Ink).text = componentName;
            WorkbenchRule(prefix + " Divider", card, new Vector2(344f, 2f), new Vector2(0f, -59f), ShipyardWorkbenchBorder);
            CreateTavernReadingText(prefix + " Stats", card, new Vector2(22f, 408f),
                new Vector2(302f, 204f), 24, 1.3f, ink, stats);
            BuildShipyardAttachedCard(card, prefix, entry, ink);
        }

        private void BuildShipyardAttachedCard(RectTransform parent, string prefix, ComponentArtEntry entry, Color ink)
        {
            WorkbenchRule(prefix + " Attached Card Divider", parent, new Vector2(302f, 1f), Vector2.zero, ShipyardWorkbenchBorder);
            ShipyardText(prefix + " Attached Card Heading", parent, new Vector2(302f, 30f), Vector2.zero,
                20, ShipyardWorkbenchMuted, TextAnchor.MiddleLeft).text = "附带卡牌";
            CoreSpecialCardDefinition definition = entry == null ? null : CoreSpecialCardCatalog.ForComponent(entry.Id);
            if (definition == null)
            {
                ShipyardText(prefix + " Attached Card Name", parent, new Vector2(302f, 72f), Vector2.zero,
                    22, ShipyardWorkbenchMuted).text = entry == null ? "—" : "无附带卡牌";
                return;
            }

            int index = SpecialCardIndex(definition.Card.Id);
            CoreBattleCard data = index < 0 || session == null ? definition.Card : session.GetBattleCard(index);
            Rect bounds = CardSchoolArtCatalog.FrameBounds(data);
            RectTransform preview = CreateRect(prefix + " Attached Card Preview", parent, bounds.size, Vector2.zero);
            string description = SpecialCardPreviewEffectDescription(data);
            RectTransform face = CreateCollectionCardFace(preview, data, prefix + " Attached Card",
                prefix + " Attached Card Art", prefix + " Attached Card Face Name", 1f,
                effectDescription: description);
            face.anchoredPosition = -bounds.center;
            foreach (Graphic graphic in preview.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            Image hit = preview.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            CollectionCardKeywordHover.Bind(preview.gameObject, face, data, FormatBattleCardDescription(description));
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
