using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public IEnumerator DepartureCrew_CourierStandbySurvivesRedrawAndChangesActualDepartureConsumption()
        {
            yield return LoadDepartureCrewChoiceFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>().value, Is.EqualTo(4));
            Button oldCook = ActiveButton("Departure Special Crew Toggle 4");
            Button.ButtonClickedEvent staleCookClick = oldCook.onClick;
            yield return ClickShipyardControl("Departure Special Crew Toggle 4");
            staleCookClick.Invoke(); yield return null;
            Assert.That(DepartureCrewChoiceState(4), Is.EqualTo("待命"), "Redraws and a stale click cannot reselect the cook.");
            Slider ordinary = GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>();
            Assert.That(ordinary.maxValue, Is.EqualTo(5));
            ordinary.value = 5; yield return null;
            Assert.That(DepartureCrewChoiceState(4), Is.EqualTo("待命"));
            Assert.That(GameObject.Find("Departure Ordinary Crew Hint").GetComponent<Text>().text, Does.Contain("已达半载"));
            yield return ClickShipyardControl("Confirm Departure Crew");
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(DepartureCrewChoiceState(4), Is.EqualTo("待命"));
            Assert.That(GameObject.Find("Departure Ordinary Crew Slider").GetComponent<Slider>().value, Is.EqualTo(5));
            yield return ClickShipyardControl("Confirm Departure Crew");
            yield return ClickShipyardControl("Start Voyage");

            CoreVoyageSession session = controller.Session;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.SelectedShipIndex, Is.EqualTo(5));
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(5));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(5));
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(10));
            Assert.That(session.IsShortStaffed, Is.False);
            Assert.That(session.HasCook, Is.False);
            Assert.That(session.HasNavigator, Is.True);
            Assert.That(session.WaterSupplyHours, Is.EqualTo(80d * 24d / 10d).Within(.001d));
            CrewVariantDefinition cook = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 4);
            foreach (NavalBattleStage stage in System.Enum.GetValues(typeof(NavalBattleStage)))
                Assert.That(session.GetBonusBattleCards(stage).Any(card => CoreVoyageSession.CardLibrary[card].RequiredCrew == cook.Id), Is.False,
                    "A crew member on standby must not contribute combat cards.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CrewLocationKeyForReset(cook.Id)), Is.EqualTo("白贝港"));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CrewAtSeaKey).Split(',').Contains(cook.Id), Is.False);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CrewAtSeaKey).Split(',').Length, Is.EqualTo(5));

            // Isolate an arrival at another port: the cook was explicitly left
            // behind, so even its active profession identity cannot teleport.
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            session.RestoreProfile(session.ProfileGold, "铜钟港");
            typeof(CoreGameplayDemoController).GetMethod("CommitCrewLocationsAfterReturn", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            RefreshDepartureCrewChoiceView(controller);
            yield return null;
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(ActiveButton("Departure Special Crew Toggle 4").interactable, Is.False);
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(session.HasCook, Is.False);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CrewLocationKeyForReset(cook.Id)), Is.EqualTo("白贝港"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureCrew_NewRecruitJoinsWithoutOverridingStandbyAndShipChangeResetsTheChoice()
        {
            yield return LoadDepartureCrewChoiceFixture(5);
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            yield return ClickShipyardControl("Departure Special Crew Toggle 4");
            RecruitDepartureCrewChoice(5);
            RefreshDepartureCrewChoiceView(controller); yield return null;
            Assert.That(DepartureCrewChoiceState(4), Is.EqualTo("待命"));
            Assert.That(DepartureCrewChoiceState(5), Is.EqualTo("已登船"));
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(controller.Session.SelectedOrdinaryCrew + controller.Session.SelectedSpecialCrewCount, Is.LessThanOrEqualTo(10));
            Assert.That(controller.Session.HasDiver, Is.True);
            Assert.That(controller.Session.HasCook, Is.False);
            yield return ClickShipyardControl("Departure Next Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.Zero);
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(DepartureCrewChoiceState(4), Is.EqualTo("已登船"));
            yield return ClickShipyardControl("Confirm Departure Crew");
            yield return ClickShipyardControl("Departure Next Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(5));
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(DepartureCrewChoiceState(4), Is.EqualTo("已登船"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureCrew_RemovesAwayAndDispatchedIdentitiesAndRejectsAnOldPortControl()
        {
            yield return LoadDepartureCrewChoiceFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            CrewVariantDefinition navigator = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 0);
            CrewVariantDefinition doctor = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 2);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(navigator.Id), "断锚港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchMissionKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchRealClockKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchEndsAtKey, System.DateTime.UtcNow.AddDays(1).Ticks.ToString());
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchCrewKey, "2");
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchCrewIdentityKey, doctor.Id);
            RefreshDepartureCrewChoiceView(controller); yield return null;
            Assert.That(ActiveButton("Departure Special Crew Toggle 0").interactable, Is.False);
            Assert.That(ActiveButton("Departure Special Crew Toggle 2").interactable, Is.False);
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(controller.Session.HasNavigator, Is.False);
            Assert.That(controller.Session.HasDoctor, Is.False);
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(4));
            yield return ClickShipyardControl("Configure Voyage Crew");
            Button oldCook = ActiveButton("Departure Special Crew Toggle 4");
            controller.Session.RestoreProfile(controller.Session.ProfileGold, "断锚港");
            oldCook.onClick.Invoke();
            var standby = (HashSet<string>)typeof(CoreGameplayDemoController).GetField("departureStandbyCrew", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(standby, Is.Empty, "A control from the previous port cannot change the new port's crew choices.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadDepartureCrewChoiceFixture(int omittedProfession = -1)
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "白贝港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(5), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 5);
            for (int profession = 0; profession < 6; profession++)
                if (profession != omittedProfession) RecruitDepartureCrewChoice(profession);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(5));
            Assert.That(controller.Session.TryDepositAllPortCargo(), Is.True);
            controller.Session.Cargo.Water = controller.Session.Cargo.Food = 80;
        }

        private static void RecruitDepartureCrewChoice(int profession)
        {
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession("白贝港", profession);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(profession), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(profession), crew.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id), "白贝港");
        }

        private static string DepartureCrewChoiceState(int profession) =>
            GameObject.Find("Departure Special Crew State " + profession).GetComponent<Text>().text;

        private static void RefreshDepartureCrewChoiceView(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
