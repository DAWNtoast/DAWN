using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureDockCharts_EmptyArchiveInvalidInputAndOrdinaryReturnKeepDock()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.Cargo.Water = session.Cargo.Food = 80;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            Texture background = GameObject.Find("Voyage Background").GetComponent<RawImage>().texture;
            RectTransform vessel = GameObject.Find("Inspect Departure Vessel").GetComponent<RectTransform>();
            Vector2 vesselSize = vessel.sizeDelta, vesselPosition = vessel.anchoredPosition;

            yield return SetDepartureChartMode(true);
            AssertInlineDockRoots(background, vesselSize, vesselPosition);
            Assert.That(GameObject.Find("Departure Dock Chart Records Empty"), Is.Not.Null);
            Assert.That(InlineDockChartButtons(controller), Is.Empty);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.False);
            NavalBattleVisualCapture.Capture(controller, "DepartureDockCharts_Empty.png",
                "已绘海图空记录在原码头海图区域内显示，船只及出航控制保持原位置", "Route Selection");

            InputField input = GameObject.Find("Seed Input").GetComponent<InputField>();
            input.text = "unusable-chart-seed";
            input.onEndEdit.Invoke(input.text);
            yield return null;
            Assert.That(session.IsChartSeedSelected, Is.False);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.False);
            InvestmentResourceSnapshot before = SnapshotInvestmentResources(session);
            ActiveButton("Start Voyage").onClick.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            AssertInvestmentResourcesEqual(session, before);
            if (GameObject.Find("Chart Seed Warning") != null)
            { ClickAcceptanceButton("Close Chart Seed Warning"); yield return null; }

            yield return ClickDestinationTestGraphic(ActiveButton("Departure Dock Ordinary Voyage").gameObject, Vector2.zero);
            Assert.That(GameObject.Find("Use Chart Seed").GetComponent<Toggle>().isOn, Is.False);
            Assert.That(controller.GetComponentsInChildren<RectTransform>(true)
                .Single(item => item.name == "Departure Chart Selection").gameObject.activeInHierarchy, Is.False);
            AssertDeparturePortRowVisibility(controller, true);
            Assert.That(GameObject.Find("Departure Dock Selected Sea").GetComponent<RawImage>().isActiveAndEnabled, Is.True);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.True);
            AssertInlineDockRoots(background, vesselSize, vesselPosition);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureDockCharts_FiltersUnavailableRecordsSelectsRouteAndStartsSameSeed()
        {
            string first = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
            string foreign = CoreVoyageSession.CreateChartSeed(0, "铜钟港", "盐冠港");
            string locked = CoreVoyageSession.CreateChartSeed(2, "断锚港", "暮潮港");
            string second = CoreVoyageSession.CreateChartSeed(1, "断锚港", "铜钟港");
            InstallInlineDockChartArchive(first, foreign, locked, second);
            string archive = PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey);
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(3) });
            Assert.That(session.CanSelectRoute(1, out _), Is.True);
            Assert.That(session.CanSelectRoute(2, out _), Is.False);
            session.Cargo.Water = session.Cargo.Food = 80;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            Texture background = GameObject.Find("Voyage Background").GetComponent<RawImage>().texture;
            RectTransform vessel = GameObject.Find("Inspect Departure Vessel").GetComponent<RectTransform>();
            Vector2 vesselSize = vessel.sizeDelta, vesselPosition = vessel.anchoredPosition;
            Vector2 supplyPosition = ActiveButton("Departure Buy Provisions").GetComponent<RectTransform>().anchoredPosition;

            yield return SetDepartureChartMode(true);
            Button[] records = InlineDockChartButtons(controller);
            Assert.That(records.Length, Is.EqualTo(2), "Only charts joining this port on unlocked routes can be selected.");
            Assert.That(GameObject.Find("Departure Dock Chart Records Heading").GetComponent<Text>().text, Does.Contain("2"));
            yield return ClickDestinationTestGraphic(records[0].gameObject, Vector2.zero);
            Assert.That(session.SelectedSeed, Is.EqualTo(first));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo("白贝港"));
            yield return ClickDestinationTestGraphic(records[1].gameObject, Vector2.zero);
            Assert.That(session.SelectedSeed, Is.EqualTo(second));
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(1));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo("铜钟港"));
            Assert.That(GameObject.Find("Seed Input").GetComponent<InputField>().text, Is.EqualTo(second));
            Assert.That(GameObject.Find("Departure Chart Preview").GetComponentInChildren<VoyageChartSurfaceGraphic>().Field,
                Is.Not.Null, "A selected record displays its generated geography.");
            Assert.That(GameObject.Find("Departure Chart Ports").GetComponent<Text>().text,
                Does.Contain("断锚港").And.Contain("铜钟港"));
            AssertDeparturePortRowVisibility(controller, false);
            AssertInlineDockRoots(background, vesselSize, vesselPosition);
            Assert.That(ActiveButton("Departure Buy Provisions").GetComponent<RectTransform>().anchoredPosition,
                Is.EqualTo(supplyPosition), "Requoting another route must keep the purchase action on its painted dock plaque.");
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey), Is.EqualTo(archive),
                "Filtering and selecting records must not rewrite or prune the archive.");
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.True);
            NavalBattleVisualCapture.Capture(controller, "DepartureDockCharts_Selected.png",
                "只列出当前港口可用海图；点击第二张切换航线和端点，码头背景与船只保持原位", "Route Selection");
            yield return ClickDestinationTestGraphic(ActiveButton("Start Voyage").gameObject, Vector2.zero);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.CurrentChartSeed, Is.EqualTo(second));
            Assert.That(session.Map.DestinationName, Is.EqualTo("铜钟港"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureDockCharts_ScrollReachesEveryAvailableRecord()
        {
            string[] seeds = Enumerable.Range(0, 9)
                .Select(index => CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港")).ToArray();
            InstallInlineDockChartArchive(seeds);
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return SetDepartureChartMode(true);
            Button[] records = InlineDockChartButtons(controller);
            Assert.That(records.Length, Is.EqualTo(seeds.Length));
            ScrollRect scroll = GameObject.Find("Departure Dock Chart Records").GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0f;
            yield return null;
            yield return ClickDestinationTestGraphic(records.Last().gameObject, Vector2.zero);
            Assert.That(controller.Session.SelectedSeed, Is.EqualTo(seeds.Last()));
            Assert.That(GameObject.Find("Seed Input").GetComponent<InputField>().text, Is.EqualTo(seeds.Last()));
            NavalBattleVisualCapture.Capture(controller, "DepartureDockCharts_Scrolled.png",
                "多条可用海图在固定桌面区域滚动，末项可以真实点击并选中", "Route Selection");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureDockTreasure_EmptyInventoryStaysInlineAndOrdinaryReturnRestoresRoutes()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            session.Cargo.Water = session.Cargo.Food = 80;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            Texture background = GameObject.Find("Voyage Background").GetComponent<RawImage>().texture;
            RectTransform vessel = GameObject.Find("Inspect Departure Vessel").GetComponent<RectTransform>();
            Vector2 vesselSize = vessel.sizeDelta, vesselPosition = vessel.anchoredPosition;
            yield return ClickDestinationTestGraphic(ActiveButton("Departure Treasure Seed Picker").gameObject, Vector2.zero);
            AssertInlineDockRoots(background, vesselSize, vesselPosition);
            Assert.That(GameObject.Find("Departure Treasure Seed Selection"), Is.Null);
            Assert.That(GameObject.Find("Departure Dock Treasure Empty").GetComponent<Text>().text, Does.Contain("鉴定碎片"));
            Assert.That(GameObject.Find("Departure Dock Treasure Dropdown").GetComponent<Dropdown>().IsInteractable(), Is.False);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.False);
            Assert.That(session.SelectedTreasureMap, Is.Null);
            NavalBattleVisualCapture.Capture(controller, "DepartureDockTreasure_Empty.png",
                "藏宝海图空库存内嵌桌面，未选种子不可出航，船只保持原位", "Route Selection");
            yield return ClickDestinationTestGraphic(ActiveButton("Departure Dock Ordinary Voyage").gameObject, Vector2.zero);
            Assert.That(GameObject.Find("Departure Dock Treasure Selection"), Is.Null);
            Assert.That(session.IsChartSeedSelected, Is.False);
            Assert.That(GameObject.Find("Use Chart Seed").GetComponent<Toggle>().isOn, Is.False);
            Assert.That(ActiveButton("Route 0").IsInteractable(), Is.True);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.True);
            AssertInlineDockRoots(background, vesselSize, vesselPosition);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureDockTreasure_DropdownReachesLastSeedAndConsumesOnlyTheDepartedMap()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 1500,
                maps = Enumerable.Range(0, 6).Select(index => new CoreTreasureMapSave
                {
                    id = "dock-treasure-" + index,
                    rankId = index % 2 == 0 ? "weathered" : "gilded",
                    seed = new string('W', 63) + index,
                }).ToArray(),
            });
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            Assert.That(session.CanDrawSeaChart, Is.False);
            session.Cargo.Water = session.Cargo.Food = 80;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            Texture background = GameObject.Find("Voyage Background").GetComponent<RawImage>().texture;
            RectTransform vessel = GameObject.Find("Inspect Departure Vessel").GetComponent<RectTransform>();
            Vector2 vesselSize = vessel.sizeDelta, vesselPosition = vessel.anchoredPosition;
            yield return ClickDestinationTestGraphic(ActiveButton("Departure Treasure Seed Picker").gameObject, Vector2.zero);
            Assert.That(session.SelectedTreasureMap, Is.Null);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.False);
            Assert.That(GameObject.Find("Departure Dock Treasure Dropdown").GetComponent<Dropdown>().options.Count, Is.EqualTo(7));
            yield return SelectDepartureTreasureOption(0);
            Assert.That(GameObject.Find("Departure Dock Treasure Preview").GetComponent<RawImage>().texture.name,
                Is.EqualTo("treasure_chart_weathered"));
            yield return SelectDepartureTreasureOption(5);
            Assert.That(session.SelectedTreasureMap.Id, Is.EqualTo("dock-treasure-5"));
            Assert.That(GameObject.Find("Departure Dock Treasure Preview").GetComponent<RawImage>().texture.name,
                Is.EqualTo("treasure_chart_gilded"));
            Assert.That(session.TreasureMaps.Count, Is.EqualTo(6));
            Assert.That(GameObject.Find("Departure Treasure Seed Selection"), Is.Null);
            AssertInlineDockRoots(background, vesselSize, vesselPosition);
            NavalBattleVisualCapture.Capture(controller, "DepartureDockTreasure_LastSeed.png",
                "下拉可滚动选择第六张藏宝海图，左侧同步缩略图，选择本身不消耗种子", "Route Selection");
            Assert.That(session.CanStartTreasureVoyage(out string reason), Is.True, reason);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.True);
            yield return ClickDestinationTestGraphic(ActiveButton("Start Voyage").gameObject, Vector2.zero);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.IsTreasureVoyage, Is.True);
            Assert.That(session.ActiveTreasureMap.Id, Is.EqualTo("dock-treasure-5"));
            Assert.That(session.TreasureMaps.Select(map => map.Id), Is.EquivalentTo(
                Enumerable.Range(0, 5).Select(index => "dock-treasure-" + index)));
            LogAssert.NoUnexpectedReceived();
        }

        // Reused by full playthroughs: exercise the actual selectable at the
        // dock, while other ports retain their existing seed-picker buttons.
        private static IEnumerator SelectDepartureTreasureOption(int index)
        {
            GameObject control = GameObject.Find("Departure Dock Treasure Dropdown");
            if (control == null)
            {
                ClickAcceptanceButton("Departure Treasure Seed Option " + index);
                yield return null;
                yield break;
            }
            Dropdown dropdown = control.GetComponent<Dropdown>();
            string optionText = dropdown.options[index + 1].text;
            yield return ClickDestinationTestGraphic(dropdown.gameObject, Vector2.zero);
            Transform list = dropdown.transform.Find("Dropdown List");
            Assert.That(list, Is.Not.Null, "The dock seed selector opens its actual dropdown list.");
            Toggle option = list.GetComponentsInChildren<Toggle>().Single(item =>
                item.GetComponentInChildren<Text>().text == optionText);
            ScrollRect scroll = list.GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            if (scroll != null && scroll.content.rect.height > scroll.viewport.rect.height)
            {
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, option.transform);
                float range = scroll.content.rect.height - scroll.viewport.rect.height;
                float delta = bounds.max.y > scroll.viewport.rect.yMax ? bounds.max.y - scroll.viewport.rect.yMax :
                    bounds.min.y < scroll.viewport.rect.yMin ? bounds.min.y - scroll.viewport.rect.yMin : 0;
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + delta / range);
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
            yield return ClickDestinationTestGraphic(option.gameObject, Vector2.zero);
            yield return new WaitForSecondsRealtime(.2f);
        }
        private static Button[] InlineDockChartButtons(CoreGameplayDemoController controller) =>
            controller.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Departure Dock Chart Record "))
                .OrderBy(button => int.Parse(button.name.Substring("Departure Dock Chart Record ".Length))).ToArray();

        private static void AssertInlineDockRoots(Texture background, Vector2 vesselSize, Vector2 vesselPosition)
        {
            Assert.That(GameObject.Find("Voyage Background").GetComponent<RawImage>().texture, Is.SameAs(background));
            foreach (string name in new[] { "Route Selection", "Voyage Setup" })
            {
                RectTransform root = GameObject.Find(name).GetComponent<RectTransform>();
                Assert.That(root.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize));
                Assert.That(root.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(root.GetComponent<Image>().color.a, Is.Zero);
                Assert.That(root.GetComponent<Image>().raycastTarget, Is.False);
            }
            RectTransform vessel = GameObject.Find("Inspect Departure Vessel").GetComponent<RectTransform>();
            Assert.That(vessel.sizeDelta, Is.EqualTo(vesselSize));
            Assert.That(vessel.anchoredPosition, Is.EqualTo(vesselPosition));
        }

        private static void InstallInlineDockChartArchive(params string[] seeds)
        {
            var archive = new InlineDockChartArchiveFixture
            {
                charts = seeds.Select(seed =>
                {
                    Assert.That(CoreVoyageSession.TryParseChartSeed(seed, out CoreSeaChartDefinition definition), Is.True);
                    return new InlineDockChartRecordFixture
                    {
                        seed = definition.Seed,
                        routeIndex = definition.RouteIndex,
                        mapGeometryVersion = definition.MapGeometryVersion,
                        routeName = CoreVoyageSession.Routes[definition.RouteIndex].Name,
                        departurePort = definition.ForwardDeparturePort,
                        destinationPort = definition.ForwardDestinationPort,
                        tutorialWaters = definition.TutorialWaters,
                    };
                }).ToArray(),
            };
            PlayerPrefs.SetString(CoreGameplayDemoController.RecordedSeaChartsKey, JsonUtility.ToJson(archive));
        }

        [Serializable]
        private sealed class InlineDockChartArchiveFixture
        {
            public int version = 1;
            public InlineDockChartRecordFixture[] charts;
        }

        [Serializable]
        private sealed class InlineDockChartRecordFixture
        {
            public string seed, routeName, departurePort, destinationPort;
            public int routeIndex, mapGeometryVersion;
            public bool tutorialWaters;
        }
    }
}
