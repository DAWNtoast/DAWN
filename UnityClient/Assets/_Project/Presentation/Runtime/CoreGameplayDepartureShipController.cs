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
                if (OwnsShipyardVessel(index) && session.CanSelectShip(index)) available++;

            Text caption = CreateText("Departure Ship Schematic Caption", setupPanel,
                new Vector2(200f, 24f), new Vector2(-180f, 121f), 13, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            caption.text = session.SelectedShip.Name + " · 当前旗舰";
            Text hint = CreateText("Departure Vessel Hint", setupPanel,
                new Vector2(200f, 18f), new Vector2(-180f, 99f), 12, FontStyle.Normal, TextAnchor.MiddleCenter, Gold);
            hint.text = "选择船身更换配件 · 可用 " + available + " 艘";
            hint.raycastTarget = caption.raycastTarget = false;

            Image hit = CreateImage("Inspect Departure Vessel", setupPanel,
                new Vector2(186f, 112f), new Vector2(-153f, 191f), Color.clear, circleSprite);
            RectTransform preview = BuildActiveShipPreview("Starter Modular Ship", hit.transform, new Vector2(180f, 110f), Vector2.zero);
            // The shared preview factory crops atlas padding before fitting the
            // hull and equipment, so this view must not crop it a second time.
            Button inspect = hit.gameObject.AddComponent<Button>();
            inspect.navigation = new Navigation { mode = Navigation.Mode.None };
            ConfigureGroundedObjectHover(inspect, hit, preview.GetComponentInChildren<RawImage>(), hint, Gold);
            inspect.onClick.AddListener(() =>
            {
                if (inspect == null || !inspect.gameObject.activeInHierarchy || !inspect.interactable || !IsDepartureShipView()) return;
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
                if (arrow == null || !arrow.gameObject.activeInHierarchy || !arrow.interactable ||
                    !Application.isPlaying || editorPreviewBuilding || !IsDepartureShipView()) return;
                // Rebuild eligibility at click time: cargo or ownership may have changed
                // since this control was drawn. Browsing never buys an unowned ship.
                for (int step = 1; step < CoreVoyageSession.Ships.Count; step++)
                {
                    int index = (session.SelectedShipIndex + direction * step + CoreVoyageSession.Ships.Count) % CoreVoyageSession.Ships.Count;
                    if (!OwnsShipyardVessel(index) || !session.CanSelectShip(index)) continue;
                    CommitDepartureSeed(panel);
                    EnsureShipComponentLoadout(index);
                    if (!session.SelectShip(index)) return;
                    RefreshComponentBonuses();
                    SaveProgress();
                    Render();
                    return;
                }
            });
        }

        private bool IsDepartureShipView() => session != null && session.Phase == CoreDemoPhase.Port && activePortFacility == 8;

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
            activePortFacility = 8;
            if (Application.isPlaying) Render();
            else
            {
                editorPreviewScreen = CoreGameplayPreviewScreen.VoyageDeparture;
                RefreshEditorPreview();
            }
        }
    }
}
