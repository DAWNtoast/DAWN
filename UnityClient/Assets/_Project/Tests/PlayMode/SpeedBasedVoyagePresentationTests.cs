using System;
using System.Collections;
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
        public IEnumerator SpeedBasedVoyage_DefaultHalfDayAndDayLimitRefreshWithHeadingAndShipSpeed()
        {
            yield return LoadSpeedBasedVoyageFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            var costs = new NavigationArrivalCosts(session);
            AssertSpeedBasedVoyageControls(session);
            Assert.That(session.UsesDefaultDistance, Is.True);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.DefaultDistanceNm));
            Assert.That(session.GetForecast().Hours, Is.EqualTo(12d).Within(.1d));
            Assert.That(GameObject.Find("Distance Label").GetComponent<Text>().text, Does.Contain("半日航程"));
            int eastMaximum = session.MaximumDistanceNm;
            NavalBattleVisualCapture.Capture(controller, "Speed_Course_Default_Half_Day.png",
                "默认半日航程按当前航速计算，滑条下显示全天最大海里数", "Navigation Controls");

            Button west = ActiveButton("Heading West");
            yield return ClickDestinationTestGraphic(west.gameObject,
                Vector2.left * (west.GetComponent<RectTransform>().rect.width * .335f));
            costs.AssertUnchanged(session);
            Assert.That(session.UsesDefaultDistance, Is.True, "Changing heading retains the half-day mode.");
            Assert.That(session.MaximumDistanceNm, Is.LessThan(eastMaximum), "Turning into the configured eastward wind reduces this day's range.");
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.DefaultDistanceNm));
            Assert.That(session.GetForecast().Hours, Is.EqualTo(12d).Within(.1d));
            AssertSpeedBasedVoyageControls(session);
            int againstWindMaximum = session.MaximumDistanceNm;
            SetSpeedBasedVoyageFixtureSpeed(session, 5f);
            RenderDestinationBossFixture(controller);
            yield return null;
            costs.AssertUnchanged(session);
            Assert.That(session.MaximumDistanceNm, Is.GreaterThan(againstWindMaximum));
            Assert.That(session.UsesDefaultDistance, Is.True, "Refreshing the slider must not act as a manual distance selection.");
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.DefaultDistanceNm));
            AssertSpeedBasedVoyageControls(session);
            NavalBattleVisualCapture.Capture(controller, "Speed_Course_Changed_Ship_Speed.png",
                "船速变化后半日默认值和全天上限同步更新，尚未执行航行", "Navigation Controls");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SpeedBasedVoyage_OneMileSliderDaySailAndSpeedClampPreserveLockedTarget()
        {
            yield return LoadSpeedBasedVoyageFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            var destination = new CoreMapNode
            {
                Id = 991, Position = new VoyagePoint(70f, 32f), Discovered = true,
                Encounter = PersistentCourseEncounter("speed-range-destination", "远方航点"),
                TerrainConstraint = EventTerrainConstraint.OpenSea
            };
            session.Map.Nodes.Add(destination);
            RenderDestinationBossFixture(controller);
            yield return null;
            var beforeSelection = new NavigationArrivalCosts(session);
            yield return ClickDestinationTestGraphic(ActiveButton("Event Node 991").gameObject, Vector2.zero);
            beforeSelection.AssertUnchanged(session);
            Assert.That(session.UsesDefaultDistance, Is.True);
            Slider slider = GameObject.Find("Distance Slider").GetComponent<Slider>();
            slider.value = 151f;
            yield return null;
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(151), "The slider no longer rounds to ten-mile steps.");
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(151));
            Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>(), Is.SameAs(slider));
            AssertDestinationTestPoint(session.DirectCourseTarget, destination.Position);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            beforeSelection.AssertUnchanged(session);
            int requestedMaximum = session.MaximumDistanceNm;
            slider.value = requestedMaximum;
            yield return null;
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(requestedMaximum));
            Assert.That(session.GetForecast().Hours, Is.LessThanOrEqualTo(24d + .0001d));
            Assert.That(session.GetForecast().Hours, Is.GreaterThan(23d));
            AssertSpeedBasedVoyageControls(session);
            NavalBattleVisualCapture.Capture(controller, "Speed_Course_Full_Day_Locked_Target.png",
                "锁定最终航点后可选择全天最大航程，本段时间不超过24小时", "Navigation Controls");
            double hoursBeforeSail = session.TotalHours;
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.TotalHours - hoursBeforeSail, Is.GreaterThan(23d).And.LessThanOrEqualTo(24d + .0001d));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.ActiveEncounter, Is.Null);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertDestinationTestPoint(session.DirectCourseTarget, destination.Position);
            var afterSail = new NavigationArrivalCosts(session);

            SetSpeedBasedVoyageFixtureSpeed(session, -8f);
            RenderDestinationBossFixture(controller);
            yield return null;
            afterSail.AssertUnchanged(session);
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.MaximumDistanceNm, Is.LessThan(requestedMaximum));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.MaximumDistanceNm));
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(requestedMaximum), "Slower conditions must not change the selected distance.");
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertDestinationTestPoint(session.DirectCourseTarget, destination.Position);
            AssertSpeedBasedVoyageControls(session);
            Assert.That(GameObject.Find("Distance Label").GetComponent<Text>().text,
                Does.Contain("设定 " + requestedMaximum + " 海里").And.Contain("本段 "));
            NavalBattleVisualCapture.Capture(controller, "Speed_Course_Slower_Ship_Clamps_Leg.png",
                "船速降低时滑条保留设定距离，单独显示24小时内本段上限，锁定航点不变", "Navigation Controls");

            SetSpeedBasedVoyageFixtureSpeed(session, 8f);
            RenderDestinationBossFixture(controller);
            yield return null;
            afterSail.AssertUnchanged(session);
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(requestedMaximum), "Recovering speed restores the player's requested nautical miles.");
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(requestedMaximum));
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertDestinationTestPoint(session.DirectCourseTarget, destination.Position);
            AssertSpeedBasedVoyageControls(session);

            // A player may explicitly replace the preserved selection with the
            // current limit. Its equality to PlannedDistanceNm is not a no-op.
            SetSpeedBasedVoyageFixtureSpeed(session, -8f);
            session.SetHeading(SailingDirection.East);
            RenderDestinationBossFixture(controller);
            yield return null;
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(GameObject.Find("Distance Label").GetComponent<Text>().text,
                Is.EqualTo("计划航程　" + requestedMaximum + " 海里"));
            AssertSpeedBasedVoyageControls(session);
            int reducedMaximum = session.MaximumDistanceNm;
            GameObject.Find("Distance Slider").GetComponent<Slider>().value = reducedMaximum;
            yield return null;
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(reducedMaximum), "The user can choose the current limit while a larger selection is retained.");
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(reducedMaximum));
            afterSail.AssertUnchanged(session);
            AssertSpeedBasedVoyageControls(session);
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadSpeedBasedVoyageFixture()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.SetSeed("SPEED-BASED-VOYAGE-PRESENTATION");
            session.Cargo.Water = session.Cargo.Food = 100;
            session.StartVoyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentVectors, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentStrengths, 0, session.Map.CellCount);
            session.Map.GlobalWindDirection = SailingDirection.East;
            session.Map.GlobalWindVector = new VoyagePoint(1f, 0f);
            session.Map.GlobalWindStrength = 4f;
            SetDestinationTestPosition(session, new VoyagePoint(30f, 32f));
            SetDestinationTestField(session, "randomAmbushOccurred", true);
            session.SetHeading(SailingDirection.East);
            RenderDestinationBossFixture(controller);
            yield return new WaitForSecondsRealtime(.4f);
            yield return null;
        }

        private static void SetSpeedBasedVoyageFixtureSpeed(CoreVoyageSession session, float speedBonus)
        {
            // This fixture controls the effective ship speed to exercise the range UI.
            // The public refit API correctly rejects loadout changes while sailing;
            // temporary context bonuses are also recalculated by Render.
            typeof(CoreVoyageSession).GetField("componentSpeedBonus",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(session, speedBonus);
        }

        private static void AssertSpeedBasedVoyageControls(CoreVoyageSession session)
        {
            Slider slider = GameObject.Find("Distance Slider").GetComponent<Slider>();
            Assert.That(slider.minValue, Is.EqualTo(CoreVoyageSession.MinimumDistanceNm));
            Assert.That(slider.maxValue, Is.EqualTo(Mathf.Max(session.MaximumDistanceNm, session.RequestedDistanceNm)));
            Assert.That(slider.value, Is.EqualTo(session.RequestedDistanceNm));
            Assert.That(slider.wholeNumbers, Is.True);
            Text limit = GameObject.Find("Distance Limit").GetComponent<Text>();
            string limitPrefix = session.RequestedDistanceNm > session.MaximumDistanceNm ? "本段上限 " : "最大 ";
            Assert.That(limit.text, Is.EqualTo(limitPrefix + session.MaximumDistanceNm + " 海里 · 24 小时"));
            Canvas.ForceUpdateCanvases();
            Assert.That(limit.cachedTextGenerator.lineCount, Is.EqualTo(1), "The day's limit stays on one readable line.");
            Rect bounds = DestinationBossWorldRect(limit.rectTransform);
            Assert.That(bounds.Overlaps(DestinationBossWorldRect(slider.GetComponent<RectTransform>())), Is.False);
            Assert.That(bounds.Overlaps(DestinationBossWorldRect(GameObject.Find("Forecast").GetComponent<RectTransform>())), Is.False);
        }
    }
}
