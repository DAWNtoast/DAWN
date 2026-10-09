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
        private string shipyardEquipmentNotice = string.Empty;

        private void ResetShipyardEquipmentSelection()
        {
            selectedShipyardComponentInstanceId = null;
            selectedShipyardComponentSlot = ComponentSlot.Bow;
            shipyardInventoryPage = 0;
            shipyardInventoryShowAll = true;
            shipyardEquipmentNotice = string.Empty;
        }

        private bool IsCurrentShipyardEquipmentView(int index)
        {
            return session != null && session.Phase == CoreDemoPhase.Port && activePortFacility == 1 &&
                shipyardDetailOpen && selectedShipPreview == index && shipyardDossierTab == ShipyardDossierTab.Equipment;
        }

        private void BuildShipyardEquipmentPage(Transform detail, int index, Color ink, Color muted)
        {
            RectTransform page = CreateRect("Shipyard Equipment Page", detail, new Vector2(1440f, 370f), Vector2.zero);
            bool owned = OwnsShipyardVessel(index);
            bool editable = Application.isPlaying && !editorPreviewBuilding && owned;
            ShipyardText("Shipyard Equipment Installed Heading", page, new Vector2(550f, 30f),
                new Vector2(-432f, 145f), 25, ink, TextAnchor.MiddleLeft).text = owned ? "本船当前装配" : "船厂原装配置 · 购买后可改装";

            for (int slotIndex = 0; slotIndex < ComponentArtCatalog.SlotCount; slotIndex++)
            {
                ComponentSlot slot = (ComponentSlot)slotIndex;
                OwnedShipComponent installed = owned ? InstalledShipComponent(index, slot) : null;
                ComponentArtEntry entry = owned ? ShipComponent(index, slot) : componentArt.Get(slotIndex * ComponentArtCatalog.EntriesPerSlot);
                float y = 98f - slotIndex * 70f;
                Button row = CreateButton("Shipyard Equipment Slot " + slot, page, new Vector2(568f, 60f),
                    new Vector2(-432f, y), Gold, Ink, 23);
                row.GetComponentInChildren<Text>().text = string.Empty;
                WoodButtonSkin.SetSelected(row, !shipyardInventoryShowAll && selectedShipyardComponentSlot == slot);
                DrawShipyardComponentIcon("Shipyard Installed Art " + slot, row.transform, entry, new Vector2(-243f, 0f), 48f);
                ShipyardText("Shipyard Installed Name " + slot, row.transform, new Vector2(335f, 28f),
                    new Vector2(-43f, 14f), 23, Ink, TextAnchor.MiddleLeft).text = ComponentArtCatalog.SlotName(slot) + " · " + (entry?.Name ?? "未装配");
                ShipyardText("Shipyard Installed Quality " + slot, row.transform, new Vector2(335f, 25f),
                    new Vector2(-43f, -14f), 19, MutedInk, TextAnchor.MiddleLeft).text = entry == null ? "空槽 · 可从库存选择配件" :
                    ComponentArtCatalog.RarityName(entry.Rarity) + "　品质 " + Mathf.RoundToInt((installed?.Quality ?? 1f) * 100f) + "%";
                row.onClick.AddListener(() =>
                {
                    if (row == null || !row.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index)) return;
                    selectedShipyardComponentSlot = slot;
                    selectedShipyardComponentInstanceId = null;
                    shipyardInventoryShowAll = false;
                    shipyardInventoryPage = 0;
                    shipyardEquipmentNotice = string.Empty;
                    RefreshShipyardDossierView();
                });
                Button remove = CreateButton("Shipyard Unequip Component " + slot, page, new Vector2(102f, 40f),
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
                    shipyardEquipmentNotice = "已卸下「" + entry.Name + "」，部件已放回库存。";
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
                    shipyardEquipmentNotice = string.Empty;
                    RefreshShipyardDossierView();
                });
            }
            if (stored.Length == 0)
                ShipyardText("Shipyard Equipment Empty Inventory", page, new Vector2(680f, 115f), new Vector2(347f, 48f), 25, muted).text =
                    "暂无" + (shipyardInventoryShowAll ? "闲置" : ComponentArtCatalog.SlotName(selectedShipyardComponentSlot)) + "配件\n到部件商店购买，或卸下现有部件。";
            ShipyardText("Shipyard Equipment Inventory Pagination", page, new Vector2(680f, 26f),
                new Vector2(347f, -53f), 19, muted, TextAnchor.MiddleRight).text = (shipyardInventoryPage + 1) + " / " + pages + "　·　其他船只的配件须先卸下";

            OwnedShipComponent selected = OwnedComponents.FirstOrDefault(item => item.InstanceId == selectedShipyardComponentInstanceId && item.ShipIndex < 0);
            ComponentArtEntry selectedEntry = selected == null ? null : componentArt.GetById(selected.ComponentId);
            Text summary = ShipyardText("Shipyard Equipment Selection Summary", page, new Vector2(680f, 86f),
                new Vector2(347f, -110f), 23, ink, TextAnchor.UpperLeft);
            summary.text = !owned ? "购入此船后可拆装配件。" : selectedEntry == null ?
                "选择右侧部件后装配。\n旧件入库，拆装免费。" :
                selectedEntry.Name + " · " + selectedEntry.Specialization + "\n" + selectedEntry.Effect;
            Button equip = CreateButton("Shipyard Equip Component", page, new Vector2(420f, 46f), new Vector2(479f, -186f), Gold, Ink, 24);
            equip.GetComponentInChildren<Text>().text = selectedEntry == null ? "请选择库存配件" :
                (InstalledShipComponent(index, selectedEntry.Slot) == null ? "装配至" : "替换") + ComponentArtCatalog.SlotName(selectedEntry.Slot);
            equip.interactable = editable && selectedEntry != null;
            string selectedId = selected?.InstanceId;
            equip.onClick.AddListener(() =>
            {
                if (equip == null || !equip.gameObject.activeInHierarchy || !IsCurrentShipyardEquipmentView(index) ||
                    selectedId != selectedShipyardComponentInstanceId || !TryEquipOwnedComponent(index, selectedId)) return;
                OnGuidedTutorialComponentEquipped();
                selectedShipyardComponentInstanceId = null;
                shipyardEquipmentNotice = "已装配「" + selectedEntry.Name + "」，原配件已归还库存。";
                RefreshShipyardDossierView();
            });
            ShipyardText("Shipyard Equipment Feedback", detail, new Vector2(750f, 30f), new Vector2(-338f, -230f), 21, muted, TextAnchor.MiddleLeft).text =
                string.IsNullOrEmpty(shipyardEquipmentNotice) ? "各船独立装配 · 部件品质与专属卡经验保留" : shipyardEquipmentNotice;
            ReflowShipyardEquipmentPage(page, detail);
        }

        private static void ReflowShipyardEquipmentPage(RectTransform page, Transform detail)
        {
            float scale = GameReadability.Scale;
            Transform[] children = page.Cast<Transform>().ToArray();
            page.sizeDelta = new Vector2(1440f, 310f);
            page.anchoredPosition = new Vector2(0f, -50f);
            RectTransform content = new GameObject("Shipyard Equipment Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(page, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            foreach (Transform child in children)
            {
                child.SetParent(content, false);
                RectTransform box = (RectTransform)child;
                box.anchorMin = box.anchorMax = new Vector2(.5f, 1f);
            }
            void Place(string name, Vector2 size, Vector2 position)
            {
                RectTransform box = content.Find(name) as RectTransform;
                if (box == null) return;
                box.sizeDelta = size; box.anchoredPosition = position;
            }
            Place("Shipyard Equipment Installed Heading", new Vector2(568f, 50f * scale), new Vector2(-432f, -30f * scale));
            Place("Shipyard Equipment Inventory Heading", new Vector2(680f, 50f * scale), new Vector2(347f, -30f * scale));
            Place("Shipyard Equipment All Components", new Vector2(210f, 48f * scale), new Vector2(112f, -91f * scale));
            Place("Shipyard Equipment Previous Page", new Vector2(100f, 48f * scale), new Vector2(570f, -91f * scale));
            Place("Shipyard Equipment Next Page", new Vector2(100f, 48f * scale), new Vector2(680f, -91f * scale));
            for (int index = 0; index < ComponentArtCatalog.SlotCount; index++)
            {
                ComponentSlot slot = (ComponentSlot)index;
                float y = -(120f + index * 114f) * scale;
                Place("Shipyard Equipment Slot " + slot, new Vector2(568f, 102f * scale), new Vector2(-432f, y));
                Place("Shipyard Unequip Component " + slot, new Vector2(102f, 46f * scale), new Vector2(-218f, y));
                RectTransform row = content.Find("Shipyard Equipment Slot " + slot) as RectTransform;
                Text name = row.Find("Shipyard Installed Name " + slot).GetComponent<Text>();
                name.rectTransform.sizeDelta = new Vector2(310f, 54f * scale);
                name.rectTransform.anchoredPosition = new Vector2(-52f, 20f * scale);
                Text quality = row.Find("Shipyard Installed Quality " + slot).GetComponent<Text>();
                quality.rectTransform.sizeDelta = new Vector2(310f, 34f * scale);
                quality.rectTransform.anchoredPosition = new Vector2(-52f, -28f * scale);
            }
            int ordinal = 0;
            foreach (Button card in content.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Shipyard Equipment Owned ", StringComparison.Ordinal)))
            {
                RectTransform box = card.GetComponent<RectTransform>();
                box.sizeDelta = new Vector2(680f, 96f * scale);
                box.anchoredPosition = new Vector2(347f, -(180f + ordinal++ * 108f) * scale);
                foreach (Transform child in card.transform)
                {
                    RectTransform item = (RectTransform)child;
                    if (child.name.StartsWith("Shipyard Inventory Art ", StringComparison.Ordinal)) item.anchoredPosition = new Vector2(-292f, 0f);
                    else if (child.name.StartsWith("Shipyard Inventory Name ", StringComparison.Ordinal)) { item.sizeDelta = new Vector2(544f, 40f * scale); item.anchoredPosition = new Vector2(32f, 22f * scale); }
                    else if (child.name.StartsWith("Shipyard Inventory Meta ", StringComparison.Ordinal)) { item.sizeDelta = new Vector2(544f, 40f * scale); item.anchoredPosition = new Vector2(32f, -22f * scale); }
                }
            }
            Place("Shipyard Equipment Empty Inventory", new Vector2(680f, 120f * scale), new Vector2(347f, -190f * scale));
            float summaryTop = (ordinal == 0 ? 280f : 132f + ordinal * 108f) * scale;
            Place("Shipyard Equipment Inventory Pagination", new Vector2(680f, 34f * scale), new Vector2(347f, -summaryTop - 17f * scale));
            Text summary = content.Find("Shipyard Equipment Selection Summary").GetComponent<Text>();
            summary.rectTransform.sizeDelta = new Vector2(680f, 100f);
            float summaryHeight = MeasureCardTextHeight(summary);
            summary.rectTransform.sizeDelta = new Vector2(680f, summaryHeight);
            summary.rectTransform.anchoredPosition = new Vector2(347f, -summaryTop - 44f * scale - summaryHeight * .5f);
            // Keep the selected action reachable while inspecting either list.
            // The lower left footer is separate from Back and Set Flagship.
            Button equip = content.Find("Shipyard Equip Component").GetComponent<Button>();
            RectTransform equipBox = equip.GetComponent<RectTransform>();
            equipBox.SetParent(detail, false);
            equipBox.anchorMin = equipBox.anchorMax = new Vector2(.5f, .5f);
            equipBox.sizeDelta = new Vector2(420f, 56f * scale);
            equipBox.anchoredPosition = new Vector2(-470f, -319f);
            Text equipLabel = equip.GetComponentInChildren<Text>();
            GameReadability.Body(equipLabel, 22);
            equipLabel.rectTransform.sizeDelta = equipBox.sizeDelta - new Vector2(16f, 8f);
            Text feedback = detail.Find("Shipyard Equipment Feedback").GetComponent<Text>();
            feedback.transform.SetParent(content, false);
            feedback.rectTransform.sizeDelta = new Vector2(568f, 100f);
            float feedbackHeight = MeasureCardTextHeight(feedback);
            feedback.rectTransform.anchorMin = feedback.rectTransform.anchorMax = new Vector2(.5f, 1f);
            feedback.rectTransform.sizeDelta = new Vector2(568f, feedbackHeight);
            feedback.rectTransform.anchoredPosition = new Vector2(-432f, -660f * scale - feedbackHeight * .5f);
            content.sizeDelta = new Vector2(1440f, Mathf.Max(660f * scale + feedbackHeight + 12f,
                summaryTop + 54f * scale + summaryHeight));
            content.anchoredPosition = Vector2.zero;
            foreach (Button button in content.GetComponentsInChildren<Button>())
            {
                Text label = button.transform.Find("Label")?.GetComponent<Text>();
                if (label == null || string.IsNullOrEmpty(label.text)) continue;
                GameReadability.Body(label, 22);
                label.rectTransform.sizeDelta = button.GetComponent<RectTransform>().sizeDelta - new Vector2(16f, 8f);
            }
            Image hit = page.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            page.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = page.gameObject.AddComponent<ScrollRect>(); scroll.viewport = page; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 48f;
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
