using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class TutorialVoyageSceneTests
    {
        [UnityTest]
        public IEnumerator Scene_BuildsUi_AndOpensFirstEncounter()
        {
            SceneManager.LoadScene("TutorialVoyage", LoadSceneMode.Single);
            yield return null;
            yield return null;

            TutorialVoyageController controller = Object.FindFirstObjectByType<TutorialVoyageController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(GameObject.Find("Tutorial Canvas"), Is.Not.Null);

            Button primary = Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(button => button.name == "Primary Action");
            Assert.That(primary, Is.Not.Null);
            Assert.That(primary.interactable, Is.True);

            primary.onClick.Invoke();
            yield return null;
            primary.onClick.Invoke();
            yield return null;

            GameObject encounterOverlay = GameObject.Find("Encounter Overlay");
            Assert.That(encounterOverlay, Is.Not.Null);
            Assert.That(encounterOverlay.activeInHierarchy, Is.True);
            Assert.That(Object.FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None).Length, Is.GreaterThanOrEqualTo(2));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
