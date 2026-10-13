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
    // The shared fixture snapshots all profile keys. These tests never write a
    // fake encounter, battle hand, inventory reward, or tutorial completion.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator GuidedTutorial_TwoVoyagesTeachNavigatorAmbushThenScriptedBossAndDisplayReward()
        {
            yield return LoadGuidedTutorialScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var seen = new HashSet<string>();
            var captureKeys = new HashSet<string>();
            AssertGuidedDefaultDecks(controller);
            yield return DriveGuidedTutorial(controller, "TutorialBoss", seen, true, captureKeys);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(controller.Session.GuidedTutorialLeg, Is.EqualTo(CoreGuidedTutorialContent.NavigatorVoyageLeg));
            CollectionAssert.AreEqual(new[] { CoreGuidedTutorialContent.NavigatorEventId, CoreGuidedTutorialContent.AmbushEventId },
                controller.Session.GuidedTutorialCompletedEncounterIds,
                "The same second voyage must resolve the navigator event and leave the ambush before reaching the boss.");
            Assert.That(controller.Session.GuidedTutorialAmbushTriggered, Is.True);
            Assert.That(controller.Session.GuidedTutorialRandomEventTriggered, Is.False);
            Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
            AssertGuidedDefaultDecks(controller);
            yield return DriveGuidedTutorial(controller, "Warehouse", seen, true, captureKeys);
            AssertGuidedDefaultDecks(controller);
            Assert.That(controller.IsTutorialActive, Is.True);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey), Is.EqualTo(1));
            Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.True);
            Assert.That(controller.WarehouseCollection.IsDisplayed(CoreWarehouseCollection.TutorialTreasureId), Is.False);
            Assert.That(controller.Session.TreasureFragments, Is.EqualTo(1), "The guaranteed boss fragment banks with the final return.");
            OwnedShipComponent storedReward = controller.OwnedComponents.Single(item =>
                item.ComponentId == CoreGameplayDemoController.GuidedTutorialRewardComponentId);
            Assert.That(storedReward.ShipIndex, Is.EqualTo(-1), "Settlement must put the part in inventory, not auto-equip it.");
            Assert.That(controller.EquippedComponent(ComponentSlot.Bow), Is.Null,
                "A new save starts with a bare hull; the tutorial reward is not installed before the equipment lesson.");
            string rewardInstanceId = storedReward.InstanceId;
            int componentCount = controller.OwnedComponents.Count;
            decimal settledGold = controller.Session.ProfileGold;
            string settledChart = PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey);
            string[] settledCollections = controller.WarehouseCollection.OwnedIds.ToArray();
            string collectedAcrossPorts = PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey);

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("Warehouse"));
            Assert.That(controller.Session.TreasureFragments, Is.EqualTo(1), "Reloading the reward lesson must preserve, not duplicate, the fragment.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(settledGold));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey), Is.EqualTo(settledChart));
            CollectionAssert.AreEqual(settledCollections, controller.WarehouseCollection.OwnedIds,
                "Reloading preserves the tutorial trophy and any earned recruitment or port milestones without duplicating them.");
            yield return DriveGuidedTutorial(controller, "Shipyard", seen, true, captureKeys);
            Assert.That(controller.IsTutorialActive, Is.True, "Displaying a keepsake must not skip the final equipment lesson.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey), Is.False);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey), Is.EqualTo(1));
            yield return DriveGuidedTutorial(controller, "SelectRewardComponent", seen, false);
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == rewardInstanceId).ShipIndex, Is.EqualTo(-1));

            // Reload during the final page keeps the reward and resumes the
            // shipyard lesson; neither visiting the page nor loading equips it.
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Shipyard));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(componentCount));
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == rewardInstanceId).ShipIndex, Is.EqualTo(-1));
            yield return DriveGuidedTutorial(controller, "EquipRewardComponent", seen, false);
            Assert.That(controller.IsTutorialActive, Is.True);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero,
                "Selecting an inventory item is not the same as fitting it.");
            yield return DriveGuidedTutorial(controller, "Complete", seen, true, captureKeys);

            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(GameObject.Find("Shipyard Panel"), Is.Not.Null);
            Assert.That(seen, Does.Contain("FirstMandatory"));
            Assert.That(seen, Does.Contain("FirstOptional"));
            Assert.That(seen, Does.Contain("FirstCrewEvent"));
            Assert.That(seen, Does.Contain("SecondPier"));
            Assert.That(seen, Does.Contain("SecondRoute"));
            Assert.That(seen, Does.Contain("SecondDeparture"));
            foreach (string required in new[] { "SecondCrewEvent", "SecondAmbush" })
                Assert.That(seen, Does.Contain(required), "The guide must route through " + required);
            foreach (string retired in new[] { "Workshop", "BuildDeck", "SecondRetreat", "SecondSettlement", "ThirdPier", "ThirdRoute", "ThirdDeparture" })
                Assert.That(seen, Does.Not.Contain(retired), "The guide must not require " + retired);
            Assert.That(seen, Does.Contain("TutorialBoss"));
            Assert.That(seen, Does.Contain("Warehouse"));
            Assert.That(seen, Does.Contain("SelectTreasure"));
            Assert.That(seen, Does.Contain("DisplayTreasure"));
            Assert.That(seen, Does.Contain("Shipyard"));
            Assert.That(seen, Does.Contain("ShipEquipment"));
            Assert.That(seen, Does.Contain("SelectRewardComponent"));
            Assert.That(seen, Does.Contain("EquipRewardComponent"));
            Assert.That(seen, Does.Not.Contain("UnequipRewardComponent"), "A new save must first equip the actual stored drop.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialDeckEditedKey, 0), Is.Zero,
                "The ordinary starter decks must be sufficient without teaching or recording a deck edit.");
            AssertGuidedDefaultDecks(controller);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey), Is.EqualTo(5));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey), Is.False);
            Assert.That(controller.WarehouseCollection.DisplayedAt(0), Is.EqualTo(CoreWarehouseCollection.TutorialTreasureId));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.WarehouseOwnedKey), Is.EqualTo(collectedAcrossPorts));
            string displayedTreasures = controller.WarehouseCollection.ExportDisplay();
            ComponentArtEntry reward = new ComponentArtCatalog().GetById(CoreGameplayDemoController.GuidedTutorialRewardComponentId);
            Assert.That(reward, Is.Not.Null);
            Assert.That(reward.Rarity, Is.EqualTo(ComponentRarity.Common));
            Assert.That(controller.EquippedComponent(reward.Slot).Id, Is.EqualTo(reward.Id));
            Assert.That(controller.InstalledShipComponent(controller.Session.SelectedShipIndex, reward.Slot).InstanceId,
                Is.EqualTo(rewardInstanceId), "The exact rewarded inventory instance must be installed, not a newly granted copy.");
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(componentCount));
            string chart = PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey);
            Assert.That(chart, Is.Not.Empty, "The final drawing must become a real persistent chart record.");
            Assert.That(chart, Does.Contain("\"tutorialWaters\":true"));
            decimal gold = controller.Session.ProfileGold;
            string[] components = Enum.GetValues(typeof(ComponentSlot)).Cast<ComponentSlot>()
                .Select(slot => controller.EquippedComponent(slot)?.Id).ToArray();

            // An ordinary reload cannot pay the tutorial battle or component twice.
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.IsTutorialActive, Is.False);
            AssertGuidedDefaultDecks(controller);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.Session.TreasureFragments, Is.EqualTo(1), "Completing and reloading the tutorial cannot pay its fragment twice.");
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(componentCount));
            CollectionAssert.AreEqual(settledCollections, controller.WarehouseCollection.OwnedIds);
            Assert.That(controller.WarehouseCollection.ExportDisplay(), Is.EqualTo(displayedTreasures));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey), Is.EqualTo(chart));
            Assert.That(Enum.GetValues(typeof(ComponentSlot)).Cast<ComponentSlot>()
                .Select(slot => controller.EquippedComponent(slot)?.Id), Is.EqualTo(components));
            foreach (string facility in new[] { "Warehouse", "Shipyard", "Tavern", "ComponentShop", "Maintenance", "CardWorkshop", "Voyage" })
                Assert.That(RequireGuidedButton("Port Hotspot " + facility).IsInteractable(), Is.True,
                    "All normal facilities unlock after the actual equipment action.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuidedTutorial_RedFocusGuidesDepartureHeadingSailAndEventWithoutDistanceLesson()
        {
            yield return LoadGuidedTutorialScene(enterDeparture: true);
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("FirstDeparture"));
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(ActiveButton("Tutorial Primary"), Is.Null, "The dock opens the normal departure screen.");
            AssertGuidedFocusTarget("Start Voyage");
            NavalBattleVisualCapture.Capture(controller, "70_Guide_Red_Focus_Departure.png",
                "正常出航入口以红框与箭头引导，不再置暗其它控件", "Start Voyage");
            foreach (int route in new[] { 1, 2 })
            {
                Button locked = RequireGuidedButton("Route " + route);
                Assert.That(locked.IsInteractable(), Is.False);
                AssertGuidedLockHasNoTint(locked);
                locked.onClick.Invoke();
            }
            Assert.That(controller.Session.SelectedRouteIndex, Is.Zero);
            ClickGuidedButton("Start Voyage");
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                Is.EqualTo(GuidedTutorialNavigationInputStep.Heading));
            AssertGuidedFocusTarget("Heading East");
            NavalBattleVisualCapture.Capture(controller, "71_Guide_Red_Focus_Heading.png",
                "第一步：点击红框和箭头标记的向东按钮", "Heading East");
            foreach (string direction in new[] { "North", "NorthEast", "SouthEast", "South", "SouthWest", "West", "NorthWest" })
            {
                Button locked = RequireGuidedButton("Heading " + direction);
                Assert.That(locked.IsInteractable(), Is.False);
                AssertGuidedLockHasNoTint(locked);
                locked.onClick.Invoke();
            }
            Assert.That(controller.Session.Heading, Is.EqualTo(SailingDirection.East));
            Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>().IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Map Course Input").GetComponent<Image>().raycastTarget, Is.False);
            float beforeDistance = controller.Session.TotalDistanceNm;
            RequireGuidedButton("Execute Sail").onClick.Invoke();
            Assert.That(controller.Session.TotalDistanceNm, Is.EqualTo(beforeDistance),
                "The red-outline lesson still requires selecting heading and distance before sailing.");

            ClickGuidedButton("Heading East");
            yield return null;
            Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                Is.EqualTo(GuidedTutorialNavigationInputStep.Sail));
            Slider distanceSlider = GameObject.Find("Distance Slider").GetComponent<Slider>();
            Assert.That(distanceSlider.IsInteractable(), Is.False);
            Assert.That(distanceSlider.GetComponent<GuidedTutorialDistanceInput>(), Is.Null);
            distanceSlider.onValueChanged.Invoke(90f);
            ExecuteEvents.Execute(distanceSlider.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(distanceSlider.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            Assert.That(controller.Session.PlannedDistanceNm, Is.EqualTo(CoreGuidedTutorialContent.PlannedDistanceNm));
            Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                Is.EqualTo(GuidedTutorialNavigationInputStep.Sail));
            AssertGuidedFocusTarget("Execute Sail");
            NavalBattleVisualCapture.Capture(controller, "73_Guide_Red_Focus_Execute.png",
                "选择航向后直接引导执行航行，教学航程自动设定", "Execute Sail");
            Button.ButtonClickedEvent staleSail = RequireGuidedButton("Execute Sail").onClick;
            ClickGuidedButton("Execute Sail");
            yield return null;
            yield return WaitForGuidedNavigationPresentation();
            Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo("tutorial_first_event"));
            Assert.That(controller.Session.ActiveEncounter.Mandatory, Is.True);
            Assert.That(ActiveButton("Skip Encounter"), Is.Null);
            AssertGuidedFocusTarget("Encounter Choice 0");
            NavalBattleVisualCapture.Capture(controller, "74_Guide_Red_Focus_Encounter.png",
                "后续事件同样用红框和箭头指向真实可点击选项", "Encounter Choice 0");
            float sailed = controller.Session.TotalDistanceNm;
            staleSail.Invoke();
            yield return null;
            Assert.That(controller.Session.TotalDistanceNm, Is.EqualTo(sailed));
            Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo("tutorial_first_event"));
            ClickGuidedButton("Encounter Choice 0");
            yield return null;
            Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                Is.EqualTo(GuidedTutorialNavigationInputStep.Heading),
                "The next sailing segment repeats the same heading, distance, and execute sequence.");
            AssertGuidedFocusTarget("Heading East");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuidedTutorial_OptionalEncounterAllowsNormalChoiceAsWellAsSkip()
        {
            yield return LoadGuidedTutorialScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "FirstOptional", new HashSet<string>(), false);
            int sailingActions = 0;
            while (controller.Session.Phase == CoreDemoPhase.Sailing && sailingActions++ < 8)
            {
                yield return CompleteGuidedNavigationSetup(controller);
                ClickGuidedButton("Execute Sail");
                yield return null;
                yield return WaitForGuidedNavigationPresentation();
            }
            yield return WaitForGuidedNavigationPresentation();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.OptionalEventId));
            Assert.That(controller.Session.ActiveEncounter.Mandatory, Is.False);
            Assert.That(RequireGuidedButton("Skip Encounter").IsInteractable(), Is.True,
                "A non-mandatory encounter must still permit passing without spending time.");
            Assert.That(RequireGuidedButton("Encounter Choice 0").IsInteractable(), Is.True,
                "Teaching an optional event must not force skipping its ordinary available option.");
            AssertGuidedFocusTarget("Skip Encounter");
            AssertGuidedFocusTarget("Encounter Choice 0");
            int gold = controller.Session.Cargo.Gold;
            int water = controller.Session.Cargo.Water;
            int food = controller.Session.Cargo.Food;
            double hours = controller.Session.TotalHours;
            Assert.That(controller.Session.HasCook, Is.False);
            double rate = controller.Session.CrewCountValue / 24d;
            int expectedRations = (int)(Math.Ceiling((hours + 1d) * rate - 1e-9d) - Math.Ceiling(hours * rate - 1e-9d));
            ClickGuidedButton("Encounter Choice 0");
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("FirstCrewEvent"));
            Assert.That(controller.Session.GuidedTutorialCompletedEncounters, Is.EqualTo(2));
            Assert.That(controller.Session.Cargo.Gold, Is.EqualTo(gold + 12));
            Assert.That(controller.Session.TotalHours, Is.EqualTo(hours + 1d).Within(.000001d));
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water - expectedRations));
            Assert.That(controller.Session.Cargo.Food, Is.EqualTo(food - expectedRations));
            AssertGuidedFocusTarget("Heading East");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GuidedTutorial_ReloadKeepsRecruitmentButRestartsUnsettledLegWithoutRewards()
        {
            yield return LoadGuidedTutorialScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "SecondPier", new HashSet<string>(), false);
            Assert.That(controller.Session.HasNavigator, Is.True);
            decimal gold = controller.Session.ProfileGold;
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("SecondPier"));
            Assert.That(controller.Session.HasNavigator, Is.True);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold), "Recruitment is not charged again during resume.");

            decimal departureGold = controller.Session.ProfileGold;
            int departureTravelCash = controller.Session.Cargo.Gold;
            yield return DriveGuidedTutorial(controller, "TutorialBoss", new HashSet<string>(), false);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
            Assert.That(controller.Session.GuidedTutorialCompletedEncounters, Is.EqualTo(2));
            AssertGuidedDefaultDecks(controller);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
            Assert.That(ProfileGoldPersistence.Load(), Is.EqualTo(departureGold),
                "Starting a tutorial leg must keep the pre-departure port wallet snapshot.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Gold")), Is.EqualTo(departureTravelCash),
                "Departure travel-cash transfer must not replace the tutorial replay snapshot.");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("SecondPier"));
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(departureGold));
            Assert.That(controller.Session.Cargo.Gold, Is.EqualTo(departureTravelCash));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator GuidedTutorial_LostBossOpensNormalRecoveryServicesAndCanRetry()
        {
            yield return LoadGuidedTutorialScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "SecondPier", new HashSet<string>(), false);
            // Explicit legacy boundary: the navigator voyage was committed,
            // while the old third-voyage checkpoint still awaits its boss.
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialNavigatorEventKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 3);
            PlayerPrefs.Save();
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "TutorialBoss", new HashSet<string>(), false);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            ClickGuidedButton("Encounter Choice 0");
            yield return null;
            // Explicit post-entry fault injection, not the normal tutorial
            // route: legal actions must still pay ram recoil and sink this hull.
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(controller.Session, 1);
            typeof(CoreGameplayDemoController).GetMethod("Render", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
            yield return null;
            int actions = 0;
            while (controller.Session.Phase == CoreDemoPhase.Battle && actions++ < 24)
            {
                yield return PlayGuidedBattleAction(controller);
                yield return null;
                float deadline = Time.realtimeSinceStartup + 12f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    AdvanceRamComicIfWaiting(controller);
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            }
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(controller.Session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
            Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.False);
            Assert.That(ActiveButton("Draw Sea Chart"), Is.Null, "A lost ship cannot yield the final tutorial chart.");
            ClickGuidedButton("Return To Port");
            yield return null;

            Assert.That(controller.IsTutorialActive, Is.True);
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("ThirdPier"));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey), Is.EqualTo(3));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialRecoveryKey), Is.EqualTo(1));
            Assert.That(RequireGuidedButton("Port Hotspot Maintenance").IsInteractable(), Is.True);
            Assert.That(RequireGuidedButton("Port Hotspot Warehouse").IsInteractable(), Is.True);
            Assert.That(RequireGuidedButton("Port Hotspot ComponentShop").IsInteractable(), Is.False);
            Assert.That(RequireGuidedButton("Port Hotspot Tavern").IsInteractable(), Is.False);
            // Both exits used to leave a stale route/departure step on the hub,
            // locking its pier hotspot after a normal recovery-service visit.
            yield return CheckGuidedRecoveryPierRoundTrip(controller, 3, false);
            yield return CheckGuidedRecoveryPierRoundTrip(controller, 3, true);
            ClickGuidedButton("Port Hotspot Maintenance");
            yield return null;
            Assert.That(GameObject.Find("Port Investment Panel"), Is.Not.Null);
            ClickGuidedButton("Open Port Market");
            yield return null;
            Assert.That(GameObject.Find("Market Panel"), Is.Not.Null);
            decimal gold = controller.Session.ProfileGold;
            int water = controller.Session.Cargo.Water;
            int purchaseQuantity = PortMarketCatalog.PurchaseStep(PortMarketItem.Water);
            decimal price = PortMarketCatalog.CalculateBuyCost(controller.Session.CurrentPort, PortMarketItem.Water, controller.Session.GameDateTime, purchaseQuantity);
            ClickGuidedButton("Market Item Icon water");
            yield return null;
            ClickGuidedButton("Market Buy water");
            yield return null;
            ClickGuidedButton("Confirm Market Buy water");
            yield return null;
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - price), "Recovery supplies use the normal market price.");
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water + purchaseQuantity));
            ClickGuidedButton("Return To Port Hub");
            yield return null;
            ClickGuidedButton("Port Hotspot Warehouse");
            yield return null;
            Assert.That(ActiveButton("Warehouse Tab Basics"), Is.Null);
            ClickGuidedButton("Warehouse Tab Storage");
            yield return null;
            int wood = controller.Session.Cargo.Wood;
            int soldWood = Math.Min(10, wood);
            Assert.That(soldWood, Is.GreaterThan(0), "The failed-voyage recovery must retain ordinary saleable starter materials.");
            decimal saleGold = PortMarketCatalog.CalculateSellRevenue(controller.Session.CurrentPort, PortMarketItem.Wood, controller.Session.GameDateTime, soldWood);
            ClickGuidedButton("Warehouse Storage Row wood");
            yield return null;
            ClickGuidedButton("Warehouse Sell Cargo");
            yield return null;
            yield return DragWarehouseSlider(GameObject.Find("Warehouse Ship Sale Slider").GetComponent<Slider>(), soldWood);
            Assert.That(GameObject.Find("Warehouse Stored Sale Slider").GetComponent<Slider>().value, Is.Zero);
            ClickGuidedButton("Confirm Warehouse Cargo Sale");
            yield return null;
            Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(wood - soldWood));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold - price + saleGold),
                "Tutorial recovery must use the real warehouse sale and its normal material price.");
            decimal recoveredGold = controller.Session.ProfileGold;
            int recoveredWater = controller.Session.Cargo.Water;
            ClickGuidedButton("Return To Port Hub");
            yield return null;

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(recoveredGold));
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(recoveredWater));
            Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(wood - soldWood));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialRecoveryKey), Is.EqualTo(1));
            Assert.That(RequireGuidedButton("Port Hotspot Maintenance").IsInteractable(), Is.True);

            // Explicit boundary fixture: restore an exhausted-cargo checkpoint,
            // such as a legacy save or repeated failed expeditions. The sale
            // above used only retained starter wood.
            // Everything after this one saved-resource setup again uses real UI.
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 0);
            PlayerPrefs.Save();
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.Cargo.Water, Is.Zero);
            yield return DriveGuidedTutorial(controller, "ThirdSail", new HashSet<string>(), false);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.GuidedTutorialLeg, Is.EqualTo(CoreGuidedTutorialContent.BossVoyageLeg));
            AssertGuidedDefaultDecks(controller);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialRecoveryKey, 0), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            Assert.That(RequireGuidedButton("Execute Sail").IsInteractable(), Is.False,
                "An impossible sailing action must not trap the player behind the tutorial gate.");
            Assert.That(RequireGuidedButton("Safe Return").IsInteractable(), Is.True,
                "The ordinary emergency return must remain available when supplies are exhausted.");
            float distance = controller.Session.TotalDistanceNm;
            RequireGuidedButton("Execute Sail").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.TotalDistanceNm, Is.EqualTo(distance));
            ClickGuidedButton("Safe Return");
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(controller.Session.Settlement.ProtectionRatio, Is.EqualTo(.35f).Within(.00001f),
                "Exhausted supplies use ordinary 35% emergency towing, not a tutorial exemption.");
            ClickGuidedButton("Return To Port");
            yield return null;
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("ThirdPier"));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey), Is.EqualTo(3));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialRecoveryKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
            Assert.That(controller.IsTutorialActive, Is.True);
            Assert.That(RequireGuidedButton("Port Hotspot Maintenance").IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuidedTutorial_ExhaustedMandatoryEncounterUsesNormalTowAndKeepsFirstLessonIncomplete()
        {
            // Explicit saved-resource boundary fixture: carry only enough to
            // reach the event, then exhaust any prepaid fractional coverage.
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 4);
            PlayerPrefs.Save();
            yield return LoadGuidedTutorialScene(enterDeparture: true);
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(4));
            ClickGuidedButton("Start Voyage");
            yield return null;
            yield return CompleteGuidedNavigationSetup(controller);
            ClickGuidedButton("Execute Sail");
            yield return null;

            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo("tutorial_first_event"));
            ExhaustGuidedRecoveryWaterFixture(controller);
            yield return null;
            Assert.That(controller.Session.Cargo.Water, Is.Zero);
            Assert.That(controller.Session.IsChoiceAvailable(0), Is.False);
            Assert.That(RequireGuidedButton("Encounter Choice 0").IsInteractable(), Is.False);
            Assert.That(ActiveButton("Skip Encounter"), Is.Null);
            RequireGuidedButton("Encounter Choice 0").onClick.Invoke();
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(controller.Session.GuidedTutorialCompletedEncounters, Is.Zero);
            Assert.That(RequireGuidedButton("Emergency Encounter Return").IsInteractable(), Is.True,
                "The mandatory-event lesson must permit the ordinary rescue when its only choice is unaffordable.");
            ClickGuidedButton("Emergency Encounter Return");
            yield return null;

            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(controller.Session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(controller.Session.Settlement.ProtectionRatio, Is.EqualTo(.35f).Within(.00001f));
            Assert.That(controller.Session.GuidedTutorialCompletedEncounters, Is.Zero);
            yield return ReturnToGuidedResourceRecovery(controller, 0, "FirstDeparture");
            yield return CheckGuidedRecoveryPierRoundTrip(controller, 0, false);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GuidedTutorial_ExhaustedBossUsesBattleTowWithoutAwardingVictory()
        {
            yield return LoadGuidedTutorialScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "SecondPier", new HashSet<string>(), false);
            // Preserve the earned checkpoint and recruitment. The actual
            // journey reaches the boss before the exhausted-water boundary is arranged.
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 4);
            // The exhausted-boss fixture starts after the newly required navigator voyage.
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialNavigatorEventKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 3);
            PlayerPrefs.Save();
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(4));
            Assert.That(controller.Session.HasNavigator, Is.True);
            yield return DriveGuidedTutorial(controller, "TutorialBoss", new HashSet<string>(), false);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
            ExhaustGuidedRecoveryWaterFixture(controller);
            yield return null;
            Assert.That(controller.Session.IsChoiceAvailable(0), Is.True,
                "The zero-hour battle choice must not require supplies already spent reaching the boss.");
            AssertGuidedFocusTarget("Encounter Choice 0");
            ClickGuidedButton("Encounter Choice 0");
            yield return null;

            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(controller.Session.GuidedTutorialAmbushTriggered, Is.False);
            Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.False);
            Assert.That(controller.Session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(controller.Session.Cargo.Water, Is.Zero);
            Assert.That(controller.Session.CanRetreatBattle, Is.False);
            Assert.That(controller.Session.LastBattleRetreated, Is.False);
            Button ordinaryRetreat = ActiveButton("Retreat Battle");
            if (ordinaryRetreat != null) Assert.That(ordinaryRetreat.IsInteractable(), Is.False);
            Assert.That(RequireGuidedButton("Emergency Battle Return").IsInteractable(), Is.True,
                "A real boss battle with insufficient retreat supplies must still offer normal emergency towing.");
            ClickGuidedButton("Emergency Battle Return");
            yield return null;

            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(controller.Session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(controller.Session.Settlement.ProtectionRatio, Is.EqualTo(.35f).Within(.00001f));
            Assert.That(controller.Session.LastBattleRetreated, Is.False,
                "Emergency towing must not fabricate a normal paid retreat or tutorial boss victory.");
            Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.False);
            Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.False);
            yield return ReturnToGuidedResourceRecovery(controller, 3, "ThirdPier");
            Assert.That(controller.Session.HasNavigator, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GuidedTutorial_LegacyDeckCheckpointsResumeBossDepartureWithoutResettingSavedDecks()
        {
            yield return LoadGuidedTutorialScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "SecondPier", new HashSet<string>(), false);
            decimal earnedGold = controller.Session.ProfileGold;
            var savedDecks = new Dictionary<NavalBattleStage, int[]>();
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
            {
                // An explicit legacy-save fixture: the removed lesson permitted
                // reducing a deck from ten to nine cards. Migration retains those
                // cards and appends the first default with a spare owned copy.
                int[] cards = controller.Session.GetBattleDeck(stage).Take(9).ToArray();
                int filler = new CoreVoyageSession().GetBattleDeck(stage).First(index => cards.Count(value => value == index) < 2);
                savedDecks.Add(stage, cards.Concat(new[] { filler }).ToArray());
                PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(stage), string.Join(",", cards));
            }
            foreach (int legacyCheckpoint in new[] { 3, 4 })
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, legacyCheckpoint);
                PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialDeckEditedKey, legacyCheckpoint == 3 ? 0 : 7);
                PlayerPrefs.Save();
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null;
                yield return null;
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("SecondPier"));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey), Is.EqualTo(2));
                Assert.That(controller.Session.HasNavigator, Is.True);
                Assert.That(controller.Session.ProfileGold, Is.EqualTo(earnedGold), "Migrating must not charge recruitment again.");
                Assert.That(ActiveButton("Guide Confirm Deck"), Is.Null);
                Assert.That(RequireGuidedButton("Port Hotspot CardWorkshop").IsInteractable(), Is.False);
                AssertGuidedFocusTarget("Port Hotspot Voyage");
                foreach (var saved in savedDecks)
                    CollectionAssert.AreEqual(saved.Value, controller.Session.GetBattleDeck(saved.Key),
                        "Removing the lesson must preserve the player's migrated legal deck.");
                var seen = new HashSet<string>();
                yield return DriveGuidedTutorial(controller, "TutorialBoss", seen, false);
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
                Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
                Assert.That(controller.Session.GuidedTutorialCompletedEncounters, Is.Zero);
                Assert.That(seen, Does.Not.Contain("Workshop"));
                Assert.That(seen, Does.Not.Contain("BuildDeck"));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
                Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuidedTutorial_LegacyDisplayedRewardRefitsSameItemWithoutDuplicatingAndResumesAfterUnload()
        {
            // An unfinished older guide auto-fitted this drop. Import the real
            // legacy loadout, then teach removing and fitting that same instance.
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 5);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 1);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Bow),
                CoreGameplayDemoController.GuidedTutorialRewardComponentId);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentConditionKey(ComponentSlot.Bow), 104);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, CoreWarehouseCollection.TutorialTreasureId);
            PlayerPrefs.Save();
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Warehouse));
            OwnedShipComponent reward = controller.OwnedComponents.Single(item =>
                item.ComponentId == CoreGameplayDemoController.GuidedTutorialRewardComponentId);
            string instanceId = reward.InstanceId;
            Assert.That(reward.ShipIndex, Is.EqualTo(controller.Session.SelectedShipIndex));
            Assert.That(reward.Quality, Is.EqualTo(1.04f).Within(.0001f));
            // Exercise a migrated inventory where the rewarded common item is
            // not on page one. Guidance must reveal its ordinary inventory row.
            foreach (ComponentArtEntry extra in new ComponentArtCatalog().Entries.Where(entry =>
                entry.Slot == ComponentSlot.Bow && entry.Id != CoreGameplayDemoController.GuidedTutorialRewardComponentId).Take(6))
                Assert.That(controller.GrantOwnedComponent(extra, 1f), Is.Not.Null);
            int count = controller.OwnedComponents.Count;
            PlayerPrefs.Save();
            var seen = new HashSet<string>();
            yield return DriveGuidedTutorial(controller, "UnequipRewardComponent", seen, false);
            Assert.That(controller.IsTutorialActive, Is.True);
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count));
            AssertGuidedFocusTarget("Shipyard Unequip Component Bow");
            ClickGuidedButton("Shipyard Unequip Component Bow");
            yield return null;
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.SelectRewardComponent));
            Assert.That(controller.InstalledShipComponent(controller.Session.SelectedShipIndex, ComponentSlot.Bow), Is.Null);
            Assert.That(controller.OwnedComponents.Single(item => item.InstanceId == instanceId).ShipIndex, Is.EqualTo(-1));
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Shipyard));
            Assert.That(controller.InstalledShipComponent(controller.Session.SelectedShipIndex, ComponentSlot.Bow), Is.Null,
                "An empty slot must remain empty on reload, not regain a free default item.");
            yield return DriveGuidedTutorial(controller, "Complete", seen, false);
            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(controller.OwnedComponents.Count, Is.EqualTo(count));
            reward = controller.InstalledShipComponent(controller.Session.SelectedShipIndex, ComponentSlot.Bow);
            Assert.That(reward.InstanceId, Is.EqualTo(instanceId));
            Assert.That(reward.Quality, Is.EqualTo(1.04f).Within(.0001f));
            Assert.That(controller.OwnedComponents.Count(item => item.ComponentId ==
                CoreGameplayDemoController.GuidedTutorialRewardComponentId), Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GuidedTutorial_LegacyCompletedCheckpointDoesNotRelockOrGrantRewards()
        {
            // Checkpoint 5 predates the treasure-display extension. Such an
            // alumnus must stay complete even if the newer completion key is absent.
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 5);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.TutorialCompletedKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey);
            PlayerPrefs.Save();
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo("Complete"));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey), Is.EqualTo(5));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
            Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.False,
                "Compatibility completion must not fabricate the new first-victory collectible.");
            Assert.That(GameObject.Find("Guided Tutorial Focus Layer"), Is.Null);
            foreach (string facility in new[] { "Warehouse", "Tavern", "ComponentShop", "Maintenance", "CardWorkshop", "Voyage" })
                Assert.That(RequireGuidedButton("Port Hotspot " + facility).IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // Recovery-only fixture: cumulative ration rounding may leave paid
        // fractional coverage even when the whole-unit cargo count is zero.
        // Match the existing Core exhausted-arrival regression by consuming
        // that real coverage; never replace the encounter or grant resources.
        private static void ExhaustGuidedRecoveryWaterFixture(CoreGameplayDemoController controller)
        {
            CoreVoyageSession session = controller.Session;
            double remainingHours = session.WaterSupplyHours;
            Assert.That(double.IsNaN(remainingHours) || double.IsInfinity(remainingHours), Is.False);
            typeof(CoreVoyageSession).GetMethod("ConsumeHours", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(session, new object[] { remainingHours });
            Assert.That(session.Cargo.Water, Is.Zero);
            Assert.That(session.WaterSupplyHours, Is.EqualTo(0d).Within(1e-8d));
            typeof(CoreGameplayDemoController).GetMethod("Render", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
        }

        private static IEnumerator ReturnToGuidedResourceRecovery(CoreGameplayDemoController controller,
            int checkpoint, string step)
        {
            Assert.That(ActiveButton("Draw Sea Chart"), Is.Null);
            ClickGuidedButton("Return To Port");
            yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo(step));
            Assert.That(controller.IsTutorialActive, Is.True);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 0), Is.EqualTo(checkpoint));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialRecoveryKey, 0), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey, string.Empty), Is.Empty);
            // Recovery resumes on the hub; tolerate a recovery-service page.
            if (ActiveButton("Return To Port Hub") != null)
            {
                ClickGuidedButton("Return To Port Hub");
                yield return null;
            }
            Assert.That(RequireGuidedButton("Port Hotspot Maintenance").IsInteractable(), Is.True);
            Assert.That(RequireGuidedButton("Port Hotspot Warehouse").IsInteractable(), Is.True);
            Assert.That(RequireGuidedButton("Port Hotspot ComponentShop").IsInteractable(), Is.False);
        }

        private static IEnumerator CheckGuidedRecoveryPierRoundTrip(CoreGameplayDemoController controller,
            int checkpoint, bool selectRouteBeforeLeaving)
        {
            Assert.That(controller.GuidedTutorialRecoveryActive, Is.True);
            ClickGuidedButton("Port Hotspot Voyage");
            yield return null;
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            string routeStep = checkpoint == 0 ? "FirstDeparture" : checkpoint == 2 ? "SecondRoute" : "ThirdRoute";
            string departureStep = checkpoint == 0 ? "FirstDeparture" : checkpoint == 2 ? "SecondDeparture" : "ThirdDeparture";
            string pierStep = checkpoint == 0 ? "FirstDeparture" : checkpoint == 2 ? "SecondPier" : "ThirdPier";
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo(routeStep));
            if (selectRouteBeforeLeaving)
            {
                ClickGuidedButton("Route 0");
                yield return null;
                Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo(departureStep));
                Assert.That(RequireGuidedButton("Start Voyage").IsInteractable(), Is.True);
            }
            ClickGuidedButton("Return To Port Hub");
            yield return null;
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo(pierStep),
                "Leaving the pier must restore the hub step instead of stranding the route-selection gate.");
            Assert.That(RequireGuidedButton("Port Hotspot Voyage").IsInteractable(), Is.True);
            ClickGuidedButton("Port Hotspot Maintenance");
            yield return null;
            Assert.That(GameObject.Find("Port Investment Panel"), Is.Not.Null);
            ClickGuidedButton("Return To Port Hub");
            yield return null;
            Assert.That(RequireGuidedButton("Port Hotspot Voyage").IsInteractable(), Is.True,
                "A recovery-service visit must not prevent returning to the departure pier.");
            ClickGuidedButton("Port Hotspot Voyage");
            yield return null;
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(RequireGuidedButton(checkpoint == 0 ? "Start Voyage" : "Route 0").IsInteractable(), Is.True);
            Assert.That(controller.GuidedTutorialRecoveryActive, Is.True,
                "Recovery ends on actual departure, not by inspecting the pier.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey), Is.EqualTo(checkpoint));
            ClickGuidedButton("Return To Port Hub");
            yield return null;
        }

        private static IEnumerator LoadGuidedTutorialScene(bool enterDeparture = false)
        {
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>().IsTutorialActive, Is.True);
            if (enterDeparture)
            {
                Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
                AssertGuidedFocusTarget("Port Hotspot Voyage");
                ClickGuidedButton("Port Hotspot Voyage");
                yield return null;
                Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            }
        }

        private static IEnumerator DriveGuidedTutorial(CoreGameplayDemoController controller, string stop,
            HashSet<string> seen, bool capture, HashSet<string> captureKeys = null)
        {
            var captured = captureKeys ?? new HashSet<string>();
            int actions = 0;
            while (!GuidedStopReached(controller, stop) && actions++ < 180)
            {
                yield return WaitForGuidedNavigationPresentation();
                if (GuidedStopReached(controller, stop)) break;
                string step = controller.CurrentGuidedTutorialStep.ToString();
                seen.Add(step);
                AssertGuidedInstructionsRemainVisible(controller);
                Assert.That(step, Is.Not.EqualTo("Workshop").And.Not.EqualTo("BuildDeck"),
                    "The guide no longer requires a card-workshop visit or manual deck construction.");
                Assert.That(ActiveButton("Guide Confirm Deck"), Is.Null);
                Assert.That(ActiveButton("Tutorial Primary"), Is.Null);
                Assert.That(ActiveButton("Tutorial Fire Cannons"), Is.Null);
                // The objective can name the next encounter before the normal
                // navigation step has physically reached it.
                if (controller.Session.Phase == CoreDemoPhase.Sailing && step != "FirstReturn" && step != "ThirdReturn")
                {
                    Assert.That(controller.Session.Heading, Is.EqualTo(SailingDirection.East));
                    yield return CompleteGuidedNavigationSetup(controller);
                    if (capture && captured.Add("sail"))
                        NavalBattleVisualCapture.Capture(controller, "50_Guided_Normal_Navigation.png",
                            "正常航海界面按航向、航程、执行顺序使用红框和箭头引导", "Navigation Chart");
                    ClickGuidedButton("Execute Sail");
                    yield return null;
                    if (controller.Session.GuidedTutorialLeg == CoreGuidedTutorialContent.BossVoyageLeg)
                    {
                        Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter),
                            "One normal eastward journey must reach the boss directly.");
                        Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
                        Assert.That(controller.Session.GuidedTutorialCompletedEncounters, Is.Zero);
                        Assert.That(controller.Session.GuidedTutorialRandomEventTriggered, Is.False);
                        Assert.That(controller.Session.GuidedTutorialAmbushTriggered, Is.False);
                    }
                    continue;
                }
                switch (step)
                {
                    case "FirstDeparture":
                        ClickGuidedButton(ActiveButton("Port Hotspot Voyage") != null
                            ? "Port Hotspot Voyage" : "Start Voyage");
                        break;
                    case "SecondDeparture":
                    case "ThirdDeparture":
                        ClickGuidedButton("Start Voyage");
                        break;
                    case "ThirdPier":
                        ClickGuidedButton("Port Hotspot Voyage");
                        break;
                    case "ThirdRoute":
                        ClickGuidedButton("Route 0");
                        break;
                    case "FirstSail":
                    case "SecondSail":
                    case "ThirdSail":
                        Assert.That(controller.Session.Heading, Is.EqualTo(SailingDirection.East));
                        yield return CompleteGuidedNavigationSetup(controller);
                        if (capture && captured.Add("sail"))
                            NavalBattleVisualCapture.Capture(controller, "50_Guided_Normal_Navigation.png",
                                "正常航海界面按航向、航程、执行顺序使用红框和箭头引导", "Navigation Chart");
                        ClickGuidedButton("Execute Sail");
                        break;
                    case "FirstMandatory":
                        Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo("tutorial_first_event"));
                        Assert.That(controller.Session.ActiveEncounter.Mandatory, Is.True);
                        Assert.That(ActiveButton("Skip Encounter"), Is.Null);
                        ClickGuidedButton("Encounter Choice 0");
                        break;
                    case "FirstOptional":
                        Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo("tutorial_optional_event"));
                        Assert.That(controller.Session.ActiveEncounter.Mandatory, Is.False);
                        ClickGuidedButton("Skip Encounter");
                        break;
                    case "FirstCrewEvent":
                        Assert.That(controller.Session.HasNavigator, Is.False);
                        Assert.That(RequireGuidedButton("Encounter Choice 0").IsInteractable(), Is.False);
                        if (ActiveButton("Skip Encounter") != null && ActiveButton("Skip Encounter").IsInteractable())
                            ClickGuidedButton("Skip Encounter");
                        else ClickGuidedButton("Encounter Choice 1");
                        break;
                    case "SecondCrewEvent":
                        Assert.That(controller.Session.HasNavigator, Is.True);
                        Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.NavigatorEventId));
                        ClickGuidedButton("Encounter Choice 0");
                        break;
                    case "SecondAmbush":
                        Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.AmbushEventId));
                        AssertGuidedFocusTarget("Encounter Choice 0");
                        int escapeHull = controller.Session.Hull;
                        int escapeGold = controller.Session.Cargo.Gold;
                        int escapeWater = controller.Session.Cargo.Water;
                        int escapeFood = controller.Session.Cargo.Food;
                        double escapeHours = controller.Session.TotalHours;
                        ClickAcceptanceButton("Encounter Choice 0");
                        Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing),
                            "Leaving the ambush continues this voyage, without settlement or teleporting to port.");
                        Assert.That(controller.Session.Battle, Is.Null, "Avoiding the ambush does not start an extra combat lesson.");
                        Assert.That(controller.Session.Hull, Is.EqualTo(escapeHull));
                        Assert.That(controller.Session.TotalHours, Is.EqualTo(escapeHours));
                        Assert.That(controller.Session.Cargo.Gold, Is.EqualTo(escapeGold));
                        Assert.That(controller.Session.Cargo.Water, Is.EqualTo(escapeWater));
                        Assert.That(controller.Session.Cargo.Food, Is.EqualTo(escapeFood));
                        Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.False);
                        Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero);
                        if (capture && captured.Add("ambush-escape"))
                        {
                            yield return WaitForGuidedCaptureScreenReveal(controller);
                            NavalBattleVisualCapture.Capture(controller, "52_Guided_Ambush_Continue.png",
                                "离开伏击后继续同一航程，首领战尚未开始", "Navigation Chart");
                        }
                        break;
                    case "SecondSettlement":
                        Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                        ClickGuidedButton("Return To Port");
                        break;
                    case "FirstReturn":
                    case "ThirdReturn":
                        if (step == "ThirdReturn")
                        {
                            Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.True);
                            Assert.That(controller.Session.VoyageTreasureFragments, Is.EqualTo(1));
                            Assert.That(controller.Session.LastBattleRetreated, Is.False);
                            AssertGuidedRewardVisible();
                            AssertGuidedFocusTarget("Safe Return");
                            if (capture && captured.Add("boss-return"))
                                NavalBattleVisualCapture.Capture(controller, "56_Guide_Boss_Victory_Return.png",
                                    "首领胜利后引导玩家点击真实主动返航按钮，保留红框及指针命中检查", "Safe Return");
                        }
                        ClickGuidedButton("Safe Return");
                        break;
                    case "FirstSettlement":
                        Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                        ClickGuidedButton("Return To Port");
                        break;
                    case "Tavern":
                        ClickGuidedButton("Port Hotspot Tavern");
                        break;
                    case "RecruitNavigator":
                        if (ActiveButton("Recruit Tavern Crew 0") != null)
                        {
                            if (captured.Add("tavern-dossier-return"))
                            {
                                Button dossierReturn = RequireGuidedButton("Return To Port Hub");
                                Assert.That(dossierReturn.GetComponentInChildren<Text>().text, Is.EqualTo("返回酒馆"));
                                ClickGuidedButton("Return To Port Hub");
                                yield return null;
                                Assert.That(GameObject.Find("Tavern Crew Stage"), Is.Not.Null);
                                Assert.That(GameObject.Find("Tavern Crew Detail"), Is.Null);
                                Button lineupReturn = RequireGuidedButton("Return To Port Hub");
                                Assert.That(lineupReturn.GetComponentInChildren<Text>().text, Is.EqualTo("返回港口"));
                                Assert.That(lineupReturn.IsInteractable(), Is.False,
                                    "The recruitment lesson permits leaving the dossier but keeps the player in the tavern.");
                                lineupReturn.onClick.Invoke();
                                yield return null;
                                Assert.That(GameObject.Find("Tavern Crew Stage"), Is.Not.Null);
                                Assert.That(GameObject.Find("Port Hub"), Is.Null);
                                ClickGuidedButton("Tavern Crew 0");
                                yield return null;
                                Assert.That(RequireGuidedButton("Return To Port Hub").GetComponentInChildren<Text>().text, Is.EqualTo("返回酒馆"));
                            }
                            if (capture && captured.Add("recruit"))
                                NavalBattleVisualCapture.Capture(controller, "51_Guided_Normal_Recruitment.png",
                                    "通过正常档案和契约按钮招募随船航海士", "Recruit Tavern Crew 0");
                            ClickGuidedButton("Recruit Tavern Crew 0");
                        }
                        else ClickGuidedButton("Tavern Crew 0");
                        break;
                    case "SecondPier":
                        if (ActiveButton("Port Hotspot Voyage") != null) ClickGuidedButton("Port Hotspot Voyage");
                        else ClickGuidedButton("Return To Port Hub");
                        break;
                    case "SecondRoute":
                        ClickGuidedButton("Route 0");
                        break;
                    case "TutorialBoss":
                        if (controller.Session.Phase == CoreDemoPhase.Encounter)
                        {
                            Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(CoreGuidedTutorialContent.BossEventId));
                            AssertGuidedFocusTarget("Encounter Choice 0");
                            ClickGuidedButton("Encounter Choice 0");
                        }
                        else
                        {
                            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
                            if (capture && captured.Add("boss"))
                            {
                                yield return WaitForGuidedCaptureScreenReveal(controller);
                                NavalBattleVisualCapture.Capture(controller, "54_Guided_Normal_Boss_Battle.png",
                                    "第二航程在正常战斗界面逐步完成首领教学");
                            }
                            yield return PlayGuidedBattleAction(controller, capture, captured);
                        }
                        break;
                    case "DrawChart":
                    case "ThirdSettlement":
                        Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                        Assert.That(controller.Session.GuidedTutorialBossDefeated, Is.True);
                        Assert.That(controller.Session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
                        Assert.That(controller.Session.Settlement.BankedFragments, Is.EqualTo(1));
                        Assert.That(GameObject.Find("Settlement Treasure Rewards").GetComponent<Text>().text,
                            Does.Contain("藏宝图碎片 +1"));
                        Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 0), Is.Zero,
                            "The component must commit with the final settlement, never in the middle of a voyage.");
                        AssertGuidedRewardVisible();
                        if (capture && captured.Add("settlement"))
                        {
                            Button skipReveal = ActiveButton("Skip Settlement Reveal");
                            if (skipReveal != null && skipReveal.IsInteractable())
                            {
                                ClickGuidedButton("Skip Settlement Reveal");
                                yield return null;
                            }
                            NavalBattleVisualCapture.Capture(controller, "55_Guided_Normal_Chart_Settlement.png",
                                "正常结算中绘制海图，确认返港后提交白色部件与首胜藏品，再进入仓库陈列教学", "Return To Port");
                        }
                        Button draw = ActiveButton("Draw Sea Chart");
                        if (draw != null && draw.IsInteractable()) ClickGuidedButton("Draw Sea Chart");
                        else ClickGuidedButton("Return To Port");
                        break;
                    case "Warehouse":
                        Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.True);
                        Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
                        Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey), Is.EqualTo(1));
                        ClickGuidedButton("Port Hotspot Warehouse");
                        break;
                    case "SelectTreasure":
                        Assert.That(ActiveButton("Warehouse Tab Basics"), Is.Null);
                        Assert.That(RequireGuidedButton("Warehouse Tab Storage").IsInteractable(), Is.False);
                        ClickGuidedButton("Warehouse Treasure " + CoreWarehouseCollection.TutorialTreasureId);
                        break;
                    case "DisplayTreasure":
                        Assert.That(controller.WarehouseCollection.IsDisplayed(CoreWarehouseCollection.TutorialTreasureId), Is.False);
                        Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero,
                            "Selecting the keepsake alone must not complete the display lesson.");
                        if (capture && captured.Add("warehouse"))
                            NavalBattleVisualCapture.Capture(controller, "64_Warehouse_Guided_First_Treasure.png",
                                "击败首领返港结算后，选择真实首胜藏品并亲自陈列，完成仓库教学", "Warehouse Panel");
                        ClickGuidedButton("Warehouse Display Selected");
                        break;
                    case "Shipyard":
                        Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
                        Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey), Is.EqualTo(1));
                        Assert.That(controller.WarehouseCollection.IsDisplayed(CoreWarehouseCollection.TutorialTreasureId), Is.True);
                        string shipButton = "Shipyard Ship " + controller.Session.SelectedShipIndex;
                        string target = ActiveButton(shipButton) != null ? shipButton :
                            ActiveButton("Port Hotspot Shipyard") != null ? "Port Hotspot Shipyard" : "Return To Port Hub";
                        AssertGuidedFocusTarget(target);
                        if (target == "Return To Port Hub" && captured.Add("return-button-clear"))
                            NavalBattleVisualCapture.Capture(controller, "77_Guided_Return_Button_Clear.png",
                                "羊皮纸自动避开当前需要点击的返回港口按钮及红框箭头", target);
                        ClickGuidedButton(target);
                        break;
                    case "ShipEquipment":
                        AssertGuidedFocusTarget("Shipyard Dossier Tab Equipment");
                        ClickGuidedButton("Shipyard Dossier Tab Equipment");
                        break;
                    case "UnequipRewardComponent":
                        AssertGuidedFocusTarget("Shipyard Unequip Component Bow");
                        ClickGuidedButton("Shipyard Unequip Component Bow");
                        break;
                    case "SelectRewardComponent":
                        OwnedShipComponent inventoryReward = controller.OwnedComponents.Single(item =>
                            item.ComponentId == CoreGameplayDemoController.GuidedTutorialRewardComponentId);
                        Assert.That(inventoryReward.ShipIndex, Is.EqualTo(-1));
                        string inventoryButton = "Shipyard Equipment Owned " + inventoryReward.InstanceId;
                        AssertGuidedFocusTarget(inventoryButton);
                        ClickGuidedButton(inventoryButton);
                        break;
                    case "EquipRewardComponent":
                        Assert.That(controller.IsTutorialActive, Is.True);
                        Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
                        AssertGuidedFocusTarget("Shipyard Equip Component");
                        if (capture && captured.Add("equipment"))
                            NavalBattleVisualCapture.Capture(controller, "75_Guided_Shipyard_Equip_First_Component.png",
                                "陈列罗盘后在正常船坞配件页亲自装备首胜部件，实际安装成功才开放自由探索", "Shipyard Equip Component");
                        ClickGuidedButton("Shipyard Equip Component");
                        yield return null;
                        Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Complete));
                        if (capture && captured.Add("equipment-complete"))
                            NavalBattleVisualCapture.Capture(controller, "76_Guided_Shipyard_Equipment_Complete.png",
                                "首胜部件已装到旗舰，替换下的旧件留在库存，完整新手教学此时解除", "Shipyard Equipment Page");
                        break;
                    default:
                        Assert.Fail("Unhandled guided step " + step + " in phase " + controller.Session.Phase);
                        break;
                }
                yield return null;
                float deadline = Time.realtimeSinceStartup + 12f;
                while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                {
                    AdvanceRamComicIfWaiting(controller);
                    yield return null;
                }
                Assert.That(controller.IsBattleAnimationPlaying, Is.False, "A real battle animation failed to finish.");
                // Battle completion rebuilds the page inside a coroutine. Let the
                // new Canvas render before simulating the next frame's pointer:
                // fresh Graphics have no raycast depth until that render pass.
                // Do not rely on the optional screenshot helper to rebuild it.
                yield return null;
            }
            Assert.That(controller.CurrentGuidedTutorialStep.ToString(), Is.EqualTo(stop),
                "The normal UI flow must reach the requested checkpoint without injected game state.");
        }

        private static bool GuidedStopReached(CoreGameplayDemoController controller, string stop)
        {
            return controller.CurrentGuidedTutorialStep.ToString() == stop;
        }

        private static void AssertGuidedDefaultDecks(CoreGameplayDemoController controller)
        {
            var starter = new CoreVoyageSession();
            foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                CollectionAssert.AreEqual(starter.GetBattleDeck(stage), controller.Session.GetBattleDeck(stage),
                    "The new-save guide must use the unedited default " + stage + " deck.");
        }

        private static IEnumerator PlayGuidedBattleAction(CoreGameplayDemoController controller,
            bool capture = false, HashSet<string> captured = null)
        {
            if (controller.Session.IsGuidedBossBattle)
            {
                yield return PlayGuidedScriptAction(controller, capture, captured);
                yield break;
            }
            CoreBattleState battle = controller.Session.Battle;
            Assert.That(battle, Is.Not.Null);
            Assert.That(battle.Turn, Is.LessThan(25), "A starter deck should defeat the authored tutorial boss promptly.");
            int selected = -1;
            float best = float.NegativeInfinity;
            for (int hand = 0; hand < battle.Hand.Count; hand++)
            {
                if (!controller.Session.CanPlayCard(hand)) continue;
                CoreBattleCard card = controller.Session.GetBattleCard(battle.Hand[hand]);
                float score = card.Damage * 3f + card.Draw * 3f + card.Block * .6f;
                score += Math.Min(controller.Session.MaxHull - controller.Session.Hull, card.Repair) * 1.5f;
                if (card.Action == NavalBattleCardAction.Approach || card.Action == NavalBattleCardAction.Navigator)
                    score += battle.DistanceMeters > 350f ? 40f : battle.DistanceMeters > 60f ? 5f : 0f;
                if (card.Action == NavalBattleCardAction.Load || card.Action == NavalBattleCardAction.PrecisionLoad) score += 9f;
                if (card.Action == NavalBattleCardAction.Polish) score += 6f;
                if (card.Action == NavalBattleCardAction.Retreat) score -= 50f;
                if (score > best) { best = score; selected = hand; }
            }
            if (selected >= 0 && best >= 0f) ClickGuidedButton("Battle Card " + selected);
            else ClickGuidedButton("End Battle Turn");
            yield return null;
        }

        private static Button RequireGuidedButton(string name)
        {
            Button button = ActiveButton(name);
            Assert.That(button, Is.Not.Null, "Missing normal UI button: " + name);
            return button;
        }

        private static IEnumerator WaitForGuidedNavigationPresentation()
        {
            // Sailing commits the encounter before its map-marker animation
            // finishes. Wait for the presented tutorial step to catch up.
            float navigationDeadline = Time.realtimeSinceStartup + 5f;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            bool waitedForNavigation = NavigationMotionIsPlaying(controller);
            while (NavigationMotionIsPlaying(controller) && Time.realtimeSinceStartup < navigationDeadline)
                yield return null;
            Assert.That(NavigationMotionIsPlaying(controller), Is.False, "The sailing presentation must complete.");
            if (waitedForNavigation) yield return null; // The new encounter canvas needs its first render pass.
        }

        private static IEnumerator CompleteGuidedNavigationSetup(CoreGameplayDemoController controller)
        {
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            if (controller.CurrentGuidedTutorialNavigationInputStep == GuidedTutorialNavigationInputStep.Heading)
            {
                AssertGuidedFocusTarget("Heading East");
                ClickGuidedButton("Heading East");
                yield return null;
            }
            Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>().IsInteractable(), Is.False);
            Assert.That(controller.Session.PlannedDistanceNm, Is.EqualTo(CoreGuidedTutorialContent.PlannedDistanceNm));
            Assert.That(controller.CurrentGuidedTutorialNavigationInputStep,
                Is.EqualTo(GuidedTutorialNavigationInputStep.Sail));
            AssertGuidedFocusTarget("Execute Sail");
        }

        private static void AssertGuidedLockHasNoTint(Selectable control)
        {
            Assert.That(control.transform.Find("Guide Locked Tint"), Is.Null,
                "Teaching locks must not add the retired dark overlay.");
            Transform guard = control.transform.Find("Guide Input Guard");
            Assert.That(guard, Is.Not.Null, "Locked controls keep a transparent input guard.");
            Image image = guard.GetComponent<Image>();
            Assert.That(image, Is.Not.Null);
            Assert.That(image.color.a, Is.Zero, "The input guard must not visually darken the control.");
            if (control.name.StartsWith("Heading ", StringComparison.Ordinal))
            {
                Assert.That(control.transition, Is.EqualTo(Selectable.Transition.None),
                    "Tutorial-only heading locks keep their normal face rather than applying a disabled tint.");
                Assert.That(control.targetGraphic.canvasRenderer.GetColor(),
                    Is.EqualTo(control.colors.normalColor * control.colors.colorMultiplier));
            }
        }

        private static void AssertGuidedInstructionsRemainVisible(CoreGameplayDemoController controller)
        {
            Canvas.ForceUpdateCanvases();
            string headerName = controller.Session.Phase == CoreDemoPhase.Port
                ? controller.ActivePortSection == "港口总览" ? "Port Hub" : "Port Facility Header"
                : "Voyage Header";
            GameObject header = GameObject.Find(headerName);
            RectTransform designSurface = controller.GetComponentsInChildren<RectTransform>()
                .Single(rect => rect.name == "Phase Content");
            Image paper = controller.GetComponentsInChildren<Image>()
                .Single(image => image.name == "Guided Tutorial Banner");
            ApplyGuidedBannerLayout(paper);
            Rect paperBounds = GuidedWorldRect(paper.rectTransform);
            Rect canvasBounds = GuidedWorldRect(designSurface);
            Assert.That(paper.transform.parent, Is.SameAs(designSurface), "Each lesson owns its independent paper.");
            Assert.That(NauticalSurfaceSkin.IsPaperSurface(paper.sprite), Is.True);
            Assert.That(paper.color.a, Is.EqualTo(1f), "The scene must not bleed through the writing area.");
            Assert.That(paper.raycastTarget, Is.False);
            Assert.That(paperBounds.xMin, Is.GreaterThanOrEqualTo(canvasBounds.xMin - .1f));
            Assert.That(paperBounds.xMax, Is.LessThanOrEqualTo(canvasBounds.xMax + .1f));
            Assert.That(paperBounds.yMin, Is.GreaterThanOrEqualTo(canvasBounds.yMin - .1f));
            Assert.That(paperBounds.yMax, Is.LessThanOrEqualTo(canvasBounds.yMax + .1f));
            foreach (GuidedTutorialFocusGraphic focus in controller.GetComponentsInChildren<GuidedTutorialFocusGraphic>())
                AssertGuidedPaperLeavesFocusClear(paper, designSurface, focus);
            if (header != null)
            {
                Image frame = header.GetComponentsInChildren<Image>().First(image => image.name.EndsWith("Frame"));
                Assert.That(paper.canvasRenderer.absoluteDepth, Is.GreaterThan(frame.canvasRenderer.absoluteDepth),
                    "Bringing the regional header forward must not hide the paper.");
            }
            Rect? precedingLabel = null;
            foreach (string name in new[] { "Guided Tutorial Objective", "Guided Tutorial Instruction" })
            {
                GameObject labelObject = GameObject.Find(name);
                Assert.That(labelObject, Is.Not.Null, "Every active lesson needs visible instructions.");
                Text label = labelObject.GetComponent<Text>();
                Rect labelBounds = GuidedWorldRect(label.rectTransform);
                // The acceptance renderer captures the authored 1920x1080
                // Phase Content. The editor's transient GameView can have a
                // different aspect and is restored after each capture; compare
                // the same design surface rather than that unrelated viewport.
                // This still rejects the former banner above the design's top.
                Assert.That(labelBounds.xMin, Is.GreaterThanOrEqualTo(canvasBounds.xMin - 0.1f), name);
                Assert.That(labelBounds.xMax, Is.LessThanOrEqualTo(canvasBounds.xMax + 0.1f), name);
                Assert.That(labelBounds.yMin, Is.GreaterThanOrEqualTo(canvasBounds.yMin - 0.1f), name);
                Assert.That(labelBounds.yMax, Is.LessThanOrEqualTo(canvasBounds.yMax + 0.1f), name);
                Assert.That(label.raycastTarget, Is.False);
                Assert.That(label.transform.IsChildOf(paper.transform), Is.True, name);
                Assert.That(labelBounds.xMin, Is.GreaterThan(paperBounds.xMin), name + " left writing margin");
                Assert.That(labelBounds.xMax, Is.LessThan(paperBounds.xMax), name + " right writing margin");
                Assert.That(labelBounds.yMin, Is.GreaterThan(paperBounds.yMin), name + " bottom writing margin");
                Assert.That(labelBounds.yMax, Is.LessThan(paperBounds.yMax), name + " top writing margin");
                Assert.That(label.resizeTextForBestFit, Is.False, name + " must not shrink the lesson.");
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(24), name);
                Assert.That(label.color.r + label.color.g + label.color.b, Is.LessThan(1.2f), name + " needs dark reading ink.");
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f),
                    name + " must display the complete lesson at its readable size.");
                if (precedingLabel.HasValue)
                    Assert.That(labelBounds.Overlaps(precedingLabel.Value), Is.False, "Objective and instruction must remain separate.");
                precedingLabel = labelBounds;
            }
        }

        private static Rect GuidedWorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertGuidedFocusTarget(string controlName)
        {
            Canvas.ForceUpdateCanvases();
            Image paper = GameObject.Find("Guided Tutorial Banner").GetComponent<Image>();
            ApplyGuidedBannerLayout(paper);
            GuidedTutorialFocusGraphic[] graphics = Object.FindObjectsByType<GuidedTutorialFocusGraphic>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            GuidedTutorialFocusGraphic focus = graphics.FirstOrDefault(graphic => graphic.Target != null && graphic.Target.name == controlName);
            Assert.That(focus, Is.Not.Null, "Missing red outline and arrow for " + controlName);
            Assert.That(focus.HasVisibleTarget, Is.True);
            foreach (GuidedTutorialFocusGraphic graphic in graphics)
            {
                Assert.That(graphic.raycastTarget, Is.False, "The red annotation must never intercept the taught action.");
                AssertGuidedPaperLeavesFocusClear(paper, (RectTransform)paper.transform.parent, graphic);
            }
            Assert.That(GameObject.Find("Guide Locked Tint"), Is.Null);
            Mesh mesh = focus.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(19),
                    "The live annotation mesh must include the rectangular outline and pointing arrow.");
            Assert.That(mesh.colors32.Any(tint => tint.r > 200 && tint.g < 90 && tint.b < 90 && tint.a > 200), Is.True,
                    "The teaching annotation must be red, not the former dark screen tint.");
            Selectable target = focus.Target.GetComponent<Selectable>();
            Canvas canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform hitTarget = focus.VisualTarget;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, hitTarget.TransformPoint(hitTarget.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "The guided control must receive normal pointer input.");
            string hitDetails = string.Join(" | ", hits.Take(3).Select(hit =>
            {
                var path = new Stack<string>();
                for (Transform item = hit.gameObject.transform; item != null; item = item.parent) path.Push(item.name);
                Graphic graphic = hit.gameObject.GetComponent<Graphic>();
                Selectable hitControl = hit.gameObject.GetComponentInParent<Selectable>();
                return string.Join("/", path) + " [graphic=" + (graphic == null ? "none" : graphic.GetType().Name + ":" + graphic.name) +
                    "; selectable=" + (hitControl == null ? "none" : hitControl.name) + "]";
            }));
            Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(), Is.SameAs(target),
                "The red frame, arrow, and instruction banner must not cover " + controlName +
                " at " + pointer.position + "; target depth=" + target.targetGraphic.depth +
                ", culled=" + target.targetGraphic.canvasRenderer.cull + ". First three pointer hits: " + hitDetails);
        }

        private static void ApplyGuidedBannerLayout(Image paper)
        {
            GuidedTutorialBannerLayout layout = paper.GetComponent<GuidedTutorialBannerLayout>();
            if (layout == null)
                Assert.That(paper.transform.parent.GetComponentsInChildren<GuidedTutorialFocusGraphic>()
                    .Any(focus => focus.isActiveAndEnabled && focus.HasVisibleTarget), Is.False,
                    "Teaching paper with an active control and arrow needs avoidance layout.");
            else layout.Apply();
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertGuidedPaperLeavesFocusClear(Image paper, RectTransform surface,
            GuidedTutorialFocusGraphic focus)
        {
            if (!focus.isActiveAndEnabled || !focus.HasVisibleTarget) return;
            Assert.That(GuidedWorldRect(paper.rectTransform).Overlaps(GuidedWorldRect(focus.VisualTarget)), Is.False,
                "The teaching paper visually covers the current action: " + focus.Target.name);
            if (!focus.TryGetProtectedRect(surface, out Rect protectedBounds)) return;
            Vector3 bottomLeft = surface.TransformPoint(protectedBounds.min);
            Vector3 topRight = surface.TransformPoint(protectedBounds.max);
            Rect protectedWorldBounds = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
            Assert.That(GuidedWorldRect(paper.rectTransform).Overlaps(protectedWorldBounds), Is.False,
                "The teaching paper must leave the red outline and the arrow's complete motion clear for " + focus.Target.name);
        }

        private static void AssertGuidedRewardVisible()
        {
            GameObject reward = GameObject.Find("Guided Tutorial Component Reward");
            Assert.That(reward, Is.Not.Null,
                "The pending white component must be attached to the current screen, not a retired navigation object.");
            Assert.That(reward.activeInHierarchy, Is.True);
            Text label = reward.GetComponentsInChildren<Text>().FirstOrDefault(text => text.name == "Guided Tutorial Reward Text");
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Does.Contain("白色品质"));
            Canvas.ForceUpdateCanvases();
            Rect rewardBounds = GuidedWorldRect(reward.GetComponent<RectTransform>());
            foreach (string name in new[] { "Voyage Supply Endurance", "Voyage Journal Scroll", "Lookout Report",
                "Lookout Time Ribbon", "Settlement Treasure Manifest" })
            {
                GameObject readout = GameObject.Find(name);
                if (readout != null)
                    Assert.That(rewardBounds.Overlaps(GuidedWorldRect(readout.GetComponent<RectTransform>())), Is.False,
                        "The tutorial's component reward must leave " + name + " visible.");
            }
        }

        private static void ClickGuidedButton(string name)
        {
            Button button = RequireGuidedButton(name);
            Assert.That(button.IsInteractable(), Is.True, "The next guided action is unexpectedly locked: " + name);
            button.onClick.Invoke();
        }
    }
}
