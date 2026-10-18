using System;
using System.Collections;
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
        [UnityTest, Timeout(240000)]
        public IEnumerator ShipVisuals_AllTwelveOwnedShipsRetainBaseHullAfterRefitsAndAtDeparture()
        {
            // Explicit inventory fixture: no player purchases or player saves are used.
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++)
                PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 1);
            SeedShipPortLocationsForFixture("断锚港", Enumerable.Range(0, CoreVoyageSession.Ships.Count).ToArray());
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            // Populate the starter loadout so the audit verifies that installed
            // components leave the vessel's base illustration unchanged.
            PopulateBareStarterForFixture(controller);
            var catalog = new ComponentArtCatalog();
            VoyageCargo cargo = controller.Session.Cargo;
            cargo.Water = cargo.Food = cargo.Beer = cargo.Medicine = cargo.Wood = cargo.Iron =
                cargo.Cloth = cargo.Powder = cargo.Cannonballs = cargo.Gold = 0;
            yield return ClickShipyardControl("Port Hotspot Shipyard");

            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                yield return BrowseShipMarketTo(ship);
                AssertBaseShipVisualWithInstalledComponents(controller, ship, "Shipyard Ship " + ship);
                AssertGalleryHullWaterContact(ship);
                yield return new WaitForSecondsRealtime(0.35f);
                NavalBattleVisualCapture.Capture(controller, "ShipVisual_Gallery_" + ship.ToString("00") + "_Stock.png",
                    CoreVoyageSession.Ships[ship].Name + " · 装有部件时的基础船图与船坞水线", "Shipyard Panel");
                foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                {
                    ComponentArtEntry replacement = catalog.Entries.First(entry => entry.Slot == slot && entry.Rarity == ComponentRarity.Rare);
                    OwnedShipComponent item = controller.GrantOwnedComponent(replacement);
                    Assert.That(controller.TryEquipOwnedComponent(ship, item.InstanceId), Is.True, ship + " / " + slot);
                }
                yield return ClickShipyardControl("Shipyard Ship " + ship);
                yield return ClickShipyardControl("Return To Port Hub");
                AssertBaseShipVisualWithInstalledComponents(controller, ship, "Shipyard Ship " + ship);
                AssertGalleryHullWaterContact(ship);
                yield return new WaitForSecondsRealtime(0.35f);
                NavalBattleVisualCapture.Capture(controller, "ShipVisual_Gallery_" + ship.ToString("00") + "_Refit.png",
                    CoreVoyageSession.Ships[ship].Name + " · 五槽换件生效并保留基础船图与水线", "Shipyard Panel");
            }

            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");
            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(ship));
                AssertBaseShipVisualWithInstalledComponents(controller, ship, "Starter Modular Ship");
                yield return new WaitForSecondsRealtime(0.35f);
                NavalBattleVisualCapture.Capture(controller, "ShipVisual_Departure_" + ship.ToString("00") + ".png",
                    CoreVoyageSession.Ships[ship].Name + " · 改装后维持基础外观的出航小图", "Voyage Setup");
                if (ship + 1 < CoreVoyageSession.Ships.Count)
                    yield return ClickShipyardControl("Departure Next Vessel");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertBaseShipVisualWithInstalledComponents(CoreGameplayDemoController controller, int ship, string rootName)
        {
            GameObject root = GameObject.Find(rootName);
            Assert.That(root, Is.Not.Null, rootName);
            RawImage[] images = root.GetComponentsInChildren<RawImage>(true);
            Assert.That(images.Count(image => image.name.Contains(" Layer ")), Is.Zero,
                "Equipping components must not add overlays to any vessel: " + rootName);
            RawImage hull = images.Single();
            Texture expectedHull = ship == 0 ? Resources.Load<Texture2D>("ModularShip/light_sloop_base")
                : new PortArtCatalog().ShipTexture(ship);
            Assert.That(hull.texture, Is.SameAs(expectedHull), "A refit keeps the vessel's base hull illustration.");
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
            {
                ComponentArtEntry installed = controller.ShipComponent(ship, slot);
                Assert.That(installed, Is.Not.Null, ship + " / " + slot);
                Assert.That(controller.InstalledShipComponent(ship, slot), Is.Not.Null,
                    "Components remain installed while the vessel keeps its base illustration.");
            }
        }

        private static void AssertGalleryHullWaterContact(int ship)
        {
            RawImage hull = (RawImage)ActiveButton("Shipyard Ship " + ship).targetGraphic;
            RectTransform water = GameObject.Find("Shipyard Water Contact").GetComponent<RectTransform>();
            Vector3 contact = water.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
            Assert.That(contact.y, Is.EqualTo(-4f).Within(0.1f), "The authored keel must meet the water, independent of atlas padding: " + ship);
        }
    }
}
