using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
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
        public IEnumerator DepartureCrew_CardPreviewAfterReloadMatchesFirstActualDeparture()
        {
            yield return LoadCardCrewPreviewFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            // Do not visit the departure panel before inspecting a paid upgrade.
            yield return OpenCardCrewPreviewThrust();
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(7));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(3));
            AssertCardCrewPreviewThrust(7, 5);
            NavalBattleVisualCapture.Capture(controller, "DepartureCrewPreview_01_Direct_Workshop.png",
                "未先访问出航：三专家占位后显示七名普通水手与真实接舷效果", "Battle Card Upgrade Panel");
            yield return ClickShipyardControl("Edit Deck From Card Upgrades");
            string deckText = GameObject.Find("Workshop Card Description 14").GetComponent<Text>().text;
            Assert.That(deckText, Does.Contain("当前 7 人"));
            Assert.That(deckText, Does.Contain("船员伤害 5"));
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(7));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(3));
            Assert.That(controller.Session.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage, Is.EqualTo(5));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureCrew_CardPreviewPreservesStandbyAndManualOrdinaryCountAcrossFacilities()
        {
            yield return LoadCardCrewPreviewFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            yield return ClickShipyardControl("Departure Special Crew Toggle 0");
            yield return ClickShipyardControl("Departure Special Crew Toggle 5");
            Slider ordinary = GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>();
            Assert.That(ordinary.maxValue, Is.EqualTo(9));
            ordinary.value = 9;
            yield return null;
            yield return ClickShipyardControl("Confirm Departure Crew");
            yield return ClickShipyardControl("Return To Port Hub");
            yield return OpenCardCrewPreviewThrust();
            Assert.That(controller.Session.HasNavigator, Is.False, "A workshop render must not restore a standby profession.");
            Assert.That(controller.Session.HasDiver, Is.False);
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(9));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(1));
            AssertCardCrewPreviewThrust(9, 6);
            NavalBattleVisualCapture.Capture(controller, "DepartureCrewPreview_02_Standby_Workshop.png",
                "导航与潜水待命：九名普通水手、一名专家及真实接舷效果", "Battle Card Upgrade Panel");
            CrewVariantDefinition navigator = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 0);
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(controller.Session.GetBonusBattleCards(stage).Any(index => controller.Session.GetBattleCard(index).RequiredCrew == navigator.Id), Is.False,
                    "A standby navigator cannot appear among the workshop's active " + stage + " source cards.");
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(DepartureCrewChoiceState(0), Is.EqualTo("待命"));
            Assert.That(DepartureCrewChoiceState(5), Is.EqualTo("待命"));
            Assert.That(GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>().value, Is.EqualTo(9));
            yield return ClickShipyardControl("Confirm Departure Crew");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(9));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(1));
            Assert.That(controller.Session.HasNavigator, Is.False);
            Assert.That(controller.Session.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage, Is.EqualTo(6));
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(controller.Session.GetBonusBattleCards(stage).Any(index => controller.Session.GetBattleCard(index).RequiredCrew == navigator.Id), Is.False,
                    "The same standby exclusion must hold in the actual departing deck.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CrewLocationKeyForReset(navigator.Id)), Is.EqualTo("白贝港"));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadCardCrewPreviewFixture()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "白贝港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 0);
            foreach (int profession in new[] { 0, 4, 5 }) RecruitDepartureCrewChoice(profession);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.ConfigureCardUpgrades(new Dictionary<string, int> { ["card_crew_thrust"] = 2 });
        }

        private static IEnumerator OpenCardCrewPreviewThrust()
        {
            yield return ClickShipyardControl("Port Hotspot CardWorkshop");
            yield return ClickShipyardControl("Open Deck Chest 2");
            yield return ClickShipyardControl("Select Upgrade Card 14");
        }

        private static void AssertCardCrewPreviewThrust(int ordinary, int damage)
        {
            string body = GameObject.Find("Upgrade Comparison Body Current").GetComponent<Text>().text;
            Assert.That(body, Does.Contain("当前 " + ordinary + " 人接舷实际"));
            Assert.That(body, Does.Contain("伤害 " + damage));
            Assert.That(body, Does.Contain("基础（28 名普通水手）"));
        }
    }
}
