using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
        [UnityTest]
        public IEnumerator DestinationBossGuidance_DiscoveryDimsPortAndOffersExplicitNavigation()
        {
            yield return LoadDestinationBossGuidanceFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            CoreMapNode boss = session.Map.Nodes.Single();
            Color originalPort = GameObject.Find("Destination Port").GetComponent<Graphic>().color;
            Assert.That(GameObject.Find("Destination Boss Guidance"), Is.Null);
            Assert.That(session.DestinationPortDiscovered, Is.False);
            Assert.That(boss.Discovered, Is.False);

            // Use the real movement callback: Core commits sighting immediately,
            // and Presentation refreshes only after the existing sailing tween.
            SailingDirection approachHeading = session.Heading;
            ActiveButton("Execute Sail").onClick.Invoke();
            Assert.That(session.DestinationPortDiscovered, Is.True);
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.IsDestinationBossGuidanceActive, Is.True);
            Assert.That(session.Heading, Is.EqualTo(approachHeading), "Discovery must not turn the ship toward the boss.");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.ActiveEncounter, Is.Null);
            Assert.That(boss.Discovered, Is.True);
            int column = Mathf.Clamp((int)(boss.Position.X / session.Map.WorldWidth * session.Map.Columns), 0, session.Map.Columns - 1);
            int row = Mathf.Clamp((int)(boss.Position.Y / session.Map.WorldHeight * session.Map.Rows), 0, session.Map.Rows - 1);
            Assert.That(session.Map.ExploredCells[row * session.Map.Columns + column], Is.False,
                "The dedicated boss marker must remain visible over unexplored fog.");
            Graphic dimPort = GameObject.Find("Destination Port").GetComponent<Graphic>();
            Assert.That(dimPort.color.r, Is.EqualTo(originalPort.r * .46f).Within(.001f));
            Assert.That(dimPort.color.g, Is.EqualTo(originalPort.g * .46f).Within(.001f));
            Assert.That(GameObject.Find("Destination Port Guidance Label").GetComponent<Text>().text, Does.Contain("封锁"));
            Assert.That(GameObject.Find("Destination Boss Guidance Text").GetComponent<Text>().text, Does.Contain("解除封锁"));
            Assert.That(GameObject.Find("Destination Boss Guidance Halo"), Is.Not.Null);
            Assert.That(GameObject.Find("Destination Boss Guidance Ring"), Is.Not.Null);
            Assert.That(GameObject.Find("Destination Label"), Is.Null);
            Assert.That(GameObject.Find("Event Label " + boss.Id), Is.Null);
            Rect frameBounds = DestinationBossWorldRect(GameObject.Find("Procedural Sea Chart Frame").GetComponent<RectTransform>());
            Rect promptBounds = DestinationBossWorldRect(GameObject.Find("Destination Boss Guidance").GetComponent<RectTransform>());
            Assert.That(promptBounds.yMax, Is.LessThanOrEqualTo(frameBounds.yMin + .1f), "The guidance reserves space below the interactive map.");
            NavalBattleVisualCapture.Capture(controller, "Port_Boss_Discovered.png",
                "目的港首次进入实际视野后置暗，底部显示解除封锁与首领定位入口", "Navigation Chart");

            var costs = new NavigationArrivalCosts(session);
            bool hadTarget = session.HasDirectCourseTarget;
            VoyagePoint previousTarget = session.DirectCourseTarget;
            SailingDirection previousHeading = session.Heading;
            NavigationMapViewport viewport = GameObject.Find("Map Layer").GetComponent<NavigationMapViewport>();
            // The full map stays visible. Even an explicit pan request must not
            // hide the southern boss or select a sailing course.
            Vector2 originalCenter = viewport.ViewCenter;
            viewport.PanBy(new Vector2(100000f, -100000f));
            Canvas.ForceUpdateCanvases();
            Assert.That(DestinationBossMarkerInsideFrame(boss), Is.True, "The whole world remains inside the fixed chart.");
            Assert.That(Vector2.Distance(viewport.ViewCenter, originalCenter), Is.LessThan(.001f));
            yield return ClickDestinationTestGraphic(ActiveButton("Locate Blocking Boss").gameObject, Vector2.zero);
            costs.AssertUnchanged(session);
            Assert.That(session.Heading, Is.EqualTo(previousHeading));
            Assert.That(session.HasDirectCourseTarget, Is.EqualTo(hadTarget));
            AssertDestinationTestPoint(session.DirectCourseTarget, previousTarget);
            Assert.That(DestinationBossMarkerInsideFrame(boss), Is.True, "Locate keeps the highlighted boss visible without moving the chart.");
            Assert.That(Vector2.Distance(viewport.ViewCenter, originalCenter), Is.LessThan(.001f));
            Vector2 locatedCenter = viewport.ViewCenter;
            RenderDestinationBossFixture(controller);
            yield return null;
            viewport = GameObject.Find("Map Layer").GetComponent<NavigationMapViewport>();
            Assert.That(Vector2.Distance(viewport.ViewCenter, locatedCenter), Is.LessThan(.001f), "A dashboard refresh keeps the chart centered.");
            Assert.That(DestinationBossMarkerInsideFrame(boss), Is.True);
            NavalBattleVisualCapture.Capture(controller, "Port_Boss_Located_In_Fog.png",
                "定位首领保持全图固定，未探索迷雾中的首领带金色高亮与点击提示", "Navigation Chart");

            // A queued footer callback must respect the same modal protection
            // as chart clicks, even if it bypasses the normal raycast layer.
            Button.ButtonClickedEvent retainedPlot = ActiveButton("Plot Blocking Boss Course").onClick;
            ActiveButton("Voyage Cargo").onClick.Invoke();
            retainedPlot.Invoke();
            costs.AssertUnchanged(session);
            Assert.That(session.Heading, Is.EqualTo(previousHeading));
            Assert.That(session.HasDirectCourseTarget, Is.EqualTo(hadTarget));
            AssertDestinationTestPoint(session.DirectCourseTarget, previousTarget);
            ActiveButton("Close Voyage Cargo").onClick.Invoke();
            yield return null;

            yield return ClickDestinationTestGraphic(ActiveButton("Plot Blocking Boss Course").gameObject, Vector2.zero);
            AssertDestinationBossCourseOnly(session, boss, costs);
            Assert.That(DestinationBossMarkerInsideFrame(boss), Is.True);
            NavalBattleVisualCapture.Capture(controller, "Port_Boss_Course_Plotted.png",
                "玩家主动规划朝首领的本段航线，尚未执行航行或触发战斗", "Navigation Chart");

            session.SetHeading(SailingDirection.North);
            RenderDestinationBossFixture(controller);
            yield return null;
            yield return ClickDestinationTestGraphic(ActiveButton("Event Node " + boss.Id).gameObject, Vector2.zero);
            AssertDestinationBossCourseOnly(session, boss, costs);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestinationBossGuidance_BlockadeLiftRestoresPortAndRemovesPrompt()
        {
            yield return LoadDestinationBossGuidanceFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            CoreMapNode boss = session.Map.Nodes.Single();
            Color originalPort = GameObject.Find("Destination Port").GetComponent<Graphic>().color;
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(GameObject.Find("Destination Boss Guidance"), Is.Not.Null);
            var costs = new NavigationArrivalCosts(session);
            // The Core battle suite covers victory. Here exercise the UI's next
            // refresh with that authoritative blockade state already resolved.
            SetDestinationTestField(session, "bossDefeated", true);
            boss.Visited = true;
            RenderDestinationBossFixture(controller);
            yield return null;
            Assert.That(session.IsDestinationBossGuidanceActive, Is.False);
            Assert.That(session.DestinationPortDiscovered, Is.True);
            Assert.That(GameObject.Find("Destination Boss Guidance"), Is.Null);
            Assert.That(GameObject.Find("Destination Boss Guidance Halo"), Is.Null);
            Assert.That(GameObject.Find("Destination Boss Guidance Ring"), Is.Null);
            Assert.That(GameObject.Find("Destination Boss Guidance Caption"), Is.Null);
            Assert.That(GameObject.Find("Destination Port").GetComponent<Graphic>().color, Is.EqualTo(originalPort));
            Assert.That(GameObject.Find("Destination Port Guidance Label").GetComponent<Text>().text, Does.Not.Contain("封锁"));
            costs.AssertUnchanged(session);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            NavalBattleVisualCapture.Capture(controller, "Port_Boss_Blockade_Lifted.png",
                "首领击败后目的港恢复亮度、封锁提示和首领高亮消失", "Navigation Chart");
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadDestinationBossGuidanceFixture()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.SetSeed("PORT-BOSS-GUIDANCE-PRESENTATION");
            session.Cargo.Water = session.Cargo.Food = 200;
            session.StartVoyage();
            CoreMapNode boss = session.Map.Nodes.Single(node => node.IsBoss && node.Encounter.BossKind == CoreBossKind.Route);
            session.Map.Nodes.Clear();
            session.Map.Nodes.Add(boss);
            boss.Position = new VoyagePoint(session.Map.WorldWidth * .45f, session.Map.WorldHeight * .12f);
            boss.Discovered = boss.IntelRevealed = boss.Visited = false;
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.ExploredCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.VoyageExploredCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.ChartExploredCells, 0, session.Map.ChartExploredCells.Length);
            SetDestinationTestPosition(session, new VoyagePoint(session.Map.Destination.X - 1f, session.Map.Destination.Y));
            SetDestinationTestField(session, "randomAmbushOccurred", true);
            session.SetCourseTo(session.Map.Destination);
            RenderDestinationBossFixture(controller);
            yield return new WaitForSecondsRealtime(.4f);
            yield return null;
        }

        private static void RenderDestinationBossFixture(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

        private static void AssertDestinationBossCourseOnly(CoreVoyageSession session, CoreMapNode boss, NavigationArrivalCosts costs)
        {
            costs.AssertUnchanged(session);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertDestinationTestPoint(session.DirectCourseTarget, boss.Position);
            Assert.That(session.GetForecast().ActualDistanceNm, Is.LessThanOrEqualTo(session.MaximumDistanceNm + .01f));
            Assert.That(GameObject.Find("Distance Label").GetComponent<Text>().text, Does.Contain("锁定："));
            Assert.That(GameObject.Find("Distance Label").GetComponent<Text>().text, Does.Contain(boss.Encounter.Name));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.ActiveEncounter, Is.Null);
        }

        private static bool DestinationBossMarkerInsideFrame(CoreMapNode boss)
        {
            RectTransform frame = GameObject.Find("Procedural Sea Chart Frame").GetComponent<RectTransform>();
            RectTransform marker = GameObject.Find("Event Node " + boss.Id).GetComponent<RectTransform>();
            Vector2 center = frame.InverseTransformPoint(marker.TransformPoint(marker.rect.center));
            return frame.rect.Contains(center);
        }

        private static Rect DestinationBossWorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
