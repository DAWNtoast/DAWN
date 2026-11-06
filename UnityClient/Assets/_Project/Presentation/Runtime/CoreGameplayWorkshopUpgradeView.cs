using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const int WorkshopUpgradeListPageSize = 6;

        private void BuildWorkshopUpgradeWorkspace()
        {
            RectTransform root = CreateWorkshopWindowPanel("Card Workshop Upgrade Workspace");
            RectTransform library = CreateWorkshopPreviewPaper("Workshop Upgrade Library", root,
                new Vector2(536f, 900f), new Vector2(-680f, 0f));
            BuildWorkshopUpgradeFilters(library);

            List<int> cards = new List<int>();
            foreach (int index in session.GetAvailableBattleCards(selectedBattleDeckStage))
            {
                CoreBattleCard card = session.GetBattleCard(index, selectedBattleDeckStage);
                if (MatchesWorkshopSchool(card) && !CoreVoyageSession.IsSpecialBattleCard(card.Id) &&
                    string.IsNullOrEmpty(card.RequiredCrew) && string.IsNullOrEmpty(card.RequiredComponent)) cards.Add(index);
            }
            cards.Sort(CoreBattleCardNumbering.CompareLibraryIndices);
            int pages = Mathf.Max(1, (cards.Count + WorkshopUpgradeListPageSize - 1) / WorkshopUpgradeListPageSize);
            battleCardUpgradePage = Mathf.Clamp(battleCardUpgradePage, 0, pages - 1);
            int first = battleCardUpgradePage * WorkshopUpgradeListPageSize;
            int selectedSlot = cards.IndexOf(selectedBattleCardUpgrade);
            if (selectedSlot < first || selectedSlot >= first + WorkshopUpgradeListPageSize)
                selectedBattleCardUpgrade = first < cards.Count ? cards[first] : -1;
            for (int slot = 0; slot < WorkshopUpgradeListPageSize && first + slot < cards.Count; slot++)
                BuildWorkshopUpgradeListRow(library, cards[first + slot], new Vector2(0f, 206f - slot * 102f));
            if (cards.Count == 0)
                CreateText("Workshop Preview Empty", library, new Vector2(440f, 140f), new Vector2(0f, -78f),
                    25, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text = WorkshopSchoolEmptyMessage(false);

            Button previous = CreateButton("Previous Upgrade Cards", library, new Vector2(98f, 42f),
                new Vector2(-181f, -396f), PanelRaised, Ink, 22);
            previous.GetComponentInChildren<Text>().text = "上一页";
            previous.interactable = battleCardUpgradePage > 0;
            previous.onClick.AddListener(() =>
            {
                if (previous == null || !previous.gameObject.activeInHierarchy || !previous.IsInteractable() || !battleCardUpgradeOpen) return;
                battleCardUpgradePage--;
                Render();
            });
            Button next = CreateButton("Next Upgrade Cards", library, new Vector2(98f, 42f),
                new Vector2(181f, -396f), PanelRaised, Ink, 22);
            next.GetComponentInChildren<Text>().text = "下一页";
            next.interactable = battleCardUpgradePage + 1 < pages;
            next.onClick.AddListener(() =>
            {
                if (next == null || !next.gameObject.activeInHierarchy || !next.IsInteractable() || !battleCardUpgradeOpen) return;
                battleCardUpgradePage++;
                Render();
            });
            CreateText("Workshop Collection Page Count", library, new Vector2(240f, 38f), new Vector2(0f, -396f),
                21, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text =
                (battleCardUpgradePage + 1) + " / " + pages + " 页 · " + cards.Count + " 种";
            BuildWorkshopUpgradePanel(root);
        }

        private void BuildWorkshopUpgradeFilters(RectTransform library)
        {
            for (int i = 0; i < WorkshopBattleStages.Length; i++)
            {
                NavalBattleStage stage = WorkshopBattleStages[i];
                Button tab = CreateButton("Workshop Preview Stage " + stage, library, new Vector2(148f, 48f),
                    new Vector2(-160f + i * 160f, 390f), Gold, Ink, 23);
                tab.GetComponentInChildren<Text>().text = WorkshopStageName(stage);
                WoodButtonSkin.SetSelected(tab, stage == selectedBattleDeckStage);
                tab.onClick.AddListener(() =>
                {
                    if (tab == null || !tab.gameObject.activeInHierarchy || !tab.IsInteractable() || !battleCardUpgradeOpen) return;
                    selectedBattleDeckStage = stage;
                    specialCardCollectionOpen = false;
                    battleCardUpgradePage = 0;
                    selectedBattleCardUpgrade = -1;
                    Render();
                });
            }
            for (int i = 0; i <= CardSchoolArtCatalog.SchoolIds.Length; i++)
            {
                string school = i == 0 ? "neutral" : CardSchoolArtCatalog.SchoolIds[i - 1];
                Vector2 position = i < 4 ? new Vector2(-180f + i * 120f, 336f)
                    : new Vector2(-120f + (i - 4) * 120f, 288f);
                Button filter = CreateButton("Workshop School Filter " + school, library,
                    new Vector2(114f, 40f), position, Gold, Ink, 22);
                filter.GetComponentInChildren<Text>().text = school == "neutral"
                    ? "无体系" : CoreSchoolUnlockCatalog.Find(school).Name;
                WorkshopSchoolButtonGraphic.Apply(filter, school, school == selectedWorkshopSchool);
                filter.onClick.AddListener(() =>
                {
                    if (filter == null || !filter.gameObject.activeInHierarchy || !filter.IsInteractable() || !battleCardUpgradeOpen) return;
                    SelectWorkshopSchool(school);
                });
            }
        }

        private void BuildWorkshopUpgradeListRow(RectTransform library, int index, Vector2 position)
        {
            CoreBattleCard card = session.GetBattleCard(index, selectedBattleDeckStage);
            bool selected = index == selectedBattleCardUpgrade;
            Button row = CreateButton("Select Upgrade Card " + index, library, new Vector2(464f, 90f),
                position, PanelRaised, Ink, 24);
            row.GetComponentInChildren<Text>().text = string.Empty;
            WoodButtonSkin.SetSelected(row, selected);
            Rect uv = CardSchoolArtCatalog.IllustrationUv(card, art);
            Texture2D picture = CardSchoolArtCatalog.Illustration(card, art);
            Vector2 pictureSize = new Vector2(78f, 66f);
            if (picture != null && uv.width > 0f && uv.height > 0f)
            {
                float aspect = picture.width * uv.width / (picture.height * uv.height);
                float targetAspect = pictureSize.x / pictureSize.y;
                if (aspect > targetAspect)
                {
                    float width = uv.width * targetAspect / aspect;
                    uv.x += (uv.width - width) * .5f;
                    uv.width = width;
                }
                else
                {
                    float height = uv.height * aspect / targetAspect;
                    uv.y += (uv.height - height) * .5f;
                    uv.height = height;
                }
            }
            CreateRawImage("Upgrade List Art " + index, row.transform, pictureSize,
                new Vector2(-181f, 0f), picture, uv).raycastTarget = false;
            Text name = CreateText("Upgrade List Name " + index, row.transform, new Vector2(264f, 30f),
                Vector2.zero, 26, FontStyle.Bold, TextAnchor.MiddleLeft, WoodButtonSkin.Ink);
            name.text = card.Name;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 19;
            name.resizeTextMaxSize = 26;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            CreateText("Upgrade List Level " + index, row.transform, new Vector2(62f, 42f),
                new Vector2(188f, 0f), 28, FontStyle.Bold, TextAnchor.MiddleCenter, Gold).text = "+" + card.UpgradeLevel;
            CreateImage("Upgrade List Selection " + index, row.transform, new Vector2(3f, 66f),
                new Vector2(-227f, 0f), selected ? Gold : Color.clear).raycastTarget = false;
            NavalBattleStage stage = selectedBattleDeckStage;
            row.onClick.AddListener(() =>
            {
                if (row == null || !row.gameObject.activeInHierarchy || !row.IsInteractable() || !battleCardUpgradeOpen ||
                    selectedBattleDeckStage != stage) return;
                selectedBattleCardUpgrade = index;
                Render();
            });
        }

        private void BuildWorkshopUpgradePanel(RectTransform root)
        {
            RectTransform panel = CreateWorkshopPreviewPaper("Battle Card Upgrade Panel", root,
                new Vector2(1336f, 900f), new Vector2(280f, 0f));
            if (selectedBattleCardUpgrade >= 0)
                BuildBattleCardUpgradeDetail(panel, selectedBattleCardUpgrade, WorkshopPaperInk, WorkshopPaperMuted);
            else
            {
                RawImage hammer = CreateRawImage("Workshop Upgrade Empty Hammer", panel, new Vector2(260f, 260f),
                    new Vector2(0f, 70f), cardWorkshopArt?.UpgradeIcon, new Rect(0f, 0f, 1f, 1f));
                hammer.raycastTarget = false;
                FitCrewPortrait(hammer, new Vector2(260f, 260f));
                CreateText("Workshop Upgrade Empty", panel, new Vector2(1040f, 100f), new Vector2(0f, -160f),
                    30, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text = "此分类暂无可强化卡牌\n通过打开卡包获取，或选择其他分类";
            }
        }
    }
}
