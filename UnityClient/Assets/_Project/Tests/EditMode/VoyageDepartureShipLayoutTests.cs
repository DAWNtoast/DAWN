using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class VoyageDepartureShipLayoutTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;

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
        public void DepartureEnlargedShipAndArrowsRemainSeparatedWithoutRemovedInformation(CoreGameplayPreviewPort port)
        {
            Preview(port);
            Canvas.ForceUpdateCanvases();
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            bool painted = port == CoreGameplayPreviewPort.BrokenAnchor;
            string[] separate = { "Departure Previous Vessel", "Inspect Departure Vessel", "Departure Next Vessel" };
            Rect[] bounds = separate.Select(name => BoundsIn(setup, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < bounds.Length; index++)
            {
                AssertInside(setup, bounds[index], separate[index]);
                if (!painted && index > 0)
                    Assert.That(bounds[index].xMin - bounds[index - 1].xMax, Is.GreaterThanOrEqualTo(3.9f),
                        separate[index - 1] + " and " + separate[index] + " need distinct clickable/readable areas.");
                for (int other = 0; !painted && other < index; other++)
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
                foreach (int separateIndex in new[] { 0, 2 })
                    Assert.That(artBounds.Overlaps(bounds[separateIndex]), Is.False,
                        art.name + " must not cover " + separate[separateIndex]);
            }
            AssertRemovedDepartureInformation();
            Assert.That(bounds[1].width, Is.GreaterThanOrEqualTo(367.9f), "The ship preview uses the enlarged central area.");
            Assert.That(bounds[1].height, Is.GreaterThanOrEqualTo(207.9f));
            float expectedCenter = painted ? DepartureDockSceneLayout.ShipHitPosition.x : 0f;
            Assert.That(bounds[1].center.x, Is.EqualTo(expectedCenter).Within(.1f));
            if (painted)
            {
                RectTransform target = inspect.GetComponent<RectTransform>();
                Assert.That(target.sizeDelta, Is.EqualTo(DepartureDockSceneLayout.ShipHitSize));
                Assert.That(target.anchoredPosition, Is.EqualTo(DepartureDockSceneLayout.ShipHitPosition));
                AssertPaintedArtRect(setup, Find<RectTransform>("Departure Previous Vessel"), new Rect(672, 322, 66, 66));
                AssertPaintedArtRect(setup, Find<RectTransform>("Departure Next Vessel"), new Rect(1598, 322, 68, 68));
                foreach (string arrowName in new[] { "Departure Previous Vessel", "Departure Next Vessel" })
                {
                    Button arrow = Find<Button>(arrowName);
                    AssertPaintedControl(arrow);
                    Assert.That(arrow.transform.GetSiblingIndex(), Is.GreaterThan(inspect.transform.GetSiblingIndex()),
                        "The painted arrows must receive input above the berth's transparent rectangular target.");
                }
            }
            RectTransform shipPreview = Find<RectTransform>("Starter Modular Ship");
            Assert.That(BoundsIn(setup, shipPreview).width, Is.GreaterThanOrEqualTo(367.9f),
                "The artwork coordinate frame must grow together with its click area.");
            RawImage hull = shipPreview.GetComponentsInChildren<RawImage>().Single(art => art.name.EndsWith(" Base") || art.name.EndsWith(" Hull"));
            Assert.That(BoundsIn(setup, hull.rectTransform).width, Is.GreaterThan(240f),
                "Enlarging only the transparent hit area must not leave the old thumbnail unchanged.");
            foreach (string name in new[] { "Departure Previous Vessel", "Departure Next Vessel" })
            {
                Button arrow = Find<Button>(name);
                Assert.That(arrow.interactable, Is.False, "Editor preview has one deterministic owned vessel.");
                Rect arrowBounds = BoundsIn(setup, arrow.GetComponent<RectTransform>());
                Assert.That(arrowBounds.width, Is.GreaterThanOrEqualTo(35.9f));
                Assert.That(arrowBounds.height, Is.GreaterThanOrEqualTo(49.9f));
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
            bool scene = port == CoreGameplayPreviewPort.BrokenAnchor;
            Rect summaryBounds = BoundsIn(route, Find<Text>("Departure Route Summary").rectTransform);
            if (scene)
            {
                AssertDockRouteAndSetupGeometry(route, setup);
            }
            else
            {
                Rect routeFrame = BoundsIn(route.parent, route);
                Rect setupFrame = BoundsIn(route.parent, setup);
                Assert.That(routeFrame.yMin, Is.EqualTo(setupFrame.yMin).Within(.1f), "The two columns need an aligned footer.");
                Assert.That(routeFrame.yMax, Is.EqualTo(setupFrame.yMax).Within(.1f), "The two columns need an aligned top edge.");

                Rect[] routeRows = new Rect[CoreVoyageSession.Routes.Count];
                for (int index = 0; index < routeRows.Length; index++)
                {
                    routeRows[index] = BoundsIn(route, Find<Button>("Route " + index).GetComponent<RectTransform>());
                    AssertInside(route, routeRows[index], "Route " + index);
                    RectTransform row = Find<Button>("Route " + index).GetComponent<RectTransform>();
                    RawImage illustration = Find<RawImage>("Route Preview " + index);
                    Text description = row.Find("Label").GetComponent<Text>();
                    Rect artBounds = BoundsIn(row, illustration.rectTransform);
                    Rect textBounds = BoundsIn(row, description.rectTransform);
                    AssertInside(row, artBounds, illustration.name);
                    AssertInside(row, textBounds, "Route " + index + " / description");
                    Assert.That(artBounds.Overlaps(textBounds), Is.False,
                        "The enlarged chart illustration must not obscure its route description.");
                    Assert.That(illustration.raycastTarget, Is.False,
                        "Clicking the chart illustration must select its route.");
                    AssertReadableHeight(description, 19);
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
                Text routeSummary = Find<Text>("Departure Route Summary");
                summaryBounds = BoundsIn(route, routeSummary.rectTransform);
                AssertInside(route, summaryBounds, routeSummary.name);
                Assert.That(summaryBounds.yMin, Is.GreaterThanOrEqualTo(fixedPortBounds.Max(bounds => bounds.yMax) + 3.9f),
                    "The current route must remain readable above the six destination ports.");
                Assert.That(routeSummary.raycastTarget, Is.False, "Route guidance must not intercept port selection.");
                AssertReadableHeight(routeSummary);
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
                    "Setup Title", "Departure Previous Vessel", "Inspect Departure Vessel",
                    "Departure Next Vessel",
                    "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewCount",
                    "DepartureOrdinaryCrewIncreaseButton", "DepartureCrewSpeedPreview",
                    "Departure Buy Provisions", "Adjust Voyage Supplies",
                    "Configure Voyage Battle Decks", "Configure Voyage Crew", "Start Voyage"
                };
                Rect[] setupBounds = setupOnly.Select(name => BoundsIn(setup, Find<RectTransform>(name))).ToArray();
                for (int index = 0; index < setupBounds.Length; index++)
                {
                    AssertInside(setup, setupBounds[index], setupOnly[index]);
                    for (int other = 0; other < index; other++)
                        Assert.That(setupBounds[index].Overlaps(setupBounds[other]), Is.False,
                            setupOnly[index] + " / " + setupOnly[other]);
                }
                Rect start = BoundsIn(setup, Find<RectTransform>("Start Voyage"));
                AssertRemovedDepartureInformation();
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
            }
            AssertRemovedDepartureInformation();
            if (scene)
            {
                Assert.That(Find<Text>("DepartureOrdinaryCrewCount").enabled, Is.False);
                Assert.That(Find<Text>("DepartureCrewSpeedPreview").enabled, Is.False);
                foreach (string name in new[] { "Departure Dock Ordinary Readout", "Departure Dock Special Readout", "Departure Dock Speed Readout" })
                {
                    Text label = Find<Text>(name);
                    Assert.That(label.enabled, Is.True);
                    Assert.That(label.raycastTarget, Is.False);
                    AssertReadableHeight(label, 14);
                }
                Assert.That(Find<Text>("Departure Dock Ordinary Readout").text, Does.Contain("普通船员"));
                Assert.That(Find<Text>("Departure Dock Special Readout").text, Does.Contain("普通上限"));
                Assert.That(Find<Text>("Departure Dock Speed Readout").text, Does.Contain("航速"));
            }
            else
            {
                AssertReadableHeight(Find<Text>("DepartureOrdinaryCrewCount"));
                AssertReadableHeight(Find<Text>("DepartureCrewSpeedPreview"));
            }
            Assert.That(Find<Text>("DepartureOrdinaryCrewCount").raycastTarget, Is.False,
                "The count label must not intercept adjacent adjustment buttons.");
            Assert.That(Find<Text>("DepartureCrewSpeedPreview").raycastTarget, Is.False);
            Rect minus = BoundsIn(setup, Find<RectTransform>("DepartureOrdinaryCrewDecreaseButton"));
            Rect plus = BoundsIn(setup, Find<RectTransform>("DepartureOrdinaryCrewIncreaseButton"));
            Assert.That(minus.yMin, Is.EqualTo(plus.yMin).Within(.1f));
            Assert.That(minus.yMax, Is.EqualTo(plus.yMax).Within(.1f));
            foreach (string name in new[] { "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton" })
            {
                Button button = Find<Button>(name);
                Rect bounds = BoundsIn(setup, button.GetComponent<RectTransform>());
                Assert.That(bounds.width, Is.GreaterThanOrEqualTo(scene ? 36f : 39.9f), name);
                Assert.That(bounds.height, Is.GreaterThanOrEqualTo(scene ? 30.5f : 31.9f), name);
                Assert.That(button.targetGraphic.raycastTarget, Is.True);
            }

            Assert.That(Has("Route Heading"), Is.False, "The edited departure scene removes the redundant route heading.");
            Assert.That(Has("Upgrade Heading"), Is.False, "The edited departure scene removes the redundant upgrade heading.");
            Assert.That(Has("Replay Tutorial"), Is.False, "Departure setup no longer includes the tutorial button.");
            Toggle chartMode = Find<Toggle>("Use Chart Seed");
            Assert.That(chartMode.transform.parent, Is.SameAs(route));
            Rect modeBounds = BoundsIn(route, chartMode.GetComponent<RectTransform>());
            Rect treasureModeBounds = BoundsIn(route, Find<RectTransform>("Departure Treasure Seed Picker"));
            AssertInside(route, modeBounds, "Use Chart Seed");
            AssertInside(route, treasureModeBounds, "Departure Treasure Seed Picker");
            Assert.That(modeBounds.Overlaps(treasureModeBounds), Is.False,
                "Ordinary and treasure chart selection need independent hit areas within the paper.");
            Assert.That(modeBounds.yMin, Is.GreaterThanOrEqualTo(summaryBounds.yMax + 3.9f));
            Assert.That(treasureModeBounds.yMin, Is.GreaterThanOrEqualTo(summaryBounds.yMax + 3.9f));
            Assert.That(setup.GetComponentsInChildren<InputField>(true), Is.Empty,
                "Seed selection belongs entirely in the left frame.");
            Assert.That(setup.GetComponentsInChildren<Toggle>(true).Any(toggle => toggle.name == "Use Chart Seed"), Is.False);
            Rect seed = BoundsIn(route, Find<InputField>("Seed Input").GetComponent<RectTransform>());
            if (!scene)
            {
                AssertInside(route, seed, "Seed Input");
                Assert.That(seed.Overlaps(BoundsIn(route, Find<Text>("Seed Label").rectTransform)), Is.False);
            }
            Dropdown seedDropdown = Find<Dropdown>("Seed Input Dropdown");
            Assert.That(seedDropdown.options.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(seedDropdown.template, Is.Not.Null);
            Rect menu = BoundsIn(route, seedDropdown.GetComponent<RectTransform>());
            Assert.That(menu.width, Is.LessThan(seed.width * .4f), "The saved-chart menu must leave the seed text area usable.");
            Assert.That(Find<InputField>("Seed Input").enabled, Is.True);
            Assert.That(chartMode.isOn, Is.False);
            Assert.That(chartMode.graphic.canvasRenderer.GetAlpha(), Is.EqualTo(0f),
                "A newly created ordinary-route preview must not show a checked chart toggle.");

            controller.ConfigureEditorPreviewDepartureChart(1);
            Canvas.ForceUpdateCanvases();
            Toggle loadedChartMode = Find<Toggle>("Use Chart Seed");
            Assert.That(loadedChartMode.isOn, Is.True);
            Assert.That(loadedChartMode.graphic.canvasRenderer.GetAlpha(), Is.EqualTo(1f),
                "The selected-chart preview must visibly show its active chart mode.");
            route = Find<RectTransform>("Route Selection");
            string[] chartModules = { "Seed Input", "Departure Chart Route", "Departure Chart Preview",
                "Departure Chart Ports", "Chart Seed Validation" };
            Rect[] chartBounds = chartModules.Select(name => BoundsIn(route, Find<RectTransform>(name))).ToArray();
            AssertHiddenDeparturePortRow();
            Rect chartSummaryBounds = BoundsIn(route, Find<Text>("Departure Route Summary").rectTransform);
            for (int index = 0; index < chartBounds.Length; index++)
            {
                Assert.That(Find<RectTransform>(chartModules[index]).gameObject.activeInHierarchy, Is.True, chartModules[index]);
                AssertInside(route, chartBounds[index], chartModules[index]);
                for (int other = 0; other < index; other++)
                    Assert.That(chartBounds[index].Overlaps(chartBounds[other]), Is.False,
                        chartModules[index] + " / " + chartModules[other]);
                Assert.That(chartBounds[index].Overlaps(chartSummaryBounds), Is.False,
                    chartModules[index] + " / current chart route summary");
            }

            // The entry caption stays stable while money, cargo and routes change.
            controller.Session.Cargo.Water = 0;
            controller.Session.Cargo.Food = 0;
            controller.Session.RestoreProfile(0, controller.Session.CurrentPort);
            controller.Session.SelectRoute(CoreVoyageSession.Routes.Count - 1);
            controller.ConfigureEditorPreviewDepartureChart(0);
            Canvas.ForceUpdateCanvases();
            Button purchase = Find<Button>("Departure Buy Provisions");
            Text purchaseCaption = purchase.GetComponentInChildren<Text>();
            Assert.That(purchaseCaption.text, Is.EqualTo("购买基础物资"));
            Assert.That(Find<Button>("Adjust Voyage Supplies").GetComponentInChildren<Text>().text,
                Is.EqualTo("前往市场"));
            if (scene)
            {
                AssertPaintedArtRect(Find<RectTransform>("Voyage Setup"), purchase.GetComponent<RectTransform>(), new Rect(1008, 737, 220, 51));
                AssertPaintedArtRect(Find<RectTransform>("Voyage Setup"), Find<RectTransform>("Adjust Voyage Supplies"), new Rect(1854, 535, 178, 48));
                AssertPaintedControl(purchase);
                AssertPaintedControl(Find<Button>("Adjust Voyage Supplies"));
            }
            else
                Assert.That(purchase.GetComponent<RectTransform>().rect.width,
                    Is.EqualTo(Find<Button>("Adjust Voyage Supplies").GetComponent<RectTransform>().rect.width).Within(.1f));
            AssertReadableHeight(purchaseCaption, 13);
            Assert.That(purchase.interactable, Is.False, "Art previews do not purchase supplies.");
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void DepartureCrewConfigurationKeepsTitlesRosterAndFooterSeparated(CoreGameplayPreviewPort port)
        {
            Preview(port);
            Find<Button>("Configure Voyage Crew").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            string[] modules = {
                "Setup Title", "Departure Crew Configuration Title", "Departure Ordinary Crew Label",
                "Departure Ordinary Crew Slider", "Departure Ordinary Crew Hint", "Departure Special Crew Heading",
                "Departure Crew Total", "Back To Voyage Setup", "Confirm Departure Crew"
            };
            Assert.That(Has("Replay Tutorial"), Is.False, "The crew page shares the departure setup without a tutorial button.");
            modules = modules.Concat(Enumerable.Range(0, 6).Select(index => "Departure Special Crew Toggle " + index)).ToArray();
            Rect[] bounds = modules.Select(name => BoundsIn(setup, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < bounds.Length; index++)
            {
                AssertInside(setup, bounds[index], modules[index]);
                for (int other = 0; other < index; other++)
                    Assert.That(bounds[index].Overlaps(bounds[other]), Is.False, modules[index] + " / " + modules[other]);
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
            Assert.That(Find<Text>("Departure Route Summary").text,
                Is.EqualTo(ExpectedRouteSummary(origin, controller.Session.SelectedDestinationPort)));

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
                    Assert.That(Find<Text>("Departure Route Summary").text,
                        Is.EqualTo(ExpectedRouteSummary(origin, unchanged)));
                    continue;
                }

                picker.onClick.Invoke();
                Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(destination));
                AssertFixedDeparturePortRow(fixedPortBounds);
                Assert.That(controller.Session.CurrentPort, Is.EqualTo(origin),
                    "Choosing an arrival port must not move the player before departure.");
                Assert.That(Has("Destination Summary"), Is.False);
                Assert.That(Find<Text>("Departure Route Summary").text,
                    Is.EqualTo(ExpectedRouteSummary(origin, destination)),
                    "Each destination click must immediately update the route guidance above the port row.");
                Assert.That(Find<Button>("Departure Destination " + originIndex).IsInteractable(), Is.False);
                int alternative = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count)
                    .First(other => other != index && other != originIndex);
                Assert.That(Find<Button>("Departure Destination " + index).colors.normalColor,
                    Is.Not.EqualTo(Find<Button>("Departure Destination " + alternative).colors.normalColor),
                    "The confirmed destination must remain visibly selected.");
            }
        }

        [Test]
        public void DepartureChartHidesAllPortButtonsAndRandomModeRestoresOnlyOtherPorts()
        {
            Preview(CoreGameplayPreviewPort.GrayGull);
            string origin = controller.Session.CurrentPort;
            int targetIndex = Enumerable.Range(0, CoreVoyageSession.DestinationPorts.Count)
                .First(index => CoreVoyageSession.DestinationPorts[index] != origin);
            string target = CoreVoyageSession.DestinationPorts[targetIndex];
            string seed = CoreVoyageSession.CreateChartSeed(0, origin, target);
            Rect[] fixedPortBounds = AssertFixedDeparturePortRow();
            Find<Toggle>("Use Chart Seed").isOn = true;
            AssertHiddenDeparturePortRow();
            Assert.That(Find<Text>("Departure Route Summary").text, Is.EqualTo("选择已绘海图，当前航线：待确认"));
            Find<InputField>("Seed Input").text = "invalid-chart-seed";
            Assert.That(controller.Session.IsChartSeedSelected, Is.False);
            AssertHiddenDeparturePortRow();
            Assert.That(Find<Text>("Departure Route Summary").text, Is.EqualTo("选择已绘海图，当前航线：待确认"));
            Find<InputField>("Seed Input").text = seed;
            Find<InputField>("Seed Input").onEndEdit.Invoke(seed);
            Assert.That(controller.Session.IsChartSeedSelected, Is.True);
            AssertHiddenDeparturePortRow();
            string chartSummary = "目标港口由海图确定，当前航线：" + origin + "——" + target;
            Assert.That(Find<Text>("Departure Route Summary").text, Is.EqualTo(chartSummary));

            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                Button picker = Find<Button>("Departure Destination " + index);
                Assert.That(picker.IsInteractable(), Is.False, "A loaded chart fixes both endpoints.");
                picker.onClick.Invoke();
                Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(target));
                Assert.That(controller.Session.IsChartSeedSelected, Is.True);
                Assert.That(Find<Text>("Departure Route Summary").text, Is.EqualTo(chartSummary),
                    "Locked port callbacks must retain the chart's confirmed endpoints.");
            }
            Find<Toggle>("Use Chart Seed").isOn = false;
            AssertFixedDeparturePortRow(fixedPortBounds);
            Assert.That(Find<Text>("Departure Route Summary").text,
                Is.EqualTo("选择目标港口，当前航线：" + origin + "——" + controller.Session.SelectedDestinationPort));
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

        private static string ExpectedRouteSummary(string origin, string destination) => origin == "断锚港"
            ? "当前航线：" + origin + " → " + destination
            : "选择目标港口，当前航线：" + origin + "——" + destination;

        private void AssertDockRouteAndSetupGeometry(RectTransform route, RectTransform setup)
        {
            Assert.That(route.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize));
            Assert.That(route.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(setup.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize));
            Assert.That(setup.anchoredPosition, Is.EqualTo(Vector2.zero));
            Rect[] ports = AssertFixedDeparturePortRow();
            Rect summary = BoundsIn(route, Find<Text>("Departure Route Summary").rectTransform);
            AssertInside(route, summary, "Departure Route Summary");
            Assert.That(summary.yMin, Is.GreaterThanOrEqualTo(ports.Max(bounds => bounds.yMax) + 1.9f));
            Assert.That(Find<Text>("Departure Route Summary").raycastTarget, Is.False);
            AssertReadableHeight(Find<Text>("Departure Route Summary"), 18);
            for (int index = 1; index < ports.Length; index++)
            {
                Assert.That(ports[index].yMin, Is.EqualTo(ports[0].yMin).Within(.1f));
                Assert.That(ports[index].xMin - ports[index - 1].xMax, Is.GreaterThanOrEqualTo(1.9f));
            }

            RawImage hero = Find<RawImage>("Departure Dock Selected Sea");
            Rect detail = BoundsIn(route, Find<Text>("Departure Dock Sea Detail").rectTransform);
            AssertInside(route, detail, "Departure Dock Sea Detail");
            Assert.That(hero.isActiveAndEnabled, Is.True, "The selected chart is independently rendered over the empty parchment slot.");
            Assert.That(hero.texture, Is.Not.Null, "Retain the selected route asset for expanded editors.");
            Assert.That(detail.yMax, Is.LessThanOrEqualTo(ports.Min(bounds => bounds.yMin) - 3.9f));
            Assert.That(hero.raycastTarget, Is.False);
            Assert.That(Find<Text>("Departure Dock Sea Detail").raycastTarget, Is.False);
            AssertReadableHeight(Find<Text>("Departure Dock Sea Detail"), 16);
            AssertPaintedArtRect(route, Find<Text>("Departure Dock Sea Detail").rectTransform, new Rect(640, 719, 226, 160));

            Rect previous = default;
            for (int index = 0; index < CoreVoyageSession.Routes.Count; index++)
            {
                RectTransform row = Find<Button>("Route " + index).GetComponent<RectTransform>();
                Rect bounds = BoundsIn(route, row);
                AssertInside(route, bounds, row.name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(detail.yMin - 3.9f));
                AssertPaintedArtRect(route, row, new Rect(new[] { 15f, 305f, 584f }[index], 898, 278, 118));
                AssertPaintedControl(row.GetComponent<Button>());
                if (index > 0)
                {
                    Assert.That(bounds.yMin, Is.EqualTo(previous.yMin).Within(.1f));
                    Assert.That(bounds.xMin - previous.xMax, Is.GreaterThanOrEqualTo(.9f));
                }
                previous = bounds;
                RawImage art = Find<RawImage>("Route Preview " + index);
                Text caption = row.Find("Label").GetComponent<Text>();
                Assert.That(art.enabled, Is.False, "The original image must not cover the painted card.");
                Assert.That(art.texture, Is.Not.Null);
                Assert.That(caption.enabled, Is.True, "Route names remain crisp independent UI text.");
                Assert.That(art.raycastTarget, Is.False);
                Assert.That(caption.text, Does.Contain(CoreVoyageSession.Routes[index].Name));
                Text unlock = Find<Text>("Departure Dock Route Lock " + index);
                Assert.That(unlock.enabled, Is.True);
                Assert.That(unlock.raycastTarget, Is.False);
                if (controller.Session.PirateLevel < CoreVoyageSession.Routes[index].UnlockLevel)
                    Assert.That(unlock.text, Does.Contain(CoreVoyageSession.Routes[index].UnlockLevel.ToString()));
                else Assert.That(unlock.text, Is.Empty);
                Assert.That(Has("Destination Port " + index), Is.False);
            }

            string[] actions = { "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewIncreaseButton",
                "Departure Buy Provisions", "Adjust Voyage Supplies", "Configure Voyage Battle Decks",
                "Configure Voyage Crew", "Start Voyage" };
            Rect[] artwork = { new Rect(1425, 387, 40, 35), new Rect(1474, 387, 40, 35),
                new Rect(1008, 737, 220, 51), new Rect(1854, 535, 178, 48), new Rect(1326, 740, 239, 52),
                new Rect(1753, 742, 181, 50), new Rect(1573, 998, 473, 110) };
            Rect[] actionBounds = actions.Select(name => BoundsIn(setup, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < actions.Length; index++)
            {
                AssertInside(setup, actionBounds[index], actions[index]);
                Assert.That(Find<Button>(actions[index]).transform.parent, Is.SameAs(setup));
                AssertPaintedArtRect(setup, Find<RectTransform>(actions[index]), artwork[index]);
                AssertPaintedControl(Find<Button>(actions[index]));
                for (int other = 0; other < index; other++)
                    Assert.That(actionBounds[index].Overlaps(actionBounds[other]), Is.False,
                        actions[index] + " / " + actions[other]);
            }
            foreach (string label in new[] { "Setup Title", "Departure Dock Ordinary Readout", "Departure Dock Special Readout", "Departure Dock Speed Readout" })
            {
                AssertInside(setup, BoundsIn(setup, Find<Text>(label).rectTransform), label);
                Assert.That(Find<Text>(label).raycastTarget, Is.False);
                Assert.That(Find<Text>(label).enabled, Is.True);
            }
            Rect start = BoundsIn(setup, Find<RectTransform>("Start Voyage"));
            Assert.That(start.yMin - setup.rect.yMin, Is.GreaterThanOrEqualTo(7.9f));
            Assert.That(Find<Button>("Adjust Voyage Supplies").transform.GetSiblingIndex(),
                Is.GreaterThan(Find<Button>("Inspect Departure Vessel").transform.GetSiblingIndex()));
        }

        private static void AssertPaintedArtRect(RectTransform parent, RectTransform rect, Rect artwork)
        {
            Rect bounds = BoundsIn(parent, rect);
            Vector2 expected = DepartureDockSceneLayout.ArtPosition(artwork.center.x, artwork.center.y);
            Assert.That(bounds.center.x, Is.EqualTo(expected.x).Within(.1f), rect.name + " / painted x");
            Assert.That(bounds.center.y, Is.EqualTo(expected.y).Within(.1f), rect.name + " / painted y");
            Assert.That(bounds.width, Is.EqualTo(artwork.width * 1920f / DepartureDockSceneLayout.SourceWidth).Within(.1f), rect.name + " / painted width");
            Assert.That(bounds.height, Is.EqualTo(artwork.height * 1080f / DepartureDockSceneLayout.SourceHeight).Within(.1f), rect.name + " / painted height");
        }

        private static void AssertPaintedControl(Button button)
        {
            Image image = button.GetComponent<Image>();
            Assert.That(image.enabled, Is.True, button.name);
            Assert.That(image.color.a, Is.Zero, button.name);
            Assert.That(image.raycastTarget, Is.True, button.name);
            Assert.That(button.targetGraphic, Is.SameAs(image));
            Assert.That(button.GetComponentInChildren<DeparturePaintedHotspot>(), Is.Not.Null, button.name);
            bool iconOnly = button.name == "Departure Previous Vessel" || button.name == "Departure Next Vessel" ||
                button.name == "DepartureOrdinaryCrewDecreaseButton" || button.name == "DepartureOrdinaryCrewIncreaseButton";
            foreach (Text label in button.GetComponentsInChildren<Text>(true))
                Assert.That(label.enabled, Is.EqualTo(!iconOnly), button.name);
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

        private void AssertHiddenDeparturePortRow()
        {
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                Button button = Find<Button>("Departure Destination " + index);
                Assert.That(button.gameObject.activeInHierarchy, Is.False,
                    "Chart selection hides every manual destination, including empty or invalid chart input.");
                Assert.That(button.IsInteractable(), Is.False, "Hidden destinations cannot replace the chart endpoints.");
            }
        }

        private Rect[] AssertFixedDeparturePortRow(Rect[] expected = null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform route = Find<RectTransform>("Route Selection");
            bool painted = controller.Session.CurrentPort == "断锚港" && route.sizeDelta == PhaseSurfaceLayout.ContentSize;
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
                if (painted)
                {
                    AssertPaintedControl(button);
                    AssertPaintedArtRect(route, button.GetComponent<RectTransform>(), new Rect(35 + index * 134, 660, 131, 40));
                    Assert.That(label.enabled, Is.True, "The unlettered plaques require independent port names.");
                }
                else
                {
                    Assert.That(label.color.a, Is.EqualTo(1f), port + " / opaque name");
                    AssertReadableHeight(label, 18);
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f), port + " / complete name width");
                }
                if (port == controller.Session.CurrentPort)
                {
                    Assert.That(button.IsInteractable(), Is.False, "The current port remains unavailable as a destination.");
                    if (!painted)
                    {
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

        private void AssertRemovedDepartureInformation()
        {
            string[] names = { "Ship Stats", "Departure Ship Schematic Caption", "Departure Vessel Hint",
                "Destination Summary", "Cargo", "Departure Provision Quote",
                "Departure Provision Water Icon", "Departure Provision Food Icon" };
            names = names.Concat(Enumerable.Range(0, ComponentArtCatalog.SlotCount)
                .SelectMany(slot => new[] { "Upgrade Part " + slot, "Upgrade Name " + slot })).ToArray();
            foreach (string name in names)
                Assert.That(Has(name), Is.False, name + " must be removed from the simplified departure setup.");
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
