using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [UnityTest]
        public IEnumerator MainMenu_PrefabsBindControlsAndReuseSaveNotices()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
            MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();
            MainMenuPrefabView surface = controller.GetComponentInChildren<MainMenuPrefabView>();
            Assert.That(surface, Is.Not.Null, "The live menu must instantiate its authored prefab.");
            Assert.That(surface.Kind, Is.EqualTo(MainMenuPrefabKind.Surface));
            Assert.That(controller.GetComponentsInChildren<MainMenuPrefabView>(true).Length, Is.EqualTo(1),
                "Optional prefab instances must remain lazy.");

            float savedMaster = PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, .8f);
            ActiveButton("Settings").onClick.Invoke();
            MainMenuPrefabView settings = controller.GetComponentsInChildren<MainMenuPrefabView>(true)
                .Single(view => view.Kind == MainMenuPrefabKind.Settings);
            Assert.That(settings.Master.value, Is.EqualTo(savedMaster).Within(.001f));
            settings.Master.value = .37f;
            Assert.That(settings.MasterValue.text, Is.EqualTo("37%"));
            settings.Secondary.onClick.Invoke();
            ActiveButton("Settings").onClick.Invoke();
            Assert.That(controller.GetComponentsInChildren<MainMenuPrefabView>(true)
                .Single(view => view.Kind == MainMenuPrefabKind.Settings), Is.SameAs(settings));
            Assert.That(settings.Master.value, Is.EqualTo(.37f).Within(.001f));
            settings.Secondary.onClick.Invoke();
            Assert.That(PlayerPrefs.GetFloat(MainMenuController.MasterVolumeKey, .8f), Is.EqualTo(savedMaster));

            MethodInfo showNotice = typeof(MainMenuController).GetMethod("ShowSaveNotice", BindingFlags.Instance | BindingFlags.NonPublic);
            showNotice.Invoke(controller, new object[] { "首次存档提示" });
            MainMenuPrefabView notice = controller.GetComponentsInChildren<MainMenuPrefabView>(true)
                .Single(view => view.Kind == MainMenuPrefabKind.SaveNotice);
            int objectCount = controller.GetComponentsInChildren<Transform>(true).Length;
            for (int index = 0; index < 5; index++)
            {
                notice.Primary.onClick.Invoke();
                Assert.That(notice.gameObject.activeSelf, Is.False);
                showNotice.Invoke(controller, new object[] { "重新读取 " + index });
                Assert.That(notice.gameObject.activeSelf, Is.True);
                Assert.That(notice.Body.text, Is.EqualTo("重新读取 " + index));
                Assert.That(controller.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount),
                    "Retrying a save must only rebind the existing notice, without creating duplicate controls.");
            }
            notice.Primary.onClick.Invoke();
            yield return null;
            Assert.That(controller.GetComponentsInChildren<MainMenuPrefabView>(true)
                .Count(view => view.Kind == MainMenuPrefabKind.SaveNotice), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
