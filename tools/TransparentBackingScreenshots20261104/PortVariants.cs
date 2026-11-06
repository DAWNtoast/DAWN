// Disposable Editor PreviewScene fixtures. No transactions, saves or PlayerPrefs writes.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static partial class TransparentBackingCapture
    {
        private static bool ArrangePort(GameObject host, string variant)
        {
            if (variant != "MarketNotice" && variant != "ChartRecords" && variant != "Warehouse" &&
                variant != "WarehouseDetail" && variant != "ComponentResale") return false;
            if (Application.isPlaying) throw new InvalidOperationException("Port captures require a stopped editor.");
            var controller = host.GetComponent<CoreGameplayDemoController>();
            if (controller == null || controller.Session == null)
                throw new InvalidOperationException("Configure a gameplay editor preview before arranging its port sample.");

            switch (variant)
            {
                case "MarketNotice":
                    // Feed a presentation message only; no purchase callback runs.
                    Field(controller, "portNotice", "已购入木板 10 单位，已送入本港仓库。");
                    Call(controller, "RefreshMarketSceneShops");
                    RequirePortImage(host, "Market Trade Notice Backing");
                    break;
                case "ChartRecords":
                    ArrangePortChartRecords(controller);
                    RequirePortImage(host, "Departure Dock Chart Record 0");
                    RequirePortImage(host, "Departure Dock Chart Record 1");
                    break;
                case "Warehouse":
                case "WarehouseDetail":
                    ArrangePortWarehouse(controller, variant == "WarehouseDetail");
                    RequirePortImage(host, "Warehouse Summary Bar");
                    RequirePortImage(host, "Warehouse Collection Card Back 2");
                    RequirePortImage(host, "Warehouse Treasure Details");
                    break;
                case "ComponentResale":
                    ArrangePortComponentResale(controller);
                    RequirePortImage(host, "Component Resale Row transparent-capture-spare-0");
                    break;
            }
            // Preview screenshots never need live transaction listeners.
            foreach (Button button in host.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            return true;
        }

        private static void ArrangePortChartRecords(CoreGameplayDemoController controller)
        {
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 900 });
            controller.Session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            // This production editor method creates one in-memory archived chart and
            // enables the ordinary-chart selection view without reading an archive.
            controller.ConfigureEditorPreviewDepartureChart(1);
            var records = (IList)ReadPortField(controller, "recordedDepartureCharts");
            if (records.Count != 1) throw new InvalidOperationException("Expected the production chart preview's sample record.");
            object first = records[0];
            Type recordType = first.GetType();
            object second = JsonUtility.FromJson(JsonUtility.ToJson(first), recordType);
            int firstRoute = (int)recordType.GetField("routeIndex").GetValue(first);
            int secondRoute = (firstRoute + 1) % CoreVoyageSession.Routes.Count;
            string departure = (string)recordType.GetField("departurePort").GetValue(first);
            string destination = (string)recordType.GetField("destinationPort").GetValue(first);
            recordType.GetField("routeIndex").SetValue(second, secondRoute);
            recordType.GetField("routeName").SetValue(second, CoreVoyageSession.Routes[secondRoute].Name);
            recordType.GetField("seed").SetValue(second,
                CoreVoyageSession.CreateChartSeed(secondRoute, departure, destination));
            records.Add(second);
            Field(controller, "departureCrewConfigurationOpen", false);
            Rebuild(controller);
        }

        private static void ArrangePortWarehouse(CoreGameplayDemoController controller, bool details)
        {
            CoreWarehouseCollection collection = controller.WarehouseCollection;
            // Leave one of the three inventory positions empty so its translucent
            // backing is visible, rather than hidden behind an opaque item button.
            collection.Import(string.Empty, string.Empty);
            collection.Grant(CoreWarehouseCollection.TutorialTreasureId);
            var second = CoreWarehouseCollection.Catalog.First(item => item.Id != CoreWarehouseCollection.TutorialTreasureId);
            collection.Grant(second.Id);
            collection.TryDisplay(CoreWarehouseCollection.TutorialTreasureId, 0);
            Field(controller, "warehouseStorageOpen", false);
            Field(controller, "warehouseCatalogOpen", false);
            Field(controller, "warehouseTreasurePage", 0);
            Field(controller, "selectedWarehouseTreasureId", details ? CoreWarehouseCollection.TutorialTreasureId : string.Empty);
            Rebuild(controller);
            if (!ReferenceEquals(collection, controller.WarehouseCollection) || collection.OwnedIds.Count != 2)
                throw new InvalidOperationException("Warehouse presentation rebuild replaced its in-memory sample collection.");
            if (details && (string)ReadPortField(controller, "selectedWarehouseTreasureId") != CoreWarehouseCollection.TutorialTreasureId)
                throw new InvalidOperationException("Warehouse presentation rebuild lost its selected sample treasure.");
        }

        private static void ArrangePortComponentResale(CoreGameplayDemoController controller)
        {
            var catalog = (ComponentArtCatalog)ReadPortField(controller, "componentArt");
            int serial = 0;
            var inventory = new OwnedShipComponentInventory(catalog, () => "transparent-capture-spare-" + serial++);
            foreach (ComponentArtEntry entry in catalog.Entries.Take(5)) inventory.Grant(entry);
            Field(controller, "componentInventory", inventory);
            Field(controller, "componentResaleOpen", true);
            Field(controller, "componentResalePage", 0);
            Field(controller, "pendingComponentSaleId", null);
            Field(controller, "selectedComponentResaleInstanceId", null);
            Rebuild(controller);
        }

        private static object ReadPortField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            return field.GetValue(target);
        }

        private static void RequirePortImage(GameObject host, string name)
        {
            Image image = host.GetComponentsInChildren<Image>(true).FirstOrDefault(value =>
                value.name == name && value.enabled && value.gameObject.activeInHierarchy);
            if (image == null || image.sprite != null || image.overrideSprite != null || image.color.a <= 0f || image.color.a >= 1f)
                throw new InvalidOperationException("Expected an active, plain translucent Image: " + name);
        }
    }
}
