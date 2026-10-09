using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class VoyageDepartureShipLayoutTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DepartureReadingSizes_EnlargeBothFramesKeepActionsFixedAndShowCompleteDetails(int level)
        {
            GameReadability.OverrideLevel = level;
            Preview(CoreGameplayPreviewPort.GrayGull);
            Canvas.ForceUpdateCanvases();
            RectTransform route = Find<RectTransform>("Route Selection");
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            Assert.That(route.rect.width, Is.EqualTo(900).Within(.1f));
            Assert.That(setup.rect.width, Is.EqualTo(950).Within(.1f));
            Assert.That(setup.rect.width, Is.GreaterThan(route.rect.width), "Departure configuration gets the wider column.");
            Assert.That(route.rect.height, Is.EqualTo(880).Within(.1f));
            Assert.That(setup.rect.height, Is.EqualTo(880).Within(.1f));
            Rect left = BoundsIn(route.parent, route), right = BoundsIn(route.parent, setup);
            Assert.That(right.xMin - left.xMax, Is.GreaterThanOrEqualTo(23.9f));
            Assert.That(left.xMin, Is.GreaterThanOrEqualTo(-940));
            Assert.That(right.xMax, Is.LessThanOrEqualTo(940));
            Assert.That(left.yMax, Is.LessThanOrEqualTo(540 - PhaseSurfaceLayout.TutorialHeaderHeight - 11.9f),
                "The same larger frames must also clear the taller tutorial header.");
            Assert.That(right.yMin, Is.GreaterThanOrEqualTo(-500.1f));
            AssertFixedDeparturePortRow();
            AssertDepartureReadingText(route);
            AssertDepartureReadingText(setup);
            Assert.That(Find<Text>("Ship Stats").fontSize, Is.EqualTo(GameReadability.FontSize(22)));
            Assert.That(Find<Text>("Cargo").fontSize, Is.EqualTo(GameReadability.FontSize(22)));
            Assert.That(Find<Text>("Departure Provision Quote").fontSize, Is.EqualTo(GameReadability.FontSize(22)));
            ScrollRect scroll = Find<ScrollRect>("Departure Details Viewport");
            Assert.That(scroll.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.content.rect.width, Is.GreaterThanOrEqualTo(872), "The wider panel must give its contents usable width.");
            foreach (RectTransform child in scroll.content)
            {
                Assert.That(child.anchorMin.y, Is.EqualTo(1f), child.name + " uses the top of the scroll content.");
                Assert.That(child.anchorMax.y, Is.EqualTo(1f), child.name);
            }
            AssertInside(scroll.viewport, BoundsIn(scroll.viewport, Find<RectTransform>("Ship Stats")), "First ship row before scrolling");
            float contentHeight = scroll.content.rect.height;
            DepartureLayout.ApplySetup(setup);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.EqualTo(contentHeight).Within(.1f), "Reflow must not repeatedly grow the content or shift its rows.");
            string[] actions = { "Departure Buy Provisions", "Adjust Voyage Supplies", "Configure Voyage Battle Decks", "Configure Voyage Crew", "Start Voyage" };
            Rect[] before = actions.Select(name => BoundsIn(setup, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < actions.Length; index++)
            {
                AssertInside(setup, before[index], actions[index]);
                Assert.That(before[index].Overlaps(BoundsIn(setup, scroll.viewport)), Is.False, actions[index]);
                Assert.That(Find<RectTransform>(actions[index]).IsChildOf(scroll.content), Is.False);
            }
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            AssertInside(scroll.viewport, BoundsIn(scroll.viewport, Find<RectTransform>("Departure Provision Quote")), "Last quote after scrolling");
            for (int index = 0; index < actions.Length; index++)
                Assert.That(Vector2.Distance(BoundsIn(setup, Find<RectTransform>(actions[index])).center, before[index].center), Is.LessThan(.01f));
            DepartureLayout.ApplySetup(setup);
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.001f), "Refreshing a quote preserves the reading position.");
            Assert.That(setup.GetComponentsInChildren<ScrollRect>(true).Length, Is.EqualTo(1), "Repeated layout must reuse its viewport.");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DepartureReadingSizes_ChartAndInvalidSeedKeepSixPortsAndCompleteRows(int level)
        {
            GameReadability.OverrideLevel = level;
            Preview(CoreGameplayPreviewPort.GrayGull);
            Rect[] ports = AssertFixedDeparturePortRow();
            controller.ConfigureEditorPreviewDepartureChart(1);
            Canvas.ForceUpdateCanvases();
            RectTransform route = Find<RectTransform>("Route Selection");
            AssertFixedDeparturePortRow(ports);
            string[] rows = { "Use Chart Seed", "Seed Input", "Departure Chart Route", "Departure Chart Preview", "Departure Chart Ports", "Chart Seed Validation" };
            Rect[] bounds = rows.Select(name => BoundsIn(route, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < rows.Length; index++)
            {
                AssertInside(route, bounds[index], rows[index]);
                foreach (Rect port in ports) Assert.That(bounds[index].Overlaps(port), Is.False, rows[index]);
                for (int other = 0; other < index; other++) Assert.That(bounds[index].Overlaps(bounds[other]), Is.False, rows[index] + " / " + rows[other]);
            }
            AssertDepartureReadingText(route);
            string destination = controller.Session.SelectedDestinationPort;
            foreach (Button button in route.GetComponentsInChildren<Button>().Where(item => item.name.StartsWith("Departure Destination ")))
            {
                Assert.That(button.IsInteractable(), Is.False);
                button.onClick.Invoke();
                Assert.That(controller.Session.SelectedDestinationPort, Is.EqualTo(destination));
            }
            InputField input = Find<InputField>("Seed Input");
            input.text = "NOT-A-SEA-CHART";
            Assert.That(Find<Button>("Start Voyage").IsInteractable(), Is.False);
            AssertFixedDeparturePortRow(ports);
            AssertDepartureReadingText(route);
            Find<Toggle>("Use Chart Seed").isOn = false;
            Assert.That(Find<Button>("Start Voyage").IsInteractable(), Is.True);
            AssertFixedDeparturePortRow(ports);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DepartureReadingSizes_CrewRosterUsesTwoColumnsAndLastRowRemainsReachable(int level)
        {
            GameReadability.OverrideLevel = level;
            Preview(CoreGameplayPreviewPort.GrayGull);
            Find<Button>("Configure Voyage Crew").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            RectTransform setup = Find<RectTransform>("Voyage Setup");
            ScrollRect scroll = Find<ScrollRect>("Departure Details Viewport");
            AssertDepartureReadingText(setup);
            Rect[] bounds = Enumerable.Range(0, 6).Select(index => BoundsIn(scroll.content, Find<RectTransform>("Departure Special Crew Toggle " + index))).ToArray();
            for (int index = 0; index < bounds.Length; index++)
            {
                AssertInside(scroll.content, bounds[index], "Crew " + index);
                for (int other = 0; other < index; other++) Assert.That(bounds[index].Overlaps(bounds[other]), Is.False);
                if (index % 2 == 1)
                    Assert.That(bounds[index].center.y, Is.EqualTo(bounds[index - 1].center.y).Within(.1f), "Each row has two readable cards.");
            }
            Rect confirm = BoundsIn(setup, Find<RectTransform>("Confirm Departure Crew"));
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            AssertInside(scroll.viewport, BoundsIn(scroll.viewport, Find<RectTransform>("Departure Special Crew Toggle 5")), "Last crew card after scrolling");
            AssertInside(scroll.viewport, BoundsIn(scroll.viewport, Find<RectTransform>("Departure Crew Total")), "Crew total after scrolling");
            Assert.That(Vector2.Distance(confirm.center, BoundsIn(setup, Find<RectTransform>("Confirm Departure Crew")).center), Is.LessThan(.01f));
            Assert.That(confirm.Overlaps(BoundsIn(setup, scroll.viewport)), Is.False);
            Find<Button>("Back To Voyage Setup").onClick.Invoke();
            // Edit-mode callbacks intentionally guard gameplay; Play regression covers confirmation.
            Assert.That(controller.Session.Phase, Is.EqualTo(ShatteredSail.Core.Gameplay.CoreDemoPhase.Port));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DepartureReadingSizes_TreasureKeepsAllRulesAndReadOnlyPorts(int level)
        {
            GameReadability.OverrideLevel = level;
            Preview(CoreGameplayPreviewPort.TwilightTide);
            controller.ConfigureEditorFlowState(GameplayFlowPreviewState.TreasureDeparture);
            Canvas.ForceUpdateCanvases();
            RectTransform route = Find<RectTransform>("Route Selection");
            AssertFixedDeparturePortRow();
            AssertDepartureReadingText(route);
            string[] names = { "Treasure Departure Title", "Treasure Departure Art", "Treasure Departure Detail", "Treasure Departure Consumption", "Cancel Treasure Voyage", "Return To Treasure Table" };
            Rect[] bounds = names.Select(name => BoundsIn(route, Find<RectTransform>(name))).ToArray();
            for (int index = 0; index < names.Length; index++)
            {
                AssertInside(route, bounds[index], names[index]);
                for (int other = 0; other < index; other++) Assert.That(bounds[index].Overlaps(bounds[other]), Is.False, names[index] + " / " + names[other]);
            }
            string selectedMap = controller.Session.SelectedTreasureMap.Id;
            foreach (Button button in route.GetComponentsInChildren<Button>().Where(item => item.name.StartsWith("Departure Destination ")))
            {
                Assert.That(button.IsInteractable(), Is.False);
                button.onClick.Invoke();
                Assert.That(controller.Session.SelectedTreasureMap.Id, Is.EqualTo(selectedMap));
            }
            Assert.That(Find<Text>("Treasure Departure Consumption").text, Does.Contain("不返还").And.Contain("95%"));
        }

        private static void AssertDepartureReadingText(RectTransform root)
        {
            foreach (Text label in root.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrWhiteSpace(label.text)) continue;
                Assert.That(label.resizeTextForBestFit, Is.False, label.name + " must never shrink the chosen reading size.");
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(18)), label.name);
                bool title = label.name.EndsWith("Title") || label.name == "Departure Chart Route" || label.name == "Departure Special Crew Heading";
                Assert.That(label.font, Is.SameAs(title ? GameTypography.InterfaceFont : GameTypography.BodyFont), label.name);
                if (title) Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(32)), label.name);
                AssertReadableHeight(label, GameReadability.FontSize(18));
                TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                using (var visible = new TextGenerator())
                using (var complete = new TextGenerator())
                {
                    visible.Populate(label.text, settings);
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    complete.Populate(label.text, settings);
                    Assert.That(visible.characterCount, Is.EqualTo(complete.characterCount), label.name + " / final character");
                    Assert.That(visible.vertexCount, Is.EqualTo(complete.vertexCount), label.name + " / final glyph");
                }
            }
        }
    }
}
