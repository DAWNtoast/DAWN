using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool shipyardReturnToDeparture;

        private void BuildDepartureVesselSelector(RectTransform setupPanel)
        {
            int available = 0;
            for (int index = 0; index < CoreVoyageSession.Ships.Count; index++)
                if (IsShipyardVesselAtCurrentPort(index) && session.CanSelectShip(index)) available++;

            Image hit = CreateImage("Inspect Departure Vessel", setupPanel,
                DepartureLayout.ShipPreviewSize, DepartureLayout.ShipPreviewPosition, Color.clear, circleSprite);
            RectTransform preview = BuildActiveShipPreview("Starter Modular Ship", hit.transform, DepartureLayout.ShipPreviewSize, Vector2.zero);
            // The shared preview factory crops atlas padding before fitting the
            // hull and equipment, so this view must not crop it a second time.
            Button inspect = hit.gameObject.AddComponent<Button>();
            inspect.navigation = new Navigation { mode = Navigation.Mode.None };
            ConfigureGroundedObjectHover(inspect, hit, preview.GetComponentInChildren<RawImage>(), null, Gold);
            inspect.onClick.AddListener(() =>
            {
                if (inspect == null || !inspect.gameObject.activeInHierarchy || !inspect.interactable || !IsDepartureShipView() || departureCrewConfigurationOpen) return;
                CommitDepartureSeed(setupPanel);
                selectedShipPreview = session.SelectedShipIndex;
                shipyardDetailOpen = true;
                shipyardReturnToDeparture = true;
                shipyardDossierTab = ShipyardDossierTab.Equipment;
                ResetShipyardEquipmentSelection();
                activePortFacility = 1;
                RefreshShipyardDossierView();
            });

            CreateDepartureVesselArrow(setupPanel, -1, available > 1);
            CreateDepartureVesselArrow(setupPanel, 1, available > 1);
        }

        private void CreateDepartureVesselArrow(RectTransform panel, int direction, bool enabled)
        {
            Button arrow = CreateButton(direction < 0 ? "Departure Previous Vessel" : "Departure Next Vessel", panel,
                new Vector2(36f, 50f), new Vector2(direction < 0 ? -270f : -35f, 191f), Color.white, Gold, 23);
            arrow.GetComponentInChildren<Text>().text = direction < 0 ? "←" : "→";
            arrow.interactable = enabled;
            arrow.onClick.AddListener(() =>
            {
                if (arrow == null || !arrow.gameObject.activeInHierarchy || !arrow.IsInteractable() ||
                    !Application.isPlaying || editorPreviewBuilding || !IsDepartureShipView() || departureCrewConfigurationOpen) return;
                // Rebuild eligibility at click time: cargo or ownership may have changed
                // since this control was drawn. Browsing never buys an unowned ship.
                for (int step = 1; step < CoreVoyageSession.Ships.Count; step++)
                {
                    int index = (session.SelectedShipIndex + direction * step + CoreVoyageSession.Ships.Count) % CoreVoyageSession.Ships.Count;
                    if (!IsShipyardVesselAtCurrentPort(index) || !session.CanSelectShip(index)) continue;
                    CommitDepartureSeed(panel);
                    EnsureShipComponentLoadout(index);
                    if (!session.SelectShip(index)) return;
                    RefreshComponentBonuses();
                    SaveProgress();
                    if (!TryRefreshDepartureVesselView(panel)) Render();
                    return;
                }
                RefreshDepartureVesselArrows(panel);
            });
        }

        // This path belongs only to vessel arrows. Other departure changes still
        // use Render, so route dialogs, tutorial guards and return navigation keep
        // their existing lifecycle rather than accidentally retaining stale UI.
        private bool TryRefreshDepartureVesselView(RectTransform panel)
        {
            if (!CanRetainPortBody || !UsesDepartureDockScene || tutorialActive ||
                !IsDepartureShipView() || departureCrewConfigurationOpen ||
                departureBasicSupplyOverlay != null ||
                !DepartureChartViewCurrent(panel, departureChartOwner, departureChartRevision)) return false;
            RectTransform routes = FindActiveGuidedContent("Route Selection") as RectTransform;
            RectTransform inspectRoot = panel.Find("Inspect Departure Vessel") as RectTransform;
            RectTransform previous = inspectRoot == null ? null : inspectRoot.Find("Starter Modular Ship") as RectTransform;
            Button inspect = inspectRoot == null ? null : inspectRoot.GetComponent<Button>();
            Image hit = inspectRoot == null ? null : inspectRoot.GetComponent<Image>();
            // Committing an edited chart can rebuild the quote in this same
            // frame. Transform.Find may still return its inactive, retired button
            // until Destroy completes; the binding owns the current visible one.
            Button buy = departureProvisionPanel == panel ? departureProvisionBuy : null;
            if (routes == null || previous == null || inspect == null || hit == null || buy == null ||
                !buy.gameObject.activeInHierarchy || buy.transform.parent != panel) return false;

            // BuildPort normally applies the newly selected ship's crew capacity
            // and professional cards. Do the same before recomputing readiness.
            ApplyDepartureCrewConfiguration();
            // Acquiring or losing the navigator changes which chart controls
            // exist. Let the normal builder handle that uncommon structural case.
            if (session.CanDrawSeaChart != (departureChartInput != null)) return false;
            selectedShipPreview = session.SelectedShipIndex;
            shipyardDetailOpen = false;
            shipyardReturnToDeparture = false;

            // Retire the old child immediately, including within this frame's
            // deferred Destroy window. Preserve the inspect button and callbacks.
            previous.name = "Retired Departure Vessel";
            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
            RectTransform preview = BuildActiveShipPreview("Starter Modular Ship", inspectRoot,
                DepartureLayout.ShipPreviewSize, Vector2.zero);
            RawImage hull = preview.GetComponentInChildren<RawImage>();
            ConfigureGroundedObjectHover(inspect, hit, hull, null, Gold);
            RefreshDepartureOrdinaryCrewControls(panel);
            BindDepartureProvisionPurchase(panel, buy);
            RefreshDepartureVesselArrows(panel);
            // Refreshes text, start readiness, water contact and regional light
            // on the existing scene objects, without rebuilding the chart form.
            RefreshDepartureChartControls();
            UpdatePortHeaderReadouts();
            if (headerStatus != null) headerStatus.text = BuildHeaderStatus();
            return true;
        }

        private void RefreshDepartureVesselArrows(RectTransform panel)
        {
            int available = 0;
            for (int index = 0; index < CoreVoyageSession.Ships.Count; index++)
                if (IsShipyardVesselAtCurrentPort(index) && session.CanSelectShip(index)) available++;
            foreach (string name in new[] { "Departure Previous Vessel", "Departure Next Vessel" })
            {
                Button arrow = panel.Find(name)?.GetComponent<Button>();
                if (arrow != null) arrow.interactable = available > 1;
            }
        }
        private bool IsDepartureShipView() => session != null && session.Phase == CoreDemoPhase.Port && activePortFacility == PortFacilityArtCatalog.VoyageFacilityIndex;

        private void CommitDepartureSeed(RectTransform panel)
        {
            if (panel == null || session == null) return;
            InputField input = departureChartInput;
            if (input != null) departureChartDraft = input.text;
            if (DepartureChartModeActive) ValidateDepartureChartDraft(false);
        }

        private void ReturnFromDepartureDossier()
        {
            shipyardReturnToDeparture = false;
            activePortFacility = PortFacilityArtCatalog.VoyageFacilityIndex;
            if (Application.isPlaying) Render();
            else
            {
                editorPreviewScreen = CoreGameplayPreviewScreen.VoyageDeparture;
                RefreshEditorPreview();
            }
        }
    }
}
