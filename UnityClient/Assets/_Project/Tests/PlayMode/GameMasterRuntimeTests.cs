using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Real runtime buttons on an isolated canvas, without gameplay Awake/audio.
    /// Each preference touched by a command is restored, including key absence.
    /// </summary>
    public sealed class GameMasterRuntimeTests
    {
        private GameObject host;
        private GameObject canvasObject;
        private GameObject eventObject;
        private EventSystem previousEvents;
        private EventSystem[] suspendedEvents;
        private GraphicRaycaster[] suspendedRaycasters;
        private GameObject previousSelection;
        private CoreGameplayDemoController controller;
        private CoreVoyageSession session;
        private RectTransform page;
        private Button underlay;
        private IntPreference[] changedInts;
        private IntPreference[] protectedInts;
        private bool scopeExisted;
        private string scopeBefore;
        private bool decimalGoldExisted;
        private string decimalGoldBefore;
        private StringPreference[] changedStrings;
        private ComponentArtCatalog componentArt;
        private PortComponentShopStockItem oldOffer;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Application.isPlaying, Is.True);
            // RaycastAll queries every registered canvas, even when that canvas's
            // EventSystem is suspended. Isolate pointer tests from prior scenes.
            suspendedRaycasters = Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None)
                .Where(raycaster => raycaster.enabled).ToArray();
            foreach (GraphicRaycaster raycaster in suspendedRaycasters) raycaster.enabled = false;
            decimalGoldExisted = PlayerPrefs.HasKey(ProfileGoldPersistence.DecimalKey);
            decimalGoldBefore = PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey, string.Empty);
            PlayerPrefs.DeleteKey(ProfileGoldPersistence.DecimalKey);
            changedStrings = new[]
            {
                StringPreference.Read(CoreGameplayDemoController.TreasureProgressKey),
                StringPreference.Read(CoreGameplayDemoController.ComponentInventoryKey),
                StringPreference.Read(CoreGameplayDemoController.AcquiredSpecialCardsKey),
                StringPreference.Read(CoreGameplayDemoController.SpecialCardExperienceKey),
                StringPreference.Read(CoreGameplayDemoController.CurrentPortKey),
                StringPreference.Read(CoreGameplayDemoController.ShipPortProgressKey),
            };
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TreasureProgressKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.AcquiredSpecialCardsKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.SpecialCardExperienceKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.CurrentPortKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ShipPortProgressKey);
            componentArt = new ComponentArtCatalog();
            oldOffer = PortComponentShopCatalog.BuildStock(componentArt, "灰鸥港", CoreVoyageSession.GameClockOrigin)[0];
            changedInts = new[]
            {
                IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey),
                IntPreference.Read(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")),
                IntPreference.Read(CoreGameplayDemoController.ComponentShopPurchaseKey(oldOffer.Id)),
            };
            protectedInts = new[]
            {
                IntPreference.Read(CoreGameplayDemoController.GameMasterShopRefreshKey("白贝港")),
                IntPreference.Read(CoreGameplayDemoController.CrewRecruitmentKey(0)),
                IntPreference.Read(CoreGameplayDemoController.CargoKey("Gold")),
                IntPreference.Read(CoreGameplayDemoController.LegacyWitchRecruitedKey),
            };
            scopeExisted = PlayerPrefs.HasKey(CoreGameplayDemoController.GameMasterShopScopeKey);
            scopeBefore = PlayerPrefs.GetString(CoreGameplayDemoController.GameMasterShopScopeKey, string.Empty);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GameMasterShopScopeKey);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港"), 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(oldOffer.Id), 1);

            host = new GameObject("Inactive GM Test Controller");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            Assert.That(controller.Session, Is.Null);
            session = new CoreVoyageSession();
            session.RestoreProfile(1234, "灰鸥港");
            Write("session", session);
            Write("componentArt", componentArt);
            Write("componentInventory", new OwnedShipComponentInventory(componentArt));
            Write("interfaceFont", Invoke("CreateInterfaceFont"));
            Invoke("BuildCanvas", "Isolated Game Master Canvas");
            page = Read<RectTransform>("contentRoot");
            canvasObject = page.GetComponentInParent<Canvas>(true).rootCanvas.gameObject;
            canvasObject.transform.SetParent(null, false);
            var underlayObject = new GameObject("Underlying GM Draft", typeof(RectTransform), typeof(Image), typeof(Button));
            underlayObject.transform.SetParent(page, false);
            underlay = underlayObject.GetComponent<Button>();
            underlay.targetGraphic = underlayObject.GetComponent<Image>();
            previousEvents = EventSystem.current;
            previousSelection = previousEvents == null ? null : previousEvents.currentSelectedGameObject;
            suspendedEvents = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Where(events => events.enabled).ToArray();
            foreach (EventSystem events in suspendedEvents) events.enabled = false;
            eventObject = new GameObject("Isolated GM EventSystem", typeof(EventSystem));
            EventSystem.current = eventObject.GetComponent<EventSystem>();
            EventSystem.current.SetSelectedGameObject(underlayObject);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (controller != null) Invoke("CloseGameMaster");
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (eventObject != null) eventObject.GetComponent<EventSystem>().enabled = false;
            if (suspendedEvents != null) foreach (EventSystem events in suspendedEvents) if (events != null) events.enabled = true;
            if (previousEvents != null)
            {
                EventSystem.current = previousEvents;
                if (previousSelection != null && previousSelection.activeInHierarchy)
                    previousEvents.SetSelectedGameObject(previousSelection);
            }
            if (canvasObject != null) Object.Destroy(canvasObject);
            if (eventObject != null) Object.Destroy(eventObject);
            if (host != null) Object.Destroy(host);
            if (suspendedRaycasters != null)
                foreach (GraphicRaycaster raycaster in suspendedRaycasters)
                    if (raycaster != null) raycaster.enabled = true;
            foreach (IntPreference preference in changedInts) preference.Restore();
            if (decimalGoldExisted) PlayerPrefs.SetString(ProfileGoldPersistence.DecimalKey, decimalGoldBefore);
            else PlayerPrefs.DeleteKey(ProfileGoldPersistence.DecimalKey);
            if (scopeExisted) PlayerPrefs.SetString(CoreGameplayDemoController.GameMasterShopScopeKey, scopeBefore);
            else PlayerPrefs.DeleteKey(CoreGameplayDemoController.GameMasterShopScopeKey);
            foreach (StringPreference preference in changedStrings) preference.Restore();
            PlayerPrefs.Save();
            yield return null;
            foreach (IntPreference preference in protectedInts) preference.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator OpeningAndSameFrameReopening_StayLockedAndPreserveDrafts()
        {
            VoyageSupplyOrder order = Read<VoyageSupplyOrder>("pendingSupplyOrder");
            order.Water = 70;
            int[] children = page.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray();
            IntPreference goldBefore = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
            Invoke("OpenGameMaster");
            Assert.That(controller.IsGameMasterOpen, Is.True);
            Assert.That(Read<Toggle>("gameMasterUnlockToggle").isOn, Is.False);
            Assert.That(Read<Button>("gameMasterSetGoldButton").interactable, Is.False);
            Assert.That(Read<InputField>("gameMasterTreasureFragmentsInput").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterSetTreasureFragmentsButton").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterRefreshShopButton").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterRefreshMarketButton").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterUnlockComponentsButton").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterLevelTenButton").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterMovePortButton").interactable, Is.False);
            Read<Button>("gameMasterSetGoldButton").onClick.Invoke();
            Read<InputField>("gameMasterTreasureFragmentsInput").text = "4";
            Read<Button>("gameMasterSetTreasureFragmentsButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshShopButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshMarketButton").onClick.Invoke();
            InvokeNewGameMasterCallbacks();
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            Assert.That(session.TreasureFragments, Is.Zero);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey), Is.False);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")), Is.Zero);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GameMasterShopScopeKey), Is.False);
            AssertNewGameMasterProgressUnchanged();
            goldBefore.AssertUnchanged();
            CanvasGroup group = page.GetComponent<CanvasGroup>();
            Assert.That(underlay.IsInteractable(), Is.False);
            Read<Toggle>("gameMasterUnlockToggle").isOn = true;
            Invoke("CloseGameMaster");
            Invoke("OpenGameMaster");
            Assert.That(Read<Toggle>("gameMasterUnlockToggle").isOn, Is.False);
            yield return null;
            Assert.That(page.GetComponent<CanvasGroup>(), Is.SameAs(group));
            Assert.That(page.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
            Assert.That(underlay.IsInteractable(), Is.False);
            Assert.That(canvasObject.GetComponentsInChildren<Transform>().Count(item => item.name == "Game Master Overlay"), Is.EqualTo(1));
            NavalBattleVisualCapture.Capture(controller, "40_Game_Master_Locked.png",
                "实际 GM 面板：默认锁定、精确金币范围、部件商店刷新与立即存档提示", "Game Master Sheet");
            Invoke("CloseGameMaster");
            Assert.That(underlay.IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(underlay.gameObject));
            CollectionAssert.AreEqual(children, page.GetComponentsInChildren<Transform>(true).Select(item => item.GetInstanceID()).ToArray());
            Assert.That(order.Water, Is.EqualTo(70));
            goldBefore.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator SettingGold_SavesExactZeroAndMaximumAndRejectsDuplicateCallbacks()
        {
            OpenUnlocked();
            Read<Button>("gameMasterPortNextButton").onClick.Invoke();
            InputField input = Read<InputField>("gameMasterGoldInput");
            Button set = Read<Button>("gameMasterSetGoldButton");
            int cargoGold = session.Cargo.Gold;
            input.text = "0";
            set.onClick.Invoke();
            Assert.That(session.ProfileGold, Is.Zero);
            Assert.That(ProfileGoldPersistence.Load(), Is.Zero);
            input.text = "1000000000";
            set.onClick.Invoke();
            Read<InputField>("gameMasterTreasureFragmentsInput").text = "4";
            Read<Button>("gameMasterSetTreasureFragmentsButton").onClick.Invoke();
            InvokeNewGameMasterCallbacks();
            Assert.That(session.ProfileGold, Is.Zero, "A duplicate callback in the same frame must not perform another command.");
            Assert.That(session.TreasureFragments, Is.Zero, "Gold and fragment commands must share the same cooldown.");
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey), Is.False);
            AssertNewGameMasterProgressUnchanged();
            Assert.That(set.interactable, Is.False);
            yield return new WaitForSecondsRealtime(0.45f);
            set.onClick.Invoke();
            Assert.That(session.ProfileGold, Is.EqualTo(CoreGameMasterTools.MaximumProfileGold));
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(CoreGameMasterTools.MaximumProfileGold));
            Assert.That(session.Cargo.Gold, Is.EqualTo(cargoGold));
        }

        [TestCase("-1")]
        [TestCase("+1")]
        [TestCase("1.00001")]
        [TestCase(" 50")]
        [TestCase("1,000")]
        [TestCase("1000000001")]
        [TestCase("99999999999999999999999")]
        public void InvalidInput_DisablesAndRejectsActionWithoutChangingAmount(string input)
        {
            IntPreference before = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
            OpenUnlocked();
            Read<InputField>("gameMasterGoldInput").text = input;
            Button set = Read<Button>("gameMasterSetGoldButton");
            Assert.That(set.interactable, Is.False);
            set.onClick.Invoke();
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            before.AssertUnchanged();
        }

        [TestCase("")]
        [TestCase("-1")]
        [TestCase("+1")]
        [TestCase("1.0")]
        [TestCase(" 4")]
        [TestCase("4 ")]
        [TestCase("1,000")]
        [TestCase("2147483648")]
        [TestCase("99999999999999999999999")]
        [TestCase("４")]
        [TestCase("١")]
        public void InvalidTreasureFragmentInput_RejectsWholeInputWithoutWritingProgress(string value)
        {
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 100, fragments = 7 });
            string before = JsonUtility.ToJson(session.CaptureTreasureProgress());
            OpenUnlocked();
            InputField input = Read<InputField>("gameMasterTreasureFragmentsInput");
            Assert.That(input.contentType, Is.EqualTo(InputField.ContentType.Standard));
            Assert.That(input.characterLimit, Is.Zero);
            input.text = value;
            Assert.That(input.text, Is.EqualTo(value), "Pasted invalid text must not be silently filtered into a valid amount.");
            Button set = Read<Button>("gameMasterSetTreasureFragmentsButton");
            Assert.That(set.interactable, Is.False);
            set.onClick.Invoke();
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(before));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey), Is.False);
        }

        [UnityTest]
        public IEnumerator SettingTreasureFragments_SavesZeroAndFourAndPreservesOtherSessionAndSavedFields()
        {
            var progress = TreasureProgressFixture();
            session.RestoreTreasureProgress(progress);
            session.Cargo.Water = 42;
            session.Cargo.Gold = 17;
            string sessionBefore = TreasureProgressWithoutFragments(session.CaptureTreasureProgress());
            // Persisted progress can differ from an unsaved runtime draft. The GM
            // command must preserve the existing record rather than save the draft.
            CoreTreasureProgress saved = TreasureProgressFixture();
            saved.fragments = 37;
            saved.identifiedWeatheredFragments = 11;
            saved.identifiedGildedFragments = 9;
            saved.identificationSeed = "SAVED-IDENTIFICATION-SEED";
            saved.identificationSequence = 123;
            saved.selectedMapId = "saved:held";
            saved.maps = new[] { new CoreTreasureMapSave { id = "saved:held", seed = "SAVED-HELD", rankId = "gilded" } };
            saved.gems = 63;
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, JsonUtility.ToJson(saved));
            string persistedBefore = TreasureProgressWithoutFragments(saved);

            OpenUnlocked();
            yield return null;
            InputField input = Read<InputField>("gameMasterTreasureFragmentsInput");
            Button set = Read<Button>("gameMasterSetTreasureFragmentsButton");
            Button.ButtonClickedEvent callback = set.onClick;
            Assert.That(input.text, Is.EqualTo("8"));
            input.text = "0";
            ClickGameMasterGraphic(set);
            Assert.That(session.TreasureFragments, Is.Zero);
            Assert.That(set.interactable, Is.False);
            Assert.That(TreasureProgressWithoutFragments(session.CaptureTreasureProgress()), Is.EqualTo(sessionBefore));
            CoreTreasureProgress loaded = JsonUtility.FromJson<CoreTreasureProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey));
            Assert.That(loaded.fragments, Is.Zero);
            Assert.That(TreasureProgressWithoutFragments(loaded), Is.EqualTo(persistedBefore));

            input.text = "4";
            callback.Invoke();
            Read<InputField>("gameMasterGoldInput").text = "99";
            Read<Button>("gameMasterSetGoldButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshShopButton").onClick.Invoke();
            Assert.That(session.TreasureFragments, Is.Zero, "A repeated fragment callback must respect the cooldown.");
            Assert.That(session.ProfileGold, Is.EqualTo(1234), "Fragments and gold must share the same cooldown.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")), Is.Zero,
                "Fragments and shop refresh must share the same cooldown.");

            yield return new WaitForSecondsRealtime(.45f);
            Invoke("RefreshGameMasterControls");
            ClickGameMasterGraphic(set);
            Assert.That(session.TreasureFragments, Is.EqualTo(4));
            Assert.That(session.Cargo.Water, Is.EqualTo(42));
            Assert.That(session.Cargo.Gold, Is.EqualTo(17));
            Assert.That(TreasureProgressWithoutFragments(session.CaptureTreasureProgress()), Is.EqualTo(sessionBefore));
            loaded = JsonUtility.FromJson<CoreTreasureProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey));
            Assert.That(loaded.fragments, Is.EqualTo(4));
            Assert.That(TreasureProgressWithoutFragments(loaded), Is.EqualTo(persistedBefore));
            var reloaded = new CoreVoyageSession();
            reloaded.RestoreProfile(1234, "灰鸥港");
            reloaded.RestoreTreasureProgress(loaded);
            Assert.That(reloaded.TreasureFragments, Is.EqualTo(4));
            Assert.That(reloaded.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(11));
            Assert.That(reloaded.GetIdentifiedTreasureFragments("gilded"), Is.EqualTo(9));
            Assert.That(reloaded.SelectedTreasureMap.Seed, Is.EqualTo("SAVED-HELD"));
            Assert.That(reloaded.Cargo.Gem, Is.EqualTo(63));
            Assert.That(reloaded.CaptureTreasureProgress().identificationSequence, Is.EqualTo(123));

            string savedAfter = PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey);
            Invoke("CloseGameMaster");
            yield return new WaitForSecondsRealtime(.45f);
            callback.Invoke();
            Assert.That(session.TreasureFragments, Is.EqualTo(4), "A closed GM panel cannot run a stored callback.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(savedAfter));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SettingTreasureFragments_WithNoSavedRecordCapturesProgressAndEnablesIdentificationAndCrafting()
        {
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 100, identificationSeed = "GM-FRAGMENT-INTERACTION", identificationSequence = 14,
                gems = 9, ancientMaterials = 5,
            });
            OpenUnlocked();
            Read<InputField>("gameMasterTreasureFragmentsInput").text = "4";
            Read<Button>("gameMasterSetTreasureFragmentsButton").onClick.Invoke();
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey),
                Is.EqualTo(JsonUtility.ToJson(session.CaptureTreasureProgress())));
            Assert.That(session.TreasureMaps, Is.Empty, "Setting unidentified fragments must not create or select a map.");
            for (int piece = 0; piece < 4; piece++)
            {
                Assert.That(session.TryIdentifyTreasureFragment(out string reason), Is.True, reason);
                Assert.That(session.TreasureFragments, Is.EqualTo(3 - piece));
                Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.EqualTo(piece + 1));
            }
            Assert.That(session.CanIdentifyTreasureFragment(out _), Is.False);
            Assert.That(session.TryCraftTreasureMapForRank("weathered", "GM-FRAGMENT-CRAFT", out string craftReason), Is.True, craftReason);
            Assert.That(session.GetIdentifiedTreasureFragments("weathered"), Is.Zero);
            Assert.That(session.TreasureMaps.Single().Seed, Is.EqualTo("GM-FRAGMENT-CRAFT"));
            Assert.That(session.SelectedTreasureMap, Is.Null);
            Assert.That(session.CaptureTreasureProgress().identificationSequence, Is.EqualTo(18));
            Assert.That(session.Cargo.Gem, Is.EqualTo(9));
            Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(5));
        }

        [TestCase("not JSON")]
        [TestCase("null")]
        [TestCase("")]
        public void UnreadableTreasureRecord_RejectsFragmentActionAndPreservesOriginalRecord(string saved)
        {
            session.RestoreTreasureProgress(TreasureProgressFixture());
            string before = JsonUtility.ToJson(session.CaptureTreasureProgress());
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, saved);
            OpenUnlocked();
            Read<InputField>("gameMasterTreasureFragmentsInput").text = "4";
            Read<Button>("gameMasterSetTreasureFragmentsButton").onClick.Invoke();
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(before));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(saved));
            Assert.That(Read<bool>("treasureProgressReadFailed"), Is.True);
            Assert.That(Read<Button>("gameMasterSetTreasureFragmentsButton").interactable, Is.False);
            Assert.That(Read<Button>("gameMasterLevelTenButton").interactable, Is.False);
            Read<Button>("gameMasterLevelTenButton").onClick.Invoke();
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(before));
            Invoke("SaveTreasureProgress");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(saved),
                "The failed-read guard must continue protecting the original record after the GM action.");
        }

        [Test]
        public void ExistingTreasureReadFailure_DisablesAndRejectsFragmentCallback()
        {
            session.RestoreTreasureProgress(TreasureProgressFixture());
            string before = JsonUtility.ToJson(session.CaptureTreasureProgress());
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, before);
            Write("treasureProgressReadFailed", true);
            OpenUnlocked();
            Read<InputField>("gameMasterTreasureFragmentsInput").text = "4";
            Button set = Read<Button>("gameMasterSetTreasureFragmentsButton");
            Assert.That(set.interactable, Is.False);
            set.onClick.Invoke();
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(before));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator UnlockComponents_AddsOnlyMissingCatalogIdsAndPreservesExistingInstancesAndEquipment()
        {
            OwnedShipComponentInventory inventory = Read<OwnedShipComponentInventory>("componentInventory");
            inventory.InitializeEmptyLoadout(0);
            ComponentArtEntry entry = componentArt.ExpandedEntries.First(item => item.Rarity == ComponentRarity.Rare);
            OwnedShipComponent installed = inventory.Grant(entry);
            OwnedShipComponent duplicate = inventory.Grant(entry);
            Assert.That(inventory.TryEquip(0, installed.InstanceId), Is.True);
            Assert.That(installed.InstalledByPlayer, Is.True);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, inventory.Export());
            string installedCard = CoreSpecialCardCatalog.ForComponent(entry.Id).Card.Id;
            string savedCrewCard = CoreSpecialCardCatalog.All.First(item => item.SourceKind == CoreSpecialCardSourceKind.Crew).Card.Id;
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { [installedCard] = 119 });
            KeyValuePair<string, int>[] experienceBefore = session.SpecialCardExperience.ToArray();
            string savedExperience = installedCard + "=360;" + savedCrewCard + "=840";
            PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey, savedExperience);
            PlayerPrefs.SetString(CoreGameplayDemoController.AcquiredSpecialCardsKey, savedCrewCard);
            string treasure = JsonUtility.ToJson(session.CaptureTreasureProgress());
            OpenUnlocked();
            yield return null;
            ClickGameMasterGraphic(Read<Button>("gameMasterPortNextButton"));
            Button unlock = Read<Button>("gameMasterUnlockComponentsButton");
            Button.ButtonClickedEvent callback = unlock.onClick;
            ClickGameMasterGraphic(unlock);

            Assert.That(inventory.Items.Count, Is.EqualTo(componentArt.ExpandedEntries.Count + 1));
            foreach (ComponentArtEntry definition in componentArt.ExpandedEntries)
                Assert.That(inventory.Items.Count(item => item.ComponentId == definition.Id),
                    Is.EqualTo(definition.Id == entry.Id ? 2 : 1), definition.Id);
            Assert.That(inventory.Installed(0, entry.Slot), Is.SameAs(installed));
            Assert.That(installed.InstalledByPlayer, Is.True);
            Assert.That(inventory.Items.Single(item => item.InstanceId == duplicate.InstanceId), Is.SameAs(duplicate));
            Assert.That(duplicate.ShipIndex, Is.EqualTo(-1));
            Assert.That(inventory.Items.Where(item => item != installed).All(item => item.ShipIndex == -1), Is.True);
            string saved = inventory.Export();
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(saved));
            var restored = new OwnedShipComponentInventory(componentArt);
            Assert.That(restored.Import(saved, ship => ship == 0), Is.True);
            Assert.That(restored.Export(), Is.EqualTo(saved));
            string[] componentCards = componentArt.ExpandedEntries.Select(item => CoreSpecialCardCatalog.ForComponent(item.Id))
                .Where(item => item != null).Select(item => item.Card.Id).Distinct().ToArray();
            CollectionAssert.AreEquivalent(componentCards, session.AcquiredSpecialCardIds);
            CollectionAssert.AreEquivalent(componentCards.Concat(new[] { savedCrewCard }),
                PlayerPrefs.GetString(CoreGameplayDemoController.AcquiredSpecialCardsKey).Split(';'));
            CollectionAssert.AreEquivalent(experienceBefore, session.SpecialCardExperience.ToArray());
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey), Is.EqualTo(savedExperience),
                "Collecting fitting cards must not overwrite their saved experience with a runtime draft.");

            // All commands share one cooldown, including callbacks held by the UI.
            callback.Invoke();
            Read<Button>("gameMasterLevelTenButton").onClick.Invoke();
            Read<Button>("gameMasterMovePortButton").onClick.Invoke();
            Read<InputField>("gameMasterGoldInput").text = "99";
            Read<Button>("gameMasterSetGoldButton").onClick.Invoke();
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(treasure));
            yield return new WaitForSecondsRealtime(.45f);
            callback.Invoke();
            Assert.That(inventory.Export(), Is.EqualTo(saved), "Unlocking a complete catalog must not add duplicate instances.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(saved));
            CollectionAssert.AreEquivalent(experienceBefore, session.SpecialCardExperience.ToArray());
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SpecialCardExperienceKey), Is.EqualTo(savedExperience));
        }

        [Test]
        public void UnlockComponents_WithUnreadableInventoryRejectsActionAndPreservesOriginalRecord()
        {
            const string original = "{unreadable-component-inventory";
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentInventoryKey, original);
            Write("componentInventoryReadFailed", true);
            OpenUnlocked();
            Button unlock = Read<Button>("gameMasterUnlockComponentsButton");
            Assert.That(unlock.interactable, Is.False);
            unlock.onClick.Invoke();
            Assert.That(Read<OwnedShipComponentInventory>("componentInventory").Items, Is.Empty);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ComponentInventoryKey), Is.EqualTo(original));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.AcquiredSpecialCardsKey), Is.False);
        }

        [TestCase(900, 100)]
        [TestCase(900, 6400)]
        [TestCase(6500, 100)]
        public void LevelTen_PreservesOtherRuntimeAndSavedFieldsAndNeverLowersExperience(int runtimeExperience, int savedExperience)
        {
            CoreTreasureProgress runtime = TreasureProgressFixture();
            runtime.pirateExperience = runtimeExperience;
            session.RestoreTreasureProgress(runtime);
            string runtimeBefore = TreasureProgressWithoutExperience(session.CaptureTreasureProgress());
            CoreTreasureProgress saved = TreasureProgressFixture();
            saved.pirateExperience = savedExperience;
            saved.fragments = 37;
            saved.identifiedWeatheredFragments = 11;
            saved.identificationSeed = "SAVED-LEVEL-IDENTIFICATION";
            saved.identificationSequence = 123;
            saved.selectedMapId = "saved:level";
            saved.maps = new[] { new CoreTreasureMapSave { id = "saved:level", seed = "SAVED-LEVEL", rankId = "gilded" } };
            saved.gems = 63;
            string savedBefore = TreasureProgressWithoutExperience(saved);
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, JsonUtility.ToJson(saved));
            OpenUnlocked();
            Read<Button>("gameMasterLevelTenButton").onClick.Invoke();

            Assert.That(session.PirateLevel, Is.EqualTo(10));
            Assert.That(session.PirateExperience, Is.EqualTo(Math.Max(runtimeExperience, CoreVoyageSession.PirateExperienceForLevel(10))));
            Assert.That(TreasureProgressWithoutExperience(session.CaptureTreasureProgress()), Is.EqualTo(runtimeBefore));
            CoreTreasureProgress loaded = JsonUtility.FromJson<CoreTreasureProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey));
            Assert.That(loaded.pirateExperience,
                Is.EqualTo(Math.Max(savedExperience, Math.Max(runtimeExperience, CoreVoyageSession.PirateExperienceForLevel(10)))));
            Assert.That(TreasureProgressWithoutExperience(loaded), Is.EqualTo(savedBefore));
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ComponentInventoryKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.AcquiredSpecialCardsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.SpecialCardExperienceKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CurrentPortKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ShipPortProgressKey), Is.False);
        }

        [UnityTest]
        public IEnumerator LevelTen_RealButtonSavesUnsavedProgressAndBlocksOtherCommandsDuringCooldown()
        {
            session.RestoreTreasureProgress(TreasureProgressFixture());
            string before = TreasureProgressWithoutExperience(session.CaptureTreasureProgress());
            OpenUnlocked();
            yield return null;
            ClickGameMasterGraphic(Read<Button>("gameMasterPortNextButton"));
            ClickGameMasterGraphic(Read<Button>("gameMasterLevelTenButton"));
            Assert.That(session.PirateLevel, Is.EqualTo(10));
            Assert.That(TreasureProgressWithoutExperience(session.CaptureTreasureProgress()), Is.EqualTo(before));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey),
                Is.EqualTo(JsonUtility.ToJson(session.CaptureTreasureProgress())));
            Assert.That(Read<Button>("gameMasterLevelTenButton").interactable, Is.False);
            Read<Button>("gameMasterUnlockComponentsButton").onClick.Invoke();
            Read<Button>("gameMasterMovePortButton").onClick.Invoke();
            Assert.That(Read<OwnedShipComponentInventory>("componentInventory").Items, Is.Empty);
            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ComponentInventoryKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CurrentPortKey), Is.False);
        }

        [UnityTest]
        public IEnumerator MovePort_CyclesDestinationsMovesOnlyFlagshipAndClearsSupplyDraftWithoutRenderingInactiveController()
        {
            Assert.That(session.RestoreShipPortProgress(new CoreShipPortProgress { ships = new[] {
                new CoreShipPortLocation { shipIndex = 0, port = "灰鸥港" },
                new CoreShipPortLocation { shipIndex = 1, port = "白贝港" },
            } }, new[] { 0, 1 }, 0), Is.True);
            string savedOtherPort = CoreVoyageSession.DestinationPorts.First(port => port != "灰鸥港" && port != "白贝港");
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "灰鸥港");
            PlayerPrefs.SetString(CoreGameplayDemoController.ShipPortProgressKey, JsonUtility.ToJson(new CoreShipPortProgress { ships = new[] {
                new CoreShipPortLocation { shipIndex = 0, port = "灰鸥港" },
                new CoreShipPortLocation { shipIndex = 1, port = savedOtherPort },
            } }));
            VoyageSupplyOrder order = Read<VoyageSupplyOrder>("pendingSupplyOrder");
            order.Water = 70;
            order.Food = 20;
            Write("activePortFacility", 3);
            int children = page.childCount;
            string treasureBefore = JsonUtility.ToJson(session.CaptureTreasureProgress());
            OpenUnlocked();
            yield return null;
            Button move = Read<Button>("gameMasterMovePortButton");
            Assert.That(CoreVoyageSession.DestinationPorts[Read<int>("gameMasterTargetPortIndex")], Is.EqualTo("灰鸥港"));
            Assert.That(move.interactable, Is.False, "Moving to the current port is disabled.");
            int initial = Read<int>("gameMasterTargetPortIndex");
            ClickGameMasterGraphic(Read<Button>("gameMasterPortPreviousButton"));
            Assert.That(Read<int>("gameMasterTargetPortIndex"),
                Is.EqualTo((initial + CoreVoyageSession.DestinationPorts.Count - 1) % CoreVoyageSession.DestinationPorts.Count));
            ClickGameMasterGraphic(Read<Button>("gameMasterPortNextButton"));
            Assert.That(Read<int>("gameMasterTargetPortIndex"), Is.EqualTo(initial));
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
                Read<Button>("gameMasterPortNextButton").onClick.Invoke();
            Assert.That(Read<int>("gameMasterTargetPortIndex"), Is.EqualTo(initial));
            Assert.That(move.interactable, Is.False);
            ClickGameMasterGraphic(Read<Button>("gameMasterPortNextButton"));
            string destination = CoreVoyageSession.DestinationPorts[Read<int>("gameMasterTargetPortIndex")];
            Assert.That(Read<Text>("gameMasterTargetPortLabel").text, Does.Contain(destination));
            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"), "Browsing must not relocate the player.");
            Assert.That(move.interactable, Is.True);
            Button.ButtonClickedEvent callback = move.onClick;
            ClickGameMasterGraphic(move);

            Assert.That(controller.IsGameMasterOpen, Is.False);
            Assert.That(session.CurrentPort, Is.EqualTo(destination));
            Assert.That(session.ShipPort(0), Is.EqualTo(destination));
            Assert.That(session.ShipPort(1), Is.EqualTo("白贝港"));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey), Is.EqualTo(destination));
            string persisted = PlayerPrefs.GetString(CoreGameplayDemoController.ShipPortProgressKey);
            CoreShipPortProgress loaded = JsonUtility.FromJson<CoreShipPortProgress>(persisted);
            Assert.That(loaded.ships.Length, Is.EqualTo(2));
            Assert.That(loaded.ships.Single(item => item.shipIndex == 0).port, Is.EqualTo(destination));
            Assert.That(loaded.ships.Single(item => item.shipIndex == 1).port, Is.EqualTo(savedOtherPort),
                "The command must not overwrite another ship's saved berth with a runtime draft.");
            Assert.That(Read<int>("activePortFacility"), Is.EqualTo(-1));
            Assert.That(order.TotalUnits, Is.Zero);
            Assert.That(page.childCount, Is.EqualTo(children), "An inactive controller defers the overview render.");
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(treasureBefore));
            yield return new WaitForSecondsRealtime(.45f);
            callback.Invoke();
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ShipPortProgressKey), Is.EqualTo(persisted));
            Assert.That(session.CurrentPort, Is.EqualTo(destination));
        }

        [TestCase("not JSON")]
        [TestCase("null")]
        [TestCase("{\"version\":1,\"ships\":[{\"shipIndex\":0,\"port\":\"灰鸥港\"},{\"shipIndex\":0,\"port\":\"白贝港\"}]}")]
        [TestCase("{\"version\":1,\"ships\":[{\"shipIndex\":0,\"port\":\"不存在的港口\"}]}")]
        [TestCase("{\"version\":2,\"ships\":[{\"shipIndex\":0,\"port\":\"灰鸥港\"}]}")]
        public void MovePort_WithUnreadableShipPortsPreservesRuntimeAndSavedRecordsAndKeepsPanelOpen(string saved)
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.ShipPortProgressKey, saved);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "灰鸥港");
            string before = JsonUtility.ToJson(session.CaptureShipPortProgress());
            VoyageSupplyOrder order = Read<VoyageSupplyOrder>("pendingSupplyOrder");
            order.Water = 70;
            Write("activePortFacility", 3);
            OpenUnlocked();
            RectTransform overlay = Read<RectTransform>("gameMasterOverlay");
            Read<Button>("gameMasterPortNextButton").onClick.Invoke();
            Assert.That(Read<Button>("gameMasterMovePortButton").interactable, Is.True);
            Read<Button>("gameMasterMovePortButton").onClick.Invoke();

            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(JsonUtility.ToJson(session.CaptureShipPortProgress()), Is.EqualTo(before));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CurrentPortKey), Is.EqualTo("灰鸥港"));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.ShipPortProgressKey), Is.EqualTo(saved));
            Assert.That(controller.IsGameMasterOpen, Is.True);
            Assert.That(Read<RectTransform>("gameMasterOverlay"), Is.SameAs(overlay));
            Assert.That(Read<int>("activePortFacility"), Is.EqualTo(3));
            Assert.That(order.Water, Is.EqualTo(70));
        }

        [UnityTest]
        public IEnumerator ClosedPanel_RejectsStoredNewActionCallbacksAfterCooldown()
        {
            OpenUnlocked();
            Read<Button>("gameMasterPortNextButton").onClick.Invoke();
            Button.ButtonClickedEvent unlock = Read<Button>("gameMasterUnlockComponentsButton").onClick;
            Button.ButtonClickedEvent level = Read<Button>("gameMasterLevelTenButton").onClick;
            Button.ButtonClickedEvent move = Read<Button>("gameMasterMovePortButton").onClick;
            Invoke("CloseGameMaster");
            yield return new WaitForSecondsRealtime(.45f);
            unlock.Invoke();
            level.Invoke();
            move.Invoke();
            AssertNewGameMasterProgressUnchanged();
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey), Is.False);
        }

        [UnityTest]
        public IEnumerator RefreshShop_PersistsNewBatchAndRetainsPurchasesCrewAndOtherPorts()
        {
            OpenUnlocked();
            Button refresh = Read<Button>("gameMasterRefreshShopButton");
            refresh.onClick.Invoke();
            refresh.onClick.Invoke();
            string key = CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港");
            string scope = PlayerPrefs.GetString(CoreGameplayDemoController.GameMasterShopScopeKey);
            Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
            Assert.That(scope.Length, Is.EqualTo(32));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.ComponentShopPurchaseKey(oldOffer.Id)), Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            var refreshed = PortComponentShopCatalog.BuildStock(componentArt, session.CurrentPort, CoreVoyageSession.GameClockOrigin, 1, scope);
            Assert.That(refreshed.Any(item => item.Id == oldOffer.Id), Is.False);
            Assert.That(PortComponentShopCatalog.IsCurrentOffer(oldOffer, session.CurrentPort, CoreVoyageSession.GameClockOrigin, 1, scope), Is.False);
            Invoke("CloseGameMaster");
            Invoke("OpenGameMaster");
            Assert.That((int)InvokeStatic("ComponentShopRefreshRevision", "灰鸥港"), Is.EqualTo(1));
            Assert.That((string)InvokeStatic("ComponentShopRefreshScope"), Is.EqualTo(scope));
            yield return null;
            foreach (IntPreference preference in protectedInts) preference.AssertUnchanged();
        }

        [UnityTest]
        public IEnumerator RefreshMarket_RealUnlockAndButtonResetOnlyThisPortsMonthLedgerWithSharedCooldown()
        {
            DateTime now = CoreVoyageSession.GameClockOrigin.AddMonths(1).Date.AddHours(12);
            const string port = "灰鸥港";
            var receipts = new List<IntPreference>();
            var expectedReceipts = new Dictionary<string, int>();
            var cargoPreferences = new List<IntPreference>();
            string warehouseKey = CoreGameplayDemoController.PortWarehouseCargoKey;
            bool warehouseExisted = PlayerPrefs.HasKey(warehouseKey);
            string warehouseBefore = PlayerPrefs.GetString(warehouseKey, string.Empty);
            try
            {
                Write("marketTimeProvider", (Func<DateTime>)(() => now));
                // Every harbor and previous month have distinct fixture receipts.
                // Preserve presence as well as values, even if an assertion fails.
                for (int harbor = 0; harbor < CoreVoyageSession.DestinationPorts.Count; harbor++)
                foreach (int offset in new[] { -1, 0 })
                for (int item = 0; item < PortMarketCatalog.AllItems.Count; item++)
                {
                    string key = CoreGameplayDemoController.MarketPurchaseKey(CoreVoyageSession.DestinationPorts[harbor],
                        now.AddMonths(offset), PortMarketCatalog.AllItems[item]);
                    receipts.Add(IntPreference.Read(key));
                    int purchased = 11 + harbor * 20 + item + (offset < 0 ? 200 : 0);
                    expectedReceipts[key] = purchased;
                    PlayerPrefs.SetInt(key, purchased);
                }
                session.RestoreProfile(1234.5678m, port);
                ProfileGoldPersistence.Save(session.ProfileGold);
                PropertyInfo[] cargoFields = typeof(VoyageCargo).GetProperties()
                    .Where(property => property.PropertyType == typeof(int) && property.CanWrite)
                    .OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
                for (int index = 0; index < cargoFields.Length; index++)
                {
                    PropertyInfo property = cargoFields[index];
                    property.SetValue(session.Cargo, index + 1);
                    string key = CoreGameplayDemoController.CargoKey(property.Name);
                    cargoPreferences.Add(IntPreference.Read(key));
                    PlayerPrefs.SetInt(key, index + 1);
                }
                Assert.That(session.PortWarehouse.Restore("v1;灰鸥港,water,83;灰鸥港,wood,9;白贝港,food,77"), Is.True);
                string warehouse = session.PortWarehouse.Serialize();
                PlayerPrefs.SetString(warehouseKey, warehouse);
                PlayerPrefs.Save();
                int[] cargo = cargoFields.Select(property => (int)property.GetValue(session.Cargo)).ToArray();
                string treasure = JsonUtility.ToJson(session.CaptureTreasureProgress());
                string decimalBalance = PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey);
                IntPreference wholeBalance = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
                var prices = PortMarketCatalog.AllItems.Select(item => new
                {
                    Item = item, Buy = PortMarketCatalog.BuyUnitPrice(port, item, now),
                    Sell = PortMarketCatalog.SellUnitPrice(port, item, now),
                    Stock = PortMarketCatalog.DayState(port, item, now).Stock,
                }).ToArray();

                Invoke("OpenGameMaster");
                yield return null;
                Toggle unlock = Read<Toggle>("gameMasterUnlockToggle");
                Button market = Read<Button>("gameMasterRefreshMarketButton");
                Button.ButtonClickedEvent callback = market.onClick;
                Assert.That(unlock.isOn, Is.False);
                ClickGameMasterGraphic(market);
                callback.Invoke(); // Even a stale callback cannot bypass the lock.
                foreach (var receipt in expectedReceipts)
                    Assert.That(PlayerPrefs.GetInt(receipt.Key), Is.EqualTo(receipt.Value), receipt.Key);
                ClickGameMasterGraphic(unlock);
                Assert.That(unlock.isOn, Is.True);
                Assert.That(market.IsInteractable(), Is.True);
                ClickGameMasterGraphic(market);

                foreach (var receipt in expectedReceipts)
                {
                    bool reset = PortMarketCatalog.AllItems.Any(item => receipt.Key == CoreGameplayDemoController.MarketPurchaseKey(port, now, item));
                    Assert.That(PlayerPrefs.HasKey(receipt.Key), Is.EqualTo(!reset), receipt.Key);
                    Assert.That(PlayerPrefs.GetInt(receipt.Key), Is.EqualTo(reset ? 0 : receipt.Value), receipt.Key);
                }
                Assert.That(market.interactable, Is.False);
                // Reseeding a receipt makes a duplicate deletion observable.
                string waterKey = CoreGameplayDemoController.MarketPurchaseKey(port, now, PortMarketItem.Water);
                PlayerPrefs.SetInt(waterKey, 7);
                callback.Invoke();
                Read<Button>("gameMasterRefreshShopButton").onClick.Invoke();
                Assert.That(PlayerPrefs.GetInt(waterKey), Is.EqualTo(7), "Duplicate market callbacks must respect cooldown.");
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey(port)), Is.Zero,
                    "Market and component-shop refresh share one command cooldown.");
                yield return new WaitForSecondsRealtime(.45f);
                Invoke("RefreshGameMasterControls"); // The isolated controller stays inactive; no LateUpdate is running.
                ClickGameMasterGraphic(market);
                Assert.That(PlayerPrefs.HasKey(waterKey), Is.False);

                Assert.That(session.ProfileGold, Is.EqualTo(1234.5678m));
                Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(1234.5678m));
                Assert.That(PlayerPrefs.GetString(ProfileGoldPersistence.DecimalKey), Is.EqualTo(decimalBalance));
                wholeBalance.AssertUnchanged();
                CollectionAssert.AreEqual(cargo, cargoFields.Select(property => (int)property.GetValue(session.Cargo)).ToArray());
                for (int index = 0; index < cargoFields.Length; index++)
                    Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey(cargoFields[index].Name)), Is.EqualTo(index + 1));
                Assert.That(session.PortWarehouse.Serialize(), Is.EqualTo(warehouse));
                Assert.That(PlayerPrefs.GetString(warehouseKey), Is.EqualTo(warehouse));
                Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(treasure));
                Assert.That(session.CurrentPort, Is.EqualTo(port));
                Assert.That((DateTime)Invoke("get_MarketNow"), Is.EqualTo(now));
                foreach (var price in prices)
                {
                    Assert.That(PortMarketCatalog.BuyUnitPrice(port, price.Item, now), Is.EqualTo(price.Buy));
                    Assert.That(PortMarketCatalog.SellUnitPrice(port, price.Item, now), Is.EqualTo(price.Sell));
                    Assert.That(PortMarketCatalog.DayState(port, price.Item, now).Stock, Is.EqualTo(price.Stock));
                    int restored = (int)Invoke("MarketRemaining", port, price.Item, now, 0);
                    Assert.That(restored, Is.EqualTo(Math.Min(price.Stock, PortMarketCatalog.MaxPurchaseQuota(price.Item, 0))));
                }
                NavalBattleVisualCapture.Capture(controller, "41_Game_Master_Market_Refreshed.png",
                    "真实解锁及刷新：只恢复本港本月九类采购额度，精确金币、货舱和分港仓储均未变", "Game Master Sheet");
                Invoke("CloseGameMaster");
                PlayerPrefs.SetInt(waterKey, 9);
                callback.Invoke();
                Assert.That(PlayerPrefs.GetInt(waterKey), Is.EqualTo(9), "A closed GM panel cannot run a stored callback.");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                foreach (IntPreference receipt in receipts) receipt.Restore();
                foreach (IntPreference preference in cargoPreferences) preference.Restore();
                if (warehouseExisted) PlayerPrefs.SetString(warehouseKey, warehouseBefore);
                else PlayerPrefs.DeleteKey(warehouseKey);
                PlayerPrefs.Save();
            }
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void PhaseChange_RejectsStaleGameMasterCallbacks(CoreDemoPhase phase)
        {
            IntPreference before = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
            OpenUnlocked();
            Read<Button>("gameMasterPortNextButton").onClick.Invoke();
            Read<InputField>("gameMasterGoldInput").text = "100";
            Read<InputField>("gameMasterTreasureFragmentsInput").text = "4";
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            Read<Button>("gameMasterSetGoldButton").onClick.Invoke();
            Read<Button>("gameMasterSetTreasureFragmentsButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshShopButton").onClick.Invoke();
            Read<Button>("gameMasterRefreshMarketButton").onClick.Invoke();
            InvokeNewGameMasterCallbacks();
            Assert.That(session.ProfileGold, Is.EqualTo(1234));
            Assert.That(session.TreasureFragments, Is.Zero);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey), Is.False);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")), Is.Zero);
            AssertNewGameMasterProgressUnchanged();
            before.AssertUnchanged();
            Invoke("CloseGameMaster");
            Invoke("OpenGameMaster");
            Assert.That(controller.IsGameMasterOpen, Is.False);
        }

        [Test]
        public void TutorialAndRevisionOverflow_CannotModifyProgress()
        {
            Write("tutorialActive", true);
            Invoke("OpenGameMaster");
            Assert.That(controller.IsGameMasterOpen, Is.False);
            Write("tutorialActive", false);
            string key = CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港");
            PlayerPrefs.SetInt(key, int.MaxValue);
            OpenUnlocked();
            Button refresh = Read<Button>("gameMasterRefreshShopButton");
            Assert.That(refresh.interactable, Is.False);
            refresh.onClick.Invoke();
            Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(int.MaxValue));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GameMasterShopScopeKey), Is.False);
        }

        [Test]
        public void OpeningOrdinarySettings_DoesNotEnableGameMasterOrChangeProgress()
        {
            IntPreference before = IntPreference.Read(CoreGameplayDemoController.ProfileGoldKey);
            Invoke("OpenPortSettings");
            Assert.That(controller.IsGameMasterOpen, Is.False);
            Assert.That(Read<RectTransform>("portSettingsOverlay"), Is.Not.Null);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GameMasterShopRefreshKey("灰鸥港")), Is.Zero);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GameMasterShopScopeKey), Is.False);
            before.AssertUnchanged();
            Invoke("ClosePortSettings");
        }

        [Test]
        public void DisableClosesModalAndDoesNotRenderOrSaveDuringTeardown()
        {
            OpenUnlocked();
            Read<InputField>("gameMasterGoldInput").text = "42";
            Read<Button>("gameMasterSetGoldButton").onClick.Invoke();
            // Simulate a real facility without loading its art. An erroneous Render
            // during OnDisable would destroy this draft and try to build that UI.
            Write("activePortFacility", 3);
            int children = page.childCount;
            Invoke("OnDisable");
            Assert.That(controller.IsGameMasterOpen, Is.False);
            Assert.That(page.childCount, Is.EqualTo(children));
            Assert.That(underlay.IsInteractable(), Is.True);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(42));
        }

        private void OpenUnlocked()
        {
            Invoke("OpenGameMaster");
            Read<Toggle>("gameMasterUnlockToggle").isOn = true;
        }

        private void InvokeNewGameMasterCallbacks()
        {
            Read<Button>("gameMasterUnlockComponentsButton").onClick.Invoke();
            Read<Button>("gameMasterLevelTenButton").onClick.Invoke();
            Read<Button>("gameMasterMovePortButton").onClick.Invoke();
        }

        private void AssertNewGameMasterProgressUnchanged()
        {
            Assert.That(Read<OwnedShipComponentInventory>("componentInventory").Items, Is.Empty);
            Assert.That(session.AcquiredSpecialCardIds, Is.Empty);
            Assert.That(session.SpecialCardExperience, Is.Empty);
            Assert.That(session.PirateExperience, Is.Zero);
            Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ComponentInventoryKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.AcquiredSpecialCardsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.SpecialCardExperienceKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CurrentPortKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.ShipPortProgressKey), Is.False);
        }

        private static CoreTreasureProgress TreasureProgressFixture()
        {
            return new CoreTreasureProgress {
                pirateExperience = 900, fragments = 8,
                identifiedWeatheredFragments = 3, identifiedGildedFragments = 2, identifiedMoonlitFragments = 1,
                identificationSeed = "GM-IDENTIFICATION-SEED", identificationSequence = 19, moonlitPityCount = 7,
                selectedMapId = "weathered:GM-HELD",
                maps = new[] { new CoreTreasureMapSave { id = "weathered:GM-HELD", seed = "GM-HELD", rankId = "weathered" } },
                usedMaps = new[] { new CoreUsedTreasureMapSave { id = "moonlit:GM-USED", seed = "GM-USED", rankId = "moonlit",
                    departurePort = "白贝港", hasExplorationCoverage = true, explorationCoverage = 45f } },
                completedSeeds = new[] { "GM-COMPLETE" }, issuedSeeds = new[] { "GM-HELD", "GM-USED", "GM-COMPLETE" },
                gems = 7, ancientMaterials = 8, relics = 9, blackPearls = 10, ancientCompasses = 11, lanterns = 12, curses = 13,
            };
        }

        private static string TreasureProgressWithoutFragments(CoreTreasureProgress progress)
        {
            int fragments = progress.fragments;
            try
            {
                progress.fragments = 0;
                return JsonUtility.ToJson(progress);
            }
            finally
            {
                progress.fragments = fragments;
            }
        }

        private static string TreasureProgressWithoutExperience(CoreTreasureProgress progress)
        {
            int experience = progress.pirateExperience;
            try
            {
                progress.pirateExperience = 0;
                return JsonUtility.ToJson(progress);
            }
            finally
            {
                progress.pirateExperience = experience;
            }
        }

        private static void ClickGameMasterGraphic(Selectable target)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)target.transform;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, target.name);
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(target.gameObject), target.name + " is obstructed by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private object Invoke(string method, params object[] args)
        {
            return typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, args);
        }

        private static object InvokeStatic(string method, params object[] args)
        {
            return typeof(CoreGameplayDemoController).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        }

        private T Read<T>(string name)
        {
            return (T)typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        }

        private void Write(string name, object value)
        {
            typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, value);
        }

        private sealed class IntPreference
        {
            private string key;
            private bool existed;
            private int value;

            public static IntPreference Read(string key)
            {
                return new IntPreference { key = key, existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key, int.MinValue) };
            }

            public void Restore()
            {
                if (existed) PlayerPrefs.SetInt(key, value);
                else PlayerPrefs.DeleteKey(key);
            }

            public void AssertUnchanged()
            {
                Assert.That(PlayerPrefs.HasKey(key), Is.EqualTo(existed), key);
                Assert.That(PlayerPrefs.GetInt(key, int.MinValue), Is.EqualTo(value), key);
            }
        }

        private sealed class StringPreference
        {
            private string key;
            private bool existed;
            private string value;

            public static StringPreference Read(string key)
            {
                return new StringPreference { key = key, existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetString(key, string.Empty) };
            }

            public void Restore()
            {
                if (existed) PlayerPrefs.SetString(key, value);
                else PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
