using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const float WorkshopCollectionCardScale = .78f;
        private static readonly Color WorkshopPaperInk = new Color(.19f, .13f, .085f);
        private static readonly Color WorkshopPaperMuted = new Color(.43f, .29f, .15f);
        private string selectedWorkshopSchool = "neutral";

        private bool MatchesWorkshopSchool(CoreBattleCard card) => card != null &&
            CardSchoolArtCatalog.NormalizeSchool(card.SchoolId) == selectedWorkshopSchool;

        private void BuildWorkshopSchoolFilters(RectTransform paper)
        {
            RectTransform filters = CreateRect("Workshop School Filters", paper,
                new Vector2(172f, 700f), new Vector2(-466f, -6f));
            CreateText("Workshop School Heading", paper, new Vector2(180f, 34f), new Vector2(-466f, 368f),
                25, FontStyle.Bold, TextAnchor.MiddleCenter, WorkshopPaperInk).text = "卡牌体系";
            Image divider = CreateImage("Workshop School Divider", paper, new Vector2(2f, 750f),
                new Vector2(-360f, -28f), new Color(.46f, .30f, .13f, .28f));
            divider.raycastTarget = false;
            for (int i = 0; i <= CardSchoolArtCatalog.SchoolIds.Length; i++)
            {
                string school = i == 0 ? "neutral" : CardSchoolArtCatalog.SchoolIds[i - 1];
                Button filter = CreateButton("Workshop School Filter " + school, filters,
                    new Vector2(150f, 56f), new Vector2(0f, 280f - i * 92f), Gold, Ink, 25);
                filter.GetComponentInChildren<Text>().text = school == "neutral"
                    ? "无体系" : CoreSchoolUnlockCatalog.Find(school).Name;
                WorkshopSchoolButtonGraphic.Apply(filter, school, school == selectedWorkshopSchool);
                filter.onClick.AddListener(() =>
                {
                    if (filter == null || !filter.gameObject.activeInHierarchy || school == selectedWorkshopSchool) return;
                    SelectWorkshopSchool(school);
                });
            }
        }

        private void SelectWorkshopSchool(string school)
        {
            school = CardSchoolArtCatalog.NormalizeSchool(school);
            if (school == selectedWorkshopSchool) return;
            // Filtering remains available while a deck draft still needs more cards.
            RememberDeckBuilderScroll();
            selectedWorkshopSchool = school;
            battleCardUpgradePage = battleCardCollectionPage = specialCardCollectionPage = 0;
            selectedBattleCardUpgrade = selectedSpecialCardIndex = -1;
            Render();
        }

        private string WorkshopSchoolEmptyMessage(bool special)
        {
            string schoolName = selectedWorkshopSchool == "neutral"
                ? "无体系" : CoreSchoolUnlockCatalog.Find(selectedWorkshopSchool).Name + "体系";
            return special ? "当前分类暂无已获得的" + schoolName + "特殊卡牌\n招募船员或获得对应部件后收录"
                : "当前阶段暂无已拥有的" + schoolName + "卡牌\n通过打开卡包获取";
        }

        // Preview and editing share the same left-hand card surface.
        private RectTransform CreateWorkshopCollection(string name, RectTransform paper)
        {
            return CreateRect(name, paper, new Vector2(880f, 832f), new Vector2(100f, -30f));
        }

        private static Vector2 WorkshopCollectionCardCenter(int slot)
        {
            return new Vector2(-280f + slot % 3 * 280f, 224f - slot / 3 * 410f);
        }

        private Button CreateWorkshopCollectionPageArrow(string name, RectTransform paper, bool forward)
        {
            // Keep paging beside the card album, clear of the school index and card actions.
            RectTransform rect = CreateRect(name, paper, new Vector2(24f, 40f),
                new Vector2(forward ? 520f : -320f, -30f));
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
            if (battleCardUpgradeOpen && !specialCardCollectionOpen)
            {
                BuildWorkshopUpgradeWorkspace();
                return;
            }
            RectTransform panel = CreateWorkshopWindowPanel("Card Workshop Preview Panel");
            RectTransform paper = CreateWorkshopPreviewPaper("Workshop Preview Parchment", panel,
                new Vector2(1140f, 948f), new Vector2(-378f, 0f));

            for (int i = 0; i < WorkshopBattleStages.Length; i++)
            {
                NavalBattleStage stage = WorkshopBattleStages[i];
                Button tab = CreateButton("Workshop Preview Stage " + stage, paper, new Vector2(238f, 54f),
                    new Vector2(-381f + i * 254f, 427f), Gold, Ink, 25);
                tab.GetComponentInChildren<Text>().text = WorkshopStageName(stage);
                WoodButtonSkin.SetSelected(tab, (!specialCardCollectionOpen || IsDeckBuilderSpecialCardCollection) && stage == selectedBattleDeckStage);
                tab.onClick.AddListener(() =>
                {
                    if (tab == null || !tab.gameObject.activeInHierarchy || !tab.IsInteractable() ||
                        BlockIncompleteDeckBuilderNavigation()) return;
                    if (IsDeckBuilderSpecialCardCollection)
                    {
                        SelectWorkshopBattleStage(stage);
                        return;
                    }
                    if (!battleCardUpgradeOpen) workshopCollectionOpen = true;
                    specialCardCollectionOpen = false;
                    selectedBattleDeckStage = stage;
                    battleCardUpgradePage = 0;
                    selectedBattleCardUpgrade = -1;
                    Render();
                });
            }
            Button special = CreateButton("Open Special Card Collection", paper, new Vector2(238f, 54f),
                new Vector2(381f, 427f), Gold, Ink, 25);
            special.GetComponentInChildren<Text>().text = "特殊卡牌";
            WoodButtonSkin.SetSelected(special, specialCardCollectionOpen);
            special.interactable = !battleCardUpgradeOpen;
            special.onClick.AddListener(() =>
            {
                if (special == null || !special.gameObject.activeInHierarchy || battleCardUpgradeOpen) return;
                OpenWorkshopSpecialCardCollection(IsDeckBuilderSpecialCardCollection);
            });

            BuildWorkshopSchoolFilters(paper);
            List<int> cards = new List<int>();
            if (specialCardCollectionOpen)
            {
                foreach (string id in session.AcquiredSpecialCardIds)
                {
                    int index = SpecialCardIndex(id);
                    if (index >= 0 && CoreVoyageSession.IsSpecialBattleCard(id) && !cards.Contains(index) &&
                        MatchesWorkshopSchool(session.GetBattleCard(index, selectedBattleDeckStage)) &&
                        (!IsDeckBuilderSpecialCardCollection || session.GetBattleCard(index, selectedBattleDeckStage).IsAllowedInStage(selectedBattleDeckStage))) cards.Add(index);
                }
            }
            else
                foreach (int index in session.GetAvailableBattleCards(selectedBattleDeckStage))
                {
                    CoreBattleCard card = session.GetBattleCard(index, selectedBattleDeckStage);
                    if (MatchesWorkshopSchool(card) && !CoreVoyageSession.IsSpecialBattleCard(card.Id) &&
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
                string description = specialCardCollectionOpen
                    ? IsDeckBuilderSpecialCardCollection
                        ? session.BattleCardFaceDescription(card, selectedBattleDeckStage)
                        : SpecialCardPreviewEffectDescription(card)
                    : UpgradeCardEffectDescription(card);
                RectTransform face = specialCardCollectionOpen
                    ? CreateCollectionCardFace(select.transform, card, "Special Card " + index,
                        "Special Card Icon " + index, "Special Card Name " + index,
                        WorkshopCollectionCardScale, description)
                    : CreateCollectionCardFace(select.transform, card, "Upgrade Collection " + index,
                        "Upgrade Collection Art " + index, "Upgrade Collection Name " + index,
                        WorkshopCollectionCardScale, description);
                if (!specialCardCollectionOpen || IsDeckBuilderSpecialCardCollection)
                    RefreshCardSupplyCost(face, card, previewStage: selectedBattleDeckStage);
                // Frame bounds, rather than the inner text rectangle, define the visual center.
                face.anchoredPosition = -bounds.center * WorkshopCollectionCardScale;
                CollectionCardKeywordHover.Bind(select.gameObject, face, card, FormatBattleCardDescription(description));
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
                else
                {
                    CreateText("Workshop Owned Count " + index, collection, new Vector2(240f, 28f),
                        center - new Vector2(0f, bounds.height * WorkshopCollectionCardScale * .5f + 20f),
                        21, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperInk).text =
                        "拥有 " + WorkshopOwnedCardCount(session.GetBaseBattleCardOwnedCopies(index)) + " 张  ·  +" + card.UpgradeLevel;
                    select.onClick.AddListener(() => { selectedBattleCardUpgrade = index; Render(); });
                }
            }
            if (cards.Count == 0)
                CreateText("Workshop Preview Empty", collection, new Vector2(850f, 160f), Vector2.zero,
                    26, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text = WorkshopSchoolEmptyMessage(specialCardCollectionOpen);
            Button previous = CreateWorkshopCollectionPageArrow("Previous Upgrade Cards", paper, false);
            previous.interactable = page > 0;
            previous.onClick.AddListener(() =>
            {
                if (specialCardCollectionOpen) specialCardCollectionPage--;
                else battleCardUpgradePage--;
                Render();
            });
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
            else BuildWorkshopCollectionDetail(panel);
        }

        private RectTransform CreateWorkshopPreviewPaper(string name, Transform parent, Vector2 size, Vector2 position)
        {
            RectTransform surface = CreateWorkshopRegion(name + " Surface", (RectTransform)parent, size, position);
            Image paper = CreateImage(name + " Texture", surface, size,
                Vector2.zero, Color.white);
            paper.rectTransform.anchorMin = Vector2.zero;
            paper.rectTransform.anchorMax = Vector2.one;
            paper.rectTransform.offsetMin = paper.rectTransform.offsetMax = Vector2.zero;
            NauticalSurfaceSkin.ApplyReadingPanel(paper);
            if (size.x < 300f || name.EndsWith("Parchment")) paper.pixelsPerUnitMultiplier = 4f;
            paper.raycastTarget = false;
            // The paper follows its allocated region, while cards, icons and text
            // retain their authored proportions inside it at every window ratio.
            RectTransform panel = CreateRect(name, surface, size, Vector2.zero);
            surface.GetComponent<WorkshopResponsiveSurface>().Configure(size, panel);
            return panel;
        }

        private RectTransform CreateWorkshopWindowPanel(string name)
        {
            ExpandFacilityToWindow();
            RectTransform shell = CreateRect("Workshop Page Shell", contentRoot, Vector2.zero, Vector2.zero);
            shell.anchorMin = Vector2.zero;
            shell.anchorMax = Vector2.one;
            shell.offsetMin = Vector2.zero;
            shell.offsetMax = new Vector2(0f, -PhaseSurfaceLayout.GetHeaderHeight(tutorialActive));
            BuildWorkshopPageNavigation(shell);
            RectTransform panel = CreateRect(name, shell, new Vector2(1896f, 948f), Vector2.zero);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(24f, 20f);
            panel.offsetMax = new Vector2(-24f, -92f);
            panel.gameObject.AddComponent<WorkshopResponsiveSurface>().Configure(new Vector2(1896f, 948f));
            return panel;
        }

        private RectTransform CreateWorkshopRegion(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            WorkshopResponsiveSurface parentLayout = parent.GetComponent<WorkshopResponsiveSurface>();
            Vector2 reference = parentLayout != null ? parentLayout.ReferenceSize : parent.rect.size;
            RectTransform region = CreateRect(name, parent, size, position);
            region.anchorMin = new Vector2(.5f + (position.x - size.x * .5f) / reference.x,
                .5f + (position.y - size.y * .5f) / reference.y);
            region.anchorMax = new Vector2(.5f + (position.x + size.x * .5f) / reference.x,
                .5f + (position.y + size.y * .5f) / reference.y);
            region.offsetMin = region.offsetMax = Vector2.zero;
            region.gameObject.AddComponent<WorkshopResponsiveSurface>().Configure(size);
            return region;
        }

    }
}
