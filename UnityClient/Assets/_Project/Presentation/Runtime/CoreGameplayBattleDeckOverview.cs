using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform battleDeckOverlay, battleDeckSheet, battleDeckContent;
        private CanvasGroup battleDeckBackground;
        private bool battleDeckPreviousInteractable, battleDeckPreviousRaycasts;
        private GameObject battleDeckPreviousSelection;
        private CoreBattleState inspectedBattle;
        private NavalBattleStage battleDeckStage;
        private ScrollRect battleDeckScroll;
        private Text battleDeckSummary;
        private readonly List<Button> battleDeckTabs = new List<Button>();
        private int battleDeckRevision;
        private bool inspectingBoardingEnemy;
        private string battleInformationTopic;

        private bool BattleInspectionBlocksInput => battleDeckOverlay != null || portSettingsOverlay != null;

        private void OpenBattleDeckOverview() => OpenBattleOverview(false);

        private void OpenBattlePile(bool discard)
        {
            OpenBattleDeckOverview();
            if (!IsBattleDeckCurrent() || battleDeckContent == null || battleDeckScroll == null) return;
            RectTransform group = battleDeckContent.Find(discard ? "Battle Deck Discard Group" : "Battle Deck Undrawn Group") as RectTransform;
            if (group == null) return;
            battleDeckScroll.StopMovement();
            float available = Mathf.Max(0f, battleDeckContent.rect.height - battleDeckScroll.viewport.rect.height);
            battleDeckContent.anchoredPosition = new Vector2(0f, discard ? Mathf.Min(-group.anchoredPosition.y, available) : 0f);
        }

        private void OpenBoardingEnemyOverview() => OpenBattleOverview(true);

        private void OpenBattleOverview(bool enemy, string informationTopic = null)
        {
            if (!isActiveAndEnabled || session?.Battle == null || session.Phase != CoreDemoPhase.Battle ||
                contentRoot == null || contentRoot.parent == null || battleAnimationPlaying || boardingCardChoiceOverlay != null ||
                portSettingsOverlay != null || gameMasterOverlay != null || battleDeckOverlay != null) return;
            if (enemy && !HasBoardingEnemySchool(session.Battle)) return;

            inspectedBattle = session.Battle;
            inspectingBoardingEnemy = enemy;
            battleInformationTopic = informationTopic;
            bool information = !string.IsNullOrEmpty(informationTopic);
            DismissBattleHandInspection();
            battleDeckStage = inspectedBattle.Stage;
            battleDeckPreviousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            battleDeckBackground = GetOrAddCanvasGroup(contentRoot.gameObject);
            battleDeckPreviousInteractable = battleDeckBackground.interactable;
            battleDeckPreviousRaycasts = battleDeckBackground.blocksRaycasts;
            battleDeckBackground.interactable = false;
            battleDeckBackground.blocksRaycasts = false;
            battleDeckOverlay = CreateStretchPanel("Battle Deck Overlay", contentRoot.parent, new Color(.025f, .018f, .012f, .68f));
            battleDeckOverlay.SetAsLastSibling();
            int revision = ++battleDeckRevision;
            battleDeckOverlay.gameObject.AddComponent<PortSettingsBackgroundFocusHandler>().OnBackgroundPressed = FocusBattleDeck;

            // Match the reference's central sheet: about 71% of the screen width and 79% of its height.
            battleDeckSheet = CreateRect("Battle Deck Sheet", battleDeckOverlay, new Vector2(1368f, 856f), new Vector2(0f, -42f));
            Image sheet = battleDeckSheet.gameObject.AddComponent<Image>();
            // Share the authored paper, carved frame and brass corners with Settings and Ship Hold.
            sheet.color = Html("#F0E6CF");
            MarketShopSkin.ApplyPanel(sheet);
            BattleDeckLabel("Battle Deck Eyebrow", battleDeckSheet, new Vector2(700f, 20f), new Vector2(0f, 398f),
                15, enemy ? "ENEMY BOARDING PLAN" : information ? "BATTLE INFORMATION" : "BATTLE DECK", MarketShopSkin.MutedInk, TextAnchor.MiddleCenter);
            Text title = BattleDeckLabel("Battle Deck Title", battleDeckSheet, new Vector2(700f, 44f), new Vector2(0f, 362f),
                34, enemy ? inspectedBattle.EnemyName + " · 接舷战术" : information
                    ? informationTopic == "enemy" ? "敌舰与当前意图" : "行动点、能量与补给"
                    : "战斗卡组", MarketShopSkin.Ink, TextAnchor.MiddleCenter);
            GameTypography.Apply(title, 34, FontStyle.Bold);
            Button close = CreateButton("Close Battle Deck", battleDeckSheet, new Vector2(100f, 44f),
                new Vector2(598f, 374f), PanelRaised, MarketShopSkin.Ink, 20);
            close.GetComponentInChildren<Text>().text = "关闭";
            MarketShopSkin.ApplyTextButton(close);
            GameTypography.Apply(close.GetComponentInChildren<Text>(), 20, FontStyle.Normal);
            close.onClick.AddListener(() => { if (revision == battleDeckRevision) CloseBattleDeckOverview(); });
            BindBattleDeckCancel(close, revision);

            battleDeckTabs.Clear();
            for (int index = 0; !enemy && !information && index < WorkshopBattleStages.Length; index++)
            {
                NavalBattleStage stage = WorkshopBattleStages[index];
                Button tab = CreateButton("Battle Deck Tab " + stage, battleDeckSheet, new Vector2(290f, 48f),
                    new Vector2((index - 1) * 426f, 306f), PanelRaised, MarketShopSkin.Ink, 22);
                GameTypography.Apply(tab.GetComponentInChildren<Text>(), 22, FontStyle.Normal);
                tab.onClick.AddListener(() =>
                {
                    if (revision != battleDeckRevision || !IsBattleDeckCurrent()) return;
                    battleDeckStage = stage;
                    RefreshBattleDeckOverview();
                });
                BindBattleDeckCancel(tab, revision);
                battleDeckTabs.Add(tab);
            }
            battleDeckSummary = BattleDeckLabel("Battle Deck Summary", battleDeckSheet, new Vector2(1240f, 30f),
                new Vector2(0f, 262f), 17, string.Empty, MarketShopSkin.MutedInk, TextAnchor.MiddleCenter);
            BattleDeckRule("Battle Deck Header Rule", battleDeckSheet, 1230f, new Vector2(0f, 240f));

            RectTransform viewport = CreateRect("Battle Deck Viewport", battleDeckSheet,
                new Vector2(1276f, 612f), new Vector2(-8f, -74f));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            battleDeckContent = CreateRect("Battle Deck Content", viewport, new Vector2(1276f, 612f), Vector2.zero);
            battleDeckContent.anchorMin = battleDeckContent.anchorMax = battleDeckContent.pivot = new Vector2(.5f, 1f);
            battleDeckScroll = viewport.gameObject.AddComponent<ScrollRect>();
            battleDeckScroll.viewport = viewport;
            battleDeckScroll.content = battleDeckContent;
            battleDeckScroll.horizontal = false;
            battleDeckScroll.vertical = true;
            battleDeckScroll.movementType = ScrollRect.MovementType.Clamped;
            battleDeckScroll.scrollSensitivity = 45f;
            battleDeckScroll.inertia = true;
            battleDeckScroll.decelerationRate = .08f;

            Image track = CreateImage("Battle Deck Scrollbar", battleDeckSheet, new Vector2(26f, 612f),
                new Vector2(644f, -74f), Color.clear);
            Image rail = CreateImage("Battle Deck Scroll Rail", track.transform, new Vector2(2f, 612f),
                Vector2.zero, new Color(.68f, .54f, .34f, .4f));
            rail.raycastTarget = false;
            RectTransform handleArea = CreateRect("Sliding Area", track.transform, Vector2.zero, Vector2.zero);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = handleArea.offsetMax = Vector2.zero;
            Image handle = CreateImage("Battle Deck Scroll Handle", handleArea, Vector2.zero, Vector2.zero, Color.clear);
            Image gripLine = CreateImage("Battle Deck Scroll Grip", handle.transform, new Vector2(4f, 0f),
                Vector2.zero, new Color(.56f, .39f, .19f, .8f));
            gripLine.rectTransform.anchorMin = new Vector2(.5f, 0f);
            gripLine.rectTransform.anchorMax = new Vector2(.5f, 1f);
            gripLine.raycastTarget = false;
            Image diamond = CreateImage("Battle Deck Scroll Diamond", handle.transform, new Vector2(22f, 36f),
                Vector2.zero, Color.white, DeckListArtCatalog.Thumb);
            diamond.preserveAspect = true;
            diamond.raycastTarget = false;
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = diamond;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            battleDeckScroll.verticalScrollbar = scrollbar;
            BindBattleDeckCancel(scrollbar, revision);
            var controls = new List<Selectable>(battleDeckTabs) { scrollbar, close };
            for (int index = 0; index < controls.Count; index++)
                controls[index].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = controls[(index + controls.Count - 1) % controls.Count],
                    selectOnRight = controls[(index + 1) % controls.Count],
                    selectOnUp = controls[(index + controls.Count - 1) % controls.Count],
                    selectOnDown = controls[(index + 1) % controls.Count],
                };
            BattleDeckRule("Battle Deck Footer Rule", battleDeckSheet, 1230f, new Vector2(0f, -388f));
            BattleDeckLabel("Battle Deck Footer", battleDeckSheet, new Vector2(1230f, 26f),
                new Vector2(0f, -406f), 15, enemy ? "滚轮查看资源、行动与循环  ·  Esc 关闭" : information
                    ? "只读检查，不消耗资源  ·  滚轮浏览  ·  Esc 关闭" : "滚轮浏览全部卡牌  ·  Esc 关闭", MarketShopSkin.MutedInk, TextAnchor.MiddleCenter);
            RefreshBattleDeckOverview();
            FocusBattleDeck();
            ObserveGuidedBattleInspection(enemy ? "enemy" : informationTopic ?? "deck");
            if (!Application.isPlaying) SetEditorPreviewFlags(battleDeckOverlay);
        }

        private bool IsBattleDeckCurrent() => battleDeckOverlay != null && isActiveAndEnabled &&
            session?.Phase == CoreDemoPhase.Battle && ReferenceEquals(session.Battle, inspectedBattle);

        private void DismissBattleHandInspection()
        {
            CompleteBattleHandFlight();
            // A hovered hand card uses an override canvas; return it below the modal,
            // including when the header is opened from keyboard/controller focus.
            if (contentRoot == null) return;
            foreach (BattleCardHoverFeedback hover in contentRoot.GetComponentsInChildren<BattleCardHoverFeedback>())
                hover.OnPointerExit(null);
        }

        private void RefreshBattleDeckOverview()
        {
            if (!IsBattleDeckCurrent()) return;
            ClearChildren(battleDeckContent);
            if (inspectingBoardingEnemy)
            {
                RefreshBoardingEnemyOverview();
                return;
            }
            if (!string.IsNullOrEmpty(battleInformationTopic))
            {
                RefreshGuidedBattleInformation(battleInformationTopic);
                return;
            }
            IReadOnlyList<int> draw = inspectedBattle.GetDrawPile(battleDeckStage);
            IReadOnlyList<int> discard = inspectedBattle.GetDiscardPile(battleDeckStage);
            for (int index = 0; index < battleDeckTabs.Count; index++)
            {
                bool selected = WorkshopBattleStages[index] == battleDeckStage;
                Text label = battleDeckTabs[index].GetComponentInChildren<Text>();
                label.text = WorkshopStageName(WorkshopBattleStages[index]);
                MarketShopSkin.SetMode(battleDeckTabs[index], selected);
            }
            string hand = battleDeckStage == inspectedBattle.Stage
                ? "  ·  手牌 " + inspectedBattle.Hand.Count + " 张（战场显示）" : string.Empty;
            battleDeckSummary.text = "未抽取 " + draw.Count + " 张  ·  弃牌堆 " + discard.Count + " 张" + hand;
            float height = BuildBattleDeckGroup("Undrawn", "未抽取", draw, 12f);
            height = BuildBattleDeckGroup("Discard", "弃牌堆", discard, height + 20f);
            battleDeckContent.sizeDelta = new Vector2(1276f, Mathf.Max(battleDeckScroll.viewport.rect.height, height + 12f));
            battleDeckScroll.StopMovement();
            battleDeckContent.anchoredPosition = Vector2.zero;
            battleDeckScroll.verticalNormalizedPosition = 1f;
        }

        private float BuildBattleDeckGroup(string key, string caption, IReadOnlyList<int> cards, float top)
        {
            // Sort a copy for browsing: inspection must neither reorder the live pile nor reveal draw order.
            var sorted = new List<int>(cards);
            sorted.Sort(CoreBattleCardNumbering.CompareLibraryIndices);
            int rows = (sorted.Count + 4) / 5;
            float height = sorted.Count == 0 ? 102f : 48f + rows * 364f;
            RectTransform group = CreateRect("Battle Deck " + key + " Group", battleDeckContent,
                new Vector2(1276f, height), new Vector2(0f, -top));
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(.5f, 1f);
            Text heading = BattleDeckLabel("Battle Deck " + key + " Heading", group, new Vector2(1188f, 36f),
                new Vector2(0f, -20f), 23, caption + "  ·  " + cards.Count + " 张", MarketShopSkin.Ink, TextAnchor.MiddleLeft);
            GameTypography.Apply(heading, 23, FontStyle.Bold);
            heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(.5f, 1f);
            Image rule = BattleDeckRule("Battle Deck " + key + " Rule", group, 1188f, new Vector2(0f, -43f));
            rule.rectTransform.anchorMin = rule.rectTransform.anchorMax = new Vector2(.5f, 1f);
            if (sorted.Count == 0)
            {
                Text empty = BattleDeckLabel("Battle Deck " + key + " Empty", group, new Vector2(1188f, 40f),
                    new Vector2(0f, -74f), 18, key == "Undrawn" ? "暂无未抽取卡牌" : "暂无弃牌", MarketShopSkin.MutedInk, TextAnchor.MiddleLeft);
                empty.rectTransform.anchorMin = empty.rectTransform.anchorMax = new Vector2(.5f, 1f);
            }
            for (int index = 0; index < sorted.Count; index++)
            {
                CoreBattleCard card = session.GetBattleCard(sorted[index], battleDeckStage);
                string prefix = "Battle Deck " + key + " Card " + index;
                RectTransform face = CreateCollectionCardFace(group, card, prefix, prefix + " Art", prefix + " Name", .72f,
                    session.BattleCardFaceDescription(card, battleDeckStage));
                face.anchorMin = face.anchorMax = new Vector2(.5f, 1f);
                face.anchoredPosition = new Vector2((index % 5 - 2) * 244f, -48f - CardFaceLayout.FrameSize.y * .36f - (index / 5) * 364f);
                foreach (Graphic graphic in face.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
            return top + height;
        }

        private Image BattleDeckRule(string name, Transform parent, float width, Vector2 position)
        {
            Image rule = CreateImage(name, parent, new Vector2(width, 1f), position, new Color(.68f, .58f, .43f, .65f));
            rule.raycastTarget = false;
            return rule;
        }

        private Text BattleDeckLabel(string name, Transform parent, Vector2 size, Vector2 position,
            int fontSize, string value, Color color, TextAnchor alignment)
        {
            Text text = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, alignment, color);
            text.text = value;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private void BindBattleDeckCancel(Selectable control, int revision)
        {
            control.gameObject.AddComponent<PortSettingsCancelHandler>().OnCancelRequested = () =>
            {
                if (revision == battleDeckRevision) CloseBattleDeckOverview();
            };
        }

        private void FocusBattleDeck()
        {
            if (battleDeckOverlay == null || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(battleDeckOverlay)) return;
            if (inspectingBoardingEnemy || !string.IsNullOrEmpty(battleInformationTopic))
            {
                Transform close = battleDeckSheet.Find("Close Battle Deck");
                if (close != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
                return;
            }
            int index = System.Array.IndexOf(WorkshopBattleStages, battleDeckStage);
            if (index >= 0 && index < battleDeckTabs.Count)
                EventSystem.current.SetSelectedGameObject(battleDeckTabs[index].gameObject);
        }

        private void CloseBattleDeckOverview(bool restoreSelection = true)
        {
            if (battleDeckOverlay == null) return;
            battleDeckRevision++;
            if (battleDeckBackground != null)
            {
                battleDeckBackground.interactable = battleDeckPreviousInteractable;
                battleDeckBackground.blocksRaycasts = battleDeckPreviousRaycasts;
            }
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(restoreSelection && battleDeckPreviousSelection != null &&
                    battleDeckPreviousSelection.activeInHierarchy ? battleDeckPreviousSelection : null);
            GameObject overlay = battleDeckOverlay.gameObject;
            overlay.SetActive(false);
            battleDeckOverlay = battleDeckSheet = battleDeckContent = null;
            battleDeckBackground = null;
            battleDeckPreviousSelection = null;
            battleDeckScroll = null;
            battleDeckSummary = null;
            inspectedBattle = null;
            inspectingBoardingEnemy = false;
            battleInformationTopic = null;
            battleDeckTabs.Clear();
            if (Application.isPlaying) Destroy(overlay); else DestroyImmediate(overlay);
            if (restoreSelection) RefreshGuidedBattleCourse();
        }
    }
}
