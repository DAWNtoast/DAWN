using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private static IEnumerator DismissTreasureStoryIfPresent(CoreGameplayDemoController controller)
        {
            Assert.That(GameObject.Find("Treasure Story Overlay"), Is.Null,
                "Entering a treasure voyage must proceed directly to navigation without an introductory comic.");
            yield break;
        }

        private static IEnumerator WaitForEncounterDossierPresentationCompletion()
        {
            GameObject dossier = GameObject.Find("Encounter Card");
            Assert.That(dossier, Is.Not.Null, "The generated encounter must have a real dossier.");
            CanvasGroup reveal = dossier.GetComponent<CanvasGroup>();
            Assert.That(reveal, Is.Not.Null, "The dossier entrance owns its presentation alpha.");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (reveal != null && reveal.alpha < 1f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(reveal != null, Is.True, "The dossier must remain available throughout its entrance.");
            Assert.That(reveal.alpha, Is.EqualTo(1f), "The finite entrance animation must finish before checking pointer reachability.");
            // Allow the nested RectMask2D geometry to catch up to the completed
            // position/scale. The subsequent click still performs a real raycast.
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
    }
}
