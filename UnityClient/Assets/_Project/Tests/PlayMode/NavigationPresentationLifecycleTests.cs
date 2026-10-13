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
        public IEnumerator AudioLifecycle_GameplayHierarchyReactivationRestartsCurrentMusic()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            FieldInfo activeMusic = typeof(CoreGameplayDemoController).GetField("activeMusicSource",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var source = (AudioSource)activeMusic.GetValue(controller);
            Assert.That(source, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!source.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(source.isPlaying, Is.True, "The fixture must start with live music.");
            AudioClip clip = source.clip;
            string key = controller.ActiveMusicKey;

            controller.gameObject.SetActive(false);
            yield return null;
            Assert.That(source.isPlaying, Is.False, "Hierarchy suspension must stop its AudioSource.");
            controller.gameObject.SetActive(true);
            deadline = Time.realtimeSinceStartup + 2f;
            while (!source.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(activeMusic.GetValue(controller), Is.SameAs(source));
            Assert.That(source.clip, Is.SameAs(clip));
            Assert.That(controller.ActiveMusicKey, Is.EqualTo(key));
            Assert.That(source.isPlaying, Is.True,
                "The real controller OnEnable must recover music after Unity reactivates its child sources.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationLifecycle_InactiveGameObjectRecoversWithoutRepeatingTheCommittedStep()
        {
            yield return VerifyNavigationSuspensionRecovery(true);
        }

        [UnityTest]
        public IEnumerator NavigationLifecycle_DisabledControllerCancelsMotionUntilReenabled()
        {
            yield return VerifyNavigationSuspensionRecovery(false);
        }

        private IEnumerator VerifyNavigationSuspensionRecovery(bool disableGameObject)
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.SetSeed("NAVIGATION-LIFECYCLE-AUDIT");
            session.Cargo.Water = session.Cargo.Food = 200;
            session.StartVoyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(30f, 32f));
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, true);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(20);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            yield return null;
            yield return null;

            FieldInfo motion = typeof(CoreGameplayDemoController).GetField("navigationMotionPlaying",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ActiveButton("Execute Sail").onClick.Invoke();
            Assert.That((bool)motion.GetValue(controller), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            double hours = session.TotalHours;
            float distance = session.TotalDistanceNm;
            int water = session.Cargo.Water, food = session.Cargo.Food;
            VoyagePoint position = session.Position;
            Canvas canvas = controller.transform.Find("Core Gameplay Canvas").GetComponent<Canvas>();
            Transform oldMap = FixedGameViewport.Bounds(canvas).Find("Phase Content/Navigation Chart/Procedural Sea Chart Frame/Map Layer");
            Assert.That(oldMap, Is.Not.Null);

            if (disableGameObject) controller.gameObject.SetActive(false);
            else controller.enabled = false;
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That((bool)motion.GetValue(controller), Is.False, "Suspension must release the navigation lock.");
            Assert.That(oldMap != null, Is.True, "Disabled controllers must not finish their coroutine and rebuild the screen.");
            if (disableGameObject) controller.gameObject.SetActive(true);
            else controller.enabled = true;
            yield return null;
            yield return null;

            Assert.That((bool)motion.GetValue(controller), Is.False);
            Assert.That(session.TotalHours, Is.EqualTo(hours));
            Assert.That(session.TotalDistanceNm, Is.EqualTo(distance));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
            Assert.That(session.Position.X, Is.EqualTo(position.X));
            Assert.That(session.Position.Y, Is.EqualTo(position.Y));
            Assert.That(GameObject.Find("Navigation Motion Overlay"), Is.Null);
            Assert.That(GameObject.Find("Navigation Motion Input Guard"), Is.Null);
            Assert.That(ActiveButton("Execute Sail").interactable, Is.True);
            Assert.That(oldMap == null, Is.True, "Recovery must reconcile the displayed ship with the committed position.");
            ActiveButton("Heading South").onClick.Invoke();
            Assert.That(session.Heading, Is.EqualTo(SailingDirection.South));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationLifecycle_DisablingDuringScreenFadeRestoresFullOpacity()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            var group = (CanvasGroup)typeof(CoreGameplayDemoController).GetField("transitionCanvasGroup",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(group, Is.Not.Null);
            // Scene construction can make the previous batch-mode frame longer
            // than the normal 0.24-second reveal. Start the production coroutine
            // with a duration guaranteed to exceed this frame's first step.
            FieldInfo transition = typeof(CoreGameplayDemoController).GetField("transitionRoutine",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var previous = (Coroutine)transition.GetValue(controller);
            if (previous != null) controller.StopCoroutine(previous);
            var reveal = (IEnumerator)typeof(CoreGameplayDemoController).GetMethod("RevealScreen",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, new object[] { Time.unscaledDeltaTime + 1f });
            transition.SetValue(controller, controller.StartCoroutine(reveal));
            Assert.That(group.alpha, Is.LessThan(1f), "Exercise an unfinished transition.");
            controller.gameObject.SetActive(false);
            yield return null;
            controller.gameObject.SetActive(true);
            yield return null;
            Assert.That(group.alpha, Is.EqualTo(1f), "A cancelled fade must not leave the screen permanently transparent.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
