using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator SettingsPrefab_ReopenReusesObjectsResetsDraftAndRejectsRetiredCallbacks()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            float savedVolume = PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, .8f);
            ActiveButton("Port Hub Settings").onClick.Invoke();
            yield return null;
            var view = controller.GetComponentInChildren<PortSettingsView>();
            Assert.That(view, Is.Not.Null, "The production settings surface uses the authored prefab.");
            int[] originalIds = view.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).ToArray();
            Assert.That(view.Parchment.texture, Is.Not.Null);
            view.Master.value = savedVolume < .5f ? .91f : .09f;
            Button.ButtonClickedEvent retiredApply = view.Apply.onClick;
            Button.ButtonClickedEvent retiredClose = view.Close.onClick;
            view.Close.onClick.Invoke();
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(view.Parchment.texture, Is.Null, "Closed retained controls must release the large paper.");
            Assert.That(PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, .8f), Is.EqualTo(savedVolume));

            // Close/reopen in one frame must not find an instance scheduled for destruction.
            ActiveButton("Port Hub Settings").onClick.Invoke();
            Assert.That(controller.GetComponentInChildren<PortSettingsView>(), Is.SameAs(view));
            Assert.That(view.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).ToArray(), Is.EqualTo(originalIds));
            Assert.That(view.Master.value, Is.EqualTo(Mathf.Clamp01(savedVolume)).Within(.001f));
            Assert.That(view.Parchment.texture, Is.Not.Null);
            retiredApply.Invoke();
            retiredClose.Invoke();
            Assert.That(view.gameObject.activeInHierarchy, Is.True, "Callbacks from a previous opening cannot apply or close a new draft.");
            Assert.That(PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, .8f), Is.EqualTo(savedVolume));
            yield return null;
            Assert.That(view.gameObject.activeInHierarchy, Is.True);
            view.Close.onClick.Invoke();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SettingsPrefab_ReturnConfirmationReusesAndRebindsFocusAndGuards()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            ActiveButton("Port Hub Settings").onClick.Invoke();
            PortSettingsView settings = controller.GetComponentInChildren<PortSettingsView>();
            settings.ReturnToMenu.onClick.Invoke();
            ReturnToMenuConfirmationView confirmation = controller.GetComponentInChildren<ReturnToMenuConfirmationView>();
            Assert.That(confirmation, Is.Not.Null);
            Button.ButtonClickedEvent retiredConfirm = confirmation.Confirm.onClick;
            Button.ButtonClickedEvent retiredCancel = confirmation.Cancel.onClick;
            confirmation.Cancel.onClick.Invoke();
            Assert.That(settings.Sheet.interactable, Is.True);
            settings.Close.onClick.Invoke();
            ActiveButton("Port Hub Settings").onClick.Invoke();
            settings.ReturnToMenu.onClick.Invoke();
            Assert.That(controller.GetComponentInChildren<ReturnToMenuConfirmationView>(), Is.SameAs(confirmation));
            Assert.That(settings.Sheet.interactable, Is.False);
            retiredConfirm.Invoke();
            retiredCancel.Invoke();
            Assert.That(confirmation.gameObject.activeInHierarchy, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CoreGameplayDemo"));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(confirmation.Cancel.gameObject));
            confirmation.Cancel.onClick.Invoke();
            settings.Close.onClick.Invoke();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
