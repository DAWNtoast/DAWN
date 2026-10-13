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
        public IEnumerator WarehouseRetainedView_SortingTabsAndTransfersKeepInventoryObjects()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.Cargo.Wood = 3;
            controller.Session.Cargo.Gem = 1;
            yield return OpenPortStorage();
            GameObject panel = GameObject.Find("Warehouse Panel");
            GameObject cabinet = GameObject.Find("Warehouse Cabinet Art");
            Button wood = ActiveButton("Warehouse Storage Row wood");
            Button sort = ActiveButton("Warehouse Storage Header Stored");
            Button.ButtonClickedEvent oldWoodAction = wood.onClick;
            ClickAcceptanceButton("Warehouse Storage Header Stored"); yield return null;
            Assert.That(GameObject.Find("Warehouse Panel"), Is.SameAs(panel));
            Assert.That(GameObject.Find("Warehouse Cabinet Art"), Is.SameAs(cabinet));
            Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.SameAs(wood));
            Assert.That(ActiveButton("Warehouse Storage Header Stored"), Is.SameAs(sort));
            oldWoodAction.Invoke(); yield return null;
            Assert.That(GameObject.Find("Warehouse Item Actions"), Is.Null,
                "Reusing a row must not revive its superseded callback.");

            ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;
            Button gem = ActiveButton("Warehouse Rare Row gem");
            Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.Null);
            int[] warmedObjects = WarehouseRetainedObjectIds(panel);
            for (int pass = 0; pass < 2; pass++)
            {
                ClickAcceptanceButton("Warehouse Tab Storage"); yield return null;
                Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.SameAs(wood));
                ClickAcceptanceButton("Warehouse Tab Valuables"); yield return null;
                Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.Null);
                Assert.That(ActiveButton("Warehouse Rare Row gem"), Is.Null);
                ClickAcceptanceButton("Warehouse Tab Rare"); yield return null;
                Assert.That(ActiveButton("Warehouse Rare Row gem"), Is.SameAs(gem));
                CollectionAssert.AreEqual(warmedObjects, WarehouseRetainedObjectIds(panel),
                    "Revisiting cached tabs must not instantiate another hierarchy.");
            }

            ClickAcceptanceButton("Warehouse Tab Storage"); yield return null;
            yield return StoreAllVisibleCargo(controller, "wood");
            Assert.That(GameObject.Find("Warehouse Panel"), Is.SameAs(panel));
            Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.SameAs(wood));
            Assert.That(GameObject.Find("Warehouse Storage Carried wood").GetComponent<Text>().text, Is.EqualTo("0"));
            Assert.That(GameObject.Find("Warehouse Storage Stored wood").GetComponent<Text>().text, Is.EqualTo("3"));
            Assert.That(GameObject.Find("Warehouse Storage Modal"), Is.Null);
            Assert.That(panel.GetComponent<CanvasGroup>().interactable, Is.True);
            Assert.That(panel.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            CollectionAssert.AreEqual(warmedObjects, WarehouseRetainedObjectIds(panel));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator WarehouseRetainedView_CollectionPagingAndDisplayReuseCardsAndRejectOldCallbacks()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string id = CoreWarehouseCollection.Catalog[0].Id;
            for (int index = 0; index < 4; index++)
                controller.WarehouseCollection.Grant(CoreWarehouseCollection.Catalog[index].Id);
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            GameObject panel = GameObject.Find("Warehouse Panel");
            GameObject slot = GameObject.Find("Warehouse Display Slot 0");
            Button treasure = ActiveButton("Warehouse Treasure " + id);
            ClickAcceptanceButton("Warehouse Treasure " + id); yield return null;
            Assert.That(ActiveButton("Warehouse Treasure " + id), Is.SameAs(treasure));
            Button.ButtonClickedEvent oldDisplay = ActiveButton("Warehouse Display Selected").onClick;
            ClickAcceptanceButton("Warehouse Treasure Page Next"); yield return null;
            Assert.That(ActiveButton("Warehouse Treasure " + id), Is.Null);
            oldDisplay.Invoke(); yield return null;
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.Empty);
            int[] warmedObjects = WarehouseRetainedObjectIds(panel);
            ClickAcceptanceButton("Warehouse Treasure Page Previous"); yield return null;
            Assert.That(ActiveButton("Warehouse Treasure " + id), Is.SameAs(treasure));
            ClickAcceptanceButton("Warehouse Treasure Page Next"); yield return null;
            CollectionAssert.AreEqual(warmedObjects, WarehouseRetainedObjectIds(panel));
            ClickAcceptanceButton("Warehouse Treasure Page Previous"); yield return null;
            ClickAcceptanceButton("Warehouse Display Selected"); yield return null;
            Assert.That(GameObject.Find("Warehouse Panel"), Is.SameAs(panel));
            Assert.That(GameObject.Find("Warehouse Display Slot 0"), Is.SameAs(slot));
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(id));
            GameObject displayedArt = GameObject.Find("Warehouse Displayed Art 0");
            Assert.That(displayedArt, Is.Not.Null);
            ClickAcceptanceButton("Warehouse Remove Selected"); yield return null;
            Assert.That(GameObject.Find("Warehouse Displayed Art 0"), Is.Null);
            ClickAcceptanceButton("Warehouse Display Selected"); yield return null;
            Assert.That(GameObject.Find("Warehouse Displayed Art 0"), Is.SameAs(displayedArt));

            // Warm the separate catalog once, then revisit every page without
            // replacing either its rows or the hidden local collection hierarchy.
            ClickAcceptanceButton("Warehouse Remove Selected"); yield return null;
            Button.ButtonClickedEvent retiredDisplay = ActiveButton("Warehouse Display Selected").onClick;
            Button.ButtonClickedEvent retiredLocalNext = ActiveButton("Warehouse Treasure Page Next").onClick;
            string localPage = GameObject.Find("Warehouse Treasure Page").GetComponent<Text>().text;
            string[] ownedBeforeCatalog = controller.WarehouseCollection.OwnedIds.ToArray();
            ClickAcceptanceButton("Warehouse Collection Catalog"); yield return null;
            GameObject catalogPanel = GameObject.Find("Warehouse Collection Catalog Panel");
            Assert.That(catalogPanel, Is.Not.Null);
            Button.ButtonClickedEvent retiredCatalogNext = ActiveButton("Warehouse Catalog Page Next").onClick;
            Button.ButtonClickedEvent retiredCatalogReturn = ActiveButton("Warehouse Collection Catalog").onClick;
            var catalogRows = new GameObject[5][];
            GameObject[] VisibleCatalogRows() => catalogPanel.GetComponentsInChildren<Transform>()
                .Where(item => item.parent == catalogPanel.transform && item.name.StartsWith("Warehouse Catalog Row "))
                .OrderBy(item => item.name).Select(item => item.gameObject).ToArray();
            for (int page = 0; page < catalogRows.Length; page++)
            {
                Assert.That(GameObject.Find("Warehouse Panel"), Is.SameAs(panel));
                Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.SameAs(catalogPanel));
                Assert.That(GameObject.Find("Warehouse Catalog Page").GetComponent<Text>().text, Is.EqualTo((page + 1) + " / 5"));
                catalogRows[page] = VisibleCatalogRows();
                Assert.That(catalogRows[page].Length, Is.EqualTo(8));
                if (page < catalogRows.Length - 1)
                {
                    ClickAcceptanceButton("Warehouse Catalog Page Next"); yield return null;
                }
            }
            Assert.That(ActiveButton("Warehouse Catalog Page Next").IsInteractable(), Is.False);
            int[] warmedCatalogObjects = WarehouseRetainedObjectIds(panel);
            ClickAcceptanceButton("Warehouse Collection Catalog"); yield return null;
            Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.Null);
            Assert.That(GameObject.Find("Warehouse Treasure Page").GetComponent<Text>().text, Is.EqualTo(localPage));
            Assert.That(GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text, Is.EqualTo("选择藏品查看来历"));
            Assert.That(ActiveButton("Warehouse Display Selected").IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Warehouse Display Slot 0"), Is.SameAs(slot));
            Assert.That(ActiveButton("Warehouse Treasure " + id), Is.SameAs(treasure));

            // A fresh valid selection makes a stale display action observable:
            // it must not display that selection or move the local page.
            ClickAcceptanceButton("Warehouse Treasure " + id); yield return null;
            string selectedTitle = GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text;
            retiredDisplay.Invoke(); retiredLocalNext.Invoke();
            retiredCatalogNext.Invoke(); retiredCatalogReturn.Invoke(); yield return null;
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.Empty);
            Assert.That(GameObject.Find("Warehouse Treasure Page").GetComponent<Text>().text, Is.EqualTo(localPage));
            Assert.That(GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text, Is.EqualTo(selectedTitle));
            Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.Null);
            ClickAcceptanceButton("Warehouse Display Selected"); yield return null;
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(id));
            Assert.That(GameObject.Find("Warehouse Displayed Art 0"), Is.SameAs(displayedArt));

            for (int pass = 0; pass < 2; pass++)
            {
                ClickAcceptanceButton("Warehouse Collection Catalog"); yield return null;
                retiredCatalogNext.Invoke(); retiredCatalogReturn.Invoke(); yield return null;
                Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.SameAs(catalogPanel),
                    "An old catalog return must not close a newly bound catalog.");
                Assert.That(GameObject.Find("Warehouse Catalog Page").GetComponent<Text>().text, Is.EqualTo("1 / 5"));
                Assert.That(GameObject.Find("Warehouse Display Cabinet"), Is.Null);
                foreach (string localDetail in new[] { "Warehouse Treasure Title", "Warehouse Treasure Source",
                    "Warehouse Treasure Story", "Warehouse Display Selected", "Warehouse Remove Selected" })
                    Assert.That(GameObject.Find(localDetail), Is.Null, "Catalog browsing must hide local display actions: " + localDetail);
                for (int page = 0; page < catalogRows.Length; page++)
                {
                    Assert.That(GameObject.Find("Warehouse Catalog Page").GetComponent<Text>().text, Is.EqualTo((page + 1) + " / 5"));
                    CollectionAssert.AreEqual(catalogRows[page], VisibleCatalogRows(), "Each catalog page must reuse its cached rows.");
                    CollectionAssert.AreEqual(warmedCatalogObjects, WarehouseRetainedObjectIds(panel),
                        "Revisiting a warmed catalog must not instantiate another hierarchy.");
                    if (page < catalogRows.Length - 1)
                    {
                        ClickAcceptanceButton("Warehouse Catalog Page Next"); yield return null;
                    }
                }
                ClickAcceptanceButton("Warehouse Collection Catalog"); yield return null;
                Assert.That(GameObject.Find("Warehouse Panel"), Is.SameAs(panel));
                Assert.That(GameObject.Find("Warehouse Display Slot 0"), Is.SameAs(slot));
                Assert.That(ActiveButton("Warehouse Treasure " + id), Is.SameAs(treasure));
                Assert.That(GameObject.Find("Warehouse Displayed Art 0"), Is.SameAs(displayedArt));
                Assert.That(GameObject.Find("Warehouse Treasure Page").GetComponent<Text>().text, Is.EqualTo(localPage));
                Assert.That(GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text, Is.EqualTo("选择藏品查看来历"));
                Assert.That(ActiveButton("Warehouse Display Selected").IsInteractable(), Is.False);
                Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(id));
                CollectionAssert.AreEqual(ownedBeforeCatalog, controller.WarehouseCollection.OwnedIds);
                CollectionAssert.AreEqual(warmedCatalogObjects, WarehouseRetainedObjectIds(panel));
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static int[] WarehouseRetainedObjectIds(GameObject panel) => panel.GetComponentsInChildren<Transform>(true)
            .Select(item => item.gameObject.GetInstanceID()).OrderBy(id => id).ToArray();
    }
}
