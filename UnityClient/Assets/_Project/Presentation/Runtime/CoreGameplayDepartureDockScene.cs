using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool UsesDepartureDockScene => session != null &&
            PortFacilityArtCatalog.UsesPaintedDepartureScene(session.CurrentPort);

        private void ApplyDeparturePresentation(RectTransform routes, RectTransform setup)
        {
            DeparturePaintedHotspot.RestoreAll(routes);
            DeparturePaintedHotspot.RestoreAll(setup);
            if (setup != null)
                foreach (string name in new[] { "DepartureOrdinaryCrewCount", "DepartureCrewSpeedPreview", "Departure Dock Ship Hint" })
                {
                    Text label = setup.Find(name)?.GetComponent<Text>();
                    if (label != null) label.enabled = true;
                }
            RectTransform ship = setup == null ? null : setup.Find("Inspect Departure Vessel/Starter Modular Ship") as RectTransform;
            if (UsesDepartureDockScene && ship != null) ship.anchoredPosition = Vector2.zero;
            DepartureLayout.Apply(routes, setup);
            DepartureSkin.Apply(routes, setup);
            ApplyDepartureDockScene(routes, setup);
        }

        private void ApplyDepartureDockScene(RectTransform routes, RectTransform setup)
        {
            if (!UsesDepartureDockScene || routes == null || setup == null) return;
            bool crew = setup.Find("Departure Crew Configuration Title") != null;
            bool treasure = DepartureDockTreasureModeActive;
            bool compact = !crew;
            BuildDeparturePaintedReadouts(routes, setup, compact);

            // Keep the original parents and Buttons: all route, vessel and
            // departure callbacks continue to use their existing state guards.
            // The translucent full-screen setup must render behind the chart table.
            if (setup.GetSiblingIndex() > routes.GetSiblingIndex())
                setup.SetSiblingIndex(routes.GetSiblingIndex());

            RawImage sea = routes.Find("Departure Dock Selected Sea")?.GetComponent<RawImage>();
            if (sea == null)
            {
                RectTransform layer = CreateRect("Departure Dock Selected Sea", routes, PhaseSurfaceLayout.ContentSize, Vector2.zero);
                sea = layer.gameObject.AddComponent<DepartureDockChartGraphic>();
                layer.SetAsFirstSibling();
            }
            sea.texture = DepartureDockChartGraphic.LoadChart(session.SelectedRouteIndex);
            sea.raycastTarget = false;
            Text detail = routes.Find("Departure Dock Sea Detail")?.GetComponent<Text>();
            if (detail == null)
                detail = CreateText("Departure Dock Sea Detail", routes, new Vector2(380, 154),
                    new Vector2(205, -5), 18, FontStyle.Normal, TextAnchor.MiddleLeft, DepartureSkin.Ink);
            CoreRouteDefinition selected = CoreVoyageSession.Routes[session.SelectedRouteIndex];
            detail.text = "<size=23>" + selected.Name + "</size>\n" + selected.Theme +
                "\n每海图格 " + selected.NauticalMilesPerMapUnit.ToString("0") + " 海里\n区域首领：" + SpecialWarshipArtCatalog.RouteCandidatesLabel(selected);
            detail.raycastTarget = false;
            if (compact)
            {
                Text summary = routes.Find("Departure Route Summary")?.GetComponent<Text>();
                if (summary != null) summary.text = treasure ? DepartureRouteSummaryText() : DepartureChartModeActive && !departureChartValid
                    ? "选择当前港口可用的已绘海图" : "当前航线：" + session.CurrentPort + " → " + session.SelectedDestinationPort;
                for (int index = 0; index < CoreVoyageSession.Routes.Count; index++)
                {
                    Text label = routes.Find("Route " + index + "/Label")?.GetComponent<Text>();
                    if (label == null) continue;
                    CoreRouteDefinition route = CoreVoyageSession.Routes[index];
                    label.text = route.Name;
                    label.transform.parent.gameObject.SetActive(true);
                }
            }

            for (int index = 0; compact && index < CoreVoyageSession.DestinationPorts.Count; index++)
                routes.Find("Departure Destination " + index)?.gameObject.SetActive(true);

            RectTransform inspect = setup.Find("Inspect Departure Vessel") as RectTransform;
            bool berth = compact && !crew;
            if (berth && inspect != null)
            {
                Text hint = setup.Find("Departure Dock Ship Hint")?.GetComponent<Text>();
                if (hint == null)
                    hint = CreateText("Departure Dock Ship Hint", setup, new Vector2(550, 28),
                        new Vector2(300, 377), 16, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
                hint.text = "点击旗舰 · 查看配件装配";
                hint.raycastTarget = false;
                RectTransform contact = FindActiveGuidedContent("Departure Dock Water Contact") as RectTransform;
                if (contact == null) contact = CreateRect("Departure Dock Water Contact", contentRoot, Vector2.one, Vector2.zero);
                contact.anchoredPosition = new Vector2(DepartureDockSceneLayout.ShipCenterX, DepartureDockSceneLayout.ShipWaterlineY);

                DepartureDockForegroundGraphic foreground = setup.Find("Departure Dock Foreground")?.GetComponent<DepartureDockForegroundGraphic>();
                if (foreground == null)
                {
                    RectTransform layer = CreateRect("Departure Dock Foreground", setup, PhaseSurfaceLayout.ContentSize, Vector2.zero);
                    foreground = layer.gameObject.AddComponent<DepartureDockForegroundGraphic>();
                    foreground.raycastTarget = false;
                }
                foreground.SetTexture(facilityArt.RegionalBackground(PortFacilityArtCatalog.VoyageFacilityIndex, session.CurrentPort));
                foreground.transform.SetSiblingIndex(inspect.GetSiblingIndex() + 1);
            }
            foreach (string name in new[] { "Departure Dock Foreground", "Departure Dock Ship Hint", "Departure Dock Water Blend", "Departure Dock Water Ripple" })
            {
                Transform child = setup.Find(name);
                if (child != null) child.gameObject.SetActive(berth);
            }
            DepartureDockSceneLayout.Apply(routes, setup, DepartureChartModeActive, treasure, crew,
                session.SelectedRouteIndex, session.SelectedShip.Name);
            RefreshDepartureDockChartRecords();
            if (berth && inspect != null)
            {
                RectTransform ship = inspect.Find("Starter Modular Ship") as RectTransform;
                RawImage hull = ship == null ? null : ship.GetComponentInChildren<RawImage>(true);
                DepartureDockShipLayout.Apply(ship, hull, session.SelectedShipIndex, DepartureDockSceneLayout.ShipWaterlineY - inspect.anchoredPosition.y);
                DepartureDockShipLighting.Apply(hull, session.CurrentPort);
                ApplyDepartureDockWaterContact(setup, inspect, hull);
                foreach (string name in new[] { "Setup Title", "Departure Dock Ship Hint", "Departure Dock Ordinary Readout", "Departure Dock Special Readout", "Departure Dock Speed Readout" })
                    setup.Find(name)?.SetAsLastSibling();
            }
            // The scene is textless below the common facility header. Keep its
            // original artwork, labels, clock, wallet and navigation controls.
            RestoreDeparturePaintedHeader();
            Transform header = FindActiveGuidedContent("Port Facility Header");
            if (header != null) PlaceDeparturePaintedHeaderAboveScene(header);
        }

        private void ApplyDepartureDockWaterContact(RectTransform setup, RectTransform inspect, RawImage hull)
        {
            Texture backdrop = facilityArt.RegionalBackground(PortFacilityArtCatalog.VoyageFacilityIndex, session.CurrentPort);
            for (int index = 0; index < 2; index++)
            {
                string name = index == 0 ? "Departure Dock Water Blend" : "Departure Dock Water Ripple";
                DepartureDockWaterContactGraphic water = setup.Find(name)?.GetComponent<DepartureDockWaterContactGraphic>();
                if (water == null)
                {
                    RectTransform layer = CreateRect(name, setup, PhaseSurfaceLayout.ContentSize, Vector2.zero);
                    water = layer.gameObject.AddComponent<DepartureDockWaterContactGraphic>();
                }
                water.gameObject.SetActive(true);
                water.Configure(backdrop, hull, session.SelectedShipIndex, index == 1, session.CurrentPort);
                water.transform.SetSiblingIndex(inspect.GetSiblingIndex() + 1 + index);
            }
        }

        private void BuildDeparturePaintedReadouts(RectTransform routes, RectTransform setup, bool compact)
        {
            Button ordinary = routes.Find("Departure Dock Ordinary Voyage")?.GetComponent<Button>();
            if (ordinary == null)
            {
                ordinary = CreateButton("Departure Dock Ordinary Voyage", routes, Vector2.one, Vector2.zero, Color.clear, Ink, 18);
                ordinary.onClick.AddListener(() =>
                {
                    if (!ordinary.gameObject.activeInHierarchy || !IsDepartureShipView() || departureCrewConfigurationOpen || departureChartToggle == null) return;
                    if (DepartureDockTreasureModeActive)
                    {
                        LeaveDepartureDockTreasureSelection(false);
                        return;
                    }
                    departureChartToggle.isOn = false;
                });
            }
            Button shortcut = routes.Find("Departure Dock Chart Shortcut")?.GetComponent<Button>();
            if (shortcut == null)
            {
                shortcut = CreateButton("Departure Dock Chart Shortcut", routes, Vector2.one, Vector2.zero, Color.clear, Ink, 18);
                shortcut.onClick.AddListener(() =>
                {
                    if (!shortcut.gameObject.activeInHierarchy || !IsDepartureShipView() || departureCrewConfigurationOpen || departureChartToggle == null ||
                        !departureChartToggle.IsInteractable()) return;
                    departureChartToggle.isOn = true;
                    if (departureChartInput != null) departureChartInput.ActivateInputField();
                });
            }
            shortcut.interactable = session.CanDrawSeaChart && !tutorialActive;
            ordinary.gameObject.SetActive(compact);
            shortcut.gameObject.SetActive(compact);

            if (!compact)
            {
                Text pickerLabel = routes.Find("Departure Treasure Seed Picker/Label")?.GetComponent<Text>();
                if (pickerLabel != null)
                    pickerLabel.text = tutorialActive ? "藏宝海图种子 · 教学结束后可选" :
                        session.SelectedTreasureMap != null ? "更换藏宝海图 · " + session.SelectedTreasureMap.Name + " ▼" :
                        "藏宝海图种子 · " + session.TreasureMaps.Count + " 张可用 ▼";
            }

            PaintedReadout(routes, "Departure Dock Treasure Count", session.TreasureMaps.Count + "张", compact);
            for (int index = 0; index < CoreVoyageSession.Routes.Count; index++)
            {
                CoreRouteDefinition route = CoreVoyageSession.Routes[index];
                PaintedReadout(routes, "Departure Dock Route Lock " + index,
                    session.PirateLevel < route.UnlockLevel ? "等级 " + route.UnlockLevel + " 开放" : string.Empty, compact);
            }
            PaintedReadout(setup, "Departure Dock Ordinary Readout", "普通船员 " + selectedDepartureOrdinaryCrew + "/" + session.CrewCountValue, compact);
            PaintedReadout(setup, "Departure Dock Special Readout", "特殊船员 " + selectedDepartureSpecialCrew.Count +
                " · 普通上限 " + Mathf.Max(0, session.CrewCountValue - selectedDepartureSpecialCrew.Count), compact);
            float penalty = session.GetCrewSpeedPenalty(selectedDepartureOrdinaryCrew);
            float bonus = session.GetCrewSpeedBonus(selectedDepartureOrdinaryCrew);
            string effect = penalty > 0 ? "  −" + penalty.ToString("0.0") : bonus > 0 ? "  +" + bonus.ToString("0.0") : string.Empty;
            PaintedReadout(setup, "Departure Dock Speed Readout", "航速 " + session.GetEffectiveSpeedKnots(selectedDepartureOrdinaryCrew).ToString("0.0") + " 节" + effect, compact);
        }

        private void PaintedReadout(RectTransform parent, string name, string value, bool active)
        {
            Text label = parent.Find(name)?.GetComponent<Text>();
            if (label == null) label = CreateText(name, parent, Vector2.one, Vector2.zero, 18, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            label.text = value;
            label.raycastTarget = false;
            label.gameObject.SetActive(active);
        }
    }
}
