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
        public IEnumerator DepartureCrew_MainButtonsCrossHalfAndEightyPercentAndUseThePreviewedSpeedAtSea()
        {
            yield return LoadOrdinaryDepartureSpeedFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            CoreVoyageSession session = controller.Session;
            Assert.That(session.CrewCountValue, Is.EqualTo(10));
            Assert.That(session.SelectedSpecialCrewCount, Is.Zero);
            float baseSpeed = session.SpeedKnots;
            AssertOrdinaryDepartureSpeed(controller, 10, baseSpeed * 1.10f);
            Assert.That(ActiveButton("DepartureOrdinaryCrewIncreaseButton").interactable, Is.False);
            yield return WaitForInvestmentAndCrewScreenToSettle("Voyage Setup");
            NavalBattleVisualCapture.Capture(controller, "DepartureCrew_01_FullOrdinaryCrew.png", "出航主界面满普通编制：航速加成10%，可直接减员", "Voyage Setup");

            yield return SetOrdinaryDepartureCount(controller, 4);
            AssertOrdinaryDepartureSpeed(controller, 4, Mathf.Max(2.5f, baseSpeed - .8f));
            yield return WaitForInvestmentAndCrewScreenToSettle("Voyage Setup");
            NavalBattleVisualCapture.Capture(controller, "DepartureCrew_02_BelowHalf.png", "普通人数4/10：距离一半少1人，航速减0.8节", "Voyage Setup");
            yield return SetOrdinaryDepartureCount(controller, 5);
            AssertOrdinaryDepartureSpeed(controller, 5, baseSpeed);
            yield return SetOrdinaryDepartureCount(controller, 8);
            AssertOrdinaryDepartureSpeed(controller, 8, baseSpeed);
            yield return SetOrdinaryDepartureCount(controller, 9);
            AssertOrdinaryDepartureSpeed(controller, 9, baseSpeed * 1.05f);
            yield return WaitForInvestmentAndCrewScreenToSettle("Voyage Setup");
            NavalBattleVisualCapture.Capture(controller, "DepartureCrew_03_AboveEightyPercent.png", "普通人数9/10：超过80%后线性加成5%", "Voyage Setup");
            yield return SetOrdinaryDepartureCount(controller, 10);
            AssertOrdinaryDepartureSpeed(controller, 10, baseSpeed * 1.10f);
            // Loading a shared chart requires a navigator. The recruited
            // fixture identity occupies one berth, so actual sailing uses 9+1.
            RecruitDepartureCrewChoice(0);
            RefreshDepartureCrewChoiceView(controller); yield return null;
            Assert.That(session.HasNavigator, Is.True);
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(1));
            AssertOrdinaryDepartureSpeed(controller, 9, baseSpeed * 1.05f);
            yield return EnterDepartureChartSeed("SS2.0.0.1.0.2.72533DBC.00000000.4858B60F");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(9), "The main-page selection must be the actual sailing crew.");
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(1));
            Assert.That(session.HasNavigator, Is.True);
            Assert.That(session.EffectiveSpeedKnots, Is.EqualTo(session.SpeedKnots * 1.05f).Within(.001f));
            yield return ClickShipyardControl("Heading West");
            GameObject.Find("Distance Slider").GetComponent<Slider>().value = 20;
            yield return null;
            SailingForecast forecast = session.GetForecast();
            Assert.That(forecast, Is.Not.Null);
            Assert.That(forecast.Blocked, Is.False);
            Assert.That(forecast.ActualDistanceNm, Is.EqualTo(20f).Within(.001f));
            float expectedActualSpeed = Mathf.Max(2.5f, session.EffectiveSpeedKnots + 1f +
                forecast.CurrentSpeedModifier + forecast.WindSpeedModifier);
            Assert.That(forecast.EffectiveSpeedKnots, Is.EqualTo(expectedActualSpeed).Within(.001f),
                "The ordinary-crew bonus and navigator's one knot must feed sailing speed before wind and currents.");
            Assert.That(GameObject.Find("Forecast").GetComponent<Text>().text,
                Does.Contain("航速 " + forecast.EffectiveSpeedKnots.ToString("0.0") + " 节"));
            double hours = session.TotalHours;
            float distance = session.TotalDistanceNm;
            yield return ClickShipyardControl("Execute Sail");
            Assert.That(session.TotalDistanceNm - distance, Is.EqualTo(forecast.ActualDistanceNm).Within(.001f));
            Assert.That(session.TotalHours - hours, Is.EqualTo(forecast.Hours).Within(.00001d));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureCrew_MainButtonsRespectSpecialOccupancyAndDoNotCountSpecialCrewTowardSpeed()
        {
            yield return LoadDepartureCrewChoiceFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(6));
            float baseSpeed = controller.Session.SpeedKnots;
            AssertOrdinaryDepartureSpeed(controller, 4, Mathf.Max(2.5f, baseSpeed - .8f));
            Assert.That(controller.Session.EffectiveCrewCount, Is.EqualTo(10));
            Button increase = ActiveButton("DepartureOrdinaryCrewIncreaseButton");
            Assert.That(increase.interactable, Is.False, "Six special crew occupy six of the ten berths.");
            increase.onClick.Invoke(); yield return null;
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(4));
            yield return WaitForInvestmentAndCrewScreenToSettle("Voyage Setup");
            NavalBattleVisualCapture.Capture(controller, "DepartureCrew_04_SpecialCrewOccupancy.png", "6名特殊船员占位后最多4名普通水手；满总人数仍有普通水手不足减益", "Voyage Setup");
            yield return ClickShipyardControl("DepartureOrdinaryCrewDecreaseButton");
            AssertOrdinaryDepartureSpeed(controller, 3, Mathf.Max(2.5f, baseSpeed - 1.6f));
            yield return ClickShipyardControl("DepartureOrdinaryCrewIncreaseButton");
            AssertOrdinaryDepartureSpeed(controller, 4, Mathf.Max(2.5f, baseSpeed - .8f));
            yield return ClickShipyardControl("Configure Voyage Crew");
            yield return ClickDepartureCrewChoice(4);
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(5));
            Assert.That(ActiveButton("DepartureOrdinaryCrewIncreaseButton").interactable, Is.True);
            yield return ClickShipyardControl("DepartureOrdinaryCrewIncreaseButton");
            AssertOrdinaryDepartureSpeed(controller, 5, baseSpeed);
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(5));
            Assert.That(ActiveButton("DepartureOrdinaryCrewIncreaseButton").interactable, Is.False);
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(5));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(5));
            Assert.That(controller.Session.EffectiveSpeedKnots, Is.EqualTo(controller.Session.SpeedKnots).Within(.001f));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureCrew_ConfigurationShowsOnlyRosterAndKeepsMainOrdinarySelection()
        {
            yield return LoadOrdinaryDepartureSpeedFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            float baseSpeed = controller.Session.SpeedKnots;
            foreach (int count in new[] { 4, 5, 8, 9, 10, 3, 4 })
            {
                yield return SetOrdinaryDepartureCount(controller, count);
                float expected = count < 5 ? Mathf.Max(2.5f, baseSpeed - (5 - count) * .8f) :
                    count > 8 ? baseSpeed * (1f + (count - 8) * .05f) : baseSpeed;
                AssertOrdinaryDepartureSpeed(controller, count, expected);
            }
            yield return ClickShipyardControl("Configure Voyage Crew");
            AssertDepartureCrewOverlayRemovedContent(controller);
            Assert.That(DepartureCrewChoiceButtons(), Is.Empty, "Unrecruited crew must not appear as disabled portrait slots.");
            Assert.That(GameObject.Find("Departure Special Crew Empty"), Is.Not.Null);
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(4));
            Button.ButtonClickedEvent oldConfirm = ActiveButton("Confirm Departure Crew").onClick;
            yield return WaitForInvestmentAndCrewScreenToSettle("Departure Crew Configuration");
            NavalBattleVisualCapture.Capture(controller, "DepartureCrew_05_RosterOnly.png", "中央船员弹窗仅保留特殊船员名单与确认操作，普通人数在码头主界面调整", "Departure Crew Configuration");
            yield return ClickShipyardControl("Confirm Departure Crew");
            AssertOrdinaryDepartureSpeed(controller, 4, Mathf.Max(2.5f, baseSpeed - .8f));
            yield return SetOrdinaryDepartureCount(controller, 9);
            yield return ClickShipyardControl("Configure Voyage Crew");
            GameObject reopened = GameObject.Find("Departure Crew Configuration");
            oldConfirm.Invoke(); yield return null;
            Assert.That(GameObject.Find("Departure Crew Configuration"), Is.SameAs(reopened), "A retired confirm callback cannot close a newer crew window.");
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(9));
            Assert.That(controller.Session.EffectiveSpeedKnots, Is.EqualTo(baseSpeed * 1.05f).Within(.001f));
            AssertDepartureCrewOverlayRemovedContent(controller);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureCrew_MainButtonCallbacksCannotReplayAfterRedrawShipChangeOrLeavingDeparture()
        {
            yield return LoadOrdinaryDepartureSpeedFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            Button.ButtonClickedEvent replay = ActiveButton("DepartureOrdinaryCrewDecreaseButton").onClick;
            yield return ClickShipyardControl("DepartureOrdinaryCrewDecreaseButton");
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(9));
            replay.Invoke(); yield return null;
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(9), "A callback from the previous redraw must not decrement twice.");
            Button.ButtonClickedEvent previousShip = ActiveButton("DepartureOrdinaryCrewDecreaseButton").onClick;
            yield return ClickShipyardControl("Departure Next Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.Zero);
            int ordinary = controller.Session.SelectedOrdinaryCrew, special = controller.Session.SelectedSpecialCrewCount;
            float speed = controller.Session.EffectiveSpeedKnots;
            previousShip.Invoke(); yield return null;
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(ordinary));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(special));
            Assert.That(controller.Session.EffectiveSpeedKnots, Is.EqualTo(speed));
            Button.ButtonClickedEvent previousView = ActiveButton("DepartureOrdinaryCrewDecreaseButton").onClick;
            yield return ClickShipyardControl("Return To Port Hub");
            previousView.Invoke(); yield return null;
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(ordinary));
            Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            Button.ButtonClickedEvent previousConfirm = ActiveButton("Confirm Departure Crew").onClick;
            yield return ClickShipyardControl("Confirm Departure Crew");
            yield return ClickShipyardControl("Departure Next Vessel");
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(5));
            ordinary = controller.Session.SelectedOrdinaryCrew;
            previousConfirm.Invoke(); previousShip.Invoke(); previousView.Invoke(); yield return null;
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(ordinary));
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DepartureCrew_CourierStandbySurvivesRedrawAndChangesActualDepartureConsumption()
        {
            yield return LoadDepartureCrewChoiceFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(4));
            Button oldCook = DepartureCrewChoiceButton(4);
            Button.ButtonClickedEvent staleCookClick = oldCook.onClick;
            yield return ClickDepartureCrewChoice(4);
            staleCookClick.Invoke(); yield return null;
            Assert.That(DepartureCrewChoiceState(4), Is.EqualTo("待命"), "Redraws and a stale click cannot reselect the cook.");
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(5));
            yield return ClickShipyardControl("Confirm Departure Crew");
            yield return SetOrdinaryDepartureCount(controller, 5);
            Assert.That(controller.Session.IsShortStaffed, Is.False);
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(DepartureCrewChoiceState(4), Is.EqualTo("待命"));
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(5));
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
            Assert.That(DepartureCrewChoiceButton(4), Is.Null, "Crew left at another port must be absent from this port's roster.");
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
            yield return ClickDepartureCrewChoice(4);
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
        public IEnumerator DepartureCrew_IgnoresLegacyAssignmentAndRejectsAwayCrewAndAnOldPortControl()
        {
            yield return LoadDepartureCrewChoiceFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            CrewVariantDefinition navigator = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 0);
            CrewVariantDefinition doctor = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 2);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(navigator.Id), "断锚港");
            PlayerPrefs.SetInt("ShatteredSail.Dispatch.Mission.v1", 0);
            PlayerPrefs.SetInt("ShatteredSail.Dispatch.RealClock.v1", 1);
            PlayerPrefs.SetString("ShatteredSail.Dispatch.EndsAt.v1", System.DateTime.UtcNow.AddDays(1).Ticks.ToString());
            PlayerPrefs.SetString("ShatteredSail.Dispatch.CrewCsv.v1", "2");
            PlayerPrefs.SetString("ShatteredSail.Dispatch.CrewIdentities.v2", doctor.Id);
            RefreshDepartureCrewChoiceView(controller); yield return null;
            Assert.That(DepartureCrewChoiceButton(0), Is.Null, "A recruited navigator at another port is not listed locally.");
            Assert.That(DepartureCrewChoiceButton(2).interactable, Is.True, "Old assignment keys must not prevent a local doctor from boarding.");
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(controller.Session.HasNavigator, Is.False);
            Assert.That(controller.Session.HasDoctor, Is.True);
            Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(5));
            yield return ClickShipyardControl("Configure Voyage Crew");
            Button oldCook = DepartureCrewChoiceButton(4);
            controller.Session.RestoreProfile(controller.Session.ProfileGold, "断锚港");
            oldCook.onClick.Invoke();
            var standby = (HashSet<string>)typeof(CoreGameplayDemoController).GetField("departureStandbyCrew", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(standby, Is.Empty, "A control from the previous port cannot change the new port's crew choices.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator WaitForInvestmentAndCrewScreenToSettle(string rootName)
        {
            GameObject screen = GameObject.Find(rootName);
            Assert.That(screen, Is.Not.Null, "Missing capture screen: " + rootName);
            CanvasGroup[] groups = screen.GetComponentsInParent<CanvasGroup>();
            Assert.That(groups, Is.Not.Empty, rootName + " must include the production screen transition.");
            float deadline = Time.realtimeSinceStartup + 2f;
            while (groups.Any(group => group != null && group.alpha < 1f) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(GameObject.Find(rootName), Is.SameAs(screen), rootName + " changed while waiting for its reveal.");
            foreach (CanvasGroup group in groups)
            {
                Assert.That(group, Is.Not.Null, rootName + " lost an ancestor CanvasGroup.");
                Assert.That(group.alpha, Is.EqualTo(1f), rootName + " must finish its reveal before capture.");
            }
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator LoadDepartureCrewChoiceFixture(int omittedProfession = -1)
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "白贝港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(5), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 5);
            InstallDepartureCrewFixtureShipBerths();
            for (int profession = 0; profession < 6; profession++)
                if (profession != omittedProfession) RecruitDepartureCrewChoice(profession);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(5));
            Assert.That(controller.Session.IsShipAtCurrentPort(0) && controller.Session.IsShipAtCurrentPort(5), Is.True);
            Assert.That(controller.Session.TryDepositAllPortCargo(), Is.True);
            controller.Session.Cargo.Water = controller.Session.Cargo.Food = 80;
        }

        private static IEnumerator LoadOrdinaryDepartureSpeedFixture()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "白贝港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(5), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 5);
            InstallDepartureCrewFixtureShipBerths();
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(5));
            Assert.That(controller.Session.IsShipAtCurrentPort(0) && controller.Session.IsShipAtCurrentPort(5), Is.True);
            Assert.That(controller.Session.TryDepositAllPortCargo(), Is.True);
            controller.Session.Cargo.Water = controller.Session.Cargo.Food = 80;
        }

        private static void InstallDepartureCrewFixtureShipBerths()
        {
            // Both owned vessels are deliberately moored locally. Without a saved
            // berth, legacy migration leaves the inactive starter hull at its home port.
            PlayerPrefs.SetString(CoreGameplayDemoController.ShipPortProgressKey, JsonUtility.ToJson(new CoreShipPortProgress
            {
                ships = new[] {
                    new CoreShipPortLocation { shipIndex = 0, port = "白贝港" },
                    new CoreShipPortLocation { shipIndex = 5, port = "白贝港" },
                },
            }));
        }

        private static IEnumerator SetOrdinaryDepartureCount(CoreGameplayDemoController controller, int target)
        {
            for (int remaining = controller.Session.CrewCountValue + 1;
                controller.Session.SelectedOrdinaryCrew != target && remaining > 0; remaining--)
            {
                int before = controller.Session.SelectedOrdinaryCrew;
                bool increase = target > before;
                yield return ClickShipyardControl(increase ? "DepartureOrdinaryCrewIncreaseButton" : "DepartureOrdinaryCrewDecreaseButton");
                Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(before + (increase ? 1 : -1)),
                    "Each main-page click must immediately change the Core crew by exactly one.");
            }
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(target));
        }

        private static void AssertOrdinaryDepartureSpeed(CoreGameplayDemoController controller, int count, float speed)
        {
            Assert.That(controller.Session.SelectedOrdinaryCrew, Is.EqualTo(count));
            Assert.That(controller.Session.EffectiveSpeedKnots, Is.EqualTo(speed).Within(.001f));
            Assert.That(GameObject.Find("DepartureOrdinaryCrewCount").GetComponent<Text>().text,
                Does.StartWith("普通船员 " + count + "/" + controller.Session.CrewCountValue)
                    .And.Contain("特殊 " + controller.Session.SelectedSpecialCrewCount));
            Assert.That(GameObject.Find("DepartureCrewSpeedPreview").GetComponent<Text>().text,
                Does.Contain("航速 " + speed.ToString("0.0") + " 节"));
            Assert.That(GameObject.Find("Ship Stats"), Is.Null,
                "The dedicated speed preview remains the only departure speed label.");
        }

        private static void RecruitDepartureCrewChoice(int profession)
        {
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession("白贝港", profession);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(profession), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(profession), crew.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(crew.Id), "白贝港");
        }

        private static Button DepartureCrewChoiceButton(int profession) => DepartureCrewChoiceButtons()
            .SingleOrDefault(button => CrewNarrativeCatalog.FindById(DepartureCrewChoiceIdentity(button))?.ProfessionIndex == profession);

        private static Button[] DepartureCrewChoiceButtons() => Object.FindFirstObjectByType<CoreGameplayDemoController>()
            .GetComponentsInChildren<Button>(false).Where(button => button.name.StartsWith("Departure Special Crew Toggle "))
            .OrderBy(button => int.Parse(button.name.Substring("Departure Special Crew Toggle ".Length))).ToArray();

        private static string DepartureCrewChoiceIdentity(Button button) => button.GetComponentsInChildren<Text>(true)
            .Single(label => label.name == "Departure Special Crew Identity").text;

        private static Button DepartureCrewChoiceButtonForIdentity(string identity) => DepartureCrewChoiceButtons()
            .SingleOrDefault(button => DepartureCrewChoiceIdentity(button) == identity);

        private static IEnumerator ClickDepartureCrewChoice(int profession)
        {
            Button button = DepartureCrewChoiceButton(profession);
            Assert.That(button, Is.Not.Null, "Expected a local recruited crew member for profession " + profession);
            yield return ClickShipyardControl(button.name);
        }

        private static string DepartureCrewChoiceState(int profession) => DepartureCrewChoiceButton(profession)
            .GetComponentsInChildren<Text>(true).Single(label => label.name.StartsWith("Departure Special Crew State ")).text
            .Replace("✓ ", string.Empty).Split('·')[0].Trim();

        private static void RefreshDepartureCrewChoiceView(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
