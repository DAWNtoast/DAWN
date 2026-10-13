using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.Events;
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
            Assert.That(Find<Text>("Treasure Departure Detail").text, Does.Contain("任意港口"));
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
            Find<Button>("Cancel Treasure Voyage").onClick.Invoke();
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
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Find<Button>("Departure Clear Treasure Seed").onClick.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(Find<Toggle>("Use Chart Seed").isOn, Is.False);
            Find<Toggle>("Use Chart Seed").isOn = true;
            Assert.That(controller.Session.IsChartSeedSelected, Is.True);
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seed));
            Assert.That(controller.Session.TreasureMaps.Count, Is.EqualTo(1));
        }

        [Test]
        public void OldPickerCallbacksCannotSelectSeedsAfterCloseOrViewRebuild()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 2, false);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            UnityEvent closedSelection = Find<Button>("Departure Treasure Seed Option 0").onClick;
            Find<Button>("Close Departure Treasure Seed Selection").onClick.Invoke();
            closedSelection.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);

            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            UnityEvent staleSelection = Find<Button>("Departure Treasure Seed Option 0").onClick;
            Render();
            Select(1);
            staleSelection.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap.Seed, Is.EqualTo("DEPARTURE-TREASURE-1"));
        }

        [Test]
        public void RemovedInventoryIsRevalidatedAndEmptyInventoryExplainsAssembly()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 1, false);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Button previousOption = Find<Button>("Departure Treasure Seed Option 0");
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            previousOption.onClick.Invoke();
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
            Assert.That(Find<Text>("Departure Treasure Seed Notice").text, Does.Not.Contain("无需航海士"));
            Render();
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Assert.That(Find<Text>("Departure Treasure Seed Empty").text, Does.Contain("鉴定碎片"));
            Assert.That(Find<Button>("Departure Treasure Seed Table").IsInteractable(), Is.True);
        }

        [Test]
        public void FullInventoryAndBothModeControlsRemainSeparated()
        {
            Create(CoreGameplayPreviewPort.BrokenAnchor, 6, true);
            RectTransform route = Find<RectTransform>("Route Selection");
            Rect normal = BoundsIn(route, Find<RectTransform>("Use Chart Seed"));
            Rect treasure = BoundsIn(route, Find<RectTransform>("Departure Treasure Seed Picker"));
            Assert.That(normal.Overlaps(treasure), Is.False);
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            RectTransform card = Find<RectTransform>("Departure Treasure Seed Card");
            Rect previous = BoundsIn(card, Find<Text>("Departure Treasure Seed Notice").rectTransform);
            for (int index = 0; index < 6; index++)
            {
                Button option = Find<Button>("Departure Treasure Seed Option " + index);
                Rect bounds = BoundsIn(card, option.GetComponent<RectTransform>());
                Assert.That(bounds.Overlaps(previous), Is.False);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(card.rect.xMin));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(card.rect.xMax));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(card.rect.yMin));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(card.rect.yMax));
                Text caption = option.GetComponentInChildren<Text>();
                Assert.That(caption.preferredHeight, Is.LessThanOrEqualTo(caption.rectTransform.rect.height + 1f));
                previous = bounds;
            }
            Assert.That(previous.Overlaps(BoundsIn(card, Find<RectTransform>("Departure Clear Treasure Seed"))), Is.False);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void TreasureRouteKeepsModePortsIllustrationAndActionsSeparated(CoreGameplayPreviewPort port)
        {
            Create(port, 6, false);
            Select(5);
            Canvas.ForceUpdateCanvases();
            RectTransform route = Find<RectTransform>("Route Selection");
            string[] names = {
                "Seed Label", "Departure Treasure Seed Picker", "Departure Route Summary",
                "Treasure Departure Title", "Treasure Departure Art", "Treasure Departure Detail",
                "Treasure Departure Consumption", "Cancel Treasure Voyage", "Return To Treasure Table"
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
            foreach (string name in new[] { "Treasure Departure Detail", "Treasure Departure Consumption" })
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
                .Any(item => item.name == "Departure Treasure Seed Selection"), Is.False);
            Assert.That(controller.Session.SelectedTreasureMap, Is.Null);
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
            Find<Button>("Departure Treasure Seed Picker").onClick.Invoke();
            Find<Button>("Departure Treasure Seed Option " + index).onClick.Invoke();
        }

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
