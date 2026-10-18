using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private GameObject schoolUnlockNotice;
        private RectTransform schoolUnlockPage;
        private readonly List<CoreSchoolUnlockDefinition> pendingSchoolUnlocks = new List<CoreSchoolUnlockDefinition>();
        private int schoolUnlockPageIndex;
        private int schoolUnlockPreviousLevel;
        private int schoolUnlockCurrentLevel;

        // Called only after a confirmed return has committed experience and saved.
        // Loading an existing profile or rebuilding the port never replays it.
        private void ShowSchoolUnlockNotice(int previousLevel)
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked || !isActiveAndEnabled ||
                session == null || session.Phase != CoreDemoPhase.Port || legacyCanvasRoot == null ||
                schoolUnlockNotice != null || session.PirateLevel <= previousLevel) return;

            pendingSchoolUnlocks.Clear();
            foreach (CoreSchoolUnlockDefinition school in CoreSchoolUnlockCatalog.All)
                if (previousLevel < school.RequiredLevel && school.RequiredLevel <= session.PirateLevel)
                    pendingSchoolUnlocks.Add(school);
            if (pendingSchoolUnlocks.Count == 0) return;

            schoolUnlockPreviousLevel = previousLevel;
            schoolUnlockCurrentLevel = session.PirateLevel;
            schoolUnlockPageIndex = 0;
            // Keep the notice outside Phase Content so port refreshes cannot dismiss it.
            RectTransform overlay = CreateRect("School Unlock Notice", legacyCanvasRoot,
                Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            schoolUnlockNotice = overlay.gameObject;
            Canvas canvas = schoolUnlockNotice.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20010;
            schoolUnlockNotice.AddComponent<GraphicRaycaster>();
            Image shade = schoolUnlockNotice.AddComponent<Image>();
            shade.color = new Color(.015f, .025f, .03f, .82f);
            shade.raycastTarget = true;
            BuildSchoolUnlockPage();
            PlaySfx("milestone_unlock", .58f);
        }

        private void BuildSchoolUnlockPage()
        {
            if (schoolUnlockPage != null)
            {
                schoolUnlockPage.gameObject.SetActive(false);
                Destroy(schoolUnlockPage.gameObject);
            }

            CoreSchoolUnlockDefinition school = pendingSchoolUnlocks[schoolUnlockPageIndex];
            schoolUnlockPage = CreateFixedPanel("School Unlock Page", schoolUnlockNotice.transform,
                new Vector2(1240f, 920f), Vector2.zero, new Color(.94f, .88f, .75f));
            MarketShopSkin.ApplyPanel(schoolUnlockPage.GetComponent<Image>());
            Color ink = MarketShopSkin.Ink;
            Color muted = MarketShopSkin.MutedInk;
            Color accent = CardSchoolArtCatalog.Palette(school.Id).Surface;

            SchoolUnlockLabel("School Unlock Title", schoolUnlockPage, new Vector2(1120f, 64f),
                new Vector2(0f, 393f), 42, FontStyle.Bold, accent, "新卡牌体系解锁 · " + school.Name);
            SchoolUnlockLabel("School Unlock Level", schoolUnlockPage, new Vector2(1080f, 36f),
                new Vector2(0f, 337f), 25, FontStyle.Bold, ink,
                "海盗等级 Lv." + schoolUnlockPreviousLevel + " → Lv." + schoolUnlockCurrentLevel);
            SchoolUnlockLabel("School Unlock Description", schoolUnlockPage, new Vector2(1080f, 42f),
                new Vector2(0f, 292f), 23, FontStyle.Normal, muted,
                school.Name + "体系的 " + school.BasicCardCount + " 张基础卡牌已解锁 · 以下为部分卡牌");
            WarehouseDialogRule("School Unlock Header Rule", schoolUnlockPage, 1080f, 264f);

            List<CoreBattleCard> previews = SchoolUnlockPreviewCards(school.Id);
            for (int i = 0; i < previews.Count; i++)
            {
                CoreBattleCard card = previews[i];
                string prefix = "School Unlock Preview " + i;
                RectTransform slot = CreateRect(prefix, schoolUnlockPage, new Vector2(328f, 520f),
                    new Vector2((i - (previews.Count - 1) * .5f) * 350f, -8f));
                bool boarding = card.AllowedStages == NavalBattleStages.Boarding;
                SchoolUnlockLabel(prefix + " Stage", slot, new Vector2(300f, 30f),
                    new Vector2(0f, 244f), 20, FontStyle.Bold, accent, boarding ? "接舷卡牌" : "船战卡牌");
                Rect bounds = CardSchoolArtCatalog.FrameBounds(card);
                float scale = Mathf.Min(1f, 432f / bounds.height, 300f / bounds.width);
                RectTransform face = CreateCollectionCardFace(slot, card, prefix, prefix + " Art",
                    prefix + " Name", scale);
                face.anchoredPosition = -bounds.center * scale;
            }

            WarehouseDialogRule("School Unlock Footer Rule", schoolUnlockPage, 1080f, -263f);
            SchoolUnlockLabel("School Unlock Workshop Hint", schoolUnlockPage, new Vector2(1080f, 48f),
                new Vector2(0f, -300f), 22, FontStyle.Normal, ink,
                "可前往港口「卡组工坊」查看全部卡牌，编入卡组后在下一次出航使用。");
            bool last = schoolUnlockPageIndex == pendingSchoolUnlocks.Count - 1;
            Button confirm = CreateButton(last ? "Confirm School Unlock" : "Next School Unlock", schoolUnlockPage,
                new Vector2(360f, 64f), new Vector2(0f, -369f), Gold, DeepSea, 25);
            MarketShopSkin.SetMode(confirm, true);
            confirm.GetComponentInChildren<Text>().text = last ? "知道了，继续冒险" : "查看下一个体系";
            confirm.navigation = new Navigation { mode = Navigation.Mode.None };
            // Ignore stale buttons from a page destroyed at the end of this frame.
            int pageIndex = schoolUnlockPageIndex;
            confirm.onClick.AddListener(() =>
            {
                if (schoolUnlockPageIndex == pageIndex) AdvanceSchoolUnlockNotice();
            });
            SchoolUnlockLabel("School Unlock Progress", schoolUnlockPage, new Vector2(1080f, 28f),
                new Vector2(0f, -423f), 18, FontStyle.Normal, muted,
                "本次新体系 " + (schoolUnlockPageIndex + 1) + " / " + pendingSchoolUnlocks.Count);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(confirm.gameObject);
        }

        private Text SchoolUnlockLabel(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, FontStyle style, Color color, string value)
        {
            Text label = CreateText(name, parent, size, position, fontSize, style, TextAnchor.MiddleCenter, color);
            label.color = color;
            label.text = value;
            return label;
        }

        private static List<CoreBattleCard> SchoolUnlockPreviewCards(string schoolId)
        {
            var cards = new List<CoreBattleCard>(3);
            // Use canonical, source-free library entries: level rewards do not grant
            // crew/equipment cards, and historic boarding aliases are not new cards.
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
            {
                if (!IsSchoolUnlockPreviewCard(card, schoolId) ||
                    !card.IsAllowedInStage(NavalBattleStage.Naval)) continue;
                cards.Add(card);
                break;
            }
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
            {
                if (cards.Count == 3) break;
                if (IsSchoolUnlockPreviewCard(card, schoolId) && card.AllowedStages == NavalBattleStages.Boarding)
                    cards.Add(card);
            }
            return cards;
        }

        private static bool IsSchoolUnlockPreviewCard(CoreBattleCard card, string schoolId) =>
            card.SchoolId == schoolId && string.IsNullOrEmpty(card.RequiredCrew) &&
            string.IsNullOrEmpty(card.RequiredComponent) && !CoreBoardingCardCatalog.IsLegacyAlias(card) &&
            card.AllowedStages != NavalBattleStages.None;

        private void AdvanceSchoolUnlockNotice()
        {
            if (schoolUnlockNotice == null || profileSaveBlocked || !isActiveAndEnabled) return;
            schoolUnlockPageIndex++;
            if (schoolUnlockPageIndex >= pendingSchoolUnlocks.Count) DismissSchoolUnlockNotice();
            else BuildSchoolUnlockPage();
        }

        private void DismissSchoolUnlockNotice()
        {
            if (schoolUnlockNotice != null)
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                    EventSystem.current.currentSelectedGameObject.transform.IsChildOf(schoolUnlockNotice.transform))
                    EventSystem.current.SetSelectedGameObject(null);
                schoolUnlockNotice.SetActive(false);
                if (Application.isPlaying) Destroy(schoolUnlockNotice);
                else DestroyImmediate(schoolUnlockNotice);
            }
            schoolUnlockNotice = null;
            schoolUnlockPage = null;
            pendingSchoolUnlocks.Clear();
        }
    }
}
