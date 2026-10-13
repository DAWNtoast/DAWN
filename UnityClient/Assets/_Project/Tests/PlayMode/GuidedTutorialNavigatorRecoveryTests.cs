using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GuidedTutorial_NavigatorFallbackReturnsForRecoveryWithoutUnlockingBoss()
        {
            yield return LoadGuidedTutorialScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return DriveGuidedTutorial(controller, "SecondPier", new HashSet<string>(), false);
            // Resume the earned recruitment checkpoint with minimal water.
            // After the actual sail, exhaust whole and prepaid fractional water
            // so this recovery fixture cannot afford the paid interpretation.
            PlayerPrefs.SetInt(CoreGameplayDemoController.CargoKey("Water"), 4);
            PlayerPrefs.Save();
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            string departure = controller.Session.CurrentPort;
            yield return DriveGuidedTutorial(controller, "SecondCrewEvent", new HashSet<string>(), false);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            ExhaustGuidedRecoveryWaterFixture(controller);
            yield return null;
            Assert.That(controller.Session.Cargo.Water, Is.Zero);
            Assert.That(controller.Session.IsChoiceAvailable(0), Is.False);
            Assert.That(RequireGuidedButton("Encounter Choice 0").IsInteractable(), Is.False);
            Assert.That(RequireGuidedButton("Encounter Choice 1").IsInteractable(), Is.True);
            AssertGuidedFocusTarget("Encounter Choice 1");
            ClickGuidedButton("Encounter Choice 1");
            yield return null;

            Assert.That(controller.Session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(controller.Session.Settlement.ProtectionRatio, Is.EqualTo(.35f));
            Assert.That(controller.Session.GuidedTutorialCompletedEncounters, Is.Zero);
            Assert.That(Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                .Any(label => label.name == "Guided Tutorial Instruction" && label.text.Contains("暗标尚未解读")), Is.True);
            yield return ReturnToGuidedResourceRecovery(controller, 2, "SecondPier");
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(departure));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.GuidedTutorialNavigatorEventKey, 0), Is.Zero);
            Assert.That(controller.Session.HasNavigator, Is.True);
            Assert.That(controller.WarehouseCollection.Owns(CoreWarehouseCollection.TutorialTreasureId), Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
