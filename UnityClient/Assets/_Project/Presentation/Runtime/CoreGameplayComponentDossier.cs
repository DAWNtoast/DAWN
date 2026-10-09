using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private string componentDossierOfferId;
        private int componentDossierPage;

        private void BuildComponentDossier(RectTransform panel, PortComponentShopStockItem offer)
        {
            ComponentArtEntry entry = offer.Entry;
            if (componentDossierOfferId != offer.Id)
            {
                componentDossierOfferId = offer.Id;
                componentDossierPage = 0;
            }
            Color rarity = ComponentArtCatalog.RarityColor(entry.Rarity);
            Color ink = Html("#302116");
            Color muted = Html("#71583A");
            Color rarityInk = Color.Lerp(rarity, ink, 0.58f);
            bool sold = PlayerPrefs.GetInt(ComponentShopPurchaseKey(offer.Id), 0) != 0;
            bool selected = EquippedComponent(entry.Slot)?.Id == entry.Id;
            bool affordable = session.ProfileGold >= offer.Price;
            RectTransform detail = CreateRect("Component Shop Detail", panel, new Vector2(1640f, 640f), Vector2.zero);
            RawImage paper = CreateRawImage("Component Detail Parchment", detail, new Vector2(1580f, 630f), Vector2.zero,
                cardWorkshopArt == null ? null : cardWorkshopArt.CollectionParchment, new Rect(0f, 0.2f, 1f, 0.6f));
            paper.raycastTarget = false;

            DossierCardText("Component Detail Name", detail, 860f, 48f, new Vector2(300f, 248f), 34, ink, entry.Name, TextAnchor.MiddleLeft);
            DossierCardText("Component Detail Meta", detail, 860f, 34f, new Vector2(300f, 204f), 22, rarityInk,
                ComponentArtCatalog.RarityName(entry.Rarity) + " · " + ComponentArtCatalog.SlotName(entry.Slot), TextAnchor.MiddleLeft);

            string[] pageNames = { "Overview", "Effects", "Story", "SpecialCards" };
            string[] pageTitles = { "部件来历", "装配效果", "航海异闻", "专属卡牌" };
            RectTransform[] pages = new RectTransform[4];
            Button[] tabs = new Button[4];
            for (int index = 0; index < pages.Length; index++)
            {
                pages[index] = CreateRect("Component Dossier " + pageNames[index] + " Page", detail,
                    new Vector2(860f, 300f), new Vector2(300f, -24f));
                tabs[index] = CreateButton("ComponentDossierPage" + pageNames[index], detail,
                    new Vector2(200f, 42f), new Vector2(-30f + index * 220f, 154f), Gold, Ink, 24);
                tabs[index].GetComponentInChildren<Text>().text = pageTitles[index];
            }
            DossierCardText("Component Detail Quality", pages[0], 860f, 34f, new Vector2(0f, 107f), 24, muted,
                "本港鉴定品质　" + Mathf.RoundToInt(offer.Quality * 100f) + "%");
            DossierCardText("Component Detail Specialization", pages[0], 860f, 36f, new Vector2(0f, 65f), 24, muted,
                "专精方向　" + entry.Specialization);
            DossierCardText("Component Detail Description", pages[0], 860f, 174f, new Vector2(0f, -46f), 25, ink, entry.Description);
            DossierCardText("Component Detail Effect", pages[1], 860f, 196f, new Vector2(0f, 36f), 25, ink,
                "装配效果\n" + entry.Effect + "\n\n" + ComponentArtCatalog.EnvironmentSummary(entry));
            DossierCardText("Component Detail Logistics", pages[1], 860f, 74f, new Vector2(0f, -111f), 21, muted,
                "工艺参考　容积 " + entry.Volume + "　·　工时 " + entry.InstallHours + " 小时　·　木材 " + entry.InstallWood +
                "\n购买即装配，旧件入库；在皇家船坞拆装。");
            DossierCardText("Component Detail Story Chapter", pages[2], 860f, 40f, new Vector2(0f, 105f), 26, rarityInk, entry.StoryTitle);
            DossierCardText("Component Detail Story Body", pages[2], 860f, 208f, new Vector2(0f, -28f), 25, ink, entry.StoryBody);
            BuildDossierSpecialCard(pages[3], "Component", CoreSpecialCardCatalog.ForComponent(entry.Id), 860f,
                "普通（白色）与精良（绿色）部件只提供装配属性。稀有（蓝色）及以上部件才带来与其来历相关的特殊卡牌。", ink, muted);

            RectTransform assembly = CreateRect("Component Detail Display Assembly", detail, new Vector2(400f, 440f), new Vector2(-490f, -20f));
            assembly.localScale = Vector3.one * 1.08f;
            BuildComponentShopDisplay(assembly, entry, "Component Detail ", string.Empty,
                sold, true, false, false, false, out _, out _, out _);
            Button chapter = CreateButton("Read Component Story Chapter", detail, new Vector2(340f, 48f), new Vector2(-490f, -257f), Gold, Ink, 20);
            chapter.GetComponentInChildren<Text>().text = "阅读关联篇章 · 第" + entry.StoryChapter.ToString("00") + "篇";
            chapter.onClick.AddListener(() => ShowComponentDossierPage(componentDossierPage == 2 ? 0 : 2, pages, tabs));
            Button back = CreateButton("Back To Component Shop", detail, new Vector2(300f, 48f), new Vector2(20f, -257f), Gold, Ink, 22);
            back.GetComponentInChildren<Text>().text = "返回部件陈列";
            back.onClick.AddListener(() =>
            {
                componentDossierOfferId = null;
                componentDossierPage = 0;
                selectedComponentShopOfferId = null;
                portNotice = "已返回" + session.CurrentPort + "部件商店陈列区。";
                Render();
            });
            Button purchase = CreateButton("Purchase Component " + offer.Id, detail, new Vector2(530f, 48f), new Vector2(465f, -257f), rarity, Ink, 22);
            purchase.GetComponentInChildren<Text>().text = sold ? "本月已售出" : affordable ?
                (selected ? "购买并替换 · " : "购买并装配 · ") + offer.Price + " 金币" : "金币不足 · 还需 " + Math.Max(0, offer.Price - session.ProfileGold);
            purchase.interactable = !sold && affordable;
            purchase.onClick.AddListener(() => PurchaseComponentOffer(offer));
            for (int index = 0; index < pages.Length; index++)
            {
                int captured = index;
                tabs[index].onClick.AddListener(() => ShowComponentDossierPage(captured, pages, tabs));
            }
            ApplyComponentDossierReadingLayout(detail, pages, tabs);
            ShowComponentDossierPage(componentDossierPage, pages, tabs);
        }

        private void ShowComponentDossierPage(int selected, RectTransform[] pages, Button[] tabs)
        {
            componentDossierPage = Mathf.Clamp(selected, 0, pages.Length - 1);
            for (int index = 0; index < pages.Length; index++)
            {
                pages[index].gameObject.SetActive(index == componentDossierPage);
                WoodButtonSkin.SetSelected(tabs[index], index == componentDossierPage);
            }
        }
    }
}
