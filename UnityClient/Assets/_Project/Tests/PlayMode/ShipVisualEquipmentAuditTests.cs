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
        public IEnumerator ShipVisuals_AllTwelveOwnedShipsRenderStockRefittedAndDepartureLayers()
        {
            // Explicit inventory fixture: no player purchases or player saves are used.
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++)
                PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var catalog = new ComponentArtCatalog();
            VoyageCargo cargo = controller.Session.Cargo;
            cargo.Water = cargo.Food = cargo.Beer = cargo.Medicine = cargo.Wood = cargo.Iron =
                cargo.Cloth = cargo.Powder = cargo.Cannonballs = cargo.Gold = 0;
            yield return ClickShipyardControl("Port Hotspot Shipyard");

            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                yield return BrowseShipMarketTo(ship);
                AssertVisibleEquipmentLayers(controller, ship, "Shipyard Ship " + ship);
                AssertGalleryHullWaterContact(ship);
                yield return new WaitForSecondsRealtime(0.35f);
                NavalBattleVisualCapture.Capture(controller, "ShipVisual_Gallery_" + ship.ToString("00") + "_Stock.png",
                    CoreVoyageSession.Ships[ship].Name + " · 原装船坞水线与配件", "Shipyard Panel");
                foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
                {
                    ComponentArtEntry replacement = catalog.Entries.First(entry => entry.Slot == slot && entry.Rarity == ComponentRarity.Rare);
                    OwnedShipComponent item = controller.GrantOwnedComponent(replacement, 1f);
                    Assert.That(controller.TryEquipOwnedComponent(ship, item.InstanceId), Is.True, ship + " / " + slot);
                }
                yield return ClickShipyardControl("Shipyard Ship " + ship);
                yield return ClickShipyardControl("Back To Shipyard Gallery");
                AssertVisibleEquipmentLayers(controller, ship, "Shipyard Ship " + ship);
                AssertGalleryHullWaterContact(ship);
                yield return new WaitForSecondsRealtime(0.35f);
                NavalBattleVisualCapture.Capture(controller, "ShipVisual_Gallery_" + ship.ToString("00") + "_Refit.png",
                    CoreVoyageSession.Ships[ship].Name + (ship == 0 ? " · 五槽换件后的模块贴图与水线" : " · 五槽换件生效并保留原船外观与水线"), "Shipyard Panel");
            }

            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");
            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(ship));
                AssertVisibleEquipmentLayers(controller, ship, "Starter Modular Ship");
                yield return new WaitForSecondsRealtime(0.35f);
                NavalBattleVisualCapture.Capture(controller, "ShipVisual_Departure_" + ship.ToString("00") + ".png",
                    CoreVoyageSession.Ships[ship].Name + " · 改装后的出航小图", "Voyage Setup");
                if (ship + 1 < CoreVoyageSession.Ships.Count)
                    yield return ClickShipyardControl("Departure Next Vessel");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertVisibleEquipmentLayers(CoreGameplayDemoController controller, int ship, string rootName)
        {
            GameObject root = GameObject.Find(rootName);
            Assert.That(root, Is.Not.Null, rootName);
            RawImage[] layers = root.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.Contains(" Layer ")).ToArray();
            Assert.That(layers.Length, Is.EqualTo(ship == 0 ? ComponentArtCatalog.SlotCount : 0), rootName);
            if (ship != 0)
            {
                var art = new PortArtCatalog();
                RawImage hull = root.GetComponentsInChildren<RawImage>(true).Single();
                Assert.That(hull.texture, Is.SameAs(art.ShipTexture(ship)), "A refit keeps the non-starter vessel's original hull atlas.");
                return;
            }
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
            {
                ComponentArtEntry installed = controller.ShipComponent(ship, slot);
                RawImage layer = layers.Single(image => image.name.EndsWith(" Layer " + installed.Id, StringComparison.Ordinal));
                Assert.That(layer.texture, Is.SameAs(installed.ShipLayer), ship + " / " + slot);
                Assert.That(layer.raycastTarget, Is.False, "Equipment must not intercept the vessel inspection hit.");
                Assert.That(layer.rectTransform.rect.width, Is.GreaterThan(0f));
                Assert.That(layer.rectTransform.rect.height, Is.GreaterThan(0f));
                Assert.That(layer.color.a, Is.GreaterThan(0f));
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
