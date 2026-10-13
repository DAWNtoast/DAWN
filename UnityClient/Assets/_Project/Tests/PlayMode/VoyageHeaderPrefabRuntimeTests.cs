using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator VoyageHeader_ReusesTimelineAndControlsAndReleasesInactiveArt()
        {
            yield return LoadProvisioningFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            controller.Session.Cargo.Water = controller.Session.Cargo.Food = 200;
            controller.Session.StartVoyage();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            RenderVoyageHeaderTest(controller);
            yield return null;
            VoyageHeaderView view = GameObject.Find("Voyage Header").GetComponent<VoyageHeaderView>();
            Assert.That(view, Is.Not.Null, "Sailing must use the authored header prefab.");
            int hierarchyCount = view.GetComponentsInChildren<Transform>(true).Length;
            int timelineId = view.Timeline.GetInstanceID();
            for (int index = 0; index < 8; index++)
            {
                RenderVoyageHeaderTest(controller);
                yield return null;
                Assert.That(GameObject.Find("Voyage Header").GetComponent<VoyageHeaderView>(), Is.SameAs(view));
                Assert.That(view.Timeline.GetInstanceID(), Is.EqualTo(timelineId));
                Assert.That(view.Timeline.ElapsedHours, Is.EqualTo(controller.Session.ElapsedGameHours).Within(.001d));
                Assert.That(view.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(hierarchyCount));
            }
            view.Settings.onClick.Invoke();
            Assert.That(GameObject.Find("Port Settings Overlay"), Is.Not.Null, "Rebinding must retain the settings action.");
            ActiveButton("Close Port Settings").onClick.Invoke();
            view.Cargo.onClick.Invoke();
            Assert.That(GameObject.Find("Voyage Cargo Overlay"), Is.Not.Null, "Rebinding must retain the cargo action.");
            RenderVoyageHeaderTest(controller);
            Assert.That(controller.Session.SafeReturn(), Is.True);
            controller.Session.ReturnToPortAfterSettlement();
            RenderVoyageHeaderTest(controller);
            yield return null;
            Assert.That(view.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.Frame.sprite, Is.Null);
            Assert.That(view.Compass.sprite, Is.Null);
            controller.Session.Cargo.Water = controller.Session.Cargo.Food = 200;
            controller.Session.StartVoyage();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            RenderVoyageHeaderTest(controller);
            yield return null;
            Assert.That(GameObject.Find("Voyage Header").GetComponent<VoyageHeaderView>(), Is.SameAs(view));
            Assert.That(view.Frame.sprite, Is.Not.Null);
            Assert.That(view.Compass.sprite, Is.Not.Null);
            Assert.That(view.Settings.interactable && view.Cargo.interactable, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private static void RenderVoyageHeaderTest(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }
}
