using System.Collections;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    // Reuse the existing fixture's complete save-key preservation and cleanup.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator Dispatch_ExpiredRecallButtonPaysOnceUsingTheCurrentDeadline()
        {
            PrepareReleaseDispatch(0, System.DateTime.UtcNow.AddMinutes(1).Ticks);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            decimal before = controller.Session.ProfileGold;
            ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
            yield return null;
            Button.ButtonClickedEvent oldRecall = ActiveButton("Recall Dispatch Job").onClick;
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchEndsAtKey, (System.DateTime.UtcNow.Ticks - 1).ToString());
            oldRecall.Invoke();
            oldRecall.Invoke();
            yield return null;
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchMissionKey), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(before + GameplayBalance.DispatchGold(0)));
        }

        [UnityTest]
        public IEnumerator Dispatch_FullHoldKeepsTheEntireRewardClaimableAfterSellingCargo()
        {
            PrepareReleaseDispatch(0, System.DateTime.UtcNow.Ticks - 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            decimal before = controller.Session.ProfileGold;
            int wood = controller.Session.Cargo.Wood;
            controller.Session.Cargo.Iron = 45;
            ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
            yield return null;
            ActiveButton("Claim Dispatch Reward").onClick.Invoke();
            yield return null;
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.DispatchMissionKey, -1), Is.Zero);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(before));
            Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(wood));
            Assert.That(GameObject.Find("Active Dispatch Notice").GetComponent<Text>().text, Does.Contain("货舱空间不足"));
            Assert.That(controller.Session.TrySellCargoMaterial("iron", 45), Is.True);
            ActiveButton("Claim Dispatch Reward").onClick.Invoke();
            yield return null;
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchMissionKey), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(before + 45 * 6 + GameplayBalance.DispatchGold(0)));
            Assert.That(controller.Session.Cargo.Wood, Is.EqualTo(wood + GameplayBalance.DispatchWood(0)));
        }

        [UnityTest]
        public IEnumerator Dispatch_InvalidMissionHasAWorkingRecoveryButtonWithoutPayment()
        {
            PrepareReleaseDispatch(99, System.DateTime.UtcNow.Ticks - 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            decimal before = controller.Session.ProfileGold;
            ActiveButton("Port Hotspot Dispatch").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Active Dispatch Badge").GetComponent<Text>().text, Does.Contain("异常"));
            Button recovery = ActiveButton("Recall Dispatch Job");
            Assert.That(recovery.GetComponentInChildren<Text>().text, Does.Contain("清除异常"));
            recovery.onClick.Invoke();
            yield return null;
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchMissionKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.DispatchEndsAtKey), Is.False);
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(before));
            Assert.That(GameObject.Find("Dispatch Table Hint"), Is.Null,
                "The authored dispatch table preview removes the separate bottom hint label.");
            Assert.That(GameObject.Find("Dispatch Table Stage"), Is.Not.Null);
        }

        private static void PrepareReleaseDispatch(int mission, long endsAt)
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchMissionKey, mission);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchDurationHoursKey, 2);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchBalanceVersionKey, GameplayBalance.Version);
            PlayerPrefs.SetInt(CoreGameplayDemoController.DispatchRealClockKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchEndsAtKey, endsAt.ToString());
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchCrewKey, string.Empty);
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchCrewIdentityKey, string.Empty);
        }
    }
}
