using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const int BattleCardCollectionPageSize = 8;
        private static readonly NavalBattleStage[] WorkshopBattleStages =
        {
            NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding,
        };

        private NavalBattleStage selectedBattleDeckStage = NavalBattleStage.Preparation;
        private int battleCardCollectionPage;
        private bool battleDeckOverviewOpen;
        private List<int> deckBuilderDraft;
        private CoreVoyageSession deckBuilderDraftSession;
        private NavalBattleStage deckBuilderDraftStage;
        private string deckBuilderDraftPort;

        private bool HasCurrentDeckBuilderDraft => deckBuilderDraft != null &&
            ReferenceEquals(deckBuilderDraftSession, session) && deckBuilderDraftStage == selectedBattleDeckStage &&
            deckBuilderDraftPort == session?.CurrentPort;

        private IReadOnlyList<int> CurrentDeckBuilderCards => HasCurrentDeckBuilderDraft
            ? deckBuilderDraft : session.GetBattleDeck(selectedBattleDeckStage);

        private bool DeckBuilderNeedsMoreCards => session != null && session.Phase == CoreDemoPhase.Port &&
            activePortFacility == 4 && cardWorkshopDeckBuilderOpen && !battleCardUpgradeOpen &&
            !specialCardCollectionOpen && !battleDeckOverviewOpen &&
            CurrentDeckBuilderCards.Count < CoreVoyageSession.MinimumBaseBattleDeckSize;

        private string DeckBuilderMinimumNotice => "基础牌组至少需要 " + CoreVoyageSession.MinimumBaseBattleDeckSize + " 张卡牌";

        private string DeckBuilderStatusText => DeckBuilderNeedsMoreCards
            ? DeckBuilderMinimumNotice + "（当前 " + CurrentDeckBuilderCards.Count + " 张，请补足后离开）"
            : string.IsNullOrEmpty(deckBuilderNotice) ? DeckBuilderMinimumNotice : deckBuilderNotice;

        private bool BlockIncompleteDeckBuilderNavigation()
        {
            if (!DeckBuilderNeedsMoreCards) return false;
            SetDeckBuilderNotice(string.Empty);
            return true;
        }

        private void ClearDeckBuilderDraft()
        {
            deckBuilderDraft = null;
            deckBuilderDraftSession = null;
            deckBuilderDraftPort = null;
        }

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
                // Core applies the same inventory limits and migration as voyage saves.
                List<int> restored = new List<int>();
                foreach (string token in saved.Split(','))
                {
                    if (int.TryParse(token, out int index)) restored.Add(index);
                }
                session.RestoreBattleDeck(stage, restored);
            }
            SaveStageBattleDecks();
            PlayerPrefs.Save();
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

            BuildInteractiveDeckBuilder();
        }

        private void BuildStageBattleDeckOverview()
        {
            BuildCardWorkshopPreview();
        }

        private void SelectWorkshopBattleStage(NavalBattleStage stage, int collectionPage = 0)
        {
            if (BlockIncompleteDeckBuilderNavigation()) return;
            ResetDeckBuilderPresentation();
            selectedBattleDeckStage = stage;
            cardWorkshopDeckBuilderOpen = true;
            battleCardUpgradeOpen = false;
            specialCardCollectionOpen = false;
            battleDeckOverviewOpen = false;
            battleCardCollectionPage = collectionPage;
            Render();
        }

        private void AdjustBattleDeck(int cardIndex, int delta)
        {
            cardIndex = CoreVoyageSession.CanonicalBattleCardIndex(cardIndex);
            if (session.Phase != CoreDemoPhase.Port || cardIndex < 0 || cardIndex >= CoreVoyageSession.CardLibrary.Count) return;
            CoreBattleCard card = CoreVoyageSession.CardLibrary[cardIndex];
            if (!card.IsAllowedInStage(selectedBattleDeckStage) || !session.IsBattleCardUnlocked(card) ||
                CoreVoyageSession.IsSpecialBattleCard(card.Id) || !string.IsNullOrEmpty(card.RequiredCrew) || !string.IsNullOrEmpty(card.RequiredComponent)) return;
            List<int> updated = new List<int>(CurrentDeckBuilderCards);
            if (delta > 0)
            {
                if (BattleDeckCopies(updated, cardIndex) >= session.GetBaseBattleCardOwnedCopies(cardIndex))
                { SetDeckBuilderNotice("同名卡牌最多加入 " + CoreVoyageSession.BaseBattleCardCopies + " 张"); return; }
                if (updated.Count >= CoreVoyageSession.MaximumBaseBattleDeckSize) { SetDeckBuilderNotice("牌组已满，可先移除一张卡牌"); return; }
                updated.Add(cardIndex);
            }
            else if (delta < 0)
            {
                if (!updated.Remove(cardIndex)) return;
            }
            else return;
            if (updated.Count < CoreVoyageSession.MinimumBaseBattleDeckSize)
            {
                // Incomplete edits stay on this screen. Gameplay and autosave retain
                // the last valid deck until the player has supplied the minimum again.
                deckBuilderDraft = updated;
                deckBuilderDraftSession = session;
                deckBuilderDraftStage = selectedBattleDeckStage;
                deckBuilderDraftPort = session.CurrentPort;
                RememberDeckBuilderScroll();
                deckBuilderNotice = string.Empty;
                deckBuilderAddedCardIndex = delta > 0 ? cardIndex : -1;
                Render();
                return;
            }
            if (!session.SetBattleDeck(selectedBattleDeckStage, updated)) return;
            ClearDeckBuilderDraft();
            SaveStageBattleDecks();
            if (Application.isPlaying && !editorPreviewBuilding)
            {
                // Restoring the curated defaults is part of profile loading;
                // only a real workshop change counts toward deck achievements.
                AchievementCatalog.RecordDeckEdited();
            }
            SaveProgress();
            portNotice = WorkshopStageName(selectedBattleDeckStage) + "已保存：基础 " + updated.Count + "/" + CoreVoyageSession.MaximumBaseBattleDeckSize + " 张，船员追加 " + session.GetCrewBattleCards(selectedBattleDeckStage).Count + " 张。";
            RememberDeckBuilderScroll();
            deckBuilderNotice = string.Empty;
            deckBuilderAddedCardIndex = delta > 0 ? cardIndex : -1;
            Render();
        }

        private int BattleDeckCopies(int cardIndex)
        {
            return BattleDeckCopies(CurrentDeckBuilderCards, cardIndex);
        }

        private int BattleDeckCopies(IReadOnlyList<int> cards, int cardIndex)
        {
            cardIndex = CoreVoyageSession.CanonicalBattleCardIndex(cardIndex);
            int count = 0;
            CoreBattleCard target = session.GetBattleCard(cardIndex, selectedBattleDeckStage);
            string key = selectedBattleDeckStage == NavalBattleStage.Boarding && target != null &&
                target.RequiredCrew.Length == 0 && target.RequiredComponent.Length == 0 ? target.BoardingSpec?.Key : null;
            for (int index = 0; index < cards.Count; index += 1)
            {
                CoreBattleCard other = key == null ? null : session.GetBattleCard(cards[index], selectedBattleDeckStage);
                if (CoreVoyageSession.CanonicalBattleCardIndex(cards[index]) == cardIndex || (key != null && other?.BoardingSpec?.Key == key &&
                    other.RequiredCrew.Length == 0 && other.RequiredComponent.Length == 0)) count += 1;
            }
            return count;
        }
    }
}
