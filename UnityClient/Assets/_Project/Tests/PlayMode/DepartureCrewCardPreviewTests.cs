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
            AssertCardCrewPreviewThrust(7);
            NavalBattleVisualCapture.Capture(controller, "DepartureCrewPreview_01_Direct_Workshop.png",
                "未先访问出航：三专家占位后显示七名普通水手与真实接舷效果", "Battle Card Upgrade Panel");
            yield return ReturnFromWorkshopForgeToSelectedDeck();
            string deckText = GameObject.Find("Workshop Card Description 14").GetComponent<Text>().text;
            Assert.That(ForgeVisibleText(deckText), Does.Not.Contain("实际").And.Not.Contain("当前7人"));
            Assert.That(ForgeVisibleText(deckText), Does.Contain("敌方船员-" + ExpectedDepartureThrust(controller.Session, 7)));
            yield return ClickShipyardControl("Return To Port Hub");
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(7));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(3));
            Assert.That(controller.Session.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage, Is.EqualTo(ExpectedDepartureThrust(controller.Session, 7)));
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
            AssertCardCrewPreviewThrust(9);
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
            Assert.That(controller.Session.GetBattleCardEffects(14, NavalBattleStage.Boarding).Damage, Is.EqualTo(ExpectedDepartureThrust(controller.Session, 9)));
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
            yield return ClickShipyardControl("Workshop Preview Stage Boarding");
            yield return ClickShipyardControl("Open Battle Card Upgrades");
            yield return ClickShipyardControl("Select Upgrade Card 14");
        }

        private static int ExpectedDepartureThrust(CoreVoyageSession session, int ordinary)
        {
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(ordinary), "The roster still controls departure capacity and health.");
            Assert.That(session.GetBattleCard(14).Damage, Is.EqualTo(12), "The authored 8-point thrust at rank 2 retains its 1.5 upgrade multiplier.");
            return 12;
        }

        private static void AssertCardCrewPreviewThrust(int ordinary)
        {
            CoreVoyageSession session = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session;
            CoreBattleCard current = session.GetBattleCard(14);
            Assert.That(session.GetBattleCardEffects(current, NavalBattleStage.Boarding).Damage, Is.EqualTo(ExpectedDepartureThrust(session, ordinary)));
            foreach (string side in new[] { "Current", "Next" })
            {
                CoreBattleCard card = side == "Current" ? current : session.GetBattleCardAtLevel(14, Mathf.Min(3, current.UpgradeLevel + 1));
                Text body = GameObject.Find("Upgrade Comparison Body " + side).GetComponent<Text>();
                RectTransform face = GameObject.Find("Upgrade Comparison " + side + " Face").GetComponent<RectTransform>();
                Assert.That(body.transform.IsChildOf(face), Is.True, side);
                Assert.That(body.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EffectSize), side);
                Assert.That(body.preferredHeight, Is.LessThanOrEqualTo(body.rectTransform.rect.height + .5f), side);
                string expected = CardDescriptionText.Compact(session.BattleCardFaceDescription(card, NavalBattleStage.Boarding));
                Assert.That(ForgeVisibleText(body.text), Is.EqualTo(ForgeVisibleText(expected)), side + " quotes the upgraded card independently of the departure roster");
                int expectedDamage = side == "Current" ? 12 : 16;
                Assert.That(session.GetBattleCardEffects(card, NavalBattleStage.Boarding).Damage, Is.EqualTo(expectedDamage));
                Assert.That(ForgeVisibleText(body.text), Does.Contain("敌方船员-" + expectedDamage));
                Assert.That(body.text, Does.Not.Contain("实际").And.Not.Contain("当前" + ordinary + "人"));
                Assert.That(body.text, Does.Not.Contain("基础（28 名普通水手）"), "The complete card face does not repeat the old long-form comparison.");
                Assert.That(GameObject.Find("Upgrade Comparison Name " + side).GetComponent<Text>().text,
                    Is.EqualTo(CardFaceLayout.DisplayName(card)), side);
            }
        }
    }
}
