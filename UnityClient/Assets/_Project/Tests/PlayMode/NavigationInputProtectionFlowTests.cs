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
        public IEnumerator NavigationInputProtection_MotionLocksExistingCallbacksThenRestoresLiveControls()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.SetSeed("NAVIGATION-INPUT-PROTECTION");
            session.Cargo.Water = session.Cargo.Food = 200;
            session.StartVoyage();
            // Keep actual sailing costs and animation, but remove random encounters
            // and coast collisions from this input-lock regression.
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(30f, 32f));
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, true);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(20);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            yield return null; yield return null;

            FieldInfo motion = typeof(CoreGameplayDemoController).GetField("navigationMotionPlaying",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Button south = ActiveButton("Heading South");
            Slider distance = GameObject.Find("Distance Slider").GetComponent<Slider>();
            Button back = ActiveButton("Safe Return");
            Button sail = ActiveButton("Execute Sail");
            Button.ButtonClickedEvent retiredHeading = south.onClick;
            Slider.SliderEvent retiredDistance = distance.onValueChanged;
            Button.ButtonClickedEvent retiredReturn = back.onClick;
            GameObject map = GameObject.Find("Map Layer");
            GameObject ship = GameObject.Find("Player Ship");

            sail.onClick.Invoke();
            Assert.That((bool)motion.GetValue(controller), Is.True, "Exercise a real in-flight animation.");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.TotalDistanceNm, Is.GreaterThan(0f));
            float committedDistance = session.TotalDistanceNm;
            double committedHours = session.TotalHours;
            int committedWater = session.Cargo.Water, committedFood = session.Cargo.Food;
            VoyagePoint committedPosition = session.Position;

            // A raycast-blocking shade does not suppress keyboard navigation or
            // already queued callbacks on the controls beneath it.
            south.onClick.Invoke();
            distance.value = 170f;
            back.onClick.Invoke();
            sail.onClick.Invoke();
            Assert.That(session.Heading, Is.EqualTo(SailingDirection.East));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(20));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.TotalDistanceNm, Is.EqualTo(committedDistance));
            Assert.That(session.TotalHours, Is.EqualTo(committedHours));
            Assert.That(session.Cargo.Water, Is.EqualTo(committedWater));
            Assert.That(session.Cargo.Food, Is.EqualTo(committedFood));
            Assert.That(session.Position.X, Is.EqualTo(committedPosition.X));
            Assert.That(session.Position.Y, Is.EqualTo(committedPosition.Y));
            Assert.That(GameObject.Find("Map Layer"), Is.SameAs(map), "Input must not replace the animated map.");
            Assert.That(GameObject.Find("Player Ship"), Is.SameAs(ship));
            Assert.That((bool)motion.GetValue(controller), Is.True);

            ActiveButton("Skip Voyage Motion").onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((bool)motion.GetValue(controller) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That((bool)motion.GetValue(controller), Is.False);
            yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>().value, Is.EqualTo(20f));

            retiredHeading.Invoke();
            retiredDistance.Invoke(170f);
            retiredReturn.Invoke();
            Assert.That(session.Heading, Is.EqualTo(SailingDirection.East));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(20));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing), "Retired controls cannot mutate the new screen.");

            ActiveButton("Heading South").onClick.Invoke();
            Assert.That(session.Heading, Is.EqualTo(SailingDirection.South));
            yield return null;
            GameObject.Find("Distance Slider").GetComponent<Slider>().value = 170f;
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(170));
            ActiveButton("Safe Return").onClick.Invoke();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.SafeReturn));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
