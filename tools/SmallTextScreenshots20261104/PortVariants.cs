// Disposable in-memory production UI fixtures; no transaction or persistence callbacks.
using System;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static partial class SmallTextCapture
    {
        private static bool ArrangePort(GameObject host, string variant)
        {
            if (!new[] { "DepartureChart", "DepartureTreasure", "ChartPlaceholder", "MarketChoice",
                "ComponentResaleEmpty", "ShipyardEmpty" }.Contains(variant)) return false;
            if (Application.isPlaying) throw new InvalidOperationException("Stopped editor required.");
            var controller = host.GetComponent<CoreGameplayDemoController>();
            if (controller == null || controller.Session == null)
                throw new InvalidOperationException("A configured gameplay preview is required.");
            bool previous = (bool)Get(controller, "editorPreviewBuilding");
            Field(controller, "editorPreviewBuilding", true);
            try
            {
                switch (variant)
                {
                    case "DepartureChart":
                    case "ChartPlaceholder":
                    case "DepartureTreasure":
                        ArrangePortDeparture(controller, variant == "DepartureTreasure");
                        if (variant == "ChartPlaceholder")
                            RequirePortText(host, "Placeholder", "选择已绘海图，或粘贴完整种子");
                        else RequirePortText(host, "Departure Route Summary", variant == "DepartureTreasure"
                            ? "藏宝海图：选择种子，开启隐藏海域" : "选择当前港口可用的已绘海图");
                        break;
                    case "MarketChoice":
                        ArrangePortMarketChoice(controller);
                        RequirePortText(host, "Market Purchase Choice Quote", "选好位置后付款；取消不扣款。");
                        break;
                    case "ComponentResaleEmpty":
                        Field(controller, "componentInventory", EmptyPortComponentInventory(controller));
                        Field(controller, "componentResaleOpen", true);
                        Field(controller, "componentResalePage", 0);
                        Field(controller, "pendingComponentSaleId", null);
                        Field(controller, "selectedComponentResaleInstanceId", null);
                        Rebuild(controller);
                        RequirePortText(host, "Component Resale Empty Detail", "卸下或航行获得的部件可在此出售。");
                        break;
                    case "ShipyardEmpty":
                        Field(controller, "componentInventory", EmptyPortComponentInventory(controller));
                        Field(controller, "editorPreviewScreen", CoreGameplayPreviewScreen.ShipyardDetail);
                        Field(controller, "editorPreviewShipIndex", 0);
                        Field(controller, "editorPreviewShipPage", 2);
                        Field(controller, "selectedShipyardComponentInstanceId", null);
                        Field(controller, "shipyardInventoryShowAll", true);
                        Field(controller, "shipyardInventoryPage", 0);
                        Rebuild(controller);
                        RequirePortText(host, "Shipyard Equipment Empty Inventory", "到部件商店购买");
                        break;
                }
                foreach (Button button in host.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            }
            finally { Field(controller, "editorPreviewBuilding", previous); }
            return true;
        }

        private static void ArrangePortDeparture(CoreGameplayDemoController controller, bool treasure)
        {
            // The restore API touches this preview's session only, and never consults saved progress.
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 900 });
            controller.Session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            controller.Session.ClearSelectedTreasureMap();
            controller.Session.ClearChartSeedSelection();
            Field(controller, "departureChartOwner", controller.Session);
            Field(controller, "departureChartPort", controller.Session.CurrentPort);
            Field(controller, "departureUseChartSeed", !treasure);
            Field(controller, "departureChartDraft", string.Empty);
            Field(controller, "departureChartValid", false);
            Field(controller, "departureChartFailure", string.Empty);
            Field(controller, "departureDockTreasureMode", treasure);
            Field(controller, "departureCrewConfigurationOpen", false);
            Rebuild(controller);
        }

        private static OwnedShipComponentInventory EmptyPortComponentInventory(CoreGameplayDemoController controller)
        {
            var inventory = new OwnedShipComponentInventory((ComponentArtCatalog)Get(controller, "componentArt"));
            // Stock fittings are installed, leaving the normal spare inventory empty.
            inventory.EnsureLoadout(0);
            return inventory;
        }

        private static void ArrangePortMarketChoice(CoreGameplayDemoController controller)
        {
            Field(controller, "marketSelectedShop", (MarketShopKind?)MarketShopKind.Supplies);
            Field(controller, "marketSelectedItem", (PortMarketItem?)PortMarketItem.Water);
            Field(controller, "marketTradeSelecting", true);
            Field(controller, "marketTradeBuying", true);
            Field(controller, "marketInventoryExpanded", false);
            Call(controller, "RefreshMarketFacilityContents");
            DateTime now = controller.Session.GameDateTime;
            int quantity = (int)Call(controller, "MarketQuantity", PortMarketItem.Water);
            int level = (int)Call(controller, "PortInvestmentLevel", controller.Session.CurrentPort);
            object quote = Call(controller, "CreateMarketPurchaseQuote", PortMarketItem.Water, quantity,
                PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, PortMarketItem.Water,
                    now, quantity, controller.Session.Cargo.Fame), now, level, 0,
                (int)Call(controller, "MarketRemaining", controller.Session.CurrentPort, PortMarketItem.Water, now, level));
            // Build the ordinary choice overlay without invoking a purchase or destination callback.
            Call(controller, "BuildMarketPurchaseChoice", quote);
        }

        private static void RequirePortText(GameObject host, string name, string contains)
        {
            Text match = host.GetComponentsInChildren<Text>(false).FirstOrDefault(text =>
                text.name == name && text.enabled && text.text.Contains(contains));
            if (match == null) throw new InvalidOperationException("Missing visible production text: " + name + " / " + contains);
        }
    }
}
