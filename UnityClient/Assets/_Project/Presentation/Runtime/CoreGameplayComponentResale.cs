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
        private string pendingComponentSaleId;
        private const int ComponentResalePageSize = 5;

        public int OwnedComponentSalePrice(string instanceId)
        {
            OwnedShipComponent item = OwnedComponents.FirstOrDefault(value => value.InstanceId == instanceId);
            ComponentArtEntry entry = item == null ? null : componentArt.GetById(item.ComponentId);
            return entry == null || session == null ? 0 :
                PortComponentShopCatalog.CalculateSalePrice(entry, session.CurrentPort, item.Quality);
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
                selectedComponentShopOfferId = null;
                Render();
            });
        }

        private void BuildComponentResale(RectTransform parent)
        {
            Color ink = Html("#302116"), muted = Html("#71583A");
            RectTransform sheet = CreateFixedPanel("Component Resale Panel", parent,
                new Vector2(1640f, 640f), Vector2.zero, Html("#E4C997"));
            if (cardWorkshopArt?.CollectionParchment != null)
            {
                RawImage paper = CreateRawImage("Component Resale Parchment", sheet, new Vector2(1640f, 640f),
                    Vector2.zero, cardWorkshopArt.CollectionParchment, new Rect(0f, .2f, 1f, .6f));
                paper.raycastTarget = false;
            }
            Text title = CreateText("Component Resale Title", sheet, new Vector2(1500f, 42f),
                new Vector2(0f, 267f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            title.text = session.CurrentPort + " · 闲置部件回收";
            Text rule = CreateText("Component Resale Rule", sheet, new Vector2(1500f, 30f),
                new Vector2(0f, 226f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
            rule.text = "按本港同品质估价的 50% 回收；已装配部件须先在皇家船坞卸下。";

            OwnedShipComponent[] items = OwnedComponents.Where(item => item.ShipIndex == -1)
                .OrderBy(item => (int)componentArt.GetById(item.ComponentId).Slot)
                .ThenByDescending(item => (int)componentArt.GetById(item.ComponentId).Rarity)
                .ThenBy(item => item.InstanceId, StringComparer.Ordinal).ToArray();
            int pages = Math.Max(1, (items.Length + ComponentResalePageSize - 1) / ComponentResalePageSize);
            componentResalePage = Mathf.Clamp(componentResalePage, 0, pages - 1);
            if (items.Length == 0)
            {
                Text empty = CreateText("Component Resale Empty", sheet, new Vector2(1300f, 120f), Vector2.zero,
                    25, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
                empty.text = "暂无闲置部件\n卸下或航行获得的部件可在此出售。";
            }
            for (int row = 0; row < ComponentResalePageSize; row++)
            {
                int index = componentResalePage * ComponentResalePageSize + row;
                if (index >= items.Length) break;
                OwnedShipComponent item = items[index];
                ComponentArtEntry entry = componentArt.GetById(item.ComponentId);
                int price = OwnedComponentSalePrice(item.InstanceId);
                RectTransform line = CreateFixedPanel("Component Resale Row " + item.InstanceId, sheet,
                    new Vector2(1500f, 72f), new Vector2(0f, 148f - row * 82f), new Color(.25f, .13f, .04f, .09f));
                CreateItemIcon("Component Resale Icon " + item.InstanceId, line,
                    ItemIconCatalog.ForComponent(entry), new Vector2(-680f, 0f), 48f);
                Text name = CreateText("Component Resale Name " + item.InstanceId, line, new Vector2(460f, 64f),
                    new Vector2(-393f, 0f), 21, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
                name.text = entry.Name + "\n" + ComponentArtCatalog.SlotName(entry.Slot) + " · " + ComponentArtCatalog.RarityName(entry.Rarity);
                Text quality = CreateText("Component Resale Quality " + item.InstanceId, line, new Vector2(240f, 64f),
                    new Vector2(-22f, 0f), 20, FontStyle.Normal, TextAnchor.MiddleCenter, muted);
                quality.text = "品质 " + Mathf.RoundToInt(item.Quality * 100f) + "%\n未装配";
                Text value = CreateText("Component Resale Price " + item.InstanceId, line, new Vector2(240f, 64f),
                    new Vector2(268f, 0f), 23, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
                value.text = "回收 " + price + " 金币";
                Button choose = CreateButton("Select Component Sale " + item.InstanceId, line,
                    new Vector2(230f, 44f), new Vector2(584f, 0f), Gold, DeepSea, 21);
                choose.GetComponentInChildren<Text>().text = "出售此部件";
                choose.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive &&
                    session.ProfileGold + price <= CoreEconomyBalance.MaximumProfileGold;
                choose.onClick.AddListener(() => { pendingComponentSaleId = item.InstanceId; Render(); });
            }
            Button back = CreateButton("Back To Component Shop Sales", sheet, new Vector2(320f, 42f),
                new Vector2(-553f, -269f), Gold, DeepSea, 21);
            back.GetComponentInChildren<Text>().text = "返回部件商店";
            back.onClick.AddListener(() => { componentResaleOpen = false; pendingComponentSaleId = null; Render(); });
            Button previous = CreateButton("Component Resale Previous", sheet, new Vector2(160f, 42f),
                new Vector2(10f, -269f), PanelRaised, Ink, 20);
            previous.GetComponentInChildren<Text>().text = "上一页";
            previous.interactable = componentResalePage > 0;
            previous.onClick.AddListener(() => { componentResalePage--; pendingComponentSaleId = null; Render(); });
            Text page = CreateText("Component Resale Page", sheet, new Vector2(270f, 42f),
                new Vector2(258f, -269f), 19, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            page.text = (componentResalePage + 1) + " / " + pages + " 页 · " + items.Length + " 件闲置";
            Button next = CreateButton("Component Resale Next", sheet, new Vector2(160f, 42f),
                new Vector2(553f, -269f), PanelRaised, Ink, 20);
            next.GetComponentInChildren<Text>().text = "下一页";
            next.interactable = componentResalePage + 1 < pages;
            next.onClick.AddListener(() => { componentResalePage++; pendingComponentSaleId = null; Render(); });
            if (!string.IsNullOrEmpty(pendingComponentSaleId)) BuildComponentSaleConfirmation(parent);
        }

        private void BuildComponentSaleConfirmation(RectTransform parent)
        {
            OwnedShipComponent item = OwnedComponents.FirstOrDefault(value => value.InstanceId == pendingComponentSaleId && value.ShipIndex == -1);
            if (item == null) { pendingComponentSaleId = null; return; }
            ComponentArtEntry entry = componentArt.GetById(item.ComponentId);
            int quotedPrice = OwnedComponentSalePrice(item.InstanceId);
            string quotedPort = session.CurrentPort, identity = item.InstanceId;
            RectTransform shade = CreateFixedPanel("Component Sale Confirmation Shade", parent,
                new Vector2(1820f, 700f), Vector2.zero, new Color(0f, 0f, 0f, .72f));
            shade.GetComponent<Image>().raycastTarget = true;
            RectTransform dialog = CreateFixedPanel("Component Sale Confirmation", shade,
                new Vector2(920f, 390f), Vector2.zero, Html("#2A211A"));
            Text title = CreateText("Component Sale Confirmation Title", dialog, new Vector2(840f, 50f),
                new Vector2(0f, 132f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            title.text = "确认出售「" + entry.Name + "」？";
            Text body = CreateText("Component Sale Confirmation Terms", dialog, new Vector2(800f, 135f),
                new Vector2(0f, 20f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            body.text = ComponentArtCatalog.RarityName(entry.Rarity) + " · 品质 " + Mathf.RoundToInt(item.Quality * 100f) +
                "%\n获得 " + quotedPrice + " 金币\n这件部件将从库存移除，出售后无法撤回。";
            Button cancel = CreateButton("Cancel Component Sale", dialog, new Vector2(350f, 52f),
                new Vector2(-210f, -124f), PanelRaised, Ink, 23);
            cancel.GetComponentInChildren<Text>().text = "保留部件";
            cancel.onClick.AddListener(() => { pendingComponentSaleId = null; Render(); });
            Button confirm = CreateButton("Confirm Component Sale", dialog, new Vector2(350f, 52f),
                new Vector2(210f, -124f), Gold, DeepSea, 23);
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
