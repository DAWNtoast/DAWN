using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DepartureDockSceneLayoutTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void BrokenAnchorUsesAnEmptyBerthWithIndependentVesselAndNonBlockingSceneRoots()
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            RectTransform route = Find<RectTransform>("Route Selection");
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            RawImage background = Find<RawImage>("Voyage Background");
            RawImage hull = Hull();
            DepartureDockForegroundGraphic foreground = Find<DepartureDockForegroundGraphic>("Departure Dock Foreground");

            foreach (RectTransform root in new[] { route, setup })
            {
                Image image = root.GetComponent<Image>();
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.color.a, Is.Zero);
                Assert.That(image.raycastTarget, Is.False, root.name + " must not intercept the other scene's controls.");
                Assert.That(root.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize));
                Assert.That(root.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(root.parent, Is.SameAs(background.transform.parent),
                    "Existing departure state refreshes locate the original direct parents.");
            }
            Assert.That(background.texture, Is.SameAs(Resources.Load<Texture2D>(
                "PortFacilities/Regional/Voyage/voyage_broken_anchor_scene_v4")));
            Assert.That(background.texture.width, Is.EqualTo(3840));
            Assert.That(background.texture.height, Is.EqualTo(2160));
            Assert.That(background.rectTransform.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize));
            Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(background.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(background.raycastTarget, Is.False);
            Assert.That(hull.texture, Is.Not.SameAs(background.texture));
            Assert.That(hull.transform.IsChildOf(Find<Button>("Inspect Departure Vessel").transform), Is.True);
            Assert.That(hull.raycastTarget, Is.False);
            Assert.That(foreground.mainTexture, Is.SameAs(background.texture));
            Assert.That(foreground.GetComponent<CanvasRenderer>(), Is.Not.Null,
                "The fixed pier needs a canvas renderer so it actually occludes the independent vessel.");
            Assert.That(foreground.raycastTarget, Is.False);
            Assert.That(foreground.transform.GetSiblingIndex(), Is.GreaterThan(Find<RectTransform>("Inspect Departure Vessel").GetSiblingIndex()));
            Assert.That(route.GetSiblingIndex(), Is.GreaterThan(setup.GetSiblingIndex()),
                "The sea-chart table remains above the ship and fixed foreground.");
            RawImage selectedSea = Find<RawImage>("Departure Dock Selected Sea");
            Assert.That(selectedSea.enabled, Is.True, "The selected chart is an independent live image.");
            Assert.That(selectedSea.texture, Is.Not.SameAs(background.texture));
            Assert.That(selectedSea.texture.width, Is.EqualTo(3840));
            Assert.That(selectedSea.texture.height, Is.EqualTo(2160));
            Assert.That(selectedSea.raycastTarget, Is.False);
            Assert.That(Find<Text>("Departure Dock Sea Detail").text,
                Does.Contain(CoreVoyageSession.Routes[controller.Session.SelectedRouteIndex].BossName));
            foreach (string name in new[] { "Departure Buy Provisions", "Adjust Voyage Supplies", "Configure Voyage Battle Decks",
                "Configure Voyage Crew", "Start Voyage" })
            {
                Assert.That(Find<Button>(name).transform.parent, Is.SameAs(setup), name);
                AssertPaintedButton(Find<Button>(name));
            }
            Assert.That(Find<Button>("Inspect Departure Vessel").transform.parent, Is.SameAs(setup));
            foreach (string name in new[] { "Use Chart Seed", "Departure Treasure Seed Picker", "Departure Route Summary" })
                Assert.That(Find<RectTransform>(name).parent, Is.SameAs(route), name);
        }

        [Test]
        public void SharedHeaderKeepsLiveTextAndOriginalVisibleControlsDuringRepeatedDockRefresh()
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            Transform header = Find<RectTransform>("Port Facility Header");
            Text time = Find<Text>("Port Facility Time");
            Text gold = Find<Text>("Port Facility Gold");
            string[] names = { "Return To Port Hub", "Previous Port Facility", "Next Port Facility", "Port Facility Settings" };
            Button[] buttons = names.Select(name => Find<Button>(name)).ToArray();
            Vector2[] positions = buttons.Select(button => button.GetComponent<RectTransform>().anchoredPosition).ToArray();
            Assert.That(header.GetComponent<Image>().enabled, Is.True);
            Assert.That(Find<Image>("Port Facility Frame").enabled, Is.True);
            Assert.That(Find<Text>("Port Facility Title").enabled, Is.True);
            Assert.That(Find<Text>("Port Facility Port").enabled, Is.True);
            Assert.That(time.enabled && gold.enabled, Is.True);
            Assert.That(time.text, Does.Contain(CoreVoyageSession.FormatGameDate(controller.Session.GameDateTime, true)));
            Assert.That(time.text, Does.Contain("游戏日期"));
            Assert.That(gold.text, Does.Contain(PortMarketCatalog.FormatPrice(controller.Session.ProfileGold)));
            controller.Session.RestoreProfile(1234.5678m, controller.Session.CurrentPort);
            typeof(CoreGameplayDemoController).GetMethod("UpdatePortHeaderReadouts", PrivateInstance).Invoke(controller, new object[] { true });
            for (int pass = 0; pass < 3; pass++)
            {
                ApplyPresentation();
                Canvas.ForceUpdateCanvases();
                Assert.That(gold.text, Does.Contain(PortMarketCatalog.FormatPrice(1234.5678m)),
                    "The common wallet readout must continue to reflect purchases.");
                Assert.That(time.text, Does.Contain("游戏日期"));
                for (int index = 0; index < buttons.Length; index++)
                {
                    Assert.That(Find<Button>(names[index]), Is.SameAs(buttons[index]));
                    Assert.That(buttons[index].GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(positions[index]));
                    Assert.That(buttons[index].GetComponent<Image>().color.a, Is.GreaterThan(0f));
                    Assert.That(buttons[index].GetComponentInChildren<Text>(true).enabled, Is.True);
                    Assert.That(buttons[index].GetComponentsInChildren<DeparturePaintedHotspot>(true).All(item => !item.enabled), Is.True);
                }
            }
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewPort.WhiteShell, 0);
            Canvas.ForceUpdateCanvases();
            for (int index = 0; index < names.Length; index++)
                Assert.That(Find<Button>(names[index]).GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(positions[index]),
                    "Dock navigation uses the same header placement as other port scenes.");
        }

        [Test]
        public void SharedHeaderRendersAboveSceneRootsAndBelowLaterModals()
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            Transform header = Find<RectTransform>("Port Facility Header");
            Transform route = Find<RectTransform>("Route Selection");
            Transform setup = Find<RectTransform>("Voyage Setup");
            Assert.That(header.parent, Is.SameAs(route.parent));
            Assert.That(header.parent, Is.SameAs(setup.parent));
            var modal = new GameObject("Departure Header Test Modal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(header.parent, false);
            for (int pass = 0; pass < 3; pass++)
            {
                ApplyPresentation();
                int lastSceneIndex = Mathf.Max(route.GetSiblingIndex(), setup.GetSiblingIndex());
                Assert.That(header.GetSiblingIndex(), Is.EqualTo(lastSceneIndex + 1),
                    "The common header remains above the dock layers after state refreshes.");
                Assert.That(header.GetSiblingIndex(), Is.LessThan(modal.transform.GetSiblingIndex()),
                    "Header buttons must remain under later modal overlays.");
            }
        }

        [Test]
        public void SelectingEachRegionChangesOnlyItsIndependent4KChartAndLiveDescription()
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
            Render();
            Texture background = Find<RawImage>("Voyage Background").texture;
            string[] resources = { "chart-shattered-reef", "chart-golden-trade", "chart-moon-ruins" };
            for (int index = 0; index < resources.Length; index++)
            {
                Button route = Find<Button>("Route " + index);
                Assert.That(route.IsInteractable(), Is.True);
                route.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(index));
                RawImage sea = Find<RawImage>("Departure Dock Selected Sea");
                Texture2D expected = Resources.Load<Texture2D>("PortFacilities/DepartureCharts4K/" + resources[index]);
                Assert.That(expected, Is.Not.Null);
                Assert.That(sea.texture, Is.SameAs(expected));
                Assert.That(sea.texture.width, Is.EqualTo(3840));
                Assert.That(sea.texture.height, Is.EqualTo(2160));
                Assert.That(sea.isActiveAndEnabled, Is.True);
                Assert.That(sea.raycastTarget, Is.False);
                Assert.That(Find<RawImage>("Voyage Background").texture, Is.SameAs(background));
                Text detail = Find<Text>("Departure Dock Sea Detail");
                Assert.That(detail.isActiveAndEnabled, Is.True);
                Assert.That(detail.text, Does.Contain(CoreVoyageSession.Routes[index].Name));
                Assert.That(detail.text, Does.Contain(CoreVoyageSession.Routes[index].BossName));
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void EveryVesselKeepsItsOwnTextureAndAtlasCellAtTheCommonBerth(int shipIndex)
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            Assert.That(controller.Session.TryDepositAllPortCargo(), Is.True);
            Assert.That(controller.Session.SelectShip(shipIndex), Is.True);
            Render();
            Canvas.ForceUpdateCanvases();
            RawImage hull = Hull();
            RectTransform ship = Find<RectTransform>("Starter Modular Ship");
            Assert.That(Find<RectTransform>("Inspect Departure Vessel").sizeDelta,
                Is.EqualTo(DepartureDockSceneLayout.ShipHitSize));
            Assert.That(Find<RectTransform>("Inspect Departure Vessel").anchoredPosition,
                Is.EqualTo(DepartureDockSceneLayout.ShipHitPosition));
            var catalog = new PortArtCatalog();
            Texture2D expectedTexture = shipIndex == 0
                ? Resources.Load<Texture2D>("ModularShip/light_sloop_base") : catalog.ShipTexture(shipIndex);
            Rect expectedUv = shipIndex == 0 ? new Rect(0f, 0f, 1f, 1f)
                : UiImageAspect.InsetUv(expectedTexture, catalog.ShipUv(shipIndex), 10f);
            Assert.That(hull.texture, Is.SameAs(expectedTexture));
            Assert.That(hull.uvRect, Is.EqualTo(expectedUv), "The berth must not crop an atlas cell for a second time.");
            if (shipIndex > 0)
                Assert.That(hull.GetComponent<BattleShipCutout>()?.OwnedMaterial, Is.Not.Null,
                    "Atlas vessels need their existing transparent cutout material.");
            AssertWaterContact(hull);
            Assert.That(hull.rectTransform.rect.width, Is.GreaterThan(399f), "The ship must remain scene-sized, not a setup thumbnail.");
            // Atlas cells include transparent padding (the Salt Mountain cell
            // has about one third). The capture suite checks actual alpha bounds.
            Assert.That(hull.rectTransform.rect.height, Is.InRange(349f, DepartureDockShipLayout.MaxVisibleSize.y * 1.5f));
            Assert.That(hull.rectTransform.rect.width, Is.LessThanOrEqualTo(DepartureDockShipLayout.MaxVisibleSize.x * 1.3f));
            float sourceAspect = Mathf.Abs(expectedUv.width * expectedTexture.width / (expectedUv.height * expectedTexture.height));
            Assert.That(hull.rectTransform.rect.width / hull.rectTransform.rect.height,
                Is.EqualTo(sourceAspect).Within(.0001f), "The vessel must not stretch to fill the berth.");
            Assert.That(Find<Text>("Setup Title").text, Does.Contain(CoreVoyageSession.Ships[shipIndex].Name));

            Vector2 hullSize = hull.rectTransform.sizeDelta;
            Vector3 hullPosition = hull.rectTransform.position;
            Vector2 shipPosition = ship.anchoredPosition;
            int graphicCount = host.GetComponentsInChildren<Graphic>(true).Length;
            for (int pass = 0; pass < 3; pass++)
            {
                ApplyPresentation();
                Assert.That(hull.texture, Is.SameAs(expectedTexture));
                Assert.That(hull.uvRect, Is.EqualTo(expectedUv));
                Assert.That(hull.rectTransform.sizeDelta.x, Is.EqualTo(hullSize.x).Within(.01f));
                Assert.That(hull.rectTransform.sizeDelta.y, Is.EqualTo(hullSize.y).Within(.01f));
                Assert.That(Vector3.Distance(hull.rectTransform.position, hullPosition), Is.LessThan(.01f));
                Assert.That(Vector2.Distance(ship.anchoredPosition, shipPosition), Is.LessThan(.01f));
                Assert.That(host.GetComponentsInChildren<Graphic>(true).Length, Is.EqualTo(graphicCount),
                    "Refreshing quotes or chart state must reuse scene labels and layers.");
                AssertWaterContact(hull);
            }
        }

        [Test]
        public void ChartModeRestoresLiveRouteArtAndReturnsToTheSamePaintedTable()
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            RectTransform route = Find<RectTransform>("Route Selection");
            Vector2 tableSize = route.sizeDelta;
            Rect[] crops = Enumerable.Range(0, 3).Select(index => Find<RawImage>("Route Preview " + index).uvRect).ToArray();
            for (int pass = 0; pass < 3; pass++)
            {
                ApplyPresentation();
                for (int index = 0; index < crops.Length; index++)
                {
                    RawImage image = Find<RawImage>("Route Preview " + index);
                    Assert.That(image.uvRect, Is.EqualTo(crops[index]));
                    Assert.That(image.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                    Assert.That(image.texture, Is.Not.Null, "The expanded editor retains the actual route asset.");
                    Assert.That(image.enabled, Is.False, "The compact card uses its painted illustration.");
                    AssertPaintedButton(Find<Button>("Route " + index));
                }
            }
            controller.ConfigureEditorPreviewDepartureChart(1);
            // Exercise the in-place toggle path as well as a newly built preview.
            // It refreshes controls without destroying and recreating the scene.
            Find<Toggle>("Use Chart Seed").isOn = false;
            Find<Toggle>("Use Chart Seed").isOn = true;
            route = Find<RectTransform>("Route Selection");
            AssertSharedPaperPanels(false);
            Assert.That(Find<InputField>("Seed Input").IsInteractable(), Is.True);
            Assert.That(Find<Button>("Start Voyage").IsInteractable(), Is.True);
            Assert.That(Find<Button>("Departure Treasure Seed Picker").transform.Find("Label").GetComponent<Text>().text,
                Does.Contain(controller.Session.TreasureMaps.Count + " 张可用"),
                "Switching from compact captions to the chart editor must restore the live map inventory count.");
            Find<Toggle>("Use Chart Seed").isOn = false;
            route = Find<RectTransform>("Route Selection");
            Assert.That(route.sizeDelta, Is.EqualTo(tableSize));
            Assert.That(route.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(Find<RectTransform>("Voyage Setup").sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize));
            Assert.That(Find<Image>("Voyage Setup").color.a, Is.Zero);
            Assert.That(Find<RawImage>("Departure Dock Selected Sea").isActiveAndEnabled, Is.True);
            Assert.That(Find<Text>("Departure Dock Sea Detail").enabled, Is.True);
            for (int index = 0; index < crops.Length; index++)
            {
                RawImage art = Find<RawImage>("Route Preview " + index);
                Assert.That(art.uvRect, Is.EqualTo(crops[index]));
                Assert.That(art.enabled, Is.False);
                AssertPaintedButton(Find<Button>("Route " + index));
            }
            AssertWaterContact(Hull());
        }

        [Test]
        public void TreasureDetailsUseBothOriginalPaperPanelsInsteadOfTheSceneBerth()
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 1500,
                maps = new[] { new CoreTreasureMapSave
                    { id = "dock-scene-treasure", rankId = "weathered", seed = "DOCK-SCENE-TREASURE" } }
            });
            Assert.That(controller.Session.TrySelectTreasureSeed("DOCK-SCENE-TREASURE", out string reason), Is.True, reason);
            Render();
            Canvas.ForceUpdateCanvases();
            AssertSharedPaperPanels(false);
            foreach (string name in new[] { "Treasure Departure Title", "Treasure Departure Art", "Treasure Departure Detail",
                "Treasure Departure Consumption", "Cancel Treasure Voyage", "Return To Treasure Table" })
                Assert.That(Find<RectTransform>(name).gameObject.activeInHierarchy, Is.True);
            Assert.That(Find<Button>("Start Voyage").GetComponentInChildren<Text>().text, Does.Contain("消耗藏宝图"));
        }

        [Test]
        public void CrewConfigurationUsesBothOriginalPaperPanelsAndHidesTheSceneOnlyLayers()
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            Find<Button>("Configure Voyage Crew").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            AssertSharedPaperPanels(true);
            for (int index = 0; index < CoreVoyageSession.Routes.Count; index++)
            {
                Text routeLabel = Find<Button>("Route " + index).transform.Find("Label").GetComponent<Text>();
                CoreRouteDefinition route = CoreVoyageSession.Routes[index];
                Assert.That(routeLabel.enabled, Is.True, "The expanded editor must restore the live route label.");
                Assert.That(routeLabel.text, Does.Contain(route.Theme), "The crew editor retains the full route explanation.");
                Assert.That(routeLabel.text, Does.Contain(route.BossName), "The crew editor retains the region's boss.");
                Assert.That(Find<RawImage>("Route Preview " + index).enabled, Is.True,
                    "The expanded editor must restore the original route illustration.");
                Assert.That(Find<RawImage>("Route Preview " + index).uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                    "Crew construction must not leave compact scene crops on the expanded route cards.");
            }
            Assert.That(Find<Slider>("Departure Ordinary Crew Slider"), Is.Not.Null);
            for (int index = 0; index < 6; index++)
                Assert.That(Find<Button>("Departure Special Crew Toggle " + index), Is.Not.Null);
            Assert.That(Find<Button>("Back To Voyage Setup"), Is.Not.Null);
            Assert.That(Find<Button>("Confirm Departure Crew"), Is.Not.Null);
            controller.ConfigureEditorPreviewDepartureChart(0);
            Assert.That(Find<Image>("Route Selection").color.a, Is.Zero);
            Assert.That(Find<Image>("Voyage Setup").color.a, Is.Zero);
            AssertWaterContact(Hull());
        }

        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void OtherPortsRetainTheSharedPaperLayoutAndTheirOwnBackground(CoreGameplayPreviewPort port)
        {
            Preview(port);
            RectTransform route = Find<RectTransform>("Route Selection");
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            Assert.That(route.sizeDelta, Is.EqualTo(DepartureLayout.RoutePanelSize));
            Assert.That(route.anchoredPosition, Is.EqualTo(DepartureLayout.RoutePanelPosition));
            Assert.That(setup.sizeDelta, Is.EqualTo(DepartureLayout.SetupPanelSize));
            Assert.That(setup.anchoredPosition, Is.EqualTo(DepartureLayout.SetupPanelPosition));
            foreach (RectTransform root in new[] { route, setup })
            {
                Assert.That(root.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(root.GetComponent<Image>().color.a, Is.EqualTo(1f));
            }
            Assert.That(host.GetComponentsInChildren<DepartureDockForegroundGraphic>(true), Is.Empty);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Departure Dock Selected Sea"), Is.False);
            Assert.That(Find<RectTransform>("Inspect Departure Vessel").sizeDelta, Is.EqualTo(DepartureLayout.ShipPreviewSize));
            RawImage background = Find<RawImage>("Voyage Background");
            Assert.That(background.texture, Is.SameAs(new PortFacilityArtCatalog().RegionalBackground(
                PortFacilityArtCatalog.VoyageFacilityIndex, controller.Session.CurrentPort)));
            Assert.That(background.rectTransform.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.BackgroundSize));
        }

        private void Preview(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Departure Dock Scene Layout Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, port, 0);
            host.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
        }

        private void AssertSharedPaperPanels(bool crew)
        {
            RectTransform route = Find<RectTransform>("Route Selection");
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            Assert.That(route.sizeDelta, Is.EqualTo(DepartureLayout.RoutePanelSize));
            Assert.That(route.anchoredPosition, Is.EqualTo(DepartureLayout.RoutePanelPosition));
            Assert.That(setup.sizeDelta, Is.EqualTo(DepartureLayout.SetupPanelSize));
            Assert.That(setup.anchoredPosition, Is.EqualTo(DepartureLayout.SetupPanelPosition));
            foreach (RectTransform panel in new[] { route, setup })
            {
                Image paper = panel.GetComponent<Image>();
                Assert.That(paper.sprite, Is.Not.Null, panel.name);
                Assert.That(paper.color.a, Is.EqualTo(1f), panel.name);
                Assert.That(paper.raycastTarget, Is.True, panel.name + " must block clicks into the scene behind its paper.");
            }
            string[] sceneLayers = { "Departure Dock Selected Sea", "Departure Dock Sea Detail", "Departure Dock Foreground",
                "Departure Dock Ship Hint" };
            foreach (Transform layer in host.GetComponentsInChildren<Transform>(true).Where(item => sceneLayers.Contains(item.name)))
                Assert.That(layer.gameObject.activeInHierarchy, Is.False, layer.name);
            if (crew) return;
            RectTransform inspect = Find<RectTransform>("Inspect Departure Vessel");
            Assert.That(inspect.sizeDelta, Is.EqualTo(DepartureLayout.ShipPreviewSize));
            Assert.That(inspect.anchoredPosition, Is.EqualTo(DepartureLayout.ShipPreviewPosition));
            RectTransform ship = Find<RectTransform>("Starter Modular Ship");
            Assert.That(ship.sizeDelta, Is.EqualTo(DepartureLayout.ShipPreviewSize));
            Assert.That(ship.anchoredPosition, Is.EqualTo(Vector2.zero), "The miniature must not inherit the berth waterline offset.");
            RawImage hull = Hull();
            Assert.That(hull.rectTransform.rect.width, Is.LessThanOrEqualTo(DepartureLayout.ShipPreviewSize.x + .1f));
            Assert.That(hull.rectTransform.rect.height, Is.LessThanOrEqualTo(DepartureLayout.ShipPreviewSize.y + .1f));
        }

        private RawImage Hull() => Find<RectTransform>("Starter Modular Ship").GetComponentsInChildren<RawImage>(true)
            .Single(image => image.name.EndsWith(" Base") || image.name.EndsWith(" Hull"));

        private void AssertWaterContact(RawImage hull)
        {
            RectTransform water = Find<RectTransform>("Departure Dock Water Contact");
            Assert.That(water.anchoredPosition.x, Is.EqualTo(DepartureDockSceneLayout.ShipCenterX).Within(.01f));
            Assert.That(water.anchoredPosition.y, Is.EqualTo(DepartureDockSceneLayout.ShipWaterlineY).Within(.01f));
            Vector3 contact = water.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
            Assert.That(contact.y, Is.EqualTo(0f).Within(.1f),
                "All source images must place their authored keel on the same berth waterline.");
        }

        private static void AssertPaintedButton(Button button)
        {
            Image hit = button.GetComponent<Image>();
            Assert.That(hit.enabled, Is.True, button.name);
            Assert.That(hit.color.a, Is.Zero, button.name + " must not repaint its old rectangular face.");
            Assert.That(hit.raycastTarget, Is.True, button.name);
            Assert.That(button.targetGraphic, Is.SameAs(hit));
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
            Assert.That(button.GetComponentInChildren<DeparturePaintedHotspot>(), Is.Not.Null);
            Text label = button.transform.Find("Label")?.GetComponent<Text>();
            Assert.That(label, Is.Not.Null, button.name + " requires an independent caption.");
            Assert.That(label.isActiveAndEnabled, Is.True, button.name);
            Assert.That(label.text, Is.Not.Empty, button.name);
            foreach (NauticalButtonMotion motion in button.GetComponentsInChildren<NauticalButtonMotion>(true))
                Assert.That(motion.enabled, Is.False, "Painted signs must not move away from their authored scenery.");
        }

        private void Render() => typeof(CoreGameplayDemoController).GetMethod("Render", PrivateInstance).Invoke(controller, null);

        private void ApplyPresentation() => typeof(CoreGameplayDemoController).GetMethod("ApplyDeparturePresentation", PrivateInstance)
            .Invoke(controller, new object[] { Find<RectTransform>("Route Selection"), Find<RectTransform>("Voyage Setup") });

        private T Find<T>(string name, bool activeOnly = true) where T : Component
        {
            T result = host.GetComponentsInChildren<T>(true)
                .SingleOrDefault(item => item.name == name && (!activeOnly || item.gameObject.activeInHierarchy));
            Assert.That(result, Is.Not.Null, name);
            return result;
        }
    }
}
