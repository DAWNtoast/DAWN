using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform departureProvisionPanel;
        private Button departureProvisionBuy;
        private int departureProvisionBindingRevision;

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
            BindDepartureProvisionPurchase(panel, buy);
            DepartureLayout.ApplySetup(panel);
            DepartureSkin.ApplySetup(panel);
            ApplyDepartureDockScene(FindActiveGuidedContent("Route Selection") as RectTransform, panel);
        }

        private void BindDepartureProvisionPurchase(RectTransform panel, Button buy)
        {
            buy.interactable = Application.isPlaying && !editorPreviewBuilding && !tutorialActive;
            departureProvisionPanel = panel;
            departureProvisionBuy = buy;
            int revision = ++departureProvisionBindingRevision;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            int ship = session.SelectedShipIndex;
            buy.onClick = new Button.ButtonClickedEvent();
            buy.onClick.AddListener(() =>
            {
                if (!Application.isPlaying || editorPreviewBuilding || tutorialActive || !buy.interactable ||
                    panel == null || !panel.gameObject.activeInHierarchy || panel != departureProvisionPanel ||
                    buy != departureProvisionBuy || revision != departureProvisionBindingRevision || !ReferenceEquals(session, owner) ||
                    !IsDepartureShipView() || departureCrewConfigurationOpen || session.CurrentPort != port ||
                    session.SelectedShipIndex != ship || departureBasicSupplyOverlay != null) return;
                CommitDepartureSeed(panel);
                OpenDepartureBasicSupplies(panel);
            });
        }
    }
}
