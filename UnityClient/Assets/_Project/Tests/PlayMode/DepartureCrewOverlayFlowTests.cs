using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
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
        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrewOverlay_KeyboardAndHeaderCallbacksStayModalAndClosingRestoresControls()
        {
            CrewVariantDefinition[] crew = CrewNarrativeCatalog.AllCrew.Where(item => item.PortName == "断锚港").Take(2).ToArray();
            yield return LoadDepartureCrewOverlayFixture(crew);
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return ClickShipyardControl("Port Hotspot Voyage");
            string[] headerNames = { "Return To Port Hub", "Previous Port Facility", "Next Port Facility" };
            Button[] header = headerNames.Select(ActiveButton).ToArray();
            Button.ButtonClickedEvent[] oldHeaderCallbacks = header.Select(button => button.onClick).ToArray();
            string facility = controller.ActivePortSection;

            foreach (string closeName in new[] { "Back To Voyage Setup", "Confirm Departure Crew" })
            {
                foreach (Button button in header) Assert.That(button.IsInteractable(), Is.True, button.name);
                EventSystem.current.SetSelectedGameObject(ActiveButton("Configure Voyage Crew").gameObject);
                yield return ClickShipyardControl("Configure Voyage Crew");
                Transform overlay = GameObject.Find("Departure Crew Overlay").transform;
                Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(overlay), Is.True);

                // Port presentation reuses its hierarchy after deactivation;
                // the same visible window must reacquire its input barrier.
                controller.gameObject.SetActive(false);
                yield return null;
                EventSystem.current.SetSelectedGameObject(null);
                controller.gameObject.SetActive(true);
                yield return null;
                Assert.That(GameObject.Find("Departure Crew Overlay").transform, Is.SameAs(overlay));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
                Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(overlay), Is.True);
                foreach (Button button in header) Assert.That(button.IsInteractable(), Is.False, button.name);

                foreach (MoveDirection direction in new[] { MoveDirection.Up, MoveDirection.Right, MoveDirection.Down, MoveDirection.Left })
                    for (int step = 0; step < 12; step++)
                    {
                        GameObject selected = EventSystem.current.currentSelectedGameObject;
                        ExecuteEvents.Execute(selected, new AxisEventData(EventSystem.current) { moveDir = direction }, ExecuteEvents.moveHandler);
                        Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(overlay), Is.True,
                            "Automatic navigation must remain inside the crew window.");
                    }

                for (int index = 0; index < header.Length; index++)
                {
                    Assert.That(header[index].IsInteractable(), Is.False, header[index].name);
                    EventSystem.current.SetSelectedGameObject(header[index].gameObject);
                    ExecuteEvents.Execute(header[index].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                    oldHeaderCallbacks[index].Invoke();
                    yield return null;
                    Assert.That(controller.ActivePortSection, Is.EqualTo(facility), header[index].name);
                    Assert.That(GameObject.Find("Departure Crew Overlay").transform, Is.SameAs(overlay));
                }

                int before = controller.Session.SelectedSpecialCrewCount;
                Button crewChoice = DepartureCrewChoiceButtonForIdentity(crew[0].Id);
                EventSystem.current.SetSelectedGameObject(crewChoice.gameObject);
                ExecuteEvents.Execute(crewChoice.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                yield return null;
                Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(before == crew.Length ? before - 1 : before + 1));
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(DepartureCrewChoiceButtonForIdentity(crew[0].Id).gameObject), "Rebuilding the roster must retain keyboard focus on the toggled identity.");
                int configured = controller.Session.SelectedSpecialCrewCount;
                Button close = ActiveButton(closeName);
                EventSystem.current.SetSelectedGameObject(close.gameObject);
                ExecuteEvents.Execute(close.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                yield return null;
                Assert.That(GameObject.Find("Departure Crew Overlay"), Is.Null);
                Assert.That(controller.Session.SelectedSpecialCrewCount, Is.EqualTo(configured));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(ActiveButton("Configure Voyage Crew").gameObject));
                foreach (Button button in header) Assert.That(button.IsInteractable(), Is.True, button.name);
            }

            ExecuteEvents.Execute(header[0].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return null;
            Assert.That(controller.ActivePortSection, Is.EqualTo("港口总览"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrewOverlay_RejectsUnderlyingDepartureShipRouteAndCrewCallbacks()
        {
            CrewVariantDefinition[] crew = CrewNarrativeCatalog.AllCrew.Where(item => item.PortName == "断锚港").Take(5).ToArray();
            yield return LoadDepartureCrewOverlayFixture(crew);
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(3) });
            Assert.That(session.CanSelectRoute(1, out _), Is.True);
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("DepartureOrdinaryCrewDecreaseButton");
            string[] names = { "Start Voyage", "Departure Next Vessel", "Route 1",
                "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton" };
            Button[] controls = names.Select(ActiveButton).ToArray();
            foreach (Button control in controls)
                Assert.That(control.IsInteractable(), Is.True, control.name + " must be usable before the modal opens.");
            Button.ButtonClickedEvent[] callbacks = controls.Select(control => control.onClick).ToArray();
            int ship = session.SelectedShipIndex, route = session.SelectedRouteIndex;
            int ordinary = session.SelectedOrdinaryCrew, special = session.SelectedSpecialCrewCount;
            string destination = session.SelectedDestinationPort;
            InvestmentResourceSnapshot resources = SnapshotInvestmentResources(session);
            yield return ClickShipyardControl("Configure Voyage Crew");
            GameObject modal = GameObject.Find("Departure Crew Configuration");
            for (int index = 0; index < callbacks.Length; index++)
            {
                callbacks[index].Invoke(); yield return null;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port), names[index]);
                Assert.That(session.SelectedShipIndex, Is.EqualTo(ship), names[index]);
                Assert.That(session.SelectedRouteIndex, Is.EqualTo(route), names[index]);
                Assert.That(session.SelectedDestinationPort, Is.EqualTo(destination), names[index]);
                Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(ordinary), names[index]);
                Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(special), names[index]);
                Assert.That(GameObject.Find("Departure Crew Configuration"), Is.SameAs(modal), names[index]);
                AssertInvestmentResourcesEqual(session, resources);
            }
            yield return ClickShipyardControl("Back To Voyage Setup");
            Assert.That(GameObject.Find("Departure Crew Overlay"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrewOverlay_KeepsDockAndChartSelectionAndRejectsClosedCallbacks()
        {
            CrewVariantDefinition[] crew = CrewNarrativeCatalog.AllCrew.Where(item => item.PortName == "断锚港").ToArray();
            yield return LoadDepartureCrewOverlayFixture(crew);
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            yield return ClickShipyardControl("Port Hotspot Voyage");
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            yield return EnterDepartureChartSeed(seed);
            Texture background = GameObject.Find("Voyage Background").GetComponent<RawImage>().texture;
            RectTransform vessel = GameObject.Find("Inspect Departure Vessel").GetComponent<RectTransform>();
            Vector2 vesselSize = vessel.sizeDelta, vesselPosition = vessel.anchoredPosition;
            yield return ClickShipyardControl("Configure Voyage Crew");
            AssertDepartureCrewOverlayDock(background, vesselSize, vesselPosition);
            Assert.That(session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(GameObject.Find("Departure Chart Selection").activeInHierarchy, Is.True);
            Assert.That(DepartureCrewChoiceButtons().Select(DepartureCrewChoiceIdentity),
                Is.EqualTo(CrewNarrativeCatalog.AllCrew.Where(item => crew.Contains(item)).Select(item => item.Id)));
            AssertDepartureCrewOverlayRemovedContent(controller);
            Button.ButtonClickedEvent oldCrew = DepartureCrewChoiceButtonForIdentity(crew.Last().Id).onClick;
            Button.ButtonClickedEvent oldConfirm = ActiveButton("Confirm Departure Crew").onClick;
            yield return WaitForInvestmentAndCrewScreenToSettle("Departure Crew Configuration");
            NavalBattleVisualCapture.Capture(controller, "DepartureCrewOverlay_01_Central.png",
                "船员配置位于中央弹窗，码头底图、船只和已选海图保持原状", "Departure Crew Configuration");
            yield return ClickShipyardControl("Back To Voyage Setup");
            Assert.That(GameObject.Find("Departure Crew Overlay"), Is.Null);
            int ordinary = session.SelectedOrdinaryCrew, special = session.SelectedSpecialCrewCount;
            oldCrew.Invoke(); oldConfirm.Invoke(); yield return null;
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(ordinary));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(special));
            Assert.That(session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(GameObject.Find("Departure Crew Overlay"), Is.Null);
            Assert.That(GameObject.Find("Voyage Background").GetComponent<RawImage>().texture, Is.SameAs(background));
            yield return ClickShipyardControl("Configure Voyage Crew");
            AssertDepartureCrewOverlayDock(background, vesselSize, vesselPosition);
            oldCrew.Invoke(); yield return null;
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(ordinary));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(special));
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(GameObject.Find("Departure Crew Overlay"), Is.Null);
            Assert.That(session.SelectedSeed, Is.EqualTo(seed));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrewOverlay_MoreThanSixIdentitiesScrollAndShareShipCapacity()
        {
            CrewVariantDefinition[] local = CrewNarrativeCatalog.AllCrew.Take(12).ToArray();
            CrewVariantDefinition away = CrewNarrativeCatalog.AllCrew.First(item => !local.Contains(item));
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(away.Id), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(away.Id), "白贝港");
            yield return LoadDepartureCrewOverlayFixture(local);
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            yield return ClickShipyardControl("Port Hotspot Voyage");
            yield return ClickShipyardControl("Configure Voyage Crew");
            Button[] rows = DepartureCrewChoiceButtons();
            Assert.That(rows.Length, Is.EqualTo(12), "All local recruited identities are listed, including repeated professions.");
            Assert.That(rows.Select(DepartureCrewChoiceIdentity), Is.EqualTo(local.Select(item => item.Id)));
            Assert.That(DepartureCrewChoiceButtonForIdentity(away.Id), Is.Null);
            Assert.That(rows.Select(DepartureCrewChoiceIdentity).Distinct().Count(), Is.EqualTo(12));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(10), "Specialists fill the ship's ten berths rather than a six-profession cap.");
            Assert.That(session.SelectedOrdinaryCrew, Is.Zero);
            ScrollRect scroll = GameObject.Find("Departure Special Crew Scroll").GetComponent<ScrollRect>();
            GameObject modal = GameObject.Find("Departure Crew Configuration");
            AssertDepartureCrewOverlayRemovedContent(controller);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0f; yield return null;
            Button last = DepartureCrewChoiceButtonForIdentity(local.Last().Id);
            last.onClick.Invoke(); yield return null;
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(10), "Clicking an eleventh specialist cannot exceed capacity.");
            Assert.That(CrewOverlayRowState(local.Last().Id), Does.Contain("满载"));
            scroll.verticalNormalizedPosition = 1f; yield return null;
            Button first = DepartureCrewChoiceButtonForIdentity(local.First().Id);
            Button.ButtonClickedEvent stale = first.onClick;
            yield return ClickShipyardControl(first.name);
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(9));
            Assert.That(GameObject.Find("Departure Crew Configuration"), Is.SameAs(modal),
                "Changing the roster keeps the secondary window in place.");
            stale.Invoke(); yield return null;
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(9), "A retired row callback cannot board the same identity again.");
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(ActiveButton("DepartureOrdinaryCrewIncreaseButton").interactable, Is.True);
            yield return SetOrdinaryDepartureCount(controller, 1);
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(10));
            yield return ClickShipyardControl("Configure Voyage Crew");
            scroll = GameObject.Find("Departure Special Crew Scroll").GetComponent<ScrollRect>();
            scroll.verticalNormalizedPosition = 0f; yield return null;
            last = DepartureCrewChoiceButtonForIdentity(local.Last().Id);
            yield return ClickShipyardControl(last.name);
            Assert.That(CrewOverlayRowState(local.Last().Id), Does.Contain("已登船"));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(10));
            Assert.That(session.SelectedOrdinaryCrew, Is.Zero, "A newly boarded specialist consumes the ordinary berth.");
            AssertDepartureCrewOverlayRemovedContent(controller);
            NavalBattleVisualCapture.Capture(controller, "DepartureCrewOverlay_02_Scrolled.png",
                "超过六名已招募船员在中央弹窗内滚动，同职业分别占位，末项可登船", "Departure Crew Configuration");
            yield return ClickShipyardControl("Confirm Departure Crew");
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(session.CrewCountValue));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrewOverlay_TwoNavigatorsRemainDistinctAndRevealFourDepartureLocations()
        {
            CrewVariantDefinition[] navigators = CrewNarrativeCatalog.AllCrew.Where(item => item.ProfessionIndex == 0).Take(2).ToArray();
            CrewVariantDefinition doctor = CrewNarrativeCatalog.AllCrew.First(item => item.ProfessionIndex == 2);
            yield return LoadDepartureCrewOverlayFixture(navigators.Concat(new[] { doctor }).ToArray());
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            yield return ClickShipyardControl("Port Hotspot Voyage");
            Assert.That(session.NavigatorCount, Is.EqualTo(2));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(3));
            yield return ClickShipyardControl("Configure Voyage Crew");
            Assert.That(DepartureCrewChoiceButtons().Count(button =>
                CrewNarrativeCatalog.FindById(DepartureCrewChoiceIdentity(button)).ProfessionIndex == 0), Is.EqualTo(2));
            Button second = DepartureCrewChoiceButtonForIdentity(navigators[1].Id);
            yield return ClickShipyardControl(second.name);
            Assert.That(session.NavigatorCount, Is.EqualTo(1));
            Assert.That(session.HasNavigator, Is.True);
            Assert.That(CrewOverlayRowState(navigators[0].Id), Does.Contain("已登船"));
            Assert.That(CrewOverlayRowState(navigators[1].Id), Does.Contain("待命"));
            second = DepartureCrewChoiceButtonForIdentity(navigators[1].Id);
            yield return ClickShipyardControl(second.name);
            Assert.That(session.NavigatorCount, Is.EqualTo(2));
            Assert.That(session.HasDoctor, Is.True);
            yield return ClickShipyardControl("Confirm Departure Crew");
            string seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            yield return EnterDepartureChartSeed(seed);
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.CurrentChartSeed, Is.EqualTo(seed));
            Assert.That(session.NavigatorCount, Is.EqualTo(2), "Presentation must pass the number of selected navigator identities to Core.");
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(3));
            CoreMapNode[] annotations = session.Map.Nodes.Where(node => node.IntelRevealed).ToArray();
            Assert.That(annotations.Length, Is.EqualTo(4), "Two navigators each reveal two initially unknown fixed event locations.");
            Assert.That(annotations.All(node => node.Encounter != null && node.Encounter.HasChartLocation && !node.Visited), Is.True);
            string[] aboard = PlayerPrefs.GetString(CoreGameplayDemoController.CrewAtSeaKey).Split(',');
            Assert.That(aboard, Does.Contain(navigators[0].Id).And.Contain(navigators[1].Id).And.Contain(doctor.Id));
            Assert.That(aboard.Length, Is.EqualTo(3));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadDepartureCrewOverlayFixture(CrewVariantDefinition[] crew)
        {
            // The shared fixture preserves/restores every recruitment, location and profile key.
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.CurrentPortKey, "断锚港");
            PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(5), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, 5);
            foreach (CrewVariantDefinition identity in crew)
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(identity.Id), 1);
                PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(identity.Id), "断锚港");
            }
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(5));
            Assert.That(controller.Session.CrewCountValue, Is.EqualTo(10));
            Assert.That(controller.Session.TryDepositAllPortCargo(), Is.True);
            controller.Session.Cargo.Water = controller.Session.Cargo.Food = 80;
        }

        private static string CrewOverlayRowState(string identity) => DepartureCrewChoiceButtonForIdentity(identity)
            .GetComponentsInChildren<Text>(true).Single(label => label.name.StartsWith("Departure Special Crew State ")).text;

        private static void AssertDepartureCrewOverlayRemovedContent(CoreGameplayDemoController controller)
        {
            string[] removed = { "Departure Ordinary Crew Label", "Departure Ordinary Crew Slider",
                "Departure Ordinary Crew Hint", "Departure Special Crew Heading", "Departure Special Crew Rules" };
            string[] names = controller.GetComponentsInChildren<Transform>(true).Select(item => item.name).ToArray();
            foreach (string name in removed)
                Assert.That(names, Does.Not.Contain(name), "The marked redundant content must be removed, including inactive objects: " + name);
            Image paper = GameObject.Find("Departure Crew Configuration").GetComponent<Image>();
            Assert.That(paper.sprite, Is.SameAs(MarketShopSkin.Paper), "The crew window uses the existing secondary-window paper and wood art.");
            Assert.That(paper.type, Is.EqualTo(Image.Type.Sliced));
        }

        private static void AssertDepartureCrewOverlayDock(Texture background, Vector2 vesselSize, Vector2 vesselPosition)
        {
            Assert.That(GameObject.Find("Voyage Background").GetComponent<RawImage>().texture, Is.SameAs(background));
            RectTransform setup = GameObject.Find("Voyage Setup").GetComponent<RectTransform>();
            RectTransform overlay = GameObject.Find("Departure Crew Overlay").GetComponent<RectTransform>();
            RectTransform card = GameObject.Find("Departure Crew Configuration").GetComponent<RectTransform>();
            Assert.That(overlay.parent, Is.SameAs(setup.parent));
            Assert.That(card.parent, Is.SameAs(overlay));
            Assert.That(card.sizeDelta, Is.EqualTo(new Vector2(840, 500)));
            Assert.That(card.anchoredPosition, Is.EqualTo(new Vector2(24, -40)));
            Assert.That(overlay.GetSiblingIndex(), Is.GreaterThan(setup.GetSiblingIndex()));
            foreach (string name in new[] { "Voyage Setup", "Route Selection" })
            {
                RectTransform root = GameObject.Find(name).GetComponent<RectTransform>();
                Assert.That(root.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize));
                Assert.That(root.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(root.GetComponent<Image>().color.a, Is.Zero);
            }
            RectTransform vessel = GameObject.Find("Inspect Departure Vessel").GetComponent<RectTransform>();
            Assert.That(vessel.sizeDelta, Is.EqualTo(vesselSize));
            Assert.That(vessel.anchoredPosition, Is.EqualTo(vesselPosition));
            foreach (string name in new[] { "Departure Special Crew Scroll", "Confirm Departure Crew", "Back To Voyage Setup" })
                Assert.That(GameObject.Find(name).transform.IsChildOf(card), Is.True, name);
        }
    }
}
