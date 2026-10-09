using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static int BattleCardCollectionPageSize => GameReadability.Level == 2 ? 2 : GameReadability.Level == 1 ? 4 : 6;
        private static readonly NavalBattleStage[] WorkshopBattleStages =
        {
            NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding,
        };

        private NavalBattleStage selectedBattleDeckStage = NavalBattleStage.Preparation;
        private int battleCardCollectionPage;
        private bool battleDeckOverviewOpen;

        public static string StageBattleDeckKey(NavalBattleStage stage)
        {
            switch (stage)
            {
                case NavalBattleStage.Preparation: return "ShatteredSail.BattleDeck.v2.Preparation";
                case NavalBattleStage.Naval: return "ShatteredSail.BattleDeck.v2.Naval";
                case NavalBattleStage.Boarding: return "ShatteredSail.BattleDeck.v2.Boarding";
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }

        private static string WorkshopStageName(NavalBattleStage stage)
        {
            switch (stage)
            {
                case NavalBattleStage.Preparation: return "备战策略";
                case NavalBattleStage.Boarding: return "接舷近战";
                default: return "炮击机动";
            }
        }

        private void RestoreStageBattleDecks()
        {
            string legacy = PlayerPrefs.GetString(BattleDeckKey, string.Empty);
            foreach (NavalBattleStage stage in WorkshopBattleStages)
            {
                string saved = PlayerPrefs.HasKey(StageBattleDeckKey(stage))
                    ? PlayerPrefs.GetString(StageBattleDeckKey(stage), string.Empty)
                    : legacy;
                if (string.IsNullOrWhiteSpace(saved)) continue;
                // Old library indices remain stable. Preserve every legal saved copy,
                // then fill only missing slots from the stage's curated defaults.
                List<int> restored = new List<int>();
                foreach (string token in saved.Split(','))
                {
                    if (int.TryParse(token, out int index)) AppendRestoredBattleCard(restored, stage, index);
                }
                IReadOnlyList<int> defaults = session.GetBattleDeck(stage);
                for (int index = 0; restored.Count < 8 && index < defaults.Count; index += 1)
                    AppendRestoredBattleCard(restored, stage, defaults[index]);
                IReadOnlyList<int> available = session.GetAvailableBattleCards(stage);
                for (int copy = 0; restored.Count < 8 && copy < 3; copy += 1)
                    for (int index = 0; restored.Count < 8 && index < available.Count; index += 1)
                        AppendRestoredBattleCard(restored, stage, available[index]);
                session.SetBattleDeck(stage, restored);
            }
            SaveStageBattleDecks();
            PlayerPrefs.Save();
        }

        private static void AppendRestoredBattleCard(List<int> cards, NavalBattleStage stage, int index)
        {
            if (cards.Count >= 10 || index < 0 || index >= CoreVoyageSession.CardLibrary.Count) return;
            CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
            if (!card.IsAllowedInStage(stage) || !string.IsNullOrEmpty(card.RequiredCrew) || !string.IsNullOrEmpty(card.RequiredComponent) || BattleDeckCopies(cards, index) >= 3) return;
            cards.Add(index);
        }

        private void SaveStageBattleDecks()
        {
            if (session == null || session.Phase != CoreDemoPhase.Port) return;
            foreach (NavalBattleStage stage in WorkshopBattleStages)
            {
                IReadOnlyList<int> cards = session.GetBattleDeck(stage);
                List<string> indexes = new List<string>();
                for (int index = 0; index < cards.Count; index += 1) indexes.Add(cards[index].ToString());
                PlayerPrefs.SetString(StageBattleDeckKey(stage), string.Join(",", indexes));
            }
            // Keep the v1 mirror compatible with the existing profile save path.
            configuredBattleDeck.Clear();
            configuredBattleDeck.AddRange(session.GetBattleDeck(NavalBattleStage.Naval));
        }

        private void BuildDeckBuilderFacility()
        {
            if (battleCardUpgradeOpen)
            {
                BuildBattleCardUpgradeFacility();
                return;
            }
            if (battleDeckOverviewOpen)
            {
                BuildStageBattleDeckOverview();
                return;
            }

            RectTransform panel = CreateFixedPanel("Deck Builder Panel", contentRoot, new Vector2(1820f, 650f), new Vector2(0f, -55f), Color.clear);
            Color parchmentInk = Html("#302116");
            Color parchmentMuted = Html("#71583A");
            RawImage parchment = CreateRawImage("Deck Builder Parchment", panel, new Vector2(990f, 630f), new Vector2(-380f, 0f), cardWorkshopArt == null ? null : cardWorkshopArt.CollectionParchment, new Rect(0f, 0f, 1f, 1f));
            parchment.raycastTarget = false;
            RectTransform deckBacking = CreateRect("Deck Builder Deck Backing", panel, new Vector2(386f, 554f), new Vector2(610f, -5f));
            Image deckBackingImage = deckBacking.gameObject.AddComponent<Image>();
            // Keep the live deck rail on the same authored parchment used by
            // UIPreviewsForMe instead of replacing it with a flat fill.
            deckBackingImage.sprite = Resources.Load<Sprite>("CardWorkshopV2/deck_builder_parchment_v2");
            deckBackingImage.type = Image.Type.Simple;
            deckBackingImage.color = Color.white;
            deckBackingImage.preserveAspect = false;
            deckBackingImage.pixelsPerUnitMultiplier = 3.0117648f;
            deckBackingImage.raycastTarget = false;
            RawImage pirateFrame = CreateRawImage("Deck Builder Pirate Frame", panel, new Vector2(430f, 640f), new Vector2(610f, 0f), cardWorkshopArt == null ? null : cardWorkshopArt.PirateDeckFrame, new Rect(0f, 0f, 1f, 1f));
            pirateFrame.raycastTarget = false;
            pirateFrame.color = new Color(.72f, .68f, .60f, 1f);

            Text ready = CreateText("Three Stage Deck Readiness", panel, new Vector2(1760f, 34f), new Vector2(0f, 337f), GameReadability.FontSize(18), FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            ready.text = session.GetBattleDeckSummary();
            AddSceneLabelOutline(ready);
            // Keep every header action relative to the paper, including the forge entry.
            // Four equal columns leave room for the parchment's torn edges.
            Vector2 headerButtonSize = new Vector2(210f, 52f * GameReadability.Scale);
            const float headerButtonGap = 12f;
            float headerWidth = headerButtonSize.x * (WorkshopBattleStages.Length + 1) + headerButtonGap * WorkshopBattleStages.Length;
            float firstHeaderX = (headerButtonSize.x - headerWidth) * 0.5f;
            float headerTop = parchment.rectTransform.rect.height * .5f - 25f;
            RectTransform header = CreateRect("Deck Builder Header", parchment.rectTransform, new Vector2(headerWidth, headerButtonSize.y), new Vector2(0f, headerTop - headerButtonSize.y * .5f));
            for (int stageIndex = 0; stageIndex < WorkshopBattleStages.Length; stageIndex += 1)
            {
                NavalBattleStage stage = WorkshopBattleStages[stageIndex];
                bool selected = stage == selectedBattleDeckStage;
                Button tab = CreateButton("Battle Deck Stage Tab " + stage, header, headerButtonSize, new Vector2(firstHeaderX + stageIndex * (headerButtonSize.x + headerButtonGap), 0f), selected ? Teal : new Color(0.72f, 0.59f, 0.4f, 0.75f), selected ? Ink : parchmentInk, GameReadability.FontSize(22));
                tab.GetComponentInChildren<Text>().text = WorkshopStageName(stage);
                WoodButtonSkin.SetSelected(tab, selected);
                tab.onClick.AddListener(() => SelectWorkshopBattleStage(stage));
            }
            Button forge = CreateButton("Open Battle Card Upgrades", header, headerButtonSize, new Vector2(-firstHeaderX, 0f), Gold, Ink, GameReadability.FontSize(22));
            forge.GetComponentInChildren<Text>().text = "卡牌强化";
            forge.onClick.AddListener(() => { battleCardUpgradeOpen = true; battleCardUpgradePage = 0; Render(); });

            IReadOnlyList<int> deck = session.GetBattleDeck(selectedBattleDeckStage);
            IReadOnlyList<int> available = session.GetAvailableBattleCards(selectedBattleDeckStage);
            List<int> collectionCards = new List<int>();
            for (int index = 0; index < available.Count; index += 1)
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[available[index]];
                if (card.IsAllowedInStage(selectedBattleDeckStage) && string.IsNullOrEmpty(card.RequiredCrew) && string.IsNullOrEmpty(card.RequiredComponent)) collectionCards.Add(available[index]);
            }
            int pageCount = Mathf.Max(1, (collectionCards.Count + BattleCardCollectionPageSize - 1) / BattleCardCollectionPageSize);
            battleCardCollectionPage = Mathf.Clamp(battleCardCollectionPage, 0, pageCount - 1);
            float collectionTop = Mathf.Min(216f, headerTop - headerButtonSize.y - 12f);
            const float collectionBottom = -244f;
            RectTransform collection = CreateRect("Deck Builder Collection", panel,
                new Vector2(920f, collectionTop - collectionBottom), new Vector2(-380f, (collectionTop + collectionBottom) * .5f));
            collection.gameObject.AddComponent<RectMask2D>();
            int first = battleCardCollectionPage * BattleCardCollectionPageSize;
            for (int slot = 0; slot < BattleCardCollectionPageSize && first + slot < collectionCards.Count; slot += 1)
                BuildWorkshopCollectionCard(collection, collectionCards[first + slot], slot, deck, parchmentInk, parchmentMuted);

            Button previous = CreateButton("Previous Battle Card Collection Page", panel, new Vector2(130f, 44f * GameReadability.Scale), new Vector2(-710f, -287f), new Color(0.72f, 0.59f, 0.4f, 0.75f), parchmentInk, GameReadability.FontSize(22));
            previous.GetComponentInChildren<Text>().text = "上一页";
            previous.interactable = battleCardCollectionPage > 0;
            previous.onClick.AddListener(() => { battleCardCollectionPage -= 1; Render(); });
            Text page = CreateText("Battle Card Collection Page", panel, new Vector2(450f, 42f), new Vector2(-380f, -287f), GameReadability.FontSize(18), FontStyle.Bold, TextAnchor.MiddleCenter, parchmentInk);
            page.text = "收藏 " + collectionCards.Count + " 种　·　第 " + (battleCardCollectionPage + 1) + "/" + pageCount + " 页";
            Button next = CreateButton("Next Battle Card Collection Page", panel, new Vector2(130f, 44f * GameReadability.Scale), new Vector2(-50f, -287f), new Color(0.72f, 0.59f, 0.4f, 0.75f), parchmentInk, GameReadability.FontSize(22));
            next.GetComponentInChildren<Text>().text = "下一页";
            next.interactable = battleCardCollectionPage < pageCount - 1;
            next.onClick.AddListener(() => { battleCardCollectionPage += 1; Render(); });

            BuildWorkshopStageDeckList(panel, deck);
            // The authored preview places the pirate frame above the scroll content,
            // so restore that sibling order after the rail and its rows are built.
            pirateFrame.transform.SetAsLastSibling();
            Button back = CreateButton("Back To Card Workshop Chests", panel, new Vector2(330f, 46f * GameReadability.Scale), new Vector2(610f, -369f), PanelRaised, Ink, GameReadability.FontSize(22));
            back.GetComponentInChildren<Text>().text = "返回卡箱陈列";
            back.onClick.AddListener(() => { ResetCardWorkshopView(); Render(); });
            foreach (Text text in panel.GetComponentsInChildren<Text>(true)) { text.font = GameTypography.BodyFont; text.resizeTextForBestFit = false; }
        }

        private void BuildWorkshopCollectionCard(RectTransform collection, int cardIndex, int slot, IReadOnlyList<int> deck, Color parchmentInk, Color parchmentMuted)
        {
            CoreBattleCard cardData = session.GetBattleCard(cardIndex);
            int copies = BattleDeckCopies(deck, cardIndex);
            bool canAdd = session.Phase == CoreDemoPhase.Port && deck.Count < 10 && copies < 3;
            int columns = GameReadability.Level == 0 ? 3 : 2;
            int rows = GameReadability.Level == 2 ? 1 : 2;
            float width = (920f - (columns - 1) * 14f) / columns;
            float height = (collection.rect.height - (rows - 1) * 14f) / rows;
            Vector2 position = new Vector2(-460f + width * .5f + slot % columns * (width + 14f),
                collection.rect.height * .5f - height * .5f - slot / columns * (height + 14f));
            Button card = CreateButton("Add Deck Card " + cardIndex, collection, new Vector2(width, height), position,
                new Color(0.92f, 0.82f, 0.62f, 0.94f), parchmentInk, GameReadability.FontSize(22));
            ColorBlock colors = card.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.88f, 0.82f, 0.69f, 1f);
            colors.disabledColor = Color.white;
            card.colors = colors;
            card.GetComponentInChildren<Text>().text = string.Empty;
            Outline border = card.gameObject.AddComponent<Outline>();
            border.effectColor = canAdd ? CardEffectColor(cardData) : parchmentMuted;
            border.effectDistance = new Vector2(1f, -1f);
            float top = height * .5f;
            RawImage picture = CreateRawImage("Workshop Card Art " + cardIndex, card.transform,
                new Vector2(56f, 56f), new Vector2(0f, top - 34f), art.CardSheet, art.CardUv(cardData.ArtId));
            picture.raycastTarget = false;
            Image costPlate = CreateImage("Workshop Card Cost Plate " + cardIndex, card.transform,
                new Vector2(44f, 44f), new Vector2(-width * .5f + 30f, top - 29f), Teal, circleSprite);
            costPlate.raycastTarget = false;
            Text cost = CreateText("Workshop Card Cost " + cardIndex, card.transform, new Vector2(40f, 42f),
                costPlate.rectTransform.anchoredPosition, GameReadability.FontSize(22), FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            cost.text = cardData.Energy.ToString();
            cost.color = Color.white;
            Text copy = CreateText("Workshop Card Count " + cardIndex, card.transform, new Vector2(76f, 42f),
                new Vector2(width * .5f - 45f, top - 29f), GameReadability.FontSize(18), FontStyle.Normal, TextAnchor.MiddleRight, parchmentInk);
            copy.text = "×" + copies;
            float titleHeight = 44f * GameReadability.Scale;
            Text title = CreateText("Workshop Card Name " + cardIndex, card.transform, new Vector2(width - 18f, titleHeight),
                new Vector2(0f, top - 64f - titleHeight * .5f), GameReadability.FontSize(32), FontStyle.Normal, TextAnchor.MiddleCenter, parchmentInk);
            title.font = GameTypography.BodyFont;
            title.text = cardData.Name + (cardData.UpgradeLevel > 0 ? " +" + cardData.UpgradeLevel : string.Empty);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.resizeTextForBestFit = false;
            float bottom = -height * .5f + 36f * GameReadability.Scale;
            float effectHeight = top - 72f - titleHeight - bottom;
            Text effect = CreateText("Workshop Card Description " + cardIndex, card.transform,
                new Vector2(width - 18f, effectHeight), Vector2.zero, GameReadability.FontSize(22), FontStyle.Normal, TextAnchor.UpperLeft, parchmentInk);
            effect.text = selectedBattleDeckStage == NavalBattleStage.Boarding
                ? "当前 " + session.SelectedOrdinaryCrew + " 人实际：\n" + session.BattleCardEffectDescription(cardData, selectedBattleDeckStage)
                : cardData.Description;
            ReadableParagraph(effect, card.transform, new Vector2(width - 18f, effectHeight),
                new Vector2(0f, bottom + effectHeight * .5f), 22, true);
            Text add = CreateText("Workshop Card Add Hint " + cardIndex, card.transform, new Vector2(width - 22f, 32f * GameReadability.Scale),
                new Vector2(0f, -height * .5f + 17f * GameReadability.Scale), GameReadability.FontSize(18), FontStyle.Normal, TextAnchor.MiddleRight, canAdd ? Teal : parchmentMuted);
            add.text = canAdd ? "+ 加入牌组" : copies >= 3 ? "同名已达 3 张" : "牌组已满";
            add.horizontalOverflow = HorizontalWrapMode.Wrap;
            add.verticalOverflow = VerticalWrapMode.Truncate;
            card.interactable = canAdd;
            card.onClick.AddListener(() => AdjustBattleDeck(cardIndex, 1));
        }

        private void BuildWorkshopStageDeckList(RectTransform panel, IReadOnlyList<int> deck)
        {
            // The deck rail keeps the title and summary fixed while the complete
            // base + crew card list is available through one vertical ScrollRect.
            RectTransform rail = CreateRect("Deck Builder Deck Rail", panel, new Vector2(386f, 554f), new Vector2(610f, -5f));
            rail.gameObject.AddComponent<RectMask2D>();
            IReadOnlyList<int> crewCards = session.GetBonusBattleCards(selectedBattleDeckStage);
            float headingHeight = 54f * GameReadability.Scale;
            // The pirate frame's crest covers the upper 60 points of the rail.
            const float titleTop = 216f;
            Text title = CreateText("Deck Builder Deck Title", rail, new Vector2(300f, headingHeight), new Vector2(0f, titleTop - headingHeight * .5f), GameReadability.FontSize(32), FontStyle.Bold, TextAnchor.MiddleCenter, Html("#382518"));
            title.text = WorkshopStageName(selectedBattleDeckStage) + "牌组";
            float summaryHeight = 72f * GameReadability.Scale;
            float summaryY = titleTop - headingHeight - 8f - summaryHeight * .5f;
            Text summary = CreateText("Card Workshop Summary", rail, new Vector2(350f, summaryHeight), new Vector2(0f, summaryY), GameReadability.FontSize(18), FontStyle.Normal, TextAnchor.MiddleCenter, Html("#49321D"));
            summary.text = "基础 " + deck.Count + "/10　·　专属 " + crewCards.Count + "\n实战共 " + (deck.Count + crewCards.Count) + " 张";

            List<int> rows = new List<int>();
            for (int index = 0; index < deck.Count; index += 1)
                if (!rows.Contains(deck[index])) rows.Add(deck[index]);
            int baseRowCount = rows.Count;
            for (int index = 0; index < crewCards.Count; index += 1) rows.Add(crewCards[index]);

            // The pirate frame's opaque header and side carvings are inside its
            // rectangular bounds. Keep list content within the visible paper opening.
            const float viewportWidth = 300f;
            float viewportTop = summaryY - summaryHeight * .5f - 8f;
            float viewportHeight = viewportTop + 215f;
            float rowStep = 68f * GameReadability.Scale;
            float removeRowHeight = rowStep - 8f;
            float crewRowHeight = removeRowHeight;
            RectTransform viewport = CreateRect("Deck Builder Deck Scroll Viewport", rail,
                new Vector2(viewportWidth, viewportHeight), new Vector2(0f, viewportTop - viewportHeight * .5f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;
            float contentHeight = Mathf.Max(viewportHeight, rows.Count * rowStep);
            RectTransform content = CreateRect("Deck Builder Deck Scroll Content", viewport,
                new Vector2(viewportWidth, contentHeight), Vector2.zero);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            for (int slot = 0; slot < rows.Count; slot += 1)
            {
                int cardIndex = rows[slot];
                bool crew = slot >= baseRowCount;
                CoreBattleCard cardData = session.GetBattleCard(cardIndex);
                float rowHeight = crew ? crewRowHeight : removeRowHeight;
                Button row = CreateButton((crew ? "Crew Bonus Deck Card " : "Remove Deck Card ") + cardIndex,
                    content, new Vector2(296f, rowHeight), new Vector2(0f, -removeRowHeight * 0.5f - slot * rowStep),
                    new Color(0.04f, 0.11f, 0.13f, 0.96f), crew ? Gold : Ink, 13);
                RectTransform rowRect = row.GetComponent<RectTransform>();
                rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 1f);
                rowRect.pivot = new Vector2(0.5f, 0.5f);
                Text label = row.GetComponentInChildren<Text>();
                label.color = crew ? Gold : Ink;
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.sizeDelta = new Vector2(180f, rowHeight);
                label.rectTransform.anchoredPosition = new Vector2(-13f, 0f);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.text = cardData.Name + (cardData.UpgradeLevel > 0 ? " +" + cardData.UpgradeLevel : string.Empty);
                GameReadability.Body(label, 22);
                Text energy = CreateText(row.name + " Cost", row.transform, new Vector2(28f, rowHeight),
                    new Vector2(-126f, 0f), GameReadability.FontSize(18), FontStyle.Normal, TextAnchor.MiddleCenter, Gold);
                energy.text = cardData.Energy.ToString();
                Text quantity = CreateText(row.name + " Quantity", row.transform, new Vector2(56f, rowHeight),
                    new Vector2(109f, 0f), GameReadability.FontSize(18), FontStyle.Normal,
                    TextAnchor.MiddleRight, crew ? Gold : Ink);
                quantity.text = crew ? (string.IsNullOrEmpty(cardData.RequiredComponent) ? "船员" : "部件") :
                    "×" + BattleDeckCopies(deck, cardIndex);
                ColorBlock colors = row.colors;
                colors.disabledColor = Color.white;
                row.colors = colors;
                row.interactable = !crew && session.Phase == CoreDemoPhase.Port && deck.Count > 8;
                row.onClick.AddListener(() => AdjustBattleDeck(cardIndex, -1));
            }

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 32f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            Image scrollTrack = CreateImage("Deck Builder Scrollbar", rail, new Vector2(8f, viewportHeight),
                new Vector2(146f, viewport.anchoredPosition.y), new Color(.18f, .12f, .07f, .3f));
            RectTransform handleArea = CreateRect("Sliding Area", scrollTrack.transform,
                new Vector2(8f, viewportHeight), Vector2.zero);
            Image handle = CreateImage("Handle", handleArea, Vector2.zero, Vector2.zero, Html("#604222"));
            handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one;
            Scrollbar scrollbar = scrollTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            // Editor Scrollbar.Prelayout emits its value. Seed it before binding,
            // otherwise its default zero scrolls an overflowing fresh list to the end.
            // The content is already top-anchored at zero; no early bounds query is needed.
            scrollbar.SetValueWithoutNotify(1f);
            scroll.verticalScrollbar = scrollbar;
            scrollTrack.gameObject.SetActive(contentHeight > viewportHeight);
            if (contentHeight > viewportHeight)
                CreateText("Deck Builder Scroll Hint", rail, new Vector2(350f, 24f), new Vector2(0f, -203f),
                    16, FontStyle.Normal, TextAnchor.MiddleCenter, Html("#49321D")).text = "";
        }
        private void BuildStageBattleDeckOverview()
        {
            RectTransform panel = CreateFixedPanel("Three Stage Deck Overview", contentRoot, new Vector2(1780f, 650f), new Vector2(0f, -55f), Color.clear);
            Text heading = CreateText("Three Stage Overview Heading", panel, new Vector2(1700f, 52f * GameReadability.Scale), new Vector2(0f, 280f), GameReadability.FontSize(32), FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            heading.text = "船长珍藏 · 出航牌组总览";
            AddSceneLabelOutline(heading);
            Text readiness = CreateText("Three Stage Overview Readiness", panel, new Vector2(1700f, 45f * GameReadability.Scale), new Vector2(0f, 210f), GameReadability.FontSize(18), FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            readiness.text = session.GetBattleDeckSummary();
            AddSceneLabelOutline(readiness);
            for (int index = 0; index < WorkshopBattleStages.Length; index += 1)
            {
                NavalBattleStage stage = WorkshopBattleStages[index];
                IReadOnlyList<int> deck = session.GetBattleDeck(stage);
                IReadOnlyList<int> bonus = session.GetBonusBattleCards(stage);
                RectTransform card = CreateFixedPanel("Stage Overview " + stage, panel, new Vector2(500f, 395f), new Vector2(-560f + index * 560f, -25f), new Color(0.025f, 0.075f, 0.085f, 0.94f));
                Text name = CreateText("Stage Overview Name " + stage, card, new Vector2(450f, 48f * GameReadability.Scale), new Vector2(0f, 140f), GameReadability.FontSize(32), FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
                name.text = (index + 1) + " · " + WorkshopStageName(stage);
                Text count = CreateText("Stage Overview Count " + stage, card, new Vector2(450f, 36f * GameReadability.Scale), new Vector2(0f, 75f), GameReadability.FontSize(18), FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
                count.text = "基础 " + deck.Count + "/10　+　专属 " + bonus.Count + " 张";
                Text detail = CreateText("Stage Overview Detail " + stage, card, new Vector2(430f, 115f), new Vector2(0f, 8f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
                detail.text = stage == NavalBattleStage.Preparation ? "交战前布置战术、装填火炮。" : stage == NavalBattleStage.Naval ? "炮击、机动与防御，\n削弱敌舰后接舷。" : "登船近战，依靠船员配合。";
                Text crew = CreateText("Stage Overview Crew " + stage, card, new Vector2(430f, 58f), new Vector2(0f, -78f), 14, FontStyle.Normal, TextAnchor.MiddleCenter, Gold);
                crew.text = bonus.Count > 0 ? "已追加船员 / 部件专属牌\n进入牌表强化，三阶后支持连击" : "暂无专属牌\n招募船员或装备部件后启用";
                StackReadableInspection(card, "Stage Overview Reading " + stage, new Vector2(430f, 164f), new Vector2(0f, -38f),
                    new[] { "Stage Overview Detail " + stage, "Stage Overview Crew " + stage });
                Button edit = CreateButton("Edit Stage Deck " + stage, card, new Vector2(390f, 44f * GameReadability.Scale), new Vector2(0f, -157f), Teal, Ink, GameReadability.FontSize(22));
                edit.GetComponentInChildren<Text>().text = "编辑" + WorkshopStageName(stage);
                edit.onClick.AddListener(() => SelectWorkshopBattleStage(stage));
            }
            Button back = CreateButton("Back To Card Workshop Chests", panel, new Vector2(330f, 46f * GameReadability.Scale), new Vector2(0f, -281f), PanelRaised, Ink, GameReadability.FontSize(22));
            back.GetComponentInChildren<Text>().text = "返回卡箱陈列";
            back.onClick.AddListener(() => { ResetCardWorkshopView(); Render(); });
            foreach (Text text in panel.GetComponentsInChildren<Text>(true)) { text.font = GameTypography.BodyFont; text.resizeTextForBestFit = false; }
        }

        private void SelectWorkshopBattleStage(NavalBattleStage stage)
        {
            selectedBattleDeckStage = stage;
            battleDeckOverviewOpen = false;
            battleCardCollectionPage = 0;
            Render();
        }

        private void AdjustBattleDeck(int cardIndex, int delta)
        {
            if (session.Phase != CoreDemoPhase.Port || cardIndex < 0 || cardIndex >= CoreVoyageSession.CardLibrary.Count) return;
            CoreBattleCard card = CoreVoyageSession.CardLibrary[cardIndex];
            if (!card.IsAllowedInStage(selectedBattleDeckStage) || !string.IsNullOrEmpty(card.RequiredCrew) || !string.IsNullOrEmpty(card.RequiredComponent)) return;
            List<int> updated = new List<int>(session.GetBattleDeck(selectedBattleDeckStage));
            if (delta > 0 && updated.Count < 10 && BattleDeckCopies(updated, cardIndex) < 3) updated.Add(cardIndex);
            else if (delta < 0 && updated.Count > 8 && updated.Contains(cardIndex)) updated.Remove(cardIndex);
            else return;
            if (!session.SetBattleDeck(selectedBattleDeckStage, updated)) return;
            SaveStageBattleDecks();
            if (Application.isPlaying && !editorPreviewBuilding) AchievementCatalog.RecordDeckEdited();
            SaveProgress();
            portNotice = WorkshopStageName(selectedBattleDeckStage) + "已保存：基础 " + updated.Count + "/10 张，船员追加 " + session.GetCrewBattleCards(selectedBattleDeckStage).Count + " 张。";
            Render();
        }

        private int BattleDeckCopies(int cardIndex)
        {
            return BattleDeckCopies(session.GetBattleDeck(selectedBattleDeckStage), cardIndex);
        }

        private static int BattleDeckCopies(IReadOnlyList<int> cards, int cardIndex)
        {
            int count = 0;
            for (int index = 0; index < cards.Count; index += 1)
                if (cards[index] == cardIndex) count += 1;
            return count;
        }
    }
}
