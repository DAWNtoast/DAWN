using System;
using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string SpecialCardExperienceKey = "ShatteredSail.SpecialCards.Experience.v1";
        public const string AcquiredSpecialCardsKey = "ShatteredSail.SpecialCards.Acquired.v1";
        private bool specialCardCollectionOpen;
        private int specialCardCollectionPage;
        private int selectedSpecialCardIndex = -1;
        private bool IsDeckBuilderSpecialCardCollection => specialCardCollectionOpen && cardWorkshopDeckBuilderOpen;

        private void OpenWorkshopSpecialCardCollection(bool fromDeckBuilder)
        {
            if (BlockIncompleteDeckBuilderNavigation()) return;
            if (!specialCardCollectionOpen || cardWorkshopDeckBuilderOpen != fromDeckBuilder)
            {
                specialCardCollectionPage = 0;
                selectedSpecialCardIndex = -1;
            }
            specialCardCollectionOpen = true;
            cardWorkshopDeckBuilderOpen = fromDeckBuilder;
            battleCardUpgradeOpen = false;
            battleDeckOverviewOpen = false;
            Render();
        }

        private void RefreshSpecialCardSources()
        {
            if (editorPreviewBuilding || session == null || session.Phase != CoreDemoPhase.Port || componentArt == null) return;
            List<string> crewIds = new List<string>();
            List<string> componentIds = new List<string>();
            List<string> acquired = new List<string>();
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
            {
                CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(crew.Id);
                if (definition != null && IsCrewRecruited(crew)) acquired.Add(definition.Card.Id);
            }
            // Owning a fitting records its card in the collection. Only the selected ship's
            // equipped fittings below supply combat cards and voyage experience.
            foreach (OwnedShipComponent item in OwnedComponents)
            {
                CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForComponent(item.ComponentId);
                if (definition != null) acquired.Add(definition.Card.Id);
            }
            // Workshop/dossier previews and the departing deck share one roster.
            // A locally available identity may still be deliberately on standby.
            EnsureDepartureCrewSelection();
            crewIds.AddRange(selectedDepartureSpecialCrew);
            for (int slot = 0; slot < ComponentArtCatalog.SlotCount; slot++)
            {
                ComponentArtEntry item = EquippedComponent((ComponentSlot)slot);
                if (item != null && item.Rarity >= ComponentRarity.Rare) componentIds.Add(item.Id);
            }
            session.AcquireSpecialCards(acquired);
            session.ConfigureSpecialCardSources(crewIds, componentIds);
        }

        private void RestoreSpecialCardProgress()
        {
            if (editorPreviewBuilding) return;
            Dictionary<string, int> xp = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string token in PlayerPrefs.GetString(SpecialCardExperienceKey, string.Empty).Split(';'))
            {
                string[] pair = token.Split('=');
                if (pair.Length == 2 && int.TryParse(pair[1], out int value) && value >= 0) xp[pair[0]] = value;
            }
            // Core merges migrated paid-special-card ranks before this XP restore.
            session.RestoreSpecialCardExperience(xp);
            session.RestoreAcquiredSpecialCards(PlayerPrefs.GetString(AcquiredSpecialCardsKey, string.Empty).Split(';'));
            RefreshSpecialCardSources();
            SaveSpecialCardProgress();
        }

        private void SaveSpecialCardProgress(bool flush = true)
        {
            // Voyage progress affects this run immediately, but is committed with the port's
            // resource settlement. Saving XP alone would let a reload restore spent supplies.
            if (editorPreviewBuilding || session == null || session.Phase != CoreDemoPhase.Port) return;
            List<string> ids = new List<string>(session.SpecialCardExperience.Keys);
            ids.Sort(StringComparer.Ordinal);
            List<string> entries = new List<string>();
            foreach (string id in ids) entries.Add(id + "=" + session.GetSpecialCardExperience(id));
            List<string> acquired = new List<string>(session.AcquiredSpecialCardIds);
            acquired.Sort(StringComparer.Ordinal);
            PlayerPrefs.SetString(SpecialCardExperienceKey, string.Join(";", entries));
            PlayerPrefs.SetString(AcquiredSpecialCardsKey, string.Join(";", acquired));
            if (flush) PlayerPrefs.Save();
        }

        private static int SpecialCardIndex(string id)
        {
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == id) return index;
            return -1;
        }

        private void BuildSpecialCardCollection()
        {
            BuildCardWorkshopPreview();
        }

        private void BuildWorkshopSpecialCardSourceAction(RectTransform collection, CoreBattleCard card, int index,
            Vector2 center, float cardHeight)
        {
            CoreSpecialCardDefinition source = CoreSpecialCardCatalog.FindByCardId(card.Id);
            bool joined = false;
            foreach (NavalBattleStage stage in WorkshopBattleStages)
            {
                if (IsDeckBuilderSpecialCardCollection && stage != selectedBattleDeckStage) continue;
                if (BattleDeckCopies(session.GetBonusBattleCards(stage), index) > 0) { joined = true; break; }
            }
            Vector2 position = center - new Vector2(0f, cardHeight * .5f + 22f);
            if (joined || source == null)
            {
                CreateText("Special Card Deck Status " + index, collection, new Vector2(240f, 32f), position,
                    21, FontStyle.Normal, TextAnchor.MiddleCenter, WorkshopPaperInk).text = joined ? "已加入" : "未加入";
                return;
            }
            Button configure = CreateButton("Configure Special Card Source " + index, collection,
                new Vector2(210f, 32f), position, PanelRaised, Ink, 20);
            configure.GetComponentInChildren<Text>().text = source.SourceKind == CoreSpecialCardSourceKind.Crew
                ? "配置船员" : "替换部件";
            CoreVoyageSession owner = session;
            NavalBattleStage shownStage = selectedBattleDeckStage;
            bool fromDeckBuilder = IsDeckBuilderSpecialCardCollection;
            configure.onClick.AddListener(() =>
            {
                if (configure == null || !configure.gameObject.activeInHierarchy || !configure.interactable ||
                    !ReferenceEquals(session, owner) || !specialCardCollectionOpen ||
                    selectedBattleDeckStage != shownStage || IsDeckBuilderSpecialCardCollection != fromDeckBuilder) return;
                selectedSpecialCardIndex = index;
                OpenWorkshopSpecialCardSource(source);
            });
        }
    }
}
