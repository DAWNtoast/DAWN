using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BoardingPartyPresentationTests
    {
        [Test]
        public void LegacyLungeRequestsLeaveBothPartiesCompletelyStill()
        {
            for (int frame = 0; frame <= 100; frame++)
            {
                float t = frame / 100f;
                Assert.That(BoardingPartyPresentation.LungeOffset(t, 1f), Is.EqualTo(Vector2.zero),
                    "Only the effects may advance; the player artwork must stay fixed.");
                Assert.That(BoardingPartyPresentation.LungeOffset(t, -1f), Is.EqualTo(Vector2.zero),
                    "The same static presentation applies to the enemy artwork.");
            }
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
