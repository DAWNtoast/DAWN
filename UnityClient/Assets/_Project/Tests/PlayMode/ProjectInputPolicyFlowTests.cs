using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator ProjectInputPolicy_AllScenesUseKeyboardMouseTouchActionsWithoutGamepadBindings()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            InputActionAsset actions = InputSystem.actions;
            Assert.That(actions, Is.Not.Null, "The project-wide input asset must be included in the player.");
            foreach (InputActionMap map in actions.actionMaps)
                foreach (InputBinding binding in map.bindings)
                {
                    Assert.That(binding.path, Does.Not.Contain("<Gamepad>").And.Not.Contain("<Joystick>"));
                    Assert.That(binding.path, Is.Not.EqualTo("*/{Submit}").And.Not.EqualTo("*/{Cancel}"),
                        "Generic submit/cancel usages would still accept unsupported devices.");
                }
            Assert.That(actions.controlSchemes.Any(scheme => scheme.name == "Gamepad" || scheme.name == "Joystick"), Is.False);

            InputActionMap ui = actions.FindActionMap("UI", true);
            CollectionAssert.Contains(ui.FindAction("Navigate", true).bindings.Select(binding => binding.path).ToArray(),
                "<Keyboard>/upArrow");
            CollectionAssert.Contains(ui.FindAction("Submit", true).bindings.Select(binding => binding.path).ToArray(),
                "<Keyboard>/{Submit}");
            CollectionAssert.Contains(ui.FindAction("Cancel", true).bindings.Select(binding => binding.path).ToArray(),
                "<Keyboard>/{Cancel}");
            foreach (string path in new[] { "<Mouse>/position", "<Touchscreen>/touch*/position" })
                CollectionAssert.Contains(ui.FindAction("Point", true).bindings.Select(binding => binding.path).ToArray(), path);
            foreach (string path in new[] { "<Mouse>/leftButton", "<Touchscreen>/touch*/press" })
                CollectionAssert.Contains(ui.FindAction("Click", true).bindings.Select(binding => binding.path).ToArray(), path);

            // Runtime AddComponent otherwise installs the Input System package's
            // own default actions, even when a project-wide asset is configured.
            foreach (string scene in new[] { "MainMenu", "CoreGameplayDemo", "TutorialVoyage" })
            {
                SceneManager.LoadScene(scene, LoadSceneMode.Single);
                yield return null; yield return null;
                Assert.That(EventSystem.current, Is.Not.Null, scene);
                InputSystemUIInputModule module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
                Assert.That(module, Is.Not.Null, scene);
                Assert.That(module.actionsAsset, Is.SameAs(actions), scene + " must not use package default bindings.");
                Assert.That(module.move.action, Is.SameAs(ui.FindAction("Navigate", true)), scene);
                Assert.That(module.submit.action, Is.SameAs(ui.FindAction("Submit", true)), scene);
                Assert.That(module.cancel.action, Is.SameAs(ui.FindAction("Cancel", true)), scene);
                Assert.That(module.point.action, Is.SameAs(ui.FindAction("Point", true)), scene);
                Assert.That(module.leftClick.action, Is.SameAs(ui.FindAction("Click", true)), scene);
                Assert.That(module.move.action.enabled && module.submit.action.enabled && module.cancel.action.enabled &&
                    module.point.action.enabled && module.leftClick.action.enabled, Is.True, scene);
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
