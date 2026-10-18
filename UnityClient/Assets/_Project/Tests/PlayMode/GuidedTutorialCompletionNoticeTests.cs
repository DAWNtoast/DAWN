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
        [UnityTest, Timeout(90000)]
        public IEnumerator GuidedTutorial_CompletionNotice_ActualEquipmentShowsOnceAcrossRefreshAndFadesWhilePaused()
        {
            // Resume the supported older save that awarded and auto-fitted the
            // component before the display/equipment lessons were introduced.
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 5);
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialWarehousePendingKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialComponentRewardKey, 1);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.GuidedTutorialEquipmentPendingKey);
            PlayerPrefs.DeleteKey(CoreGameplayDemoController.ComponentInventoryKey);
            PlayerPrefs.SetString(CoreGameplayDemoController.ComponentLoadoutKey(ComponentSlot.Bow),
                CoreGameplayDemoController.GuidedTutorialRewardComponentId);
            PlayerPrefs.SetInt(CoreGameplayDemoController.ComponentConditionKey(ComponentSlot.Bow), 104);
            PlayerPrefs.SetString(CoreGameplayDemoController.WarehouseOwnedKey, CoreWarehouseCollection.TutorialTreasureId);
            PlayerPrefs.Save();

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Warehouse));
            Assert.That(GameObject.Find("Guided Tutorial Completion Notice"), Is.Null,
                "Loading a pending final lesson must not announce completion.");

            yield return DriveGuidedTutorial(controller, "EquipRewardComponent", new HashSet<string>(), false);
            Assert.That(controller.IsTutorialActive, Is.True);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            Assert.That(GameObject.Find("Guided Tutorial Completion Notice"), Is.Null,
                "Displaying the treasure and selecting the component still leave an actual equipment action to teach.");

            Button.ButtonClickedEvent completedEquipClick = RequireGuidedButton("Shipyard Equip Component").onClick;
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                ClickGuidedButton("Shipyard Equip Component");
                yield return null;
                yield return null;
                Assert.That(controller.IsTutorialActive, Is.False);
                Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Complete));
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey), Is.EqualTo(1));

                GameObject notice = GameObject.Find("Guided Tutorial Completion Notice");
                Assert.That(notice, Is.Not.Null, "The real successful final action must announce free sailing.");
                CanvasGroup group = notice.GetComponent<CanvasGroup>();
                Assert.That(group, Is.Not.Null);
                Assert.That(group.alpha, Is.EqualTo(1f).Within(.001f));
                Assert.That(group.blocksRaycasts, Is.False);
                Assert.That(group.interactable, Is.False);
                Assert.That(notice.GetComponentsInChildren<Selectable>(true), Is.Empty);
                Assert.That(notice.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                Text message = notice.GetComponentsInChildren<Text>().Single(text => text.name == "Guided Tutorial Completion Text");
                Assert.That(message.text.Replace("\n", string.Empty), Is.EqualTo("恭喜完成引导，接下来开始自由航行吧"));
                Assert.That(message.fontSize, Is.GreaterThanOrEqualTo(48));
                Assert.That(message.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                NavalBattleVisualCapture.Capture(controller, "GuidedTutorialCompletion_01_Visible.png",
                    "真实最后装配完成后，中央大字提示自由航行，保持清晰且不阻挡操作", "Guided Tutorial Completion Notice");

                // A real navigation action rebuilds the shipyard page immediately
                // after graduation; the lifetime must belong to the overlay.
                ClickGuidedButton("Return To Port Hub");
                yield return null;
                yield return null;
                Assert.That(GameObject.Find("Guided Tutorial Completion Notice"), Is.SameAs(notice),
                    "A normal UI refresh must neither remove nor restart the graduation notice.");

                float deadline = Time.realtimeSinceStartup + 8f;
                while (group != null && group.alpha > .75f && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(group, Is.Not.Null, "The message must have a visible gradual fade before cleanup.");
                Assert.That(group.alpha, Is.GreaterThan(0f).And.LessThanOrEqualTo(.75f),
                    "Unscaled time must advance the fade even when the game is paused.");
                float fadingAlpha = group.alpha;
                NavalBattleVisualCapture.Capture(controller, "GuidedTutorialCompletion_02_Fading.png",
                    "游戏暂停时提示仍按真实时间缓慢淡出，刷新界面不重复播放", "Guided Tutorial Completion Notice");
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(group == null || group.alpha < fadingAlpha, Is.True,
                    "The visible opacity must continue decreasing, rather than disappear after a delay.");

                deadline = Time.realtimeSinceStartup + 5f;
                while (notice != null && notice.activeInHierarchy && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(GameObject.Find("Guided Tutorial Completion Notice"), Is.Null,
                    "The completed fade must retire its transient UI.");
                completedEquipClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Guided Tutorial Completion Notice"), Is.Null,
                    "A stale callback from the final equipment screen must not replay graduation.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(GameObject.Find("Guided Tutorial Completion Notice"), Is.Null,
                "Restoring a graduated save must not replay the one-time completion notice.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
