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
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator DepartureShips_ArrowsWrapOnlyOwnedVesselsAndImmediatelySetTheFlagshipWithoutPurchasing()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(9000);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(2), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(3), 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            decimal gold = controller.Session.ProfileGold;
            string inventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
            yield return ClickShipyardControl("Port Hotspot Voyage");

            foreach (int expectedShip in new[] { 2, 3, 0 })
            {
                yield return ClickShipyardControl("Departure Next Vessel");
                AssertDepartureFlagship(controller, expectedShip);
            }
            foreach (int expectedShip in new[] { 3, 2, 0 })
            {
                yield return ClickShipyardControl("Departure Previous Vessel");
                AssertDepartureFlagship(controller, expectedShip);
            }

            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Cycling owned vessels cannot charge a purchase fee.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(1), 0), Is.Zero,
                "An affordable but unowned ship must never enter the departure cycle.");
            Assert.That(controller.OwnedComponents.Any(item => item.ShipIndex == 1), Is.False);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(inventory));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureShips_SingleOwnedVesselDisablesBothArrowsAndRejectsForcedCallbacks()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(9000);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            decimal gold = controller.Session.ProfileGold;
            string inventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);

            foreach (string name in new[] { "Departure Previous Vessel", "Departure Next Vessel" })
            {
                Button arrow = ActiveButton(name);
                Assert.That(arrow, Is.Not.Null, name);
                Assert.That(arrow.interactable, Is.False, name);
                arrow.onClick.Invoke();
                yield return null;
                Assert.That(controller.Session.SelectedShipIndex, Is.Zero);
            }

            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(inventory));
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++)
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 0), Is.Zero);
            yield return ClickShipyardControl("Inspect Departure Vessel");
            Assert.That(GameObject.Find("Shipyard Dossier Name").GetComponent<Text>().text, Is.EqualTo(CoreVoyageSession.Ships[0].Name));
            yield return ClickShipyardControl("Back To Voyage Departure");
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(ActiveButton("Departure Previous Vessel").interactable, Is.False);
            Assert.That(ActiveButton("Departure Next Vessel").interactable, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureShips_CargoCapacityFiltersOwnedVesselsAndIsRecheckedByArrowCallbacks()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 1);
            // Merchant (150) and salvage (185) straddle the second boundary;
            // relic (145) and the starter (80) cannot carry the initial load.
            foreach (int ship in new[] { 1, 3, 4 })
                PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            VoyageCargo cargo = controller.Session.Cargo;
            float initialTarget = (CoreVoyageSession.Ships[3].CargoCapacity + CoreVoyageSession.Ships[1].CargoCapacity) * .5f;
            cargo.Wood = Mathf.CeilToInt(initialTarget - (cargo.UsedVolume - cargo.Wood));
            Assert.That(cargo.UsedVolume, Is.GreaterThan(CoreVoyageSession.Ships[3].CargoCapacity));
            Assert.That(cargo.UsedVolume, Is.LessThanOrEqualTo(CoreVoyageSession.Ships[1].CargoCapacity));
            float initialVolume = cargo.UsedVolume;
            decimal gold = controller.Session.ProfileGold;
            Assert.That(controller.Session.CanSelectShip(0), Is.False);
            Assert.That(controller.Session.CanSelectShip(1), Is.True);
            Assert.That(controller.Session.CanSelectShip(3), Is.False);
            Assert.That(controller.Session.CanSelectShip(4), Is.True);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(2), 0), Is.Zero);
            yield return ClickShipyardControl("Port Hotspot Voyage");

            yield return ClickShipyardControl("Departure Next Vessel");
            AssertDepartureFlagship(controller, 4);
            yield return ClickShipyardControl("Departure Next Vessel");
            AssertDepartureFlagship(controller, 1);
            yield return ClickShipyardControl("Departure Previous Vessel");
            AssertDepartureFlagship(controller, 4);
            yield return ClickShipyardControl("Departure Previous Vessel");
            AssertDepartureFlagship(controller, 1);
            yield return ClickShipyardControl("Departure Next Vessel");
            AssertDepartureFlagship(controller, 4);
            Assert.That(cargo.UsedVolume, Is.EqualTo(initialVolume));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));

            Button previouslyEnabledNext = ActiveButton("Departure Next Vessel");
            Assert.That(previouslyEnabledNext.IsInteractable(), Is.True);
            float largerTarget = (CoreVoyageSession.Ships[1].CargoCapacity + CoreVoyageSession.Ships[4].CargoCapacity) * .5f;
            cargo.Wood = Mathf.CeilToInt(largerTarget - (cargo.UsedVolume - cargo.Wood));
            Assert.That(cargo.UsedVolume, Is.GreaterThan(CoreVoyageSession.Ships[1].CargoCapacity));
            Assert.That(cargo.UsedVolume, Is.LessThanOrEqualTo(CoreVoyageSession.Ships[4].CargoCapacity));
            Assert.That(controller.Session.CanSelectShip(0), Is.False);
            Assert.That(controller.Session.CanSelectShip(1), Is.False);
            Assert.That(controller.Session.CanSelectShip(2), Is.False);
            Assert.That(controller.Session.CanSelectShip(3), Is.False);
            Assert.That(controller.Session.CanSelectShip(4), Is.True);
            float volume = cargo.UsedVolume;
            previouslyEnabledNext.onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(4), "The stale callback must reject the now-overloaded merchant and keep the salvage flagship.");
            yield return ClickShipyardControl("Route 0");
            Assert.That(ActiveButton("Departure Previous Vessel").interactable, Is.False);
            Assert.That(ActiveButton("Departure Next Vessel").interactable, Is.False);
            ActiveButton("Departure Previous Vessel").onClick.Invoke();
            ActiveButton("Departure Next Vessel").onClick.Invoke();
            yield return null;
            AssertDepartureFlagship(controller, 4);
            Assert.That(cargo.UsedVolume, Is.EqualTo(volume));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureShips_DossierRefitsSelectedVesselAndReturnsWithRouteSeedDecksBeforeSailing()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            // High-route equipment navigation is an explicit UI fixture; these
            // experience points are not presented as a played leveling voyage.
            var progress = controller.Session.CaptureTreasureProgress();
            progress.pirateExperience = 1500;
            controller.Session.RestoreTreasureProgress(progress);
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry rareBow = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Bow && entry.Rarity == ComponentRarity.Rare);
            OwnedShipComponent replacement = controller.GrantOwnedComponent(rareBow, 0.94f);
            string otherShipBow = controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId;
            string originalBow = controller.InstalledShipComponent(1, ComponentSlot.Bow).InstanceId;
            int itemCount = controller.OwnedComponents.Count;
            decimal gold = controller.Session.ProfileGold;
            Dictionary<NavalBattleStage, int[]> decks = new Dictionary<NavalBattleStage, int[]>();
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                decks[stage] = controller.Session.GetBattleDeck(stage).Reverse().ToArray();
                Assert.That(controller.Session.SetBattleDeck(stage, decks[stage]), Is.True);
            }

            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Route 2");
            string arrowSeed = CoreVoyageSession.CreateChartSeed(2, controller.Session.CurrentPort, "白贝港");
            yield return EnterDepartureChartSeed(arrowSeed, false);
            yield return ClickShipyardControl("Departure Next Vessel");
            AssertDepartureFlagship(controller, 1);
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(arrowSeed),
                "Switching vessels must commit pending input before rebuilding the departure UI.");
            string seed = CoreVoyageSession.CreateChartSeed(2, controller.Session.CurrentPort, "白贝港");
            yield return EnterDepartureChartSeed(seed, false);
            yield return ClickShipyardControl("Inspect Departure Vessel");
            Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
            Assert.That(GameObject.Find("Shipyard Dossier Name").GetComponent<Text>().text, Is.EqualTo(CoreVoyageSession.Ships[1].Name));
            Assert.That(GameObject.Find("Shipyard Equipment Page"), Is.Not.Null, "Departure opens the selected vessel's fittings directly.");
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            yield return ClickShipyardControl("Shipyard Dossier Tab Performance");
            Assert.That(GameObject.Find("Shipyard Dossier Stat 0"), Is.Not.Null);
            yield return ClickShipyardControl("Shipyard Dossier Tab Background");
            Assert.That(GameObject.Find("Shipyard Dossier Story"), Is.Not.Null);
            Assert.That(GameObject.Find("Shipyard Dossier Stat 0"), Is.Null);
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.False);
            yield return ClickShipyardControl("Shipyard Equipment Owned " + replacement.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(1, ComponentSlot.Bow).InstanceId, Is.EqualTo(replacement.InstanceId));
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == originalBow).ShipIndex, Is.EqualTo(-1));
            AssertShipyardTactic(controller, rareBow, true);
            NavalBattleVisualCapture.Capture(controller, "86_Departure_Vessel_Equipment.png", "从出航打开当前旗舰档案并更换船角，底部可直接返回出航码头", "Shipyard Detail");
            yield return ClickShipyardControl("Back To Voyage Departure");
            Assert.That(FindRenderedShipLayer("Voyage Setup", rareBow.ShipLayer), Is.Null,
                "Non-initial flagships retain their original artwork after refitting, without component overlays.");
            RawImage[] departureShipImages = GameObject.Find("Starter Modular Ship").GetComponentsInChildren<RawImage>();
            Assert.That(departureShipImages.Length, Is.EqualTo(1), "Only the original hull is drawn for a non-initial flagship.");
            Assert.That(departureShipImages[0].texture, Is.EqualTo(new PortArtCatalog().ShipTexture(1)));
            Assert.That(departureShipImages[0].isActiveAndEnabled, Is.True);
            Assert.That(departureShipImages[0].color.a, Is.GreaterThan(0f));
            Assert.That(GameObject.Find("Upgrade Name 0").GetComponent<Text>().text, Is.EqualTo(rareBow.Name));
            Assert.That(GameObject.Find("Upgrade Part 0").GetComponent<RawImage>().texture, Is.EqualTo(rareBow.Texture));
            yield return ClickShipyardControl("Inspect Departure Vessel");
            yield return ClickShipyardControl("Shipyard Unequip Component Bow");
            Assert.That(controller.InstalledShipComponent(1, ComponentSlot.Bow), Is.Null);
            Assert.That(controller.Session.MaxHull, Is.EqualTo(CoreVoyageSession.Ships[1].MaxHull - catalog.Get(0).HullBonus));
            AssertShipyardTactic(controller, rareBow, false);
            yield return ClickShipyardControl("Back To Voyage Departure");
            Assert.That(GameObject.Find("Upgrade Name 0").GetComponent<Text>().text, Is.EqualTo("空槽"));
            Assert.That(GameObject.Find("Upgrade Part 0"), Is.Null, "An unloaded slot must not show a fallback component illustration.");
            yield return ClickShipyardControl("Inspect Departure Vessel");
            yield return ClickShipyardControl("Shipyard Equipment Owned " + replacement.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            int expectedHull = CoreVoyageSession.Ships[1].MaxHull + rareBow.HullBonus - catalog.Get(0).HullBonus;
            Assert.That(controller.Session.MaxHull, Is.EqualTo(expectedHull));
            yield return ClickShipyardControl("Shipyard Dossier Tab Performance");
            Assert.That(GameObject.Find("Shipyard Dossier Stat 0").GetComponent<Text>().text, Is.EqualTo("船体上限  " + expectedHull));
            yield return ClickShipyardControl("Back To Voyage Departure");

            // Leaving the dossier through the shared header must clear its return origin too.
            yield return ClickShipyardControl("Inspect Departure Vessel");
            Assert.That(ActiveButton("Back To Voyage Departure"), Is.Not.Null);
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return ClickShipyardControl("Shipyard Ship 1");
            Assert.That(ActiveButton("Back To Voyage Departure"), Is.Null);
            yield return ClickShipyardControl("Back To Shipyard Gallery");
            Assert.That(ActiveButton("Shipyard Ship 1"), Is.Not.Null);
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");

            AssertDepartureFlagship(controller, 1);
            Assert.That(GameObject.Find("Shipyard Detail"), Is.Null);
            Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(2));
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(GameObject.Find("Seed Input").GetComponent<InputField>().text, Is.EqualTo(seed));
            Assert.That(GameObject.Find("Ship Stats").GetComponent<Text>().text, Does.Contain("船体 " + expectedHull));
            Assert.That(GameObject.Find("Upgrade Name 0").GetComponent<Text>().text, Is.EqualTo(rareBow.Name));
            Assert.That(GameObject.Find("Upgrade Part 0").GetComponent<RawImage>().texture, Is.EqualTo(rareBow.Texture));
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(otherShipBow));
            Assert.That(controller.InstalledShipComponent(1, ComponentSlot.Bow).Quality, Is.EqualTo(0.94f).Within(0.0001f));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(itemCount));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                CollectionAssert.AreEqual(decks[stage], controller.Session.GetBattleDeck(stage), stage.ToString());
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage)), Is.EqualTo(string.Join(",", decks[stage])));
            }
            AssertShipyardTactic(controller, rareBow, true);
            NavalBattleVisualCapture.Capture(controller, "85_Departure_Selected_Vessel.png", "出航切船与档案配装返回后保留航线、种子和牌组", "Voyage Setup");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(controller.Session.MaxHull, Is.EqualTo(expectedHull));
            Assert.That(controller.Session.Hull, Is.EqualTo(expectedHull));
            Assert.That(controller.Session.CargoCapacityValue, Is.EqualTo(CoreVoyageSession.Ships[1].CargoCapacity));
            Assert.That(controller.Session.Map.Route, Is.SameAs(CoreVoyageSession.Routes[2]));
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                CollectionAssert.AreEqual(decks[stage], controller.Session.GetBattleDeck(stage), stage.ToString());
            AssertShipyardTactic(controller, rareBow, true);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertDepartureFlagship(CoreGameplayDemoController controller, int expectedShip)
        {
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(expectedShip), "Departure arrows must change the actual flagship.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.SelectedShipKey, -1), Is.EqualTo(expectedShip));
            Assert.That(GameObject.Find("Departure Ship Schematic Caption").GetComponent<Text>().text, Does.Contain(CoreVoyageSession.Ships[expectedShip].Name));
            Assert.That(GameObject.Find("Ship Stats").GetComponent<Text>().text, Does.Contain("货舱 " + CoreVoyageSession.Ships[expectedShip].CargoCapacity.ToString("0")));
        }
    }
}
