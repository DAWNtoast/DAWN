using System;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private sealed class WorkshopSpecialCardReturnContext
        {
            public CoreVoyageSession Session;
            public string Port;
            public int Facility;
            public NavalBattleStage Stage;
            public int Page;
            public int SelectedIndex;
            public int Chest;
            public bool DeckBuilder;
            public bool DeckOverview;
            public int DepartureFacility;
            public CoreVoyageSession DepartureSession;
            public string DeparturePort;
        }

        private WorkshopSpecialCardReturnContext workshopSpecialCardReturn;

        private bool HasWorkshopSpecialCardReturn => workshopSpecialCardReturn != null &&
            session != null && session.Phase == CoreDemoPhase.Port &&
            ReferenceEquals(workshopSpecialCardReturn.Session, session) &&
            workshopSpecialCardReturn.Port == session.CurrentPort &&
            workshopSpecialCardReturn.Facility == activePortFacility;

        private void OpenWorkshopSpecialCardSource(CoreSpecialCardDefinition source)
        {
            if (!Application.isPlaying || editorPreviewBuilding || source == null || session == null ||
                session.Phase != CoreDemoPhase.Port || activePortFacility != 4 || !specialCardCollectionOpen ||
                !session.AcquiredSpecialCardIds.Contains(source.Card.Id) ||
                cardWorkshopDeckBuilderOpen && !source.Card.IsAllowedInStage(selectedBattleDeckStage)) return;

            int facility = source.SourceKind == CoreSpecialCardSourceKind.Crew
                ? PortFacilityArtCatalog.VoyageFacilityIndex : 1;
            workshopSpecialCardReturn = new WorkshopSpecialCardReturnContext
            {
                Session = session,
                Port = session.CurrentPort,
                Facility = facility,
                Stage = selectedBattleDeckStage,
                Page = specialCardCollectionPage,
                SelectedIndex = selectedSpecialCardIndex,
                Chest = selectedCardWorkshopChest,
                DeckBuilder = cardWorkshopDeckBuilderOpen,
                DeckOverview = battleDeckOverviewOpen,
                // BuildPort clears a departure return when its facility changes.
                // Keep the original route through the workshop for the round trip.
                DepartureFacility = departureReturnFacility,
                DepartureSession = departureReturnSession,
                DeparturePort = departureReturnPort,
            };
            HideDeckBuilderHoverPreview();
            activePortFacility = facility;
            if (source.SourceKind == CoreSpecialCardSourceKind.Crew)
            {
                departureCrewConfigurationOpen = true;
                EnsureDepartureCrewSelection();
                Render();
                return;
            }

            departureCrewConfigurationOpen = false;
            selectedShipPreview = session.SelectedShipIndex;
            shipyardDetailOpen = true;
            shipyardReturnToDeparture = false;
            shipyardDossierTab = ShipyardDossierTab.Equipment;
            ResetShipyardEquipmentSelection();
            ComponentArtEntry entry = componentArt?.GetById(source.SourceId);
            if (entry != null)
            {
                selectedShipyardComponentSlot = entry.Slot;
                shipyardInventoryShowAll = false;
                OwnedShipComponent[] stored = OwnedComponents.Where(item => item.ShipIndex < 0 &&
                        componentArt.GetById(item.ComponentId)?.Slot == entry.Slot)
                    .OrderByDescending(item => (int)componentArt.GetById(item.ComponentId).Rarity)
                    .ThenBy(item => item.ComponentId, StringComparer.Ordinal)
                    .ThenBy(item => item.InstanceId, StringComparer.Ordinal).ToArray();
                int candidate = Array.FindIndex(stored, item => item.ComponentId == source.SourceId);
                if (candidate >= 0)
                {
                    selectedShipyardComponentInstanceId = stored[candidate].InstanceId;
                    shipyardInventoryPage = candidate / ShipyardInventoryPageSize;
                }
            }
            RefreshShipyardDossierView();
        }

        private bool TryReturnToWorkshopSpecialCards()
        {
            if (!HasWorkshopSpecialCardReturn) return false;
            WorkshopSpecialCardReturnContext context = workshopSpecialCardReturn;
            ClearWorkshopSpecialCardReturn();
            departureCrewConfigurationOpen = false;
            shipyardDetailOpen = false;
            shipyardReturnToDeparture = false;
            ResetShipyardEquipmentSelection();
            activePortFacility = 4;
            selectedBattleDeckStage = context.Stage;
            specialCardCollectionPage = context.Page;
            selectedSpecialCardIndex = context.SelectedIndex;
            selectedCardWorkshopChest = context.Chest;
            specialCardCollectionOpen = true;
            cardWorkshopDeckBuilderOpen = context.DeckBuilder;
            battleDeckOverviewOpen = context.DeckOverview;
            battleCardUpgradeOpen = false;
            departureReturnFacility = context.DepartureFacility;
            departureReturnSession = context.DepartureSession;
            departureReturnPort = context.DeparturePort;
            RefreshSpecialCardSources();
            Render();
            return true;
        }

        private void ClearWorkshopSpecialCardReturn() => workshopSpecialCardReturn = null;
    }
}
