using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_IncompleteDeckStaysUntilTenCardsThenReturnsOneLevel()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return LoadDeckBuilderCardFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                while (controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count > 10)
                {
                    Button row = DeckBuilderBaseCards().First();
                    yield return RevealDeckBuilderCard(row);
                    ClickDeckBuilderHeader(row.name);
                    yield return null;
                }
                int[] committed = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
                var saved = DeckBuilderSavedProgress();
                Button removed = DeckBuilderBaseCards().First();
                int removedIndex = DeckBuilderCardIndex(removed);
                yield return RevealDeckBuilderCard(removed);
                ClickDeckBuilderHeader(removed.name);
                yield return null;
                GameObject draft = GameObject.Find("Deck Builder Panel");
                AssertDeckBuilderDraftCount(9);
                AssertDeckBuilderMinimumLock(true);
                EventSystem.current.SetSelectedGameObject(null);

                yield return input.Press();
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.SameAs(draft));
                AssertDeckBuilderDraftCount(9);
                AssertDeckBuilderMinimumHint(9);
                CollectionAssert.AreEqual(committed, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
                CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress(), "Rejected Escape must preserve the last legal saved deck.");

                yield return RevealDeckBuilderLibraryCard(removedIndex);
                ClickDeckBuilderHeader("Add Deck Card " + removedIndex);
                yield return null;
                AssertDeckBuilderSaved(controller, 10);
                AssertDeckBuilderMinimumLock(false);
                yield return input.Press();
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null);
                Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null,
                    "Leaving the editor returns to the workshop preview before leaving the facility.");
                Assert.That(GameObject.Find("Port Hub"), Is.Null);
                yield return input.Press();
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Naval), Has.Count.EqualTo(10));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_SettingsConfirmationClosesOneLayerWithAndWithoutSelection()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return EscapeNavigationLoadPort();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                float savedVolume = PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, .8f);
                foreach (bool clearSelection in new[] { false, true })
                {
                    ClickDeckBuilderHeader("Port Hub Settings");
                    yield return null;
                    PortSettingsView settings = controller.GetComponentInChildren<PortSettingsView>();
                    Assert.That(settings, Is.Not.Null);
                    settings.Master.value = savedVolume < .5f ? .91f : .09f;
                    ClickDeckBuilderHeader(settings.ReturnToMenu.name);
                    yield return null;
                    var confirmation = controller.GetComponentInChildren<ReturnToMenuConfirmationView>();
                    Assert.That(confirmation, Is.Not.Null);
                    if (clearSelection) EventSystem.current.SetSelectedGameObject(null);

                    yield return input.Press(5);
                    Assert.That(confirmation.gameObject.activeInHierarchy, Is.False);
                    Assert.That(settings.gameObject.activeInHierarchy, Is.True,
                        "A held Escape and the UI cancel event must not also close the parent settings.");
                    Assert.That(settings.Sheet.interactable, Is.True);
                    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CoreGameplayDemo"));
                    if (clearSelection) EventSystem.current.SetSelectedGameObject(null);
                    yield return input.Press();
                    Assert.That(settings.gameObject.activeInHierarchy, Is.False);
                    Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                    Assert.That(PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, .8f), Is.EqualTo(savedVolume),
                        "Escape dismisses the settings draft without applying it.");
                }
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_FacilityAndDossiersReturnToTheirImmediateParent()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return EscapeNavigationLoadPort();
                ClickDeckBuilderHeader("Port Hotspot Market");
                yield return null;
                Assert.That(GameObject.Find("Market Panel"), Is.Not.Null);
                yield return input.Press();
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);

                ClickDeckBuilderHeader("Port Hotspot Tavern");
                yield return null;
                ClickDeckBuilderHeader("Tavern Crew 0");
                yield return null;
                Assert.That(GameObject.Find("Tavern Crew Detail"), Is.Not.Null);
                yield return input.Press();
                Assert.That(GameObject.Find("Tavern Crew Detail"), Is.Null);
                Assert.That(GameObject.Find("Tavern Crew Stage"), Is.Not.Null);
                Assert.That(GameObject.Find("Port Hub"), Is.Null);
                yield return input.Press();
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);

                ClickDeckBuilderHeader("Port Hotspot Shipyard");
                yield return null;
                ClickDeckBuilderHeader("Shipyard Ship 0");
                yield return null;
                Assert.That(GameObject.Find("Shipyard Dossier Name"), Is.Not.Null);
                yield return input.Press();
                Assert.That(GameObject.Find("Shipyard Dossier Name"), Is.Null);
                Assert.That(ActiveButton("Shipyard Ship 0"), Is.Not.Null);
                Assert.That(GameObject.Find("Port Hub"), Is.Null);
                yield return input.Press();
                GameObject hub = GameObject.Find("Port Hub");
                Assert.That(hub, Is.Not.Null);
                yield return input.Press();
                Assert.That(GameObject.Find("Port Hub"), Is.SameAs(hub), "The root screen has no parent to dismiss.");
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_PreservesTutorialLocksSailingAndMandatoryEncounter()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return LoadGuidedTutorialScene(enterDeparture: true);
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                GameObject departure = GameObject.Find("Voyage Setup");
                Assert.That(ActiveButton("Return To Port Hub").IsInteractable(), Is.False);
                EventSystem.current.SetSelectedGameObject(null);
                yield return input.Press();
                Assert.That(GameObject.Find("Voyage Setup"), Is.SameAs(departure));
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("FirstDeparture"));

                ClickGuidedButton("Start Voyage");
                yield return null;
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                float distance = controller.Session.TotalDistanceNm;
                int water = controller.Session.Cargo.Water;
                int food = controller.Session.Cargo.Food;
                yield return input.Press();
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(controller.Session.TotalDistanceNm, Is.EqualTo(distance));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(food));
                Assert.That(controller.Session.Settlement, Is.Null);

                ClickGuidedButton("Heading East");
                yield return null;
                ClickGuidedButton("Execute Sail");
                yield return null;
                yield return WaitForGuidedNavigationPresentation();
                var encounter = controller.Session.ActiveEncounter;
                Assert.That(encounter, Is.Not.Null);
                Assert.That(encounter.Mandatory, Is.True);
                Assert.That(ActiveButton("Skip Encounter"), Is.Null);
                distance = controller.Session.TotalDistanceNm;
                decimal gold = controller.Session.ProfileGold;
                yield return input.Press();
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
                Assert.That(controller.Session.ActiveEncounter, Is.SameAs(encounter));
                Assert.That(controller.Session.TotalDistanceNm, Is.EqualTo(distance));
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Settlement, Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_DragCancelsWithoutEditingOrLeavingTheDeck()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return LoadDeckBuilderCardFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                int[] deck = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
                var saved = DeckBuilderSavedProgress();
                GameObject panel = GameObject.Find("Deck Builder Panel");
                Button card = DeckBuilderBaseCards().First();
                yield return RevealDeckBuilderCard(card);
                PointerEventData pointer = DeckBuilderPointer(card);
                pointer.pointerDrag = card.gameObject;
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.beginDragHandler);
                pointer.dragging = true;
                Vector2 destination = DeckBuilderNamedPoint("Deck Builder Parchment");
                pointer.delta = destination - pointer.position;
                pointer.position = destination;
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Drag Ghost"), Is.Not.Null);
                EventSystem.current.SetSelectedGameObject(card.gameObject);
                yield return input.Press();
                Assert.That(GameObject.Find("Deck Builder Drag Ghost"), Is.Null);
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.SameAs(panel),
                    "Escape belongs to the active drag and cannot leave its parent window in the same frame.");
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);
                pointer.dragging = false;
                ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                yield return null;
                CollectionAssert.AreEqual(deck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
                CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress());
                yield return input.Press();
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null);
                Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_DepartureSuppliesClosesBeforeTheDockWithoutPurchasing()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return EscapeNavigationLoadPort();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                ClickDeckBuilderHeader("Port Hotspot Voyage");
                yield return null;
                decimal gold = controller.Session.ProfileGold;
                int water = controller.Session.Cargo.Water;
                int food = controller.Session.Cargo.Food;
                ClickDeckBuilderHeader("Departure Buy Provisions");
                yield return null;
                Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Not.Null);
                EventSystem.current.SetSelectedGameObject(null);
                yield return input.Press();
                Assert.That(GameObject.Find("Departure Basic Supplies Overlay"), Is.Null);
                Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
                Assert.That(controller.Session.Cargo.Food, Is.EqualTo(food));
                yield return input.Press();
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_SaveGuardsAndDisabledModalCloseNeverReachTheParent()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return EscapeNavigationLoadPort();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                ClickDeckBuilderHeader("Port Hotspot Market");
                yield return null;
                GameObject market = GameObject.Find("Market Panel");
                ClickDeckBuilderHeader("Port Facility Settings");
                yield return null;
                var settings = controller.GetComponentInChildren<PortSettingsView>();
                ClickDeckBuilderHeader(settings.ReturnToMenu.name);
                yield return null;
                var confirmation = controller.GetComponentInChildren<ReturnToMenuConfirmationView>();
                Assert.That(confirmation, Is.Not.Null);

                // These transient save conditions normally occur within a synchronous
                // write. Hold them explicitly so a real keyboard frame can observe them.
                foreach (string guard in new[] { "returnMenuSaving", "profileSaveBlocked" })
                {
                    FieldInfo field = typeof(CoreGameplayDemoController).GetField(guard, BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.That(field, Is.Not.Null);
                    field.SetValue(controller, true);
                    try
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                        yield return input.Press();
                        Assert.That(confirmation.gameObject.activeInHierarchy, Is.True, guard);
                        Assert.That(settings.gameObject.activeInHierarchy, Is.True, guard);
                        Assert.That(GameObject.Find("Market Panel"), Is.SameAs(market), guard);
                    }
                    finally { field.SetValue(controller, false); }
                }

                confirmation.Cancel.interactable = false;
                EventSystem.current.SetSelectedGameObject(null);
                yield return input.Press();
                Assert.That(confirmation.gameObject.activeInHierarchy, Is.True,
                    "A disabled cancel consumes Escape without falling through to settings or the facility.");
                Assert.That(settings.gameObject.activeInHierarchy, Is.True);
                confirmation.Cancel.interactable = true;
                yield return input.Press();
                Assert.That(confirmation.gameObject.activeInHierarchy, Is.False);
                settings.Close.interactable = false;
                EventSystem.current.SetSelectedGameObject(null);
                yield return input.Press();
                Assert.That(settings.gameObject.activeInHierarchy, Is.True);
                Assert.That(GameObject.Find("Market Panel"), Is.SameAs(market));
                settings.Close.interactable = true;
                yield return input.Press();
                Assert.That(settings.gameObject.activeInHierarchy, Is.False);
                Assert.That(GameObject.Find("Market Panel"), Is.SameAs(market));
                Assert.That(GameObject.Find("Port Hub"), Is.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_MarketPurchaseShopAndFacilityRequireThreePresses()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.Date.AddHours(12);
            var ledger = PreserveMarketStorageLedgers(now);
            try
            {
                using (var input = new EscapeNavigationKeyboard())
                {
                    yield return LoadProvisioningFixture();
                    var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                    SetProvisionClock(controller, () => now);
                    yield return MarketSceneClick("Port Hotspot Market");
                    yield return SelectMarketStorageMode(controller, MarketPurchaseStorageMode.Ask);
                    yield return MarketSceneClick("Market Shop Supplies");
                    yield return EnterMarketQuantity("water", "7");
                    decimal gold = controller.Session.ProfileGold;
                    int water = controller.Session.Cargo.Water;
                    string stored = controller.Session.PortWarehouse.Serialize();
                    yield return MarketSceneClick("Confirm Market Buy water");
                    Assert.That(GameObject.Find("Market Purchase Choice"), Is.Not.Null);
                    Button.ButtonClickedEvent retiredPurchase = ActiveButton("Market Buy To Warehouse").onClick;

                    yield return input.Press();
                    Assert.That(GameObject.Find("Market Purchase Choice"), Is.Null);
                    Assert.That(GameObject.Find("Market Item Detail"), Is.Not.Null);
                    Assert.That(GameObject.Find("Market Panel"), Is.Not.Null);
                    retiredPurchase.Invoke();
                    yield return null;
                    Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                    Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
                    Assert.That(controller.Session.PortWarehouse.Serialize(), Is.EqualTo(stored));
                    yield return input.Press();
                    Assert.That(GameObject.Find("Market Item Detail"), Is.Null);
                    Assert.That(GameObject.Find("Market Panel"), Is.Not.Null);
                    Assert.That(GameObject.Find("Port Hub"), Is.Null);
                    yield return input.Press();
                    Assert.That(GameObject.Find("Market Panel"), Is.Null);
                    Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                    Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
                    LogAssert.NoUnexpectedReceived();
                }
            }
            finally { RestoreProvisionLedger(ledger); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_WarehouseAdjustmentAndSaleCancelWithoutChangingInventory()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                var session = controller.Session;
                session.Cargo.Wood = 7;
                Assert.That(session.PortWarehouse.Restore("v1;" + session.CurrentPort + ",wood,11"), Is.True);
                yield return OpenPortStorage();
                int carried = session.Cargo.Wood;
                string stored = session.PortWarehouse.Serialize();
                decimal gold = session.ProfileGold;

                yield return OpenWarehouseCargoAdjustment("wood");
                Slider adjustment = GameObject.Find("Warehouse Cargo Adjustment Slider").GetComponent<Slider>();
                yield return DragWarehouseSlider(adjustment, adjustment.minValue);
                Button.ButtonClickedEvent retiredAdjustment = ActiveButton("Confirm Warehouse Cargo Adjustment").onClick;
                yield return input.Press();
                Assert.That(GameObject.Find("Warehouse Storage Modal"), Is.Null);
                Assert.That(GameObject.Find("Warehouse Panel"), Is.Not.Null);
                retiredAdjustment.Invoke();
                yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(carried));
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(stored));
                Assert.That(session.ProfileGold, Is.EqualTo(gold));

                yield return OpenWarehouseCargoSale("wood");
                Slider sale = GameObject.Find("Warehouse Stored Sale Slider").GetComponent<Slider>();
                yield return DragWarehouseSlider(sale, sale.maxValue);
                Button.ButtonClickedEvent retiredSale = ActiveButton("Confirm Warehouse Cargo Sale").onClick;
                yield return input.Press();
                Assert.That(GameObject.Find("Warehouse Storage Modal"), Is.Null);
                Assert.That(GameObject.Find("Warehouse Panel"), Is.Not.Null);
                retiredSale.Invoke();
                yield return null;
                Assert.That(session.Cargo.Wood, Is.EqualTo(carried));
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(stored));
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                yield return input.Press();
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EscapeNavigation_MainMenuAchievementDropdownClosesBeforeAchievements()
        {
            using (var input = new EscapeNavigationKeyboard())
            {
                SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                yield return null;
                yield return null;
                ClickDeckBuilderHeader("Achievements");
                yield return null;
                GameObject achievements = GameObject.Find("Achievements Panel");
                Assert.That(achievements, Is.Not.Null);
                Dropdown category = GameObject.Find("Achievements Category").GetComponent<Dropdown>();
                Assert.That(category.IsInteractable(), Is.True);
                int selected = category.value;
                EventSystem.current.SetSelectedGameObject(category.gameObject);
                ExecuteEvents.Execute(category.gameObject, new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }, ExecuteEvents.pointerClickHandler);
                yield return null;
                Assert.That(category.transform.Find("Dropdown List"), Is.Not.Null);

                yield return input.Press(5);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(category.transform.Find("Dropdown List"), Is.Null);
                Assert.That(achievements.activeInHierarchy, Is.True,
                    "Canceling a dropdown must not close the achievements window in the same frame.");
                Assert.That(category.value, Is.EqualTo(selected));
                yield return input.Press();
                Assert.That(achievements.activeInHierarchy, Is.False);
                Assert.That(ActiveButton("Achievements"), Is.Not.Null);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                LogAssert.NoUnexpectedReceived();
            }
        }

        private static IEnumerator EscapeNavigationLoadPort()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
        }

        private sealed class EscapeNavigationKeyboard : IDisposable
        {
            private readonly InputSettings originalSettings;
            private readonly HideFlags originalFlags;
            private readonly InputSettings temporarySettings;
            private readonly Keyboard keyboard;

            public EscapeNavigationKeyboard()
            {
                originalSettings = InputSystem.settings;
                originalFlags = originalSettings.hideFlags;
                if (originalFlags == HideFlags.HideAndDontSave) originalSettings.hideFlags = HideFlags.DontSave;
                temporarySettings = Object.Instantiate(originalSettings);
                temporarySettings.hideFlags = HideFlags.DontSave;
                temporarySettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                temporarySettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                temporarySettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings = temporarySettings;
                keyboard = InputSystem.AddDevice<Keyboard>();
            }

            public IEnumerator Press(int heldFrames = 2)
            {
                keyboard.MakeCurrent();
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                for (int frame = 0; frame < heldFrames; frame++) yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
            }

            public void Dispose()
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalSettings;
                originalSettings.hideFlags = originalFlags;
                Object.Destroy(temporarySettings);
            }
        }
    }
}
