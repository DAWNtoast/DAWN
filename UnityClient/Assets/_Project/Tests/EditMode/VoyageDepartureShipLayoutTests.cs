using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class VoyageDepartureShipLayoutTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private int? previousReadingLevel;

        [SetUp]
        public void PrepareReadingLevel()
        {
            previousReadingLevel = GameReadability.OverrideLevel;
            GameReadability.OverrideLevel = 0;
        }

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameReadability.OverrideLevel = previousReadingLevel;
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DepartureArrowsShipHitAreaAndStatsRemainSeparatedWithinSetupPanel(CoreGameplayPreviewPort port)
        {
            Preview(port);
            Canvas.ForceUpdateCanvases();
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            string[] separate = { "Departure Previous Vessel", "Inspect Departure Vessel", "Departure Next Vessel", "Ship Stats" };
            Rect[] bounds = separate.Select(name => BoundsIn(setup, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < bounds.Length; index++)
            {
                AssertInside(setup, bounds[index], separate[index]);
                if (index > 0)
                    Assert.That(bounds[index].xMin - bounds[index - 1].xMax, Is.GreaterThanOrEqualTo(3.9f),
                        separate[index - 1] + " and " + separate[index] + " need distinct clickable/readable areas.");
                for (int other = 0; other < index; other++)
                    Assert.That(bounds[index].Overlaps(bounds[other]), Is.False, separate[index] + " / " + separate[other]);
            }

            Button inspect = Find<Button>("Inspect Departure Vessel");
            Assert.That(inspect.interactable, Is.True);
            Assert.That(inspect.GetComponent<Image>().raycastTarget, Is.True, "The transparent ship hit area must receive pointer clicks.");
            Assert.That(inspect.targetGraphic, Is.Not.Null);
            Assert.That(inspect.targetGraphic.transform.IsChildOf(inspect.transform), Is.True);
            foreach (RawImage art in inspect.GetComponentsInChildren<RawImage>())
            {
                Assert.That(art.raycastTarget, Is.False, "Ship layers must route clicks to their common parent.");
                Rect artBounds = BoundsIn(setup, art.rectTransform);
                AssertInside(setup, artBounds, art.name);
                foreach (int separateIndex in new[] { 0, 2, 3 })
                    Assert.That(artBounds.Overlaps(bounds[separateIndex]), Is.False,
                        art.name + " must not cover " + separate[separateIndex]);
            }
            Assert.That(Find<Text>("Departure Ship Schematic Caption").raycastTarget, Is.False);
            Assert.That(Find<Text>("Departure Vessel Hint").raycastTarget, Is.False);
            Rect caption = BoundsIn(setup, Find<Text>("Departure Ship Schematic Caption").rectTransform);
            Rect hint = BoundsIn(setup, Find<Text>("Departure Vessel Hint").rectTransform);
            Rect seedLabel = BoundsIn(setup, Find<Text>("Seed Label").rectTransform);
            Rect seedInput = BoundsIn(setup, Find<InputField>("Seed Input").GetComponent<RectTransform>());
            Assert.That(caption.Overlaps(hint), Is.False, "The ship name and interaction hint need separate lines.");
            Assert.That(hint.Overlaps(seedLabel), Is.False, "The interaction hint must not overlap the seed heading.");
            Assert.That(seedLabel.Overlaps(seedInput), Is.False, "The seed heading must not overlap the input.");
            foreach (string name in new[] { "Departure Previous Vessel", "Departure Next Vessel" })
            {
                Button arrow = Find<Button>(name);
                Assert.That(arrow.interactable, Is.False, "Editor preview has one deterministic owned vessel.");
                Rect arrowBounds = BoundsIn(setup, arrow.GetComponent<RectTransform>());
                Assert.That(arrowBounds.width, Is.GreaterThanOrEqualTo(35.9f));
                Assert.That(arrowBounds.height, Is.GreaterThanOrEqualTo(49.9f));
            }

            Rect previous = default;
            for (int slot = 0; slot < ComponentArtCatalog.SlotCount; slot++)
            {
                RawImage part = Find<RawImage>("Upgrade Part " + slot);
                Text label = Find<Text>("Upgrade Name " + slot);
                Assert.That(part.texture, Is.Not.Null);
                Assert.That(part.raycastTarget, Is.False);
                Assert.That(label.text, Is.Not.Empty);
                Rect labelBounds = BoundsIn(setup, label.rectTransform);
                AssertInside(setup, BoundsIn(setup, part.rectTransform), part.name);
                AssertInside(setup, labelBounds, label.name);
                if (slot > 0) Assert.That(labelBounds.xMin - previous.xMax, Is.GreaterThanOrEqualTo(3.9f), label.name);
                previous = labelBounds;
            }

            RectTransform hitArea = inspect.GetComponent<RectTransform>();
            Vector3 position = hitArea.position;
            Vector3 scale = hitArea.localScale;
            EventTrigger trigger = inspect.GetComponent<EventTrigger>();
            trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerEnter).callback.Invoke(new BaseEventData(null));
            Assert.That(hitArea.position, Is.EqualTo(position));
            Assert.That(hitArea.localScale, Is.EqualTo(scale));
            trigger.triggers.Single(item => item.eventID == EventTriggerType.PointerExit).callback.Invoke(new BaseEventData(null));
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DeparturePreviewOpensEquipmentAndAllTabsReturnToTheSamePortsDeparture(CoreGameplayPreviewPort port)
        {
            Preview(port);
            string portName = controller.Session.CurrentPort;
            foreach (string tab in new[] { "Equipment", "Performance", "Background" })
            {
                Find<Button>("Inspect Departure Vessel").onClick.Invoke();
                Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.ShipyardDetail));
                Assert.That(controller.HasCompleteEditorPreview, Is.True);
                Assert.That(Find<RectTransform>("Shipyard Equipment Page"), Is.Not.Null);
                Assert.That(Has("Voyage Setup"), Is.False);
                Assert.That(Has("Back To Shipyard Gallery"), Is.False);
                Find<Button>("Shipyard Dossier Tab " + tab).onClick.Invoke();
                controller.RefreshEditorPreview();
                Assert.That(Find<Button>("Back To Voyage Departure"), Is.Not.Null,
                    "Refreshing a dossier tab must retain its departure return target.");
                Find<Button>("Back To Voyage Departure").onClick.Invoke();
                Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.VoyageDeparture));
                Assert.That(controller.HasCompleteEditorPreview, Is.True);
                Assert.That(controller.Session.CurrentPort, Is.EqualTo(portName));
                Assert.That(Find<RectTransform>("Voyage Setup"), Is.Not.Null);
                Assert.That(Has("Shipyard Detail"), Is.False);
            }

            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Shipyard, port, 0);
            AssertOrdinaryShipyardReturn();
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DepartureRouteAndSetupModulesStayInsideFramesWithClearFooter(CoreGameplayPreviewPort port)
        {
            Preview(port);
            Canvas.ForceUpdateCanvases();
            RectTransform route = Find<RectTransform>("Route Selection");
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            Rect routeFrame = BoundsIn(route.parent, route);
            Rect setupFrame = BoundsIn(route.parent, setup);
            Assert.That(routeFrame.yMin, Is.EqualTo(setupFrame.yMin).Within(.1f), "The two columns need an aligned footer.");
            Assert.That(routeFrame.yMax, Is.EqualTo(setupFrame.yMax).Within(.1f), "The two columns need an aligned top edge.");

            Rect[] routeRows = new Rect[CoreVoyageSession.Routes.Count];
            for (int index = 0; index < routeRows.Length; index++)
            {
                routeRows[index] = BoundsIn(route, Find<Button>("Route " + index).GetComponent<RectTransform>());
                AssertInside(route, routeRows[index], "Route " + index);
                Assert.That(Has("Destination Port " + index), Is.False,
                    "Route rows must no longer contain the old cycling destination control.");
                if (index > 0)
                {
                    Assert.That(routeRows[index].yMax, Is.LessThanOrEqualTo(routeRows[index - 1].yMin - 3.9f),
                        "Route rows need a visible gap so their frames do not merge.");
                    Assert.That(routeRows[index - 1].yMin - routeRows[index].yMax,
                        Is.LessThan(routeRows[index].height * .3f), "Route rows must not be separated by large empty bands.");
                }
            }
            Assert.That(routeRows.Last().yMin - route.rect.yMin, Is.InRange(12f, 48f),
                "The final route should use the lower frame while retaining clear padding.");

            Rect previousDestination = default;
            Rect[] fixedPortBounds = AssertFixedDeparturePortRow();
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                string name = "Departure Destination " + index;
                Rect destination = BoundsIn(route, Find<Button>(name).GetComponent<RectTransform>());
                AssertInside(route, destination, name);
                Assert.That(destination.yMin, Is.GreaterThanOrEqualTo(routeRows[0].yMax + 3.9f),
                    "The destination buttons belong above the route list.");
                if (index > 0)
                {
                    Assert.That(destination.yMin, Is.EqualTo(previousDestination.yMin).Within(.1f),
                        "All six ports must share one row.");
                    Assert.That(destination.xMin, Is.GreaterThanOrEqualTo(previousDestination.xMax + 3.9f),
                        "Each port needs its own separate clickable area.");
                }
                previousDestination = destination;
            }

            string[] setupOnly = {
                "Setup Title", "Replay Tutorial", "Departure Previous Vessel", "Inspect Departure Vessel",
                "Departure Next Vessel", "Ship Stats", "Departure Ship Schematic Caption", "Departure Vessel Hint",
                "Destination Summary", "Cargo", "Departure Provision Quote", "Departure Provision Water Icon", "Departure Provision Food Icon",
                "Departure Buy Provisions", "Adjust Voyage Supplies",
                "Configure Voyage Battle Decks", "Configure Voyage Crew", "Start Voyage"
            };
            setupOnly = setupOnly.Concat(Enumerable.Range(0, ComponentArtCatalog.SlotCount)
                .SelectMany(slot => new[] { "Upgrade Part " + slot, "Upgrade Name " + slot })).ToArray();
            Rect[] setupBounds = setupOnly.Select(name => BoundsIn(setup, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < setupBounds.Length; index++)
            {
                RectTransform element = Find<RectTransform>(setupOnly[index]);
                ScrollRect details = Find<ScrollRect>("Departure Details Viewport");
                bool inDetails = element.IsChildOf(details.content);
                AssertInside(inDetails ? details.content : setup,
                    BoundsIn(inDetails ? details.content : setup, element), setupOnly[index]);
                for (int other = 0; other < index; other++)
                    if (Find<RectTransform>(setupOnly[other]).IsChildOf(details.content) == inDetails)
                        Assert.That(setupBounds[index].Overlaps(setupBounds[other]), Is.False,
                            setupOnly[index] + " / " + setupOnly[other]);
                if (!inDetails)
                    Assert.That(BoundsIn(setup, details.viewport).Overlaps(setupBounds[index]), Is.False,
                        "The clipped scrolling detail region must not cover " + setupOnly[index]);
            }
            Rect destinationSummary = BoundsIn(setup, Find<RectTransform>("Destination Summary"));
            Rect cargo = BoundsIn(setup, Find<RectTransform>("Cargo"));
            Rect quote = BoundsIn(setup, Find<RectTransform>("Departure Provision Quote"));
            Rect start = BoundsIn(setup, Find<RectTransform>("Start Voyage"));
            Assert.That(destinationSummary.xMin, Is.EqualTo(cargo.xMin).Within(.1f));
            Assert.That(quote.xMin, Is.EqualTo(cargo.xMin + 38f).Within(.1f), "Quote reserves a 32-unit icon column and a 6-unit gap.");
            Assert.That(quote.xMax, Is.EqualTo(cargo.xMax).Within(.1f));
            foreach (string name in new[] { "Departure Provision Water Icon", "Departure Provision Food Icon" })
            {
                Rect icon = BoundsIn(setup, Find<RectTransform>(name));
                Assert.That(icon.center.x, Is.EqualTo(cargo.xMin + 16f).Within(.1f), name + " remains centered in the aligned icon column.");
                Assert.That(icon.xMin, Is.GreaterThanOrEqualTo(cargo.xMin - .1f), name);
                Assert.That(quote.xMin - icon.xMax, Is.GreaterThanOrEqualTo(5.9f), name + " must leave the quote text unobscured.");
                Assert.That(icon.width, Is.LessThanOrEqualTo(32.1f), name);
                Assert.That(icon.height, Is.LessThanOrEqualTo(32.1f), name);
            }
            Assert.That(start.xMin, Is.LessThanOrEqualTo(cargo.xMin));
            Assert.That(start.xMax, Is.GreaterThanOrEqualTo(cargo.xMax));
            foreach (string[] row in new[] {
                new[] { "Departure Buy Provisions", "Adjust Voyage Supplies" },
                new[] { "Configure Voyage Battle Decks", "Configure Voyage Crew" }
            })
            {
                Rect left = BoundsIn(setup, Find<RectTransform>(row[0]));
                Rect right = BoundsIn(setup, Find<RectTransform>(row[1]));
                Assert.That(left.yMin, Is.EqualTo(right.yMin).Within(.1f), string.Join(" / ", row));
                Assert.That(left.yMax, Is.EqualTo(right.yMax).Within(.1f), string.Join(" / ", row));
                Assert.That(left.xMin, Is.EqualTo(start.xMin).Within(.1f), row[0]);
                Assert.That(right.xMax, Is.EqualTo(start.xMax).Within(.1f), row[1]);
                Assert.That(right.xMin - left.xMax, Is.GreaterThanOrEqualTo(7.9f));
            }
            Assert.That(start.yMin - setup.rect.yMin, Is.GreaterThanOrEqualTo(7.9f),
                "The primary departure action must not touch the frame.");
            AssertReadableHeight(Find<Text>("Cargo"));
            AssertReadableHeight(Find<Text>("Departure Provision Quote"));

            Assert.That(Has("Route Heading"), Is.False, "The edited departure scene removes the redundant route heading.");
            Assert.That(Has("Upgrade Heading"), Is.False, "The edited departure scene removes the redundant upgrade heading.");
            Toggle chartMode = Find<Toggle>("Use Chart Seed");
            Assert.That(chartMode.transform.parent, Is.SameAs(route));
            Rect modeBounds = BoundsIn(route, chartMode.GetComponent<RectTransform>());
            AssertInside(route, modeBounds, "Chart mode");
            Assert.That(modeBounds.yMin, Is.GreaterThanOrEqualTo(fixedPortBounds.Max(bounds => bounds.yMax) + 3.9f),
                "The chart switch has its own row inside the taller left frame, above all six ports.");
            Assert.That(modeBounds.xMin, Is.GreaterThanOrEqualTo(route.rect.xMin));
            Assert.That(modeBounds.xMax, Is.LessThanOrEqualTo(route.rect.xMax));
            Assert.That(setup.GetComponentsInChildren<InputField>(true), Is.Empty,
                "Seed selection belongs entirely in the left frame.");
            Assert.That(setup.GetComponentsInChildren<Toggle>(true).Any(toggle => toggle.name == "Use Chart Seed"), Is.False);
            Rect seed = BoundsIn(route, Find<InputField>("Seed Input").GetComponent<RectTransform>());
            AssertInside(route, seed, "Seed Input");
            Assert.That(seed.Overlaps(BoundsIn(route, Find<Text>("Seed Label").rectTransform)), Is.False);
            Dropdown seedDropdown = Find<Dropdown>("Seed Input Dropdown");
            Assert.That(seedDropdown.options.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(seedDropdown.template, Is.Not.Null);
            Rect menu = BoundsIn(route, seedDropdown.GetComponent<RectTransform>());
            Assert.That(menu.width, Is.LessThan(seed.width * .4f), "The saved-chart menu must leave the seed text area usable.");
            Assert.That(Find<InputField>("Seed Input").enabled, Is.True);
            Assert.That(chartMode.isOn, Is.False);

            controller.ConfigureEditorPreviewDepartureChart(1);
            Canvas.ForceUpdateCanvases();
            route = Find<RectTransform>("Route Selection");
            string[] chartModules = { "Seed Input", "Departure Chart Route", "Departure Chart Preview",
                "Departure Chart Ports", "Chart Seed Validation" };
            Rect[] chartBounds = chartModules.Select(name => BoundsIn(route, Find<RectTransform>(name))).ToArray();
            Rect[] chartPortBounds = AssertFixedDeparturePortRow(fixedPortBounds);
            for (int index = 0; index < chartBounds.Length; index++)
            {
                Assert.That(Find<RectTransform>(chartModules[index]).gameObject.activeInHierarchy, Is.True, chartModules[index]);
                AssertInside(route, chartBounds[index], chartModules[index]);
                for (int other = 0; other < index; other++)
                    Assert.That(chartBounds[index].Overlaps(chartBounds[other]), Is.False,
                        chartModules[index] + " / " + chartModules[other]);
                for (int portIndex = 0; portIndex < chartPortBounds.Length; portIndex++)
                    Assert.That(chartBounds[index].Overlaps(chartPortBounds[portIndex]), Is.False,
                        chartModules[index] + " / fixed port " + CoreVoyageSession.DestinationPorts[portIndex]);
            }
            Assert.That(chartPortBounds.Min(bounds => bounds.yMin) - chartBounds[0].yMax,
                Is.GreaterThanOrEqualTo(3.9f), "Keep a visible gap between the fixed port row and chart input.");

            // An empty hold exercises the longer purchase and insufficient-funds
            // copy rather than only the short 'supplies ready' preview message.
            controller.Session.Cargo.Water = 0;
            controller.Session.Cargo.Food = 0;
            controller.Session.RestoreProfile(0, controller.Session.CurrentPort);
            controller.Session.SelectRoute(CoreVoyageSession.Routes.Count - 1);
            controller.ConfigureEditorPreviewDepartureChart(0);
            Canvas.ForceUpdateCanvases();
            Text purchaseQuote = Find<Text>("Departure Provision Quote");
            Assert.That(purchaseQuote.text, Does.Contain("需购淡水"));
            AssertReadableHeight(purchaseQuote);
        }

        [Test]
        public void DepartureCrewConfigurationKeepsTitlesRosterAndFooterSeparated()
        {
            Preview(CoreGameplayPreviewPort.GrayGull);
            Find<Button>("Configure Voyage Crew").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            string[] modules = {
                "Setup Title", "Replay Tutorial", "Departure Crew Configuration Title", "Departure Ordinary Crew Label",
                "Departure Ordinary Crew Slider", "Departure Ordinary Crew Hint", "Departure Special Crew Heading",
                "Departure Crew Total", "Back To Voyage Setup", "Confirm Departure Crew"
            };
            modules = modules.Concat(Enumerable.Range(0, 6).Select(index => "Departure Special Crew Toggle " + index)).ToArray();
            Rect[] bounds = modules.Select(name => BoundsIn(setup, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < bounds.Length; index++)
            {
                ScrollRect details = Find<ScrollRect>("Departure Details Viewport");
                RectTransform element = Find<RectTransform>(modules[index]);
                bool inDetails = element.IsChildOf(details.content);
                AssertInside(inDetails ? details.content : setup,
                    BoundsIn(inDetails ? details.content : setup, element), modules[index]);
                for (int other = 0; other < index; other++)
                    if (Find<RectTransform>(modules[other]).IsChildOf(details.content) == inDetails)
                        Assert.That(bounds[index].Overlaps(bounds[other]), Is.False, modules[index] + " / " + modules[other]);
                if (!inDetails)
                    Assert.That(BoundsIn(setup, details.viewport).Overlaps(bounds[index]), Is.False,
                        "Crew scrolling must not cover " + modules[index]);
            }
            for (int index = 0; index < 6; index++)
            {
                RectTransform button = Find<RectTransform>("Departure Special Crew Toggle " + index);
                string[] childNames = { "Departure Special Crew Portrait " + index,
                    "Departure Special Crew Name " + index, "Departure Special Crew State " + index };
                Rect[] children = childNames.Select(name => BoundsIn(button, Find<RectTransform>(name))).ToArray();
                for (int child = 0; child < children.Length; child++)
                {
                    AssertInside(button, children[child], childNames[child]);
                    for (int previous = 0; previous < child; previous++)
                        Assert.That(children[child].Overlaps(children[previous]), Is.False,
                            childNames[child] + " / " + childNames[previous]);
                }
            }
            Rect back = BoundsIn(setup, Find<RectTransform>("Back To Voyage Setup"));
            Rect confirm = BoundsIn(setup, Find<RectTransform>("Confirm Departure Crew"));
            Assert.That(back.yMin, Is.EqualTo(confirm.yMin).Within(.1f));
            Assert.That(back.yMax, Is.EqualTo(confirm.yMax).Within(.1f));
            Assert.That(confirm.xMin - back.xMax, Is.GreaterThanOrEqualTo(7.9f));
            Assert.That(back.yMin - setup.rect.yMin, Is.GreaterThanOrEqualTo(7.9f));
            foreach (string name in new[] { "Departure Ordinary Crew Hint", "Departure Crew Total" })
                AssertReadableHeight(Find<Text>(name), 12);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DepartureOffersSixNamedPortsDisablesOriginAndDirectlySelectsEveryOtherPort(CoreGameplayPreviewPort port)
        {
            Preview(port);
            string origin = controller.Session.CurrentPort;
            int originIndex = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count)
                .Single(index => CoreVoyageSession.DestinationPorts[index] == origin);
            Assert.That(host.GetComponentsInChildren<Button>(true)
                .Count(button => button.name.StartsWith("Departure Destination ")), Is.EqualTo(6));
            Rect[] fixedPortBounds = AssertFixedDeparturePortRow();

            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                string destination = CoreVoyageSession.DestinationPorts[index];
                Button picker = Find<Button>("Departure Destination " + index);
                Assert.That(picker.GetComponentInChildren<Text>().text, Is.EqualTo(destination));
                Assert.That(picker.IsInteractable(), Is.EqualTo(index != originIndex), destination);
                if (index == originIndex)
                {
                    Assert.That(picker.colors.disabledColor.grayscale, Is.LessThan(picker.colors.normalColor.grayscale),
                        "The departure port must appear darker than selectable ports.");
                    string unchanged = controller.Session.SelectedDestinationPort;
                    picker.onClick.Invoke();
                    Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(unchanged),
                        "Even a direct callback must not select the departure port.");
                    continue;
                }

                picker.onClick.Invoke();
                Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(destination));
                AssertFixedDeparturePortRow(fixedPortBounds);
                Assert.That(controller.Session.CurrentPort, Is.EqualTo(origin),
                    "Choosing an arrival port must not move the player before departure.");
                Assert.That(Find<Text>("Destination Summary").text, Does.Contain("到达港口：" + destination));
                Assert.That(Find<Button>("Departure Destination " + originIndex).IsInteractable(), Is.False);
                int alternative = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count)
                    .First(other => other != index && other != originIndex);
                Assert.That(Find<Button>("Departure Destination " + index).colors.normalColor,
                    Is.Not.EqualTo(Find<Button>("Departure Destination " + alternative).colors.normalColor),
                    "The confirmed destination must remain visibly selected.");
            }
        }

        [Test]
        public void DepartureChartLocksAllPortButtonsAndRandomModeRestoresOnlyOtherPorts()
        {
            Preview(CoreGameplayPreviewPort.GrayGull);
            string origin = controller.Session.CurrentPort;
            int targetIndex = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count)
                .First(index => CoreVoyageSession.DestinationPorts[index] != origin);
            string target = CoreVoyageSession.DestinationPorts[targetIndex];
            string seed = CoreVoyageSession.CreateChartSeed(0, origin, target);
            Rect[] fixedPortBounds = AssertFixedDeparturePortRow();
            Find<Toggle>("Use Chart Seed").isOn = true;
            AssertFixedDeparturePortRow(fixedPortBounds);
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
                Assert.That(Find<Button>("Departure Destination " + index).IsInteractable(), Is.False,
                    "Chart mode locks all six endpoints even while its input is empty.");
            Find<InputField>("Seed Input").text = seed;
            Find<InputField>("Seed Input").onEndEdit.Invoke(seed);
            Assert.That(controller.Session.IsChartSeedSelected, Is.True);
            AssertFixedDeparturePortRow(fixedPortBounds);

            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                Button picker = Find<Button>("Departure Destination " + index);
                Assert.That(picker.IsInteractable(), Is.False, "A loaded chart fixes both endpoints.");
                picker.onClick.Invoke();
                Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(target));
                Assert.That(controller.Session.IsChartSeedSelected, Is.True);
            }
            foreach (string module in new[] { "Seed Input", "Departure Chart Route", "Departure Chart Preview",
                "Departure Chart Ports", "Chart Seed Validation" })
            {
                Rect bounds = BoundsIn(Find<RectTransform>("Route Selection"), Find<RectTransform>(module));
                foreach (Rect portBounds in fixedPortBounds)
                    Assert.That(bounds.Overlaps(portBounds), Is.False, module + " / fixed port row");
            }
            Find<Toggle>("Use Chart Seed").isOn = false;
            AssertFixedDeparturePortRow(fixedPortBounds);
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
                Assert.That(Find<Button>("Departure Destination " + index).IsInteractable(),
                    Is.EqualTo(CoreVoyageSession.DestinationPorts[index] != origin));
        }

        [Test]
        public void DepartureSeedInputUsesExplicitModeAndSavedChartMenuWithoutInventedPresets()
        {
            Preview(CoreGameplayPreviewPort.BrokenAnchor);
            Dropdown dropdown = Find<Dropdown>("Seed Input Dropdown");
            InputField input = Find<InputField>("Seed Input");
            Toggle mode = Find<Toggle>("Use Chart Seed");
            Assert.That(mode.isOn, Is.False);
            Assert.That(input.enabled, Is.True);
            Assert.That(input.IsInteractable(), Is.False);
            Assert.That(dropdown.options.Select(option => option.text), Does.Not.Contain("SHATTERED-SAIL"));
            Assert.That(dropdown.options.Select(option => option.text), Does.Not.Contain("GOLDEN-TIDE"));
            Assert.That(dropdown.options[0].text, Does.Contain("海图"));
            Assert.That(Find<Text>("Chart Seed Validation"), Is.Not.Null);
            RectTransform seed = input.GetComponent<RectTransform>();
            Rect menu = BoundsIn(seed, dropdown.GetComponent<RectTransform>());
            Assert.That(menu.xMin, Is.GreaterThan(0f), "The menu sits at the right edge, leaving an actual keyboard input on the left.");
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitShipyardPreviewConfigurationClearsAPreviousDepartureReturnTarget(bool configureDossier)
        {
            Preview(CoreGameplayPreviewPort.GrayGull);
            Find<Button>("Inspect Departure Vessel").onClick.Invoke();
            Assert.That(Find<Button>("Back To Voyage Departure"), Is.Not.Null);
            if (configureDossier)
                controller.ConfigureEditorShipyard(0, true, 2);
            else
            {
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Shipyard, CoreGameplayPreviewPort.GrayGull, 0);
                Find<Button>("Shipyard Ship 0").onClick.Invoke();
            }
            Assert.That(Has("Back To Voyage Departure"), Is.False, "A newly configured shipyard preview is not a continuation of departure.");
            Find<Button>("Back To Shipyard Gallery").onClick.Invoke();
            Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.Shipyard));
            Assert.That(Find<Button>("Shipyard Ship 0"), Is.Not.Null);
            Assert.That(Has("Voyage Setup"), Is.False);
        }

        private void Preview(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Voyage Departure Ship Layout Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, port, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
        }

        private Rect[] AssertFixedDeparturePortRow(Rect[] expected = null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform route = Find<RectTransform>("Route Selection");
            Rect[] bounds = new Rect[CoreVoyageSession.DestinationPorts.Count];
            for (int index = 0; index < bounds.Length; index++)
            {
                string port = CoreVoyageSession.DestinationPorts[index];
                Button button = Find<Button>("Departure Destination " + index);
                Text label = button.GetComponentInChildren<Text>();
                Assert.That(button.gameObject.activeInHierarchy, Is.True, port + " / fixed visible plaque");
                CanvasGroup group = button.GetComponent<CanvasGroup>();
                Assert.That(group, Is.Not.Null, port);
                Assert.That(group.alpha, Is.EqualTo(1f), port + " / plaque must not fade away");
                Assert.That(button.colors.disabledColor.a, Is.EqualTo(1f), port + " / solid disabled plaque");
                Assert.That(label.text, Is.EqualTo(port), port);
                Assert.That(label.color.a, Is.EqualTo(1f), port + " / opaque name");
                AssertReadableHeight(label, 18);
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f), port + " / complete name width");
                if (port == controller.Session.CurrentPort)
                {
                    Assert.That(button.IsInteractable(), Is.False, "The current port remains unavailable as a destination.");
                    Assert.That(label.color.grayscale, Is.GreaterThan(button.colors.disabledColor.grayscale + .15f),
                        "The darker current-port name must remain legible above its darker plaque.");
                    TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                    using (var visible = new TextGenerator())
                    using (var complete = new TextGenerator())
                    {
                        visible.Populate(label.text, settings);
                        settings.verticalOverflow = VerticalWrapMode.Overflow;
                        complete.Populate(label.text, settings);
                        Assert.That(visible.vertexCount, Is.GreaterThan(4), port + " / visible current-port glyphs");
                        Assert.That(visible.characterCount, Is.EqualTo(complete.characterCount), port + " / complete current-port name");
                        Assert.That(visible.vertexCount, Is.EqualTo(complete.vertexCount), port + " / complete current-port glyphs");
                    }
                }
                bounds[index] = BoundsIn(route, button.GetComponent<RectTransform>());
                AssertInside(route, bounds[index], port);
                if (expected != null)
                {
                    Assert.That(bounds[index].xMin, Is.EqualTo(expected[index].xMin).Within(.1f), port + " / fixed left");
                    Assert.That(bounds[index].xMax, Is.EqualTo(expected[index].xMax).Within(.1f), port + " / fixed right");
                    Assert.That(bounds[index].yMin, Is.EqualTo(expected[index].yMin).Within(.1f), port + " / fixed bottom");
                    Assert.That(bounds[index].yMax, Is.EqualTo(expected[index].yMax).Within(.1f), port + " / fixed top");
                }
            }
            return bounds;
        }

        private void AssertOrdinaryShipyardReturn()
        {
            Find<Button>("Shipyard Ship 0").onClick.Invoke();
            Assert.That(Has("Back To Voyage Departure"), Is.False);
            Find<Button>("Shipyard Dossier Tab Equipment").onClick.Invoke();
            Find<Button>("Back To Shipyard Gallery").onClick.Invoke();
            Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.Shipyard));
            Assert.That(Find<Button>("Shipyard Ship 0"), Is.Not.Null);
            Assert.That(Has("Voyage Setup"), Is.False);
        }

        private bool Has(string name) => host.GetComponentsInChildren<Transform>(true).Any(item => item.name == name);

        private T Find<T>(string name) where T : Component
        {
            T result = host.GetComponentsInChildren<T>(true).SingleOrDefault(item => item.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static void AssertInside(RectTransform panel, Rect bounds, string name)
        {
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(panel.rect.xMin - 0.1f), name + " / left");
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(panel.rect.xMax + 0.1f), name + " / right");
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(panel.rect.yMin - 0.1f), name + " / bottom");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(panel.rect.yMax + 0.1f), name + " / top");
        }

        private static void AssertReadableHeight(Text label, int minimumReadableSize = 14)
        {
            TextGenerationSettings settings = label.GetGenerationSettings(new Vector2(label.rectTransform.rect.width, 0f));
            settings.resizeTextForBestFit = false;
            settings.fontSize = label.resizeTextForBestFit ? label.resizeTextMinSize : label.fontSize;
            Assert.That(settings.fontSize, Is.GreaterThanOrEqualTo(minimumReadableSize), label.name + " must remain readable.");
            using (var generator = new TextGenerator())
                Assert.That(generator.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit,
                    Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f), label.name + " must show all lines.");
        }

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            foreach (Vector3 corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
