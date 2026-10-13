using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const float DeckBuilderListRowHeight = 64f;
        private const float DeckBuilderListViewportHeight = 640f;
        private const float DeckBuilderListViewportWidth = 306f;
        private float deckBuilderBaseScrollOffset;
        private float deckBuilderSpecialScrollOffset;
        private string deckBuilderNotice = string.Empty;
        private int deckBuilderAddedCardIndex = -1;
        private Text deckBuilderStatus;
        private ScrollRect deckBuilderBaseScrollRect;
        private ScrollRect deckBuilderSpecialScrollRect;
        private RectTransform deckBuilderHoverPreview;
        private Button deckBuilderHoverSource;

        private sealed class DeckBuilderGroupedCard
        {
            public int Index;
            public int Quantity;
            public int FirstSlot;
        }

        private void ResetDeckBuilderPresentation()
        {
            ClearDeckBuilderDraft();
            deckBuilderBaseScrollOffset = deckBuilderSpecialScrollOffset = 0f;
            deckBuilderNotice = string.Empty;
            deckBuilderAddedCardIndex = -1;
        }

        private void BuildInteractiveDeckBuilder()
        {
            float headerHeight = PhaseSurfaceLayout.GetHeaderHeight(tutorialActive);
            RectTransform panel = CreateRect("Deck Builder Panel", contentRoot,
                new Vector2(1896f, 948f), new Vector2(0f, -headerHeight * .5f));
            panel.localScale = Vector3.one * Mathf.Min(1f, (1080f - headerHeight - 24f) / 948f);
            RectTransform paper = CreateWorkshopPreviewPaper("Deck Builder Parchment", panel,
                new Vector2(1140f, 948f), new Vector2(-378f, 0f));
            RectTransform rail = CreateWorkshopPreviewPaper("Deck Builder Deck Rail", panel,
                new Vector2(744f, 948f), new Vector2(576f, 0f));
            Image targetGlow = CreateImage("Deck Builder Drop Highlight", rail, new Vector2(726f, 930f),
                Vector2.zero, new Color(1f, .7f, .15f, .15f));
            targetGlow.raycastTarget = false;
            targetGlow.gameObject.SetActive(false);
            Image removalGlow = CreateImage("Deck Builder Removal Highlight", paper, new Vector2(1122f, 930f),
                Vector2.zero, new Color(1f, .7f, .15f, .15f));
            removalGlow.raycastTarget = false;
            removalGlow.gameObject.SetActive(false);
            RectTransform header = CreateRect("Deck Builder Header", paper,
                new Vector2(1000f, 54f), new Vector2(0f, 415f));
            for (int i = 0; i < WorkshopBattleStages.Length; i++)
            {
                NavalBattleStage stage = WorkshopBattleStages[i];
                Button tab = CreateButton("Battle Deck Stage Tab " + stage, header, new Vector2(238f, 54f),
                    new Vector2(-381f + i * 254f, 0f), Gold, Ink, 25);
                tab.GetComponentInChildren<Text>().text = WorkshopStageName(stage);
                WoodButtonSkin.SetSelected(tab, stage == selectedBattleDeckStage);
                tab.onClick.AddListener(() => SelectWorkshopBattleStage(stage));
            }
            Button special = CreateButton("Open Special Card Collection", header, new Vector2(238f, 54f),
                new Vector2(381f, 0f), Gold, Ink, 25);
            special.GetComponentInChildren<Text>().text = "特殊卡牌";
            special.onClick.AddListener(() =>
            {
                if (special == null || !special.gameObject.activeInHierarchy || BlockIncompleteDeckBuilderNavigation()) return;
                RememberDeckBuilderScroll();
                OpenWorkshopSpecialCardCollection(true);
            });

            IReadOnlyList<int> deck = CurrentDeckBuilderCards;
            List<int> collectionCards = new List<int>();
            foreach (int index in session.GetAvailableBattleCards(selectedBattleDeckStage))
            {
                CoreBattleCard data = session.GetBattleCard(index);
                if (data != null && !CoreVoyageSession.IsSpecialBattleCard(data.Id) &&
                    string.IsNullOrEmpty(data.RequiredCrew) && string.IsNullOrEmpty(data.RequiredComponent))
                    collectionCards.Add(index);
            }
            int pages = Mathf.Max(1, (collectionCards.Count + BattleCardCollectionPageSize - 1) / BattleCardCollectionPageSize);
            battleCardCollectionPage = Mathf.Clamp(battleCardCollectionPage, 0, pages - 1);
            RectTransform collection = CreateWorkshopCollection("Deck Builder Collection", paper);
            int first = battleCardCollectionPage * BattleCardCollectionPageSize;
            for (int slot = 0; slot < BattleCardCollectionPageSize && first + slot < collectionCards.Count; slot++)
            {
                int index = collectionCards[first + slot];
                Vector2 center = WorkshopCollectionCardCenter(slot);
                Button card = CreateDeckBuilderCard(collection, index, "Add Deck Card " + index,
                    "Workshop Card " + index, center, WorkshopCollectionCardScale, true);
                BindDeckBuilderCard(card, panel, rail, targetGlow, index, false);
                float height = CardSchoolArtCatalog.FrameBounds(session.GetBattleCard(index)).height * WorkshopCollectionCardScale;
                CreateText("Workshop Card Count " + index, collection, new Vector2(240f, 28f),
                    center - new Vector2(0f, height * .5f + 20f), 21, FontStyle.Normal,
                    TextAnchor.MiddleCenter, WorkshopPaperInk).text = "已加入 " + BattleDeckCopies(deck, index) + " / " + session.GetBaseBattleCardOwnedCopies(index) + " 张";
            }
            if (collectionCards.Count == 0)
                CreateText("Deck Builder Collection Empty", collection, new Vector2(920f, 100f), Vector2.zero,
                    28, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text = "暂无已解锁的基础卡牌";
            Button previous = CreateWorkshopCollectionPageArrow("Previous Battle Card Collection Page", paper, false);
            previous.interactable = battleCardCollectionPage > 0;
            previous.onClick.AddListener(() => { RememberDeckBuilderScroll(); battleCardCollectionPage--; Render(); });
            CreateText("Battle Card Collection Page", paper, new Vector2(430f, 38f), new Vector2(0f, WorkshopCollectionPagerY),
                22, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperInk).text =
                "收藏 " + collectionCards.Count + " 种　·　第 " + (battleCardCollectionPage + 1) + " / " + pages + " 页";
            Button next = CreateWorkshopCollectionPageArrow("Next Battle Card Collection Page", paper, true);
            next.interactable = battleCardCollectionPage + 1 < pages;
            next.onClick.AddListener(() => { RememberDeckBuilderScroll(); battleCardCollectionPage++; Render(); });

            BuildDeckBuilderCards(panel, rail, removalGlow, deck);
        }

        private Button CreateDeckBuilderCard(Transform parent, int index, string name, string prefix,
            Vector2 position, float scale, bool collection = false)
        {
            CoreBattleCard data = session.GetBattleCard(index);
            Rect bounds = CardSchoolArtCatalog.FrameBounds(data);
            Button card = CreateButton(name, parent, bounds.size * scale, position, Color.clear, Ink, 16);
            card.GetComponentInChildren<Text>().text = string.Empty;
            card.transition = Selectable.Transition.None;
            RectTransform face = CreateCollectionCardFace(card.transform, data, prefix,
                collection ? "Workshop Card Art " + index : prefix + " Art",
                collection ? "Workshop Card Name " + index : prefix + " Name", scale,
                session.BattleCardFaceDescription(data, selectedBattleDeckStage),
                collection ? "Workshop Card Description " + index : prefix + " Effect");
            face.anchoredPosition = -bounds.center * scale;
            // The full frame defines one hit area; nested cost ornaments cannot steal the drag.
            foreach (Graphic graphic in face.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            card.image.raycastPadding = Vector4.zero;
            return card;
        }

        private void BindDeckBuilderCard(Button card, RectTransform panel, RectTransform rail,
            Image targetGlow, int index, bool fromDeck)
        {
            DeckBuilderCardDrag drag = card.gameObject.AddComponent<DeckBuilderCardDrag>();
            drag.Bind(panel, rail, fromDeck, () =>
            {
                HideDeckBuilderHoverPreview();
                RectTransform ghost = CreateRect("Deck Builder Drag Ghost", panel, new Vector2(240f, 400f), Vector2.zero);
                CoreBattleCard data = session.GetBattleCard(index);
                const float scale = .70f;
                RectTransform face = CreateCollectionCardFace(ghost, data, "Deck Drag", "Deck Drag Art", "Deck Drag Name",
                    scale, session.BattleCardFaceDescription(data, selectedBattleDeckStage));
                face.anchoredPosition = -CardSchoolArtCatalog.FrameBounds(data).center * scale;
                return ghost;
            }, () => AdjustBattleDeck(index, fromDeck ? -1 : 1),
            active => { if (targetGlow != null) targetGlow.gameObject.SetActive(active); });
            card.onClick.AddListener(() => { if (!drag.SuppressClick) AdjustBattleDeck(index, fromDeck ? -1 : 1); });
        }

        private void BuildDeckBuilderCards(RectTransform panel, RectTransform rail, Image targetGlow, IReadOnlyList<int> deck)
        {
            IReadOnlyList<int> bonus = session.GetBonusBattleCards(selectedBattleDeckStage);
            CreateText("Deck Builder Deck Title", rail, new Vector2(670f, 56f), new Vector2(0f, 389f),
                36, FontStyle.Bold, TextAnchor.MiddleCenter, WorkshopPaperInk).text = WorkshopStageName(selectedBattleDeckStage) + "牌组";
            deckBuilderBaseScrollRect = BuildDeckBuilderColumn(panel, rail, targetGlow, deck, false);
            deckBuilderSpecialScrollRect = BuildDeckBuilderColumn(panel, rail, targetGlow, bonus, true);
            deckBuilderStatus = CreateText("Deck Builder Status", rail, new Vector2(660f, 24f), new Vector2(0f, -405f),
                20, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperInk);
            deckBuilderStatus.text = DeckBuilderStatusText;
            Button back = CreateButton("Back To Card Workshop Chests", rail, new Vector2(570f, 46f),
                new Vector2(0f, -443f), PanelRaised, Ink, 24);
            back.GetComponentInChildren<Text>().text = "返回卡组预览";
            back.onClick.AddListener(() =>
            {
                if (BlockIncompleteDeckBuilderNavigation()) return;
                NavalBattleStage stage = selectedBattleDeckStage;
                int page = battleCardCollectionPage;
                ResetCardWorkshopView();
                selectedBattleDeckStage = stage;
                battleCardUpgradePage = page;
                Render();
            });
        }

        private void SetDeckBuilderNotice(string message)
        {
            deckBuilderNotice = message;
            if (deckBuilderStatus != null) deckBuilderStatus.text = DeckBuilderStatusText;
        }

        private void RememberDeckBuilderScroll()
        {
            // Read synchronously too: a card click can precede ScrollRect.LateUpdate.
            RememberDeckBuilderColumnScroll(deckBuilderBaseScrollRect, false);
            RememberDeckBuilderColumnScroll(deckBuilderSpecialScrollRect, true);
        }

        private ScrollRect BuildDeckBuilderColumn(RectTransform panel, RectTransform rail, Image targetGlow,
            IReadOnlyList<int> cards, bool special)
        {
            string prefix = special ? "Deck Builder Special" : "Deck Builder Base";
            RectTransform column = CreateDeckListFrame(rail, prefix + " Column", new Vector2(332f, 718f),
                new Vector2(special ? 174f : -174f, -7f),
                (special ? "专属牌 " : "基础卡 ") + cards.Count + " 张");
            RectTransform viewport = CreateRect(prefix + " Scroll Viewport", column,
                new Vector2(DeckBuilderListViewportWidth, DeckBuilderListViewportHeight), new Vector2(-5f, -19f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            List<DeckBuilderGroupedCard> groups = GroupDeckBuilderCards(cards);
            float contentHeight = Mathf.Max(DeckBuilderListViewportHeight, groups.Count * DeckBuilderListRowHeight);
            RectTransform content = CreateRect(prefix + " Scroll Content", viewport,
                new Vector2(DeckBuilderListViewportWidth, contentHeight), Vector2.zero);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            for (int row = 0; row < groups.Count; row++)
            {
                DeckBuilderGroupedCard group = groups[row];
                int index = group.Index;
                string rowName = (special ? "Crew Bonus Deck Card " : "Remove Deck Card ") + index + " Copy " + group.FirstSlot;
                Button card = CreateCompactDeckListCard(content, index, group.Quantity, rowName,
                    new Vector2(DeckBuilderListViewportWidth, DeckBuilderListRowHeight),
                    new Vector2(0f, -(row + .5f) * DeckBuilderListRowHeight),
                    !special && index == deckBuilderAddedCardIndex);
                RectTransform rect = card.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
                DeckCardPreviewHover hover = card.gameObject.AddComponent<DeckCardPreviewHover>();
                hover.Bind(() => ShowDeckBuilderHoverPreview(panel, card, index, special),
                    () => { if (deckBuilderHoverSource == card) HideDeckBuilderHoverPreview(); });
                if (!special) BindDeckBuilderCard(card, panel, rail, targetGlow, index, true);
                else card.onClick.AddListener(() => SetDeckBuilderNotice("专属牌随船员／部件自动加入，不能手动移除"));
            }
            if (groups.Count == 0)
                CreateText(prefix + " Empty", viewport, new Vector2(286f, 140f), Vector2.zero,
                    21, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperMuted).text = special
                    ? "本阶段暂无专属牌\n招募船员或装备对应部件后自动加入" : "本阶段暂无基础卡牌";

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = DeckBuilderListRowHeight;
            scroll.inertia = false;
            float savedOffset = special ? deckBuilderSpecialScrollOffset : deckBuilderBaseScrollOffset;
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(savedOffset, 0f, contentHeight - DeckBuilderListViewportHeight));
            scroll.onValueChanged.AddListener(_ =>
            {
                RememberDeckBuilderColumnScroll(scroll, special);
                HideDeckBuilderHoverPreview();
            });

            // The fixed-width rail never changes the card viewport. Only its diamond appears on overflow.
            RectTransform track = CreateRect(prefix + " Scrollbar", column,
                new Vector2(18f, DeckBuilderListViewportHeight), new Vector2(157f, -19f));
            Image trackHit = track.gameObject.AddComponent<Image>();
            trackHit.color = Color.clear;
            Image thumb = CreateDeckListScrollThumb(track, prefix + " Scroll Thumb", new Vector2(18f, 30f), Vector2.zero);
            DeckListDiamondScrollbar scrollbar = track.gameObject.AddComponent<DeckListDiamondScrollbar>();
            scrollbar.Bind(scroll, thumb.rectTransform);
            return scroll;
        }

        private void ShowDeckBuilderHoverPreview(RectTransform panel, Button source, int index, bool special)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy || source == null || !source.IsActive()) return;
            if (deckBuilderHoverSource == source && deckBuilderHoverPreview != null) return;
            HideDeckBuilderHoverPreview();
            CoreBattleCard data = session.GetBattleCard(index);
            Rect bounds = CardSchoolArtCatalog.FrameBounds(data);
            float scale = Mathf.Min(260f / bounds.width, 390f / bounds.height);
            // Keep the full card vertically centered in the view, independent of the hovered row.
            // Base cards sit over the last collection column; special cards sit over the base list.
            float centerY = panel.InverseTransformPoint(contentRoot.TransformPoint(Vector3.zero)).y;
            deckBuilderHoverPreview = CreateRect("Deck Builder Hover Preview", panel, bounds.size * scale,
                new Vector2(special ? 402f : 12f, centerY));
            deckBuilderHoverSource = source;
            CanvasGroup group = deckBuilderHoverPreview.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            RectTransform face = CreateCollectionCardFace(deckBuilderHoverPreview, data, "Deck Hover",
                "Deck Hover Art", "Deck Hover Name", scale,
                session.BattleCardFaceDescription(data, selectedBattleDeckStage), "Deck Hover Effect");
            face.anchoredPosition = -bounds.center * scale;
            foreach (Graphic graphic in deckBuilderHoverPreview.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            deckBuilderHoverPreview.SetAsLastSibling();
        }

        private void HideDeckBuilderHoverPreview()
        {
            RectTransform preview = deckBuilderHoverPreview;
            deckBuilderHoverPreview = null;
            deckBuilderHoverSource = null;
            if (preview == null) return;
            preview.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(preview.gameObject);
            else DestroyImmediate(preview.gameObject);
        }

        private static List<DeckBuilderGroupedCard> GroupDeckBuilderCards(IReadOnlyList<int> cards)
        {
            var groups = new List<DeckBuilderGroupedCard>();
            var byIndex = new Dictionary<int, DeckBuilderGroupedCard>();
            for (int slot = 0; slot < cards.Count; slot++)
            {
                int index = cards[slot];
                if (byIndex.TryGetValue(index, out DeckBuilderGroupedCard group)) group.Quantity++;
                else
                {
                    group = new DeckBuilderGroupedCard { Index = index, Quantity = 1, FirstSlot = slot };
                    byIndex.Add(index, group);
                    groups.Add(group);
                }
            }
            return groups;
        }

        private void RememberDeckBuilderColumnScroll(ScrollRect scroll, bool special)
        {
            if (scroll == null || !scroll.isActiveAndEnabled || scroll.content == null || scroll.viewport == null) return;
            float extent = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
            float offset = Mathf.Clamp(scroll.content.anchoredPosition.y, 0f, extent);
            if (special)
            {
                deckBuilderSpecialScrollOffset = offset;
            }
            else
            {
                deckBuilderBaseScrollOffset = offset;
            }
        }
    }
}
