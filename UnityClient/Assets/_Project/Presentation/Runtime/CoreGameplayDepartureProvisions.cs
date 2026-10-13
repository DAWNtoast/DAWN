using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform departureProvisionPanel;
        private Button departureProvisionBuy;

        private static int ProvisionShortfall(int target, int existing)
        {
            long missing = System.Math.Max(0L, (long)target - existing);
            return (int)System.Math.Min(int.MaxValue, missing);
        }

        private void BuildDepartureProvisionPurchase(RectTransform panel)
        {
            marketLastObservedMonth = MarketMonth(MarketNow);
            Button buy = CreateButton("Departure Buy Provisions", panel, new Vector2(270f, 58f),
                new Vector2(-141f, -104f), Gold, DeepSea, 18);
            buy.GetComponentInChildren<Text>().text = "购买基础物资";
            buy.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive;
            departureProvisionPanel = panel;
            departureProvisionBuy = buy;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int ship = session.SelectedShipIndex;
            buy.onClick.AddListener(() =>
            {
                if (!Application.isPlaying || editorPreviewBuilding || tutorialActive || !buy.interactable ||
                    panel == null || !panel.gameObject.activeInHierarchy || panel != departureProvisionPanel ||
                    buy != departureProvisionBuy || !ReferenceEquals(session, owner) ||
                    !IsDepartureShipView() || departureCrewConfigurationOpen || session.CurrentPort != port ||
                    session.SelectedShipIndex != ship || departureBasicSupplyOverlay != null) return;
                CommitDepartureSeed(panel);
                OpenDepartureBasicSupplies(panel);
            });
            DepartureLayout.ApplySetup(panel);
            DepartureSkin.ApplySetup(panel);
            ApplyDepartureDockScene(FindActiveGuidedContent("Route Selection") as RectTransform, panel);
        }
    }
}
