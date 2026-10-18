using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    // Ship-market UI fixtures share the main fixture's complete preference
    // snapshot. Public port changes below carry the active hull only; these
    // tests do not claim to sail between the six ports or earn the setup money.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private static readonly string[] ShipMarketTestPorts = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipMarket_SixPortsShowTheirStockAndOwnedHullOnlyAndRejectCrossPortPurchaseCallback()
        {
            yield return LoadShipMarketFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            int[][] localStock = { new[] { 0, 4 }, new[] { 5, 6 }, new[] { 2, 7 },
                new[] { 8, 9 }, new[] { 3, 10 }, new[] { 1, 11 } };
            decimal gold = controller.Session.ProfileGold;
            for (int port = 0; port < ShipMarketTestPorts.Length; port++)
            {
                controller.Session.RestoreProfile(gold, ShipMarketTestPorts[port]);
                yield return ClickShipyardControl("Port Hotspot Shipyard");
                var visited = new List<int>();
                int first = VisibleShipMarketVessel();
                for (int step = 0; step <= CoreVoyageSession.Ships.Count; step++)
                {
                    int ship = VisibleShipMarketVessel();
                    if (step > 0 && ship == first) break;
                    Assert.That(visited.Contains(ship), Is.False, "The gallery must not duplicate owned local ships.");
                    visited.Add(ship);
                    yield return ClickShipyardControl("Shipyard Next Vessel");
                }
                CollectionAssert.AreEquivalent(localStock[port].Concat(new[] { 0 }).Distinct(), visited,
                    "Only this port's offerings and the carried starter should be browsable: " + ShipMarketTestPorts[port]);
                Assert.That(VisibleShipMarketVessel(), Is.EqualTo(first), "Next must wrap within this port's stock.");
                yield return ClickShipyardControl("Shipyard Previous Vessel");
                Assert.That(VisibleShipMarketVessel(), Is.EqualTo(visited[visited.Count - 1]));
                yield return BrowseShipMarketTo(localStock[port].Last());
                NavalBattleVisualCapture.Capture(controller, "ShipMarket_Port_" + port + ".png",
                    "分港售船 UI 夹具 · " + ShipMarketTestPorts[port] + "本港船型与已拥有船只", "Shipyard Panel");
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.SelectedShipIndex, Is.Zero, "Browsing cannot change the flagship.");
                yield return ClickShipyardControl("Return To Port Hub");
            }

            controller.Session.RestoreProfile(gold, ShipMarketTestPorts[0]);
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return BrowseShipMarketTo(4);
            yield return ClickShipyardControl("Shipyard Ship 4");
            Button quotedPurchase = ActiveButton("Confirm Shipyard Vessel");
            Assert.That(quotedPurchase.IsInteractable(), Is.True);
            var stalePurchase = quotedPurchase.onClick;
            string inventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey, "");
            controller.Session.RestoreProfile(gold, ShipMarketTestPorts[1]);
            stalePurchase.Invoke(); yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.Session.SelectedShipIndex, Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(4), 0), Is.Zero);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey, ""), Is.EqualTo(inventory));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipMarket_NewHullPurchaseRefitReloadForeignPortSelectionAndDepartureKeepOneOwnedVessel()
        {
            yield return LoadShipMarketFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            const int ship = 4;
            decimal gold = controller.Session.ProfileGold;
            int cost = CoreShipMarketCatalog.PurchaseCost(ship);
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return BrowseShipMarketTo(ship);
            yield return ClickShipyardControl("Shipyard Ship " + ship);
            var originalPurchase = ActiveButton("Confirm Shipyard Vessel").onClick;
            yield return ClickShipyardControl("Confirm Shipyard Vessel");
            originalPurchase.Invoke(); yield return null;
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(ship));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost), "Repeated confirmation charges once.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(ship)), Is.EqualTo(1));
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                Assert.That(controller.InstalledShipComponent(ship, slot), Is.Not.Null);

            // One explicit owned-component fixture tests refitting a new hull;
            // purchasing, installation, persistence and departure use live UI.
            var catalog = new ComponentArtCatalog();
            ComponentArtEntry bow = catalog.Entries.First(item => item.Slot == ComponentSlot.Bow && item.Rarity == ComponentRarity.Rare);
            OwnedShipComponent replacement = controller.GrantOwnedComponent(bow);
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            yield return ClickShipyardControl("Shipyard Equipment Owned " + replacement.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(ship, ComponentSlot.Bow).InstanceId, Is.EqualTo(replacement.InstanceId));
            string[] componentIds = controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray();
            NavalBattleVisualCapture.Capture(controller, "ShipMarket_NewHull_Refitted.png",
                "新船购买与改装 UI 夹具 · 默认部件独立、替换配件保存", "Shipyard Detail");

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(ship));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost));
            Assert.That(controller.InstalledShipComponent(ship, ComponentSlot.Bow).InstanceId, Is.EqualTo(replacement.InstanceId));
            Assert.That(controller.InstalledShipComponent(ship, ComponentSlot.Bow).ComponentId, Is.EqualTo(bow.Id));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Does.Not.Contain("quality"));
            CollectionAssert.AreEqual(componentIds, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray());

            controller.Session.RestoreProfile(controller.Session.ProfileGold, ShipMarketTestPorts[1]);
            Assert.That(CoreShipMarketCatalog.IsSoldAtPort(1, ship), Is.False);
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return AssertShipPortGallery(new[] { 5, 6, ship });
            Assert.That(controller.Session.SelectShip(0), Is.False, "The starter stayed at the departure port.");
            yield return BrowseShipMarketTo(ship);
            yield return ClickShipyardControl("Shipyard Ship " + ship);
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(ship), "An owned foreign hull remains usable.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - cost), "Using a locally berthed owned vessel is free.");
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(ship));
            Assert.That(controller.InstalledShipComponent(ship, ComponentSlot.Bow).InstanceId, Is.EqualTo(replacement.InstanceId));
            Assert.That(GameObject.Find("Ship Label"), Is.Null);
            NavalBattleVisualCapture.Capture(controller, "ShipMarket_NewHull_Departure.png",
                "新船出航 UI 夹具 · 已购外港船可切为旗舰并保留改装", "Navigation Chart");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortShipMarket_LegacyFourOwnedIndicesRetainIdentityWithoutGrantingNewHulls()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(5000);
            for (int index = 1; index < 4; index++) PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(index), 1);
            string[] ids = { "sloop", "merchant", "patrol", "relic" };
            int[] prices = { 0, 800, 1800, 2200 };
            for (int selected = 0; selected < ids.Length; selected++)
            {
                PlayerPrefs.DeleteKey(CoreGameplayDemoController.ShipPortProgressKey);
                PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, selected);
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(selected));
                Assert.That(controller.Session.SelectedShip.Id, Is.EqualTo(ids[selected]));
                Assert.That(CoreShipMarketCatalog.PurchaseCost(selected), Is.EqualTo(prices[selected]));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(5000));
                for (int owned = 1; owned < 4; owned++)
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(owned)), Is.EqualTo(1));
                for (int unowned = 4; unowned < CoreVoyageSession.Ships.Count; unowned++)
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(unowned), 0), Is.Zero);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipMarket_ArrivalAtBrokenAnchorKeepsOnlyLocalAndArrivingHullsOperable() =>
            VerifyLocalOwnedHullsAfterShipMarketArrival(0);

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipMarket_ArrivalAtWhiteShellKeepsOnlyLocalAndArrivingHullsOperable() =>
            VerifyLocalOwnedHullsAfterShipMarketArrival(1);

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipMarket_ArrivalAtCopperBellKeepsOnlyLocalAndArrivingHullsOperable() =>
            VerifyLocalOwnedHullsAfterShipMarketArrival(2);

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipMarket_ArrivalAtSaltCrownKeepsOnlyLocalAndArrivingHullsOperable() =>
            VerifyLocalOwnedHullsAfterShipMarketArrival(3);

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipMarket_ArrivalAtDuskTideKeepsOnlyLocalAndArrivingHullsOperable() =>
            VerifyLocalOwnedHullsAfterShipMarketArrival(4);

        [UnityTest, Timeout(180000)]
        public IEnumerator PortShipMarket_ArrivalAtGreyGullKeepsOnlyLocalAndArrivingHullsOperable() =>
            VerifyLocalOwnedHullsAfterShipMarketArrival(5);

        private static IEnumerator VerifyLocalOwnedHullsAfterShipMarketArrival(int portIndex)
        {
            // Ownership and the cleared pier are explicit fixtures. The normal
            // sail/settlement/port controls exercise arrival; this is not a
            // claim to have bought the fleet or played six complete voyages.
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(5000);
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++)
                PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 1);
            int arrivingShip = portIndex + 6;
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, arrivingShip);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey,
                ShipMarketTestPorts[(portIndex + ShipMarketTestPorts.Length - 1) % ShipMarketTestPorts.Length]);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            Assert.That(session.SelectedShipIndex, Is.EqualTo(arrivingShip));
            string[] fittings = ShipMarketInstalledComponentIds(controller);
            Assert.That(session.SelectDestinationPort(portIndex), Is.True);
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Map.DestinationName, Is.EqualTo(ShipMarketTestPorts[portIndex]));
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
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(session.CurrentPort, Is.EqualTo(ShipMarketTestPorts[portIndex]));
            Assert.That(session.SelectedShipIndex, Is.EqualTo(arrivingShip), "Arrival must keep the voyage flagship.");
            CollectionAssert.AreEqual(fittings, ShipMarketInstalledComponentIds(controller), "Arrival cannot replace fittings.");
            decimal gold = session.ProfileGold;
            int itemCount = controller.OwnedComponents.Count;
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            Assert.That(VisibleShipMarketVessel(), Is.EqualTo(arrivingShip), "The arriving flagship opens in the shipyard.");

            int[] localShips = CoreShipMarketCatalog.ShipsForPort(portIndex).Concat(new[] { arrivingShip }).Distinct().OrderBy(ship => ship).ToArray();
            yield return AssertShipPortGallery(localShips);
            foreach (int remoteShip in Enumerable.Range(0, CoreVoyageSession.Ships.Count).Except(localShips))
                Assert.That(session.SelectShip(remoteShip), Is.False, "Owned remote ships stay at their home port.");

            foreach (int ship in localShips)
            {
                string context = ShipMarketTestPorts[portIndex] + " / " + CoreVoyageSession.Ships[ship].Name;
                yield return BrowseShipMarketTo(ship);
                yield return ClickShipyardControl("Shipyard Ship " + ship);
                Assert.That(GameObject.Find("Shipyard Dossier Name").GetComponent<Text>().text,
                    Is.EqualTo(CoreVoyageSession.Ships[ship].Name), context);
                Assert.That(GameObject.Find("Shipyard Dossier Stat 0"), Is.Not.Null, context);
                yield return ClickShipyardControl("Shipyard Dossier Tab Background");
                Assert.That(GameObject.Find("Shipyard Dossier Story").GetComponent<Text>().text.Replace("\r", string.Empty).Replace("\n", string.Empty),
                    Is.EqualTo(CoreVoyageSession.Ships[ship].Lore.Replace("\r", string.Empty).Replace("\n", string.Empty)), context);
                yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
                Assert.That(GameObject.Find("Shipyard Equipment Page"), Is.Not.Null, context);
                OwnedShipComponent installedBow = controller.InstalledShipComponent(ship, ComponentSlot.Bow);
                if (installedBow == null)
                {
                    Assert.That(GameObject.Find("Shipyard Equipment Slot Bow"), Is.Not.Null, context);
                    Assert.That(controller.InstalledShipComponent(ship, ComponentSlot.Bow), Is.Null, context);
                }
                else
                {
                    string bow = installedBow.InstanceId;
                    yield return ClickShipyardControl("Shipyard Unequip Component Bow");
                    Assert.That(controller.InstalledShipComponent(ship, ComponentSlot.Bow), Is.Null, context);
                    yield return ClickShipyardControl("Shipyard Equipment Owned " + bow);
                    yield return ClickShipyardControl("Shipyard Equip Component");
                    Assert.That(controller.InstalledShipComponent(ship, ComponentSlot.Bow).InstanceId, Is.EqualTo(bow), context);
                }
                yield return ClickShipyardControl("Close Shipyard Equipment");
                Assert.That(GameObject.Find("Shipyard Dossier Story"), Is.Not.Null, "Closing equipment returns to the background page it opened from.");
                yield return ClickShipyardControl("Shipyard Dossier Tab Performance");
                Assert.That(ActiveButton("Confirm Shipyard Vessel").GetComponentInChildren<Text>().text,
                    Is.EqualTo("设为旗舰"), context + " must offer free use, including foreign hulls.");
                yield return ClickShipyardControl("Confirm Shipyard Vessel");
                Assert.That(session.SelectedShipIndex, Is.EqualTo(ship), context);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.SelectedShipKey, -1), Is.EqualTo(ship), context);
                Assert.That(session.ProfileGold, Is.EqualTo(gold), context + " cannot charge for an owned hull or fitting.");
                Assert.That(controller.OwnedComponents.Count, Is.EqualTo(itemCount), context);
                CollectionAssert.AreEqual(fittings, ShipMarketInstalledComponentIds(controller), context);
                yield return ClickShipyardControl("Return To Port Hub");
            }

            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");
            AssertDepartureFlagship(controller, localShips.Last());
            yield return ClickShipyardControl("Inspect Departure Vessel");
            Assert.That(GameObject.Find("Shipyard Equipment Page"), Is.Not.Null);
            yield return ClickShipyardControl("Close Shipyard Equipment");
            AssertDepartureFlagship(controller, localShips.Last());
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(ShipMarketTestPorts[portIndex]));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(localShips.Last()));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(itemCount));
            CollectionAssert.AreEqual(fittings, ShipMarketInstalledComponentIds(controller));
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++)
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 0), Is.EqualTo(1));
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            Assert.That(VisibleShipMarketVessel(), Is.EqualTo(localShips.Last()));
            yield return AssertShipPortGallery(localShips);
            LogAssert.NoUnexpectedReceived();
        }

        private static string[] ShipMarketInstalledComponentIds(CoreGameplayDemoController controller) =>
            Enumerable.Range(0, CoreVoyageSession.Ships.Count)
                .SelectMany(ship => Enum.GetValues(typeof(ComponentSlot)).Cast<ComponentSlot>()
                    .Select(slot => controller.InstalledShipComponent(ship, slot)?.InstanceId)).ToArray();

        private static IEnumerator LoadShipMarketFixture()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(50000);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, ShipMarketTestPorts[0]);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            VoyageCargo cargo = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session.Cargo;
            cargo.Water = cargo.Food = 80;
            cargo.Beer = cargo.Medicine = cargo.Wood = cargo.Iron = cargo.Cloth = cargo.Powder = cargo.Cannonballs = cargo.Gold = 0;
        }

        private static int VisibleShipMarketVessel()
        {
            Button ship = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Shipyard Ship ", StringComparison.Ordinal));
            return int.Parse(ship.name.Substring("Shipyard Ship ".Length));
        }

        private static IEnumerator BrowseShipMarketTo(int ship)
        {
            for (int step = 0; ActiveButton("Shipyard Ship " + ship) == null && step < CoreVoyageSession.Ships.Count; step++)
                yield return ClickShipyardControl("Shipyard Next Vessel");
            Assert.That(ActiveButton("Shipyard Ship " + ship), Is.Not.Null, "Requested vessel is absent from this port's available gallery: " + ship);
        }
    }
}
