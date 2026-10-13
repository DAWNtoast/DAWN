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
        [UnityTest]
        public IEnumerator ShipyardEquipment_ComparisonFollowsRealSelectionReplacementAndEmptySlots()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry bowEntry = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Bow && entry.Rarity == ComponentRarity.Rare);
            ComponentArtEntry sailEntry = catalog.Entries.First(entry => entry.Slot == ComponentSlot.Sail && entry.Rarity == ComponentRarity.Rare);
            OwnedShipComponent bow = controller.GrantOwnedComponent(bowEntry, 1.08f);
            OwnedShipComponent sail = controller.GrantOwnedComponent(sailEntry, 0.94f);
            ComponentArtEntry oldBow = controller.ShipComponent(0, ComponentSlot.Bow);
            ComponentArtEntry oldSail = controller.ShipComponent(0, ComponentSlot.Sail);
            decimal gold = controller.Session.ProfileGold;

            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return ClickShipyardControl("Shipyard Ship 0");
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            foreach (string removed in new[] { "Shipyard Dossier Price", "Confirm Shipyard Vessel", "Shipyard Equipment Feedback",
                "Shipyard Equipment Selection Summary", "Shipyard Equipment Inventory Pagination" })
                Assert.That(GameObject.Find(removed), Is.Null, removed + " must not occupy the equipment page.");
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.False);
            AssertShipyardComparisonLayout();

            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            Assert.That(oldBow, Is.Null, "The new starter hull must expose an empty bow slot.");
            Assert.That(ShipyardComparisonLabel("Current", "Name"), Does.Contain("未装配"));
            Assert.That(ShipyardComparisonLabel("Selected", "Name"), Does.Contain(bowEntry.Name));
            Assert.That(ShipyardComparisonLabel("Selected", "Stats"), Does.Not.Match("[（(].*[）)]"), "The empty starter slot has no current fields to compare.");
            Assert.That(ShipyardComparisonLabel("Current", "Stats"), Does.Not.Match("[（(].*[）)]"), "Differences belong in the right comparison panel.");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null, "Inspecting a candidate must not install it.");

            yield return ClickShipyardControl("Shipyard Equipment Owned " + sail.InstanceId);
            Assert.That(oldSail, Is.Null);
            Assert.That(ShipyardComparisonLabel("Current", "Name"), Does.Contain("未装配"), "An empty sail must compare against an empty slot, not the last inspected bow.");
            Assert.That(ShipyardComparisonLabel("Selected", "Name"), Does.Contain(sailEntry.Name));
            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(bow.InstanceId));
            Assert.That(ShipyardComparisonLabel("Current", "Name"), Does.Contain(bowEntry.Name));
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.False, "Successful installation clears the candidate selection.");

            yield return ClickShipyardControl("Shipyard Unequip Component Bow");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null);
            yield return ClickShipyardControl("Shipyard Equipment Owned " + bow.InstanceId);
            Assert.That(ShipyardComparisonLabel("Current", "Name"), Does.Contain("未装配"));
            Assert.That(ShipyardComparisonLabel("Selected", "Stats"), Does.Not.Match("[（(].*[）)]"), "An empty slot has no existing fields to compare.");
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.True);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            AssertShipyardComparisonLayout();
            yield return ClickShipyardControl("Back To Shipyard Gallery");
            Assert.That(GameObject.Find("Shipyard Detail"), Is.Null);
            Assert.That(ActiveButton("Shipyard Ship 0"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        private static string ShipyardComparisonLabel(string side, string field)
        {
            GameObject label = GameObject.Find("Shipyard Equipment " + side + " Comparison " + field);
            Assert.That(label, Is.Not.Null, side + " " + field);
            return label.GetComponent<Text>().text;
        }

        private static void AssertShipyardComparisonLayout()
        {
            Canvas.ForceUpdateCanvases();
            RectTransform detail = GameObject.Find("Shipyard Detail").GetComponent<RectTransform>();
            Rect lastSlot = ShipyardRectIn(detail, "Shipyard Equipment Slot Instrument");
            Rect equip = ShipyardRectIn(detail, "Shipyard Equip Component");
            Rect back = ShipyardRectIn(detail, "Back To Shipyard Gallery");
            Rect current = ShipyardRectIn(detail, "Shipyard Equipment Current Comparison");
            Rect selected = ShipyardRectIn(detail, "Shipyard Equipment Selected Comparison");
            Assert.That(equip.yMin, Is.EqualTo(lastSlot.yMin).Within(0.5f), "The installed column and replacement button share a bottom edge.");
            Assert.That(current.xMax, Is.LessThan(selected.xMin));
            Assert.That(current.yMin, Is.EqualTo(selected.yMin).Within(0.5f));
            Assert.That(equip.yMax, Is.LessThan(current.yMin));
            Assert.That(back.xMax, Is.LessThan(0));
            Assert.That(back.yMin, Is.GreaterThan(ShipyardRectIn(detail, "Shipyard Dossier Name").yMax));
            foreach (ComponentSlot slot in System.Enum.GetValues(typeof(ComponentSlot)))
                Assert.That(ShipyardRectIn(detail, "Shipyard Equipment Slot " + slot).height, Is.GreaterThanOrEqualTo(80f), "Installed fittings need the expanded vertical spacing.");
        }

        private static Rect ShipyardRectIn(RectTransform parent, string name)
        {
            Vector3[] corners = new Vector3[4];
            GameObject target = GameObject.Find(name);
            Assert.That(target, Is.Not.Null, name);
            target.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y), local.Max(point => point.x), local.Max(point => point.y));
        }
    }
}
