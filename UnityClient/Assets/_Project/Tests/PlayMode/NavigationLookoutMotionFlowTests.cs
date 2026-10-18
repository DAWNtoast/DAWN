using System;
using System.Collections;
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
        public IEnumerator NavigationLookoutMotion_RealVoyageMovesSceneryWithChartAndKeepsCaptionsStill()
        {
            yield return LoadLookoutMotionFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var motion = Object.FindFirstObjectByType<NavigationLookoutMotion>();
            var view = motion.transform.parent as RectTransform;
            var ship = GameObject.Find("Player Ship").GetComponent<RectTransform>();
            var vessel = view.Find("Lookout event-992").GetComponent<RawImage>();
            var heading = view.Find("Lookout Heading") as RectTransform;
            var report = view.Find("Lookout Report") as RectTransform;
            Vector2 startShip = ship.anchoredPosition, startScenery = vessel.rectTransform.anchoredPosition;
            Vector2 startSize = vessel.rectTransform.sizeDelta;
            Vector2 headingPosition = heading.anchoredPosition, reportPosition = report.anchoredPosition;
            Rect originalUv = vessel.uvRect;
            Material originalMaterial = vessel.material;
            float originalAspect = startSize.x / startSize.y;
            NavalBattleVisualCapture.Capture(controller, "Lookout_Motion_01_Start.png", "真实航行前的瞭望窗和船标", "Forward View Panel");
            ActiveButton("Execute Sail").onClick.Invoke();
            Assert.That(motion.VoyagePrepared, Is.True);
            var committed = new NavigationArrivalCosts(controller.Session);
            double committedClock = controller.Session.ElapsedGameHours;
            bool moved = false, sampled = false;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (motion != null && NavigationMotionIsPlaying(controller) && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(heading.anchoredPosition, Is.EqualTo(headingPosition));
                Assert.That(report.anchoredPosition, Is.EqualTo(reportPosition));
                Assert.That(vessel.uvRect, Is.EqualTo(originalUv), "The atlas cell must not pan into a neighboring ship.");
                Assert.That(vessel.material, Is.SameAs(originalMaterial), "Keep the existing ship cutout material.");
                Assert.That(vessel.rectTransform.sizeDelta.x / vessel.rectTransform.sizeDelta.y, Is.EqualTo(originalAspect).Within(.0001f));
                CanvasGroup fade = (CanvasGroup)typeof(CoreGameplayDemoController).GetField("transitionCanvasGroup",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                Assert.That(fade == null || Mathf.Approximately(fade.alpha, 1f), Is.True, "A sailing step must not fade the whole dashboard.");
                committed.AssertUnchanged(controller.Session);
                Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(committedClock));
                moved |= Vector2.Distance(ship.anchoredPosition, startShip) > .1f &&
                    Vector2.Distance(vessel.rectTransform.anchoredPosition, startScenery) > .1f &&
                    Vector2.Distance(vessel.rectTransform.sizeDelta, startSize) > .1f;
                if (!sampled && motion.VoyageProgress >= .6f && motion.VoyageProgress < .95f)
                {
                    sampled = true;
                    NavalBattleVisualCapture.Capture(controller, "Lookout_Motion_02_Weather_Transition.png",
                        "航行中岛礁和船只接近，天空天气渐变，标题与报告固定", "Forward View Panel");
                }
                yield return null;
            }
            Assert.That(NavigationMotionIsPlaying(controller), Is.False);
            Assert.That(moved, Is.True, "The real chart marker and projected ship must both move and the approaching ship must grow.");
            Assert.That(sampled, Is.True);
            committed.AssertUnchanged(controller.Session);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            yield return null;
            NavalBattleVisualCapture.Capture(controller, "Lookout_Motion_03_End.png", "航行结束后的实际天气、昼夜与瞭望位置", "Forward View Panel");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationLookoutMotion_PauseAndResumeReconcileTheCommittedVoyageOnce()
        {
            yield return LoadLookoutMotionFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var motion = Object.FindFirstObjectByType<NavigationLookoutMotion>();
            ActiveButton("Execute Sail").onClick.Invoke();
            Assert.That(NavigationMotionIsPlaying(controller), Is.True);
            var committed = new NavigationArrivalCosts(controller.Session);
            double committedClock = controller.Session.ElapsedGameHours;
            controller.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            motion.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
            Assert.That(NavigationMotionIsPlaying(controller), Is.False);
            Assert.That(motion.VoyageProgress, Is.EqualTo(1f));
            var clockField = typeof(NavigationLookoutMotion).GetField("clock", BindingFlags.Instance | BindingFlags.NonPublic);
            float stoppedClock = (float)clockField.GetValue(motion);
            yield return null;
            yield return null;
            Assert.That((float)clockField.GetValue(motion), Is.EqualTo(stoppedClock));
            committed.AssertUnchanged(controller.Session);
            controller.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            yield return null;
            yield return null;
            Assert.That(motion == null, Is.True, "Resume replaces the old sampled scene with the committed endpoint.");
            Assert.That(Object.FindFirstObjectByType<NavigationLookoutMotion>(), Is.Not.Null);
            Assert.That(ActiveButton("Execute Sail").IsInteractable(), Is.True);
            committed.AssertUnchanged(controller.Session);
            Assert.That(controller.Session.ElapsedGameHours, Is.EqualTo(committedClock));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationLookoutMotion_EncounterRetiresEnvironmentalMotionWithoutFadingChart()
        {
            yield return LoadNavigationArrivalFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var session = controller.Session;
            var encounter = new CoreEncounter("convoy-lookout-arrival", "瞭望抵达", "事件", "抵达后打开航海日志。", true, false, 0, 0,
                new CoreEncounterChoice("继续", "无需消耗", 0d, new VoyageEffect()));
            session.Map.Nodes.Add(new CoreMapNode { Id = 993, Position = session.GetForecast().Target, Encounter = encounter,
                Discovered = true, TerrainConstraint = EventTerrainConstraint.OpenSea });
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            yield return null;
            var motion = Object.FindFirstObjectByType<NavigationLookoutMotion>();
            ActiveButton("Execute Sail").onClick.Invoke();
            var committed = new NavigationArrivalCosts(session);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (NavigationMotionIsPlaying(controller) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(motion == null, Is.True);
            Assert.That(Object.FindFirstObjectByType<NavigationLookoutMotion>(), Is.Null, "Event paper must not retain an invisible background animator.");
            CanvasGroup fade = (CanvasGroup)typeof(CoreGameplayDemoController).GetField("transitionCanvasGroup",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(fade == null || Mathf.Approximately(fade.alpha, 1f), Is.True);
            committed.AssertUnchanged(session);
            NavalBattleVisualCapture.Capture(controller, "Lookout_Motion_04_Encounter.png", "瞭望窗切换为事件纸，海图保持不透明", "Forward View Panel");
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadLookoutMotionFixture()
        {
            yield return LoadNavigationArrivalFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            CoreVoyageMap map = session.Map;
            int weatherRow = map.PointToCell(session.Position).Row;
            // Place the route just inside the south edge of one weather row. The ship
            // enters that row's storm, while the visible vessel to the south has a clear
            // line of sight instead of being hidden behind several storm cells.
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session,
                new VoyagePoint(session.Position.X, weatherRow * map.CellHeight + .05f));
            session.SetDistance(60);
            typeof(CoreVoyageSession).GetProperty("ElapsedGameHours").SetValue(session, 8.5d);
            SailingForecast forecast = session.GetForecast();
            float midX = (session.Position.X + forecast.Target.X) * .5f;
            for (int i = 0; i < map.CellCount; i++)
            {
                VoyagePoint cell = map.CellCenter(i % map.Columns, i / map.Columns);
                if (cell.X > midX && i / map.Columns == weatherRow)
                    map.TerrainCells[i] = SeaTerrainType.StormFront;
            }
            // This route is 27 nm per map unit: adding three whole cells put the old
            // fixture beyond the 120 nm sight range. A 1.5-unit lateral offset stays
            // outside the real 1.25-unit encounter radius, yet inside the 55-degree view
            // cone both before and after the 60 nm voyage.
            const float side = 1.5f;
            var vesselPosition = new VoyagePoint(forecast.Target.X + side, forecast.Target.Y - side);
            session.Map.Nodes.Add(new CoreMapNode { Id = 992, Position = vesselPosition,
                Encounter = new CoreEncounter("convoy-lookout-motion", "远处商船", "事件", "远处商船正在航行。", false, false, 0, 0),
                Discovered = true, TerrainConstraint = EventTerrainConstraint.OpenSea });
            SetLookoutTerrain(map, new VoyagePoint(forecast.Target.X + map.CellWidth * 4f,
                forecast.Target.Y + map.CellHeight * 2f), SeaTerrainType.Island);
            var observations = VoyageLookoutProjector.Project(map, session.Position, session.Heading,
                120f + (session.HasNavigator ? 60f : 0f) + session.VisionBonusNm);
            bool projectedVessel = false;
            foreach (LookoutObservation item in observations) projectedVessel |= item.Key == "event-992";
            Assert.That(projectedVessel, Is.True, "Fixture vessel must pass real sight-range, occlusion and field-of-view rules before rendering.");
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            yield return null;
            yield return null;
            Assert.That(GameObject.Find("Lookout event-992"), Is.Not.Null, "Fixture must start with a visible, authored ship atlas cell.");
        }

        private static void SetLookoutTerrain(CoreVoyageMap map, VoyagePoint point, SeaTerrainType terrain)
        {
            int nearest = 0;
            float best = float.PositiveInfinity;
            for (int i = 0; i < map.CellCount; i++)
            {
                VoyagePoint center = map.CellCenter(i % map.Columns, i / map.Columns);
                float distance = (center.X - point.X) * (center.X - point.X) + (center.Y - point.Y) * (center.Y - point.Y);
                if (distance < best) { best = distance; nearest = i; }
            }
            map.TerrainCells[nearest] = terrain;
        }
    }
}
