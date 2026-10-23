using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DepartureTreasureSeedSelectionTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void OwnedSeedsCanBeSelectedSwitchedAndRemovedAtEveryPortWithoutNavigator(CoreGameplayPreviewPort port)
        {
            Create(port, 2, false);
            Assert.That(controller.Session.CanDrawSeaChart, Is.False);
            string origin = controller.Session.CurrentPort;
            Select(0);
            Assert.That(controller.Session.SelectedTreasureMap.Seed, Is.EqualTo("DEPARTURE-TREASURE-0"));
            Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(2), "Choosing a seed does not consume it.");
            Assert.That(Find<Text>("Departure Dock Treasure Detail").text, Does.Contain(origin));
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(origin));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Destination Summary"), Is.False);
            foreach (Button destination in host.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Departure Destination ")))
            {
                Assert.That(destination.IsInteractable(), Is.False);
                destination.onClick.Invoke();
                Assert.That(controller.Session.SelectedTreasureMap.Seed, Is.EqualTo("DEPARTURE-TREASURE-0"));
            }
            Select(1);
            Assert.That(controller.Session.SelectedTreasureMap.Seed, Is.EqualTo("DEPARTURE-TREASURE-1"));
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(origin));
            Find<Button>("Departure Dock Ordinary Voyage").onClick.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(Find<Button>("Route 0").gameObject.activeInHierarchy, Is.True);
            Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(2));
        }

        [Test]
        public void TreasureSelectionReplacesOrdinarySeedAndOrdinaryModeCanBeRestored()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 1, true);
            string origin = controller.Session.CurrentPort;
            string destination = CoreVoyageSession.DestinationPorts.First(port => port != origin);
            string seed = CoreVoyageSession.CreateChartSeed(0, origin, destination);
            Find<Toggle>("Use Chart Seed").isOn = true;
            Find<InputField>("Seed Input").text = seed;
            Assert.That(controller.Session.IsChartSeedSelected, Is.True);
            Select(0);
            Assert.That(controller.Session.IsChartSeedSelected, Is.False);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Not.Null);
            Assert.That(host.GetComponentsInChildren<RectTransform>(true)
                .Any(item => item.name == "Departure Chart Selection" && item.gameObject.activeInHierarchy), Is.False);
            Find<Toggle>("Use Chart Seed").isOn = true;
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null, "The chart tab can replace an equipped treasure seed directly.");
            Assert.That(controller.Session.IsChartSeedSelected, Is.True);
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(HasActive("Departure Dock Treasure Selection"), Is.False);
            Select(0);
            Find<Button>("Departure Dock Ordinary Voyage").onClick.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(controller.Session.IsChartSeedSelected, Is.False);
            Assert.That(Find<Toggle>("Use Chart Seed").isOn, Is.False);
            Find<Toggle>("Use Chart Seed").isOn = true;
            Assert.That(controller.Session.IsChartSeedSelected, Is.True);
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegionalDropdownCallbacksCannotSelectSeedsAfterOrdinaryReturnOrViewRebuild()
        {
            Create(CoreGameplayPreviewPort.GrayGull, 2, false);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Dropdown.DropdownEvent closedSelection = Find<Dropdown>("Departure Dock Treasure Dropdown").onValueChanged;
            Find<Button>("Departure Dock Ordinary Voyage").onClick.Invoke();
            closedSelection.Invoke(1);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);

            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Dropdown.DropdownEvent staleSelection = Find<Dropdown>("Departure Dock Treasure Dropdown").onValueChanged;
            Render();
            Select(1);
            staleSelection.Invoke(1);
            Assert.That(controller.Session.SelectedTreasureMap.Seed, Is.EqualTo("DEPARTURE-TREASURE-1"));
        }

        [Test]
        public void RemovedInventoryIsRevalidatedAndEmptyInventoryKeepsAssemblyAccess()
        {
            Create(CoreGameplayPreviewPort.GrayGull, 1, false);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Dropdown.DropdownEvent previousSelection = Find<Dropdown>("Departure Dock Treasure Dropdown").onValueChanged;
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            previousSelection.Invoke(1);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(Find<Text>("Departure Dock Treasure Detail").text, Does.Contain("该种子已不可用"));
            Render();
            if (!HasActive("Departure Dock Treasure Selection"))
                Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Assert.That(Find<Text>("Departure Dock Treasure Empty").text, Is.Empty);
            Assert.That(Find<Dropdown>("Departure Dock Treasure Dropdown").IsInteractable(), Is.False);
            Assert.That(Find<Button>("Departure Treasure Seed Table").IsInteractable(), Is.True);
        }

        [Test]
        public void FullInventoryAndBothModeControlsRemainSeparated()
        {
            Create(CoreGameplayPreviewPort.GrayGull, 6, true);
            RectTransform route = Find<RectTransform>("Route Selection");
            Rect normal = BoundsIn(route, Find<RectTransform>("Use Chart Seed"));
            Rect treasure = BoundsIn(route, Find<RectTransform>("Departure Treasure Seed Picker"));
            Assert.That(normal.Overlaps(treasure), Is.False);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Dropdown dropdown = Find<Dropdown>("Departure Dock Treasure Dropdown");
            Assert.That(dropdown.options.Count, Is.EqualTo(7));
            for (int index = 0; index < 6; index++)
            {
                Assert.That(dropdown.options[index + 1].text,
                    Does.Contain(controller.Session.TreasureMaps[index].Seed), "Every owned seed remains available in the inline menu.");
            }
            Assert.That(HasActive("Departure Treasure Seed Selection"), Is.False);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void TreasureRouteKeepsModePortsIllustrationAndActionsSeparated(CoreGameplayPreviewPort port)
        {
            Create(port, 6, false);
            Select(5);
            Canvas.ForceUpdateCanvases();
            RectTransform route = Find<RectTransform>("Route Selection");
            string[] names = {
                "Departure Dock Ordinary Voyage", "Departure Treasure Seed Picker",
                "Departure Dock Treasure Preview", "Departure Dock Treasure Dropdown", "Departure Dock Treasure Detail",
                "Departure Dock Treasure Consumption", "Departure Dock Treasure Status", "Departure Treasure Seed Table"
            };
            names = names.Concat(Enumerable.Range(0, 6).Select(index => "Departure Destination " + index)).ToArray();
            Rect[] bounds = names.Select(name => BoundsIn(route, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < bounds.Length; index++)
            {
                Assert.That(bounds[index].xMin, Is.GreaterThanOrEqualTo(route.rect.xMin), names[index]);
                Assert.That(bounds[index].xMax, Is.LessThanOrEqualTo(route.rect.xMax), names[index]);
                Assert.That(bounds[index].yMin, Is.GreaterThanOrEqualTo(route.rect.yMin), names[index]);
                Assert.That(bounds[index].yMax, Is.LessThanOrEqualTo(route.rect.yMax), names[index]);
                for (int other = 0; other < index; other++)
                    Assert.That(bounds[index].Overlaps(bounds[other]), Is.False, names[index] + " / " + names[other]);
            }
            foreach (string name in new[] { "Departure Dock Treasure Detail", "Departure Dock Treasure Consumption" })
            {
                Text text = Find<Text>(name);
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f), name);
            }
        }

        [Test]
        public void TutorialRejectsForcedTreasurePickerCallback()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 1, false);
            typeof(CoreGameplayDemoController).GetField("tutorialActive", PrivateInstance).SetValue(controller, true);
            Render();
            Button picker = Find<Button>("Departure Treasure Seed Picker");
            Assert.That(picker.IsInteractable(), Is.False);
            picker.onClick.Invoke();
            Assert.That(host.GetComponentsInChildren<RectTransform>(true)
                .Any(item => item.name == "Departure Treasure Seed Selection" || item.name == "Departure Dock Treasure Selection"), Is.False);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
        }

        [Test]
        public void DockTreasureTabOpensInlineWithoutSelectingOrStartingOrdinaryVoyage()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 2, false);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Assert.That(HasActive("Departure Dock Treasure Selection"), Is.True);
            Assert.That(HasActive("Departure Treasure Seed Selection"), Is.False, "The dock must not open a second modal.");
            Assert.That(Find<Dropdown>("Departure Dock Treasure Dropdown").value, Is.Zero);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null, "Opening the tab never equips the first seed.");
            Assert.That(Find<Button>("Start Voyage").IsInteractable(), Is.False);
            Find<Button>("Start Voyage").onClick.Invoke();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(2));
        }

        [Test]
        public void DockDropdownSwitchesAllSixSeedsAndShowsTheirOwnThumbnailWithoutNavigator()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 6, false);
            Assert.That(controller.Session.CanDrawSeaChart, Is.False);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Assert.That(Find<Dropdown>("Departure Dock Treasure Dropdown").options.Count, Is.EqualTo(7));
            for (int index = 0; index < 6; index++)
            {
                Select(index);
                Assert.That(controller.Session.SelectedTreasureMap.Id, Is.EqualTo("departure-treasure-" + index));
                Assert.That(Find<Dropdown>("Departure Dock Treasure Dropdown").value, Is.EqualTo(index + 1));
                Assert.That(Find<RawImage>("Departure Dock Treasure Preview").texture.name,
                    Is.EqualTo(index % 2 == 0 ? "treasure_chart_weathered" : "treasure_chart_gilded"));
                Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(6), "Browsing and switching never consumes a seed.");
                Assert.That(HasActive("Departure Treasure Seed Selection"), Is.False);
            }
            Find<Dropdown>("Departure Dock Treasure Dropdown").value = 0;
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(Find<Button>("Start Voyage").IsInteractable(), Is.False);
            Assert.That(HasActive("Departure Dock Treasure Selection"), Is.True);
        }

        [Test]
        public void DockPreviewAndSeedDropdownStayInsideTheTableWithPreviewOnTheLeft()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 6, false);
            Select(5);
            Canvas.ForceUpdateCanvases();
            RectTransform selection = Find<RectTransform>("Departure Dock Treasure Selection");
            Rect preview = BoundsIn(selection, Find<RectTransform>("Departure Dock Treasure Preview"));
            Rect dropdown = BoundsIn(selection, Find<RectTransform>("Departure Dock Treasure Dropdown"));
            Rect detail = BoundsIn(selection, Find<RectTransform>("Departure Dock Treasure Detail"));
            Assert.That(selection.rect.width, Is.LessThan(820f));
            Assert.That(selection.rect.height, Is.LessThan(200f));
            Assert.That(preview.xMax, Is.LessThanOrEqualTo(dropdown.xMin));
            Assert.That(dropdown.Overlaps(detail), Is.False);
            foreach (Rect bounds in new[] { preview, dropdown, detail })
            {
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(selection.rect.xMin - .1f));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(selection.rect.xMax + .1f));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(selection.rect.yMin - .1f));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(selection.rect.yMax + .1f));
            }
        }

        [Test]
        public void DockDropdownCallbacksCannotEquipAfterOrdinaryReturnOrViewRebuild()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 2, false);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Dropdown.DropdownEvent closedSelection = Find<Dropdown>("Departure Dock Treasure Dropdown").onValueChanged;
            Find<Button>("Departure Dock Ordinary Voyage").onClick.Invoke();
            closedSelection.Invoke(1);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(controller.Session.IsChartSeedSelected, Is.False);
            Assert.That(HasActive("Departure Dock Treasure Selection"), Is.False);
            Assert.That(Find<Button>("Route 0").gameObject.activeInHierarchy, Is.True);

            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Dropdown.DropdownEvent staleSelection = Find<Dropdown>("Departure Dock Treasure Dropdown").onValueChanged;
            Render();
            Select(1);
            staleSelection.Invoke(1);
            Assert.That(controller.Session.SelectedTreasureMap.Seed, Is.EqualTo("DEPARTURE-TREASURE-1"));
            Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(2));
        }

        [Test]
        public void DockRemovedInventoryIsRevalidatedAndEmptyInventoryKeepsDepartureDisabled()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 1, false);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Dropdown.DropdownEvent previousSelection = Find<Dropdown>("Departure Dock Treasure Dropdown").onValueChanged;
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            previousSelection.Invoke(1);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Render();
            if (!HasActive("Departure Dock Treasure Selection"))
                Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Assert.That(Find<Text>("Departure Dock Treasure Empty").text, Is.Empty);
            Assert.That(Find<Dropdown>("Departure Dock Treasure Dropdown").IsInteractable(), Is.False);
            Assert.That(Find<Button>("Start Voyage").IsInteractable(), Is.False);
            Assert.That(HasActive("Departure Treasure Seed Selection"), Is.False);
        }
        private void Create(CoreGameplayPreviewPort port, int maps, bool navigator)
        {
            host = new GameObject("Treasure Departure UI Test");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, port, 0);
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 1500,
                maps = Enumerable.Range(0, maps).Select(index => new CoreTreasureMapSave
                {
                    id = "departure-treasure-" + index,
                    rankId = index % 2 == 0 ? "weathered" : "gilded",
                    seed = maps == 6 ? new string('W', 63) + index : "DEPARTURE-TREASURE-" + index,
                }).ToArray(),
            });
            controller.Session.ConfigureCrewCapabilities(navigator, false, false, false, false, false);
            Render();
            Canvas.ForceUpdateCanvases();
        }

        private void Select(int index)
        {
            if (!HasActive("Departure Dock Treasure Selection"))
                Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Find<Dropdown>("Departure Dock Treasure Dropdown").value = index + 1;
        }

        private bool HasActive(string name) => host.GetComponentsInChildren<Transform>(true)
            .Any(item => item.name == name && item.gameObject.activeInHierarchy);

        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true)
            .Single(item => item.name == name && item.gameObject.activeInHierarchy);
        private void Render() => typeof(CoreGameplayDemoController).GetMethod("Render", PrivateInstance).Invoke(controller, null);

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3 min = parent.InverseTransformPoint(corners[0]);
            Vector3 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
