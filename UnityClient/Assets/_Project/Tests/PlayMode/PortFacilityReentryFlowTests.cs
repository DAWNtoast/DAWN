using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator PortFacilityReentry_WarehouseStartsAtCollectionWithoutChangingCargoOrExhibits()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            for (int index = 0; index < 4; index++)
                controller.WarehouseCollection.Grant(CoreWarehouseCollection.Catalog[index].Id);
            string displayedId = CoreWarehouseCollection.Catalog[0].Id;
            controller.WarehouseCollection.TryDisplay(displayedId, 5);
            controller.Session.Cargo.Wood = 3;
            controller.Session.Cargo.Gem = 2;
            int[] cargo = WarehouseCargoSnapshot(controller);
            decimal gold = controller.Session.ProfileGold;
            string[] owned = controller.WarehouseCollection.OwnedIds.ToArray();
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;

            string[] subpages = { "Warehouse Tab Storage", "Warehouse Tab Rare", "Warehouse Collection Catalog", "Warehouse Treasure Page Next" };
            string[] visibleMarkers = { "Warehouse Storage Row wood", "Warehouse Rare Row gem", "Warehouse Collection Catalog Panel", "Warehouse Treasure Page" };
            for (int exit = 0; exit < 2; exit++)
            {
                for (int page = 0; page < subpages.Length; page++)
                {
                    AssertWarehouseReentryFrontPage();
                    ClickAcceptanceButton(subpages[page]); yield return null;
                    if (page == 2) { ClickAcceptanceButton("Warehouse Catalog Page Next"); yield return null; }
                    if (page == 3)
                    {
                        ClickAcceptanceButton("Warehouse Treasure " + CoreWarehouseCollection.Catalog[3].Id); yield return null;
                        ClickAcceptanceButton("Warehouse Display Slot 4"); yield return null;
                    }
                    Assert.That(GameObject.Find(visibleMarkers[page]), Is.Not.Null);
                    GameObject panel = GameObject.Find("Warehouse Panel");
                    string pageLabel = page < 2 ? null : GameObject.Find(page == 2 ? "Warehouse Catalog Page" : "Warehouse Treasure Page").GetComponent<Text>().text;
                    RefreshFacilityForReentryTest(controller); yield return null;
                    Assert.That(GameObject.Find(visibleMarkers[page]), Is.Not.Null,
                        "Refreshing the current visit must keep the player's open tab or catalog.");
                    Assert.That(GameObject.Find("Warehouse Panel"), Is.SameAs(panel));
                    if (pageLabel != null)
                        Assert.That(GameObject.Find(page == 2 ? "Warehouse Catalog Page" : "Warehouse Treasure Page").GetComponent<Text>().text, Is.EqualTo(pageLabel));

                    yield return ReenterFacilityForTest("Warehouse", exit == 0);
                    AssertWarehouseReentryFrontPage();
                    CollectionAssert.AreEqual(owned, controller.WarehouseCollection.OwnedIds);
                    Assert.That(controller.WarehouseCollection.DisplayedAt(5), Is.EqualTo(displayedId));
                    CollectionAssert.AreEqual(cargo, WarehouseCargoSnapshot(controller));
                    Assert.That(controller.Session.Cargo.Gem, Is.EqualTo(2));
                    Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                }
            }
            // A previous rare-goods visit must not make the basic-goods action reopen rares.
            ClickAcceptanceButton("Warehouse Tab Storage"); yield return null;
            Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.Not.Null);
            Assert.That(ActiveButton("Warehouse Rare Row gem"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortFacilityReentry_TreasureTableClosesFolioButRefreshKeepsItsCurrentPage()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var progress = controller.Session.CaptureTreasureProgress();
            progress.maps = Enumerable.Range(0, 3).Select(index => new CoreTreasureMapSave
            {
                id = "reentry-map-" + index, seed = "REENTRY-MAP-" + index, rankId = "weathered"
            }).ToArray();
            progress.selectedMapId = string.Empty;
            controller.Session.RestoreTreasureProgress(progress);
            string savedProgress = JsonUtility.ToJson(controller.Session.CaptureTreasureProgress());
            ClickAcceptanceButton("Port Hotspot Treasure"); yield return null;
            for (int exit = 0; exit < 2; exit++)
            {
                Assert.That(GameObject.Find("Treasure Folio Overlay"), Is.Null);
                ClickAcceptanceButton("Treasure Open Folio"); yield return null;
                Assert.That(GameObject.Find("Treasure Folio Title").GetComponent<Text>().text, Is.EqualTo("藏图册"));
                ClickAcceptanceButton("Treasure Browse Unused"); yield return null;
                ClickAcceptanceButton("Treasure Folio Next Page"); yield return null;
                Assert.That(GameObject.Find("Treasure Folio Page").GetComponent<Text>().text, Is.EqualTo("2 / 2"));
                RefreshFacilityForReentryTest(controller); yield return null;
                Assert.That(GameObject.Find("Treasure Folio Title").GetComponent<Text>().text, Is.EqualTo("未使用的藏宝图"));
                Assert.That(GameObject.Find("Treasure Folio Page").GetComponent<Text>().text, Is.EqualTo("2 / 2"));

                yield return ReenterFacilityForTest("Treasure", exit == 0);
                Assert.That(GameObject.Find("Treasure Folio Overlay"), Is.Null,
                    "A new visit opens the treasure worktable instead of the previous folio page.");
                Assert.That(ActiveButton("Treasure Open Folio"), Is.Not.Null);
                Assert.That(JsonUtility.ToJson(controller.Session.CaptureTreasureProgress()), Is.EqualTo(savedProgress));
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortFacilityReentry_DepartureClosesSubpagesAndKeepsVoyageChoicesAndDetourOrigin()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return SelectDepartureReturnFixture(controller);
            ClickAcceptanceButton("DepartureOrdinaryCrewDecreaseButton"); yield return null;
            controller.Session.Cargo.Wood = 3;
            int route = controller.Session.SelectedRouteIndex;
            string destination = controller.Session.SelectedDestinationPort;
            int ordinaryCrew = controller.Session.SelectedOrdinaryCrew;
            int specialCrew = controller.Session.SelectedSpecialCrewCount;
            string seed = controller.Session.SelectedSeed;
            Button back = ActiveButton("Return To Port Hub");

            ClickAcceptanceButton("Departure Treasure Seed Picker"); yield return null;
            Assert.That(GameObject.Find("Departure Dock Treasure Selection"), Is.Not.Null);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            RefreshFacilityForReentryTest(controller); yield return null;
            Assert.That(GameObject.Find("Departure Dock Treasure Selection"), Is.Not.Null);
            ClickAcceptanceButton("Adjust Voyage Supplies"); yield return null;
            AssertDepartureReturnCaption(back, "返回码头");
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(GameObject.Find("Port Hub"), Is.Null);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(route));
            Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(destination));
            Assert.That(GameObject.Find("Departure Dock Treasure Selection"), Is.Not.Null,
                "A contextual return from the market continues the existing departure visit.");
            AssertDepartureReturnCaption(back, "返回港口");

            yield return ReenterFacilityForTest("Voyage", true);
            Assert.That(GameObject.Find("Departure Dock Treasure Selection"), Is.Null,
                "Browsing treasure mode without choosing a map is temporary presentation state.");
            AssertDepartureReturnSelection(controller, route, destination);
            ClickAcceptanceButton("Configure Voyage Crew"); yield return null;
            Assert.That(GameObject.Find("Departure Crew Configuration"), Is.Not.Null);
            RefreshFacilityForReentryTest(controller); yield return null;
            Assert.That(GameObject.Find("Departure Crew Configuration"), Is.Not.Null,
                "Refreshing departure must not close the crew configuration being edited.");
            GameObject crewConfiguration = GameObject.Find("Departure Crew Configuration");
            Button blockedNavigation = ActiveButton("Next Port Facility");
            Assert.That(blockedNavigation.IsInteractable(), Is.False, "Close the modal crew window before leaving departure.");
            blockedNavigation.onClick.Invoke(); yield return null;
            Assert.That(GameObject.Find("Departure Crew Configuration"), Is.SameAs(crewConfiguration),
                "A retained navigation callback cannot bypass the modal guard.");
            ClickAcceptanceButton("Back To Voyage Setup"); yield return null;
            Assert.That(GameObject.Find("Departure Crew Configuration"), Is.Null);
            yield return ReenterFacilityForTest("Voyage", false);
            Assert.That(GameObject.Find("Departure Crew Configuration"), Is.Null);
            Assert.That(GameObject.Find("Departure Dock Treasure Selection"), Is.Null);
            AssertDepartureReturnSelection(controller, route, destination);
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(ordinaryCrew));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(specialCrew));
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(3));

            // An explicitly selected treasure map is voyage configuration, not an open subpage.
            var progress = controller.Session.CaptureTreasureProgress();
            progress.maps = new[] { new CoreTreasureMapSave { id = "reentry-selected", seed = "REENTRY-SELECTED", rankId = "weathered" } };
            progress.selectedMapId = "reentry-selected";
            controller.Session.RestoreTreasureProgress(progress);
            yield return ReenterFacilityForTest("Voyage", true);
            Assert.That(controller.Session.SelectedTreasureMap.Id, Is.EqualTo("reentry-selected"));
            Assert.That(GameObject.Find("Departure Dock Treasure Selection"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortFacilityReentry_InvestmentWarehouseOpensBasicStorageAndReturnsToInvestmentBeforeNormalVisit()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            for (int index = 0; index < 4; index++)
                controller.WarehouseCollection.Grant(CoreWarehouseCollection.Catalog[index].Id);
            controller.Session.Cargo.Wood = 3;
            controller.Session.Cargo.Gem = 2;
            string stock = controller.Session.PortWarehouse.Serialize();
            decimal gold = controller.Session.ProfileGold;
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            Button back = ActiveButton("Return To Port Hub");
            foreach (string staleSubpage in new[] { "Warehouse Tab Rare", "Warehouse Collection Catalog" })
            {
                AssertWarehouseReentryFrontPage();
                ClickAcceptanceButton(staleSubpage); yield return null;
                Assert.That(GameObject.Find(staleSubpage == "Warehouse Tab Rare" ?
                    "Warehouse Rare Row gem" : "Warehouse Collection Catalog Panel"), Is.Not.Null);
                // Keep the previous subpage open when invoking the bound exit callback.
                back.onClick.Invoke(); yield return null;
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                ClickAcceptanceButton("Port Hotspot Maintenance"); yield return null;
                Assert.That(GameObject.Find("Port Investment Panel"), Is.Not.Null);
                ClickAcceptanceButton("Open Investment Warehouse"); yield return null;
                Assert.That(controller.ActivePortSection, Is.EqualTo("船长收藏室"));
                Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.Not.Null,
                    "The investment shortcut deliberately opens basic cargo rather than the collection front page.");
                Assert.That(ActiveButton("Warehouse Rare Row gem"), Is.Null);
                Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.Null,
                    "A previous catalog visit must not hide the requested basic-cargo shortcut.");
                Assert.That(GameObject.Find("Warehouse Collection Count"), Is.Null);
                AssertDepartureReturnCaption(back, "返回港口投资建设");

                ClickAcceptanceButton("Return To Port Hub"); yield return null;
                Assert.That(GameObject.Find("Port Investment Panel"), Is.Not.Null);
                Assert.That(GameObject.Find("Warehouse Panel"), Is.Null);
                Assert.That(GameObject.Find("Port Hub"), Is.Null);
                AssertDepartureReturnCaption(back, "返回港口");
                ClickAcceptanceButton("Return To Port Hub"); yield return null;
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
                AssertWarehouseReentryFrontPage();
                AssertDepartureReturnCaption(back, "返回港口");
                Assert.That(controller.Session.PortWarehouse.Serialize(), Is.EqualTo(stock));
                Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(3));
                Assert.That(controller.Session.Cargo.Gem, Is.EqualTo(2));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            }
            LogAssert.NoUnexpectedReceived();
        }
        private static void AssertWarehouseReentryFrontPage()
        {
            Assert.That(GameObject.Find("Warehouse Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Warehouse Collection Count"), Is.Not.Null);
            Assert.That(GameObject.Find("Warehouse Collection Catalog Panel"), Is.Null);
            Assert.That(ActiveButton("Warehouse Storage Row wood"), Is.Null);
            Assert.That(ActiveButton("Warehouse Rare Row gem"), Is.Null);
            Assert.That(GameObject.Find("Warehouse Treasure Title").GetComponent<Text>().text, Is.EqualTo("选择藏品查看来历"));
            Assert.That(GameObject.Find("Warehouse Treasure Page").GetComponent<Text>().text, Is.EqualTo("1 / 2"));
            Assert.That(ActiveButton("Warehouse Display Selected").IsInteractable(), Is.False);
        }

        private static void RefreshFacilityForReentryTest(CoreGameplayDemoController controller)
        {
            MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(render, Is.Not.Null);
            render.Invoke(controller, null);
        }

        private static IEnumerator ReenterFacilityForTest(string facilityKey, bool throughHub)
        {
            // Exercise real navigation while nonmodal folios are open. The crew
            // modal must first be closed through its own visible control.
            string departureAction = throughHub ? "Return To Port Hub" : "Next Port Facility";
            Button navigation = ActiveButton(departureAction);
            Assert.That(navigation, Is.Not.Null);
            Assert.That(navigation.IsInteractable(), Is.True);
            navigation.onClick.Invoke(); yield return null;
            if (throughHub)
            {
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                ClickAcceptanceButton("Port Hotspot " + facilityKey); yield return null;
            }
            else
            {
                ClickAcceptanceButton("Previous Port Facility"); yield return null;
            }
        }
    }
}
