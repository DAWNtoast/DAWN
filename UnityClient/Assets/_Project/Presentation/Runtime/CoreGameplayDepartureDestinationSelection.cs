using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildDepartureDestinationSelection(RectTransform routePanel)
        {
            Text routeSummary = CreateText("Departure Route Summary", routePanel,
                new Vector2(1060f, 42f), new Vector2(0f, 364f), 24, FontStyle.Bold,
                TextAnchor.MiddleLeft, Gold);
            routeSummary.text = DepartureRouteSummaryText();
            routeSummary.raycastTarget = false;
            CoreVoyageSession owner = session;
            string origin = session.CurrentPort;
            int revision = departureChartRevision;
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                string port = CoreVoyageSession.DestinationPorts[index];
                Button button = CreateButton("Departure Destination " + index, routePanel,
                    new Vector2(160f, 56f), new Vector2(-440f + index * 176f, 304f),
                    ScenePlaqueColor(0.92f), Gold, 22);
                button.GetComponentInChildren<Text>().text = port;
                ApplyDepartureDestinationState(button, port);
                button.onClick.AddListener(() =>
                {
                    if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable() ||
                        !ReferenceEquals(session, owner) || session.CurrentPort != origin ||
                        revision != departureChartRevision || !IsDepartureShipView() ||
                        DepartureChartModeActive || session.SelectedTreasureMap != null || port == session.CurrentPort) return;
                    if (!session.SelectDestinationPort(port)) return;
                    OnGuidedTutorialDestinationSelected();
                    Render();
                });
            }
        }

        private void RefreshDepartureDestinationSelection()
        {
            Transform routePanel = contentRoot == null ? null : FindActiveGuidedContent("Route Selection");
            if (routePanel == null || session == null) return;
            Text routeSummary = routePanel.Find("Departure Route Summary")?.GetComponent<Text>();
            if (routeSummary != null) routeSummary.text = DepartureRouteSummaryText();
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                Button button = routePanel.Find("Departure Destination " + index)?.GetComponent<Button>();
                if (button != null)
                {
                    ApplyDepartureDestinationState(button, CoreVoyageSession.DestinationPorts[index]);
                }
            }
        }

        private string DepartureRouteSummaryText()
        {
            if (session.SelectedTreasureMap != null)
                return "隐藏海域航线：" + session.CurrentPort + "——隐藏海域（返航：" + session.CurrentPort + "）";
            if (DepartureChartModeActive)
            {
                CoreSeaChartDefinition chart = session.SelectedChartDefinition;
                return departureChartValid && chart != null
                    ? "目标港口由海图确定，当前航线：" + session.CurrentPort + "——" + chart.OtherPort(session.CurrentPort)
                    : "选择已绘海图，当前航线：待确认";
            }
            return "选择目标港口，当前航线：" + session.CurrentPort + "——" + session.SelectedDestinationPort;
        }

        private void ApplyDepartureDestinationState(Button button, string port)
        {
            button.gameObject.SetActive(!DepartureChartModeActive);
            bool origin = port == session.CurrentPort;
            bool locked = DepartureChartModeActive || session.SelectedTreasureMap != null;
            bool selected = !origin && session.SelectedTreasureMap == null && port == session.SelectedDestinationPort;
            WoodButtonSkin.SetSelected(button, selected);
            button.interactable = !origin && !locked;
            // Manual selection restores all six opaque plaques in their fixed
            // slots. A selected chart supplies its own endpoints instead.
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            ColorBlock colors = button.colors;
            colors.disabledColor = origin ? new Color(.38f, .39f, .40f, 1f)
                : selected ? colors.normalColor : new Color(.52f, .53f, .54f, 1f);
            button.colors = colors;
            if (origin) button.GetComponentInChildren<Text>(true).color = new Color(.65f, .63f, .59f, 1f);
        }
    }
}
