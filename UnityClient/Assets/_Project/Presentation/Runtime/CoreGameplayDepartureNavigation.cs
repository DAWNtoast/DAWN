using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private int departureReturnFacility = -1;
        private CoreVoyageSession departureReturnSession;
        private string departureReturnPort;

        private bool HasDepartureReturn => departureReturnFacility == activePortFacility &&
            session != null && session.Phase == CoreDemoPhase.Port &&
            ReferenceEquals(departureReturnSession, session) && departureReturnPort == session.CurrentPort;

        private string FacilityReturnCaption => HasInvestmentReturn ? "返回港口投资建设" :
            activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex ? (HasDepartureReturn ? "返回码头" : "返回港口") :
            HasWorkshopSpecialCardReturn ? "返回特殊卡牌" :
            IsShipyardDossierOpen ? (shipyardReturnToDeparture ? "返回出航码头" : "返回船坞") :
            IsTavernDossierOpen ? "返回酒馆" :
            IsComponentDossierOpen ? "返回部件陈列" :
            HasDepartureReturn ? "返回出航界面" : "返回港口";

        private void OpenFacilityFromDeparture(int facility, RectTransform setupPanel)
        {
            CommitDepartureSeed(setupPanel);
            ResetPortFacilityEntryView(facility);
            departureReturnFacility = facility;
            departureReturnSession = session;
            departureReturnPort = session.CurrentPort;
            activePortFacility = facility;
        }

        private void ClearDepartureReturn()
        {
            departureReturnFacility = -1;
            departureReturnSession = null;
            departureReturnPort = null;
        }
    }
}


