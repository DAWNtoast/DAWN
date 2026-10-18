using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const int BoardingChoicePageSize = 5;
        private RectTransform boardingCardChoiceOverlay;
        private CanvasGroup boardingChoiceBackground;
        private bool boardingChoiceBackgroundInteractable, boardingChoiceBackgroundRaycasts;
        private CoreBattleState boardingChoiceBattle;
        private CoreBoardingPlayChoice boardingChoice;
        private CoreBoardingPlayOptions boardingChoiceOptions;
        private Button boardingChoiceSource;
        private CoreBattleCard boardingChoiceCard;
        private int boardingChoiceHandIndex, boardingChoicePage;
        private string boardingChoiceNotice;

        public bool IsBoardingCardChoiceOpen => boardingCardChoiceOverlay != null;

        private bool TryBeginBoardingCardChoice(Button source, int handIndex, CoreBattleCard card)
        {
            if (card?.BoardingSpec == null || session?.Battle?.Stage != NavalBattleStage.Boarding) return false;
            CoreBoardingPlayChoice choice = session.GetBoardingPlayChoice(handIndex);
            if (choice == null || !choice.RequiresChoice || choice.Kind == CoreBoardingChoiceKind.None ||
                choice.Kind == CoreBoardingChoiceKind.DelayedEffect) return false;
            CloseBoardingCardChoice();
            boardingChoiceSource = source;
            boardingChoiceHandIndex = handIndex;
            boardingChoiceCard = card;
            boardingChoiceBattle = session.Battle;
            boardingChoiceOptions = new CoreBoardingPlayOptions { SnapshotToken = choice.SnapshotToken };
            boardingChoice = choice;
            boardingChoicePage = 0;
            boardingChoiceNotice = string.Empty;
            boardingChoiceBackground = GetOrAddCanvasGroup(contentRoot.gameObject);
            boardingChoiceBackgroundInteractable = boardingChoiceBackground.interactable;
            boardingChoiceBackgroundRaycasts = boardingChoiceBackground.blocksRaycasts;
            boardingChoiceBackground.interactable = false;
            boardingChoiceBackground.blocksRaycasts = false;
            boardingCardChoiceOverlay = CreateStretchPanel("Boarding Card Choice Overlay", contentRoot.parent,
                new Color(.02f, .025f, .035f, .9f));
            boardingCardChoiceOverlay.SetAsLastSibling();
            Image shade = boardingCardChoiceOverlay.GetComponent<Image>();
            shade.sprite = null; shade.overrideSprite = null; shade.raycastTarget = true;
            RenderBoardingCardChoice();
            return true;
        }

        private bool IsBoardingChoiceCurrent() => boardingCardChoiceOverlay != null && isActiveAndEnabled &&
            session != null && session.Phase == CoreDemoPhase.Battle && ReferenceEquals(session.Battle, boardingChoiceBattle) &&
            session.Battle.Stage == NavalBattleStage.Boarding && boardingChoiceHandIndex >= 0 &&
            boardingChoiceHandIndex < session.Battle.Hand.Count &&
            session.GetBattleCard(session.Battle.Hand[boardingChoiceHandIndex]).Id == boardingChoiceCard.Id;

        private void RenderBoardingCardChoice()
        {
            if (!IsBoardingChoiceCurrent()) { CloseBoardingCardChoice(); return; }
            Transform old = boardingCardChoiceOverlay.Find("Boarding Card Choice Sheet");
            if (old != null)
            {
                old.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            RectTransform sheet = CreateFixedPanel("Boarding Card Choice Sheet", boardingCardChoiceOverlay,
                new Vector2(1540f, 900f), Vector2.zero, Panel);
            MarketShopSkin.ApplyPanel(sheet.GetComponent<Image>());
            string title = boardingChoice.Kind == CoreBoardingChoiceKind.HandBurn ? "选择要烧掉的手牌" :
                boardingChoice.Kind == CoreBoardingChoiceKind.HandRetain ? "选择要保留的手牌" :
                boardingChoice.Kind == CoreBoardingChoiceKind.DiscardRecover ? "选择要取回的卡牌" : "选择入手的卡牌";
            BoardingChoiceLabel("Boarding Choice Title", sheet, new Vector2(1420f, 55f), new Vector2(0f, 393f),
                30, boardingChoiceCard.Name + " · " + title);
            BoardingChoiceLabel("Boarding Choice Description", sheet, new Vector2(1400f, 70f), new Vector2(0f, 316f),
                21, boardingChoice.Description);

            var candidates = new List<CoreBoardingChoiceCandidate>();
            if (boardingChoice.Candidates != null)
                foreach (CoreBoardingChoiceCandidate candidate in boardingChoice.Candidates)
                    candidates.Add(candidate);
            int pages = Mathf.Max(1, (candidates.Count + BoardingChoicePageSize - 1) / BoardingChoicePageSize);
            boardingChoicePage = Mathf.Clamp(boardingChoicePage, 0, pages - 1);
            int first = boardingChoicePage * BoardingChoicePageSize;
            int count = Mathf.Min(BoardingChoicePageSize, candidates.Count - first);
            for (int i = 0; i < count; i++)
            {
                CoreBoardingChoiceCandidate candidate = candidates[first + i];
                CreateBoardingChoiceCandidate(sheet, candidate, new Vector2((i - (count - 1) * .5f) * 280f, -20f));
            }
            if (candidates.Count == 0 && boardingChoice.Kind != CoreBoardingChoiceKind.None)
                BoardingChoiceLabel("Boarding Choice Empty", sheet, new Vector2(1300f, 90f), new Vector2(0f, -30f), 24, "当前没有可选择的目标");
            if (pages > 1)
            {
                BoardingChoiceButton("Boarding Choice Previous", sheet, new Vector2(150f, 44f), new Vector2(-230f, -270f),
                    "上一页", () => { boardingChoicePage--; RenderBoardingCardChoice(); }).interactable = boardingChoicePage > 0;
                BoardingChoiceLabel("Boarding Choice Page", sheet, new Vector2(200f, 44f), new Vector2(0f, -270f), 20,
                    (boardingChoicePage + 1) + " / " + pages);
                BoardingChoiceButton("Boarding Choice Next", sheet, new Vector2(150f, 44f), new Vector2(230f, -270f),
                    "下一页", () => { boardingChoicePage++; RenderBoardingCardChoice(); }).interactable = boardingChoicePage + 1 < pages;
            }

            string summary = boardingChoice.MinSelection == boardingChoice.MaxSelection
                ? "已选择 " + boardingChoiceOptions.SelectionIndexes.Count + " / " + boardingChoice.MaxSelection
                : "已选择 " + boardingChoiceOptions.SelectionIndexes.Count + " 张（可选 " + boardingChoice.MinSelection + "–" + boardingChoice.MaxSelection + " 张）";
            if (boardingChoice.Kind == CoreBoardingChoiceKind.DrawChoose) summary += " · 剩余卡牌按原顺序放回牌库底";
            if (!string.IsNullOrEmpty(boardingChoiceNotice)) summary += "\n" + boardingChoiceNotice;
            BoardingChoiceLabel("Boarding Choice Summary", sheet, new Vector2(1400f, 56f), new Vector2(0f, -330f), 21, summary);
            Button cancel = BoardingChoiceButton("Cancel Boarding Card Choice", sheet, new Vector2(210f, 58f),
                new Vector2(-300f, -404f), "取消", CloseBoardingCardChoice);
            Button confirm = BoardingChoiceButton("Confirm Boarding Card Choice", sheet, new Vector2(250f, 58f),
                new Vector2(300f, -404f), "确认出牌", ConfirmBoardingCardChoice);
            confirm.interactable = boardingChoiceOptions.SelectionIndexes.Count >= boardingChoice.MinSelection &&
                    boardingChoiceOptions.SelectionIndexes.Count <= boardingChoice.MaxSelection;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }

        private void CreateBoardingChoiceCandidate(RectTransform sheet, CoreBoardingChoiceCandidate candidate, Vector2 position)
        {
            List<int> selections = boardingChoiceOptions.SelectionIndexes;
            bool selected = selections.Contains(candidate.Value);
            Image selection = CreateImage("Boarding Candidate Selection " + candidate.Value, sheet,
                new Vector2(256f, 436f), position, selected ? Gold : PanelRaised);
            selection.raycastTarget = false;
            var outline = selection.gameObject.AddComponent<Outline>();
            outline.effectColor = selected ? Gold : new Color(.22f, .19f, .14f, .6f);
            outline.effectDistance = new Vector2(3f, -3f);
            Button button = BoardingChoiceButton("Boarding Candidate " + candidate.Value, sheet,
                new Vector2(252f, 432f), position, string.Empty, () => ToggleBoardingChoiceCandidate(candidate.Value));
            button.GetComponentInChildren<Text>().enabled = false;
            Image backing = button.image;
            backing.color = selected ? Gold : PanelRaised;
            backing.raycastPadding = Vector4.zero;
            if (candidate.CardIndex >= 0 && candidate.CardIndex < CoreVoyageSession.CardLibrary.Count)
            {
                CoreBattleCard data = session.GetBattleCard(candidate.CardIndex);
                CreateCollectionCardFace(button.transform, data, "Boarding Choice " + candidate.Value,
                    "Boarding Choice Art " + candidate.Value, "Boarding Choice Card Name " + candidate.Value, .9f);
                // The collection factory expands parent hit targets for full-sized cards.
                // This modal owns a fixed, nonoverlapping choice rectangle.
                button.image.raycastPadding = Vector4.zero;
            }
            else
                BoardingChoiceLabel("Boarding Choice Effect " + candidate.Value, button.transform, new Vector2(196f, 264f),
                    Vector2.zero, 21, candidate.Label);
            string caption = selected ? "已选 · " : string.Empty;
            caption += candidate.Label;
            Text label = BoardingChoiceLabel("Boarding Candidate Label " + candidate.Value, sheet,
                new Vector2(260f, 48f), position + Vector2.up * 240f, 18, caption);
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }

        private void ToggleBoardingChoiceCandidate(int value)
        {
            if (!IsBoardingChoiceCurrent()) { CloseBoardingCardChoice(); return; }
            List<int> selected = boardingChoiceOptions.SelectionIndexes;
            if (!selected.Remove(value))
            {
                int maximum = boardingChoice.MaxSelection;
                if (maximum == 1) selected.Clear();
                if (selected.Count < maximum) selected.Add(value);
            }
            boardingChoiceNotice = string.Empty;
            RenderBoardingCardChoice();
        }

        private void ConfirmBoardingCardChoice()
        {
            if (!IsBoardingChoiceCurrent()) { CloseBoardingCardChoice(); return; }
            if (boardingChoice.Kind == CoreBoardingChoiceKind.DrawChoose)
            {
                boardingChoiceOptions.BottomOrderIndexes.Clear();
                foreach (CoreBoardingChoiceCandidate candidate in boardingChoice.Candidates)
                    if (!boardingChoiceOptions.SelectionIndexes.Contains(candidate.Value)) boardingChoiceOptions.BottomOrderIndexes.Add(candidate.Value);
            }
            string reason = session.GetBoardingChoiceUnavailableReason(boardingChoiceHandIndex, boardingChoiceOptions);
            if (!string.IsNullOrEmpty(reason)) { boardingChoiceNotice = reason; RenderBoardingCardChoice(); return; }
            Button source = boardingChoiceSource;
            int handIndex = boardingChoiceHandIndex;
            CoreBattleCard card = boardingChoiceCard;
            CoreBoardingPlayOptions options = boardingChoiceOptions;
            CloseBoardingCardChoice();
            if (source != null && source.gameObject.activeInHierarchy && !battleAnimationPlaying)
                StartCoroutine(AnimateBattleCard(source, handIndex, card, options));
        }

        private Text BoardingChoiceLabel(string name, Transform parent, Vector2 size, Vector2 position, int fontSize, string value)
        {
            Text label = CreateText(name, parent, size, position, fontSize, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            label.text = value ?? string.Empty; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = Mathf.Max(14, fontSize - 3); label.resizeTextMaxSize = fontSize;
            return label;
        }

        private Button BoardingChoiceButton(string name, Transform parent, Vector2 size, Vector2 position, string caption, Action action)
        {
            Button button = CreateButton(name, parent, size, position, Gold, DeepSea, 22);
            button.GetComponentInChildren<Text>().text = caption;
            MarketShopSkin.ApplyLightButton(button);
            button.onClick.AddListener(() => action());
            return button;
        }

        private void CloseBoardingCardChoice()
        {
            RectTransform overlay = boardingCardChoiceOverlay;
            boardingCardChoiceOverlay = null;
            if (boardingChoiceBackground != null)
            {
                boardingChoiceBackground.interactable = boardingChoiceBackgroundInteractable;
                boardingChoiceBackground.blocksRaycasts = boardingChoiceBackgroundRaycasts;
            }
            boardingChoiceBackground = null;
            boardingChoiceBattle = null; boardingChoiceSource = null; boardingChoiceCard = null;
            boardingChoiceOptions = null; boardingChoice = null;
            if (overlay == null) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(overlay)) EventSystem.current.SetSelectedGameObject(null);
            overlay.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject);
        }
    }
}
