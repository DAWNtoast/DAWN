using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator WarehouseGrid_BasicCatalogOrderAndCorrectItemTransfer()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            for (int index = 0; index < PortMarketCatalog.AllItems.Count; index++)
                SetInvestmentCargo(session.Cargo, PortMarketCatalog.AllItems[index], index + 1);
            Assert.That(session.PortWarehouse.Restore("v1;" + session.CurrentPort + ",wood,99;" + session.CurrentPort + ",iron,99"), Is.True);
            string storedBefore = session.PortWarehouse.Serialize();
            decimal goldBefore = session.ProfileGold;
            InvestmentResourceSnapshot cargoBefore = SnapshotInvestmentResources(session);
            yield return OpenPortStorage();
            string savedStorageBefore = PlayerPrefs.GetString(CoreGameplayDemoController.PortWarehouseCargoKey);
            string savedGoldBefore = PlayerPrefs.GetString(CoreGameplayDemoController.ProfileGoldKey);
            string[] canonical = { "water", "food", "beer", "medicine", "wood", "iron", "cloth", "powder", "cannonballs" };
            CollectionAssert.AreEqual(canonical, WarehouseSortedKeys("Warehouse Storage Row "));
            Assert.That(GameObject.Find("Warehouse Storage Quantity wood").GetComponent<Text>().text, Is.EqualTo("99"));
            Button.ButtonClickedEvent staleRow = ActiveButton("Warehouse Storage Row wood").onClick;
            Button.ButtonClickedEvent staleLocation = ActiveButton("Warehouse Inventory Location Stored").onClick;
            ClickAcceptanceButton("Warehouse Inventory Location Carried"); yield return null;
            Assert.That(GameObject.Find("Warehouse Storage Quantity wood").GetComponent<Text>().text, Is.EqualTo("5"));
            staleLocation.Invoke(); yield return null;
            Assert.That(GameObject.Find("Warehouse Storage Quantity wood").GetComponent<Text>().text, Is.EqualTo("5"),
                "A location callback from the previous view must not change the selected inventory.");
            ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;
            Assert.That(GameObject.Find("Warehouse Rare Quantity gem").GetComponent<Text>().text,
                Is.EqualTo(session.Cargo.Gem.ToString(CultureInfo.InvariantCulture)), "Item-category changes preserve the selected location.");
            ClickAcceptanceButton("Warehouse Tab Storage"); yield return null;
            Assert.That(GameObject.Find("Warehouse Storage Quantity wood").GetComponent<Text>().text, Is.EqualTo("5"));
            ClickAcceptanceButton("Warehouse Inventory Location Stored"); yield return null;
            Assert.That(GameObject.Find("Warehouse Storage Quantity wood").GetComponent<Text>().text, Is.EqualTo("99"));
            ClickAcceptanceButton("Warehouse Inventory Location Carried"); yield return null;
            CollectionAssert.AreEqual(canonical, WarehouseSortedKeys("Warehouse Storage Row "),
                "Switching tabs must retain catalog order regardless of stock or carried quantities.");
            staleRow.Invoke(); yield return null;
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Null, "A row from the previous render must not open a new quote.");
            foreach (string header in new[] { "Name", "Stored", "Carried", "Volume" })
                Assert.That(ActiveButton("Warehouse Storage Header " + header), Is.Null);
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(storedBefore));
            Assert.That(session.ProfileGold, Is.EqualTo(goldBefore));
            AssertInvestmentResourcesEqual(session, cargoBefore);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.PortWarehouseCargoKey), Is.EqualTo(savedStorageBefore), "Tab browsing alone must not save cargo.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ProfileGoldKey), Is.EqualTo(savedGoldBefore), "Location selection must not save the player profile.");
            ClickAcceptanceButton("Warehouse Storage Row wood"); yield return null;
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Not.Null);
            ClickAcceptanceButton("Cancel Warehouse Item Actions"); yield return null;
            // After tab reuse the visible wood cell must still transfer only wood.
            yield return StoreAllVisibleCargo(controller, "wood");
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(104));
            Assert.That(session.Cargo.Iron, Is.EqualTo(6));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "iron"), Is.EqualTo(99));
            Assert.That(GameObject.Find("Warehouse Storage Quantity wood").GetComponent<Text>().text, Is.EqualTo("0"),
                "The current ship view must refresh after a confirmed deposit.");
            ClickAcceptanceButton("Warehouse Inventory Location Stored"); yield return null;
            Assert.That(GameObject.Find("Warehouse Storage Quantity wood").GetComponent<Text>().text, Is.EqualTo("104"));
            CollectionAssert.AreEqual(canonical, WarehouseSortedKeys("Warehouse Storage Row "));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WarehouseGrid_RareCatalogOrderAndSingleItemTransfers()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            SetRareWarehouseFixture(session, 1, 4, 1, 2, 3, 10);
            int[] before = RareWarehouseCargo(session);
            string port = session.CurrentPort;
            string otherPort = port == "白贝港" ? "断锚港" : "白贝港";
            Assert.That(session.PortWarehouse.Restore("v2;" + port + ",gem,3;" + port + ",blackpearl,8;" + port +
                ",lantern,8;" + otherPort + ",gem,100"), Is.True);
            string storageBefore = session.PortWarehouse.Serialize();
            decimal goldBefore = session.ProfileGold;
            yield return OpenRareWarehouse();
            string savedStorageBefore = PlayerPrefs.GetString(CoreGameplayDemoController.PortWarehouseCargoKey);
            string savedTreasureBefore = PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey);
            CollectionAssert.AreEqual(RareWarehouseTestKeys, WarehouseSortedKeys("Warehouse Rare Row "));
            Assert.That(GameObject.Find("Warehouse Rare Quantity gem").GetComponent<Text>().text, Is.EqualTo("3"));
            Button.ButtonClickedEvent staleCell = ActiveButton("Warehouse Rare Row ancientmaterial").onClick;
            ClickAcceptanceButton("Warehouse Inventory Location Carried"); yield return null;
            Assert.That(GameObject.Find("Warehouse Rare Quantity gem").GetComponent<Text>().text, Is.EqualTo("1"));
            ClickAcceptanceButton("Warehouse Tab Storage"); yield return null;
            Assert.That(GameObject.Find("Warehouse Storage Quantity wood").GetComponent<Text>().text,
                Is.EqualTo(session.Cargo.Wood.ToString(CultureInfo.InvariantCulture)));
            ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;
            Assert.That(GameObject.Find("Warehouse Rare Quantity ancientmaterial").GetComponent<Text>().text, Is.EqualTo("4"));
            ClickAcceptanceButton("Warehouse Inventory Location Stored"); yield return null;
            Assert.That(GameObject.Find("Warehouse Rare Quantity gem").GetComponent<Text>().text, Is.EqualTo("3"));
            ClickAcceptanceButton("Warehouse Inventory Location Carried"); yield return null;
            CollectionAssert.AreEqual(RareWarehouseTestKeys, WarehouseSortedKeys("Warehouse Rare Row "),
                "Rare items remain in catalog order after revisiting the page.");
            foreach (string header in new[] { "Name", "Stored", "Carried", "Volume" })
                Assert.That(ActiveButton("Warehouse Rare Header " + header), Is.Null);
            Assert.That(ActiveButton("Warehouse Store All Rare"), Is.Null);
            staleCell.Invoke(); yield return null;
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Null);
            CollectionAssert.AreEqual(before, RareWarehouseCargo(session));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(storageBefore));
            Assert.That(session.ProfileGold, Is.EqualTo(goldBefore));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.PortWarehouseCargoKey), Is.EqualTo(savedStorageBefore));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(savedTreasureBefore),
                "Viewing rare cargo in either location must not write progression or cargo saves.");
            yield return OpenRareWarehouseAdjustment("ancientmaterial");
            yield return DragWarehouseSlider(RareWarehouseSlider(), -2);
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(2));
            Assert.That(session.PortWarehouse.GetAmount(port, "ancientmaterial"), Is.EqualTo(2));
            Assert.That(session.Cargo.Gem, Is.EqualTo(1), "A grid cell must not operate on another item.");
            yield return OpenRareWarehouseAdjustment("gem");
            yield return DragWarehouseSlider(RareWarehouseSlider(), RareWarehouseSlider().minValue);
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.Cargo.Gem, Is.Zero);
            Assert.That(GameObject.Find("Warehouse Rare Quantity gem").GetComponent<Text>().text, Is.EqualTo("0"));
            Assert.That(session.PortWarehouse.GetAmount(port, "gem"), Is.EqualTo(4));
            CollectionAssert.AreEqual(new[] { 0, 2, before[2], before[3], before[4], before[5] }, RareWarehouseCargo(session));
            Assert.That(session.PortWarehouse.GetAmount(port, "blackpearl"), Is.EqualTo(8));
            Assert.That(session.PortWarehouse.GetAmount(port, "lantern"), Is.EqualTo(8));
            Assert.That(session.PortWarehouse.GetAmount(otherPort, "gem"), Is.EqualTo(100));
            CollectionAssert.AreEqual(RareWarehouseTestKeys, WarehouseSortedKeys("Warehouse Rare Row "));
            ClickAcceptanceButton("Warehouse Inventory Location Stored"); yield return null;
            Assert.That(GameObject.Find("Warehouse Rare Quantity gem").GetComponent<Text>().text, Is.EqualTo("4"));
            Assert.That(GameObject.Find("Warehouse Rare Quantity ancientmaterial").GetComponent<Text>().text, Is.EqualTo("2"));
            ClickAcceptanceButton("Warehouse Inventory Location Carried"); yield return null;
            Button.ButtonClickedEvent oldPortLocation = ActiveButton("Warehouse Inventory Location Carried").onClick;
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            session.RestoreProfile(session.ProfileGold, otherPort);
            yield return OpenRareWarehouse();
            Assert.That(GameObject.Find("Warehouse Rare Quantity gem").GetComponent<Text>().text, Is.EqualTo("100"),
                "Entering a different port resets the location selection to that port's warehouse.");
            oldPortLocation.Invoke(); yield return null;
            Assert.That(GameObject.Find("Warehouse Rare Quantity gem").GetComponent<Text>().text, Is.EqualTo("100"),
                "A location callback from the previous port must not change the new port's view.");
            LogAssert.NoUnexpectedReceived();
        }

        private static string[] WarehouseSortedKeys(string prefix) =>
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.isActiveAndEnabled && button.name.StartsWith(prefix, StringComparison.Ordinal))
                .OrderByDescending(button => ((RectTransform)button.transform).anchoredPosition.y)
                .ThenBy(button => ((RectTransform)button.transform).anchoredPosition.x)
                .Select(button => button.name.Substring(prefix.Length)).ToArray();
    }
}
