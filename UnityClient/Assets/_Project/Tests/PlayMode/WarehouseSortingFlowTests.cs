using System;
using System.Collections;
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
        public IEnumerator WarehouseSorting_BasicStableOrderAndCorrectItemTransfer()
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
            string[] canonical = { "water", "food", "beer", "medicine", "wood", "iron", "cloth", "powder", "cannonballs" };
            CollectionAssert.AreEqual(canonical, WarehouseSortedKeys("Warehouse Storage Row "));
            Button.ButtonClickedEvent staleRow = ActiveButton("Warehouse Storage Row wood").onClick;
            ClickAcceptanceButton("Warehouse Storage Header Stored"); yield return null;
            CollectionAssert.AreEqual(new[] { "wood", "iron", "water", "food", "beer", "medicine", "cloth", "powder", "cannonballs" },
                WarehouseSortedKeys("Warehouse Storage Row "));
            staleRow.Invoke(); yield return null;
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Null, "A row from the previous render must not open a new quote.");
            Button.ButtonClickedEvent staleHeader = ActiveButton("Warehouse Storage Header Stored").onClick;
            ClickAcceptanceButton("Warehouse Storage Header Stored"); yield return null;
            string[] ascending = WarehouseSortedKeys("Warehouse Storage Row ");
            Assert.That(ascending[7], Is.EqualTo("wood"));
            Assert.That(ascending[8], Is.EqualTo("iron"));
            staleHeader.Invoke(); yield return null;
            CollectionAssert.AreEqual(ascending, WarehouseSortedKeys("Warehouse Storage Row "), "A previous render's header must not toggle the new view.");
            string[] occupiedVolume = { "iron", "cannonballs", "cloth", "wood", "powder", "beer", "food", "medicine", "water" };
            ClickAcceptanceButton("Warehouse Storage Header Volume"); yield return null;
            CollectionAssert.AreEqual(occupiedVolume, WarehouseSortedKeys("Warehouse Storage Row "));
            ClickAcceptanceButton("Warehouse Storage Header Volume"); yield return null;
            CollectionAssert.AreEqual(occupiedVolume.Reverse(), WarehouseSortedKeys("Warehouse Storage Row "));
            ClickAcceptanceButton("Warehouse Storage Header Carried"); yield return null;
            CollectionAssert.AreEqual(canonical.Reverse(), WarehouseSortedKeys("Warehouse Storage Row "));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(storedBefore));
            Assert.That(session.ProfileGold, Is.EqualTo(goldBefore));
            AssertInvestmentResourcesEqual(session, cargoBefore);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.PortWarehouseCargoKey), Is.EqualTo(savedStorageBefore), "Sorting alone must not save cargo.");
            // A currently visible header cannot dismiss or invalidate an open quote.
            Button.ButtonClickedEvent headerDuringQuote = ActiveButton("Warehouse Storage Header Name").onClick;
            ClickAcceptanceButton("Warehouse Storage Row wood"); yield return null;
            headerDuringQuote.Invoke(); yield return null;
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Not.Null);
            ClickAcceptanceButton("Cancel Warehouse Item Actions"); yield return null;
            // Row position changed, but the visible wood button must still transfer wood.
            yield return StoreAllVisibleCargo(controller, "wood");
            Assert.That(session.Cargo.Wood, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "wood"), Is.EqualTo(104));
            Assert.That(session.Cargo.Iron, Is.EqualTo(6));
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "iron"), Is.EqualTo(99));
            ClickAcceptanceButton("Warehouse Storage Header Name"); yield return null;
            CollectionAssert.AreEqual(canonical, WarehouseSortedKeys("Warehouse Storage Row "));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WarehouseSorting_RareUsesOccupiedVolumeAndPreservesStoreAll()
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
            int[] storedBefore = RareWarehouseTestKeys.Select(key => session.PortWarehouse.GetAmount(port, key)).ToArray();
            string storageBefore = session.PortWarehouse.Serialize();
            decimal goldBefore = session.ProfileGold;
            yield return OpenRareWarehouse();
            Button.ButtonClickedEvent staleAll = ActiveButton("Warehouse Store All Rare").onClick;
            ClickAcceptanceButton("Warehouse Rare Header Stored"); yield return null;
            CollectionAssert.AreEqual(new[] { "blackpearl", "lantern", "gem", "ancientmaterial", "relic", "ancientcompass" },
                WarehouseSortedKeys("Warehouse Rare Row "));
            ClickAcceptanceButton("Warehouse Rare Header Carried"); yield return null;
            CollectionAssert.AreEqual(new[] { "lantern", "ancientmaterial", "ancientcompass", "blackpearl", "gem", "relic" },
                WarehouseSortedKeys("Warehouse Rare Row "));
            ClickAcceptanceButton("Warehouse Rare Header Volume"); yield return null;
            CollectionAssert.AreEqual(new[] { "lantern", "ancientcompass", "blackpearl", "ancientmaterial", "gem", "relic" },
                WarehouseSortedKeys("Warehouse Rare Row "), "Sort actual total volume, not item quantity or unit size; keep ties in catalog order.");
            ClickAcceptanceButton("Warehouse Rare Header Volume"); yield return null;
            CollectionAssert.AreEqual(new[] { "gem", "relic", "ancientmaterial", "blackpearl", "ancientcompass", "lantern" },
                WarehouseSortedKeys("Warehouse Rare Row "), "Ascending volume still preserves catalog order for ties.");
            staleAll.Invoke(); yield return null;
            CollectionAssert.AreEqual(before, RareWarehouseCargo(session));
            Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(storageBefore));
            Assert.That(session.ProfileGold, Is.EqualTo(goldBefore));
            yield return OpenRareWarehouseAdjustment("ancientmaterial");
            yield return DragWarehouseSlider(RareWarehouseSlider(), -2);
            ClickAcceptanceButton("Confirm Warehouse Cargo Adjustment"); yield return null;
            Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(2));
            Assert.That(session.PortWarehouse.GetAmount(port, "ancientmaterial"), Is.EqualTo(2));
            Assert.That(session.Cargo.Gem, Is.EqualTo(1), "The reordered row must not operate on another item.");
            ClickAcceptanceButton("Warehouse Store All Rare"); yield return null;
            CollectionAssert.AreEqual(new int[6], RareWarehouseCargo(session));
            for (int index = 0; index < RareWarehouseTestKeys.Length; index++)
                Assert.That(session.PortWarehouse.GetAmount(port, RareWarehouseTestKeys[index]), Is.EqualTo(storedBefore[index] + before[index]));
            Assert.That(session.PortWarehouse.GetAmount(otherPort, "gem"), Is.EqualTo(100));
            ClickAcceptanceButton("Warehouse Rare Header Name"); yield return null;
            CollectionAssert.AreEqual(RareWarehouseTestKeys, WarehouseSortedKeys("Warehouse Rare Row "));
            LogAssert.NoUnexpectedReceived();
        }

        private static string[] WarehouseSortedKeys(string prefix) =>
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.isActiveAndEnabled && button.name.StartsWith(prefix, StringComparison.Ordinal))
                .OrderByDescending(button => ((RectTransform)button.transform).anchoredPosition.y)
                .Select(button => button.name.Substring(prefix.Length)).ToArray();
    }
}
