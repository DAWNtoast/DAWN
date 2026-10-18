using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipLocation_SalvageVoyageLeavesStarterHomeAndOnlyCarriedShipMovesAfterSwitchAndReload()
        {
            yield return LoadShipMarketFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 0, 4 });
            yield return BrowseShipMarketTo(0);
            yield return ClickShipyardControl("Shipyard Ship 0");
            var retainedStarterSelection = ActiveButton("Confirm Shipyard Vessel").onClick;
            yield return ClickShipyardControl("Return To Port Hub");
            yield return BrowseShipMarketTo(4);
            yield return ClickShipyardControl("Shipyard Ship 4");
            decimal gold = controller.Session.ProfileGold;
            yield return ClickShipyardControl("Confirm Shipyard Vessel");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - CoreShipMarketCatalog.PurchaseCost(4)));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(4));
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Return To Port Hub");

            yield return SailShipPortFixtureTo(controller, 1);
            Assert.That(controller.Session.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(controller.Session.ShipPort(4), Is.EqualTo("白贝港"));
            Assert.That(controller.Session.CanSelectShip(0), Is.False);
            Assert.That(controller.Session.SelectShip(0), Is.False, "A parked ship cannot be summoned from another port.");
            retainedStarterSelection.Invoke();
            yield return null;
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(4), "A retained shipyard callback cannot select a remote hull.");
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 5, 6, 4 });
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(0), 1), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ShipPortProgressKey), Is.Not.Empty);

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.CurrentPort, Is.EqualTo("白贝港"));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(4));
            Assert.That(controller.Session.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(controller.Session.ShipPort(4), Is.EqualTo("白贝港"));
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 5, 6, 4 });
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");
            foreach (string arrow in new[] { "Departure Previous Vessel", "Departure Next Vessel" })
            {
                Assert.That(ActiveButton(arrow).interactable, Is.False, "The starter remains parked at home.");
                ActiveButton(arrow).onClick.Invoke();
            }
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(4));
            yield return ClickShipyardControl("Return To Port Hub");

            yield return SailShipPortFixtureTo(controller, 0);
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 0, 4 });
            yield return BrowseShipMarketTo(0);
            yield return ClickShipyardControl("Shipyard Ship 0");
            decimal goldBeforeSwitch = controller.Session.ProfileGold;
            yield return ClickShipyardControl("Confirm Shipyard Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.Zero);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforeSwitch));
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Return To Port Hub");
            yield return SailShipPortFixtureTo(controller, 1);
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 5, 6, 0 });
            Assert.That(controller.Session.ShipPort(4), Is.EqualTo("断锚港"), "Switching flagship leaves the salvage vessel at the departure port.");
            Assert.That(controller.Session.IsShipAtCurrentPort(4), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortShipLocation_LegacySaveKeepsActiveSalvageHereAndStarterAtItsHome()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(5000);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(4), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 4);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "白贝港");
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ShipPortProgressKey);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(4));
            Assert.That(controller.Session.ShipPort(0), Is.EqualTo("断锚港"));
            Assert.That(controller.Session.ShipPort(4), Is.EqualTo("白贝港"));
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 5, 6, 4 });
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(5000));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipLocation_OwnedHullParkedAbroadDoesNotReappearAsHomeStockAndRemainsUsableOnReturn()
        {
            yield return LoadShipMarketFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return BrowseShipMarketTo(4);
            yield return ClickShipyardControl("Shipyard Ship 4");
            yield return ClickShipyardControl("Confirm Shipyard Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(4));
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Return To Port Hub");
            yield return SailShipPortFixtureTo(controller, 1);

            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return BrowseShipMarketTo(5);
            yield return ClickShipyardControl("Shipyard Ship 5");
            decimal goldBeforePurchase = controller.Session.ProfileGold;
            yield return ClickShipyardControl("Confirm Shipyard Vessel");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforePurchase - CoreShipMarketCatalog.PurchaseCost(5)));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(5));
            string salvageBow = controller.InstalledShipComponent(4, ComponentSlot.Bow).InstanceId;
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Return To Port Hub");
            yield return SailShipPortFixtureTo(controller, 0);

            Assert.That(CoreShipMarketCatalog.IsSoldAtPort(0, 4), Is.True);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(4), 0), Is.EqualTo(1));
            Assert.That(controller.Session.ShipPort(4), Is.EqualTo("白贝港"));
            Assert.That(controller.Session.ShipPort(5), Is.EqualTo("断锚港"));
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 0, 5 });
            Assert.That(controller.Session.SelectShip(4), Is.False, "A home-port listing cannot summon the owned hull parked abroad.");
            Assert.That(controller.TryUnequipShipComponent(4, ComponentSlot.Bow), Is.False,
                "An owned vessel in another port cannot be refitted remotely.");
            Assert.That(controller.InstalledShipComponent(4, ComponentSlot.Bow).InstanceId, Is.EqualTo(salvageBow));
            yield return ClickShipyardControl("Return To Port Hub");

            yield return SailShipPortFixtureTo(controller, 1);
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 5, 6, 4 });
            yield return BrowseShipMarketTo(4);
            yield return ClickShipyardControl("Shipyard Ship 4");
            decimal goldBeforeSwitch = controller.Session.ProfileGold;
            yield return ClickShipyardControl("Confirm Shipyard Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(4));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(goldBeforeSwitch), "Returning to a parked vessel allows a free flagship switch.");
            Assert.That(controller.InstalledShipComponent(4, ComponentSlot.Bow).InstanceId, Is.EqualTo(salvageBow));
            Assert.That(controller.Session.ShipPort(0), Is.EqualTo("断锚港"));
            LogAssert.NoUnexpectedReceived();
        }

        // The cleared destination is an explicit arrival fixture. Departure,
        // settlement, mooring, purchases and selection use the live UI.
        private static IEnumerator SailShipPortFixtureTo(CoreGameplayDemoController controller, int portIndex)
        {
            var session = controller.Session;
            session.Cargo.Water = session.Cargo.Food = 80;
            Assert.That(session.SelectDestinationPort(portIndex), Is.True);
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            SetDestinationTestPosition(session, session.Map.Destination);
            SetDestinationTestField(session, "bossDefeated", true);
            SetDestinationTestField(session, "randomAmbushOccurred", true);
            yield return ClickShipyardControl("Heading East");
            yield return ClickShipyardControl("Execute Sail");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
            Object.FindFirstObjectByType<SettlementRevealController>().CompleteImmediately();
            yield return ClickShipyardControl("Return To Port");
            yield return SchoolUnlockNoticeTestActions.ConfirmAll();
            Assert.That(session.CurrentPort, Is.EqualTo(ShipMarketTestPorts[portIndex]));
        }

        private static IEnumerator AssertShipPortGallery(IEnumerable<int> expected)
        {
            var actual = new List<int>();
            int first = VisibleShipMarketVessel();
            do
            {
                int visible = VisibleShipMarketVessel();
                Assert.That(actual.Contains(visible), Is.False, "The gallery cannot duplicate a local owned hull.");
                actual.Add(visible);
                Assert.That(actual.Count, Is.LessThanOrEqualTo(CoreVoyageSession.Ships.Count));
                yield return ClickShipyardControl("Shipyard Next Vessel");
            } while (VisibleShipMarketVessel() != first);
            CollectionAssert.AreEquivalent(expected, actual);
        }

        // Equipment/visual tests deliberately moor their pre-owned vessels
        // together; owning a hull alone no longer puts it in every port.
        private static void SeedShipPortLocationsForFixture(string port, params int[] ships)
        {
            var progress = new CoreShipPortProgress
            {
                ships = ships.Distinct().Select(ship => new CoreShipPortLocation { shipIndex = ship, port = port }).ToArray()
            };
            PlayerPrefs.SetString(CoreGameplayDemoController.ShipPortProgressKey, JsonUtility.ToJson(progress));
        }
    }
}
