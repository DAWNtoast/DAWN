using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator ShipyardEquipment_RealClicksReplaceUnloadAndRetainIndependentVesselsAfterReload()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(900);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(1), 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            // This fixture covers replacement and cross-vessel retention, so
            // make the starter's existing fittings an explicit precondition.
            PopulateBareStarterForFixture(controller);
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            ComponentArtEntry[] variants = catalog.Entries.Where(item => item.Slot == ComponentSlot.Bow && item.Rarity == ComponentRarity.Rare).Take(2).ToArray();
            OwnedShipComponent first = controller.GrantOwnedComponent(variants[0], 0.94f);
            OwnedShipComponent replacement = controller.GrantOwnedComponent(variants[1], 1.08f);
            string originalBow = controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId;
            string secondShipBow = controller.InstalledShipComponent(1, ComponentSlot.Bow).InstanceId;
            int itemCount = controller.OwnedComponents.Count;
            decimal gold = controller.Session.ProfileGold;

            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return ClickShipyardControl("Shipyard Ship 0");
            Assert.That(GameObject.Find("Shipyard Dossier Story"), Is.Null);
            Assert.That(Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Count(item => item.name.StartsWith("Shipyard Dossier Stat ")), Is.EqualTo(5));
            NavalBattleVisualCapture.Capture(controller, "80_Shipyard_Performance.png", "船只档案：独立性能页，显示当前装配后的数值", "Shipyard Detail");
            yield return ClickShipyardControl("Shipyard Dossier Tab Background");
            Assert.That(GameObject.Find("Shipyard Dossier Story"), Is.Not.Null);
            Assert.That(GameObject.Find("Shipyard Dossier Stat 0"), Is.Null);
            NavalBattleVisualCapture.Capture(controller, "81_Shipyard_Background.png", "船只档案：独立背景页，大字号故事不与性能混排", "Shipyard Detail");
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            Assert.That(GameObject.Find("Shipyard Dossier Story"), Is.Null);
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.False, "Opening a vessel's fittings must not preselect a replacement.");
            NavalBattleVisualCapture.Capture(controller, "82_Shipyard_Equipment_Inventory.png", "配件页同时列出本船五槽与闲置部件库存", "Shipyard Detail");

            yield return ClickShipyardControl("Shipyard Equipment Owned " + first.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(first.InstanceId));
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == originalBow).ShipIndex, Is.EqualTo(-1));
            AssertShipyardTactic(controller, variants[0], true);
            AssertShipyardTactic(controller, variants[1], false);
            Assert.That(controller.Session.MaxHull, Is.EqualTo(controller.Session.SelectedShip.MaxHull + variants[0].HullBonus - catalog.Get(0).HullBonus));
            yield return ClickShipyardControl("Shipyard Dossier Tab Performance");
            Assert.That(GameObject.Find("Shipyard Dossier Stat 0").GetComponent<Text>().text, Is.EqualTo("船体上限  " + controller.Session.MaxHull));
            Assert.That(GameObject.Find("Shipyard Dossier Stat 2").GetComponent<Text>().text, Is.EqualTo("火力  " + controller.Session.Firepower));
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");

            yield return ClickShipyardControl("Shipyard Equipment Owned " + replacement.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow).InstanceId, Is.EqualTo(replacement.InstanceId));
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == first.InstanceId).ShipIndex, Is.EqualTo(-1));
            AssertShipyardTactic(controller, variants[0], false);
            AssertShipyardTactic(controller, variants[1], true);
            Assert.That(controller.Session.AcquiredSpecialCardIds, Has.Member(CoreSpecialCardCatalog.ForComponent(variants[0].Id).Card.Id),
                "Unloading a card source removes it from the voyage deck, not from the player's acquired collection.");
            NavalBattleVisualCapture.Capture(controller, "83_Shipyard_Equipment_Replaced.png", "替换后旧部件回到库存，专属战术随当前装配切换", "Shipyard Detail");

            yield return ClickShipyardControl("Shipyard Unequip Component Bow");
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null);
            AssertShipyardTactic(controller, variants[1], false);
            int hullWithoutBow = controller.Session.MaxHull;
            Assert.That(hullWithoutBow, Is.EqualTo(controller.Session.SelectedShip.MaxHull),
                "An empty slot leaves the hull at its authored base value; it does not subtract a catalogue baseline.");
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(itemCount), "Refitting must retain both the replaced and unloaded physical items.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Already-owned fittings do not charge purchase or installation fees.");

            // Leave an inventory item highlighted without installing it, then inspect a different ship.
            yield return ClickShipyardControl("Shipyard Equipment Owned " + replacement.InstanceId);
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.True);
            yield return ClickShipyardControl("Back To Shipyard Gallery");
            yield return ClickShipyardControl("Shipyard Next Vessel");
            yield return ClickShipyardControl("Shipyard Ship 1");
            Assert.That(GameObject.Find("Shipyard Dossier Stat 0"), Is.Not.Null, "A new vessel opens its own performance page.");
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            Assert.That(ActiveButton("Shipyard Equip Component").interactable, Is.False, "A highlighted inventory item must not leak between vessel dossiers.");
            yield return ClickShipyardControl("Shipyard Equipment Owned " + replacement.InstanceId);
            yield return ClickShipyardControl("Shipyard Equip Component");
            Assert.That(controller.InstalledShipComponent(1, ComponentSlot.Bow).InstanceId, Is.EqualTo(replacement.InstanceId));
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == secondShipBow).ShipIndex, Is.EqualTo(-1));
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null);
            Assert.That(controller.Session.SelectedShipIndex, Is.Zero, "Editing a moored vessel must not change the flagship.");
            Assert.That(controller.Session.MaxHull, Is.EqualTo(hullWithoutBow));
            AssertShipyardTactic(controller, variants[1], false);
            yield return ClickShipyardControl("Shipyard Dossier Tab Performance");
            Assert.That(FindRenderedShipLayer("Shipyard Detail", variants[1].ShipLayer), Is.Null,
                "Non-starter vessels retain their complete hull illustration after refitting.");
            Assert.That(GameObject.Find("Shipyard Detail").GetComponentsInChildren<RawImage>()
                .Any(image => image.name.Contains(" Layer ")), Is.False);
            Assert.That(GameObject.Find("Shipyard Dossier Stat 0").GetComponent<Text>().text,
                Is.EqualTo("船体上限  " + (CoreVoyageSession.Ships[1].MaxHull + variants[1].HullBonus - catalog.Get(0).HullBonus)),
                "The dossier must show the inspected vessel's equipment, not the current flagship's equipment.");
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");

            NavalBattleVisualCapture.Capture(controller, "84_Shipyard_Equipment_NonFlagship.png", "船坞配件页：分别维护每艘船，卸下或替换不收费", "Shipyard Detail");
            yield return ClickShipyardControl("Shipyard Dossier Tab Performance");
            yield return ClickShipyardControl("Confirm Shipyard Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(controller.Session.MaxHull, Is.EqualTo(controller.Session.SelectedShip.MaxHull + variants[1].HullBonus - catalog.Get(0).HullBonus));
            AssertShipyardTactic(controller, variants[1], true);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            string[] ids = controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(1));
            Assert.That(controller.InstalledShipComponent(0, ComponentSlot.Bow), Is.Null, "An intentionally emptied slot must not silently receive a free default on reload.");
            Assert.That(controller.InstalledShipComponent(1, ComponentSlot.Bow).InstanceId, Is.EqualTo(replacement.InstanceId));
            Assert.That(controller.InstalledShipComponent(1, ComponentSlot.Bow).Quality, Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == first.InstanceId).Quality, Is.EqualTo(0.94f).Within(0.0001f));
            CollectionAssert.AreEqual(ids, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id, StringComparer.Ordinal).ToArray());
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            AssertShipyardTactic(controller, variants[1], true);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ShipyardEquipment_UnownedVesselsCannotInstallEvenThroughRetainedCallbacks()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            ProfileGoldPersistence.Save(10);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            OwnedShipComponent item = controller.GrantOwnedComponent(catalog.Entries.First(entry => entry.Slot == ComponentSlot.Bow && entry.Rarity == ComponentRarity.Rare), 1f);
            int count = controller.OwnedComponents.Count;
            string inventory = PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey);
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return ClickShipyardControl("Shipyard Previous Vessel");
            yield return ClickShipyardControl("Shipyard Ship 4");
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            Button candidate = ActiveButton("Shipyard Equipment Owned " + item.InstanceId);
            if (candidate != null) candidate.onClick.Invoke();
            yield return null;
            Button equip = ActiveButton("Shipyard Equip Component");
            Assert.That(equip.interactable, Is.False);
            equip.onClick.Invoke(); // Bypass Button's normal interactability check to exercise the state guard.
            foreach (Button unload in Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(button => button.name.StartsWith("Shipyard Unequip Component ")))
            {
                Assert.That(unload.interactable, Is.False);
                unload.onClick.Invoke();
            }
            yield return null;
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count));
            Assert.That(controller.OwnedComponents.Single(entry => entry.InstanceId == item.InstanceId).ShipIndex, Is.EqualTo(-1));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(inventory));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(10));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ShipOwnedKey(4), 0), Is.Zero);
            Assert.That(controller.Session.SelectedShipIndex, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertShipyardTactic(CoreGameplayDemoController controller, ComponentArtEntry component, bool expected)
        {
            CoreSpecialCardDefinition tactic = CoreSpecialCardCatalog.ForComponent(component.Id);
            Assert.That(tactic, Is.Not.Null);
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(controller.Session.GetComponentBattleCards(stage).Any(index => CoreVoyageSession.CardLibrary[index].Id == tactic.Card.Id),
                    Is.EqualTo(expected && tactic.Card.IsAllowedInStage(stage)), component.Id + " / " + stage);
        }

        private static void PopulateBareStarterForFixture(CoreGameplayDemoController controller)
        {
            ComponentArtCatalog catalog = new ComponentArtCatalog();
            foreach (ComponentSlot slot in Enum.GetValues(typeof(ComponentSlot)))
            {
                ComponentArtEntry entry = catalog.Get((int)slot * ComponentArtCatalog.EntriesPerSlot);
                OwnedShipComponent item = controller.GrantOwnedComponent(entry, 1f);
                Assert.That(item, Is.Not.Null, slot.ToString());
                Assert.That(controller.TryEquipOwnedComponent(0, item.InstanceId), Is.True, slot.ToString());
            }
        }

        private static RawImage FindRenderedShipLayer(string parentName, Texture expectedTexture)
        {
            GameObject parent = GameObject.Find(parentName);
            if (parent == null || expectedTexture == null) return null;
            return parent.GetComponentsInChildren<RawImage>(true)
                .FirstOrDefault(image => image.texture == expectedTexture);
        }

        private static IEnumerator ClickShipyardControl(string name)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button button = ActiveButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.interactable, Is.True, name);
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform rect = (RectTransform)button.transform;
            if (button.targetGraphic is NavigationCompassSectorGraphic)
                rect = (RectTransform)button.transform.Find("Label");
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, name + " must accept a real pointer hit.");
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(button.gameObject), name + " blocked by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
