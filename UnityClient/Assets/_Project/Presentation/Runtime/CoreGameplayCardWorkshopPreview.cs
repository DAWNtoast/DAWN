using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const float WorkshopCollectionCardScale = .76f;
        private const float WorkshopCollectionPagerY = -430f;
        private static readonly Color WorkshopPaperInk = new Color(.19f, .13f, .085f);
        private static readonly Color WorkshopPaperMuted = new Color(.43f, .29f, .15f);

        // Preview and editing share the same left-hand card surface.
        private RectTransform CreateWorkshopCollection(string name, RectTransform paper)
        {
            return CreateRect(name, paper, new Vector2(1080f, 792f), new Vector2(0f, -12f));
        }

        private static Vector2 WorkshopCollectionCardCenter(int slot)
        {
            return new Vector2(-390f + slot % 4 * 260f, 208f - slot / 4 * 404f);
        }

        private Button CreateWorkshopCollectionPageArrow(string name, RectTransform paper, bool forward)
        {
            // Reserve the paper's narrow side margins for paging, clear of cards and their source actions.
            float side = paper.rect.width * .5f - 38f;
            RectTransform rect = CreateRect(name, paper, new Vector2(32f, 76f),
                new Vector2(forward ? side : -side, 0f));
            WoodenPageArrowGraphic wood = rect.gameObject.AddComponent<WoodenPageArrowGraphic>();
            wood.Configure(forward);
            Button arrow = rect.gameObject.AddComponent<Button>();
            arrow.targetGraphic = wood;
            ColorBlock colors = arrow.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.18f, 1.08f, 1f);
            colors.selectedColor = new Color(1.12f, 1.08f, 1f, 1f);
            colors.pressedColor = new Color(.7f, .64f, .56f, 1f);
            colors.disabledColor = new Color(.58f, .56f, .52f, 1f);
            colors.fadeDuration = .1f;
            arrow.colors = colors;
            rect.gameObject.AddComponent<NauticalButtonMotion>();
            return arrow;
        }

        private void BuildCardWorkshopPreview()
        {
            float headerHeight = PhaseSurfaceLayout.GetHeaderHeight(tutorialActive);
            RectTransform panel = CreateRect("Card Workshop Preview Panel", contentRoot,
                new Vector2(1896f, 948f), new Vector2(0f, -headerHeight * .5f));
            // Fit the same composition below the taller guided-tutorial header.
            float scale = Mathf.Min(1f, (1080f - headerHeight - 24f) / 948f);
            panel.localScale = Vector3.one * scale;
            RectTransform paper = CreateWorkshopPreviewPaper("Workshop Preview Parchment", panel,
                new Vector2(1140f, 948f), new Vector2(-378f, 0f));

            for (int i = 0; i < WorkshopBattleStages.Length; i++)
            {
                NavalBattleStage stage = WorkshopBattleStages[i];
                Button tab = CreateButton("Workshop Preview Stage " + stage, paper, new Vector2(238f, 54f),
                    new Vector2(-381f + i * 254f, 415f), Gold, Ink, 25);
                tab.GetComponentInChildren<Text>().text = WorkshopStageName(stage);
                WoodButtonSkin.SetSelected(tab, (!specialCardCollectionOpen || IsDeckBuilderSpecialCardCollection) && stage == selectedBattleDeckStage);
                tab.onClick.AddListener(() =>
                {
                    if (IsDeckBuilderSpecialCardCollection)
                    {
                        SelectWorkshopBattleStage(stage);
                        return;
                    }
                    specialCardCollectionOpen = false;
                    selectedBattleDeckStage = stage;
                    battleCardUpgradePage = 0;
                    selectedBattleCardUpgrade = -1;
                    Render();
                });
            }
            Button special = CreateButton("Open Special Card Collection", paper, new Vector2(238f, 54f),
                new Vector2(381f, 415f), Gold, Ink, 25);
            special.GetComponentInChildren<Text>().text = "特殊卡牌";
            WoodButtonSkin.SetSelected(special, specialCardCollectionOpen);
            special.onClick.AddListener(() =>
            {
                if (special == null || !special.gameObject.activeInHierarchy) return;
                OpenWorkshopSpecialCardCollection(IsDeckBuilderSpecialCardCollection);
            });

            List<int> cards = new List<int>();
            if (specialCardCollectionOpen)
            {
                foreach (string id in session.AcquiredSpecialCardIds)
                {
                    int index = SpecialCardIndex(id);
                    if (index >= 0 && CoreVoyageSession.IsSpecialBattleCard(id) && !cards.Contains(index) &&
                        (!IsDeckBuilderSpecialCardCollection || session.GetBattleCard(index, selectedBattleDeckStage).IsAllowedInStage(selectedBattleDeckStage))) cards.Add(index);
                }
            }
            else
                foreach (int index in session.GetAvailableBattleCards(selectedBattleDeckStage))
                {
                    CoreBattleCard card = session.GetBattleCard(index, selectedBattleDeckStage);
                    if (card != null && !CoreVoyageSession.IsSpecialBattleCard(card.Id) &&
                        string.IsNullOrEmpty(card.RequiredCrew) && string.IsNullOrEmpty(card.RequiredComponent)) cards.Add(index);
                }
            cards.Sort(CoreBattleCardNumbering.CompareLibraryIndices);
            int pages = Mathf.Max(1, (cards.Count + BattleCardCollectionPageSize - 1) / BattleCardCollectionPageSize);
            int page = Mathf.Clamp(specialCardCollectionOpen ? specialCardCollectionPage : battleCardUpgradePage, 0, pages - 1);
            if (specialCardCollectionOpen) specialCardCollectionPage = page;
            else battleCardUpgradePage = page;
            int first = page * BattleCardCollectionPageSize;
            int selected = specialCardCollectionOpen ? selectedSpecialCardIndex : selectedBattleCardUpgrade;
            int selectedSlot = cards.IndexOf(selected);
            if (selectedSlot < first || selectedSlot >= first + BattleCardCollectionPageSize)
                selected = first < cards.Count ? cards[first] : -1;
            if (specialCardCollectionOpen) selectedSpecialCardIndex = selected;
            else selectedBattleCardUpgrade = selected;
            RectTransform collection = CreateWorkshopCollection("Workshop Preview Collection", paper);
            for (int slot = 0; slot < BattleCardCollectionPageSize && first + slot < cards.Count; slot++)
            {
                int index = cards[first + slot];
                CoreBattleCard card = session.GetBattleCard(index, selectedBattleDeckStage);
                Rect bounds = CardSchoolArtCatalog.FrameBounds(card);
                Vector2 center = WorkshopCollectionCardCenter(slot);
                Button select = CreateButton((specialCardCollectionOpen ? "Select Special Card " : "Select Upgrade Card ") + index, collection,
                    bounds.size * WorkshopCollectionCardScale, center, Color.clear, Ink, 16);
                select.GetComponentInChildren<Text>().text = string.Empty;
                select.transition = Selectable.Transition.None;
                RectTransform face = specialCardCollectionOpen
                    ? CreateCollectionCardFace(select.transform, card, "Special Card " + index,
                        "Special Card Icon " + index, "Special Card Name " + index,
                        WorkshopCollectionCardScale, IsDeckBuilderSpecialCardCollection
                            ? session.BattleCardFaceDescription(card, selectedBattleDeckStage) : SpecialCardPreviewEffectDescription(card))
                    : CreateCollectionCardFace(select.transform, card, "Upgrade Collection " + index,
                        "Upgrade Collection Art " + index, "Upgrade Collection Name " + index,
                        WorkshopCollectionCardScale, UpgradeCardEffectDescription(card));
                // Frame bounds, rather than the inner text rectangle, define the visual center.
                face.anchoredPosition = -bounds.center * WorkshopCollectionCardScale;
                if (index == selected)
                {
                    CardSchoolFrameGraphic frame = face.GetComponentInChildren<CardSchoolFrameGraphic>();
                    Outline glow = frame.gameObject.AddComponent<Outline>();
                    glow.effectColor = new Color(1f, .74f, .12f, .8f);
                    glow.effectDistance = new Vector2(2f, -2f);
                }
                if (specialCardCollectionOpen)
                {
                    BuildWorkshopSpecialCardSourceAction(collection, card, index, center, bounds.height * WorkshopCollectionCardScale);
                    select.onClick.AddListener(() => { selectedSpecialCardIndex = index; Render(); });
                }
                else select.onClick.AddListener(() => { selectedBattleCardUpgrade = index; Render(); });
            }
            if (cards.Count == 0)
                CreateText("Workshop Preview Empty", collection, new Vector2(850f, 160f), Vector2.zero,
                    26, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text = specialCardCollectionOpen
                        ? (IsDeckBuilderSpecialCardCollection ? "本阶段暂无已获得的特殊卡牌" : "暂无已获得的特殊卡牌") +
                            "\n招募船员或获得对应部件后收录" : "暂无已解锁的基础卡牌";
            Button previous = CreateWorkshopCollectionPageArrow("Previous Upgrade Cards", paper, false);
            previous.interactable = page > 0;
            previous.onClick.AddListener(() =>
            {
                if (specialCardCollectionOpen) specialCardCollectionPage--;
                else battleCardUpgradePage--;
                Render();
            });
            CreateText("Upgrade Collection Page", paper, new Vector2(430f, 38f), new Vector2(0f, WorkshopCollectionPagerY),
                22, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperInk).text =
                (IsDeckBuilderSpecialCardCollection ? WorkshopStageName(selectedBattleDeckStage) + " " : "收藏 ") +
                cards.Count + " 种　·　第 " + (page + 1) + " / " + pages + " 页";
            Button next = CreateWorkshopCollectionPageArrow("Next Upgrade Cards", paper, true);
            next.interactable = page + 1 < pages;
            next.onClick.AddListener(() =>
            {
                if (specialCardCollectionOpen) specialCardCollectionPage++;
                else battleCardUpgradePage++;
                Render();
            });

            if (IsDeckBuilderSpecialCardCollection)
            {
                RectTransform rail = CreateWorkshopPreviewPaper("Deck Builder Deck Rail", panel,
                    new Vector2(744f, 948f), new Vector2(576f, 0f));
                BuildDeckBuilderCards(panel, rail, null, CurrentDeckBuilderCards);
            }
            else if (battleCardUpgradeOpen && !specialCardCollectionOpen) BuildWorkshopUpgradePanel(panel);
            else BuildWorkshopPreviewActions(panel);
        }

        private RectTransform CreateWorkshopPreviewPaper(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform panel = CreateRect(name, parent, size, position);
            Image paper = CreateImage(name + " Texture", panel, size - Vector2.one * 6f,
                Vector2.zero, Color.white);
            NauticalSurfaceSkin.ApplyReadingPanel(paper);
            if (size.x < 300f || name.EndsWith("Parchment")) paper.pixelsPerUnitMultiplier = 4f;
            paper.raycastTarget = false;
            return panel;
        }

        private void BuildWorkshopPreviewActions(RectTransform root)
        {
            RectTransform actions = CreateRect("Workshop Actions", root, new Vector2(744f, 948f), new Vector2(576f, 0f));
            RectTransform forge = CreateWorkshopPreviewPaper("Workshop Forge Entry", actions, new Vector2(744f, 468f), new Vector2(0f, 240f));
            CreateText("Card Upgrade Heading", forge, new Vector2(660f, 66f), new Vector2(0f, 148f),
                48, FontStyle.Bold, TextAnchor.MiddleCenter, WorkshopPaperInk).text = "卡牌强化";
            RawImage hammer = CreateRawImage("Workshop Upgrade Hammer Icon", forge, new Vector2(250f, 250f),
                new Vector2(0f, -16f), cardWorkshopArt?.UpgradeIcon, new Rect(0f, 0f, 1f, 1f));
            hammer.raycastTarget = false;
            if (specialCardCollectionOpen)
            {
                RectTransform disabled = CreateRect("Workshop Upgrade Disabled Mark", forge, new Vector2(250f, 250f), hammer.rectTransform.anchoredPosition);
                ProhibitionMarkGraphic mark = disabled.gameObject.AddComponent<ProhibitionMarkGraphic>();
                mark.color = Html("#C0392B");
                mark.raycastTarget = false;
            }
            Button open = CreateButton("Open Battle Card Upgrades", forge, new Vector2(570f, 58f), new Vector2(0f, -170f), Gold, Ink, 30);
            open.GetComponentInChildren<Text>().text = "进入卡牌强化";
            open.interactable = !specialCardCollectionOpen && selectedBattleCardUpgrade >= 0;
            open.onClick.AddListener(() =>
            {
                if (specialCardCollectionOpen || selectedBattleCardUpgrade < 0) return;
                cardWorkshopDeckBuilderOpen = true;
                battleCardUpgradeOpen = true;
                battleDeckOverviewOpen = false;
                Render();
            });
            for (int i = 0; i < WorkshopBattleStages.Length; i++)
            {
                NavalBattleStage stage = WorkshopBattleStages[i];
                RectTransform panel = CreateWorkshopPreviewPaper("Workshop Stage " + stage, actions,
                    new Vector2(240f, 468f), new Vector2(-252f + i * 252f, -240f));
                CreateText("Workshop Stage Name " + stage, panel, new Vector2(226f, 54f), new Vector2(0f, 168f),
                    29, FontStyle.Bold, TextAnchor.MiddleCenter, WorkshopPaperInk).text = WorkshopStageName(stage);
                RawImage stageIcon = CreateRawImage("Workshop Stage Icon " + stage, panel, new Vector2(168f, 168f),
                    new Vector2(0f, 48f), cardWorkshopArt?.StageIcon(i), new Rect(0f, 0f, 1f, 1f));
                stageIcon.raycastTarget = false;
                CreateText("Stage Overview Count " + stage, panel, new Vector2(210f, 80f), new Vector2(0f, -82f),
                    25, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperInk).text =
                    "基础 " + session.GetBattleDeck(stage).Count + "/" + CoreVoyageSession.MaximumBaseBattleDeckSize + "\n专属 " + session.GetBonusBattleCards(stage).Count + " 张";
                Button edit = CreateButton("Edit Stage Deck " + stage, panel, new Vector2(198f, 56f), new Vector2(0f, -170f), Gold, Ink, 26);
                edit.GetComponentInChildren<Text>().text = "编辑卡组";
                edit.onClick.AddListener(() => SelectWorkshopBattleStage(stage,
                    !specialCardCollectionOpen && stage == selectedBattleDeckStage ? battleCardUpgradePage : 0));
            }
        }

        private void BuildWorkshopUpgradePanel(RectTransform root)
        {
            RectTransform panel = CreateWorkshopPreviewPaper("Battle Card Upgrade Panel", root,
                new Vector2(744f, 948f), new Vector2(576f, 0f));
            CreateText("Card Upgrade Heading", panel, new Vector2(660f, 66f), new Vector2(0f, 388f),
                48, FontStyle.Bold, TextAnchor.MiddleCenter, WorkshopPaperInk).text = "卡牌强化";
            if (selectedBattleCardUpgrade >= 0) BuildBattleCardUpgradeDetail(panel, selectedBattleCardUpgrade, WorkshopPaperInk, WorkshopPaperMuted);
            Button back = CreateButton("Back From Card Upgrades", panel, new Vector2(260f, 40f), new Vector2(0f, -419f), PanelRaised, Ink, 21);
            back.GetComponentInChildren<Text>().text = "返回卡组预览";
            back.onClick.AddListener(() => { battleCardUpgradeOpen = false; cardWorkshopDeckBuilderOpen = false; battleDeckOverviewOpen = false; Render(); });
        }
    }
}
