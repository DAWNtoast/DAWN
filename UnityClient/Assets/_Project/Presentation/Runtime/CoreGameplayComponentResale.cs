using System;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool componentResaleOpen;
        private int componentResalePage;
        private string selectedComponentResaleInstanceId;
        private string pendingComponentSaleId;
        private const int ComponentResalePageSize = 5;

        public int OwnedComponentSalePrice(string instanceId)
        {
            OwnedShipComponent item = OwnedComponents.FirstOrDefault(value => value.InstanceId == instanceId);
            ComponentArtEntry entry = item == null ? null : componentArt.GetById(item.ComponentId);
            return entry == null || session == null ? 0 :
                PortComponentShopCatalog.CalculateSalePrice(entry, session.CurrentPort);
        }

        public bool TrySellOwnedComponent(string instanceId)
        {
            if (!Application.isPlaying || editorPreviewBuilding || tutorialActive || session == null ||
                session.Phase != CoreDemoPhase.Port || componentInventory == null) return false;
            OwnedShipComponent item = OwnedComponents.FirstOrDefault(value => value.InstanceId == instanceId);
            if (item == null || item.ShipIndex != -1) return false;
            int price = OwnedComponentSalePrice(instanceId);
            if (price <= 0 || session.ProfileGold < 0 ||
                session.ProfileGold + price > CoreEconomyBalance.MaximumProfileGold) return false;
            string name = componentArt.GetById(item.ComponentId).Name;
            if (!componentInventory.TryRemoveUninstalled(instanceId)) return false;
            // Synchronous transaction: all rejection conditions precede removal,
            // and the guarded credit cannot be capped or fail afterward.
            session.GrantProfileGold(price, "出售闲置部件「" + name + "」");
            SaveProgress();
            return true;
        }

        private void BuildComponentResaleEntrance(RectTransform panel)
        {
            int count = OwnedComponents.Count(item => item.ShipIndex == -1);
            Button open = CreateButton("Open Component Resale", panel, new Vector2(340f, 48f),
                new Vector2(650f, 265f), Gold, DeepSea, 22);
            open.GetComponentInChildren<Text>().text = "出售闲置部件 · " + count + " 件";
            open.interactable = !tutorialActive;
            open.onClick.AddListener(() =>
            {
                if (tutorialActive) return;
                componentResaleOpen = true;
                pendingComponentSaleId = null;
                componentResalePage = 0;
                selectedComponentResaleInstanceId = null;
                selectedComponentShopOfferId = null;
                Render();
            });
        }

        private void BuildComponentResale(RectTransform parent)
        {
            Color ink = MarketShopSkin.Ink, muted = new Color(.42f, .33f, .24f, 1f);
            RectTransform sheet = CreateFixedPanel("Component Resale Panel", parent,
                new Vector2(1440f, 760f), Vector2.zero, Color.white);
            MarketShopSkin.ApplyPanel(sheet.GetComponent<Image>());
            Text eyebrow = CreateText("Component Resale Eyebrow", sheet, new Vector2(1260f, 26f),
                new Vector2(0f, 328f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            eyebrow.text = "COMPONENT EXCHANGE";
            Text title = CreateText("Component Resale Title", sheet, new Vector2(1260f, 48f),
                new Vector2(0f, 286f), 34, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            title.text = session.CurrentPort + " · 闲置部件回收";
            Text rule = CreateText("Component Resale Rule", sheet, new Vector2(1260f, 32f),
                new Vector2(0f, 241f), 20, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            rule.text = "按本港售价的 50% 回收；已装配部件须先在皇家船坞卸下。";
            ComponentResaleRule(sheet, "Component Resale Heading Divider", 1290f, 212f);

            OwnedShipComponent[] items = OwnedComponents.Where(item => item.ShipIndex == -1)
                .OrderBy(item => (int)componentArt.GetById(item.ComponentId).Slot)
                .ThenByDescending(item => (int)componentArt.GetById(item.ComponentId).Rarity)
                .ThenBy(item => item.InstanceId, StringComparer.Ordinal).ToArray();
            int pages = Math.Max(1, (items.Length + ComponentResalePageSize - 1) / ComponentResalePageSize);
            componentResalePage = Mathf.Clamp(componentResalePage, 0, pages - 1);
            if (items.Length == 0)
            {
                Image emblem = CreateImage("Component Resale Empty Emblem", sheet, new Vector2(14f, 14f),
                    new Vector2(0f, 88f), Html("#AA844B"));
                emblem.sprite = emblem.overrideSprite = null;
                emblem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                emblem.raycastTarget = false;
                Text empty = CreateText("Component Resale Empty", sheet, new Vector2(1120f, 48f),
                    new Vector2(0f, 30f), 30, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
                empty.text = "暂无闲置部件";
                Text detail = CreateText("Component Resale Empty Detail", sheet, new Vector2(1120f, 40f),
                    new Vector2(0f, -20f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
                detail.text = "卸下或航行获得的部件可在此出售。";
            }
            for (int row = 0; row < ComponentResalePageSize; row++)
            {
                int index = componentResalePage * ComponentResalePageSize + row;
                if (index >= items.Length) break;
                OwnedShipComponent item = items[index];
                ComponentArtEntry entry = componentArt.GetById(item.ComponentId);
                int price = OwnedComponentSalePrice(item.InstanceId);
                RectTransform line = CreateRect("Component Resale Row " + item.InstanceId, sheet,
                    new Vector2(1290f, 78f), new Vector2(0f, 150f - row * 86f));
                Image face = line.gameObject.AddComponent<Image>();
                face.color = new Color(.55f, .42f, .24f, row % 2 == 0 ? .065f : .025f);
                face.raycastTarget = true;
                Button inspect = line.gameObject.AddComponent<Button>();
                inspect.targetGraphic = face;
                inspect.transition = Selectable.Transition.None;
                inspect.onClick.AddListener(() => OpenComponentResaleDossier(inspect, item.InstanceId));
                CreateItemIcon("Component Resale Icon " + item.InstanceId, line,
                    ItemIconCatalog.ForComponent(entry), new Vector2(-580f, 0f), 64f);
                Text name = CreateText("Component Resale Name " + item.InstanceId, line, new Vector2(590f, 34f),
                    new Vector2(-230f, 17f), 24, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
                name.text = entry.Name;
                Text category = CreateText("Component Resale Category " + item.InstanceId, line, new Vector2(590f, 28f),
                    new Vector2(-230f, -18f), 18, FontStyle.Normal, TextAnchor.MiddleLeft, muted);
                category.text = ComponentArtCatalog.SlotName(entry.Slot) + " · " + ComponentArtCatalog.RarityName(entry.Rarity);
                Text value = CreateText("Component Resale Price " + item.InstanceId, line, new Vector2(290f, 40f),
                    new Vector2(230f, 0f), 24, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
                value.text = "回收 " + price + " 金币";
                Button choose = CreateButton("Select Component Sale " + item.InstanceId, line,
                    new Vector2(200f, 48f), new Vector2(512f, 0f), Gold, DeepSea, 22);
                choose.GetComponentInChildren<Text>().text = "出售此部件";
                MarketShopSkin.ApplyLightButton(choose);
                choose.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive &&
                    session.ProfileGold + price <= CoreEconomyBalance.MaximumProfileGold;
                choose.onClick.AddListener(() => { pendingComponentSaleId = item.InstanceId; Render(); });
                ComponentResaleRule(line, "Component Resale Row Divider " + item.InstanceId, 1260f, -42f);
            }
            ComponentResaleRule(sheet, "Component Resale Footer Divider", 1290f, -270f);
            Button back = CreateButton("Back To Component Shop Sales", sheet, new Vector2(300f, 50f),
                new Vector2(-475f, -320f), Gold, DeepSea, 23);
            back.GetComponentInChildren<Text>().text = "返回部件商店";
            WoodButtonSkin.SetRole(back, WoodButtonRole.Primary);
            back.onClick.AddListener(() => { componentResaleOpen = false; pendingComponentSaleId = null; Render(); });
            Button previous = CreateButton("Component Resale Previous", sheet, new Vector2(150f, 46f),
                new Vector2(10f, -320f), PanelRaised, Ink, 22);
            previous.GetComponentInChildren<Text>().text = "上一页";
            MarketShopSkin.ApplyLightButton(previous);
            previous.interactable = componentResalePage > 0;
            previous.onClick.AddListener(() => { componentResalePage--; pendingComponentSaleId = null; Render(); });
            Text page = CreateText("Component Resale Page", sheet, new Vector2(290f, 42f),
                new Vector2(275f, -320f), 20, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            page.text = (componentResalePage + 1) + " / " + pages + " 页 · " + items.Length + " 件闲置";
            Button next = CreateButton("Component Resale Next", sheet, new Vector2(150f, 46f),
                new Vector2(550f, -320f), PanelRaised, Ink, 22);
            next.GetComponentInChildren<Text>().text = "下一页";
            MarketShopSkin.ApplyLightButton(next);
            next.interactable = componentResalePage + 1 < pages;
            next.onClick.AddListener(() => { componentResalePage++; pendingComponentSaleId = null; Render(); });
            if (!string.IsNullOrEmpty(pendingComponentSaleId)) BuildComponentSaleConfirmation(parent);
        }

        private void ComponentResaleRule(Transform parent, string name, float width, float y)
        {
            Image line = CreateImage(name, parent, new Vector2(width, 1f), new Vector2(0f, y),
                new Color(.60f, .47f, .30f, .55f));
            line.sprite = line.overrideSprite = null;
            line.raycastTarget = false;
        }

        private void OpenComponentResaleDossier(Button source, string instanceId)
        {
            if (source == null || !source.gameObject.activeInHierarchy || !componentResaleOpen ||
                activePortFacility != PortFacilityArtCatalog.ComponentShopFacilityIndex ||
                !string.IsNullOrEmpty(pendingComponentSaleId) ||
                !OwnedComponents.Any(item => item.InstanceId == instanceId && item.ShipIndex == -1)) return;
            selectedComponentResaleInstanceId = instanceId;
            componentDossierOfferId = null;
            componentDossierPage = 0;
            Render();
        }

        private void BuildComponentSaleConfirmation(RectTransform parent)
        {
            OwnedShipComponent item = OwnedComponents.FirstOrDefault(value => value.InstanceId == pendingComponentSaleId && value.ShipIndex == -1);
            if (item == null) { pendingComponentSaleId = null; return; }
            ComponentArtEntry entry = componentArt.GetById(item.ComponentId);
            int quotedPrice = OwnedComponentSalePrice(item.InstanceId);
            string quotedPort = session.CurrentPort, identity = item.InstanceId;
            RectTransform shade = CreateFixedPanel("Component Sale Confirmation Shade", parent,
                new Vector2(1820f, 930f), Vector2.zero, new Color(0f, 0f, 0f, .68f));
            shade.GetComponent<Image>().raycastTarget = true;
            RectTransform dialog = CreateFixedPanel("Component Sale Confirmation", shade,
                new Vector2(920f, 420f), Vector2.zero, Color.white);
            MarketShopSkin.ApplyPanel(dialog.GetComponent<Image>());
            Text title = CreateText("Component Sale Confirmation Title", dialog, new Vector2(840f, 50f),
                new Vector2(0f, 140f), 30, FontStyle.Bold, TextAnchor.MiddleCenter, MarketShopSkin.Ink);
            title.text = "确认出售「" + entry.Name + "」？";
            ComponentResaleRule(dialog, "Component Sale Confirmation Heading Divider", 780f, 102f);
            Text body = CreateText("Component Sale Confirmation Terms", dialog, new Vector2(800f, 135f),
                new Vector2(0f, 20f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, MarketShopSkin.Ink);
            body.lineSpacing = 1.35f;
            body.text = ComponentArtCatalog.RarityName(entry.Rarity) + " · " + ComponentArtCatalog.SlotName(entry.Slot) +
                "\n获得 " + quotedPrice + " 金币\n这件部件将从库存移除，出售后无法撤回。";
            Button cancel = CreateButton("Cancel Component Sale", dialog, new Vector2(350f, 52f),
                new Vector2(-205f, -134f), PanelRaised, Ink, 23);
            cancel.GetComponentInChildren<Text>().text = "保留部件";
            MarketShopSkin.ApplyLightButton(cancel);
            cancel.onClick.AddListener(() => { pendingComponentSaleId = null; Render(); });
            Button confirm = CreateButton("Confirm Component Sale", dialog, new Vector2(350f, 52f),
                new Vector2(205f, -134f), Gold, DeepSea, 23);
            confirm.GetComponentInChildren<Text>().text = "确认出售 · +" + quotedPrice + " 金";
            confirm.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive &&
                session.ProfileGold + quotedPrice <= CoreEconomyBalance.MaximumProfileGold;
            confirm.onClick.AddListener(() =>
            {
                bool validQuote = componentResaleOpen && activePortFacility == PortFacilityArtCatalog.ComponentShopFacilityIndex &&
                    pendingComponentSaleId == identity && session.CurrentPort == quotedPort && OwnedComponentSalePrice(identity) == quotedPrice;
                pendingComponentSaleId = null;
                bool sold = validQuote && TrySellOwnedComponent(identity);
                portNotice = sold ? "已出售「" + entry.Name + "」，金币 +" + quotedPrice + "。" :
                    "出售未完成：部件状态或报价已变化，请重新选择。";
                if (sold) PlaySfx("trade_confirm", .5f);
                Render();
            });
        }
    }
}
