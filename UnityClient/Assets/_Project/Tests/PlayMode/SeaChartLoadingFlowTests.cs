using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
        [UnityTest, Timeout(120000)]
        public IEnumerator SeaChartLoading_DefaultRandomAndInvalidEndpointCannotDepartOrSpend()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            session.Cargo.Water = session.Cargo.Food = 80;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            Assert.That(GameObject.Find("Use Chart Seed").GetComponent<Toggle>().isOn, Is.False);
            InputField initialInput = controller.GetComponentsInChildren<InputField>(true).Single(input => input.name == "Seed Input");
            Assert.That(initialInput.gameObject.activeInHierarchy, Is.False);
            Assert.That(initialInput.IsInteractable(), Is.False);
            yield return EnterDepartureChartSeed(CoreVoyageSession.CreateChartSeed(1, "白贝港", "铜钟港"));
            AssertDeparturePortRowVisibility(controller, false);
            Assert.That(GameObject.Find("Chart Seed Warning"), Is.Not.Null);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.False);
            InvestmentResourceSnapshot before = SnapshotInvestmentResources(session);
            ActiveButton("Start Voyage").onClick.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            AssertInvestmentResourcesEqual(session, before);
            CaptureSeaChartFlow(controller, "01_Wrong_Port_Warning", "第三港不允许装载该海图，旧出航回调也不能扣款", "Chart Seed Warning");
            ClickAcceptanceButton("Close Chart Seed Warning"); yield return null;
            yield return SetDepartureChartMode(false);
            ClickAcceptanceButton("Start Voyage"); yield return null;
            string first = session.CurrentChartSeed;
            Assert.That(CoreVoyageSession.TryParseChartSeed(first, out var firstDefinition), Is.True);
            Assert.That(new[] { firstDefinition.PortA, firstDefinition.PortB }, Does.Contain(session.CurrentPort));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            yield return FinishChartFlowVoyage(false);
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            Assert.That(GameObject.Find("Use Chart Seed").GetComponent<Toggle>().isOn, Is.False);
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.CurrentChartSeed, Is.Not.EqualTo(first), "A new random departure creates a fresh chart only when departing.");
            CaptureSeaChartFlow(controller, "02_Fresh_Random_Voyage", "取消使用种子后，两次随机出航得到不同海域", "Navigation Chart");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SeaChartLoading_InputLocksRouteAndDestinationAndRecordedDropdownReopensBothEndsAfterReload()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            // This is a route-selection and persistence fixture, not earned
            // progression. Arrange the level needed by the higher-route chart.
            var progress = session.CaptureTreasureProgress();
            progress.pirateExperience = 1500;
            session.RestoreTreasureProgress(progress);
            session.Cargo.Water = session.Cargo.Food = 80;
            string origin = session.CurrentPort, destination = "白贝港";
            string seed = CoreVoyageSession.CreateChartSeed(1, origin, destination);
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return EnterDepartureChartSeed(seed);
            Assert.That(session.IsChartSeedSelected, Is.True);
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(1));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(destination));
            Assert.That(GameObject.Find("Departure Route Summary").GetComponent<Text>().text,
                Is.EqualTo("目标港口由海图确定，当前航线：" + origin + "——" + destination));
            // Changing the chart's route recreates its quote while the previous
            // objects await end-of-frame destruction. The replacements must
            // receive the same separated layout as the initial departure view.
            Canvas.ForceUpdateCanvases();
            RectTransform provisionBuy = ActiveButton("Departure Buy Provisions").GetComponent<RectTransform>();
            RectTransform marketBuy = ActiveButton("Adjust Voyage Supplies").GetComponent<RectTransform>();
            Assert.That(provisionBuy.anchoredPosition.y, Is.EqualTo(marketBuy.anchoredPosition.y).Within(.1f));
            Assert.That(provisionBuy.anchoredPosition.x + provisionBuy.rect.xMax,
                Is.LessThanOrEqualTo(marketBuy.anchoredPosition.x + marketBuy.rect.xMin - 7.9f),
                "Requoting a different chart must leave a clear gap between the supply buttons.");
            Assert.That(GameObject.Find("Departure Provision Quote"), Is.Null,
                "Requoting a chart must not recreate the removed detailed provisioning summary.");
            Assert.That(provisionBuy.rect.height, Is.GreaterThanOrEqualTo(57.9f),
                "The replacement purchase action keeps the larger shared layout.");
            foreach (int route in new[] { 0, 1, 2 })
            {
                Button routeButton = controller.GetComponentsInChildren<Button>(true).Single(button => button.name == "Route " + route);
                Assert.That(routeButton.gameObject.activeInHierarchy, Is.False, "The chart selection replaces the random route list.");
                Assert.That(routeButton.IsInteractable(), Is.False, "Loaded chart owns its route.");
            }
            for (int port = 0; port < CoreVoyageSession.DestinationPorts.Count; port++)
            {
                Button destinationButton = controller.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "Departure Destination " + port);
                Assert.That(destinationButton.gameObject.activeInHierarchy, Is.False,
                    "A loaded chart hides the manual port row because its endpoints are already fixed.");
                Assert.That(destinationButton.IsInteractable(), Is.False,
                    "A loaded chart owns its destination, including the selected port button.");
            }
            Assert.That(GameObject.Find("Departure Chart Ports").GetComponent<Text>().text,
                Does.Contain("出发港口：" + origin).And.Contain("到达港口：" + destination));
            Assert.That(GameObject.Find("Departure Chart Preview").GetComponentInChildren<VoyageChartSurfaceGraphic>(), Is.Not.Null);
            CaptureSeaChartFlow(controller, "03_Seed_Route_Locked", "真实输入完整海图种子后自动选择航线并显示地图与端点", "Route Selection");
            ClickAcceptanceButton("Adjust Voyage Supplies"); yield return null;
            Assert.That(ActiveButton("Return To Port Hub").GetComponentInChildren<Text>().text, Is.EqualTo("返回港口"));
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(GameObject.Find("Use Chart Seed").GetComponent<Toggle>().isOn, Is.True);
            Assert.That(GameObject.Find("Seed Input").GetComponent<InputField>().text, Is.EqualTo(seed));
            AssertDeparturePortRowVisibility(controller, false);
            ClickAcceptanceButton("Start Voyage"); yield return null;
            CoreVoyageMap first = session.Map;
            var terrain = first.TerrainCells.ToArray();
            var explored = first.ExploredCells.ToArray();
            var nodes = first.Nodes.Select(node => node.Encounter.Id + ":" + node.Position.X + ":" + node.Position.Y).ToArray();
            uint hash = first.Hash;
            VoyagePoint originalDeparture = first.Departure, originalDestination = first.Destination;
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, ""), Does.Not.Contain(seed));
            ClickAcceptanceButton("Safe Return"); yield return null;
            if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
            { ClickAcceptanceButton("Skip Settlement Reveal"); yield return null; }
            ClickAcceptanceButton("Draw Sea Chart"); yield return null;
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, ""), Does.Not.Contain(seed), "Drawing is pending until confirming the settlement.");
            ClickAcceptanceButton("Return To Port"); yield return null;
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, ""), Does.Contain(seed));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            session = controller.Session;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return SelectRecordedChartSeed(seed, "reload recorded chart at original endpoint");
            CaptureSeaChartFlow(controller, "04_Recorded_Chart_Selected", "确认保存并重载后，通过左侧已绘海图菜单选择种子并显示缩略图", "Route Selection");
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.Map.Hash, Is.EqualTo(hash));
            CollectionAssert.AreEqual(terrain, session.Map.TerrainCells);
            CollectionAssert.AreEqual(nodes, session.Map.Nodes.Select(node => node.Encounter.Id + ":" + node.Position.X + ":" + node.Position.Y).ToArray());
            for (int cell = 0; cell < explored.Length; cell++)
                if (explored[cell]) Assert.That(session.Map.ExploredCells[cell], Is.True, "Recorded exploration " + cell);
            yield return FinishChartFlowVoyage(false);
            // The UI/load boundary is under test. This public port fixture is not
            // presented as a naturally completed voyage to the other endpoint.
            RestoreChartEndpointWithNavigatorFixture(controller, destination);
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return SelectRecordedChartSeed(seed, "load recorded chart from opposite endpoint");
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(origin));
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(session.Map.Hash, Is.EqualTo(hash));
            CollectionAssert.AreEqual(terrain, session.Map.TerrainCells);
            AssertDestinationTestPoint(session.Position, originalDestination);
            AssertDestinationTestPoint(session.Map.Destination, originalDeparture);
            Assert.That(session.Map.DestinationName, Is.EqualTo(origin));
            CaptureSeaChartFlow(controller, "05_Reverse_Same_Chart", "公开到港夹具验证反向装载：地形不变，船位与目标端点互换", "Navigation Chart");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator SeaChartLoading_RealTutorialChartReloadsAsOrdinaryShortWatersWithoutRepeatingBossOrRewards()
        {
            yield return LoadGuidedTutorialScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "DrawChart", new HashSet<string>(), false);
            // The settled voyage map is released on return to port. Capture its
            // actual geography while the chart can still be drawn.
            string seed = controller.Session.CurrentChartSeed;
            Assert.That(CoreVoyageSession.TryParseChartSeed(seed, out var chart), Is.True);
            Assert.That(chart.TutorialWaters, Is.True);
            var terrain = controller.Session.Map.TerrainCells.ToArray();
            uint hash = controller.Session.Map.Hash;
            string originalPort = controller.Session.CurrentPort;
            string oppositePort = chart.OtherPort(originalPort);
            VoyagePoint originalDeparture = controller.Session.Map.Departure;
            VoyagePoint originalDestination = controller.Session.Map.Destination;
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey),
                Does.Contain(CoreGuidedTutorialContent.BossEventId), "Actual boss completion is saved before reloading the chart.");
            yield return DriveGuidedTutorial(controller, "Complete", new HashSet<string>(), false);
            Assert.That(controller.IsTutorialActive, Is.False);
            string[] components = controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray();
            int fragments = controller.Session.TreasureFragments;
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return SelectRecordedChartSeed(seed);
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(controller.Session.GuidedTutorialLeg, Is.Zero, "Loading the chart must not restart tutorial progression.");
            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(controller.Session.Map.Route.NauticalMilesPerMapUnit, Is.EqualTo(10f));
            Assert.That(controller.Session.Map.Hash, Is.EqualTo(hash));
            CollectionAssert.AreEqual(terrain, controller.Session.Map.TerrainCells);
            Assert.That(controller.Session.BossDefeated, Is.True, "The saved real boss victory must open the port on a revisit.");
            Assert.That(controller.Session.Map.Nodes.Any(node => node.IsBoss && !node.Visited), Is.False);
            CaptureSeaChartFlow(controller, "06_Tutorial_Chart_Reused", "实际三航次教学绘图后重载，保留近港短程地形与首领完成记录", "Navigation Chart");
            yield return FinishChartFlowVoyage(false);
            // As above, this public port fixture isolates the second endpoint
            // loader; the tutorial victory and its saved record were played.
            RestoreChartEndpointWithNavigatorFixture(controller, oppositePort);
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return SelectRecordedChartSeed(seed, "load tutorial chart from opposite endpoint");
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(controller.Session.GuidedTutorialLeg, Is.Zero);
            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(controller.Session.Map.Hash, Is.EqualTo(hash));
            CollectionAssert.AreEqual(terrain, controller.Session.Map.TerrainCells);
            AssertDestinationTestPoint(controller.Session.Position, originalDestination);
            AssertDestinationTestPoint(controller.Session.Map.Destination, originalDeparture);
            Assert.That(controller.Session.Map.DestinationName, Is.EqualTo(originalPort));
            Assert.That(controller.Session.BossDefeated, Is.True);
            CaptureSeaChartFlow(controller, "07_Tutorial_Chart_Reverse", "另一端载入同一教学海图，首领与一次性教学奖励均不重置", "Navigation Chart");
            yield return FinishChartFlowVoyage(false);
            Assert.That(controller.Session.TreasureFragments, Is.EqualTo(fragments));
            CollectionAssert.AreEqual(components, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray());
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator SeaChartLoading_RealTutorialVictoryBeforeSettlementSurvivesReloadAndCommitsRewardsOnce()
        {
            yield return LoadGuidedTutorialScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var firstRun = new HashSet<string>();
            yield return DriveGuidedTutorial(controller, "DrawChart", firstRun, false);
            Assert.That(firstRun, Does.Contain("TutorialBoss"), "The first victory must be played with the real battle UI.");
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(controller.Session.Settlement.BankedFragments, Is.EqualTo(1));
            Assert.That(controller.Session.Settlement.BankedPirateExperience, Is.EqualTo(80));
            Assert.That(controller.Session.TreasureFragments, Is.Zero, "The settlement has not been confirmed.");
            Assert.That(controller.Session.PirateExperience, Is.EqualTo(20), "Only the first lesson's 20 experience is banked before boss settlement.");
            string seed = controller.Session.CurrentChartSeed;
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.RecordedSeaChartsKey, ""), Does.Not.Contain(seed));
            var savedVictory = JsonUtility.FromJson<CoreSeaChartProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey));
            Assert.That(savedVictory.charts.Single(chart => chart.seed == seed).pendingTutorialFragments, Is.EqualTo(1));
            CaptureSeaChartFlow(controller, "08_Tutorial_Victory_Unconfirmed", "真实首胜后未绘图、未确认结算，立即重载场景", "Settlement Card");

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var resumedSteps = new HashSet<string>();
            yield return DriveGuidedTutorial(controller, "DrawChart", resumedSteps, false);
            Assert.That(resumedSteps, Does.Not.Contain("TutorialBoss"), "A saved victory must never require fighting the tutorial boss again.");
            Assert.That(controller.Session.CurrentChartSeed, Is.EqualTo(seed));
            Assert.That(controller.Session.Map.Nodes.Any(node => node.IsBoss), Is.False);
            Assert.That(controller.Session.Settlement.BankedFragments, Is.EqualTo(1));
            Assert.That(controller.Session.Settlement.BankedPirateExperience, Is.EqualTo(80));
            CaptureSeaChartFlow(controller, "09_Tutorial_Victory_Recovered", "重载后首领保持消失，真实返航结算恢复待领取碎片与经验", "Settlement Card");
            yield return DriveGuidedTutorial(controller, "Complete", resumedSteps, false);
            Assert.That(resumedSteps, Does.Not.Contain("TutorialBoss"));
            Assert.That(controller.Session.TreasureFragments, Is.EqualTo(1));
            Assert.That(controller.Session.PirateExperience, Is.EqualTo(100));
            Assert.That(controller.Session.PirateLevel, Is.EqualTo(2));
            string[] components = controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray();
            savedVictory = JsonUtility.FromJson<CoreSeaChartProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.SeaChartProgressKey));
            Assert.That(savedVictory.charts.Single(chart => chart.seed == seed).pendingTutorialFragments, Is.Zero);

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single); yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(controller.Session.TreasureFragments, Is.EqualTo(1));
            Assert.That(controller.Session.PirateExperience, Is.EqualTo(100));
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            yield return SelectRecordedChartSeed(seed);
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(controller.Session.GuidedTutorialLeg, Is.Zero);
            Assert.That(controller.Session.Map.Nodes.Any(node => node.IsBoss), Is.False);
            Assert.That(controller.Session.VoyageTreasureFragments, Is.Zero);
            CaptureSeaChartFlow(controller, "10_Recovered_Chart_Revisited", "领取并再次重载后正常重访海图，首胜奖励不重复", "Navigation Chart");
            yield return FinishChartFlowVoyage(false);
            Assert.That(controller.Session.TreasureFragments, Is.EqualTo(1));
            Assert.That(controller.Session.PirateExperience, Is.EqualTo(100), "Returning without exploring must not repeat the recovered boss experience.");
            CollectionAssert.AreEqual(components, controller.OwnedComponents.Select(item => item.InstanceId).OrderBy(id => id).ToArray());
            LogAssert.NoUnexpectedReceived();
        }

        private static void RestoreChartEndpointWithNavigatorFixture(CoreGameplayDemoController controller, string port)
        {
            // RestoreProfile changes the captain's port, not crew locations.
            // This endpoint-loader fixture explicitly brings the same recruited
            // navigator along, matching a completed arrival's crew prerequisite.
            string navigatorId = PlayerPrefs.GetString(CoreGameplayDemoController.ActiveCrewVariantKey(0), string.Empty);
            Assert.That(navigatorId, Is.Not.Empty, "The fixture must already have an active navigator identity.");
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(navigatorId)), Is.EqualTo(1));
            controller.Session.RestoreProfile(controller.Session.ProfileGold, port);
            PlayerPrefs.SetString(CoreGameplayDemoController.CrewLocationKeyForReset(navigatorId), port);
        }

        private static IEnumerator SetDepartureChartMode(bool useChart, string stage = "departure chart mode")
        {
            Toggle toggle = GameObject.Find("Use Chart Seed").GetComponent<Toggle>();
            if (useChart)
            {
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.CanDrawSeaChart, Is.True,
                    stage + " at " + controller.Session.CurrentPort + ": the fixture must have an available navigator before loading a chart.");
                Assert.That(toggle.IsInteractable(), Is.True, "Chart selection must be available outside the guided tutorial.");
            }
            if (toggle.isOn != useChart)
                yield return ClickDestinationTestGraphic(toggle.gameObject, Vector2.zero);
            Assert.That(GameObject.Find("Use Chart Seed").GetComponent<Toggle>().isOn, Is.EqualTo(useChart));
            AssertDeparturePortRowVisibility(Object.FindFirstObjectByType<CoreGameplayDemoController>(), !useChart);
        }

        private static void AssertDeparturePortRowVisibility(CoreGameplayDemoController controller, bool visible)
        {
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
            {
                Button button = controller.GetComponentsInChildren<Button>(true)
                    .Single(item => item.name == "Departure Destination " + index);
                Assert.That(button.gameObject.activeInHierarchy, Is.EqualTo(visible),
                    "Manual destinations are shown only outside chart-selection mode.");
                Assert.That(button.IsInteractable(),
                    Is.EqualTo(visible && CoreVoyageSession.DestinationPorts[index] != controller.Session.CurrentPort),
                    "Restoring manual selection must keep the current port unavailable.");
            }
        }

        private static IEnumerator EnterDepartureChartSeed(string seed, bool endEdit = true)
        {
            yield return SetDepartureChartMode(true, "enter shared chart seed");
            InputField input = GameObject.Find("Seed Input").GetComponent<InputField>();
            Assert.That(input.enabled && input.IsInteractable(), Is.True, "The player must have an editable seed input.");
            // Hit the text area, leaving the right-hand recorded-chart menu alone.
            yield return ClickDestinationTestGraphic(input.gameObject, new Vector2(-70f, 0f));
            Assert.That(input.isFocused, Is.True);
            input.ProcessEvent(Event.KeyboardEvent("^a"));
            input.ProcessEvent(Event.KeyboardEvent("backspace"));
            foreach (char character in seed)
                input.ProcessEvent(new Event { type = EventType.KeyDown, character = character, keyCode = KeyCode.None });
            Assert.That(input.text, Is.EqualTo(seed));
            if (endEdit) input.DeactivateInputField();
            yield return null;
        }

        private static IEnumerator SelectRecordedChartSeed(string seed, string stage = "select recorded chart")
        {
            yield return SetDepartureChartMode(true, stage);
            Dropdown dropdown = GameObject.Find("Seed Input Dropdown").GetComponent<Dropdown>();
            Assert.That(dropdown.IsInteractable(), Is.True);
            Assert.That(dropdown.options.Count, Is.GreaterThan(1));
            // These fixtures archive exactly one real chart. Its friendly label
            // need not expose the complete seed; the input must receive it.
            yield return ClickDestinationTestGraphic(dropdown.gameObject, Vector2.zero);
            string label = dropdown.options[1].text;
            Toggle option = Object.FindObjectsByType<Toggle>(FindObjectsSortMode.None).Single(toggle =>
                toggle.gameObject.activeInHierarchy && toggle.transform.root == dropdown.transform.root &&
                toggle.GetComponentsInChildren<Text>().Any(text => text.text == label));
            yield return ClickDestinationTestGraphic(option.gameObject, Vector2.zero);
            Assert.That(GameObject.Find("Seed Input").GetComponent<InputField>().text, Is.EqualTo(seed));
        }

        private static IEnumerator FinishChartFlowVoyage(bool draw)
        {
            ClickAcceptanceButton("Safe Return"); yield return null;
            if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
            { ClickAcceptanceButton("Skip Settlement Reveal"); yield return null; }
            if (draw) { ClickAcceptanceButton("Draw Sea Chart"); yield return null; }
            ClickAcceptanceButton("Return To Port"); yield return null;
        }

        private static void CaptureSeaChartFlow(CoreGameplayDemoController controller, string stage, string note, string root) =>
            NavalBattleVisualCapture.Capture(controller, "SeaChart_" + stage + ".png", "海图装载 UI 回归 · " + note, root);
    }
}
