namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void EnterPortFacility(int facility)
        {
            ResetCachedFacilityNavigation();
            ResetPortFacilityEntryView(facility);
            activePortFacility = facility;
        }

        // A fresh visit starts at the facility's front page. Refreshes and
        // contextual returns deliberately do not pass through this method.
        // Only presentation state is reset; cargo, exhibits and departure
        // selections continue to belong to the current game session.
        private void ResetPortFacilityEntryView(int facility)
        {
            switch (facility)
            {
                case PortFacilityArtCatalog.WarehouseFacilityIndex:
                    warehouseStorageOpen = false;
                    warehouseCatalogOpen = false;
                    warehouseTreasurePage = 0;
                    selectedWarehouseTreasureId = string.Empty;
                    selectedWarehouseDisplaySlot = 0;
                    portWarehouseNotice = string.Empty;
                    InvalidateWarehouseCargoDialog();
                    break;
                case 1:
                    shipyardDetailOpen = false;
                    shipyardReturnToDeparture = false;
                    shipyardDossierTab = ShipyardDossierTab.Performance;
                    selectedShipPreview = session.SelectedShipIndex;
                    ResetShipyardEquipmentSelection();
                    break;
                case PortFacilityArtCatalog.TavernFacilityIndex:
                    ResetTavernDossierView();
                    break;
                case PortFacilityArtCatalog.ComponentShopFacilityIndex:
                    ResetComponentShopView();
                    componentDossierOfferId = null;
                    componentDossierPage = 0;
                    break;
                case 4:
                    ResetCardWorkshopView();
                    break;
                case PortFacilityArtCatalog.TreasureFacilityIndex:
                    treasureFolioOpen = false;
                    treasureFolioView = 0;
                    treasureFolioPage = 0;
                    treasureBoardIndex = 0;
                    break;
                case PortFacilityArtCatalog.MaintenanceFacilityIndex:
                    investmentSupplyPanelOpen = false;
                    break;
                case PortFacilityArtCatalog.VoyageFacilityIndex:
                    departureCrewConfigurationOpen = false;
                    departureDockTreasureMode = false;
                    break;
                case PortFacilityArtCatalog.MarketFacilityIndex:
                    ResetMarketItemDetailView();
                    ResetMarketSceneView();
                    break;
            }
        }
    }
}
