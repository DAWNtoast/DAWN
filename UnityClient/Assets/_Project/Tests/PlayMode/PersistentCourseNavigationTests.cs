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
        public IEnumerator PersistentCourse_EventClickSurvivesMultipleLegsAndRandomEventThenSwitchesOrCancels()
        {
            yield return LoadPersistentCourseFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            CoreMapNode first = session.Map.Nodes[0], second = session.Map.Nodes[1];
            var beforeSelection = new NavigationArrivalCosts(session);
            yield return ClickDestinationTestGraphic(ActiveButton("Event Node " + first.Id).gameObject, Vector2.zero);
            beforeSelection.AssertUnchanged(session);
            AssertPersistentCourseTarget(session, first);
            Assert.That(session.ActiveEncounter, Is.Null, "Selecting a distant event must not start it.");
            Slider slider = GameObject.Find("Distance Slider").GetComponent<Slider>();
            slider.value = 60;
            yield return null;
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(60));
            Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>(), Is.SameAs(slider));
            beforeSelection.AssertUnchanged(session);
            AssertPersistentCourseTarget(session, first);
            Assert.That(session.GetForecast().ActualDistanceNm, Is.EqualTo(60f).Within(.02f));
            Assert.That(GameObject.Find("Event Label " + first.Id), Is.Null);
            NavalBattleVisualCapture.Capture(controller, "Course_Lock_Selected_Event.png",
                "点击远方事件后锁定最终目标；调节滑条只改变本段航程", "Navigation Controls");

            for (int leg = 0; leg < 2; leg++)
            {
                float previousDistance = session.TotalDistanceNm;
                float previousRemaining = PersistentCourseDistance(session.Position, first.Position);
                ActiveButton("Execute Sail").onClick.Invoke();
                yield return WaitForNavigationMotionCompletion(controller);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(session.TotalDistanceNm - previousDistance, Is.EqualTo(60f).Within(.02f));
                Assert.That(PersistentCourseDistance(session.Position, first.Position), Is.LessThan(previousRemaining));
                AssertPersistentCourseTarget(session, first);
            }
            NavalBattleVisualCapture.Capture(controller, "Course_Lock_After_Two_Legs.png",
                "连续两次实际执行航行后仍锁定同一事件，下一段继续朝目标前进", "Navigation Controls");

            // Schedule a real existing random encounter on the next leg. This
            // controls timing only; sailing and resolving its choice use live UI.
            CoreEncounter tailwind = (CoreEncounter)typeof(CoreVoyageSession)
                .GetMethod("CreateSeaChartRandomEncounter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { "random_tailwind" });
            SetPersistentCourseField(session, "chartRandomEncounters", new[] { tailwind });
            SetPersistentCourseField(session, "chartRandomOrder", new[] { "random_tailwind" });
            SetPersistentCourseField(session, "chartRandomIndex", 0);
            SetPersistentCourseField(session, "nextChartRandomDistance", session.TotalDistanceNm + 15f);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo("random_tailwind"));
            AssertPersistentCourseTarget(session, first);
            var interruptedCosts = new NavigationArrivalCosts(session);
            yield return WaitForEncounterDossierPresentationCompletion();
            ActiveButton("Encounter Choice 0").onClick.Invoke();
            yield return null;
            interruptedCosts.AssertUnchanged(session);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            AssertPersistentCourseTarget(session, first);
            SetPersistentCourseField(session, "nextChartRandomDistance", 100000f);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            AssertPersistentCourseTarget(session, first);
            NavalBattleVisualCapture.Capture(controller, "Course_Lock_After_Random_Encounter.png",
                "处理途中随机事件后无需重选目标，执行航行仍沿原目标继续", "Navigation Controls");

            var beforeSwitch = new NavigationArrivalCosts(session);
            yield return ClickDestinationTestGraphic(ActiveButton("Event Node " + second.Id).gameObject, Vector2.zero);
            beforeSwitch.AssertUnchanged(session);
            AssertPersistentCourseTarget(session, second);
            AssertInterruptedDistanceSelection(session, 60);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(60));
            NavalBattleVisualCapture.Capture(controller, "Course_Lock_Switched_Event.png",
                "点击另一个事件切换最终锁定目标，滑条与手动航程仍保持 60 海里，不自动航行", "Navigation Controls");
            Button south = ActiveButton("Heading South");
            yield return ClickDestinationTestGraphic(south.gameObject,
                Vector2.down * (south.GetComponent<RectTransform>().rect.width * .335f));
            beforeSwitch.AssertUnchanged(session);
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(session.Heading, Is.EqualTo(SailingDirection.South));
            AssertInterruptedDistanceSelection(session, 60);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(60));
            Assert.That(GameObject.Find("Distance Label").GetComponent<Text>().text, Does.Contain("计划航程"));
            Assert.That(GameObject.Find("Forecast").GetComponent<Text>().text, Does.Not.Contain("取消锁定"));
            NavalBattleVisualCapture.Capture(controller, "Course_Lock_Manual_Heading_Cancels.png",
                "手动选择航向后解除锁定，继续保留手动设定的 60 海里航程", "Navigation Controls");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PersistentCourse_IntermediateFixedEventKeepsTargetAndDestinationArrivalReleasesIt()
        {
            yield return LoadPersistentCourseFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            CoreMapNode destination = session.Map.Nodes[0], intermediate = session.Map.Nodes[1];
            destination.Position = new VoyagePoint(38f, 32f);
            intermediate.Position = new VoyagePoint(34f, 32f);
            destination.Encounter = PersistentCourseEncounter("course-lock-destination", "锁定终点");
            intermediate.Encounter = PersistentCourseEncounter("course-lock-intermediate", "途中检查");
            RenderDestinationBossFixture(controller);
            yield return null;
            yield return ClickDestinationTestGraphic(ActiveButton("Event Node " + destination.Id).gameObject, Vector2.zero);
            AssertPersistentCourseTarget(session, destination);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.ActiveEncounter, Is.SameAs(intermediate.Encounter));
            AssertPersistentCourseTarget(session, destination);
            yield return WaitForEncounterDossierPresentationCompletion();
            var intermediateCosts = new NavigationArrivalCosts(session);
            ActiveButton("Encounter Choice 0").onClick.Invoke();
            yield return null;
            intermediateCosts.AssertUnchanged(session);
            AssertPersistentCourseTarget(session, destination);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.ActiveEncounter, Is.SameAs(destination.Encounter));
            yield return WaitForEncounterDossierPresentationCompletion();
            ActiveButton("Encounter Choice 0").onClick.Invoke();
            yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(destination.Visited, Is.True);
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(session.UsesDefaultDistance, Is.True);
            Assert.That(GameObject.Find("Distance Label").GetComponent<Text>().text, Does.Contain("半日航程"));
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadPersistentCourseFixture()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            Assert.That(session.TrySelectChartSeed("SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6", out string reason),
                Is.True, reason);
            session.Cargo.Water = session.Cargo.Food = 100;
            session.StartVoyage();
            Assert.That(session.CurrentChartSeed, Is.Not.Empty,
                "A selected sea chart is required for the production random-encounter queue.");
            Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue),
                "The fixture must preserve enough spare capacity to resolve normal encounter choices.");
            CoreMapNode[] nodes = session.Map.Nodes.Where(node => !node.IsBoss && node.Encounter.HasChartLocation &&
                node.Encounter.BossKind == CoreBossKind.None).Take(2).ToArray();
            Assert.That(nodes.Length, Is.EqualTo(2));
            session.Map.Nodes.Clear();
            session.Map.Nodes.AddRange(nodes);
            nodes[0].Position = new VoyagePoint(70f, 32f);
            nodes[1].Position = new VoyagePoint(30f, 55f);
            foreach (CoreMapNode node in nodes) { node.Discovered = true; node.Visited = node.Skipped = false; }
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            SetDestinationTestPosition(session, new VoyagePoint(30f, 32f));
            SetDestinationTestField(session, "randomAmbushOccurred", true);
            SetPersistentCourseField(session, "nextChartRandomDistance", 100000f);
            session.SetHeading(SailingDirection.East);
            RenderDestinationBossFixture(controller);
            yield return new WaitForSecondsRealtime(.4f);
            yield return null;
        }

        private static void AssertPersistentCourseTarget(CoreVoyageSession session, CoreMapNode node)
        {
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertDestinationTestPoint(session.DirectCourseTarget, node.Position);
            Assert.That(session.GetForecast().ActualDistanceNm, Is.LessThanOrEqualTo(session.PlannedDistanceNm + .02f));
            Text label = GameObject.Find("Distance Label").GetComponent<Text>();
            Assert.That(label.text, Does.Contain("锁定："));
            Assert.That(label.text, Does.Contain(node.Encounter.Name));
            Assert.That(label.text, Does.Contain("本段"));
            Assert.That(GameObject.Find("Forecast").GetComponent<Text>().text, Does.Contain("手动改航向可取消锁定"));
        }

        private static CoreEncounter PersistentCourseEncounter(string id, string name) =>
            new CoreEncounter(id, name, "事件", "确认此处航点后继续航行。", false, false, 0, 0,
                new CoreEncounterChoice("继续", "不消耗时间与物资", 0d, new VoyageEffect()));

        private static float PersistentCourseDistance(VoyagePoint a, VoyagePoint b) =>
            Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));

        private static void SetPersistentCourseField(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);
    }
}
