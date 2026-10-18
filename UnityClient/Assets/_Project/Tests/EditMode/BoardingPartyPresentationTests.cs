using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BoardingPartyPresentationTests
    {
        [Test]
        public void GroundedStep_IsMirroredBoundedAndReturnsToItsDeckPosition()
        {
            Assert.That(BoardingPartyPresentation.LungeOffset(0f, 1f), Is.EqualTo(Vector2.zero));
            Assert.That(BoardingPartyPresentation.LungeOffset(1f, 1f).magnitude, Is.LessThan(.0001f));
            Assert.That(BoardingPartyPresentation.LungeOffset(.5f, 1f).x, Is.EqualTo(64f).Within(.001f));
            for (int frame = 0; frame <= 100; frame++)
            {
                float t = frame / 100f;
                Vector2 advance = BoardingPartyPresentation.LungeOffset(t, 1f);
                Assert.That(advance.y, Is.Zero, "The boots and their shared contact shadow must remain on the deck.");
                Assert.That(advance.x, Is.InRange(0f, 64.001f));
                Assert.That(BoardingPartyPresentation.LungeOffset(t, -1f), Is.EqualTo(-advance));
            }
            Assert.That(BoardingPartyPresentation.LungeOffset(.001f, 1f).x, Is.LessThan(.001f),
                "An eased departure should not snap the whole formation forward on its first frame.");
        }

        [Test]
        public void BoardingFeedback_UsesPartyMarkerAfterCompactHudRemovesFormationLabel()
        {
            var party = new GameObject("Grounded party", typeof(RectTransform), typeof(BoardingPartyPresentation));
            var ship = new GameObject("Naval ship", typeof(RectTransform));
            var label = new GameObject("Naval ship Formation Label", typeof(RectTransform));
            label.transform.SetParent(ship.transform, false);
            try
            {
                MethodInfo dispatch = typeof(CoreGameplayDemoController).GetMethod("IsBoardingVisualTarget",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(dispatch, Is.Not.Null);
                Assert.That(dispatch.Invoke(null, new object[] { party.GetComponent<RectTransform>() }), Is.True);
                Assert.That(dispatch.Invoke(null, new object[] { ship.GetComponent<RectTransform>() }), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(party);
                Object.DestroyImmediate(ship);
            }
        }
    }
}
