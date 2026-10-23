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
        private int componentShopCategory = -1;
        private string componentShopShowroomOfferId;
        private string componentShopClickedOfferId;

        private void BuildComponentShopShowroom(RectTransform panel,
            IReadOnlyList<PortComponentShopStockItem> stock, DateTime now, int soldCount)
        {
            if (componentShopCategory < 0 || componentShopCategory > 4)
                componentShopCategory = stock.Count > 0 ? (int)stock[0].Entry.Slot : (int)ComponentSlot.Bow;
            ComponentSlot slot = (ComponentSlot)componentShopCategory;
            PortComponentShopStockItem[] category = stock.Where(item => item.Entry.Slot == slot).ToArray();
            int pages = ComponentShopShowroomLayout.PageCount(category.Length);
            componentShopDisplayPage = Mathf.Clamp(componentShopDisplayPage, 0, pages - 1);
            int first = componentShopDisplayPage * ComponentShopShowroomLayout.PageSize;
            PortComponentShopStockItem[] visible = category.Skip(first).Take(ComponentShopShowroomLayout.PageSize).ToArray();
            PortComponentShopStockItem selected = visible.FirstOrDefault(item => item.Id == componentShopShowroomOfferId)
                ?? visible.FirstOrDefault();
            componentShopShowroomOfferId = selected?.Id;
            // An automatically displayed item still needs a first catalogue click
            // before another click on that same item can open its dossier.
            componentShopClickedOfferId = null;

            BuildComponentShopCategories(panel, stock, now, soldCount);
            BuildComponentShopCatalogue(panel, stock, now, soldCount, visible, pages);
            RectTransform showcase = CreateRect("Component Shop Showcase", panel, new Vector2(1200f, 810f), Vector2.zero);
            if (selected != null) BuildComponentShopHero(showcase, selected);
            else
            {
                Text empty = ShopText("Component Shop Empty Category", showcase, new Vector2(850f, 120f),
                    new Vector2(0f, 75f), 28, WoodButtonSkin.Ink,
                    "本月暂无" + ComponentArtCatalog.SlotName(slot) + "到货\n可浏览其他分类，或下月再来。", TextAnchor.MiddleCenter);
                AddSceneLabelOutline(empty);
            }
            Text rule = ShopText("Component Shop Regional Rule", panel, new Vector2(1200f, 46f),
                new Vector2(0f, -452f), 17, WoodButtonSkin.Ink,
                "本港主售 " + PortComponentShopCatalog.PreferredSlotLabel(session.CurrentPort) +
                "　·　每游戏月补货　·　下次 " + PortComponentShopCatalog.NextRefreshLabel(now), TextAnchor.MiddleCenter);
            AddSceneLabelOutline(rule);
        }

        private void BuildComponentShopCategories(RectTransform panel,
            IReadOnlyList<PortComponentShopStockItem> stock, DateTime now, int soldCount)
        {
            for (int index = 0; index < 5; index++)
            {
                ComponentSlot slot = (ComponentSlot)index;
                int count = stock.Count(item => item.Entry.Slot == slot);
                Button button = CreateButton("Component Shop Category " + slot, panel, new Vector2(218f, 106f),
                    new Vector2(-800f, 305f - index * 120f), ScenePlaqueColor(.98f), WoodButtonSkin.Ink, 26);
                Text label = button.GetComponentInChildren<Text>();
                label.text = ComponentArtCatalog.SlotName(slot) + "\n<size=17>" + count + " 件到货</size>";
                label.rectTransform.sizeDelta = new Vector2(127f, 82f);
                label.rectTransform.anchoredPosition = new Vector2(33f, 0f);
                ComponentArtEntry icon = stock.FirstOrDefault(item => item.Entry.Slot == slot)?.Entry
                    ?? componentArt.Entries.FirstOrDefault(item => item.Slot == slot);
                if (icon != null)
                {
                    RawImage image = CreateRawImage("Component Shop Category Icon " + slot, button.transform,
                        new Vector2(75f, 75f), new Vector2(-67f, 0f), icon.Texture, new Rect(0, 0, 1, 1));
                    image.raycastTarget = false;
                }
                WoodButtonSkin.SetSelected(button, index == componentShopCategory);
                int captured = index;
                button.onClick.AddListener(() =>
                {
                    componentShopCategory = captured;
                    componentShopDisplayPage = 0;
                    componentShopShowroomOfferId = null;
                    RebuildComponentShopShowroom(panel, stock, now, soldCount);
                });
            }
            BuildComponentResaleEntrance(panel);
            RectTransform resale = panel.GetComponentsInChildren<RectTransform>()
                .First(item => item.name == "Open Component Resale");
            resale.sizeDelta = new Vector2(218f, 66f);
            resale.anchoredPosition = new Vector2(-800f, -327f);
            Text resaleLabel = resale.GetComponentInChildren<Text>();
            resaleLabel.rectTransform.sizeDelta = new Vector2(195f, 48f);
            resaleLabel.fontSize = 20;
        }

        private void BuildComponentShopCatalogue(RectTransform panel, IReadOnlyList<PortComponentShopStockItem> stock,
            DateTime now, int soldCount, PortComponentShopStockItem[] visible, int pages)
        {
            Text title = ShopText("Component Shop Catalogue Title", panel, new Vector2(218f, 35f),
                new Vector2(800f, 410f), 24, WoodButtonSkin.Ink,
                ComponentArtCatalog.SlotName((ComponentSlot)componentShopCategory) + "目录", TextAnchor.MiddleCenter);
            AddSceneLabelOutline(title);
            for (int index = 0; index < visible.Length; index++)
            {
                PortComponentShopStockItem offer = visible[index];
                bool sold = PlayerPrefs.GetInt(ComponentShopPurchaseKey(offer.Id), 0) != 0;
                bool equipped = EquippedComponent(offer.Entry.Slot)?.Id == offer.Entry.Id;
                Button card = CreateButton("Component Shop Offer " + offer.Id, panel, new Vector2(218f, 106f),
                    ComponentShopShowroomLayout.GetDisplay(visible.Length, index).Position,
                    ScenePlaqueColor(.98f), WoodButtonSkin.Ink, 20);
                card.GetComponentInChildren<Text>().text = string.Empty;
                ComponentRarityWoodSkin.Apply(card, offer.Entry.Rarity);
                ComponentRarityWoodSkin.SetSelected(card, offer.Id == componentShopShowroomOfferId);
                RawImage icon = CreateRawImage("Component Shop Thumbnail " + offer.Entry.Id, card.transform,
                    new Vector2(91f, 91f), new Vector2(-57f, 0f), offer.Entry.Texture, new Rect(0, 0, 1, 1));
                icon.raycastTarget = false;
                icon.color = sold ? new Color(.68f, .68f, .68f, 1f) : Color.white;
                ShopText("Component Shop Thumbnail Name " + offer.Id, card.transform, new Vector2(112f, 53f),
                    new Vector2(46f, 11f), 20, WoodButtonSkin.Ink, offer.Entry.Name, TextAnchor.MiddleCenter);
                ShopText("Component Shop Thumbnail State " + offer.Id, card.transform, new Vector2(112f, 27f),
                    new Vector2(46f, -29f), 15, sold || equipped ? Gold : WoodButtonSkin.Ink,
                    sold ? "本月已售出" : equipped ? "已装同款" : offer.Price + " 金币", TextAnchor.MiddleCenter);
                card.onClick.AddListener(() =>
                {
                    if (card == null || !card.gameObject.activeInHierarchy) return;
                    if (componentShopClickedOfferId == offer.Id && componentShopShowroomOfferId == offer.Id)
                    {
                        OpenComponentShopDossier(card, offer);
                        return;
                    }
                    PreviewComponentShopOffer(panel, offer);
                    componentShopClickedOfferId = offer.Id;
                });
            }
            Button previous = CreateButton("Component Shop Previous Display", panel, new Vector2(92f, 50f),
                new Vector2(740f, -279f), ScenePlaqueColor(1f), WoodButtonSkin.Ink, 24);
            previous.GetComponentInChildren<Text>().text = "上一页";
            previous.interactable = componentShopDisplayPage > 0;
            previous.onClick.AddListener(() =>
            {
                componentShopDisplayPage--;
                componentShopShowroomOfferId = null;
                RebuildComponentShopShowroom(panel, stock, now, soldCount);
            });
            Button next = CreateButton("Component Shop Next Display", panel, new Vector2(92f, 50f),
                new Vector2(860f, -279f), ScenePlaqueColor(1f), WoodButtonSkin.Ink, 24);
            next.GetComponentInChildren<Text>().text = "下一页";
            next.interactable = componentShopDisplayPage + 1 < pages;
            next.onClick.AddListener(() =>
            {
                componentShopDisplayPage++;
                componentShopShowroomOfferId = null;
                RebuildComponentShopShowroom(panel, stock, now, soldCount);
            });
            Text page = ShopText("Component Shop Display Page", panel, new Vector2(218f, 34f), new Vector2(800f, -329f),
                20, WoodButtonSkin.Ink, (componentShopDisplayPage + 1) + " / " + pages, TextAnchor.MiddleCenter);
            AddSceneLabelOutline(page);
        }

        private void PreviewComponentShopOffer(RectTransform panel, PortComponentShopStockItem offer)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy || componentShopShowroomOfferId == offer.Id) return;
            RectTransform showcase = panel.GetComponentsInChildren<RectTransform>()
                .FirstOrDefault(item => item.name == "Component Shop Showcase");
            if (showcase == null) return;
            componentShopShowroomOfferId = offer.Id;
            ClearChildren(showcase);
            BuildComponentShopHero(showcase, offer);
            foreach (Button button in panel.GetComponentsInChildren<Button>())
                if (button.name.StartsWith("Component Shop Offer ", StringComparison.Ordinal))
                    ComponentRarityWoodSkin.SetSelected(button, button.name == "Component Shop Offer " + offer.Id);
        }

        private void RebuildComponentShopShowroom(RectTransform panel,
            IReadOnlyList<PortComponentShopStockItem> stock, DateTime now, int soldCount)
        {
            ClearChildren(panel);
            BuildComponentShopShowroom(panel, stock, now, soldCount);
        }

        private void BuildComponentShopHero(RectTransform parent, PortComponentShopStockItem offer)
        {
            ComponentArtEntry entry = offer.Entry;
            Rect uv = ComponentShopDisplayGeometry.For(entry, session.CurrentPort).ModelUv;
            Vector2 maximum = entry.Slot == ComponentSlot.Instrument ? new Vector2(390f, 330f) : new Vector2(1100f, 610f);
            Vector2 size = FitComponentShopArt(uv, entry.ShipLayer, maximum);
            RawImage model = CreateRawImage("Component Shop Hero " + entry.Id, parent, size,
                new Vector2(0f, 25f), entry.ShipLayer, uv);
            model.raycastTarget = true;
            Button inspect = model.gameObject.AddComponent<Button>();
            inspect.targetGraphic = model;
            inspect.transition = Selectable.Transition.None;
            inspect.onClick.AddListener(() => OpenComponentShopDossier(model, offer));
        }

        private void OpenComponentShopDossier(Component source, PortComponentShopStockItem offer)
        {
            if (source == null || !source.gameObject.activeInHierarchy || componentShopShowroomOfferId != offer.Id) return;
            selectedComponentShopOfferId = offer.Id;
            componentDossierOfferId = offer.Id;
            componentDossierPage = 0;
            Render();
        }

        private Text ShopText(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, Color color, string value, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text text = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, alignment, color);
            text.color = color;
            text.text = value;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(15, fontSize - 5);
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Vector2 FitComponentShopArt(Rect uv, Texture texture, Vector2 maximum)
        {
            float width = (texture == null ? 1024f : texture.width) * uv.width;
            float height = (texture == null ? 1024f : texture.height) * uv.height;
            float factor = Mathf.Min(maximum.x / Mathf.Max(1f, width), maximum.y / Mathf.Max(1f, height));
            return new Vector2(width * factor, height * factor);
        }
    }
}
