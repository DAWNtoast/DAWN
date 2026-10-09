using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildDepartureDestinationSelection(RectTransform routePanel)
        {
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
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                Button button = routePanel.Find("Departure Destination " + index)?.GetComponent<Button>();
                if (button != null)
                {
                    ApplyDepartureDestinationState(button, CoreVoyageSession.DestinationPorts[index]);
                    button.gameObject.SetActive(true);
                }
            }
        }

        private void ApplyDepartureDestinationState(Button button, string port)
        {
            bool origin = port == session.CurrentPort;
            bool locked = DepartureChartModeActive || session.SelectedTreasureMap != null;
            bool selected = !origin && session.SelectedTreasureMap == null && port == session.SelectedDestinationPort;
            WoodButtonSkin.SetSelected(button, selected);
            button.interactable = !origin && !locked;
            // Keep all six plaques opaque and in their fixed slots. Darken the
            // origin by color rather than fading its frame into the background.
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            ColorBlock colors = button.colors;
            colors.disabledColor = origin ? new Color(.38f, .39f, .40f, 1f)
                : selected ? colors.normalColor : new Color(.52f, .53f, .54f, 1f);
            button.colors = colors;
            if (origin) button.GetComponentInChildren<Text>().color = new Color(.65f, .63f, .59f, 1f);
        }
    }
}
