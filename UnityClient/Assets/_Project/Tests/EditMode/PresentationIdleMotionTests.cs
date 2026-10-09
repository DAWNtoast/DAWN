using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class PresentationIdleMotionTests
    {
        [Test]
        public void IdleButton_DoesNotDirtyItsTransformOnEveryFrame()
        {
            var host = new GameObject("Idle Button Audit", typeof(RectTransform), typeof(Button));
            try
            {
                RectTransform rect = (RectTransform)host.transform;
                rect.localScale = new Vector3(1.5f, 1.2f, 1f);
                var motion = host.AddComponent<NauticalButtonMotion>();
                // EditMode does not run lifecycle callbacks for this runtime
                // component; invoke the same initialization explicitly.
                Invoke(motion, "Awake");
                Invoke(motion, "OnEnable");
                rect.hasChanged = false;
                for (int frame = 0; frame < 100; frame++) Invoke(motion, "Update");
                Assert.That(rect.hasChanged, Is.False, "Idle button motion must not invalidate its transform each frame.");
                Assert.That(rect.localScale, Is.EqualTo(new Vector3(1.5f, 1.2f, 1f)));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ConvergedButton_SnapsExactlyOnceAndPreservesHoverAndDisableTargets()
        {
            var host = new GameObject("Converged Button Audit", typeof(RectTransform), typeof(Button));
            try
            {
                RectTransform rect = (RectTransform)host.transform;
                var motion = host.AddComponent<NauticalButtonMotion>();
                Invoke(motion, "Awake");
                Invoke(motion, "OnEnable");
                motion.OnPointerEnter(null);
                Vector3 hover = Vector3.one * 1.028f;
                rect.localScale = hover + Vector3.one * 0.000001f;
                Invoke(motion, "Update");
                Assert.That(rect.localScale.Equals(hover), Is.True, "Converged feedback must snap to the original hover scale.");
                rect.hasChanged = false;
                Invoke(motion, "Update");
                Assert.That(rect.hasChanged, Is.False);
                Invoke(motion, "OnDisable");
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static void Invoke(NauticalButtonMotion motion, string method) =>
            typeof(NauticalButtonMotion).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, null);
    }
}
