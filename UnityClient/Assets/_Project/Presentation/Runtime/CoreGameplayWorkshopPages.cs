using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private enum WorkshopPage { Home = -1, Collection, DeckBuilder, Packs, Upgrade }
        private bool workshopPacksOpen;
        private bool workshopCollectionOpen;

        // Derive the page from the same state used by existing voyage and preview entry points.
        private WorkshopPage CurrentWorkshopPage => workshopPacksOpen ? WorkshopPage.Packs :
            battleCardUpgradeOpen ? WorkshopPage.Upgrade :
            cardWorkshopDeckBuilderOpen ? WorkshopPage.DeckBuilder :
            workshopCollectionOpen || specialCardCollectionOpen ? WorkshopPage.Collection : WorkshopPage.Home;

        private void SelectWorkshopPage(WorkshopPage page)
        {
            if (session == null || session.Phase != CoreDemoPhase.Port || activePortFacility != 4 ||
                BlockIncompleteDeckBuilderNavigation()) return;
            if (CurrentWorkshopPage == page) return;
            HideDeckBuilderHoverPreview();
            RememberDeckBuilderScroll();
            if (CurrentWorkshopPage == WorkshopPage.DeckBuilder)
                battleCardUpgradePage = battleCardCollectionPage;
            else if (page == WorkshopPage.DeckBuilder)
                battleCardCollectionPage = battleCardUpgradePage;
            workshopPacksOpen = page == WorkshopPage.Packs;
            workshopCollectionOpen = page == WorkshopPage.Collection;
            battleCardUpgradeOpen = page == WorkshopPage.Upgrade;
            cardWorkshopDeckBuilderOpen = page == WorkshopPage.DeckBuilder || page == WorkshopPage.Upgrade;
            battleDeckOverviewOpen = false;
            // Special cards remain browsable in collection and deck pages, but have no paid upgrade.
            if (page == WorkshopPage.Home || page == WorkshopPage.Upgrade || page == WorkshopPage.Packs)
                specialCardCollectionOpen = false;
            Render();
        }

        private void BuildWorkshopPageNavigation(RectTransform shell)
        {
            RectTransform surface = CreateRect("Workshop Page Navigation Surface", shell, Vector2.zero, Vector2.zero);
            surface.anchorMin = new Vector2(0f, 1f);
            surface.anchorMax = Vector2.one;
            surface.pivot = new Vector2(.5f, 1f);
            surface.sizeDelta = new Vector2(0f, 92f);
            const float navigationWidth = 1896f;
            RectTransform navigation = CreateRect("Workshop Page Navigation", surface, new Vector2(navigationWidth, 92f), Vector2.zero);
            surface.gameObject.AddComponent<WorkshopResponsiveSurface>().Configure(new Vector2(navigationWidth, 92f), navigation);
            string[] titles = { "查看已有卡牌", "组建牌组", "打开卡包", "卡牌强化" };
            float slotWidth = navigationWidth / titles.Length;
            for (int i = 0; i < titles.Length; i++)
            {
                WorkshopPage page = (WorkshopPage)i;
                bool selected = CurrentWorkshopPage == page;
                Button tab = CreateButton("Workshop Page " + page, navigation, new Vector2(360f, 50f),
                    new Vector2(-navigationWidth * .5f + (i + .5f) * slotWidth, 0f), Gold, Ink, 24);
                tab.GetComponentInChildren<Text>().text = titles[i];
                WoodButtonSkin.SetSelected(tab, selected);
                if (selected)
                {
                    Image line = CreateImage("Workshop Active Page Marker", tab.transform,
                        new Vector2(180f, 3f), new Vector2(0f, -30f), Gold);
                    line.raycastTarget = false;
                }
                tab.onClick.AddListener(() =>
                {
                    if (tab == null || !tab.gameObject.activeInHierarchy || !tab.IsInteractable()) return;
                    SelectWorkshopPage(page);
                });
            }
        }

        private void BuildWorkshopCollectionDetail(RectTransform root)
        {
            RectTransform panel = CreateWorkshopPreviewPaper("Workshop Collection Detail", root,
                new Vector2(744f, 948f), new Vector2(576f, 0f));
            CreateText("Workshop Collection Heading", panel, new Vector2(660f, 50f), new Vector2(0f, 420f),
                32, FontStyle.Bold, TextAnchor.MiddleCenter, WorkshopPaperInk).text = "卡牌档案";
            Image divider = CreateImage("Workshop Detail Divider", panel, new Vector2(622f, 2f),
                new Vector2(0f, 385f), new Color(.46f, .30f, .13f, .32f));
            divider.raycastTarget = false;
            int index = specialCardCollectionOpen ? selectedSpecialCardIndex : selectedBattleCardUpgrade;
            if (index < 0)
            {
                CreateText("Workshop Detail Empty", panel, new Vector2(620f, 120f), Vector2.zero,
                    28, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text = "选择一张已有卡牌\n查看完整卡面与效果";
                return;
            }
            CoreBattleCard card = session.GetBattleCard(index, selectedBattleDeckStage);
            Text name = CreateText("Workshop Detail Card Name", panel, new Vector2(650f, 48f), new Vector2(0f, 344f),
                34, FontStyle.Bold, TextAnchor.MiddleCenter, WorkshopPaperInk);
            name.text = CardFaceLayout.DisplayName(card);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 24;
            name.resizeTextMaxSize = 34;
            string description = specialCardCollectionOpen ? SpecialCardPreviewEffectDescription(card) :
                session.BattleCardFaceDescription(card, selectedBattleDeckStage);
            Rect bounds = CardSchoolArtCatalog.FrameBounds(card);
            float scale = Mathf.Min(420f / bounds.width, 520f / bounds.height);
            RectTransform face = CreateCollectionCardFace(panel, card, "Workshop Selected Card",
                "Workshop Selected Card Art", "Workshop Selected Card Name", scale, description);
            face.anchoredPosition = new Vector2(0f, 34f) - bounds.center * scale;
            if (!specialCardCollectionOpen) RefreshCardSupplyCost(face, card, previewStage: selectedBattleDeckStage);
            Image hit = face.GetComponent<Image>();
            if (hit != null) hit.raycastTarget = true;
            CollectionCardKeywordHover.Bind(face.gameObject, face, card, FormatBattleCardDescription(description));
            CreateText("Workshop Selected Card Ownership", panel, new Vector2(650f, 44f), new Vector2(0f, -276f),
                26, FontStyle.Bold, TextAnchor.MiddleCenter, WorkshopPaperInk).text = specialCardCollectionOpen
                ? "已收录 · 船员 / 部件专属卡牌"
                : "已拥有 " + WorkshopOwnedCardCount(session.GetBaseBattleCardOwnedCopies(index)) + " 张    ·    强化 +" + card.UpgradeLevel;
            string school = CardSchoolArtCatalog.NormalizeSchool(card.SchoolId);
            CreateText("Workshop Selected Card Hint", panel, new Vector2(650f, 48f), new Vector2(0f, -325f),
                23, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text = specialCardCollectionOpen
                ? SpecialCardGrowthDescription(card)
                : WorkshopStageName(selectedBattleDeckStage) + "    /    " +
                    (school == "neutral" ? "无体系" : CoreSchoolUnlockCatalog.Find(school).Name);
            Button deck = CreateButton("Workshop Collection Build Deck", panel, new Vector2(290f, 58f),
                new Vector2(-158f, -414f), Gold, Ink, 25);
            deck.GetComponentInChildren<Text>().text = "前往组建牌组";
            deck.onClick.AddListener(() =>
            {
                if (deck == null || !deck.gameObject.activeInHierarchy || !deck.IsInteractable() ||
                    CurrentWorkshopPage != WorkshopPage.Collection) return;
                SelectWorkshopPage(WorkshopPage.DeckBuilder);
            });
            Button action = CreateButton("Workshop Collection Card Action", panel, new Vector2(290f, 58f),
                new Vector2(158f, -414f), Gold, Ink, 25);
            CoreSpecialCardDefinition source = specialCardCollectionOpen ? CoreSpecialCardCatalog.FindByCardId(card.Id) : null;
            action.GetComponentInChildren<Text>().text = specialCardCollectionOpen ? "查看卡牌来源" : "强化这张卡牌";
            action.interactable = !specialCardCollectionOpen || source != null;
            action.onClick.AddListener(() =>
            {
                if (action == null || !action.gameObject.activeInHierarchy || !action.IsInteractable() ||
                    CurrentWorkshopPage != WorkshopPage.Collection) return;
                if (specialCardCollectionOpen)
                {
                    if (selectedSpecialCardIndex == index) OpenWorkshopSpecialCardSource(source);
                }
                else if (selectedBattleCardUpgrade == index) SelectWorkshopPage(WorkshopPage.Upgrade);
            });
        }

    }
}
