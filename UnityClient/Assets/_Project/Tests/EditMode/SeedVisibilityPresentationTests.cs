using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;
using ShatteredSail.Core.Tutorial;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class SeedVisibilityPresentationTests
    {
        private const string PrivateSeed = "UNDRAWN-CHART-UI-REGRESSION";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void Departure_RequiresAnAvailableNavigatorToShowAndEditTheSeed()
        {
            CreatePortPreview();
            WriteField(controller, "activePortFacility", 7);
            controller.Session.SetSeed(PrivateSeed);

            foreach (bool navigatorAboard in new[] { false, true, false })
            {
                SetNavigator(navigatorAboard);
                Rebuild("BuildPort");
                InputField input = host.GetComponentsInChildren<InputField>(true)
                    .SingleOrDefault(item => item.name == "Seed Input");

                if (navigatorAboard)
                {
                    Assert.That(input, Is.Not.Null);
                    Assert.That(input.enabled, Is.True, "The visible input replaces the old disabled compatibility field.");
                    Assert.That(input.interactable, Is.False, "Random departure is the initial mode; explicit selection enables entry.");
                    Assert.That(Find<Toggle>("Use Chart Seed").isOn, Is.False);
                    Assert.That(input.text, Is.Empty, "Random mode must not silently copy the hidden seed into a chart selection.");
                    Assert.That(Find<Text>("Seed Label").text, Does.Contain("普通海图"));
                }
                else
                {
                    Assert.That(input, Is.Null, "An unavailable seed must not remain in a disabled or hidden input.");
                    Assert.That(Find<Text>("Seed Label").text, Does.Contain("航海士"));
                    Assert.That(Find<Button>("Departure Treasure Seed Picker").IsInteractable(), Is.True,
                        "Treasure seeds remain available without a navigator.");
                    AssertNoVisibleValue(PrivateSeed);
                }

                Assert.That(Find<Button>("Start Voyage").interactable, Is.True,
                    "Chart drawing is optional for departure.");
                Assert.That(controller.Session.SelectedSeed, Is.EqualTo(PrivateSeed),
                    "Changing crew availability must preserve the internal map seed.");
            }
        }

        [Test]
        public void Departure_OffersDestinationPickerAndUsesItForTheMapEndpoint()
        {
            CreatePortPreview();
            WriteField(controller, "activePortFacility", 7);
            Rebuild("BuildPort");

            string initial = controller.Session.SelectedDestinationPort;
            int targetIndex = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count)
                .First(index => CoreVoyageSession.DestinationPorts[index] != initial &&
                    CoreVoyageSession.DestinationPorts[index] != controller.Session.CurrentPort);
            string target = CoreVoyageSession.DestinationPorts[targetIndex];
            Button picker = Find<Button>("Departure Destination " + targetIndex);
            Assert.That(picker.GetComponentInChildren<Text>().text, Is.EqualTo(target));
            picker.onClick.Invoke();
            string selected = controller.Session.SelectedDestinationPort;
            Assert.That(selected, Is.EqualTo(target));
            Assert.That(selected, Is.Not.EqualTo(controller.Session.CurrentPort));

            controller.Session.SetSeed("DESTINATION-PRESENTATION");
            controller.Session.StartVoyage();
            Assert.That(controller.Session.Map.DestinationName, Is.EqualTo(selected));
        }

        [Test]
        public void Departure_ChartModeReplacesLeftRoutesAndShowsSelectedChartAndDirectionalPorts()
        {
            CreatePortPreview();
            controller.ConfigureEditorPreviewDepartureChart(0);
            Transform routes = Find<RectTransform>("Route Selection");
            Transform setup = Find<RectTransform>("Voyage Setup");
            Toggle toggle = Find<Toggle>("Use Chart Seed");
            RectTransform selection = Find<RectTransform>("Departure Chart Selection");
            Assert.That(toggle.transform.parent, Is.SameAs(routes));
            RectTransform toggleRect = toggle.GetComponent<RectTransform>();
            Assert.That(toggleRect.anchoredPosition.y + toggleRect.rect.yMax,
                Is.LessThan(((RectTransform)routes).rect.yMax), "The chart switch belongs inside the current paper frame.");
            Assert.That(toggleRect.anchoredPosition.y, Is.GreaterThan(0f), "The switch stays above the route choices.");
            Assert.That(setup.GetComponentsInChildren<Toggle>(true).Any(item => item.name == "Use Chart Seed"), Is.False);
            Assert.That(setup.GetComponentsInChildren<InputField>(true), Is.Empty);
            Assert.That(selection.gameObject.activeSelf, Is.False);
            AssertDepartureRandomChoicesVisible(true);

            string origin = controller.Session.CurrentPort;
            decimal gold = controller.Session.ProfileGold;
            int water = controller.Session.Cargo.Water;
            toggle.isOn = true;
            Assert.That(selection.gameObject.activeSelf, Is.True);
            AssertDepartureRandomChoicesVisible(false);
            Dropdown dropdown = Find<Dropdown>("Seed Input Dropdown");
            Assert.That(dropdown.IsInteractable(), Is.True);
            Assert.That(dropdown.transform.IsChildOf(selection), Is.True);
            dropdown.value = 1;
            Canvas.ForceUpdateCanvases();

            Assert.That(controller.Session.IsChartSeedSelected, Is.True);
            string destination = controller.Session.SelectedDestinationPort;
            Transform preview = Find<RectTransform>("Departure Chart Preview");
            Assert.That(preview.gameObject.activeInHierarchy, Is.True);
            VoyageChartSurfaceGraphic graphic = preview.GetComponentInChildren<VoyageChartSurfaceGraphic>();
            Assert.That(graphic, Is.Not.Null, "A selected seed must show its generated map thumbnail.");
            Assert.That(graphic.Field, Is.Not.Null);
            Assert.That(graphic.Field.GeometryFingerprint,
                Is.EqualTo(VoyageChartCoastField.Fingerprint(controller.Session.PreviewSelectedSeaChart())),
                "The thumbnail must use the selected seed's geography.");
            Assert.That(Find<Text>("Departure Chart Ports").text,
                Does.Contain("出发港口：" + origin).And.Contain("到达港口：" + destination));
            Assert.That(Find<Text>("Departure Chart Route").text,
                Does.Contain(CoreVoyageSession.Routes[controller.Session.SelectedRouteIndex].Name));
            Assert.That(controller.Session.Map, Is.Null, "Previewing a chart must not start a voyage.");
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));

            controller.Session.RestoreProfile(gold, destination);
            Rebuild("BuildPort");
            Assert.That(Find<Text>("Departure Chart Ports").text,
                Does.Contain("出发港口：" + destination).And.Contain("到达港口：" + origin));
            Find<Toggle>("Use Chart Seed").isOn = false;
            Assert.That(Find<RectTransform>("Departure Chart Selection").gameObject.activeSelf, Is.False);
            AssertDepartureRandomChoicesVisible(true);
            Assert.That(controller.Session.IsChartSeedSelected, Is.False);
        }

        private void AssertDepartureRandomChoicesVisible(bool visible)
        {
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route++)
                Assert.That(Find<Button>("Route " + route).gameObject.activeSelf, Is.EqualTo(visible));
            for (int port = 0; port < CoreVoyageSession.DestinationPorts.Count; port++)
                Assert.That(Find<Button>("Departure Destination " + port).gameObject.activeInHierarchy, Is.EqualTo(visible),
                    "Manual destination plaques are replaced by the selected chart's endpoints in chart mode.");
        }

        [Test]
        public void TreasureTable_DoesNotExposeTheOrdinaryRouteSeed()
        {
            // This fixture exercises the current undrawn chart, independently
            // of a player's previously recorded archive.
            bool hadDrawnChart = PlayerPrefs.HasKey(CoreGameplayDemoController.DrawnSeaChartKey);
            string drawnChart = PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey, string.Empty);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.DrawnSeaChartKey);
            try
            {
                CreatePortPreview();
                WriteField(controller, "activePortFacility", 5);
                controller.Session.SetSeed(PrivateSeed);

                foreach (bool navigatorAboard in new[] { false, true, false })
                {
                    SetNavigator(navigatorAboard);
                    Rebuild("BuildPort");
                    Assert.That(Find<Button>("Treasure Identify Fragment").GetComponentInChildren<Text>().text, Does.Contain("鉴定碎片"));
                    Assert.That(Find<Text>("Treasure Workbench Status").text, Does.Contain("未鉴定碎片"));
                    Assert.That(host.GetComponentsInChildren<Text>(true).Any(label => label.name == "Treasure Route Detail"), Is.False);
                    AssertNoVisibleValue(PrivateSeed);
                }
            }
            finally
            {
                if (hadDrawnChart) PlayerPrefs.SetString(CoreGameplayDemoController.DrawnSeaChartKey, drawnChart);
                else PlayerPrefs.DeleteKey(CoreGameplayDemoController.DrawnSeaChartKey);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Navigation_KeepsSeedAndHashHiddenRegardlessOfDrawingAbility(bool navigatorAboard)
        {
            CreatePortPreview();
            SetNavigator(navigatorAboard);
            controller.Session.SetSeed(PrivateSeed);
            controller.Session.StartVoyage();
            Rebuild("BuildNavigationDashboard");

            string hash = controller.Session.Map.Hash.ToString("X8");
            Assert.That(host.GetComponentsInChildren<Text>(true).Any(label => label.name == "Map Seed"), Is.False);
            AssertNoVisibleValue(PrivateSeed, hash);

            Assert.That(host.GetComponentsInChildren<Text>(true).Any(label => label.name == "Voyage Log"), Is.False);
            Assert.That(controller.Session.Map.Seed, Is.EqualTo(PrivateSeed));
            Assert.That(Find<MapCourseInputSurface>("Map Course Input"), Is.Not.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Navigation_OnboardIncidentNeverCreatesAMarkerOrAcceptsAnEventCourse_EvenWithOldDiscoveryFlags(bool visited)
        {
            CreatePortPreview();
            controller.Session.StartVoyage();
            CoreVoyageMap map = controller.Session.Map;
            CoreMapNode leak = map.Nodes.Single(node => node.Encounter.Id == "event_reef_saltwater_leak");
            CoreMapNode sighted = map.Nodes.Single(node => node.Encounter.Id == "event_reef_broken_mast");
            CoreMapNode hidden = map.Nodes.Single(node => node.Encounter.Id == "event_reef_shortcut");
            VoyagePoint sharedPosition = new VoyagePoint(controller.Session.Position.X + 3f, controller.Session.Position.Y + 3f);
            map.Nodes.Clear();
            map.Nodes.Add(leak);
            map.Nodes.Add(sighted);
            map.Nodes.Add(hidden);
            foreach (CoreMapNode node in map.Nodes) node.Position = sharedPosition;
            leak.Discovered = true;
            leak.IntelRevealed = true;
            leak.Visited = visited;
            sighted.Discovered = true;
            sighted.IntelRevealed = false;
            sighted.Visited = false;
            hidden.Discovered = false;
            hidden.IntelRevealed = true;
            hidden.Visited = false;

            Rebuild("BuildNavigationDashboard");
            AssertNoEventVisual(leak.Id);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Event Label " + sighted.Id), Is.False);
            Assert.That(Find<RawImage>("Event Icon " + sighted.Id).texture, Is.Not.Null);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Event Label " + hidden.Id), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Event Icon " + hidden.Id), Is.False,
                "Intelligence may reveal a location without showing its undiscovered artwork.");
            AssertNoVisibleValue(leak.Encounter.Name, hidden.Encounter.Name);

            SailingDirection headingBefore = controller.Session.Heading;
            int distanceBefore = controller.Session.PlannedDistanceNm;
            Invoke(controller, "SelectCourseToEvent", 0);
            Assert.That(controller.Session.HasDirectCourseTarget, Is.False);
            Assert.That(controller.Session.Heading, Is.EqualTo(headingBefore));
            Assert.That(controller.Session.PlannedDistanceNm, Is.EqualTo(distanceBefore));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Course Target Label"), Is.False);

            Button visibleMarker = Find<Button>("Event Node " + sighted.Id);
            Assert.That(visibleMarker.interactable, Is.True);
            visibleMarker.onClick.Invoke();
            Assert.That(controller.Session.HasDirectCourseTarget, Is.True);
            Assert.That(controller.Session.DirectCourseTarget.X, Is.EqualTo(sighted.Position.X).Within(0.001f));
            Assert.That(controller.Session.DirectCourseTarget.Y, Is.EqualTo(sighted.Position.Y).Within(0.001f));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Course Target Label"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Event Label " + sighted.Id), Is.False);
            AssertNoVisibleValue(leak.Encounter.Name, hidden.Encounter.Name);

            controller.Session.SetHeading(SailingDirection.North);
            Button intelMarker = Find<Button>("Event Node " + hidden.Id);
            Assert.That(intelMarker.interactable, Is.True);
            intelMarker.onClick.Invoke();
            Assert.That(controller.Session.HasDirectCourseTarget, Is.True);
            Assert.That(controller.Session.DirectCourseTarget.X, Is.EqualTo(hidden.Position.X).Within(0.001f));
            Assert.That(controller.Session.DirectCourseTarget.Y, Is.EqualTo(hidden.Position.Y).Within(0.001f));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Course Target Label"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Event Label " + hidden.Id), Is.False);
            AssertNoVisibleValue(leak.Encounter.Name, hidden.Encounter.Name);
            AssertNoEventVisual(leak.Id);
            Assert.That(controller.Session.ActiveEncounter, Is.Null, "Selecting chart markers must not trigger a shipboard incident.");
        }

        [Test]
        public void EmbeddedTutorial_OnboardAndAmbushEventsRemainResolvableWithoutFixedMapMarkers()
        {
            CreatePortPreview();
            TutorialVoyageSession tutorial = controller.TutorialSession;
            tutorial.BeginFirstLeg();
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 0), Is.True);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 1), Is.True);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 2), Is.False);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 3), Is.False);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 4), Is.False);

            tutorial.SailToNext();
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 2), Is.True,
                "Ordinary locations become visible one encounter ahead.");
            tutorial.ResolveChoice(0);
            tutorial.SailToNext();
            tutorial.SkipCurrentEncounter();
            TutorialEncounter onboard = tutorial.SailToNext();
            Assert.That(onboard.Id, Is.EqualTo("tutorial_crew_requirement_event"));
            Rebuild("BuildTutorial");
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 3), Is.False);
            AssertNoTutorialMarker(3);
            AssertNoTutorialMarker(4);
            Assert.That(tutorial.CurrentEncounter, Is.SameAs(onboard));
            Assert.That(tutorial.Phase, Is.EqualTo(TutorialVoyagePhase.ResolvingEncounter));
            Assert.That(Find<Button>("Tutorial Choice 1").interactable, Is.True);

            Assert.That(tutorial.ResolveChoice(1), Is.True);
            TutorialEncounter ambush = tutorial.SailToNext();
            Assert.That(ambush.Id, Is.EqualTo("tutorial_ambush_event"));
            Rebuild("BuildTutorial");
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 4), Is.False);
            AssertNoTutorialMarker(3);
            AssertNoTutorialMarker(4);
            Assert.That(tutorial.CurrentEncounter, Is.SameAs(ambush));
            Assert.That(Find<Button>("Tutorial Choice 0").interactable, Is.True);
            Assert.That(tutorial.ResolveChoice(0), Is.True);
            Assert.That(tutorial.Phase, Is.EqualTo(TutorialVoyagePhase.FirstLegComplete));
        }

        [Test]
        public void TutorialChart_HiddenLocationRequiresIntelligenceOrActualEncounter_AndDoesNotConsumeTheEncounter()
        {
            TutorialVoyageSession tutorial = new TutorialVoyageSession();
            CompleteFirstTutorialLeg(tutorial);
            tutorial.BeginAdvancedLeg();
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 0), Is.True);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 1), Is.True);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 2), Is.False);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 3), Is.False);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 4), Is.True, "The destination remains visible.");

            TutorialEncounter navigatorEvent = tutorial.SailToNext();
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 2), Is.False,
                "A hidden place cannot use the ordinary one-node-ahead preview.");
            Assert.That(tutorial.CurrentEncounter, Is.SameAs(navigatorEvent));
            Assert.That(tutorial.ResolveChoice(0), Is.True);
            Assert.That(tutorial.HiddenLocationRevealed, Is.True);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 2), Is.True);

            TutorialEncounter hiddenEncounter = tutorial.SailToNext();
            Assert.That(hiddenEncounter.Id, Is.EqualTo("tutorial_hidden_event"));
            // Model arriving without retained intelligence: the active encounter itself
            // is sufficient evidence for its location and must remain resolvable.
            typeof(TutorialVoyageSession).GetProperty("HiddenLocationRevealed")
                .GetSetMethod(true).Invoke(tutorial, new object[] { false });
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 2), Is.True);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 3), Is.True);
            Assert.That(tutorial.CurrentEncounter, Is.SameAs(hiddenEncounter));
            Assert.That(tutorial.Phase, Is.EqualTo(TutorialVoyagePhase.ResolvingEncounter));
            Assert.That(tutorial.ResolveChoice(0), Is.True);
        }

        [Test]
        public void TutorialChart_RejectsMissingSessionsAndOutOfRangeMarkers()
        {
            Assert.That(TutorialChartVisibility.IsNodeVisible(null, 0), Is.False);
            TutorialVoyageSession tutorial = new TutorialVoyageSession();
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, -1), Is.False);
            Assert.That(TutorialChartVisibility.IsNodeVisible(tutorial, 5), Is.False);
        }

        [Test]
        public void EmbeddedTutorial_RevealsTheAdvancedSeedOnlyAfterTheNavigatorJoins_AndHidesItOnReset()
        {
            CreatePortPreview();
            TutorialVoyageSession tutorial = controller.TutorialSession;
            Rebuild("BuildTutorial");
            AssertTutorialSeedsHidden();

            CompleteFirstTutorialLeg(tutorial);
            Rebuild("BuildTutorial");
            AssertTutorialSeedsHidden();

            tutorial.BeginAdvancedLeg();
            Rebuild("BuildTutorial");
            Assert.That(Find<Text>("Tutorial Map Title").text, Does.Contain(tutorial.Seed));
            Assert.That(tutorial.ChartSaved, Is.False, "Seed visibility must not wait for tutorial completion.");

            tutorial.Reset();
            Rebuild("BuildTutorial");
            AssertTutorialSeedsHidden();
        }

        [Test]
        public void StandaloneTutorial_SubtitleHidesBothSeedAndHashUntilTheAdvancedLeg()
        {
            Assert.That(Application.isPlaying, Is.False);
            host = new GameObject("Standalone Tutorial Seed Visibility Test");
            TutorialVoyageController tutorialController = host.AddComponent<TutorialVoyageController>();
            TutorialVoyageSession tutorial = new TutorialVoyageSession();
            WriteField(tutorialController, "session", tutorial);
            // Build real text controls without Awake's generated textures or global EventSystem.
            Invoke(tutorialController, "BuildInterface");
            RenderStandaloneTutorial(tutorialController, tutorial);
            AssertTutorialSeedsHidden();

            CompleteFirstTutorialLeg(tutorial);
            RenderStandaloneTutorial(tutorialController, tutorial);
            AssertTutorialSeedsHidden();

            tutorial.BeginAdvancedLeg();
            RenderStandaloneTutorial(tutorialController, tutorial);
            string subtitle = Find<Text>("Subtitle").text;
            Assert.That(subtitle, Does.Contain(tutorial.Seed));
            Assert.That(subtitle, Does.Contain(TutorialHash(tutorial.Seed)));
            Assert.That(tutorial.ChartSaved, Is.False);

            tutorial.Reset();
            RenderStandaloneTutorial(tutorialController, tutorial);
            AssertTutorialSeedsHidden();
        }

        private void CreatePortPreview()
        {
            Assert.That(Application.isPlaying, Is.False);
            host = new GameObject("Seed Visibility Presentation Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            Assert.That(controller.Session, Is.Not.Null);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
        }

        private void SetNavigator(bool available)
        {
            controller.Session.ConfigureCrewCapabilities(available, false, false, false, false, false);
            Assert.That(controller.Session.CanDrawSeaChart, Is.EqualTo(available));
        }

        private void Rebuild(string builder)
        {
            RectTransform content = ReadField<RectTransform>(controller, "contentRoot");
            for (int index = content.childCount - 1; index >= 0; index--)
                Object.DestroyImmediate(content.GetChild(index).gameObject);
            // Rebuilding only the page preserves the explicitly configured crew fixture.
            // RefreshEditorPreview would recreate the session with a navigator aboard.
            Invoke(controller, builder);
            Canvas.ForceUpdateCanvases();
        }

        private static void RenderStandaloneTutorial(TutorialVoyageController target, TutorialVoyageSession tutorial)
        {
            // Route geometry is unrelated to subtitle disclosure and uses deferred Destroy.
            // Skip that geometry pass so this EditMode fixture needs no runtime lifecycle.
            WriteField(target, "renderedLeg", tutorial.Leg == 2 ? 2 : 1);
            Invoke(target, "Render");
        }

        private static void CompleteFirstTutorialLeg(TutorialVoyageSession tutorial)
        {
            tutorial.BeginFirstLeg();
            tutorial.SailToNext();
            Assert.That(tutorial.ResolveChoice(0), Is.True);
            tutorial.SailToNext();
            Assert.That(tutorial.SkipCurrentEncounter(), Is.True);
            tutorial.SailToNext();
            Assert.That(tutorial.ResolveChoice(1), Is.True);
            tutorial.SailToNext();
            Assert.That(tutorial.ResolveChoice(0), Is.True);
            Assert.That(tutorial.Phase, Is.EqualTo(TutorialVoyagePhase.FirstLegComplete));
        }

        private void AssertTutorialSeedsHidden()
        {
            AssertNoVisibleValue(TutorialVoyageSession.FirstLegSeed, TutorialVoyageSession.AdvancedLegSeed,
                TutorialHash(TutorialVoyageSession.FirstLegSeed), TutorialHash(TutorialVoyageSession.AdvancedLegSeed));
        }

        private void AssertNoEventVisual(int nodeId)
        {
            string[] forbidden = { "Event Node " + nodeId, "Event Label " + nodeId, "Event Icon " + nodeId };
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => forbidden.Contains(item.name)), Is.False,
                "Shipboard problems have no map marker, label, or icon, including stale inactive controls.");
        }

        private void AssertNoTutorialMarker(int markerIndex)
        {
            string[] forbidden = { "Tutorial Node " + markerIndex, "Tutorial Node Label " + markerIndex };
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => forbidden.Contains(item.name)), Is.False);
        }

        private void AssertNoVisibleValue(params string[] values)
        {
            Text[] texts = host.GetComponentsInChildren<Text>(true)
                .Where(item => item.isActiveAndEnabled && item.gameObject.activeInHierarchy).ToArray();
            InputField[] inputs = host.GetComponentsInChildren<InputField>(true)
                .Where(item => item.isActiveAndEnabled && item.gameObject.activeInHierarchy).ToArray();
            Assert.That(texts, Is.Not.Empty, "A hidden page must not produce a vacuous disclosure check.");
            foreach (string value in values)
            {
                foreach (Text text in texts)
                    Assert.That(text.text, Does.Not.Contain(value), text.name + " disclosed an unavailable seed or fingerprint.");
                foreach (InputField input in inputs)
                    Assert.That(input.text, Does.Not.Contain(value), input.name + " disclosed an unavailable seed.");
            }
        }

        private T Find<T>(string name) where T : Component =>
            host.GetComponentsInChildren<T>(true).Single(item => item.name == name);

        private static string TutorialHash(string seed) =>
            SeedHash.Compute("route_shattered_sail_reef::" + seed).ToString("X8");

        private static T ReadField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }

        private static void WriteField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, PrivateInstance);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(target, arguments);
        }
    }
}
